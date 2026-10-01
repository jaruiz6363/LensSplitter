using System;
using AberrationCalculator.Core.Forbes;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The series arithmetic Forbes' route is built on. These are algebraic identities, so they are
/// checked to machine precision rather than to a tolerance chosen to pass: a truncated series
/// ring either satisfies them exactly or is wrong.
/// </summary>
public class InvariantSeriesTests
{
    private const int D = 3;   // the tertiary; see InvariantSeries for why three is enough

    private static void AssertSame(InvariantSeries a, InvariantSeries b, string what)
    {
        var x = a.Coefficients();
        var y = b.Coefficients();
        Assert.Equal(x.Length, y.Length);
        for (int i = 0; i < x.Length; i++)
            Assert.True(Math.Abs(x[i] - y[i]) < 1e-12 * (1 + Math.Abs(x[i])),
                $"{what}: coefficient {i} is {x[i]:E17} against {y[i]:E17}");
    }

    [Fact]
    public void TwentyMonomialsAtDegreeThree()
    {
        Assert.Equal(1, InvariantSeries.TermCount(0));
        Assert.Equal(4, InvariantSeries.TermCount(1));
        Assert.Equal(10, InvariantSeries.TermCount(2));
        Assert.Equal(20, InvariantSeries.TermCount(3));
        Assert.Equal(35, InvariantSeries.TermCount(4));
    }

    [Fact]
    public void ProductsTruncateRatherThanGrow()
    {
        var p = InvariantSeries.P(D);
        var q = p.Pow(3);
        Assert.Equal(1.0, q[3, 0, 0]);

        // p^4 is past the truncation and must be gone, not small.
        var r = p.Pow(4);
        Assert.True(r.IsZero(), $"p^4 survived truncation at degree {D}: {r}");
    }

    [Fact]
    public void TheThreeInvariantsAreIndependent()
    {
        var p = InvariantSeries.P(D);
        var k = InvariantSeries.K(D);
        var u = InvariantSeries.U(D);

        var m = p * k * u;
        Assert.Equal(1.0, m[1, 1, 1]);
        Assert.Equal(0.0, m[3, 0, 0]);
        Assert.Equal(0.0, m[2, 1, 0]);
    }

    [Fact]
    public void DifferenceOfSquaresHoldsExactly()
    {
        var p = InvariantSeries.P(D);
        var left = (1.0 + p) * (1.0 - p);
        var right = 1.0 - p * p;
        AssertSame(left, right, "(1+p)(1-p)");
    }

    [Fact]
    public void InverseTimesTheSeriesIsOne()
    {
        var s = 1.0 + 2.0 * InvariantSeries.P(D) - 0.5 * InvariantSeries.K(D)
                    + 3.0 * InvariantSeries.U(D) * InvariantSeries.P(D);
        AssertSame(s * s.Inverse(), InvariantSeries.Constant(D, 1.0), "s * 1/s");
    }

    [Fact]
    public void SqrtSquaredIsTheSeries()
    {
        // The shape the trace actually meets: a = (1 - u)^(1/2), constant term one.
        var s = 1.0 - InvariantSeries.U(D) - 0.25 * InvariantSeries.P(D) * InvariantSeries.K(D);
        var root = s.Sqrt();
        AssertSame(root * root, s, "sqrt(s)^2");
        Assert.Equal(1.0, root.ConstantTerm, 12);
    }

    [Fact]
    public void SqrtAgreesWithTheBinomialSeriesTermByTerm()
    {
        // (1 - u)^(1/2) = 1 - u/2 - u^2/8 - u^3/16 - ...
        var root = (1.0 - InvariantSeries.U(D)).Sqrt();
        Assert.Equal(1.0, root[0, 0, 0], 12);
        Assert.Equal(-0.5, root[0, 0, 1], 12);
        Assert.Equal(-0.125, root[0, 0, 2], 12);
        Assert.Equal(-0.0625, root[0, 0, 3], 12);
    }

    [Fact]
    public void InverseAndSqrtRefuseASeriesWithoutAConstantTerm()
    {
        // In a symmetric trace every quantity inverted or rooted is one on axis, so a zero
        // constant term means the caller built something wrongly. It must not be papered over.
        Assert.Throws<DivideByZeroException>(() => InvariantSeries.P(D).Inverse());
        Assert.Throws<ArgumentException>(() => InvariantSeries.P(D).Sqrt());
    }

    [Fact]
    public void MixingTruncationsIsRefused()
    {
        var a = InvariantSeries.P(3);
        var b = InvariantSeries.P(4);
        Assert.Throws<ArgumentException>(() => a + b);
        Assert.Throws<ArgumentException>(() => a * b);
    }

    /// <summary>
    /// Composition is how a surface figure enters, and the point of Forbes' formulation for us:
    /// sphere, conic and even asphere differ only in the coefficients of <c>g</c>.
    /// </summary>
    [Fact]
    public void ComposeSubstitutesIntoAUnivariateSeries()
    {
        var p = InvariantSeries.P(D);
        var k = InvariantSeries.K(D);
        var arg = p + 2.0 * k;                       // no constant term, as an argument must have
        var g = new[] { 5.0, 3.0, 7.0 };             // g(t) = 5 + 3t + 7t^2

        var got = arg.Compose(g);
        var want = 5.0 + 3.0 * arg + 7.0 * (arg * arg);
        AssertSame(got, want, "compose");
    }

    [Fact]
    public void ComposeRefusesAnArgumentWithAConstantTerm()
    {
        var bad = 1.0 + InvariantSeries.P(D);
        Assert.Throws<ArgumentException>(() => bad.Compose(new[] { 1.0, 1.0 }));
    }

    /// <summary>
    /// The sag of a sphere as a series in <c>p = y.y</c>, which is the first surface figure the
    /// trace will need: <c>z = c p / 2 + c^3 p^2 / 8 + c^5 p^3 / 16 + ...</c>. Checked against
    /// the closed form <c>z = (1 - sqrt(1 - c^2 p)) / c</c> expanded the same way.
    /// </summary>
    [Fact]
    public void SphericalSagSeriesMatchesItsClosedForm()
    {
        const double c = 0.02;                        // R = 50
        var p = InvariantSeries.P(D);

        var closed = (1.0 - (1.0 - c * c * p).Sqrt()) * (1.0 / c);

        var byHand = InvariantSeries.Zero(D)
                   + 0.5 * c * p
                   + 0.125 * Math.Pow(c, 3) * (p * p)
                   + 0.0625 * Math.Pow(c, 5) * (p * p * p);

        AssertSame(closed, byHand, "spherical sag");
    }
}
