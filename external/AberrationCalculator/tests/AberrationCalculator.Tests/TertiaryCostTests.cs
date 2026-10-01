using System;
using System.Collections.Generic;
using System.Diagnostics;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// What the two routes to the twenty tau actually cost.
///
/// <para>The case for finishing Buchdahl's aspheric arrangement rests partly on its being the
/// faster of the two, Forbes' series trace being a numerical expansion rather than a closed
/// form. That is worth measuring rather than assuming: if the two are comparable, one of the two
/// reasons for the work goes away, and if the gap is large it is worth knowing how large.</para>
/// </summary>
public class TertiaryCostTests
{
    private readonly ITestOutputHelper _out;
    public TertiaryCostTests(ITestOutputHelper output) => _out = output;

    private sealed record Loaded(Core.Models.OpticalSystem System, double[] Indices,
                                 ParaxialResult Paraxial, double Field);

    private static Loaded Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        return new Loaded(sys, n, ParaxialTrace.Trace(sys, n, field), field);
    }

    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("KingslakeDG")]
    public void HowMuchCheaperIsTheSchemeThanTheSeriesTrace(string fixtureName)
    {
        var d = Load(fixtureName);

        // Both routes once, to take the JIT and the first-touch allocations out of the timing.
        RunScheme(d);
        RunForbes(d);

        const int repeats = 50;

        var clock = Stopwatch.StartNew();
        for (int i = 0; i < repeats; i++) RunScheme(d);
        double scheme = clock.Elapsed.TotalMilliseconds / repeats;

        clock.Restart();
        for (int i = 0; i < repeats; i++) RunForbes(d);
        double forbes = clock.Elapsed.TotalMilliseconds / repeats;

        _out.WriteLine($"{fixtureName,-40} scheme {scheme,8:F3} ms   "
                     + $"Forbes {forbes,8:F3} ms   ratio {forbes / scheme,7:F1}x");

        Assert.True(scheme > 0.0 && forbes > 0.0, "one of the routes did no work");
    }

    /// <summary>Buchdahl's closed-form scheme: the whole coefficient set, no rays traced.</summary>
    private static void RunScheme(Loaded d)
    {
        var b = BuchdahlCoefficients.Compute(d.System, d.Paraxial);
        TertiaryCoefficients.Attach(d.System, d.Indices, d.Paraxial, b, d.Field);
    }

    /// <summary>Forbes' series trace, inverted to the same twenty tau.</summary>
    private static void RunForbes(Loaded d)
        => ForbesCoefficients.Invert(d.System, d.Indices, d.Paraxial, d.Field);
}
