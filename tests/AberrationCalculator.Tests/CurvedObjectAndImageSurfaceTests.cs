using System;
using System.Collections.Generic;
using System.Reflection;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// What happens when the OBJECT surface or the IMAGE surface is not a plane.
///
/// <para>Every aberration route in this repository loops from surface 1 to
/// <see cref="OpticalSystem.LastOpticalSurface"/>, which is <c>Count - 2</c>: the object surface
/// and the image surface sit outside it by construction. The real ray trace does the same, and
/// finishes with a flat transfer to a z target rather than an intersection with the image
/// surface. So the CURVATURE, CONIC and ASPHERIC COEFFICIENTS of both end surfaces are read from
/// the file, stored on the model, and then used by nothing at all.</para>
///
/// <para><b>These tests pin that, and then measure what it costs.</b> Pinning it matters because
/// the silence is the dangerous part: a lens with a curved detector loads without complaint and
/// every number that comes back describes a different lens. Measuring it matters because
/// "ignored" on its own sounds like a rounding decision, and it is not - see the two costing
/// tests at the bottom.</para>
///
/// <para><b>For the COEFFICIENTS this is the convention, and that was checked rather than
/// assumed.</b> OpticStudio was run on three of the fixtures in
/// <c>tests/fixtures/coefficient-reference</c> - <c>E0_infinite_flat</c>, <c>Ea_image_curved</c>
/// (R = -50) and <c>Ed_image_flat_a2</c> (flat, A2 = -0.01, the same surface written the other
/// way) - and its Seidel table and FIFTHORD output are identical across all three, to every
/// printed digit, with an <c>IMA</c> row of zeros. The third-order sums are a property of the
/// LENS, referred to the paraxial image point; the detector is what they are telling you to
/// choose, not an input to them. <see cref="FieldSurfaceTests"/> holds that side of it.</para>
///
/// <para><b>For the RAY TRACE it is a defect.</b> A spot, an RMS radius or a fan on a curved
/// detector must be measured on that detector, and both OpticStudio and LensHH-LT intersect it.
/// This program transfers to a plane, so it answers about a flat detector whatever the file says.
/// <see cref="WhatTheNeglectedDetectorSagCosts"/> measures what that costs.</para>
///
/// <para><b>The OBJECT surface is a third case and is not settled here.</b> A curved object puts
/// each field point at its own conjugate distance, which changes the aberrations themselves
/// rather than where they are measured, so the argument that defends the image surface does not
/// apply. <c>Ee</c> through <c>Eh</c> exist to put that question to OpticStudio.</para>
///
/// <para>Designs that carry these: Schmidt and Wright cameras and most fast catadioptrics
/// (curved focal surface), fibre faceplates and film gates (curved object), and anything
/// deliberately matched to a curved detector.</para>
/// </summary>
public class CurvedObjectAndImageSurfaceTests
{
    private const double D = 0.5875618;

    /// <summary>The four shapes asked about, applied to whichever end surface is under test.</summary>
    public static IEnumerable<object[]> Shapes => new List<object[]>
    {
        new object[] { "curved" },
        new object[] { "curved+conic" },
        new object[] { "curved+A4,A6" },
        new object[] { "curved+A2" },
    };

    private static void Shape(Surface s, string how)
    {
        if (how == "flat") return;
        s.Curvature = 1.0 / 200.0;
        switch (how)
        {
            case "curved":
                break;
            case "curved+conic":
                s.Type = SurfaceType.EvenAsphere; s.Conic = -1.0; break;
            case "curved+A4,A6":
                s.Type = SurfaceType.EvenAsphere;
                s.AsphericCoefficients[1] = 1e-7;
                s.AsphericCoefficients[2] = 1e-11; break;
            case "curved+A2":
                s.Type = SurfaceType.EvenAsphere;
                s.AsphericCoefficients[0] = 1e-4; break;
            default:
                throw new ArgumentException("unknown shape " + how, nameof(how));
        }
    }

    // ── The two designs ─────────────────────────────────────────────────────────────────

    /// <summary>Infinite conjugate singlet, image surface at the paraxial focus.</summary>
    private static (OpticalSystem Sys, double[] N) Infinite(string imageShape)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 4.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(5.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "GLASS", IsStop = true, SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 49.3670886076,
                                     SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 20.0 });
        Shape(s.Surfaces[3], imageShape);
        return (s, new[] { 1.0, 1.6, 1.0, 1.0 });
    }

    /// <summary>Finite conjugate, object 200 mm away, field stated as an object height.</summary>
    private static (OpticalSystem Sys, double[] N) Finite(string objectShape)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 8.0),
                                    FieldType = FieldType.ObjectHeight };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(10.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = 200.0 });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "GLASS", IsStop = true, SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 66.3865546218,
                                     SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 20.0 });
        Shape(s.Surfaces[0], objectShape);
        return (s, new[] { 1.0, 1.6, 1.0, 1.0 });
    }

    // ── The fingerprint ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Everything this repository will say about a lens, as a flat list of numbers: the whole
    /// paraxial result including both traced rays surface by surface, every Seidel array and
    /// total, every Buchdahl total through the seventh order (by reflection, so a coefficient
    /// added later is covered without editing this), the Forbes series evaluated on two concrete
    /// rays, and a fan of real ray landings.
    /// </summary>
    private static List<(string Name, double Value)> Fingerprint(OpticalSystem sys, double[] n,
                                                                 double field)
    {
        var f = new List<(string, double)>();
        var p = ParaxialTrace.Trace(sys, n, field);

        f.Add(("efl", p.Efl)); f.Add(("power", p.Power)); f.Add(("bfl", p.Bfl));
        f.Add(("epd", p.Epd)); f.Add(("enp", p.EntrancePupilPosition));
        f.Add(("exp", p.ExitPupilPosition)); f.Add(("expDia", p.ExitPupilDiameter));
        f.Add(("fno", p.FNumber)); f.Add(("imageHeight", p.ImageHeight));
        f.Add(("focusDistance", p.ParaxialFocusDistance));
        f.Add(("paraxialImageHeight", p.ParaxialImageHeight));
        f.Add(("magnification", p.Magnification)); f.Add(("H", p.LagrangeInvariant));
        f.Add(("drift", p.InvariantDrift));
        for (int i = 0; i < p.Y.Length; i++)
        {
            f.Add(($"y[{i}]", p.Y[i]));       f.Add(($"u[{i}]", p.U[i]));
            f.Add(($"ybar[{i}]", p.Ybar[i])); f.Add(($"ubar[{i}]", p.Ubar[i]));
            f.Add(($"n[{i}]", p.N[i]));
        }

        var sd = SeidelCoefficients.Compute(sys, n, n, n, p);
        f.Add(("S1", sd.TotalS1)); f.Add(("S2", sd.TotalS2)); f.Add(("S3", sd.TotalS3));
        f.Add(("S4", sd.TotalS4)); f.Add(("S5", sd.TotalS5));
        f.Add(("CL", sd.TotalCL)); f.Add(("CT", sd.TotalCT));
        for (int i = 0; i < sd.S1.Length; i++)
        {
            f.Add(($"S1[{i}]", sd.S1[i])); f.Add(($"S2[{i}]", sd.S2[i]));
            f.Add(($"S3[{i}]", sd.S3[i])); f.Add(($"S4[{i}]", sd.S4[i]));
            f.Add(($"S5[{i}]", sd.S5[i])); f.Add(($"S1asph[{i}]", sd.S1Aspheric[i]));
        }

        var b = BuchdahlCoefficients.Compute(sys, p);
        foreach (var member in typeof(BuchdahlTerms).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (member.FieldType == typeof(double))
                f.Add(("buchdahl." + member.Name, (double)member.GetValue(b.Totals)!));
        f.Add(("buchdahl.fno", b.FNumber)); f.Add(("buchdahl.lagrange", b.Lagrange));

        var forbes = ForbesCoefficients.Trace(sys, n, p, degree: 7);
        var (m1, s1) = forbes.Predict(2.0, 0.0, 0.02, 0.0);
        var (m2, s2) = forbes.Predict(1.0, 1.0, 0.01, -0.01);
        f.Add(("forbes.m1", m1)); f.Add(("forbes.s1", s1));
        f.Add(("forbes.m2", m2)); f.Add(("forbes.s2", s2));

        int count = sys.Surfaces.Count;
        foreach (double h in new[] { 0.0, 0.5 * field, field })
            foreach (double py in new[] { -1.0, -0.5, 0.5, 1.0 })
            {
                var e = RealRayTrace.TraceRecord(sys, n, p, h, py, 0.0)[count - 1];
                f.Add(($"ray({h},{py}).x", e.X)); f.Add(($"ray({h},{py}).y", e.Y));
                f.Add(($"ray({h},{py}).L", e.L)); f.Add(($"ray({h},{py}).M", e.M));
            }

        return f;
    }

    private static void SameToTheBit(List<(string Name, double Value)> flat,
                                     List<(string Name, double Value)> shaped,
                                     string what, string how)
    {
        Assert.Equal(flat.Count, shaped.Count);
        for (int i = 0; i < flat.Count; i++)
        {
            Assert.Equal(flat[i].Name, shaped[i].Name);
            Assert.True(flat[i].Value.Equals(shaped[i].Value),
                $"{what} '{how}': {flat[i].Name} is {shaped[i].Value:E17} with the shape on and "
              + $"{flat[i].Value:E17} with it off. Something now reads the end surface's shape - "
              + "either the gap this file documents has been closed, or it has been closed by "
              + "accident. Rewrite this test deliberately rather than retuning it.");
        }
    }

    // ── (a)-(d) The image surface ───────────────────────────────────────────────────────

    /// <summary>
    /// Curvature, conic, A4/A6 and A2 on the IMAGE surface reach nothing: not the paraxial
    /// trace, not Seidel, not Buchdahl, not Forbes, and not a traced ray. Bit for bit.
    ///
    /// <para>The paraxial and coefficient parts of that are correct, and OpticStudio does the
    /// same on these very files - see the class note. The RAY part is not.
    /// <see cref="WhatTheNeglectedDetectorSagCosts"/> measures what it costs.</para>
    ///
    /// <para>So this test is a CONFORMANCE test for everything except the four ray landings in
    /// the fingerprint, which pin a known defect until it is fixed. If it starts failing, look at
    /// which entry moved before assuming either.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheShapeOfTheImageSurfaceReachesNothing(string how)
    {
        var (flatSys, n) = Infinite("flat");
        var (shapedSys, _) = Infinite(how);
        SameToTheBit(Fingerprint(flatSys, n, 5.0), Fingerprint(shapedSys, n, 5.0),
                     "image surface", how);
    }

    // ── (e)-(h) The object surface, at finite conjugate ─────────────────────────────────

    /// <summary>
    /// The same for the OBJECT surface of a finite-conjugate system. Here the silence is harder
    /// to defend: a curved object surface puts the off-axis object point at a different DISTANCE
    /// from the lens, so each field point has its own conjugate and its own defocus.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheShapeOfTheObjectSurfaceReachesNothing(string how)
    {
        var (flatSys, n) = Finite("flat");
        var (shapedSys, _) = Finite(how);
        SameToTheBit(Fingerprint(flatSys, n, 10.0), Fingerprint(shapedSys, n, 10.0),
                     "object surface", how);
    }

    /// <summary>
    /// And at INFINITE conjugate, where ignoring the object surface is the only sensible thing
    /// to do - an object at infinity has no shape to speak of. Kept so that the two conjugates
    /// are not confused with one another later.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheShapeOfTheObjectSurfaceReachesNothingAtInfiniteConjugateEither(string how)
    {
        var (flatSys, n) = Infinite("flat");
        var (shapedSys, _) = Infinite("flat");
        Shape(shapedSys.Surfaces[0], how);
        SameToTheBit(Fingerprint(flatSys, n, 5.0), Fingerprint(shapedSys, n, 5.0),
                     "object surface at infinity", how);
    }

    // ── What the silence costs ──────────────────────────────────────────────────────────

    /// <summary>
    /// The RMS spot this program reports, against the RMS spot on the detector the file
    /// describes. The same traced rays in both cases; the only difference is the surface they
    /// are caught on, and the second is computed here from each ray's own direction cosines
    /// rather than from any model.
    ///
    /// <para>On this f/12 singlet at 5 degrees, a detector curved to R = -35 mm collects about
    /// 9.7 um RMS where the program reports 17.2 um. The program is not nearly right; it is
    /// answering about a flat detector, and it is the file that says the detector is not flat.</para>
    ///
    /// <para><b>This is a defect and not a convention.</b> OpticStudio and LensHH-LT both catch
    /// rays on the curved image surface - it is how a curved-detector design is worked on at all
    /// - and it is only the COEFFICIENT routes that leave the surface alone, which they are right
    /// to do. So the gap is in <c>RealRayTrace</c>, whose last step is a flat transfer to a z
    /// target rather than an intersection.</para>
    ///
    /// <para><b>This test is expected to fail the day that is fixed</b>, which is the point of
    /// it: the number below is the size of what is missing, and when it stops being missing this
    /// test should be replaced by one requiring the two to AGREE.</para>
    /// </summary>
    [Fact]
    public void WhatTheNeglectedDetectorSagCosts()
    {
        double cDetector = -1.0 / 35.0;
        var (sys, n) = Infinite("flat");
        sys.Surfaces[3].Curvature = cDetector;
        var p = ParaxialTrace.Trace(sys, n, 5.0);
        int count = sys.Surfaces.Count;

        var onPlane = new List<(double X, double Y)>();
        var onDetector = new List<(double X, double Y)>();
        for (int r = 1; r <= 8; r++)
            for (int a = 0; a < 12; a++)
            {
                double rho = Math.Sqrt(r / 8.0);                 // equal-area rings
                double th = 2.0 * Math.PI * a / 12.0;
                var e = RealRayTrace.TraceRecord(sys, n, p, 5.0,
                                                 rho * Math.Sin(th), rho * Math.Cos(th))[count - 1];
                if (!e.Ok) continue;
                onPlane.Add((e.X, e.Y));
                // Where that same ray meets the stated detector: its sag at the ray's own
                // landing radius, carried along the ray's own direction.
                double sag = cDetector * (e.X * e.X + e.Y * e.Y) / 2.0;
                onDetector.Add((e.X + sag * e.L / e.N, e.Y + sag * e.M / e.N));
            }
        Assert.True(onPlane.Count > 80, "too few rays survived to measure anything");

        double plane = RmsRadiusMicrons(onPlane), detector = RmsRadiusMicrons(onDetector);
        Assert.True(plane > 15.0 && plane < 20.0,
            $"the reported spot is {plane:F2} um; this test's commentary describes a 17.2 um one, "
          + "so the design has drifted and the numbers below mean something else now.");
        Assert.True(detector < 0.7 * plane,
            $"the detector sag is neglected but costs nothing here: {plane:F2} um reported "
          + $"against {detector:F2} um on the stated detector. If those have converged, curved "
          + "image surfaces may now be implemented - check that before retuning this.");
    }

    /// <summary>
    /// The same question on the object side, measured on single rays rather than a spot: where
    /// the ray from the TRUE object point lands, against where this program puts it. The true
    /// point is the one on the curved object surface - a sag nearer the lens - and the ray to
    /// the same pupil point is built here exactly as <c>RealRayTrace</c> builds it, so the sag
    /// is the only difference between the two.
    ///
    /// <para>An object surface of R = 25 mm at 10 mm off axis stands 2 mm out of its own vertex
    /// plane, 1% of a 200 mm conjugate, and the worst ray lands 47 um from where this program
    /// says it does - against a spot that is itself only a few microns across.</para>
    /// </summary>
    [Fact]
    public void WhatTheNeglectedObjectSagCosts()
    {
        double rObject = 25.0, height = 10.0;
        var (sys, n) = Finite("flat");
        sys.Surfaces[0].Curvature = 1.0 / rObject;
        var p = ParaxialTrace.Trace(sys, n, height);

        double sag = (height * height / rObject) / 2.0;          // toward the lens
        double distance = Math.Abs(sys.Surfaces[0].Thickness);
        double epr = 0.5 * p.Epd, ep = p.EntrancePupilPosition;
        int count = sys.Surfaces.Count;

        double worst = 0.0;
        foreach (double py in new[] { -1.0, -0.5, 0.0, 0.5, 1.0 })
        {
            var asFlat = RealRayTrace.TraceRecord(sys, n, p, height, py, 0.0)[count - 1];

            // The same ray, from the object point where the curved surface actually puts it.
            double span = ep + (distance - sag);
            double dx = 0.0, dy = py * epr - height, dz = span;
            double fraction = (distance - sag) / span;
            var asCurved = RealRayTrace.TraceRecordFrom(sys, n, p, fraction * dx,
                                                        height + fraction * dy,
                                                        dx, dy, dz)[count - 1];

            Assert.True(asFlat.Ok && asCurved.Ok, "a ray failed");
            worst = Math.Max(worst, 1000.0 * Math.Abs(asCurved.Y - asFlat.Y));
        }

        Assert.True(worst > 20.0,
            $"the object sag is neglected but costs only {worst:F2} um here, so this test is no "
          + "longer measuring anything. Either the design drifted or curved object surfaces are "
          + "now handled - find out which before retuning it.");
    }

    private static double RmsRadiusMicrons(List<(double X, double Y)> pts)
    {
        double sx = 0.0, sy = 0.0;
        foreach (var q in pts) { sx += q.X; sy += q.Y; }
        sx /= pts.Count; sy /= pts.Count;
        double s2 = 0.0;
        foreach (var q in pts) s2 += (q.X - sx) * (q.X - sx) + (q.Y - sy) * (q.Y - sy);
        return 1000.0 * Math.Sqrt(s2 / pts.Count);
    }
}
