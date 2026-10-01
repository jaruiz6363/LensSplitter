using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The script-T machinery, checked against Buchdahl at both ends.
///
/// <para>Buchdahl gives the tables and he gives the answer, but not the working between
/// them - "these two steps require some elementary work which is very tedious indeed ...
/// it suffices to quote merely the final result". Doing the working and landing on his
/// answer is therefore a real check, and a broad one: it exercises every integer of Tables
/// II and III, the substitution (2.5), the monomial ordering of (6.1), and the restoration
/// of c0, all at once.</para>
/// </summary>
public class TertiaryScriptTTests
{
    /// <summary>
    /// Every column of Buchdahl's tables must sum to zero except the last, which must sum
    /// to one - his own check, from setting xi = eta = zeta. It catches a mistyped digit
    /// but not a transposition within a column, which is why it is not the only test here.
    /// </summary>
    [Fact]
    public void TheTablesSatisfyBuchdahlsColumnIdentity()
    {
        // The identity says sum_mu nu_mu(k) = 1 for every k, and likewise for 'nu.
        foreach (double k in new[] { 0.5, 0.618735, 1.0, 1.6162, 2.5 })
        {
            double nu = 0.0, nuPrime = 0.0;
            foreach (double x in TertiaryScriptT.Nu(k)) nu += x;
            foreach (double x in TertiaryScriptT.NuPrime(k)) nuPrime += x;

            Assert.Equal(1.0, nu, 10);
            Assert.Equal(1.0, nuPrime, 10);
        }
    }

    /// <summary>
    /// Carrying out the substitution Buchdahl skips must reproduce the (6.2) he prints.
    /// This is the test the whole class exists to pass.
    /// </summary>
    [Theory]
    [InlineData(0.618735, 4.824392, 0.0)]
    [InlineData(1.6162, -2.5374409, 1.8393703)]
    [InlineData(0.63593, -4.8317844, 3.4029414)]
    [InlineData(1.35, 0.7, -0.4)]
    [InlineData(0.8, -1.3, 2.1)]
    public void TheExpansionReproducesPaperIIEq62(double k, double i, double v)
    {
        double Y = i + v;                       // c0 y, and paper II works at c0 = 1
        var mine = TertiaryScriptT.Expand(TertiaryScriptT.Nu(k), TertiaryScriptT.NuPrime(k), Y, v);
        var his = TertiaryScriptT.Published(Y, v, i, k);

        for (int m = 1; m <= 10; m++)
        {
            double scale = Math.Max(Math.Abs(his[m]), 1e-6);
            Assert.True(Math.Abs(mine[m] - his[m]) / scale < 1e-11,
                $"script-T {m}: expansion {mine[m]:G10}, published {his[m]:G10}");
        }
    }

    private static (List<Surface> Surfaces, double[] Indices) Triplet()
    {
        double[] c = { 0, 0.09648784, -0.01507852, -0.03290101, 0.10235886, 0.0, 0.00621452, -0.02922327, 0 };
        double[] d = { 0, 2.013900, 0.842550, 0.480725, 2.067650, 4.869250, 1.566230, 41.800027, 0 };
        double[] n = { 1.0, 1.6162, 1.0, 1.5725, 1.0, 1.0, 1.6162, 1.0, 1.0 };
        var s = new List<Surface>();
        for (int i = 0; i < c.Length; i++) s.Add(new Surface { Curvature = c[i], Thickness = d[i] });
        s[5].IsStop = true; s[5].SemiDiameter = 3.91358;
        return (s, n);
    }

    /// <summary>
    /// The bridge from a script-T to the computing scheme's z is a single factor per
    /// surface, and the same one for all ten. That is what makes it usable: an aspheric
    /// increment to script-T can be carried into the scheme without reassembling N, omega
    /// and the powers of c0 that the factor is made of.
    ///
    /// <para>This runs on Buchdahl's own triplet, where the scheme's z are already known
    /// to be right, so a failure here is a failure of the script-T side.</para>
    /// </summary>
    [Fact]
    public void TheBridgeToTheComputingSchemeIsOneFactorForAllTen()
    {
        var (surfaces, n) = Triplet();
        var rows = BuchdahlTableI.Compute(surfaces, n, efl: 50.0, stopParameter: 0.113227601);

        foreach (int s in new[] { 1, 2, 3, 4, 6, 7 })
        {
            var r = rows[s];
            double c0 = surfaces[s].Curvature * 50.0;      // the scheme works at unit focal length
            double k = n[s - 1] / n[s];
            double i = r.Ip, j = r.J;

            var T = TertiaryScriptT.Published(c0 * r.Yp, r.Vp, i, k);
            double bridge = TertiaryScriptT.Bridge(r.Z(10), i, j);

            // z3 and z4 do not appear singly: 'script-T_3 is z3 + z4 and 'script-T_4 is 8 z4.
            var target = new double[11];
            for (int m = 1; m <= 10; m++) target[m] = r.Z(m);
            target[3] = r.Z(3) + r.Z(4);
            target[4] = 8.0 * r.Z(4);

            for (int m = 1; m <= 10; m++)
            {
                double predicted = bridge * i * Math.Pow(j, TertiaryScriptT.JPower[m]) * T[m];
                double scale = Math.Max(Math.Abs(target[m]), 1e-12);
                Assert.True(Math.Abs(predicted - target[m]) / scale < 1e-9,
                    $"surface {s}, mu {m}: bridge gives {predicted:G10}, scheme has {target[m]:G10}");
            }
        }
    }

    /// <summary>
    /// c0 y = i + v is what lets paper II's unit-curvature working be restored. If the
    /// scheme's paraxial quantities ever stopped satisfying it, the test above would fail
    /// for a reason that had nothing to do with the tables, so it is pinned separately.
    /// </summary>
    [Fact]
    public void TheUnitCurvatureSubstitutionHolds()
    {
        var (surfaces, n) = Triplet();
        var rows = BuchdahlTableI.Compute(surfaces, n, efl: 50.0, stopParameter: 0.113227601);

        foreach (int s in new[] { 1, 2, 3, 4, 6, 7 })
        {
            double c0 = surfaces[s].Curvature * 50.0;
            Assert.Equal(rows[s].Ip + rows[s].Vp, c0 * rows[s].Yp, 6);
        }
    }

    /// <summary>
    /// The c0-weight of each monomial in xi, eta, zeta: two for each power of xi and one for
    /// each power of eta, since paper II's unit-curvature form has xi carrying c0^2.
    /// </summary>
    private static readonly int[] Weight = { 6, 5, 4, 4, 3, 2, 3, 2, 1, 0 };

    /// <summary>
    /// The physical expansion and the unit-curvature one are the same substitution written
    /// in different coordinates, so they must agree wherever both are defined. Converting a
    /// cubic between the two costs c0 to the monomial weight, and converting the answer back
    /// costs c0 to the j-power - which is the same statement about where c0 lives, made at
    /// the other end.
    ///
    /// <para>This is what licenses the physical form. It is not a new derivation to be
    /// trusted on its own; it is the verified one, re-coordinated.</para>
    /// </summary>
    [Theory]
    [InlineData(4.824392, 1.0, 0.0)]
    [InlineData(-0.753926, 0.92591384, 1.8393703)]
    [InlineData(2.0, 0.7, -0.4)]
    [InlineData(0.1, 1.3, 0.9)]
    [InlineData(-3.5, -0.6, 2.2)]
    public void ThePhysicalExpansionAgreesWithTheUnitCurvatureOne(double c0, double y, double v)
    {
        // An arbitrary cubic, in physical coordinates.
        var physical = new double[10];
        for (int m = 0; m < 10; m++) physical[m] = 1.0 + 0.37 * m - 0.11 * m * m;

        // The same cubic in the unit-curvature coordinates.
        var unit = new double[10];
        for (int m = 0; m < 10; m++) unit[m] = physical[m] / Math.Pow(c0, Weight[m]);

        var mine = TertiaryScriptT.ExpandCubicPhysical(physical, y, v, c0);
        var his = TertiaryScriptT.ExpandCubic(unit, c0 * y, v);

        for (int m = 1; m <= 10; m++)
        {
            double predicted = his[m] * Math.Pow(c0, TertiaryScriptT.JPower[m]);
            double scale = Math.Max(Math.Abs(predicted), 1e-9);
            Assert.True(Math.Abs(mine[m] - predicted) / scale < 1e-9,
                $"mu {m} at c0={c0}: physical {mine[m]:G10}, unit-curvature {predicted:G10}");
        }
    }

    /// <summary>
    /// And the same for the cubic the script-T are actually built from, figuring included -
    /// so the agreement covers the assembly in <see cref="TertiaryCubics.DCubic"/> and not
    /// only the substitution.
    /// </summary>
    [Theory]
    [InlineData(0.618735, 4.824392, 1.0, 0.0)]
    [InlineData(1.6162, -0.753926, 0.92591384, 1.8393703)]
    [InlineData(0.8, 2.0, 0.7, -0.4)]
    public void ThePhysicalCubicAgreesWithTheUnitCurvatureOne(double k, double c0, double y, double v)
    {
        const double c1 = 0.004, c2 = 0.0007;                     // physical figuring
        double c1Unit = c1 / Math.Pow(c0, 3), c2Unit = c2 / Math.Pow(c0, 5);

        var physical = TertiaryCubics.DCubic(k, c1, c2, c0, y, v);
        var unit = TertiaryCubics.DCubic(k, c1Unit, c2Unit, 1.0, c0 * y, v);

        var mine = TertiaryScriptT.ExpandCubicPhysical(physical, y, v, c0);
        var his = TertiaryScriptT.ExpandCubic(unit, c0 * y, v);

        for (int m = 1; m <= 10; m++)
        {
            double predicted = his[m] * Math.Pow(c0, TertiaryScriptT.JPower[m]);
            double scale = Math.Max(Math.Abs(predicted), 1e-12);
            Assert.True(Math.Abs(mine[m] - predicted) / scale < 1e-8,
                $"mu {m} at c0={c0}: physical {mine[m]:G10}, unit-curvature {predicted:G10}");
        }
    }

    /// <summary>
    /// A plano surface, which the unit-curvature form cannot express at all - its figuring
    /// would have to be divided by zero. The physical form goes through, and what it says is
    /// worth knowing: the D cubic vanishes IDENTICALLY at c0 = 0, figured or not.
    ///
    /// <para>Both its terms carry c0 - the ray-height term through Y = c0 y, and the X3 term
    /// through the explicit factor that the differing weights require - so a figured plate
    /// contributes nothing through D. Everything it does comes through L. That is what makes
    /// Buchdahl's (73.7), which states a figured plate's tertiary coefficients in closed form,
    /// a test of L alone rather than of the two mixed together.</para>
    /// </summary>
    [Fact]
    public void ThePlanoCubicIsFiniteAndVanishesSoThatThePlateTestsLAlone()
    {
        const double k = 1.0 / 1.5;

        foreach (double v in new[] { 0.0, 0.25, -1.3 })
        foreach (double y in new[] { 1.0, 0.4 })
        {
            var figured = TertiaryCubics.DCubic(k, c1: 0.0, c2: 3e-4, c0: 0.0, y: y, v: v);
            foreach (double x in figured)
                Assert.True(double.IsFinite(x), "the physical cubic is not finite at c0 = 0");

            var expanded = TertiaryScriptT.ExpandCubicPhysical(figured, y, v, 0.0);
            foreach (double x in expanded)
                Assert.True(double.IsFinite(x), "the physical expansion is not finite at c0 = 0");

            // And it is not merely finite - it is zero.
            for (int m = 0; m < 10; m++) Assert.Equal(0.0, figured[m], 12);
        }

        // Fourth-order figuring does not change that either.
        var withC1 = TertiaryCubics.DCubic(k, c1: 5e-3, c2: 3e-4, c0: 0.0, y: 1.0, v: 0.7);
        for (int m = 0; m < 10; m++) Assert.Equal(0.0, withC1[m], 12);
    }
}
