using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Distortion predicted from the coefficients, against distortion traced.
///
/// <para><b>Why this is a different question from the spot.</b> <see cref="Prms"/> measures
/// how well the series predicts the SIZE of the patch, and distortion is deliberately
/// absent from it: E, E5 and tau20 displace the whole patch without changing its size, so
/// they cannot appear in an RMS radius. Nothing that consumes a predicted spot can therefore
/// say anything about them. This is that check, and it is the one a designer asks separately
/// anyway, because distortion is specified separately.</para>
///
/// <para><b>What is predicted.</b> Robb's Eq. (2) with the pupil radius set to zero. Every
/// term carrying rho vanishes, and three survive:</para>
///
/// <code>
///   eps_y(0, theta, h) = E h^3 + E5 h^5 + tau20 h^7
/// </code>
///
/// <para>That is the whole of the seventh order's contribution to distortion: ONE
/// coefficient. Note which one it is not - B7, the seventh-order spherical aberration that
/// FIFTHORD prints and that this program could report long before the tertiary set existed,
/// has an aperture power of seven and a field power of zero, so it says nothing at all about
/// distortion. Whatever the seventh order buys here, it buys through tau20, which is
/// reachable no other way.</para>
///
/// <para><b>What is traced.</b> The ray with rho = 0: the one that leaves the field point
/// and crosses the centre of the PARAXIAL entrance pupil, traced exactly by
/// <see cref="RealRayTrace"/> and caught at paraxial focus. That is the ray the polynomial
/// is a series for, so it is the only fair reference - aiming it iteratively at the centre
/// of the real stop instead would fold in pupil aberration, which is real but which the
/// rho = 0 term of the polynomial makes no claim about. It is also what a lens design
/// program reports as distortion with ray aiming off.</para>
///
/// <para><b>The field variable is a tangent, not an angle.</b> The coefficients are
/// converted with H = tan(theta_max), so fractional field h means tan(theta) = h
/// tan(theta_max), and the ray to trace is at atan(h tan(theta_max)) rather than at
/// h theta_max. The two differ by 0.8 per cent at nine tenths of a 20 degree field, which
/// is 2.4 per cent in a term of degree three and 5.7 per cent in one of degree seven - and
/// more to the point the Gaussian image height they imply differs by fifteen times the
/// whole distortion being measured. Getting this wrong does not blur the answer; it
/// replaces it.</para>
/// </summary>
public static class DistortionPrediction
{
    /// <summary>The field fractions the report walks when the caller names none.</summary>
    public static readonly double[] DefaultFractions =
        { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };

    /// <summary>
    /// One field point: where the traced chief ray landed, where the Gaussian image is, and
    /// what each truncation of the series says the gap between them is.
    /// </summary>
    /// <param name="H">Fractional field, in the tangent sense set out above.</param>
    /// <param name="Field">The field value that corresponds to it, in the system's own units.</param>
    /// <param name="Gaussian">
    /// Paraxial chief-ray height at the reference plane - the F-tan(theta) ideal height, since
    /// for an object at infinity the paraxial chief ray lands at f tan(theta) by construction.
    /// </param>
    /// <param name="IdealFTheta">
    /// The F-theta ideal height, f theta with the same f: <c>Gaussian * theta / tan(theta)</c>.
    /// NaN where the mapping has no meaning - object-height fields, or a finite conjugate.
    /// </param>
    /// <param name="Traced">Real chief-ray height at the same plane.</param>
    /// <param name="Third">Predicted displacement from E alone.</param>
    /// <param name="Fifth">Predicted displacement from E and E5.</param>
    /// <param name="Seventh">Predicted displacement from E, E5 and tau20.</param>
    /// <param name="Ok">False when the ray did not get through; the row is then unusable.</param>
    public readonly record struct Row(double H, double Field, double Gaussian, double IdealFTheta,
                                      double Traced, double Third, double Fifth, double Seventh,
                                      bool Ok)
    {
        /// <summary>Traced displacement from the Gaussian image point, in lens units.</summary>
        public double Displacement => Traced - Gaussian;

        /// <summary>
        /// F-tan(theta) distortion as a design program quotes it: per cent of the ideal height.
        /// This is the default sense of the word, and the one an ordinary imaging lens is
        /// specified against.
        /// </summary>
        public double TracedPercent => Percent(Displacement);

        /// <summary>The same for a predicted displacement.</summary>
        public double Percent(double displacement) =>
            Math.Abs(Gaussian) > 1e-12 ? 100.0 * displacement / Gaussian : double.NaN;

        /// <summary>
        /// F-theta distortion: the same landing measured against f theta instead of
        /// f tan(theta) - the mapping a scanning or projection lens is specified against,
        /// where what matters is that image height be proportional to field ANGLE.
        ///
        /// <para>Nothing about the ray changes; only the reference does. On an ordinary lens
        /// this figure is large and mostly geometry - tan(theta) exceeds theta by 4.3 per cent
        /// at 20 degrees - which is why it is reported beside the F-tan(theta) column rather
        /// than instead of it.</para>
        /// </summary>
        public double TracedPercentFTheta => PercentFTheta(Displacement);

        /// <summary>The F-theta figure for a predicted displacement.</summary>
        public double PercentFTheta(double displacement) =>
            Math.Abs(IdealFTheta) > 1e-12
                ? 100.0 * (Gaussian + displacement - IdealFTheta) / IdealFTheta
                : double.NaN;

        /// <summary>Predicted displacement at this truncation: 3, 5 or 7.</summary>
        public double Predicted(int order) => order switch
        {
            3 => Third,
            5 => Fifth,
            _ => Seventh,
        };

        /// <summary>
        /// Error of a truncation as a fraction of the traced displacement. Relative rather
        /// than absolute because distortion spans four decades across a field ladder, and an
        /// absolute error would say nothing except where the field is largest.
        /// </summary>
        public double RelativeError(int order) =>
            Math.Abs(Displacement) > 1e-15
                ? (Predicted(order) - Displacement) / Displacement
                : double.NaN;
    }

    /// <summary>
    /// The predicted transverse displacement of the chief ray at fractional field
    /// <paramref name="h"/>, in the same length units as the coefficients.
    ///
    /// <para><paramref name="order"/> truncates the series: 3 keeps E, 5 adds E5, 7 adds
    /// tau20. Anything else is treated as the full set.</para>
    /// </summary>
    public static double Displacement(BuchdahlTerms totals, double h, int order = 7)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        double d = totals.E * h * h * h;
        if (order >= 5) d += totals.E5 * Math.Pow(h, 5);
        if (order >= 7) d += totals.Tau20 * Math.Pow(h, 7);
        return d;
    }

    /// <summary>
    /// The field value, in the units <see cref="OpticalSystem.FieldType"/> names, whose
    /// field variable is <paramref name="h"/> times the maximum.
    ///
    /// <para>An object height scales linearly, because the field variable is the height over
    /// the object distance and the distance does not move. An angle does not: the variable
    /// is its tangent.</para>
    /// </summary>
    public static double FieldFor(OpticalSystem system, double maxField, double h)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        if (system.FieldType == FieldType.ObjectHeight) return h * maxField;
        return Math.Atan(h * Math.Tan(maxField * Math.PI / 180.0)) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Walks a ladder of field fractions, tracing the chief ray at each and evaluating the
    /// three truncations against it.
    /// </summary>
    /// <param name="totals">
    /// The transverse coefficient set for the wavelength being traced, with the tertiary
    /// terms already attached - <see cref="TertiaryCoefficients.Attach"/>. Without them
    /// tau20 is zero and the seventh-order column silently repeats the fifth.
    /// </param>
    /// <param name="maxField">
    /// The largest field the system defines, in its own units. Zero - an on-axis-only design
    /// - has no distortion to measure and yields no rows.
    /// </param>
    /// <param name="atParaxialFocus">
    /// Where the chief ray is caught and what it is measured against. True - the default and
    /// the only setting the PREDICTION is valid at - is the paraxial image plane, which is
    /// where the coefficients are referred and which Robb's polynomial has no defocus term to
    /// leave. False is the image surface the file itself defines, which is what a design
    /// program quotes distortion at; on a design saved at best focus the two differ, and the
    /// traced column then differs with them.
    /// </param>
    public static IReadOnlyList<Row> Compare(OpticalSystem system, double[] indices,
                                             ParaxialResult paraxial, BuchdahlTerms totals,
                                             double maxField,
                                             IReadOnlyList<double>? fractions = null,
                                             bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        var rows = new List<Row>();
        if (Math.Abs(maxField) < 1e-15) return rows;

        foreach (double h in fractions ?? DefaultFractions)
        {
            if (Math.Abs(h) < 1e-12) continue;      // no field, no distortion, no row

            double field = FieldFor(system, maxField, h);
            var pf = ParaxialTrace.Trace(system, indices, field);

            // The chief ray is a straight line in object space, so the paraxial trace hands
            // over its geometry exactly: height at surface one, slope before it. Expressing
            // it that way rather than as a field angle and a pupil fraction gives the chief ray
            // exactly from the paraxial trace, at either conjugate.
            var land = RealRayTrace.TraceFrom(system, indices, paraxial,
                                              0.0, pf.Ybar[1], 0.0, pf.Ubar[0], 1.0,
                                              atParaxialFocus);

            double ideal = atParaxialFocus ? pf.ParaxialImageHeight : pf.ImageHeight;

            // F-theta needs a field ANGLE to be proportional to. An object height is not one,
            // and neither is an angle subtended at a finite object distance.
            double idealFTheta = double.NaN;
            if (system.FieldType != FieldType.ObjectHeight && paraxial.InfiniteConjugate)
            {
                double theta = field * Math.PI / 180.0;
                double tan = Math.Tan(theta);
                if (Math.Abs(tan) > 1e-15) idealFTheta = ideal * theta / tan;
            }

            rows.Add(new Row(h, field, ideal, idealFTheta, land.Y,
                             Displacement(totals, h, 3),
                             Displacement(totals, h, 5),
                             Displacement(totals, h, 7),
                             land.Ok));
        }
        return rows;
    }

    /// <summary>
    /// One coefficient read back out of the traced rays, next to the one the program reports.
    /// </summary>
    /// <param name="Name">E, E5 or Tau20.</param>
    /// <param name="Order">Its power of h: 3, 5 or 7.</param>
    /// <param name="Reported">What this program computes for it.</param>
    /// <param name="FromRays">What the rays say it is.</param>
    /// <param name="H">The larger of the two field fractions the estimate was taken from.</param>
    /// <param name="Spread">
    /// Relative disagreement between the two estimates the extrapolation was made from - the
    /// estimate's own error bar. Large means the recovery failed rather than that the
    /// coefficient is wrong.
    /// </param>
    public readonly record struct Recovery(string Name, int Order, double Reported,
                                           double FromRays, double H, double Spread)
    {
        /// <summary>Reported over recovered. One is agreement.</summary>
        public double Ratio => Math.Abs(FromRays) > 1e-300 ? Reported / FromRays : double.NaN;

        /// <summary>
        /// Whether the recovery is worth reading. The two estimates it interpolates between
        /// have to agree with each other before their disagreement with the reported value
        /// means anything.
        /// </summary>
        public bool Reliable => Spread < 0.25;
    }

    /// <summary>Field fractions the recovery samples. Each is half of the one before it.</summary>
    private static readonly double[] RecoveryLadder = { 0.4, 0.2, 0.1, 0.05, 0.025 };

    /// <summary>
    /// Reads E, E5 and tau20 back out of the traced rays and compares them with the reported
    /// ones - the check the predicted spot cannot make.
    ///
    /// <para><b>How it separates them.</b> At zero pupil radius the polynomial has exactly
    /// three terms and they are separated by their power of h alone. Subtract the lower orders
    /// from a traced displacement, divide by h^k, and what is left approaches the kth
    /// coefficient as h falls - so each one is measured on its own rather than inside a sum
    /// where errors cancel. An RMS spot mixes eighteen coefficients and can be right for the
    /// wrong reasons; this cannot.</para>
    ///
    /// <para><b>It is not the only ray route to tau20.</b>
    /// <see cref="CoefficientInversion"/> recovers all twenty from traced rays by scaling ray
    /// shapes and fitting an odd polynomial, and its default shapes include the zero-pupil case
    /// for exactly this reason. That is the more general instrument and it came first. This one
    /// is narrower and cheaper: three coefficients, no basis, no least-squares solve, no model
    /// of the other seventeen, and an error bar of its own. Where the two agree on tau20 - they
    /// do, to between 0.03 and 1.2 per cent across the figured fixtures - the agreement rests on
    /// almost nothing shared beyond the tracer.</para>
    ///
    /// <para><b>How the field fraction is chosen.</b> The estimate at h carries the next
    /// coefficient times h^2, so it improves as h falls - until the term being measured drops
    /// below the tracer's own precision, after which it is noise. Both ends are wrong and
    /// neither announces itself, so the ladder is walked in halves, each adjacent pair is
    /// extrapolated in h^2, and the pair whose two estimates agree best is the one reported.
    /// That disagreement is carried out as <see cref="Recovery.Spread"/>: it is the estimate's
    /// error bar, and a recovery whose own two estimates disagree says nothing about the
    /// coefficient.</para>
    /// </summary>
    public static IReadOnlyList<Recovery> Recover(OpticalSystem system, double[] indices,
                                                  ParaxialResult paraxial, BuchdahlTerms totals,
                                                  double maxField)
    {
        var rows = Compare(system, indices, paraxial, totals, maxField, RecoveryLadder);
        var found = new List<Recovery>();
        if (rows.Count < 2) return found;

        var names = new[] { ("E", 3, totals.E), ("E5", 5, totals.E5), ("Tau20", 7, totals.Tau20) };
        foreach (var (name, order, reported) in names)
        {
            // The residual left after the LOWER orders are removed, divided by its own power
            // of h. The lower orders come from the reported set, so a wrong E would show up in
            // E5's recovery as well - which is the right behaviour: the coefficients are only
            // separable in sequence, and saying so is better than pretending otherwise.
            var estimate = new double[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                double r = rows[i].Displacement;
                if (order >= 5) r -= totals.E * Math.Pow(rows[i].H, 3);
                if (order >= 7) r -= totals.E5 * Math.Pow(rows[i].H, 5);
                estimate[i] = r / Math.Pow(rows[i].H, order);
            }

            int best = -1;
            double bestSpread = double.PositiveInfinity;
            for (int i = 0; i + 1 < rows.Count; i++)
            {
                if (!rows[i].Ok || !rows[i + 1].Ok) continue;
                double scale = Math.Max(Math.Abs(estimate[i]), Math.Abs(estimate[i + 1]));
                if (scale < 1e-300) continue;
                double spread = Math.Abs(estimate[i + 1] - estimate[i]) / scale;
                if (spread < bestSpread) { bestSpread = spread; best = i; }
            }
            if (best < 0) continue;

            // Richardson in h^2: the ladder halves, so four times the finer estimate less the
            // coarser one, over three, cancels the next coefficient's leading contamination.
            double value = (4.0 * estimate[best + 1] - estimate[best]) / 3.0;
            found.Add(new Recovery(name, order, reported, value, rows[best].H, bestSpread));
        }
        return found;
    }
}
