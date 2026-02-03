using LensSplitter.Core.Models;
using Xunit;

namespace LensSplitter.Core.Tests;

public class GlassTests
{
    [Fact]
    public void Air_HasRefractiveIndexOne()
    {
        // Arrange
        var air = Glass.Air;

        // Act
        var n = air.GetRefractiveIndex(0.5876);

        // Assert
        Assert.Equal(1.0, n, 10);
    }

    [Fact]
    public void IdealGlass_HasConstantRefractiveIndex()
    {
        // Arrange
        var glass = Glass.Ideal(1.5);

        // Act
        var n1 = glass.GetRefractiveIndex(0.4);
        var n2 = glass.GetRefractiveIndex(0.5);
        var n3 = glass.GetRefractiveIndex(0.6);

        // Assert
        Assert.Equal(1.5, n1, 10);
        Assert.Equal(1.5, n2, 10);
        Assert.Equal(1.5, n3, 10);
    }

    [Fact]
    public void SchottDispersion_CalculatesCorrectly()
    {
        // Arrange - Coefficients for a typical crown glass
        var glass = new Glass
        {
            Name = "TestSchott",
            DispersionModel = DispersionModel.Schott,
            Coefficients = new double[] { 2.2718929, -0.010108077, 0.010592509, 0.0002356272, -1.7849109e-05, 8.0689979e-07 }
        };

        // Act
        var n = glass.GetRefractiveIndex(0.5876);

        // Assert
        Assert.True(n > 1.4 && n < 1.7, $"Refractive index {n} out of expected range");
    }

    [Fact]
    public void SellmeierDispersion_CalculatesCorrectly()
    {
        // Arrange - Coefficients for a typical glass (BK7-like)
        var glass = new Glass
        {
            Name = "TestSellmeier",
            DispersionModel = DispersionModel.Sellmeier1,
            Coefficients = new double[] { 1.03961212, 0.00600069867, 0.231792344, 0.0200179144, 1.01046945, 103.560653 }
        };

        // Act
        var n = glass.GetRefractiveIndex(0.5876);

        // Assert
        Assert.True(n > 1.4 && n < 1.7, $"Refractive index {n} out of expected range");
    }

    [Fact]
    public void Dispersion_ShowsWavelengthDependence()
    {
        // Arrange - Sellmeier glass with known dispersion
        var glass = new Glass
        {
            Name = "TestSellmeier",
            DispersionModel = DispersionModel.Sellmeier1,
            Coefficients = new double[] { 1.03961212, 0.00600069867, 0.231792344, 0.0200179144, 1.01046945, 103.560653 }
        };

        // Act
        var nBlue = glass.GetRefractiveIndex(0.4861); // F-line
        var nRed = glass.GetRefractiveIndex(0.6563);  // C-line

        // Assert - Blue should be higher than red (normal dispersion)
        Assert.True(nBlue > nRed, $"Expected nBlue ({nBlue}) > nRed ({nRed})");
    }
}
