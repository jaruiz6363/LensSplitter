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
/// Which order each deformation term first reaches, measured term by term.
///
/// <para>The rule is that <c>A_n r^n</c> first contributes at wave order <c>n</c>, which is
/// transverse order <c>n-1</c>. So r^4 first reaches the third order, r^6 the fifth, r^8 the
/// seventh and r^10 the ninth - and r^2 is not figuring at all, being a curvature change that
/// moves the focal length. The consequence a designer cares about is the one below the onset:
/// <b>r^6 cannot touch the third order however large it is</b>, so an r^6 term can be used to
/// correct the fifth without disturbing a third-order solution.</para>
///
/// <para><b>What this does NOT say is that a term only affects its own order.</b> It affects
/// every order above the onset as well, both in its own right - Buchdahl's aspheric increments
/// cascade, the fifth-order term carrying the fourth-order one inside it and the seventh
/// carrying both - and by induction, since changing a surface changes the rays every later
/// surface sees. The claim tested here is one-sided and precise: nothing below the onset moves
/// AT ALL, and something at the onset does.</para>
///
/// <para>Bit-identity below the onset, rather than a tolerance. The terms cannot reach those
/// orders, so there is nothing for a tolerance to absorb. See
/// <see cref="AsphericBeyondR8Tests"/> for the same argument at the top of the ladder, where
/// r^10 falls off the end of the seventh order entirely.</para>
/// </summary>
public class AsphericOnsetTests
{
    private static readonly string[] Third = { "B", "F", "C", "Pi", "E" };

    private static readonly string[] Fifth =
        { "B5", "F1", "F2", "M1", "M2", "M3", "N1", "N2", "N3", "C5", "Pi5", "E5" };

    private static readonly string[] Seventh =
        { "B7", "Tau2", "Tau3", "Tau4", "Tau5", "Tau6", "Tau7", "Tau8", "Tau9", "Tau10",
          "Tau11", "Tau12", "Tau13", "Tau14", "Tau15", "Tau16", "Tau17", "Tau18", "Tau19",
          "Tau20" };

    private static string[] Group(int order)
        => order == 3 ? Third : order == 5 ? Fifth : Seventh;

    private sealed record Run(BuchdahlTerms Totals, double Efl);

    /// <summary>
    /// Analyse a design, optionally nudging one even-asphere slot on a surface that is figured
    /// ALREADY - so that <c>Surface.IsFigured</c> does not change and the tertiary stays on the
    /// route it was on. Otherwise the last bits move for a reason that has nothing to do with
    /// the term, which <see cref="AsphericBeyondR8Tests"/> documents.
    /// </summary>
    private static Run Analyse(string name, int slot, double edgeSag)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);

        if (edgeSag != 0.0)
        {
            bool done = false;
            for (int i = 1; i <= sys.LastOpticalSurface() && !done; i++)
            {
                var s = sys.Surfaces[i];
                if (!s.IsFigured) continue;
                double r = s.SemiDiameter > 0.0 ? s.SemiDiameter
                         : sys.Aperture.Type == ApertureType.EPD ? 0.5 * sys.Aperture.Value
                         : 5.0;
                var a = new double[Math.Max(8, s.AsphericCoefficients.Length)];
                Array.Copy(s.AsphericCoefficients, a, s.AsphericCoefficients.Length);
                a[slot] += edgeSag / Math.Pow(r, 2 * slot + 2);
                s.AsphericCoefficients = a;
                done = true;
            }
            Assert.True(done, $"{name} has no figured surface to nudge");
        }

        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return new Run(b.Totals, p.Efl);
    }

    /// <summary>
    /// The onset table, measured. Slot 1 is r^4 and first reaches the third order, slot 2 is r^6
    /// and first reaches the fifth, slot 3 is r^8 and first reaches the seventh.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8", 1, 3)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8", 2, 5)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8", 3, 7)]
    [InlineData("TertiaryTestbed_Triplet24", 1, 3)]
    [InlineData("TertiaryTestbed_Triplet24", 2, 5)]
    [InlineData("TertiaryTestbed_Triplet24", 3, 7)]
    [InlineData("Ladder1_Conic", 2, 5)]
    [InlineData("Ladder1_Conic", 3, 7)]
    public void EachTermFirstReachesTheOrderItsPowerImplies(string name, int slot, int onset)
    {
        int power = 2 * slot + 2;
        var plain = Analyse(name, slot, 0.0);
        var nudged = Analyse(name, slot, 1e-3);

        // The focal length is untouched by anything past r^2: only a curvature change moves it.
        Assert.Equal(plain.Efl, nudged.Efl, 15);

        // Below the onset: nothing moves, to the bit.
        foreach (int below in new[] { 3, 5, 7 })
        {
            if (below >= onset) continue;
            foreach (string c in Group(below))
                Assert.True(plain.Totals[c].Equals(nudged.Totals[c]),
                    $"{name}: r^{power} moved {c}, which is of order {below}, from "
                  + $"{plain.Totals[c]:E17} to {nudged.Totals[c]:E17}. That term first reaches "
                  + $"order {onset} and cannot touch anything below it.");
        }

        // At the onset: something must move, or the test above is vacuous.
        bool moved = false;
        foreach (string c in Group(onset))
            if (!plain.Totals[c].Equals(nudged.Totals[c])) moved = true;

        Assert.True(moved,
            $"{name}: r^{power} changed nothing at order {onset}, which is the order it first "
          + "reaches. Either it is not being carried at all, or the onset is not where this "
          + "test says it is.");
    }

    /// <summary>
    /// And the other half of the same statement: a term DOES go on affecting every order above
    /// its onset. r^4 reaches the third order first and is not finished there - it feeds the
    /// fifth and the seventh as well, through Buchdahl's cascade of aspheric increments and
    /// through what it induces in the surfaces behind it.
    ///
    /// <para>Recorded because "r^4 is the third-order term" is the natural way to read the
    /// table and is wrong. The table says where a term STARTS.</para>
    /// </summary>
    [Fact]
    public void ATermKeepsAffectingEveryOrderAboveItsOnset()
    {
        var plain = Analyse("TertiaryTestbed_Triplet24", 1, 0.0);
        var nudged = Analyse("TertiaryTestbed_Triplet24", 1, 1e-3);

        foreach (int order in new[] { 3, 5, 7 })
        {
            bool moved = false;
            foreach (string c in Group(order))
                if (!plain.Totals[c].Equals(nudged.Totals[c])) moved = true;

            Assert.True(moved,
                $"r^4 left order {order} untouched. It first reaches the third order and goes on "
              + "contributing above it, both in its own right and by induction, so a design "
              + "cannot be corrected order by order with one term each.");
        }
    }
}
