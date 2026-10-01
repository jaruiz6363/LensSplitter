using System;
using System.Collections.Generic;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Robb's analytic RMS spot, checked against closed forms that can be worked out by hand
/// from his Eq. (2) rather than against another implementation.
///
/// Each term of the intersection polynomial is coefficient * rho^a * H^b * f(theta), so for
/// a single aberration acting alone the pupil average is elementary and the answer is a
/// small exact rational. Those are the strongest tests available here: they pin the radial
/// weights, the theta averages and the centroid subtraction independently of each other.
/// </summary>
public class PrmsTests
{
    /// <summary>
    /// Third-order spherical acting alone. eps_y = B rho^3 cos, eps_z = B rho^3 sin, so
    /// eps_y^2 + eps_z^2 = B^2 rho^6 and the pupil average of rho^6 is 2/8 = 1/4.
    /// The mean of eps_y is zero because &lt;cos&gt; = 0, so no centroid term arises.
    /// RMS = B/2.
    /// </summary>
    [Fact]
    public void SphericalAlone_GivesHalfTheCoefficient()
    {
        var t = new BuchdahlTerms { B = 0.4 };
        Assert.Equal(0.25 * 0.16, Prms.MeanSquare(t, 1.0), 12);
        Assert.Equal(0.2, Prms.Value(t, 1.0), 12);
    }

    /// <summary>
    /// Fifth-order spherical alone: &lt;rho^10&gt; = 2/12 = 1/6, so RMS^2 = B5^2/6.
    /// Together with the test above this pins the radial weight 2/(a_i+a_j+2) at two
    /// different powers, which a single test cannot do.
    /// </summary>
    [Fact]
    public void FifthOrderSphericalAlone_UsesTheCorrectRadialWeight()
    {
        var t = new BuchdahlTerms { B5 = 0.6 };
        Assert.Equal(0.36 / 6.0, Prms.MeanSquare(t, 1.0), 12);
    }

    /// <summary>
    /// Seventh-order spherical alone: &lt;rho^14&gt; = 2/16 = 1/8.
    /// </summary>
    [Fact]
    public void SeventhOrderSphericalAlone_UsesTheCorrectRadialWeight()
    {
        var t = new BuchdahlTerms { B7 = 0.5 };
        Assert.Equal(0.25 / 8.0, Prms.MeanSquare(t, 1.0), 12);
    }

    /// <summary>
    /// The spherical family is purely aperture-driven, so its contribution cannot depend on
    /// the field at all. Anything that leaked a field power into those terms breaks here.
    /// </summary>
    [Fact]
    public void SphericalContributionIsIndependentOfField()
    {
        var t = new BuchdahlTerms { B = 0.3, B5 = -0.2, B7 = 0.05 };
        double onAxis = Prms.MeanSquare(t, 0.0);
        Assert.Equal(onAxis, Prms.MeanSquare(t, 1.0), 12);
        Assert.Equal(onAxis, Prms.MeanSquare(t, 0.5), 12);
    }

    /// <summary>
    /// Coma acting alone. Its two eps_y terms are 2F rho^2 H and F rho^2 H cos(2theta), and
    /// eps_z carries F rho^2 H sin(2theta). The constant part has a non-zero mean, so the
    /// centroid subtraction bites here and nowhere in the spherical tests:
    ///
    ///   &lt;eps_y^2&gt; = (4F^2 + F^2/2) &lt;rho^4&gt; H^2,   &lt;eps_z^2&gt; = (F^2/2) &lt;rho^4&gt; H^2
    ///   &lt;eps_y&gt;^2 = (2F &lt;rho^2&gt; H)^2
    ///
    /// with &lt;rho^4&gt; = 1/3 and &lt;rho^2&gt; = 1/2, giving RMS^2 = (5/3 - 1) F^2 H^2 = 2/3 F^2 H^2.
    /// </summary>
    [Fact]
    public void ComaAlone_IsQuadraticInFieldAndCentroidReferenced()
    {
        var t = new BuchdahlTerms { F = 0.3 };
        double expected = (2.0 / 3.0) * 0.09;
        Assert.Equal(expected, Prms.MeanSquare(t, 1.0), 12);
        Assert.Equal(expected * 0.25, Prms.MeanSquare(t, 0.5), 12);   // H^2 scaling
        Assert.Equal(0.0, Prms.MeanSquare(t, 0.0), 12);
    }

    /// <summary>
    /// Distortion displaces the whole patch without changing its size, so it must not appear
    /// in the spot radius at any order. Robb's corresponding terms vanish for the same
    /// reason, and this is easy to reintroduce by mistake when transcribing the polynomials.
    /// </summary>
    [Fact]
    public void DistortionDoesNotAffectTheSpotSize()
    {
        var without = new BuchdahlTerms { B = 0.2, F = 0.1, C = 0.05, Pi = 0.03 };
        var with = without.Clone();
        with.E = 5.0;
        with.E5 = -3.0;

        Assert.Equal(Prms.MeanSquare(without, 1.0), Prms.MeanSquare(with, 1.0), 12);
        Assert.DoesNotContain(Prms.Terms, x => x.A == "E" || x.B == "E" || x.A == "E5" || x.B == "E5");
    }

    /// <summary>
    /// A perfect system has no spot, and the value is never negative: the series is a
    /// truncation, and on a well-corrected design the surviving terms can cancel into a
    /// small negative number that is numerical dust rather than a real variance.
    /// </summary>
    [Fact]
    public void AnAberrationFreeSystemHasZeroSpotAndValueIsNeverNegative()
    {
        Assert.Equal(0.0, Prms.Value(new BuchdahlTerms(), 1.0), 12);

        // Contrived cancellation driving the mean square below zero.
        var t = new BuchdahlTerms { B = 1.0, B5 = -2.4 };
        Assert.True(Prms.Value(t, 1.0) >= 0.0);
    }

    /// <summary>
    /// Every coefficient enters quadratically, so scaling them all by s scales the RMS by
    /// |s| exactly. That holds for the whole form at once, including the cross terms and
    /// the centroid subtraction.
    /// </summary>
    [Fact]
    public void ScalingEveryCoefficientScalesTheRmsLinearly()
    {
        var t = new BuchdahlTerms { B = 0.3, F = -0.2, C = 0.1, Pi = 0.05, B5 = 0.02,
                                    F1 = -0.01, F2 = 0.03, M1 = 0.004, M2 = -0.002,
                                    M3 = 0.001, N1 = 0.0005, N2 = -0.0003, N3 = 0.0002,
                                    C5 = 0.0001, Pi5 = -0.00005, B7 = 0.00002 };
        var scaled = t.Clone();
        foreach (var f in typeof(BuchdahlTerms).GetFields())
            if (f.FieldType == typeof(double)) f.SetValue(scaled, (double)f.GetValue(scaled)! * 3.0);

        Assert.Equal(3.0 * Prms.Value(t, 0.7), Prms.Value(scaled, 0.7), 10);
    }

    /// <summary>
    /// PRMSA is the weighted mean of the mean SQUARES, square-rooted once - Robb's
    /// spectrally weighted average. Averaging the RMS values themselves is a different and
    /// smaller quantity, and the two coincide only when every case is identical, so this
    /// checks both the identical case and a case where they must differ.
    /// </summary>
    [Fact]
    public void CompositeAveragesMeanSquaresNotRmsValues()
    {
        var a = new BuchdahlTerms { B = 0.2 };
        var b = new BuchdahlTerms { B = 0.8 };

        // Identical cases: the composite is just that value.
        double same = Prms.Composite(new[] { (a, 1.0, 1.0), (a, 1.0, 3.0) });
        Assert.Equal(Prms.Value(a, 1.0), same, 12);

        // Differing cases: mean of squares, not mean of values.
        double composite = Prms.Composite(new[] { (a, 1.0, 1.0), (b, 1.0, 1.0) });
        double meanOfSquares = Math.Sqrt((Prms.MeanSquare(a, 1.0) + Prms.MeanSquare(b, 1.0)) / 2.0);
        double meanOfValues = (Prms.Value(a, 1.0) + Prms.Value(b, 1.0)) / 2.0;

        Assert.Equal(meanOfSquares, composite, 12);
        Assert.NotEqual(meanOfValues, composite, 6);
    }

    /// <summary>Weights are honoured, and a zero-weight case is excluded entirely.</summary>
    [Fact]
    public void CompositeHonoursWeightsAndIgnoresZeroWeightCases()
    {
        var a = new BuchdahlTerms { B = 0.2 };
        var b = new BuchdahlTerms { B = 0.8 };

        double weighted = Prms.Composite(new[] { (a, 1.0, 3.0), (b, 1.0, 1.0) });
        double expected = Math.Sqrt((3 * Prms.MeanSquare(a, 1.0) + Prms.MeanSquare(b, 1.0)) / 4.0);
        Assert.Equal(expected, weighted, 12);

        double ignoring = Prms.Composite(new[] { (a, 1.0, 1.0), (b, 1.0, 0.0) });
        Assert.Equal(Prms.Value(a, 1.0), ignoring, 12);
    }

    /// <summary>
    /// The assembled quadratic form should have the size the derivation predicts. A term
    /// count that drifts means a polynomial term was lost or duplicated in transcription.
    ///
    /// <para>It was 71 while seventh order was represented by spherical aberration alone.
    /// Adding the other eighteen coefficients that affect spot size takes it to 232 - more
    /// than tripling, because the form is quadratic and each new coefficient pairs with
    /// every existing one that shares its field power.</para>
    /// </summary>
    [Fact]
    public void TheAssembledFormHasTheExpectedNumberOfTerms()
    {
        Assert.Equal(232, Prms.TermCount);
    }

    /// <summary>
    /// Seventh-order distortion, tau20, must be absent for the same reason E and E5 are: it
    /// displaces the patch without resizing it.
    /// </summary>
    [Fact]
    public void SeventhOrderDistortionDoesNotAffectTheSpotSize()
    {
        Assert.DoesNotContain(Prms.Terms, x => x.A == "Tau20" || x.B == "Tau20");
    }

    /// <summary>
    /// Oblique spherical aberration at seventh order - tau4, tau5, tau6 - is aperture-heavy
    /// and field-dependent, so it must scale with both. This is the family Buchdahl singled
    /// out as the reason fifth-order predictions fail in the outer field.
    /// </summary>
    [Fact]
    public void TheSeventhOrderFieldTermsScaleWithField()
    {
        var t = new BuchdahlTerms { Tau4 = 0.01, Tau5 = 0.008, Tau6 = -0.005 };

        Assert.Equal(0.0, Prms.MeanSquare(t, 0.0), 12);

        // A = 5, B = 2 throughout, so the mean square goes as H^4.
        double atHalf = Prms.MeanSquare(t, 0.5);
        double atFull = Prms.MeanSquare(t, 1.0);
        Assert.Equal(atFull / 16.0, atHalf, 12);
    }
}
