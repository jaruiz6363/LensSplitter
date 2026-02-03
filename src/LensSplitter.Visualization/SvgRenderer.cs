using System.Globalization;
using System.Text;
using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;

namespace LensSplitter.Visualization;

/// <summary>
/// Renders optical systems and split results to SVG format.
/// </summary>
public class SvgRenderer
{
    private readonly RenderSettings _settings;

    public SvgRenderer(RenderSettings? settings = null)
    {
        _settings = settings ?? new RenderSettings();
    }

    /// <summary>
    /// Renders an iterative split result showing before/after comparison with aberration analysis.
    /// </summary>
    /// <param name="result">The iterative split result.</param>
    /// <returns>SVG content as string.</returns>
    public string RenderIterativeComparison(IterativeSplitResult result)
    {
        var sb = new StringBuilder();

        // Calculate layout dimensions
        double systemWidth = CalculateSystemWidth(result.OriginalSystem);
        double splitWidth = CalculateSystemWidth(result.SplitSystem);
        double maxWidth = Math.Max(systemWidth, splitWidth);

        double margin = _settings.Margin;
        double labelHeight = 30;
        double systemHeight = _settings.MaxHeight;
        double gap = 40;
        double infoHeight = 80;

        double totalWidth = maxWidth + 2 * margin;
        double totalHeight = 2 * systemHeight + 2 * labelHeight + gap + infoHeight + 2 * margin;

        // SVG header
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(totalWidth)}\" height=\"{F(totalHeight)}\" viewBox=\"0 0 {F(totalWidth)} {F(totalHeight)}\">");
        sb.AppendLine("<defs>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    .lens-fill { fill: #b3d9ff; fill-opacity: 0.6; stroke: #0066cc; stroke-width: 1.5; }");
        sb.AppendLine("    .air-space { fill: none; stroke: #cccccc; stroke-width: 0.5; stroke-dasharray: 2,2; }");
        sb.AppendLine("    .optical-axis { stroke: #999999; stroke-width: 0.5; stroke-dasharray: 4,4; }");
        sb.AppendLine("    .marginal-ray { stroke: #0000ff; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("    .chief-ray { stroke: #00cc00; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("    .label { font-family: Arial, sans-serif; font-size: 14px; fill: #333333; }");
        sb.AppendLine("    .title { font-family: Arial, sans-serif; font-size: 16px; font-weight: bold; fill: #333333; }");
        sb.AppendLine("    .info { font-family: Arial, sans-serif; font-size: 12px; fill: #666666; }");
        sb.AppendLine("    .highlight { font-family: Arial, sans-serif; font-size: 13px; font-weight: bold; fill: #006600; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</defs>");
        sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{_settings.BackgroundColor}\"/>");

        // Original system (top)
        double y1 = margin;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(y1 + 16)}\" class=\"title\">Original System (EFL: {result.OriginalEfl:F2} mm)</text>");

        sb.AppendLine($"<g transform=\"translate({F(margin)}, {F(y1 + labelHeight)})\">");
        RenderSystem(sb, result.OriginalSystem, result.OriginalMarginalRay, result.OriginalChiefRay, maxWidth, systemHeight);
        sb.AppendLine("</g>");

        // Split system (bottom)
        double y2 = margin + systemHeight + labelHeight + gap;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(y2 + 16)}\" class=\"title\">Optimized Split System (EFL: {result.SplitEfl:F2} mm)</text>");

        sb.AppendLine($"<g transform=\"translate({F(margin)}, {F(y2 + labelHeight)})\">");
        RenderSystem(sb, result.SplitSystem, result.SplitMarginalRay, result.SplitChiefRay, maxWidth, systemHeight);
        sb.AppendLine("</g>");

        // Info panel with aberration analysis
        double infoY = totalHeight - margin - infoHeight;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY)}\" class=\"info\">Split element {result.SplitElementIndex}: Original power = {result.OriginalPower:F6}</text>");
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY + 16)}\" class=\"info\">Optimal power ratio = {result.OptimalPowerRatio:F4}, Air gap = {result.OptimalAirGap:F3} mm</text>");
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY + 32)}\" class=\"info\">Split powers: φ₁ = {result.OptimizedResult.Phi1:F6}, φ₂ = {result.OptimizedResult.Phi2:F6}</text>");
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY + 48)}\" class=\"info\">Single lens S₁ = {result.SingleLensS1:E4}, Split S₁ = {result.OptimizedResult.S1_Total:E4}</text>");
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY + 64)}\" class=\"highlight\">Spherical aberration reduction: {result.S1ReductionFactor:F2}x</text>");

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    /// Renders a split result showing before/after comparison.
    /// </summary>
    /// <param name="result">The split result.</param>
    /// <returns>SVG content as string.</returns>
    public string RenderComparison(SplitResult result)
    {
        var sb = new StringBuilder();

        // Calculate layout dimensions
        double systemWidth = CalculateSystemWidth(result.OriginalSystem);
        double splitWidth = CalculateSystemWidth(result.SplitSystem);
        double maxWidth = Math.Max(systemWidth, splitWidth);

        double margin = _settings.Margin;
        double labelHeight = 30;
        double systemHeight = _settings.MaxHeight;
        double gap = 40;

        double totalWidth = maxWidth + 2 * margin;
        double totalHeight = 2 * systemHeight + 2 * labelHeight + gap + 2 * margin;

        // SVG header
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(totalWidth)}\" height=\"{F(totalHeight)}\" viewBox=\"0 0 {F(totalWidth)} {F(totalHeight)}\">");
        sb.AppendLine("<defs>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    .lens-fill { fill: #b3d9ff; fill-opacity: 0.6; stroke: #0066cc; stroke-width: 1.5; }");
        sb.AppendLine("    .air-space { fill: none; stroke: #cccccc; stroke-width: 0.5; stroke-dasharray: 2,2; }");
        sb.AppendLine("    .optical-axis { stroke: #999999; stroke-width: 0.5; stroke-dasharray: 4,4; }");
        sb.AppendLine("    .marginal-ray { stroke: #0000ff; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("    .chief-ray { stroke: #00cc00; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("    .label { font-family: Arial, sans-serif; font-size: 14px; fill: #333333; }");
        sb.AppendLine("    .title { font-family: Arial, sans-serif; font-size: 16px; font-weight: bold; fill: #333333; }");
        sb.AppendLine("    .info { font-family: Arial, sans-serif; font-size: 12px; fill: #666666; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</defs>");
        sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{_settings.BackgroundColor}\"/>");

        // Original system (top)
        double y1 = margin;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(y1 + 16)}\" class=\"title\">Original System (EFL: {result.OriginalEfl:F2} mm)</text>");

        sb.AppendLine($"<g transform=\"translate({F(margin)}, {F(y1 + labelHeight)})\">");
        RenderSystem(sb, result.OriginalSystem, result.OriginalMarginalRay, result.OriginalChiefRay, maxWidth, systemHeight);
        sb.AppendLine("</g>");

        // Split system (bottom)
        double y2 = margin + systemHeight + labelHeight + gap;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(y2 + 16)}\" class=\"title\">Split System (EFL: {result.SplitEfl:F2} mm)</text>");

        sb.AppendLine($"<g transform=\"translate({F(margin)}, {F(y2 + labelHeight)})\">");
        RenderSystem(sb, result.SplitSystem, result.SplitMarginalRay, result.SplitChiefRay, maxWidth, systemHeight);
        sb.AppendLine("</g>");

        // Info panel
        double infoY = totalHeight - margin - 40;
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY)}\" class=\"info\">Split element {result.SplitElementIndex}: Original power = {result.OriginalPower:F6}, Split powers = {result.FirstElementPower:F6} + {result.SecondElementPower:F6}</text>");
        sb.AppendLine($"<text x=\"{F(margin)}\" y=\"{F(infoY + 16)}\" class=\"info\">Air gap = {result.AirGap:F2} mm, Power error = {result.RelativePowerError:F3}%</text>");

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    /// Renders a single optical system.
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <param name="marginalRay">Optional marginal ray to render.</param>
    /// <param name="chiefRay">Optional chief ray to render.</param>
    /// <returns>SVG content as string.</returns>
    public string RenderSystem(OpticalSystem system, ParaxialRay? marginalRay = null, ParaxialRay? chiefRay = null)
    {
        var sb = new StringBuilder();

        double width = CalculateSystemWidth(system) + 2 * _settings.Margin;
        double height = _settings.MaxHeight + 2 * _settings.Margin;

        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\">");
        sb.AppendLine("<defs>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    .lens-fill { fill: #b3d9ff; fill-opacity: 0.6; stroke: #0066cc; stroke-width: 1.5; }");
        sb.AppendLine("    .optical-axis { stroke: #999999; stroke-width: 0.5; stroke-dasharray: 4,4; }");
        sb.AppendLine("    .marginal-ray { stroke: #0000ff; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("    .chief-ray { stroke: #00cc00; stroke-width: 1.5; fill: none; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</defs>");
        sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{_settings.BackgroundColor}\"/>");

        sb.AppendLine($"<g transform=\"translate({F(_settings.Margin)}, {F(_settings.Margin)})\">");
        RenderSystem(sb, system, marginalRay, chiefRay, width - 2 * _settings.Margin, height - 2 * _settings.Margin);
        sb.AppendLine("</g>");

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private void RenderSystem(StringBuilder sb, OpticalSystem system, ParaxialRay? marginalRay, ParaxialRay? chiefRay, double width, double height)
    {
        // Calculate scale
        double systemLength = CalculateSystemLength(system);
        double maxSemiDiam = CalculateMaxSemiDiameter(system);

        double xScale = (width - 40) / Math.Max(systemLength, 1);
        double yScale = (height / 2 - 20) / Math.Max(maxSemiDiam, 1);
        double scale = Math.Min(xScale, yScale);

        double centerY = height / 2;
        double startX = 20;

        // Draw optical axis
        sb.AppendLine($"<line x1=\"0\" y1=\"{F(centerY)}\" x2=\"{F(width)}\" y2=\"{F(centerY)}\" class=\"optical-axis\"/>");

        // Draw lens elements
        double x = startX;
        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            var surface = system.Surfaces[i];
            var nextSurface = i + 1 < system.Surfaces.Count ? system.Surfaces[i + 1] : null;

            bool hasGlass = !string.IsNullOrEmpty(surface.GlassName) && surface.GlassName != "AIR";

            if (hasGlass && nextSurface != null)
            {
                // Draw lens element (this surface and next)
                RenderLensElement(sb, surface, nextSurface, x, centerY, scale);
            }

            x += surface.Thickness * scale;
        }

        // Draw rays
        if (marginalRay != null)
        {
            RenderRay(sb, system, marginalRay, startX, centerY, scale, "marginal-ray");
        }

        if (chiefRay != null)
        {
            RenderRay(sb, system, chiefRay, startX, centerY, scale, "chief-ray");
        }
    }

    private void RenderLensElement(StringBuilder sb, Surface front, Surface rear, double x, double centerY, double scale)
    {
        double semiDiam = Math.Max(front.SemiDiameter, rear.SemiDiameter);
        if (semiDiam <= 0) semiDiam = 10; // Default

        double thickness = front.Thickness * scale;
        double h = semiDiam * scale;

        // Calculate sag for curved surfaces
        double frontSag = CalculateSag(front.Radius, semiDiam) * scale;
        double rearSag = CalculateSag(rear.Radius, semiDiam) * scale;

        // Build path for lens element
        var path = new StringBuilder();
        path.Append($"M {F(x + frontSag)} {F(centerY - h)} ");

        // Front surface (top to bottom)
        if (front.IsFlat)
        {
            path.Append($"L {F(x)} {F(centerY + h)} ");
        }
        else
        {
            // Approximate arc with quadratic bezier
            double frontCx = x + frontSag * 2;
            path.Append($"Q {F(frontCx)} {F(centerY)} {F(x + frontSag)} {F(centerY + h)} ");
        }

        // Bottom edge to rear surface
        path.Append($"L {F(x + thickness + rearSag)} {F(centerY + h)} ");

        // Rear surface (bottom to top)
        if (rear.IsFlat)
        {
            path.Append($"L {F(x + thickness)} {F(centerY - h)} ");
        }
        else
        {
            double rearCx = x + thickness + rearSag * 2;
            path.Append($"Q {F(rearCx)} {F(centerY)} {F(x + thickness + rearSag)} {F(centerY - h)} ");
        }

        // Close path
        path.Append("Z");

        sb.AppendLine($"<path d=\"{path}\" class=\"lens-fill\"/>");
    }

    private void RenderRay(StringBuilder sb, OpticalSystem system, ParaxialRay ray, double startX, double centerY, double scale, string cssClass)
    {
        if (ray.States.Count < 2) return;

        var points = new List<string>();
        double x = startX;

        // Start before first optical surface
        if (ray.States.Count > 0)
        {
            var firstState = ray.States[0];
            double y = centerY - firstState.Height * scale;
            points.Add($"{F(x)},{F(y)}");
        }

        // Trace through surfaces
        for (int i = 1; i < ray.States.Count && i < system.Surfaces.Count; i++)
        {
            var prevSurface = system.Surfaces[i - 1];
            x += prevSurface.Thickness * scale;

            var state = ray.States[i];
            double y = centerY - state.Height * scale;
            points.Add($"{F(x)},{F(y)}");
        }

        if (points.Count >= 2)
        {
            sb.AppendLine($"<polyline points=\"{string.Join(" ", points)}\" class=\"{cssClass}\"/>");
        }
    }

    private double CalculateSystemWidth(OpticalSystem system)
    {
        double length = CalculateSystemLength(system);
        double maxSemiDiam = CalculateMaxSemiDiameter(system);

        return Math.Max(length, maxSemiDiam * 4) + 100;
    }

    private double CalculateSystemLength(OpticalSystem system)
    {
        double total = 0;
        foreach (var surface in system.Surfaces)
        {
            if (!double.IsInfinity(surface.Thickness))
            {
                total += Math.Abs(surface.Thickness);
            }
        }
        return total;
    }

    private double CalculateMaxSemiDiameter(OpticalSystem system)
    {
        double max = 10; // Minimum default
        foreach (var surface in system.Surfaces)
        {
            if (surface.SemiDiameter > max)
            {
                max = surface.SemiDiameter;
            }
        }
        return max;
    }

    private double CalculateSag(double radius, double semiDiameter)
    {
        if (double.IsInfinity(radius) || Math.Abs(radius) < 1e-10)
        {
            return 0;
        }

        double y = semiDiameter;
        double r = Math.Abs(radius);

        if (y >= r) return 0;

        double sag = r - Math.Sqrt(r * r - y * y);
        return radius > 0 ? sag : -sag;
    }

    private static string F(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Settings for SVG rendering.
/// </summary>
public class RenderSettings
{
    public double Margin { get; set; } = 40;
    public double MaxHeight { get; set; } = 200;
    public string BackgroundColor { get; set; } = "#ffffff";
    public string LensFillColor { get; set; } = "#b3d9ff";
    public string LensStrokeColor { get; set; } = "#0066cc";
    public string MarginalRayColor { get; set; } = "#0000ff";
    public string ChiefRayColor { get; set; } = "#00cc00";
}
