using LensSplitter.Core.Models;
using Xunit;

namespace LensSplitter.Core.Tests;

public class LensElementTests
{
    [Fact]
    public void GetThinLensPower_BiconvexLens_ReturnsPositivePower()
    {
        // Arrange
        var element = CreateBiconvexElement(50.0, -50.0, 5.0, 1.5);

        // Act
        var power = element.GetThinLensPower(0.5876);

        // Assert
        Assert.True(power > 0, $"Biconvex lens should have positive power, got {power}");
    }

    [Fact]
    public void GetThinLensPower_BiconcaveLens_ReturnsNegativePower()
    {
        // Arrange
        var element = CreateBiconvexElement(-50.0, 50.0, 5.0, 1.5);

        // Act
        var power = element.GetThinLensPower(0.5876);

        // Assert
        Assert.True(power < 0, $"Biconcave lens should have negative power, got {power}");
    }

    [Fact]
    public void GetThickLensPower_ApproximatesThinLens_ForThinElement()
    {
        // Arrange
        var element = CreateBiconvexElement(50.0, -50.0, 0.1, 1.5); // Very thin

        // Act
        var thinPower = element.GetThinLensPower(0.5876);
        var thickPower = element.GetThickLensPower(0.5876);

        // Assert
        Assert.True(Math.Abs(thinPower - thickPower) < 0.001,
            $"Thin ({thinPower}) and thick ({thickPower}) powers should be similar for thin lens");
    }

    [Fact]
    public void GetShapeFactor_Equiconvex_ReturnsZero()
    {
        // Arrange - R1 = -R2
        var element = CreateBiconvexElement(50.0, -50.0, 5.0, 1.5);

        // Act
        var shapeFactor = element.GetShapeFactor();

        // Assert
        Assert.Equal(0.0, shapeFactor, 6);
    }

    [Fact]
    public void GetShapeFactor_PlanoConvex_ReturnsOne()
    {
        // Arrange - R1 finite, R2 infinite (flat second surface)
        var element = CreateBiconvexElement(50.0, double.PositiveInfinity, 5.0, 1.5);

        // Act
        var shapeFactor = element.GetShapeFactor();

        // Assert
        Assert.Equal(1.0, shapeFactor, 6);
    }

    [Fact]
    public void GetOptimalShapeFactor_TypicalGlass_ReturnsNegativeValue()
    {
        // Arrange
        var element = CreateBiconvexElement(50.0, -50.0, 5.0, 1.5);

        // Act
        var optimalShape = element.GetOptimalShapeFactor(0.5876);

        // Assert
        // For n=1.5: X_opt = -2(1.5²-1)/(1.5+2) = -2(1.25)/3.5 = -0.714
        Assert.True(optimalShape < 0, $"Optimal shape factor should be negative for typical glass, got {optimalShape}");
    }

    [Fact]
    public void GetFocalLength_PositiveLens_ReturnsPositiveValue()
    {
        // Arrange
        var element = CreateBiconvexElement(50.0, -50.0, 5.0, 1.5);

        // Act
        var fl = element.GetFocalLength(0.5876);

        // Assert
        Assert.True(fl > 0, $"Positive lens should have positive focal length, got {fl}");
    }

    private LensElement CreateBiconvexElement(double r1, double r2, double thickness, double n)
    {
        var glass = Glass.Ideal(n);

        var front = new Surface
        {
            Index = 0,
            Radius = r1,
            Thickness = thickness,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = 10
        };

        var rear = new Surface
        {
            Index = 1,
            Radius = r2,
            Thickness = 0,
            SemiDiameter = 10
        };

        return new LensElement
        {
            Index = 0,
            FrontSurface = front,
            RearSurface = rear,
            Glass = glass
        };
    }
}
