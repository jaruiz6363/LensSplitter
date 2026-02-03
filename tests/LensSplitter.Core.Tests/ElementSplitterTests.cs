using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Core.Tests;

public class ElementSplitterTests
{
    private readonly ElementSplitter _splitter = new();

    [Fact]
    public void Split_SingleElement_PreservesPower()
    {
        // Arrange
        var system = CreateSingleElementSystem();

        // Act
        var result = _splitter.Split(system, 0, 0.1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RelativePowerError < 5.0, $"Power error {result.RelativePowerError}% exceeds 5%");
    }

    [Fact]
    public void Split_SingleElement_CreatesTwoElements()
    {
        // Arrange
        var system = CreateSingleElementSystem();
        var originalElementCount = system.GetLensElements().Count;

        // Act
        var result = _splitter.Split(system, 0, 0.1);

        // Assert
        var newElementCount = result.SplitSystem.GetLensElements().Count;
        Assert.True(newElementCount > originalElementCount);
    }

    [Fact]
    public void Split_SingleElement_SplitSystemHasMoreSurfaces()
    {
        // Arrange
        var system = CreateSingleElementSystem();
        var originalSurfaceCount = system.Surfaces.Count;

        // Act
        var result = _splitter.Split(system, 0, 0.1);

        // Assert
        Assert.True(result.SplitSystem.Surfaces.Count > originalSurfaceCount);
    }

    [Fact]
    public void Split_InvalidElementIndex_ThrowsException()
    {
        // Arrange
        var system = CreateSingleElementSystem();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _splitter.Split(system, 5, 0.1));
    }

    [Fact]
    public void Split_EqualPowerDistribution_HalvesPower()
    {
        // Arrange
        var system = CreateSingleElementSystem();

        // Act
        var result = _splitter.Split(system, 0, 0.1);

        // Assert
        // Each element should have approximately half the original power
        var halfPower = result.OriginalPower / 2.0;

        // Allow 50% tolerance due to thick lens effects
        Assert.True(Math.Abs(result.FirstElementPower - halfPower) / Math.Abs(halfPower) < 0.5);
        Assert.True(Math.Abs(result.SecondElementPower - halfPower) / Math.Abs(halfPower) < 0.5);
    }

    [Fact]
    public void EstimateSaReductionFactor_EqualSplit_ApproximatelyFour()
    {
        // Arrange
        double originalPower = 0.02;
        double power1 = 0.01;
        double power2 = 0.01;

        // Act
        double reduction = ElementSplitter.EstimateSaReductionFactor(originalPower, power1, power2);

        // Assert
        // For equal power split, SA reduction should be factor of 4
        Assert.True(reduction > 3.5 && reduction < 4.5, $"SA reduction factor {reduction} not close to 4");
    }

    [Fact]
    public void Split_ResultContainsValidRays()
    {
        // Arrange
        var system = CreateSingleElementSystem();

        // Act
        var result = _splitter.Split(system, 0, 0.1);

        // Assert
        Assert.NotNull(result.OriginalMarginalRay);
        Assert.NotNull(result.SplitMarginalRay);
        Assert.True(result.OriginalMarginalRay.States.Count > 0);
        Assert.True(result.SplitMarginalRay.States.Count > 0);
    }

    private OpticalSystem CreateSingleElementSystem()
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
}
