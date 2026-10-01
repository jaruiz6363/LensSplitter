using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The Laurent-series route for a figured flat facing collimated light, and the gates it is built
/// against. See <c>TertiaryCoefficients.FlatCollimated.cs</c>.
/// </summary>
public class FlatCollimatedSeriesTests
{
    private static (Core.Models.OpticalSystem Sys, double[] N, double Field) Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        return (sys, n, field);
    }

    private static double[] Shipped(string name)
    {
        var (sys, n, field) = Load(name);
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var tau = new double[21];
        for (int k = 1; k <= 20; k++)
            tau[k] = k == 1 ? b.Totals.B7
                   : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;
        return tau;
    }

    /// <summary>
    /// <b>The gate on the arithmetic itself.</b> Seed a surface that is NOT singular with
    /// <c>c + e</c>: the series route must then give back, at e^0, exactly what the double route
    /// gives - the same formulas in a different number system - and report that it converged.
    /// This is what makes its answer on a flat worth anything.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_A4_Both", 2)]
    [InlineData("Ladder3_A4_Middle", 3)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE", 1)]
    public void TheSeriesRouteReproducesTheDoubleRouteOnARegularSurface(string name, int surface)
    {
        var (sys, n, field) = Load(name);
        var series = TertiaryCoefficients.SeriesTau(sys, n, field, new[] { surface });
        var shipped = Shipped(name);

        Assert.True(series.Converged,
            $"{name}: the series route did not vouch for itself - underflows {series.Underflows}, "
          + $"dropped {series.WorstDroppedLeading:E2}, negative orders {series.NegativeOrderResidue:E2}, "
          + $"truncations disagree by {series.TruncationDisagreement:E2}.\n{series.Trace}");

        double largest = 0.0;
        for (int k = 2; k <= 20; k++) largest = Math.Max(largest, Math.Abs(shipped[k]));
        for (int k = 2; k <= 20; k++)
            Assert.True(Math.Abs(series.Tau[k] - shipped[k]) <= 1e-10 * largest,
                $"{name} tau{k}: series {series.Tau[k]:E15}, double {shipped[k]:E15}.");
    }

    /// <summary>
    /// <b>The figured flat in collimated light, through the shipping path.</b> Every tau against
    /// Forbes' series trace. The flat branch alone gave 710 per cent here.
    /// </summary>
    [Fact]
    public void AFiguredFlatInCollimatedLightAgreesWithForbes()
    {
        const string name = "Ladder2_FlatFigured";
        var (sys, n, field) = Load(name);
        var p = ParaxialTrace.Trace(sys, n, field);
        var forbes = ForbesCoefficients.Invert(sys, n, p, field);
        Assert.NotNull(forbes);
        var mine = Shipped(name);

        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(forbes!.Tau[k]));
        for (int k = 1; k <= 20; k++)
        {
            double reference = forbes!.Tau[k];
            if (Math.Abs(reference) < 1e-9 * largest) continue;
            double rel = Math.Abs(mine[k] - reference) / Math.Abs(reference);
            Assert.True(rel < 1e-8,
                $"tau{k}: reported {mine[k]:E12}, Forbes {reference:E12}, relative {rel:E2}.");
        }
    }

    /// <summary>And on the flat the series route vouches for its own answer.</summary>
    [Fact]
    public void OnTheFlatTheSeriesRouteVouchesForItself()
    {
        var (sys, n, field) = Load("Ladder2_FlatFigured");
        var r = TertiaryCoefficients.SeriesTau(sys, n, field, new[] { 2 });
        Assert.True(r.Converged,
            $"underflows {r.Underflows}, dropped {r.WorstDroppedLeading:E2}, negative orders "
          + $"{r.NegativeOrderResidue:E2}, truncations disagree by {r.TruncationDisagreement:E2}.");
    }
}
