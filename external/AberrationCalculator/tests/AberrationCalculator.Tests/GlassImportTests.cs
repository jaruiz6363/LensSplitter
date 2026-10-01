using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Glass as LensHH-LT computes it, and as lens files from other programs carry it:
/// <list type="bullet">
/// <item>the LensHH-LT model glass;</item>
/// <item>all thirteen AGF dispersion formulas;</item>
/// <item>OpticStudio table glasses (.ZTG);</item>
/// <item>catalogs a .zmx names on its GCAT line that are not loaded.</item>
/// </list>
/// The model-glass values are LensHH-LT's. The formula values are the standard AGF definitions,
/// evaluated independently.
/// </summary>
[Collection("UserGlassCatalog")]   // the tests share UserGlassCatalog.FolderOverride
public class GlassImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acglass_" + Guid.NewGuid().ToString("N"));

    public GlassImportTests()
    {
        Directory.CreateDirectory(_dir);
        UserGlassCatalog.FolderOverride = Path.Combine(_dir, "userglass");
    }

    public void Dispose()
    {
        UserGlassCatalog.FolderOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Theory]
    [InlineData(1.45, 20, -0.03, 0.40, 1.4921241811459112)]
    [InlineData(1.45, 50, 0.03, 0.6562725, 1.4473789944311757)]
    [InlineData(1.65, 85, -0.03, 0.40, 1.6619892764766324)]
    [InlineData(1.45, 50, -0.03, 0.30, 1.4901074962036123)]
    [InlineData(1.45, 20, 0.00, 2.50, 1.4177584699200692)]
    public void TheModelGlassIsLensHHLTs(double nd, double vd, double dPgF, double lambda, double expected)
    {
        Assert.Equal(expected, IndexResolver.ModelIndex(nd, vd, dPgF, lambda), 12);
    }

    [Theory]
    [InlineData(4, new[] { 0.2, 1.0, 0.1, 0.01, 10.0 }, 0.60, 1.4928064399883685)]
    [InlineData(12, new[] { 2.27, -0.01, 0.012, 0.0002, 0.00001, 0.000001, 0.0001, 0.00001 }, 1.50, 1.5011641857870066)]
    [InlineData(13, new[] { 2.27, -0.01, 0.0001, 0.012, 0.0002, 0.00001, 0.000001, 0.0000001, 0.00000001 }, 0.40, 1.5351651591670554)]
    [InlineData(9, new[] { 1.3, 1.0, 0.01, 0.5, 100 }, 0.80, 1.5207407418612653)]
    public void TheFormulasThisGotWrongAreRight(int formula, double[] c, double lambda, double expected)
    {
        var g = new GlassData { Formula = (DispersionFormula)formula, Coefficients = c.Concat(new double[10 - c.Length]).ToArray() };
        Assert.Equal(expected, g.IndexAt(lambda), 13);
    }

    // A singlet whose first surface's glass is named <glass>, with an optional GCAT line.
    private string Lens(string glass, string? gcat = null)
    {
        var sb = new StringBuilder();
        sb.Append("VERS 221026 1 0\r\nMODE SEQ\r\nNAME test\r\nUNIT MM X W X CM MR CPMM\r\nENPD 10\r\n");
        if (gcat != null) sb.Append("GCAT " + gcat + "\r\n");
        sb.Append("FTYP 0 0 1 1 0 0 0 1\r\nWAVM 1 0.5875618 1\r\nPWAV 1\r\n");
        sb.Append("SURF 0\r\n  TYPE STANDARD\r\n  CURV 0.0\r\n  DISZ INFINITY\r\n");
        sb.Append("SURF 1\r\n  STOP\r\n  TYPE STANDARD\r\n  CURV 0.02\r\n  DISZ 5\r\n  GLAS " + glass + " 0 0 1.5 40 0 0 0 0 0 0\r\n");
        sb.Append("SURF 2\r\n  TYPE STANDARD\r\n  CURV -0.02\r\n  DISZ 45\r\n");
        sb.Append("SURF 3\r\n  TYPE STANDARD\r\n  CURV 0.0\r\n  DISZ 0\r\n");
        string path = Path.Combine(_dir, "lens.zmx");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    [Fact]
    public void AThreePointTableOfAModelGlassComesBackAsThatModelGlass()
    {
        const double nd = 1.680409965080894, vd = 60.32364915667316, dPgF = -0.001046599903552936;
        var ztg = new StringBuilder("! Code V Private Glass Catalog Data\r\n");
        foreach (var l in new[] { 0.65, 0.55, 0.48 })
            ztg.Append(string.Format(CultureInfo.InvariantCulture, "{0:F4} {1:R}\r\n", l, IndexResolver.ModelIndex(nd, vd, dPgF, l)));
        File.WriteAllText(Path.Combine(_dir, "LHHMODEL1.ZTG"), ztg.ToString(), Encoding.Unicode);

        var sys = ZmxReader.Read(Lens("LHHMODEL1.ZTG"), new GlassCatalog());

        var s = sys.Surfaces[1];
        Assert.True(s.ModelIndexEnabled);
        Assert.Equal(nd, (double)s.ModelNd, 9);
        Assert.Equal(vd, (double)s.ModelVd, 6);
        Assert.Equal(dPgF, (double)s.ModelDPgF, 9);
        Assert.Contains("passes through every point", sys.Notes);
    }

    [Fact]
    public void ALongTableBecomesATableCatalogGlass()
    {
        var bk7 = new GlassData { Formula = DispersionFormula.Sellmeier1,
            Coefficients = new[] { 1.03961212, 0.00600069867, 0.231792344, 0.0200179144, 1.01046945, 103.560653 } };
        var ztg = new StringBuilder("! BK7 as a table\r\nDENSITY 2.51\r\n");
        for (int k = 0; k <= 100; k++)
        {
            double l = 0.40 + 0.004 * k;
            ztg.Append(string.Format(CultureInfo.InvariantCulture, "{0:F4} {1:R}\r\n", l, bk7.IndexAt(l)));
        }
        File.WriteAllText(Path.Combine(_dir, "TABLETEST.ZTG"), ztg.ToString());
        var glass = new GlassCatalog();

        var sys = ZmxReader.Read(Lens("TABLETEST.ZTG"), glass);

        Assert.Equal("TABLETEST", sys.Surfaces[1].Material);
        var g = glass.Find("TABLETEST", sys.GlassCatalogs);
        Assert.NotNull(g);
        foreach (var l in new[] { 0.402, 0.55, 0.798 })
            Assert.Equal(bk7.IndexAt(l), g!.IndexAt(l), 5);
        Assert.True(File.Exists(UserGlassCatalog.TableCatalogPath));
    }

    [Fact]
    public void AGlassFromTheLenssOwnCatalogIsBroughtIn()
    {
        File.WriteAllText(Path.Combine(_dir, "CODEV_CONVERTED.AGF"),
            "CC Code V Private Glass Catalog Data\r\n" +
            "NM PRVGLASS 2 0 1.5168 64.17 0 0 0\r\n" +
            "CD 1.03961212 0.00600069867 0.231792344 0.0200179144 1.01046945 103.560653 0 0 0 0\r\n" +
            "LD 0.3 2.5\r\n");
        var glass = new GlassCatalog();

        var sys = ZmxReader.Read(Lens("PRVGLASS", "CODEV_CONVERTED"), glass);

        Assert.Contains("CODEV_CONVERTED", glass.LoadedCatalogs);
        Assert.Equal(1.5168, glass.Find("PRVGLASS", sys.GlassCatalogs)!.IndexAt(0.5875618), 4);
        Assert.Contains("Glass catalog CODEV_CONVERTED", sys.Notes);
        Assert.True(File.Exists(Path.Combine(UserGlassCatalog.Folder, "CODEV_CONVERTED.AGF")));
    }

    [Fact]
    public void AListingThatContradictsItsDataTakesTheData()
    {
        var glass = CatalogLocator.LoadBundled();
        Assert.Equal(1.620750, glass.Find("CDGM:H-TK9")!.Nd, 5);          // listed 1.587166
        Assert.Equal(60.30, glass.Find("CDGM:H-TK9")!.Vd, 1);             // listed 75.90
        Assert.Equal(1.770473, glass.Find("HOYA:MC-TAF115")!.Nd, 5);      // listed 1.777047
        Assert.Equal(36.3, glass.Find("SUMITA:F2")!.Vd, 12);              // rounding, left alone
        Assert.DoesNotContain(glass.ListedValueCorrections, n => n.StartsWith("SCHOTT:") || n.StartsWith("OHARA:"));
    }

    [Fact]
    public void AMissingTableIsReported()
    {
        var sys = ZmxReader.Read(Lens("NOSUCH.ZTG"), new GlassCatalog());
        Assert.Contains("NOSUCH.ZTG not found", sys.Notes);
    }
}
