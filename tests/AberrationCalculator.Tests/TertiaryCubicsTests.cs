using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The cubics S3 and X3, built from the monograph's equations, against the tables Buchdahl
/// publishes for them in paper II.
///
/// <para>This is the test that makes the aspheric route safe. The construction takes six
/// transcribed equations - (77.2), (77.3), (77.4), (77.5), (78.1), (78.7) - and combines
/// them into two cubics. Zeroing the figuring must then land exactly on Tables I, II and
/// III, which are 160 published integers reached by a completely different route. A slip
/// in any one of the six cannot survive that.</para>
/// </summary>
public class TertiaryCubicsTests
{
    // Table I: lambda_1..lambda_10, coefficients of k^6 down to k^0.
    private static readonly int[,] TableI =
    {
        {  5, -11,  19, -21,  19, -11,   5 },
        {-30,  50, -70,  58, -46,  22, -14 },
        { 15, -17,  16,   0,  -4,   6,   2 },
        { 60, -68,  64, -32,  24,  -8,   8 },
        {-60,  36,   0, -32,  16,  -8,   0 },
        { 15,  -1, -13,  11,   2,  -6,   0 },
        {-40,  24,   0,   0,   0,   0,   0 },
        { 60,  -4, -52,  12,   0,   0,   0 },
        {-30, -14,  46,  14, -16,   0,   0 },
        {  5,   5, -10, -10,   5,   5,   0 },
    };

    private static double Horner(int[,] table, int row, int columns, double k)
    {
        double s = 0.0;
        for (int c = 0; c < columns; c++) s = s * k + table[row, c];
        return s;
    }

    private static void Same(double expected, double actual, string what)
    {
        double scale = Math.Max(Math.Abs(expected), 1.0);
        Assert.True(Math.Abs(expected - actual) / scale < 1e-10,
            $"{what}: expected {expected:G12}, built {actual:G12}");
    }

    /// <summary>
    /// An unfigured surface must reproduce Table I - the ten coefficients of 16 S3.
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.618735)]
    [InlineData(1.0)]
    [InlineData(1.6162)]
    [InlineData(2.5)]
    public void TheSphericalCubicReproducesTableI(double k)
    {
        var lambda = TertiaryCubics.Lambda(k, 0.0, 0.0);
        for (int m = 0; m < 10; m++)
            Same(Horner(TableI, m, 7, k), lambda[m], $"lambda{m + 1} at k={k}");
    }

    /// <summary>And Table II - the six coefficients of 16 X3.</summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.618735)]
    [InlineData(1.0)]
    [InlineData(1.6162)]
    [InlineData(2.5)]
    public void TheSphericalCubicReproducesTableII(double k)
    {
        var built = TertiaryCubics.NuPrime(k, 0.0, 0.0);
        var published = TertiaryScriptT.NuPrime(k);
        for (int m = 0; m < 6; m++)
            Same(published[m], built[m], $"'nu{m + 1} at k={k}");
    }

    /// <summary>And Table III, which is the combination the script-T expansion consumes.</summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.618735)]
    [InlineData(1.0)]
    [InlineData(1.6162)]
    [InlineData(2.5)]
    public void TheSphericalCubicReproducesTableIII(double k)
    {
        var built = TertiaryCubics.Nu(k, 0.0, 0.0);
        var published = TertiaryScriptT.Nu(k);
        for (int m = 0; m < 10; m++)
            Same(published[m], built[m], $"nu{m + 1} at k={k}");
    }

    /// <summary>
    /// The whole chain, end to end: an unfigured surface built from the monograph's
    /// equations, run through the expansion, must give paper II's published script-T. This
    /// is the two halves - <see cref="TertiaryCubics"/> and <see cref="TertiaryScriptT"/> -
    /// meeting in the middle without the tables between them.
    /// </summary>
    [Theory]
    [InlineData(0.618735, 4.824392, 0.0)]
    [InlineData(1.6162, -2.5374409, 1.8393703)]
    [InlineData(0.8, -1.3, 2.1)]
    public void TheSphericalChainGivesPaperIIScriptT(double k, double i, double v)
    {
        double Y = i + v;
        var mine = TertiaryScriptT.Expand(TertiaryCubics.Nu(k, 0.0, 0.0),
                                          TertiaryCubics.NuPrime(k, 0.0, 0.0), Y, v);
        var his = TertiaryScriptT.Published(Y, v, i, k);
        for (int m = 1; m <= 10; m++)
        {
            double scale = Math.Max(Math.Abs(his[m]), 1e-6);
            Assert.True(Math.Abs(mine[m] - his[m]) / scale < 1e-10,
                $"script-T {m}: chain {mine[m]:G10}, published {his[m]:G10}");
        }
    }

    /// <summary>
    /// An unfigured surface must contribute no increment at all. Trivial, but it is the
    /// thing that would break first if the figuring were ever wired in with a sign or an
    /// offset wrong.
    /// </summary>
    [Fact]
    public void AnUnfiguredSurfaceHasNoIncrement()
    {
        var (nu, nuPrime) = TertiaryCubics.Increment(1.6162, 0.0, 0.0);
        foreach (double x in nu) Assert.Equal(0.0, x, 12);
        foreach (double x in nuPrime) Assert.Equal(0.0, x, 12);
    }

    /// <summary>
    /// Figuring must actually reach both cubics. Sec. 80, taken at face value, says the
    /// fourth-order term c1 does not touch S2 - but that is its c1 = 0 case, and with c1
    /// present S2 shifts as well, which is the whole reason this class exists rather than
    /// the two published increments.
    /// </summary>
    [Fact]
    public void FourthOrderFiguringReachesBothCubics()
    {
        var (nu, nuPrime) = TertiaryCubics.Increment(1.6162, c1: 0.01, c2: 0.0);

        double nuMax = 0.0, npMax = 0.0;
        foreach (double x in nu) nuMax = Math.Max(nuMax, Math.Abs(x));
        foreach (double x in nuPrime) npMax = Math.Max(npMax, Math.Abs(x));

        Assert.True(nuMax > 1e-9, "c1 left nu untouched");
        Assert.True(npMax > 1e-9, "c1 left 'nu untouched");
    }

    /// <summary>
    /// The c0-weight of each monomial. Buchdahl's tables are written at c0 = 1, and the
    /// physical cubic differs from that form by one power of c0 for each power of eta and
    /// two for each power of xi - which is to say xi carries c0^2 and eta carries c0, zeta
    /// carrying none.
    ///
    /// <para>This is worth pinning because it is the reason the figuring has to be handed
    /// in already divided by c0^3 and c0^5, and therefore the reason a plano asphere cannot
    /// be carried through this form at all.</para>
    /// </summary>
    [Theory]
    [InlineData(0.618735, 2.0)]
    [InlineData(0.618735, 0.5)]
    [InlineData(1.6162, 0.1)]
    [InlineData(1.6162, 3.0)]
    public void TheSphericalCubicScalesWithC0AsTheMonomialWeight(double k, double c0)
    {
        int[] weight = { 6, 5, 4, 4, 3, 2, 3, 2, 1, 0 };
        var unit = TertiaryCubics.Lambda(k, 0.0, 0.0, 1.0);
        var scaled = TertiaryCubics.Lambda(k, 0.0, 0.0, c0);

        for (int m = 0; m < 10; m++)
            Same(unit[m] * Math.Pow(c0, weight[m]), scaled[m], $"lambda{m + 1} at c0={c0}");
    }

    /// <summary>
    /// The gamma of (77.2) are what the L side of the tertiary increment is built from, and
    /// every one of them is proportional to a figuring coefficient. So they all vanish for a
    /// sphere - which is exactly why paper II, treating only spherical surfaces, can write
    /// its (2.1) as ΔΛ = D I with no L term at all.
    /// </summary>
    [Fact]
    public void TheGammaVanishForASphere()
    {
        var s = new TertiaryCubics.Surface(1.6162, 0.0, 0.0);
        foreach (var g in new[] { s.G1, s.G2, s.G3 })
            foreach (double x in TertiaryCubics.CubicPart(g))
                Assert.Equal(0.0, x, 14);
    }

    /// <summary>
    /// The eighth-order figuring reaches gamma3 and nothing else. That is what puts it
    /// beyond the D side entirely: S3 and X3 never see it, so it can only arrive through
    /// L(3). (73.7) says the same thing from the other end, giving a figured plate's
    /// tertiary spherical coefficient as phi_3.
    /// </summary>
    [Fact]
    public void TheEighthOrderFiguringReachesOnlyGamma3()
    {
        var bare = new TertiaryCubics.Surface(1.6162, 0.3, 0.2);
        var withC3 = new TertiaryCubics.Surface(1.6162, 0.3, 0.2, 1.0, c3: 0.7);

        // S3, X3, gamma1 and gamma2 are untouched.
        foreach (var (a, b) in new[] { (bare.S3, withC3.S3), (bare.X3, withC3.X3),
                                       (bare.G1, withC3.G1), (bare.G2, withC3.G2) })
        {
            var x = TertiaryCubics.CubicPart(a);
            var y = TertiaryCubics.CubicPart(b);
            for (int m = 0; m < 10; m++) Assert.Equal(x[m], y[m], 12);
        }

        // gamma3 moves, and by exactly c3 on the xi^3 term.
        var g0 = TertiaryCubics.CubicPart(bare.G3);
        var g3 = TertiaryCubics.CubicPart(withC3.G3);
        Assert.Equal(0.7, g3[0] - g0[0], 12);
        for (int m = 1; m < 10; m++) Assert.Equal(g0[m], g3[m], 12);
    }
}
