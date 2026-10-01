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
/// Zernike overlays above trefoil - stage 4c - from Fuerschbach, Rolland and Thompson,
/// <i>Opt. Express</i> <b>22</b>, 26585 (2014), Eqs. (42) and (52) and Tables 3 and 4.
///
/// <para>Oblique spherical <c>Z12/13</c> lands on five VECTOR SQUARE moments, all added;
/// fifth-order aperture coma <c>Z14/15</c> lands on seven FIRST moments, all subtracted. In both
/// cases only the first row survives at the stop, the rest carrying powers of the beam walk.</para>
/// </summary>
public class HigherOverlayTests
{
    private const int Stop = 4;            // the Cooke triplet's stop
    private const int OffStop = 1;

    private static NatFifthOrder Field(int surface, int termA, int termB, double value)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        sys.Surfaces[2].TiltY = 0.15;
        if (surface > 0)
        {
            sys.Surfaces[surface].FringeZernike = new double[19];
            sys.Surfaces[surface].FringeZernike[termA] = value;
        }

        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, f);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        var nat = NatField.Compute(sys, n, p, s);
        var wf = WaveFront.FromSystem(sys, n, p);
        Assert.NotNull(wf);

        int last = sys.LastOpticalSurface();
        var bridge = NormalisationBridge.Fit(
            new double[] { s.TotalS1 / 8, s.TotalS2 / 2, s.TotalS3 / 2, s.TotalS5 / 2 },
            new double[] { wf!.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });
        Assert.True(bridge.IsUsable);

        Vec2[]? oblique = null, coma5 = null;
        if (surface > 0 && termA == 12)
        {
            oblique = new Vec2[last + 1];
            oblique[surface] = bridge.SeidelToW(2, 4) * Conventions.ObliqueSphericalOverlay(
                value, 0.0, n[surface - 1], n[surface]);
        }
        if (surface > 0 && termA == 14)
        {
            coma5 = new Vec2[last + 1];
            coma5[surface] = bridge.SeidelToW(1, 5) * Conventions.FifthOrderComaOverlay(
                value, 0.0, n[surface - 1], n[surface]);
        }

        return NatFifthOrder.Compute(
            j => wf.PerSurface[j], j => wf.PerSurface[j].W131, j => nat.Sigmas.Sigma[j], last + 1,
            null,
            j => j >= 1 && j <= last ? Conventions.BeamDisplacement(p.Y[j], p.Ybar[j]) : 0.0,
            oblique == null ? null : j => oblique[j],
            coma5 == null ? null : j => coma5[j]);
    }

    private static double Moved(Vec2 a, Vec2 b) => (a - b).Magnitude;

    /// <summary>
    /// <c>|n' - n|</c> at the stop over the same at surface one. An overlay's magnitude is
    /// proportional to the index step, so the field-constant row scales with this and not with
    /// anything about where the surface sits.
    /// </summary>
    private static double IndexStepRatio()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        return Math.Abs(n[Stop] - n[Stop - 1]) / Math.Abs(n[OffStop] - n[OffStop - 1]);
    }

    /// <summary>
    /// The two overlay vectors: Eq. (42) is <c>8(n' - n)|z|</c> at TWICE the orientation, Eq. (52)
    /// is <c>10(n' - n)|z|</c> at the orientation itself.
    ///
    /// <para>The coefficients run 2, 3, 4, 8, 10 across the five overlays, and each comes from
    /// its Zernike's radial form rather than from a pattern - there is no rule to guess.</para>
    /// </summary>
    [Fact]
    public void TheOverlayVectorsCarryTheirOwnCoefficientsAndSymmetries()
    {
        const double z = 0.0003, nb = 1.0, na = 1.6;

        var obl = Conventions.ObliqueSphericalOverlay(z, 0.0, nb, na);
        Assert.Equal(8.0 * (na - nb) * z, (double)obl.Magnitude, 12);
        Assert.Equal(2.0 * (double)Conventions.NatObliqueSpherical(z, 0.0),
                     (double)obl.Orientation, 12);

        var coma5 = Conventions.FifthOrderComaOverlay(z, 0.0, nb, na);
        Assert.Equal(10.0 * (na - nb) * z, (double)coma5.Magnitude, 12);
        Assert.Equal((double)Conventions.NatFifthOrderComa(z, 0.0),
                     (double)coma5.Orientation, 12);
    }

    /// <summary>
    /// <b>Oblique spherical at the stop moves one moment; away from the stop it moves five.</b>
    /// Table 3's first row is field constant and the other four carry powers of the beam walk,
    /// which is zero at a pupil.
    /// </summary>
    [Fact]
    public void ObliqueSphericalSpreadsOnlyWhenItIsAwayFromTheStop()
    {
        var baseline = Field(0, 0, 0, 0);
        var atStop = Field(Stop, 12, 13, 0.0003);
        var away = Field(OffStop, 12, 13, 0.0003);

        // The field-constant row acts wherever the plate sits. It is NOT the same size at both,
        // because the overlay is 8(n' - n)z and the index step differs between surfaces - the
        // stop here is a glass-to-air surface and surface 1 an air-to-glass one. What must hold
        // is that the two moves are in the ratio of those steps.
        double stopFirst = Moved(atStop.M242.B2, baseline.M242.B2);
        double awayFirst = Moved(away.M242.B2, baseline.M242.B2);
        Assert.True(stopFirst > 1e-6, $"the plate at the stop did nothing: {stopFirst}");
        Assert.Equal(IndexStepRatio(), stopFirst / awayFirst, 9);

        // The other four are off at the stop and on away from it.
        Assert.True(Moved(atStop.M333.B2, baseline.M333.B2) < 1e-9);
        Assert.True(Moved(atStop.M331M.B2, baseline.M331M.B2) < 1e-9);
        Assert.True(Moved(atStop.M422.B2, baseline.M422.B2) < 1e-9);
        Assert.True(Moved(atStop.M420M.B2, baseline.M420M.B2) < 1e-9);

        Assert.True(Moved(away.M333.B2, baseline.M333.B2) > 1e-6);
        Assert.True(Moved(away.M331M.B2, baseline.M331M.B2) > 1e-6);
        Assert.True(Moved(away.M422.B2, baseline.M422.B2) > 1e-6);
        Assert.True(Moved(away.M420M.B2, baseline.M420M.B2) > 1e-6);
    }

    /// <summary>
    /// <b>Fifth-order aperture coma at the stop moves one moment; away from the stop it moves
    /// seven.</b> Table 4, and it lands on the FIRST moments rather than the squares.
    /// </summary>
    [Fact]
    public void FifthOrderComaSpreadsOnlyWhenItIsAwayFromTheStop()
    {
        var baseline = Field(0, 0, 0, 0);
        var atStop = Field(Stop, 14, 15, 0.0002);
        var away = Field(OffStop, 14, 15, 0.0002);

        double stopFirst = Moved(atStop.M151.A, baseline.M151.A);
        Assert.True(stopFirst > 1e-6, $"the plate at the stop did nothing: {stopFirst}");
        Assert.Equal(IndexStepRatio(), stopFirst / Moved(away.M151.A, baseline.M151.A), 9);

        foreach (var (atS, aw, bas, name) in new[]
        {
            (atStop.M240M.A, away.M240M.A, baseline.M240M.A, "A240M"),
            (atStop.M242.A, away.M242.A, baseline.M242.A, "A242"),
            (atStop.M333.A, away.M333.A, baseline.M333.A, "A333"),
            (atStop.M331M.A, away.M331M.A, baseline.M331M.A, "A331M"),
            (atStop.M422.A, away.M422.A, baseline.M422.A, "A422"),
            (atStop.M420M.A, away.M420M.A, baseline.M420M.A, "A420M"),
        })
        {
            Assert.True(Moved(atS, bas) < 1e-9, $"{name} moved with the plate at the stop");
            Assert.True(Moved(aw, bas) > 1e-9, $"{name} did not move with the plate off the stop");
        }
    }

    /// <summary>
    /// <b>A raw Fringe Z12 carries astigmatism, and it must not be dropped.</b>
    ///
    /// <para><c>Z12 = 4 rho^4 cos2phi - 3 rho^2 cos2phi</c>, and the quadratic half is exactly
    /// <c>-3 Z5</c>. The sidecar states raw sag, so stating <c>Z12</c> alone must move the
    /// THIRD-order astigmatism too. Fuerschbach avoids the issue by working with an adjusted
    /// Zernike; a program reading raw coefficients cannot.</para>
    /// </summary>
    [Fact]
    public void ARawZ12CarriesAstigmatismIntoTheThirdOrder()
    {
        Assert.Equal(-3.0 * 0.0003,
                     (double)Conventions.AstigmatismCarriedByObliqueSpherical(0.0003), 15);

        var catalog = CatalogLocator.LoadBundled();
        var plain = ThirdOrder(catalog, 0, 0.0);
        var withZ12 = ThirdOrder(catalog, 12, 0.0003);
        Assert.True((withZ12 - plain).Magnitude > 1e-9,
            "a raw Z12 left the third-order astigmatism untouched");

        // And stating the compensating Z5 explicitly cancels it back out, which is the
        // "adjusted Zernike" the paper works with.
        var adjusted = ThirdOrder(catalog, 12, 0.0003, extraTerm: 5, extraValue: 3.0 * 0.0003);
        Assert.True((adjusted - plain).Magnitude < 1e-12,
            "Z12 + 3 Z5 did not reduce to pure oblique spherical");
    }

    /// <summary>And a raw Fringe Z14 carries third-order coma the same way.</summary>
    [Fact]
    public void ARawZ14CarriesComaIntoTheThirdOrder()
    {
        Assert.Equal(-4.0 * 0.0002, (double)Conventions.ComaCarriedByFifthOrderComa(0.0002), 15);

        var catalog = CatalogLocator.LoadBundled();
        var plain = ThirdOrderComa(catalog, 0, 0.0);
        var withZ14 = ThirdOrderComa(catalog, 14, 0.0002);
        Assert.True((withZ14 - plain).Magnitude > 1e-9,
            "a raw Z14 left the third-order coma untouched");
    }

    private static Vec2 ThirdOrder(GlassCatalog catalog, int term, double value,
                                   int extraTerm = 0, double extraValue = 0.0)
        => ThirdOrderField(catalog, term, value, extraTerm, extraValue).B222Squared;

    private static Vec2 ThirdOrderComa(GlassCatalog catalog, int term, double value)
        => ThirdOrderField(catalog, term, value, 0, 0.0).A131;

    private static NatField ThirdOrderField(GlassCatalog catalog, int term, double value,
                                            int extraTerm, double extraValue)
    {
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        if (term > 0)
        {
            sys.Surfaces[OffStop].FringeZernike = new double[19];
            sys.Surfaces[OffStop].FringeZernike[term] = value;
            if (extraTerm > 0) sys.Surfaces[OffStop].FringeZernike[extraTerm] = extraValue;
        }
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double f = 0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(f)) f = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, f);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        return NatField.Compute(sys, n, p, s);
    }
}
