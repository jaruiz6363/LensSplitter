using System;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The BARRED q-side secondary coefficients, Sbar_1q .. Sbar_5q, recovered from Buchdahl's
/// identities.
///
/// <para><b>Why they have to be recovered rather than computed.</b> Buchdahl's scheme produces
/// the p coefficients and the unbarred q coefficients, and stops. He obtained the barred q ones
/// "by transformation of coordinates from the corresponding canonical coefficients of M Table
/// VII" (paper III 5a-ii), which needs a table this program does not implement. The identities
/// of M Sec. 22 give them instead, from quantities the scheme already carries.</para>
///
/// <para><b>Sbar_6q cannot be had this way, and is not wanted.</b> M Sec. 19 says so outright -
/// "when all the p-coefficients are known, all but one of the q-coefficients ... can be obtained
/// from them by means of the identities" - and the one left over is the last barred one. The
/// tertiary identities (8.1-6) of paper III use S1*..S5* and Sbar_1*..Sbar_5* and never
/// Sbar_6*, so the five available are exactly the five needed.</para>
///
/// <para><b>The pairing.</b> M (21.5) writes the identities in a bracket notation whose
/// underlying pairing it does not define. M (20.21) applies the same pairing to paraxial rays -
/// <c>(v'|v) = -N1 pomega</c> - which gives it away: it is the skew bracket over the p and q
/// members, <c>(X|Y) = X_p Y_q - X_q Y_p</c>. Since <c>[AB] = (1/N1)(A|B)</c> is a direct
/// evaluation rather than an accumulation, every bracket follows from the running sums.
/// <see cref="BracketPairingTests"/> confirms this against a bracket the identities determine
/// independently.</para>
/// </summary>
public static class BuchdahlSecondaryQ
{
    /// <summary>The skew pairing (X|Y) = X_p Y_q - X_q Y_p.</summary>
    public static Scalar Pair(Scalar xp, Scalar xq, Scalar yp, Scalar yq)
        => xp * yq - xq * yp;

    /// <summary>
    /// Sbar_1q .. Sbar_5q at surface <paramref name="j"/>, indexed 1..5; index 0 is unused and
    /// index 6 is left at zero, being the one the identities cannot supply.
    /// </summary>
    public static Scalar[] At(BuchdahlTableIRow[] rows, int j) => At(rows, j, 1.0);

    /// <summary>The same, with the object-space index given rather than taken as unity.</summary>
    public static Scalar[] At(BuchdahlTableIRow[] rows, int j, Scalar n1)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (j < 1 || j >= rows.Length) throw new ArgumentOutOfRangeException(nameof(j));

        var r = rows[j];
        Scalar vp = r[2], vq = r[5];
        Scalar vp1 = rows[1][2], vq1 = rows[1][5];

        // Running sums, p and q. B_p = 2 Abar_p by (20.42) and Bbar_q = 2 C_q by (20.43); those
        // two are not tabulated and follow from the primary identities.
        Scalar Ap = r[15], Aq = r[20];
        Scalar Abp = r[16], Abq = r[21];
        Scalar Bp = 2.0 * Abp, Bq = r[22];
        Scalar Bbp = r[17], Bbq = 2.0 * r[23];
        Scalar Cp = r[18], Cq = r[23];
        Scalar Cbp = r[19], Cbq = r[24];

        Scalar AC = Pair(Ap, Aq, Cp, Cq);
        Scalar BC = Pair(Bp, Bq, Cp, Cq);
        Scalar AbarC = Pair(Abp, Abq, Cp, Cq);
        Scalar AbarCbar = Pair(Abp, Abq, Cbp, Cbq);
        Scalar BbarAbar = Pair(Bbp, Bbq, Abp, Abq);
        Scalar BbarC = Pair(Bbp, Bbq, Cp, Cq);
        Scalar BCbar = Pair(Bp, Bq, Cbp, Cbq);
        Scalar BbarCbar = Pair(Bbp, Bbq, Cbp, Cbq);
        Scalar CbarC = Pair(Cbp, Cbq, Cp, Cq);

        // [X] = (X|v).
        Scalar bA = Pair(Ap, Aq, vp, vq);
        Scalar bAbar = Pair(Abp, Abq, vp, vq);
        Scalar bBbar = Pair(Bbp, Bbq, vp, vq);
        Scalar bC = Pair(Cp, Cq, vp, vq);
        Scalar bCbar = Pair(Cbp, Cbq, vp, vq);

        Scalar dvp = vp * vp - vp1 * vp1;
        Scalar dvq = vq * vq - vq1 * vq1;
        Scalar dpq = vp * vq - vp1 * vq1;

        // The six identities that each carry one barred q coefficient.
        Scalar om8 = BbarAbar + 2.0 * AbarC + 2.0 * BC + vp * bBbar
                   - n1 * vp * vq * dpq;                                          // (22.42)
        Scalar om9 = 6.0 * AC - vq * bA + vp * bAbar
                   - 1.5 * n1 * dvp * dpq;                                        // (22.43)
        Scalar om11 = 4.0 * BbarC + 2.0 * BCbar + vq * bBbar + 2.0 * vp * bCbar
                    - n1 * (2.0 * vp * vq * vq * vq - vp * vq * vq1 * vq1
                            - vq * vq * vp1 * vq1);                               // (22.52)
        Scalar om14 = 3.0 * BbarCbar + 2.0 * CbarC + 2.0 * vq * bCbar
                    - n1 * vq * vq * dvq;                                         // (22.62)
        Scalar om15 = 6.0 * AbarCbar + vq * bC - vp * bCbar
                    - 1.5 * n1 * dvq * dpq;                                       // (22.63)

        Scalar S3p = r[73], S6p = r[79];
        Scalar S3q = r[92], S5q = r[97], S6q = r[98];

        var s = new Scalar[7];
        s[1] = (om9 + 2.0 * S3p) / 4.0;      // (22.43)
        s[2] = om8 + 2.0 * S3q;             // (22.42)
        s[3] = (om15 + 4.0 * S6p) / 2.0;    // (22.63)
        s[4] = (om11 + 2.0 * S5q) / 2.0;    // (22.52)
        s[5] = om14 + 4.0 * S6q;            // (22.62)
        return s;
    }

    /// <summary>
    /// Sbar_2q the OTHER way, from (22.53). It must agree with <see cref="At"/>'s second entry,
    /// and that agreement is the only check available on the whole recovery: the brackets, the
    /// pairing, the two derived primaries and the transcription of two dense equations all have
    /// to be right for it to hold.
    /// </summary>
    public static Scalar SecondBarredQAlternative(BuchdahlTableIRow[] rows, int j)
        => SecondBarredQAlternative(rows, j, 1.0);

    /// <summary>The same, with the object-space index given rather than taken as unity.</summary>
    public static Scalar SecondBarredQAlternative(BuchdahlTableIRow[] rows, int j,
                                                  Scalar n1)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        var r = rows[j];
        Scalar vp = r[2], vq = r[5];
        Scalar vp1 = rows[1][2], vq1 = rows[1][5];

        Scalar Ap = r[15], Aq = r[20];
        Scalar Abp = r[16], Abq = r[21];
        Scalar Bp = 2.0 * Abp, Bq = r[22];
        Scalar Bbp = r[17], Bbq = 2.0 * r[23];
        Scalar Cp = r[18], Cq = r[23];
        Scalar Cbp = r[19], Cbq = r[24];

        Scalar om12 = 2.0 * Pair(Abp, Abq, Bbp, Bbq)
                    + 4.0 * Pair(Abp, Abq, Cp, Cq)
                    + 4.0 * Pair(Ap, Aq, Cbp, Cbq)
                    + 2.0 * Pair(Bp, Bq, Cp, Cq)
                    - n1 * (3.0 * SMath.Pow(vp * vq - vp1 * vq1, 2)
                            - SMath.Pow(vp * vq1 - vq * vp1, 2));                  // (22.53)
        return (om12 + 2.0 * r[77]) / 2.0;
    }
}
