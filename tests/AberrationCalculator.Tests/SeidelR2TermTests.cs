using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The r-squared term on the SEIDEL route, which had a bug for as long as the file existed.
///
/// <para><see cref="AsphericR2TermTests"/> makes the same check on the BUCHDAHL route and has
/// always passed. Nothing made it on the Seidel route, and that is exactly where the defect was:
/// the aspheric term was computed as <c>Conic*c^3/8 + A4</c> against the VERTEX curvature, which
/// is the right answer whenever <c>A2 = 0</c> and wrong whenever it is not, because an r^2
/// coefficient moves the vertex sphere out from under the r^4 departure.</para>
///
/// <para><b>Every fixture in this repository has A2 = 0</b>, deliberately - the
/// coefficient-reference README says so - so no test could have caught it by accident. It was
/// caught by writing one surface two ways and getting two answers.</para>
///
/// <para><b>The two descriptions.</b> A surface of base curvature <c>cb</c> carrying <c>A2 = d</c>
/// is the same surface as one of curvature <c>cb + 2d</c> carrying
/// <c>A4 = (cb^3 - (cb+2d)^3)/8</c>: matching the r^2 coefficients fixes the vertex curvature and
/// matching the r^4 coefficients fixes the compensating term. They differ only at r^6, which is
/// fifth order and cannot reach a Seidel sum.</para>
/// </summary>
public class SeidelR2TermTests
{
    private sealed record Run(SeidelResult Seidel, double Efl);

    /// <summary>
    /// Build a two-surface singlet described either way, and analyse it. <paramref name="asA2"/>
    /// true puts the departure in an r^2 coefficient; false rolls it into the curvature and puts
    /// the compensating r^4 term on instead.
    /// </summary>
    private static Run Analyse(double cBase, double conic, double a2, double a4Extra, bool asA2)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 12.0) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(3.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });

        var s = new Surface
        {
            Index = 1, Thickness = 5.0, Material = "GLASS", IsStop = true,
            Type = SurfaceType.EvenAsphere, Conic = conic,
        };

        if (asA2)
        {
            s.Curvature = cBase;
            s.AsphericCoefficients[0] = a2;
            s.AsphericCoefficients[1] = a4Extra;
        }
        else
        {
            // The same surface with the r^2 term rolled into the curvature. The r^4 coefficient
            // must absorb the difference between the two spheres' own r^4 terms.
            double v = cBase + 2.0 * a2;
            s.Curvature = v;
            s.AsphericCoefficients[0] = 0.0;
            s.AsphericCoefficients[1] = a4Extra
                                      + (1.0 + conic) * cBase * cBase * cBase / 8.0
                                      - (1.0 + conic) * v * v * v / 8.0;
            // The conic is carried on the new base sphere, so its own r^4 share is already in
            // the line above; what remains is that the conic now multiplies a different cube.
        }

        sys.Surfaces.Add(s);
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -0.012, Thickness = 60.0 });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });

        var n = new[] { 1.0, 1.5168, 1.0, 1.0 };
        var p = ParaxialTrace.Trace(sys, n, 3.0);
        return new Run(SeidelCoefficients.Compute(sys, n, n, n, p), p.Efl);
    }

    /// <summary>
    /// One surface, two descriptions, one answer. This is the test that did not exist, and its
    /// absence is why the defect survived.
    /// </summary>
    [Theory]
    [InlineData(0.02, 0.0, 1e-4, 0.0)]
    [InlineData(0.02, 0.0, -2e-4, 0.0)]
    [InlineData(0.02, 0.0, 1e-4, 3e-7)]
    [InlineData(0.02, -0.6, 1e-4, 0.0)]
    [InlineData(0.035, -1.0, 5e-5, -1e-7)]
    public void AnRSquaredTermGivesTheSameSeidelAsTheShiftedSphere(
        double cBase, double conic, double a2, double a4)
    {
        var withR2 = Analyse(cBase, conic, a2, a4, asA2: true);
        var shifted = Analyse(cBase, conic, a2, a4, asA2: false);

        Assert.Equal(withR2.Efl, shifted.Efl, 12);

        void Same(double a, double b, string which)
        {
            double scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1e-12);
            Assert.True(Math.Abs(a - b) / scale < 1e-10,
                $"c={cBase} k={conic} A2={a2} A4={a4}: {which} is {a:E12} written with an "
              + $"r-squared term and {b:E12} written as the equivalent shifted sphere. They are "
              + "one surface and must give one answer.");
        }
        Same(withR2.Seidel.TotalS1, shifted.Seidel.TotalS1, "S1");
        Same(withR2.Seidel.TotalS2, shifted.Seidel.TotalS2, "S2");
        Same(withR2.Seidel.TotalS3, shifted.Seidel.TotalS3, "S3");
        Same(withR2.Seidel.TotalS4, shifted.Seidel.TotalS4, "S4");
        Same(withR2.Seidel.TotalS5, shifted.Seidel.TotalS5, "S5");
    }

    /// <summary>
    /// And the term is not simply being ignored: an r-squared coefficient MUST move the answer,
    /// because it changes the surface's power. Without this the test above would pass against
    /// code that dropped A2 on the floor - which is the bug the r^2 term actually had once on the
    /// Buchdahl side, recorded in <see cref="AsphericR2TermTests"/>.
    /// </summary>
    [Fact]
    public void AnRSquaredTermIsNotSilentlyDiscarded()
    {
        var plain = Analyse(0.02, 0.0, 0.0, 0.0, asA2: true);
        var withR2 = Analyse(0.02, 0.0, 1e-4, 0.0, asA2: true);

        Assert.True(Math.Abs(plain.Efl - withR2.Efl) > 1e-6,
            "the focal length did not move, so the r-squared term is reaching nothing");
        Assert.True(Math.Abs(plain.Seidel.TotalS1 - withR2.Seidel.TotalS1) > 1e-9,
            "S1 did not move");
        Assert.True(Math.Abs(plain.Seidel.TotalS4 - withR2.Seidel.TotalS4) > 1e-9,
            "Petzval did not move, and an r-squared term is a curvature change that must move it");
    }

    /// <summary>
    /// The correction must vanish EXACTLY, not nearly, wherever there is no r-squared term -
    /// which is every design in this repository - so that nothing computed before the fix moves
    /// by even a bit.
    ///
    /// <para>This is not pedantry, and the first version of the fix failed it. Writing the
    /// departure as <c>(1+k)cb^3/8 - c^3/8</c> is the same algebra and different arithmetic:
    /// <c>1 + k</c> rounds, so a conic of -0.6 came out a few bits from where it had always been.
    /// Several tests in this suite demand bit-identity of exactly these numbers. The fix is
    /// grouped so the conic keeps its own untouched term and the correction is a difference of
    /// two cubes of the SAME double, which is identically zero.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.02)]
    [InlineData(-0.6, 0.02)]
    [InlineData(-1.0, 0.035)]
    public void WithNoRSquaredTermTheCorrectionIsExactlyZero(double conic, double curvature)
    {
        var s = new Surface
        {
            Index = 1, Curvature = curvature, Conic = conic, Type = SurfaceType.EvenAsphere,
        };
        // No r^2 term: the vertex curvature must be the base curvature, bit for bit.
        Assert.True(s.VertexCurvature.Equals(s.Curvature),
            $"VertexCurvature {s.VertexCurvature:E17} is not bitwise the base curvature "
          + $"{s.Curvature:E17} with A2 = 0, so the correction below cannot cancel.");

        double cb = s.Curvature, c = s.VertexCurvature;
        double correction = cb * cb * cb / 8.0 - c * c * c / 8.0;

        Assert.True(correction.Equals(0.0),
            $"conic {conic}: the vertex correction is {correction:E17} rather than exactly zero, "
          + "so every number this program already reported would move.");
    }
}
