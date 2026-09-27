using AberrationCalculator.Optimize.Io;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Tests;

/// <summary>The glass search: screening, refining, ranking, and what the refined splits are.</summary>
public class GlassSearchTests
{
    // Kept small: each refined pair is a whole optimised split.
    private static GlassSearchOptions Small(IReadOnlyList<string> glasses, int refine, IReadOnlyList<AberrationCalculator.Optimize.Operands.Operand>? merit = null) => new()
    {
        Glasses = glasses,
        Refine = refine,
        Split = new SplitOptions { Refine = 1, Iterations = 150, Merit = merit },
    };

    private static readonly Lazy<List<GlassPair>> Cooke3 = new(() =>
        GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, Small(new[] { "N-BK7", "N-SSK5", "N-LAK9" }, 3)));

    [Fact]
    public void EveryOrderedPairIsScreenedOnceTheElementsOwnGlassIncluded()
    {
        var pairs = Cooke3.Value;
        var glasses = new[] { "N-BK7", "N-SSK5", "N-LAK9", "SK16" };
        Assert.Equal(glasses.Length * glasses.Length, pairs.Count);
        Assert.Equal(pairs.Count, pairs.Select(p => (p.A, p.B)).Distinct().Count());
        foreach (var a in glasses)
            foreach (var b in glasses)
                Assert.Contains(pairs, p => p.A == a && p.B == b);
        Assert.All(pairs, p => Assert.True(double.IsFinite(p.Screen)));
    }

    [Fact]
    public void TheRefinedPairsComeFirstRankedByMeritThenTheRestByScreen()
    {
        var pairs = Cooke3.Value;
        var refined = pairs.Where(p => p.Result != null).ToList();
        Assert.Equal(3, refined.Count);
        Assert.Equal(refined, pairs.Take(3));
        Assert.Equal(refined.Select(p => p.Merit).OrderBy(m => m), refined.Select(p => p.Merit));
        var screened = pairs.Skip(3).Select(p => p.Screen).ToList();
        Assert.Equal(screened.OrderBy(s => s), screened);
        Assert.All(pairs.Skip(3), p => Assert.True(double.IsPositiveInfinity(p.Merit)));
    }

    /// <summary>The pairs refined are the best-screened ones.</summary>
    [Fact]
    public void ThePairsRefinedAreTheBestScreened()
    {
        var pairs = Cooke3.Value;
        double worstRefined = pairs.Where(p => p.Result != null).Max(p => p.Screen);
        double bestOther = pairs.Where(p => p.Result == null).Min(p => p.Screen);
        Assert.True(worstRefined <= bestOther);
    }

    [Fact]
    public void ARefinedSplitIsOfItsGlassesWithTheFocalLengthExact()
    {
        foreach (var p in Cooke3.Value.Where(p => p.Result != null))
        {
            var r = p.Result!;
            Assert.Equal(p.A, r.Split.System.Surfaces[r.Front].Material);
            Assert.Null(r.Split.System.Surfaces[r.Front + 1].Material);
            Assert.Equal(p.B, r.Split.System.Surfaces[r.Front + 2].Material);
            Assert.Equal(r.Original.Efl, r.Split.Efl, 9);
            Assert.Empty(r.Split.Unresolved);
        }
    }

    /// <summary>
    /// The ranking is by the merit function the search was given. With the predicted spot alone,
    /// a pair's merit IS its predicted spot.
    /// </summary>
    [Fact]
    public void TheRankingIsByTheMeritFunctionGiven()
    {
        var spotOnly = MeritFile.Parse(new[] { "PRMSA, 1, TAR 0" });
        var pairs = GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, Small(new[] { "N-BK7", "N-SSK5" }, 2, spotOnly));
        foreach (var p in pairs.Where(p => p.Result != null))
            Assert.Equal(p.Result!.Split.Prmsa, p.Merit, 9);
    }

    [Fact]
    public void ScreeningInParallelGivesTheSameAnswerEveryTime()
    {
        var options = new GlassSearchOptions { Glasses = new[] { "N-BK7", "N-SSK5", "N-LAK9" }, Refine = 0 };
        // Refine 0 still refines one: the answer must have a split.
        var a = GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, options);
        var b = GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, options);
        Assert.Equal(a.Select(p => (p.A, p.B, p.Screen)), b.Select(p => (p.A, p.B, p.Screen)));
    }

    [Fact]
    public void AGlassTheCatalogsLackIsRefusedByName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, Small(new[] { "N-BK7", "NOSUCHGLASS" }, 1)));
        Assert.Contains("NOSUCHGLASS", ex.Message);
    }

    [Fact]
    public void AnElementThatDoesNotExistIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 5, Small(new[] { "N-BK7" }, 1)));
    }

    /// <summary>
    /// The element's own glass is always a candidate, so the search can say "keep it"; a
    /// candidate list that already names it does not get it twice.
    /// </summary>
    [Fact]
    public void TheElementsOwnGlassIsACandidateOnce()
    {
        var pairs = GlassSearch.Search(Lenses.Cooke(), Lenses.Catalog, 2, Small(new[] { "SK16", "N-BK7" }, 1));
        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.A == "SK16" && p.B == "SK16");
    }

    [Fact]
    public void AFiguredElementsSplitKeepsItsFiguringWhateverTheGlasses()
    {
        var lens = Lenses.FiguredSinglet();
        var pairs = GlassSearch.Search(lens, Lenses.Catalog, 0, Small(new[] { "N-BK7", "N-SK16" }, 1));
        var r = pairs[0].Result!;
        Assert.Equal(-0.6, r.Split.System.Surfaces[1].Conic, 12);
        Assert.Equal(lens.Surfaces[1].AsphericCoefficients, r.Split.System.Surfaces[1].AsphericCoefficients);
        Assert.Equal(r.Original.Efl, r.Split.Efl, 9);
    }
}
