using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A flat surface facing collimated space - the flat side of a plano-convex lens turned toward
/// infinity, a window, a cover glass - and the second of the singular configurations found in
/// the guard audit.
///
/// <para><b>The configuration.</b> With the object at infinity and the surface flat, both the
/// curvature and the marginal ray angle vanish, so <c>i_p = c y_p - v_p = 0</c> and the scheme's
/// ratio <c>q = i_q/i_p</c> is infinite. The code guards on <c>i_p</c> and returns zero.</para>
///
/// <para><b>What that costs.</b> Measured against the R = 1e10 twin, which is the same lens to
/// any precision anyone can measure:</para>
///
/// <code>
///   front radius 1e4 .. 1e10   worst error 0.03 - 0.09 per cent
///   front radius Infinity      155 per cent    before Sec. 84(e)
///                               16 per cent    the PRIMARY family
///                              7.6 per cent    the SECONDARY
///                              2.3 per cent    the t24 ACCUMULATION
///              tau1..tau19 exact, tau20 2.8    t114
///                            0.046 per cent    t155      <- CLOSED
/// </code>
///
/// <para>0.046 per cent is better than the R = 1e10 twin manages (0.067), and the two schemes
/// now agree with each other to 0.000002 per cent of the largest coefficient. What is left is
/// the ordinary accuracy of the ray-inversion oracle, not a defect.</para>
///
/// <para>The scheme is smooth and right all the way down to 1e10 and then jumps. The offending
/// entry is <c>t14 = q c13</c>: in the limit q is -7.47E+07 and c13 is 4.15E-09, a product of
/// -3.10E-01, and the guard makes it zero. From there it corrupts t19, t24, t30, t33 and the
/// whole secondary chain, by 35 to 190 per cent.</para>
///
/// <para><b>What the monograph says to do.</b> Sec. 84(e) observes that the hatted quantities
/// "appear only in the combinations i_p t_mu-p, i_q t_mu-p" and gives (84.51, 52) for those
/// PRODUCTS with no q in them; (84.54) is only the condensed rewrite, obtained by substituting
/// (26.2), b_p = 2 q a_p and c_p = q^2 a_p - varpi/2. The q-free form is the primary one and it
/// is regular. The singularity is an artefact of the condensed arrangement this program follows,
/// paper III Table I, and not of the theory.</para>
///
/// <para>Applied to the primary family it removes most of the error. With a_p = g i^2 and
/// g = (1/2)(v" - i)(k - 1) y / L, the powers of q ride the incidences instead: q a_p = g i i_q,
/// q^2 a_p = g i_q^2, no division anywhere. The barred entry keeps one, through
/// c/i = c/(c y - v), which is exactly 1/y where v vanishes - the curvature path the ray oracle
/// picks out. That took 155 per cent to 16.</para>
///
/// <para>The barred secondary went the same way. Every quantity in the intrinsic chain is a
/// fixed power of i times something regular - P ~ i, t34 ~ i^2, t49 ~ i^3, t58 ~ i^2, t64 ~ i,
/// a_p ~ i^3 - and carrying each as X/i^p gives the six lifts powers 5, 4, 3, 3, 2, 1, so
/// q s_mu = S_mu i_q i^(p-1) is regular for all of them. The apparent divergences, q^4 a_p among
/// them, cancel against those factors of i, which is why a term-by-term lift of the chain fails
/// where this succeeds. The flat surface's own row is now exact: diffed against its R = 1e10
/// twin, nothing differs on it but q itself, which is no longer used for anything.</para>
///
/// <para>The accumulation went the same way. t24 carries the PREVIOUS surface's q against an
/// increment that vanishes with it - the two halves being -(1/2)(v_q" ^2 - v_q^2) and c-bar_p,
/// each of order one and cancelling to order i, so they must be regularised together. Over the
/// common factor the i-free part is -i_q - 1/(y n), which vanishes identically because with
/// v_p = 0 the invariant gives L = -i_q y and n L = H, so i_q = -H/(n y), and the scheme works
/// in units where H = 1. Expanding to first order,
/// <c>q delta = (1/2)(k-1) i_q^2 (c/i) y_q [ (1-k) + 2 y i_q u / L ]</c>, and t24 then agrees
/// with its curvature limit to 0.00003 per cent.</para>
///
/// <para>t114 went the same way. Its bracket collapses to -t98 on a flat surface, and t98
/// vanishes with the incidence by a five-term cancellation whose leading part is
/// t80 = (3/2) t19^2. Per unit incidence
/// <c>t98/i = (t80 - 1.5 t19^2)/i + (t23/i)(2 t19 - t82) + t24 (t81 + t18)/i</c>, and the fourth
/// piece closes because the reduced quantities of the Sec. 84(e) chain do not depend on i_q at
/// all: the intrinsic sixth is a QUARTIC,
/// <c>s6/i = 3 a T i_q^4 + a P i_q^3 + (B - A) i_q^2 + C i_q + D</c>, t80 is that times i_q, and
/// c-bar_p = alpha i_q^3 + beta i_q, so X/i = (c/i) y_q X'(i_q). Checked against the R = 1e10
/// twin the quartic reproduces t80 exactly and X/i to seven figures.</para>
///
/// <para><b>The whole of Table I is now continuous across the limit.</b> Every t[] entry on
/// every surface agrees with its R = 1e10 twin, and nineteen of the twenty coefficients agree to
/// 0.000 per cent.</para>
///
/// <para>The last term was tau20, from <c>TertiaryTotalBar[10]</c>, which on a surface with
/// nothing accumulated is q t155 alone with t155 = t152. Unrolling the intrinsic chain,
/// <c>t152 = q^6 t121 + q^5 t122 + q^4(t123 + 9 t124) + q^3(t125 + t127) + q^2(t126 + t128)
/// + q t129 + t130</c>, and the ten z carry incidence powers 7, 6, 5, 5, 4, 3, 4, 3, 2, 1 - so
/// every term is of order i EXACTLY and there is no cancellation to track, unlike every layer
/// below. Dividing out the common i leaves a polynomial in i_q whose coefficients are the
/// reduced z, and it reproduces t152/i to eight figures at every radius from 1e4 to 1e10. These
/// are identities, not limits.</para>
///
/// <para>Note that fixing t114 moved the aggregate error UP, 2.3 to 2.8, while taking nineteen
/// coefficients to exact. Surface 3's t120 and surface 2's barred tenth were wrong in opposite
/// directions and had been partly cancelling.</para>
/// </summary>
public class FlatSurfaceInCollimatedSpaceTests
{
    private static (double Worst, double Residual) Score(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var inv = CoefficientInversion.Invert(sys, n, p, field);
        Assert.NotNull(inv);

        var t = b.Totals;
        double Scheme(int k) => k == 1 ? t.B7
            : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        double big = 0.0, worst = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(Scheme(k) - inv!.Tau[k]) / big);
        return (worst, inv!.Residual);
    }

    /// <summary>
    /// The limit case is right, which is what makes the exactly-flat case a defect rather than
    /// a hard question: the physics is smooth and the oracle tracks it.
    /// </summary>
    [Fact]
    public void TheCurvatureLimitOfAFlatFaceIsAccurate()
    {
        var (worst, residual) = Score("Ladder2_FlatPlain_NearLimit");

        Assert.True(residual < 1e-3, $"the fit did not close, residual {residual:E2}");
        Assert.True(worst < 0.005,
            $"worst disagreement is {100 * worst:F3} per cent of the largest coefficient at a " +
            "front radius of 1e10, where the scheme was measured at 0.067 per cent. If this " +
            "fails the fault has spread from the exactly-flat case into the ordinary one.");
    }

    /// <summary>
    /// The case itself, and it now holds. Kept at the same threshold as every other rung, so a
    /// regression anywhere in the five layers shows up here.
    /// </summary>
    [Fact]
    public void AnExactlyFlatSurfaceInCollimatedSpaceIsRight()
    {
        var (worst, _) = Score("Ladder2_FlatPlain");
        Assert.True(worst < 0.005,
            $"worst disagreement is {100 * worst:F3} per cent of the largest coefficient.");
    }
}
