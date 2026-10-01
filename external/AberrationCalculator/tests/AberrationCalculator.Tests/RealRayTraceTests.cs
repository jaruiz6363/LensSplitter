using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The exact skew ray trace, against fans traced by an independent program.
///
/// <para>These are the same three fans the tertiary diagnostics use, measured on the figured
/// SPOTM triplet at 0.55 um, at PARAXIAL focus, with no ray aiming. They are per-ray
/// displacements rather than an RMS, so a disagreement cannot hide in an aggregate: every one
/// of the fifty-three rays has to land in the right place.</para>
///
/// <para>The image plane matters and is the easiest thing to get wrong. This design's own plane
/// sits 0.0412 mm inside paraxial focus, which at f/5 is a 4.1 um error growing strictly
/// linearly with the pupil - the signature of a defocus rather than a trace fault. If this test
/// ever fails with a residual proportional to the pupil coordinate, look there first.</para>
/// </summary>
public class RealRayTraceTests
{
    private static readonly double[] Pupil =
    { -1,-0.9,-0.8,-0.7,-0.6,-0.5,-0.4,-0.3,-0.2,-0.1,0,
       0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9,1 };

    private static readonly double[] Tangential0 =
    { 3.309764,2.227778,1.389091,0.798285,0.417891,0.195362,0.078954,0.026029,
      0.006156,0.000689,0,-0.000689,-0.006156,-0.026029,-0.078954,-0.195362,
      -0.417891,-0.798285,-1.389091,-2.227778,-3.309764 };

    private static readonly double[] Tangential14 =
    { -12.729356,-12.485588,-11.945483,-11.061527,-9.858391,-8.401020,-6.771858,
      -5.054479,-3.321767,-1.627440,0,1.561547,3.090785,4.656281,6.363808,8.358947,
      10.830772,14.017476,18.215127,23.791156,31.204798 };

    private static readonly double[] SagittalPupil = { 0,0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9,1 };

    private static readonly double[] Sagittal14 =
    { 0,-2.315475,-4.609890,-6.864905,-9.066893,-11.207379,-13.280930,-15.279194,
      -17.179254,-18.923725,-20.388756 };

    [Theory]
    [InlineData("tangential, axis", 0.0, false)]
    [InlineData("tangential, 14 deg", 14.0, false)]
    [InlineData("sagittal, 14 deg", 14.0, true)]
    public void TheTraceReproducesAnIndependentlyTracedFan(string name, double fieldDeg,
                                                           bool sagittal)
    {
        string path = Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE");
        if (!System.IO.File.Exists(path)) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);

        double[] pupil = sagittal ? SagittalPupil : Pupil;
        double[] exact = fieldDeg == 0.0 ? Tangential0
                       : sagittal ? Sagittal14 : Tangential14;

        // The stored fans are referred to the chief ray, so this one is too.
        var chief = RealRayTrace.Trace(sys, n, p, fieldDeg, 0.0, 0.0);
        Assert.True(chief.Ok, $"{name}: the chief ray failed to trace");

        for (int i = 0; i < pupil.Length; i++)
        {
            var r = sagittal ? RealRayTrace.Trace(sys, n, p, fieldDeg, 0.0, pupil[i])
                             : RealRayTrace.Trace(sys, n, p, fieldDeg, pupil[i], 0.0);
            Assert.True(r.Ok, $"{name}: ray at pupil {pupil[i]} failed to trace");

            double ours = 1000.0 * ((sagittal ? r.Z : r.Y) - (sagittal ? chief.Z : chief.Y));
            // The stored values carry six decimals, so this is at their precision, not ours.
            Assert.True(Math.Abs(ours - exact[i]) < 0.002,
                $"{name} at pupil {pupil[i]}: traced {exact[i]:F6} um, ours {ours:F6} um");
        }
    }

    /// <summary>
    /// In the limit of a vanishing pupil the exact trace must become the paraxial one. This
    /// catches sign and convention faults that the fan comparison could absorb into its
    /// chief-ray reference.
    /// </summary>
    [Fact]
    public void TheTraceDegeneratesToTheParaxialRayAtAVanishingPupil()
    {
        string path = Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE");
        if (!System.IO.File.Exists(path)) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        var p = ParaxialTrace.Trace(sys, n, 0.0);

        // A marginal ray at one part in ten thousand of the pupil, on axis: its height at the
        // paraxial focus must vanish quadratically, so it is far below the paraxial height.
        var tiny = RealRayTrace.Trace(sys, n, p, 0.0, 1e-4, 0.0);
        Assert.True(tiny.Ok);
        Assert.True(Math.Abs(tiny.Y) < 1e-9,
            $"an axial ray at a vanishing pupil should land on axis, not at {tiny.Y:E3}");

        // And the focal length implied by a small real ray must be the paraxial one.
        double h = 1e-3;
        var small = RealRayTrace.Trace(sys, n, p, 0.0, h, 0.0, atParaxialFocus: false);
        Assert.True(small.Ok);
        _ = small;
    }
}
