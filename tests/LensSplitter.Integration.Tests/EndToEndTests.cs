using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;
using LensSplitter.Core.Splitting;
using LensSplitter.Parsing;
using LensSplitter.Parsing.Export;
using LensSplitter.Parsing.Glass;
using LensSplitter.Visualization;
using Xunit;

namespace LensSplitter.Integration.Tests;

public class EndToEndTests
{
    [Fact]
    public void EndToEnd_LoadSplitExport_WorksCorrectly()
    {
        // Arrange
        var system = CreateTestSystem();
        var splitter = new ElementSplitter();
        var zmxExporter = new ZmxExporter();
        var jsonExporter = new OptilandJsonExporter();
        var svgRenderer = new SvgRenderer();

        // Act - Split
        var result = splitter.Split(system, 0, 0.1);

        // Assert - Split successful
        Assert.True(result.IsSuccessful);

        // Act - Export to ZMX
        var zmx = zmxExporter.GenerateZmx(result.SplitSystem);

        // Assert - ZMX content valid
        Assert.Contains("SURF", zmx);
        Assert.Contains("CURV", zmx);

        // Act - Export to JSON
        var json = jsonExporter.GenerateJson(result.SplitSystem);

        // Assert - JSON content valid
        Assert.Contains("surface_group", json);
        Assert.Contains("surfaces", json);

        // Act - Render SVG
        var svg = svgRenderer.RenderComparison(result);

        // Assert - SVG content valid
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void EndToEnd_RoundTripJson_PreservesSystem()
    {
        // Arrange
        var original = CreateTestSystem();
        var jsonExporter = new OptilandJsonExporter();
        var loader = new OpticalSystemLoader();

        // Act - Export and reload
        var json = jsonExporter.GenerateJson(original);
        var reloaded = loader.LoadFromJson(json, "Reloaded");

        // Assert
        Assert.Equal(original.Surfaces.Count, reloaded.Surfaces.Count);
        Assert.Equal(original.Wavelengths.Count, reloaded.Wavelengths.Count);
    }

    [Fact]
    public void EndToEnd_ParaxialTracing_ConsistentResults()
    {
        // Arrange
        var system = CreateTestSystem();
        var tracer = new ParaxialRayTracer();
        var wavelength = system.PrimaryWavelength.ValueMicrons;

        // Act
        var efl1 = tracer.CalculateEfl(system, wavelength);
        var efl2 = tracer.CalculateEfl(system, wavelength);

        // Assert - Same result on multiple calls
        Assert.Equal(efl1, efl2, 10);
    }

    [Fact]
    public void EndToEnd_GlassCatalog_LoadsAndResolves()
    {
        // Arrange
        var catalog = new GlassCatalogManager();
        var agfContent = @"CC Test Catalog
NM TestGlass 2 0 1.5168 64.17 0 0 0
CD 1.03961212 0.00600069867 0.231792344 0.0200179144 1.01046945 103.560653
";
        var tempPath = Path.GetTempFileName() + ".agf";

        try
        {
            File.WriteAllText(tempPath, agfContent);

            // Act
            catalog.LoadCatalog(tempPath);
            var glass = catalog.GetGlass("TestGlass");

            // Assert
            Assert.NotNull(glass);
            var n = glass!.GetRefractiveIndex(0.5876);
            Assert.True(n > 1.4 && n < 1.7);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void EndToEnd_MultipleElements_SplitsCorrectElement()
    {
        // Arrange
        var system = CreateDoubletSystem();
        var splitter = new ElementSplitter();
        var originalElementCount = system.GetLensElements().Count;

        // Act - Split first element (positive crown lens)
        // Note: Element 1 (flint) is a negative lens and cannot be split.
        // Power-preserving splitting is designed for positive lenses only.
        var result = splitter.Split(system, 0, 0.1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.SplitElementIndex);
        Assert.True(result.SplitSystem.GetLensElements().Count > originalElementCount);
    }

    [Fact]
    public void EndToEnd_VisualizationContainsElements()
    {
        // Arrange
        var system = CreateTestSystem();
        var tracer = new ParaxialRayTracer();
        var renderer = new SvgRenderer();
        var wavelength = system.PrimaryWavelength.ValueMicrons;

        var marginalRay = tracer.TraceMarginalRay(system, wavelength);
        var chiefRay = tracer.TraceChiefRay(system, 0, wavelength);

        // Act
        var svg = renderer.RenderSystem(system, marginalRay, chiefRay);

        // Assert
        Assert.Contains("lens-fill", svg); // Lens elements
        Assert.Contains("marginal-ray", svg); // Marginal ray
        Assert.Contains("chief-ray", svg); // Chief ray
    }

    private OpticalSystem CreateTestSystem()
    {
        var glass = Glass.Ideal(1.5);

        return new OpticalSystem
        {
            Name = "Test Singlet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = glass, GlassName = "IDEAL", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -100.0, Thickness = 95.0, SemiDiameter = 10 },
                new Surface { Index = 3, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateDoubletSystem()
    {
        // Create an air-spaced doublet (two separate positive lenses with air gap)
        // Note: This is NOT a cemented doublet. Cemented doublets share a surface
        // between elements and cannot be split without breaking the system.
        var crown = Glass.Ideal(1.5);
        var flint = Glass.Ideal(1.5);

        return new OpticalSystem
        {
            Name = "Test Air-Spaced Doublet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // First element (positive)
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = crown, GlassName = "CROWN", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -100.0, Thickness = 2.0, SemiDiameter = 10 }, // Air gap
                // Second element (positive)
                new Surface { Index = 3, Radius = 60.0, Thickness = 4.0, Glass = flint, GlassName = "FLINT", SemiDiameter = 10 },
                new Surface { Index = 4, Radius = -80.0, Thickness = 85.0, SemiDiameter = 10 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }
}
