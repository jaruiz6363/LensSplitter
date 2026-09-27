using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Tests;

public class GlassAndMeritTests
{
    [Fact]
    public void TheDefaultMeritIsThePredictedSpotAndForSeveralWavelengthsTheColour()
    {
        var cooke = new LensOptics(Lenses.Cooke(), Lenses.Catalog);
        Assert.Equal(new[] { OperandType.PRMSA, OperandType.AXC, OperandType.LCF }, SplitMerit.DefaultFor(cooke).Select(o => o.Type));

        var mono = new LensOptics(Lenses.FiguredSinglet(), Lenses.Catalog);
        Assert.Equal(new[] { OperandType.PRMSA }, SplitMerit.DefaultFor(mono).Select(o => o.Type));
    }

    [Fact]
    public void AMeritFileIsAberrationCalculatorsFormatAndRoundTrips()
    {
        var ops = MeritFile.Parse(SplitMerit.DefaultTextFor(new LensOptics(Lenses.Cooke(), Lenses.Catalog)).Split('\n'));
        var again = MeritFile.Parse(MeritFile.Write(ops).Split('\n'));
        Assert.Equal(ops.Select(o => (o.Type, o.Weight, o.Target)), again.Select(o => (o.Type, o.Weight, o.Target)));
    }

    [Fact]
    public void TheSplitAddsTheFocalLengthAndEdgesToAnyMerit()
    {
        var ops = SplitMerit.ForSplit(SplitMerit.Default(), 50.0, 5, new SplitLimits(), 100, 100);
        Assert.Contains(ops, o => o.Type == OperandType.EFL && o.Target == 50.0);
        Assert.Contains(ops, o => o.Type == OperandType.EGT && o.Surface == 5 && o.Surface2 == 8 && o.Min == 0.5);
        Assert.Contains(ops, o => o.Type == OperandType.EAT && o.Surface == 4 && o.Surface2 == 8);
    }

    [Fact]
    public void AGlassSearchRanksPairsAndSplitsTheBest()
    {
        var pairs = GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, new GlassSearchOptions
        {
            Glasses = new[] { "N-BK7", "N-SK16", "N-LAK9" },
            Refine = 2,
            Split = new SplitOptions { Refine = 2 },
        });
        // Three glasses and the element's own SK16: sixteen ordered pairs.
        Assert.Equal(16, pairs.Count);
        Assert.True(pairs.Take(2).All(p => p.Result != null && double.IsFinite(p.Merit)));
        Assert.True(pairs[0].Merit <= pairs[1].Merit);
    }

    [Fact]
    public void TheDefaultGlassesAreCoreSet28AsTheVendorCatalogsNameThem()
    {
        var names = GlassSearch.DefaultGlasses(Lenses.Catalog);
        Assert.Equal(28, names.Count);
        Assert.Contains("N-BK7", names);
    }
}
