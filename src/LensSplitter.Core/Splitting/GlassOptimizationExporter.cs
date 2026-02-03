using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Exports glass optimization results to various file formats.
/// </summary>
public class GlassOptimizationExporter
{
    /// <summary>
    /// Exports results to a CSV file.
    /// </summary>
    public void ExportToCsv(GlassOptimizationResult result, string filePath)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Rank,Glass1,Glass2,N1,N2,V1,V2,PowerRatio,AirGap_mm," +
                     "S1_Total,S1_Improvement,LongColor_mm,LongColor_Improvement," +
                     "LatColor_mm,LatColor_Improvement,MeritScore,EFL_mm");

        // Original (unsplit) baseline row
        var origGlass = result.OriginalElement?.Glass;
        var origGlassName = origGlass?.Name ?? "UNKNOWN";
        var origNd = origGlass?.Nd ?? 0;
        var origVd = origGlass?.Vd ?? 0;
        sb.AppendLine(string.Join(",",
            "Orig",
            origGlassName,
            "(unsplit)",
            F(origNd),
            F(0),
            F(origVd),
            F(0),
            F(0),
            F(0),
            E(result.OriginalS1),
            F(1.0),
            F(result.OriginalChromaticAberration.LongitudinalColor),
            F(1.0),
            F(result.OriginalChromaticAberration.LateralColor),
            F(1.0),
            F(1.0),
            F(result.OriginalEfl)
        ));

        // Split with original glass row
        if (result.OriginalGlassSplitResult != null)
        {
            var og = result.OriginalGlassSplitResult;
            sb.AppendLine(string.Join(",",
                "Splt",
                og.Glass1Name,
                og.Glass2Name,
                F(og.N1),
                F(og.N2),
                F(og.V1),
                F(og.V2),
                F(og.PowerRatio),
                F(og.AirGap),
                E(og.TotalS1),
                F(og.S1Improvement),
                F(og.LongitudinalColor),
                F(og.LongColorImprovement),
                F(og.LateralColor),
                F(og.LatColorImprovement),
                F(og.MeritScore),
                F(og.Efl)
            ));
        }

        // Data rows
        foreach (var r in result.TopResults)
        {
            sb.AppendLine(string.Join(",",
                r.Rank,
                r.Glass1Name,
                r.Glass2Name,
                F(r.N1),
                F(r.N2),
                F(r.V1),
                F(r.V2),
                F(r.PowerRatio),
                F(r.AirGap),
                E(r.TotalS1),
                F(r.S1Improvement),
                F(r.LongitudinalColor),
                F(r.LongColorImprovement),
                F(r.LateralColor),
                F(r.LatColorImprovement),
                F(r.MeritScore),
                F(r.Efl)
            ));
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    /// <summary>
    /// Exports results to a JSON file.
    /// </summary>
    public void ExportToJson(GlassOptimizationResult result, string filePath)
    {
        var exportData = new GlassOptimizationExportData
        {
            Summary = new OptimizationSummary
            {
                ElementIndex = result.ElementIndex,
                OriginalPower = result.OriginalPower,
                OriginalS1 = result.OriginalS1,
                OriginalLongitudinalColor = result.OriginalChromaticAberration.LongitudinalColor,
                OriginalLateralColor = result.OriginalChromaticAberration.LateralColor,
                GlassesAvailable = result.AvailableGlasses.Count,
                CombinationsTried = result.TotalCombinationsTried,
                ResultsReturned = result.TopResults.Count,
                Weights = new WeightSettings
                {
                    W1 = result.Settings.AberrationWeights.W1,
                    W2 = result.Settings.AberrationWeights.W2,
                    W3 = result.Settings.AberrationWeights.W3,
                    W4 = result.Settings.AberrationWeights.W4,
                    W5 = result.Settings.AberrationWeights.W5
                }
            },
            Results = result.TopResults.Select(r => new GlassCombinationExportData
            {
                Rank = r.Rank,
                Glass1 = r.Glass1Name,
                Glass2 = r.Glass2Name,
                N1 = r.N1,
                N2 = r.N2,
                V1 = r.V1,
                V2 = r.V2,
                PowerRatio = r.PowerRatio,
                AirGap = r.AirGap,
                Phi1 = r.Phi1,
                Phi2 = r.Phi2,
                Aberrations = new AberrationData
                {
                    S1Total = r.TotalS1,
                    S1Element1 = r.S1_Element1,
                    S1Element2 = r.S1_Element2,
                    S1Improvement = r.S1Improvement,
                    LongitudinalColor = r.LongitudinalColor,
                    LongColorImprovement = r.LongColorImprovement,
                    LateralColor = r.LateralColor,
                    LatColorImprovement = r.LatColorImprovement
                },
                MeritScore = r.MeritScore,
                Efl = r.Efl
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(exportData, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Exports a summary report as text.
    /// </summary>
    public void ExportToText(GlassOptimizationResult result, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    GLASS OPTIMIZATION REPORT                         ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("ORIGINAL SYSTEM");
        sb.AppendLine("───────────────");
        sb.AppendLine($"  Element index:        {result.ElementIndex}");
        sb.AppendLine($"  Element power:        {result.OriginalPower:F6}");
        sb.AppendLine($"  Spherical aberration: {result.OriginalS1:E4}");
        sb.AppendLine($"  Longitudinal color:   {result.OriginalChromaticAberration.LongitudinalColor:F4} mm");
        sb.AppendLine($"  Lateral color:        {result.OriginalChromaticAberration.LateralColor:F4} mm (at {result.Settings.FieldAngleForLateralColor}° field)");
        sb.AppendLine();

        sb.AppendLine("OPTIMIZATION SETTINGS");
        sb.AppendLine("─────────────────────");
        sb.AppendLine($"  Glasses available:    {result.AvailableGlasses.Count}");
        sb.AppendLine($"  Combinations tried:   {result.TotalCombinationsTried}");
        var w = result.Settings.AberrationWeights;
        sb.AppendLine($"  Aberration weights:   W1={w.W1}, W2={w.W2}, W3={w.W3}, W4={w.W4}, W5={w.W5}");
        sb.AppendLine();

        sb.AppendLine("RESULTS");
        sb.AppendLine("───────");
        sb.AppendLine();
        sb.AppendLine("Rank  Glass1          Glass2          MF Improv  LongColor     LatColor      EFL (mm)");
        sb.AppendLine("────  ──────────────  ──────────────  ─────────  ────────────  ────────────  ────────");

        // Original (unsplit) baseline
        var origGlassName = result.OriginalElement?.Glass?.Name ?? "UNKNOWN";
        sb.AppendLine($"{"Orig",4}  {origGlassName,-14}  {"(unsplit)",-14}  {"1.00x",9}  {result.OriginalChromaticAberration.LongitudinalColor,11:F4}mm  {result.OriginalChromaticAberration.LateralColor,11:F4}mm  {result.OriginalEfl,8:F2}");

        // Split with original glass
        if (result.OriginalGlassSplitResult != null)
        {
            var og = result.OriginalGlassSplitResult;
            sb.AppendLine($"{"Splt",4}  {og.Glass1Name,-14}  {og.Glass2Name,-14}  {og.MeritScore,8:F2}x  {og.LongitudinalColor,11:F4}mm  {og.LateralColor,11:F4}mm  {og.Efl,8:F2}");
        }

        sb.AppendLine("────  ──────────────  ──────────────  ─────────  ────────────  ────────────  ────────");

        // Top 50 ranked results
        foreach (var r in result.TopResults.Take(50))
        {
            sb.AppendLine($"{r.Rank,4}  {r.Glass1Name,-14}  {r.Glass2Name,-14}  {r.MeritScore,8:F2}x  {r.LongitudinalColor,11:F4}mm  {r.LateralColor,11:F4}mm  {r.Efl,8:F2}");
        }

        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────────────");

        if (result.BestResult != null)
        {
            var best = result.BestResult;
            sb.AppendLine();
            sb.AppendLine("BEST CONFIGURATION DETAILS");
            sb.AppendLine("──────────────────────────");
            sb.AppendLine($"  Glass 1:              {best.Glass1Name} (n={best.N1:F4}, V={best.V1:F2})");
            sb.AppendLine($"  Glass 2:              {best.Glass2Name} (n={best.N2:F4}, V={best.V2:F2})");
            sb.AppendLine($"  Power ratio:          {best.PowerRatio:F4}");
            sb.AppendLine($"  Air gap:              {best.AirGap:F2} mm");
            sb.AppendLine($"  Element 1 power:      {best.Phi1:F6}");
            sb.AppendLine($"  Element 2 power:      {best.Phi2:F6}");
            sb.AppendLine($"  Effective focal len:  {best.Efl:F2} mm");
            sb.AppendLine();
            sb.AppendLine("  Aberration Improvement:");
            sb.AppendLine($"    S1:                 {result.OriginalS1:E4} → {best.TotalS1:E4} ({best.S1Improvement:F2}x better)");
            sb.AppendLine($"    Longitudinal color: {result.OriginalChromaticAberration.LongitudinalColor:F4} → {best.LongitudinalColor:F4} mm ({best.LongColorImprovement:F2}x better)");
            sb.AppendLine($"    Lateral color:      {result.OriginalChromaticAberration.LateralColor:F4} → {best.LateralColor:F4} mm ({best.LatColorImprovement:F2}x better)");
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string E(double value) => value.ToString("E4", CultureInfo.InvariantCulture);
}

// JSON export data structures
public class GlassOptimizationExportData
{
    public OptimizationSummary Summary { get; set; } = null!;
    public List<GlassCombinationExportData> Results { get; set; } = new();
}

public class OptimizationSummary
{
    public int ElementIndex { get; set; }
    public double OriginalPower { get; set; }
    public double OriginalS1 { get; set; }
    public double OriginalLongitudinalColor { get; set; }
    public double OriginalLateralColor { get; set; }
    public int GlassesAvailable { get; set; }
    public int CombinationsTried { get; set; }
    public int ResultsReturned { get; set; }
    public WeightSettings Weights { get; set; } = null!;
}

public class WeightSettings
{
    public double W1 { get; set; }
    public double W2 { get; set; }
    public double W3 { get; set; }
    public double W4 { get; set; }
    public double W5 { get; set; }
}

public class GlassCombinationExportData
{
    public int Rank { get; set; }
    public string Glass1 { get; set; } = string.Empty;
    public string Glass2 { get; set; } = string.Empty;
    public double N1 { get; set; }
    public double N2 { get; set; }
    public double V1 { get; set; }
    public double V2 { get; set; }
    public double PowerRatio { get; set; }
    public double AirGap { get; set; }
    public double Phi1 { get; set; }
    public double Phi2 { get; set; }
    public AberrationData Aberrations { get; set; } = null!;
    public double MeritScore { get; set; }
    public double Efl { get; set; }
}

public class AberrationData
{
    public double S1Total { get; set; }
    public double S1Element1 { get; set; }
    public double S1Element2 { get; set; }
    public double S1Improvement { get; set; }
    public double LongitudinalColor { get; set; }
    public double LongColorImprovement { get; set; }
    public double LateralColor { get; set; }
    public double LatColorImprovement { get; set; }
}
