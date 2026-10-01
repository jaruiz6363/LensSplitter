using System;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The two cubics S3 and X3 that carry a surface's tertiary intrinsic contribution, built
/// from the monograph's Chapter IX equations rather than lifted from the tables - so that
/// a FIGURED surface can be handled, which the tables cannot be.
///
/// <para><b>Why not just use Sec. 80.</b> Buchdahl publishes the aspheric increments to
/// sigma3, tau3 and S3 there, but he takes c1 = 0 while doing it, and c1 is zero only for
/// an unfigured sphere - in the notation of this program's own aspheric code,
/// <c>c1 = 8 A4 + conic c0^3</c>. Any surface with a fourth-order figuring term has c1
/// nonzero, and then sigma2 and tau2 carry it too, so S2 shifts as well and Sec. 80's
/// increments are the wrong specialisation. Building the cubics from
/// (77.1)-(77.5), (78.1) and (78.7), which carry every c1 term, avoids the question.</para>
///
/// <para><b>It checks itself.</b> Setting the figuring to zero must reproduce Buchdahl's
/// published Tables I, II and III - 160 integers arrived at by an independent route - so a
/// slip in transcribing any of those equations cannot pass. That is what
/// <c>TertiaryCubicsTests</c> asserts.</para>
///
/// <para><b>Units.</b> Everything here is in Buchdahl's c0 = 1 normalisation, which paper
/// II adopts in its footnote 5 and restores at the end. The figuring coefficients scale
/// with it: c1 by c0^3 and c2 by c0^5, since the theta they belong to are the coefficients
/// of the sag expansion in r^2, r^4, r^6.</para>
/// </summary>
public static class TertiaryCubics
{
    /// <summary>
    /// A polynomial in xi, eta, zeta of total degree at most three, indexed by the
    /// exponents. Only the ten cubic terms are ever read out, but lower degrees have to be
    /// carried because S1 and S2 are built first and then multiplied up.
    /// </summary>
    public sealed class Poly
    {
        public readonly Scalar[,,] C = new Scalar[4, 4, 4];

        public static Poly Scalar(Scalar s)
        {
            var p = new Poly();
            p.C[0, 0, 0] = s;
            return p;
        }

        /// <summary>a*xi + b*eta + c*zeta.</summary>
        public static Poly Linear(Scalar a, Scalar b, Scalar c)
        {
            var p = new Poly();
            p.C[1, 0, 0] = a; p.C[0, 1, 0] = b; p.C[0, 0, 1] = c;
            return p;
        }

        public static Poly operator +(Poly x, Poly y)
        {
            var r = new Poly();
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            for (int c = 0; c < 4; c++)
                r.C[a, b, c] = x.C[a, b, c] + y.C[a, b, c];
            return r;
        }

        public static Poly operator *(Scalar s, Poly x)
        {
            var r = new Poly();
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            for (int c = 0; c < 4; c++)
                r.C[a, b, c] = s * x.C[a, b, c];
            return r;
        }

        public static Poly operator *(Poly x, Poly y)
        {
            var r = new Poly();
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
    }

    /// <summary>
    /// The ten cubic monomials in the order Buchdahl's tables use: xi^3, xi^2 eta,
    /// xi^2 zeta, xi eta^2, xi eta zeta, xi zeta^2, eta^3, eta^2 zeta, eta zeta^2, zeta^3.
    /// </summary>
    private static readonly (int A, int B, int C)[] Cubics =
    {
        (3,0,0), (2,1,0), (2,0,1), (1,2,0), (1,1,1), (1,0,2), (0,3,0), (0,2,1), (0,1,2), (0,0,3),
    };

    /// <summary>Reads the ten cubic coefficients out of a polynomial, in table order.</summary>
    public static Scalar[] CubicPart(Poly p)
    {
        var v = new Scalar[10];
        for (int m = 0; m < 10; m++)
        {
            var (a, b, c) = Cubics[m];
            v[m] = p.C[a, b, c];
        }
        return v;
    }

    /// <summary>
    /// Everything one surface contributes, at c0 = 1.
    /// </summary>
    /// <param name="k">n/n' at the surface.</param>
    /// <param name="c1">Fourth-order figuring, <c>8 A4 + conic c0^3</c>, divided by c0^3.</param>
    /// <param name="c2">Sixth-order figuring, divided by c0^5.</param>
    public sealed class Surface
    {
        public Poly S1 = new(), S2 = new(), S3 = new(), X3 = new();

        /// <summary>The sigma and tau of (77.4-5), kept so that the identity (78.9) can be
        /// checked against them - it is an independently derived route to the same pair.</summary>
        public Poly Sigma2 = new(), Sigma3 = new(), Tau2 = new(), Tau3 = new();

        /// <summary>The figuring polynomials of (77.2) and the beta of (78.7), which the
        /// L side of the tertiary increment is built from. All zero for a sphere.</summary>
        public Poly G1 = new(), G2 = new(), G3 = new(), B1 = new(), B2 = new();

        public Surface(Scalar k, Scalar c1, Scalar c2) : this(k, c1, c2, 1.0, default) { }

        public Surface(Scalar k, Scalar c1, Scalar c2, Scalar c0) : this(k, c1, c2, c0, default) { }

        public Surface(Scalar k, Scalar c1, Scalar c2, Scalar c0, Scalar c3)
        {
            Scalar th1 = c0 / 2.0;                     // theta1 = c0/2, from (77.1) and Sec. 79
            Scalar th2 = th1 * th1, th3 = th2 * th1, th4 = th3 * th1, th5 = th4 * th1, th6 = th5 * th1;
            Scalar k2 = k * k;

            Scalar sigma0 = -1.0 / (2.0 * k);
            Scalar tau0 = (1.0 - k) / (2.0 * k);
            Scalar ms = -sigma0, mt = -tau0;           // the leading -sigma0, -tau0 of (77.4-5)

            // (77.4)
            var sigma1 = ms * Poly.Linear(0.5 * c0 * c0, -k2 * c0, k2 - 1.0);
            var sigma2 = ms * Quad(1.5 * th1 * c1 + 2.0 * th4, -(k2 * c1 + 4.0 * th3), 2.0 * th2,
                                   0.0, 0.0, 0.0);
            var sigma3 = ms * Cubic(4.0 / 3.0 * th1 * c2 + 4.0 * th3 * c1 + 0.25 * c1 * c1 + 4.0 * th6,
                                    -(k2 * c2 + (k2 + 7.0) * th2 * c1 + 12.0 * th5),
                                    1.5 * th1 * c1 + 4.0 * th4,
                                    k2 * c0 * c1 + 8.0 * th4,
                                    -4.0 * th3,
                                    0.0);

            // (77.5)
            var tau1 = mt * Poly.Linear((k2 + 1.0) * c0 * c0, -2.0 * k2 * c0, k2 - 1.0);
            var tau2 = mt * Quad((4.0 * k2 + 3.0) * th1 * c1, -(2.0 * k2 * c1 + c0 * c0 * c0),
                                 c0 * c0, 0.0, 0.0, 0.0);
            var tau3 = mt * Cubic((2.0 * k2 + 4.0 / 3.0) * c0 * c2 + (k2 + 0.5) * c1 * c1
                                      + (4.0 * k2 + 2.0) * th3 * c1,
                                  -(2.0 * k2 * c2 + (10.0 * k2 + 14.0) * th2 * c1 + 8.0 * th5),
                                  3.0 * th1 * c1 + 4.0 * th4,
                                  2.0 * k2 * c0 * c1 + c0 * c0 * c0 * c0,
                                  -c0 * c0 * c0,
                                  0.0);

            Sigma2 = sigma2; Sigma3 = sigma3; Tau2 = tau2; Tau3 = tau3;

            // (78.1)
            S1 = tau1 + 2.0 * sigma1 + Poly.Linear(0.0, 0.0, (1.0 + k) / (2.0 * k));
            S2 = tau0 * (S1 * S1) + 2.0 * ((tau1 + sigma1) * S1) + (tau2 + 2.0 * sigma2);
            S3 = 2.0 * ((tau0 * S1 + tau1 + sigma1) * S2)
               + ((tau1 * S1) + 2.0 * tau2 + 2.0 * sigma2) * S1
               + (tau3 + 2.0 * sigma3);

            // (77.2). c3 reaches neither S3 nor X3 - it enters only through gamma3, which is
            // why the D side never sees it and the L side does.
            var gamma1 = Poly.Linear(c1, 0.0, 0.0);
            var gamma2 = Quad(c2 + th2 * c1, -c0 * c1, 0.0, 0.0, 0.0, 0.0);
            // Exactly as (77.2) prints it, read at magnification: theta1 c1^2 on xi^3 and
            // (1/2)c1^2 on xi^2 eta.
            //
            // The aspheric tertiary was wrong in its c1-squared part for a long time, and the
            // fault was in neither of these. Every c1-squared coefficient in the derivation
            // was checked against the page and every one matched - sigma3's (1/4)c1^2 by
            // (77.4), tau3's (k^2+1/2)c1^2 by (77.5), c3's -(3/4)c0 c1^2 by (56.5), and these
            // two by (77.2). What was wrong was the c being fed to them: twice Buchdahl's,
            // which scales terms linear in c by two and c1-squared terms by FOUR. That is why
            // no single factor ever fixed both, and why searching this file kept turning up
            // nothing. See the note in Figuring.From.
            //
            // They were also carried at HALF for a while, copied across from the fifth-order
            // code where a parabola proves the halved form right. That copy was never
            // justified here - the two assemblies differ, the fifth-order code putting its
            // 1/2 on the gamma1 and gamma2 products and not on gamma3, and (78.4) putting no
            // 1/2 anywhere - and it is undone.
            var gamma3 = Cubic(c3 + c0 * c0 * c2 / 3.0 + th1 * c1 * c1 + 2.0 * th4 * c1,
                               -(2.0 * c0 * c2 + 6.0 * th3 * c1 + 0.5 * c1 * c1),
                               th2 * c1,
                               c0 * c0 * c1,
                               0.0, 0.0);

            // (77.1), with theta2 and theta3 in closed form. Solving (78.7) for beta gives
            // -(p + ...)/c0, and the c0 cancels analytically - but not numerically, which
            // would put a division by zero on every plano surface. Carrying out the
            // cancellation once, by hand, gives the theta below; the c1^2 terms drop out of
            // theta3 exactly. Nothing is assumed by doing so: the Table I, II and III tests
            // pass through beta, so a wrong theta could not survive them.
            Scalar c0sq = c0 * c0, c0fifth = c0sq * c0sq * c0;
            Scalar theta2 = c0sq * c0 / 8.0 + c1 / 4.0;
            Scalar theta3 = c0fifth / 16.0 + c0sq * c1 / 4.0 + c2 / 6.0;
            var beta1 = Poly.Linear(th1, 0.0, 0.0);
            var beta2 = Quad(theta2, -2.0 * th2, 0.0, 0.0, 0.0, 0.0);
            var beta3 = Cubic(theta3, -6.0 * th1 * theta2, th3, 4.0 * th3, 0.0, 0.0);

            // Paper II (4.1)
            X3 = beta1 * S2 + beta2 * S1 + beta3;

            G1 = gamma1; G2 = gamma2; G3 = gamma3;
            B1 = beta1;  B2 = beta2;
        }

        private static Poly Quad(Scalar xx, Scalar xe, Scalar xz, Scalar ee, Scalar ez, Scalar zz)
        {
            var p = new Poly();
            p.C[2, 0, 0] = xx; p.C[1, 1, 0] = xe; p.C[1, 0, 1] = xz;
            p.C[0, 2, 0] = ee; p.C[0, 1, 1] = ez; p.C[0, 0, 2] = zz;
            return p;
        }

        private static Poly Cubic(Scalar xxx, Scalar xxe, Scalar xxz, Scalar xee, Scalar xez, Scalar xzz)
        {
            var p = new Poly();
            p.C[3, 0, 0] = xxx; p.C[2, 1, 0] = xxe; p.C[2, 0, 1] = xxz;
            p.C[1, 2, 0] = xee; p.C[1, 1, 1] = xez; p.C[1, 0, 2] = xzz;
            return p;
        }
    }

    /// <summary>The ten coefficients of 16 S3 - Buchdahl's lambda, Table I. Index 0..9.</summary>
    public static Scalar[] Lambda(Scalar k, Scalar c1, Scalar c2) => Lambda(k, c1, c2, 1.0);

    /// <summary>The same, for a surface whose leading figuring coefficient is given.</summary>
    public static Scalar[] Lambda(Scalar k, Scalar c1, Scalar c2, Scalar c0)
    {
        var s = new Surface(k, c1, c2, c0);
        var v = CubicPart(s.S3);
        for (int m = 0; m < 10; m++) v[m] *= 16.0;
        return v;
    }

    /// <summary>The six coefficients of 16 X3 - Buchdahl's 'nu, Table II. Index 0..5.</summary>
    public static Scalar[] NuPrime(Scalar k, Scalar c1, Scalar c2) => NuPrime(k, c1, c2, 1.0);

    /// <summary>The same, for a surface whose leading figuring coefficient is given.</summary>
    public static Scalar[] NuPrime(Scalar k, Scalar c1, Scalar c2, Scalar c0)
    {
        var s = new Surface(k, c1, c2, c0);
        var v = CubicPart(s.X3);
        var r = new Scalar[6];
        for (int m = 0; m < 6; m++) r[m] = 16.0 * v[m];
        return r;
    }

    /// <summary>
    /// The ten coefficients of the cubic multiplying y in paper II (5.3) - Buchdahl's nu,
    /// Table III. Index 0..9.
    ///
    /// <para>From (5.2) against (5.3), <c>nu = [lambda + (k-1) 'nu] / k</c>. That relation
    /// was checked against the printed tables before being relied on.</para>
    /// </summary>
    public static Scalar[] Nu(Scalar k, Scalar c1, Scalar c2)
    {
        var lambda = Lambda(k, c1, c2);
        var nuPrime = NuPrime(k, c1, c2);
        var v = new Scalar[10];
        for (int m = 0; m < 10; m++)
        {
            Scalar p = m < 6 ? nuPrime[m] : 0.0;
            v[m] = (lambda[m] + (k - 1.0) * p) / k;
        }
        return v;
    }

    /// <summary>
    /// What the figuring adds to nu and 'nu: the figured surface less the same surface
    /// unfigured. These are what feed <see cref="TertiaryScriptT.Expand"/> to give the
    /// aspheric increment to the script-T.
    /// </summary>
    public static (Scalar[] Nu, Scalar[] NuPrime) Increment(Scalar k, Scalar c1, Scalar c2)
    {
        var nu = Nu(k, c1, c2);
        var nu0 = Nu(k, 0.0, 0.0);
        var np = NuPrime(k, c1, c2);
        var np0 = NuPrime(k, 0.0, 0.0);

        var dn = new Scalar[10];
        for (int m = 0; m < 10; m++) dn[m] = nu[m] - nu0[m];
        var dp = new Scalar[6];
        for (int m = 0; m < 6; m++) dp[m] = np[m] - np0[m];
        return (dn, dp);
    }

    /// <summary>
    /// The cubic whose theta expansion gives the script-T - which is
    /// <c>16 D(3) / (N(1-k))</c>, written by paper II (5.2) as <c>(16/k)[y S3 - v' X3]</c>
    /// once v' is replaced by (1-k)y + kv.
    ///
    /// <para>Everything here is physical: c0 is carried rather than normalised away, so a
    /// plano surface goes through unharmed. At c0 = 0 the y term drops out entirely and the
    /// cubic is simply -v X3, which is not zero when the surface is figured.</para>
    /// </summary>
    public static Scalar[] DCubic(Scalar k, Scalar c1, Scalar c2, Scalar c0, Scalar y, Scalar v)
    {
        var lambda = Lambda(k, c1, c2, c0);          // 16 S3
        var nuPrime = NuPrime(k, c1, c2, c0);        // 16 X3
        Scalar Y = c0 * y;
        Scalar vPrime = (1.0 - k) * Y + k * v;

        // S3 and X3 do not carry the same power of c0. Converting a cubic between the
        // unit-curvature form and this one costs c0 to the monomial weight for S3, and one
        // less than that for X3 - beta1 is theta1 xi = (c0/2) xi where the unit-curvature
        // form has xi-hat/2, and the same offset runs through beta2 and beta3. So the X3
        // term carries an explicit c0 here. It is a multiplication, not a division, which is
        // the whole point of working in these variables.
        var cubic = new Scalar[10];
        for (int m = 0; m < 10; m++)
            cubic[m] = (Y * lambda[m] - (m < 6 ? c0 * vPrime * nuPrime[m] : 0.0)) / k;
        return cubic;
    }

    /// <summary>
    /// The cubic whose theta expansion gives the L side of the tertiary increment:
    /// <c>16 L(3) / (N(1-k))</c>, from (76.4) with (78.2-3) and the gamma of (77.2).
    ///
    /// <para>Physical throughout, and weighted to match <see cref="DCubic"/> so that the
    /// same expansion consumes both. Getting that right is the whole difficulty: gamma
    /// carries one more power of c0 than S3 does and X carries one less, so the D(1) and
    /// D(2) inside are formed with y rather than Y and without the c0 that DCubic needs.
    /// Every factor is then finite at c0 = 0, which is what lets a figured plate through.</para>
    ///
    /// <para>L vanishes for an unfigured surface, every term being proportional to a
    /// figuring coefficient - which is exactly why paper II, treating only spherical
    /// surfaces, writes its (2.1) as ΔΛ = D I with no L term at all.</para>
    /// </summary>
    public static Scalar[] LCubic(
        Scalar k, Scalar c1, Scalar c2, Scalar c3, Scalar c0, Scalar y, Scalar v)
    {
        var s = new Surface(k, c1, c2, c0, c3);
        Scalar vPrime = (1.0 - k) * c0 * y + k * v;

        // D(n)/(N(1-k)), divided by c0 so that the product with gamma lands at the same
        // weight as DCubic. Both parts stay finite as c0 goes to zero.
        var d1 = (1.0 / k) * (y * s.S1 + (-vPrime) * s.B1);
        var d2 = (1.0 / k) * (y * s.S2 + (-vPrime) * (s.B2 + s.B1 * s.S1));

        var l3 = (1.0 / k) * (y * s.G3) + s.G2 * d1 + s.G1 * d2;

        var cubic = CubicPart(l3);
        for (int m = 0; m < 10; m++) cubic[m] *= 16.0;
        return cubic;
    }

    /// <summary>
    /// The six quadratic monomials in the same order the cubics use: xi^2, xi eta, xi zeta,
    /// eta^2, eta zeta, zeta^2.
    /// </summary>
    private static readonly (int A, int B, int C)[] Quadratics =
    {
        (2,0,0), (1,1,0), (1,0,1), (0,2,0), (0,1,1), (0,0,2),
    };

    /// <summary>Reads the six quadratic coefficients out of a polynomial, in table order.</summary>
    public static Scalar[] QuadraticPart(Poly p)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        var v = new Scalar[6];
        for (int m = 0; m < 6; m++)
        {
            var (a, b, c) = Quadratics[m];
            v[m] = p.C[a, b, c];
        }
        return v;
    }

    /// <summary>
    /// The two halves of the SECOND-order intrinsic contribution: what the figuring adds to
    /// D, and the whole of L.
    ///
    /// <para><b>Why this exists.</b> Every aspheric split in this program was built on the
    /// reading that a sphere contributes through D and the figuring through L, so that the
    /// figuring travels on the height ratio while the sphere travels on the incidence ratio.
    /// M (65.5) says that of the FIRST order and only the first: <c>D(1) = 0D(1)</c>, no
    /// figuring in D at all. At second order (65.6) gives
    /// <c>D(2) = 0D(2) + gamma6 L(1) - (1/4) cbar1 v0 gamma5^2</c>, and both added terms carry
    /// the figuring linearly. They are visible one section earlier, in (64.2-3), where S(2)
    /// carries <c>c1 gamma5 gamma6</c> and X(2) carries <c>(1/4)(c1 - (3/2)c0^3) gamma5^2</c>;
    /// gamma5 is not a figuring quantity that might make them vanish, being the aperture
    /// variable xi itself by (64.1).</para>
    ///
    /// <para>Both halves are formed in IDENTICAL units, straight from (65.1-2) with the common
    /// Nbar dropped, so that their ratio carries no weighting convention:
    /// <c>D(2) = y S(2) - v (X(2))</c> and <c>L(2) = y (C(2) + C(1) S(1)) - v C(1) X(1)</c>,
    /// with <c>X(2) = B2 + B1 S(1)</c>. Only the ratio is used - the absolute scale of the
    /// aspheric secondary is already fixed by the bridge in
    /// <see cref="AsphericSchemeIncrements"/> - so anything common to the two cancels.</para>
    /// </summary>
    public static (Scalar[] D, Scalar[] L) SecondaryHalves(
        Scalar k, Scalar c1, Scalar c2, Scalar c0, Scalar y, Scalar v)
    {
        Scalar vPrime = (1.0 - k) * c0 * y + k * v;

        Poly SecondD(Scalar a1, Scalar a2)
        {
            var s = new Surface(k, a1, a2, c0);
            var x2 = s.B2 + s.B1 * s.S1;                 // X(2), by (63.4)
            return y * s.S2 + (-vPrime) * x2;
        }

        var f = new Surface(k, c1, c2, c0);
        var l2 = y * (f.G2 + f.G1 * f.S1) + (-vPrime) * (f.G1 * f.B1);
        var dInc = SecondD(c1, c2) + (-1.0) * SecondD(0.0, 0.0);

        return (QuadraticPart(dInc), QuadraticPart(l2));
    }

    /// <summary>
    /// The bare spherical D(2), same units as <see cref="SecondaryHalves"/>. Exposed so that
    /// the monomial ordering can be checked against the scheme, which is the one thing about
    /// the quadratic basis that cannot be taken on trust: the six quadratics have to line up
    /// with the scheme s1..s6 in the SAME sequence, and on a spherical system the ratio
    /// between them must be a single constant if they do.
    /// </summary>
    public static Scalar[] SecondaryDSpherical(Scalar k, Scalar c0, Scalar y, Scalar v)
    {
        Scalar vPrime = (1.0 - k) * c0 * y + k * v;
        var s = new Surface(k, 0.0, 0.0, c0);
        var x2 = s.B2 + s.B1 * s.S1;
        return QuadraticPart(y * s.S2 + (-vPrime) * x2);
    }

    /// <summary>What the figuring adds to <see cref="DCubic"/>: figured less unfigured.</summary>
    public static Scalar[] DCubicIncrement(
        Scalar k, Scalar c1, Scalar c2, Scalar c0, Scalar y, Scalar v)
    {
        var figured = DCubic(k, c1, c2, c0, y, v);
        var bare = DCubic(k, 0.0, 0.0, c0, y, v);
        for (int m = 0; m < 10; m++) figured[m] -= bare[m];
        return figured;
    }

    /// <summary>
    /// <see cref="DCubic"/> with one power of the curvature divided out, which costs nothing
    /// because both of its terms carry <c>c0</c> explicitly - <c>Y = c0 y</c> in the first and
    /// <c>c0 v"</c> in the second. Removing it by hand rather than dividing afterwards is what
    /// lets a PLANO figured surface through: the scheme wants <c>D/c0</c>, and forming D first
    /// and dividing gives 0/0 on a flat surface even though the ratio is perfectly finite.
    ///
    /// <para>Everything left - lambda, nu-prime, v" - is finite at <c>c0 = 0</c>, which is the
    /// same property <see cref="LCubic"/> already relies on.</para>
    /// </summary>
    public static Scalar[] DCubicOverC0(Scalar k, Scalar c1, Scalar c2, Scalar c0,
                                        Scalar y, Scalar v)
    {
        var lambda = Lambda(k, c1, c2, c0);
        var nuPrime = NuPrime(k, c1, c2, c0);
        Scalar vPrime = (1.0 - k) * c0 * y + k * v;

        var cubic = new Scalar[10];
        for (int m = 0; m < 10; m++)
            cubic[m] = (y * lambda[m] - (m < 6 ? vPrime * nuPrime[m] : 0.0)) / k;
        return cubic;
    }

    /// <summary>
    /// The figuring's increment to <see cref="DCubicOverC0"/>. The expansion that consumes it
    /// is linear in the cubic coefficients, so expanding this is the same as expanding
    /// <see cref="DCubicIncrement"/> and dividing the result by <c>c0</c> - except that it
    /// survives <c>c0 = 0</c>.
    /// </summary>
    public static Scalar[] DCubicIncrementOverC0(
        Scalar k, Scalar c1, Scalar c2, Scalar c0, Scalar y, Scalar v)
    {
        var figured = DCubicOverC0(k, c1, c2, c0, y, v);
        var bare = DCubicOverC0(k, 0.0, 0.0, c0, y, v);
        for (int m = 0; m < 10; m++) figured[m] -= bare[m];
        return figured;
    }

    /// <summary>
    /// A surface's figuring: Buchdahl's c1, c2 and c3, formed exactly as this program's
    /// fifth-order code forms them, because they are the same quantities. All three are zero
    /// for an unfigured sphere, and c1 alone is zero for what Buchdahl calls a figured
    /// sphere - the class of figuring that leaves the primary aberrations untouched. No
    /// conicoid is one of those, so a conic has c1 nonzero.
    ///
    /// <para>They are returned scaled to the unit-focal-length system the scheme works in,
    /// where the curvature is multiplied by the focal length: c1 goes with f^3, c2 with f^5
    /// and c3 with f^7, which keeps c1/c0^3 and its fellows invariant.</para>
    ///
    /// <para>Nothing is divided by the curvature, so a plano asphere passes through.</para>
    /// </summary>
    public readonly struct Figuring
    {
        public readonly Scalar C1, C2, C3;
        public readonly bool Present;

        private Figuring(Scalar c1, Scalar c2, Scalar c3, bool present)
        {
            C1 = c1; C2 = c2; C3 = c3; Present = present;
        }

        public static readonly Figuring None = new(0.0, 0.0, 0.0, false);

        /// <summary>
        /// Figuring coefficients for a surface ALREADY in vertex form - see
        /// <c>Surface.VertexForm</c>, which folds any r-squared coefficient into the curvature
        /// before this is reached. Passing a raw curvature alongside a raw r-squared term would
        /// measure the figuring from the wrong sphere.
        /// </summary>
        public static Figuring From(Scalar conic, Scalar a4, Scalar a6, Scalar a8,
                                    Scalar curvature, Scalar scale)
        {
            // Vanishes rather than a magnitude test and an == 0.0, for the reason set out on
            // Surface.IsFigured: under differentiation a figuring term of exactly zero that the
            // optimiser is moving has a derivative, and returning None here would drop the whole
            // tertiary figuring contribution from the gradient while leaving its value right.
            if (SMath.Vanishes(conic, 1e-14) && SMath.Vanishes(a4)
                && SMath.Vanishes(a6) && SMath.Vanishes(a8)) return None;

            Scalar cv = curvature, cv2 = cv * cv, cv3 = cv2 * cv;
            Scalar c1 = 8.0 * a4 + conic * cv3;
            Scalar c2 = 12.0 * a6
                      + 0.75 * cv2 * (cv3 * conic * (conic + 2.0) - 2.0 * c1);
            Scalar t = cv3 * conic * (conic * conic + 3.0 * conic + 3.0) - 3.0 * c1;
            t = cv2 * (5.0 * cv2 * t - 12.0 * c2);
            t = (-6.0 * cv * c1 * c1 + t) / 8.0;
            Scalar c3 = 16.0 * a8 + t;

            // HALVED, because the three lines above are the fifth-order code's convention and
            // this file wants Buchdahl's. (56.5) defines c1 = 4(th2 - th1^3) with th2 the r^4
            // coefficient of the sag and th1 = c0/2; expanding a conic gives
            // th2 = (1+conic)c0^3/8 + A4, whence c1 = 4 A4 + conic c0^3/2 - half of the
            // 8 A4 + conic c0^3 above. The same factor of two runs through c2 and c3, checked
            // term by term against (56.5), c3's -(3/4)c0 c1^2 included.
            //
            // The two conventions cannot be mixed, and mixing them is what made the aspheric
            // tertiary wrong. (77.2), (77.4) and (77.5) are written in Buchdahl's c, so
            // feeding them doubled values scales every term LINEAR in c by two and every
            // c1-squared term by FOUR. No single factor can undo that, which is exactly why
            // the sweep over this file found nothing that fixed the quadratic without
            // breaking the linear part. theta2 and theta3 below are already Buchdahl's, taken
            // straight from (56.51), so they were being fed the wrong c as well.
            Scalar f2 = scale * scale, f3 = f2 * scale;
            return new Figuring(0.5 * c1 * f3, 0.5 * c2 * f3 * f2, 0.5 * c3 * f3 * f2 * f2, true);
        }
    }
}
