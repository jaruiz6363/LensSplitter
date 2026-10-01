using System;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The contribution breakdown makes two claims a reader will act on: that the isolated
/// values are comparable between aberrations, and that the shares add up to the spot the
/// design actually has. Both are testable, and the second is exact.
/// </summary>
public class ContributionAnalysisTests
{
    private static BuchdahlTerms Mixed() => new()
    {
        B = 0.30, F = -0.20, C = 0.10, Pi = 0.05, E = 9.9,
        B5 = -0.15, F1 = 0.04, F2 = -0.03, M1 = 0.02, M2 = -0.01, M3 = 0.008,
        N1 = 0.003, N2 = -0.002, N3 = 0.001, C5 = 0.0006, Pi5 = -0.0004,
        E5 = -7.7, B7 = 0.05,
    };

    /// <summary>
    /// The shares are an exact decomposition: they sum to the mean square spot, so the
    /// percentages sum to 100. This is the property that makes a percentage honest, and it
    /// only holds because every cross term is split between its two partners.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void SharesSumToTheMeanSquareExactly(double h)
    {
        var t = Mixed();
        var rows = ContributionAnalysis.Compute(t, h);

        double total = Prms.MeanSquare(t, h);
        Assert.Equal(total, rows.Sum(r => r.Share), 12);
        Assert.Equal(100.0, rows.Sum(r => r.Percent), 8);
    }

    /// <summary>
    /// With one aberration acting alone, the isolated value IS the spot and its share is
    /// all of it. This anchors the re-normalisation against the closed forms already
    /// established for PRMS.
    /// </summary>
    [Fact]
    public void ASingleAberrationOwnsTheWholeSpot()
    {
        var t = new BuchdahlTerms { B = 0.4 };
        var rows = ContributionAnalysis.Compute(t, 1.0);
        var b = rows.Single(r => r.Name == "B");

        Assert.Equal(0.2, b.Isolated, 12);              // B/2, as for spherical alone
        Assert.Equal(0.2, Prms.Value(t, 1.0), 12);
        Assert.Equal(100.0, b.Percent, 8);
        foreach (var r in rows.Where(x => x.Name != "B"))
            Assert.Equal(0.0, r.Percent, 10);
    }

    /// <summary>
    /// The isolated value must not depend on what else is present - that is what makes it a
    /// property of the aberration rather than of the design, and what lets two aberrations
    /// be compared. The share, by contrast, must change.
    /// </summary>
    [Fact]
    public void IsolatedValuesAreIndependentOfTheOtherAberrations()
    {
        var alone = new BuchdahlTerms { B = 0.3 };
        var crowded = Mixed();

        double isolatedAlone = ContributionAnalysis.Compute(alone, 1.0).Single(r => r.Name == "B").Isolated;
        double isolatedCrowded = ContributionAnalysis.Compute(crowded, 1.0).Single(r => r.Name == "B").Isolated;

        Assert.Equal(isolatedAlone, isolatedCrowded, 12);   // B = 0.30 in both

        double shareAlone = ContributionAnalysis.Compute(alone, 1.0).Single(r => r.Name == "B").Percent;
        double shareCrowded = ContributionAnalysis.Compute(crowded, 1.0).Single(r => r.Name == "B").Percent;
        Assert.NotEqual(shareAlone, shareCrowded, 3);
    }

    /// <summary>
    /// Two aberrations that cancel: the design is far better than either alone, so both
    /// isolated values are large while the total is small, and one share must go negative.
    /// This is the case the table exists to expose, and the one a naive "percentage of
    /// spot" would report as nonsense.
    /// </summary>
    [Fact]
    public void CancellingAberrationsProduceANegativeShare()
    {
        // B and B5 enter with opposite sign; sized so they largely cancel.
        var t = new BuchdahlTerms { B = 1.0, B5 = -1.15 };

        double combined = Prms.Value(t, 1.0);
        double bAlone = Prms.Value(new BuchdahlTerms { B = 1.0 }, 1.0);
        Assert.True(combined < bAlone, "the pair must be better than B alone for this to be balancing");

        var rows = ContributionAnalysis.Compute(t, 1.0);
        Assert.Contains(rows, r => r.Percent < 0.0);
        Assert.Equal(100.0, rows.Sum(r => r.Percent), 8);

        // And the balancing ratio must register it.
        Assert.True(ContributionAnalysis.BalancingRatio(t, 1.0) < 0.5);
    }

    /// <summary>
    /// Independent aberrations - ones sharing no cross term - give a balancing ratio of
    /// one, because the spot really is the quadrature sum of their isolated values.
    /// </summary>
    [Fact]
    public void IndependentAberrationsGiveABalancingRatioOfOne()
    {
        // Spherical (H^0) and coma (H^2) share no cross term with each other at H=1?
        // They do, so use a genuinely disjoint pair: B alone against B with distortion,
        // which contributes nothing at all.
        var t = new BuchdahlTerms { B = 0.3, E = 5.0, E5 = -2.0 };
        Assert.Equal(1.0, ContributionAnalysis.BalancingRatio(t, 1.0), 9);
    }

    /// <summary>
    /// Distortion moves the patch without resizing it, so it must show a zero isolated
    /// value and a zero share however large its coefficient - otherwise a designer would
    /// be sent to fix the one aberration that cannot affect the spot.
    /// </summary>
    [Fact]
    public void DistortionShowsNoContributionAtAnySize()
    {
        var t = Mixed();                                  // E = 9.9, E5 = -7.7
        var rows = ContributionAnalysis.Compute(t, 1.0);

        foreach (var name in new[] { "E", "E5" })
        {
            var r = rows.Single(x => x.Name == name);
            Assert.Equal(0.0, r.Isolated, 12);
            Assert.Equal(0.0, r.Share, 12);
            Assert.Equal(0.0, r.Percent, 10);
        }
    }


    /// <summary>
    /// A cross term belongs to BOTH of the aberrations that produce it, so it must be split
    /// evenly between them. Giving it wholly to one still sums to 100%, so the sum test
    /// cannot see the error - only symmetry can.
    ///
    /// B and B7 are sized here so their diagonal contributions are exactly equal
    /// (0.25 B^2 = 0.125 B7^2). Their shares must then be equal too, because the only other
    /// thing either receives is half of the same cross term.
    /// </summary>
    [Fact]
    public void ACrossTermIsSplitEvenlyBetweenItsTwoPartners()
    {
        const double b = 0.4;
        double b7 = b * Math.Sqrt(2.0);                 // equal diagonal contributions

        var t = new BuchdahlTerms { B = b, B7 = b7 };
        var rows = ContributionAnalysis.Compute(t, 1.0);

        var rb = rows.Single(r => r.Name == "B");
        var rb7 = rows.Single(r => r.Name == "B7");

        Assert.Equal(rb.Isolated, rb7.Isolated, 12);    // the premise: equal alone
        Assert.Equal(rb.Share, rb7.Share, 12);          // so equal in the design too
        Assert.Equal(50.0, rb.Percent, 8);
        Assert.Equal(50.0, rb7.Percent, 8);
    }
    /// <summary>
    /// A design with no aberration at all must not produce a division by zero or a set of
    /// meaningless percentages.
    /// </summary>
    [Fact]
    public void APerfectSystemProducesZerosRatherThanNonsense()
    {
        var rows = ContributionAnalysis.Compute(new BuchdahlTerms(), 1.0);
        foreach (var r in rows)
        {
            Assert.Equal(0.0, r.Isolated, 12);
            Assert.Equal(0.0, r.Share, 12);
            Assert.Equal(0.0, r.Percent, 12);
            Assert.False(double.IsNaN(r.Percent));
        }
    }

    /// <summary>
    /// Isolated values are comparable across aberrations with different units and different
    /// pupil and field powers - that is the whole point of re-normalising. Equal isolated
    /// values must mean equal damage, whatever the raw coefficients look like.
    /// </summary>
    [Fact]
    public void EqualIsolatedValuesMeanEqualSpotsDespiteUnequalCoefficients()
    {
        // Spherical alone: RMS = B/2.  Fifth-order spherical alone: RMS = B5/sqrt(6).
        var sph = new BuchdahlTerms { B = 0.4 };                        // 0.2
        var fifth = new BuchdahlTerms { B5 = 0.2 * Math.Sqrt(6.0) };    // also 0.2

        double a = ContributionAnalysis.Compute(sph, 1.0).Single(r => r.Name == "B").Isolated;
        double b = ContributionAnalysis.Compute(fifth, 1.0).Single(r => r.Name == "B5").Isolated;

        Assert.Equal(a, b, 12);
        Assert.NotEqual(sph.B, fifth.B5, 3);            // the raw coefficients differ
        Assert.Equal(Prms.Value(sph, 1.0), Prms.Value(fifth, 1.0), 12);
    }

    /// <summary>
    /// The per-surface decomposition must be exact: intrinsic plus aspheric plus induced is
    /// the surface total, and those summed over the surfaces and scaled by the F/number are
    /// the system totals. If that fails, the breakdown is attributing aberration to the
    /// wrong place even though the totals still look right.
    /// </summary>
    [Fact]
    public void PerSurfacePartsReconstructTheSystemTotals()
    {
        string? fixture = ZmxReaderTests.FindOracleFixture("F6_triplet_two_aspheres.zmx");
        if (fixture == null) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(fixture, catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var b = BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, field));

        foreach (var name in BuchdahlTerms.Names)
        {
            double sum = 0.0;
            for (int i = 1; i < sys.Surfaces.Count - 1; i++)
            {
                // parts must equal the surface total
                double parts = b.Intrinsic[i][name] + b.Induced[i][name]
                             + (b.Aspheric[i]?[name] ?? 0.0);
                Assert.Equal(parts, b.PerSurface[i][name], 12);
                sum += parts;
            }
            sum *= b.FNumber;
            double scale = Math.Max(Math.Abs(b.Totals[name]), 1e-12);
            Assert.True(Math.Abs(sum - b.Totals[name]) / scale < 1e-9,
                $"{name}: surfaces reconstruct {sum:E12}, totals say {b.Totals[name]:E12}");
        }
    }

    /// <summary>
    /// Nothing precedes the first surface, so it can have no induced contribution at all.
    /// A non-zero value there would mean the running sums were being updated before the
    /// corrections were applied rather than after.
    /// </summary>
    [Fact]
    public void TheFirstSurfaceHasNoInducedContribution()
    {
        string? fixture = ZmxReaderTests.FindOracleFixture("F6_triplet_two_aspheres.zmx");
        if (fixture == null) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(fixture, catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var b = BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, field));

        foreach (var name in BuchdahlTerms.Names)
            Assert.Equal(0.0, b.Induced[1][name], 12);
    }

    /// <summary>
    /// Surface shares are an exact decomposition of the spot, exactly as the aberration
    /// shares are - so they sum to 100% however heavily the surfaces cancel.
    /// </summary>
    [Fact]
    public void SurfaceSharesSumToOneHundredPercent()
    {
        string? fixture = ZmxReaderTests.FindOracleFixture("F6_triplet_two_aspheres.zmx");
        if (fixture == null) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(fixture, catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var b = BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, field));

        foreach (double h in new[] { 0.5, 1.0 })
        {
            var rows = ContributionAnalysis.BySurface(b, h);
            Assert.Equal(Prms.MeanSquare(b.Totals, h), rows.Sum(r => r.Share), 10);
            Assert.Equal(100.0, rows.Sum(r => r.Percent), 6);
        }
    }
}
