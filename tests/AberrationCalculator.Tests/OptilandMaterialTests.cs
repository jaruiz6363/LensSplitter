using System;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Materials in Optiland files, as Optiland writes them and as LensHH-LT writes them for it. This
/// reader kept only a <c>Material</c>'s name and turned every other material into air. It did not
/// know that a glass's catalog decides which glass the name is.
/// </summary>
public class OptilandMaterialTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acoptiland_" + Guid.NewGuid().ToString("N"));

    public OptilandMaterialTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // N-BK7 in SCHOTT, and an SK16 in both SCHOTT and SUMITA that differ.
    private GlassCatalog Catalogs()
    {
        File.WriteAllText(Path.Combine(_dir, "SCHOTT.AGF"),
            "NM N-BK7 2 517642 1.5168 64.17 0 1 0\r\n" +
            "CD 1.03961212 0.00600069867 0.231792344 0.0200179144 1.01046945 103.560653 0 0 0 0\r\n" +
            "LD 0.3 2.5\r\n" +
            "NM SK16 2 620603 1.62041 60.32 0 1 0\r\n" +
            "CD 1.34317774 0.00704687339 0.241144399 0.0229005 0.994317969 92.7508526 0 0 0 0\r\n" +
            "LD 0.31 2.5\r\n");
        File.WriteAllText(Path.Combine(_dir, "SUMITA.AGF"),
            "NM SK16 1 620603 1.62041 60.34 0 1 0\r\n" +
            "CD 2.5 -0.01 0.018 0.0003 0.00001 0.000001 0 0 0 0\r\n" +
            "LD 0.36 1.0\r\n");
        var glass = new GlassCatalog();
        glass.LoadFile(Path.Combine(_dir, "SCHOTT.AGF"));
        glass.LoadFile(Path.Combine(_dir, "SUMITA.AGF"));
        return glass;
    }

    private static string Surface(double z, string material) =>
        "{\"type\": \"Surface\", \"geometry\": {\"type\": \"StandardGeometry\", \"cs\": {\"z\": " + z + "}, \"radius\": 50.0, \"conic\": 0.0}, " +
        "\"material_post\": " + material + ", \"is_stop\": false}";

    private static string Glass(string name, string catalog) =>
        "{\"type\": \"Material\", \"name\": \"" + name + "\", \"reference\": null, \"catalog\": \"" + catalog +
        "\", \"match_policy\": \"strict\", \"robust_search\": null, \"min_wavelength\": null, \"max_wavelength\": null}";

    [Fact]
    public void EachMaterialIsReadAsTheGlassItNames()
    {
        string json = "{\"aperture\": {\"type\": \"EPD\", \"value\": 10.0}, \"surface_group\": {\"surfaces\": [" +
            "{\"type\": \"ObjectSurface\", \"geometry\": {\"type\": \"Plane\", \"cs\": {\"z\": -Infinity}, \"radius\": Infinity}, \"material_post\": {\"type\": \"IdealMaterial\", \"index\": 1.0}}, " +
            Surface(0, Glass("N-BK7", "lenshh-schott")) + ", " +
            Surface(5, Glass("SK16", "lenshh-sumita")) + ", " +
            Surface(8, Glass("MODEL_1.78470000_25.680000_0.00920000", "lenshh-model")) + ", " +
            Surface(10, "{\"type\": \"AbbeMaterial\", \"index\": 1.6, \"abbe\": 40.0}") + ", " +
            Surface(12, "{\"type\": \"IdealMaterial\", \"index\": 1.45, \"absorp\": 0.0}") + ", " +
            Surface(14, "{\"type\": \"IdealMaterial\", \"index\": 1.0}") + "]}}";
        string path = Path.Combine(_dir, "lens.json");
        File.WriteAllText(path, json);
        var glass = Catalogs();

        var sys = OptilandReader.Read(path, glass);

        // SK16 is SUMITA's, though N-BK7 named SCHOTT first and SCHOTT has an SK16 too.
        Assert.Equal("N-BK7", sys.Surfaces[1].Material);
        Assert.Equal("SK16", sys.Surfaces[2].Material);
        Assert.Equal("SUMITA", glass.Find("SK16", sys.GlassCatalogs)!.Catalog);
        Assert.Equal("SCHOTT", glass.Find("N-BK7", sys.GlassCatalogs)!.Catalog);

        var m = sys.Surfaces[3];
        Assert.True(m.ModelIndexEnabled);
        Assert.Equal(1.7847, m.ModelNd, 8);
        Assert.Equal(25.68, m.ModelVd, 6);
        Assert.Equal(0.0092, m.ModelDPgF, 8);

        Assert.True(sys.Surfaces[4].ModelIndexEnabled);
        Assert.Equal(40.0, sys.Surfaces[4].ModelVd);
        Assert.True(sys.Surfaces[5].ModelIndexEnabled);
        Assert.Equal(1.45, IndexResolver.ModelIndex(sys.Surfaces[5].ModelNd, sys.Surfaces[5].ModelVd, 0.0, 0.45));
        Assert.False(sys.Surfaces[6].ModelIndexEnabled);
        Assert.Null(sys.Surfaces[6].Material);

        Assert.Contains("Abbe material", sys.Notes);
        Assert.Contains("without dispersion", sys.Notes);
        Assert.DoesNotContain("not in the loaded catalogs", sys.Notes);
    }

    [Fact]
    public void AGlassTheCatalogsLackIsReported()
    {
        string json = "{\"aperture\": {\"type\": \"EPD\", \"value\": 10.0}, \"surface_group\": {\"surfaces\": [" +
            "{\"type\": \"ObjectSurface\", \"geometry\": {\"type\": \"Plane\", \"cs\": {\"z\": -Infinity}, \"radius\": Infinity}, \"material_post\": {\"type\": \"IdealMaterial\", \"index\": 1.0}}, " +
            Surface(0, Glass("NOSUCH", "schott")) + ", " + Surface(5, "{\"type\": \"IdealMaterial\", \"index\": 1.0}") + "]}}";
        string path = Path.Combine(_dir, "lens.json");
        File.WriteAllText(path, json);

        var sys = OptilandReader.Read(path, Catalogs());

        Assert.Contains("glass NOSUCH is not in the loaded catalogs", sys.Notes);
    }
}
