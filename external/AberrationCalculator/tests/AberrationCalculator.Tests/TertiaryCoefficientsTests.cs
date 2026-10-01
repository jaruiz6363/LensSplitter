using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The twenty seventh-order coefficients, against the values Buchdahl published for his
/// own triplet in Table II of <i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958).
///
/// These are the quantities Robb's polynomial consumes and this program has never had -
/// only tau1, under the name B7. Every one of them is now computed from the literature and
/// checked against a printed value.
/// </summary>
public class TertiaryCoefficientsTests
{
    private static (List<Surface>, double[]) Triplet()
    {
        double[] c = { 0, 4.82439, -0.753929, -1.64505, 5.11794, 0.310726, -1.46116, 0 };
        double[] dBefore = { 0, 0, 0.040278, 0.016851, 0.0096145, 0.138738, 0.0313246, 0.836 };
        double[] n = { 1.0, 1.6162, 1.0, 1.5725, 1.0, 1.6162, 1.0, 1.0 };
        var s = new List<Surface>();
        for (int i = 0; i < c.Length; i++) s.Add(new Surface { Curvature = c[i] });
        for (int i = 0; i + 1 < c.Length; i++) s[i].Thickness = dBefore[i + 1];
        return (s, n);
    }

    /// <summary>All twenty, against Buchdahl's Table II.</summary>
    [Fact]
    public void TheTwentyTertiaryCoefficientsMatchTableII()
    {
        var (surfaces, indices) = Triplet();
        var tau = TertiaryCoefficients.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        double[] published =
        {
            0, -4653.4, -2598.6, -1950.5, -647.43, -323.99, -506.00, -45.97, -21.92,
            0.60, 2.325, -25.02, 4.06, -4.55, 1.82, 3.54, 2.379, 0.312, 1.775, 0.0913, 0.0723,
        };

        for (int i = 1; i <= 20; i++)
        {
            // tau9 is a residual: T5/2 = -4.066 and T7/4 = +4.651 cancel to 0.585. Buchdahl
            // prints T5 to two significant figures as -8.1, and half of that rounding lands
            // straight in tau9 - recomputing from his own printed T gives exactly his 0.60.
            // So the looser bound here is his printing precision, not our arithmetic.
            double tol = i == 9 ? 3e-2 : 1e-2;

            double rel = Math.Abs((tau[i] - published[i]) / published[i]);
            Assert.True(rel <= tol,
                $"tau{i}: computed {tau[i]}, published {published[i]}, relative {rel:G3} exceeds {tol:G3}");
        }
    }

    /// <summary>
    /// tau1 is the coefficient this program already had, as B7 - the only seventh-order
    /// term any of the six programs Johnson surveyed in 1972 exposed. Worth pinning
    /// separately: if the new pipeline disagreed with the old one here, one of them is
    /// wrong about the aberration everybody does compute.
    /// </summary>
    [Fact]
    public void Tau1IsTheSeventhOrderSphericalCoefficient()
    {
        var (surfaces, indices) = Triplet();
        var tau = TertiaryCoefficients.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        Assert.True(Math.Abs((tau[1] + 4653.4) / 4653.4) < 1e-4,
            $"tau1 = {tau[1]}, published -4653.4");
    }

    /// <summary>Distortion is the last of each order, and tau20 is the seventh-order one.</summary>
    [Fact]
    public void Tau20IsTheSeventhOrderDistortion()
    {
        var (surfaces, indices) = Triplet();
        var tau = TertiaryCoefficients.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        // Buchdahl remarks on p.753 that coefficients beyond the fifth order contribute a
        // POSITIVE amount to distortion, which he calls surprising. tau20 is indeed positive.
        Assert.True(tau[20] > 0, $"tau20 = {tau[20]}, expected positive");
        Assert.True(Math.Abs((tau[20] - 0.0723) / 0.0723) < 1e-2);
    }
}
