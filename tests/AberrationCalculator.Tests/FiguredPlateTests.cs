using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl's figured thin corrector plate, against the closed-form answer he publishes for
/// it in (73.7) — Chapter VIII, p.128.
///
/// <para>This is the acceptance test for the aspheric side, and a rare one: he states the
/// plate's surviving tertiary coefficients outright, in terms of nothing but the index and
/// the figuring, and says he obtained them using eqs. (80.5) and (81.3) — which is to say
/// the L term and the intrinsic-to-total step. So it is a published answer produced by
/// exactly the machinery under test, on a wholly aspheric system. Nothing is fitted to it;
/// there is no free constant to fit.</para>
///
/// <para>Two things make the plate a clean test rather than a hard one. Its surfaces are
/// plano, so the D side vanishes identically — both terms of the D cubic carry c0 — and
/// what remains is L alone. And <c>t1p</c> is simply <c>z1</c>, whose j-power is zero, so
/// it needs neither the stop-shift q nor j, both of which are degenerate here: a collimated
/// p ray meets a plane at zero incidence, and q = i_q/i_p has no value.</para>
/// </summary>
public class FiguredPlateTests
{
    /// <summary>
    /// The plate: two plano surfaces in contact, glass of index N between them, figured.
    /// Buchdahl's (73.1) — c(0,1) = c(0,2) = 0, d(1) = 0, N(1) = N'(2) = 1.
    ///
    /// <para>With the object at infinity the p ray runs at unit height and zero angle, and
    /// a plane leaves both untouched, so y = 1 and v = 0 at each surface.</para>
    /// </summary>
    private static double FirstTertiaryCoefficient(double n, double c3First, double c3Second,
                                                   double c2First, double c2Second)
    {
        double total = 0.0;

        // Surface 1: air into glass. Surface 2: glass into air.
        foreach (var (indexBefore, k, c2, c3) in new[]
                 {
                     (1.0, 1.0 / n, c2First, c3First),
                     (n, n, c2Second, c3Second),
                 })
        {
            const double y = 1.0, v = 0.0, c0 = 0.0, c1 = 0.0;

            var cubic = TertiaryCubics.LCubic(k, c1, c2, c3, c0, y, v);
            var scriptT = TertiaryScriptT.ExpandCubicPhysical(cubic, y, v, c0);

            // z_mu = (1/16) N (1-k) j^JPower [ i T_D + y T_L ]. Here i = 0, and mu = 1 has
            // no power of j, so neither j nor q is needed.
            total += (1.0 / 16.0) * indexBefore * (1.0 - k) * y * scriptT[1];
        }
        return total;
    }

    /// <summary>
    /// <c>T'(1p) = phi3</c>, with <c>phi_n = (N-1)(c(n,1) - c(n,2))</c> — Buchdahl's
    /// "extra-axial powers" of (73.2).
    ///
    /// <para>The dependence on the index and on BOTH surfaces has to come out, not just the
    /// magnitude, which is what makes this a test of the construction rather than of one
    /// number. There is no scale factor to adjust: the normalisation is (1/16)N(1-k), which
    /// is paper II's own (6.1), with the ray height in place of the incidence because this
    /// is the L term.</para>
    /// </summary>
    [Theory]
    [InlineData(1.5, 3.0e-4, 0.0)]
    [InlineData(1.5, 0.0, 3.0e-4)]
    [InlineData(1.5, 3.0e-4, -1.1e-4)]
    [InlineData(1.62, 7.0e-5, 2.0e-5)]
    [InlineData(1.9, -4.0e-4, 1.3e-4)]
    [InlineData(1.33, 1.0e-3, 1.0e-3)]
    public void TheFirstTertiaryCoefficientIsPhi3(double n, double c3First, double c3Second)
    {
        double published = (n - 1.0) * (c3First - c3Second);
        double computed = FirstTertiaryCoefficient(n, c3First, c3Second, 0.0, 0.0);

        double scale = Math.Max(Math.Abs(published), 1e-12);
        Assert.True(Math.Abs(computed - published) / scale < 1e-10,
            $"N = {n}: computed {computed:G10}, published phi3 = {published:G10}");
    }

    /// <summary>
    /// And the sixth-order figuring must not leak into it. (73.7) gives the first tertiary
    /// coefficient as phi3 alone, with the phi2 terms landing in the other three — so c2
    /// reaching this one would be a real error, not a rounding one.
    ///
    /// <para>It cannot, structurally: c2 enters L only through gamma2, which multiplies
    /// D(1), and D(1) is proportional to S1, which carries only zeta. With v = 0 that
    /// vanishes. The test states it anyway, because that argument is exactly the kind that
    /// survives a change to the code.</para>
    /// </summary>
    [Theory]
    [InlineData(1.5, 2.0e-3, 0.0)]
    [InlineData(1.5, 0.0, -5.0e-4)]
    [InlineData(1.62, 1.0e-3, 4.0e-4)]
    public void SixthOrderFiguringDoesNotReachTheFirstCoefficient(
        double n, double c2First, double c2Second)
    {
        double bare = FirstTertiaryCoefficient(n, 0.0, 0.0, 0.0, 0.0);
        double withC2 = FirstTertiaryCoefficient(n, 0.0, 0.0, c2First, c2Second);

        Assert.Equal(bare, withC2, 15);
    }

    /// <summary>
    /// An unfigured plate is a plane-parallel plate of zero thickness, which is nothing at
    /// all, and must come out as nothing.
    /// </summary>
    [Theory]
    [InlineData(1.5)]
    [InlineData(1.9)]
    public void AnUnfiguredPlateHasNoTertiaryAberration(double n)
    {
        Assert.Equal(0.0, FirstTertiaryCoefficient(n, 0.0, 0.0, 0.0, 0.0), 15);
    }

    /// <summary>
    /// A plate figured identically on both faces has phi_n = 0 and contributes nothing —
    /// the two figurings cancel. Buchdahl notes the same thing of the primary and secondary
    /// case: "either Omega1 or Omega2 might therefore be taken as plane".
    /// </summary>
    [Theory]
    [InlineData(1.5, 3.0e-4)]
    [InlineData(1.72, -8.0e-5)]
    public void IdenticalFiguringOnBothFacesCancels(double n, double c3)
    {
        Assert.Equal(0.0, FirstTertiaryCoefficient(n, c3, c3, 0.0, 0.0), 14);
    }
}
