using System;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A parabolic mirror, which is the one aspheric system whose answer is known exactly without
/// consulting anybody.
///
/// <para>A paraboloid images a point at infinity onto its focus with no aberration at all on
/// axis - not to some order, exactly. So its spherical aberration must vanish at every order:
/// the surface's intrinsic contribution and its figuring contribution have to cancel term for
/// term. Nothing here is taken from a table, a macro or another program.</para>
///
/// <para><b>Why this test exists.</b> Buchdahl's (77.2), read at magnification, gives the two
/// c1^2 terms of gamma3 as theta1*c1^2 and (1/2)c1^2. This program carries half of each, in
/// both its fifth-order code and its tertiary cubics. Every other term of gamma3 matches the
/// printed equation exactly, so the discrepancy looked like a transcription error waiting to
/// be fixed - and on a lens with weak figuring it is worth only a fraction of a per cent,
/// which is not enough to tell the two apart.</para>
///
/// <para>On a parabola it is worth everything, because c1 = -c0^3 is the only figuring
/// present and c1^2 is then the leading term. With the halved values the intrinsic and
/// aspheric sevenths cancel to zero. With the printed values they do not, and the mirror is
/// left with a seventh-order spherical aberration it cannot physically have. So the halved
/// form is right and the reading of the page is wrong, whether the error is in the reading or
/// in the printing.</para>
/// </summary>
public class ParabolicMirrorTests
{
    /// <summary>R = -200 with conic -1, stop at the mirror, focus 100 in front of it.</summary>
    private static OpticalSystem Paraboloid(double conic)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 40.0) };
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Fields.Add(new Field(0.5));

        void Add(double curvature, double thickness, string? glass = null,
                 bool stop = false, double k = 0.0)
            => sys.Surfaces.Add(new Surface
            {
                Index = sys.Surfaces.Count,
                Curvature = curvature,
                Thickness = thickness,
                Material = glass,
                IsStop = stop,
                Conic = k,
            });

        Add(0.0, double.PositiveInfinity);
        Add(-0.005, -100.0, "MIRROR", stop: true, k: conic);
        Add(0.0, 0.0);
        return sys;
    }

    private static BuchdahlResult Coefficients(double conic)
    {
        var sys = Paraboloid(conic);
        var catalog = CatalogLocator.LoadBundled();
        var n = IndexResolver.Build(sys, catalog, 0.55);

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        return BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, field));
    }

    /// <summary>
    /// Third, fifth and seventh order spherical aberration all vanish. Each is compared with
    /// the size of the intrinsic term it had to cancel, so this measures cancellation rather
    /// than smallness - a sphere of the same radius has an intrinsic seventh of 1.85e-5.
    /// </summary>
    [Fact]
    public void AParabolaHasNoSphericalAberrationAtAnyOrder()
    {
        var r = Coefficients(-1.0);
        double scale = Math.Abs(r.Intrinsic[1].B7);

        Assert.True(scale > 1e-6, $"nothing to cancel: intrinsic B7 = {scale:E3}");
        Assert.True(Math.Abs(r.Totals.B) < 1e-12, $"third order {r.Totals.B:E3}");
        Assert.True(Math.Abs(r.Totals.B5) < 1e-12, $"fifth order {r.Totals.B5:E3}");
        Assert.True(Math.Abs(r.Intrinsic[1].B7 + r.Aspheric[1]!.B7) / scale < 1e-12,
            $"seventh order: intrinsic {r.Intrinsic[1].B7:E6}, "
          + $"aspheric {r.Aspheric[1]!.B7:E6}");
    }

    /// <summary>
    /// And it is the parabola specifically. A sphere, an ellipsoid and a hyperboloid of the
    /// same radius all keep a seventh-order term; only conic -1 cancels. Without this the
    /// test above could be passed by any expression that happened to vanish.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(-2.0)]
    [InlineData(0.5)]
    public void NoOtherConicCancels(double conic)
    {
        var r = Coefficients(conic);
        var a = r.Aspheric[1];
        double left = r.Intrinsic[1].B7 + (a == null ? 0.0 : a.B7);
        Assert.True(Math.Abs(left) / Math.Abs(r.Intrinsic[1].B7) > 1e-3,
            $"conic {conic}: seventh order cancelled to {left:E3}");
    }

    /// <summary>
    /// The seventh order of a mirror: finite, and what real rays say it is, in the same frame as
    /// the third and fifth.
    ///
    /// <para>tau2..tau20 used to come out NaN here. <see cref="TertiaryCoefficients.Attach"/>
    /// handed the scheme the plain indices, so the mirror was a curved surface with no index
    /// step and no power, while the paraxial data it was scaled by said f = 100. Given the
    /// signed ones - a reflection carried as a refraction into -n, as the paraxial trace carries
    /// it - the scheme is finite; and with the image index taken as |N'| in the length factor, as
    /// the fifth-order code already takes it for the F/number, it is in the same frame as the
    /// orders below it rather than negated.</para>
    ///
    /// <para>The reference is real rays through the mirror, which reflect since RealRayTrace
    /// learned to. At ten degrees every coefficient stands well clear of the ray inversion's
    /// floor of about 1E-9; at the design's own half degree the field-dependent ones do not, and
    /// the comparison there measures the floor rather than the scheme. All nineteen agree to
    /// 6E-6 of the largest. tau13..tau20 are zero from the scheme, and the rays return the floor
    /// for them.</para>
    /// </summary>
    [Fact]
    public void TheSeventhOrderOfAMirrorIsFiniteAndAgreesWithRealRays()
    {
        const double field = 10.0;
        var sys = Paraboloid(-1.0);
        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);

        var rays = CoefficientInversion.Invert(sys, n, p, field);
        Assert.NotNull(rays);

        double largest = 0.0;
        for (int k = 2; k <= 20; k++)
        {
            double tau = b.Totals["Tau" + k];
            Assert.False(double.IsNaN(tau), $"tau{k} is NaN");
            largest = Math.Max(largest, Math.Abs(tau));
        }
        for (int k = 2; k <= 20; k++)
            Assert.True(Math.Abs(b.Totals["Tau" + k] - rays!.Tau[k]) < 5e-5 * largest,
                $"tau{k}: scheme {b.Totals["Tau" + k]:E6}, rays {rays.Tau[k]:E6}");

        // The third and fifth order from the same rays, so the three orders are seen to be in
        // one frame - which Prms, multiplying them together, needs.
        var lower = CoefficientInversion.InvertThirdAndFifth(sys, n, p, field)!;
        foreach (var names in new[] { CoefficientInversion.ThirdOrderNames, CoefficientInversion.FifthOrderNames })
        {
            double big = 0.0;
            foreach (var name in names) big = Math.Max(big, Math.Abs(b.Totals[name]));
            foreach (var name in names)
                Assert.True(Math.Abs(b.Totals[name] - lower.Terms[name]) < 3e-5 * big,
                    $"{name}: Buchdahl {b.Totals[name]:E6}, rays {lower.Terms[name]:E6}");
        }
    }
}
