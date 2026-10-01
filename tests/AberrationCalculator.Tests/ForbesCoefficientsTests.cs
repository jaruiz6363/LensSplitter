using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The tertiary coefficients by Forbes' route, against Table I's and against real rays.
///
/// <para>Both routes are read off by the SAME inversion, so no change of basis stands between
/// them and nothing here depends on my having derived one correctly. What differs is only what
/// produced the ray landings: an exact numerical trace, or a truncated series.</para>
/// </summary>
public class ForbesCoefficientsTests
{
    private sealed record Run(double[] Table, double[] Forbes, double[] Rays, double Largest,
                              double Residual);

    private static Run Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var paraxial = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, paraxial);
        TertiaryCoefficients.Attach(sys, n, paraxial, b, field);

        var rays = CoefficientInversion.Invert(sys, n, paraxial, field);
        var forbes = ForbesCoefficients.Invert(sys, n, paraxial, field);
        Assert.NotNull(rays);
        Assert.NotNull(forbes);

        var t = b.Totals;
        var table = new double[21];
        double largest = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            table[k] = k == 1 ? t.B7
                     : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;
            largest = Math.Max(largest, Math.Abs(table[k]));
        }
        return new Run(table, forbes!.Tau, rays!.Tau, largest, forbes!.Residual);
    }

    private static double Worst(double[] a, double[] b, double largest)
    {
        double w = 0.0;
        for (int k = 1; k <= 20; k++) w = Math.Max(w, Math.Abs(a[k] - b[k]) / largest);
        return w;
    }

    /// <summary>
    /// The gate on the whole route. On a system with no figuring, Table I is validated against
    /// real rays to three thousandths of a per cent, so Forbes' route reproducing it proves the
    /// new machinery against a known answer before it is used anywhere it cannot be checked. If
    /// this fails, nothing below it means anything.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_Sphere")]
    public void ForbesReproducesTableIOnSphericalSystems(string fixtureName)
    {
        var r = Load(fixtureName);
        double w = Worst(r.Table, r.Forbes, r.Largest);

        Assert.True(w < 1e-4,
            $"{fixtureName}: Forbes and Table I differ by {100 * w:F4} per cent of the largest " +
            "coefficient on a spherical system, where they must agree. Two routes that disagree " +
            "with no figuring present disagree about the spherical scheme, which Table I gets " +
            "right, so the fault would be in the new one.");
    }

    /// <summary>
    /// The direct solve is exact, and this is the guard on it.
    ///
    /// <para>The degree-seven aberration is supplied exactly rather than fitted, so the twenty
    /// coefficients must reproduce it to roundoff - a residual near 1E-16 where the fitted route
    /// gives 1E-6. It is also the check on the cached least-squares operator: that operator is
    /// built once from the model and reused for every design, and if it were wrong for any reason
    /// the solution would stop satisfying the system and this residual would blow up.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheDirectSolveReproducesItsOwnDataToRoundoff(string fixtureName)
    {
        var r = Load(fixtureName);
        Assert.True(r.Residual < 1e-12,
            $"{fixtureName}: the solve leaves a relative residual of {r.Residual:E2}. Supplied " +
            "exactly, the degree-seven aberration lies in the span of the twenty coefficients and " +
            "the residual is roundoff; anything larger means the model, the data or the cached " +
            "operator no longer agree.");
    }

    /// <summary>
    /// And it is not vacuous: the coefficients are large enough that agreeing to a part in ten
    /// thousand of the largest is a real statement rather than two ways of computing zero.
    /// </summary>
    [Fact]
    public void TheSphericalCoefficientsAreNotAllNearlyZero()
    {
        var r = Load("CookeTriplet");
        int big = 0;
        for (int k = 1; k <= 20; k++) if (Math.Abs(r.Table[k]) > 0.01 * r.Largest) big++;
        Assert.True(big >= 5,
            $"only {big} of the twenty coefficients reach a hundredth of the largest, so the " +
            "agreement above is not saying much");
    }

    /// <summary>
    /// On the figured designs the reported set now agrees with real rays exactly as Forbes' route
    /// does, read off by the same instrument.
    ///
    /// <para>These are the fixtures the aspheric tertiary could not bring in for a long time:
    /// this test used to assert that the scheme missed the rays by at least one to six per cent
    /// on each, and it failed, as it asked to, when the Sec. 85 arrangement was completed -
    /// members one to five of the barred q accumulation from the identities, the sixth by
    /// paper XII's duality, and the figuring's D half in the hat pass. It now records the
    /// agreement: both routes within a fortieth of a per cent of the largest coefficient, where
    /// the measured figures are between two thousandths and three hundredths, the rays' own
    /// floor.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("Ladder2_FiguredSphere_Then_A4")]
    [InlineData("Ladder2_A4_Both")]
    [InlineData("Ladder2_A4_First")]
    [InlineData("Ladder2_A4_Then_FiguredSphere")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void BothRoutesAgreeWithRealRaysOnFiguredDesigns(string fixtureName)
    {
        var r = Load(fixtureName);
        double forbes = Worst(r.Forbes, r.Rays, r.Largest);
        double table = Worst(r.Table, r.Rays, r.Largest);

        Assert.True(forbes < 5e-4,
            $"{fixtureName}: Forbes disagrees with real rays by {100 * forbes:F4} per cent, " +
            "which is far more than the hundredths of a per cent it manages elsewhere.");

        Assert.True(table < 5e-4,
            $"{fixtureName}: the reported set disagrees with real rays by {100 * table:F4} per " +
            "cent, where Forbes' route manages hundredths. The aspheric arrangement has regressed.");
    }

    /// <summary>
    /// On the figured spheres both routes are right, which matters: it says Forbes is not simply
    /// better everywhere for some uninteresting reason, and that the disagreement above is
    /// confined to the cases the aspheric arrangement was already known to miss.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_FiguredSphere_Second")]
    [InlineData("Ladder2_FiguredSphere_First")]
    [InlineData("Ladder2_FiguredSphere_Both")]
    public void BothRoutesAgreeWhereTheAsphericTableIsAlreadyRight(string fixtureName)
    {
        var r = Load(fixtureName);
        Assert.True(Worst(r.Forbes, r.Rays, r.Largest) < 5e-4, "Forbes against rays");
        Assert.True(Worst(r.Table, r.Rays, r.Largest) < 5e-4, "Table I against rays");
        Assert.True(Worst(r.Table, r.Forbes, r.Largest) < 5e-4, "Table I against Forbes");
    }

    /// <summary>
    /// Raising the truncation barely moves the answer, and what movement there is belongs to the
    /// inversion rather than to the trace.
    ///
    /// <para>The trace itself is truncation-independent to a part in ten to the eleventh, which
    /// <c>ForbesTraceTests.RunningAtAHigherDegreeDoesNotChangeTheLowerCoefficients</c> checks on
    /// the coefficients directly. Here the coefficients are read off through the inversion, and a
    /// degree-four trace really does carry ninth-order content that the fit then has to separate
    /// from the seventh - the same job it does on real rays. So the two do not agree exactly, and
    /// they should not: the residual difference measures the inversion's own separation error, and
    /// at a hundredth of a per cent it is the same size as the disagreement between Forbes and
    /// real rays. That is worth knowing, because it says the agreement reported above is at the
    /// floor the instrument can resolve rather than at the floor the method can reach.</para>
    /// </summary>
    [Fact]
    public void TheAnswerDoesNotDependOnTheTruncation()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("Ladder2_A4_Second"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var paraxial = ParaxialTrace.Trace(sys, n, field);

        var three = ForbesCoefficients.Invert(sys, n, paraxial, field, degree: 3);
        var four = ForbesCoefficients.Invert(sys, n, paraxial, field, degree: 4);
        Assert.NotNull(three);
        Assert.NotNull(four);

        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(three!.Tau[k]));

        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(three!.Tau[k] - four!.Tau[k]) < 1e-4 * largest,
                $"tau{k} is {three!.Tau[k]:E6} at degree three and {four!.Tau[k]:E6} at degree " +
                "four, further apart than the inversion's own separation error accounts for.");
    }
}
