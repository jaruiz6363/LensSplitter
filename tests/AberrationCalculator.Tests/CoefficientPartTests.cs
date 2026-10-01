using System;
using System.Linq;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Targeting the intrinsic, figuring and induced parts of a surface's contribution.
///
/// <para>The three answer to different actions, which is why a designer needs them apart. What a
/// surface generates out of its own curvature is corrected by bending THAT surface. What its
/// figuring adds is corrected by changing the figuring. What was INDUCED in it by the aberration
/// already reaching it is not its doing at all, and correcting it there is a second wrong
/// balancing a first.</para>
///
/// <para>Shafer's case for the split is that the limiting aberrations of a corrected design
/// cannot be controlled without it. See <c>docs/references.md</c>.</para>
/// </summary>
public class CoefficientPartTests
{
    private static VariableSet Vars(params Variable[] v)
    {
        var set = new VariableSet();
        set.AddRange(v);
        return set;
    }

    private static Operand At(string name, int surface, CoefficientPart part) =>
        new Operand
        {
            Type = OperandType.ABER, Coefficient = name, Surface = surface,
            Part = part, Target = 0.0, Weight = 1.0,
        };

    private static Design Open(string lens, VariableSet vars, Action<OpticalSystem>? prepare = null)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        prepare?.Invoke(system);
        return new Design(system, catalog, vars);
    }

    private static VariableSet OneCurvature => Vars(
        new Variable { Kind = VariableKind.Curvature, Surface = 1 });

    /// <summary>
    /// <b>Intrinsic plus figuring plus induced is the whole contribution</b>, on every surface and
    /// every coefficient that has the three. This is the property the split must have to be worth
    /// targeting: a designer who bounds the induced part and drives the intrinsic one needs the
    /// two to account for what the surface actually carries, with nothing unallocated.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void TheThreePartsSumToTheWholeContribution(string lens)
    {
        var design = Open(lens, OneCurvature);
        int last = design.System.LastOpticalSurface();

        string[] names = { "B", "F", "C", "E", "B5", "F1", "M1", "M2", "N2", "C5", "Pi5", "E5", "B7" };

        var merit = new MeritFunction(design);
        foreach (string n in names)
            for (int s = 1; s <= last; s++)
            {
                merit.Add(At(n, s, CoefficientPart.Total));
                merit.Add(At(n, s, CoefficientPart.Intrinsic));
                merit.Add(At(n, s, CoefficientPart.Figuring));
                merit.Add(At(n, s, CoefficientPart.Induced));
            }

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        int k = 0;
        foreach (string n in names)
            for (int s = 1; s <= last; s++)
            {
                double total = r.Values[k];
                double parts = r.Values[k + 1] + r.Values[k + 2] + r.Values[k + 3];
                k += 4;

                double tol = 1e-9 * Math.Max(1e-12, Math.Abs(total));
                Assert.True(Math.Abs(total - parts) <= tol,
                    $"{n} s{s}: total {total:G12}, parts sum to {parts:G12}");
            }
    }

    /// <summary>
    /// The induced part is real where something precedes the surface and identically zero on the
    /// first, where nothing does. Both halves matter: the first says the operand reaches
    /// something, the second that it reaches the right thing.
    /// </summary>
    [Fact]
    public void TheInducedPartIsZeroOnTheFirstSurfaceAndNotOnTheOthers()
    {
        var design = Open("CookeTriplet", OneCurvature);

        var merit = new MeritFunction(design);
        merit.Add(At("B5", 1, CoefficientPart.Induced));
        merit.Add(At("B5", 4, CoefficientPart.Induced));

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        Assert.Equal(0.0, r.Values[0]);
        Assert.NotEqual(0.0, r.Values[1]);
    }

    /// <summary>
    /// The figuring part is zero on a sphere and not on a figured surface. A spherical surface has
    /// NO aspheric entry rather than one full of zeros, so this also checks that an absent entry
    /// is summed as nothing instead of throwing.
    /// </summary>
    [Fact]
    public void TheFiguringPartIsZeroOnASphereAndNotOnAFiguredSurface()
    {
        var design = Open("CookeTriplet", OneCurvature, s => s.Surfaces[2].Conic = -0.6);

        var merit = new MeritFunction(design);
        merit.Add(At("B", 1, CoefficientPart.Figuring));
        merit.Add(At("B", 2, CoefficientPart.Figuring));

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        Assert.Equal(0.0, r.Values[0]);
        Assert.NotEqual(0.0, r.Values[1]);
    }

    /// <summary>
    /// A part asked of the SYSTEM is that part summed over the surfaces - which is the
    /// decomposition behind "how much of this design's oblique spherical is induced".
    /// </summary>
    [Fact]
    public void ASystemLevelPartIsThatPartSummedOverTheSurfaces()
    {
        var design = Open("CookeTriplet", OneCurvature);
        int last = design.System.LastOpticalSurface();

        var merit = new MeritFunction(design);
        merit.Add(At("B5", 0, CoefficientPart.Induced));
        for (int s = 1; s <= last; s++) merit.Add(At("B5", s, CoefficientPart.Induced));

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        double sum = 0.0;
        for (int s = 1; s <= last; s++) sum += r.Values[s];

        Assert.Equal(r.Values[0], sum, 10);
    }

    /// <summary>The part is written and read back, and labelled so a report says which it is.</summary>
    [Fact]
    public void APartRoundTripsThroughTheFileAndIsLabelled()
    {
        var original = MeritFile.Parse(new[]
        {
            "M2.INT, 1, TAR 0, 5",
            "M2.fig, 2, TAR 0, 5",
            "B5.IND, 3, MAX 0.001, 5",
        });

        Assert.Equal(CoefficientPart.Intrinsic, original[0].Part);
        Assert.Equal(CoefficientPart.Figuring, original[1].Part);
        Assert.Equal(CoefficientPart.Induced, original[2].Part);
        Assert.Equal("M2.INT s5", original[0].Label);
        Assert.Equal("B5.IND s5", original[2].Label);

        var again = MeritFile.Parse(MeritFile.Write(original).Split('\n'));

        Assert.Equal(3, again.Count);
        Assert.Equal(CoefficientPart.Intrinsic, again[0].Part);
        Assert.Equal(CoefficientPart.Figuring, again[1].Part);
        Assert.Equal(CoefficientPart.Induced, again[2].Part);
        Assert.Equal(5, again[2].Surface);
    }

    /// <summary>
    /// <b>A third-order induced part is refused.</b> There is no such thing: a third-order
    /// contribution is built from the surface's own quantities alone, so nothing earlier can act
    /// on it. The answer would be zero on every design, which is not distinguishable from an
    /// aberration that has been corrected.
    /// </summary>
    [Theory]
    [InlineData("B.IND")]
    [InlineData("F.IND")]
    [InlineData("Pi.IND")]
    public void AThirdOrderInducedPartIsRefused(string token)
    {
        var ex = Assert.Throws<FormatException>(
            () => MeritFile.Parse(new[] { token + ", 1, TAR 0, 3" }));

        Assert.Contains("THIRD-ORDER", ex.Message, StringComparison.Ordinal);
        Assert.Contains("nothing for an earlier surface to induce", ex.Message,
                        StringComparison.Ordinal);
    }

    /// <summary>But its intrinsic and figuring parts are real, and accepted.</summary>
    [Theory]
    [InlineData("B.INT")]
    [InlineData("B.FIG")]
    public void AThirdOrderIntrinsicOrFiguringPartIsFine(string token)
    {
        var ops = MeritFile.Parse(new[] { token + ", 1, TAR 0, 3" });

        Assert.Equal("B", ops[0].Coefficient);
        Assert.Equal(3, ops[0].Surface);
    }

    /// <summary>A tertiary coefficient has no parts either, and says so.</summary>
    [Fact]
    public void ATertiaryCoefficientHasNoParts()
    {
        var ex = Assert.Throws<FormatException>(
            () => MeritFile.Parse(new[] { "Tau15.IND, 1, TAR 0" }));

        Assert.Contains("SYSTEM operand", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>An unknown suffix names the three that exist.</summary>
    [Fact]
    public void AnUnknownPartNamesTheOnesThatExist()
    {
        var ex = Assert.Throws<FormatException>(
            () => MeritFile.Parse(new[] { "M2.TOTAL, 1, TAR 0, 5" }));

        Assert.Contains("INT", ex.Message, StringComparison.Ordinal);
        Assert.Contains("FIG", ex.Message, StringComparison.Ordinal);
        Assert.Contains("IND", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Every part differentiates exactly, on a figured design as on a spherical one.</summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Both")]
    public void PartDerivativesMatchCentralDifferences(string lens)
    {
        var vars = Vars(
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var design = Open(lens, vars);
        var merit = new MeritFunction(design);

        foreach (string n in new[] { "B5", "M2", "Pi5", "B7" })
            for (int s = 2; s <= 3; s++)
            {
                merit.Add(At(n, s, CoefficientPart.Intrinsic));
                merit.Add(At(n, s, CoefficientPart.Figuring));
                merit.Add(At(n, s, CoefficientPart.Induced));
            }

        CheckJacobian(design, merit, vars);
    }

    // ── The same Jacobian check the other derivative tests use ──────────────────────────────

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
}
