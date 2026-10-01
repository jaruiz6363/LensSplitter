using System;
using AberrationCalculator.Core.Nat;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The wave front conversions against Buchdahl's own printed numbers.
///
/// <para>Paper VII, <i>J. Opt. Soc. Am.</i> <b>50</b>, 539 (1960), prints two tables for the
/// triplet Sigma1 which between them close the whole chain:</para>
/// <list type="bullet">
///   <item>VI <b>Table II</b> gives the aberration coefficients of orders 3, 5 and 7 in BOTH
///   paracanonical and W coordinates.</item>
///   <item>VII <b>Table I</b> gives the deformation and retardation coefficients of the same
///   three orders for the same system.</item>
/// </list>
///
/// <para>So VI Table II (W column) fed through VII Eqs. (6.5-6) must reproduce VII Table I. That
/// is an oracle for the entire fifth-order wave front conversion which owes nothing to a ray
/// trace, to a fit, or to any other program - and it is the verification route that replaced the
/// abandoned attempt to reach these coefficients through Robb's transverse polynomial.</para>
///
/// <para><b>The one fitted quantity is e.</b> Buchdahl computed Sigma1 with the exit-pupil to
/// image distance not equal to unity and did not publish its value, but his Sec. 7(a) gives the
/// power of <c>e</c> that each coefficient carries. Solving for <c>e</c> from <c>pi1</c> alone
/// leaves the remaining thirteen coefficients as free checks on a single parameter.</para>
/// </summary>
public class DeformationTests
{
    // ── Buchdahl VI Table II, Sigma1, the W-coordinate column ────────────────────────────
    private const double A = 1.3591, Ab = -0.01468, Bb = -0.03176, C = 0.15420, Cb = -0.01906;
    private const double S1 = -93.021, S1b = -23.650, S3 = -9.0392, S4 = -13.406, S4b = 0.5879;
    private const double S5 = 1.506, S5b = 0.146, S6 = -0.3859, S6b = -0.0569;

    // ── Buchdahl VII Table I, Sigma1, the D column ───────────────────────────────────────
    private static readonly double[] PiD = { 0.38571, -0.016143, 0.082148, -0.016921, -0.019674 };
    private static readonly double[] SigmaD =
        { -18.34, -26.905, -2.547, -5.9997, 0.8986, -0.20577, 0.1806, 0.1471, -0.05877 };

    // ── Buchdahl VII Table I, Sigma1, the R column ───────────────────────────────────────
    private static readonly double[] SigmaR =
        { -18.34, -26.905, -2.752, -5.9997, 0.9072, -0.24933, 0.1806, 0.1561, -0.04829 };

    /// <summary>
    /// Sec. 7(a): with e as the unit of length each coefficient takes a power of e, the barred
    /// ones taking one more, and the deformation coefficients are then restored by e^-3, e^-5.
    /// </summary>
    private static Deformation Sigma1Deformation(double e)
    {
        double p3 = Math.Pow(e, -3), p5 = Math.Pow(e, -5);
        var d = Deformation.FromWCoordinates(
            A / e, Ab, Bb * e, C * e, Cb * e * e,
            S1 / e, S1b, S3 * e, S4 * e, S4b * e * e,
            S5 * e * e, S5b * e * e * e, S6 * e * e * e, S6b * e * e * e * e);
        return new Deformation(
            d.Pi1 * p3, d.Pi2 * p3, d.Pi3 * p3, d.Pi4 * p3, d.Pi5 * p3,
            d.Sigma1 * p5, d.Sigma2 * p5, d.Sigma3 * p5, d.Sigma4 * p5, d.Sigma5 * p5,
            d.Sigma6 * p5, d.Sigma7 * p5, d.Sigma8 * p5, d.Sigma9 * p5);
    }

    /// <summary>4 pi1 = A e^-1, then times e^-3, so e is the fourth root of A/(4 pi1).</summary>
    private static double SolveE() => Math.Pow(A / (4.0 * PiD[0]), 0.25);

    [Fact]
    public void VITableIIThroughEq66ReproducesVIITableI()
    {
        double e = SolveE();
        var d = Sigma1Deformation(e);

        // e came out of pi1, so pi1 is not a check. The other thirteen are.
        double[] pi = { d.Pi1, d.Pi2, d.Pi3, d.Pi4, d.Pi5 };
        double[] sg = { d.Sigma1, d.Sigma2, d.Sigma3, d.Sigma4, d.Sigma5,
                        d.Sigma6, d.Sigma7, d.Sigma8, d.Sigma9 };

        for (int i = 1; i < 5; i++)
            Assert.True(Math.Abs(pi[i] - PiD[i]) <= 2e-4 * Math.Abs(PiD[i]) + 1e-8,
                $"pi{i + 1}: got {pi[i]}, Table I has {PiD[i]}");

        // Table I prints these to four significant figures, so the absolute floor is what the
        // rounding of the last digit allows; the relative tolerance carries the large ones.
        for (int i = 0; i < 9; i++)
            Assert.True(Math.Abs(sg[i] - SigmaD[i]) <= 2e-3 * Math.Abs(SigmaD[i]) + 5e-5,
                $"sigma{i + 1}: got {sg[i]}, Table I has {SigmaD[i]}");
    }

    /// <summary>
    /// Eq. (3.4) leaves sigma1, sigma2, sigma4 and sigma7 alone, and Buchdahl's D and R columns
    /// agree exactly on those four. That is the sharpest check in the table: it is exact, not
    /// approximate, and it would fail immediately if the five corrections were attached to the
    /// wrong coefficients.
    /// </summary>
    [Fact]
    public void RetardationLeavesFourSecondaryCoefficientsUntouched()
    {
        var d = new Deformation(
            PiD[0], PiD[1], PiD[2], PiD[3], PiD[4],
            SigmaD[0], SigmaD[1], SigmaD[2], SigmaD[3], SigmaD[4],
            SigmaD[5], SigmaD[6], SigmaD[7], SigmaD[8]);
        var r = d.ToRetardation(0.9688);

        Assert.Equal(SigmaD[0], (double)r.Sigma1, 12);
        Assert.Equal(SigmaD[1], (double)r.Sigma2, 12);
        Assert.Equal(SigmaD[3], (double)r.Sigma4, 12);
        Assert.Equal(SigmaD[6], (double)r.Sigma7, 12);

        // ... and the primary coefficients are unchanged at every order.
        Assert.Equal(PiD[0], (double)r.Pi1, 12);
        Assert.Equal(PiD[4], (double)r.Pi5, 12);
    }

    /// <summary>
    /// The five corrected coefficients against Table I's R column. The value of e used here is
    /// the one obtained from VI Table II and Eq. (6.5), so this is a genuine cross-check between
    /// two tables in two different papers by way of two different equations.
    /// </summary>
    [Fact]
    public void RetardationReproducesTableIRColumn()
    {
        double e = SolveE();
        var d = new Deformation(
            PiD[0], PiD[1], PiD[2], PiD[3], PiD[4],
            SigmaD[0], SigmaD[1], SigmaD[2], SigmaD[3], SigmaD[4],
            SigmaD[5], SigmaD[6], SigmaD[7], SigmaD[8]);
        var r = d.ToRetardation(e);

        double[] got = { r.Sigma1, r.Sigma2, r.Sigma3, r.Sigma4, r.Sigma5,
                         r.Sigma6, r.Sigma7, r.Sigma8, r.Sigma9 };
        for (int i = 0; i < 9; i++)
            Assert.True(Math.Abs(got[i] - SigmaR[i]) <= 3e-2 * Math.Abs(SigmaR[i]) + 2e-3,
                $"'sigma{i + 1}: got {got[i]}, Table I R column has {SigmaR[i]}");
    }

    /// <summary>
    /// Eq. (2.8) maps one invariant monomial to one Hopkins term. The accessors must not drift.
    /// </summary>
    [Fact]
    public void TheMonomialMapIsTheOneEq28Prints()
    {
        var d = new Deformation(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        Assert.Equal(1.0, (double)d.W040);   // pi1 lambda^2
        Assert.Equal(2.0, (double)d.W131);   // pi2 lambda mu
        Assert.Equal(3.0, (double)d.W220);   // pi3 lambda nu
        Assert.Equal(4.0, (double)d.W222);   // pi4 mu^2
        Assert.Equal(5.0, (double)d.W311);   // pi5 mu nu
        Assert.Equal(5.0, (double)d.W220M);  // W220 + W222/2 = 3 + 2

        Assert.Equal(6.0, (double)d.W060);   // sigma1 lambda^3
        Assert.Equal(7.0, (double)d.W151);   // sigma2 lambda^2 mu
        Assert.Equal(8.0, (double)d.W240);   // sigma3 lambda^2 nu
        Assert.Equal(9.0, (double)d.W242);   // sigma4 lambda mu^2
        Assert.Equal(10.0, (double)d.W331);  // sigma5 lambda mu nu
        Assert.Equal(11.0, (double)d.W420);  // sigma6 lambda nu^2
        Assert.Equal(12.0, (double)d.W333);  // sigma7 mu^3
        Assert.Equal(13.0, (double)d.W422);  // sigma8 mu^2 nu
        Assert.Equal(14.0, (double)d.W511);  // sigma9 mu nu^2
    }
}
