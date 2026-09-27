using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Optimize.Io;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;

namespace LensSplitter.Core.Reporting;

/// <summary>What LensSplitter prints, and writes beside its lenses.</summary>
public static class Report
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string G(double v, int digits = 6) =>
        double.IsNaN(v) ? "-" : double.IsInfinity(v) ? (v > 0 ? "inf" : "-inf") : v.ToString("G" + digits, Inv);
    private static string E(double v) => double.IsFinite(v) ? v.ToString("0.0000E+00", Inv) : G(v);

    /// <summary>The lens, as first-order numbers and its predicted spot.</summary>
    public static string Lens(LensOptics o, string? source = null)
    {
        var s = o.System;
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(s.Title) ? "Lens" : s.Title);
        if (source != null) sb.AppendLine($"  File:          {source}");
        sb.AppendLine($"  Surfaces:      {s.Surfaces.Count} (object and image included)");
        sb.AppendLine($"  Focal length:  {G(o.Efl, 9)}");
        sb.AppendLine($"  F-number:      {G(o.Paraxial.FNumber, 5)}   (entrance pupil {G(o.Paraxial.Epd, 6)})");
        sb.AppendLine(double.IsInfinity(s.Surfaces[0].Thickness) || Math.Abs(s.Surfaces[0].Thickness) >= 1e10
            ? "  Object:        at infinity"
            : $"  Object:        at {G(s.Surfaces[0].Thickness, 6)}");
        string unit = s.FieldType == FieldType.ObjectHeight ? "" : " deg";
        sb.AppendLine($"  Fields:        {string.Join(", ", s.Fields.Select(f => G(f.Y, 6) + unit))}");
        sb.AppendLine($"  Wavelengths:   {string.Join(", ", s.Wavelengths.Select((w, i) => G(w.Value, 6) + (i == o.PrimaryWave ? "*" : "")))} um");
        sb.AppendLine($"  Predicted spot (Robb's PRMSA, RMS radius): {G(o.Prmsa, 5)}");
        return sb.ToString();
    }

    /// <summary>The elements, what each contributes to the spot, and which can be split.</summary>
    public static string Elements(IReadOnlyList<ElementReport> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Elements");
        sb.AppendLine("   #  Surfaces  Glass             Power        Spot share      S1          S2          S3        Split?");
        foreach (var r in reports)
            sb.AppendLine(string.Format(Inv, "  {0,2}  {1,3}-{2,-4}  {3,-16} {4,11} {5,12}  {6,11} {7,11} {8,11}   {9}",
                r.Element.Number + 1, r.Element.Front, r.Element.Rear, Trim(r.Element.Glass, 16), G(r.Power, 5),
                G(r.SpotPercent, 4) + " %", E(r.S1), E(r.S2), E(r.S3),
                r.NotSplittable == null ? (r.Figured ? "yes (figured)" : "yes") : "no: " + r.NotSplittable));
        sb.AppendLine("  Spot share: the element's share of the mean-square predicted spot, cross terms split evenly;");
        sb.AppendLine("  shares sum to 100 % over the lens, and a negative one is an element correcting the others.");
        var rec = ElementAnalysis.Recommend(reports);
        sb.AppendLine(rec == null
            ? "  Nothing to recommend: no splittable element is adding to the spot."
            : $"  Recommended: element {rec.Element.Number + 1}, the splittable element adding most to the spot.");
        return sb.ToString();
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "~";

    /// <summary>A split: what was done, and the lens before and after.</summary>
    public static string Split(SplitResult r)
    {
        var sb = new StringBuilder();
        var o = r.Original.System;
        var s = r.Split.System;
        int f = r.Front;
        sb.AppendLine($"Split of element {r.Element.Number + 1} (surfaces {r.Element.Front}-{r.Element.Rear}, {r.Element.Glass})");
        sb.AppendLine($"  Started from:  {r.Start}, of {r.StartingPoints} starting points; {r.Optimised} optimised");
        sb.AppendLine($"  Optimiser:     {r.Method}, {r.Iterations} iterations, stopped: {r.Stop}"
                      + (r.PolishIterations > 0 ? $"; then {r.PolishIterations} polishing" : ""));
        sb.AppendLine($"  Thicknesses:   {G(r.T1, 6)} glass, {G(r.Gap, 6)} air, {G(r.T2, 6)} glass; air after {G(s.Surfaces[f + 3].Thickness, 6)} (was {G(o.Surfaces[r.Element.Rear].Thickness, 6)})");
        sb.AppendLine();
        sb.AppendLine("  Surface      Radius             Thickness        Glass            Conic / aspheric");
        for (int i = f; i <= f + 3; i++)
        {
            var x = s.Surfaces[i];
            string fig = x.Conic != 0 || x.AsphericCoefficients.Any(c => c != 0)
                ? $"K {G(x.Conic, 6)}" + (x.AsphericCoefficients.Any(c => c != 0) ? ", terms kept" : "")
                : "";
            sb.AppendLine(string.Format(Inv, "  {0,5}{1}  {2,-18} {3,-16} {4,-16} {5}",
                i, x.IsStop ? "*" : " ", G(x.Radius, 10), G(x.Thickness, 8), LensElements.GlassName(x), fig));
        }
        sb.AppendLine("  (* the stop)");
        sb.AppendLine();
        sb.AppendLine("  Quantity                      Original           Split              Size");
        // The change in SIZE: an aberration of either sign is better smaller.
        void Row(string name, double a, double b, bool relative = true)
        {
            string change = !relative || Math.Abs(a) < 1e-300 ? "" :
                string.Format(Inv, "{0:+0.0;-0.0} %", 100.0 * (Math.Abs(b) - Math.Abs(a)) / Math.Abs(a));
            sb.AppendLine(string.Format(Inv, "  {0,-28} {1,-18} {2,-18} {3}", name, G(a), G(b), change));
        }
        Row("Predicted spot (PRMSA)", r.Original.Prmsa, r.Split.Prmsa);
        Row("Merit function", r.OriginalMerit, r.SplitMerit);
        Row("Focal length", r.Original.Efl, r.Split.Efl, false);
        Row("Back focal length", r.Original.Paraxial.Bfl, r.Split.Paraxial.Bfl, false);
        Row("S1 spherical", r.Original.Seidel.TotalS1, r.Split.Seidel.TotalS1);
        Row("S2 coma", r.Original.Seidel.TotalS2, r.Split.Seidel.TotalS2);
        Row("S3 astigmatism", r.Original.Seidel.TotalS3, r.Split.Seidel.TotalS3);
        Row("S4 Petzval", r.Original.Seidel.TotalS4, r.Split.Seidel.TotalS4);
        Row("S5 distortion", r.Original.Seidel.TotalS5, r.Split.Seidel.TotalS5);
        if (o.Wavelengths.Count > 1)
        {
            Row("CL axial colour", r.Original.Seidel.TotalCL, r.Split.Seidel.TotalCL);
            Row("CT lateral colour", r.Original.Seidel.TotalCT, r.Split.Seidel.TotalCT);
        }
        foreach (var name in new[] { "B", "F", "C", "Pi", "E", "B5", "F1", "M1", "N1", "B7" })
            Row("Buchdahl " + name, r.Original.Buchdahl.Totals[name], r.Split.Buchdahl.Totals[name]);
        sb.AppendLine("  Seidel sums are the wavefront coefficients; Buchdahl's are transverse, at the full field and aperture.");
        sb.AppendLine();
        sb.AppendLine(r.Improved
            ? $"  The split makes the predicted spot {G(r.Original.Prmsa / Math.Max(r.Split.Prmsa, 1e-300), 3)} times smaller."
            : "  The split does not make the predicted spot smaller. The lens may be better left as it is, or split elsewhere.");
        foreach (var n in r.Notes) sb.AppendLine("  Note: " + n);
        sb.AppendLine();
        sb.AppendLine("Merit function used (LensSplitter adds the focal length and edges):");
        foreach (var line in MeritFile.Write(r.Merit).Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine("  " + line.TrimEnd());
        return sb.ToString();
    }

    /// <summary>A glass search's ranking.</summary>
    public static string Glasses(IReadOnlyList<GlassPair> pairs, int show)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Glass pairs, best first (first half + second half)");
        sb.AppendLine("  Rank  Glasses                          Merit          Predicted spot   Screened at");
        int k = 0;
        foreach (var p in pairs.Take(show))
            sb.AppendLine(string.Format(Inv, "  {0,4}  {1,-32} {2,-14} {3,-16} {4}",
                ++k, p.A + " + " + p.B, p.Result == null ? "-" : G(p.Merit, 5),
                p.Result == null ? "-" : G(p.Result.Split.Prmsa, 5), G(p.Screen, 5)));
        sb.AppendLine("  Pairs not refined were screened only: their best unoptimised starting point.");
        return sb.ToString();
    }
}
