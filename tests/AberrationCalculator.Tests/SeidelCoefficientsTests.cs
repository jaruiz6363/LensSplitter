using System;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The Seidel sums, checked against properties that follow from the physics rather than
/// against another program's output. Agreement with other implementations is recorded in
/// docs/verification.md; matching one implementation proves only that two things agree,
/// which is worth much less than a result that must hold for any correct implementation.
/// </summary>
public class SeidelCoefficientsTests
{
    private const double D = 0.5875618, F = 0.4861327, C = 0.6562725;

    private static (OpticalSystem Sys, double[] N) Singlet(
        double r1, double r2, double index, double thickness = 4.0, double imageDistance = 95.0)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(3.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r1, Thickness = thickness,
                                     Material = "GLASS", IsStop = true });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = 1.0 / r2, Thickness = imageDistance });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });
        return (s, new[] { 1.0, index, 1.0, 1.0 });
    }

    private static SeidelResult Run(OpticalSystem sys, double[] n, double field, double[]? nS = null, double[]? nL = null)
    {
        var p = ParaxialTrace.Trace(sys, n, field);
        return SeidelCoefficients.Compute(sys, n, nS ?? n, nL ?? n, p);
    }

    /// <summary>
    /// Petzval curvature depends only on the surface curvatures and the index step - not on
    /// the aperture, not on the field, not on where the stop sits. Moving the stop changes
    /// coma, astigmatism and distortion and must leave S4 alone; that invariance is the
    /// sharpest single check on the field-dependent terms being separated correctly.
    /// </summary>
    [Fact]
    public void Petzval_DoesNotMoveWhenTheStopMoves()
    {
        var (a, n) = Singlet(60.0, -60.0, 1.6);
        double s4OnLens = Run(a, n, 3.0).TotalS4;

        // Same lens, stop moved 25 mm in front of it.
        var b = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        b.Wavelengths.Add(new Wavelength(D, 1.0, true));
        b.Fields.Add(new Field(3.0));
        b.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        b.Surfaces.Add(new Surface { Index = 1, Thickness = 25.0, IsStop = true });
        b.Surfaces.Add(new Surface { Index = 2, Curvature = 1.0 / 60.0, Thickness = 4.0, Material = "GLASS" });
        b.Surfaces.Add(new Surface { Index = 3, Curvature = -1.0 / 60.0, Thickness = 95.0 });
        b.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });
        var nB = new[] { 1.0, 1.0, 1.6, 1.0, 1.0 };

        double s4StopInFront = Run(b, nB, 3.0).TotalS4;

        Assert.Equal(s4OnLens, s4StopInFront, 10);
    }

    /// <summary>
    /// With the stop at the lens the chief ray goes through the vertex, so Abar carries no
    /// contribution from the surface heights and the field-dependent terms vanish while
    /// spherical does not. Any leakage of aperture terms into the field terms shows here.
    /// </summary>
    [Fact]
    public void StopAtTheLens_LeavesComaAndAstigmatismFromTheChiefRayAlone()
    {
        var (sys, n) = Singlet(60.0, -60.0, 1.6);
        var onAxis = Run(sys, n, 0.0);

        Assert.True(Math.Abs(onAxis.TotalS1) > 1e-6, "a singlet must have spherical aberration");
        // On axis the chief ray is identically zero, so every term carrying Abar must be too.
        Assert.Equal(0.0, onAxis.TotalS2, 12);
        Assert.Equal(0.0, onAxis.TotalS3, 12);
        Assert.Equal(0.0, onAxis.TotalS5, 12);
    }

    /// <summary>
    /// Spherical aberration is an aperture effect: it scales as the fourth power of the
    /// pupil. Doubling the entrance pupil must multiply S1 by sixteen.
    /// </summary>
    [Fact]
    public void Spherical_ScalesAsTheFourthPowerOfTheAperture()
    {
        var (small, n) = Singlet(60.0, -60.0, 1.6);
        double s1Small = Run(small, n, 0.0).TotalS1;

        var (big, _) = Singlet(60.0, -60.0, 1.6);
        big.Aperture = new Aperture(ApertureType.EPD, 40.0);
        double s1Big = Run(big, n, 0.0).TotalS1;

        Assert.Equal(16.0, s1Big / s1Small, 6);
    }

    /// <summary>
    /// Astigmatism is quadratic in field. Tripling the field angle multiplies S3 by nine,
    /// while S1 - which has no field dependence at all - must not move.
    /// </summary>
    [Fact]
    public void Astigmatism_IsQuadraticInField_AndSphericalIsNot()
    {
        // Stop displaced from the lens so the field terms are non-zero.
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        sys.Wavelengths.Add(new Wavelength(D, 1.0, true));
        sys.Fields.Add(new Field(2.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Thickness = 25.0, IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = 1.0 / 60.0, Thickness = 4.0, Material = "GLASS" });
        sys.Surfaces.Add(new Surface { Index = 3, Curvature = -1.0 / 60.0, Thickness = 95.0 });
        sys.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });
        var n = new[] { 1.0, 1.0, 1.6, 1.0, 1.0 };

        var one   = Run(sys, n, 2.0);
        var three = Run(sys, n, 6.0);

        double ratio = Math.Tan(6.0 * Math.PI / 180.0) / Math.Tan(2.0 * Math.PI / 180.0);
        Assert.Equal(ratio * ratio, three.TotalS3 / one.TotalS3, 4);
        Assert.Equal(one.TotalS1, three.TotalS1, 10);
    }

    /// <summary>
    /// A plane surface between identical media is not there optically, and must contribute
    /// nothing to any coefficient. This is the check that catches a term written with the
    /// wrong index difference, which would otherwise produce a small spurious contribution.
    /// </summary>
    [Fact]
    public void ADummySurfaceContributesNothing()
    {
        var (plain, n) = Singlet(60.0, -60.0, 1.6);
        var plainResult = Run(plain, n, 3.0);

        var padded = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        padded.Wavelengths.Add(new Wavelength(D, 1.0, true));
        padded.Fields.Add(new Field(3.0));
        padded.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        padded.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                          Material = "GLASS", IsStop = true });
        padded.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 40.0 });
        padded.Surfaces.Add(new Surface { Index = 3, Thickness = 55.0 });     // dummy plane in air
        padded.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });
        var nPad = new[] { 1.0, 1.6, 1.0, 1.0, 1.0 };

        var paddedResult = Run(padded, nPad, 3.0);

        Assert.Equal(plainResult.TotalS1, paddedResult.TotalS1, 10);
        Assert.Equal(plainResult.TotalS3, paddedResult.TotalS3, 10);
        Assert.Equal(plainResult.TotalS4, paddedResult.TotalS4, 10);
        Assert.Equal(0.0, paddedResult.S1[3], 12);          // the dummy itself
        Assert.Equal(0.0, paddedResult.S4[3], 12);
    }

    /// <summary>
    /// A monochromatic system has no dispersion, so both chromatic sums must be identically
    /// zero - and become non-zero the moment the indices at the two ends differ.
    /// </summary>
    [Fact]
    public void ChromaticTerms_AreZeroWithoutDispersionAndNonZeroWithIt()
    {
        var (sys, n) = Singlet(60.0, -60.0, 1.6);

        var none = Run(sys, n, 3.0);
        Assert.Equal(0.0, none.TotalCL, 12);
        Assert.Equal(0.0, none.TotalCT, 12);

        var nF = new[] { 1.0, 1.612, 1.0, 1.0 };
        var nC = new[] { 1.0, 1.594, 1.0, 1.0 };
        var with = Run(sys, n, 3.0, nF, nC);
        Assert.True(Math.Abs(with.TotalCL) > 1e-6, "a dispersive singlet must have axial colour");
    }

    /// <summary>
    /// The per-surface arrays must sum to the reported totals. Cheap, but it is what makes
    /// the surface breakdown trustworthy as an attribution of where aberration comes from.
    /// </summary>
    [Fact]
    public void PerSurfaceContributionsSumToTheTotals()
    {
        var (sys, n) = Singlet(60.0, -60.0, 1.6);
        var nF = new[] { 1.0, 1.612, 1.0, 1.0 };
        var nC = new[] { 1.0, 1.594, 1.0, 1.0 };
        var r = Run(sys, n, 3.0, nF, nC);

        Assert.Equal(r.TotalS1, Sum(r.S1), 12);
        Assert.Equal(r.TotalS2, Sum(r.S2), 12);
        Assert.Equal(r.TotalS3, Sum(r.S3), 12);
        Assert.Equal(r.TotalS4, Sum(r.S4), 12);
        Assert.Equal(r.TotalS5, Sum(r.S5), 12);
        Assert.Equal(r.TotalCL, Sum(r.CL), 12);
        Assert.Equal(r.TotalCT, Sum(r.CT), 12);
    }

    /// <summary>
    /// A conic constant changes spherical aberration and nothing about Petzval, which
    /// depends only on curvature and index. This exercises the aspheric branch, which the
    /// all-spherical designs never reach.
    /// </summary>
    [Fact]
    public void AConicChangesSphericalButNotPetzval()
    {
        var (sphere, n) = Singlet(60.0, -60.0, 1.6);
        var withSphere = Run(sphere, n, 0.0);

        var (conic, _) = Singlet(60.0, -60.0, 1.6);
        conic.Surfaces[1].Conic = -1.0;                      // parabolic front
        var withConic = Run(conic, n, 0.0);

        Assert.NotEqual(withSphere.TotalS1, withConic.TotalS1);
        Assert.Equal(withSphere.TotalS4, withConic.TotalS4, 12);
    }


    /// <summary>
    /// Petzval, checked against its closed form rather than against itself.
    ///
    /// For a thin lens in air of power phi and index n the Petzval sum is phi/n, giving
    /// S4 = H^2 phi / n exactly. The stop-invariance test above compares S4 with S4 and so
    /// cannot see a wrong constant factor; this can.
    /// </summary>
    [Fact]
    public void Petzval_MatchesTheThinLensClosedForm()
    {
        const double n = 1.6, r = 60.0;
        double phi = (n - 1.0) * (1.0 / r - 1.0 / -r);        // lensmaker, thin

        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        sys.Wavelengths.Add(new Wavelength(D, 1.0, true));
        sys.Fields.Add(new Field(3.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r, Thickness = 0.0,
                                       Material = "GLASS", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / r, Thickness = 1.0 / phi });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });
        var idx = new[] { 1.0, n, 1.0, 1.0 };

        var p = ParaxialTrace.Trace(sys, idx, 3.0);
        var s = SeidelCoefficients.Compute(sys, idx, idx, idx, p);

        double expected = p.LagrangeInvariant * p.LagrangeInvariant * phi / n;
        Assert.Equal(expected, s.TotalS4, 10);
    }

    /// <summary>
    /// The stop-shift equations, which are exact and couple the coefficients to one another:
    ///
    ///   S1* = S1
    ///   S2* = S2 + E S1
    ///   S3* = S3 + 2E S2 + E^2 S1
    ///   S4* = S4
    ///   S5* = S5 + E(3 S3 + S4) + 3E^2 S2 + E^3 S1
    ///
    /// Taking the shift parameter E from the S2 equation leaves the rest as independent
    /// checks. S5 is the only relation containing S4, so a distortion term that dropped its
    /// Petzval part cannot satisfy it - and S3 constrains the coma and spherical terms
    /// against each other, so a wrong power or a swapped invariant fails here too.
    ///
    /// Note these are the FULL equations. The tempting simplification - put the stop on the
    /// lens so S2, S3 and S5 start at zero - only holds for a THIN lens. A singlet of real
    /// thickness still presents a non-zero chief ray height at its second surface.
    /// </summary>
    [Fact]
    public void StopShiftEquationsHold()
    {
        const double n = 1.6, r = 60.0, epd = 20.0, field = 3.0;

        var atLens = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, epd) };
        atLens.Wavelengths.Add(new Wavelength(D, 1.0, true));
        atLens.Fields.Add(new Field(field));
        atLens.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        atLens.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r, Thickness = 4.0,
                                          Material = "GLASS", IsStop = true });
        atLens.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / r, Thickness = 95.0 });
        atLens.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });
        var a = Run(atLens, new[] { 1.0, n, 1.0, 1.0 }, field);

        // Same lens, same stated aperture, stop moved 30 mm in front. The marginal ray is
        // unchanged because the entrance pupil DIAMETER is given rather than derived.
        var shifted = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, epd) };
        shifted.Wavelengths.Add(new Wavelength(D, 1.0, true));
        shifted.Fields.Add(new Field(field));
        shifted.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        shifted.Surfaces.Add(new Surface { Index = 1, Thickness = 30.0, IsStop = true });
        shifted.Surfaces.Add(new Surface { Index = 2, Curvature = 1.0 / r, Thickness = 4.0, Material = "GLASS" });
        shifted.Surfaces.Add(new Surface { Index = 3, Curvature = -1.0 / r, Thickness = 95.0 });
        shifted.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });
        var b = Run(shifted, new[] { 1.0, 1.0, n, 1.0, 1.0 }, field);

        // The two aperture-only coefficients are untouched by a stop shift.
        Assert.Equal(a.TotalS1, b.TotalS1, 10);
        Assert.Equal(a.TotalS4, b.TotalS4, 10);

        double E = (b.TotalS2 - a.TotalS2) / a.TotalS1;
        Assert.True(Math.Abs(E) > 1e-6, "the shift must actually move the pupil");

        double s3Predicted = a.TotalS3 + 2 * E * a.TotalS2 + E * E * a.TotalS1;
        double s5Predicted = a.TotalS5 + E * (3 * a.TotalS3 + a.TotalS4)
                           + 3 * E * E * a.TotalS2 + E * E * E * a.TotalS1;

        Assert.Equal(s3Predicted, b.TotalS3, 10);
        Assert.Equal(s5Predicted, b.TotalS5, 10);
    }

    /// <summary>
    /// The aspheric contribution is a fourth-power-of-aperture term like the spherical one,
    /// so a lens carrying a conic must still show S1 scaling as the fourth power when the
    /// pupil is doubled. An aspheric term written with the wrong power of y breaks the
    /// scaling even though its value still looks plausible.
    /// </summary>
    [Fact]
    public void SphericalStillScalesAsAperturePowerFour_WithAConicPresent()
    {
        var (small, n) = Singlet(60.0, -60.0, 1.6);
        small.Surfaces[1].Conic = -3.5;
        double s1Small = Run(small, n, 0.0).TotalS1;

        var (big, _) = Singlet(60.0, -60.0, 1.6);
        big.Surfaces[1].Conic = -3.5;
        big.Aperture = new Aperture(ApertureType.EPD, 40.0);
        double s1Big = Run(big, n, 0.0).TotalS1;

        Assert.Equal(16.0, s1Big / s1Small, 6);
    }

    /// <summary>
    /// A paraboloid images an axial object at infinity with no spherical aberration at all.
    /// That is exact, classical, and independent of any implementation - so it pins the
    /// absolute size of the conic contribution, not merely that one exists.
    ///
    /// The sphere's own spherical aberration and the conic's correction must cancel to
    /// rounding. A conic term carrying the wrong constant factor still changes S1 and still
    /// scales as the fourth power of the aperture, so only a cancellation like this catches
    /// it: with the factor doubled the two terms miss by the whole of the spherical term.
    /// </summary>
    [Fact]
    public void AParabolicMirrorHasNoSphericalAberration()
    {
        const double radius = -100.0;                 // concave toward the incoming light

        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 30.0) };
        sys.Wavelengths.Add(new Wavelength(D, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / radius, Conic = -1.0,
                                       Thickness = radius / 2.0, Material = "MIRROR", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Thickness = 0.0 });
        var n = new[] { 1.0, 1.0, 1.0 };

        var parabola = Run(sys, n, 0.0);

        // The same mirror left spherical must NOT be corrected - otherwise the test would
        // pass on an implementation that simply ignored the conic.
        var sphereSys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 30.0) };
        sphereSys.Wavelengths.Add(new Wavelength(D, 1.0, true));
        sphereSys.Fields.Add(new Field(0.0));
        sphereSys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sphereSys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / radius, Thickness = radius / 2.0,
                                             Material = "MIRROR", IsStop = true });
        sphereSys.Surfaces.Add(new Surface { Index = 2, Thickness = 0.0 });
        var sphere = Run(sphereSys, n, 0.0);

        Assert.True(Math.Abs(sphere.TotalS1) > 1e-6,
            $"a spherical mirror must have spherical aberration, got {sphere.TotalS1}");
        Assert.Equal(0.0, parabola.TotalS1, 12);
    }
    private static double Sum(double[] v)
    {
        double t = 0;
        foreach (var x in v) t += x;
        return t;
    }
}
