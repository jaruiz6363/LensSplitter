using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The two halves of the second-order intrinsic contribution, from M (65.1-2).
///
/// <para>These exist because of what M (65.5-6) say: the figuring enters D from the SECOND
/// order onward, where every aspheric split in this program assumes it never does. See
/// docs/verification.md. They are not yet wired into the scheme - the map from these quantities
/// to the scheme's s1..s6 is still open - so what is pinned here are the invariants that must
/// hold whatever that map turns out to be.</para>
/// </summary>
public class SecondaryHalvesTests
{
    /// <summary>
    /// On a sphere both halves vanish: L because every term carries a figuring coefficient,
    /// D because the increment is figured-less-unfigured and there is no figuring. If this
    /// ever fails, wiring the split in would move a spherical system, and every spherical
    /// result in the suite is already verified against Buchdahl's published numbers.
    /// </summary>
    [Theory]
    [InlineData(0.618735, 4.82439, 1.0, 0.0)]
    [InlineData(1.61620, -0.753929, 0.9259, 1.8394)]
    [InlineData(0.635930, -1.64505, 0.8686, 3.4029)]
    public void BothHalvesVanishOnASphere(double k, double c0, double y, double v)
    {
        var (d, l) = TertiaryCubics.SecondaryHalves(k, 0.0, 0.0, c0, y, v);
        Assert.All(d, x => Assert.True(Math.Abs(x) < 1e-12, $"D increment {x:E3} on a sphere"));
        Assert.All(l, x => Assert.True(Math.Abs(x) < 1e-12, $"L {x:E3} on a sphere"));
    }

    /// <summary>
    /// And on a figured surface neither vanishes. The D half being non-zero IS the finding:
    /// were it zero, "the figuring travels entirely on the height ratio" would be true at
    /// second order as it is at first, and the aspheric split would have been right all along.
    /// </summary>
    [Fact]
    public void TheFiguringReachesTheDHalfAtSecondOrder()
    {
        var (d, l) = TertiaryCubics.SecondaryHalves(0.618735, 3.0e-3, 1.0e-5, 4.82439, 1.0, 0.0);

        Assert.Contains(d, x => Math.Abs(x) > 1e-12);
        Assert.Contains(l, x => Math.Abs(x) > 1e-12);
    }

    /// <summary>
    /// The D increment is linear in c1 to leading order, as (65.6) requires - its two figuring
    /// terms, gamma6 L(1) and -(1/4) cbar1 v0 gamma5^2, both carry one power of c1. Doubling c1
    /// with c2 held at zero must therefore double it.
    /// </summary>
    [Fact]
    public void TheDHalfIsLinearInTheFirstFiguringCoefficient()
    {
        var (d1, _) = TertiaryCubics.SecondaryHalves(0.618735, 1.0e-3, 0.0, 4.82439, 1.0, 0.0);
        var (d2, _) = TertiaryCubics.SecondaryHalves(0.618735, 2.0e-3, 0.0, 4.82439, 1.0, 0.0);

        for (int m = 0; m < 6; m++)
        {
            if (Math.Abs(d1[m]) < 1e-14) continue;
            double ratio = d2[m] / d1[m];
            Assert.True(Math.Abs(ratio - 2.0) < 1e-6,
                $"monomial {m}: doubling c1 scaled the D half by {ratio:F6}, not 2");
        }
    }

    /// <summary>The quadratic expansion must agree with the cubic one on their common machinery.</summary>
    [Fact]
    public void TheQuadraticExpansionIsTheSameSubstitutionAsTheCubic()
    {
        // xi^2 expands as (y^2 t1 + 2y t2 + t3)^2, whose t1^2 coefficient is y^4.
        double y = 1.7, v = 0.4, c0 = 2.1;
        var q = TertiaryScriptT.ExpandQuadraticPhysical(
            new[] { 1.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, y, v, c0);
        Assert.Equal(y * y * y * y, q[0], 10);
        Assert.Equal(4.0 * y * y * y, q[1], 10);   // 2 * y^2 * 2y
    }
}
