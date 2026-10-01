using System;
using System.Collections.Generic;
using System.Reflection;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The ray-inversion oracle: every seventh-order coefficient recovered from exact ray traces
/// alone, and compared with the scheme one at a time.
///
/// <para>This is the check the identities cannot give. They constrain fourteen of the nineteen
/// tau and only as combinations, leaving tau5, tau13, tau18, tau19 and tau20 untouched. The
/// inversion reaches all twenty separately, on any design, spherical or figured.</para>
///
/// <para>It is also the check an RMS spot cannot give, for the opposite reason: a spot
/// collapses eighteen coefficients into one number, where errors cancel. Here each coefficient
/// has its own signature in pupil radius, azimuth and field, and is solved for individually.
/// </para>
/// </summary>
public class CoefficientInversionTests
{
    private static (BuchdahlTerms Scheme, CoefficientInversion.Result Inverted) Run(string name)
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
        return (b.Totals, inv!);
    }

    private static double Scheme(BuchdahlTerms t, int k) =>
        k == 1 ? t.B7 : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

    /// <summary>
    /// On a system of spheres the scheme is right, and the inversion has to agree with it or
    /// the oracle is worthless. Every one of the twenty, individually, including the five no
    /// identity constrains.
    /// </summary>
    [Fact]
    public void TheInversionReproducesTheSchemeOnSpheres()
    {
        var (scheme, inv) = Run("CookeTriplet");

        Assert.True(inv.Residual < 1e-3,
            $"the least-squares fit did not close: residual {inv.Residual:E2}. The traced " +
            "degree-seven data is not representable in the tau basis, so nothing below means " +
            "anything.");

        for (int k = 1; k <= 20; k++)
        {
            double a = Scheme(scheme, k), b = inv.Tau[k];
            double sc = Math.Max(Math.Abs(a), Math.Abs(b));
            if (sc < 1e-9) continue;
            Assert.True(Math.Abs(a - b) / sc < 0.01,
                $"tau{k}: scheme {a:E6}, inverted from rays {b:E6}");
        }
    }

    /// <summary>
    /// The same on a figured design, where it FAILS - which is the point. The fit closes just
    /// as tightly as on the spheres, so the traced data is representable and the inverted
    /// values are sound; it is the scheme that disagrees, by up to 117 per cent.
    ///
    /// <para>The pattern is the finding. Error grows with the FIELD power: tau1, pure aperture,
    /// is exact; tau15 to tau20, which carry five to seven powers of the field, are wrong by
    /// ninety per cent and more. Measured at the commit that added this, on
    /// CookeTriplet_SPOTM_START_LO_ASPHERE.</para>
    ///
    /// <para>This asserts only that the fit closes and that tau1 survives, both of which must
    /// hold however the aspheric tertiary is eventually fixed. The disagreement itself is not
    /// pinned: a test that froze today's wrong numbers would have to be rewritten by whoever
    /// fixes them, and would quietly bless them in the meantime.</para>
    /// </summary>
    [Fact]
    public void TheInversionIsSoundOnAFiguredDesignEvenWhereTheSchemeIsNot()
    {
        var (scheme, inv) = Run("CookeTriplet_SPOTM_START_LO_ASPHERE");

        Assert.True(inv.Residual < 1e-3,
            $"the fit did not close on the figured design: residual {inv.Residual:E2}");

        // tau1 is B7, which the FIFTHORD comparison already pins exactly, so the inversion
        // must return it on a figured design too.
        double a = Scheme(scheme, 1), b = inv.Tau[1];
        Assert.True(Math.Abs(a - b) / Math.Abs(a) < 0.01,
            $"tau1 on a figured design: scheme {a:E6}, inverted {b:E6}");
    }
}
