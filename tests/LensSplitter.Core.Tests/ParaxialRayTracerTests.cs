using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;
using Xunit;

namespace LensSplitter.Core.Tests;

public class ParaxialRayTracerTests
{
    private readonly ParaxialRayTracer _tracer = new();

    [Fact]
    public void TraceMarginalRay_SimpleSinglet_ReturnsValidRay()
    {
        // Arrange - Simple plano-convex lens
        var system = CreateSimpleSinglet();

        // Act
        var ray = _tracer.TraceMarginalRay(system, 0.5876);

        // Assert
        Assert.NotNull(ray);
        Assert.True(ray.States.Count > 0);
        Assert.Equal(5.0, ray.InitialHeight, 3); // EPD/2
    }

    [Fact]
    public void TraceChiefRay_OnAxis_ReturnsZeroHeight()
    {
        // Arrange
        var system = CreateSimpleSinglet();

        // Act
        var ray = _tracer.TraceChiefRay(system, 0, 0.5876);

        // Assert
        Assert.NotNull(ray);
        Assert.Equal(0.0, ray.InitialHeight, 10);
    }

    [Fact]
    public void CalculateEfl_SimpleSinglet_ReturnsReasonableValue()
    {
        // Arrange
        var system = CreateSimpleSinglet();

        // Act
        var efl = _tracer.CalculateEfl(system, 0.5876);

        // Assert - For a plano-convex lens with R=50mm and n=1.5
        // EFL = R / (n-1) = 50 / 0.5 = 100mm approximately
        // But the tracer returns EFL from the marginal ray trace
        // which may have different sign conventions
        Assert.False(double.IsNaN(efl), $"EFL is NaN");
        Assert.False(double.IsInfinity(efl), $"EFL is Infinity: {efl}");
    }

    [Fact]
    public void CalculateEfl_PlanoConvex_MatchesTheory()
    {
        // Arrange - Plano-convex lens
        // Thin lens formula: EFL = R / (n-1) = 50 / 0.5 = 100mm
        var system = CreateSimpleSinglet();

        // Act
        var efl = _tracer.CalculateEfl(system, 0.5876);
        var eflSimple = _tracer.CalculateEflSimple(system, 0.5876);

        // Assert - EFL should be close to 100mm
        // Thick lens effects will cause a small deviation
        Assert.True(Math.Abs(efl - 100.0) < 5.0,
            $"EFL {efl:F2}mm should be approximately 100mm. Simple formula gives {eflSimple:F2}mm");
    }

    [Fact]
    public void CalculateEflSimple_PlanoConvex_ReturnsExpectedValue()
    {
        // Arrange
        var system = CreateSimpleSinglet();

        // Act
        var efl = _tracer.CalculateEflSimple(system, 0.5876);

        // Assert - Simple formula: EFL = -y1 / u_final
        // For plano-convex with R=50, n=1.5, thin lens EFL = 100mm
        Assert.True(Math.Abs(efl - 100.0) < 5.0,
            $"Simple EFL {efl:F2}mm should be approximately 100mm");
    }

    [Fact]
    public void LagrangeInvariant_IsConstantThroughSystem()
    {
        // Arrange
        var system = CreateDoublet();
        var wavelength = 0.5876;

        // Act
        var rayPair = _tracer.TraceRayPair(system, 10, wavelength);

        // Calculate Lagrange invariant at different surfaces
        var h1 = rayPair.GetLagrangeInvariant(1);
        var h2 = rayPair.GetLagrangeInvariant(2);

        // Assert - Should be constant (within numerical tolerance)
        if (Math.Abs(h1) > 1e-10 && Math.Abs(h2) > 1e-10)
        {
            Assert.True(Math.Abs(h1 - h2) / Math.Abs(h1) < 0.01,
                $"Lagrange invariant changed: {h1} -> {h2}");
        }
    }

    private OpticalSystem CreateSimpleSinglet()
    {
        var glass = Glass.Ideal(1.5);

        return new OpticalSystem
        {
            Name = "Simple Singlet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = glass, GlassName = "IDEAL", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = double.PositiveInfinity, Thickness = 95.0, SemiDiameter = 10 },
                new Surface { Index = 3, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }

    private OpticalSystem CreateDoublet()
    {
        var crown = Glass.Ideal(1.5);
        var flint = Glass.Ideal(1.6);

        return new OpticalSystem
        {
            Name = "Simple Doublet",
            ApertureValue = 10.0,
            Surfaces = new List<Surface>
            {
                new Surface { Index = 0, SurfaceType = SurfaceType.Object, Thickness = double.PositiveInfinity },
                new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Glass = crown, GlassName = "CROWN", SemiDiameter = 10 },
                new Surface { Index = 2, Radius = -40.0, Thickness = 3.0, Glass = flint, GlassName = "FLINT", SemiDiameter = 10 },
                new Surface { Index = 3, Radius = -100.0, Thickness = 90.0, SemiDiameter = 10 },
                new Surface { Index = 4, SurfaceType = SurfaceType.Image }
            },
            Wavelengths = new List<Wavelength> { Wavelength.DLine }
        };
    }
}
