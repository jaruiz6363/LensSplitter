using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Core.Tests;

public class SeidelCalculatorTests
{
    private readonly SeidelCalculator _calculator = new();

    private static OpticalSystem CreateSingleLensSystem(double power, double n = 1.5168)
    {
        // Create a simple singlet lens system
        // For a thin lens: P = (n-1)(1/R1 - 1/R2)
        // For an equiconvex lens: R1 = -R2 = 2*(n-1)/P
        double R = 2.0 * (n - 1.0) / power;

        var system = new OpticalSystem
        {
            Name = "Test Singlet",
            ApertureType = ApertureType.EntrancePupilDiameter,
            ApertureValue = 10.0 // 10mm EPD
        };

        // Object surface
        system.Surfaces.Add(new Surface
        {
            Index = 0,
            SurfaceType = SurfaceType.Object,
            Radius = double.PositiveInfinity,
            Thickness = double.PositiveInfinity
        });

        // Front surface
        system.Surfaces.Add(new Surface
        {
            Index = 1,
            Radius = R,
            Thickness = 5.0, // 5mm center thickness
            GlassName = "TEST",
            Glass = new Glass { Name = "TEST", Nd = n, Vd = 64.0, DispersionModel = DispersionModel.Constant, Coefficients = new[] { n } },
            SemiDiameter = 10.0
        });

        // Rear surface
        system.Surfaces.Add(new Surface
        {
            Index = 2,
            Radius = -R,
            Thickness = 100.0, // Distance to image
            GlassName = null,
            Glass = null,
            SemiDiameter = 10.0
        });

        // Image surface
        system.Surfaces.Add(new Surface
        {
            Index = 3,
            SurfaceType = SurfaceType.Image,
            Radius = double.PositiveInfinity,
            Thickness = 0
        });

        system.EnsureWavelengths();
        system.EnsureFields();

        return system;
    }

    [Fact]
    public void Calculate_SingleLens_ReturnsNonZeroS1()
    {
        // Arrange - A singlet lens always has spherical aberration
        var system = CreateSingleLensSystem(0.01); // f = 100mm

        // Act
        var result = _calculator.Calculate(system, 0.5876, 1.0);

        // Assert
        Assert.NotEqual(0, result.S1);
    }

    [Fact]
    public void Calculate_SingleLens_ReturnsAllCoefficients()
    {
        // Arrange
        var system = CreateSingleLensSystem(0.01);

        // Act
        var result = _calculator.Calculate(system, 0.5876, 5.0);

        // Assert - All coefficients should be computed
        Assert.NotNull(result);
        Assert.True(result.SurfaceCoefficients.Count > 0, "Should have surface coefficients");
        // S1 should dominate for single lens
        Assert.True(Math.Abs(result.S1) > 1e-12);
    }

    [Fact]
    public void CalculateMeritFunction_WithDefaultWeights_SumsAberrations()
    {
        // Arrange
        var result = new SeidelResult
        {
            S1 = 1.0,
            S2 = 2.0,
            S3 = 3.0,
            S4 = 4.0,
            S5 = 5.0,
            CL = 0.1,
            CT = 0.2
        };
        var weights = AberrationWeights.Default;

        // Act
        var mf = SeidelCalculator.CalculateMeritFunction(result, weights);

        // Assert - Default weights: W1=1.0, W2=1.0, W3=1.0, W4=1.0, W5=0.0, WCL=1.0, WCT=1.0
        // MF = 1*1 + 1*2 + 1*3 + 1*4 + 0*5 + 1*0.1 + 1*0.2 = 1 + 2 + 3 + 4 + 0 + 0.1 + 0.2 = 10.3
        Assert.Equal(10.3, mf, 0.001);
    }

    [Fact]
    public void CalculateMeritFunction_WithNoDistortion_ExcludesS5()
    {
        // Arrange
        var result = new SeidelResult
        {
            S1 = 1.0,
            S2 = 2.0,
            S3 = 3.0,
            S4 = 4.0,
            S5 = 5.0,
            CL = 0.1,
            CT = 0.2
        };
        var weights = AberrationWeights.NoDistortion;

        // Act
        var mf = SeidelCalculator.CalculateMeritFunction(result, weights);

        // Assert - NoDistortion: W1=1.0, W2=1.0, W3=1.0, W4=1.0, W5=0, WCL=1.0, WCT=1.0
        // MF = 1*1 + 1*2 + 1*3 + 1*4 + 0*5 + 1*0.1 + 1*0.2 = 1 + 2 + 3 + 4 + 0 + 0.1 + 0.2 = 10.3
        Assert.Equal(10.3, mf, 0.001);
    }

    [Fact]
    public void CalculateMeritFunction_SphericalOnly_OnlyS1()
    {
        // Arrange
        var result = new SeidelResult
        {
            S1 = 1.0,
            S2 = 2.0,
            S3 = 3.0,
            S4 = 4.0,
            S5 = 5.0,
            CL = 0.1,
            CT = 0.2
        };
        var weights = AberrationWeights.SphericalOnly;

        // Act
        var mf = SeidelCalculator.CalculateMeritFunction(result, weights);

        // Assert
        Assert.Equal(1.0, mf, 0.001);
    }

    [Fact]
    public void AberrationWeights_Default_HasOptimizedWeights()
    {
        var weights = AberrationWeights.Default;

        Assert.Equal(1.0, weights.W1);  // Spherical
        Assert.Equal(1.0, weights.W2);  // Coma
        Assert.Equal(1.0, weights.W3);  // Astigmatism
        Assert.Equal(1.0, weights.W4);  // Petzval
        Assert.Equal(0.0, weights.W5);  // Distortion - zero
    }

    [Fact]
    public void AberrationWeights_Equal_HasEqualWeights()
    {
        var weights = AberrationWeights.Equal;

        Assert.Equal(1.0, weights.W1);
        Assert.Equal(1.0, weights.W2);
        Assert.Equal(1.0, weights.W3);
        Assert.Equal(1.0, weights.W4);  // Equal preset has all weights = 1.0
        Assert.Equal(1.0, weights.W5);
    }

    [Fact]
    public void AberrationWeights_NoDistortion_HasZeroW5()
    {
        var weights = AberrationWeights.NoDistortion;

        Assert.Equal(1.0, weights.W1);
        Assert.Equal(1.0, weights.W2);  // Coma
        Assert.Equal(1.0, weights.W3);
        Assert.Equal(1.0, weights.W4);
        Assert.Equal(0.0, weights.W5);  // Zero distortion
    }

    /// <summary>
    /// Creates a Cooke triplet finite conjugate system matching ZEMAX reference file:
    /// "Cooke 40 degree field for split_FC.zmx"
    /// Object at 1000mm, field 20°, stop at surface 4, EPD 10mm.
    /// </summary>
    private static OpticalSystem CreateCookeTripletFiniteConjugate()
    {
        // SK16 glass: Nd=1.62041, Vd=60.3
        // F2 glass: Nd=1.62004, Vd=36.4
        var glassSK16 = new Glass { Name = "SK16", Nd = 1.62041, Vd = 60.3, DispersionModel = DispersionModel.Constant, Coefficients = new[] { 1.62041 } };
        var glassF2 = new Glass { Name = "F2", Nd = 1.62004, Vd = 36.4, DispersionModel = DispersionModel.Constant, Coefficients = new[] { 1.62004 } };

        var system = new OpticalSystem
        {
            Name = "Cooke 40 degree field FC",
            ApertureType = ApertureType.EntrancePupilDiameter,
            ApertureValue = 10.0 // 10mm EPD (from ENPD in ZMX)
        };

        // Surface 0: Object at 1000mm
        system.Surfaces.Add(new Surface
        {
            Index = 0,
            SurfaceType = SurfaceType.Object,
            Radius = double.PositiveInfinity,
            Thickness = 1000.0,
            SemiDiameter = 368.16
        });

        // Surface 1: L1 Front, R = 1/0.04542648 = 22.01mm
        system.Surfaces.Add(new Surface
        {
            Index = 1,
            Radius = 1.0 / 0.04542648,  // 22.01mm
            Thickness = 3.2589558,
            GlassName = "SK16",
            Glass = glassSK16,
            SemiDiameter = 9.5
        });

        // Surface 2: L1 Back, R = 1/-0.002294839 = -435.8mm
        system.Surfaces.Add(new Surface
        {
            Index = 2,
            Radius = 1.0 / -0.002294839,  // -435.8mm
            Thickness = 6.0075511,
            GlassName = null,
            Glass = null,
            SemiDiameter = 9.5
        });

        // Surface 3: L2 Front (negative element), R = 1/-0.04501812 = -22.21mm
        system.Surfaces.Add(new Surface
        {
            Index = 3,
            Radius = 1.0 / -0.04501812,  // -22.21mm
            Thickness = 0.99997457,
            GlassName = "F2",
            Glass = glassF2,
            SemiDiameter = 5.0
        });

        // Surface 4: STOP, R = 1/0.04928069 = 20.29mm
        system.Surfaces.Add(new Surface
        {
            Index = 4,
            Radius = 1.0 / 0.04928069,  // 20.29mm
            Thickness = 4.7504089,
            GlassName = null,
            Glass = null,
            SemiDiameter = 5.0,
            IsStop = true
        });

        // Surface 5: L3 Front, R = 1/0.01254963 = 79.68mm
        system.Surfaces.Add(new Surface
        {
            Index = 5,
            Radius = 1.0 / 0.01254963,  // 79.68mm
            Thickness = 2.9520756,
            GlassName = "SK16",
            Glass = glassSK16,
            SemiDiameter = 7.5
        });

        // Surface 6: L3 Back, R = 1/-0.05436162 = -18.40mm
        system.Surfaces.Add(new Surface
        {
            Index = 6,
            Radius = 1.0 / -0.05436162,  // -18.40mm
            Thickness = 45.012067,
            GlassName = null,
            Glass = null,
            SemiDiameter = 7.5
        });

        // Surface 7: Image
        system.Surfaces.Add(new Surface
        {
            Index = 7,
            SurfaceType = SurfaceType.Image,
            Radius = double.PositiveInfinity,
            Thickness = 0,
            SemiDiameter = 19.13
        });

        // Add 20 degree field (from YFLN 0 14 20)
        system.Fields.Add(new Field(0, 20.0, FieldType.Angle));

        system.EnsureWavelengths();

        return system;
    }

    [Fact]
    public void Calculate_CookeTripletFiniteConjugate_ReturnsReasonableSeidels()
    {
        // Arrange - Cooke triplet with finite conjugate
        var system = CreateCookeTripletFiniteConjugate();

        // Act
        var result = _calculator.Calculate(system, 0.55, 20.0);

        // Get diagnostics for debugging
        var diagnostics = _calculator.GetDiagnostics(system, 0.55);

        // Assert - Seidel values should be in reasonable range (order of 0.001-0.1)
        // ZEMAX reference approximately: S1≈0.007, S2≈-0.002, S3≈-0.009, S4≈0.026, S5≈0.003
        Assert.True(Math.Abs(result.S1) < 1.0, $"S1={result.S1} should be small. Diag:\n{diagnostics}");
        Assert.True(Math.Abs(result.S2) < 1.0, $"S2={result.S2} should be small. Diag:\n{diagnostics}");
        Assert.True(Math.Abs(result.S3) < 1.0, $"S3={result.S3} should be small. Diag:\n{diagnostics}");
        Assert.True(Math.Abs(result.S4) < 1.0, $"S4={result.S4} should be small. Diag:\n{diagnostics}");
        Assert.True(Math.Abs(result.S5) < 1.0, $"S5={result.S5} should be small. Diag:\n{diagnostics}");

        // Output the values for inspection (these will show in test output)
        System.Diagnostics.Debug.WriteLine($"S1={result.S1:F6}, S2={result.S2:F6}, S3={result.S3:F6}, S4={result.S4:F6}, S5={result.S5:F6}");
        System.Diagnostics.Debug.WriteLine(diagnostics);
    }

    [Fact]
    public void Calculate_CookeTripletFiniteConjugate_MatchesExpectedOrder()
    {
        // This test verifies finite conjugate Seidel calculation produces
        // values in the expected order of magnitude (not garbage values).
        // The exact values depend on precise system parameters matching ZEMAX.
        var system = CreateCookeTripletFiniteConjugate();
        var result = _calculator.Calculate(system, 0.55, 20.0);

        // Values should all be in reasonable range (0.001 to 0.5)
        // Previously the bug produced S3=189, S5=27400 which was obviously wrong.
        // After fix, values are in the 0.01-0.2 range.
        Assert.True(Math.Abs(result.S1) < 0.5 && Math.Abs(result.S1) > 0.001,
            $"S1={result.S1} should be in reasonable range");
        Assert.True(Math.Abs(result.S3) < 0.5 && Math.Abs(result.S3) > 0.001,
            $"S3={result.S3} should be in reasonable range");
        Assert.True(Math.Abs(result.S4) < 0.5 && Math.Abs(result.S4) > 0.001,
            $"S4={result.S4} should be in reasonable range");
        Assert.True(Math.Abs(result.S5) < 0.5,
            $"S5={result.S5} should be in reasonable range (was 27400 before fix)");
    }

    [Fact]
    public void Calculate_CookeTripletFiniteConjugate_MatchesZemaxReference()
    {
        // This test verifies Seidel values match ZEMAX reference within 10%
        // Reference file: "Cooke 40 degree field for split_FC.zmx"
        // ZEMAX reference: S1=0.007, S2=-0.002, S3=-0.009, S4=0.026, S5=0.003
        var system = CreateCookeTripletFiniteConjugate();
        var result = _calculator.Calculate(system, 0.55, 20.0);

        // Verify values match ZEMAX within 10% tolerance
        // (Small differences due to constant glass model vs Sellmeier dispersion)
        Assert.True(Math.Abs(result.S1 - 0.007) < 0.001, $"S1={result.S1} should be ~0.007");
        Assert.True(Math.Abs(result.S2 - (-0.002)) < 0.001, $"S2={result.S2} should be ~-0.002");
        Assert.True(Math.Abs(result.S3 - (-0.009)) < 0.001, $"S3={result.S3} should be ~-0.009");
        Assert.True(Math.Abs(result.S4 - 0.026) < 0.003, $"S4={result.S4} should be ~0.026");
        Assert.True(Math.Abs(result.S5 - 0.003) < 0.001, $"S5={result.S5} should be ~0.003");
    }

    /// <summary>
    /// Creates a simple finite conjugate singlet for optimization testing.
    /// Object at 500mm, field 10°.
    /// </summary>
    private static OpticalSystem CreateFiniteConjugateSinglet()
    {
        var glass = new Glass { Name = "BK7", Nd = 1.5168, Vd = 64.0, DispersionModel = DispersionModel.Constant, Coefficients = new[] { 1.5168 } };

        var system = new OpticalSystem
        {
            Name = "Finite Conjugate Singlet",
            ApertureType = ApertureType.EntrancePupilDiameter,
            ApertureValue = 10.0
        };

        // Object at 500mm (finite conjugate)
        system.Surfaces.Add(new Surface
        {
            Index = 0,
            SurfaceType = SurfaceType.Object,
            Radius = double.PositiveInfinity,
            Thickness = 500.0,  // Finite conjugate
            SemiDiameter = 100.0
        });

        // Simple positive singlet
        system.Surfaces.Add(new Surface
        {
            Index = 1,
            Radius = 50.0,
            Thickness = 5.0,
            GlassName = "BK7",
            Glass = glass,
            SemiDiameter = 10.0,
            IsStop = true
        });

        system.Surfaces.Add(new Surface
        {
            Index = 2,
            Radius = -100.0,
            Thickness = 100.0,
            SemiDiameter = 10.0
        });

        system.Surfaces.Add(new Surface
        {
            Index = 3,
            SurfaceType = SurfaceType.Image,
            Radius = double.PositiveInfinity,
            Thickness = 0
        });

        system.Fields.Add(new Field(0, 10.0, FieldType.Angle));
        system.EnsureWavelengths();

        return system;
    }

    [Fact]
    public void Calculate_FiniteConjugateSinglet_ReturnsReasonableSeidels()
    {
        // Test that finite conjugate singlet produces reasonable Seidel values
        // This catches the bug where finite conjugate chief ray was wrong
        var system = CreateFiniteConjugateSinglet();
        var result = _calculator.Calculate(system, 0.55, 10.0);

        // All Seidel values should be in reasonable range (not garbage like 100+ or 1000+)
        Assert.True(Math.Abs(result.S1) < 1.0, $"S1={result.S1} should be reasonable (was garbage before fix)");
        Assert.True(Math.Abs(result.S2) < 1.0, $"S2={result.S2} should be reasonable");
        Assert.True(Math.Abs(result.S3) < 1.0, $"S3={result.S3} should be reasonable (was ~189 before fix)");
        Assert.True(Math.Abs(result.S4) < 1.0, $"S4={result.S4} should be reasonable");
        Assert.True(Math.Abs(result.S5) < 1.0, $"S5={result.S5} should be reasonable (was ~27400 before fix)");

        // S1 should be non-zero (singlet always has spherical aberration)
        Assert.True(Math.Abs(result.S1) > 1e-6, $"S1={result.S1} should be non-zero for a singlet");
    }
}
