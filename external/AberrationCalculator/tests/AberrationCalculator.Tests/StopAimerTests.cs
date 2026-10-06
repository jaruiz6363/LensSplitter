using System;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Ray aiming at the real stop.
///
/// <para>The check is the definition: a ray launched where the aimer says must cross the stop
/// where it was asked to, which is tested by tracing it through the WHOLE lens - not the cut-off
/// copy the aimer searches with - and reading its crossing. The double Gauss has its stop between
/// two groups, so its paraxial and real pupils differ and the aiming has something to do.</para>
/// </summary>
public class StopAimerTests
{
    private static (OpticalSystem System, double[] N, ParaxialResult P, double Field) DoubleGauss()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("KingslakeDG"), catalog);
        double lambda = system.Wavelengths[system.PrimaryWavelengthIndex].Value;
        var n = IndexResolver.Build(system, catalog, lambda);
        double field = system.Fields.Max(f => Math.Abs(f.Y));
        return (system, n, ParaxialTrace.Trace(system, n, field), field);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(1.0, 1.0, 0.0)]
    [InlineData(1.0, -1.0, 0.0)]
    [InlineData(1.0, 0.0, 1.0)]
    [InlineData(0.7, 0.6, -0.5)]
    public void AnAimedRayCrossesTheStopWhereItWasAsked(double fieldFraction, double stopY, double stopX)
    {
        var (system, n, p, maxField) = DoubleGauss();
        int stop = system.StopSurfaceIndex;
        var aimer = StopAimer.ForSystem(system, n, p);
        double field = fieldFraction * maxField;

        var launch = aimer.Launch(field, stopY, stopX);
        Assert.True(launch.HasValue, "no ray found");
        var (py, pz) = launch!.Value;

        var hits = RealRayTrace.TraceRecord(system, n, p, field, py, pz, atParaxialFocus: false);
        Assert.True(hits[system.Surfaces.Count - 1].Ok, "the aimed ray does not reach the image");
        double h = aimer.StopHeight;
        Assert.True(Math.Abs(hits[stop].Y - stopY * h) <= 1e-9 * Math.Abs(h) * 1.0001,
            $"meridional {hits[stop].Y - stopY * h:E3}");
        Assert.True(Math.Abs(hits[stop].X - stopX * h) <= 1e-9 * Math.Abs(h) * 1.0001,
            $"sagittal {hits[stop].X - stopX * h:E3}");
    }

    /// <summary>
    /// With the stop's radius taken from the real axial marginal ray, aiming on axis changes
    /// nothing: the ray aimed at the stop's rim is the one launched at the rim of the paraxial
    /// entrance pupil. (Zemax OpticStudio's real ray aiming behaves so; its stop radius on the
    /// double Gauss is 5.1467147 mm, against the paraxial 5.1507.)
    /// </summary>
    [Fact]
    public void WithTheRealAxialRadiusAnAxialRayIsUnaimed()
    {
        var (system, n, p, _) = DoubleGauss();
        var aimer = StopAimer.ForSystem(system, n, p, stopHeight: StopAimer.RealAxialHeight(system, n, p));
        Assert.Equal(5.1467147, aimer.StopHeight, 6);
        var (py, pz) = aimer.Launch(0.0, 1.0, 0.0)!.Value;
        Assert.Equal(1.0, py, 8);
        Assert.Equal(0.0, pz, 12);
        Assert.NotEqual(aimer.StopHeight, StopAimer.ForSystem(system, n, p).StopHeight, 4);
    }

    /// <summary>
    /// At full field the double Gauss's chief ray, launched through the centre of the paraxial
    /// entrance pupil, misses the centre of its stop - which is what makes aiming worth having.
    /// The aimer's chief ray starts elsewhere.
    /// </summary>
    [Fact]
    public void TheAimedChiefRayLeavesFromOffTheParaxialPupilCentre()
    {
        var (system, n, p, field) = DoubleGauss();
        var chief = StopAimer.ForSystem(system, n, p).Launch(field, 0.0, 0.0);
        Assert.True(chief.HasValue);
        Assert.True(Math.Abs(chief!.Value.Py) > 1e-4, $"launch {chief.Value.Py:E3}");
        Assert.True(Math.Abs(chief.Value.Pz) < 1e-12, "a meridional field has a meridional chief ray");
    }

    /// <summary>
    /// With a flat stop in front of everything the entrance pupil IS the stop, so there are no
    /// pupil aberrations and the launch point is the target itself. The stop has to be FLAT for
    /// that: the launch is given on surface 1's vertex plane, and on a curved stop the oblique
    /// ray meets the surface a sag times the tangent of the field away from it.
    /// </summary>
    [Fact]
    public void WithAFlatStopInFrontTheLaunchIsTheTarget()
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 40.0) };
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Fields.Add(new Field(5.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Thickness = 0.0, IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -0.005, Thickness = -100.0, Material = "MIRROR" });
        sys.Surfaces.Add(new Surface { Index = 3 });
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, 5.0);

        var launch = StopAimer.ForSystem(sys, n, p).Launch(5.0, 0.5, -0.3);
        Assert.True(launch.HasValue);
        Assert.Equal(0.5, launch!.Value.Py, 9);
        Assert.Equal(-0.3, launch.Value.Pz, 9);
    }

    /// <summary>A system with no stop cannot be aimed, and says so rather than guessing.</summary>
    [Fact]
    public void ASystemWithoutAStopIsRefused()
    {
        var (system, n, p, _) = DoubleGauss();
        foreach (var s in system.Surfaces) s.IsStop = false;
        Assert.Throws<ArgumentException>(() => StopAimer.ForSystem(system, n, p));
    }
}
