using System;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The ten script-T polynomials of Buchdahl's paper II - <i>J. Opt. Soc. Am.</i> <b>48</b>,
/// 563 (1958) - which carry the tertiary intrinsic contribution of one surface, and the
/// machinery that derives them from his Tables II and III.
///
/// <para><b>Why this exists alongside <see cref="BuchdahlTableI"/>.</b> That class
/// implements the CONDENSED scheme of paper III, which reaches the same coefficients in
/// far fewer entries but is spherical-only as implemented. Its aspheric extension is
/// monograph Sec. 85, which is NOT of doubtful value for this purpose - Buchdahl qualifies
/// that remark with "unless tertiary or higher-order coefficients are to be calculated". The
/// general route runs through the script-T, and the figuring enters it in one identifiable
/// place: the two cubics S3 and X3 that paper II (5.1) is LINEAR in. So this is the way
/// in for aspherics.</para>
///
/// <para><b>The tables are not transcribed on trust.</b> <see cref="Expand"/> carries out
/// the substitution (2.5) that Buchdahl describes but declines to show - "very tedious
/// indeed ... it suffices to quote merely the final result" - and running it on the
/// published tables must reproduce his published (6.2). It does, to machine precision, and
/// that single check covers all 160 integers of Tables II and III, the monomial ordering,
/// the substitution itself, and the restoration of c0. It also found a transcription error
/// that nothing else would have: see <see cref="Published"/>.</para>
/// </summary>
public static class TertiaryScriptT
{
    /// <summary>
    /// Table III of paper II: nu_1..nu_10, coefficients of k^5 down to k^0. These are the
    /// coefficients of the cubic that multiplies y in (5.3).
    /// </summary>
    private static readonly int[,] TableIII =
    {
        {  5,  -8,  11,  -8,   5,  0 },
        {-30,  38, -46,  28, -20,  0 },
        { 15, -11,   8,   3,   0,  3 },
        { 60, -56,  48, -20,  16,  0 },
        {-60,  24,   8, -24,   8, -4 },
        { 15,   2, -13,   5,   2, -3 },
        {-40,  24,   0,   0,   0,  0 },
        { 60,  -4, -52,  12,   0,  0 },
        {-30, -14,  46,  14, -16,  0 },
        {  5,   5, -10, -10,   5,  5 },
    };

    /// <summary>
    /// Table II of paper II: 'nu_1..'nu_6, coefficients of k^4 down to k^0 - the cubic that
    /// multiplies v in (5.3), and equal to 16 X3.
    /// </summary>
    private static readonly int[,] TableII =
    {
        {  3,  -5,   8,  -6,   5 },
        {-12,  12, -18,   8, -14 },
        {  6,  -2,   1,   5,   2 },
        { 12,  -4,   8,   0,   8 },
        {-12,  -4,   4,  -4,   0 },
        {  3,   3,  -3,  -3,   0 },
    };

    /// <summary>
    /// Table IV of paper II: the power of j absorbed into script-T_mu when it becomes
    /// 'script-T_mu. Index 1..10.
    /// </summary>
    public static readonly int[] JPower = { 0, 0, 1, 2, 2, 3, 4, 3, 4, 5, 6 };

    private static Scalar Horner(int[,] table, int row, int columns, Scalar k)
    {
        Scalar s = 0.0;
        for (int c = 0; c < columns; c++) s = s * k + table[row, c];
        return s;
    }

    /// <summary>Table III evaluated at k. Index 0..9.</summary>
    public static Scalar[] Nu(Scalar k)
    {
        var v = new Scalar[10];
        for (int m = 0; m < 10; m++) v[m] = Horner(TableIII, m, 6, k);
        return v;
    }

    /// <summary>Table II evaluated at k. Index 0..5.</summary>
    public static Scalar[] NuPrime(Scalar k)
    {
        var v = new Scalar[6];
        for (int m = 0; m < 6; m++) v[m] = Horner(TableII, m, 5, k);
        return v;
    }

    /// <summary>A polynomial in theta1, theta2, theta3 of total degree at most three.</summary>
    private sealed class Cubic
    {
        public readonly Scalar[,,] C = new Scalar[4, 4, 4];

        public static Cubic One()
        {
            var p = new Cubic();
            p.C[0, 0, 0] = 1.0;
            return p;
        }

        public static Cubic Linear(Scalar a, Scalar b, Scalar c)
        {
            var p = new Cubic();
            p.C[1, 0, 0] = a; p.C[0, 1, 0] = b; p.C[0, 0, 1] = c;
            return p;
        }

        public static Cubic operator *(Cubic x, Cubic y)
        {
            var r = new Cubic();
            for (int a = 0; a < 4; a++)
            for (int b = 0; a + b < 4; b++)
            for (int c = 0; a + b + c < 4; c++)
            {
                Scalar xv = x.C[a, b, c];
                if (SMath.Vanishes(xv)) continue;
                for (int d = 0; a + d < 4; d++)
                for (int e = 0; a + b + d + e < 4; e++)
                for (int f = 0; a + b + c + d + e + f < 4; f++)
                {
                    Scalar yv = y.C[d, e, f];
                    if (SMath.Vanishes(yv)) continue;
                    r.C[a + d, b + e, c + f] += xv * yv;
                }
            }
            return r;
        }

        public void AddScaled(Cubic other, Scalar s)
        {
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            for (int c = 0; c < 4; c++)
                C[a, b, c] += s * other.C[a, b, c];
        }
    }

    // The ten cubic monomials in theta1, theta2, theta3, in the order (6.1) uses.
    private static readonly (int A, int B, int C)[] Monomials =
    {
        (3,0,0), (2,1,0), (2,0,1), (1,2,0), (1,1,1), (1,0,2), (0,3,0), (0,2,1), (0,1,2), (0,0,3),
    };

    /// <summary>
    /// Expands <c>Y*sum(nu_m * m) - v*sum('nu_m * m)</c> over the ten monomials in xi, eta,
    /// zeta, substituting (2.5), and returns the ten coefficients in the theta basis -
    /// which is to say, the script-T of (6.1). Index 1..10.
    ///
    /// <para>The <paramref name="nu"/> and <paramref name="nuPrime"/> are passed in rather
    /// than read from the tables so that the same routine serves both the spherical case,
    /// where they come from Tables III and II, and an aspheric increment, where they come
    /// from the figuring terms of S3 and X3.</para>
    /// </summary>
    /// <param name="Y">
    /// Paper II works at c0 = 1 (its footnote 5), so its y is c0 times ours - and since
    /// i = c0 y - v, that is exactly i + v.
    /// </param>
    public static Scalar[] Expand(Scalar[] nu, Scalar[] nuPrime, Scalar Y, Scalar v)
    {
        if (nu == null) throw new ArgumentNullException(nameof(nu));
        if (nuPrime == null) throw new ArgumentNullException(nameof(nuPrime));

        var coefficients = new Scalar[10];
        for (int m = 0; m < 10; m++)
            coefficients[m] = Y * nu[m] - (m < 6 ? v * nuPrime[m] : 0.0);
        return ExpandCubic(coefficients, Y, v);
    }

    /// <summary>
    /// The same substitution applied to a plain cubic in xi, eta, zeta - its ten
    /// coefficients in, its ten theta coefficients out.
    ///
    /// <para>The D side of the tertiary increment arrives already in the y-nu minus v-nuprime
    /// form that <see cref="Expand"/> takes, because that is how paper II (5.3) writes it.
    /// The L side does not - it is a cubic like any other - so it needs this.</para>
    /// </summary>
    public static Scalar[] ExpandCubic(Scalar[] coefficients, Scalar Y, Scalar v)
        => Accumulate(coefficients,
                      Cubic.Linear(Y * Y, 2 * Y, 1),
                      Cubic.Linear(Y * v, Y + v, 1),
                      Cubic.Linear(v * v, 2 * v, 1));

    /// <summary>
    /// The substitution in PHYSICAL variables, carrying c0 explicitly instead of working at
    /// c0 = 1 and dividing the figuring by powers of it.
    ///
    /// <para>Paper II sets c0 = 1 and restores it at the end, which makes xi carry c0^2 and
    /// eta carry c0 - so the figuring has to be handed to <see cref="ExpandCubic"/> already
    /// divided by c0^3 and c0^5, and a plano surface cannot be carried through it at all.
    /// Rewriting the substitution in terms of theta scaled by the same weights removes every
    /// division:</para>
    ///
    /// <code>
    ///   xi   = y^2 T1 + 2y T2 + T3
    ///   eta  = y v T1 + (c0 y + v) T2 + c0 T3
    ///   zeta = v^2 T1 + 2 c0 v T2 + c0^2 T3
    /// </code>
    ///
    /// <para>The two forms are the same expansion in different coordinates, so they must
    /// agree wherever both are defined, and <c>TertiaryScriptTTests</c> asserts that. The
    /// scaled theta monomial of index mu carries c0 to the power <see cref="JPower"/>[mu] -
    /// which is to say Buchdahl's j carries a factor of c0, and Table IV's powers are those
    /// weights.</para>
    /// </summary>
    public static Scalar[] ExpandCubicPhysical(Scalar[] coefficients, Scalar y, Scalar v, Scalar c0)
        => Accumulate(coefficients,
                      Cubic.Linear(y * y, 2 * y, 1),
                      Cubic.Linear(y * v, c0 * y + v, c0),
                      Cubic.Linear(v * v, 2 * c0 * v, c0 * c0));

    /// <summary>The six quadratic monomials, in the same sequence the cubics use.</summary>
    private static readonly (int A, int B, int C)[] QuadMonomials =
    {
        (2,0,0), (1,1,0), (1,0,1), (0,2,0), (0,1,1), (0,0,2),
    };

    /// <summary>
    /// The SECOND-order counterpart of <see cref="ExpandCubicPhysical"/>: a quadratic in
    /// xi, eta, zeta re-expressed as a quadratic in Buchdahl theta, whose six coefficients are
    /// the secondary quantities.
    ///
    /// <para>Paper II Sec. 2 states the substitution and what it produces - "the coefficients
    /// of the resulting polynomial in theta1, theta2, theta3 correspond exactly to the
    /// secondary quantities" - and it is the same substitution the cubic expansion already
    /// uses, read at one degree lower. The basis matters: the secondary coefficients are NOT
    /// a reweighting of the xi-eta-zeta monomials, which is why comparing them in that basis
    /// gives ratios that are neither constant nor any power law.</para>
    /// </summary>
    public static Scalar[] ExpandQuadraticPhysical(Scalar[] coefficients, Scalar y, Scalar v,
                                                   Scalar c0)
    {
        if (coefficients == null) throw new ArgumentNullException(nameof(coefficients));

        var xi = Cubic.Linear(y * y, 2 * y, 1);
        var eta = Cubic.Linear(y * v, c0 * y + v, c0);
        var zeta = Cubic.Linear(v * v, 2 * c0 * v, c0 * c0);

        var total = new Cubic();
        for (int m = 0; m < 6; m++)
        {
            if (SMath.Vanishes(coefficients[m])) continue;
            var (nx, ne, nz) = QuadMonomials[m];
            var product = Cubic.One();
            for (int t = 0; t < nx; t++) product = product * xi;
            for (int t = 0; t < ne; t++) product = product * eta;
            for (int t = 0; t < nz; t++) product = product * zeta;
            total.AddScaled(product, coefficients[m]);
        }

        var result = new Scalar[6];
        for (int m = 0; m < 6; m++)
        {
            var (a, b, c) = QuadMonomials[m];
            result[m] = total.C[a, b, c];
        }
        return result;
    }

    private static Scalar[] Accumulate(Scalar[] coefficients, Cubic xi, Cubic eta, Cubic zeta)
    {
        if (coefficients == null) throw new ArgumentNullException(nameof(coefficients));

        var total = new Cubic();
        for (int m = 0; m < 10; m++)
        {
            if (SMath.Vanishes(coefficients[m])) continue;
            var (nx, ne, nz) = Monomials[m];      // same exponent pattern in xi, eta, zeta
            var product = Cubic.One();
            for (int t = 0; t < nx; t++) product = product * xi;
            for (int t = 0; t < ne; t++) product = product * eta;
            for (int t = 0; t < nz; t++) product = product * zeta;
            total.AddScaled(product, coefficients[m]);
        }

        var result = new Scalar[11];
        for (int m = 0; m < 10; m++)
        {
            var (a, b, c) = Monomials[m];
            result[m + 1] = total.C[a, b, c];
        }
        return result;
    }

    /// <summary>
    /// Paper II (6.2) as printed, for checking <see cref="Expand"/> against. Index 1..10.
    ///
    /// <para>One coefficient is not as printed. The i^3 term of script-T_4 is taken as
    /// -k(k^2+k+1), which is what expanding his own simplified form (7.3) gives; read
    /// without the k, script-T_4 is the only one of the ten whose normalisation against the
    /// computing scheme's z is wrong, and wrong by exactly k. Both the expansion here and
    /// the scheme agree on the k, so the printed form is a misreading or a slip.</para>
    /// </summary>
    public static Scalar[] Published(Scalar Y, Scalar v, Scalar i, Scalar k)
    {
        Scalar i2 = i * i, i3 = i2 * i, i4 = i3 * i, i5 = i4 * i;
        Scalar v2 = v * v, v3 = v2 * v, v4 = v3 * v, v5 = v4 * v;
        Scalar k2 = k * k, k3 = k2 * k, k4 = k3 * k, k5 = k4 * k;

        var T = new Scalar[11];
        T[1] = Y * i * ((5 * k5 - 8 * k4 + 11 * k3 - 8 * k2 + 5 * k) * i5
                      + (-13 * k4 + 25 * k3 - 28 * k2 + 16 * k - 5) * i4 * v
                      + (4 * k3 - 19 * k2 + 13 * k - 8) * i3 * v2
                      + (11 * k2 - 5 * k + 4) * i2 * v3
                      + (-2 * k + 7) * i * v4 - 5 * v5);
        T[2] = Y * i * (-10 * k * (k3 - 2 * k2 + 2 * k - 1) * i4
                      + (-24 * k2 + 10 * k - 10) * i3 * v
                      + (36 * k2 - 22 * k + 10) * i2 * v2
                      + (-2 * k + 26) * i * v3 - 24 * v4);
        T[3] = i * ((3 * k4 - 5 * k3 + 7 * k2 - 5 * k + 3) * i4
                  + (-8 * k3 + 20 * k2 - 20 * k + 8) * i3 * v
                  + (10 * k2 - 14 * k + 10) * i2 * v2
                  + 4 * (k - 1) * i * v3 - 9 * v4);
        T[4] = 4 * Y * i * (-k * (k2 + k + 1) * i3
                          + (8 * k2 - 6 * k + 1) * i2 * v
                          + (2 * k + 7) * i * v2 - 9 * v3);
        T[5] = 4 * i * (-(2 * k3 - 5 * k2 + 5 * k - 2) * i3
                      + (3 * k2 - 4 * k + 3) * i2 * v
                      + 5 * (k - 1) * i * v2 - 6 * v3);
        T[6] = i * (-(k2 - k + 1) * i2 + 6 * (k - 1) * i * v - 3 * v2);
        T[7] = 8 * Y * i * (k * (k - 1) * i2 + (k + 1) * i * v - 2 * v2);
        T[8] = 4 * i * ((k2 - k + 1) * i2 + 4 * (k - 1) * i * v - 3 * v2);
        T[9] = 6 * (k - 1) * i2;
        T[10] = i;
        return T;
    }

    /// <summary>
    /// The factor that carries a script-T into the computing scheme's z, read off the
    /// scheme itself rather than assembled from N, omega and the powers of c0.
    ///
    /// <para>Paper II absorbs (1/16) N (1-k) i j^r into script-T_mu to make 'script-T_mu,
    /// and z_mu is 'script-T_mu (except that z3 and z4 pair up). Since script-T_10 is
    /// simply i and its j-power is six, the whole factor is z10 / (i^2 j^6) - and the same
    /// number then works for all ten, which is the check
    /// <c>TertiaryScriptTTests</c> makes.</para>
    /// </summary>
    public static Scalar Bridge(Scalar z10, Scalar i, Scalar j)
    {
        Scalar den = i * i * SMath.Pow(j, JPower[10]);
        return SMath.Abs(den) > 1e-300 ? z10 / den : 0.0;
    }
}
