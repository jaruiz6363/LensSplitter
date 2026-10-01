using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The per-surface breakdown by Forbes' route.
///
/// <para>Every property checked here follows from how the decomposition is built rather than from
/// a number someone measured, so they are checked to roundoff. That is the point of building it
/// this way: a decomposition that has to be arranged to add up can be arranged wrongly, and this
/// one cannot.</para>
///
/// <para><b>What is NOT claimed.</b> These contributions do not agree with
/// <see cref="BuchdahlResult.PerSurface"/> and are not expected to. Both sum to the same system
/// total, but they attribute it differently, and on a nonlinear system many attributions are
/// possible. See the class comment on <see cref="ForbesPerSurface"/> and the note in
/// <c>docs/forbes.md</c>.</para>
/// </summary>
public class ForbesPerSurfaceTests
{
    private sealed record Setup(ForbesPerSurface.Breakdown Breakdown, double[] Total,
                                double Largest, int Last, bool[] Figured);

    private static Setup Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var paraxial = ParaxialTrace.Trace(sys, n, field);

        var breakdown = ForbesPerSurface.Compute(sys, n, paraxial, field);
        var total = ForbesCoefficients.Invert(sys, n, paraxial, field);
        Assert.NotNull(breakdown);
        Assert.NotNull(total);

        int last = sys.LastOpticalSurface();
        var figured = new bool[last + 1];
        for (int i = 1; i <= last; i++)
        {
            var s = sys.Surfaces[i];
            if (Math.Abs(s.Conic) > 0) figured[i] = true;
            for (int k = 1; k < s.AsphericCoefficients.Length; k++)
                if (s.AsphericCoefficients[k] != 0.0) figured[i] = true;
        }

        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(total!.Tau[k]));
        return new Setup(breakdown!, total!.Tau, largest, last, figured);
    }

    /// <summary>The surfaces and the reference reproduce the system totals, to roundoff.</summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheContributionsAddUpToTheTotal(string fixtureName)
    {
        var s = Load(fixtureName);
        for (int k = 1; k <= 20; k++)
        {
            double sum = s.Breakdown.Reference[k];
            foreach (var c in s.Breakdown.Surfaces) sum += c.Total[k];
            Assert.True(Math.Abs(sum - s.Total[k]) < 1e-12 * s.Largest,
                $"tau{k}: the surfaces and the reference come to {sum:E10} against a system total " +
                $"of {s.Total[k]:E10}. This is meant to telescope exactly.");
        }
    }

    /// <summary>Intrinsic, aspheric and induced are a partition of each surface's share.</summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheThreePartsArePreciselyTheWhole(string fixtureName)
    {
        var s = Load(fixtureName);
        foreach (var c in s.Breakdown.Surfaces)
            for (int k = 1; k <= 20; k++)
            {
                double parts = c.Intrinsic[k] + c.Aspheric[k] + c.Induced[k];
                Assert.True(Math.Abs(parts - c.Total[k]) < 1e-12 * s.Largest,
                    $"surface {c.Surface}, tau{k}: the three parts come to {parts:E10} against a " +
                    $"contribution of {c.Total[k]:E10}.");
            }
    }

    /// <summary>
    /// The first surface has nothing before it, so it induces nothing. This is not imposed - it
    /// falls out because the aberration reaching the first surface is the reference itself, which
    /// is subtracted from both terms of the difference that defines the induced part.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheFirstSurfaceInducesNothing(string fixtureName)
    {
        var s = Load(fixtureName);
        var first = s.Breakdown.Surfaces[0];
        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(first.Induced[k]) < 1e-14 * s.Largest,
                $"tau{k} induced at the first surface is {first.Induced[k]:E10}, and nothing " +
                "precedes it.");
    }

    /// <summary>
    /// The aspheric part is exactly zero on a surface with no figuring, and not zero on one with
    /// figuring. Without the second half the first would pass on a route that never looked at the
    /// figure at all.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheAsphericPartTracksWhichSurfacesAreFigured(string fixtureName)
    {
        var s = Load(fixtureName);
        foreach (var c in s.Breakdown.Surfaces)
        {
            double worst = 0.0;
            for (int k = 1; k <= 20; k++) worst = Math.Max(worst, Math.Abs(c.Aspheric[k]));

            if (s.Figured[c.Surface])
                Assert.True(worst > 1e-10 * s.Largest,
                    $"surface {c.Surface} is figured and its aspheric part is {worst:E4}");
            else
                Assert.True(worst == 0.0,
                    $"surface {c.Surface} carries no figuring and its aspheric part is {worst:E4}, " +
                    "which should be identically zero rather than merely small");
        }
    }

    /// <summary>
    /// The reference term is seventh-order distortion and nothing else. It is a property of the
    /// coordinate convention - a paraxially perfect system launched with direction cosines lands
    /// near efl sin(theta) where the coefficients are referred to efl tan(theta) - so it carries
    /// no aperture and shows up in tau20 alone. If it ever appeared elsewhere, the linearised
    /// trace would be introducing aberration and the whole decomposition would rest on nothing.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheReferenceIsPureDistortion(string fixtureName)
    {
        var s = Load(fixtureName);
        for (int k = 1; k <= 19; k++)
            Assert.True(Math.Abs(s.Breakdown.Reference[k]) < 1e-10 * s.Largest,
                $"the all-paraxial system shows tau{k} = {s.Breakdown.Reference[k]:E6}. Only " +
                "tau20 may be non-zero there; anything else means a linearised step is not linear.");

        Assert.True(Math.Abs(s.Breakdown.Reference[20]) > 1e-6 * s.Largest,
            "the reference tau20 has vanished, so this test is no longer checking anything");
    }
}
