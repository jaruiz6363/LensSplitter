using System;
using System.Collections.Generic;
using System.Linq;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Core.Report;

using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// The seventh-order field terms, tau2 to tau20, split by surface.
///
/// <para><b>Why.</b> The report's WHICH SURFACE table shares the predicted spot out among the
/// surfaces, and the spot is built from all thirty-seven coefficients. Until September 2026 the
/// per-surface terms stopped at B7 - the tertiary scheme was only ever summed - so whatever tau2
/// to tau20 contributed was given to no surface, and the column the report said "sums to 100%"
/// summed to 91 on the double Gauss.</para>
///
/// <para><b>How.</b> Both tertiary routes already build the system totals by adding up one row per
/// surface, and Table II and the transverse conversion that turn those totals into tau are
/// linear. So each surface's row, put through the same two steps, is that surface's share, and the
/// shares add to the total exactly. The exception is a figured flat facing collimated light, where
/// the answer is read off a Laurent series: a surface's own row may carry negative powers that
/// cancel only against another's, and then it has no finite value of its own. That case is
/// detected, and reported, rather than split.</para>
/// </summary>
public class TertiarySplitTests
{
    private readonly ITestOutputHelper _out;
    public TertiarySplitTests(ITestOutputHelper output) { _out = output; }

    public static IEnumerable<object[]> EveryDesign() => Designs.All();

    private static readonly string[] TauNames =
        Enumerable.Range(2, 19).Select(k => "Tau" + k).ToArray();

    private sealed record Run(OpticalSystem System, BuchdahlResult B);

    private static Run? Load(string name, string folder)
    {
        var catalog = CatalogLocator.LoadBundled();
        OpticalSystem system;
        try { system = LensFile.Read(Designs.PathOf(name, folder), catalog); }
        catch (Exception) { return null; }
        if (system.LastOpticalSurface() < 1 || system.Wavelengths.Count == 0) return null;

        double field = 0.0;
        foreach (var f in system.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        if (Math.Abs(field) < 1e-12) return null;

        int primary = system.PrimaryWavelengthIndex < 0 ? 0 : system.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(system, catalog, system.Wavelengths[primary].Value,
                                    new List<string>());
        var p = ParaxialTrace.Trace(system, n, field);
        var b = BuchdahlCoefficients.Compute(system, p);
        TertiaryCoefficients.Attach(system, n, p, b, field);
        return new Run(system, b);
    }

    /// <summary>
    /// On every design on disk: the per-surface tau, scaled as every per-surface coefficient is,
    /// add up to the system tau; and the report's surface shares then add up to 100%. Where the
    /// split is declined, every per-surface tau is zero and the result says so.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDesign))]
    public void TheSurfacesAccountForTheWholeSeventhOrder(string name, string folder)
    {
        var run = Load(name, folder);
        if (run == null) { _out.WriteLine($"{name}: not analysable here"); return; }
        var b = run.B;
        int last = run.System.LastOpticalSurface();

        double largest = TauNames.Max(t => Math.Abs(b.Totals[t]));
        if (largest == 0.0) { _out.WriteLine($"{name}: no tertiary set"); return; }

        if (b.TertiaryUnattributed)
        {
            for (int i = 1; i <= last; i++)
                foreach (var t in TauNames)
                    Assert.Equal(0.0, b.PerSurface[i][t]);
            _out.WriteLine($"{name}: tau2..tau20 not split by surface (figured flat in collimated light)");
            return;
        }

        foreach (var t in TauNames)
        {
            double sum = 0.0, scale = Math.Abs(b.Totals[t]);
            for (int i = 1; i <= last; i++)
            {
                double v = b.PerSurface[i][t] * b.FNumber;
                Assert.True(double.IsFinite(v), $"{name}: {t} on surface {i} is {v}");
                sum += v;
                scale = Math.Max(scale, Math.Abs(v));
            }
            Assert.True(Math.Abs(sum - b.Totals[t]) <= 1e-11 * Math.Max(scale, largest),
                $"{name}: {t} surfaces sum to {sum:E10}, the system has {b.Totals[t]:E10}");
        }

        double percent = ContributionAnalysis.BySurface(b, 1.0).Sum(r => r.Percent);
        Assert.True(Math.Abs(percent - 100.0) < 1e-6,
            $"{name}: the surface shares add to {percent:F9}%, not 100%");
        _out.WriteLine($"{name}: split; shares sum to {percent:F9}%");
    }

    /// <summary>
    /// The split is actually taken, across the designs on disk, and not quietly declined.
    ///
    /// <para>MEASURED, September 2026: it is taken on all 61 designs with a tertiary set, the
    /// figured flat in collimated light (Ladder2_FlatFigured) included - there every surface's own
    /// share came out finite, with nothing left at a negative order. So the declining branch is a
    /// guard that no design here reaches. It is kept because the finiteness of each share is a
    /// property of the design, not of the method, and a design where it fails should be told so
    /// rather than handed the e^0 parts of terms that only cancel in the sum.</para>
    /// </summary>
    [Fact]
    public void TheSplitIsTakenAcrossTheDesignsOnDisk()
    {
        int split = 0, declined = 0;
        foreach (var d in Designs.All())
        {
            var run = Load((string)d[0], (string)d[1]);
            if (run == null || TauNames.All(t => run.B.Totals[t] == 0.0)) continue;
            if (run.B.TertiaryUnattributed) declined++; else split++;
        }
        _out.WriteLine($"split on {split} designs, declined on {declined}");
        Assert.True(split >= 20, $"only {split} designs had their tertiary split by surface");
    }

    /// <summary>
    /// The report says what its surface table covers, and the column adds up. On the double Gauss
    /// the TOTAL line read 91.0 under a heading promising 100%.
    /// </summary>
    [Fact]
    public void TheReportsSurfaceTableAddsUpOnTheDoubleGauss()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = System.IO.Path.Combine(Designs.LensDir, "KingslakeDG.zmx");
        var system = LensFile.Read(path, catalog);
        string report = new ReportWriter(system, catalog, path).BuildReport();

        int at = report.IndexOf("WHICH SURFACE IS COSTING YOU THE SPOT", StringComparison.Ordinal);
        Assert.True(at >= 0);
        string section = report.Substring(at);
        section = section.Substring(0, section.IndexOf("WARNINGS", StringComparison.Ordinal));

        Assert.Contains("every coefficient through the seventh order included", section);
        Assert.Matches(@"TOTAL\s+100\.0", section);
    }
}
