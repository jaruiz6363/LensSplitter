using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Carries the aspheric primary and secondary contributions into Buchdahl's computing
/// scheme, so that its induced tertiary terms are formed from coefficients that know about
/// the figuring.
///
/// <para><b>Why this is needed.</b> The scheme computes its primary and secondary
/// coefficients from spherical formulae, and builds every induced tertiary term from running
/// sums of them. On a real asphere that is not a small error: on one of the Cooke triplets
/// the figuring takes B from −0.0332 to −0.0006 and flips the sign of F, B5, M1, M3 and N2.
/// Injecting an aspheric tertiary increment into a scheme whose lower orders are still
/// spherical therefore says nothing, however right the increment is.</para>
///
/// <para><b>How the conversion is fixed.</b> This program computes the aspheric primary and
/// secondary already, in the fifth-order code's variables rather than the scheme's. The two
/// differ by one constant per coefficient, and those constants are measured rather than
/// derived: on a spherical system both routes are known to be right, so the ratio of the
/// scheme's entry to the corresponding fifth-order quantity IS the conversion. It comes out
/// identical on every surface, and identical again when the totals are compared instead of
/// the intrinsic parts - which also says the two routes agree on the induced terms.</para>
///
/// <para>The pairing is the one the fifth-order code's own mapping implies: its mu are its
/// s multiplied by a ray angle, so B5, F2, M2, M3 and N3 each carry one factor of the
/// marginal incidence, and the sixth needs Pi5 + C5, which is that same product for S6p.
/// Taking E5 instead pairs S6p through the CHIEF incidence, and the ratio then varies from
/// surface to surface, which is how the right pairing was found.</para>
/// </summary>
public static class AsphericSchemeIncrements
{
    /// <summary>The seven quantities carried across, in the order the scheme wants them.</summary>
    private static Scalar[] MacroSeven(BuchdahlTerms t) =>
        new[] { t.B, t.B5, t.F2, t.M2, t.M3, t.N3, t.Pi5 + t.C5 };

    /// <summary>
    /// All seven are carried. What is verified differs between them, and the difference is
    /// worth stating.
    ///
    /// <para>The PRIMARY is exact everywhere. With the increment applied the scheme's figured
    /// a_p, its barred partner and its last entry all agree with the fifth-order code on
    /// every surface of both aspheric triplets, induced part included.</para>
    ///
    /// <para>The SECONDARY was, when this was written, exact only up to the first figured
    /// surface and out by tens of per cent past it, for want of the same D-and-L split the
    /// primary needed. THAT IS FIXED: <see cref="BuchdahlTableI"/> now runs the secondary
    /// twice, the spherical half on the incidence ratio and the figured half on the height
    /// ratio, with its own dagger family built on the height ratio for the second pass. This
    /// paragraph is kept because the defect it describes is the shape of the one still open
    /// in the tertiary, and because a reader who found the old text would otherwise go looking
    /// for a bug that is no longer there.</para>
    /// </summary>
    private const int Carried = 7;

    /// <summary>The scheme's counterparts: a_p, then the six intrinsic secondary entries.</summary>
    private static Scalar[] SchemeSeven(BuchdahlTableIRow r) =>
        new[] { r[10], r[38], r[44], r[50], r[54], r[59], r[65] };

    /// <summary>
    /// Builds the per-surface increments in the scheme's units, or null when nothing is
    /// figured. Index the result like the surfaces; each entry is
    /// <c>[da_p, dS1p, dS2p, dS3p, dS4p, dS5p, dS6p]</c>.
    /// </summary>
    /// <param name="spherical">
    /// The scheme run WITHOUT figuring, which is what the conversion is measured on.
    /// </param>
    public static Scalar[][]? Build(BuchdahlResult macro, BuchdahlTableIRow[] spherical,
                                    int lastSurface)
    {
        if (macro == null) throw new ArgumentNullException(nameof(macro));
        if (spherical == null) throw new ArgumentNullException(nameof(spherical));

        bool any = false;
        for (int i = 1; i <= lastSurface && i < macro.Aspheric.Length; i++)
            if (macro.Aspheric[i] != null) any = true;
        if (!any) return null;

        // The conversion, taken from whichever surface states each quantity most strongly.
        // It is the same on every surface, so this is only a guard against dividing by a
        // coefficient that happens to vanish.
        var bridge = new Scalar[7];
        var best = new Scalar[7];
        for (int i = 1; i <= lastSurface && i < spherical.Length; i++)
        {
            if (i >= macro.Intrinsic.Length) break;
            var m = MacroSeven(macro.Intrinsic[i]);
            var s = SchemeSeven(spherical[i]);
            for (int q = 0; q < 7; q++)
                if (SMath.Abs(m[q]) > best[q])
                {
                    best[q] = SMath.Abs(m[q]);
                    bridge[q] = s[q] / m[q];
                }
        }

        var result = new Scalar[spherical.Length][];
        for (int i = 1; i <= lastSurface && i < spherical.Length; i++)
        {
            var a = i < macro.Aspheric.Length ? macro.Aspheric[i] : null;
            if (a == null) continue;

            var m = MacroSeven(a);
            var increment = new Scalar[7];
            for (int q = 0; q < Carried; q++) increment[q] = bridge[q] * m[q];
            result[i] = increment;
        }
        return result;
    }

    /// <summary>
    /// The same increments for the DUAL run of the scheme, paper XII Sec. 6(iii): the fifth-order
    /// code on the interchanged paraxial rays with the refractive indices negated, bridged into
    /// the dual scheme exactly as <see cref="Build"/> bridges the direct ones. Null when nothing
    /// is figured.
    ///
    /// <para>Gated in <c>AsphericLadderSurvey.DualityOnFigured</c>: the bridge constants are again
    /// the same on every surface, and the dual run fed these reproduces the five barred q
    /// accumulations the identities recover, to 2E-13, on every figured design without a flat
    /// surface in collimated space. The fifth-order code's F-number, which the interchange can
    /// make infinite, scales only its system totals and never reaches these.</para>
    /// </summary>
    /// <param name="schemeP">The stop parameter the direct increments were bridged at.</param>
    public static Scalar[][]? BuildDual(Models.OpticalSystem system, RayTrace.ParaxialResult paraxial,
                                        Scalar[] indices, Scalar schemeP, Scalar iota)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (indices == null) throw new ArgumentNullException(nameof(indices));

        var negatedN = new Scalar[paraxial.N.Length];
        for (int k = 0; k < negatedN.Length; k++) negatedN[k] = -paraxial.N[k];
        var interchanged = new RayTrace.ParaxialResult
        {
            Y = paraxial.Ybar, U = paraxial.Ubar, Ybar = paraxial.Y, Ubar = paraxial.U,
            N = negatedN,
            Efl = paraxial.Efl, Power = paraxial.Power, Bfl = paraxial.Bfl, Epd = paraxial.Epd,
            EntrancePupilPosition = paraxial.EntrancePupilPosition,
            // n (y ubar - ybar u): the interchange and the negation each flip it, so it stands.
            LagrangeInvariant = paraxial.LagrangeInvariant,
            InfiniteConjugate = paraxial.InfiniteConjugate,
        };

        var negated = new Scalar[indices.Length];
        for (int k = 0; k < indices.Length; k++) negated[k] = -indices[k];

        var macro = BuchdahlCoefficients.Compute(system, interchanged);
        var spherical = BuchdahlTableI.Compute(system.Surfaces, negated, paraxial.Efl, schemeP,
                                               iota: iota, dual: true);
        return Build(macro, spherical, system.LastOpticalSurface());
    }
}
