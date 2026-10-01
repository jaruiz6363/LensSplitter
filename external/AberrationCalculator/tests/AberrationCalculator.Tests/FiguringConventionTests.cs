using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// <see cref="TertiaryCubics.Figuring"/> against M (56.5), computed independently from the sag
/// series. This pins the CONVENTION, which has been got wrong here before: the fifth-order code
/// forms c1 as 8 A4 + conic c0^3, exactly twice Buchdahl's, and feeding that to formulae written
/// in his convention scales every linear term by two and every c1-squared term by four.
///
/// <para><b>Conics were untested until this file.</b> Every aspheric fixture in the repository
/// has conic = 0, so the conic terms of c1, c2 and c3 had no coverage at all. They are exercised
/// here alongside the polynomial ones and the mixtures.</para>
///
/// <para><b>On a confusion worth recording.</b> The first polynomial coefficient is NOT the
/// curvature and the second is NOT the conic, though each pair coincides at one order. A2 shifts
/// the vertex curvature to c + 2 A2, but a surface with (c, A2) differs from one with (c + 2 A2,
/// 0) at r^4 and beyond - which is exactly why <see cref="Core.Models.Surface.VertexForm"/> has
/// to compensate A4, A6 and A8 when it folds A2 in. Likewise a conic contributes
/// (1+k) c^3 / 8 at r^4, interchangeable with A4 THERE, but it also contributes
/// (1+k)^2 c^5 / 16 at r^6 where A4 contributes nothing. They are independent parameters; what
/// is degenerate is the combination entering each order, which is why (56.5) is written in the
/// sag coefficients rather than in c, k and the A's.</para>
/// </summary>
public class FiguringConventionTests
{
    /// <summary>
    /// M (56.5), from the sag series. th_n is the coefficient of r^(2n).
    /// </summary>
    private static (double C1, double C2, double C3) FromSag(double t1, double t2,
                                                             double t3, double t4)
    {
        double c1 = 4.0 * (t2 - t1 * t1 * t1);
        double c2 = 6.0 * (t3 - 4.0 * t1 * t1 * t2 + 2.0 * Math.Pow(t1, 5));
        double c3 = 4.0 * (2.0 * t4 - 9.0 * t1 * t1 * t3 + 30.0 * Math.Pow(t1, 4) * t2
                           - 12.0 * t1 * t2 * t2 - 10.0 * Math.Pow(t1, 7));
        return (c1, c2, c3);
    }

    [Theory]
    // polynomial only
    [InlineData(0.0455, 0.0, -1.4e-6, 0.0, 0.0)]
    [InlineData(0.0455, 0.0, 0.0, 2.0e-8, 0.0)]
    [InlineData(0.0455, 0.0, 0.0, 0.0, -1.0e-10)]
    [InlineData(0.0125, 0.0, -1.4e-6, 2.0e-8, -1.0e-10)]
    // conic only - no fixture in the repository exercises these
    [InlineData(0.0455, -1.0, 0.0, 0.0, 0.0)]
    [InlineData(0.0455, -0.5, 0.0, 0.0, 0.0)]
    [InlineData(0.0455, 0.5, 0.0, 0.0, 0.0)]
    [InlineData(0.0455, -4.0, 0.0, 0.0, 0.0)]
    [InlineData(0.0125, -2.0, 0.0, 0.0, 0.0)]
    // mixtures, and a negative curvature
    [InlineData(0.0455, -0.5, -1.4e-6, 2.0e-8, -1.0e-10)]
    [InlineData(-0.0450, 3.0, 2.0e-6, -5.0e-8, 2.0e-10)]
    public void TheFiguringCoefficientsAreBuchdahlSixteenFiveOfTheSag(
        double c0, double conic, double a4, double a6, double a8)
    {
        var f = TertiaryCubics.Figuring.From(conic, a4, a6, a8, c0, 1.0);

        double k1 = 1.0 + conic;
        double t1 = c0 / 2.0;
        double t2 = k1 * Math.Pow(c0, 3) / 8.0 + a4;
        double t3 = k1 * k1 * Math.Pow(c0, 5) / 16.0 + a6;
        double t4 = 5.0 * k1 * k1 * k1 * Math.Pow(c0, 7) / 128.0 + a8;
        var (c1, c2, c3) = FromSag(t1, t2, t3, t4);

        // The tolerance is relative to the largest TERM entering each expression, not to the
        // result. (56.5) is a difference of nearly equal quantities - for a pure r^8 surface c2
        // is exactly zero and evaluating it this way returns 1E-23 of pure cancellation - so a
        // tolerance on the result alone would fail on arithmetic rather than on the code.
        double s1 = Math.Max(Math.Abs(t2), Math.Abs(t1 * t1 * t1));
        double s2 = Math.Max(Math.Abs(t3),
                    Math.Max(Math.Abs(4.0 * t1 * t1 * t2), Math.Abs(2.0 * Math.Pow(t1, 5))));
        double s3 = Math.Max(Math.Abs(2.0 * t4),
                    Math.Max(Math.Abs(9.0 * t1 * t1 * t3),
                    Math.Max(Math.Abs(30.0 * Math.Pow(t1, 4) * t2),
                    Math.Max(Math.Abs(12.0 * t1 * t2 * t2),
                             Math.Abs(10.0 * Math.Pow(t1, 7))))));

        Close(c1, f.C1, 4.0 * s1, "c1", c0, conic);
        Close(c2, f.C2, 6.0 * s2, "c2", c0, conic);
        Close(c3, f.C3, 4.0 * s3, "c3", c0, conic);
    }

    private static void Close(double expected, double actual, double termScale, string name,
                              double c0, double conic)
    {
        double scale = Math.Max(Math.Max(Math.Abs(expected), Math.Abs(actual)), termScale);
        if (scale < 1e-300) return;
        Assert.True(Math.Abs(expected - actual) / scale < 1e-10,
            $"{name} at c0={c0}, conic={conic}: (56.5) gives {expected:E10}, " +
            $"Figuring.From gives {actual:E10}");
    }

    /// <summary>
    /// A sphere has no figuring at all, whatever else is true of it. This is the case every
    /// coefficient in the program depends on, since the figured path is only entered when
    /// something here is non-zero.
    /// </summary>
    [Theory]
    [InlineData(0.0455)]
    [InlineData(-0.0450)]
    [InlineData(0.0)]
    public void ASphereHasNoFiguring(double c0)
    {
        var f = TertiaryCubics.Figuring.From(0.0, 0.0, 0.0, 0.0, c0, 1.0);
        Assert.False(f.Present);
        Assert.Equal(0.0, f.C1);
        Assert.Equal(0.0, f.C2);
        Assert.Equal(0.0, f.C3);
    }
}
