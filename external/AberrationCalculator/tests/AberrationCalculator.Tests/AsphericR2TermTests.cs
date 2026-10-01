using System;
using System.Collections.Generic;
using System.Reflection;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The even-asphere polynomial starts at r-squared, and that first coefficient is not figuring:
/// it moves the vertex curvature, and with it the surface power.
///
/// <para>It used to be read out of the file and then consumed by nothing at all - the paraxial
/// power came from <c>Curvature</c>, the Buchdahl scheme likewise, and <c>Sag</c>, the only
/// thing that honoured it, had no callers in the library. A design carrying an r-squared term
/// was therefore analysed as a different surface from the one it describes, silently.</para>
///
/// <para>The check below does not assert a hand-computed number, which would only test itself.
/// It rewrites every surface into a SECOND, equally valid representation of the same physical
/// surface - base curvature pulled down by 2d, an r-squared coefficient of d put back, and the
/// higher coefficients adjusted so the sag series is unchanged term by term to r^8 - and
/// requires the two to agree. They describe one surface, so every coefficient must match.</para>
/// </summary>
public class AsphericR2TermTests
{
    /// <summary>
    /// Rewrite a surface as base curvature (c - 2d) with an r-squared term d, preserving the
    /// sag series to r^8. Matching th1 = c/2 + A2 fixes the shift; th2, th3 and th4 then fix
    /// the three higher coefficients against the new base sphere.
    /// </summary>
    static void Reparameterise(Surface s, double delta)
    {
        double c = s.Curvature, k = s.Conic;
        double c2 = c * c, c3 = c2 * c, c5 = c3 * c2, c7 = c5 * c2;
        double k1 = 1.0 + k;

        var a = s.AsphericCoefficients;
        double a4 = a.Length > 1 ? a[1] : 0.0;
        double a6 = a.Length > 2 ? a[2] : 0.0;
        double a8 = a.Length > 3 ? a[3] : 0.0;

        double th2 = k1 * c3 / 8.0 + a4;
        double th3 = k1 * k1 * c5 / 16.0 + a6;
        double th4 = 5.0 * k1 * k1 * k1 * c7 / 128.0 + a8;

        double b = c - 2.0 * delta;
        double b2 = b * b, b3 = b2 * b, b5 = b3 * b2, b7 = b5 * b2;

        var n = new double[Math.Max(8, a.Length)];
        n[0] = delta;
        n[1] = th2 - b3 / 8.0;
        n[2] = th3 - b5 / 16.0;
        n[3] = th4 - 5.0 * b7 / 128.0;

        s.Curvature = b;
        s.Conic = 0.0;
        s.AsphericCoefficients = n;
        if (s.Type == SurfaceType.Standard) s.Type = SurfaceType.EvenAsphere;
    }

    static (ParaxialResult Paraxial, BuchdahlResult Buchdahl) Analyse(string name, double delta)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        if (delta != 0.0)
            for (int i = 1; i <= sys.LastOpticalSurface(); i++)
                if (Math.Abs(sys.Surfaces[i].Curvature) > 1e-12)
                    Reparameterise(sys.Surfaces[i], delta * sys.Surfaces[i].Curvature);

        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return (p, b);
    }

    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    [InlineData("TertiaryTestbed_Triplet24")]
    public void AnRSquaredTermIsTheSameSurfaceWrittenAnotherWay(string name)
    {
        var plain = Analyse(name, 0.0);
        var moved = Analyse(name, 0.05);

        Assert.Equal(plain.Paraxial.Efl, moved.Paraxial.Efl, 9);

        foreach (var f in typeof(BuchdahlTerms).GetFields(BindingFlags.Public
                                                          | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(double)) continue;
            double a = (double)f.GetValue(plain.Buchdahl.Totals)!;
            double c = (double)f.GetValue(moved.Buchdahl.Totals)!;
            double scale = Math.Max(Math.Abs(a), Math.Abs(c));
            if (scale < 1e-12) continue;
            Assert.True(Math.Abs(a - c) / scale < 1e-8,
                $"{name}: {f.Name} {a:E10} against {c:E10} once the same surface is written " +
                "with an r-squared term");
        }
    }

    /// <summary>
    /// And the term is not simply ignored: moving the vertex curvature MUST change the answer.
    /// Without this the test above would pass just as well against code that dropped the
    /// r-squared coefficient on the floor, which is the bug it exists to catch.
    /// </summary>
    [Fact]
    public void AnRSquaredTermIsNotSilentlyDiscarded()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        double before = ParaxialTrace.Trace(sys, n, field).Efl;

        var a = sys.Surfaces[1].AsphericCoefficients;
        var bumped = new double[Math.Max(8, a.Length)];
        Array.Copy(a, bumped, a.Length);
        bumped[0] = 1e-3;
        sys.Surfaces[1].AsphericCoefficients = bumped;

        double after = ParaxialTrace.Trace(sys, n, field).Efl;
        Assert.True(Math.Abs(after - before) > 1e-6,
            $"an r-squared coefficient moved the vertex curvature but the focal length did " +
            $"not move: {before:F6} to {after:F6}");
    }
}
