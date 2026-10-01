using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Whether a given design would actually TEST the aspheric tertiary path, decided without
/// tracing a single ray.
///
/// <para>Three of the twenty tertiary coefficients - tau9, tau14 and tau17 - are wrong on
/// figured surfaces, and correcting them moves the predicted RMS spot materially (see
/// `docs/verification.md`). Finding the fault needs a design that exercises them hard. Ray
/// tracing every candidate to find out is slow and needs a second program; the four numbers
/// below settle it from the coefficients alone, so a pile of candidates can be sorted in
/// seconds and only the promising ones go near a ray trace.</para>
///
/// <para>The screen is a screen, not a proof. It says a design SHOULD show the defect
/// clearly; it cannot say the defect is there, because that is exactly the question the ray
/// trace is asked.</para>
/// </summary>
public sealed class AsphericScreen
{
    /// <summary>Field the screen was run at, as a fraction of the maximum.</summary>
    public double H { get; init; }

    /// <summary>
    /// How much of the predicted RMS spot the three suspect coefficients carry: the relative
    /// change when tau9, tau14 and tau17 are zeroed. Small means an error in them cannot be
    /// seen however carefully the design is traced.
    /// </summary>
    public double SuspectShare { get; init; }

    /// <summary>
    /// How much of those three comes from the FIGURING rather than from the underlying
    /// spheres. Near zero means the design would test the spherical path over again, which is
    /// already known to be right, and would say nothing about aspherics.
    /// </summary>
    public double AsphericLeverage { get; init; }

    /// <summary>
    /// Size of the seventh-order correction relative to the fifth-order one, taken as the
    /// WORST value over the field span from half the screened height out to it, not at that
    /// height alone. The prediction is
    /// a power series in aperture and field, and this is the ratio of consecutive terms in it:
    /// well under one means the ninth order is smaller still and can be neglected. Near or above
    /// one means the series has stopped converging usefully, and any residual is as likely to be
    /// the missing ninth order as a fault in the seventh.
    ///
    /// <para><b>That last sentence used to end the matter, and no longer has to.</b> The ninth
    /// order was uncomputable here when this was written, so an on-axis residual could only be
    /// ATTRIBUTED to it. <see cref="QuaternarySpherical"/> computes the spherical part of it, which
    /// on axis is the whole of it, so on a system of spheres the question is now arithmetic rather
    /// than a judgement about a ratio. Off axis, and on any figured design, the ratio is still all
    /// there is.</para>
    /// </summary>
    public double SeriesRatio { get; init; }

    /// <summary>
    /// How much the seventh order moves the answer at all, relative to the whole spot. Below a
    /// per cent or so the seventh-order set is a rounding correction on this design, and its
    /// errors are unmeasurable against ray-trace noise.
    /// </summary>
    public double SeventhSignificance { get; init; }

    /// <summary>RMS spot radius from the series truncated at third, fifth and seventh order.</summary>
    public double Rms3 { get; init; }

    /// <inheritdoc cref="Rms3"/>
    public double Rms5 { get; init; }

    /// <inheritdoc cref="Rms3"/>
    public double Rms7 { get; init; }

    /// <summary>The three suspect coefficients, as computed and with the figuring removed.</summary>
    public IReadOnlyList<(string Name, double Figured, double Bare)> Suspects { get; init; }
        = Array.Empty<(string, double, double)>();

    /// <summary>
    /// How much of the figuring lies in terms the theory cannot represent: the sag from r^10
    /// and beyond, against the sag from r^4 to r^8, at the clear aperture of the worst
    /// surface.
    ///
    /// <para>A seventh-order theory reaches r^8 and stops. Terms beyond it are ninth order and
    /// above, so they are not approximated badly - they are ABSENT, and everything they would
    /// have contributed lands in the residual. A design whose figuring is mostly in those
    /// terms cannot be used to test the seventh order, however strong its aspherics look.</para>
    /// </summary>
    public double UnrepresentableFiguring { get; init; }

    /// <summary>
    /// Whether the design is worth tracing. All four have to hold: the suspects must matter,
    /// they must be driven by figuring, the series must converge, and the seventh order must
    /// be visible. A design failing any one of them cannot settle the question.
    /// </summary>
    public bool IsDiagnostic =>
        SuspectShare >= 0.03 && AsphericLeverage >= 0.25 &&
        SeriesRatio <= 0.5 && SeventhSignificance >= 0.01 && UnrepresentableFiguring <= 0.25;

    private static string Pct(double v) =>
        !double.IsNaN(v) && !double.IsInfinity(v) ? (100.0 * v).ToString("F2", CultureInfo.InvariantCulture) + "%"
                           : "infinite";

    private static string Mark(bool ok) => ok ? "yes" : "NO ";

    /// <summary>The verdict as a short readable block.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("Aspheric tertiary screen at H = ")
          .AppendLine(H.ToString("F2", CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.Append("  RMS spot   3rd ").Append(Rms3.ToString("E4", CultureInfo.InvariantCulture))
          .Append("   5th ").Append(Rms5.ToString("E4", CultureInfo.InvariantCulture))
          .Append("   7th ").AppendLine(Rms7.ToString("E4", CultureInfo.InvariantCulture));
        sb.AppendLine();
        Line(sb, SuspectShare >= 0.03, "tau9/14/17 share of spot", SuspectShare, "want >= 3%");
        Line(sb, AsphericLeverage >= 0.25, "driven by figuring", AsphericLeverage, "want >= 25%");
        Line(sb, SeriesRatio <= 0.5, "7th order work, worst", SeriesRatio, "want <= 50%");
        Line(sb, SeventhSignificance >= 0.01, "7th order visible at all", SeventhSignificance,
             "want >= 1%");
        Line(sb, UnrepresentableFiguring <= 0.25, "figuring beyond r^8", UnrepresentableFiguring,
             "want <= 25%");
        sb.AppendLine();
        sb.AppendLine(IsDiagnostic
            ? "  VERDICT: diagnostic - worth tracing."
            : "  VERDICT: not diagnostic - a trace of this could not settle the question.");
        sb.AppendLine();
        sb.AppendLine("  coefficient         figured            bare    from figuring");
        foreach (var (name, fig, bare) in Suspects)
        {
            double share = Math.Abs(fig) > 0 ? Math.Abs(fig - bare) / Math.Abs(fig) : 0.0;
            sb.Append("  ").Append(name.PadRight(10))
              .Append(fig.ToString("E5", CultureInfo.InvariantCulture).PadLeft(15))
              .Append(bare.ToString("E5", CultureInfo.InvariantCulture).PadLeft(16))
              .AppendLine(Pct(share).PadLeft(17));
        }
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, bool ok, string label, double value, string want)
        => sb.Append("  ").Append(Mark(ok)).Append(' ').Append(label.PadRight(26))
             .Append(Pct(value).PadLeft(9)).Append("   (").Append(want).AppendLine(")");
}

/// <summary>Runs the screen described by <see cref="AsphericScreen"/>.</summary>
public static class AsphericDiagnostic
{
    private static readonly string[] Suspect = { "Tau9", "Tau14", "Tau17" };

    /// <summary>
    /// Screens a design. <paramref name="h"/> is the field as a fraction of the maximum; the
    /// default of one is the corner, where the high field powers these three carry are largest.
    /// </summary>
    public static AsphericScreen Screen(OpticalSystem system, double[] indices,
                                        double maxField, double h = 1.0)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));

        var figured = Totals(system, indices, maxField);
        var bare = WithoutFiguring(system, indices, maxField);

        var zeroed = figured.Clone();
        zeroed.Tau9 = 0.0;
        zeroed.Tau14 = 0.0;
        zeroed.Tau17 = 0.0;

        double r3 = Rms(figured, h, 3);
        double r5 = Rms(figured, h, 5);
        double r7 = Rms(figured, h, 7);
        double rz = Rms(zeroed, h, 7);

        // How hard the seventh order is working, measured against the SPOT rather than against
        // the fifth-order increment.
        //
        // The increment ratio |r7-r5| / |r5-r3| is the natural thing to write and is unusable:
        // its denominator passes through zero wherever the third- and fifth-order predictions
        // cross, so it reported 2824% on a design whose series is perfectly well behaved, and
        // 17% on one whose rms7 dips and climbs back - an artefact in each direction. Dividing
        // by the answer instead cannot blow up, because rms7 is a spot radius and is positive.
        //
        // The quantity wants to sit in a BAND. Below a per cent the seventh order is a rounding
        // correction and its errors are unmeasurable; above about half, the truncation is doing
        // so much of the work that the ninth order will be doing plenty too, and a residual
        // could not be attributed - though on a system of spheres QuaternarySpherical can now
        // measure the axial part of that instead of leaving it to this ratio. Taken as the worst over a span of field
        // so that one lucky point cannot carry a design.
        double seventhStep = Math.Abs(r7 - r5);
        double worstWork = 0.0;
        for (int k = 0; k <= 5; k++)
        {
            double hk = h * (0.5 + 0.1 * k);
            double a5 = Rms(figured, hk, 5), a7 = Rms(figured, hk, 7);
            double work = a7 > 0 ? Math.Abs(a7 - a5) / a7 : double.PositiveInfinity;
            if (work > worstWork) worstWork = work;
        }

        // Leverage is summed over the three rather than averaged per coefficient, so that one
        // large term the figuring does drive cannot vouch for two it never touches.
        double num = 0.0, den = 0.0;
        foreach (var name in Suspect)
        {
            num += Math.Abs(figured[name] - bare[name]);
            den += Math.Abs(figured[name]);
        }

        return new AsphericScreen
        {
            H = h,
            Rms3 = r3,
            Rms5 = r5,
            Rms7 = r7,
            SuspectShare = r7 > 0 ? Math.Abs(r7 - rz) / r7 : 0.0,
            AsphericLeverage = den > 0 ? num / den : 0.0,
            SeriesRatio = worstWork,
            SeventhSignificance = r7 > 0 ? seventhStep / r7 : 0.0,
            Suspects = Suspect.Select(n => (n, figured[n], bare[n])).ToArray(),
            UnrepresentableFiguring = BeyondEighthOrder(system),
        };
    }

    /// <summary>
    /// The same system with every conic and aspheric term removed, so that what the figuring
    /// contributes can be had by difference. The system is restored before returning: the
    /// caller hands in its own object and should not find it altered.
    /// </summary>
    private static BuchdahlTerms WithoutFiguring(OpticalSystem system, double[] indices,
                                                 double maxField)
    {
        var conics = system.Surfaces.Select(s => s.Conic).ToArray();
        var terms = system.Surfaces.Select(s => (double[])s.AsphericCoefficients.Clone()).ToArray();
        try
        {
            foreach (var s in system.Surfaces)
            {
                s.Conic = 0.0;
                s.AsphericCoefficients = new double[s.AsphericCoefficients.Length];
            }
            return Totals(system, indices, maxField);
        }
        finally
        {
            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                system.Surfaces[i].Conic = conics[i];
                system.Surfaces[i].AsphericCoefficients = terms[i];
            }
        }
    }

    /// <summary>
    /// The worst surface's ratio of sag from r^10 and beyond to sag from r^4 to r^8, at its
    /// own clear aperture.
    ///
    /// <para>Measured as sag rather than as raw coefficients because the coefficients are not
    /// comparable with one another - each carries a different power of r, so on a 0.6 mm
    /// surface an r^14 coefficient a thousand times larger than the r^4 one still contributes
    /// nothing. What decides whether the theory can see the figuring is what the terms are
    /// worth at the edge of the aperture, which is what this measures.</para>
    /// </summary>
    private static double BeyondEighthOrder(OpticalSystem system)
    {
        double worst = 0.0;
        foreach (var s in system.Surfaces)
        {
            var a = s.AsphericCoefficients;
            if (a == null || a.Length == 0) continue;
            double r = s.SemiDiameter;
            if (r <= 0.0) continue;

            // Entry k multiplies r^(2k+2), so entry 1 is r^4 and entry 3 is r^8 - the last
            // order a seventh-order theory reaches.
            double within = 0.0, beyond = 0.0;
            for (int k = 1; k < a.Length; k++)
            {
                if (a[k] == 0.0) continue;
                double term = Math.Abs(a[k]) * Math.Pow(r, 2 * k + 2);
                if (k <= 3) within += term; else beyond += term;
            }
            if (within + beyond <= 0.0) continue;

            double ratio = beyond / (within + beyond);
            if (ratio > worst) worst = ratio;
        }
        return worst;
    }

    /// <summary>
    /// A full coefficient set, tertiary terms included. The attach step is not optional: the
    /// fifth-order code leaves tau2..tau20 at zero, and a screen run on that would report that
    /// the suspects carry none of the spot - which would be true of the numbers and false of
    /// the design.
    /// </summary>
    private static BuchdahlTerms Totals(OpticalSystem system, double[] indices, double maxField)
    {
        var trace = ParaxialTrace.Trace(system, indices, maxField);
        var b = BuchdahlCoefficients.Compute(system, trace);
        TertiaryCoefficients.Attach(system, indices, trace, b, maxField);
        return b.Totals;
    }

    /// <summary>
    /// RMS spot radius from the series truncated at <paramref name="order"/>, about its own
    /// centroid, by quadrature over the pupil.
    ///
    /// <para><see cref="Prms.Value"/> exists and is analytic, but it is the full seventh-order
    /// form and cannot be truncated. Integrating <see cref="Prms.Transverse"/> instead gives
    /// all three orders from one routine; that it agrees with the analytic form at order 7 is
    /// checked by test rather than assumed.</para>
    /// </summary>
    public static double Rms(BuchdahlTerms totals, double h, int order)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        const int nr = 96, nt = 256;
        double sum = 0.0, wsum = 0.0, meanY = 0.0;

        for (int i = 0; i < nr; i++)
        {
            // Midpoint in rho, weighted by rho so equal weight covers equal area.
            double rho = (i + 0.5) / nr;
            for (int j = 0; j < nt; j++)
            {
                double theta = 2 * Math.PI * (j + 0.5) / nt;
                var (y, z) = Prms.Transverse(totals, rho, theta, h, order);
                sum += rho * (y * y + z * z);
                meanY += rho * y;
                wsum += rho;
            }
        }
        meanY /= wsum;
        return Math.Sqrt(Math.Max(0.0, sum / wsum - meanY * meanY));
    }
}
