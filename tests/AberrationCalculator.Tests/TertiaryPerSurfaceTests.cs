using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Per-surface INTRINSIC and INDUCED at seventh order, from Table I.
///
/// <para>Table I has always separated the two - each total is its intrinsic plus a run of induced
/// products, and t155 says so outright, <c>4 t19 t65 + t152 + t153 + t154</c> with t152 the
/// intrinsic - but the separation was never recorded, so the program reported system totals only.
/// This is what the seventh-order macro is for: the existing fifth-order one gives neither the
/// seventh order nor the split.</para>
/// </summary>
public class TertiaryPerSurfaceTests
{
    private static BuchdahlTableIRow[] Rows(string name, out int last)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var paraxial = ParaxialTrace.Trace(sys, n, field);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, paraxial.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        last = sys.LastOpticalSurface();
        return BuchdahlTableI.Compute(sys.Surfaces, n, paraxial.Efl, scheme.P);
    }

    /// <summary>
    /// The two-pass capture agrees with the long-standing <c>TertiaryIntrinsic(i)</c> accessor,
    /// on figured systems as well as spherical ones - and it must, for a reason worth recording.
    ///
    /// <para>The intrinsic chain <c>t134..t152</c> is LINEAR in the z quantities it is built
    /// from, and the two passes load the hat and check halves of those, which sum to the whole.
    /// So the intrinsic of the hat pass plus the intrinsic of the check pass is the intrinsic of
    /// a single pass carrying both, exactly. The accessor reads the latter; the capture forms
    /// the former. They differ only in the order the additions happen.</para>
    ///
    /// <para>Which is why the capture is still worth having: the accessor reads whatever the
    /// final pass happened to leave in the table, so it is only as good as that pass being run
    /// and being run last, while the capture is taken from the two passes that actually produce
    /// the totals. And the accessor has no barred partner, which the per-surface coefficients
    /// need.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("Ladder2_A4_Second")]
    public void TheCaptureAgreesWithTheAccessor(string fixtureName)
    {
        var rows = Rows(fixtureName, out int last);

        double biggest = 0.0;
        for (int i = 1; i <= last; i++)
            for (int k = 1; k <= 10; k++)
                biggest = Math.Max(biggest, Math.Abs(rows[i].TertiaryTotal[k]));

        for (int i = 1; i <= last; i++)
            for (int k = 1; k <= 10; k++)
                Assert.True(
                    Math.Abs(rows[i].TertiaryIntrinsic(k) - rows[i].TertiaryIntrinsicTotal[k])
                        < 1e-12 * biggest,
                    $"surface {i}, T{k}: the accessor gives {rows[i].TertiaryIntrinsic(k):E10} " +
                    $"and the two-pass capture {rows[i].TertiaryIntrinsicTotal[k]:E10}. These are " +
                    "the same sum in a different order and should differ only by roundoff.");
    }

    /// <summary>
    /// Nothing precedes the first powered surface, so it inherits nothing and induces nothing.
    /// This is not imposed anywhere - the induced terms are products with quantities accumulated
    /// over the surfaces before, and there are none - so it is a real check that the split is
    /// being read off the right entries.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_Sphere")]
    public void TheFirstPoweredSurfaceInducesNothing(string fixtureName)
    {
        var rows = Rows(fixtureName, out int last);

        int first = 0;
        double biggest = 0.0;
        for (int i = 1; i <= last; i++)
            for (int k = 1; k <= 10; k++)
                biggest = Math.Max(biggest, Math.Abs(rows[i].TertiaryTotal[k]));

        for (int i = 1; i <= last && first == 0; i++)
            for (int k = 1; k <= 10; k++)
                if (Math.Abs(rows[i].TertiaryIntrinsicTotal[k]) > 1e-12 * biggest) { first = i; break; }

        Assert.True(first > 0, "no surface has any intrinsic tertiary at all");
        for (int k = 1; k <= 10; k++)
        {
            double induced = rows[first].TertiaryTotal[k] - rows[first].TertiaryIntrinsicTotal[k];
            Assert.True(Math.Abs(induced) < 1e-12 * biggest,
                $"the first powered surface ({first}) induces {induced:E6} in T{k}, with nothing " +
                "before it to inherit from");
        }
    }

    /// <summary>
    /// And the split is not vacuous: on a real triplet the induced part is not a rounding
    /// correction but the larger share on most surfaces, which is the whole reason for reporting
    /// it separately.
    /// </summary>
    [Fact]
    public void TheInducedPartIsSubstantialOnARealSystem()
    {
        var rows = Rows("CookeTriplet", out int last);

        int dominated = 0;
        for (int i = 1; i <= last; i++)
        {
            double intrinsic = Math.Abs(rows[i].TertiaryIntrinsicTotal[1]);
            double induced = Math.Abs(rows[i].TertiaryTotal[1] - rows[i].TertiaryIntrinsicTotal[1]);
            if (induced > intrinsic) dominated++;
        }

        Assert.True(dominated >= 3,
            $"only {dominated} of {last} surfaces have their seventh-order spherical aberration " +
            "dominated by what they inherit. On a Cooke triplet most of them do, and if that has " +
            "stopped being true the split is probably being read off the wrong entries.");
    }
}
