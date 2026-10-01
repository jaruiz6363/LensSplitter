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
/// A ladder of minimal designs that switches Buchdahl's INDUCED terms on and off, to separate
/// them from the intrinsic ones on a figured surface.
///
/// <para><b>The device.</b> An induced contribution is a product of what a surface has of its
/// own with what the surfaces ahead of it have accumulated. Give a system exactly ONE powered
/// surface and there is nothing ahead to accumulate, so every induced term vanishes identically
/// and what remains is intrinsic. Add a second powered surface and they switch on. The plano
/// stop ahead of the lens gives a real stop shift q without contributing anything itself, a
/// plane having no power.</para>
///
/// <para><b>Why it was needed.</b> The aspheric tertiary WAS wrong and had resisted localisation
/// for a long time, because every design it had been measured on was a six-surface triplet
/// where intrinsic and induced content are inseparable. On the ladder they separate completely.
/// </para>
///
/// <para><b>THE DEFECT THIS LADDER LOCATED IS FIXED.</b> The reconstruction was completed in
/// "The sixth member by duality" (14 September 2026), and every rung below now agrees with
/// Forbes. The table that follows is the state BEFORE that, kept because it is the evidence
/// that located the defect and because the shape of the failure is what named the mechanism.
/// See <c>docs/verification.md</c>, <i>The aspheric arrangement, and how it was established</i>,
/// for the settled account.</para>
///
/// <para><b>What it found</b>, as the worst relative error over the twenty coefficients, each
/// against itself, with Forbes' series as the reference - and what the same measurement reads
/// today:</para>
///
/// <code>
///                                                        THEN            NOW
///   one powered surface, spherical        no induced     none out by 1%  9E-14
///   one powered surface, r^4 figured      no induced     none            4E-12
///   one powered surface, conic figured    no induced     none            2E-13
///   two powered surfaces, spherical       induced        none            9E-13
///   two powered surfaces, figured SPHERE  induced        none            2E-12
///   two powered surfaces, r^4 on first    induced        31%,  8 of 20   4E-13
///   two powered surfaces, r^4 on second   induced        40%, 19 of 20   5E-13
///   three surfaces, r^4 in the middle     induced        19%, 12 of 20   9E-13
/// </code>
///
/// <para>The NOW column is the worst relative error over the twenty, not a percentage, and it
/// holds across all twenty-six rungs of the ladder. The three near-degenerate rungs are the
/// exception and are expected to be: <c>Ladder2_FlatFigured</c> reads 1.2E-9 through the
/// Laurent route, <c>Ladder2_FiguredNearFlatRear</c> 1E-7 and
/// <c>Ladder2_FlatPlain_NearLimit</c> 6E-7, all three limited by the designs rather than by
/// the arrangement.</para>
///
/// <para><b>Why the failing shape was worth recording.</b> The INTRINSIC aspheric tertiary was
/// never merely as good as a spherical one, it was EXACT - none of the twenty out by one per
/// cent - so the whole error was in the induced stage. The failing cases decomposed without
/// ambiguity, because the spherical rungs were clean and the one-surface figured ones were
/// clean: with the figuring first, a SPHERICAL surface's induced terms are built from
/// accumulated FIGURED content; with it second, a FIGURED surface's are built from accumulated
/// SPHERICAL content. That is what sent the search to the barred q accumulation.</para>
///
/// <para><b>A figured SPHERE was the control that named the mechanism.</b> Buchdahl's Sec. 66a
/// figuring has <c>8A4 + Kc^3 = 0</c>, so the figuring's PRIMARY contribution vanishes while its
/// sixth-order content does not; those rungs pushed as much figuring through the induced stage
/// as the r^4 ones and came out exact even then. The induced stage did not fail on figuring -
/// it failed on the figuring's primary content. Those rungs are still run, now as a floor
/// rather than as a contrast.</para>
///
/// <para><b>These figures are not the ones this file was written with.</b> It originally scored
/// each error as a share of the LARGEST coefficient in the set, which is blind to exactly the
/// failures this arrangement has: <c>docs/verification.md</c> records tau15 out by a factor of
/// nearly five and tau20 by half while the large ones agree to one per cent, and under that
/// normalisation the design that does it reported as 1.7 per cent. The rungs above read 0.026,
/// 0.112 and 12.282 per cent then. Nothing about the scheme changed; the instrument did.</para>
///
/// <para><b>The once-failing rungs are still not pinned to a number, and that is now a gap
/// rather than a decision.</b> While they were wrong, freezing their values would have blessed
/// them, so <c>TheOracleIsSoundOnTheRungsWhereTheInducedStageFails</c> asserts only that the
/// fit closes - which is what makes the oracle worth believing at all. Now that those rungs
/// agree with Forbes to 5E-13 they could be gated like every other rung, and until they are,
/// this file would not catch the defect coming back. <c>BuchdahlAsphericSchemeTests</c> would;
/// the ladder is where it should be visible.</para>
/// </summary>
public class AsphericInducedLadderTests
{
    /// <summary>The scheme coefficients alone, tau1..tau20, without tracing anything.</summary>
    private static Func<int, double> Coefficients(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var t = b.Totals;
        return k => k == 1 ? t.B7
                  : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;
    }

    /// <summary>
    /// The worst RELATIVE error over the twenty coefficients, each against itself, and the
    /// ray-inversion residual that says whether the rung means anything at all.
    ///
    /// <para><b>This used to divide every error by the largest coefficient in the set, and that
    /// was wrong.</b> <c>docs/verification.md</c> records this arrangement getting tau15 out by a
    /// factor of nearly five including its sign and tau20 by half, while the large ones agree to
    /// one per cent - and divided by the largest coefficient, a small one wrong by five times is
    /// nothing. The instrument reported the design that does exactly that as 1.7 per cent. A gate
    /// built on it can pass while a coefficient is out by a factor of ten.</para>
    ///
    /// <para>The reference is FORBES rather than the ray inversion, for the same reason. Relative
    /// error is meaningless at the inversion's noise floor, which is what the old normalisation
    /// was working around; Forbes' series is an expansion rather than a least-squares recovery
    /// from traced landings, so it has no floor of its own and tracks the rays to between 0.001
    /// and 0.13 per cent on every design here. The inversion is still run, because its RESIDUAL
    /// is what says the traced data is representable in the tau basis at all - without that, a
    /// disagreement on this rung could not be blamed on the scheme.</para>
    /// </summary>
    private static (double WorstRelative, double Residual) Score(string fixtureName)
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

        var forbes = ForbesCoefficients.Invert(sys, n, p, field);
        Assert.NotNull(forbes);

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        Assert.True(big > 0.0, $"{fixtureName}: every coefficient is zero");

        double worst = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            double reference = forbes!.Tau[k];

            // A coefficient that is zero to within a billionth of the largest has no signal to
            // take a relative error against, and dividing by it would manufacture one.
            if (Math.Abs(reference) < 1e-9 * big) continue;

            worst = Math.Max(worst, Math.Abs(Scheme(k) - reference) / Math.Abs(reference));
        }

        return (worst, inv!.Residual);
    }

    /// <summary>
    /// The ladder itself has to be sound before anything read off it means something: the fit
    /// must close on every rung, and the spherical rungs must come out right, with and without
    /// induced terms.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("Ladder3_Sphere")]
    public void TheSphericalRungsAreCorrect(string fixtureName)
    {
        var (worst, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the least-squares fit did not close, residual {residual:E2}. " +
            "Nothing read off this rung means anything.");
        Assert.True(worst < 0.005,
            $"{fixtureName}: worst RELATIVE error over the twenty is {100 * worst:F3} per cent, " +
            "on a system of SPHERES where the scheme is known right - it agrees with Forbes to " +
            "roundoff there. The ladder or the oracle has broken, not the aspheric tertiary.");
    }

    /// <summary>
    /// The finding: with the induced terms switched off, a FIGURED surface is as accurate as a
    /// spherical one. Both kinds of figuring - the conic, which reaches the coefficients by one
    /// path, and the r^4 polynomial term, which reaches them by another.
    ///
    /// <para>This is what says the intrinsic side is finished and the remaining work is all in
    /// the induced stage. If it ever fails, that conclusion is void.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder1_A4")]
    [InlineData("Ladder1_Conic")]
    public void TheIntrinsicAsphericTertiaryIsCorrectWhereNoInducedTermsExist(string fixtureName)
    {
        var (worst, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the fit did not close, residual {residual:E2}");
        Assert.True(worst < 0.005,
            $"{fixtureName}: worst RELATIVE error over the twenty is {100 * worst:F3} per cent. " +
            "On a single powered surface there are NO induced terms, so this is the intrinsic " +
            "aspheric tertiary alone, and NONE of the twenty was out by one per cent when this " +
            "was measured - it is exact, not merely close.");
    }

    /// <summary>
    /// Figuring that is present must be read whether or not the surface carries the label for
    /// it. The two fixtures here are the same lens, differing only in <c>Type</c>: one says
    /// <c>EvenAsphere</c>, the other <c>Standard</c>, and both have the same r^4 coefficient.
    ///
    /// <para>They must give the same coefficients, because the ray trace gives the same rays.
    /// Before this test the scheme skipped the figuring on the unlabelled one and returned the
    /// SPHERICAL answer, which reads as a 151 per cent error in tau1 - a coefficient that is
    /// in fact computed correctly. Every reader in the repository sets the label, so no file
    /// on disk was ever affected; a hand-edited file or a system built through the API is.
    /// </para>
    /// </summary>
    [Fact]
    public void FiguringIsReadWhetherOrNotTheSurfaceIsLabelledAnAsphere()
    {
        var labelled = Coefficients("Ladder1_A4");
        var unlabelled = Coefficients("Ladder1_A4_Unlabelled");

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(labelled(k)));

        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(labelled(k) - unlabelled(k)) / big < 1e-12,
                $"tau{k}: labelled EvenAsphere gives {labelled(k):E6}, the same lens left " +
                $"Standard gives {unlabelled(k):E6}. The figuring is present on both.");
    }

    /// <summary>
    /// THE THIRD RUNG: a figured surface with a powered surface on BOTH sides of it.
    ///
    /// <para>Ladder2 cannot produce this. Figure its first powered surface and nothing has
    /// accumulated ahead for the induced terms to be built from; figure its second and nothing
    /// downstream ever reads what it accumulates. Several terms of the arrangement - M (68.8)'s
    /// bracket among them, which is built from the accumulations ahead of a figured surface and
    /// read by the surface after it - are therefore identically zero on every rung of it, and a
    /// reading of them cannot be tested at all. The triplets do exercise those terms, but a
    /// six-surface triplet mixes intrinsic and induced content in exactly the way the ladder was
    /// built to avoid.</para>
    ///
    /// <para><b>These fixtures were built in a lens design program rather than generated.</b>
    /// A generated fixture and the code under test can share an assumption, and then the fixture
    /// is shaped by the very error it is meant to expose. Ladder3 is a cemented doublet - three
    /// powered surfaces, no flats among them, the same remote plano stop, aperture, field and
    /// wavelength as the rest of the family, and its first two surfaces identical to Ladder2's
    /// so that the two compare directly.</para>
    ///
    /// <para>What it is: surfaces 2 and 3 as Ladder2 has them, +50 in SK16 and -83.3333, but the
    /// second is now a cemented interface into SF10 with a third powered surface at -60 behind
    /// it. The figuring goes on the MIDDLE one.</para>
    /// </summary>
    [Fact]
    public void TheMiddleRungIsSoundAndItsSphericalTwinIsCorrect()
    {
        var (worstSpherical, residualSpherical) = Score("Ladder3_Sphere");
        Assert.True(residualSpherical < 1e-3,
            $"Ladder3_Sphere: the fit did not close, residual {residualSpherical:E2}");
        Assert.True(worstSpherical < 0.005,
            $"Ladder3_Sphere: worst disagreement with the rays is {100 * worstSpherical:F3} per "
          + "cent on a system of SPHERES. The fixture is at fault, not the theory - it was "
          + "exact - none of the twenty out by one per cent - when it was built.");

        var (_, residualFigured) = Score("Ladder3_A4_Middle");
        Assert.True(residualFigured < 1e-3,
            $"Ladder3_A4_Middle: the fit did not close, residual {residualFigured:E2}. Nothing "
          + "read off this rung means anything.");
    }

    /// <summary>
    /// The control that goes with it. A figured SPHERE in Buchdahl's sense (Sec. 66a) has
    /// <c>8A4 + Kc^3 = 0</c>, so <c>alpha = c-_1 y_p^4</c> of M (67.1) vanishes and with it every
    /// term that carries the figuring's primary. It must therefore stay at the ray oracle's
    /// floor whatever reading of Sec. 85 is in force, on the middle rung as on the others.
    ///
    /// <para>If this ever fails, a reading has moved something that carries no figured primary,
    /// and it is refuted without reference to whether it improved anything else.</para>
    ///
    /// <para><b>But this particular rung is a WEAK control, and nothing much should be read into
    /// its passing.</b> Measured against its own r^4 twin, it drives the figured secondary to
    /// 1.6E-3 against 3.0E-2 and the figured tertiary to 2.3E-3 against 2.1E-2 - about a
    /// twentieth and a ninth - and the figuring moves the coefficients by 0.08 per cent against
    /// 5.1. Two reasons: the middle surface is a cemented interface, where the index step is
    /// 0.108 against an air-glass 0.62, and a conic of -0.6 puts very little into the r^6 term
    /// that is all a figured sphere has left. Its alpha is not exactly zero either - -5.3E-6,
    /// from the A4 in the fixture being 0.12 per cent off the exact 8A4 + Kc^3 = 0.</para>
    ///
    /// <para>The control that the localisation actually rests on is
    /// <c>TheFiguredSphereRungsAreStrongControls</c> below, on Ladder1 and Ladder2, where the
    /// figured secondary and tertiary reach 88 and 87 per cent of their r^4 twins with alpha
    /// exactly zero. To make this rung comparable it wants a deeper conic with the matching A4 -
    /// for R = -83.3333, K = -3 and A4 = -6.4800078E-7.</para>
    /// </summary>
    [Fact]
    public void TheFiguredSphereOnTheMiddleRungStaysAtTheFloor()
    {
        var (worst, residual) = Score("Ladder3_FiguredSphere_Middle");

        Assert.True(residual < 1e-3,
            $"Ladder3_FiguredSphere_Middle: the fit did not close, residual {residual:E2}");
        Assert.True(worst < 0.005,
            $"Ladder3_FiguredSphere_Middle: worst disagreement with the rays is "
          + $"{100 * worst:F3} per cent. Its figuring has no primary content, so the induced "
          + "stage has nothing to get wrong, and it was exact when this was written.");
    }

    /// <summary>
    /// <b>The figured-sphere control is a real control, and this is what says so.</b>
    ///
    /// <para>The whole localisation turns on one comparison: figured spheres come out exact and
    /// r^4 figuring does not, so the defect is in the figuring's PRIMARY content. That inference
    /// is worth exactly as much as the control is. A figured sphere is a sphere to fourth order
    /// in sag - the conic's r^4 term is <c>(1+K)c^3/8</c> and Buchdahl's <c>A4 = -Kc^3/8</c>
    /// leaves <c>c^3/8</c>, which is the sphere's - so it CANNOT have a figured primary. The
    /// question is whether anything else survives, or whether it is a null test.</para>
    ///
    /// <para>It is not. On these rungs the figured secondary and the figured tertiary reach most
    /// of what their r^4 twins reach, with alpha exactly zero:</para>
    ///
    /// <code>
    ///                                  alpha   figured sec   figured tert
    ///   Ladder1_FiguredSphere          0.000       2.096         16.79
    ///   Ladder1_A4                     1.290       2.368         19.19
    ///   Ladder2_FiguredSphere_First    0.000       0.212          3.62
    ///   Ladder2_A4_First               0.326       0.378          4.85
    /// </code>
    ///
    /// <para>So the same code paths run at 56 to 88 per cent of full strength with one input
    /// switched off, which is what a single-variable experiment is supposed to look like.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder1_FiguredSphere", "Ladder1_A4")]
    [InlineData("Ladder2_FiguredSphere_First", "Ladder2_A4_First")]
    public void TheFiguredSphereRungsAreStrongControls(string control, string twin)
    {
        var (alpha, sec, tert) = Figuring(control);
        var (_, secTwin, tertTwin) = Figuring(twin);

        Assert.True(alpha == 0.0,
            $"{control}: a figured sphere has no figured primary by construction - its r^4 sag "
          + $"is a sphere's - yet alpha is {alpha:E3}. The fixture does not satisfy "
          + "8A4 + Kc^3 = 0, and the control is not the one it claims to be.");

        Assert.True(sec > 0.4 * secTwin,
            $"{control}: the figured secondary is {sec:E3} against {secTwin:E3} on {twin}. Too "
          + "little of the aspheric path is being exercised for this to be a control at all.");

        Assert.True(tert > 0.4 * tertTwin,
            $"{control}: the figured tertiary is {tert:E3} against {tertTwin:E3} on {twin}. "
          + "Same objection.");
    }

    /// <summary>The three things the scheme carries for a figured surface, at its strongest.</summary>
    private static (double Alpha, double Secondary, double Tertiary) Figuring(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        double objectDistance = sys.Surfaces[0].Thickness;
        double iota = double.IsInfinity(objectDistance) ? 0.0 : -p.Efl / objectDistance;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter,
                                            iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(b, spherical, sys.LastOpticalSurface());
        var rows = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, increments,
                                          iota: iota);

        double alpha = 0.0, sec = 0.0, tert = 0.0;
        for (int i = 1; i < sys.Surfaces.Count - 1; i++)
        {
            alpha = Math.Max(alpha, Math.Abs(rows[i].ApFigured));
            for (int m = 1; m <= 6; m++) sec = Math.Max(sec, Math.Abs(rows[i].SecFig[m]));
            for (int m = 1; m <= 10; m++) tert = Math.Max(tert, Math.Abs(rows[i].ZCheck[m]));
        }
        return (alpha, sec, tert);
    }

    /// <summary>
    /// The rungs where the scheme USED to be wrong, before the reconstruction was completed.
    /// Only the soundness of the oracle is asserted, which is all that could be asserted while
    /// they disagreed; all three now agree with Forbes to 5E-13 and the assertion has not been
    /// tightened to match. See the class remarks.
    /// </summary>
    /// <remarks>The name is kept so the history stays searchable, and there are three of them,
    /// not two as the old class remarks said.</remarks>
    [Theory]
    [InlineData("Ladder2_A4_First")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("Ladder3_A4_First")]
    public void TheOracleIsSoundOnTheRungsWhereTheInducedStageFails(string fixtureName)
    {
        var (_, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the fit did not close, residual {residual:E2}. The traced " +
            "degree-seven data is not representable in the tau basis, so the disagreement on " +
            "this rung could not be blamed on the scheme.");
    }
}
