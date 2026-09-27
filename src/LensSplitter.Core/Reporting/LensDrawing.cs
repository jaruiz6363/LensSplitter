using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using LensSplitter.Core.Lens;

namespace LensSplitter.Core.Reporting;

/// <summary>
/// A lens drawn in section as SVG: each glass outlined by its two faces' real sag (figuring
/// included) out to the aperture the paraxial beam needs, and the paraxial marginal and full-field
/// chief rays. Two lenses stack, one above the other, to the same scale - a lens and its split.
/// </summary>
public static class LensDrawing
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v) => v.ToString("0.###", Inv);

    public static string Svg(IReadOnlyList<(string Title, LensOptics Lens)> lenses, int highlightFrom = -1, int highlightTo = -1)
    {
        const double width = 900, panel = 300, pad = 40;
        // One scale for all: the longest track and the tallest aperture.
        double track = 0, height = 0;
        var layouts = new List<(double[] Z, double[] H, ParaxialResult P)>();
        foreach (var (_, o) in lenses)
        {
            var s = o.System;
            int last = s.LastOpticalSurface();
            var z = new double[s.Surfaces.Count];
            for (int i = 2; i < s.Surfaces.Count; i++) z[i] = z[i - 1] + (double.IsFinite(s.Surfaces[i - 1].Thickness) ? s.Surfaces[i - 1].Thickness : 0);
            var p = o.Paraxial;
            var h = new double[s.Surfaces.Count];
            for (int i = 1; i <= last + 1 && i < s.Surfaces.Count; i++)
                h[i] = Math.Max(s.Surfaces[i].SemiDiameter, Math.Abs(p.Y[i]) + Math.Abs(p.Ybar[i]));
            track = Math.Max(track, z[^1]);
            height = Math.Max(height, h.Max());
            layouts.Add((z, h, p));
        }
        double scale = Math.Min((width - 2 * pad) / Math.Max(track, 1e-9), (panel - 2 * pad) / Math.Max(2 * height, 1e-9));

        var sb = new StringBuilder();
        double total = panel * lenses.Count;
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(total)}\" viewBox=\"0 0 {F(width)} {F(total)}\">");
        sb.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        sb.AppendLine("<style>.glass{fill:#cfe6ff;stroke:#1f5fa8;stroke-width:1.2}.new{fill:#ffe2b8;stroke:#b3621b}"
                    + ".axis{stroke:#999;stroke-dasharray:4 4;stroke-width:.6}.marginal{stroke:#2a4fd6;fill:none;stroke-width:1}"
                    + ".chief{stroke:#1a9a3a;fill:none;stroke-width:1}.t{font:14px sans-serif;fill:#222}.stop{stroke:#000;stroke-width:2}</style>");

        for (int k = 0; k < lenses.Count; k++)
        {
            var (title, o) = lenses[k];
            var s = o.System;
            var (z, h, p) = layouts[k];
            double y0 = k * panel + panel / 2 + 10;
            double X(double zz) => pad + zz * scale;
            double Y(double yy) => y0 - yy * scale;

            sb.AppendLine($"<text class=\"t\" x=\"{F(pad)}\" y=\"{F(k * panel + 22)}\">{Escape(title)}</text>");
            sb.AppendLine($"<line class=\"axis\" x1=\"{F(pad / 2)}\" y1=\"{F(y0)}\" x2=\"{F(width - pad / 2)}\" y2=\"{F(y0)}\"/>");

            int last = s.LastOpticalSurface();
            for (int i = 1; i < last; i++)
            {
                if (!LensElements.GlassAfter(s, i)) continue;
                double r = Math.Min(h[i], h[i + 1]);
                bool isNew = k > 0 && i >= highlightFrom && i <= highlightTo;
                sb.AppendLine($"<path class=\"glass{(isNew ? " new" : "")}\" d=\"{Outline(s.Surfaces[i], s.Surfaces[i + 1], z[i], z[i + 1], r, X, Y)}\"/>");
            }
            if (s.StopSurfaceIndex > 0 && s.StopSurfaceIndex <= last)
            {
                int st = s.StopSurfaceIndex;
                double r = h[st];
                sb.AppendLine($"<line class=\"stop\" x1=\"{F(X(z[st]))}\" y1=\"{F(Y(r * 1.15))}\" x2=\"{F(X(z[st]))}\" y2=\"{F(Y(r))}\"/>");
                sb.AppendLine($"<line class=\"stop\" x1=\"{F(X(z[st]))}\" y1=\"{F(Y(-r * 1.15))}\" x2=\"{F(X(z[st]))}\" y2=\"{F(Y(-r))}\"/>");
            }
            // The paraxial rays, from the first surface to the image.
            sb.Append("<polyline class=\"marginal\" points=\"");
            for (int i = 1; i < s.Surfaces.Count; i++) sb.Append($"{F(X(z[i]))},{F(Y(p.Y[i]))} ");
            sb.AppendLine("\"/>");
            if (Math.Abs(o.MaxField) > 0)
            {
                sb.Append("<polyline class=\"chief\" points=\"");
                for (int i = 1; i < s.Surfaces.Count; i++) sb.Append($"{F(X(z[i]))},{F(Y(p.Ybar[i]))} ");
                sb.AppendLine("\"/>");
            }
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // A glass between two faces, closed at its edges, each face drawn by its real sag.
    private static string Outline(Surface a, Surface b, double za, double zb, double r, Func<double, double> X, Func<double, double> Y)
    {
        const int n = 40;
        var sb = new StringBuilder();
        double Sag(Surface s, double y) { double v = s.Sag(Math.Abs(y)); return double.IsFinite(v) ? v : 0.0; }
        for (int k = 0; k <= n; k++)
        {
            double y = -r + 2 * r * k / n;
            sb.Append(k == 0 ? "M" : "L").Append(F(X(za + Sag(a, y)))).Append(',').Append(F(Y(y))).Append(' ');
        }
        for (int k = n; k >= 0; k--)
        {
            double y = -r + 2 * r * k / n;
            sb.Append('L').Append(F(X(zb + Sag(b, y)))).Append(',').Append(F(Y(y))).Append(' ');
        }
        return sb.Append('Z').ToString();
    }

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
