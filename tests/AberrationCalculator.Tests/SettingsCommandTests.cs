using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Building a merit function and a variable list a command at a time.
///
/// <para><b>The settings file is the session.</b> Each command reads what is there, changes it and
/// writes it back, so a command line that exits between every command still behaves like a
/// program that remembers - and what it remembers survives a restart and can be opened in an
/// editor. These tests are mostly about that: that the state a command leaves is the state the
/// next one finds.</para>
/// </summary>
[Collection(ProcessWideState.Name)]
public class SettingsCommandTests
{
    private sealed class Scratch : IDisposable
    {
        public string Dir { get; }
        public string Zmx { get; }
        public string Lhlt { get; }

        public Scratch()
        {
            Dir = Path.Combine(Path.GetTempPath(), "abcalc-cmd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);

            // A spherical .zmx: every fixture in the repository is figured, and the optimiser
            // refuses those, so the conic is flattened for a design that can actually be run.
            Zmx = Path.Combine(Dir, "L.zmx");
            string source = Directory
                .GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures",
                                       "coefficient-reference"), "*.zmx")
                .OrderBy(f => f).First();

            using (var reader = new StreamReader(source, Encoding.UTF8, true))
            {
                var text = new StringBuilder();
                string? line;
                while ((line = reader.ReadLine()) != null)
                    text.AppendLine(line.TrimStart().StartsWith("CONI", StringComparison.Ordinal)
                                        ? "  CONI 0" : line);
                File.WriteAllText(Zmx, text.ToString(), reader.CurrentEncoding);
            }

            Lhlt = Path.Combine(Dir, "T.lhlt");
            File.Copy(Fixtures.Lens("CookeTriplet"), Lhlt);
        }

        public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
    }

    private static int Run(params string[] args) => Cli.Program.Run(args);

    // ── Variables ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void VarDeclaresAVariableAndVarlistShowsItNumbered()
    {
        using var s = new Scratch();

        Assert.Equal(0, Run(s.Zmx, "VAR", "CV 1 MIN 0.005 MAX 0.05", "-q"));
        Assert.Equal(0, Run(s.Zmx, "VAR", "TH 2 MIN 40 MAX 120", "-q"));

        string vars = File.ReadAllText(Sidecar.VariablePathFor(s.Zmx));
        Assert.Contains("VAR CV 1 MIN 0.005 MAX 0.05", vars);
        Assert.Contains("VAR TH 2 MIN 40 MAX 120", vars);

        var spec = VarFile.Read(Sidecar.VariablePathFor(s.Zmx));
        Assert.Equal(2, spec.Variables.Count);
    }

    /// <summary>
    /// The reason merging was chosen over last-wins. A command naming only the maximum must not
    /// discard a minimum set a moment earlier - the surprise <c>chmod u+x</c> exists to avoid.
    /// </summary>
    [Fact]
    public void ASecondVarCommandMergesRatherThanReplacing()
    {
        using var s = new Scratch();

        Assert.Equal(0, Run(s.Zmx, "VAR", "TH 2 MIN 1.0", "-q"));
        Assert.Equal(0, Run(s.Zmx, "VAR", "TH 2 MAX 25.0", "-q"));

        var v = Assert.Single(VarFile.Read(Sidecar.VariablePathFor(s.Zmx)).Variables.Items);
        Assert.Equal(1.0, v.Min);
        Assert.Equal(25.0, v.Max);
    }

    [Fact]
    public void FreeDropsTheBoundsAgain()
    {
        using var s = new Scratch();
        Assert.Equal(0, Run(s.Zmx, "VAR", "CV 1 MIN 0.01 MAX 0.03", "-q"));
        Assert.Equal(0, Run(s.Zmx, "VAR", "CV 1 FREE", "-q"));

        var v = Assert.Single(VarFile.Read(Sidecar.VariablePathFor(s.Zmx)).Variables.Items);
        Assert.False(v.IsBounded);
    }

    [Fact]
    public void VarremoveTakesTheNumberFromTheListing()
    {
        using var s = new Scratch();
        Run(s.Zmx, "VAR", "CV 1", "VAR", "CV 2", "VAR", "TH 2", "-q");

        Assert.Equal(0, Run(s.Zmx, "VARREMOVE", "2", "-q"));

        var left = VarFile.Read(Sidecar.VariablePathFor(s.Zmx)).Variables.Items;
        Assert.Equal(2, left.Count);
        Assert.Equal(1, left[0].Surface);
        Assert.Equal(VariableKind.Thickness, left[1].Kind);
    }

    // ── Operands ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void OpAddsToTheMeritFunctionAndOplistNumbersIt()
    {
        using var s = new Scratch();

        Assert.Equal(0, Run(s.Zmx, "OP", "PRMSA, 1, TAR 0", "-q"));
        Assert.Equal(0, Run(s.Zmx, "OP", "EFL, 10, TAR 50", "-q"));
        Assert.Equal(0, Run(s.Zmx, "OP", "EGT, 5, MIN 1, 1, 2", "-q"));

        var ops = MeritFile.Read(Sidecar.MeritPathFor(s.Zmx));
        Assert.Equal(3, ops.Count);
        Assert.Equal(Optimize.Operands.OperandType.PRMSA, ops[0].Type);
        Assert.Equal(50.0, ops[1].Target);
        Assert.Equal(1.0, ops[2].Min);
    }

    /// <summary>
    /// Removing shifts the numbers, so the listing is reprinted. A user removing two things in a
    /// row would otherwise be working from numbers that no longer mean what they meant.
    /// </summary>
    [Fact]
    public void OpremoveShiftsTheRestAndTheListingIsShownAgain()
    {
        using var s = new Scratch();
        Run(s.Zmx, "OP", "PRMSA, 1, TAR 0", "OP", "EFL, 10, TAR 50",
                   "OP", "EGT, 5, MIN 1, 1, 2", "-q");

        var output = new StringWriter();
        var previous = Console.Out;
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, Run(s.Zmx, "OPREMOVE", "2"));
        }
        finally { Console.SetOut(previous); }

        string text = output.ToString();
        Assert.Contains("MERIT FUNCTION", text);
        Assert.Contains("1  PRMSA", text);
        Assert.Contains("2  EGT", text);          // renumbered, not left as 3
        Assert.DoesNotContain("EFL", text);

        var ops = MeritFile.Read(Sidecar.MeritPathFor(s.Zmx));
        Assert.Equal(2, ops.Count);
    }

    [Fact]
    public void RemovingSomethingThatIsNotThereIsRefusedByNumber()
    {
        using var s = new Scratch();
        Run(s.Zmx, "OP", "PRMSA, 1, TAR 0", "-q");

        var error = new StringWriter();
        var previous = Console.Error;
        try
        {
            Console.SetError(error);
            Assert.Equal(1, Run(s.Zmx, "OPREMOVE", "9", "-q"));
        }
        finally { Console.SetError(previous); }

        Assert.Contains("the numbers run from 1 to 1", error.ToString());

        // And nothing was removed.
        Assert.Single(MeritFile.Read(Sidecar.MeritPathFor(s.Zmx)));
    }

    // ── Pickups ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PickupIsRecordedAndListed()
    {
        using var s = new Scratch();
        Assert.Equal(0, Run(s.Zmx, "PICKUP", "TH 2 INDEX 1 SCALE 1 OFFSET -0.1", "-q"));

        var p = Assert.Single(VarFile.Read(Sidecar.VariablePathFor(s.Zmx)).Pickups);
        Assert.Equal(2, p.TargetSurfaceIndex);
        Assert.Equal(1, p.SourceSurfaceIndex);
        Assert.Equal(-0.1, p.Offset);
    }

    // ── Where the settings go ────────────────────────────────────────────────────────────

    /// <summary>
    /// A .lhlt keeps its variables in the lens file, which is where its author put them and where
    /// the program that wrote it will look for them next. It gets no .var file at all.
    /// </summary>
    [Fact]
    public void ForALhltTheVariablesGoIntoTheLensItself()
    {
        using var s = new Scratch();

        Assert.Equal(0, Run(s.Lhlt, "VAR", "CV 1", "VAR", "TH 2 MIN 1 MAX 12", "-q"));

        Assert.False(File.Exists(Sidecar.VariablePathFor(s.Lhlt)),
                     "a .lhlt should not get a .var file");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(s.Lhlt, catalog);
        Assert.True(system.Surfaces[1].CurvatureVariable);
        Assert.True(system.Surfaces[2].ThicknessVariable);
        Assert.Equal(1.0, system.Surfaces[2].ThicknessMin);
        Assert.Equal(12.0, system.Surfaces[2].ThicknessMax);
    }

    /// <summary>The merit function is a .mf whatever the format, and is never put in the lens.</summary>
    [Fact]
    public void TheMeritFunctionIsAlwaysASidecar()
    {
        using var s = new Scratch();
        Assert.Equal(0, Run(s.Lhlt, "OP", "PRMSA, 1, TAR 0", "-q"));

        Assert.True(File.Exists(Sidecar.MeritPathFor(s.Lhlt)));
        Assert.DoesNotContain("PRMSA", File.ReadAllText(s.Lhlt));
    }

    // ── Commands and a run together ──────────────────────────────────────────────────────

    [Fact]
    public void CommandsMayBeFollowedByARunInOneInvocation()
    {
        using var s = new Scratch();

        Assert.Equal(0, Run(s.Zmx,
            "VAR", "CV 1", "VAR", "CV 2",
            "OP", "PRMSA, 1, TAR 0", "OP", "EFL, 10, TAR 50",
            "--optimize", "--iterations", "20", "-q"));

        Assert.True(File.Exists(Path.Combine(s.Dir, "L.optimised.zmx")));
    }

    /// <summary>
    /// <c>--save</c> with no path overwrites the design that was read. That is destructive by
    /// design, and only ever happens when it is asked for by name.
    /// </summary>
    [Fact]
    public void SaveWithNoPathOverwritesTheLens()
    {
        using var s = new Scratch();
        Run(s.Zmx, "VAR", "CV 1", "OP", "EFL, 10, TAR 50", "-q");

        var catalog = CatalogLocator.LoadBundled();
        double before = LensFile.Read(s.Zmx, catalog).Surfaces[1].Curvature;

        Assert.Equal(0, Run(s.Zmx, "--optimize", "--iterations", "30", "--save", "-q"));

        Assert.False(File.Exists(Path.Combine(s.Dir, "L.optimised.zmx")),
                     "--save should have written in place, not beside");
        Assert.NotEqual(before, LensFile.Read(s.Zmx, catalog).Surfaces[1].Curvature);
    }

    [Fact]
    public void SaveasWritesSomewhereNewAndLeavesTheOriginal()
    {
        using var s = new Scratch();
        Run(s.Zmx, "VAR", "CV 1", "OP", "EFL, 10, TAR 50", "-q");

        var catalog = CatalogLocator.LoadBundled();
        double before = LensFile.Read(s.Zmx, catalog).Surfaces[1].Curvature;

        string target = Path.Combine(s.Dir, "new.zmx");
        Assert.Equal(0, Run(s.Zmx, "--optimize", "--iterations", "30", "--saveas", target, "-q"));

        Assert.True(File.Exists(target));
        Assert.Equal(before, LensFile.Read(s.Zmx, catalog).Surfaces[1].Curvature);
    }

    // ── Basin hopping ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Basin hopping produces a design per chain, so it needs somewhere to put them and is
    /// refused without it rather than quietly keeping one and discarding the rest.
    /// </summary>
    [Fact]
    public void BasinHoppingIsRefusedWithoutAFolder()
    {
        using var s = new Scratch();
        Run(s.Zmx, "VAR", "CV 1", "OP", "EFL, 10, TAR 50", "-q");

        var error = new StringWriter();
        var previous = Console.Error;
        try
        {
            Console.SetError(error);
            Assert.Equal(1, Run(s.Zmx, "--optimize_basin_hopping", "-q"));
        }
        finally { Console.SetError(previous); }

        Assert.Contains("folder", error.ToString());
    }

    [Fact]
    public void BasinHoppingWritesOneDesignPerChain()
    {
        using var s = new Scratch();
        Run(s.Zmx, "VAR", "CV 1", "VAR", "CV 2", "OP", "EFL, 10, TAR 50", "-q");

        string folder = Path.Combine(s.Dir, "hop");
        Assert.Equal(0, Run(s.Zmx, "--optimize_basin_hopping", "--save", folder,
                            "--hops", "4", "--chains", "3", "--iterations", "10",
                            "--seed", "3", "-q"));

        var designs = Directory.GetFiles(folder, "L.chain*.zmx");
        Assert.Equal(3, designs.Length);

        // Each carries the settings it was made with, so any of them can be picked up and
        // worked on further.
        foreach (string d in designs)
        {
            Assert.True(File.Exists(Sidecar.MeritPathFor(d)));
            Assert.True(File.Exists(Sidecar.VariablePathFor(d)));
        }
        Assert.True(File.Exists(Path.Combine(folder, "L.zmx.optimisation.txt")));
    }

    /// <summary>
    /// Glasses come from the NAMED substitution catalogue and nowhere else. A search free to pick
    /// from every vendor catalogue at once settles on glasses nobody stocks.
    /// </summary>
    [Fact]
    public void GlassSubstitutionDrawsOnlyFromTheNamedCatalogue()
    {
        using var s = new Scratch();

        // The triplet, not the single-element .zmx: with one element and only a focal length to
        // hit, a glass change is very nearly free, so whether the walk keeps one is a coin toss
        // on the seed. Three elements and a spot to correct make substitution mean something.
        Run(s.Lhlt, "VAR", "CV 1", "VAR", "CV 4", "OP", "PRMSA, 1, TAR 0",
                    "OP", "EFL, 10, TAR 50", "-q");

        // Enough hops for a swap to actually be taken. The per-hop kick is a tenth of a per
        // cent, so glass moves by being proposed and judged over many hops
        // rather than by the design being flung somewhere new each time.
        string folder = Path.Combine(s.Dir, "hop");
        Assert.Equal(0, Run(s.Lhlt, "--optimize_basin_hopping",
                            "--glass_substitution", "CoreSet28", "--save", folder,
                            "--hops", "60", "--chains", "2", "--iterations", "20",
                            "--seed", "11", "-q"));

        // Without this the check below could pass by having changed nothing at all - the design
        // may start on glasses that are themselves in CoreSet28.
        Assert.Contains("glass substitutions accepted",
                        File.ReadAllText(Path.Combine(folder, "T.lhlt.optimisation.txt")));

        var coreSet = SubstitutionCatalog.Load("CoreSet28");
        var catalog = CatalogLocator.LoadBundled();

        // The invariant is not "every glass is in CoreSet28" - a surface the walk never swapped
        // keeps whatever the designer put there, and the triplet starts on SK16, which is not in
        // the set (N-SK16 is). What must hold is that nothing NEW came from outside it.
        var started = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var surface in LensFile.Read(s.Lhlt, catalog).Surfaces)
            if (!string.IsNullOrWhiteSpace(surface.Material)) started.Add(surface.Material);

        foreach (string d in Directory.GetFiles(folder, "T.chain*.lhlt"))
            foreach (var surface in LensFile.Read(d, catalog).Surfaces)
            {
                if (string.IsNullOrWhiteSpace(surface.Material) || surface.IsMirror) continue;
                if (started.Contains(surface.Material)) continue;         // never swapped
                Assert.True(coreSet.Find(surface.Material) != null,
                            $"{Path.GetFileName(d)} was given {surface.Material}, "
                          + "which is not in CoreSet28");
            }
    }
}
