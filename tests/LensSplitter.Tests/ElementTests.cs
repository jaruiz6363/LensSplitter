using AberrationCalculator.Core.Aberrations;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Tests;

public class ElementTests
{
    [Fact]
    public void TheCookeTripletHasThreeElementsAndTheNegativeOneIsNotSplit()
    {
        var reports = ElementAnalysis.Analyse(new LensOptics(Lenses.Cooke(), Lenses.Catalog));
        Assert.Equal(3, reports.Count);
        Assert.Equal(new[] { 1, 3, 5 }, reports.Select(r => r.Element.Front));
        Assert.Equal("negative power", reports[1].NotSplittable);
        Assert.True(reports[0].Splittable && reports[2].Splittable);
    }

    /// <summary>The shares of the spot are a decomposition: over every surface they add up to the whole.</summary>
    [Fact]
    public void TheSpotSharesAddUpToTheSpot()
    {
        var optics = new LensOptics(Lenses.Cooke(), Lenses.Catalog);
        double total = optics.SpotShareBySurface().Sum();
        double ms = 0, w = 0;
        foreach (var (h, weight) in optics.FieldPoints())
        {
            ms += weight * Prms.MeanSquare(optics.Buchdahl.Totals, h);
            w += weight;
        }
        Assert.Equal(ms / w, total, 12);
    }

    [Fact]
    public void ACementedDoubletIsNeitherRecommendedNorSplit()
    {
        var lens = Lenses.CementedDoublet();
        var reports = ElementAnalysis.Analyse(new LensOptics(lens, Lenses.Catalog));
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.Equal("part of a cemented doublet", r.NotSplittable));
        Assert.Null(ElementAnalysis.Recommend(reports));
        Assert.Throws<InvalidOperationException>(() => Splitter.Split(lens, Lenses.Catalog, 0));
    }

    [Fact]
    public void AFiguredElementCanBeSplit()
    {
        var reports = ElementAnalysis.Analyse(new LensOptics(Lenses.FiguredSinglet(), Lenses.Catalog));
        Assert.Single(reports);
        Assert.True(reports[0].Figured);
        Assert.True(reports[0].Splittable);
    }
}
