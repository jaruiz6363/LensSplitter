using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A FIGURED surface standing behind another FIGURED one.
///
/// <para><b>Why these fixtures exist.</b> Every ladder before them carries one asphere, so the
/// surface upstream of it is spherical and contributes no figured half at all. That makes a
/// whole class of readings indistinguishable: each competing account of how a figured half is
/// carried across a surface differs only by a term proportional to the UPSTREAM surface's
/// figured barred secondary, which is identically zero there. Five such variants were measured
/// against <c>Ladder2_A4_Second</c> and all five returned 8.1326 per cent to the last digit.
/// These fixtures put a figured surface upstream so that the term is live.</para>
///
/// <para><b>What each one isolates.</b> The Y-chain recursions reach the answer only multiplied
/// by the current surface's <c>ApFigured</c>, which is proportional to <c>c1</c>. So the two
/// knobs are set independently:</para>
///
/// <code>
///     fixture                          upstream        downstream    SecBarFig   ApFigured
///                                                                    upstream    downstream
///     Ladder2_FiguredSphere_Both       figured sphere  fig. sphere   nonzero     ZERO
///     Ladder2_FiguredSphere_Then_A4    figured sphere  r^4           nonzero     nonzero
///     Ladder2_A4_Then_FiguredSphere    r^4             fig. sphere   nonzero     ZERO
///     Ladder2_A4_Both                  r^4             r^4           nonzero     nonzero
/// </code>
///
/// <para><c>Ladder2_FiguredSphere_Then_A4</c> is the clean isolate: the upstream surface is
/// figured, so the disputed term is live, but it has <c>c1 = 0</c> and so contributes no
/// check-pass primary fault of its own; the downstream surface carries the <c>c1</c> that lets
/// the Y chain reach the answer. Its spherical-upstream twin is <c>Ladder2_A4_Second</c>, the
/// same downstream surface behind a plain sphere, and the pair differ in exactly one thing.</para>
///
/// <para><b>What they showed.</b> The two with a zero downstream <c>ApFigured</c> are invariant
/// under every Y-chain variant tried - <c>Ladder2_A4_Then_FiguredSphere</c> sat at 3.0472 per
/// cent through all of them - which is a direct confirmation that the Y chain reaches the answer
/// by that one path and no other. The clean isolate does separate them, where its
/// spherical-upstream twin could not.</para>
///
/// <para>These tests assert the STRUCTURE, not the open numbers. A fixture that quietly stopped
/// being figured-upstream-of-figured would make the measurements above meaningless while every
/// test still passed, which is the failure mode that produced a bogus 151 per cent reading
/// earlier in this work. The one accuracy assertion is on
/// <c>Ladder2_FiguredSphere_Both</c>, which is exact today and is a real regression guard.</para>
/// </summary>
public class FiguredUpstreamTests
{
    private sealed record Run(BuchdahlTableIRow[] Rows, double[] Rays, double Residual,
                              BuchdahlResult Macro, int Last);

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

        return new Run(rows, inv!.Tau, inv!.Residual, b, sys.LastOpticalSurface());
    }

    /// <summary>
    /// The reason these fixtures exist: the UPSTREAM figured surface must contribute a nonzero
    /// figured barred secondary, or the term under dispute is zero and the fixture separates
    /// nothing. On the single-asphere ladders this quantity IS zero upstream, which is why they
    /// could not tell five different readings apart.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_FiguredSphere_Both")]
    [InlineData("Ladder2_FiguredSphere_Then_A4")]
    [InlineData("Ladder2_A4_Then_FiguredSphere")]
    [InlineData("Ladder2_A4_Both")]
    public void TheUpstreamSurfaceCarriesALiveFiguredHalf(string fixtureName)
    {
        var r = Load(fixtureName);

        int lastFigured = -1;
        for (int i = 1; i <= r.Last; i++)
            if (r.Macro.Aspheric[i] != null) lastFigured = i;
        Assert.True(lastFigured > 0, $"{fixtureName} has no figured surface at all");

        bool live = false;
        for (int i = 1; i < lastFigured; i++)
            for (int m = 0; m < 6; m++)
                if (Math.Abs(r.Rows[i].SecBarFig[m]) > 1e-12) live = true;

        Assert.True(live,
            $"{fixtureName}: every surface upstream of the last figured one has an identically " +
            "zero figured barred secondary, so this fixture is no different from the " +
            "single-asphere ladders and separates nothing.");
    }

    /// <summary>
    /// And the two knobs are set as the class comment claims. The Y chain reaches the answer
    /// only through the current surface's <c>ApFigured</c>, so a fixture built to switch that
    /// path off must actually have it zero, and one built to switch it on must not.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_FiguredSphere_Both", false)]
    [InlineData("Ladder2_A4_Then_FiguredSphere", false)]
    [InlineData("Ladder2_FiguredSphere_Then_A4", true)]
    [InlineData("Ladder2_A4_Both", true)]
    public void TheDownstreamCheckPassPrimaryIsSetAsIntended(string fixtureName, bool expectLive)
    {
        var r = Load(fixtureName);

        int lastFigured = -1;
        for (int i = 1; i <= r.Last; i++)
            if (r.Macro.Aspheric[i] != null) lastFigured = i;

        double ap = Math.Abs(r.Rows[lastFigured].ApFigured);
        if (expectLive)
            Assert.True(ap > 1e-9,
                $"{fixtureName}: surface {lastFigured} has ApFigured {ap:E4}, so the c1 path " +
                "the fixture is meant to exercise is switched off.");
        else
            Assert.True(ap < 1e-18,
                $"{fixtureName}: surface {lastFigured} has ApFigured {ap:E4}, not zero, so the " +
                "c1 path this fixture is meant to switch OFF is live and it no longer isolates " +
                "the carrying rule by itself.");
    }

    /// <summary>
    /// Figured upstream of figured, with <c>c1 = 0</c> on both, is EXACT. Since the figured
    /// halves are live on both surfaces and only the <c>c1</c> path is off, this says the
    /// carrying of a figured half across a surface is already right and the whole of the
    /// remaining aspheric tertiary error is on the <c>c1</c> path.
    /// </summary>
    [Fact]
    public void FiguredUpstreamOfFiguredIsExactWhenC1Vanishes()
    {
        var r = Load("Ladder2_FiguredSphere_Both");
        Assert.True(r.Residual < 1e-3, $"the fit did not close: residual {r.Residual:E2}");

        var t = r.Macro.Totals;
        double Scheme(int k) => k == 1 ? t.B7
            : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        double big = 0.0, worst = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(Scheme(k) - r.Rays[k]) / big);

        Assert.True(worst < 0.005,
            $"worst disagreement is {100 * worst:F4} per cent of the largest coefficient with a " +
            "figured sphere behind a figured sphere, where it was 0.0053. Something in the " +
            "carrying of a figured half across a surface has broken - that path was exact.");
    }
}
