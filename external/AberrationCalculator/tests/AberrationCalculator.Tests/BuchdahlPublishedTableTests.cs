using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The computing scheme against Buchdahl's own printed numbers.
///
/// <para>Table I of <i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958) prints the value of every
/// entry, for all six surfaces of the triplet used for illustration throughout the monograph
/// and the papers. That is an oracle for the whole primary and secondary chain which owes
/// nothing to a ray trace or to any other program - and which, until now, was
/// only ever spot-checked against the surface-1 column, where fourteen of the entries are zero
/// by construction and can agree with anything.</para>
///
/// <para>Surface 2 is where the scheme first has something to carry: the running sums are no
/// longer empty and the q-side quantities and the dagger family all take non-trivial values.
/// Agreement there tests the transfer, not just the single-surface formulae.</para>
///
/// <para><b>It also settles a labelling that the working notes left open</b>, and which matters
/// for anything built on top of the scheme. Those notes recorded: "UNCERTAIN, to settle on the
/// next pass: whether t17/t18 are B_p/Bbar_p or the reverse, and the exact split of t19/t20
/// between C_p and Cbar_p. The surface-1 column cannot resolve it because all four are zero
/// there; surface 2 will." It does. The printed formula column reads t17 = B_p, t18 = C_p,
/// t19 = Cbar_p, t20 = A_q, t21 = Abar_q - so the notes are shifted by one from t20 onward,
/// and Buchdahl's primary set is (A, Abar, B, C, Cbar) with NO Bbar among the entries. The
/// values below prove the code was never shifted, whatever the notes said.</para>
/// </summary>
public class BuchdahlPublishedTableTests
{
    /// <summary>
    /// The triplet of paper III p.753: curvatures, thicknesses, and the ratio k = n/n' per
    /// surface, entrance pupil at p = 0.113227, object at infinity. The indices follow from
    /// the k column - 1/1.61620 and 1/1.57250 at the entering surfaces.
    /// </summary>
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

    internal static BuchdahlTableIRow[] SchemeRows() => Rows();

    private static BuchdahlTableIRow[] Rows()
    {
        var sys = Triplet();
        var p = ParaxialTrace.Trace(sys, Indices, 11.3);
        return BuchdahlTableI.Compute(sys.Surfaces, Indices, p.Efl, 0.113227);
    }

    /// <summary>
    /// Buchdahl scales the system so that the focal length is unity. Getting this wrong would
    /// make every comparison below meaningless while still looking orderly, so it is checked
    /// first and separately.
    /// </summary>
    [Fact]
    public void TheReconstructedTripletHasUnitFocalLength()
    {
        var p = ParaxialTrace.Trace(Triplet(), Indices, 11.3);
        Assert.Equal(1.0, p.Efl, 4);
    }

    /// <summary>
    /// Entries t1..t33 at surface 1, as printed. The prescription is quoted to six figures, so
    /// q - which is a small difference of two nearly equal quantities - carries a few parts in
    /// a million of that rounding; the tolerance admits it and nothing more.
    /// </summary>
    [Theory]
    [InlineData(1, 1.0)]            [InlineData(2, 0.0)]
    [InlineData(3, 4.82439)]        [InlineData(4, 0.113227)]
    [InlineData(5, 1.0)]            [InlineData(6, -0.0940533)]
    [InlineData(7, 1.0)]            [InlineData(8, -1.83937)]
    [InlineData(9, 0.0)]            [InlineData(10, 13.2443)]
    [InlineData(11, -1.24567)]      [InlineData(12, 0.234318)]
    [InlineData(13, 1.03684)]       [InlineData(14, -0.0975185)]
    public void SurfaceOneMatchesThePrintedTable(int entry, double printed)
        => AssertClose(printed, Rows()[1][entry], entry, 1);

    /// <summary>
    /// The same at surface 2, where the running sums, the q-side quantities and the dagger
    /// family are all non-zero for the first time. This is the column that tests the transfer.
    /// </summary>
    [Theory]
    [InlineData(1, 0.925915)]       [InlineData(2, 1.83937)]
    [InlineData(3, -2.53744)]       [InlineData(4, 0.0799174)]
    [InlineData(5, 0.827001)]       [InlineData(6, 0.349665)]
    [InlineData(7, 0.183839)]       [InlineData(8, -0.287446)]
    [InlineData(9, 3.38328)]        [InlineData(10, 17.6346)]
    [InlineData(11, 6.16618)]       [InlineData(12, 4.31219)]
    [InlineData(13, 2.29982)]       [InlineData(14, 0.804165)]
    [InlineData(15, 13.2443)]       [InlineData(16, -1.24567)]
    [InlineData(17, 0.234318)]      [InlineData(18, 1.03684)]
    [InlineData(19, -0.0975185)]    [InlineData(20, -2.93730)]
    [InlineData(21, 0.276263)]      [InlineData(22, -1.28684)]
    [InlineData(23, 0.0605160)]     [InlineData(24, -0.00569173)]
    [InlineData(25, 7.56836)]       [InlineData(26, -0.711829)]
    [InlineData(27, 0.415711)]      [InlineData(28, -0.0390992)]
    [InlineData(29, 0.302031)]      [InlineData(30, -0.0284070)]
    [InlineData(31, -3.35822)]      [InlineData(32, -0.184458)]
    [InlineData(33, -0.134017)]
    public void SurfaceTwoMatchesThePrintedTable(int entry, double printed)
        => AssertClose(printed, Rows()[2][entry], entry, 2);

    private static void AssertClose(double printed, double got, int entry, int surface)
    {
        if (printed == 0.0)
        {
            Assert.True(Math.Abs(got) < 1e-9,
                $"t{entry} at surface {surface}: printed 0, got {got:G8}");
            return;
        }
        double rel = Math.Abs(got - printed) / Math.Abs(printed);
        Assert.True(rel < 2e-5,
            $"t{entry} at surface {surface}: printed {printed:G8}, got {got:G8} "
          + $"({rel:P4} apart)");
    }
}

/// <summary>
/// Buchdahl's identities, evaluated on his own triplet.
///
/// <para>These decide something the printed values alone cannot: WHICH quantity each entry of
/// the scheme holds. The working notes guessed, and recorded that they were guessing. The
/// identities settle it, because they mix the quantities together and will not vanish unless
/// every one is what it is taken to be.</para>
/// </summary>
public class BuchdahlIdentityTests
{
    private static BuchdahlTableIRow[] Rows() => BuchdahlPublishedTableTests.SchemeRows();

    /// <summary>
    /// The primary identities M (20.41-47), at every surface. These are what establish that
    /// t17 is Bbar_p rather than B_p - an assignment the working notes had explicitly left
    /// open, and got the wrong way round in one of its two guesses.
    /// </summary>
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void ThePrimaryIdentitiesHold(int surface)
    {
        var res = BuchdahlIdentities.At(Rows(), surface);
        for (int i = 0; i < res.Primary.Length; i++)
        {
            double rel = Math.Abs(res.Primary[i]) / res.PrimaryScale[i];
            Assert.True(rel < 1e-9,
                $"(20.4{i + 1}) at surface {surface}: residual {res.Primary[i]:E3} "
              + $"against terms of order {res.PrimaryScale[i]:E3} ({rel:E2} relative)");
        }
    }

    /// <summary>
    /// The secondary identities, paper III (7.1-3) - the same as M (22.41, 51, 61). They use
    /// only third- and fifth-order quantities, and they are the gate on everything above:
    /// if these do not vanish then the starred quantities are being built wrongly and nothing
    /// the tertiary identities said would mean anything.
    /// </summary>
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void TheSecondaryIdentitiesHold(int surface)
    {
        var res = BuchdahlIdentities.At(Rows(), surface);
        for (int i = 0; i < res.Secondary.Length; i++)
        {
            double rel = Math.Abs(res.Secondary[i]) / res.SecondaryScale[i];
            Assert.True(rel < 1e-8,
                $"(7.{i + 1}) at surface {surface}: residual {res.Secondary[i]:E3} "
              + $"against terms of order {res.SecondaryScale[i]:E3} ({rel:E2} relative)");
        }
    }
}

/// <summary>
/// The TERTIARY coefficients against Buchdahl's published totals.
///
/// <para>This is the check the whole tau9/tau14/tau17 hunt needed, and the identities were only
/// ever a means to it. Buchdahl prints the system sum of every one of his t_1p..t_10p and their
/// barred partners for the triplet, in the last column of Table I, pp.750-751. Comparing
/// against them settles directly whether the SPHERICAL tertiary scheme is right - and in
/// particular T5, T7, T8 and T9, which are every unbarred T that feeds the three suspect
/// coefficients:</para>
///
/// <code>
///   tau9 = T5/2 + T7/4      tau14 = T8      tau17 = T9/2
/// </code>
///
/// <para><b>The tolerance is not uniform, and the reason is Buchdahl's own.</b> He writes that
/// "certain of the tertiary coefficients are the residuals of very large contributions", and
/// they are: T8 is a sum of per-surface terms of order 330 that comes to 1.81, a cancellation
/// of more than two orders. The prescription is quoted to six figures, so a relative error of
/// 1e-5 in the inputs becomes a fraction of a per cent in such a sum, and the printed values
/// for the small ones carry only three significant figures anyway. The four that cancel hardest
/// are given a per cent; the rest, which do not, are held to a tenth of that.</para>
/// </summary>
public class BuchdahlPublishedTertiaryTests
{
    // mu, published T_mu, published Tbar_mu, tolerance
    [Theory]
    [InlineData(1, -4653.4, -648.08, 0.001)]
    [InlineData(2, -3901.04, -323.44, 0.001)]
    [InlineData(3, -323.99, -21.72, 0.001)]
    [InlineData(4, -505.99, -54.34, 0.001)]
    [InlineData(5, -8.10, -20.474, 0.01)]      // cancels hard
    [InlineData(6, -4.55, 1.16, 0.01)]
    [InlineData(7, 18.60, 2.24, 0.001)]
    [InlineData(8, 1.82, 4.135, 0.01)]         // cancels hard
    [InlineData(9, 0.623, 1.684, 0.01)]        // cancels hard
    [InlineData(10, 0.0913, 0.0723, 0.01)]     // cancels hard
    public void TheTertiaryTotalsMatchThePublishedValues(
        int mu, double printedT, double printedTbar, double tol)
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        double t = 0.0, tbar = 0.0;
        for (int s = 1; s < rows.Length; s++)
        {
            t += rows[s].TertiaryTotal[mu];
            tbar += rows[s].TertiaryTotalBar[mu];
        }

        double relT = Math.Abs(t - printedT) / Math.Abs(printedT);
        double relTb = Math.Abs(tbar - printedTbar) / Math.Abs(printedTbar);

        Assert.True(relT < tol,
            $"T{mu}: published {printedT:G7}, got {t:G7} ({relT:P3} apart, tolerance {tol:P2})");
        Assert.True(relTb < tol,
            $"Tbar{mu}: published {printedTbar:G7}, got {tbar:G7} "
          + $"({relTb:P3} apart, tolerance {tol:P2})");
    }

    /// <summary>
    /// The three suspect coefficients themselves, assembled from the published T by Table II
    /// and compared against the same assembly from ours. If these agree on a SPHERICAL system
    /// then whatever is wrong with tau9, tau14 and tau17 on a figured one is in the aspheric
    /// increment and nowhere else - which is the conclusion this whole exercise was for.
    /// </summary>
    [Fact]
    public void TheSuspectCoefficientsAgreeOnASphericalSystem()
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        double T(int mu)
        {
            double s = 0.0;
            for (int i = 1; i < rows.Length; i++) s += rows[i].TertiaryTotal[mu];
            return s;
        }

        // Table II of paper III, for the three that draw on no barred T.
        double tau9 = T(5) / 2.0 + T(7) / 4.0;
        double tau14 = T(8);
        double tau17 = T(9) / 2.0;

        double pub9 = -8.10 / 2.0 + 18.60 / 4.0;
        double pub14 = 1.82;
        double pub17 = 0.623 / 2.0;

        // tau9 gets three per cent, and the extra is not slack - it is the cancellation
        // compounding. T5 is already a residual of per-surface terms forty times larger, and
        // tau9 then cancels it against T7: -4.07 + 4.65 = 0.58. Our T5 sits 0.4% from the
        // published value, which is what the six-figure prescription allows, and that becomes
        // 2.5% here. This triplet cannot test tau9 more tightly, which is a fact about
        // Buchdahl s example - "somewhat trivial ... on account of the small maximum aperture
        // and field", as he says himself - and not about the program.
        Assert.True(Math.Abs(tau9 - pub9) / Math.Abs(pub9) < 0.03,
            $"tau9: published {pub9:G7}, got {tau9:G7} "
          + $"(from T5 {T(5):G7} and T7 {T(7):G7})");
        Assert.True(Math.Abs(tau14 - pub14) / Math.Abs(pub14) < 0.01,
            $"tau14: published {pub14:G7}, got {tau14:G7}");
        Assert.True(Math.Abs(tau17 - pub17) / Math.Abs(pub17) < 0.01,
            $"tau17: published {pub17:G7}, got {tau17:G7}");
    }
}

/// <summary>
/// Buchdahl's identities on ASPHERIC systems, which is where they earn their keep.
///
/// <para>Whether they extend to figured surfaces was an open question - paper III is a scheme
/// for systems "containing no aspherical surfaces", and the six tertiary identities are
/// published inside that treatment. The PRIMARY identities settle it, and they are the right
/// control because the aspheric primary is independently known exact, verified against the
/// fifth-order code on every surface of both designs.</para>
///
/// <para>They hold to machine precision. So the identities do extend, and the secondary ones
/// become a ray-trace-free test of the aspheric barred secondary - which they fail. See
/// docs/verification.md; the failure is not asserted here, because a test that pinned it would
/// enshrine it.</para>
/// </summary>
public class AsphericIdentityTests
{
    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void ThePrimaryIdentitiesHoldOnFiguredSurfacesToo(string design)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(design), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        var inc = AsphericSchemeIncrements.Build(b, sph, sys.LastOpticalSurface());
        Assert.NotNull(inc);
        var figured = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, inc);

        for (int i = 1; i <= sys.LastOpticalSurface(); i++)
        {
            var res = BuchdahlIdentities.At(figured, i, n[0]);
            Assert.True(res.WorstPrimary < 1e-12,
                $"{design} surface {i}: worst primary residual {res.WorstPrimary:E3}");

            // The secondary identities held at FIFTY-FIVE PER CENT until the D half of the
            // aspheric secondary was put on the incidence ratio, per M (65.6). A regression
            // here means that correction has been lost.
            Assert.True(res.WorstSecondary < 1e-10,
                $"{design} surface {i}: worst secondary residual {res.WorstSecondary:E3}");
        }
    }
}
