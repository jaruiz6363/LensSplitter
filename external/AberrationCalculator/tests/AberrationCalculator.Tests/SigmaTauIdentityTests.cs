using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The sigma and tau series of (77.4) and (77.5), against the independent identity (78.9).
///
/// <para>Buchdahl derives the sigma and tau twice. (77.4-5) write them out term by term from
/// (74.6-7); (78.8) then gives a relation for <c>tau_n + 2(1-k)sigma_n</c> reached a different
/// way, and (78.9) states it explicitly for n = 2 and 3. The two routes have to agree, so the
/// second is a check on the transcription of the first that needs no ray trace, no table and
/// no other program - just two equations from facing pages of the same book.</para>
///
/// <para><b>Why this test exists.</b> The tertiary aspheric coefficients are wrong in their
/// c1-squared part, and sigma3 and tau3 are two of the four places that part could have come
/// from - they carry <c>(1/4)c1^2</c> and <c>(k^2+1/2)c1^2</c> respectively. This eliminates
/// both: their combination is required to produce (78.9)'s single <c>k^2 c1^2</c>, and it
/// does, which no pair of mistranscribed coefficients would. See docs/verification.md.</para>
/// </summary>
public class SigmaTauIdentityTests
{
    /// <summary>
    /// (78.9), second line:
    /// <c>tau3 + 2(1-k)sigma3 = -tau0{[2k^2 c0 c2 + k^2 c1^2 + (4k^2-6)th1^3 c1 - 8 th1^6]xi
    /// - (2k^2 c0^2 c1 - 16 th1^5)eta - 4 th1^4 zeta}xi^2</c>.
    /// </summary>
    private static double[] Published3(double k, double c1, double c2, double c0)
    {
        double th1 = c0 / 2.0, th3 = th1 * th1 * th1, th4 = th3 * th1,
               th5 = th4 * th1, th6 = th5 * th1;
        double k2 = k * k, tau0 = (1.0 - k) / (2.0 * k);

        var v = new double[10];
        v[0] = -tau0 * (2.0 * k2 * c0 * c2 + k2 * c1 * c1
                        + (4.0 * k2 - 6.0) * th3 * c1 - 8.0 * th6);      // xi^3
        v[1] = -tau0 * -(2.0 * k2 * c0 * c0 * c1 - 16.0 * th5);          // xi^2 eta
        v[2] = -tau0 * -4.0 * th4;                                       // xi^2 zeta
        return v;
    }

    /// <summary>(78.9), first line: <c>tau2 + 2(1-k)sigma2 = -4 tau0(k^2 th1 c1 - th1^4)xi^2</c>.</summary>
    private static double Published2(double k, double c1, double c0)
    {
        double th1 = c0 / 2.0, th4 = th1 * th1 * th1 * th1;
        return -4.0 * ((1.0 - k) / (2.0 * k)) * (k * k * th1 * c1 - th4);
    }

    [Theory]
    [InlineData(0.65, 0.0, 0.0, 1.0)]
    [InlineData(0.65, 0.4, 0.0, 1.0)]
    [InlineData(0.65, 0.0, 0.3, 1.0)]
    [InlineData(0.65, 0.4, 0.3, 1.0)]
    [InlineData(0.65, -1.7, 0.9, 1.0)]
    [InlineData(-1.0, 0.4, 0.3, 1.0)]      // a mirror, where 1-k is 2
    [InlineData(1.4, -0.6, 0.2, 1.0)]
    [InlineData(0.65, 0.4, 0.3, 0.7)]      // c0 away from 1, so the weights are exercised
    [InlineData(0.65, 0.4, 0.3, 1.6)]
    public void TheThirdOrderPairSatisfies789(double k, double c1, double c2, double c0)
    {
        var s = new TertiaryCubics.Surface(k, c1, c2, c0);

        var combined = TertiaryCubics.CubicPart(s.Tau3 + (2.0 * (1.0 - k)) * s.Sigma3);
        var published = Published3(k, c1, c2, c0);

        string[] name = { "xi^3", "xi^2 eta", "xi^2 zeta", "xi eta^2", "xi eta zeta",
                          "xi zeta^2", "eta^3", "eta^2 zeta", "eta zeta^2", "zeta^3" };

        // Several of the ten are exactly zero on both sides, so the tolerance is set from the
        // size of the whole polynomial rather than from each entry - otherwise the ones that
        // vanish are compared against nothing and any rounding dust fails them.
        double biggest = 0.0;
        foreach (double p in published) biggest = Math.Max(biggest, Math.Abs(p));
        double tolerance = 1e-12 * Math.Max(biggest, 1.0);

        for (int m = 0; m < 10; m++)
            Assert.True(Math.Abs(combined[m] - published[m]) < tolerance,
                $"k={k} c1={c1} c2={c2} c0={c0}, {name[m]}: (77.4-5) give {combined[m]:G10}, "
              + $"(78.9) gives {published[m]:G10}");
    }

    /// <summary>
    /// The same for n = 2. Shorter, and it pins the c1 term of sigma2 and tau2 - which matter
    /// because S2 carries them into every later order.
    /// </summary>
    [Theory]
    [InlineData(0.65, 0.4, 1.0)]
    [InlineData(0.65, -1.7, 1.0)]
    [InlineData(-1.0, 0.4, 1.0)]
    [InlineData(1.4, -0.6, 1.0)]
    [InlineData(0.65, 0.4, 0.7)]
    [InlineData(0.65, 0.4, 1.6)]
    public void TheSecondOrderPairSatisfies789(double k, double c1, double c0)
    {
        var s = new TertiaryCubics.Surface(k, c1, 0.0, c0);
        var combined = s.Tau2 + (2.0 * (1.0 - k)) * s.Sigma2;

        double got = combined.C[2, 0, 0];                       // the xi^2 coefficient
        double published = Published2(k, c1, c0);

        double scale = Math.Max(Math.Abs(published), 1e-12);
        Assert.True(Math.Abs(got - published) / scale < 1e-12,
            $"k={k} c1={c1} c0={c0}: (77.4-5) give {got:G10}, (78.9) gives {published:G10}");

        // And nothing outside xi^2 survives, which (78.9) also asserts.
        Assert.True(Math.Abs(combined.C[1, 1, 0]) < 1e-12, "xi eta should vanish");
        Assert.True(Math.Abs(combined.C[1, 0, 1]) < 1e-12, "xi zeta should vanish");
    }
}
