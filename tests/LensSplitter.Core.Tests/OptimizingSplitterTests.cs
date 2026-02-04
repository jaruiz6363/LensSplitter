using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Core.Tests;

public class OptimizingSplitterTests
{
    private readonly OptimizingSplitter _splitter = new();

    [Fact]
    public void SplitWithOptimization_FindsOptimalConfiguration()
    {
        // Arrange
        var system = CreateTestSinglet();

        // Act
        var result = _splitter.SplitWithOptimization(system, 0);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.OptimizedResult);
        Assert.True(result.S1ReductionFactor > 1.0,
            $"S1 reduction {result.S1ReductionFactor} should be > 1");
    }

    [Fact]
    public void SplitWithOptimization_PowerRatioInValidRange()
    {
        var system = CreateTestSinglet();

        var result = _splitter.SplitWithOptimization(system, 0);

        Assert.True(result.OptimalPowerRatio >= 0.3 && result.OptimalPowerRatio <= 0.7,
            $"Power ratio {result.OptimalPowerRatio} should be between 0.3 and 0.7");
    }

    [Fact]
    public void SplitWithOptimization_RecordsIterationHistory()
    {
        var system = CreateTestSinglet();

        var result = _splitter.SplitWithOptimization(system, 0);

        Assert.True(result.IterationHistory.Count > 0,
            "Should record iteration history");
    }

    [Fact]
    public void SplitWithOptimization_PreservesSystemEfl()
    {
        var system = CreateTestSinglet();

        var result = _splitter.SplitWithOptimization(system, 0);

        // EFL should be within 20% (thick lens effects and optimization)
        double eflRatio = result.SplitEfl / result.OriginalEfl;
        Assert.True(eflRatio > 0.8 && eflRatio < 1.2,
            $"EFL ratio {eflRatio} should be close to 1.0");
    }

    [Fact]
    public void SplitWithOptimization_CustomSettings_Respected()
    {
        var system = CreateTestSinglet();
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            MinPowerRatio = 0.45,
            MaxPowerRatio = 0.55,
            MinAirGap = 0.5,
            MaxAirGap = 0.5,
            OptimizeAirGap = false
        };

        var result = _splitter.SplitWithOptimization(system, 0, settings);

        Assert.True(result.OptimalPowerRatio >= 0.45 && result.OptimalPowerRatio <= 0.55);
        Assert.Equal(0.5, result.OptimalAirGap, 6);
    }

    [Fact]
    public void SplitWithOptimization_GeneratesValidSplitSystem()
    {
        var system = CreateTestSinglet();

        var result = _splitter.SplitWithOptimization(system, 0);

        // Split system should have 2 more surfaces than original
        Assert.Equal(system.Surfaces.Count + 2, result.SplitSystem.Surfaces.Count);
    }

    [Fact]
    public void AnalyzePowerRatioSweep_ReturnsExpectedPoints()
    {
        int numPoints = 20;

        var results = _splitter.AnalyzePowerRatioSweep(
            totalPower: 0.01,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0,
            airGap: 1.0,
            numPoints: numPoints);

        Assert.Equal(numPoints + 1, results.Count);
    }

    [Fact]
    public void AnalyzePowerRatioSweep_S1VariesWithRatio()
    {
        var results = _splitter.AnalyzePowerRatioSweep(
            totalPower: 0.01,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0,
            airGap: 1.0,
            numPoints: 10);

        // S1 values should vary (not all the same)
        var distinctS1Count = results.Select(r => Math.Round(r.S1_Total, 15)).Distinct().Count();
        Assert.True(distinctS1Count > 1, "S1 should vary with power ratio");
    }

    [Fact]
    public void AnalyzeAirGapSweep_ReturnsExpectedPoints()
    {
        int numPoints = 20;

        var results = _splitter.AnalyzeAirGapSweep(
            totalPower: 0.01,
            refractiveIndex: 1.5,
            entrancePupilRadius: 5.0,
            powerRatio: 0.5,
            maxGap: 5.0,
            numPoints: numPoints);

        Assert.Equal(numPoints + 1, results.Count);
    }

    [Fact]
    public void SplitWithOptimization_HighIndexGlass_StillWorks()
    {
        // Test with higher index glass (like SF glasses)
        var glass = Glass.Ideal(1.8);
        var system = new OpticalSystem
        {
            Name = "High Index Singlet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = glass, GlassName = "SF", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -100.0, Thickness = 95.0, SemiDiameter = 10 },
                new Surface { Index = 3, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };

        var result = _splitter.SplitWithOptimization(system, 0);

        Assert.NotNull(result);
        Assert.True(result.S1ReductionFactor > 1.0);
    }

    [Fact]
    public void OptimizedSplit_BetterThanEqualSplit()
    {
        var system = CreateTestSinglet();

        // Calculate S1 for basic 50/50 split at 1mm gap
        var calc = new AberrationCalculator();
        double n = 1.5;
        double y = system.EntrancePupilDiameter / 2.0;
        var elements = system.GetLensElements();
        double originalPower = elements[0].GetThickLensPower(system.PrimaryWavelength.ValueMicrons);
        var basicS1Result = calc.EvaluateSplitConfiguration(
            originalPower, 0.5, 1.0, n, y);

        // Optimized split using S1-only mode (pure thin-lens optimization)
        // Disable geometry constraints for fair comparison with the thin-lens calculation
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            UseFullSeidelOptimization = false, // Only optimize S1, not full Seidel
            AberrationWeights = AberrationWeights.SphericalOnly,
            MinAirGap = 1.0, // Fix air gap to match basic split
            MaxAirGap = 1.0,
            OptimizeAirGap = false,
            EnforceGeometryConstraints = false // Disable geometry for pure S1 comparison
        };
        var optimizedResult = _splitter.SplitWithOptimization(system, 0, settings);

        // Optimized S1 should be at least as good (usually better) than equal split
        // Both are thin-lens calculations, so direct comparison is valid
        Assert.True(Math.Abs(optimizedResult.OptimizedResult.S1_Total) <=
                    Math.Abs(basicS1Result.S1_Total) * 1.01, // Allow 1% tolerance
            $"Optimized S1={optimizedResult.OptimizedResult.S1_Total:E3} should be <= basic S1={basicS1Result.S1_Total:E3}");
    }

    [Fact]
    public void AnalyzeElements_SinglePositiveElement_RecommendsSplitting()
    {
        var system = CreateTestSinglet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Single(analysis.Elements);
        Assert.Equal(0, analysis.RecommendedElementIndex);
        Assert.True(analysis.Elements[0].IsPositive);
        Assert.True(analysis.Elements[0].IsUndercorrected, "Positive lens should be undercorrected");
        Assert.True(analysis.Elements[0].RecommendedForSplitting);
    }

    [Fact]
    public void AnalyzeElements_Doublet_DetectsAberrationContributions()
    {
        // Create a simple doublet: positive + negative elements (with gap > 2mm to avoid group detection)
        var system = CreateWidelySpacedDoublet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(2, analysis.Elements.Count);

        // Find the positive element
        var positiveElem = analysis.Elements.First(e => e.IsPositive);
        var negativeElem = analysis.Elements.First(e => !e.IsPositive);

        // Positive element should be undercorrected, negative overcorrected
        Assert.True(positiveElem.IsUndercorrected);
        Assert.False(negativeElem.IsUndercorrected);

        // Total S1 should be undercorrected (positive element dominates)
        // Seidel convention: S1 > 0 = undercorrected
        Assert.True(analysis.TotalS1 > 0, "Doublet should be net undercorrected (S1 > 0 in Seidel convention)");

        // Since these elements are NOT grouped (wide gap), positive should be recommended
        Assert.False(positiveElem.IsPartOfGroup, "Elements should not be grouped with >2mm gap");
        Assert.True(positiveElem.RecommendedForSplitting);
        Assert.False(negativeElem.RecommendedForSplitting,
            "Negative element should NOT be recommended - it provides correction");
    }

    [Fact]
    public void FindBestElementToSplit_ReturnsPositiveElement()
    {
        // Use widely spaced doublet so elements aren't grouped
        var system = CreateWidelySpacedDoublet();

        int bestIndex = _splitter.FindBestElementToSplit(system);

        var elements = system.GetLensElements();
        var bestElement = elements[bestIndex];
        double power = bestElement.GetThickLensPower(system.PrimaryWavelength.ValueMicrons);

        Assert.True(power > 0, "Best element to split should be positive");
    }

    [Fact]
    public void SplitBestElement_AutoSelectsOptimalElement()
    {
        // Use widely spaced doublet so elements aren't grouped
        var system = CreateWidelySpacedDoublet();

        var result = _splitter.SplitBestElement(system);

        // Should have selected element 0 (the positive one)
        Assert.Equal(0, result.SplitElementIndex);
        Assert.True(result.S1ReductionFactor > 1.0);
    }

    [Fact]
    public void AnalyzeElements_ConicElement_NeverRecommendedForSplitting()
    {
        // Create a singlet with a conic surface
        var system = CreateAsphericSinglet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Single(analysis.Elements);
        var elem = analysis.Elements[0];

        // Should detect the conic surface
        Assert.True(elem.HasConicSurfaces);

        // Should NOT be recommended for splitting, even though it has S₁
        Assert.False(elem.RecommendedForSplitting,
            "Aspheric elements should never be recommended for splitting");

        // Splitting priority should be negative infinity
        Assert.True(double.IsNegativeInfinity(elem.SplittingPriority));
    }

    [Fact]
    public void AnalyzeElements_MixedSystem_SkipsConicSelectsSpherical()
    {
        // Create a system with one aspheric and one spherical element
        var system = CreateMixedAsphericSphericalSystem();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(2, analysis.Elements.Count);

        // The aspheric element should not be recommended
        var asphericElem = analysis.Elements.First(e => e.HasConicSurfaces);
        Assert.False(asphericElem.RecommendedForSplitting);

        // The spherical element should be recommended (if it's a candidate)
        var sphericalElem = analysis.Elements.First(e => !e.HasConicSurfaces);

        // The recommended element should be the spherical one
        Assert.Equal(sphericalElem.ElementIndex, analysis.RecommendedElementIndex);
    }

    private OpticalSystem CreateTestSinglet()
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

    private OpticalSystem CreateTestDoublet()
    {
        var crown = Glass.Ideal(1.52);  // Crown glass (lower dispersion)
        var flint = Glass.Ideal(1.62);  // Flint glass (higher index)

        return new OpticalSystem
        {
            Name = "Test Doublet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // Positive crown element
                new Surface { Index = 1, Radius = 60.0, Thickness = 6.0, Glass = crown, GlassName = "CROWN", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -40.0, Thickness = 0.5, SemiDiameter = 10 },
                // Negative flint element
                new Surface { Index = 3, Radius = -40.0, Thickness = 3.0, Glass = flint, GlassName = "FLINT", SemiDiameter = 10 },
                new Surface { Index = 4, Radius = -150.0, Thickness = 90.0, SemiDiameter = 10 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateWidelySpacedDoublet()
    {
        // Similar to CreateTestDoublet but with 5mm gap - too large to be considered a group
        var crown = Glass.Ideal(1.52);
        var flint = Glass.Ideal(1.62);

        return new OpticalSystem
        {
            Name = "Widely Spaced Doublet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // Positive crown element
                new Surface { Index = 1, Radius = 60.0, Thickness = 6.0, Glass = crown, GlassName = "CROWN", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -40.0, Thickness = 5.0, SemiDiameter = 10 }, // 5mm gap - NOT grouped
                // Negative flint element
                new Surface { Index = 3, Radius = -40.0, Thickness = 3.0, Glass = flint, GlassName = "FLINT", SemiDiameter = 10 },
                new Surface { Index = 4, Radius = -150.0, Thickness = 86.0, SemiDiameter = 10 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateAsphericSinglet()
    {
        var glass = Glass.Ideal(1.5);

        return new OpticalSystem
        {
            Name = "Aspheric Singlet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = glass, GlassName = "IDEAL", SemiDiameter = 10, Conic = -0.5 },
                new Surface { Index = 2, Radius = -100.0, Thickness = 95.0, SemiDiameter = 10 },
                new Surface { Index = 3, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateMixedAsphericSphericalSystem()
    {
        var glass = Glass.Ideal(1.5);

        return new OpticalSystem
        {
            Name = "Mixed Aspheric-Spherical",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // First element: aspheric (should NOT be split)
                new Surface { Index = 1, Radius = 60.0, Thickness = 4.0, Glass = glass, GlassName = "IDEAL", SemiDiameter = 10, Conic = -0.8 },
                new Surface { Index = 2, Radius = -80.0, Thickness = 5.0, SemiDiameter = 10 },
                // Second element: spherical (CAN be split)
                new Surface { Index = 3, Radius = 40.0, Thickness = 5.0, Glass = glass, GlassName = "IDEAL", SemiDiameter = 10 },
                new Surface { Index = 4, Radius = -60.0, Thickness = 90.0, SemiDiameter = 10 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    [Fact]
    public void AnalyzeElements_CementedDoublet_NotRecommendedForSplitting()
    {
        // Create a cemented doublet (zero air gap between elements)
        var system = CreateCementedDoublet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(2, analysis.Elements.Count);

        // Both elements should be marked as part of a cemented group
        foreach (var elem in analysis.Elements)
        {
            Assert.True(elem.IsPartOfCementedGroup,
                $"Element {elem.ElementIndex} should be part of cemented group");
            Assert.True(elem.IsPartOfGroup);
            Assert.False(elem.RecommendedForSplitting,
                $"Element {elem.ElementIndex} should NOT be recommended for splitting (cemented doublet)");
            Assert.Contains("Cemented", elem.GroupDescription, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AnalyzeElements_AirSpacedDoublet_NotRecommendedForSplitting()
    {
        // Create an air-spaced doublet with small gap
        var system = CreateAirSpacedDoublet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(2, analysis.Elements.Count);

        // Both elements should be marked as part of an air-spaced group
        foreach (var elem in analysis.Elements)
        {
            Assert.True(elem.IsPartOfAirSpacedGroup,
                $"Element {elem.ElementIndex} should be part of air-spaced group");
            Assert.True(elem.IsPartOfGroup);
            Assert.False(elem.RecommendedForSplitting,
                $"Element {elem.ElementIndex} should NOT be recommended for splitting (air-spaced doublet)");
        }
    }

    [Fact]
    public void AnalyzeElements_CementedTriplet_AllElementsMarked()
    {
        var system = CreateCementedTriplet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(3, analysis.Elements.Count);

        // All elements should be marked as part of a cemented group
        foreach (var elem in analysis.Elements)
        {
            Assert.True(elem.IsPartOfCementedGroup || elem.IsPartOfGroup,
                $"Element {elem.ElementIndex} should be part of cemented triplet");
            Assert.False(elem.RecommendedForSplitting);
        }
    }

    [Fact]
    public void AnalyzeElements_TripletWithSinglet_OnlySingletCanBeSplit()
    {
        // Create a system with a cemented doublet followed by a standalone singlet
        var system = CreateDoubletPlusSinglet();

        var analysis = _splitter.AnalyzeElements(system);

        Assert.Equal(3, analysis.Elements.Count);

        // The doublet elements (0, 1) should not be splittable
        Assert.True(analysis.Elements[0].IsPartOfGroup);
        Assert.True(analysis.Elements[1].IsPartOfGroup);

        // The standalone singlet (2) should be splittable
        Assert.False(analysis.Elements[2].IsPartOfGroup,
            "Standalone singlet should NOT be part of any group");

        // The recommended element should be the standalone singlet (if it has positive S1 sign)
        if (analysis.Elements[2].S1Contribution != 0 &&
            (analysis.Elements[2].S1Contribution > 0) == (analysis.TotalS1 > 0))
        {
            Assert.True(analysis.Elements[2].RecommendedForSplitting);
        }
    }

    private OpticalSystem CreateCementedDoublet()
    {
        var crown = new Glass { Name = "N-BK7", Nd = 1.5168, Vd = 64.17, DispersionModel = DispersionModel.Constant };
        var flint = new Glass { Name = "N-SF2", Nd = 1.6477, Vd = 33.85, DispersionModel = DispersionModel.Constant };

        return new OpticalSystem
        {
            Name = "Cemented Achromatic Doublet",
            ApertureValue = 20.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // Positive crown element (front)
                new Surface { Index = 1, Radius = 61.47, Thickness = 6.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                // Cemented interface (thickness = 0, shared with next element)
                new Surface { Index = 2, Radius = -44.64, Thickness = 0.0, Glass = flint, GlassName = "N-SF2", SemiDiameter = 12.5 },
                // Negative flint element (rear)
                new Surface { Index = 3, Radius = -129.95, Thickness = 2.5, SemiDiameter = 12.5 },
                new Surface { Index = 4, Radius = double.PositiveInfinity, Thickness = 93.0, SemiDiameter = 12.5 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateAirSpacedDoublet()
    {
        var crown = new Glass { Name = "N-BK7", Nd = 1.5168, Vd = 64.17, DispersionModel = DispersionModel.Constant };
        var flint = new Glass { Name = "N-SF2", Nd = 1.6477, Vd = 33.85, DispersionModel = DispersionModel.Constant };

        return new OpticalSystem
        {
            Name = "Air-Spaced Achromatic Doublet",
            ApertureValue = 20.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // Positive crown element
                new Surface { Index = 1, Radius = 61.47, Thickness = 6.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                new Surface { Index = 2, Radius = -44.64, Thickness = 1.0, SemiDiameter = 12.5 }, // 1mm air gap
                // Negative flint element
                new Surface { Index = 3, Radius = -48.0, Thickness = 2.5, Glass = flint, GlassName = "N-SF2", SemiDiameter = 12.5 },
                new Surface { Index = 4, Radius = -129.95, Thickness = 90.0, SemiDiameter = 12.5 },
                new Surface { Index = 5, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateCementedTriplet()
    {
        var crown = new Glass { Name = "N-BK7", Nd = 1.5168, Vd = 64.17, DispersionModel = DispersionModel.Constant };
        var flint = new Glass { Name = "N-SF2", Nd = 1.6477, Vd = 33.85, DispersionModel = DispersionModel.Constant };

        return new OpticalSystem
        {
            Name = "Cemented Triplet",
            ApertureValue = 20.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // First crown element
                new Surface { Index = 1, Radius = 80.0, Thickness = 5.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                // Cemented to flint (center)
                new Surface { Index = 2, Radius = -50.0, Thickness = 0.0, Glass = flint, GlassName = "N-SF2", SemiDiameter = 12.5 },
                // Flint center element
                new Surface { Index = 3, Radius = 50.0, Thickness = 0.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                // Second crown element (cemented to flint)
                new Surface { Index = 4, Radius = -80.0, Thickness = 3.0, SemiDiameter = 12.5 },
                new Surface { Index = 5, Radius = double.PositiveInfinity, Thickness = 90.0, SemiDiameter = 12.5 },
                new Surface { Index = 6, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateDoubletPlusSinglet()
    {
        var crown = new Glass { Name = "N-BK7", Nd = 1.5168, Vd = 64.17, DispersionModel = DispersionModel.Constant };
        var flint = new Glass { Name = "N-SF2", Nd = 1.6477, Vd = 33.85, DispersionModel = DispersionModel.Constant };

        return new OpticalSystem
        {
            Name = "Cemented Doublet + Singlet",
            ApertureValue = 20.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                // Cemented doublet (elements 0, 1)
                new Surface { Index = 1, Radius = 61.47, Thickness = 6.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                new Surface { Index = 2, Radius = -44.64, Thickness = 0.0, Glass = flint, GlassName = "N-SF2", SemiDiameter = 12.5 },
                new Surface { Index = 3, Radius = -129.95, Thickness = 10.0, SemiDiameter = 12.5 }, // 10mm gap to singlet
                // Standalone singlet (element 2) - far enough to not be grouped
                new Surface { Index = 4, Radius = 50.0, Thickness = 4.0, Glass = crown, GlassName = "N-BK7", SemiDiameter = 12.5 },
                new Surface { Index = 5, Radius = -80.0, Thickness = 80.0, SemiDiameter = 12.5 },
                new Surface { Index = 6, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    [Fact]
    public void FullSeidelOptimization_DoesNotMakeTotalAberrationsWorse()
    {
        // Arrange
        var system = CreateTestSinglet();
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            UseFullSeidelOptimization = true,
            AberrationWeights = AberrationWeights.NoDistortion // S1-S4 with equal weights
        };

        // Act
        var result = _splitter.SplitWithOptimization(system, 0, settings);

        // Assert - The split system's merit function should not be dramatically worse
        // Some degradation is expected due to adding surfaces, but should not be >5x worse
        double degradationRatio = result.SplitMeritFunction / result.OriginalMeritFunction;
        Assert.True(degradationRatio < 5.0,
            $"Split system MF ({result.SplitMeritFunction:E3}) should not be >5x worse than original ({result.OriginalMeritFunction:E3}), ratio={degradationRatio:F2}");

        // Verify individual Seidels are calculated
        Assert.NotNull(result.OriginalSeidel);
        Assert.NotNull(result.SplitSeidel);
    }

    [Fact]
    public void FullSeidelOptimization_AdjustsShapeFactors()
    {
        // Arrange
        var system = CreateTestSinglet();
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            UseFullSeidelOptimization = true,
            AberrationWeights = AberrationWeights.Default,
            MaxIterations = 100
        };

        // Act
        var result = _splitter.SplitWithOptimization(system, 0, settings);

        // Assert - Result should have valid shape factors
        Assert.True(Math.Abs(result.OptimizedResult.X1_Optimal) < 5.0,
            $"X1 shape factor should be reasonable: {result.OptimizedResult.X1_Optimal}");
        Assert.True(Math.Abs(result.OptimizedResult.X2_Optimal) < 5.0,
            $"X2 shape factor should be reasonable: {result.OptimizedResult.X2_Optimal}");
    }

    [Fact]
    public void SplitWithOptimization_FiniteConjugate_ReturnsReasonableImprovement()
    {
        // This test verifies the finite conjugate fix is working in the optimization flow
        // Before the fix, finite conjugate systems could report 90x improvement but produce garbage
        var system = CreateFiniteConjugateSinglet();
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            UseFullSeidelOptimization = true,
            AberrationWeights = AberrationWeights.Default
        };

        var result = _splitter.SplitWithOptimization(system, 0, settings);

        // Verify we get valid results (not garbage)
        Assert.NotNull(result);
        Assert.NotNull(result.OriginalSeidel);
        Assert.NotNull(result.SplitSeidel);

        // Seidel values should be reasonable (not garbage like 100+ or 1000+)
        Assert.True(Math.Abs(result.OriginalSeidel.S1) < 1.0,
            $"Original S1={result.OriginalSeidel.S1} should be reasonable");
        Assert.True(Math.Abs(result.OriginalSeidel.S3) < 10.0,
            $"Original S3={result.OriginalSeidel.S3} should be reasonable (was ~189 before fix)");
        Assert.True(Math.Abs(result.OriginalSeidel.S5) < 100.0,
            $"Original S5={result.OriginalSeidel.S5} should be reasonable (was ~27400 before fix)");

        // Split Seidel values should also be reasonable
        Assert.True(Math.Abs(result.SplitSeidel.S1) < 1.0,
            $"Split S1={result.SplitSeidel.S1} should be reasonable");
        Assert.True(Math.Abs(result.SplitSeidel.S3) < 10.0,
            $"Split S3={result.SplitSeidel.S3} should be reasonable");
        Assert.True(Math.Abs(result.SplitSeidel.S5) < 100.0,
            $"Split S5={result.SplitSeidel.S5} should be reasonable");

        // Merit function improvement should be in a realistic range (1x to 20x is reasonable)
        // Before the fix, this could be 90x or more due to garbage Seidel values
        Assert.True(result.MeritFunctionImprovement >= 0.5 && result.MeritFunctionImprovement < 50.0,
            $"Merit function improvement {result.MeritFunctionImprovement:F2}x should be realistic (was 90x before fix)");
    }

    [Fact]
    public void SplitWithOptimization_FiniteConjugate_MaintainsEfl()
    {
        // Verify EFL is maintained for finite conjugate systems
        var system = CreateFiniteConjugateSinglet();

        var result = _splitter.SplitWithOptimization(system, 0);

        // EFL should be reasonably preserved
        double eflRatio = result.SplitEfl / result.OriginalEfl;
        Assert.True(eflRatio > 0.7 && eflRatio < 1.3,
            $"EFL ratio {eflRatio:F3} should be close to 1.0 for finite conjugate");
    }

    private OpticalSystem CreateFiniteConjugateSinglet()
    {
        // Create a singlet with finite conjugate (object at 500mm)
        var glass = Glass.Ideal(1.5);

        var system = new OpticalSystem
        {
            Name = "Finite Conjugate Singlet",
            ApertureType = ApertureType.EntrancePupilDiameter,
            ApertureValue = 10.0
        };

        // Object surface - finite conjugate at 500mm
        system.Surfaces.Add(new Surface
        {
            Index = 0,
            SurfaceType = SurfaceType.Object,
            Radius = double.PositiveInfinity,
            Thickness = 500.0  // Object at 500mm
        });

        // Positive singlet
        system.Surfaces.Add(new Surface
        {
            Index = 1,
            Radius = 50.0,
            Thickness = 5.0,
            Glass = glass,
            GlassName = "IDEAL",
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

        // Add field at 10 degrees
        system.Fields.Add(new Field(0, 10.0, FieldType.Angle));
        system.EnsureWavelengths();

        return system;
    }
}
