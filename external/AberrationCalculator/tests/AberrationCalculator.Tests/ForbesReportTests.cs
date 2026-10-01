using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The report the command line, the MCP server and the automation program all print.
///
/// <para>It is one formatter for the three of them, so these tests stand for all three. What
/// they check is not the layout but the two claims the report makes about itself: that the
/// cross-check at the foot of it is live rather than decorative, and that the truncation the
/// caller chooses does not change the answer.</para>
/// </summary>
public class ForbesReportTests
{
    private static string Build(string fixture, int degree)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(fixture), catalog);
        var writer = new ReportWriter(system, catalog, Fixtures.Lens(fixture));
        string? text = writer.BuildForbesText(degree);
        Assert.NotNull(text);
        return text!;
    }

    /// <summary>Reads a labelled value out of the printed cross-check.</summary>
    private static double Value(string report, string label)
    {
        var m = Regex.Match(report, Regex.Escape(label) + @"\s+(-?\d\.\d+E[+-]\d+)");
        Assert.True(m.Success, $"the report does not print '{label}':\n{report}");
        return double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// tau1 and B7 are seventh-order spherical aberration reached by two routes that share no
    /// code - a power series from Forbes, and Buchdahl's fifth-order working. Nothing in the
    /// program substitutes one for the other, so this is a real comparison and it is printed
    /// on every run. If it ever stops agreeing on a system of spheres, one of them is wrong.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    public void TheTwoRoutesToSeventhOrderSphericalAgree(string fixture)
    {
        string report = Build(fixture, 3);
        double tau1 = Value(report, "tau1, Forbes series trace");
        double b7 = Value(report, "B7, Buchdahl fifth-order working");

        double scale = Math.Max(Math.Abs(tau1), Math.Abs(b7));
        Assert.True(scale > 0, $"{fixture}: both routes report zero, so nothing is being compared");
        Assert.True(Math.Abs(tau1 - b7) / scale < 1e-6,
            $"{fixture}: tau1 {tau1:E6} against B7 {b7:E6}. These are the same quantity by two "
          + "unrelated routes and must agree on a system of spheres.");

        Assert.Contains("They agree.", report);
    }

    /// <summary>
    /// The truncation is a knob the caller can turn, and turning it must not move the answer -
    /// the seventh-order part of the series is the seventh-order part however much of the tail
    /// is carried. That it does not move is also the only check available that the series has
    /// converged for a particular design, which is why the program offers the knob at all.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Conic")]
    public void RaisingTheTruncationDoesNotChangeTheAnswer(string fixture)
    {
        double low = Value(Build(fixture, 3), "tau1, Forbes series trace");
        double high = Value(Build(fixture, 5), "tau1, Forbes series trace");

        Assert.True(Math.Abs(low) > 0, $"{fixture}: tau1 is zero, so this proves nothing");
        Assert.True(Math.Abs(low - high) / Math.Abs(low) < 1e-9,
            $"{fixture}: tau1 is {low:E6} at degree 3 and {high:E6} at degree 5. A higher "
          + "truncation carries more of the tail; it must not alter the seventh order.");
    }

    /// <summary>
    /// A figured surface is marked and gets the extra row. Buchdahl's scheme cannot separate
    /// that part at seventh order - the arrangement was never published - which is the whole
    /// reason the seventh order here comes from Forbes instead.
    /// </summary>
    [Fact]
    public void AFiguredSurfaceIsMarkedAndItsFiguringSeparated()
    {
        string report = Build("Ladder1_Conic", 3);
        Assert.Contains("(figured)", report);
        Assert.Contains("  fig", report);
    }

    /// <summary>
    /// The reference row belongs to no surface and is not zero: it is the sin-against-tan
    /// convention of the field variable, which lands wholly in tau20. Printing it as its own
    /// row rather than folding it into the first surface is the point, so it must be there.
    /// </summary>
    [Fact]
    public void TheReferenceRowIsPrintedRatherThanHiddenInASurface()
    {
        string report = Build("CookeTriplet", 3);
        Assert.Contains("Reference - every step linearised", report);
        Assert.Contains("  ref", report);
    }
}
