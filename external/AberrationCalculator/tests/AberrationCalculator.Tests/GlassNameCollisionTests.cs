using System;
using System.Linq;

using AberrationCalculator.Core.Glass;

using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// Which catalogue wins a bare glass name that no file claimed.
///
/// <para><b>Nothing tested this, and load order was deciding it.</b> The catalogues load
/// alphabetically, so CDGM answered for every name it shares - and its F series is RENUMBERED
/// against Schott's, so the wrong catalogue returns a neighbour from the same family rather than
/// an obviously different glass. CDGM's F3 is 1.616592, which is Schott's F4 exactly; CDGM's F4
/// is 1.620047, which is Schott's F2 to five decimals.</para>
///
/// <para><b>It was found from outside.</b> Kingslake's double Gauss read F4 as 1.620047 where
/// OpticStudio reads 1.616592, and the difference showed up as an eleven per cent disagreement in
/// the ninth-order coefficient between this program and the ZPL macro. The report had been saying
/// so all along - it names the catalogue it used - which is the part worth remembering: the
/// warning existed and was not read.</para>
/// </summary>
public class GlassNameCollisionTests
{
    private readonly ITestOutputHelper _out;
    public GlassNameCollisionTests(ITestOutputHelper o) { _out = o; }

    /// <summary>
    /// The nine names CDGM and Schott both use, with the index each gives at the d line. Every one
    /// differs, and the F series is offset by one place - which is why this cannot be left to
    /// whichever file the operating system lists first.
    /// </summary>
    [Theory]
    [InlineData("F1", 1.625882)]
    [InlineData("F2", 1.620040)]
    [InlineData("F3", 1.612931)]
    [InlineData("F4", 1.616592)]
    [InlineData("F5", 1.603420)]
    [InlineData("F6", 1.636359)]
    [InlineData("F7", 1.625361)]
    [InlineData("F13", 1.622372)]
    [InlineData("BAF4", 1.605621)]
    public void ABareNameResolvesToSchott(string name, double schottNd)
    {
        var catalog = CatalogLocator.LoadBundled();

        var owners = catalog.CatalogsContaining(name);
        Assert.True(owners.Count > 1,
            $"{name} is meant to be one of the ambiguous names; it is only in {string.Join(",", owners)}");

        var g = catalog.Find(name);
        Assert.NotNull(g);
        _out.WriteLine($"{name,-6} owners {string.Join(",", owners),-22} -> {g!.Catalog} {g.Nd}");

        Assert.Equal("SCHOTT", g.Catalog);
        Assert.Equal(schottNd, g.Nd, 5);
    }

    /// <summary>
    /// <b>A catalogue the file actually named still wins.</b> The fallback is for files that said
    /// nothing; it must not override a file that said something, or a design written in CDGM
    /// glasses would silently become a Schott one.
    /// </summary>
    [Fact]
    public void ADeclaredCatalogStillBeatsTheFallback()
    {
        var catalog = CatalogLocator.LoadBundled();

        var g = catalog.Find("F4", new[] { "CDGM" });
        Assert.NotNull(g);
        Assert.Equal("CDGM", g!.Catalog);
        Assert.Equal(1.620047, g.Nd, 5);
    }

    /// <summary>
    /// And a name Schott does not have still falls through to load order rather than failing -
    /// the preference is a tie-break, not a filter.
    /// </summary>
    [Fact]
    public void ANameSchottDoesNotHaveStillResolves()
    {
        var catalog = CatalogLocator.LoadBundled();

        var schottNames = catalog.InCatalog("SCHOTT").Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var elsewhere = catalog.InCatalog("CDGM").FirstOrDefault(x => !schottNames.Contains(x.Name));

        Assert.NotNull(elsewhere);
        var g = catalog.Find(elsewhere!.Name);
        Assert.NotNull(g);
        Assert.Equal(elsewhere.Name, g!.Name);
    }

    /// <summary>
    /// <b>The ambiguity is still declared.</b> A better guess is not a substitute for saying it was
    /// a guess, and this is the assertion that stops the fallback from being used to make the
    /// warning go away.
    /// </summary>
    [Fact]
    public void ResolvingItDoesNotSilenceIt()
    {
        var catalog = CatalogLocator.LoadBundled();

        Assert.Equal("SCHOTT", catalog.Find("F4")!.Catalog);
        Assert.Contains("CDGM", catalog.CatalogsContaining("F4"));
        Assert.True(catalog.CatalogsContaining("F4").Count >= 3);
    }
}
