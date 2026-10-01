using System;
using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Report;

/// <summary>
/// The third, fifth and seventh orders per surface, split into what each surface generates on
/// its own and what it generates by acting on the aberration already present when light
/// reaches it.
///
/// <para>Written once and here so the three programs that report it cannot drift apart: the
/// command line, the MCP server, and the automation program that drives a host application.
/// They differ only in where the lens comes from.</para>
///
/// <para>The seventh order comes from the Forbes series trace, which handles spheres, conics
/// and even aspheres alike; the third and fifth from Buchdahl's scheme, which carries figuring
/// at those orders. All three are printed because a seventh-order coefficient means little on
/// its own - it is the size of the correction to the orders beneath it, and whether that
/// correction matters is a question about all three.</para>
/// </summary>
public static class ForbesReport
{
    private static readonly string[][] LowerGroups =
    {
        new[] { "B", "F", "C", "Pi", "E" },
        new[] { "B5", "F1", "F2", "M1", "M2", "M3" },
        new[] { "N1", "N2", "N3", "C5", "Pi5", "E5" },
    };

    /// <summary>
    /// Builds the report. Returns null when the coefficients cannot be separated, which happens
    /// on a system with no field, when the series trace does not close on a design, and on a
    /// design with a mirror, which the series trace declines.
    /// </summary>
    /// <param name="degree">
    /// Truncation of the series trace. Three is the seventh order. A higher value costs time
    /// and must not change the answer, which is a real check on whether the series has
    /// converged for a particular design.
    /// </param>
    public static string? Build(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                double maxFieldDegrees, int degree = 3)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        var breakdown = ForbesPerSurface.Compute(system, indices, paraxial, maxFieldDegrees, degree);
        if (breakdown == null) return null;
        var lower = BuchdahlCoefficients.Compute(system, paraxial);

        int last = system.LastOpticalSurface();
        bool anyFigured = false;
        for (int i = 1; i <= last; i++) if (IsFigured(system.Surfaces[i])) { anyFigured = true; break; }

        var w = new StringBuilder();
        w.AppendLine(F("Effective focal length {0:0.000000}", paraxial.Efl));
        w.AppendLine(F("Conjugate              {0}", paraxial.InfiniteConjugate
            ? "object at infinity"
            : "finite, object " + system.Surfaces[0].Thickness.ToString("0.###", Inv) + " away"));
        w.AppendLine(F("Maximum field          {0:0.000000}", maxFieldDegrees));
        w.AppendLine(F("Truncation degree      {0}   (three is the seventh order)", degree));
        w.AppendLine();

        // --- third and fifth, Buchdahl -------------------------------------------------
        w.AppendLine("THIRD AND FIFTH ORDER, by Buchdahl's scheme.");
        w.AppendLine();
        w.AppendLine(F("Per surface, unconverted - multiply by the F/number {0:0.000000} for a", lower.FNumber));
        w.AppendLine("transverse aberration in lens units, which is done for the totals below.");
        w.AppendLine();

        for (int i = 1; i <= last; i++)
        {
            bool figured = IsFigured(system.Surfaces[i]);
            w.AppendLine(F(" Surface {0}{1}", i, figured ? "   (figured)" : ""));
            foreach (string[] g in LowerGroups)
            {
                Head(w, g);
                Named(w, "  int", g, n => lower.Intrinsic[i][n]);
                if (figured && lower.Aspheric[i] != null) Named(w, "  fig", g, n => lower.Aspheric[i]![n]);
                Named(w, "  ind", g, n => lower.Induced[i][n]);
                Named(w, "  tot", g, n => lower.PerSurface[i][n]);
            }
            w.AppendLine();
        }

        w.AppendLine(" System totals, transverse measure");
        foreach (string[] g in LowerGroups) { Head(w, g); Named(w, "  tot", g, n => lower.Totals[n]); }

        // --- seventh, Forbes -----------------------------------------------------------
        w.AppendLine();
        w.AppendLine("SEVENTH ORDER, by the Forbes series trace.");
        w.AppendLine();
        w.AppendLine("Per surface, transverse measure. The aperture power falls from seven to zero");
        w.AppendLine("across the twenty and the field power rises to meet it.");
        if (anyFigured)
            w.AppendLine("A figured surface carries a third row: what the figuring itself adds.");
        w.AppendLine();

        var totals = new double[21];
        for (int k = 1; k <= 20; k++) totals[k] = breakdown.Reference[k];

        foreach (var c in breakdown.Surfaces)
        {
            if (c == null) continue;
            for (int k = 1; k <= 20; k++) totals[k] += c.Total[k];
            bool figured = IsFigured(system.Surfaces[c.Surface]);
            w.AppendLine(F(" Surface {0}{1}", c.Surface, figured ? "   (figured)" : ""));
            for (int block = 0; block < 4; block++)
            {
                TauHead(w, block);
                Row(w, "  int", c.Intrinsic, block);
                if (figured) Row(w, "  fig", c.Aspheric, block);
                Row(w, "  ind", c.Induced, block);
                Row(w, "  tot", c.Total, block);
            }
            w.AppendLine();
        }

        // The reference belongs to no surface and is not zero. A paraxially perfect system
        // launched on direction cosines sends a ray at theta to about efl sin(theta) while the
        // coefficients are referred to efl tan(theta); that difference is odd and carries no
        // aperture, so it lands wholly in tau20. Folding it into the first surface would
        // misreport that surface badly, so it is printed as what it is.
        w.AppendLine(" Reference - every step linearised. Belongs to no surface; it is the");
        w.AppendLine(" sin-against-tan convention of the field variable, so it is all in tau20.");
        for (int block = 0; block < 4; block++) { TauHead(w, block); Row(w, "  ref", breakdown.Reference, block); }
        w.AppendLine();
        w.AppendLine(" System totals - the reference plus every surface");
        for (int block = 0; block < 4; block++) { TauHead(w, block); Row(w, "  tot", totals, block); }

        // --- the cross-check -----------------------------------------------------------
        // tau1 and B7 are the same quantity by two routes that share no code: a power-series
        // trace from Forbes, and Buchdahl's fifth-order working. Nothing substitutes one for
        // the other, so agreement means something. It holds on figured surfaces too - what
        // Buchdahl never published is the TERTIARY aspheric arrangement, the twenty tau, and
        // B7 is not from it.
        double tau1 = totals[1], b7 = lower.Totals.B7;
        double scale = Math.Max(Math.Abs(tau1), Math.Abs(b7));
        double slack = anyFigured ? 1e-4 : 1e-6;
        w.AppendLine();
        w.AppendLine(" Cross-check - the same quantity by two unrelated routes");
        w.AppendLine(F("   tau1, Forbes series trace        {0,15:0.000000E+00}", tau1));
        w.AppendLine(F("   B7, Buchdahl fifth-order working {0,15:0.000000E+00}", b7));
        if (scale > 0 && Math.Abs(tau1 - b7) / scale < slack)
        {
            w.AppendLine("   They agree.");
            if (anyFigured)
                w.AppendLine("   On a figured surface as well - what Buchdahl left unpublished is the"
                           + Environment.NewLine
                           + "   tertiary aspheric arrangement, the twenty tau, and B7 is not from it.");
        }
        else
        {
            w.AppendLine(F("   THEY DISAGREE, by {0:0.00E+00} relative, where they should not.",
                           scale > 0 ? Math.Abs(tau1 - b7) / scale : 0.0));
            w.AppendLine("   Trust neither until that is understood.");
        }
        return w.ToString();
    }

    private static bool IsFigured(Surface s) => s.IsFigured;

    private static void Head(StringBuilder w, string[] names)
    {
        w.Append("       ");
        foreach (string n in names) w.Append(F("{0,15}", n));
        w.AppendLine();
    }

    private static void TauHead(StringBuilder w, int block)
    {
        w.Append("       ");
        for (int k = block * 5 + 1; k <= block * 5 + 5; k++) w.Append(F("{0,15}", "tau" + k));
        w.AppendLine();
    }

    private static void Named(StringBuilder w, string tag, string[] names, Func<string, double> value)
    {
        w.Append(F("{0,-7}", tag));
        foreach (string n in names) w.Append(F("{0,15:0.000000E+00}", value(n)));
        w.AppendLine();
    }

    private static void Row(StringBuilder w, string tag, double[] v, int block)
    {
        w.Append(F("{0,-7}", tag));
        for (int k = block * 5 + 1; k <= block * 5 + 5; k++) w.Append(F("{0,15:0.000000E+00}", v[k]));
        w.AppendLine();
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(string f, params object?[] a) => string.Format(Inv, f, a);
}
