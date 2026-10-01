using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// That the tertiary coefficients reach the reported coefficient set at all.
///
/// <para><b>Why this exists.</b> For the whole life of the tertiary work they did not.
/// <see cref="TertiaryCoefficients.Compute"/> was written, validated against Buchdahl's
/// published values, checked against traced ray fans and against a closed-form conic - and
/// called by nothing outside this test project. Every report the program produced carried
/// tau1 and NINETEEN ZEROS, and <see cref="Prms"/>, which reads eighteen of the twenty,
/// predicted spots from a set it had never been given.</para>
///
/// <para>Nothing caught it because every tertiary test assembled its own coefficients - the
/// scheme, the stop parameter, the aspheric increments, the transverse conversion - and then
/// wrote them into a <see cref="BuchdahlTerms"/> by reflection. Each one proved the
/// arithmetic and none of them touched the path a user gets. The tests below use the shipping
/// path deliberately, and would have failed on day one.</para>
/// </summary>
public class TertiaryWiringTests
{
    private static (Core.Models.OpticalSystem Sys, double[] N, double Field) Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        return (sys, n, field);
    }

    /// <summary>The coefficient set as the program actually produces it.</summary>
    private static BuchdahlTerms Shipped(string name)
    {
        var (sys, n, field) = Load(name);
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return b.Totals;
    }

    /// <summary>
    /// The one that matters. Nineteen coefficients, every one of them zero, on designs whose
    /// tertiary content is known to be substantial.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheReportedSetCarriesTheTertiaryCoefficients(string design)
    {
        var t = Shipped(design);

        var zero = new List<string>();
        for (int i = 2; i <= 20; i++)
            if (t["Tau" + i] == 0.0) zero.Add("tau" + i);

        Assert.True(zero.Count == 0,
            $"{design}: {zero.Count} of the nineteen tertiary coefficients are exactly zero "
          + $"in the shipped set ({string.Join(", ", zero)}). Exact zeros mean they were never "
          + "computed - see TertiaryCoefficients.Attach.");
    }

    /// <summary>
    /// The shipped path must agree with the hand assembly the other tertiary tests do, or
    /// those tests are validating arithmetic that no user ever reaches.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheShippedSetMatchesTheHandAssembledOne(string design)
    {
        var (sys, n, field) = Load(design);
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);

        double u = -1.0 / (2.0 * b.FNumber);
        double hmax = Math.Tan(field * Math.PI / 180.0);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        var increments = AsphericSchemeIncrements.Build(b, spherical, sys.LastOpticalSurface());

        // The two routines: spheres through Table I, a figured system through the aspheric one
        // with the dual increments its sixth barred member needs.
        var raw = increments == null
            ? TertiaryCoefficients.Compute(sys.Surfaces, n, p.Efl, scheme.P, null)
            : BuchdahlAsphericScheme.Tau(sys.Surfaces, n, p.Efl, scheme.P, increments, 0.0,
                  BuchdahlAsphericScheme.Options.Default,
                  AsphericSchemeIncrements.BuildDual(sys, p, n, scheme.P, 0.0));
        var expected = TertiaryCoefficients.ToTransverse(raw, p.Efl, u, hmax, b.Totals.B7);

        var shipped = Shipped(design);
        for (int i = 2; i <= 20; i++)
            Assert.Equal(expected[i], shipped["Tau" + i], 12);
    }

    /// <summary>
    /// And that it changes the answer. A wiring test that passes while the numbers it carries
    /// make no difference would be measuring its own plumbing.
    ///
    /// <para>The threshold is deliberately loose. What is being caught is an INERT set - the
    /// nineteen zeros this suite was written for - not a particular magnitude, and the
    /// magnitude moves whenever the coefficients themselves are corrected. The Sec. 85
    /// two-pass took it from just over five per cent to 4.7, which says nothing about either
    /// version except that both carry real numbers.</para>
    /// </summary>
    [Fact]
    public void TheTertiaryCoefficientsMoveThePredictedSpot()
    {
        var full = Shipped("CookeTriplet_SPOTM_START_LO_ASPHERE");

        var withoutTertiary = full.Clone();
        for (int i = 2; i <= 20; i++)
            typeof(BuchdahlTerms).GetField("Tau" + i)!.SetValue(withoutTertiary, 0.0);

        double with = Prms.Value(full, 0.85);
        double without = Prms.Value(withoutTertiary, 0.85);

        Assert.True(Math.Abs(with - without) / without > 0.02,
            "zeroing tau2..tau20 should move the predicted spot appreciably at "
          + $"H = 0.85; it moved it from {without:E4} to {with:E4}.");
    }

    /// <summary>
    /// The screen reads the same wired set. It was written while the coefficients were still
    /// zero, and reported - correctly, of the numbers, and uselessly - that the three suspects
    /// carried none of the spot on every design put through it.
    /// </summary>
    [Fact]
    public void TheScreenSeesTheSuspectCoefficients()
    {
        var (sys, n, field) = Load("CookeTriplet_SPOTM_START_LO_ASPHERE");
        var screen = AsphericDiagnostic.Screen(sys, n, field, 0.85);

        Assert.True(screen.SuspectShare > 0.0, "the suspects must carry some of the spot");
        Assert.True(screen.AsphericLeverage > 0.0, "the figuring must drive some of them");
        Assert.All(screen.Suspects, s => Assert.NotEqual(0.0, s.Figured));
    }

    /// <summary>
    /// Screening must not alter the design it screens. It strips the conics and aspheric terms
    /// to get its bare reference, and a caller handing in its own system should not find the
    /// figuring gone afterwards.
    /// </summary>
    [Fact]
    public void ScreeningLeavesTheDesignAlone()
    {
        var (sys, n, field) = Load("CookeTriplet_SPOTM_START_LO_ASPHERE");

        var conics = new List<double>();
        var terms = new List<double[]>();
        foreach (var s in sys.Surfaces)
        {
            conics.Add(s.Conic);
            terms.Add((double[])s.AsphericCoefficients.Clone());
        }

        AsphericDiagnostic.Screen(sys, n, field);

        for (int i = 0; i < sys.Surfaces.Count; i++)
        {
            Assert.Equal(conics[i], sys.Surfaces[i].Conic);
            Assert.Equal(terms[i], sys.Surfaces[i].AsphericCoefficients);
        }
    }
}
