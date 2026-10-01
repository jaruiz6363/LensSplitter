using System;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The reader must hand back the surface list exactly as the file wrote it.
///
/// Some programs do two tidying passes on import - inserting a zero-thickness stop ahead of
/// a catalog element, and sums away air-only surfaces before the image - and both are right
/// for a program that composes stock parts into new designs. Both renumber the surfaces.
///
/// This program reports a prescription and attributes aberration to a surface, and both
/// answers are useless if its numbering disagrees with the program the user is looking at:
/// "surface 3 is your problem" has to mean surface 3 in their file. So neither pass runs
/// here, and these tests hold that line.
/// </summary>
public class ZmxReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "abcalc-zmx-" + Guid.NewGuid().ToString("N"));

    public ZmxReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Write(string name, string body)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, body);
        return path;
    }

    private static string Head(string title) =>
        $"TITL {title}\nENPD 10\nWAVM 1 0.5876 1.0\nPWAV 1\n"
        + "SURF 0\n  TYPE STANDARD\n  CURV 0\n  DISZ INFINITY\n";

    /// <summary>
    /// A stop sitting on a glass vertex stays there. Inserting a dummy in front of it would
    /// shift every following surface by one against the source file.
    /// </summary>
    [Fact]
    public void AStopOnAGlassSurfaceDoesNotGainADummyInFrontOfIt()
    {
        var z = new StringBuilder(Head("Stop On Glass"));
        z.AppendLine("SURF 1");
        z.AppendLine("  STOP");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0.02");
        z.AppendLine("  DISZ 6");
        z.AppendLine("  GLAS N-BK7");
        z.AppendLine("  DIAM 5.0 1 0 0 1 \"\"");
        z.AppendLine("SURF 2");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV -0.005");
        z.AppendLine("  DISZ 76");
        z.AppendLine("SURF 3");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ 0");

        var sys = LensFile.Read(Write("stoponglass.zmx", z.ToString()));

        // Object, the glass vertex carrying the stop, its back, image - and nothing else.
        Assert.Equal(4, sys.Surfaces.Count);
        Assert.Equal(1, sys.StopSurfaceIndex);
        Assert.True(sys.Surfaces[1].IsStop);
        Assert.Equal("N-BK7", sys.Surfaces[1].Material);
        Assert.Equal(0.02, sys.Surfaces[1].Curvature, 9);
        Assert.Equal(-0.005, sys.Surfaces[2].Curvature, 9);
    }

    /// <summary>
    /// An air-only surface between the last element and the image is kept, at its own index
    /// and with its own thickness, rather than being summed into the surface in front.
    /// </summary>
    [Fact]
    public void TrailingAirSurfacesAreKeptWhereTheFileWroteThem()
    {
        var z = new StringBuilder(Head("Spacer"));
        z.AppendLine("SURF 1");
        z.AppendLine("  STOP");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0.05");
        z.AppendLine("  DISZ 4");
        z.AppendLine("  GLAS N-BK7");
        z.AppendLine("SURF 2");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV -0.02");
        z.AppendLine("  DISZ 20");
        z.AppendLine("SURF 3");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ 15");
        z.AppendLine("SURF 4");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ 0");

        var sys = LensFile.Read(Write("spacer.zmx", z.ToString()));

        Assert.Equal(5, sys.Surfaces.Count);
        Assert.Equal(20.0, sys.Surfaces[2].Thickness, 9);    // NOT summed into 35
        Assert.Equal(15.0, sys.Surfaces[3].Thickness, 9);
    }

    /// <summary>
    /// A moulded aspheric layer is written as a model glass - an index with no material
    /// name. It must survive as its own surface with its own aspheric terms, and image
    /// space must end up as air rather than as the layer's glass.
    /// </summary>
    [Fact]
    public void AMouldedAsphericLayerSurvivesWithItsOwnSurfaces()
    {
        var z = new StringBuilder(Head("Hybrid Asphere"));
        z.AppendLine("SURF 1");
        z.AppendLine("  STOP");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0.05");
        z.AppendLine("  DISZ 4");
        z.AppendLine("  GLAS N-BK7");
        z.AppendLine("SURF 2");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV -0.02");
        z.AppendLine("  DISZ 0.5");
        z.AppendLine("  GLAS ___BLANK 1 0 1.517 52 0 0 0 0 0 0");
        z.AppendLine("SURF 3");
        z.AppendLine("  TYPE EVENASPH");
        z.AppendLine("  CURV -0.01");
        z.AppendLine("  PARM 2 0.0007");
        z.AppendLine("  DISZ 40");
        z.AppendLine("SURF 4");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ 0");

        var sys = LensFile.Read(Write("hybrid.zmx", z.ToString()));

        Assert.Equal(5, sys.Surfaces.Count);

        var layerFront = sys.Surfaces[2];
        Assert.True(layerFront.ModelIndexEnabled, "the moulded layer's model glass was lost");
        Assert.Equal(1.517, layerFront.ModelNd, 6);
        Assert.Equal(0.5, layerFront.Thickness, 9);

        var layerBack = sys.Surfaces[3];
        Assert.Equal(Core.Enums.SurfaceType.EvenAsphere, layerBack.Type);
        Assert.Equal(0.0007, layerBack.AsphericCoefficients[1], 9);

        // Image space is air, not the layer's glass.
        var unresolved = new System.Collections.Generic.List<string>();
        var n = IndexResolver.Build(sys, new GlassCatalog(), 0.5875618, unresolved);
        Assert.Equal(1.0, n[sys.LastOpticalSurface()], 9);
        Assert.Equal(1.517, n[2], 3);
    }

    /// <summary>
    /// The numbering contract stated directly, on a real reference fixture rather than a
    /// synthetic one - vendor and reference files are exactly where the tidying passes used
    /// to fire. Skips when the private reference repo is not in this clone.
    /// </summary>
    [Fact]
    public void SurfaceNumberingMatchesTheSourceFile()
    {
        string? fixture = FindOracleFixture("F3_conic_a4_a6_a8.zmx");
        if (fixture == null) return;

        var sys = LensFile.Read(fixture, CatalogLocator.LoadBundled());

        // The .zmx declares SURF 0..3, with the asphere and the stop both on SURF 1.
        Assert.Equal(4, sys.Surfaces.Count);
        Assert.Equal(1, sys.StopSurfaceIndex);
        Assert.Equal(Core.Enums.SurfaceType.EvenAsphere, sys.Surfaces[1].Type);
        Assert.Equal(-0.6, sys.Surfaces[1].Conic, 9);
    }

    /// <summary>
    /// A reference fixture, which lives in this repository. Null only if it is genuinely
    /// missing, which a clone should make impossible.
    /// </summary>
    internal static string? FindOracleFixture(string name)
    {
        var p = Path.Combine(AppContext.BaseDirectory, "fixtures", "coefficient-reference", name);
        return File.Exists(p) ? p : null;
    }
}
