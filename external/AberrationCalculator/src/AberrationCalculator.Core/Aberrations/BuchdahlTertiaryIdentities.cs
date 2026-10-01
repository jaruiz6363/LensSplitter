using System;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The six tertiary identities of paper III Sec. 6, (8.1-6).
///
/// <para>These are what the whole tau9/tau14/tau17 hunt has wanted: a set of relations the
/// SEVENTH-order coefficients must satisfy exactly, needing no ray trace and no second program.
/// The secondary identities played that part one order down and found the aspheric secondary
/// fault immediately, after eleven guesses had failed against traced fans.</para>
///
/// <para>They were blocked for a long time on <c>Sbar_mu_q</c>, which Buchdahl's scheme does not
/// produce. <see cref="BuchdahlSecondaryQ"/> now recovers those from M Sec. 22, so the starred
/// quantities of paper III (5) and (6) can be formed and these evaluated.</para>
///
/// <para>They constrain, in order, the pairs (T2, Tbar1), (T4, Tbar2), (T5, Tbar3),
/// (T7, Tbar4), (T8, Tbar5) and (T9, Tbar6) - which between them carry T5, T7, T8 and T9, every
/// unbarred T that feeds the three suspect coefficients and nothing else that does.</para>
/// </summary>
public static class BuchdahlTertiaryIdentities
{
    /// <summary>
    /// Residuals of (8.1) .. (8.6) at surface <paramref name="j"/>, and the size of the largest
    /// term entering each, so a residual can be judged against what it is a residual of.
    /// </summary>
    public static (Scalar[] Residual, Scalar[] Scale) At(BuchdahlTableIRow[] rows, int j)
        => At(rows, j, 1.0);

    /// <summary>The same, with the object-space index given rather than taken as unity.</summary>
    public static (Scalar[] Residual, Scalar[] Scale) At(BuchdahlTableIRow[] rows, int j,
                                                         Scalar n1)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (j < 1 || j >= rows.Length) throw new ArgumentOutOfRangeException(nameof(j));

        var r = rows[j];
        Scalar vp = r[2], vq = r[5];

        Scalar Ap = r[15], Abp = r[16], Bbp = r[17], Cp = r[18], Cbp = r[19];
        Scalar Aq = r[20], Abq = r[21], Bq = r[22], Cq = r[23], Cbq = r[24];
        Scalar Bp = 2.0 * Abp, Bbq = 2.0 * Cq;

        // Paper III (4): G_v = v_q G_p - v_p G_q.
        Scalar Av = vq * Ap - vp * Aq, Abv = vq * Abp - vp * Abq;
        Scalar Bv = vq * Bp - vp * Bq, Bbv = vq * Bbp - vp * Bbq;
        Scalar Cv = vq * Cp - vp * Cq, Cbv = vq * Cbp - vp * Cbq;

        // Paper III (5) and (6).
        Scalar As = Av - 0.5 * vp * vp * vp;
        Scalar Bs = Bv - vp * vp * vq;
        Scalar Cs = Cv - 0.5 * vp * vq * vq;
        Scalar Abs = Abv - 0.5 * vp * vp * vq;
        Scalar Bbs = Bbv - vp * vq * vq;
        Scalar Cbs = Cbv - 0.5 * vq * vq * vq;

        Scalar[] Sp = { 0, r[69], r[71], r[73], r[75], r[77], r[79] };
        Scalar[] Sbp = { 0, r[70], r[72], r[74], r[76], r[78], r[80] };
        Scalar[] Sq = { 0, r[86], r[89], r[92], r[94], r[97], r[98] };
        var Sbq = BuchdahlSecondaryQ.At(rows, j, n1);

        Scalar Sv(int m) => vq * Sp[m] - vp * Sq[m];
        Scalar Sbv(int m) => vq * Sbp[m] - vp * Sbq[m];

        Scalar S1s = Sv(1) - 1.5 * vp * vp * Av + 0.375 * SMath.Pow(vp, 5);
        Scalar S2s = Sv(2) - 2.0 * vp * vq * Av - vp * vp * (Abv + 1.5 * Bv)
                   + 1.5 * SMath.Pow(vp, 4) * vq;
        Scalar S3s = Sv(3) - 0.5 * vq * vq * Av - vp * vq * Abv - 1.5 * vp * vp * Cv
                   + 0.75 * SMath.Pow(vp, 3) * vq * vq;
        Scalar S4s = Sv(4) - 2.0 * vp * vq * Bv - vp * vp * Bbv
                   + 1.5 * SMath.Pow(vp, 3) * vq * vq;
        Scalar S5s = Sv(5) - 0.5 * vq * vq * Bv - vp * vq * (Bbv + 2.0 * Cv) - vp * vp * Cbv
                   + 1.5 * vp * vp * SMath.Pow(vq, 3);

        Scalar S1bs = Sbv(1) - vp * vq * Av - 0.5 * vp * vp * Abv
                    + 0.375 * SMath.Pow(vp, 4) * vq;
        Scalar S2bs = Sbv(2) - vq * vq * Av - vp * vq * (2.0 * Abv + Bv) - 0.5 * vp * vp * Bbv
                    + 1.5 * SMath.Pow(vp, 3) * vq * vq;
        Scalar S3bs = Sbv(3) - 1.5 * vq * vq * Abv - vp * vq * Cv - 0.5 * vp * vp * Cbv
                    + 0.75 * vp * vp * SMath.Pow(vq, 3);
        Scalar S4bs = Sbv(4) - vq * vq * Bv - 2.0 * vp * vq * Bbv
                    + 1.5 * vp * vp * SMath.Pow(vq, 3);
        Scalar S5bs = Sbv(5) - vq * vq * (1.5 * Bbv + Cv) - 2.0 * vp * vq * Cbv
                    + 1.5 * vp * SMath.Pow(vq, 4);

        // Tertiary running sums, accumulated over the surfaces already passed, exactly as the
        // secondary ones the identities pair them with.
        var T = new Scalar[11];
        var Tb = new Scalar[11];
        for (int i = 1; i < j; i++)
            for (int m = 1; m <= 10; m++)
            {
                T[m] += rows[i].TertiaryTotal[m];
                Tb[m] += rows[i].TertiaryTotalBar[m];
            }

        Scalar[] residual =
        {
            (T[2] - 6.0 * Tb[1]) * vp + As * (Sp[2] - 4.0 * Sbp[1]) - (Bs - 2.0 * Abs) * Sp[1]
                - (S2s - 4.0 * S1bs) * Ap
                + 2.0 * (As * Sp[2] - S2s * Ap + 2.0 * S1s * Bp - 2.0 * Bs * Sp[1]),   // (8.1)

            2.0 * (T[4] - 2.0 * Tb[2]) * vp + 2.0 * As * (Sp[4] - Sbp[2])
                + Bs * (Sp[2] - 4.0 * Sbp[1]) - (Bs - 2.0 * Abs) * Sp[2]
                - (S2s - 4.0 * S1bs) * Bp - 2.0 * (S4s - S2bs) * Ap
                + 2.0 * (2.0 * As * Sp[4] - 2.0 * S4s * Ap + S2s * Bp - Bs * Sp[2]
                         + As * Sbp[2] - S2s * Abp + 2.0 * S1s * Bbp - 2.0 * Bs * Sbp[1]
                         + Abs * Sp[2] - S2bs * Ap + 2.0 * S1bs * Bp - 2.0 * Bbs * Sp[1]), // (8.2)

            (T[5] - 4.0 * Tb[3]) * vp + As * (Sp[5] - 2.0 * Sbp[3])
                + Cs * (Sp[2] - 4.0 * Sbp[1]) - (Bs - 2.0 * Abs) * Sp[3]
                - (S2s - 4.0 * S1bs) * Cp - (S5s - 2.0 * S3bs) * Ap
                + 2.0 * (As * Sp[5] - S5s * Ap + S3s * Bp - Bs * Sp[3]
                         + Abs * Sbp[2] - S2bs * Abp + 2.0 * S1bs * Bbp
                         - 2.0 * Bbs * Sbp[1]),                                        // (8.3)

            (3.0 * T[7] - 2.0 * Tb[4]) * vp + 2.0 * Bs * (Sp[4] - Sbp[2])
                - (Bs - 2.0 * Abs) * Sp[4] - 2.0 * (S4s - S2bs) * Bp
                + 2.0 * (2.0 * Abs * Sp[4] - 2.0 * S4bs * Ap + S2bs * Bp - Bbs * Sp[2]
                         + 2.0 * As * Sbp[4] - 2.0 * S4s * Abp + S2s * Bbp
                         - Bs * Sbp[2]),                                               // (8.4)

            2.0 * (T[8] - Tb[5]) * vp + Bs * (Sp[5] - 2.0 * Sbp[3])
                + 2.0 * Cs * (Sp[4] - Sbp[2]) - (Bs - 2.0 * Abs) * Sp[5]
                - 2.0 * (S4s - S2bs) * Cp - (S5s - 2.0 * S3bs) * Bp
                + 2.0 * (Abs * Sp[5] - S5bs * Ap + S3bs * Bp - Bbs * Sp[3]
                         + As * Sbp[5] - S5s * Abp + S3s * Bbp - Bs * Sbp[3]
                         + 2.0 * Abs * Sbp[4] - 2.0 * S4bs * Abp + S2bs * Bbp
                         - Bbs * Sbp[2]),                                              // (8.5)

            (T[9] - 2.0 * Tb[6]) * vp + Cs * (Sp[5] - 2.0 * Sbp[3])
                - (Bs - 2.0 * Abs) * Sp[6] - (S5s - 2.0 * S3bs) * Cp
                + 2.0 * (Abs * Sbp[5] - S5bs * Abp + S3bs * Bbp - Bbs * Sbp[3]),       // (8.6)
        };

        Scalar M(params Scalar[] terms)
        {
            Scalar m = 0.0;
            foreach (Scalar t in terms) if (SMath.Abs(t) > m) m = SMath.Abs(t);
            return m > 0.0 ? m : 1.0;
        }

        Scalar[] scale =
        {
            M((T[2] - 6.0 * Tb[1]) * vp, As * Sp[2], S2s * Ap, S1s * Bp, Bs * Sp[1]),
            M((T[4] - 2.0 * Tb[2]) * vp, As * Sp[4], S4s * Ap, S2s * Bp, Bs * Sp[2]),
            M((T[5] - 4.0 * Tb[3]) * vp, As * Sp[5], S5s * Ap, S3s * Bp, Bs * Sp[3]),
            M((3.0 * T[7] - 2.0 * Tb[4]) * vp, Bs * Sp[4], S4s * Abp, S2s * Bbp),
            M((T[8] - Tb[5]) * vp, Bs * Sp[5], S5s * Abp, S3s * Bbp, Cs * Sp[4]),
            M((T[9] - 2.0 * Tb[6]) * vp, Cs * Sp[5], S5bs * Abp, S3bs * Bbp),
        };

        _ = Cbs;
        return (residual, scale);
    }
}
