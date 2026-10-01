using System;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.Nat;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The computing scheme in Buchdahl's W coordinates, against his own printed numbers.
///
/// <para>Paper VI, <i>J. Opt. Soc. Am.</i> <b>50</b>, 534 (1960), Sec. 5 says W coordinates
/// "lead to the modification of 39 of the 192 rows of Table I of III", and its Table I, p.536,
/// prints them. Its <b>Table II</b>, p.538, then gives the triplet Sigma1's aberration
/// coefficients in BOTH coordinate systems - which makes it an oracle for the patch and for the
/// unpatched baseline at the same time.</para>
///
/// <para>Why bother: VII Eq. (6.6), which produces the wave front coefficients nodal aberration
/// theory needs, is stated for W coordinates. The primary coefficients are identical in the two
/// systems, so third-order work never had to care; the secondary ones differ by up to six per
/// cent, which is too large to neglect and too small to notice as an error.</para>
/// </summary>
public class WCoordinateSchemeTests
{
    /// <summary>Buchdahl's Sigma1, paper III p.753, as <see cref="BuchdahlPublishedTableTests"/>
    /// builds it: object at infinity, focal length unity, entrance pupil at p = 0.113227.</summary>
    private static OpticalSystem Triplet()
    {
        double[] c = { 4.82439, -0.753929, -1.64505, 5.11794, 0.310726, -1.46116 };
        double[] d = { 0.040278, 0.016851, 0.0096145, 0.138738, 0.0313246, 0.0 };
        double[] nAfter = { 1.61620, 1.0, 1.57250, 1.0, 1.61620, 1.0 };

        var sys = new OpticalSystem();
        sys.Surfaces.Add(new Surface { Radius = double.PositiveInfinity,
                                       Thickness = double.PositiveInfinity });
        for (int i = 0; i < 6; i++)
            sys.Surfaces.Add(new Surface
            {
                Radius = 1.0 / c[i],
                Thickness = d[i],
                Material = Math.Abs(nAfter[i] - 1.0) < 1e-9 ? "" : nAfter[i].ToString("R"),
                SemiDiameterMode = SemiDiameterMode.Auto,
            });
        sys.Surfaces.Add(new Surface { Radius = double.PositiveInfinity, Thickness = 0 });
        sys.Aperture = new Aperture(ApertureType.EPD, 0.16);
        sys.Fields.Add(new Field(0.0));
        sys.Fields.Add(new Field(11.3));
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        return sys;
    }

    private static readonly double[] Indices =
        { 1.0, 1.61620, 1.0, 1.57250, 1.0, 1.61620, 1.0, 1.0 };

    private static BuchdahlTableIRow[] Rows(bool w)
    {
        var sys = Triplet();
        var p = ParaxialTrace.Trace(sys, Indices, 11.3);
        return BuchdahlTableI.Compute(sys.Surfaces, Indices, p.Efl, 0.113227, null, false,
                                      default, w);
    }

    /// <summary>
    /// The system coefficients are sums of the per-surface INCREMENTS. Summing the running sums
    /// t15..t19 instead would double count, and it looks orderly while being wrong, so the row
    /// numbers are named here once.
    /// </summary>
    private static double Total(BuchdahlTableIRow[] r, int row)
    {
        double a = 0;
        for (int j = 1; j <= 6; j++) a += r[j].T[row];
        return a;
    }

    // Buchdahl VI Table II. Row number in the scheme, then the OT* and W columns.
    //
    // The tolerance below is 5e-3 relative with a 1e-3 absolute floor, which is what Table II's
    // printed precision allows - several entries carry only three decimals. Exactly one entry
    // needs the floor: S5b comes out 0.16014 against a printed 0.161. It misses in BOTH columns
    // by the same amount (W: 0.1455 against 0.146), so that is a property of the BASELINE scheme
    // and not of the W patch - and the baseline is checked against paper III Table I entry by
    // entry in BuchdahlPublishedTableTests. Every other coefficient agrees to about 0.01 per cent.
    private static readonly (string name, int row, double ot, double w)[] TableII =
    {
        ("A",   10,   1.3591,   1.3591),   ("Ab",  11, -0.01468, -0.01468),
        ("B",   12,  -0.03176, -0.03176),  ("C",   13,  0.15420,  0.15420),
        ("Cb",  14,  -0.01906, -0.01906),
        ("S1",  41, -90.923,  -93.021),    ("S1b", 42, -23.173,  -23.650),
        ("S2",  46, -92.137,  -97.430),    ("S2b", 47, -11.869,  -11.942),
        ("S3",  52,  -8.7593,  -9.0392),   ("S3b", 53,   0.7135,   0.7539),
        ("S4",  56, -13.187,  -13.406),    ("S4b", 57,   0.5479,   0.5879),
        ("S5",  62,   1.498,    1.506),    ("S5b", 63,   0.161,    0.146),
        ("S6",  67,  -0.3760,  -0.3859),   ("S6b", 68,  -0.0644,  -0.0569),
    };

    /// <summary>
    /// The unpatched scheme against VI Table II's OT* column. This is the baseline the patch is
    /// measured from, and checking it first is what makes the W comparison meaningful rather
    /// than a coincidence of two errors.
    /// </summary>
    [Fact]
    public void TheParacanonicalSchemeReproducesTableIIOTColumn()
    {
        var r = Rows(w: false);
        foreach (var (name, row, ot, _) in TableII)
            Assert.True(Math.Abs(Total(r, row) - ot) <= 5e-3 * Math.Abs(ot) + 1e-3,
                $"{name}: got {Total(r, row)}, VI Table II OT* has {ot}");
    }

    /// <summary>
    /// The patched scheme against VI Table II's W column - all seventeen coefficients.
    /// </summary>
    [Fact]
    public void TheWCoordinateSchemeReproducesTableIIWColumn()
    {
        var r = Rows(w: true);
        foreach (var (name, row, _, w) in TableII)
            Assert.True(Math.Abs(Total(r, row) - w) <= 5e-3 * Math.Abs(w) + 1e-3,
                $"{name}: got {Total(r, row)}, VI Table II W has {w}");
    }

    /// <summary>
    /// The primary coefficients are the SAME in both coordinate systems - VI Eq. (6.6) is
    /// <c>Aa = Ap</c> and so on, and Table II prints them identically in both columns. This is
    /// why no third-order result in this program is disturbed by any of the above, and it is
    /// asserted separately so that a regression there cannot hide inside a loose tolerance on
    /// the secondary set.
    /// </summary>
    [Fact]
    public void ThePrimaryCoefficientsAreUnchangedByTheCoordinateSystem()
    {
        var para = Rows(w: false);
        var w = Rows(w: true);
        foreach (int row in new[] { 10, 11, 12, 13, 14 })
            Assert.Equal(Total(para, row), Total(w, row), 10);
    }

    /// <summary>
    /// VI (4.17), the secondary identities in W coordinates, with the powers of <c>e</c> that
    /// Table II's own footnote insists on. These are three of the ten identities VII Sec. 6(b)
    /// refers to, and they are what localised the one row that was wrong: (4.17a) and (4.17c)
    /// were already clean while (4.17b) was not, which pointed at S4 alone.
    ///
    /// <para>They hold on Buchdahl's published W column to about 1e-4, so that is the standard
    /// to meet rather than machine precision.</para>
    /// </summary>
    [Fact]
    public void TheSecondaryIdentitiesOfVI417Hold()
    {
        var r = Rows(w: true);
        double e = Math.Pow(Total(r, 10) / (4.0 * 0.38571), 0.25);

        // Sec. 7(a) powers: A:-1, B:0, C:1, S1:-1, S2:0, S3,S4:1, S5:2, S6:3; barred one more.
        double A = Total(r, 10) / e, Ab = Total(r, 11), Bb = Total(r, 12) * e;
        double S1b = Total(r, 42), S2 = Total(r, 46), S2b = Total(r, 47) * e;
        double S3b = Total(r, 53) * e * e, S4 = Total(r, 56) * e, S5 = Total(r, 62) * e * e;

        Assert.True(Math.Abs((S2 - 4 * S1b) - 2 * (Ab - A)) < 2e-3, "VI (4.17a)");
        Assert.True(Math.Abs((S4 - S2b) - (-A + Bb / 2)) < 2e-3, "VI (4.17b)");
        Assert.True(Math.Abs((S5 - 2 * S3b) - (-2 * Ab + Bb)) < 2e-3, "VI (4.17c)");
    }

    /// <summary>
    /// The whole chain, end to end: a lens prescription through the W-coordinate scheme, through
    /// VII Eqs. (6.5-6), through Eq. (3.4), landing on VII Table I - the published deformation
    /// and retardation coefficients, which are Thompson's nine fifth-order <c>Wklm</c>.
    ///
    /// <para>No ray trace, no fit, no second program. <c>e</c> is not published, so it is solved
    /// from <c>pi1</c> and the remaining thirteen are free checks on one parameter.</para>
    /// </summary>
    [Fact]
    public void TheChainFromAPrescriptionReachesVIITableI()
    {
        var r = Rows(w: true);
        double A = Total(r, 10), Ab = Total(r, 11), Bb = Total(r, 12);
        double C = Total(r, 13), Cb = Total(r, 14);
        double e = Math.Pow(A / (4.0 * 0.38571), 0.25);
        double p3 = Math.Pow(e, -3), p5 = Math.Pow(e, -5);

        var raw = Deformation.FromWCoordinates(
            A / e, Ab, Bb * e, C * e, Cb * e * e,
            Total(r, 41) / e, Total(r, 42), Total(r, 52) * e, Total(r, 56) * e,
            Total(r, 57) * e * e, Total(r, 62) * e * e, Total(r, 63) * e * e * e,
            Total(r, 67) * e * e * e, Total(r, 68) * e * e * e * e);
        var d = new Deformation(
            raw.Pi1 * p3, raw.Pi2 * p3, raw.Pi3 * p3, raw.Pi4 * p3, raw.Pi5 * p3,
            raw.Sigma1 * p5, raw.Sigma2 * p5, raw.Sigma3 * p5, raw.Sigma4 * p5, raw.Sigma5 * p5,
            raw.Sigma6 * p5, raw.Sigma7 * p5, raw.Sigma8 * p5, raw.Sigma9 * p5);
        var rr = d.ToRetardation(e);

        double[] piI = { 0.38571, -0.016143, 0.082148, -0.016921, -0.019674 };
        double[] sD = { -18.34, -26.905, -2.547, -5.9997, 0.8986, -0.20577, 0.1806, 0.1471, -0.05877 };
        double[] sR = { -18.34, -26.905, -2.752, -5.9997, 0.9072, -0.24933, 0.1806, 0.1561, -0.04829 };

        double[] gotPi = { d.Pi1, d.Pi2, d.Pi3, d.Pi4, d.Pi5 };
        double[] gotD = { d.Sigma1, d.Sigma2, d.Sigma3, d.Sigma4, d.Sigma5,
                          d.Sigma6, d.Sigma7, d.Sigma8, d.Sigma9 };
        double[] gotR = { rr.Sigma1, rr.Sigma2, rr.Sigma3, rr.Sigma4, rr.Sigma5,
                          rr.Sigma6, rr.Sigma7, rr.Sigma8, rr.Sigma9 };

        for (int i = 1; i < 5; i++)      // pi1 fixed e, so it is not a check
            Assert.True(Math.Abs(gotPi[i] - piI[i]) <= 3e-3 * Math.Abs(piI[i]) + 1e-8,
                $"pi{i + 1}: got {gotPi[i]}, VII Table I has {piI[i]}");

        for (int i = 0; i < 9; i++)
        {
            Assert.True(Math.Abs(gotD[i] - sD[i]) <= 3e-3 * Math.Abs(sD[i]) + 5e-5,
                $"sigma{i + 1} (D): got {gotD[i]}, VII Table I has {sD[i]}");
            Assert.True(Math.Abs(gotR[i] - sR[i]) <= 3e-3 * Math.Abs(sR[i]) + 5e-5,
                $"'sigma{i + 1} (R): got {gotR[i]}, VII Table I has {sR[i]}");
        }

        // And the named accessors are the point of the exercise.
        Assert.Equal((double)rr.Sigma1, (double)rr.W060, 12);
        Assert.Equal((double)rr.Sigma6, (double)rr.W420, 12);
        Assert.Equal((double)rr.Sigma7, (double)rr.W333, 12);
    }

    /// <summary>
    /// <c>e</c> computed from the rays against <c>e</c> implied by VII Table I.
    ///
    /// <para>Buchdahl never published the value, and every check above solved it from
    /// <c>pi1</c> - which costs one of the fourteen coefficients as a check. Computing it
    /// instead from where the two paraxial rays reach the axis gives 0.968799 against the
    /// 0.968801 his table implies, six significant figures, and hands the fitted coefficient
    /// back.</para>
    /// </summary>
    [Fact]
    public void TheExitPupilDistanceAgreesWithTheOneTableIImplies()
    {
        var r = Rows(w: true);
        double fromTable = Math.Pow(Total(r, 10) / (4.0 * 0.38571), 0.25);
        double computed = WaveFront.ExitPupilDistance(r[6]);
        Assert.Equal(fromTable, computed, 5);
    }

    /// <summary>
    /// The same chain as <see cref="TheChainFromAPrescriptionReachesVIITableI"/> but through
    /// <see cref="WaveFront"/>, which computes <c>e</c> instead of fitting it. Every one of the
    /// fourteen coefficients is then a free check with nothing tuned anywhere.
    /// </summary>
    [Fact]
    public void TheWaveFrontChainReachesVIITableIWithNothingFitted()
    {
        var wf = WaveFront.FromScheme(Rows(w: true), 6);

        double[] piI = { 0.38571, -0.016143, 0.082148, -0.016921, -0.019674 };
        double[] sD = { -18.34, -26.905, -2.547, -5.9997, 0.8986, -0.20577, 0.1806, 0.1471, -0.05877 };
        double[] sR = { -18.34, -26.905, -2.752, -5.9997, 0.9072, -0.24933, 0.1806, 0.1561, -0.04829 };

        var d = wf.SystemDeformation;
        var rr = wf.System;
        double[] gotPi = { d.Pi1, d.Pi2, d.Pi3, d.Pi4, d.Pi5 };
        double[] gotD = { d.Sigma1, d.Sigma2, d.Sigma3, d.Sigma4, d.Sigma5,
                          d.Sigma6, d.Sigma7, d.Sigma8, d.Sigma9 };
        double[] gotR = { rr.Sigma1, rr.Sigma2, rr.Sigma3, rr.Sigma4, rr.Sigma5,
                          rr.Sigma6, rr.Sigma7, rr.Sigma8, rr.Sigma9 };

        for (int i = 0; i < 5; i++)
            Assert.True(Math.Abs(gotPi[i] - piI[i]) <= 3e-3 * Math.Abs(piI[i]) + 1e-8,
                $"pi{i + 1}: got {gotPi[i]}, VII Table I has {piI[i]}");
        for (int i = 0; i < 9; i++)
        {
            Assert.True(Math.Abs(gotD[i] - sD[i]) <= 3e-3 * Math.Abs(sD[i]) + 5e-5,
                $"sigma{i + 1} (D): got {gotD[i]}, VII Table I has {sD[i]}");
            Assert.True(Math.Abs(gotR[i] - sR[i]) <= 3e-3 * Math.Abs(sR[i]) + 5e-5,
                $"'sigma{i + 1} (R): got {gotR[i]}, VII Table I has {sR[i]}");
        }
    }

    /// <summary>
    /// The per-surface split adds up to the system, to machine precision.
    ///
    /// <para>It has to: every step from the scheme's per-surface contributions to the retardation
    /// coefficients is linear. This asserts it anyway, because the claim that the wave front is
    /// additive over surfaces - unlike the transverse coefficients, which need induced terms - is
    /// the reason NAT can use this decomposition at all, and a future change that quietly broke
    /// it would otherwise show up only as a wrong node position.</para>
    /// </summary>
    [Fact]
    public void ThePerSurfaceSplitSumsToTheSystem()
    {
        var wf = WaveFront.FromScheme(Rows(w: true), 6);

        double[] sum = new double[14];
        for (int j = 1; j <= 6; j++)
        {
            var s = wf.PerSurface[j];
            sum[0] += s.Pi1; sum[1] += s.Pi2; sum[2] += s.Pi3; sum[3] += s.Pi4; sum[4] += s.Pi5;
            sum[5] += s.Sigma1; sum[6] += s.Sigma2; sum[7] += s.Sigma3; sum[8] += s.Sigma4;
            sum[9] += s.Sigma5; sum[10] += s.Sigma6; sum[11] += s.Sigma7; sum[12] += s.Sigma8;
            sum[13] += s.Sigma9;
        }

        var t = wf.System;
        double[] want = { t.Pi1, t.Pi2, t.Pi3, t.Pi4, t.Pi5, t.Sigma1, t.Sigma2, t.Sigma3,
                          t.Sigma4, t.Sigma5, t.Sigma6, t.Sigma7, t.Sigma8, t.Sigma9 };
        for (int i = 0; i < 14; i++)
            Assert.True(Math.Abs(sum[i] - want[i]) <= 1e-10 * (1.0 + Math.Abs(want[i])),
                $"coefficient {i}: per-surface sum {sum[i]} against system {want[i]}");
    }

    /// <summary>
    /// The individual surfaces are far larger than their sum, which is the whole reason the split
    /// matters. On this triplet the per-surface W060 runs from +113 to -149 and totals -18.34: an
    /// eightfold cancellation. A sigma displacement acts on each contribution separately, so a
    /// misalignment far too small to matter term by term can still move the sum a long way - and
    /// a decomposition that had quietly collapsed to "the system total, apportioned" would lose
    /// exactly that effect while still summing correctly.
    /// </summary>
    [Fact]
    public void TheSurfaceContributionsAreMuchLargerThanTheirSum()
    {
        var wf = WaveFront.FromScheme(Rows(w: true), 6);

        double largest = 0, total = Math.Abs(wf.System.W060);
        for (int j = 1; j <= 6; j++) largest = Math.Max(largest, Math.Abs(wf.PerSurface[j].W060));

        Assert.True(largest > 5.0 * total,
            $"largest per-surface W060 {largest} against system {total}");
    }
}
