using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.Nat;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The two ways a sigma vector can fail to exist, which are different conditions at different
/// surfaces and were once reported through one list.
///
/// <list type="bullet">
///   <item><c>sigma</c> needs the chief-ray INCIDENCE, and fails where the chief ray strikes a
///   surface normally.</item>
///   <item><c>sigma_aspheric</c> needs the chief-ray HEIGHT, and fails at a pupil - but only a
///   figured surface has anything there to centre.</item>
/// </list>
///
/// <para>Conflating them meant an ordinary surface at the stop was reported as having a diverging
/// sigma and had the medial vertex refused on its account, while its sigma was perfectly good.</para>
/// </summary>
public class SigmaSuppressionTests
{
    private static (OpticalSystem Sys, double[] N, ParaxialResult P) Load(bool conicOnStop)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        sys.Surfaces[2].TiltY = 0.15;
        if (conicOnStop) sys.Surfaces[sys.StopSurfaceIndex].Conic = -0.6;

        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        return (sys, n, ParaxialTrace.Trace(sys, n, f));
    }

    /// <summary>
    /// <b>An unfigured surface at the stop is not suppressed.</b> Its chief-ray HEIGHT is zero,
    /// which is what makes an aspheric sigma impossible - but it has no figuring, so there is no
    /// aspheric sigma to want, and its own sigma is an ordinary finite number.
    /// </summary>
    [Fact]
    public void APlainSurfaceAtTheStopKeepsItsSigma()
    {
        var (sys, n, p) = Load(conicOnStop: false);
        var sig = SigmaVector.Compute(sys, n, p);
        int stop = sys.StopSurfaceIndex;

        // The condition that used to trip is genuinely present: the chief ray height is zero.
        Assert.True(Math.Abs((double)p.Ybar[stop]) < 1e-9,
            $"this fixture no longer has its stop where ybar vanishes: {p.Ybar[stop]}");

        // ... and the chief ray INCIDENCE, which is what sigma actually needs, is not.
        Assert.True(Math.Abs((double)sig.ChiefIncidence[stop]) > 1e-3,
            $"chief-ray incidence at the stop: {sig.ChiefIncidence[stop]}");

        Assert.DoesNotContain(stop, sig.SigmaSuppressedAt);
        Assert.True(sig.Sigma[stop].Magnitude > 0.0, "the stop surface has no sigma");
    }

    /// <summary>
    /// And the medial vertex is available, where it used to be refused on that surface's account.
    /// </summary>
    [Fact]
    public void TheMedialVertexIsNotRefusedBecauseASurfaceSitsAtTheStop()
    {
        var (sys, n, p) = Load(conicOnStop: false);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var nat = NatField.Compute(sys, n, p, seidel);

        Assert.True(nat.MedialExists, "the medial vertex was refused");
        Assert.True(nat.MedialVertex.Magnitude > 0.0,
            "a perturbed system put the medial vertex at the field centre");
    }

    /// <summary>
    /// <b>The two routes agree on the medial vertex.</b> The third order reaches it from the
    /// Seidel sums; the fifth-order machinery reaches it through Buchdahl's W coordinates and the
    /// deformation coefficients. Nothing is shared but the sigmas.
    ///
    /// <para>This is what caught the second half of the bug. The weight is <c>W220P + W222</c>,
    /// the medial surface, and <see cref="NatField"/> was forming it inline as
    /// <c>S4/4 + S3/4</c> - the SAGITTAL one - which survived the fix to
    /// <c>WaveCoefficients.Third.W220M</c> because it never went through that property.</para>
    /// </summary>
    [Fact]
    public void BothRoutesPutTheMedialVertexInTheSamePlace()
    {
        var (sys, n, p) = Load(conicOnStop: false);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var nat = NatField.Compute(sys, n, p, seidel);
        var wf = WaveFront.FromSystem(sys, n, p);
        Assert.NotNull(wf);
        Assert.True(nat.MedialExists);

        int last = sys.LastOpticalSurface();
        var fifth = NatFifthOrder.Compute(j => wf!.PerSurface[j], j => wf.PerSurface[j].W131,
                                          j => nat.Sigmas.Sigma[j], last + 1);

        Assert.Equal((double)nat.MedialVertex.X, (double)fifth.M220M.a.X, 6);
        Assert.Equal((double)nat.MedialVertex.Y, (double)fifth.M220M.a.Y, 6);
    }

    /// <summary>
    /// Put figuring on that same surface and the ASPHERIC sigma really is impossible - there is
    /// now something to centre and no chief-ray height to centre it by - so the aspheric list
    /// fires and the medial vertex is declined. The spherical list stays empty throughout, which
    /// is the distinction the two lists exist to keep.
    /// </summary>
    [Fact]
    public void AFiguredSurfaceAtTheStopSuppressesOnlyTheAsphericSigma()
    {
        var (sys, n, p) = Load(conicOnStop: true);
        var sig = SigmaVector.Compute(sys, n, p);
        int stop = sys.StopSurfaceIndex;

        Assert.Contains(stop, sig.SigmaAsphericSuppressedAt);
        Assert.DoesNotContain(stop, sig.SigmaSuppressedAt);

        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var nat = NatField.Compute(sys, n, p, seidel);
        Assert.False(nat.MedialExists,
            "the aspheric half of the medial weight could not be centred, so it must be declined");
    }

    /// <summary>
    /// On a design with no figuring at all, the aspheric list is empty whatever the geometry -
    /// there is nothing for it to be about.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    [InlineData("Ladder2_Sphere")]
    public void AnUnfiguredDesignHasNoAsphericSuppression(string lens)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(lens), catalog);
        sys.Surfaces[1].TiltY = 0.1;
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, f);

        Assert.Empty(SigmaVector.Compute(sys, n, p).SigmaAsphericSuppressedAt);
    }
}
