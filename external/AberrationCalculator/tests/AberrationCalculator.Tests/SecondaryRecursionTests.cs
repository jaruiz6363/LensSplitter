using System;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The scheme's secondary recursion against M (218.7), written out directly.
///
/// <para>This is the map that three separate guesses failed to find - see docs/verification.md.
/// It is neither a one-to-one correspondence with the xi-eta-zeta monomials nor a diagonal
/// reweighting of them: it is TRIANGULAR IN q, each secondary quantity built from s_1p and the
/// five w with ascending powers of the incidence ratio. That shape is why a per-coefficient
/// ratio could never have reproduced it.</para>
///
/// <para><c>BuchdahlTableI</c> writes the same recursion in a rearranged form suited to
/// computation - `s3 = 0.5 q (w1 + s2) + w2 + w3` rather than `2q^2 s1 + q w1 + w2 + w3`. The
/// two are equal, and this test is what says so, because the rearrangement is not obvious by
/// inspection and a slip in it would be invisible against every spherical check in the suite:
/// the published totals would still come out right if the rearrangement were self-consistent
/// but the aspheric increment entered at the wrong step.</para>
/// </summary>
public class SecondaryRecursionTests
{
    /// <summary>
    /// The rearranged form the code uses, and the literal (218.7), for arbitrary inputs. Any
    /// algebraic difference shows up here whatever the optics.
    /// </summary>
    [Theory]
    [InlineData(0.35, 13.24, 4.44, 24.36, 10.58, 24.99, 176.65, 42.10, 9.01)]
    [InlineData(-0.094, 17.63, 3.09, -6.86, 2.30, -0.87, 163.25, 22.15, 3.91)]
    [InlineData(1.72, -23.90, -2.62, 8.08, -1.31, 0.66, -6.72, 6.15, 1.73)]
    public void TheCodeShapeEqualsTheLiteralRecursion(
        double q, double ap, double w, double w1, double w2, double w3, double w4, double w5,
        double unused)
    {
        _ = unused;

        // As BuchdahlTableI writes it.
        double s1 = 3.0 * ap * w;
        double s2 = 4.0 * q * s1 + w1;
        double s3 = 0.5 * q * (w1 + s2) + w2 + w3;
        double s4 = 2.0 * (s3 - 2.0 * w2 - w3);
        double s5 = (-q * s2 + 2.0 * s3 + s4 - 2.0 * w2) * q + w4;
        double s6 = 0.5 * (0.25 * (2.0 * w3 - 2.0 * s3 - s4) * q + w4 + s5) * q + w5;

        // As M (218.7) writes it.
        double q2 = q * q, q3 = q2 * q, q4 = q3 * q;
        double r1 = 3.0 * w * ap;
        double r2 = 4.0 * q * r1 + w1;
        double r3 = 2.0 * q2 * r1 + q * w1 + w2 + w3;
        double r4 = 4.0 * q2 * r1 + 2.0 * q * w1 - 2.0 * w2;
        double r5 = 4.0 * q3 * r1 + 3.0 * q2 * w1 - 2.0 * q * w2 + 2.0 * q * w3 + w4;
        double r6 = q4 * r1 + q3 * w1 - q2 * w2 + q2 * w3 + q * w4 + w5;

        Assert.Equal(r1, s1, 9);
        Assert.Equal(r2, s2, 9);
        Assert.Equal(r3, s3, 9);
        Assert.Equal(r4, s4, 9);
        Assert.Equal(r5, s5, 9);
        Assert.Equal(r6, s6, 9);
    }
}
