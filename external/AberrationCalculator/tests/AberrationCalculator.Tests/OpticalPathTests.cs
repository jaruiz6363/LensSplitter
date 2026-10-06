using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The optical path the real ray trace accumulates.
///
/// <para>Each check is a case whose answer is known without consulting anything: a perfect
/// imager has equal optical paths from the object to the image along every ray, which is
/// Fermat's principle and the definition of a perfect image, and a plate in a parallel beam has
/// a path that follows from Snell's law in one line. Nothing here is taken from another
/// program.</para>
/// </summary>
public class OpticalPathTests
{
    private static OpticalSystem Mirror(double objectDistance, double conic)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 40.0) };
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = objectDistance });
        sys.Surfaces.Add(new Surface
        {
            Index = 1, Curvature = -0.005, Thickness = -100.0, Material = "MIRROR",
            IsStop = true, Conic = conic,
        });
        sys.Surfaces.Add(new Surface { Index = 2 });
        return sys;
    }

    private static (double[] Path, RealRayTrace.SurfaceHit[] Hits) Trace(
        OpticalSystem sys, double[] n, ParaxialResult p, double field, double py, double pz)
    {
        var path = new double[sys.Surfaces.Count];
        var hits = RealRayTrace.TraceRecord(sys, n, p, field, py, pz, atParaxialFocus: true,
                                            null, null, path);
        return (path, hits);
    }

    /// <summary>
    /// A paraboloid focuses a collimated axial beam perfectly, so every ray's optical path from
    /// a plane across the beam to the focus is the same. The launch points all lie on surface
    /// 1's vertex plane, which on axis IS such a plane, so the paths must agree outright - and
    /// the central ray, which goes to the vertex and back to the focus 100 in front of it, must
    /// have exactly 100.
    /// </summary>
    [Fact]
    public void EveryRayToTheFocusOfAParaboloidHasTheSameOpticalPath()
    {
        var sys = Mirror(double.PositiveInfinity, -1.0);
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        int image = sys.Surfaces.Count - 1;

        var (centre, _) = Trace(sys, n, p, 0.0, 0.0, 0.0);
        Assert.Equal(100.0, centre[image], 10);

        foreach (var (py, pz) in new[] { (1.0, 0.0), (0.0, 1.0), (0.7, -0.7), (-0.4, 0.2) })
        {
            var (path, hits) = Trace(sys, n, p, 0.0, py, pz);
            Assert.True(hits[image].Ok);
            Assert.True(Math.Abs(path[image] - centre[image]) < 1e-10,
                $"({py}, {pz}): {path[image] - centre[image]:E3} from the central ray");
        }
    }

    /// <summary>
    /// A sphere images its own centre of curvature onto itself perfectly, at any aperture. The
    /// rays leave the object point rather than a plane, so the stretch from the object to each
    /// one's launch point on surface 1's vertex plane is added before comparing them.
    /// </summary>
    [Fact]
    public void EveryRayFromTheCentreOfASphereBackToItHasTheSameOpticalPath()
    {
        var sys = Mirror(200.0, 0.0);
        sys.Surfaces[1].Thickness = -200.0;
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        int image = sys.Surfaces.Count - 1;

        double Total(double py, double pz)
        {
            var (x, y, _, _, _) = RealRayTrace.LaunchRay(sys, p, 0.0, py, pz);
            var (path, hits) = Trace(sys, n, p, 0.0, py, pz);
            Assert.True(hits[image].Ok);
            return Math.Sqrt(x * x + y * y + 200.0 * 200.0) + path[image];
        }

        double centre = Total(0.0, 0.0);
        Assert.Equal(400.0, centre, 10);
        foreach (var (py, pz) in new[] { (1.0, 0.0), (0.0, 1.0), (0.6, 0.8), (-0.3, 0.5) })
            Assert.True(Math.Abs(Total(py, pz) - centre) < 1e-10, $"({py}, {pz})");
    }

    /// <summary>
    /// Inside a plate the path is the index times the length: a ray refracted to the angle
    /// theta' crosses a plate of thickness T in T / cos(theta') and so accumulates
    /// n T / cos(theta'). This is the check that the right index weights each segment - a
    /// parallel beam would show equal paths across the pupil whatever index were used.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(10.0)]
    [InlineData(25.0)]
    public void APlateAddsItsIndexTimesTheLengthInside(double fieldDeg)
    {
        const double thickness = 12.0;
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        sys.Wavelengths.Add(new Wavelength(0.5876, 1.0, true));
        sys.Fields.Add(new Field(fieldDeg));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Thickness = thickness, Material = "N-BK7", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Thickness = 20.0 });
        sys.Surfaces.Add(new Surface { Index = 3 });
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.5876);
        var p = ParaxialTrace.Trace(sys, n, fieldDeg);

        var path = new double[sys.Surfaces.Count];
        RealRayTrace.TraceRecord(sys, n, p, fieldDeg, 0.6, 0.3, atParaxialFocus: false, null, null, path);

        double sinInside = Math.Sin(fieldDeg * Math.PI / 180.0) / n[1];
        double expected = n[1] * thickness / Math.Sqrt(1.0 - sinInside * sinInside);
        Assert.Equal(expected, path[2] - path[1], 10);
    }

    /// <summary>The optical path is optional: asking for it changes nothing about the rays.</summary>
    [Fact]
    public void AskingForTheOpticalPathLeavesTheTraceUnchanged()
    {
        var sys = Mirror(double.PositiveInfinity, -0.5);
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, 0.0);

        var without = RealRayTrace.TraceRecord(sys, n, p, 0.0, 0.8, 0.1);
        var (_, with) = Trace(sys, n, p, 0.0, 0.8, 0.1);
        Assert.Equal(without, with);
    }
    /// <summary>
    /// A virtual entrance pupil BEHIND the object: a positive lens with its stop beyond its focal
    /// length images the stop back past an object close in front of it. The light still leaves
    /// the object towards the lens, so every ray's optical path from the object is positive - the
    /// axial ray's is the sum of index times thickness from surface 1 - and rays above the axis in the pupil go
    /// up. (A launch that went from the object towards that pupil traced each ray backwards and
    /// negated every path.)
    /// </summary>
    [Fact]
    public void AnEntrancePupilBehindTheObjectStillSendsTheLightForward()
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.ObjectSpaceNA, 0.1) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = 20.0 });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 50.0, Thickness = 5.0, Material = "N-BK7" });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 50.0, Thickness = 60.0 });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 40.0, IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 4 });
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.5875618);
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        Assert.True(p.EntrancePupilPosition < -20.0, $"the entrance pupil is at {p.EntrancePupilPosition}, not behind the object");

        var path = new double[sys.Surfaces.Count];
        var axial = RealRayTrace.TraceRecord(sys, n, p, 0.0, 0.0, 0.0, atParaxialFocus: false, null, null, path);
        Assert.True(axial[4].Ok);
        // The trace's path starts on surface 1's vertex plane, which the axial ray meets at the vertex.
        Assert.Equal(n[1] * 5.0 + 60.0 + 40.0, path[4], 9);

        var upper = RealRayTrace.TraceRecord(sys, n, p, 0.0, 1.0, 0.0, atParaxialFocus: false);
        Assert.True(upper[1].Ok);
        Assert.True(upper[1].Y > 0.0, $"the upper rim ray meets the lens at y = {upper[1].Y}");
        Assert.True(upper[1].N > 0.0);
    }
}
