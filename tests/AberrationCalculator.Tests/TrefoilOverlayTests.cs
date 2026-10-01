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
/// Zernike trefoil overlays - stage 4b - from Fuerschbach, Rolland and Thompson,
/// <i>Opt. Express</i> <b>22</b>, 26585 (2014), Eq. (34) and Table 2.
/// </summary>
public class TrefoilOverlayTests
{
    private static (OpticalSystem Sys, double[] N, ParaxialResult P) Load(int trefoilSurface,
                                                                         double z10)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        if (trefoilSurface > 0)
        {
            sys.Surfaces[trefoilSurface].FringeZernike = new double[19];
            sys.Surfaces[trefoilSurface].FringeZernike[10] = z10;
        }
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        return (sys, n, ParaxialTrace.Trace(sys, n, f));
    }

    private static (Vec2[] N333, Vec2[] N422) Nodes(int trefoilSurface, double z10)
    {
        var (sys, n, p) = Load(trefoilSurface, z10);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        var nat = NatField.Compute(sys, n, p, s);
        var wf = WaveFront.FromSystem(sys, n, p);
        Assert.NotNull(wf);

        int last = sys.LastOpticalSurface();
        var bridge = NormalisationBridge.Fit(
            new double[] { s.TotalS1 / 8, s.TotalS2 / 2, s.TotalS3 / 2, s.TotalS5 / 2 },
            new double[] { wf!.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });
        Assert.True(bridge.IsUsable);

        Vec2[]? ff = null;
        if (trefoilSurface > 0)
        {
            ff = new Vec2[last + 1];
            ff[trefoilSurface] = bridge.SeidelToW(3, 3) * Conventions.TrefoilOverlay(
                z10, 0.0, n[trefoilSurface - 1], n[trefoilSurface]);
        }

        var fifth = NatFifthOrder.Compute(
            j => wf.PerSurface[j], j => wf.PerSurface[j].W131, j => nat.Sigmas.Sigma[j], last + 1,
            ff == null ? null : j => ff[j],
            j => j >= 1 && j <= last ? Conventions.BeamDisplacement(p.Y[j], p.Ybar[j]) : 0.0);

        return (fifth.Nodes333, fifth.Nodes422);
    }

    private static double Moved(Vec2[] a, Vec2[] b)
    {
        double worst = 0;
        for (int i = 0; i < a.Length; i++) worst = Math.Max(worst, (a[i] - b[i]).Magnitude);
        return worst;
    }

    /// <summary>
    /// The overlay vector itself: magnitude <c>4(n' - n)|z|</c> at THREE times the trefoil's
    /// orientation, Eq. (34).
    ///
    /// <para>The four is the part worth asserting. Fringe <c>Z10/11</c> is <c>rho^3 cos3phi</c>
    /// as a sag, and <c>cos 3t = 4cos^3 t - 3cos t</c>, so the part landing on <c>W333</c> - a
    /// <c>cos^3</c> aberration - carries four where coma's overlay carries three and
    /// astigmatism's carries two.</para>
    /// </summary>
    [Fact]
    public void TheOverlayIsFourTimesTheIndexStepAtThreeTimesTheOrientation()
    {
        const double z10 = 0.0005, nBefore = 1.0, nAfter = 1.6;
        var v = Conventions.TrefoilOverlay(z10, 0.0, nBefore, nAfter);

        Assert.Equal(4.0 * (nAfter - nBefore) * z10, (double)v.Magnitude, 12);
        Assert.Equal(3.0 * (double)Conventions.NatTrefoil(z10, 0.0), (double)v.Orientation, 12);

        // It scales linearly and reverses with the index step, being an optical path difference.
        var twice = Conventions.TrefoilOverlay(2 * z10, 0.0, nBefore, nAfter);
        Assert.Equal(2.0 * (double)v.Magnitude, (double)twice.Magnitude, 12);
        var flipped = Conventions.TrefoilOverlay(z10, 0.0, nAfter, nBefore);
        Assert.Equal((double)v.Magnitude, (double)flipped.Magnitude, 12);
    }

    /// <summary>
    /// <b>The result Fuerschbach's Schmidt telescope was built to show.</b> A trefoil plate AT the
    /// stop generates field-constant elliptical coma and nothing else; moved AWAY from the stop it
    /// also generates field-linear astigmatism.
    ///
    /// <para>Table 2's second row carries the beam displacement <c>ybar/y</c>, which is zero at a
    /// pupil - there the beam footprint is the same for every field point, so the contribution
    /// cannot acquire a field dependence. On the Cooke triplet the stop is surface 4, where
    /// <c>ybar/y</c> is zero to rounding, and surface 1 has <c>ybar/y = -0.84</c>.</para>
    ///
    /// <para>This is also the check that the second row was wired to the right place: it lands on
    /// fifth-order astigmatism's cubic vector, so it moves the <c>W422</c> nodes and nothing
    /// else. If it had been added to <c>W333</c> twice, or to the wrong moment, the two placements
    /// would be indistinguishable.</para>
    /// </summary>
    [Fact]
    public void ATrefoilPlateGeneratesAstigmatismOnlyWhenItIsAwayFromTheStop()
    {
        const double z10 = 0.0005;
        var baseline = Nodes(0, 0.0);

        var atStop = Nodes(4, z10);            // the Cooke triplet's stop
        double stopTrefoil = Moved(atStop.N333, baseline.N333);
        double stopAstig = Moved(atStop.N422, baseline.N422);

        var awayFromStop = Nodes(1, z10);
        double awayTrefoil = Moved(awayFromStop.N333, baseline.N333);
        double awayAstig = Moved(awayFromStop.N422, baseline.N422);

        // Field-constant: the trefoil nodes move by the same amount wherever the plate sits.
        Assert.True(stopTrefoil > 0.1, $"the plate at the stop moved no trefoil node: {stopTrefoil}");
        Assert.Equal(stopTrefoil, awayTrefoil, 2);

        // Field-linear: the astigmatic nodes move only away from the stop, and by a lot.
        Assert.True(stopAstig < 1e-4, $"a plate AT the stop moved the astigmatic nodes: {stopAstig}");
        Assert.True(awayAstig > 0.1, $"a plate away from the stop did not: {awayAstig}");
    }

    /// <summary>A system with no trefoil anywhere is left exactly as it was.</summary>
    [Fact]
    public void NoOverlayChangesNothing()
    {
        var a = Nodes(0, 0.0);
        var b = Nodes(0, 0.0);
        Assert.Equal(0.0, Moved(a.N333, b.N333), 12);
        Assert.Equal(0.0, Moved(a.N422, b.N422), 12);
    }

    /// <summary>
    /// The scale between the two routes is exact, and it says so itself.
    ///
    /// <para>Four third-order coefficients give four ratios and there are two unknowns, so
    /// <c>W222</c> and <c>W311</c> are free checks on a fit made from <c>W040</c> and
    /// <c>W131</c>. They pass to MACHINE PRECISION on every fixture, which is what makes the
    /// scaling safe to apply to an overlay: the relation <c>A^l F^k</c> is not a convenient
    /// approximation, it is the transformation.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("Ladder1_Sphere")]
    public void TheNormalisationBridgeIsExact(string lens)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(lens), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, f);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        var wf = WaveFront.FromSystem(sys, n, p);
        Assert.NotNull(wf);

        var bridge = NormalisationBridge.Fit(
            new double[] { s.TotalS1 / 8, s.TotalS2 / 2, s.TotalS3 / 2, s.TotalS5 / 2 },
            new double[] { wf!.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });

        Assert.True(bridge.IsUsable);
        Assert.True(bridge.Residual < 1e-10,
            $"{lens}: the two free checks disagree by {bridge.Residual}");
    }

    /// <summary>
    /// And when it cannot be fitted it refuses, rather than returning a factor of one and
    /// quietly scaling an overlay by the wrong amount.
    /// </summary>
    [Fact]
    public void TheBridgeRefusesWhenItCannotBeFitted()
    {
        // A coefficient too small to carry information disqualifies the fit.
        var bad = NormalisationBridge.Fit(new double[] { 0.0, 1.0, 1.0, 1.0 },
                                          new double[] { 1.0, 1.0, 1.0, 1.0 });
        Assert.False(bad.IsUsable);

        // So does a set that simply is not of the form A^l F^k.
        var inconsistent = NormalisationBridge.Fit(new double[] { 1.0, 1.0, 1.0, 1.0 },
                                                   new double[] { 16.0, 8.0, 4.0, 99.0 });
        Assert.False(inconsistent.IsUsable);
    }

    /// <summary>
    /// <b>The two routes agree on the medial surface, and pi3 is the sagittal one.</b>
    ///
    /// <para>Buchdahl's <c>pi3</c> - the plain <c>rho^2 H^2</c> coefficient of Eq. (2.8) -
    /// converts into exactly what <c>WaveCoefficients.Third</c> calls <c>W220M</c>, namely
    /// <c>W220P + W222/2</c>. Thompson's MEDIAL coefficient, the one his 2011 Sec. 2 relations
    /// are written in, is that plus another <c>W222/2</c>. The difference is half the
    /// astigmatism: a plausible error, not an obvious one, and it was caught only because the
    /// report printed both side by side under one name.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("Ladder2_Sphere")]
    public void BuchdahlsPi3IsTheSagittalSurfaceAndTheMedialsAgree(string lens)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(lens), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, f);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        var wf = WaveFront.FromSystem(sys, n, p);
        Assert.NotNull(wf);

        var bridge = NormalisationBridge.Fit(
            new double[] { s.TotalS1 / 8, s.TotalS2 / 2, s.TotalS3 / 2, s.TotalS5 / 2 },
            new double[] { wf!.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });
        Assert.True(bridge.IsUsable);

        double conv = bridge.WToSeidel(2, 2);
        var w = WaveCoefficients.OfSystem(s);

        // pi3 alone is the SAGITTAL surface ...
        Assert.Equal(w.W220S, (double)(wf.System.Pi3 * conv), 8);

        // ... and both routes now agree on the MEDIAL one, which is what the round trip in
        // the report checks. Before the fix these differed by half the astigmatism.
        Assert.Equal(w.W220M, (double)(wf.System.W220M * conv), 8);
    }
}
