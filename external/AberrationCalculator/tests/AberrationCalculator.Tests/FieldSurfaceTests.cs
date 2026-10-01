using System;
using System.Collections.Generic;
using System.IO;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The field surfaces - where the image this lens FORMS lies, said as a longitudinal distance
/// rather than as an astigmatism coefficient.
///
/// <para><b>These have an external reference, which most things here do not.</b> OpticStudio
/// prints the same four numbers - the Petzval radius and the LPFC, LSFC and LTFC longitudinal
/// coefficients - and its output on <c>E0_infinite_flat.zmx</c> was recorded on 20 September 2026
/// and is inlined below. The fixture is in this repository, so the comparison can be repeated.</para>
///
/// <para>This is also the answer to a question that came up when the image surface was found to
/// reach nothing (see <see cref="CurvedObjectAndImageSurfaceTests"/>): the third-order analysis
/// DOES deal with a curved detector, by saying where the detector should be rather than by
/// reading where it is. A design's field sag is the specification; the surface in the file is one
/// attempt at meeting it.</para>
/// </summary>
public class FieldSurfaceTests
{
    private static string Dir =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "coefficient-reference");

    private sealed record Loaded(OpticalSystem Sys, double[] N, ParaxialResult P, SeidelResult S,
                                 double Field);

    private static Loaded Load(string name)
    {
        string path = Path.Combine(Dir, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"fixture '{name}' is missing from {Dir}", path);

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        int primary = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[primary].Value, new List<string>());

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        return new Loaded(sys, n, p, SeidelCoefficients.Compute(sys, n, n, n, p), field);
    }

    /// <summary>
    /// Against OpticStudio's own numbers for the same file, to five decimals - which is as far
    /// as it prints.
    ///
    /// <para>OpticStudio, <c>E0_infinite_flat.zmx</c>, 0.5876 um, field 5 degrees:</para>
    /// <code>
    /// Petzval radius : -79.9998
    ///         LPFC      LSFC      LTFC
    /// TOT   0.122644  0.293001  0.633715
    /// </code>
    /// </summary>
    [Fact]
    public void TheFieldSagsAgreeWithOpticStudio()
    {
        var l = Load("E0_infinite_flat.zmx");
        var f = FieldSurfaces.Compute(l.Sys, l.S, l.P, l.Field);

        Assert.Equal(-79.9998, f.PetzvalRadius, 3);
        // To five decimals, as a tolerance: rounding both sides to five places fails a value
        // that sits on a rounding boundary (0.6337148 against a printed 0.633715).
        Assert.Equal(0.122644, f.PetzvalSag, 5e-6);
        Assert.Equal(0.293001, f.SagittalSag, 5e-6);
        Assert.Equal(0.633715, f.TangentialSag, 5e-6);

        // The medial surface is the mean of the two astigmatic ones, by construction.
        Assert.Equal(0.5 * (f.SagittalSag + f.TangentialSag), f.MedialSag, 12);
    }

    /// <summary>
    /// The Petzval RADIUS is a property of the lens - curvatures and index steps only - so it
    /// cannot depend on the field the trace was made at, even though <c>S4</c> does.
    /// </summary>
    [Fact]
    public void ThePetzvalRadiusDoesNotDependOnTheField()
    {
        var l = Load("E0_infinite_flat.zmx");
        double reference = FieldSurfaces.Compute(l.Sys, l.S, l.P, l.Field).PetzvalRadius;

        foreach (double field in new[] { 1.0, 3.0, 7.0 })
        {
            var p = ParaxialTrace.Trace(l.Sys, l.N, field);
            var s = SeidelCoefficients.Compute(l.Sys, l.N, l.N, l.N, p);
            double here = FieldSurfaces.Compute(l.Sys, s, p, field).PetzvalRadius;
            Assert.True(Math.Abs(here - reference) < 1e-9,
                $"at {field} degrees the Petzval radius is {here:F6} and at {l.Field} it is "
              + $"{reference:F6}. It is a property of the surfaces and must be one number.");
        }
    }

    /// <summary>
    /// The sags scale as the square of the field, which is what makes them a SURFACE: a
    /// paraboloid through the paraxial focus. Doubling the field must quadruple every sag.
    /// </summary>
    [Fact]
    public void TheSagsGoAsTheSquareOfTheField()
    {
        var l = Load("E0_infinite_flat.zmx");

        var p1 = ParaxialTrace.Trace(l.Sys, l.N, 2.0);
        var f1 = FieldSurfaces.Compute(l.Sys, SeidelCoefficients.Compute(l.Sys, l.N, l.N, l.N, p1), p1, 2.0);
        var p2 = ParaxialTrace.Trace(l.Sys, l.N, 4.0);
        var f2 = FieldSurfaces.Compute(l.Sys, SeidelCoefficients.Compute(l.Sys, l.N, l.N, l.N, p2), p2, 4.0);

        // tan(4 deg) / tan(2 deg) is not quite two, so the ratio is taken on the image heights
        // the sags are actually quoted at.
        double expected = (f2.ImageHeight / f1.ImageHeight) * (f2.ImageHeight / f1.ImageHeight);
        Assert.Equal(expected, f2.SagittalSag / f1.SagittalSag, 6);
        Assert.Equal(expected, f2.TangentialSag / f1.TangentialSag, 6);
        Assert.Equal(expected, f2.PetzvalSag / f1.PetzvalSag, 6);
    }

    /// <summary>
    /// The matching radius does what it says: a detector bent to it sits on the medial surface,
    /// so its own sag at full field equals the medial sag. The sign is the point - a field that
    /// falls SHORT of the paraxial plane needs a detector curving TOWARD the lens, which is a
    /// negative radius, and getting that backwards would double the error rather than remove it.
    /// </summary>
    [Fact]
    public void TheMatchingRadiusPutsADetectorOnTheMedialSurface()
    {
        var l = Load("E0_infinite_flat.zmx");
        var f = FieldSurfaces.Compute(l.Sys, l.S, l.P, l.Field);

        Assert.True(f.MedialSag > 0.0, "this singlet's field falls short of the paraxial plane");
        Assert.True(f.MedialMatchingRadius < 0.0,
            $"the matching radius came back {f.MedialMatchingRadius:F4}; a field falling short "
          + "needs a detector concave toward the lens.");

        double sagOfThatDetector = -(1.0 / f.MedialMatchingRadius) * f.ImageHeight * f.ImageHeight / 2.0;
        Assert.Equal(f.MedialSag, sagOfThatDetector, 12);
    }

    /// <summary>
    /// The sags are unchanged by whatever the file's image surface happens to be - they describe
    /// the lens, not the detector - but the RESIDUAL against that surface is not, and is the one
    /// number in this program that reads the image surface at all.
    ///
    /// <para><c>Ea</c> is the baseline with a detector of R = -50 fitted. Its field sags must be
    /// the baseline's to the bit, and its residual must be the medial sag less what that detector
    /// already takes out.</para>
    /// </summary>
    [Fact]
    public void ADetectorChangesTheResidualAndNotTheSags()
    {
        var flat = Load("E0_infinite_flat.zmx");
        var bent = Load("Ea_image_curved.zmx");

        var f0 = FieldSurfaces.Compute(flat.Sys, flat.S, flat.P, flat.Field);
        var fa = FieldSurfaces.Compute(bent.Sys, bent.S, bent.P, bent.Field);

        Assert.True(f0.SagittalSag.Equals(fa.SagittalSag), "the detector changed the field sag");
        Assert.True(f0.TangentialSag.Equals(fa.TangentialSag), "the detector changed the field sag");
        Assert.True(f0.PetzvalRadius.Equals(fa.PetzvalRadius), "the detector changed the Petzval radius");

        Assert.False(f0.ImageSurfaceIsCurved);
        Assert.True(fa.ImageSurfaceIsCurved);
        Assert.Equal(-50.0, 1.0 / fa.ImageSurfaceCurvature, 9);

        double takenOut = -fa.ImageSurfaceCurvature * fa.ImageHeight * fa.ImageHeight / 2.0;
        Assert.Equal(fa.MedialSag - takenOut, fa.MedialResidual, 12);

        // And that detector is on the wrong side of the medial surface, so it leaves MORE than
        // nothing - the matching radius is -21, not -50.
        Assert.True(Math.Abs(fa.MedialResidual) > 0.0,
            "R = -50 is not the matching radius, so something must be left over");
    }
}
