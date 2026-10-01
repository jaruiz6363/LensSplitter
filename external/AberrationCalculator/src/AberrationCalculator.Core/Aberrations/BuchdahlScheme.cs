using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>Per-surface quantities of the Buchdahl computing scheme.</summary>
public sealed class BuchdahlSurface
{
    public Scalar Yp, Yq, Ip, Iq, Vp, Vq;
    public Scalar VpPrime, VqPrime, YpPrime, YqPrime, IpPrime, IqPrime;
    public Scalar C, D, N, K;

    /// <summary>Secondary coefficients S1..S6, unbarred and barred, for this surface.</summary>
    public Scalar S1p, S1pBar, S2p, S2pBar, S3p, S3pBar;
    public Scalar S4p, S4pBar, S5p, S5pBar, S6p, S6pBar;
}

/// <summary>System totals of the Buchdahl computing scheme.</summary>
public sealed class BuchdahlSchemeResult
{
    /// <summary>Primary coefficients: Buchdahl's a, b, c for the p and q rays.</summary>
    public Scalar Ap, Bp, Cp, Aq, Bq, Cq;
    public Scalar ApBar, BpBar, CpBar, AqBar, BqBar, CqBar;

    /// <summary>Secondary coefficients S1..S6, unbarred and barred.</summary>
    public Scalar S1p, S1pBar, S2p, S2pBar, S3p, S3pBar;
    public Scalar S4p, S4pBar, S5p, S5pBar, S6p, S6pBar;

    /// <summary>
    /// Stop-shift parameter. Buchdahl calls this p; his triplet has p = 0.113227.
    ///
    /// <para>It is the ray-height ratio -y_q/y_p AT the stop, which is what displaces the
    /// q ray onto the stop centre. Chapter VI of the monograph traces the q ray from the
    /// axis at the first surface (y = 0, v = 1); paper III starts it at y = p instead, and
    /// the difference between the two is exactly p times the p ray. So p is the stop
    /// shift, and it equals the paraxial entrance pupil position in units of the focal
    /// length.</para>
    /// </summary>
    public Scalar P;

    /// <summary>
    /// The p ray's reduced angle after the last optical surface, in focal lengths - M
    /// (31.11)'s v'_pk. The conversion of a coefficient to a transverse aberration carries
    /// 1/(N'_k v'_pk), and III states the same rule: entries must be "divided by N_k'v_pk'"
    /// to turn the augmented coefficients into the actual ones. It is exactly one when the
    /// object is at infinity, which is why efl stood in its place for as long as only that
    /// case was computed.
    /// </summary>
    public Scalar PRayFinalAngle = 1.0;

    /// <summary>Entrance pupil position and radius, from the p and q rays.</summary>
    public Scalar EntrancePupilPosition, EntrancePupilRadius, StopRadius;

    /// <summary>False when no stop was found, in which case the pupil values are meaningless.</summary>
    public bool Evaluated;

    /// <summary>Per-surface detail, indexed like the system's surfaces.</summary>
    public BuchdahlSurface[] Surfaces = Array.Empty<BuchdahlSurface>();

    /// <summary>
    /// The scheme's numbered working entries, [surface][j], kept so the computation can be
    /// inspected mid-chain rather than only at its ends.
    ///
    /// <para>The numbering is Buchdahl's own, from TABLE II of Chapter VI of the
    /// monograph - t1 is the p-ray reduced angle, t2 its refracted value, t46 = a_p, t83
    /// = s-hat_1p, and so on to t108. It is NOT the numbering of Table I of J. Opt. Soc.
    /// Am. 48, 747 (1958), which runs to t155 and computes the tertiary coefficients;
    /// <see cref="BuchdahlTableI"/> implements that one separately.</para>
    ///
    /// <para>Because the numbering is his, every entry can be read against the worked
    /// triplet he prints in that table, column by column - which is how the errors in
    /// t100..t108 were found.</para>
    /// </summary>
    public Scalar[][] T = Array.Empty<Scalar[]>();
}

/// <summary>
/// Buchdahl's own computing scheme for the primary and secondary aberration coefficients,
/// in his notation rather than Rimmer's.
///
/// This is a second, independent path to the same physics that
/// <see cref="BuchdahlCoefficients"/> computes. It exists for two reasons.
///
/// <para>First, it is checkable against published numbers. Buchdahl tabulates every entry
/// of this scheme for a triplet whose prescription he also publishes
/// (<i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958), Table I and p.753), so the arithmetic
/// can be verified against the source rather than against another implementation.</para>
///
/// <para>Second, it is the input the seventh-order coefficients need. Buchdahl's tertiary
/// scheme consumes these quantities - his a, b, c and S1..S6 with their p, q and barred
/// variants - not the Rimmer-notation coefficients this program otherwise reports. Robb's
/// polynomial then takes the tertiary coefficients through the mapping of that paper's
/// Table II.</para>
///
/// <para>Ported from a C++ implementation written from the monograph, which reproduces
/// the published values for Buchdahl's own triplet. <see cref="BuchdahlSchemeResult.T"/>
/// exposes the numbered working entries so the computation can be inspected mid-chain.
/// <b>That numbering is the implementation's own, NOT Buchdahl's Table I numbering</b> -
/// his t1 is the p-ray height where this t1 is the p-ray angle, and the two diverge from
/// there. Do not read one against the other without establishing the correspondence
/// first.</para>
///
/// <para><b>Spherical surfaces only.</b> Aspheric figuring is not handled here; Buchdahl
/// treats it separately, in monograph Secs. 80 and 85.</para>
/// </summary>
public static class BuchdahlScheme
{
    private const int MaxTerms = 141;

    private static Scalar SafeInverse(Scalar v) => SMath.Abs(v) < 1e-300 ? 0.0 : 1.0 / v;

    private static Scalar SafeDivide(Scalar num, Scalar den) =>
        SMath.Abs(den) < 1e-300 ? 0.0 : num / den;

    /// <summary>
    /// Runs the scheme over a centred system. <paramref name="indices"/> holds the
    /// refractive index of the medium after each surface, indexed like the surfaces.
    /// </summary>
    /// <param name="efl">
    /// The system's focal length. Buchdahl works in units of it: curvatures are multiplied
    /// by it and separations divided, so that the marginal ray starts at unit height. His
    /// triplet has c1 = 4.82439, which is that surface's curvature times its focal length.
    /// </param>
    public static BuchdahlSchemeResult Compute(
        IReadOnlyList<Models.Surface> surfaces, Scalar[] indices, Scalar efl, Scalar stopRadius = default,
        Scalar iota = default)
    {
        if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
        if (indices == null) throw new ArgumentNullException(nameof(indices));

        int count = surfaces.Count;
        var result = new BuchdahlSchemeResult();
        if (count < 3) return result;

        Scalar scale = SMath.Abs(efl) > 1e-300 ? 1.0 / efl : 1.0;

        var t = new Scalar[count][];
        for (int i = 0; i < count; i++) t[i] = new Scalar[MaxTerms];
        var parax = new BuchdahlSurface[count];
        for (int i = 0; i < count; i++) parax[i] = new BuchdahlSurface();

        // The p and q rays: a basis pair, rather than marginal and chief. Buchdahl's
        // construction, and the reason the stop shift falls out as a single number.
        //
        // The starting values are M (13.4), reduced OT-coordinates: the p ray leaves at
        // height one with reduced angle iota = 1/l_01, and iota is the ONLY thing in the
        // whole scheme that knows where the object is. It was previously hard-coded to
        // zero, which is the infinite-conjugate case, so every finite-conjugate system
        // was quietly given the answer for an object at infinity - identical to seven
        // figures, because nothing else downstream asks about the conjugate at all.
        //
        // Lengths here are in focal lengths (see scale above), so iota is efl/l_01 and
        // not 1/l_01.
        Scalar pY = 1.0, pV = iota, qY = 0.0, qV = 1.0;
        Scalar pYPrime = 0.0, pVPrime = 0.0, qYPrime = 0.0, qVPrime = 0.0;

        Scalar stopPosition = 0.0, denominator = 0.0;
        bool stopFound = false;

        for (int i = 1; i < count - 1; i++)
        {
            var ti = t[i];
            var pi = parax[i];

            Scalar nPrev = i - 1 < indices.Length ? indices[i - 1] : 1.0;
            Scalar nCurr = i < indices.Length ? indices[i] : 1.0;
            if (SMath.Abs(nCurr) < 1e-12) nCurr = 1.0;

            Scalar k = SafeDivide(nPrev, nCurr);
            Scalar k1 = 1.0 - k;
            Scalar c = surfaces[i].VertexCurvature * SafeInverse(scale);
            Scalar d = surfaces[i].Thickness * scale;
            Scalar i1p = c * pY - pV;
            Scalar i1q = c * qY - qV;
            Scalar i1pPrime = k * i1p;
            Scalar i1qPrime = k * i1q;
            Scalar n = nPrev;

            pi.C = c; pi.D = d; pi.N = n; pi.K = k;

            pVPrime = k1 * c * pY + k * pV;
            pYPrime = (1.0 - k1 * c * d) * pY - k * d * pV;
            qVPrime = k1 * c * qY + k * qV;
            qYPrime = (1.0 - k1 * c * d) * qY - k * d * qV;

            pi.Vq = qV; pi.Vp = pV; pi.VpPrime = pVPrime; pi.VqPrime = qVPrime;
            pi.Yp = pY; pi.Yq = qY; pi.YpPrime = pYPrime; pi.YqPrime = qYPrime;
            pi.Iq = i1q; pi.Ip = i1p; pi.IqPrime = i1qPrime; pi.IpPrime = i1pPrime;

            ti[1] = pV;  ti[2] = pVPrime;  ti[3] = qV;  ti[4] = qVPrime;
            if (SMath.Abs(c) > 1e-12) ti[5] = n * SafeInverse(c) * i1p;
            ti[6] = i1p - i1pPrime;
            if (SMath.Abs(c) > 1e-12) ti[7] = n * SafeInverse(c) * i1q;
            ti[8] = i1q - i1qPrime;
            ti[9] = i1p * i1pPrime;
            ti[10] = i1p * i1qPrime;
            ti[11] = i1q * i1qPrime;
            ti[12] = pV * pV;
            ti[13] = pV * pVPrime;
            ti[14] = pVPrime * pVPrime;
            ti[15] = qV * qV;
            ti[16] = qV * qVPrime;
            ti[17] = qVPrime * qVPrime;
            ti[18] = pV * qVPrime + qV * pVPrime;
            ti[19] = pV * qV;
            ti[20] = pVPrime * qVPrime;
            if (SMath.Abs(k1) > 1e-12) ti[21] = k / (k1 * k1);
            ti[22] = ti[9] + ti[14];
            ti[23] = ti[10] + ti[20];
            ti[24] = ti[11] + ti[17];
            ti[25] = ti[12] - ti[14];
            ti[26] = ti[19] - ti[20];
            ti[27] = ti[15] - ti[17];
            ti[28] = ti[6] * ti[22];  ti[29] = ti[6] * ti[23];  ti[30] = ti[6] * ti[24];
            ti[31] = ti[8] * ti[22];  ti[32] = ti[8] * ti[23];  ti[33] = ti[8] * ti[24];
            ti[34] = ti[2] * ti[25];  ti[35] = ti[2] * ti[26];  ti[36] = ti[2] * ti[27];
            ti[37] = ti[4] * ti[25];  ti[38] = ti[4] * ti[26];  ti[39] = ti[4] * ti[27];
            ti[40] = 0.5 * (ti[28] + ti[34]);
            ti[41] = ti[29] + ti[35];
            ti[42] = 0.5 * (ti[30] + ti[36]);
            ti[43] = 0.5 * (ti[31] + ti[37]);
            ti[44] = ti[32] + ti[38];
            ti[45] = 0.5 * (ti[33] + ti[39]);
            ti[46] = ti[5] * ti[40];  ti[47] = ti[5] * ti[41];  ti[48] = ti[5] * ti[42];
            ti[49] = ti[5] * ti[43];  ti[50] = ti[5] * ti[44];  ti[51] = ti[5] * ti[45];
            ti[52] = ti[7] * ti[40];  ti[53] = ti[7] * ti[41];  ti[54] = ti[7] * ti[42];
            ti[55] = ti[7] * ti[43];  ti[56] = ti[7] * ti[44];  ti[57] = ti[7] * ti[45];
            ti[70] = -0.25 * (3.0 * ti[12] + ti[14]);
            ti[71] = -0.5 * (3.0 * ti[19] + ti[20]);
            ti[72] = -0.25 * (3.0 * ti[15] + ti[17]);
            ti[73] = ti[70] + 0.75 * ti[22];
            ti[74] = ti[71] + 1.5 * ti[23];
            ti[75] = ti[72] + 0.75 * ti[24];
            ti[76] = ti[70] + ti[13];
            ti[77] = ti[71] + ti[18];
            ti[78] = ti[72] + ti[16];
            ti[79] = ti[21] * ti[6];
            ti[80] = 0.5 * (ti[1] * ti[22] + ti[79] * ti[25]);
            ti[81] = ti[1] * ti[23] + ti[79] * ti[26];
            ti[82] = 0.5 * (ti[1] * ti[24] + ti[79] * ti[27]);
            ti[83] = ti[40] * ti[73] + ti[76] * ti[80];
            ti[84] = ti[41] * ti[73] + ti[40] * ti[74] + ti[76] * ti[81] + ti[77] * ti[80];
            ti[85] = ti[42] * ti[73] + ti[40] * ti[75] + ti[76] * ti[82] + ti[78] * ti[80];
            ti[86] = ti[41] * ti[74] + ti[77] * ti[81];
            ti[87] = ti[42] * ti[74] + ti[41] * ti[75] + ti[77] * ti[82] + ti[78] * ti[81];
            ti[88] = ti[42] * ti[75] + ti[78] * ti[82];

            if (!stopFound && surfaces[i].IsStop)
            {
                // p is the ratio of the q and p ray heights AT the stop plane. The chief
                // ray is q + p times the marginal, and its height at the stop is zero by
                // definition, so p = -q_y / p_y there.
                //
                // The version this was ported from propagated both rays forward by the
                // separation after the stop before taking the ratio, which measures at the
                // NEXT surface instead. That made the answer depend on a dummy - a plane
                // with no index change, which cannot alter anything physical - and it
                // reproduced Buchdahl's published p only because his fixture places a dummy
                // exactly at the true stop, which lies between two refracting surfaces.
                // Two errors cancelling: the stop flagged one surface early, and the
                // formula reaching one surface late.
                if (SMath.Abs(pY) > 1e-12)
                {
                    stopPosition = -qY / pY;
                    denominator = pY;
                    result.P = stopPosition;
                    stopFound = true;
                }
            }

            qV = qVPrime; pV = pVPrime; qY = qYPrime; pY = pYPrime;
            result.PRayFinalAngle = pVPrime;
        }

        // Running sums over preceding surfaces: this is what makes a coefficient INDUCED
        // rather than intrinsic - what the surface inherits from everything before it.
        for (int k = 0; k <= 6; k += 6)
            for (int offset = 0; offset < 6; offset++)
                for (int i = 1; i < count - 1; i++)
                {
                    Scalar sum = 0.0;
                    for (int j = 0; j < i; j++) sum += t[j][46 + k + offset];
                    t[i][58 + k + offset] = sum;
                }

        for (int i = 1; i < count - 1; i++)
        {
            var ti = t[i];
            ti[89] = ti[12] - ti[64];
            ti[90] = 2.0 * ti[67] + 3.0 * ti[62];
            ti[91] = ti[89] + ti[59];
            ti[92] = ti[19] - ti[62];
            ti[93] = ti[92] - 2.0 * ti[67];
            ti[94] = ti[12] + ti[61];
            ti[95] = ti[94] + ti[59];
            ti[96] = ti[66] - ti[68];
            ti[97] = ti[19] + ti[65];
            ti[98] = ti[96] - ti[68];
            ti[99] = ti[97] + 2.0 * ti[60];
            ti[100] = 2.0 * ti[66] - ti[63];
            ti[101] = ti[100] - ti[68];
            ti[102] = 4.0 * ti[19] + ti[90];
        }

        // t103..t108 are products Buchdahl numbers only because each is used twice: he
        // introduces every one of them in a barred block and then reuses it in the
        // unbarred block below (t103 appears in s1bar and again in s2, t104 in s2bar and
        // again - doubled - in s4, and so on). They are not intermediates in their own
        // right, which is why they look so slight next to the entries around them.
        for (int i = 1; i < count - 1; i++)
        {
            var ti = t[i];
            ti[103] = ti[58] * ti[53];
            ti[104] = -ti[68] * ti[46];
            ti[105] = 2.0 * ti[58] * ti[54];
            ti[106] = -ti[69] * ti[46];
            ti[107] = 2.0 * ti[59] * ti[54];
            ti[108] = -ti[69] * ti[47];
        }

        for (int i = 1; i < count - 1; i++)
        {
            var ti = t[i];
            var pi = parax[i];
            pi.S1p = -3.0 * ti[61] * ti[46] + ti[58] * ti[52] + ti[58] * ti[47] + ti[5] * ti[83];
            pi.S1pBar = -ti[67] * ti[46] + ti[89] * ti[52] + ti[58] * ti[53] + ti[7] * ti[83];
            pi.S2p = -ti[90] * ti[46] + ti[59] * ti[52] + ti[91] * ti[47] + ti[103]
                     + 2.0 * ti[58] * ti[48] + ti[5] * ti[84];
            pi.S2pBar = -ti[68] * ti[46] + ti[93] * ti[52] - ti[67] * ti[47] + ti[95] * ti[53]
                        + 2.0 * ti[58] * ti[54] + ti[7] * ti[84];
            pi.S3p = -3.0 * ti[63] * ti[46] + ti[60] * ti[52] + 0.5 * ti[19] * ti[47]
                     + ti[94] * ti[48] + 0.5 * ti[105] + ti[5] * ti[85];
            pi.S3pBar = -ti[69] * ti[46] + ti[96] * ti[52] + 0.5 * ti[19] * ti[53]
                        - ti[67] * ti[48] + 3.0 * ti[64] * ti[54] + ti[7] * ti[85];
            pi.S4p = 2.0 * ti[104] + ti[92] * ti[47] + ti[59] * ti[53] + 2.0 * ti[59] * ti[48]
                     + ti[5] * ti[86];
            pi.S4pBar = -2.0 * ti[68] * ti[52] - ti[68] * ti[47] + ti[97] * ti[53]
                        + 2.0 * ti[59] * ti[54] + ti[7] * ti[86];
            pi.S5p = 2.0 * ti[106] + ti[98] * ti[47] + ti[60] * ti[53] + ti[99] * ti[48]
                     + 0.5 * ti[107] + ti[5] * ti[87];
            pi.S5pBar = -2.0 * ti[69] * ti[52] - ti[69] * ti[47] + ti[101] * ti[53]
                        - ti[68] * ti[48] + ti[102] * ti[54] + ti[7] * ti[87];
            pi.S6p = ti[108] + ti[100] * ti[48] + ti[60] * ti[54] + ti[5] * ti[88];
            pi.S6pBar = -ti[69] * ti[53] - ti[69] * ti[48] + 3.0 * ti[66] * ti[54] + ti[7] * ti[88];
        }

        Scalar Sum(int index)
        {
            Scalar s = 0.0;
            for (int i = 1; i < count - 1; i++) s += t[i][index];
            return s;
        }

        result.Ap = Sum(46); result.Bp = Sum(47); result.Cp = Sum(48);
        result.Aq = Sum(49); result.Bq = Sum(50); result.Cq = Sum(51);
        result.ApBar = Sum(52); result.BpBar = Sum(53); result.CpBar = Sum(54);
        result.AqBar = Sum(55); result.BqBar = Sum(56); result.CqBar = Sum(57);

        for (int i = 1; i < count - 1; i++)
        {
            var pi = parax[i];
            result.S1p += pi.S1p; result.S1pBar += pi.S1pBar;
            result.S2p += pi.S2p; result.S2pBar += pi.S2pBar;
            result.S3p += pi.S3p; result.S3pBar += pi.S3pBar;
            result.S4p += pi.S4p; result.S4pBar += pi.S4pBar;
            result.S5p += pi.S5p; result.S5pBar += pi.S5pBar;
            result.S6p += pi.S6p; result.S6pBar += pi.S6pBar;
        }

        if (stopFound && SMath.Abs(denominator) > 1e-12)
        {
            result.EntrancePupilPosition = stopPosition;
            Scalar radius = stopRadius;
            if (radius <= 0.0)
                for (int i = 0; i < count; i++)
                    if (surfaces[i].IsStop) { radius = surfaces[i].SemiDiameter; break; }
            result.StopRadius = radius;
            result.EntrancePupilRadius = SMath.Abs(radius) > 1e-12 ? radius / denominator : 0.0;
            result.Evaluated = true;
        }

        result.Surfaces = parax;
        result.T = t;
        return result;
    }
}
