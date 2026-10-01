using System;
using System.Collections.Generic;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// <see cref="CoefficientInversion.InvertThirdAndFifth"/> on this program's own rays: the third
/// and fifth orders read off the traced landings, against Buchdahl's closed form.
///
/// <para>This is the instrument the Optiland cross-check uses (<c>OptilandTests</c>), gated here
/// on rays whose tracer is already validated, so that when Optiland's rays are put through it the
/// only new thing is the rays.</para>
/// </summary>
public class LowerOrderInversionTests
{
    private readonly ITestOutputHelper _out;
    public LowerOrderInversionTests(ITestOutputHelper output) { _out = output; }

    internal sealed record Loaded(string Name, OpticalSystem System, double[] Indices,
                                  ParaxialResult Paraxial, double Field, double WavelengthUm);

    internal static Loaded Load(string name, string folder, GlassCatalog catalog)
    {
        var sys = LensFile.Read(Designs.PathOf(name, folder), catalog);
        double wl = sys.Wavelengths[sys.PrimaryWavelengthIndex].Value;
        var n = IndexResolver.Build(sys, catalog, wl, new List<string>());
        double field = sys.MaxFieldY();
        return new Loaded(name, sys, n, ParaxialTrace.Trace(sys, n, field), field, wl);
    }

    /// <summary>Designs the ray inversion applies to: object at infinity, and a field.</summary>
    internal static IEnumerable<Loaded> Invertible(GlassCatalog catalog)
    {
        foreach (var d in Designs.All())
        {
            var l = Load((string)d[0], (string)d[1], catalog);
            if (!l.Paraxial.InfiniteConjugate || l.Field == 0.0) continue;
            yield return l;
        }
    }

    /// <summary>Worst difference over one order's coefficients, relative to the largest of them.</summary>
    internal static (double Worst, string Name) Worst(string[] names, BuchdahlTerms reference, BuchdahlTerms other)
    {
        double largest = names.Max(n => Math.Abs(reference[n]));
        string at = names.OrderByDescending(n => Math.Abs(other[n] - reference[n])).First();
        return (Math.Abs(other[at] - reference[at]) / largest, at);
    }

    /// <summary>
    /// Every design on disk that the inversion applies to. The bounds are set from what the
    /// scale ladder can separate: the third order comes back to 5E-8 of the largest third-order
    /// coefficient at worst and the fifth to 1.3E-5, both on the Triplet24 testbed, whose ninth
    /// order is the largest here; on most designs it is 1E-11 and 1E-7.
    /// </summary>
    [Fact]
    public void OwnRaysGiveBackBuchdahlsThirdAndFifthOrder()
    {
        var catalog = CatalogLocator.LoadBundled();
        int count = 0;
        foreach (var l in Invertible(catalog))
        {
            var b = BuchdahlCoefficients.Compute(l.System, l.Paraxial).Totals;
            var r = CoefficientInversion.InvertThirdAndFifth(l.System, l.Indices, l.Paraxial, l.Field);
            Assert.NotNull(r);

            var (w3, n3) = Worst(CoefficientInversion.ThirdOrderNames, b, r!.Terms);
            var (w5, n5) = Worst(CoefficientInversion.FifthOrderNames, b, r.Terms);
            _out.WriteLine($"{l.Name,-45} third {w3:E1} ({n3})  fifth {w5:E1} ({n5})");
            Assert.True(w3 < 1e-7, $"{l.Name}: third order off by {w3:E2} of the largest, at {n3}");
            Assert.True(w5 < 3e-5, $"{l.Name}: fifth order off by {w5:E2} of the largest, at {n5}");
            count++;
        }
        Assert.True(count >= 30, $"only {count} designs were inverted");
    }

    /// <summary>
    /// A paraboloid images an axial point at infinity perfectly, at every aperture, so every
    /// axial ray must cross the focal plane at y = 0 - a closed-form answer, owed to no program.
    ///
    /// <para>RealRayTrace used to fail this by 20 mm. It had no reflection: at a mirror it
    /// refracted between equal indices and the ray carried straight on, landing at the height it
    /// was launched with - 10 mm at half the pupil and 20 mm at the edge of this one. Found by
    /// the Optiland cross-check, whose rays landed at 4E-15; settled by this test, which needs
    /// no Optiland.</para>
    /// </summary>
    [Fact]
    public void AParaboloidFocusesEveryAxialRayOnTheAxis()
    {
        var l = Load("F4_parabolic_mirror.zmx", "coefficient-reference", CatalogLocator.LoadBundled());
        foreach (double py in new[] { 0.25, 0.5, 0.75, 1.0 })
            foreach (double theta in new[] { 0.0, 0.7, 1.9 })
            {
                var land = RealRayTrace.Trace(l.System, l.Indices, l.Paraxial, 0.0,
                                              py * Math.Cos(theta), py * Math.Sin(theta));
                Assert.True(land.Ok);
                Assert.True(Math.Abs(land.Y) < 1e-12 && Math.Abs(land.Z) < 1e-12,
                    $"pupil {py}, azimuth {theta}: lands at ({land.Y:E3}, {land.Z:E3}), not on the axis");
            }

        // Off axis the chief ray lands at the paraxial image height, f tan(field), to within the
        // paraboloid's own distortion - which at half a degree is a few parts in a million.
        var chief = RealRayTrace.Trace(l.System, l.Indices, l.Paraxial, l.Field, 0.0, 0.0);
        double paraxialHeight = l.Paraxial.Efl * Math.Tan(l.Field * Math.PI / 180.0);
        Assert.True(Math.Abs(chief.Y - paraxialHeight) < 1e-5 * Math.Abs(paraxialHeight),
            $"chief ray at {chief.Y:E9}, paraxial image at {paraxialHeight:E9}");
    }

    /// <summary>
    /// The Seidel distortion of a flat face in collimated light.
    ///
    /// <para>There the marginal ray meets the face at normal incidence, A = 0, and the usual
    /// S5 = (Abar/A)(S3 + S4) is 0/0. SeidelCoefficients used to set that face's S5 to zero -
    /// silently, since S4 is zero on a flat and nothing was flagged - and on
    /// <c>Ladder2_FlatPlain</c> that dropped a contribution of +1.06E-3 and gave a total of
    /// -5.336E-4 where the right one is +5.267E-4. It now takes S5 in the form with the A divided
    /// out (see the calculator), which is finite everywhere.</para>
    ///
    /// <para>What says +5.267E-4 is right owes nothing to the formula. S5 and Buchdahl's E are
    /// the same aberration in two normalisations, S5 = 2 E n'u', and E is confirmed on the flat
    /// design by its own real rays, identical to its value with the face bent to R = 1E10. So S5
    /// must be the bent design's, and the identity must hold on the flat as it does elsewhere.
    /// (Optiland, whose distortion has no 1/A in it, found the discrepancy; it is not the
    /// evidence.)</para>
    /// </summary>
    [Fact]
    public void SeidelDistortionOfAFlatFaceInCollimatedLight()
    {
        var catalog = CatalogLocator.LoadBundled();
        double Ratio(Loaded l, out double s5, out double eRays)
        {
            var s = SeidelCoefficients.Compute(l.System, l.Indices, l.Indices, l.Indices, l.Paraxial);
            var b = BuchdahlCoefficients.Compute(l.System, l.Paraxial).Totals;
            eRays = CoefficientInversion.InvertThirdAndFifth(l.System, l.Indices, l.Paraxial, l.Field)!.Terms.E;
            int last = l.System.LastOpticalSurface();
            s5 = s.TotalS5;
            return s5 / (b.E * l.Paraxial.N[last] * l.Paraxial.U[last]);
        }

        var near = Load("Ladder2_FlatPlain_NearLimit.lhlt", "lenses", catalog);
        var flat = Load("Ladder2_FlatPlain.lhlt", "lenses", catalog);
        double ratioNear = Ratio(near, out double s5Near, out double eNear);
        double ratioFlat = Ratio(flat, out double s5Flat, out double eFlat);
        _out.WriteLine($"S5: flat {s5Flat:E9}, R = 1E10 {s5Near:E9}; S5/(E n'u') {ratioFlat:F9} and {ratioNear:F9}");

        Assert.True(Math.Abs(eFlat - eNear) < 1e-6 * Math.Abs(eNear), $"E from rays: {eFlat:E9} vs {eNear:E9}");
        Assert.Equal(2.0, ratioNear, 6);
        Assert.Equal(2.0, ratioFlat, 6);
        Assert.True(Math.Abs(s5Flat - s5Near) < 1e-6 * Math.Abs(s5Near), $"S5 {s5Flat:E9} vs {s5Near:E9}");
    }

    /// <summary>
    /// The form of S5 with the A divided out is the same sum as the quotient, and is checked as
    /// such wherever the quotient is defined: every refracting surface of every design on disk.
    /// The algebra that turns one into the other is in the calculator; this is its gate.
    /// </summary>
    [Fact]
    public void TheDistortionFormWithoutOneOverAIsTheQuotient()
    {
        var catalog = CatalogLocator.LoadBundled();
        int surfaces = 0;
        foreach (var d in Designs.All())
        {
            var l = Load((string)d[0], (string)d[1], catalog);
            if (l.Field == 0.0) continue;
            var p = l.Paraxial;
            var seidel = SeidelCoefficients.Compute(l.System, l.Indices, l.Indices, l.Indices, p);
            // Scaled by the largest contribution of ANY of the five sums: where distortion is
            // itself zero - a mirror with the stop on it - its own largest is roundoff.
            double largest = 1e-30;
            foreach (var column in new[] { seidel.S1, seidel.S2, seidel.S3, seidel.S4, seidel.S5 })
                largest = Math.Max(largest, column.Max(Math.Abs));
            for (int j = 1; j <= l.System.LastOpticalSurface(); j++)
            {
                double n = p.N[j - 1], n1 = p.N[j];
                if (n == n1 || Math.Abs(n) < 1e-15 || Math.Abs(n1) < 1e-15) continue;
                double c = l.System.Surfaces[j].VertexCurvature;
                double y = p.Y[j], yb = p.Ybar[j];
                double A = n * (y * c + p.U[j - 1]), Ab = n * (yb * c + p.Ubar[j - 1]);
                if (Math.Abs(A) < 1e-12) continue;
                double closed = -Ab * Ab * Ab * y * (1 / (n1 * n1) - 1 / (n * n))
                              + Ab * yb * c * (2 * Ab * y - A * yb) * (1 / n1 - 1 / n);
                double quotient = seidel.S5[j] - seidel.S5Aspheric[j];
                Assert.True(Math.Abs(closed - quotient) < 1e-10 * largest,
                    $"{l.Name} surface {j}: {closed:E12} against {quotient:E12}");
                surfaces++;
            }
        }
        _out.WriteLine($"{surfaces} surfaces");
        Assert.True(surfaces > 100, $"only {surfaces} surfaces compared");
    }
}
