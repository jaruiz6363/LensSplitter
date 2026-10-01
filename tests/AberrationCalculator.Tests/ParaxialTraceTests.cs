using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The paraxial trace is the foundation everything else stands on: the aberration
/// coefficients are sums over the marginal and chief rays, so an error here would show up
/// as a wrong coefficient and be blamed on the coefficient formula.
///
/// These tests are built on systems whose first-order behaviour is known from the textbook
/// relations, so they pin the trace itself rather than agreement with another program.
/// </summary>
public class ParaxialTraceTests
{
    /// <summary>A thin lens is the one case where every first-order quantity is exact.</summary>
    private static OpticalSystem ThinLens(double radius1, double radius2, double index,
                                          double imageDistance, double epd = 10.0)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, epd) };
        s.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(5.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / radius1, Thickness = 0.0,
                                     Material = "GLASS", IsStop = true });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = 1.0 / radius2, Thickness = imageDistance });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });
        return s;
    }

    private static double[] Indices(double index) => new[] { 1.0, index, 1.0, 1.0 };

    /// <summary>
    /// The lensmaker's equation, 1/f = (n-1)(1/R1 - 1/R2), holds exactly for a lens of zero
    /// thickness. If the surface powers or the index step were wrong, the focal length would
    /// miss it.
    /// </summary>
    [Theory]
    [InlineData(50.0, -50.0, 1.5)]     // biconvex
    [InlineData(30.0, 1e9, 1.5)]       // plano-convex
    [InlineData(-40.0, 40.0, 1.6)]     // biconcave, negative focal length
    [InlineData(25.0, 100.0, 1.7)]     // meniscus
    public void ThinLensFocalLength_MatchesTheLensmakersEquation(double r1, double r2, double n)
    {
        double expected = 1.0 / ((n - 1.0) * (1.0 / r1 - 1.0 / r2));
        var sys = ThinLens(r1, r2, n, Math.Abs(expected));

        var p = ParaxialTrace.Trace(sys, Indices(n), 0.0);

        Assert.Equal(expected, p.Efl, 8);
        Assert.Equal(expected, p.Bfl, 8);   // zero thickness puts the principal planes at the lens
    }

    /// <summary>
    /// A mirror's focal length is R/2 whatever its glass would have been, and the sign
    /// convention has to survive the index negation that carries the reflection.
    /// </summary>
    [Fact]
    public void ConcaveMirror_HasHalfItsRadiusForAFocalLength()
    {
        const double radius = -100.0;      // concave toward the incoming light
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / radius, Thickness = radius / 2.0,
                                     Material = "MIRROR", IsStop = true });
        s.Surfaces.Add(new Surface { Index = 2, Thickness = 0.0 });

        var p = ParaxialTrace.Trace(s, new[] { 1.0, 1.0, 1.0 }, 0.0);

        // Light travels back to the left after the mirror, so the focus sits at a negative
        // distance and the trace must report the magnitude R/2.
        Assert.Equal(Math.Abs(radius) / 2.0, Math.Abs(p.Bfl), 9);
        Assert.Equal(Math.Abs(radius) / 2.0, Math.Abs(p.Efl), 9);
        Assert.True(p.N[1] < 0.0, "the medium after a mirror must carry a negative index");
    }

    /// <summary>
    /// The Lagrange invariant is conserved surface by surface. It couples the marginal ray,
    /// the chief ray and the indices, so it fails if any one of the three is inconsistent -
    /// which makes it the cheapest broad check the trace has.
    /// </summary>
    [Fact]
    public void LagrangeInvariant_IsConservedThroughTheSystem()
    {
        var sys = ThinLens(50.0, -50.0, 1.5, 50.0);
        sys.Surfaces[1].Thickness = 4.0;      // a real thickness, so the surfaces differ

        var p = ParaxialTrace.Trace(sys, Indices(1.5), 5.0);

        Assert.True(Math.Abs(p.LagrangeInvariant) > 1e-9, "the test field must give a non-zero invariant");
        Assert.True(p.InvariantDrift < 1e-12, $"invariant drifted by {p.InvariantDrift:0.0E+0}");
    }

    /// <summary>
    /// A stop behind a lens is not its own entrance pupil: the lens images it, and the chief
    /// ray must be aimed at that image instead. The thin-lens imaging relation gives the
    /// answer exactly, so this pins the pupil solve rather than merely exercising it.
    ///
    /// A stop sitting ON the first surface makes the pupil position zero and hides any error
    /// in that solve, which is why the interesting case is the one tested here.
    /// </summary>
    [Fact]
    public void EntrancePupil_IsTheImageOfTheStop_NotTheStop()
    {
        const double f = 50.0, stopDistance = 20.0;
        double r = 2.0 * f * (1.5 - 1.0);            // thin lens of focal length f, R2 = -R1

        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(5.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r, Thickness = 0.0, Material = "GLASS" });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / r, Thickness = stopDistance });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 30.0, IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });

        var p = ParaxialTrace.Trace(sys, new[] { 1.0, 1.5, 1.0, 1.0, 1.0 }, 5.0);

        // Imaging the stop back through the lens: 1/v - 1/u = 1/f with v = +20 puts its
        // object-space image at +33.33, i.e. a virtual pupil behind the lens.
        double expected = 1.0 / (1.0 / stopDistance - 1.0 / f);
        Assert.Equal(expected, p.EntrancePupilPosition, 7);

        // The property that defines the chief ray: it crosses the axis at the stop.
        Assert.Equal(0.0, p.Ybar[3], 9);

        // Aiming at a pupil that is not on the first surface means arriving there off-axis.
        Assert.True(Math.Abs(p.Ybar[1]) > 1e-3,
            $"chief ray should not enter on axis when the pupil is at {expected:0.###}, got {p.Ybar[1]:0.#####}");
    }

    /// <summary>
    /// An F/number aperture has to end up at the same marginal ray as the entrance pupil
    /// diameter it implies. Files state the aperture both ways, and a design read one way
    /// must not analyse differently from the same design read the other.
    /// </summary>
    [Fact]
    public void FNumberAperture_AgreesWithTheEquivalentPupilDiameter()
    {
        var byEpd = ThinLens(50.0, -50.0, 1.5, 50.0, epd: 12.5);
        var pEpd = ParaxialTrace.Trace(byEpd, Indices(1.5), 0.0);

        var byFno = ThinLens(50.0, -50.0, 1.5, 50.0);
        byFno.Aperture = new Aperture(ApertureType.FNumber, pEpd.Efl / 12.5);
        var pFno = ParaxialTrace.Trace(byFno, Indices(1.5), 0.0);

        Assert.Equal(pEpd.Epd, pFno.Epd, 9);
        Assert.Equal(pEpd.Y[1], pFno.Y[1], 9);
    }

    /// <summary>
    /// For a finite object the magnification must satisfy the imaging relation. This is the
    /// branch that aims the chief ray from a real object height rather than an angle, and it
    /// is the one a system built around infinite conjugates gets wrong silently.
    /// </summary>
    [Fact]
    public void FiniteConjugate_ReproducesTheImagingMagnification()
    {
        const double f = 50.0, objectDistance = 150.0;
        var sys = ThinLens(2.0 * f * 0.5, -2.0 * f * 0.5, 1.5, 0.0);   // radii set below

        // Build the thin lens exactly at focal length f: (n-1)(1/R1 - 1/R2) = 1/f with
        // R2 = -R1 gives R1 = 2f(n-1).
        double r = 2.0 * f * (1.5 - 1.0);
        sys.Surfaces[1].Curvature = 1.0 / r;
        sys.Surfaces[2].Curvature = -1.0 / r;
        sys.Surfaces[0].Thickness = objectDistance;
        sys.FieldType = FieldType.ObjectHeight;

        // Thin-lens imaging: 1/v = 1/f - 1/u, magnification m = -v/u.
        double imageDistance = 1.0 / (1.0 / f - 1.0 / objectDistance);
        double expectedMag = -imageDistance / objectDistance;
        sys.Surfaces[2].Thickness = imageDistance;

        var p = ParaxialTrace.Trace(sys, Indices(1.5), -10.0);

        Assert.Equal(f, p.Efl, 8);
        Assert.Equal(imageDistance, p.ParaxialFocusDistance, 7);
        Assert.Equal(expectedMag, p.Magnification, 8);
        Assert.Equal(expectedMag * -10.0, p.ParaxialImageHeight, 7);
    }

    /// <summary>
    /// A dummy surface changes nothing physical, so no first-order quantity may move. Files
    /// are full of them - a stop plane in air, a spacer left behind by an edit - and the
    /// trace must not treat "the last surface with glass" as the end of the system.
    /// </summary>
    [Fact]
    public void DummySurfaces_DoNotChangeAnyFirstOrderQuantity()
    {
        var plain = ThinLens(50.0, -50.0, 1.5, 50.0);
        var pPlain = ParaxialTrace.Trace(plain, Indices(1.5), 5.0);

        var padded = ThinLens(50.0, -50.0, 1.5, 20.0);
        padded.Surfaces.Insert(3, new Surface { Index = 3, Thickness = 30.0 });   // dummy in the back focus
        for (int i = 0; i < padded.Surfaces.Count; i++) padded.Surfaces[i].Index = i;
        var pPadded = ParaxialTrace.Trace(padded, new[] { 1.0, 1.5, 1.0, 1.0, 1.0 }, 5.0);

        Assert.Equal(pPlain.Efl, pPadded.Efl, 9);
        Assert.Equal(pPlain.Epd, pPadded.Epd, 9);
        Assert.Equal(pPlain.ImageHeight, pPadded.ImageHeight, 9);
        Assert.Equal(pPlain.EntrancePupilPosition, pPadded.EntrancePupilPosition, 9);
    }

    /// <summary>
    /// A single refracting surface between air and glass: power is c(n'-n), so the focal
    /// length referred to object space is n/phi and the distance to the focus inside the
    /// glass is n'/phi - a factor n' larger. Design programs print the first as the
    /// effective focal length, and reporting the second instead inflates every focal length
    /// on any design whose image space is not air.
    /// </summary>
    [Fact]
    public void ImageSpaceInGlass_ReportsTheFocalLengthReferredToObjectSpace()
    {
        const double radius = 50.0, nGlass = 1.5;
        double power = (nGlass - 1.0) / radius;

        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        s.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / radius, Thickness = nGlass / power,
                                     Material = "GLASS", IsStop = true });
        s.Surfaces.Add(new Surface { Index = 2, Thickness = 0.0 });

        var p = ParaxialTrace.Trace(s, new[] { 1.0, nGlass, nGlass }, 0.0);

        Assert.Equal(1.0 / power, p.Efl, 8);            // 100 mm: n_object / phi
        Assert.Equal(nGlass / power, p.Bfl, 8);         // 150 mm: the focus really is that far
    }

    /// <summary>
    /// The exit pupil is the image of the stop, so it exists whether or not the design has
    /// an off-axis field. Deriving it from the traced chief ray loses it entirely on an
    /// on-axis-only design - a whole stock-lens catalog looks like that - because that ray
    /// is identically zero.
    /// </summary>
    [Fact]
    public void ExitPupil_DoesNotDependOnTheFieldBeingOffAxis()
    {
        const double f = 50.0, stopDistance = 20.0;
        double r = 2.0 * f * (1.5 - 1.0);

        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r, Thickness = 0.0, Material = "GLASS" });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / r, Thickness = stopDistance });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 30.0, IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0 });
        var n = new[] { 1.0, 1.5, 1.0, 1.0, 1.0 };

        var onAxis = ParaxialTrace.Trace(sys, n, 0.0);
        var offAxis = ParaxialTrace.Trace(sys, n, 5.0);

        Assert.False(double.IsInfinity(onAxis.ExitPupilPosition), "on-axis design reported no exit pupil");
        Assert.Equal(offAxis.ExitPupilPosition, onAxis.ExitPupilPosition, 8);
        Assert.Equal(offAxis.ExitPupilDiameter, onAxis.ExitPupilDiameter, 8);

        // The stop is behind the lens, so its image lies behind it too: a real exit pupil.
        Assert.True(onAxis.ExitPupilDiameter > 0.0);
    }

    /// <summary>
    /// Magnification is a property of the conjugates, not of the field. A finite-conjugate
    /// system has one whether or not it defines an off-axis field, and whichever way its
    /// fields are written.
    ///
    /// Deriving it as (image height / object height) needs a non-zero object height to
    /// divide by, so it silently returned zero for an on-axis-only design and for any
    /// system whose fields are stated as angles - which is most of them. The marginal-ray
    /// form n*u/(n'*u') has no such dependency.
    /// </summary>
    [Theory]
    [InlineData(FieldType.ObjectHeight, -10.0)]
    [InlineData(FieldType.ObjectHeight, 0.0)]
    [InlineData(FieldType.ObjectAngle, 5.0)]
    [InlineData(FieldType.ObjectAngle, 0.0)]
    public void Magnification_DoesNotDependOnTheField(FieldType fieldType, double fieldY)
    {
        const double f = 50.0, objectDistance = 150.0;
        double r = 2.0 * f * (1.5 - 1.0);
        double imageDistance = 1.0 / (1.0 / f - 1.0 / objectDistance);
        double expected = -imageDistance / objectDistance;          // thin-lens imaging, -0.5

        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0), FieldType = fieldType };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(fieldY));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = objectDistance });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / r, Thickness = 0.0,
                                       Material = "GLASS", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / r, Thickness = imageDistance });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });

        var p = ParaxialTrace.Trace(sys, new[] { 1.0, 1.5, 1.0, 1.0 }, fieldY);

        Assert.Equal(expected, p.Magnification, 8);
    }

    /// <summary>An object at infinity has no transverse magnification to report.</summary>
    [Fact]
    public void Magnification_IsZeroForAnObjectAtInfinity()
    {
        var sys = ThinLens(50.0, -50.0, 1.5, 50.0);
        var p = ParaxialTrace.Trace(sys, Indices(1.5), 5.0);

        Assert.True(p.InfiniteConjugate);
        Assert.Equal(0.0, p.Magnification, 12);
    }
}
