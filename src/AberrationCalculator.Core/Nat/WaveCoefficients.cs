using System;
using AberrationCalculator.Core.Aberrations;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The third-order wave aberration coefficients nodal aberration theory is written in, from the
/// Seidel sums this program already computes.
///
/// <para>NAT speaks <c>Wklm</c> - Thompson's notation, and Buchdahl's before him - where this
/// program reports Seidel sums <c>S1..S5</c>. The bridge is arithmetic and exact:</para>
/// <code>
///     W040 = S1/8     W131 = S2/2     W222 = S3/2     W220P = S4/4     W311 = S5/2
///     W220M = W220P + W222/2
/// </code>
///
/// <para>That this is the right bridge can be checked without leaving the repository. Gu 2020
/// Eq. (9) writes the coma coefficient as</para>
/// <code>
///     W131 = -(1/2) i ibar y n^2 d(u/n)
/// </code>
/// <para>and <see cref="SeidelCoefficients"/> computes <c>s2 = -A Abar y dUoverN</c> with
/// <c>A = n i</c> and <c>Abar = n ibar</c>, which is <c>-n^2 i ibar y d(u/n)</c>. The two differ
/// by the factor of two and nothing else. The same substitution against Gu Eq. (14) gives
/// <c>W222 = S3/2</c>.</para>
///
/// <para><b>Nothing here is a fit or an approximation.</b> Third-order aberration is additive
/// over surfaces, there is no induced part and no aspheric reconstruction at this order, so the
/// per-surface conversion is as sound as the Seidel sums are - which is to say validated.</para>
///
/// <para><b>Medial versus Petzval versus sagittal.</b> NAT's field curvature term is the MEDIAL
/// one, the average of the tangential and sagittal focal surfaces, because that is what has a
/// single node. The Seidel S4 is the Petzval sum. There are FOUR quantities here, not two, and
/// they are a half-astigmatism apart in a chain:</para>
/// <code>
///     Petzval    W220P = S4/4
///     sagittal   W220S = W220P + W222/2   = (S3 + S4)/4        the plain rho^2 H^2 coefficient
///     medial     W220M = W220P + W222     = (2 S3 + S4)/4      the one NAT uses
///     tangential W220T = W220P + 3W222/2  = (3 S3 + S4)/4
/// </code>
/// <para>Confusing any neighbouring pair is a common way to get the field-curvature node in the
/// wrong place, and this file did confuse two of them: <c>W220M</c> read <c>W220P + W222/2</c>,
/// which is the sagittal surface, under a comment saying medial. It went unnoticed until the
/// fifth-order route - whose medial comes from Thompson's Eq. (B1) - was printed beside it in
/// the same units and the two did not agree. Half the astigmatism is a plausible discrepancy,
/// not an obvious one.</para>
/// </summary>
public static class WaveCoefficients
{
    /// <summary>The third-order wave coefficients of one surface, or of a whole system.</summary>
    public readonly struct Third
    {
        /// <summary>Spherical aberration.</summary>
        public readonly Scalar W040;

        /// <summary>Coma.</summary>
        public readonly Scalar W131;

        /// <summary>Astigmatism.</summary>
        public readonly Scalar W222;

        /// <summary>Petzval field curvature.</summary>
        public readonly Scalar W220P;

        /// <summary>Distortion.</summary>
        public readonly Scalar W311;

        public Third(Scalar w040, Scalar w131, Scalar w222, Scalar w220p, Scalar w311)
        {
            W040 = w040; W131 = w131; W222 = w222; W220P = w220p; W311 = w311;
        }

        /// <summary>
        /// The plain <c>rho^2 H^2</c> coefficient, which is the SAGITTAL focal surface:
        /// <c>W220P + W222/2 = (S3 + S4)/4</c>. At <c>theta = 90</c> the <c>cos^2</c> term
        /// vanishes and this is what is left.
        /// </summary>
        public Scalar W220S => W220P + 0.5 * W222;

        /// <summary>
        /// The TANGENTIAL focal surface, <c>W220P + 3 W222/2 = (3 S3 + S4)/4</c>. At
        /// <c>theta = 0</c> the <c>cos^2</c> term contributes in full.
        /// </summary>
        public Scalar W220T => W220P + 1.5 * W222;

        /// <summary>
        /// The MEDIAL focal surface, the average of <see cref="W220S"/> and <see cref="W220T"/>:
        /// <c>W220P + W222 = (2 S3 + S4)/4</c>. This is the one NAT uses, because it is the one
        /// with a single node.
        ///
        /// <para><b>This was wrong until it was caught by a round trip.</b> It read
        /// <c>W220P + W222/2</c>, which is the SAGITTAL surface - the comment above it said
        /// "medial" and the formula computed the other one. The error is half the astigmatism,
        /// and it showed only when the fifth-order route, which takes its medial from Thompson's
        /// Eq. (B1), was printed beside it in the same units and the two did not match.</para>
        ///
        /// <para>Deriving it from Eq. (B1) rather than from convention: for an aligned system he
        /// writes <c>W220M(H.H)(rho.rho) + (1/2)W222(H^2 . rho^2)</c>, and since
        /// <c>cos 2t = 2 cos^2 t - 1</c> that regroups into
        /// <c>(W220M - W222/2) rho^2 H^2 + W222 rho^2 H^2 cos^2 t</c>. Matching the first term
        /// against the plain <c>rho^2 H^2</c> coefficient gives <c>W220M = W220S + W222/2</c>.</para>
        /// </summary>
        public Scalar W220M => W220P + W222;
    }

    /// <summary>The wave coefficients of surface <paramref name="j"/>.</summary>
    public static Third OfSurface(SeidelResult seidel, int j)
    {
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        if (j < 0 || j >= seidel.S1.Length)
            throw new ArgumentOutOfRangeException(nameof(j), j, "no such surface");

        return new Third(seidel.S1[j] / 8.0, seidel.S2[j] / 2.0, seidel.S3[j] / 2.0,
                         seidel.S4[j] / 4.0, seidel.S5[j] / 2.0);
    }

    /// <summary>The wave coefficients of the whole system.</summary>
    public static Third OfSystem(SeidelResult seidel)
    {
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        return new Third(seidel.TotalS1 / 8.0, seidel.TotalS2 / 2.0, seidel.TotalS3 / 2.0,
                         seidel.TotalS4 / 4.0, seidel.TotalS5 / 2.0);
    }
}
