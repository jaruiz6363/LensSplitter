using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Forbes;

/// <summary>
/// A ray trace carried out on power series rather than on numbers — Forbes, JOSA 73, 782 (1983),
/// Sec. 3(a). A general ray is specified by its height and direction at an input base plane, and
/// every intercept and direction downstream is held as
///
/// <code>
///     y_i = S_i y0 + T_i b0 ,      b_i = V_i y0 + W_i b0                          (3.1)
/// </code>
///
/// with <c>S, T, V, W</c> series in the three rotational invariants. One trace therefore yields
/// the aberrations of every order the truncation keeps, for every ray at once.
///
/// <para><b>Aspheres are not a special case here.</b> The <i>i</i>th surface is
/// <c>x = f_i(y . y)</c> with <c>f_i</c> a power series whose constant term is the vertex
/// position. A sphere, a conic and an even asphere differ only in the coefficients of
/// <c>f_i</c> and run through identical code. There is no split into halves, no second pass and
/// no ratio to carry a figured half on — see <c>docs/forbes.md</c> for why that is the reason
/// this route exists.</para>
/// </summary>
public sealed class ForbesTrace
{
    /// <summary>Height at the output base plane: <c>y_out = S y0 + T b0</c>.</summary>
    public InvariantSeries S { get; }

    /// <summary>Height at the output base plane: <c>y_out = S y0 + T b0</c>.</summary>
    public InvariantSeries T { get; }

    /// <summary>Direction at the output base plane: <c>b_out = V y0 + W b0</c>.</summary>
    public InvariantSeries V { get; }

    /// <summary>Direction at the output base plane: <c>b_out = V y0 + W b0</c>.</summary>
    public InvariantSeries W { get; }

    private ForbesTrace(InvariantSeries s, InvariantSeries t, InvariantSeries v, InvariantSeries w)
    {
        S = s; T = t; V = v; W = w;
    }

    /// <summary>
    /// The figure of one surface as Forbes writes it: <c>f[0]</c> is the vertex position on the
    /// axis and <c>f[j]</c> the coefficient of <c>p^j</c> in the sag, where <c>p = y . y</c>.
    ///
    /// <para>For a conic of curvature <c>c</c> and constant <c>K</c> the sag is
    /// <c>c p / (1 + sqrt(1 - (1 + K) c^2 p))</c>, and an even asphere adds <c>A_j p^j</c>. Both
    /// are built here through <see cref="InvariantSeries"/> in <c>p</c> alone rather than from
    /// hand-expanded coefficients, so the expansion is the one the series tests already
    /// check.</para>
    ///
    /// <para><b>One coefficient beyond the trace.</b> The figure is carried to <c>p^(D+1)</c>
    /// where the trace keeps degree <c>D</c>, because the refraction needs <c>df/dp</c> itself
    /// accurate to degree <c>D</c>: it enters the refracted direction through a factor standing
    /// beside <c>S</c> and <c>T</c> with nothing of degree one or more in front of it, and the
    /// <c>p^D</c> term of <c>df/dp</c> comes from the <c>p^(D+1)</c> term of <c>f</c>. Everywhere
    /// else <c>df/dp</c> is multiplied by a quantity that vanishes with the ray, so degree
    /// <c>D-1</c> would do and the shortfall stays hidden. Carrying one coefficient less costs
    /// exactly the top order: the trace then converges at <c>2D+1</c> rather than <c>2D+3</c>,
    /// which is what the order tests measure.</para>
    /// </summary>
    public static double[] Figure(double vertexZ, double curvature, double conic,
                                  IReadOnlyList<double>? evenAspheric, int degree)
    {
        int build = degree + 1;                 // see the note above on why one degree beyond
        var p = InvariantSeries.P(build);
        var sag = InvariantSeries.Zero(build);

        if (curvature != 0.0)
        {
            var root = (1.0 - (1.0 + conic) * curvature * curvature * p).Sqrt();
            sag += (curvature * p) * (1.0 + root).Inverse();
        }

        if (evenAspheric != null)
        {
            var power = InvariantSeries.Constant(build, 1.0);
            for (int j = 1; j <= build; j++)
            {
                power *= p;
                double a = j - 1 < evenAspheric.Count ? evenAspheric[j - 1] : 0.0;
                if (a != 0.0) sag += power * a;
            }
        }

        var f = new double[build + 1];
        f[0] = vertexZ;
        for (int j = 1; j <= build; j++) f[j] = sag[j, 0, 0];
        return f;
    }

    /// <summary>
    /// Runs the trace.
    /// </summary>
    /// <param name="figures">
    /// Surface figures in order. The first is the input base plane the ray is launched from and
    /// the last the output base plane it is caught on; neither refracts. Everything between is
    /// an optical surface.
    /// </param>
    /// <param name="indices">
    /// Refractive index of each region: <c>indices[i]</c> is the index between surface <c>i</c>
    /// and surface <c>i + 1</c>, so <c>indices[0]</c> is the index the ray starts in.
    /// </param>
    /// <param name="degree">
    /// Highest total degree in the invariants. Three is the tertiary; see
    /// <see cref="InvariantSeries"/> for why.
    /// </param>
    public static ForbesTrace Run(IReadOnlyList<double[]> figures, IReadOnlyList<double> indices,
                                  int degree)
        => Run(figures, indices, degree, null);

    /// <summary>
    /// Runs the trace with selected steps LINEARISED.
    /// </summary>
    /// <param name="aberrating">
    /// One flag per figure: whether the step INTO that figure - the transfer to it and the
    /// refraction at it - is the real one or its paraxial linearisation. Null means all real.
    ///
    /// <para>A linearised step is exactly linear in the ray state, so it propagates whatever
    /// aberration is handed to it without adding any of its own. That is what makes a per-surface
    /// decomposition possible, and it is why the whole step is linearised rather than only the
    /// sag: straight-line transfer is itself nonlinear in the direction cosines, through the
    /// obliquity, so a system with every surface flattened but real transfers would still show
    /// aberration and there would be no zero to measure contributions from. With every step
    /// linearised the trace is the paraxial one and its aberration is identically nothing, which
    /// is the reference the decomposition needs.</para>
    /// </summary>
    public static ForbesTrace Run(IReadOnlyList<double[]> figures, IReadOnlyList<double> indices,
                                  int degree, IReadOnlyList<bool>? aberrating)
    {
        if (figures == null) throw new ArgumentNullException(nameof(figures));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (figures.Count < 2)
            throw new ArgumentException("need at least an input and an output base plane");
        if (indices.Count < figures.Count - 1)
            throw new ArgumentException(
                $"{figures.Count} surfaces need {figures.Count - 1} region indices, got {indices.Count}");

        int last = figures.Count - 1;

        var p0 = InvariantSeries.P(degree);
        var k0 = InvariantSeries.K(degree);
        var u0 = InvariantSeries.U(degree);

        // The inner product of two vectors held in the (y0, b0) basis, reduced to the invariants:
        // (a1 y0 + b1 b0) . (a2 y0 + b2 b0) = a1 a2 p + (a1 b2 + b1 a2) k + b1 b2 u.
        InvariantSeries Dot(InvariantSeries a1, InvariantSeries b1,
                            InvariantSeries a2, InvariantSeries b2)
            => a1 * a2 * p0 + (a1 * b2 + b1 * a2) * k0 + b1 * b2 * u0;

        var S = InvariantSeries.Constant(degree, 1.0);
        var T = InvariantSeries.Zero(degree);
        var V = InvariantSeries.Zero(degree);
        var W = InvariantSeries.Constant(degree, 1.0);

        for (int i = 0; i < last; i++)
        {
            // The step into figure i+1, save that the last one lands on the output base plane
            // and is taken to belong to the surface before it.
            int governs = Math.Min(i + 1, last - 1);
            bool real = aberrating == null || governs >= aberrating.Count || aberrating[governs];

            if (!real)
            {
                // Paraxial: transfer over the vertex separation, then refract on the vertex
                // curvature alone. Both are linear in the state, so nothing is added to it.
                double gap = figures[i + 1][0] - figures[i][0];
                var sPar = S + V * gap;
                var tPar = T + W * gap;
                S = sPar;
                T = tPar;
                if (i + 1 < last)
                {
                    double nn = indices[i], np = indices[i + 1];
                    double curvature = figures[i + 1].Length > 1 ? 2.0 * figures[i + 1][1] : 0.0;
                    double bend = (np - nn) * curvature / np;
                    V = (nn / np) * V - bend * S;
                    W = (nn / np) * W - bend * T;
                }
                continue;
            }

            var pHere = Dot(S, T, S, T);
            var uHere = Dot(V, W, V, W);
            var alpha = (1.0 - uHere).Sqrt();          // the axial direction cosine
            var invAlpha = alpha.Inverse();
            var fHere = pHere.Compose(figures[i]);

            // Transfer, M (3.2). It is implicit - the axial distance travelled depends on where
            // the ray lands - so Forbes solves it by order: with S and T known to degree m, the
            // argument of f is known to degree m + 1, and (3.3) then gives S and T to degree
            // m + 1. Iterating degree + 1 times therefore reaches the truncation exactly, and
            // one more is run so that the last iteration is provably a fixed point.
            var sNext = S;
            var tNext = T;
            for (int pass = 0; pass <= degree + 1; pass++)
            {
                var pThere = Dot(sNext, tNext, sNext, tNext);
                var step = (pThere.Compose(figures[i + 1]) - fHere) * invAlpha;
                sNext = S + V * step;
                tNext = T + W * step;
            }

            if (i + 1 == last) { S = sNext; T = tNext; break; }   // caught, not refracted

            // Refraction, M (3.4). The surface normal is (1, -2 f' y) over its length, both
            // components taken with the axial one positive, so with
            //
            //     G = alpha - 2 f' (b_i . y_{i+1}) ,   H = sqrt(1 + 4 p f'^2)
            //
            // the cosine of incidence is G / H. Preserving the tangential component gives
            // n' cos I' = sqrt(n'^2 - n^2 + n^2 cos^2 I), whose constant term is n' and so is
            // safely rooted, and the refracted direction follows from
            // n' b' = n b + (n' cos I' - n cos I) times the normal.
            double n = indices[i], nPrime = indices[i + 1];
            if (nPrime == 0.0)
                throw new ArgumentException($"region {i + 1} has a refractive index of zero");
            if (Math.Sign(nPrime) != Math.Sign(n))
                throw new NotSupportedException(
                    "ForbesTrace does not handle reflecting surfaces yet. Forbes carries them by " +
                    "letting the index change sign, but the branch of the root that picks the " +
                    "reflected cosine has to be chosen explicitly and is not written here.");

            var pAt = Dot(sNext, tNext, sNext, tNext);
            var fPrime = pAt.Compose(Derivative(figures[i + 1]));
            var slope = Dot(V, W, sNext, tNext);                    // b_i . y_{i+1}

            var g = alpha - 2.0 * fPrime * slope;
            var h = (1.0 + 4.0 * pAt * fPrime * fPrime).Sqrt();
            var invH = h.Inverse();
            var cosI = g * invH;

            var lambda = ((nPrime * nPrime - n * n) + (n * n) * (cosI * cosI)).Sqrt() - n * cosI;
            var factor = (2.0 / nPrime) * lambda * fPrime * invH;

            S = sNext;
            T = tNext;
            var vNext = (n / nPrime) * V - factor * S;
            var wNext = (n / nPrime) * W - factor * T;
            V = vNext;
            W = wNext;
        }

        return new ForbesTrace(S, T, V, W);
    }

    /// <summary>Coefficients of <c>df/dp</c> from those of <c>f</c>. The vertex position, being
    /// constant, drops out.</summary>
    private static double[] Derivative(double[] f)
    {
        if (f.Length <= 1) return new double[] { 0.0 };
        var d = new double[f.Length - 1];
        for (int j = 0; j < d.Length; j++) d[j] = (j + 1) * f[j + 1];
        return d;
    }

    /// <summary>
    /// The height this trace predicts for one concrete ray, as a check against a real one:
    /// <c>y_out = S y0 + T b0</c> with the invariants evaluated from that ray's own initial
    /// height and direction.
    /// </summary>
    public (double Meridional, double Sagittal) Predict(double y0Meridional, double y0Sagittal,
                                                        double b0Meridional, double b0Sagittal)
    {
        double p = y0Meridional * y0Meridional + y0Sagittal * y0Sagittal;
        double k = y0Meridional * b0Meridional + y0Sagittal * b0Sagittal;
        double u = b0Meridional * b0Meridional + b0Sagittal * b0Sagittal;

        double s = Evaluate(S, p, k, u);
        double t = Evaluate(T, p, k, u);
        return (s * y0Meridional + t * b0Meridional, s * y0Sagittal + t * b0Sagittal);
    }

    private static double Evaluate(InvariantSeries series, double p, double k, double u)
    {
        double sum = 0.0;
        for (int a = 0; a <= series.Degree; a++)
            for (int b = 0; a + b <= series.Degree; b++)
                for (int c = 0; a + b + c <= series.Degree; c++)
                {
                    double coefficient = series[a, b, c];
                    if (coefficient == 0.0) continue;
                    sum += coefficient * Math.Pow(p, a) * Math.Pow(k, b) * Math.Pow(u, c);
                }
        return sum;
    }
}
