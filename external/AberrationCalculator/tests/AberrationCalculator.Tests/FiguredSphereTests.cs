using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl's FIGURED SPHERE - Sec. 66(a), the class of figuring with <c>c1 = 0</c>, which
/// leaves the primary aberrations untouched. Neither the conic nor the r^4 term is zero; their
/// combination is, <c>c1 = 8 A4 + conic c^3</c>, so the fixtures are built by choosing one to
/// cancel the other.
///
/// <para><b>What Sec. 66(a) states, and this checks.</b> (66.1) gives</para>
///
/// <code>
///     D(1) = 0D(1),   L(1) = 0
///     D(2) = 0D(2),   L(2) = c-bar_2 y_0 gamma_5^2
/// </code>
///
/// <para>so the figuring adds nothing to D at either order - it is pure L - and the whole effect
/// on the secondary is the addition arising from <c>c-bar_2 y_0 (y_p Y_1 + y_q V_1) xi^2</c>,
/// which "only requires the calculation of <c>c-bar_2 y_p^6 q~^s</c>, s = 0..6", with
/// <c>q~ = y_q/y_p</c> by (66.2).</para>
///
/// <para><b>Why it earns its place.</b> It is a printed answer to test the figured secondary
/// against, rather than a ray fit - and it turned out to isolate the open fault exactly. A
/// figured sphere agrees with the ray oracle to 0.0035 per cent where the same geometry with a
/// pure r^4 asphere is out by 12.3. Sweeping a conic against an r^4 term to carry <c>c1</c>
/// through zero, the disagreement runs 2.39E-07, 1.59E-07, 8.01E-08, 4.02E-08, 1.63E-09,
/// 3.95E-08, 7.91E-08, 1.59E-07, 2.38E-07 - linear in <c>c1</c>, with its zero where
/// <c>c1</c> has one.</para>
/// </summary>
public class FiguredSphereTests
{
    private sealed record Run(BuchdahlResult Macro, BuchdahlTableIRow[] Rows, double[] Rays,
                              double Residual, int Last);

    private static Run Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var inv = CoefficientInversion.Invert(sys, n, p, field);
        Assert.NotNull(inv);

        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        var inc = AsphericSchemeIncrements.Build(b, sph, sys.LastOpticalSurface());
        var rows = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, inc);

        return new Run(b, rows, inv!.Tau, inv!.Residual, sys.LastOpticalSurface());
    }

    /// <summary>
    /// (66.1): the figuring is pure L, so the PRIMARY is untouched. Exactly, not nearly - the
    /// aspheric primary is proportional to c1 in both routes, so it must come out identically
    /// zero and does.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_FiguredSphere")]
    [InlineData("Ladder2_FiguredSphere_First")]
    [InlineData("Ladder2_FiguredSphere_Second")]
    public void ThePrimaryIsUntouchedOnAFiguredSphere(string fixtureName)
    {
        var r = Load(fixtureName);
        int figured = 0;
        for (int i = 1; i <= r.Last; i++)
        {
            if (r.Macro.Aspheric[i] == null) continue;
            figured++;
            var a = r.Macro.Aspheric[i]!;
            foreach (var (name, v) in new[]
                     { ("B", a.B), ("F", a.F), ("C", a.C), ("E", a.E) })
                Assert.True(Math.Abs(v) < 1e-18,
                    $"surface {i}: the fifth-order aspheric {name} is {v:E4} on a figured " +
                    "sphere, where (66.1) requires the primary to be untouched. Either c1 is " +
                    "not zero on this fixture or the figuring is reaching the primary.");

            Assert.True(Math.Abs(r.Rows[i].ApFigured) < 1e-18,
                $"surface {i}: Table I ApFigured is {r.Rows[i].ApFigured:E4}, not zero");
            Assert.True(Math.Abs(r.Rows[i].C13Figured) < 1e-18,
                $"surface {i}: Table I C13Figured is {r.Rows[i].C13Figured:E4}, not zero");
        }
        Assert.True(figured > 0, $"{fixtureName} has no figured surface at all");
    }

    /// <summary>
    /// And the fixture is not vacuous: the figuring is present and reaches the SECONDARY, which
    /// is where (66.1) puts it. Without this the test above would pass on a plain sphere.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_FiguredSphere")]
    [InlineData("Ladder2_FiguredSphere_Second")]
    public void TheFiguringStillReachesTheSecondary(string fixtureName)
    {
        var r = Load(fixtureName);
        bool any = false;
        for (int i = 1; i <= r.Last; i++)
            for (int q = 1; q <= 6; q++)
                if (Math.Abs(r.Rows[i].SecFig[q]) > 1e-12) any = true;

        Assert.True(any,
            $"{fixtureName}: no figured secondary anywhere, so the fixture is not exercising " +
            "any figuring and the primary test above is empty.");
    }

    /// <summary>
    /// The induced stage is exact when the D-side figuring vanishes. This is the sharpest
    /// statement available about the open fault: the same two-surface geometry with a pure r^4
    /// asphere is out by 12.3 per cent, and with c1 traded away to zero it is out by 0.0035.
    ///
    /// <para>Since <c>ApFigured</c> is proportional to <c>c1</c> and vanishes here while
    /// <c>SecFig</c> does not, the induced terms carrying the check SECONDARY are right and the
    /// ones carrying the check PRIMARY are not. That is where the remaining work is.</para>
    /// </summary>
    [Fact]
    public void TheInducedStageIsExactWhenTheDSideFiguringVanishes()
    {
        var r = Load("Ladder2_FiguredSphere_Second");

        Assert.True(r.Residual < 1e-3, $"the fit did not close: residual {r.Residual:E2}");

        var t = r.Macro.Totals;
        double Scheme(int k) => k == 1 ? t.B7
            : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        double big = 0.0, worst = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(Scheme(k) - r.Rays[k]) / big);

        Assert.True(worst < 0.005,
            $"worst disagreement is {100 * worst:F4} per cent of the largest coefficient on a " +
            "figured sphere, where it was 0.0035. The induced stage has stopped being exact " +
            "where the D-side figuring vanishes, which is the one part of it known to work.");
    }
}
