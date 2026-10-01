using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Core.Nat;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Tracing real rays through a surface that is tilted or decentred.
///
/// <para>The transform's conventions are ported rather than derived - see
/// <see cref="LocalFrame"/> - so what is checked here is not "does it match a formula I wrote
/// down" but the properties it must have whatever the conventions are: that going in and coming
/// out is the identity, that an unperturbed surface is untouched, and that a whole lens moved
/// bodily still images the way it did.</para>
/// </summary>
public class LocalFrameTests
{
    /// <summary>
    /// <b>The inverse really is the inverse.</b> The forward transform is copied from a tested
    /// source; the inverse is derived here, so this is the one that guards the derivation.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 0.05, 0.0)]
    [InlineData(0.002, 0.0, 0.0, 0.0)]
    [InlineData(0.0, -0.003, 0.0, 0.02)]
    [InlineData(0.01, 0.007, -0.04, 0.06)]
    public void GoingInAndComingOutIsTheIdentity(double tx, double ty, double dcx, double dcy)
    {
        var s = new Surface { TiltX = tx, TiltY = ty, DecenterX = dcx, DecenterY = dcy };

        double x = 1.3, y = -0.7, z = 0.25;
        double dx = 0.1, dy = -0.2, dz = 0.9741;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= len; dy /= len; dz /= len;

        double x0 = x, y0 = y, z0 = z, dx0 = dx, dy0 = dy, dz0 = dz;

        LocalFrame.Into(s, ref x, ref y, ref z, ref dx, ref dy, ref dz);
        LocalFrame.OutOf(s, ref x, ref y, ref z, ref dx, ref dy, ref dz);

        Assert.Equal(x0, x, 12);
        Assert.Equal(y0, y, 12);
        Assert.Equal(z0, z, 12);
        Assert.Equal(dx0, dx, 12);
        Assert.Equal(dy0, dy, 12);
        Assert.Equal(dz0, dz, 12);
    }

    /// <summary>A rotation preserves length, so a unit direction stays one.</summary>
    [Fact]
    public void TheTransformPreservesDirectionLength()
    {
        var s = new Surface { TiltX = 0.03, TiltY = -0.02, DecenterY = 0.1 };
        double x = 0.0, y = 0.0, z = 0.0, dx = 0.2, dy = 0.3, dz = 0.9327;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= len; dy /= len; dz /= len;

        LocalFrame.Into(s, ref x, ref y, ref z, ref dx, ref dy, ref dz);

        Assert.Equal(1.0, Math.Sqrt(dx * dx + dy * dy + dz * dz), 12);
    }

    /// <summary>An unperturbed surface is not transformed at all.</summary>
    [Fact]
    public void AnAlignedSurfaceIsNotPerturbed()
    {
        Assert.False(LocalFrame.IsPerturbed(new Surface()));
        Assert.True(LocalFrame.IsPerturbed(new Surface { TiltX = 1e-9 }));
    }

    /// <summary>
    /// <b>A whole lens moved bodily still images the way it did.</b>
    ///
    /// <para>Decentre every surface by the same amount and the lens is translated - its axis
    /// moves to <c>y = D</c> and nothing else about it changes. So a ray launched along that new
    /// axis must behave exactly as the axial ray of the nominal lens did, and a whole bundle
    /// offset by <c>D</c> must reproduce the nominal bundle offset by <c>D</c>.</para>
    ///
    /// <para>This is the strongest property available without an oracle: it exercises the
    /// transform at every surface, in both directions, and requires the errors to cancel
    /// exactly across six surfaces rather than merely be small. A sign wrong anywhere, or the
    /// inverse applied in the wrong order, and the bundle comes back sheared.</para>
    /// </summary>
    [Fact]
    public void AUniformlyDecentredLensImagesLikeTheNominalOneMovedOver()
    {
        const double d = 0.05;
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var nominal = LensFile.Read(path, catalog);
        var moved = LensFile.Read(path, catalog);
        for (int j = 1; j <= moved.LastOpticalSurface(); j++) moved.Surfaces[j].DecenterY = d;

        var n = IndexResolver.Build(nominal, catalog, 0.55, new List<string>());
        var p = ParaxialTrace.Trace(nominal, n, 0.0);

        double radius = 0.5 * p.Epd;
        foreach (double py in new[] { 0.0, 0.3, -0.5, 0.8 })
        foreach (double px in new[] { 0.0, 0.4, -0.7 })
        {
            var a = RealRayTrace.TraceFrom(nominal, n, p,
                        px * radius, py * radius, 0.0, 0.0, 1.0);
            var b = RealRayTrace.TraceFrom(moved, n, p,
                        px * radius, py * radius + d, 0.0, 0.0, 1.0);

            Assert.True(a.Ok && b.Ok, "both rays must get through");
            Assert.Equal(a.Z, b.Z, 10);              // sagittal: unchanged
            Assert.Equal(a.Y + d, b.Y, 10);          // meridional: moved over by exactly d
        }
    }

    /// <summary>
    /// And the corollary that makes the result sharp: it is the TRANSFORM doing that, not the
    /// perturbation being ignored. The same bundle traced without the offset lands somewhere
    /// else entirely.
    /// </summary>
    [Fact]
    public void ADecentredLensReallyDoesMoveTheRay()
    {
        const double d = 0.05;
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var nominal = LensFile.Read(path, catalog);
        var moved = LensFile.Read(path, catalog);
        for (int j = 1; j <= moved.LastOpticalSurface(); j++) moved.Surfaces[j].DecenterY = d;

        var n = IndexResolver.Build(nominal, catalog, 0.55, new List<string>());
        var p = ParaxialTrace.Trace(nominal, n, 0.0);

        var a = RealRayTrace.TraceFrom(nominal, n, p, 0.0, 0.3 * 0.5 * p.Epd, 0.0, 0.0, 1.0);
        var b = RealRayTrace.TraceFrom(moved, n, p, 0.0, 0.3 * 0.5 * p.Epd, 0.0, 0.0, 1.0);

        Assert.True(a.Ok && b.Ok);
        Assert.True(Math.Abs(a.Y - b.Y) > 1e-6,
                    "a decentred lens that changes nothing is a transform that is not applied");
    }

    /// <summary>
    /// A tilted surface bends a ray that an untilted one passes straight through - the minimum
    /// evidence that the rotation half of the transform reaches the refraction at all.
    /// </summary>
    [Fact]
    public void ATiltedSurfaceDeviatesARayThatWouldOtherwisePassStraight()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var nominal = LensFile.Read(path, catalog);
        var tilted = LensFile.Read(path, catalog);
        tilted.Surfaces[2].TiltX = 0.01;

        var n = IndexResolver.Build(nominal, catalog, 0.55, new List<string>());
        var p = ParaxialTrace.Trace(nominal, n, 0.0);

        var a = RealRayTrace.TraceFrom(nominal, n, p, 0.0, 0.0, 0.0, 0.0, 1.0);
        var b = RealRayTrace.TraceFrom(tilted, n, p, 0.0, 0.0, 0.0, 0.0, 1.0);

        Assert.True(a.Ok && b.Ok);
        Assert.True(Math.Abs(a.Y - b.Y) > 1e-9, "a tilted surface must deviate the axial ray");
    }
}

/// <summary>
/// The aberration field decentre vectors measured from traced rays, against the paraxial
/// perturbation formula they are supposed to agree with.
/// </summary>
public class RealSigmaTests
{
    private static (Core.Models.OpticalSystem Sys, double[] N, ParaxialResult P) Load()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        return (sys, n, ParaxialTrace.Trace(sys, n, field));
    }

    /// <summary>An aligned system displaces nothing, by either route.</summary>
    [Fact]
    public void AnAlignedSystemMeasuresZero()
    {
        var (sys, n, p) = Load();
        var real = Core.Nat.RealSigma.Measure(sys, n, p);

        Assert.True(real.Aimed, "the optical axis ray must reach the stop centre");
        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
            Assert.True(real.Sigma[j].Magnitude < 1e-12, $"surface {j} moved in an aligned system");
    }

    /// <summary>
    /// <b>A rigidly translated lens displaces nothing either, and this is the case where the
    /// answer is known by symmetry.</b>
    ///
    /// <para>Thompson locates the unperturbed field centre BY the optical axis ray. Translate
    /// the whole lens and the optical axis ray translates with it, every centre of curvature
    /// stays on it, and so every surface's field centre is exactly where it was: <c>sigma = 0</c>
    /// identically, not merely small.</para>
    ///
    /// <para><see cref="LocalFrameTests.AUniformlyDecentredLensImagesLikeTheNominalOneMovedOver"/>
    /// establishes the premise independently, by showing the traced bundle really is the nominal
    /// one moved over. This then measures the consequence.</para>
    /// </summary>
    [Fact]
    public void ARigidlyTranslatedLensMeasuresZero()
    {
        var (sys, n, p) = Load();
        for (int j = 1; j <= sys.LastOpticalSurface(); j++) sys.Surfaces[j].DecenterY = 0.05;

        var real = Core.Nat.RealSigma.Measure(sys, n, p);
        Assert.True(real.Aimed);

        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
            Assert.True(real.Sigma[j].Magnitude < 1e-10,
                        $"surface {j} measured {real.Sigma[j]}, but a translated lens displaces nothing");
    }

    /// <summary>
    /// Downstream of a single perturbed surface the optical axis ray is one definite ray
    /// travelling through an aligned system, so it corresponds to one definite field offset -
    /// and every surface after the perturbation must therefore measure the SAME sigma.
    /// </summary>
    [Fact]
    public void SigmaIsConstantDownstreamOfASinglePerturbedSurface()
    {
        var (sys, n, p) = Load();
        sys.Surfaces[2].TiltX = 0.115 * Math.PI / 180.0;

        var real = Core.Nat.RealSigma.Measure(sys, n, p);
        Assert.True(real.Aimed);

        var reference = real.Sigma[4];
        Assert.True(reference.Magnitude > 1e-6, "the tilt must displace something");

        for (int j = 4; j <= sys.LastOpticalSurface(); j++)
            Assert.True((real.Sigma[j] - reference).Magnitude < 1e-5 * reference.Magnitude,
                        $"surface {j} measured {real.Sigma[j]}, not {reference}");
    }
}
