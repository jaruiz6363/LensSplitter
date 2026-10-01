using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Forbes;

/// <summary>
/// Tertiary coefficients by Forbes route.
///
/// <para><b>Nothing is traced and nothing is fitted.</b> The real-ray route recovers the
/// coefficients by tracing each shape at a ladder of twelve scales and fitting eight odd powers
/// through the result, because a traced ray gives no way to separate the seventh order from the
/// ninth except by watching how they shrink. A series trace has no such difficulty: the
/// degree-seven part is written down. So the ladder and the fit are gone, and what is left is the
/// linear model that maps that quantity onto the twenty coefficients - which is shared with the
/// real-ray route, so no change of basis was derived by hand here.</para>
///
/// <para>The effect is visible in the residual. Fitted off real rays it sits near 1E-6; taken
/// exactly it sits at 1E-16, machine precision, which says the degree-seven aberration of the
/// trace lies exactly in the span of the twenty coefficients rather than approximately in it.
/// That also removes the readout floor: the earlier figures were limited by what the ladder
/// could separate, not by what the method could reach.</para>
/// </summary>
public static class ForbesCoefficients
{
    /// <summary>
    /// The twenty tertiary coefficients, read straight off the trace.
    ///
    /// <para>No rays are traced and nothing is fitted. For each shape the transverse aberration
    /// is built as a series in the ray SCALE and its seventh coefficient taken exactly; that is
    /// the same quantity the real-ray route recovers by tracing a ladder of twelve scales and
    /// fitting eight odd powers through it, and it is the definition of the coefficients rather
    /// than an estimate of them. The mapping onto the twenty is then the same linear model the
    /// real-ray route uses, so nothing here rests on a change of basis derived by hand.</para>
    ///
    /// <para>Null if the model cannot be formed, which happens for a system with no field,
    /// exactly as it does for the real-ray route.</para>
    /// </summary>
    /// <param name="degree">
    /// Truncation of the series trace. Three is the tertiary; a higher value costs nothing much
    /// and must not change the answer, which <c>ForbesTraceTests</c> checks.
    /// </param>
    public static CoefficientInversion.Result? Invert(
        OpticalSystem system, double[] indices, ParaxialResult paraxial, double maxFieldDeg,
        int degree = 3, IReadOnlyList<bool>? aberrating = null, int flatten = 0)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        // Declined, not attempted: the series trace does not handle a reflection yet (see
        // ForbesTrace). Every caller already reads null as "this route has nothing to say".
        foreach (var s in system.Surfaces) if (s.IsMirror) return null;

        var trace = Trace(system, indices, paraxial, degree, aberrating, flatten);

        // How the field is measured depends on the conjugate. For an object at infinity it is
        // the tangent of the field angle; for a finite one it is an object HEIGHT, and the ray
        // is no longer collimated in object space - its direction depends on the pupil point
        // as well. ParaxialImageHeight/Magnification is the object height the paraxial trace
        // already worked out, at whatever conjugate and however the fields are stated.
        double objectDistance = system.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double hmax = infinite ? Math.Tan(maxFieldDeg * Math.PI / 180.0) : 0.0;
        double objectHeight = 0.0;
        if (!infinite)
        {
            if (Math.Abs(paraxial.Magnification) < 1e-12) return null;
            objectHeight = paraxial.ParaxialImageHeight / paraxial.Magnification;
            if (Math.Abs(objectHeight) < 1e-12) return null;
        }
        else if (Math.Abs(hmax) < 1e-12) return null;

        // The direction cosine depends on the field alone, and the default shapes use four
        // distinct field values between seventy-five shapes. Its series costs a square root and
        // an inverse, so it is built once per field rather than once per shape.
        var sines = new Dictionary<double, ScaleSeries>();

        return CoefficientInversion.SolveFromDegreeSeven(
            CoefficientInversion.DefaultShapes(),
            sh => DegreeSeven(trace, paraxial, hmax, sh, sines, objectDistance, objectHeight));
    }

    /// <summary>
    /// The degree-seven transverse aberration at one shape, exactly.
    ///
    /// <para>Shrinking a ray by <c>s</c> sends the pupil to <c>s rho</c> and the field to
    /// <c>s h</c>. Under that scaling the height at the input base plane is exactly linear,
    /// because the walk back from the entrance pupil contributes <c>-ep tan(field)</c> and the
    /// tangent is what the inversion makes linear in <c>s</c>. The DIRECTION is not: it is
    /// <c>sin = tan / sqrt(1 + tan^2)</c>, whose cubic and quintic terms carry lower-degree parts
    /// of <c>S</c> and <c>T</c> up into the seventh. Building the whole thing as a series in
    /// <c>s</c> takes all of that; reading the degree-three part of <c>S</c> and <c>T</c> alone
    /// would drop it and be wrong by of order the field squared.</para>
    /// </summary>
    private static (double Y, double Z) DegreeSeven(
        ForbesTrace trace, ParaxialResult paraxial, double hmax,
        CoefficientInversion.Shape sh, Dictionary<double, ScaleSeries> sines,
        double objectDistance, double objectHeight)
    {
        const int order = 7;
        double epr = 0.5 * paraxial.Epd;
        double ep = paraxial.EntrancePupilPosition;

        var s = ScaleSeries.S(order);
        ScaleSeries ym, ys, bm, bs;

        if (double.IsInfinity(objectDistance))
        {
            // tan(field) is exactly linear in the scale, by the inversion's own parameterisation.
            if (!sines.TryGetValue(sh.H, out var sin))
            {
                var tan = s * (sh.H * hmax);
                sin = tan * (1.0 + tan * tan).Sqrt().Inverse();
                sines[sh.H] = sin;
            }
            ym = s * (sh.Rho * Math.Cos(sh.Theta) * epr - ep * sh.H * hmax);
            ys = s * (sh.Rho * Math.Sin(sh.Theta) * epr);
            bm = sin;

            // The sagittal direction is zero: a collimated beam tilts in the meridian only.
            bs = ScaleSeries.Zero(order);
        }
        else
        {
            // The ray is the straight line from the object point to the chosen point on the
            // entrance pupil. Its POSITION where it crosses surface one's vertex plane is a
            // weighted mean of the two, so the normalisation cancels and it stays exactly
            // linear in the scale, as the collimated case is.
            double d = ep + objectDistance;                     // object plane to entrance pupil
            double yp = sh.Rho * Math.Cos(sh.Theta) * epr;
            double xp = sh.Rho * Math.Sin(sh.Theta) * epr;
            double hob = sh.H * objectHeight;

            ym = s * ((hob * ep + yp * objectDistance) / d);
            ys = s * (xp * objectDistance / d);

            // Its DIRECTION is not linear: the transverse numerators scale with the ray and
            // the axial one does not, so the normalising root carries lower-degree parts of S
            // and T up into the seventh - the same way sin = tan/sqrt(1+tan^2) does above.
            // And the sagittal direction no longer vanishes: a skew ray from a finite object
            // point is tilted out of the meridian, which is why bs is carried through below.
            var dy = s * ((yp - hob) / d);
            var dx = s * (xp / d);
            var inv = (1.0 + dy * dy + dx * dx).Sqrt().Inverse();
            bm = dy * inv;
            bs = dx * inv;
        }

        // The invariants, and then S and T evaluated on them.
        var p = ym * ym + ys * ys;
        var k = ym * bm + ys * bs;
        var u = bm * bm + bs * bs;

        var sSeries = Evaluate(trace.S, p, k, u, order);
        var tSeries = Evaluate(trace.T, p, k, u, order);

        var outM = sSeries * ym + tSeries * bm;
        var outS = sSeries * ys + tSeries * bs;
        return (outM[order], outS[order]);
    }

    /// <summary>A series in the invariants, re-expressed as a series in the ray scale.</summary>
    private static ScaleSeries Evaluate(InvariantSeries series, ScaleSeries p, ScaleSeries k,
                                        ScaleSeries u, int order)
    {
        // The powers are built once and the pk product carried down the c loop, rather than
        // forming p^a k^b u^c from scratch for each of the twenty terms. Each invariant is
        // quadratic in the scale, so anything of total degree four or more is already past the
        // seventh order and is skipped rather than multiplied out.
        int d = series.Degree;
        var pPow = new ScaleSeries[d + 1];
        var kPow = new ScaleSeries[d + 1];
        var uPow = new ScaleSeries[d + 1];
        pPow[0] = kPow[0] = uPow[0] = ScaleSeries.Constant(order, 1.0);
        for (int i = 1; i <= d; i++)
        {
            pPow[i] = pPow[i - 1] * p;
            kPow[i] = kPow[i - 1] * k;
            uPow[i] = uPow[i - 1] * u;
        }

        var sum = ScaleSeries.Zero(order);
        for (int a = 0; a <= d; a++)
            for (int b = 0; a + b <= d; b++)
            {
                ScaleSeries? pk = null;
                for (int c = 0; a + b + c <= d; c++)
                {
                    double coefficient = series[a, b, c];
                    if (coefficient == 0.0) continue;
                    pk ??= pPow[a] * kPow[b];
                    sum += (c == 0 ? pk : pk * uPow[c]) * coefficient;
                }
            }
        return sum;
    }

    /// <summary>
    /// The series trace of a whole system, launched at surface one's vertex plane and caught on
    /// the PARAXIAL image plane - the plane the coefficients are referred to, and the one
    /// <see cref="RealRayTrace"/> uses by default.
    /// </summary>
    /// <param name="aberrating">
    /// Which surfaces act in full; the rest are linearised. Null means all of them. See
    /// <see cref="ForbesPerSurface"/> for what that is for.
    /// </param>
    /// <param name="flatten">
    /// A surface whose FIGURING is taken off, leaving a sphere - the conic and the higher
    /// aspheric terms dropped. Zero means none. The sphere keeps the surface vertex curvature
    /// rather than its base curvature, so an r-squared aspheric term stays folded in and the
    /// paraxial power is untouched; otherwise flattening would move the image and the whole
    /// decomposition would be measured against a different reference.
    /// </param>
    public static ForbesTrace Trace(OpticalSystem system, double[] indices,
                                    ParaxialResult paraxial, int degree = 3,
                                    IReadOnlyList<bool>? aberrating = null, int flatten = 0)
    {
        int last = system.LastOpticalSurface();

        var figures = new List<double[]> { new double[] { 0.0 } };
        double z = 0.0;
        double lastVertex = 0.0;
        for (int i = 1; i <= last; i++)
        {
            var s = system.Surfaces[i];
            figures.Add(i == flatten
                ? ForbesTrace.Figure(z, s.VertexCurvature, 0.0, null, degree)
                : ForbesTrace.Figure(z, s.Curvature, s.Conic, s.AsphericCoefficients, degree));
            lastVertex = z;
            z += s.Thickness;
        }
        figures.Add(new[] { lastVertex + paraxial.ParaxialFocusDistance });

        // Signed, as the paraxial trace signs them: negated after an odd number of mirrors.
        // ForbesTrace refuses a reflection - it recognises one by the index changing sign - and
        // handed the plain indices it never saw one: the parabola went through as a refraction
        // into the same index, the ray did not bend, and every tau came back as zero.
        var regions = new List<double>();
        double sign = 1.0;
        for (int i = 0; i <= last; i++)
        {
            if (system.Surfaces[i].IsMirror) sign = -sign;
            regions.Add(sign * Math.Abs(i < indices.Length ? indices[i] : 1.0));
        }

        return ForbesTrace.Run(figures, regions, degree, aberrating);
    }
}
