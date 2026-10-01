using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A flat surface contributes to the tertiary coefficients like any other, and the scheme has
/// to say so.
///
/// <para><b>What was wrong.</b> <c>a_p</c> is formed with a factor <c>Omega/j</c>, and BOTH of
/// those vanish on a plane. <c>Omega = (k-1)c/n</c> has the curvature as a factor; and
/// substituting <c>q = (c y_q - v_q)/(c y_p - v_p)</c> into <c>j = -v_p q + v_q</c> gives</para>
///
/// <code>
///     j = c (v_q y_p - v_p y_q) / i_p
/// </code>
///
/// <para>so j carries the curvature too, the bracket being the Lagrange invariant. The ratio
/// therefore has the finite non-zero limit <c>i_p/(v_q y_p - v_p y_q)</c>, but the code guarded
/// on j and returned zero, which deleted the surface from the tertiary altogether - every
/// primary of it, and with them its whole row.</para>
///
/// <para><b>Why nothing caught it.</b> Buchdahl's published triplet has no flat face, so the
/// entire spherical validation - which is exact - never exercised the case. Nor did any aspheric
/// fixture. It is not an aspheric fault at all: an ALL-SPHERICAL plano-convex pair was wrong by
/// 39 per cent of its largest coefficient, and is now wrong by 0.043.</para>
///
/// <para>Flat surfaces are not a corner case. Plano-convex singlets, windows, cover glasses,
/// cemented flats and unfolded prisms all have them.</para>
/// </summary>
public class PlanoSurfaceTests
{
    private static (double WorstShare, double Residual) Score(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var inv = CoefficientInversion.Invert(sys, n, p, field);
        Assert.NotNull(inv);

        var t = b.Totals;
        double Scheme(int k) => k == 1 ? t.B7
            : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        double big = 0.0, worst = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(Scheme(k) - inv!.Tau[k]) / big);

        return (worst, inv!.Residual);
    }

    /// <summary>
    /// The case that exposed it: two powered surfaces, no figuring anywhere, the rear one flat.
    /// The scheme is known right on spheres, so this has to agree with the rays as closely as
    /// any other spherical design. It was 39.356 per cent.
    /// </summary>
    [Fact]
    public void ASphericalSystemWithAFlatSurfaceIsRight()
    {
        var (worst, residual) = Score("Ladder2_Sphere_FlatRear");

        Assert.True(residual < 1e-3,
            $"the fit did not close, residual {residual:E2}");
        Assert.True(worst < 0.005,
            $"worst disagreement with the rays is {100 * worst:F3} per cent of the largest " +
            "coefficient, on an ALL-SPHERICAL system where the scheme is known right. A flat " +
            "surface is being dropped from the tertiary again.");
    }

    /// <summary>
    /// The conversion between the scheme's <c>a_p</c> and the fifth-order code's <c>B</c> is one
    /// constant per system - <see cref="AsphericSchemeIncrements"/> depends on it, and says it
    /// "comes out identical on every surface". That makes it a sharp test needing no ray trace:
    /// a surface the scheme has silently dropped shows up as a ratio of zero against a
    /// fifth-order contribution that is not zero.
    ///
    /// <para>On the flat-rear pair the ratios were -414.34 and -0.000000. They now agree.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder2_Sphere_FlatRear")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("CookeTriplet")]
    public void ThePrimaryConversionIsTheSameOnEverySurfaceIncludingFlatOnes(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var rows = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);

        double? first = null;
        int compared = 0;
        for (int i = 1; i <= sys.LastOpticalSurface(); i++)
        {
            double five = b.Intrinsic[i].B;
            if (Math.Abs(five) < 1e-14) continue;          // the stop contributes nothing

            double ratio = rows[i].T[10] / five;
            if (first == null) { first = ratio; compared++; continue; }

            Assert.True(Math.Abs(ratio - first.Value) / Math.Abs(first.Value) < 1e-9,
                $"{fixtureName} surface {i} (c = {sys.Surfaces[i].Curvature:E3}): the scheme's " +
                $"a_p is {rows[i].T[10]:E5} against a fifth-order B of {five:E5}, a ratio of " +
                $"{ratio:F6}, where the earlier surfaces give {first.Value:F6}. The two routes " +
                "disagree about this surface.");
            compared++;
        }

        Assert.True(compared >= 2,
            $"{fixtureName}: only {compared} surfaces had a contribution to compare");
    }
}

/// <summary>
/// The scheme must be CONTINUOUS across a flat surface: a radius of 1e10 and a radius of
/// infinity are the same lens to any precision anyone can measure, so they must give the same
/// coefficients. This needs no ray trace and no oracle, only the two runs, which makes it the
/// sharpest test in the audit - any guard that takes a special branch at c = 0 without matching
/// its own limit shows up here at once.
/// </summary>
public class FlatSurfaceContinuityTests
{
    private static double[] Tau(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);

        var t = b.Totals;
        var tau = new double[21];
        for (int k = 1; k <= 20; k++)
            tau[k] = k == 1 ? t.B7
                   : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;
        return tau;
    }

    private static void MustAgree(string flat, string nearFlat, double tolerance)
    {
        var a = Tau(flat);
        var b = Tau(nearFlat);
        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(b[k]));

        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(a[k] - b[k]) / big < tolerance,
                $"tau{k}: the exactly-flat lens gives {a[k]:E6} and its R = 1e10 twin gives " +
                $"{b[k]:E6}, a gap of {100 * Math.Abs(a[k] - b[k]) / big:F3} per cent of the " +
                "largest coefficient. They are the same lens; a branch at c = 0 does not match " +
                "its own limit.");
    }

    /// <summary>
    /// The spherical case, in converging space. This is what the flat-surface fix bought: before
    /// it, the exactly-flat member of the pair was out by 39 per cent.
    /// </summary>
    [Fact]
    public void ASphericalFlatSurfaceAgreesWithItsCurvatureLimit()
        => MustAgree("Ladder2_Sphere_FlatRear", "Ladder2_Sphere_NearFlatRear", 0.001);

    /// <summary>
    /// The figured case - a corrector plate. The z assembly used to be skipped outright at
    /// c = 0, because it forms <c>(j/c)^r</c> and <c>dD/c</c>, so the figuring was dropped
    /// entirely and tau2 changed sign across the limit.
    ///
    /// <para>Neither had to be singular. <c>j = c(v_q y_p - v_p y_q)/i_p</c> exactly, so
    /// <c>j/c = L/i_p</c>, wanting only a marginal incidence. And every term of the D cubic
    /// carries c0 - <c>Y = c0 y</c> in the first, <c>c0 v"</c> in the second - so
    /// <see cref="TertiaryCubics.DCubicOverC0"/> removes it by hand; the expansion that consumes
    /// the cubic is linear in its coefficients, so expanding that IS <c>dD/c</c>. The L side
    /// never needed either, which is why Buchdahl's plate of (73.7) came out right through the
    /// general route while this one did not.</para>
    ///
    /// <para><b>This test now guards continuity, and only that.</b> The design remains 113 per
    /// cent out against the ray oracle - and so does its R = 1e10 twin, which is the point: they
    /// now AGREE, to better than one part in ten thousand on every coefficient, where before
    /// they differed by half. A surface whose figuring dwarfs its base curvature is a regime the
    /// aspheric tertiary handles badly quite apart from any singularity, and that is a different
    /// open problem - the same one <c>Ladder2_A4_Second</c> shows at 12 per cent.</para>
    ///
    /// <para>Note also that the flat member moved from 110.8 per cent to 113.2 when this was
    /// fixed. It had been carrying a compensating error, the same reading hazard the t114 work
    /// ran into: an aggregate can improve for the wrong reason.</para>
    /// </summary>
    [Fact]
    public void AFiguredFlatSurfaceAgreesWithItsCurvatureLimit()
        => MustAgree("Ladder2_FiguredFlatRear", "Ladder2_FiguredNearFlatRear", 0.001);
}
