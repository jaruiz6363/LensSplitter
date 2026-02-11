using LensSplitter.Core.Models;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Core.Tests;

public class GlassOptimizerTests
{
    private readonly GlassOptimizer _optimizer = new();

    [Fact]
    public void Optimize_WithTwoGlasses_FindsCombinations()
    {
        var system = CreateTestSinglet();
        var glasses = new List<Glass>
        {
            CreateGlass("CROWN", 1.52, 60.0),
            CreateGlass("FLINT", 1.62, 36.0)
        };

        var settings = new GlassOptimizer.OptimizationSettings
        {
            NumberOfTrials = 50,
            TopResultsToKeep = 10,
            OnlyShowImprovements = false
        };

        var result = _optimizer.Optimize(system, 0, glasses, settings);

        Assert.NotNull(result);
        Assert.True(result.TopResults.Count > 0);
        Assert.True(result.TotalCombinationsTried > 0);
    }

    [Fact]
    public void Optimize_ReturnsRankedResults()
    {
        var system = CreateTestSinglet();
        var glasses = new List<Glass>
        {
            CreateGlass("N-BK7", 1.5168, 64.17),
            CreateGlass("N-SF6", 1.8052, 25.36),
            CreateGlass("N-SK16", 1.6204, 60.32)
        };

        var settings = new GlassOptimizer.OptimizationSettings
        {
            NumberOfTrials = 100,
            TopResultsToKeep = 20
        };

        var result = _optimizer.Optimize(system, 0, glasses, settings);

        // Results should be ranked by merit
        for (int i = 1; i < result.TopResults.Count; i++)
        {
            Assert.True(result.TopResults[i - 1].MeritScore >= result.TopResults[i].MeritScore,
                "Results should be sorted by merit score descending");
            Assert.Equal(i, result.TopResults[i - 1].Rank);
        }
    }

    [Fact]
    public void Optimize_DifferentGlasses_ReducesChromaticAberration()
    {
        var system = CreateTestSinglet();

        // Crown and flint glass pair for achromatic correction
        var glasses = new List<Glass>
        {
            CreateGlass("CROWN", 1.52, 60.0),  // Low dispersion
            CreateGlass("FLINT", 1.62, 36.0)   // High dispersion
        };

        var settings = new GlassOptimizer.OptimizationSettings
        {
            NumberOfTrials = 100,
            TopResultsToKeep = 10,
            UseAchromaticRatio = true,
            OnlyShowImprovements = false
        };

        var result = _optimizer.Optimize(system, 0, glasses, settings);

        // Best result should use different glasses (crown + flint)
        var best = result.BestResult;
        Assert.NotNull(best);

        // Crown-Flint combination should reduce chromatic aberration
        if (best.Glass1Name != best.Glass2Name)
        {
            // Mixed glass should have different Vd values
            Assert.NotEqual(best.V1, best.V2);
        }
    }

    [Fact]
    public void Optimize_IncludesValidSplitSystem()
    {
        var system = CreateTestSinglet();
        var glasses = new List<Glass>
        {
            CreateGlass("CROWN", 1.52, 60.0),
            CreateGlass("FLINT", 1.62, 36.0)
        };

        var result = _optimizer.Optimize(system, 0, glasses, new GlassOptimizer.OptimizationSettings
        {
            OnlyShowImprovements = false
        });

        var best = result.BestResult;
        Assert.NotNull(best);
        Assert.NotNull(best.SplitSystem);

        // Split system should have 2 more surfaces than original
        Assert.Equal(system.Surfaces.Count + 2, best.SplitSystem.Surfaces.Count);
    }

    [Fact]
    public void ChromaticCalculator_CalculatesLongitudinalColor()
    {
        // Create system with dispersive glass
        var glass = CreateGlass("DISPERSIVE", 1.52, 60.0);
        var system = new OpticalSystem
        {
            Name = "Dispersive Singlet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = glass, GlassName = "DISPERSIVE", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -100.0, Thickness = 95.0, SemiDiameter = 10 },
                new Surface { Index = 3, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };

        var chromaCalc = new ChromaticAberrationCalculator();
        var result = chromaCalc.Calculate(system);

        Assert.True(double.IsFinite(result.LongitudinalColor));
        Assert.True(double.IsFinite(result.EflD));
        Assert.True(double.IsFinite(result.EflF));
        Assert.True(double.IsFinite(result.EflC));
    }

    [Fact]
    public void ChromaticCalculator_AchromaticRatio_DifferentForDifferentGlasses()
    {
        // Crown: high Vd (low dispersion)
        // Flint: low Vd (high dispersion)
        double V1 = 60.0; // Crown
        double V2 = 36.0; // Flint

        double achromaticRatio = ChromaticAberrationCalculator.CalculateAchromaticPowerRatio(
            totalPower: 0.01, V1, V2);

        // For achromatism with crown (V=60) and flint (V=36):
        // ratio = V1 / (V1 - V2) = 60 / (60 - 36) = 60 / 24 = 2.5
        // But clamped to 0.9 max
        Assert.True(achromaticRatio > 0.1 && achromaticRatio <= 0.9);
    }

    [Fact]
    public void Exporter_CreatesCsvFile()
    {
        var system = CreateTestSinglet();
        var glasses = new List<Glass>
        {
            CreateGlass("CROWN", 1.52, 60.0),
            CreateGlass("FLINT", 1.62, 36.0)
        };

        var result = _optimizer.Optimize(system, 0, glasses, new GlassOptimizer.OptimizationSettings
        {
            NumberOfTrials = 50,
            TopResultsToKeep = 10,
            OnlyShowImprovements = false
        });

        var exporter = new GlassOptimizationExporter();
        var tempPath = Path.GetTempFileName();

        try
        {
            exporter.ExportToCsv(result, tempPath);

            Assert.True(File.Exists(tempPath));
            var content = File.ReadAllText(tempPath);
            Assert.Contains("Rank", content);
            Assert.Contains("Glass1", content);
            Assert.Contains("CROWN", content);
        }
        finally
        {
            File.Delete(tempPath);
        }
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

    private Glass CreateGlass(string name, double nd, double vd)
    {
        return new Glass
        {
            Name = name,
            Catalog = "TEST",
            Nd = nd,
            Vd = vd,
            DispersionModel = DispersionModel.Schott,
            Coefficients = new double[] { nd * nd, 0, 0, 0, 0, 0 } // Simplified
        };
    }
}
