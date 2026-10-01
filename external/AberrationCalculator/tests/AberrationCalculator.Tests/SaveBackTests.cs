using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Writing an optimised design back to the file it came from.
///
/// <para>The rule is that only what the optimiser moved may change - curvatures, thicknesses,
/// glass names, conics and aspheric terms - and that everything else in the file survives, including the parts this program
/// has no model for. That is not a nicety: this program recognises twenty-three .zmx directives
/// and a real .zmx has many times that, so a writer that regenerated the file from what it
/// understood would quietly delete the rest of somebody's design.</para>
/// </summary>
public class SaveBackTests
{
    private sealed class Scratch : IDisposable
    {
        public string Dir { get; }
        public Scratch()
        {
            Dir = Path.Combine(Path.GetTempPath(), "abcalc-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
        }
        public string Copy(string source, string name)
        {
            string path = Path.Combine(Dir, name);
            File.Copy(source, path);
            return path;
        }
        public string At(string name) => Path.Combine(Dir, name);
        public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "lenses", name);

    private static string ZmxDir =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "coefficient-reference");

    private static string ZmxFixture() =>
        Directory.GetFiles(ZmxDir, "*.zmx").OrderBy(f => f).First();

    /// <summary>Every .zmx fixture, by bare name, so a new one is covered the day it lands.</summary>
    public static IEnumerable<object[]> EveryZmxFixture() =>
        Directory.GetFiles(ZmxDir, "*.zmx").OrderBy(f => f)
                 .Select(f => new object[] { Path.GetFileName(f) });

    /// <summary>
    /// Patching ONE value in a .zmx leaves every other surface describing the same lens - the
    /// same curvatures, thicknesses, conics and, above all, the same REFRACTIVE INDICES.
    ///
    /// <para><b>A model glass was being deleted.</b> The reader leaves <c>Material</c> blank for
    /// one deliberately, because its index comes from (Nd, Vd) rather than a catalogue, and the
    /// patcher read that blank as "the glass is gone" and removed the GLAS line. The element
    /// became air. Nothing complained: the file still parsed, still had the right number of
    /// surfaces, and still had the curvature that had just been optimised.</para>
    ///
    /// <para>It survived because every .zmx fixture here named a catalogue glass, so no test had
    /// a model glass to lose - the same shape of blind spot as the r-squared term, and found the
    /// same way, by adding a design that sits in it. Running over EVERY fixture rather than one
    /// is the guard against the next one.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryZmxFixture))]
    public void PatchingAZmxLeavesEveryOtherSurfaceDescribingTheSameLens(string name)
    {
        using var s = new Scratch();
        string lens = s.Copy(Path.Combine(ZmxDir, name), "L.zmx");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        int primary = system.PrimaryWavelengthIndex < 0 ? 0 : system.PrimaryWavelengthIndex;
        double lambda = system.Wavelengths[primary].Value;
        var before = IndexResolver.Build(system, catalog, lambda,
                                         new System.Collections.Generic.List<string>());

        double moved = system.Surfaces[1].Curvature * 1.05 + 1e-4;
        system.Surfaces[1].Curvature = moved;

        string output = s.At("L.optimised.zmx");
        LensPatcher.Save(system, lens, output);

        var back = LensFile.Read(output, catalog);
        var after = IndexResolver.Build(back, catalog, lambda,
                                        new System.Collections.Generic.List<string>());

        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);
        Assert.Equal(moved, back.Surfaces[1].Curvature, 12);

        for (int i = 0; i < system.Surfaces.Count; i++)
        {
            Assert.True(Math.Abs(before[i] - after[i]) < 1e-12,
                $"{name}: the index after surface {i} went from {before[i]:F9} to {after[i]:F9} "
              + "when one curvature was patched. A surface has lost or changed its glass, which "
              + "makes the saved file a different lens from the one that was optimised.");

            if (i == 1) continue;
            Assert.Equal(system.Surfaces[i].Curvature, back.Surfaces[i].Curvature, 12);
            Assert.Equal(system.Surfaces[i].Conic, back.Surfaces[i].Conic, 12);
            if (!double.IsInfinity(system.Surfaces[i].Thickness))
                Assert.Equal(system.Surfaces[i].Thickness, back.Surfaces[i].Thickness, 9);
            else
                Assert.True(double.IsInfinity(back.Surfaces[i].Thickness),
                    $"{name}: surface {i} was at infinity and came back at "
                  + $"{back.Surfaces[i].Thickness}");
        }
    }

    // ── .lhlt ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .lhlt states what may be optimised, and the tool reads it rather than asking again.
    /// </summary>
    [Fact]
    public void VariablesAreReadFromTheLhltItself()
    {
        using var s = new Scratch();
        string lens = s.Copy(Fixtures.Lens("CookeTriplet"), "T.lhlt");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].CurvatureVariable = true;
        system.Surfaces[2].ThicknessVariable = true;
        system.Surfaces[2].ThicknessMin = 1.5;
        system.Surfaces[2].ThicknessMax = 9.0;

        var vars = SurfaceVariables.Read(system);

        Assert.Equal(2, vars.Count);
        Assert.Equal(VariableKind.Curvature, vars[0].Kind);
        Assert.Equal(1, vars[0].Surface);
        Assert.False(vars[0].IsBounded);

        Assert.Equal(VariableKind.Thickness, vars[1].Kind);
        Assert.Equal(1.5, vars[1].Min);
        Assert.Equal(9.0, vars[1].Max);
    }

    /// <summary>
    /// Stamping variables back clears the ones that are gone. A design must not accumulate
    /// variables nobody asked for just because nothing replaced them.
    /// </summary>
    [Fact]
    public void WritingVariablesBackClearsTheOnesThatWereRemoved()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        system.Surfaces[3].CurvatureVariable = true;
        system.Surfaces[5].ThicknessVariable = true;

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        SurfaceVariables.Write(vars, system);

        Assert.True(system.Surfaces[1].CurvatureVariable);
        Assert.False(system.Surfaces[3].CurvatureVariable);
        Assert.False(system.Surfaces[5].ThicknessVariable);
    }

    /// <summary>
    /// The thing this whole approach exists for: a setting this program does not model survives
    /// being written through it. The file's own merit function is the case in point - this tool
    /// deliberately does not read it, and therefore has no business destroying it.
    /// </summary>
    [Fact]
    public void PatchingALhltPreservesSettingsThisProgramDoesNotModel()
    {
        using var s = new Scratch();
        string lens = s.At("T.lhlt");

        string original = File.ReadAllText(Fixtures.Lens("CookeTriplet"));
        original = original.Replace("\n  \"Pickups\"",
            "\n  \"MeritFunction\": { \"Operands\": [ { \"Type\": \"SPOTM\", \"Weight\": 42 } ] },"
          + "\n  \"SomeFutureSetting\": \"keep me\",\n  \"Pickups\"");
        File.WriteAllText(lens, original);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Curvature *= 1.05;

        string output = s.At("T.optimised.lhlt");
        LhltPatcher.Patch(system, lens, output);

        string written = File.ReadAllText(output);
        Assert.Contains("MeritFunction", written);
        Assert.Contains("SPOTM", written);
        Assert.Contains("42", written);
        Assert.Contains("keep me", written);

        // And the change did happen.
        var back = LensFile.Read(output, catalog);
        Assert.Equal(system.Surfaces[1].Curvature, back.Surfaces[1].Curvature, 12);
    }

    /// <summary>
    /// Only what changed is rewritten. A surface the optimiser never touched keeps the exact
    /// text it had, down to how the file chose to spell it.
    /// </summary>
    [Fact]
    public void PatchingALhltLeavesUntouchedSurfacesAlone()
    {
        using var s = new Scratch();
        string lens = s.Copy(Fixtures.Lens("CookeTriplet"), "T.lhlt");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        double before = system.Surfaces[3].Curvature;
        system.Surfaces[1].Curvature *= 1.05;

        string output = s.At("T2.lhlt");
        LhltPatcher.Patch(system, lens, output);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(before, back.Surfaces[3].Curvature, 15);

        // The object distance is written as the string "Infinity" and must stay infinite.
        Assert.True(double.IsInfinity(back.Surfaces[0].Thickness));
    }

    [Fact]
    public void PatchingALhltWritesTheVariableFlagsAndBounds()
    {
        using var s = new Scratch();
        string lens = s.Copy(Fixtures.Lens("CookeTriplet"), "T.lhlt");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[2].ThicknessVariable = true;
        system.Surfaces[2].ThicknessMin = 1.5;
        system.Surfaces[2].ThicknessMax = 9.0;
        system.Surfaces[4].CurvatureVariable = true;

        string output = s.At("T3.lhlt");
        LhltPatcher.Patch(system, lens, output);

        var back = LensFile.Read(output, catalog);
        Assert.True(back.Surfaces[2].ThicknessVariable);
        Assert.Equal(1.5, back.Surfaces[2].ThicknessMin);
        Assert.Equal(9.0, back.Surfaces[2].ThicknessMax);
        Assert.True(back.Surfaces[4].CurvatureVariable);
        Assert.False(back.Surfaces[1].CurvatureVariable);

        // An unbounded variable leaves no limit behind: absent is how the format says unbounded.
        Assert.True(double.IsNegativeInfinity(back.Surfaces[4].CurvatureMin));
    }

    // ── .zmx ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .zmx is UTF-16 with CRLF, and a patched one has to still be. Reading it as UTF-8 and
    /// writing it back as UTF-8 produces a file the format's own readers will not open.
    /// </summary>
    [Fact]
    public void PatchingAZmxKeepsItsEncodingAndChangesOnlyWhatMoved()
    {
        using var s = new Scratch();
        string lens = s.Copy(ZmxFixture(), "L.zmx");
        string originalText = ReadUtf16(lens);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        double newCurvature = system.Surfaces[1].Curvature * 1.1;
        system.Surfaces[1].Curvature = newCurvature;

        string output = s.At("L.optimised.zmx");
        LensPatcher.Save(system, lens, output);

        // Same encoding: still readable as UTF-16, and the reader still accepts it.
        var back = LensFile.Read(output, catalog);
        Assert.Equal(newCurvature, back.Surfaces[1].Curvature, 12);
        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);

        // Only the one CURV line differs from the original.
        var a = ReadUtf16(lens).Split('\n');
        var b = ReadUtf16(output).Split('\n');
        Assert.Equal(a.Length, b.Length);

        int changed = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) changed++;
        Assert.True(changed == 1, $"{changed} lines differ; exactly one should");

        // And nothing the reader ignores was lost.
        foreach (string keyword in new[] { "VERS", "MODE", "UNIT", "GCAT", "ENPD" })
            Assert.Contains(keyword, ReadUtf16(output), StringComparison.Ordinal);
        Assert.True(originalText.Length > 0);
    }

    private static string ReadUtf16(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// A design opened from a file written in INCHES goes back in inches. Returning millimetres
    /// would be wrong by a factor of twenty-five and would look entirely reasonable.
    /// </summary>
    [Fact]
    public void DistancesGoBackInTheFilesOwnUnits()
    {
        using var s = new Scratch();
        string lens = s.Copy(ZmxFixture(), "I.zmx");

        // Say the file is in inches. The reader will scale everything to mm on the way in.
        string text = ReadUtf16(lens).Replace("UNIT MM", "UNIT IN");
        File.WriteAllText(lens, text, new UnicodeEncoding(false, true));

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        Assert.Equal(25.4, system.FileUnitScale, 6);

        double thicknessMm = system.Surfaces[1].Thickness;
        string output = s.At("I.optimised.zmx");
        LensPatcher.Save(system, lens, output);

        // Unchanged values are not rewritten at all, so the proof is that reading it back gives
        // the same millimetres - which can only happen if the file still says inches.
        var back = LensFile.Read(output, catalog);
        Assert.Equal(thicknessMm, back.Surfaces[1].Thickness, 9);

        // Now move something and check it survives the round trip through inches.
        system.Surfaces[1].Thickness = thicknessMm * 1.25;
        string moved = s.At("I.moved.zmx");
        LensPatcher.Save(system, lens, moved);
        var backMoved = LensFile.Read(moved, catalog);
        Assert.Equal(thicknessMm * 1.25, backMoved.Surfaces[1].Thickness, 9);
    }

    // ── Optiland ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Optiland records absolute positions rather than thicknesses, so moving one surface moves
    /// every surface after it. Getting that wrong shortens or stretches the whole lens.
    /// </summary>
    [Fact]
    public void PatchingOptilandReaccumulatesThePositions()
    {
        using var s = new Scratch();
        string lens = s.Copy(Fixture("CookeTriplet.optiland.json"), "T.json");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        double t2 = system.Surfaces[2].Thickness;
        double t4 = system.Surfaces[4].Thickness;
        system.Surfaces[2].Thickness = t2 * 1.4;

        string output = s.At("T.optimised.json");
        LensPatcher.Save(system, lens, output);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(t2 * 1.4, back.Surfaces[2].Thickness, 9);
        // Everything downstream keeps its own thickness: the surfaces moved, the gaps did not.
        Assert.Equal(t4, back.Surfaces[4].Thickness, 9);
    }

    // ── The sidecar ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheSidecarsSitBesideTheLensAndAreNamedForIt()
    {
        Assert.Equal("a/b/t.zmx.mf", Sidecar.MeritPathFor("a/b/t.zmx").Replace('\\', '/'));
        Assert.Equal("a/b/t.zmx.var", Sidecar.VariablePathFor("a/b/t.zmx").Replace('\\', '/'));

        // Two formats of the same design keep their settings apart.
        Assert.NotEqual(Sidecar.MeritPathFor("t.zmx"), Sidecar.MeritPathFor("t.seq"));

        Assert.True(Sidecar.KeepsVariablesInTheLensFile("x.lhlt"));
        Assert.False(Sidecar.KeepsVariablesInTheLensFile("x.zmx"));
    }

    /// <summary>
    /// The whole of the difference between the formats: a .lhlt keeps its own variables, so it
    /// gets no .var file. Writing them in both places would give a design two statements of what
    /// may move and nothing to say which wins.
    /// </summary>
    [Fact]
    public void OnlyFormatsThatCannotKeepVariablesGetAVarFile()
    {
        using var s = new Scratch();

        var setup = new OptimizationSetup();
        setup.Variables.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        setup.Pickups.Add(new Pickup
        {
            TargetSurfaceIndex = 3,
            SourceSurfaceIndex = 2,
            Parameter = Core.Enums.PickupParameter.Curvature,
        });
        setup.Operands.Add(new Operand { Type = OperandType.EFL, Target = 50.0 });

        string lhlt = s.At("d.lhlt");
        var forLhlt = Sidecar.Save(lhlt, setup);
        Assert.Single(forLhlt);
        Assert.EndsWith(".mf", forLhlt[0]);
        Assert.False(File.Exists(Sidecar.VariablePathFor(lhlt)));

        string zmx = s.At("d.zmx");
        var forZmx = Sidecar.Save(zmx, setup);
        Assert.Equal(2, forZmx.Count);
        Assert.True(File.Exists(Sidecar.VariablePathFor(zmx)));

        string vars = File.ReadAllText(Sidecar.VariablePathFor(zmx));
        Assert.Contains("VAR CV 1", vars);
        Assert.Contains("PICKUP CV 3 INDEX 2", vars);

        // And the merit function never carries variables, whatever the format.
        Assert.DoesNotContain("VAR ", File.ReadAllText(Sidecar.MeritPathFor(zmx)));
    }

    /// <summary>
    /// A .zmx is loaded from its two sidecars; a .lhlt from the lens itself.
    /// </summary>
    [Fact]
    public void TheSetupIsGatheredFromWhereverThatFormatKeepsIt()
    {
        using var s = new Scratch();
        var catalog = CatalogLocator.LoadBundled();

        string zmx = s.Copy(ZmxFixture(), "L.zmx");
        File.WriteAllText(Sidecar.VariablePathFor(zmx), "VAR CV 1 MIN 0.01 MAX 0.03\n");
        File.WriteAllText(Sidecar.MeritPathFor(zmx), "EFL, 1, TAR 50\n");

        var setup = Sidecar.Load(LensFile.Read(zmx, catalog), zmx);
        var v = Assert.Single(setup.Variables.Items);
        Assert.Equal(VariableKind.Curvature, v.Kind);
        Assert.Equal(0.01, v.Min);
        Assert.Single(setup.Operands);

        // The .lhlt takes its variables from the lens, and has no .var file to read.
        string lens = s.Copy(Fixtures.Lens("CookeTriplet"), "T.lhlt");
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[2].ThicknessVariable = true;
        File.WriteAllText(Sidecar.MeritPathFor(lens), "PRMSA, 1, TAR 0\n");

        var lhltSetup = Sidecar.Load(system, lens);
        var lv = Assert.Single(lhltSetup.Variables.Items);
        Assert.Equal(VariableKind.Thickness, lv.Kind);
        Assert.Equal(2, lv.Surface);
        Assert.Contains("lens file", lhltSetup.VariableSource);
    }

    /// <summary>
    /// A format this program cannot yet edit in place says so, rather than writing something
    /// that looks like the user's design and is not.
    /// </summary>
    [Fact]
    public void AFormatThatCannotYetBeWrittenSaysSoPlainly()
    {
        using var s = new Scratch();
        string lens = s.At("x.roc");
        File.WriteAllText(lens, "RDY THI\n");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

        var ex = Assert.Throws<NotSupportedException>(
            () => LensPatcher.Save(system, lens, s.At("y.roc")));
        Assert.Contains(".roc", ex.Message);
        Assert.Contains("sidecar", ex.Message);
        Assert.False(LensPatcher.CanSave(lens));
    }

    // ── The three text formats ───────────────────────────────────────────────────────────
    //
    // All five exports of the same double Gauss are in the fixtures, so these are held to the
    // standard the .zmx patcher is held to: the design comes back, and the file does not change
    // anywhere it did not have to.

    private static string Export(string extension) =>
        Path.Combine(Fixtures.LensDir, "KingslakeDG" + extension);

    /// <summary>
    /// Every line of a text file, for counting how many of them a patch touched.
    /// </summary>
    private static int LinesThatDiffer(string before, string after)
    {
        var a = File.ReadAllLines(before);
        var b = File.ReadAllLines(after);
        Assert.Equal(a.Length, b.Length);

        int changed = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) changed++;
        return changed;
    }

    /// <summary>
    /// A .seq surface line is POSITIONAL - <c>S radius thickness material</c> - so editing the
    /// radius means taking the line apart. The thickness and the glass beside it have to come
    /// through unharmed, and no other line may move at all.
    /// </summary>
    [Fact]
    public void PatchingASeqRewritesOneFieldAndLeavesItsNeighbours()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".seq"), "K.seq");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        double radius = system.Surfaces[1].Radius;
        double thickness = system.Surfaces[1].Thickness;
        string? material = system.Surfaces[1].Material;
        system.Surfaces[1].Radius = radius * 1.1;

        string output = s.At("K.optimised.seq");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(radius * 1.1, back.Surfaces[1].Radius, 9);
        Assert.Equal(thickness, back.Surfaces[1].Thickness, 12);
        Assert.Equal(material, back.Surfaces[1].Material);
        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);

        Assert.Equal(1, LinesThatDiffer(lens, output));

        // And the directives the patcher has no business touching are still there.
        string text = File.ReadAllText(output);
        foreach (string keyword in new[] { "RDM", "DIM M", "EPD", "WL", "WTW", "REF", "YAN", "GO" })
            Assert.Contains(keyword, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A flat surface is written as radius ZERO in a .seq - the format has no infinity for a
    /// radius. Returning this program's infinity would produce a file neither program can read.
    /// </summary>
    [Fact]
    public void ASeqTakesAFlattenedSurfaceBackAsAZero()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".seq"), "F.seq");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Curvature = 0.0;

        string output = s.At("F.optimised.seq");
        LensPatcher.Save(system, lens, output, catalog);

        Assert.Contains("S 0 ", File.ReadAllText(output), StringComparison.Ordinal);
        var back = LensFile.Read(output, catalog);
        Assert.True(double.IsInfinity(back.Surfaces[1].Radius));
    }

    /// <summary>
    /// A glass substitution has to survive the .seq spelling, which drops the punctuation out of
    /// a name and may qualify it with its catalog. N-BK7 goes out as NBK7_SCHOTT and has to come
    /// back as N-BK7 - not as NBK7, and not as somebody else's glass of the same bare name.
    /// </summary>
    [Fact]
    public void ASeqCarriesASubstitutedGlassThroughItsOwnSpelling()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".seq"), "G.seq");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Material = "N-BK7";

        string output = s.At("G.optimised.seq");
        LensPatcher.Save(system, lens, output, catalog);

        Assert.DoesNotContain("N-BK7", File.ReadAllText(output), StringComparison.Ordinal);
        var back = LensFile.Read(output, catalog);
        Assert.Equal("N-BK7", back.Surfaces[1].Material);
        Assert.Equal(1, LinesThatDiffer(lens, output));
    }

    // A .seq in the shape Code V saves one: several commands to a line, Code V's 0.1E+14 infinity,
    // a fictitious glass, and a private glass from its PRV catalog.
    private const string CodeVStyle = @"RDM;LEN       ""VERSION: 10.4""
TITLE 'DOUBLET'
EPD   20.0
DIM   M
WL    656.3 587.6 486.1
REF   2
YAN   0.0 5.0
SO    0.0 0.1E+14
S     50.0 5.0 516800.641700 ; CIR 12.5
  STO
S     -50.0 2.0 'LHHP'
  ASP
  K  -1.0
  A  1.0E-05 ; B -2.0E-08
S     -200.0 95.0
SI    0.0 0.0
PRV
PWL 656.3 587.6 486.1
'LHHP' 1.61 1.62 1.63
END
GO
";

    /// <summary>
    /// What Code V writes has to come through an in-place save as it was: a model glass - a
    /// fictitious-glass code or a private glass - is not a name, and was rewritten as AIR; the
    /// CIR after a semicolon, and Code V's own spelling of infinity, are left alone; and only the
    /// line that moved is touched.
    /// </summary>
    [Fact]
    public void ACodeVSeqKeepsWhatTheDesignDidNotChange()
    {
        using var s = new Scratch();
        string lens = s.At("CV.seq");
        File.WriteAllText(lens, CodeVStyle);
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        Assert.True(system.Surfaces[1].ModelIndexEnabled);
        Assert.Equal(1.0e-5, (double)system.Surfaces[2].AsphericCoefficients[1], 1e-20);   // A under ASP

        system.Surfaces[3].Radius = -210.0;
        string output = s.At("CV.optimised.seq");
        LensPatcher.Save(system, lens, output, catalog);

        string text = File.ReadAllText(output);
        Assert.Contains("S     50.0 5.0 516800.641700 ; CIR 12.5", text, StringComparison.Ordinal);
        Assert.Contains("S     -50.0 2.0 'LHHP'", text, StringComparison.Ordinal);
        Assert.Contains("SO    0.0 0.1E+14", text, StringComparison.Ordinal);
        Assert.Equal(1, LinesThatDiffer(lens, output));

        // A model glass the design changed goes out as Code V's code.
        system.Surfaces[1].ModelNd = 1.62;
        LensPatcher.Save(system, lens, output, catalog);
        Assert.Contains("620000.641700", File.ReadAllText(output), StringComparison.Ordinal);
        Assert.Equal(1.62, LensFile.Read(output, catalog).Surfaces[1].ModelNd, 12);
    }

    /// <summary>In curvature mode (RDM N) the first field of a surface line is a curvature.</summary>
    [Fact]
    public void ACodeVSeqInCurvatureModeGetsACurvature()
    {
        using var s = new Scratch();
        string lens = s.At("C.seq");
        File.WriteAllText(lens, "RDM N\nLEN\nEPD 10\nWL 587.6\nSO 0 1e10\nS 0.02 5 516800.641700\nS 0 95\nSI 0 0\nGO\n");
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        Assert.Equal(50.0, system.Surfaces[1].Radius, 9);

        system.Surfaces[1].Radius = 40.0;
        string output = s.At("C.optimised.seq");
        LensPatcher.Save(system, lens, output, catalog);
        Assert.Contains("S 0.025 5 516800.641700", File.ReadAllText(output), StringComparison.Ordinal);
        Assert.Equal(40.0, LensFile.Read(output, catalog).Surfaces[1].Radius, 9);
    }

    /// <summary>
    /// A .len is a stream of commands, and a property with nothing to say is simply not written -
    /// so a plane has no RD line. Bending one means ADDING a line, in the block it belongs to.
    /// </summary>
    [Fact]
    public void PatchingALenAddsTheLineAFlatSurfaceNeverHad()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".len"), "K.len");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        // Surface 5 is the stop, and it is flat: the file gives it AST, AP and TH and no RD.
        Assert.True(double.IsInfinity(system.Surfaces[5].Radius));
        double t5 = system.Surfaces[5].Thickness;
        system.Surfaces[5].Radius = 250.0;

        string output = s.At("K.optimised.len");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(250.0, back.Surfaces[5].Radius, 9);
        Assert.Equal(t5, back.Surfaces[5].Thickness, 12);
        Assert.True(back.Surfaces[5].IsStop);
        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);

        // Exactly one line longer, and everything else where it was.
        Assert.Equal(File.ReadAllLines(lens).Length + 1, File.ReadAllLines(output).Length);
        Assert.Contains("END 10", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    /// The ordinary case: a value that already has a line of its own is edited in place, and
    /// nothing else in the file moves.
    /// </summary>
    [Fact]
    public void PatchingALenChangesOnlyTheValueThatMoved()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".len"), "T.len");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        double t = system.Surfaces[2].Thickness;
        system.Surfaces[2].Thickness = t * 1.3;

        string output = s.At("T.optimised.len");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(t * 1.3, back.Surfaces[2].Thickness, 9);
        Assert.Equal(1, LinesThatDiffer(lens, output));
    }

    /// <summary>
    /// A glass substitution into an air space needs a GLA line that was never there, and one out
    /// of a glass needs the line gone: a GLA naming nothing is not a well-formed file.
    /// </summary>
    [Fact]
    public void ALenGainsAndLosesItsGlassLines()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".len"), "G.len");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        Assert.Equal("SK4", system.Surfaces[1].Material);
        Assert.True(string.IsNullOrEmpty(system.Surfaces[2].Material));

        system.Surfaces[1].Material = null;          // glass out
        system.Surfaces[2].Material = "N-BK7";       // glass in

        string output = s.At("G.optimised.len");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.True(string.IsNullOrEmpty(back.Surfaces[1].Material));
        Assert.Equal("N-BK7", back.Surfaces[2].Material);

        // One line left, one line arrived, so the file is the same length.
        Assert.Equal(File.ReadAllLines(lens).Length, File.ReadAllLines(output).Length);
    }

    /// <summary>
    /// An .otx gives the shape as a CURVATURE, which is what this program optimises - so no
    /// zero-means-infinity translation is needed, and a plane is an honest zero.
    /// </summary>
    [Fact]
    public void PatchingAnOtxEditsTheCurvatureItself()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".otx"), "K.otx");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        double c = system.Surfaces[1].Curvature;
        string? material = system.Surfaces[1].Material;
        system.Surfaces[1].Curvature = c * 0.9;

        string output = s.At("K.optimised.otx");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(c * 0.9, back.Surfaces[1].Curvature, 12);
        Assert.Equal(material, back.Surfaces[1].Material);
        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);

        Assert.Equal(1, LinesThatDiffer(lens, output));

        // The VAR lines say what ANOTHER program was allowed to vary. They are not ours to
        // rewrite, and neither is the APE that records the aperture it computed.
        string text = File.ReadAllText(output);
        Assert.Contains("VAR 2 CUY THI", text, StringComparison.Ordinal);
        Assert.Contains("APE ", text, StringComparison.Ordinal);
        Assert.Contains("VERS", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The image surface is written as thickness -999 - a flag, not a distance, which the reader
    /// turns into a zero. Writing that zero back would throw away what the file was saying.
    /// </summary>
    [Fact]
    public void AnOtxKeepsItsImageSurfaceFlag()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".otx"), "I.otx");
        if (!File.ReadAllText(lens).Contains("-999", StringComparison.Ordinal)) return;

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Curvature *= 1.05;

        string output = s.At("I.optimised.otx");
        LensPatcher.Save(system, lens, output, catalog);

        Assert.Contains("-999", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    /// A glass substitution in an .otx, both ways round.
    /// </summary>
    [Fact]
    public void AnOtxGainsAndLosesItsGlassLines()
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(".otx"), "G.otx");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        Assert.Equal("SK4", system.Surfaces[1].Material);
        Assert.True(string.IsNullOrEmpty(system.Surfaces[2].Material));

        system.Surfaces[1].Material = null;
        system.Surfaces[2].Material = "N-BK7";

        string output = s.At("G.optimised.otx");
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.True(string.IsNullOrEmpty(back.Surfaces[1].Material));
        Assert.Equal("N-BK7", back.Surfaces[2].Material);
        Assert.Equal(File.ReadAllLines(lens).Length, File.ReadAllLines(output).Length);
    }

    /// <summary>
    /// A file that had no byte order mark still has none.
    ///
    /// <para>Three bytes at the front of the first line, in every diff, on a file the user never
    /// asked to be re-encoded - and some readers of these formats show them as a stray character
    /// on the first keyword rather than swallowing them. The .zmx escapes this because it is
    /// UTF-16 and has a mark already; the three text formats do not.</para>
    /// </summary>
    [Theory]
    [InlineData(".seq")]
    [InlineData(".len")]
    [InlineData(".otx")]
    public void PatchingDoesNotAddAByteOrderMark(string extension)
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(extension), "B" + extension);
        Assert.NotEqual(0xEF, File.ReadAllBytes(lens)[0]);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Curvature *= 1.03;

        string output = s.At("B.optimised" + extension);
        LensPatcher.Save(system, lens, output, catalog);

        var bytes = File.ReadAllBytes(output);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(File.ReadAllBytes(lens)[0], bytes[0]);
    }

    /// <summary>
    /// The same design in five formats, optimised the same way, comes back the same design.
    ///
    /// <para>Per-format patchers are six chances to be subtly wrong in one of them - a curvature
    /// written where a radius was wanted, a thickness that scaled the wrong way. Reading each
    /// patched file back and comparing the geometry is the check that they all agree.</para>
    /// </summary>
    [Theory]
    [InlineData(".seq")]
    [InlineData(".len")]
    [InlineData(".otx")]
    [InlineData(".zmx")]
    [InlineData(".json")]
    [InlineData(".lhlt")]
    public void EveryFormatCarriesTheSameMoveBackUnchanged(string extension)
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(extension), "R" + extension);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        // Move a curvature, a thickness and a glass, which is everything a patcher can write.
        system.Surfaces[1].Curvature *= 1.07;
        system.Surfaces[3].Thickness *= 1.21;
        system.Surfaces[8].Material = "N-BK7";

        double c1 = system.Surfaces[1].Curvature;
        double t3 = system.Surfaces[3].Thickness;

        string output = s.At("R.optimised" + extension);
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        Assert.Equal(system.Surfaces.Count, back.Surfaces.Count);
        Assert.Equal(c1, back.Surfaces[1].Curvature, 12);
        Assert.Equal(t3, back.Surfaces[3].Thickness, 9);
        Assert.Equal("N-BK7", back.Surfaces[8].Material);

        // And nothing the optimiser did not touch moved either.
        for (int i = 0; i < system.Surfaces.Count; i++)
        {
            Assert.Equal(system.Surfaces[i].Curvature, back.Surfaces[i].Curvature, 12);
            if (double.IsInfinity(system.Surfaces[i].Thickness))
                Assert.True(double.IsInfinity(back.Surfaces[i].Thickness));
            else
                Assert.Equal(system.Surfaces[i].Thickness, back.Surfaces[i].Thickness, 9);
        }
    }

    // ── Figuring ─────────────────────────────────────────────────────────────────────────
    //
    // Conics and aspheric terms are variables, so they are things the optimiser moves. Until
    // September 2026 no patcher wrote them: an optimised conic singlet (F1, CC -0.6 -> -1.46,
    // merit down 48%) saved a file that still said CONI -0.6 and read back as the design that
    // went in, and nothing said so.

    private static void SameFiguring(OpticalSystem want, OpticalSystem got)
    {
        Assert.Equal(want.Surfaces.Count, got.Surfaces.Count);
        for (int i = 0; i < want.Surfaces.Count; i++)
        {
            Close(want.Surfaces[i].Conic, got.Surfaces[i].Conic, $"conic on surface {i}");
            var a = want.Surfaces[i].AsphericCoefficients;
            var b = got.Surfaces[i].AsphericCoefficients;
            for (int k = 0; k < Math.Min(a.Length, b.Length); k++)
                Close(a[k], b[k], $"A{2 * k + 2} on surface {i}");
        }
    }

    private static void Close(double want, double got, string what) =>
        Assert.True(Math.Abs(want - got) <= 1e-12 * Math.Max(Math.Abs(want), Math.Abs(got)),
                    $"{what}: wrote {want:G17}, read back {got:G17}");

    /// <summary>
    /// A conic and aspheric terms put on SPHERES go back, in every format that can carry them.
    /// Every surface of the double Gauss is spherical, so this is the hard case: the .zmx
    /// surface has to become an EVENASPH with PARM lines it never had, and the Optiland one an
    /// EvenAsphere with a coefficient list.
    /// </summary>
    [Theory]
    [InlineData(".zmx")]
    [InlineData(".json")]
    [InlineData(".lhlt")]
    public void FiguringGoesBackIntoEveryFormatThatCanCarryIt(string extension)
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(extension), "F" + extension);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        system.Surfaces[1].Curvature *= 1.03;
        system.Surfaces[2].Conic = -0.37;
        system.Surfaces[5].AsphericCoefficients[1] = 3.1e-6;
        system.Surfaces[5].AsphericCoefficients[2] = -4.2e-9;
        system.Surfaces[5].AsphericCoefficients[7] = 1.5e-25;

        string output = s.At("F.optimised" + extension);
        LensPatcher.Save(system, lens, output, catalog);

        var back = LensFile.Read(output, catalog);
        SameFiguring(system, back);
        Assert.Equal(system.Surfaces[1].Curvature, back.Surfaces[1].Curvature, 12);
    }

    /// <summary>
    /// A surface that is already an even asphere has its terms EDITED in place, and a file
    /// whose figuring did not move is not touched at all.
    /// </summary>
    [Fact]
    public void AZmxEvenAsphereHasItsTermsEditedInPlace()
    {
        using var s = new Scratch();
        string lens = s.Copy(Path.Combine(ZmxDir, "F3_conic_a4_a6_a8.zmx"), "A.zmx");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);

        // Nothing moved: not a byte changes.
        string same = s.At("A.same.zmx");
        LensPatcher.Save(system, lens, same, catalog);
        Assert.Equal(File.ReadAllBytes(lens), File.ReadAllBytes(same));

        int figured = system.Surfaces.FindIndex(x => x.AsphericCoefficients[1] != 0.0);
        Assert.True(figured > 0, "F3 should have an r^4 term");
        system.Surfaces[figured].Conic *= 1.5;
        system.Surfaces[figured].AsphericCoefficients[1] *= -2.0;

        string output = s.At("A.optimised.zmx");
        LensPatcher.Save(system, lens, output, catalog);

        SameFiguring(system, LensFile.Read(output, catalog));
        Assert.Equal(2, LinesThatDiffer(lens, output));
    }

    /// <summary>
    /// The figuring goes back in the file's OWN units. A coefficient of r^(2k+2) scales as
    /// length^-(2k+1), so a file in inches takes a very different number from the same
    /// surface in millimetres, and getting the power wrong is invisible until the lens is made.
    /// </summary>
    [Fact]
    public void FiguringGoesBackInTheFilesOwnUnits()
    {
        using var s = new Scratch();
        string lens = s.Copy(Path.Combine(ZmxDir, "F3_conic_a4_a6_a8.zmx"), "I.zmx");
        File.WriteAllText(lens, ReadUtf16(lens).Replace("UNIT MM", "UNIT IN"),
                          new UnicodeEncoding(false, true));

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        Assert.Equal(25.4, system.FileUnitScale, 6);

        int figured = system.Surfaces.FindIndex(x => x.AsphericCoefficients[1] != 0.0);
        system.Surfaces[figured].AsphericCoefficients[1] *= 1.3;
        system.Surfaces[figured].AsphericCoefficients[3] = 2.5e-14;

        string output = s.At("I.optimised.zmx");
        LensPatcher.Save(system, lens, output, catalog);
        SameFiguring(system, LensFile.Read(output, catalog));
    }

    /// <summary>
    /// CODE V, OSLO and Optalix files cannot yet take figuring back, and a save whose figuring
    /// moved is REFUSED, with nothing written, rather than written without it - which is the
    /// failure this whole section exists for. Figuring that did not move is no obstacle.
    /// </summary>
    [Theory]
    [InlineData(".seq")]
    [InlineData(".len")]
    [InlineData(".otx")]
    public void AFormatThatCannotCarryFiguringRefusesToDropIt(string extension)
    {
        using var s = new Scratch();
        string lens = s.Copy(Export(extension), "N" + extension);

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(lens, catalog);
        system.Surfaces[1].Curvature *= 1.03;

        string fine = s.At("N.fine" + extension);
        LensPatcher.Save(system, lens, fine, catalog);
        Assert.True(File.Exists(fine));

        system.Surfaces[2].Conic = -0.37;
        string output = s.At("N.optimised" + extension);
        var ex = Assert.Throws<NotSupportedException>(
            () => LensPatcher.Save(system, lens, output, catalog));
        Assert.Contains("surface 2", ex.Message);
        Assert.False(File.Exists(output), "nothing may be written when the figuring cannot be");
    }

    /// <summary>
    /// The optimisation report names the figuring that moved. It used to list only radii,
    /// thicknesses and glasses, so a run that moved nothing but a conic reported "Nothing".
    /// </summary>
    [Fact]
    public void TheReportNamesTheFiguringThatMoved()
    {
        var catalog = CatalogLocator.LoadBundled();
        var start = LensFile.Read(Path.Combine(ZmxDir, "F1_conic_singlet.zmx"), catalog);
        var best = AberrationCalculator.Optimize.Evaluation.DesignCopy.Deep(start);
        best.Surfaces[1].Conic = -1.46;
        best.Surfaces[1].AsphericCoefficients[2] = 3e-15;

        string report = OptimizationReport.Build(new AberrationCalculator.Optimize.RunOutcome
        {
            Best = best, Start = start, InitialMerit = 0.1, FinalMerit = 0.05,
        }, "F1.zmx");

        Assert.DoesNotContain("Nothing.", report);
        Assert.Matches(@"\n  1\s+conic\s+-0\.6\s+-1\.46", report);
        Assert.Matches(@"\n  1\s+A6\s+0\s+3\.000000E-15\s+from zero", report);
    }
}
