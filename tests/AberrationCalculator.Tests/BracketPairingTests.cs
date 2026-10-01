using System;
using AberrationCalculator.Core.Aberrations;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The pairing (X|Y) of M Sec. 21-22, identified.
///
/// <para>M (21.5) introduces the bracket notation the secondary identities are written in -
/// <c>(1/N1)(A|Abar) = [AAbar]</c> and <c>(A|v) = [A]</c> - without defining the pairing
/// itself, and that definition was the last thing standing between this program and the six
/// tertiary identities. M (20.21) gives it away by using it on paraxial rays:
/// <c>(v'|v) = -N1 pomega</c>. It is the skew bracket over the p and q pair,</para>
///
/// <code>
///     (X|Y) = X_p Y_q - X_q Y_p
/// </code>
///
/// <para>which is testable without assuming it, because <c>[AB]</c> can be DETERMINED: omega1
/// is <c>Sbar1p - S1q</c> and omega7 is <c>4 Sbar1p - S2p</c>, both computable from the scheme,
/// and (21.6) against (22.41) gives <c>omega7 - 2 omega1 = 3[AB] + paraxial terms</c>. The
/// bracket reproduces those determined values exactly.</para>
///
/// <para>The obvious alternative reading, that <c>(X|y)</c> is the plain product <c>X y</c>,
/// is refuted by the same test - it disagrees by factors of one to seven hundred, with sign
/// changes. Paper III, Secs. 5 and 6, gives the identities.</para>
/// </summary>
public class BracketPairingTests
{
    private static double Pair(double xp, double xq, double yp, double yq)
        => xp * yq - xq * yp;

    /// <summary>
    /// [AB], accumulated by (22.12) with the bracket, against the value omega1 and omega7
    /// determine between them. Buchdahl's own triplet, where N1 is unity.
    /// </summary>
    [Fact]
    public void TheBracketReproducesTheDeterminedAB()
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        const double N1 = 1.0;
        double vp1 = rows[1][2];

        double accumulated = 0.0;
        int compared = 0;

        for (int j = 1; j <= 6; j++)
        {
            var r = rows[j];
            double vp = r[2];

            double omega1 = r[70] - r[86];              // (21.6), first member
            double omega7 = 4.0 * r[70] - r[71];        // (22.41)(i)
            double dvp = vp * vp - vp1 * vp1;
            double determined =
                (omega7 - 2.0 * omega1
                 + N1 * vp * vp * dvp
                 - 0.25 * N1 * dvp * (vp * vp + 3.0 * vp1 * vp1)) / 3.0;

            if (Math.Abs(determined) > 1e-9)
            {
                double rel = Math.Abs(accumulated - determined) / Math.Abs(determined);
                Assert.True(rel < 1e-9,
                    $"surface {j}: bracket gives [AB] = {accumulated:E6}, the identities "
                  + $"require {determined:E6} ({rel:E2} apart)");
                compared++;
            }

            // (22.12): Delta[AB] = (1/N1){(A|b) - (B|a) + (a|b)}.
            double Ap = r[15], Aq = r[20], Abp = r[16], Bq = r[22];
            double Bp = 2.0 * Abp;          // (20.42)
            double ap = r[10], aq = r[99];
            double bp = 2.0 * r[11];        // (20.32): b_p = 2 abar_p, printed
            double bq = r[100];

            accumulated += (Pair(Ap, Aq, bp, bq) - Pair(Bp, Bq, ap, aq)
                            + Pair(ap, aq, bp, bq)) / N1;
        }

        Assert.True(compared >= 4, $"only {compared} surfaces carried a testable [AB]");
    }

    /// <summary>
    /// The bracket is skew: (X|Y) = -(Y|X), and (X|X) vanishes. Cheap, and it is what makes
    /// (22.13)'s two terms a difference rather than a sum.
    /// </summary>
    [Fact]
    public void TheBracketIsSkew()
    {
        Assert.Equal(-Pair(3.0, 5.0, 7.0, 11.0), Pair(7.0, 11.0, 3.0, 5.0), 12);
        Assert.Equal(0.0, Pair(3.0, 5.0, 3.0, 5.0), 12);
    }
}

/// <summary>
/// The barred q-side secondary coefficients, recovered from M Sec. 22's identities.
///
/// <para>The recovery has exactly one internal check and it is a strong one: (22.42) and
/// (22.53) each yield Sbar_2q, by different routes through different brackets. For them to
/// agree, the pairing, the two derived primaries B_p = 2 Abar_p and Bbar_q = 2 C_q, and the
/// transcription of two dense printed equations must all be right.</para>
///
/// <para>They did NOT agree at first, and the disagreement caught two genuine misreadings:
/// (22.42) carries [BbarAbar] where it had been read as [BbarA], and (22.53) carries [AbarBbar]
/// where it had been read as [ABbar]. Both forms occur on the same page - (22.51) really does
/// use [ABbar] - which is how the confusion arose and why the cross-check was worth having.</para>
/// </summary>
public class SecondaryQTests
{
    [Fact]
    public void TheTwoRoutesToTheSecondBarredQAgree()
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        int compared = 0;

        for (int j = 2; j <= 6; j++)
        {
            double viaFirst = BuchdahlSecondaryQ.At(rows, j)[2];
            double viaSecond = BuchdahlSecondaryQ.SecondBarredQAlternative(rows, j);
            if (Math.Abs(viaSecond) < 1e-9) continue;

            double rel = Math.Abs(viaFirst - viaSecond) / Math.Abs(viaSecond);
            Assert.True(rel < 1e-9,
                $"surface {j}: (22.42) gives Sbar_2q = {viaFirst:E6}, (22.53) gives "
              + $"{viaSecond:E6} ({rel:E2} apart)");
            compared++;
        }
        Assert.True(compared >= 4, $"only {compared} surfaces were comparable");
    }

    /// <summary>
    /// Sbar_6q is deliberately absent. M Sec. 19: "when all the p-coefficients are known, all
    /// but one of the q-coefficients ... can be obtained from them by means of the identities."
    /// The tertiary identities never use it, so nothing downstream should reach for it.
    /// </summary>
    [Fact]
    public void TheSixthBarredQIsNotSupplied()
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        Assert.Equal(0.0, BuchdahlSecondaryQ.At(rows, 4)[6]);
    }
}

/// <summary>
/// The six tertiary identities of paper III (8.1-6), which are the instrument the
/// tau9/tau14/tau17 hunt has wanted from the start.
///
/// <para>They vanish to machine precision on Buchdahl's own spherical triplet. That single fact
/// validates a long chain at once: the skew pairing, the recovery of Sbar_1q..Sbar_5q from
/// Sec. 22, the starred quantities of paper III (5) and (6), the transcription of six dense
/// printed equations, and the spherical tertiary scheme itself - a fourth independent
/// confirmation of the last, after the printed Table I entries, the primary and secondary
/// identities, and the published T totals.</para>
///
/// <para>On an ASPHERIC system they fail by of order a hundred per cent, which is the defect
/// this program has been chasing, now measured without a ray trace. That failure is NOT
/// asserted here - a test pinning it would enshrine it - and it is written up in
/// docs/verification.md instead.</para>
/// </summary>
public class TertiaryIdentityTests
{
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void TheTertiaryIdentitiesVanishOnASphericalSystem(int surface)
    {
        var rows = BuchdahlPublishedTableTests.SchemeRows();
        var (residual, scale) = BuchdahlTertiaryIdentities.At(rows, surface);

        for (int m = 0; m < residual.Length; m++)
        {
            double rel = Math.Abs(residual[m]) / scale[m];
            Assert.True(rel < 1e-10,
                $"(8.{m + 1}) at surface {surface}: residual {residual[m]:E3} against terms of "
              + $"order {scale[m]:E3} ({rel:E2} relative)");
        }
    }
}
