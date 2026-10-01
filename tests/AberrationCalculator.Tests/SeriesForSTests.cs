using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// S(1) and S(2) against M (63.3), which prints them explicitly in xi, eta and zeta.
///
/// <para>This is an independent check on a large part of the machinery. The scheme reaches
/// S1 and S2 through (78.1) - S1 = tau1 + 2 sigma1 + [(1+k)/2k] zeta, and S2 by a product of
/// polynomials - whereas (63.3) states the answer outright. So it exercises sigma1, sigma2,
/// tau1, tau2, the recursion of (78.1) and the polynomial multiplication all at once, against
/// a printed result that shares none of that construction.</para>
///
/// <para>The eta-squared, eta-zeta and zeta-squared coefficients matter most here: no helper
/// in <c>TertiaryCubics</c> ever sets them directly, so they can only arise from S1 squared -
/// which makes them the terms most sensitive to an error in the multiplication.</para>
/// </summary>
public class SeriesForSTests
{
    [Theory]
    [InlineData(0.6, 0.0455, -5.6e-6)]
    [InlineData(1.5, 0.0455, -5.6e-6)]
    [InlineData(0.6, 0.0125, 1.2e-5)]
    [InlineData(0.8, -0.045, 0.0)]
    [InlineData(0.65, 0.02, 3.0e-5)]
    public void TheFirstTwoSeriesTermsAreBuchdahlSixtyThreeThree(double k, double c0, double c1)
    {
        var s = new TertiaryCubics.Surface(k, c1, 0.0, c0);

        double th1 = c0 / 2.0, th12 = th1 * th1, th14 = th12 * th12;
        double k2 = k * k, k3 = k2 * k, k4 = k3 * k;

        // (63.3), S(1)
        Close(0.5 * (k2 - k + 1.0) * c0 * c0, s.S1.C[1, 0, 0], "S1 xi");
        Close(-k2 * c0,                       s.S1.C[0, 1, 0], "S1 eta");
        Close(0.5 * k * (k + 1.0),            s.S1.C[0, 0, 1], "S1 zeta");

        // (63.3), S(2)
        Close(0.25 * (4 * k2 - 4 * k + 3.0) * c0 * c1
              + 2.0 * (3 * k4 - 5 * k3 + 7 * k2 - 5 * k + 3.0) * th14,
              s.S2.C[2, 0, 0], "S2 xi^2");
        Close(-(k2 * c1 + 0.5 * (3 * k4 - 3 * k3 + 3 * k2 - k + 1.0) * c0 * c0 * c0),
              s.S2.C[1, 1, 0], "S2 xi eta");
        Close(k * (3 * k3 - k2 + 2.0) * th12,      s.S2.C[1, 0, 1], "S2 xi zeta");
        Close(2.0 * k3 * (3 * k - 1.0) * th12,     s.S2.C[0, 2, 0], "S2 eta^2");
        Close(-k2 * (k + 1.0) * (3 * k - 2.0) * th1, s.S2.C[0, 1, 1], "S2 eta zeta");
        Close((3.0 / 8.0) * k * (k + 1.0) * (k2 - 1.0), s.S2.C[0, 0, 2], "S2 zeta^2");
    }

    private static void Close(double expected, double actual, string name)
    {
        double scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        if (scale < 1e-300) return;
        Assert.True(Math.Abs(expected - actual) / scale < 1e-10,
            $"{name}: (63.3) gives {expected:E10}, the scheme gives {actual:E10}");
    }
}
