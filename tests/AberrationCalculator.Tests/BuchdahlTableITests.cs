using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl's Table I, checked entry by entry against the numbers he printed.
///
/// He works a triplet through the whole scheme in <i>J. Opt. Soc. Am.</i> <b>48</b>, 747
/// (1958) and gives its prescription on p.753, so every intermediate quantity has a
/// published value. That matters more here than anywhere else in this program: the scheme
/// is a chain of a hundred and fifty numbered entries, most of which mean nothing on their
/// own, so a transcription error produces plausible numbers and nothing but the printed
/// column would catch it.
/// </summary>
public class BuchdahlTableITests
{
    /// <summary>
    /// Buchdahl's triplet. He gives curvatures in units of the focal length and separations
    /// against it, so this is built directly in those units with efl = 1.
    ///
    /// His separations are indexed as the gap BEFORE each surface; ours are the gap after,
    /// hence the shift.
    /// </summary>
    private static (List<Surface> Surfaces, double[] Indices) Triplet()
    {
        double[] c = { 0, 4.82439, -0.753929, -1.64505, 5.11794, 0.310726, -1.46116, 0 };
        double[] dBefore = { 0, 0, 0.040278, 0.016851, 0.0096145, 0.138738, 0.0313246, 0.836 };
        double[] n = { 1.0, 1.6162, 1.0, 1.5725, 1.0, 1.6162, 1.0, 1.0 };

        var s = new List<Surface>();
        for (int i = 0; i < c.Length; i++) s.Add(new Surface { Curvature = c[i] });
        for (int i = 0; i + 1 < c.Length; i++) s[i].Thickness = dBefore[i + 1];

        return (s, n);
    }

    /// <summary>
    /// Agreement at the level the published inputs support.
    ///
    /// Buchdahl prints six significant figures and computed on a desk machine; the
    /// prescription itself carries six. So a relative agreement of a few parts in 100,000
    /// is the most that can be asked, and asking for more would fail on his rounding rather
    /// than on our arithmetic. Late entries in the chain accumulate slightly more than
    /// early ones, which is why the tolerance is relative rather than a decimal place.
    /// </summary>
    private static void Near(double published, double computed, double rel = 3e-5)
    {
        double scale = System.Math.Abs(published);
        double tol = scale > 1e-9 ? scale * rel : 1e-9;
        Assert.True(System.Math.Abs(computed - published) <= tol,
            $"published {published}, computed {computed}, differs by "
            + $"{System.Math.Abs(computed - published):G3} which exceeds {tol:G3}");
    }

    private static BuchdahlTableIRow[] Run() =>
        BuchdahlTableI.Compute(Triplet().Surfaces, Triplet().Indices, efl: 1.0,
                               stopParameter: 0.113227);

    /// <summary>
    /// The paraxial entries, surface 1. These are the foundation - if the p and q rays are
    /// wrong nothing downstream can be right, and the failure would look like an error in
    /// whatever is checked next rather than here.
    /// </summary>
    [Fact]
    public void TheParaxialEntriesMatchSurfaceOne()
    {
        var r = Run()[1];

        Near(1.0, r.Yp);
        Near(0.0, r.Vp);
        Near(4.82439, r.Ip);
        Near(0.113227, r.Yq);
        Near(1.0, r.Vq);
        Near(-0.0940533, r.Q);
        Near(1.0, r.J);
        Near(-1.83937, r.Omega);
    }

    /// <summary>
    /// The ray transfer, checked on surface 2. Surface 1 cannot test it - the values there
    /// are the starting conditions. This is also where Buchdahl's separation indexing bites:
    /// his d for surface 2 is the gap between surfaces 1 and 2.
    /// </summary>
    [Fact]
    public void TheRayTransferMatchesSurfaceTwo()
    {
        var r = Run()[2];

        Near(0.925915, r.Yp);
        Near(1.83937, r.Vp);
    }

    /// <summary>
    /// w and the three phi. These are the quantities the whole tertiary chain is built on,
    /// and the only place the leading and trailing asterisks give different answers - w
    /// comes out 4.02313 with the wrong reading and 4.44604 with the right one.
    /// </summary>
    [Fact]
    public void TheIntermediateQuantitiesMatchSurfaceOne()
    {
        var r = Run()[1];

        Near(13.2443, r[10]);      // a_p
        Near(4.44604, r.W);
        Near(0.0, r.Phi1);
        Near(10.5837, r.Phi2);
        Near(24.9846, r.Phi3);
    }

    /// <summary>
    /// w1 through w5, which paper II Eq. (8.3) consumes. Their identification took two
    /// independent routes to settle - the sequence in the table, and the algebra of z7, z9
    /// and z10 - so they are worth pinning explicitly.
    /// </summary>
    [Fact]
    public void TheWQuantitiesMatchSurfaceOne()
    {
        var r = Run()[1];

        Near(24.3611, r.W1);
        Near(6.62214, r.W2);
        Near(1.55577, r.W3);
        Near(1.69164, r.W4);
        Near(-0.229921, r.W5);
    }

    /// <summary>
    /// z1 through z10. Nine of the ten were cross-checked algebraically against paper II's
    /// independent derivation; this checks all ten against the printed column.
    /// </summary>
    [Fact]
    public void TheZQuantitiesMatchSurfaceOne()
    {
        var r = Run()[1];

        Near(2779.35, r.Z(1));
        Near(541.552, r.Z(2));
        Near(153.241, r.Z(3));
        Near(-38.5623, r.Z(4));
        Near(17.9038, r.Z(5));
        Near(-2.04448, r.Z(6));
        Near(-24.3611, r.Z(7));
        Near(8.17790, r.Z(8));
        Near(-1.26873, r.Z(9));
        Near(0.114960, r.Z(10));
    }

    /// <summary>
    /// The ten intrinsic tertiary coefficients - the seventh-order quantities this whole
    /// exercise exists to produce.
    ///
    /// Each is a short recurrence on the ones before it, so an error in an early one
    /// propagates into every later one. Paper II gives the same coefficients as explicit
    /// polynomials in q and the z, and all ten were confirmed to agree; this checks them
    /// against Buchdahl's printed values as well.
    /// </summary>
    [Fact]
    public void TheTertiaryIntrinsicCoefficientsMatchSurfaceOne()
    {
        var r = Run()[1];

        Near(2779.35, r.TertiaryIntrinsic(1));
        Near(-1026.89, r.TertiaryIntrinsic(2));
        Near(137.50, r.TertiaryIntrinsic(3));
        Near(-217.20, r.TertiaryIntrinsic(4));
        Near(33.786, r.TertiaryIntrinsic(5));
        Near(-4.6772, r.TertiaryIntrinsic(6));
        Near(34.333, r.TertiaryIntrinsic(7));
        // t8p is a residual: its five contributions are 2.61, -3.60, -9.59, 3.51 and
        // 8.18, cancelling almost nine-fold to 1.10. Buchdahl flags exactly this on
        // p.753 - "in this system it so happens that certain of the tertiary coefficients
        // are the residuals of very large contributions" - and adds that the number of
        // significant figures is "as small as three in certain cases". So a part in 10,000
        // is all his printed value can carry here, not a defect in the arithmetic.
        Near(1.1018, r.TertiaryIntrinsic(8), 5e-4);
        Near(-1.85963, r.TertiaryIntrinsic(9));
        Near(0.276688, r.TertiaryIntrinsic(10));
    }

    /// <summary>
    /// The SYSTEM sums, against the column Buchdahl prints down the right of Table I.
    ///
    /// This is the check the per-surface tests cannot make. Everything to do with the
    /// induced mechanism - what a surface inherits from those before it - is identically
    /// zero on surface 1, so a wrong reading there is invisible until the contributions
    /// are summed across the whole system.
    ///
    /// It earned its keep immediately: with entry 20 missing, S1p came out -105.216
    /// against a published -90.923, and the surface-1 column had shown nothing wrong.
    /// </summary>
    [Fact]
    public void TheSystemSumsMatchBuchdahlsColumn()
    {
        var (surfaces, indices) = Triplet();
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        double Sum(int entry)
        {
            double a = 0.0;
            for (int i = 1; i < surfaces.Count - 1; i++) a += rows[i].T[entry];
            return a;
        }

        // Sums are looser than per-surface values, and for two compounding reasons: they
        // accumulate the rounding of six surfaces rather than one, and several of them are
        // small residuals of larger contributions that partly cancel. c_p sums to 0.154
        // from per-surface terms an order of magnitude bigger.
        const double sumTol = 2e-4;

        Near(1.35914, Sum(10), sumTol);     // a_p
        Near(-0.031763, Sum(12), 1e-3);     // b_p, five printed digits on a small residual
        Near(0.154204, Sum(13), sumTol);    // c_p
        Near(-0.01906, Sum(14), 1e-3);      // cbar_p

        // The twelve secondary coefficients. These need the induced mechanism to be
        // right, and S1p is the assertion that caught the missing entry 20.
        Near(-90.923, Sum(41), sumTol);     // S1p
        Near(-23.173, Sum(42), sumTol);     // S1p bar
        Near(-92.137, Sum(46), sumTol);     // S2p
        Near(-11.869, Sum(47), 3e-4);       // S2p bar
        Near(-8.759, Sum(52), sumTol);      // S3p
        Near(0.7135, Sum(53), 1e-3);        // S3p bar
        Near(-13.1871, Sum(56), sumTol);    // S4p
        Near(0.5479, Sum(57), sumTol);      // S4p bar
        Near(1.498, Sum(62), 1e-3);         // S5p
        Near(0.161, Sum(63), 1e-2);         // S5p bar, three printed digits
        Near(-0.376, Sum(67), 1e-3);        // S6p
        Near(-0.0644, Sum(68), 5e-3);       // S6p bar, three printed digits
    }

    /// <summary>
    /// The first TOTAL tertiary coefficient, summed over the system.
    ///
    /// This is the whole chain in one number: paraxial rays, intrinsic primary
    /// coefficients, the induced mechanism, the secondary coefficients, w and the phi,
    /// w1..w5, z1..z10, the intrinsic tertiary coefficient, and finally what each surface
    /// inherits from those before it.
    ///
    /// Its sum is Buchdahl's T1, and Table II of the same paper gives tau1 = T1. So this
    /// value is Robb's seventh-order spherical aberration coefficient - the first of the
    /// twenty this program needs, and the first ever produced here from first principles
    /// rather than taken from another implementation.
    /// </summary>
    [Fact]
    public void TheFirstTotalTertiaryCoefficientMatches()
    {
        var (surfaces, indices) = Triplet();
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        // Per surface, where the total equals the intrinsic because nothing precedes it.
        Near(2779.35, rows[1].T[132], 1e-4);

        double sum = 0.0;
        for (int i = 1; i < surfaces.Count - 1; i++) sum += rows[i].T[132];

        // Buchdahl's T1, which Table II gives as Robb's tau1.
        Near(-4653.4, sum, 1e-4);
    }

    /// <summary>
    /// The tertiary totals implemented so far, against the sums Buchdahl prints.
    ///
    /// Each is the intrinsic coefficient plus what the surface inherits from those before
    /// it. The intrinsic halves were all confirmed earlier, so a failure here is in the
    /// induced part - which is exactly how the sign error in t135 was found: its
    /// surface-1 value was exact, and only the sum was wrong.
    /// </summary>
    [Fact]
    public void TheTertiaryTotalsMatchBuchdahlsColumn()
    {
        var (surfaces, indices) = Triplet();
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);

        double Tot(int m, bool bar)
        {
            double a = 0.0;
            for (int i = 1; i < surfaces.Count - 1; i++)
                a += bar ? rows[i].TertiaryTotalBar[m] : rows[i].TertiaryTotal[m];
            return a;
        }

        Near(-4653.4, Tot(1, false), 1e-4);    // T1,     Robb's tau1
        Near(-648.08, Tot(1, true), 3e-4);     // T1 bar, with T2 gives tau2
        Near(-3901.04, Tot(2, false), 3e-4);   // T2,     with T1 bar gives tau2; halved, tau3
        Near(-323.44, Tot(2, true), 5e-4);     // T2 bar, with T3 gives tau4
        Near(-323.99, Tot(3, false), 5e-4);    // T3,     Robb's tau5
        Near(-21.72, Tot(3, true), 3e-3);      // T3 bar, four printed digits
        Near(-505.99, Tot(4, false), 5e-4);    // T4,     Robb's tau6
        Near(-54.34, Tot(4, true), 1e-3);      // T4 bar
    }
}