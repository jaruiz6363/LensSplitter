using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// What one surface contributes to the coefficient of QUATERNARY - ninth-order - spherical
/// aberration, and the intermediate rows the contribution is built from.
/// </summary>
public sealed class QuaternaryRow
{
    /// <summary>The accumulated tertiary spherical, <c>Sum_1^(j-1) t132 = T1p</c>. Buchdahl's r1.</summary>
    public Scalar R1;

    /// <summary>Its barred partner, <c>Sum_1^(j-1) t133 = T1p bar</c>. Buchdahl's r2.</summary>
    public Scalar R2;

    /// <summary>
    /// The DAGGERED tertiary spherical <c>T1+</c>, Buchdahl's r3, which Eq. (3.1) needs and the
    /// tertiary scheme does not carry. See <see cref="QuaternarySpherical"/> for why this does
    /// not require a dual run.
    /// </summary>
    public Scalar T1Dagger;

    /// <summary>Buchdahl's r4, <c>(1/4)(t9* - t9)t10</c>.</summary>
    public Scalar R4;

    /// <summary>Buchdahl's r5 and r6, the two bracket terms of the intrinsic coefficient.</summary>
    public Scalar R5, R6;

    /// <summary>
    /// The INTRINSIC contribution <c>q1p</c> of Eq. (2.15) - what this surface generates on its
    /// own, before anything inherited from the surfaces ahead of it.
    /// </summary>
    public Scalar Intrinsic;

    /// <summary>
    /// The TOTAL contribution of Eq. (3.1): intrinsic plus induced. This is the quantity
    /// Buchdahl's Table I sums across the system.
    /// </summary>
    public Scalar Total;
}

/// <summary>The ninth-order spherical aberration of a system, surface by surface.</summary>
public sealed class QuaternaryResult
{
    /// <summary>Indexed like the surfaces; entry 0 and the image row are unused.</summary>
    public QuaternaryRow[] Rows { get; init; } = Array.Empty<QuaternaryRow>();

    /// <summary>The system coefficient, <c>Sum q1p</c> over the optical surfaces.</summary>
    public Scalar Total { get; init; }
}

/// <summary>
/// The coefficient of quaternary (ninth-order) spherical aberration.
///
/// <para>Source: Buchdahl, H. A., "Optical Aberration Coefficients. IV. The Coefficient of
/// Quaternary Spherical Aberration," <i>J. Opt. Soc. Am.</i> <b>48</b>, 757 (1958). Referred to
/// below by equation number alone; <c>M</c> is the monograph.</para>
///
/// <para><b>This class reads the tertiary scheme and writes nothing back to it.</b> That is not
/// caution, it is the shape of the thing: Buchdahl built the quaternary computation as "an
/// appendix to that for the set of tertiary coefficients", and every one of the twenty-eight
/// quantities the fourteen rows need is already an entry of <see cref="BuchdahlTableI"/>. So
/// this is a pure function of rows computed for other reasons, it costs nothing when nobody asks
/// for it, and it cannot perturb a scheme that is checked entry by entry against paper III.</para>
///
/// <para><b>No dual run is needed, though Sec. 3 sounds as if one is.</b> It says the only
/// quantity in Eq. (3.1) not explicit in the tertiary scheme is <c>T1+</c>, and that obtaining it
/// "requires T1q", through the identity M (21.7). Buchdahl then does that reduction himself: the
/// three rows feeding r3 in his Table I ARE M (21.7) written out, in p-side quantities alone.
/// Reading Sec. 3 without the table would send an implementer to
/// <c>BuchdahlTableI.Compute(dual: true)</c> for nothing.</para>
///
/// <para><b>CONFIRMED AGAINST A SECOND IMPLEMENTATION.</b> BUCH7.ZPL stage F computes the same
/// fourteen rows inside OpticStudio, off its own paraxial data. On Kingslake's double Gauss every
/// surface agrees with this class in all three columns to the five figures the report prints, and
/// the system figure with it - 4.196252E+04. That took fixing the glass resolution first: the
/// design reads F4, and the bare name had been resolving to CDGM's F4 rather than Schott's, which
/// put the two eleven per cent apart. See <see cref="GlassCatalog.FallbackPreference"/>.</para>
///
/// <para><b>Two exact checks, and both are cheap.</b> Eq. (2.12): at a PLANE refracting surface,
/// where <c>i = -v</c>, the intrinsic coefficient must reduce to
/// <c>(35/128) N (1 - k^2)^4 y v^9</c> - a closed form needing no lens and no tolerance argument,
/// which Buchdahl uses himself as "a fairly reliable check". And his Table I computes the whole
/// thing for the triplet <c>Sigma1</c>, the same system paper III is checked against here, with a
/// system total of <c>-172968</c>. The six r rows are kept on <see cref="QuaternaryRow"/>
/// precisely so that a mismatch against that table localises to a row instead of to the
/// scheme.</para>
///
/// <para><b>The two Sigma conventions differ, and confusing them gives a plausible wrong
/// number.</b> Buchdahl flags it in the text: the <c>Sigma</c> in the r3 row "exceptionally
/// indicates the sum of the entries in the THREE preceding rows", while the <c>Sigma</c> in the
/// final row follows the usual convention and takes the four above it - the intrinsic row
/// included. Both are written out below as explicit sums rather than as a running accumulator,
/// so neither can quietly acquire a row.</para>
///
/// <para><b>Scope: spherical surfaces.</b> Paper IV has no figuring anywhere in it, and narrows
/// itself explicitly - "all entries relating to t_mu-pj, i-bar_mu-pj (mu = 2,...,10) except z2
/// are of course irrelevant". A quaternary ASPHERIC arrangement does not exist; Buchdahl never
/// wrote one, as he never wrote the tertiary one that <see cref="BuchdahlAsphericScheme"/> had to
/// reconstruct.</para>
///
/// <para><b>Units.</b> The same as the scheme it reads - Buchdahl's Table I is computed at unit
/// focal length, and nothing here rescales. His closing remark is worth keeping beside the
/// number: in a system meant to work at f/2 one aims at individual surface contributions of at
/// most order 1000 with f = 1, and Sigma1's run to six figures, "as is of course to be expected
/// of so poorly corrected a system".</para>
/// </summary>
public static class QuaternarySpherical
{
    /// <summary>
    /// The fourteen rows of paper IV Table I, for a scheme already computed by
    /// <see cref="BuchdahlTableI.Compute"/>.
    /// </summary>
    /// <param name="rows">A computed tertiary scheme.</param>
    /// <param name="lastSurface">Index of the last optical surface.</param>
    public static QuaternaryResult Compute(IReadOnlyList<BuchdahlTableIRow> rows, int lastSurface)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (lastSurface < 1 || lastSurface >= rows.Count)
            throw new ArgumentOutOfRangeException(nameof(lastSurface), lastSurface,
                "no such surface in the scheme");

        var outRows = new QuaternaryRow[rows.Count];
        Scalar total = 0.0;

        for (int i = 1; i <= lastSurface; i++)
        {
            var t = rows[i].T;
            var row = new QuaternaryRow();

            // t9 and t2 carry a TRAILING asterisk in the paper, which by the notation
            // BuchdahlTableI documents means the PRIMED value at this surface - not the value at
            // the preceding one. Both decorations appear in this series and confusing them is a
            // documented way to produce plausible wrong numbers, so these are taken from the
            // row's own primed angles rather than from rows[i - 1].
            Scalar t2Star = rows[i].VpPrime;
            Scalar t9Star = t2Star * t2Star;

            // ── r1, r2: the accumulated tertiary spherical and its barred partner ──────────
            // Sum_1^(j-1), so over the surfaces BEFORE this one - the same convention t69..t80
            // follow for the secondary and t15..t19 for the primary.
            Scalar r1 = 0.0, r2 = 0.0;
            for (int j = 1; j < i; j++)
            {
                r1 += rows[j].T[132];
                r2 += rows[j].T[133];
            }

            // ── The three rows feeding r3, which are M (21.7) written out ──────────────────
            Scalar a = ((t[83] - t[9] - t[20]) * t[15] + t[69]) * t[81] + t[6] * r1 - r2;
            Scalar b = ((0.5 * t[20] + t[9] - t[83]) * t[20] + t[9] * t[83] - t[86]) * t[9];
            Scalar c = (0.5 * t[15] * t[82] - t[6] * t[70] + t[102]) * t[15]
                     + t[16] * t[86] - t[21] * t[69];

            // r3 = T1+. The Sigma here is the THREE rows above and nothing else.
            Scalar r3 = (2.5 * t[83] - 3.0 * t[9]) * t[83] * t[83] + t[20] * t[70] + (a + b + c);

            // ── The intrinsic coefficient, Eq. (2.15) ──────────────────────────────────────
            // t34 is Buchdahl's w and t10 his a_p, so the 280/8 below is the 35 of (2.15).
            Scalar r4 = 0.25 * (t9Star - t[9]) * t[10];
            Scalar r5 = (17.0 * t[2] - 9.0 * t2Star) * t[7] * r4;
            Scalar r6 = (41.0 * t2Star - 87.0 * t[2]) * t[7] * t[38] / 3.0;

            Scalar intrinsic = (280.0 * t[34] * t[34] * t[34] + r5 + r6) * t[10] / 8.0;

            // ── The induced rows of Eq. (3.1) ──────────────────────────────────────────────
            // t8 is omega-tilde, t25 is A-dagger, t101 is S1-dagger, t121 is the intrinsic
            // tertiary spherical t1p, and t122 is z2 - the one mu = 2 entry the paper keeps.
            Scalar d = ((-0.5 * t[8] * t[25] - t[48] + t[49]) * t[15] - t[8] * t[69] + t[122])
                     * t[15];
            Scalar e = ((t[25] * t[25] + 6.0 * t[101]) * t[25] + 3.0 * r3) * t[10];
            Scalar f = (4.0 * t[15] * t[43] + 7.0 * t[121]) * t[25] + t[43] * t[69];

            // The final Sigma is the usual one: the FOUR rows above, the intrinsic included.
            Scalar contribution = (2.0 * t[25] * t[25] + t[101]) * 5.0 * t[38]
                                + (intrinsic + d + e + f);

            row.R1 = r1; row.R2 = r2; row.T1Dagger = r3;
            row.R4 = r4; row.R5 = r5; row.R6 = r6;
            row.Intrinsic = intrinsic;
            row.Total = contribution;

            outRows[i] = row;
            total += contribution;
        }

        return new QuaternaryResult { Rows = outRows, Total = total };
    }

    /// <summary>
    /// Why this system cannot be given a ninth-order spherical coefficient, or null if it can.
    ///
    /// <para><b>A figured surface is refused rather than approximated.</b> Paper IV has no
    /// aspheric arrangement in it and Buchdahl never wrote one. Running the scheme on a figured
    /// system would not give a slightly wrong answer, it would give a number that is neither the
    /// spherical coefficient nor the aspheric one - the figuring changes <c>a_p</c>,
    /// <c>S1p</c> and <c>w</c>, which the fourteen rows then consume as though they were
    /// spherical. That is the plausible wrong number this repository keeps deciding not to
    /// print.</para>
    /// </summary>
    public static string? Unsupported(Models.OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        int last = system.LastOpticalSurface();
        for (int i = 1; i <= last && i < system.Surfaces.Count; i++)
            if (system.Surfaces[i].IsFigured)
                return $"surface {i} is figured, and Buchdahl's quaternary scheme (paper IV) "
                     + "covers spherical surfaces only - he never published an aspheric "
                     + "arrangement at this order";

        if (system.StopSurfaceIndex < 0 || system.StopSurfaceIndex >= system.Surfaces.Count)
            return "the system has no stop surface";

        if (last < 1) return "the system has no optical surfaces";

        return null;
    }

    /// <summary>
    /// The ninth-order spherical coefficient of a system, running the tertiary scheme itself.
    /// Returns null when <see cref="Unsupported"/> gives a reason.
    /// </summary>
    public static QuaternaryResult? FromSystem(Models.OpticalSystem system, Scalar[] indices,
                                               RayTrace.ParaxialResult paraxial)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        if (Unsupported(system) != null) return null;

        // M (13.4)'s iota, 1/l_01 in focal lengths; zero for an object at infinity. The same two
        // lines as TertiaryCoefficients.IotaOf, which is private - repeated rather than opened up,
        // because it is one expression and this class deliberately does not reach into the
        // seventh-order code.
        Scalar t0 = system.Surfaces[0].Thickness;
        Scalar iota = Scalar.IsInfinity(t0) ? 0.0 : -paraxial.Efl / t0;

        var scheme = BuchdahlScheme.Compute(system.Surfaces, indices, paraxial.Efl,
                                            system.Surfaces[system.StopSurfaceIndex].SemiDiameter,
                                            iota);
        var rows = BuchdahlTableI.Compute(system.Surfaces, indices, paraxial.Efl, scheme.P,
                                          iota: iota);

        return Compute(rows, system.LastOpticalSurface());
    }
}
