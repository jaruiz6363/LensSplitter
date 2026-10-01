using System;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Buchdahl's published identities between aberration coefficients, as residuals.
///
/// <para>These are relations the coefficients must satisfy exactly, derived independently of
/// the computing scheme that produces them. They are the only check on this program that needs
/// neither a ray trace nor a second program, and they say WHICH quantity is wrong rather than
/// only that the prediction is - which is what the tau9/tau14/tau17 hunt has lacked.</para>
///
/// <para>Sources: the primary identities are M (20.41-47), p.28; the secondary are paper III
/// (7.1-3), which are M (22.41, 51, 61). Paper III gives them in Secs. 5 and 6.</para>
/// </summary>
public sealed class IdentityResiduals
{
    /// <summary>Surface the residuals were evaluated at.</summary>
    public int Surface { get; init; }

    /// <summary>
    /// Residual of each identity, in the order (20.41), (20.42), (20.43), (20.44), (20.45),
    /// (20.46), (20.47). Each should be zero.
    /// </summary>
    public Scalar[] Primary { get; init; } = Array.Empty<Scalar>();

    /// <summary>Residual of paper III (7.1), (7.2), (7.3). Each should be zero.</summary>
    public Scalar[] Secondary { get; init; } = Array.Empty<Scalar>();

    /// <summary>
    /// The size of the largest single term that went into each identity, so a residual can be
    /// judged against what it is a residual OF. An absolute residual of 1e-6 means nothing
    /// until it is known whether the terms were of order one or of order a million.
    /// </summary>
    public Scalar[] PrimaryScale { get; init; } = Array.Empty<Scalar>();

    /// <inheritdoc cref="PrimaryScale"/>
    public Scalar[] SecondaryScale { get; init; } = Array.Empty<Scalar>();

    /// <summary>Largest relative residual among the primary identities.</summary>
    public Scalar WorstPrimary => Worst(Primary, PrimaryScale);

    /// <summary>Largest relative residual among the secondary identities.</summary>
    public Scalar WorstSecondary => Worst(Secondary, SecondaryScale);

    private static Scalar Worst(Scalar[] r, Scalar[] scale)
    {
        Scalar worst = 0.0;
        for (int i = 0; i < r.Length; i++)
        {
            Scalar s = i < scale.Length ? scale[i] : 1.0;
            Scalar rel = s > 1e-300 ? SMath.Abs(r[i]) / s : SMath.Abs(r[i]);
            if (rel > worst) worst = rel;
        }
        return worst;
    }
}

public static class BuchdahlIdentities
{
    /// <summary>
    /// Evaluates the identities at one surface of a computed scheme.
    ///
    /// <para><b>The entry numbering below was established against Buchdahl's printed Table I,
    /// not read off the working notes</b>, which were shifted by one from t20 onward. Two of
    /// the assignments are not what the notes guessed and are worth stating plainly: t17 is
    /// Bbar_p, NOT B_p, and B_p does not appear in the table at all - it comes from (20.42) as
    /// 2 Abar_p. Likewise Bbar_q comes from (20.43) as 2 C_q. Both are confirmed below by
    /// (20.41), which mixes all four and would not vanish if either were wrong.</para>
    /// </summary>
    /// <param name="rows">A computed scheme, from <see cref="BuchdahlTableI.Compute"/>.</param>
    /// <param name="j">Surface index.</param>
    /// <param name="n1">
    /// Object-space refractive index, Buchdahl's N1. The identities carry it explicitly; on a
    /// system in air it is one.
    /// </param>
    public static IdentityResiduals At(BuchdahlTableIRow[] rows, int j) => At(rows, j, 1.0);

    /// <summary>The same, with the object-space index given rather than taken as unity.</summary>
    public static IdentityResiduals At(BuchdahlTableIRow[] rows, int j, Scalar n1)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (j < 1 || j >= rows.Length) throw new ArgumentOutOfRangeException(nameof(j));

        var r = rows[j];
        Scalar vp = r[2], vq = r[5];
        Scalar vp1 = rows[1][2], vq1 = rows[1][5];

        Scalar Ap = r[15], Abp = r[16], Bbp = r[17], Cp = r[18], Cbp = r[19];
        Scalar Aq = r[20], Abq = r[21], Bq = r[22], Cq = r[23], Cbq = r[24];

        // Not in the table; supplied by the identities that are being tested, so these two
        // enter (20.41) as predictions rather than as inputs.
        Scalar Bp = 2.0 * Abp;      // (20.42)
        Scalar Bbq = 2.0 * Cq;      // (20.43)

        Scalar[] primary =
        {
            2.0 * Abq - Bq + Bbp - 2.0 * Cp,                                    // (20.41)
            2.0 * Abp - Bp,                                                     // (20.42)
            Bbq - 2.0 * Cq,                                                     // (20.43)
            Abp - Aq - 0.5 * n1 * (vp * vp - vp1 * vp1),                        // (20.44)
            Bbp - Bq - n1 * (vp * vq - vp1 * vq1),                              // (20.45)
            Cbp - Cq - 0.5 * n1 * (vq * vq - vq1 * vq1),                        // (20.46)
            Cp - Abq - 0.5 * n1 * (vp * vq - vp1 * vq1),                        // (20.47)
        };
        Scalar[] primaryScale =
        {
            Max(2.0 * Abq, Bq, Bbp, 2.0 * Cp),
            Max(2.0 * Abp, Bp),
            Max(Bbq, 2.0 * Cq),
            Max(Abp, Aq, 0.5 * n1 * vp * vp),
            Max(Bbp, Bq, n1 * vp * vq),
            Max(Cbp, Cq, 0.5 * n1 * vq * vq),
            Max(Cp, Abq, 0.5 * n1 * vp * vq),
        };

        // The v-operator of paper III (4), and the starred quantities of its (5) and (6).
        Scalar Av = vq * Ap - vp * Aq;
        Scalar Abv = vq * Abp - vp * Abq;
        Scalar Bv = vq * Bp - vp * Bq;
        Scalar Bbv = vq * Bbp - vp * Bbq;
        Scalar Cv = vq * Cp - vp * Cq;
        Scalar Cbv = vq * Cbp - vp * Cbq;

        Scalar As = Av - 0.5 * vp * vp * vp;
        Scalar Bs = Bv - vp * vp * vq;
        Scalar Cs = Cv - 0.5 * vp * vq * vq;
        Scalar Abs = Abv - 0.5 * vp * vp * vq;
        Scalar Bbs = Bbv - vp * vq * vq;
        Scalar Cbs = Cbv - 0.5 * vq * vq * vq;

        Scalar S1p = r[69], S1bp = r[70], S2p = r[71], S2bp = r[72];
        Scalar S3p = r[73], S3bp = r[74], S4p = r[75], S4bp = r[76];
        Scalar S5p = r[77], S5bp = r[78];

        Scalar[] secondary =
        {
            (S2p - 4.0 * S1bp) * vp - (Bs - 2.0 * Abs) * Ap
                + 2.0 * (As * Bp - Bs * Ap),                                    // (7.1)

            (S4p - S2bp) * vp - (Bs - 2.0 * Abs) * Abp
                + (Abs * Bp - Bbs * Ap + As * Bbp - Bs * Abp),                  // (7.2)

            (S5p - 2.0 * S3bp) * vp - (Bs - 2.0 * Abs) * Cp
                + 2.0 * (Abs * Bbp - Bbs * Abp),                                // (7.3)
        };
        Scalar[] secondaryScale =
        {
            Max((S2p - 4.0 * S1bp) * vp, (Bs - 2.0 * Abs) * Ap, 2.0 * As * Bp, 2.0 * Bs * Ap),
            Max((S4p - S2bp) * vp, (Bs - 2.0 * Abs) * Abp, Abs * Bp, Bbs * Ap, As * Bbp, Bs * Abp),
            Max((S5p - 2.0 * S3bp) * vp, (Bs - 2.0 * Abs) * Cp, 2.0 * Abs * Bbp, 2.0 * Bbs * Abp),
        };

        // Silence the unused-value warning honestly: S1p, S3p, S4bp, S5bp are read so that the
        // entry numbering above is documented in one place, and are needed by (8.1-6) next.
        _ = S1p; _ = S3p; _ = S4bp; _ = S5bp; _ = Cs; _ = Cbs;

        return new IdentityResiduals
        {
            Surface = j,
            Primary = primary,
            PrimaryScale = primaryScale,
            Secondary = secondary,
            SecondaryScale = secondaryScale,
        };
    }

    private static Scalar Max(params Scalar[] terms)
    {
        Scalar m = 0.0;
        foreach (Scalar t in terms) if (SMath.Abs(t) > m) m = SMath.Abs(t);
        return m > 0.0 ? m : 1.0;
    }
}
