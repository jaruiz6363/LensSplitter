using System;
using System.Linq;

using AberrationCalculator.Core.Glass;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The glasses the optimiser may substitute in.
///
/// <para>Kept in their own folder, apart from the catalogues used to READ a design. Reading needs
/// every vendor present or an index resolves wrongly; choosing needs the opposite, because a
/// search free to pick from every catalogue at once will settle on a glass nobody stocks.</para>
/// </summary>
public class SubstitutionCatalogTests
{
    [Fact]
    public void CoreSet28IsBundledAndLoads()
    {
        var catalog = SubstitutionCatalog.Load("CoreSet28");

        // Twenty-eight is the name, and the point of it: a working set small enough to search.
        Assert.InRange(catalog.Count, 20, 40);

        // It is a real catalogue with real dispersion, not an empty file that parsed.
        var glasses = catalog.InCatalog("CORESET28").ToList();
        Assert.NotEmpty(glasses);
        Assert.All(glasses, g =>
        {
            Assert.False(string.IsNullOrWhiteSpace(g.Name));
            Assert.InRange(g.Nd, 1.3, 2.3);
            Assert.InRange(g.Vd, 15.0, 100.0);
        });
    }

    /// <summary>
    /// The file is UTF-16, where every other catalogue in this repository is ASCII. A loader that
    /// assumed one encoding would read it as line noise and silently find no glasses at all -
    /// which looks exactly like an empty catalogue.
    /// </summary>
    [Fact]
    public void TheCatalogueIsReadDespiteItsEncoding()
    {
        var catalog = SubstitutionCatalog.Load("CoreSet28");
        var f2 = catalog.Find("F2");
        Assert.NotNull(f2);
        Assert.Equal(1.62004, f2!.Nd, 4);
    }

    [Fact]
    public void ItIsListedByName()
    {
        Assert.Contains("CoreSet28", SubstitutionCatalog.Available(),
                        StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Naming a catalogue that is not there says which ones are, rather than failing silently
    /// with nothing to substitute.
    /// </summary>
    [Fact]
    public void AnUnknownCatalogueSaysWhatIsAvailable()
    {
        var ex = Assert.Throws<System.IO.FileNotFoundException>(
            () => SubstitutionCatalog.Load("NoSuchSet"));
        Assert.Contains("CoreSet28", ex.Message);
    }

    /// <summary>
    /// It must NOT pull in the analysis catalogues. A substitution set that quietly contained
    /// every Schott glass would defeat the whole reason for naming a set.
    /// </summary>
    [Fact]
    public void ItDoesNotMixWithTheAnalysisCatalogues()
    {
        var substitution = SubstitutionCatalog.Load("CoreSet28");
        var analysis = CatalogLocator.LoadBundled();

        Assert.True(analysis.Count > substitution.Count * 5,
                    "the analysis catalogues should be far larger");

        var loaded = substitution.LoadedCatalogs;
        Assert.Equal("CORESET28", Assert.Single(loaded), ignoreCase: true);
    }
}
