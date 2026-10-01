using System;
using System.IO;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Reading Optiland's JSON.
///
/// <para>The reader existed but nothing exercised it, which for an import path is the same as
/// not knowing whether it works. A format reader has one job - put the prescription in the
/// program's own terms - and the way to test that is to read the SAME lens twice, once from
/// Optiland and once from a format already trusted, and require the optics to agree. A
/// mistake in a reader shows up as a different focal length or a different Seidel sum, not as
/// an exception.</para>
/// </summary>
public class OptilandReaderTests
{
    private static void Near(double expected, double actual, string what)
        => Assert.True(Math.Abs(actual - expected) <= 1e-9 * Math.Abs(expected),
                       $"{what}: expected {expected:G6}, got {actual:G6}");

    private static string Json(string name) =>
        Path.Combine(Fixtures.LensDir, name + ".optiland.json");

    /// <summary>
    /// The Cooke triplet, read from Optiland JSON and from the native format, must be the
    /// same lens. Focal length first, because a slip in the thickness chain or a radius sign
    /// moves it immediately; then the Seidel sums, which are sensitive to every surface.
    ///
    /// <para><b>The catalog has to be named by the caller.</b> Optiland's JSON carries a glass
    /// NAME and nothing to say whose glass it is, so "F2" resolves against whatever catalogs
    /// are loaded - and picking a different vendor's F2 moves this triplet's focal length from
    /// 50.000 to 49.063, two per cent, with no error anywhere. That is a property of the
    /// format rather than a fault in the reader, but it is a trap, so it is pinned here: the
    /// comparison only means something once both systems are told to use SCHOTT.</para>
    /// </summary>
    [Fact]
    public void TheCookeTripletReadsTheSameAsTheNativeFile()
    {
        var catalog = CatalogLocator.LoadBundled();
        var fromJson = LensFile.Read(Json("CookeTriplet"), catalog);
        var native = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        fromJson.GlassCatalogs.Add("SCHOTT");

        Assert.Equal(native.Surfaces.Count, fromJson.Surfaces.Count);

        var nj = IndexResolver.Build(fromJson, catalog, 0.55);
        var nn = IndexResolver.Build(native, catalog, 0.55);
        var pj = ParaxialTrace.Trace(fromJson, nj, 20.0);
        var pn = ParaxialTrace.Trace(native, nn, 20.0);

        Assert.True(Math.Abs(pj.Efl - pn.Efl) / Math.Abs(pn.Efl) < 1e-6,
            $"focal length: Optiland {pj.Efl:F6}, native {pn.Efl:F6}");

        var sj = SeidelCoefficients.Compute(fromJson, nj, nj, nj, pj);
        var sn = SeidelCoefficients.Compute(native, nn, nn, nn, pn);

        static double Sum(double[] v) { double a = 0; foreach (var x in v) a += x; return a; }
        void Same(string what, double[] a, double[] b)
            => Assert.True(Math.Abs(Sum(a) - Sum(b)) <= 1e-6 * Math.Max(Math.Abs(Sum(b)), 1e-12),
                           $"{what}: Optiland {Sum(a):G10}, native {Sum(b):G10}");

        Same("spherical", sj.S1, sn.S1);
        Same("coma", sj.S2, sn.S2);
        Same("astigmatism", sj.S3, sn.S3);
        Same("Petzval", sj.S4, sn.S4);
        Same("distortion", sj.S5, sn.S5);
    }

    /// <summary>
    /// The stop, the glasses and the aperture have to survive the trip, because each of them
    /// changes the answer somewhere and none of them would throw if it were dropped.
    /// </summary>
    [Fact]
    public void TheStopGlassesAndApertureSurvive()
    {
        var sys = LensFile.Read(Json("CookeTriplet"), CatalogLocator.LoadBundled());

        Assert.Equal(4, sys.StopSurfaceIndex);
        Assert.Equal(ApertureType.EPD, sys.Aperture.Type);
        Assert.Equal(10.0, sys.Aperture.Value, 9);
        Assert.Equal("SK16", sys.Surfaces[1].Material);
        Assert.Equal("F2", sys.Surfaces[3].Material);
        Assert.Equal("SK16", sys.Surfaces[5].Material);
        Assert.True(double.IsPositiveInfinity(sys.Surfaces[0].Thickness),
            "the object surface should be at infinity");
        Assert.Equal(2, sys.Fields.Count);
        Assert.Equal(20.0, sys.Fields[1].Y, 9);
    }

    /// <summary>
    /// An Optiland even asphere lands in the right slots.
    ///
    /// <para>This is the one that matters, and it is easy to get wrong in a way nothing else
    /// notices. Optiland's <c>coefficients</c> array starts at r^2, as this program's
    /// <c>AsphericCoefficients</c> does, so it copies across position for position. MEASURED
    /// in Optiland 0.6.2: an even asphere built there with [1e-7, 2e-11, 3e-15] has the sag of
    /// 1e-7 r^2 + 2e-11 r^4 + 3e-15 r^6. This reader once started them at r^4, on the strength
    /// of a hand-written fixture Optiland itself could not load; the fixture is now a file
    /// Optiland wrote, with coefficients [0, 1e-7, 2e-11, 3e-15].</para>
    /// </summary>
    [Fact]
    public void AnEvenAsphereLandsInTheRightCoefficientSlots()
    {
        var sys = LensFile.Read(Json("AsphericSinglet"), CatalogLocator.LoadBundled());
        var s = sys.Surfaces[1];

        Assert.Equal(SurfaceType.EvenAsphere, s.Type);
        Assert.Equal(-0.5, s.Conic, 12);

        Assert.True(Math.Abs(s.AsphericCoefficients[0]) < 1e-30,
            $"the r^2 slot should be empty, holds {s.AsphericCoefficients[0]:G6} - the file's "
          + "first coefficient, the r^2 one, is zero");
        Near(1.0e-7,  s.AsphericCoefficients[1], "A4, the r^4 term");
        Near(2.0e-11, s.AsphericCoefficients[2], "A6");
        Near(3.0e-15, s.AsphericCoefficients[3], "A8");
    }

    /// <summary>
    /// And the figuring has to reach the coefficients. If A4 were dropped into the ignored
    /// slot the seventh-order spherical would come out at its unfigured value, so this is the
    /// same check again by consequence rather than by inspection.
    /// </summary>
    [Fact]
    public void TheAsphericFiguringReachesTheAberrationCoefficients()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Json("AsphericSinglet"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55);
        var b = BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, 0.5));

        Assert.NotNull(b.Aspheric[1]);
        Assert.True(Math.Abs(b.Aspheric[1]!.B) > 1e-12,
            "the figuring should contribute to third-order spherical");
        Assert.True(Math.Abs(b.Aspheric[1]!.B7) > 1e-18,
            "the figuring should contribute to seventh-order spherical");
    }
}
