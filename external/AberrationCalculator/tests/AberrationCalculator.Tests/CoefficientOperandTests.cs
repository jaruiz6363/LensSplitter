using System;
using System.Linq;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Targeting a named aberration coefficient.
///
/// <para>The point of these beside PRMSA is that a predicted spot is a poor instrument for asking
/// about any single coefficient - `verification.md` measures two designs whose `tau15` differs by
/// a factor of five predicting the same spot to one part in ten thousand. A designer correcting a
/// NAMED aberration is asking about the coefficient, and PRMSA cannot hear that question.</para>
/// </summary>
public class CoefficientOperandTests
{
    private static (Design Design, MeritFunction Merit) Build(string lens, VariableSet vars,
                                                              params Operand[] operands)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(operands);
        return (design, merit);
    }

    private static VariableSet Vars(params Variable[] v)
    {
        var set = new VariableSet();
        set.AddRange(v);
        return set;
    }

    private static Operand Coef(string name, double target = 0.0, double weight = 1.0) =>
        new Operand { Type = OperandType.ABER, Coefficient = name, Target = target, Weight = weight };

    private static Operand CoefAt(string name, int surface) =>
        new Operand { Type = OperandType.ABER, Coefficient = name, Surface = surface,
                      Target = 0.0, Weight = 1.0 };

    /// <summary>
    /// Every one of the thirty-seven parses, by its own name, in whatever case it is typed. The
    /// canonical spelling is what travels, so that the indexer, the label and the file written
    /// back all see one string.
    /// </summary>
    [Fact]
    public void EveryCoefficientParsesByItsOwnName()
    {
        foreach (string name in BuchdahlTerms.Names)
        {
            var ops = MeritFile.Parse(new[] { $"{name.ToUpperInvariant()}, 1, TAR 0" });

            var op = Assert.Single(ops);
            Assert.Equal(OperandType.ABER, op.Type);
            Assert.Equal(name, op.Coefficient);      // canonical, not as typed
            Assert.Equal(name, op.Label);            // and that is what the report shows
        }
    }

    /// <summary>A coefficient operand round-trips through the file it is written to.</summary>
    [Fact]
    public void ACoefficientOperandRoundTripsThroughTheFile()
    {
        var original = MeritFile.Parse(new[]
        {
            "B,     1, TAR 0",
            "Pi5,   2, TAR 0",
            "tau15, 5, MIN -0.001, MAX 0.001",
            "PRMSA, 1, TAR 0",
        });

        var again = MeritFile.Parse(MeritFile.Write(original).Split('\n'));

        Assert.Equal(4, again.Count);
        Assert.Equal("B", again[0].Coefficient);
        Assert.Equal("Pi5", again[1].Coefficient);
        Assert.Equal("Tau15", again[2].Coefficient);
        Assert.Equal(-0.001, again[2].Min);
        Assert.Equal(OperandType.PRMSA, again[3].Type);
        Assert.Null(again[3].Coefficient);
    }

    /// <summary>
    /// <b>The operand and the report agree about what the coefficient is.</b> This is the whole
    /// contract: a designer reads `Tau15` off the report, types `Tau15` into the merit function,
    /// and the optimiser drives the number they read. If these two ever diverged, a merit
    /// function would be aiming at something the report never showed.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void ACoefficientOperandEvaluatesToWhatTheReportPrints(string lens)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);

        var vars = Vars(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        var design = new Design(system, catalog, vars);

        // What the analysis side computes for this same design, by its own route through Core.
        // The comparison is not circular: it is what checks that the probe hands the chain the
        // right wavelength and the right field, and that it applies the tertiary at all.
        var indices = design.Indices(design.PrimaryWave);
        var paraxial = ParaxialTrace.Trace(system, indices, design.MaxField);
        var reference = BuchdahlCoefficients.Compute(system, paraxial);
        TertiaryCoefficients.Attach(system, indices, paraxial, reference, design.MaxField);
        var expected = reference.Totals;

        var merit = new MeritFunction(design);
        foreach (string name in BuchdahlTerms.Names) merit.Add(Coef(name));

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        for (int i = 0; i < BuchdahlTerms.Names.Length; i++)
        {
            string name = BuchdahlTerms.Names[i];
            double want = expected[name];
            double got = r.Values[i];
            double tol = 1e-9 * Math.Max(1.0, Math.Abs(want));
            Assert.True(Math.Abs(want - got) <= tol,
                $"{name}: report {want:G12}, operand {got:G12}");
        }
    }

    /// <summary>
    /// The derivative of every coefficient is analytic and exact, on a spherical design and on a
    /// figured one. The figured case is the one that runs through the aspheric arrangement, which
    /// no operand reached before the coefficients became targetable.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void EveryCoefficientDerivativeMatchesCentralDifferences(string lens)
    {
        var vars = Vars(
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        foreach (string name in BuchdahlTerms.Names) merit.Add(Coef(name));

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// A coefficient targeted with a FIGURING variable, which is the combination the last two
    /// pieces of work existed to make possible: correct a named fifth-order aberration by
    /// figuring a surface, with the gradient coming through the aspheric arrangement.
    /// </summary>
    [Fact]
    public void ACoefficientCanBeTargetedByAConic()
    {
        var vars = Vars(
            new Variable { Kind = VariableKind.Conic, Surface = 1 },
            new Variable { Kind = VariableKind.Asphere4, Surface = 4 });

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.Add(Coef("B"));
        merit.Add(Coef("B5"));
        merit.Add(Coef("M2"));      // Shafer's sagittal oblique spherical
        merit.Add(Coef("Pi5"));     // and fifth-order field curvature
        merit.Add(Coef("Tau15"));

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// ABER on its own is refused, and the message says what to write instead. It is a
    /// reasonable thing to type - it is the name in the enum - and the difference between a
    /// limitation and a mistake is carried entirely by the message.
    /// </summary>
    [Fact]
    public void ABERWithoutACoefficientIsRefusedWithTheRemedy()
    {
        var ex = Assert.Throws<FormatException>(() => MeritFile.Parse(new[] { "ABER, 1, TAR 0" }));

        Assert.Contains("which coefficient", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TAU15", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Something that is neither an operand nor a coefficient names both lists, because a user
    /// who typed `TAU21` or `SPHA` needs to see which of the two they were reaching for.
    /// </summary>
    [Fact]
    public void AnUnknownNameNamesBothLists()
    {
        var ex = Assert.Throws<FormatException>(() => MeritFile.Parse(new[] { "TAU21, 1, TAR 0" }));

        Assert.Contains("not an operand and not an aberration coefficient", ex.Message,
                        StringComparison.Ordinal);
        Assert.Contains("PRMSA", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Tau20", ex.Message, StringComparison.Ordinal);
    }

    // ── The same Jacobian check the derivative tests use ────────────────────────────────────

    private static void CheckJacobian(Design design, MeritFunction merit, VariableSet vars)
    {
        var x0 = design.Read();
        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, analytic.Failure);

        int m = merit.Operands.Count, n = vars.Count;

        double largest = 0.0;
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                largest = Math.Max(largest, Math.Abs(analytic.Jacobian[i, j]));
        Assert.True(largest > 0.0, "the analytic Jacobian is entirely zero");

        for (int j = 0; j < n; j++)
        {
            double column = 0.0;
            for (int i = 0; i < m; i++)
                column = Math.Max(column, Math.Abs(analytic.Jacobian[i, j]));

            double magnitude = Math.Max(Math.Abs(x0[j]), 1.0);
            double h = column > 0.0 ? 1e-6 / column : 1e-6;
            h = Math.Min(h, 1e-4 * magnitude);
            h = Math.Max(h, 1e-13 * magnitude);

            var plus = (double[])x0.Clone(); plus[j] += h;
            design.Apply(plus);
            var rp = merit.Evaluate(false);

            var minus = (double[])x0.Clone(); minus[j] -= h;
            design.Apply(minus);
            var rm = merit.Evaluate(false);

            design.Apply((double[])x0.Clone());
            Assert.True(rp.Ok && rm.Ok, "the design could not be evaluated beside the start point");

            for (int i = 0; i < m; i++)
            {
                double numeric = (rp.Residuals[i] - rm.Residuals[i]) / (2.0 * h);
                double exact = analytic.Jacobian[i, j];
                double scale = Math.Max(Math.Abs(exact), Math.Abs(numeric));
                double tolerance = 2e-4 * scale + 1e-7 * largest;

                Assert.True(Math.Abs(exact - numeric) <= tolerance,
                    $"d({merit.Operands[i].Label})/d({vars[j].Name}): " +
                    $"analytic {exact:G10}, central difference {numeric:G10}, " +
                    $"differ by {Math.Abs(exact - numeric):G4} (tolerance {tolerance:G4})");
            }
        }
    }

    /// <summary>
    /// <c>HELP B</c> and <c>HELP TAU15</c> answer, because a coefficient's own name is how the
    /// operand is written and so has to be how it is asked about. Before this they threw "not a
    /// command or an operand" - wrong and discouraging in the same breath, and exactly what a
    /// designer would type first after reading a coefficient off the report.
    /// </summary>
    [Theory]
    [InlineData("B")]
    [InlineData("tau15")]
    [InlineData("Pi5")]
    [InlineData("M2")]
    [InlineData("B7")]
    public void HelpAnswersForACoefficientName(string name)
    {
        string text = SettingsCommands.Help(name);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("coefficient", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And something that is neither is still refused, so a typo is still heard.</summary>
    [Fact]
    public void HelpStillRefusesAnUnknownName()
    {
        Assert.Throws<ArgumentException>(() => SettingsCommands.Help("TAU21"));
    }

    /// <summary>
    /// <b>The surfaces add to the system total.</b> This is the property that makes a per-surface
    /// operand worth having: a designer targeting surface 5's coma has to be able to compare it
    /// with the system's, and with the other surfaces'. The chain keeps per-surface contributions
    /// UNSCALED and multiplies only the totals by the F/number, so without the scaling applied in
    /// <c>SurfaceCoefficients</c> these would be in different units from the number printed beside
    /// them - agreeing with nothing and summing to nothing.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void ThePerSurfaceContributionsSumToTheSystemTotal(string lens)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        var vars = Vars(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        var design = new Design(system, catalog, vars);

        int last = system.LastOpticalSurface();

        // The eighteen that HAVE a per-surface value. Tau2 to Tau20 do not, and are refused.
        string[] perSurface =
            { "B", "F", "C", "Pi", "E", "B5", "F1", "F2", "M1", "M2", "M3",
              "N1", "N2", "N3", "C5", "Pi5", "E5", "B7" };

        var merit = new MeritFunction(design);
        foreach (string name in perSurface) merit.Add(Coef(name));                  // the totals
        foreach (string name in perSurface)
            for (int s = 1; s <= last; s++) merit.Add(CoefAt(name, s));             // and the parts

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        for (int i = 0; i < perSurface.Length; i++)
        {
            double total = r.Values[i];

            double sum = 0.0;
            for (int s = 0; s < last; s++)
                sum += r.Values[perSurface.Length + i * last + s];

            double tol = 1e-9 * Math.Max(1e-12, Math.Abs(total));
            Assert.True(Math.Abs(total - sum) <= tol,
                $"{perSurface[i]}: total {total:G12}, surfaces sum to {sum:G12}");
        }
    }

    /// <summary>
    /// A per-surface coefficient is a different number from the system's, so the operand is
    /// actually reaching the surface rather than quietly returning the total.
    /// </summary>
    [Fact]
    public void APerSurfaceCoefficientIsNotTheSystemTotal()
    {
        var catalog = CatalogLocator.LoadBundled();
        var design = new Design(LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog), catalog,
                                Vars(new Variable { Kind = VariableKind.Curvature, Surface = 1 }));

        var merit = new MeritFunction(design);
        merit.Add(Coef("B"));
        merit.Add(CoefAt("B", 1));
        merit.Add(CoefAt("B", 3));

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        Assert.NotEqual(r.Values[0], r.Values[1]);
        Assert.NotEqual(r.Values[1], r.Values[2]);
    }

    /// <summary>
    /// <b>A tertiary coefficient is a system operand, and a surface is refused.</b> The report
    /// splits the twenty by surface, as each surface's share of the total, but on a figured flat
    /// facing collimated light a surface's own share may have no finite value, and the operand
    /// would then return a silent zero, which reads exactly like a surface that contributes
    /// nothing - the failure this session has met three times and the reason the refusal is
    /// explicit.
    /// </summary>
    [Theory]
    [InlineData("Tau5")]
    [InlineData("Tau15")]
    [InlineData("Tau20")]
    public void ATertiaryCoefficientIsRefusedPerSurface(string name)
    {
        var ex = Assert.Throws<FormatException>(
            () => MeritFile.Parse(new[] { $"{name}, 1, TAR 0, 3" }));

        Assert.Contains("SYSTEM operand", ex.Message, StringComparison.Ordinal);
        Assert.Contains("B to B7", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>But the same coefficient without a surface is the system's, and is accepted.</summary>
    [Fact]
    public void ATertiaryCoefficientIsStillFineForTheSystem()
    {
        var ops = MeritFile.Parse(new[] { "Tau15, 1, TAR 0" });
        Assert.Equal(0, ops[0].Surface);
        Assert.Equal("Tau15", ops[0].Coefficient);
    }

    /// <summary>
    /// Surface first, wavelength second, and the label says which is which. Surface 0 is the
    /// system and is not labelled "s0", which would read as the object surface.
    /// </summary>
    [Fact]
    public void ASurfaceAndAWavelengthRoundTripThroughTheFile()
    {
        var original = MeritFile.Parse(new[]
        {
            "B,   1, TAR 0",            // the system
            "M2,  2, TAR 0, 4",         // surface 4
            "Pi5, 3, TAR 0, 4, 2",      // surface 4, wavelength 2
        });

        Assert.Equal(0, original[0].Surface);
        Assert.Equal(4, original[1].Surface);
        Assert.Equal(4, original[2].Surface);
        Assert.Equal(2, original[2].Wave);

        Assert.Equal("B", original[0].Label);
        Assert.Equal("M2 s4", original[1].Label);

        var again = MeritFile.Parse(MeritFile.Write(original).Split('\n'));
        Assert.Equal(3, again.Count);
        Assert.Equal(0, again[0].Surface);
        Assert.Equal(4, again[1].Surface);
        Assert.Equal(4, again[2].Surface);
        Assert.Equal(2, again[2].Wave);
    }

    /// <summary>
    /// The derivative of a per-surface coefficient is analytic and exact, on a figured design as
    /// well as a spherical one.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void PerSurfaceDerivativesMatchCentralDifferences(string lens)
    {
        var vars = Vars(
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var catalog = CatalogLocator.LoadBundled();
        var design = new Design(LensFile.Read(Fixtures.Lens(lens), catalog), catalog, vars);

        var merit = new MeritFunction(design);
        foreach (string name in new[] { "B", "F", "C", "E", "B5", "M2", "Pi5", "B7" })
            for (int s = 1; s <= 3; s++) merit.Add(CoefAt(name, s));

        CheckJacobian(design, merit, vars);
    }
}
