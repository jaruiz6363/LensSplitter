using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The best-focus plane predicted from the coefficients - the quantity Robb's spot cannot see.
///
/// <para><b>What is being checked, and why it is not a transcription check.</b> Sands (1973)
/// gives the focal shift as combinations of his own coefficients in his own normalisation, and
/// nothing here is copied from him. The shift is derived instead from Robb's polynomial in this
/// program's convention, so the tests have to establish two separate things: that the derivation
/// is arithmetically what it claims - a quadratic in the plane shift whose minimum is written
/// down rather than searched for - and that the plane it names is the plane where the spot is
/// actually smallest. The first is checked against a numerically sampled pupil, the second
/// against real traced rays.</para>
///
/// <para><b>And one published number is available.</b> For third-order spherical alone, Sands
/// states the minimum-radius-of-gyration plane as <c>-(2 sigma1/3 va')p0^2</c>, and remarks that
/// it is NOT the disk of least confusion at <c>-(3 sigma1/4 va')p0^2</c>. Both fall out of the
/// construction here, so the ratio 8/9 between them is an external check on a derivation that
/// otherwise has none.</para>
/// </summary>
[Collection(ProcessWideState.Name)]
public class BestFocusTests
{
    private sealed record Setup(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                                BuchdahlTerms Totals, double Field, double U);

    private static Setup Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        int primary = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[primary].Value,
                                    new List<string>());

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return new Setup(sys, n, p, b.Totals, field, p.U[sys.LastOpticalSurface()]);
    }

    // ── The one published number ────────────────────────────────────────────────────────

    /// <summary>
    /// Third-order spherical alone. Sands puts the minimum-gyration plane at
    /// <c>-(2 sigma1/3 va')</c> at unit pupil radius, and this program's <c>B</c> is his
    /// <c>sigma1</c> - the correspondence Robb's Eq. (2) fixes and <c>Prms</c> records.
    ///
    /// <para>This is the only external check the construction has, and it is worth stating what
    /// it covers: the radial average, the azimuthal average, the factor of two from minimising a
    /// quadratic, and the sign. It does not cover any coefficient but the first.</para>
    /// </summary>
    [Fact]
    public void ThirdOrderSphericalReproducesSandsPublishedPlane()
    {
        var totals = new BuchdahlTerms { B = 0.0125 };
        const double u = -0.1;

        double predicted = BestFocus.DeltaZ(totals, 0.0, u);
        double sands = -2.0 * totals.B / (3.0 * u);

        Assert.Equal(sands, predicted, 12);
    }

    /// <summary>
    /// And it is NOT the disk of least confusion, which Sands is explicit about. The two planes
    /// stand in the ratio 8/9, and a program that quietly returned one while documenting the
    /// other would be wrong by eleven per cent of the focal shift on every design with spherical
    /// aberration.
    /// </summary>
    [Fact]
    public void BestFocusIsNotTheDiskOfLeastConfusion()
    {
        var totals = new BuchdahlTerms { B = 0.0125 };
        const double u = -0.1;

        double gyration = BestFocus.DeltaZ(totals, 0.0, u);
        double leastConfusion = -3.0 * totals.B / (4.0 * u);

        Assert.Equal(8.0 / 9.0, gyration / leastConfusion, 12);
        Assert.NotEqual(gyration, leastConfusion, 6);
    }

    // ── The quadratic is the real one ───────────────────────────────────────────────────

    /// <summary>
    /// The analytic mean square at a shifted plane, against the same thing computed by sampling
    /// the pupil - Robb's polynomial evaluated ray by ray, displaced by <c>dZ u rho</c>, and its
    /// variance about its own centroid taken numerically.
    ///
    /// <para>This is the test that earns the closed form. If the radial average, the azimuthal
    /// average or the centroid handling of the cross term were wrong, the two would part company
    /// at some shift even though both would still be smooth functions of it - so the check is
    /// made at several shifts either side of zero rather than at the minimum, where an error in
    /// the linear term is hardest to see.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet", 0.0)]
    [InlineData("CookeTriplet", 0.7)]
    [InlineData("CookeTriplet", 1.0)]
    [InlineData("Ladder1_Conic", 1.0)]
    public void TheClosedFormMatchesASampledPupil(string fixture, double h)
    {
        var s = Load(fixture);
        double scale = Math.Abs(BestFocus.DeltaZ(s.Totals, h, s.U));
        if (scale < 1e-9) scale = 0.05;

        foreach (double dz in new[] { -2.0 * scale, -scale, 0.0, scale, 2.0 * scale })
        {
            double sampled = SampledMeanSquare(s.Totals, h, dz, s.U);
            double closed = Prms.MeanSquareDefocused(s.Totals, h, dz, s.U);

            double tol = 1e-9 * Math.Max(sampled, 1e-12);
            Assert.True(Math.Abs(sampled - closed) <= tol,
                $"{fixture} at h = {h}, dZ = {dz:G6}: sampled {sampled:E12} against closed form "
              + $"{closed:E12}. The pupil averages in Prms.DefocusCoupling do not describe the "
              + "polynomial they were built from.");
        }
    }

    /// <summary>
    /// The plane the closed form names is the plane the sampled spot is actually smallest at.
    /// Scanned rather than asserted: fifty planes across four times the predicted shift, and the
    /// smallest sample must be the one bracketing the prediction.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet", 0.0)]
    [InlineData("CookeTriplet", 1.0)]
    [InlineData("Ladder1_Sphere", 0.0)]
    public void TheNamedPlaneIsWhereTheSampledSpotIsSmallest(string fixture, double h)
    {
        var s = Load(fixture);
        double dz = BestFocus.DeltaZ(s.Totals, h, s.U);
        double span = Math.Max(Math.Abs(dz) * 4.0, 1e-6);

        double best = double.MaxValue, bestAt = 0.0;
        for (int i = 0; i <= 200; i++)
        {
            double z = -span + 2.0 * span * i / 200.0;
            double ms = SampledMeanSquare(s.Totals, h, z, s.U);
            if (ms < best) { best = ms; bestAt = z; }
        }

        double step = 2.0 * span / 200.0;
        Assert.True(Math.Abs(bestAt - dz) <= step,
            $"{fixture} at h = {h}: the scan put the smallest spot at dZ = {bestAt:G6} while the "
          + $"closed form names {dz:G6}, more than one scan step ({step:G6}) away.");
    }

    /// <summary>
    /// The minimum mean square is <c>M - 2L^2</c>, which is worth pinning for its own sake: it
    /// says the spot at best focus is the spot at the paraxial plane less a quantity that cannot
    /// be negative, so refocusing never makes a design worse and the improvement is exactly the
    /// square of the coupling.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("Ladder1_Conic")]
    [InlineData("KingslakeDG")]
    public void RefocusingNeverMakesItWorseAndTheGainIsTheCouplingSquared(string fixture)
    {
        var s = Load(fixture);
        foreach (double h in new[] { 0.0, 0.5, 1.0 })
        {
            double dz = BestFocus.DeltaZ(s.Totals, h, s.U);
            double atParaxial = Prms.MeanSquare(s.Totals, h);
            double atBest = Prms.MeanSquareDefocused(s.Totals, h, dz, s.U);
            double coupling = Prms.DefocusCoupling(s.Totals, h);

            Assert.True(atBest <= atParaxial + 1e-18,
                $"{fixture} at h = {h}: refocusing made the spot larger, {atBest:E6} against "
              + $"{atParaxial:E6}.");
            Assert.Equal(atParaxial - 2.0 * coupling * coupling, atBest, 15);
        }
    }

    // ── Against real rays ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The predicted plane against real traced rays, on axis, where the prediction rests on
    /// <c>B</c> and <c>B5</c> and <c>B7</c> alone and nothing else can be compensating.
    ///
    /// <para><b>This is a measurement and is toleranced as one.</b> Two things stand between the
    /// prediction and the rays and neither is a fault: the coefficients are a seventh-order
    /// truncation, and the plane shift is applied to the rays exactly while the prediction
    /// applies it paraxially - Sands's Eq. (9). The agreement asked for here is ten per cent of
    /// the shift, which is far inside the gap between this plane and the disk of least
    /// confusion, so the test still distinguishes the criterion it implements from the one it
    /// does not.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Sphere")]
    public void TheAxialPlaneAgreesWithTracedRays(string fixture)
    {
        var s = Load(fixture);
        double predicted = BestFocus.DeltaZ(s.Totals, 0.0, s.U);

        // Trace a spoked pupil once, keeping each ray's arrival point and direction, then walk
        // the plane. The rays are traced exactly; only the propagation to the shifted plane is
        // done here, and that is geometry rather than a model.
        var hits = new List<(double X, double Y, double L, double M, double N)>();
        for (int r = 1; r <= 8; r++)
            for (int a = 0; a < 16; a++)
            {
                double rho = Math.Sqrt(r / 8.0);          // equal-area rings
                double th = 2.0 * Math.PI * a / 16.0;
                var rec = RealRayTrace.TraceRecord(s.System, s.Indices, s.Paraxial, 0.0,
                                                   rho * Math.Cos(th), rho * Math.Sin(th));
                var last = rec[rec.Length - 1];
                if (!last.Ok) continue;
                hits.Add((last.X, last.Y, last.L, last.M, last.N));
            }
        Assert.True(hits.Count > 100, $"{fixture}: too few rays survived to measure anything.");

        double span = Math.Abs(predicted) * 3.0 + 1e-9;
        double best = double.MaxValue, bestAt = 0.0;
        for (int i = 0; i <= 600; i++)
        {
            double z = -span + 2.0 * span * i / 600.0;
            double sx = 0, sy = 0, sxx = 0;
            foreach (var p in hits)
            {
                double x = p.X + z * p.L / p.N;
                double y = p.Y + z * p.M / p.N;
                sx += x; sy += y; sxx += x * x + y * y;
            }
            int n = hits.Count;
            double ms = sxx / n - (sx / n) * (sx / n) - (sy / n) * (sy / n);
            if (ms < best) { best = ms; bestAt = z; }
        }

        double tol = 0.1 * Math.Abs(predicted) + 1e-6;
        Assert.True(Math.Abs(bestAt - predicted) <= tol,
            $"{fixture}: rays put best focus at dZ = {bestAt:G6}, the coefficients predict "
          + $"{predicted:G6}, a difference of {Math.Abs(bestAt - predicted):G6} against a "
          + $"tolerance of {tol:G6}.");
    }

    // ── Per wavelength ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Each wavelength gets its own paraxial focus, its own marginal slope, its own coefficients
    /// and therefore its own plane. On a lens with three colours the three back focal lengths
    /// must differ - that is longitudinal chromatic aberration and it is what makes a single
    /// best-focus answer meaningless - and the shift each colour wants FROM ITS OWN plane must
    /// differ too.
    ///
    /// <para>The trap this guards is the one the report has fallen into before: computing the
    /// coefficients at the primary wavelength and quoting them against every colour's paraxial
    /// data. Here the totals are rebuilt per colour, and if they were not, all three rows would
    /// carry the same dZ.</para>
    /// </summary>
    [Fact]
    public void EachWavelengthGetsItsOwnPlaneAndItsOwnBfl()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        Assert.True(sys.Wavelengths.Count >= 3, "fixture no longer has three wavelengths");

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var per = new List<(int, double, double, bool, ParaxialResult, BuchdahlTerms)>();
        for (int wi = 0; wi < sys.Wavelengths.Count; wi++)
        {
            var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[wi].Value,
                                        new List<string>());
            var p = ParaxialTrace.Trace(sys, n, field);
            var b = BuchdahlCoefficients.Compute(sys, p);
            TertiaryCoefficients.Attach(sys, n, p, b, field);
            per.Add((wi, sys.Wavelengths[wi].Value, sys.Wavelengths[wi].Weight,
                     sys.Wavelengths[wi].IsPrimary, p, b.Totals));
        }

        var fields = new List<(double, double)> { (0.0, 1.0), (0.7, 1.0), (1.0, 1.0) };
        var rows = BestFocus.ForSystem(per, fields, sys.LastOpticalSurface());

        Assert.Equal(sys.Wavelengths.Count, rows.Count);

        // Longitudinal colour: the paraxial planes themselves are not the same.
        for (int i = 1; i < rows.Count; i++)
            Assert.True(Math.Abs(rows[i].Bfl - rows[0].Bfl) > 1e-6,
                $"wavelengths {rows[0].Value} and {rows[i].Value} report the same BFL, so the "
              + "per-colour indices are not reaching the paraxial trace.");

        // And the shift each colour wants from its own plane is its own number.
        for (int i = 1; i < rows.Count; i++)
            Assert.True(Math.Abs(rows[i].Fields[0].DeltaZ - rows[0].Fields[0].DeltaZ) > 1e-9,
                $"wavelengths {rows[0].Value} and {rows[i].Value} want the same dZ, so the "
              + "coefficients are not being rebuilt per colour.");

        foreach (var r in rows)
        {
            Assert.Equal(3, r.Fields.Count);
            foreach (var f in r.Fields)
                Assert.True(f.RmsBestFocus <= f.RmsParaxial + 1e-12,
                    $"{r.Value} at h = {f.H}: best focus is not better than paraxial.");
        }
    }

    /// <summary>
    /// The whole-field plane is the weighted compromise and not one of the per-field answers: it
    /// must lie inside their range, and weighting the axis out must move it.
    /// </summary>
    [Fact]
    public void TheWholeFieldPlaneIsACompromiseAndRespectsTheWeights()
    {
        var s = Load("CookeTriplet");
        var hs = new List<double> { 0.0, 0.7, 1.0 };

        var equal = new List<double> { 1.0, 1.0, 1.0 };
        double compromise = BestFocus.DeltaZWholeField(s.Totals, hs, equal, s.U);

        double lo = double.MaxValue, hi = double.MinValue;
        foreach (double h in hs)
        {
            double d = BestFocus.DeltaZ(s.Totals, h, s.U);
            lo = Math.Min(lo, d); hi = Math.Max(hi, d);
        }
        Assert.InRange(compromise, lo, hi);

        // Sands's own second example weights the axial image out of the average deliberately.
        var offAxisOnly = new List<double> { 0.0, 1.0, 1.0 };
        double weighted = BestFocus.DeltaZWholeField(s.Totals, hs, offAxisOnly, s.U);
        Assert.True(Math.Abs(weighted - compromise) > 1e-9,
            "dropping the axial field changed nothing, so the weights are not being used.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The mean square radius about the centroid, by sampling Robb's polynomial over the pupil
    /// and displacing each ray by <c>dZ u rho</c>.
    ///
    /// <para><b>The quadrature is exact, not merely fine.</b> An equal-area ring scheme was tried
    /// first and disagreed with the closed form in the fifth figure - the midpoint rule's own
    /// error, not the formula's, but a discrepancy that has to be argued away rather than seen to
    /// be absent. Instead: the radial integral carries weight <c>2 rho drho</c> over an integrand
    /// that is a polynomial in <c>rho</c> of degree at most fifteen, so twelve Gauss-Legendre
    /// nodes integrate it exactly; and the azimuthal integrand is a trigonometric polynomial of
    /// degree at most eight, so equally spaced samples are spectrally exact well below the
    /// sixty-four used. What is left is roundoff, which is what the tolerance in the test is
    /// then allowed to be.</para>
    /// </summary>
    private static double SampledMeanSquare(BuchdahlTerms totals, double h, double dz, double u)
    {
        const int spokes = 64;
        var (nodes, weights) = GaussLegendreUnitInterval(12);
        double d = dz * u;
        double sx = 0, sy = 0, sq = 0;

        for (int r = 0; r < nodes.Length; r++)
        {
            double rho = nodes[r];
            double w = weights[r] * 2.0 * rho;          // the disc's own weight, integrating to 1
            for (int a = 0; a < spokes; a++)
            {
                double th = 2.0 * Math.PI * a / spokes;
                var (ey, ez) = Prms.Transverse(totals, rho, th, h);
                double y = ey + d * rho * Math.Cos(th);
                double z = ez + d * rho * Math.Sin(th);
                double ww = w / spokes;
                sx += ww * y; sy += ww * z; sq += ww * (y * y + z * z);
            }
        }
        return sq - sx * sx - sy * sy;
    }

    /// <summary>Gauss-Legendre nodes and weights on [0, 1], by Newton on the Legendre polynomial.</summary>
    private static (double[] Nodes, double[] Weights) GaussLegendreUnitInterval(int n)
    {
        var x = new double[n];
        var w = new double[n];
        for (int i = 0; i < n; i++)
        {
            // Chebyshev start, then Newton: the Legendre roots are close enough for it to
            // converge in a handful of steps and the derivative comes from the same recurrence.
            double t = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5));
            for (int it = 0; it < 100; it++)
            {
                double p0 = 1.0, p1 = 0.0;
                for (int k = 0; k < n; k++)
                {
                    double p2 = p1; p1 = p0;
                    p0 = ((2 * k + 1) * t * p1 - k * p2) / (k + 1);
                }
                double dp = n * (t * p0 - p1) / (t * t - 1.0);
                double dt = -p0 / dp;
                t += dt;
                if (Math.Abs(dt) < 1e-16) break;
            }
            double p0f = 1.0, p1f = 0.0;
            for (int k = 0; k < n; k++)
            {
                double p2 = p1f; p1f = p0f;
                p0f = ((2 * k + 1) * t * p1f - k * p2) / (k + 1);
            }
            double dpf = n * (t * p0f - p1f) / (t * t - 1.0);

            x[i] = 0.5 * (1.0 - t);                      // map [-1, 1] onto [0, 1]
            w[i] = 1.0 / ((1.0 - t * t) * dpf * dpf);
        }
        return (x, w);
    }
}
