using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Core.Tests;

public class AberrationCalculatorTests
{
    [Fact]
    public void CalculateOptimalShapeFactor_ObjectAtInfinity_ReturnsExpectedValue()
    {
        // For n=1.5, Y=-1 (object at infinity):
        // X_opt = -(n²-1)·Y / (n+2) = -(2.25-1)·(-1) / 3.5 = 1.25/3.5 ≈ 0.357
        double n = 1.5;
        double Y = -1.0;

        double X_opt = AberrationCalculator.CalculateOptimalShapeFactor(n, Y);

        Assert.True(X_opt > 0.3 && X_opt < 0.4, $"X_opt={X_opt} not in expected range");
    }

    [Fact]
    public void CalculateOptimalShapeFactor_SymmetricConjugates_ReturnsZero()
    {
        // For Y=0 (symmetric 1:1 conjugates), X_opt = 0 (equiconvex)
        double n = 1.5;
        double Y = 0.0;

        double X_opt = AberrationCalculator.CalculateOptimalShapeFactor(n, Y);

        Assert.Equal(0.0, X_opt, 6);
    }

    [Fact]
    public void CalculateS1ThinLens_PositiveLens_ReturnsNegativeS1()
    {
        // Positive lens has undercorrected (negative) spherical aberration
        double y = 5.0;
        double phi = 0.01; // 100mm focal length
        double n = 1.5;
        double X = 0.357; // Near optimal for infinity
        double Y = -1.0;

        double S1 = AberrationCalculator.CalculateS1ThinLens(y, phi, n, X, Y);

        Assert.True(S1 < 0, $"S1={S1} should be negative for positive lens");
    }

    [Fact]
    public void CalculateS1ThinLens_ZeroPower_ReturnsZero()
    {
        double S1 = AberrationCalculator.CalculateS1ThinLens(5.0, 0.0, 1.5, 0.5, -1.0);

        Assert.Equal(0.0, S1, 15);
    }

    [Fact]
    public void CalculateS1ThinLens_ScalesWithY4()
    {
        double phi = 0.01;
        double n = 1.5;
        double X = 0.5;
        double Y = -1.0;

        double S1_y5 = AberrationCalculator.CalculateS1ThinLens(5.0, phi, n, X, Y);
        double S1_y10 = AberrationCalculator.CalculateS1ThinLens(10.0, phi, n, X, Y);

        // S1 ∝ y⁴, so doubling y should multiply S1 by 16
        double ratio = S1_y10 / S1_y5;
        Assert.True(Math.Abs(ratio - 16.0) < 0.01, $"Ratio={ratio}, expected 16");
    }

    [Fact]
    public void CalculateS1ThinLens_ScalesWithPhi3()
    {
        double y = 5.0;
        double n = 1.5;
        double X = 0.5;
        double Y = -1.0;

        double S1_phi1 = AberrationCalculator.CalculateS1ThinLens(y, 0.01, n, X, Y);
        double S1_phi2 = AberrationCalculator.CalculateS1ThinLens(y, 0.02, n, X, Y);

        // S1 ∝ φ³, so doubling φ should multiply S1 by 8
        double ratio = S1_phi2 / S1_phi1;
        Assert.True(Math.Abs(ratio - 8.0) < 0.01, $"Ratio={ratio}, expected 8");
    }

    [Fact]
    public void CalculatePositionFactor_ObjectAtInfinity_ReturnsMinusOne()
    {
        double Y = AberrationCalculator.CalculatePositionFactor(double.NegativeInfinity, 100);

        Assert.Equal(-1.0, Y, 10);
    }

    [Fact]
    public void CalculatePositionFactor_SymmetricConjugates_ReturnsZero()
    {
        // s = -100, s' = +100 (1:1 imaging)
        double Y = AberrationCalculator.CalculatePositionFactor(-100, 100);

        Assert.Equal(0.0, Y, 10);
    }

    [Fact]
    public void CalculateRayHeightAtSecondLens_NoGap_ReturnsSameHeight()
    {
        double y1 = 5.0;
        double phi1 = 0.01;
        double gap = 0.0;

        double y2 = AberrationCalculator.CalculateRayHeightAtSecondLens(y1, phi1, gap);

        Assert.Equal(y1, y2, 10);
    }

    [Fact]
    public void CalculateRayHeightAtSecondLens_WithGap_ReturnsReducedHeight()
    {
        double y1 = 5.0;
        double phi1 = 0.01; // Positive lens converges rays
        double gap = 10.0;

        double y2 = AberrationCalculator.CalculateRayHeightAtSecondLens(y1, phi1, gap);

        // y2 = y1(1 - d·φ1) = 5(1 - 10·0.01) = 5(0.9) = 4.5
        Assert.Equal(4.5, y2, 6);
    }

    [Fact]
    public void EvaluateSplitConfiguration_EqualSplit_ReturnsValidResult()
    {
        var calc = new AberrationCalculator();

        var result = calc.EvaluateSplitConfiguration(
            totalPower: 0.01,
            powerRatio: 0.5,
            airGap: 1.0,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0);

        Assert.Equal(0.5, result.PowerRatio);
        // Powers are scaled up to preserve combined power = totalPower
        // Phi1 and Phi2 should be equal (50/50 split) but > 0.005 to compensate for separation
        Assert.Equal(result.Phi1, result.Phi2, 6); // Equal split
        Assert.True(result.Phi1 > 0.005, "Phi1 should be > 0.005 to compensate for separation");
        Assert.Equal(-1.0, result.Y1);
        // Second lens sees converging rays (virtual object) since gap < f1
        // This gives Y2 < -1.0 (more negative than infinity conjugate)
        Assert.True(result.Y2 < -1.0, "Second lens sees virtual object (converging rays from first lens)");
        Assert.True(double.IsFinite(result.Y2), "Y2 should be finite");
    }

    [Fact]
    public void SplitReducesAberration_ComparedToSingleLens()
    {
        // Single lens S1
        double y = 5.0;
        double phi = 0.01;
        double n = 1.5;
        double Y = -1.0;
        double X_opt = AberrationCalculator.CalculateOptimalShapeFactor(n, Y);
        double singleS1 = AberrationCalculator.CalculateS1ThinLens(y, phi, n, X_opt, Y);

        // Split lens S1
        var calc = new AberrationCalculator();
        var splitResult = calc.EvaluateSplitConfiguration(phi, 0.5, 1.0, n, y);

        // Split should have lower |S1|
        Assert.True(Math.Abs(splitResult.S1_Total) < Math.Abs(singleS1),
            $"Split S1={splitResult.S1_Total:E3} should be less than single S1={singleS1:E3}");
    }

    [Fact]
    public void EvaluateSplitConfiguration_EffectivePower_PreservedWithScaling()
    {
        var calc = new AberrationCalculator();

        // With no gap, effective power = totalPower (no scaling needed)
        var resultNoGap = calc.EvaluateSplitConfiguration(0.01, 0.5, 0.0, 1.5, 5.0);
        Assert.Equal(0.01, resultNoGap.EffectivePower, 6);
        Assert.Equal(1.0, resultNoGap.PowerScaleFactor, 6); // No scaling needed

        // With gap, powers are scaled so effective power still equals totalPower
        var resultWithGap = calc.EvaluateSplitConfiguration(0.01, 0.5, 5.0, 1.5, 5.0);
        Assert.Equal(0.01, resultWithGap.EffectivePower, 4); // Should be close to target
        Assert.True(resultWithGap.PowerScaleFactor > 1.0,
            $"Scale factor {resultWithGap.PowerScaleFactor} should be > 1.0 to compensate for separation");
    }

    [Fact]
    public void CalculateS1ConicContribution_SphericalSurface_ReturnsZero()
    {
        // K = 0 (sphere) should give zero conic contribution
        double result = AberrationCalculator.CalculateS1ConicContribution(
            rayHeight: 5.0,
            radius: 50.0,
            conic: 0.0,
            nBefore: 1.0,
            nAfter: 1.5);

        Assert.Equal(0.0, result, 15);
    }

    [Fact]
    public void CalculateS1ConicContribution_ParabolicSurface_ReducesAberration()
    {
        double y = 5.0;
        double R = 50.0;
        double n = 1.5;

        // For a positive lens surface (n' > n, R > 0), a negative conic (parabola)
        // should reduce spherical aberration (add positive S1 to balance negative spherical S1)
        double conicContribution = AberrationCalculator.CalculateS1ConicContribution(
            rayHeight: y,
            radius: R,
            conic: -1.0, // Parabola
            nBefore: 1.0,
            nAfter: n);

        // The conic contribution should be positive for K < 0 on a positive surface
        // ΔS₁ = -(n' - n) · K · c³ · y⁴
        // = -(1.5 - 1.0) · (-1.0) · (1/50)³ · 5⁴
        // = -0.5 · (-1) · 8e-6 · 625
        // = 0.5 · 8e-6 · 625 = 0.0025
        Assert.True(conicContribution > 0, "Parabolic surface should add positive S1 contribution");
    }

    [Fact]
    public void CalculateS1WithConic_IncludesConicContribution()
    {
        double y = 5.0;
        double power = 0.01;
        double n = 1.5;
        double X = 0.357; // Near optimal
        double Y = -1.0;
        double R1 = 50.0;
        double R2 = -100.0;

        // Spherical only
        double S1_spherical = AberrationCalculator.CalculateS1ThinLens(y, power, n, X, Y);

        // With parabolic front surface
        double S1_withConic = AberrationCalculator.CalculateS1WithConic(
            y, power, n, X, Y, R1, R2, conic1: -0.5, conic2: 0.0);

        // Should be different due to conic contribution
        Assert.NotEqual(S1_spherical, S1_withConic);

        // Negative conic on front positive surface should reduce |S1| (make it less negative)
        Assert.True(S1_withConic > S1_spherical,
            $"Conic should reduce aberration: S1_conic={S1_withConic:E4} vs S1_sphere={S1_spherical:E4}");
    }

    [Fact]
    public void IsFiniteConjugate_InfiniteObject_ReturnsFalse()
    {
        var system = new OpticalSystem
        {
            Name = "Infinite Conjugate",
            Surfaces = new List<Surface>
            {
                new() { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new() { Index = 1, Radius = 50.0, Thickness = 5.0 },
                new() { Index = 2, Radius = -100.0, Thickness = 95.0 },
                new() { Index = 3, SurfaceType = SurfaceType.Image }
            }
        };

        Assert.False(AberrationCalculator.IsFiniteConjugate(system));
    }

    [Fact]
    public void IsFiniteConjugate_FiniteObject_ReturnsTrue()
    {
        var system = new OpticalSystem
        {
            Name = "Finite Conjugate (1:1)",
            Surfaces = new List<Surface>
            {
                new() { Index = 0, SurfaceType = SurfaceType.Object, Thickness = 200.0 }, // Object 200mm away
                new() { Index = 1, Radius = 50.0, Thickness = 5.0 },
                new() { Index = 2, Radius = -100.0, Thickness = 95.0 },
                new() { Index = 3, SurfaceType = SurfaceType.Image }
            }
        };

        Assert.True(AberrationCalculator.IsFiniteConjugate(system));
    }

    [Fact]
    public void GetObjectDistance_InfiniteConjugate_ReturnsNegativeInfinity()
    {
        var system = new OpticalSystem
        {
            Surfaces = new List<Surface>
            {
                new() { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new() { Index = 1, Radius = 50.0, Thickness = 5.0 },
                new() { Index = 2, SurfaceType = SurfaceType.Image }
            }
        };

        double objectDistance = AberrationCalculator.GetObjectDistance(system);

        Assert.True(double.IsNegativeInfinity(objectDistance));
    }

    [Fact]
    public void GetObjectDistance_FiniteConjugate_ReturnsNegativeValue()
    {
        var system = new OpticalSystem
        {
            Surfaces = new List<Surface>
            {
                new() { Index = 0, SurfaceType = SurfaceType.Object, Thickness = 200.0 },
                new() { Index = 1, Radius = 50.0, Thickness = 5.0 },
                new() { Index = 2, SurfaceType = SurfaceType.Image }
            }
        };

        double objectDistance = AberrationCalculator.GetObjectDistance(system);

        // Object distance should be negative (object to the left)
        Assert.Equal(-200.0, objectDistance, 6);
    }

    [Fact]
    public void EvaluateSplitConfiguration_FiniteConjugate_UsesCustomPositionFactor()
    {
        var calc = new AberrationCalculator();

        // Symmetric 1:1 conjugate (Y = 0)
        var resultFinite = calc.EvaluateSplitConfiguration(
            totalPower: 0.01,
            powerRatio: 0.5,
            airGap: 1.0,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0,
            objectPositionFactor: 0.0); // 1:1 imaging

        // Infinite conjugate (Y = -1)
        var resultInfinite = calc.EvaluateSplitConfiguration(
            totalPower: 0.01,
            powerRatio: 0.5,
            airGap: 1.0,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0,
            objectPositionFactor: -1.0); // Object at infinity

        // Position factors should be as specified
        Assert.Equal(0.0, resultFinite.Y1, 10);
        Assert.Equal(-1.0, resultInfinite.Y1, 10);

        // Optimal shapes should differ for different conjugates
        Assert.NotEqual(resultFinite.X1_Optimal, resultInfinite.X1_Optimal);

        // For Y=0 (symmetric), optimal shape should be equiconvex (X=0)
        Assert.Equal(0.0, resultFinite.X1_Optimal, 6);
    }

    [Fact]
    public void CalculatePositionFactor_FiniteConjugate_ReturnsCorrectValue()
    {
        // Object at -200mm, image at +200mm (1:1 imaging) → Y = 0
        double Y_symmetric = AberrationCalculator.CalculatePositionFactor(-200, 200);
        Assert.Equal(0.0, Y_symmetric, 6);

        // Object at -100mm, image at +200mm (2x magnification, m = -2)
        // Y = (s' + s) / (s' - s) = (200 + (-100)) / (200 - (-100)) = 100/300 = 1/3
        // For magnified images (|m| > 1), Y is between 0 and 1
        double Y_magnified = AberrationCalculator.CalculatePositionFactor(-100, 200);
        Assert.True(Math.Abs(Y_magnified - 1.0/3.0) < 0.001,
            $"Magnified finite conjugate should have Y ≈ 1/3, got {Y_magnified}");

        // Object at -400mm, image at +200mm (0.5x reduction, m = -0.5)
        // Y = (200 + (-400)) / (200 - (-400)) = -200/600 = -1/3
        // For reduced images (|m| < 1), Y is between -1 and 0
        double Y_reduced = AberrationCalculator.CalculatePositionFactor(-400, 200);
        Assert.True(Y_reduced > -1.0 && Y_reduced < 0.0,
            $"Reduced finite conjugate should have -1 < Y < 0, got {Y_reduced}");
    }
}
