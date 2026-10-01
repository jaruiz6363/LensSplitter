using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// An object-space NA is n sin(theta), theta the marginal ray's angle leaving the axial object
/// point, and the entrance pupil it fills has radius z tan(theta), z the distance from the object
/// to the pupil. The paraxial trace took NA/n itself as the slope - sin for tan - and filled a
/// pupil 13 % small at NA 0.5 (0.12 % at 0.05); OSLO and LensHH-LT take tan, and OpticStudio
/// reports an entrance pupil diameter of 230.9401 for the lens below (2026-09-25).
/// </summary>
public class ObjectSpaceNaTests
{
    [Fact]
    public void AnObjectNaFillsThePupilItsMarginalRayReaches()
    {
        // An ideal f = 100 lens at the stop, the object 200 before it, in air.
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.ObjectSpaceNA, 0.5) };
        sys.Surfaces.Add(new Surface { Thickness = 200.0 });
        sys.Surfaces.Add(new Surface { Type = SurfaceType.Paraxial, FocalLength = 100.0, IsStop = true, Thickness = 200.0 });
        sys.Surfaces.Add(new Surface { Thickness = 0.0 });

        var p = ParaxialTrace.Trace(sys, new double[] { 1.0, 1.0, 1.0 }, 0.0);

        Assert.Equal(2.0 * 200.0 * Math.Tan(Math.PI / 6.0), p.Epd, 9);   // 230.94, not 200
    }
}
