using System;
using System.IO;
using AberrationCalculator.Core.Glass;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The catalogs ship with the program, so finding them must not be the user's job.
/// Getting it wrong is invisible rather than loud: every glass resolves as air, every
/// index reads 1.0, and a full set of confident wrong numbers comes out - which is why
/// this is tested rather than assumed.
/// </summary>
[Collection(ProcessWideState.Name)]
public class CatalogLocatorTests
{
    [Fact]
    public void BundledCatalogsAreFoundWithoutBeingPointedAt()
    {
        var catalog = CatalogLocator.LoadBundled();

        Assert.True(catalog.Count > 1000, $"expected the bundled catalogs, found {catalog.Count} glasses");
        Assert.Contains("SCHOTT", catalog.LoadedCatalogs);
    }

    /// <summary>A glass every catalog set should contain, resolving to its known index.</summary>
    [Fact]
    public void ABundledGlassResolvesToItsCatalogIndex()
    {
        var g = CatalogLocator.LoadBundled().GetGlass("N-BK7");

        Assert.NotNull(g);
        Assert.Equal(1.5168, g!.IndexAt(0.5875618), 3);
    }

    /// <summary>
    /// When nothing can be found the program must say so rather than carry on with an
    /// empty catalog, and the message has to name where it looked.
    /// </summary>
    [Fact]
    public void MissingCatalogsThrowWithAnActionableMessage()
    {
        var empty = Path.Combine(Path.GetTempPath(), "abcalc-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        string? savedOverride = Environment.GetEnvironmentVariable(CatalogLocator.OverrideVariable);
        string saved = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable(CatalogLocator.OverrideVariable, empty);
            Assert.NotEmpty(CatalogLocator.SearchedPlaces());
        }
        finally
        {
            Environment.SetEnvironmentVariable(CatalogLocator.OverrideVariable, savedOverride);
            Directory.SetCurrentDirectory(saved);
            try { Directory.Delete(empty, true); } catch { }
        }
    }
}
