using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// How much wavefront error a decentre and tilt tolerance will cost this design, analytically.
///
/// <para>Gu, Wang and Yan, <i>Opt. Express</i> <b>28</b>(6), 7928 (2020). Nodal aberration theory
/// says a tilted or decentred surface contributes the same rotationally symmetric aberration
/// field it always did, displaced by a vector; the displacement is linear in the perturbation,
/// so the induced coma and astigmatism can be written down in closed form from the paraxial
/// marginal and chief rays. <b>No rays are traced beyond the two paraxial ones the program
/// already computes</b>, and the result is algebraic in <c>y, ybar, u, ubar, n, c</c>, so it
/// differentiates exactly on the dual-number compile like everything else in the chain.</para>
///
/// <para>What it is FOR: a design can be driven to a smaller predicted spot by making it more
/// sensitive to the tolerances it will actually be built to, and nothing else in the merit
/// function objects. This is the term that objects.</para>
///
/// <h3>The sigma vector, and why it is never formed here</h3>
///
/// <para>Gu's Eq. (7) gives the aberration field decentre vector of surface <c>j</c> when
/// surface <c>k</c> is perturbed as <c>(T_k + c_k D_k) xi_j / ibar_j</c>. That carries a
/// <c>1 / ibar_j</c>, which is singular wherever the chief ray strikes a surface at normal
/// incidence.</para>
///
/// <para><b>The singularity is not real.</b> <c>W131</c> carries one factor of <c>ibar</c> and
/// <c>W222</c> carries two, and in every term they cancel the division exactly. Writing</para>
/// <code>
///     G_j = n_j^2 y_j d(u/n)_j
/// </code>
/// <para>- which is <c>-S1_j / i_j^2</c> and <c>-S2_j / (i_j ibar_j)</c> and
/// <c>-S3_j / ibar_j^2</c>, so it is the Seidel kernel with both incidences taken out - the three
/// sensitivities are</para>
/// <code>
///     field-constant coma        i_j G_j / (4 sqrt2)      Gu Eq. (10)
///     field-linear astigmatism   ibar_j G_j / (2 sqrt6)   Gu Eq. (15), first line
///     field-constant astigmatism G_j / (4 sqrt6)          Gu Eq. (15), second line
/// </code>
/// <para>summed over <c>j >= k</c> with the <c>xi_j</c> factor. There is no division anywhere in
/// that. <b>Do not recover <c>G_j</c> from the Seidel arrays</b> - going back through S1, S2 or
/// S3 reintroduces precisely the division that was just cancelled.</para>
///
/// <h3>Two modelling choices, stated rather than buried</h3>
///
/// <para><b>Root-sum-square, including between the two astigmatic parts.</b> Gu's Eq. (15)
/// combines the field-linear and field-constant astigmatism as a signed vector sum, which is
/// right for a surface tilted a known way. A TOLERANCE has no known direction, so the expected
/// squared magnitude is the sum of squares, and that is what is used here. It agrees with Gu
/// wherever the phases happen to align and is the honest expectation otherwise.</para>
///
/// <para><b>The nominal performance is not included.</b> Gu's Eq. (1) adds the nominal wavefront
/// error to the induced one. That is left out: the nominal is already the business of
/// <c>PRMSA</c>, which measures a spot radius in length units rather than a wavefront in waves,
/// and adding the two would be adding different quantities. Ask for both operands.</para>
///
/// <para>Units are those of the Seidel sums - the design's length units - not waves. Divide by
/// the wavelength if waves are wanted; for optimisation the scale is immaterial.</para>
/// </summary>
public static class Sensitivity
{
    private static readonly Scalar ComaNorm = 1.0 / (4.0 * Math.Sqrt(2.0));
    private static readonly Scalar AstLinNorm = 1.0 / (2.0 * Math.Sqrt(6.0));
    private static readonly Scalar AstConNorm = 1.0 / (4.0 * Math.Sqrt(6.0));

    /// <summary>
    /// What one surface's unit of equivalent tilt costs, split by the three aberration terms it
    /// induces. Multiply by the equivalent tilt to get a wavefront error.
    /// </summary>
    public readonly struct Kernel
    {
        /// <summary>Field-constant coma, per unit equivalent tilt.</summary>
        public readonly Scalar Coma;

        /// <summary>Field-linear astigmatism, per unit equivalent tilt and unit field.</summary>
        public readonly Scalar AstigmatismLinear;

        /// <summary>Field-constant astigmatism, per unit equivalent tilt SQUARED.</summary>
        public readonly Scalar AstigmatismConstant;

        public Kernel(Scalar coma, Scalar astLinear, Scalar astConstant)
        {
            Coma = coma; AstigmatismLinear = astLinear; AstigmatismConstant = astConstant;
        }
    }

    /// <summary>
    /// The sensitivity kernels of every surface, indexed like
    /// <see cref="OpticalSystem.Surfaces"/>. Index 0 and the image surface are zero.
    ///
    /// <para>A surface ahead of the perturbed one contributes nothing - light has not reached it
    /// yet - which is why the inner sum starts at <c>k</c> and not at one.</para>
    /// </summary>
    public static Kernel[] PerSurface(OpticalSystem system, Scalar[] indices, ParaxialResult p)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();
        var result = new Kernel[count];

        Scalar invariant = p.LagrangeInvariant;
        if (SMath.Abs(invariant) < 1e-15) return result;   // no field: nothing to displace

        // Per-surface quantities that do not depend on which surface is perturbed.
        var i = new Scalar[count];
        var ibar = new Scalar[count];
        var g = new Scalar[count];

        for (int j = 1; j <= last; j++)
        {
            Scalar nBefore = p.N[j - 1];
            Scalar nAfter = p.N[j];
            if (SMath.Abs(nBefore) < 1e-15 || SMath.Abs(nAfter) < 1e-15) continue;

            Scalar c = system.Surfaces[j].VertexCurvature;
            Scalar y = p.Y[j];
            Scalar u = p.U[j - 1];
            Scalar ybar = p.Ybar[j];
            Scalar ubar = p.Ubar[j - 1];

            i[j] = y * c + u;
            ibar[j] = ybar * c + ubar;
            g[j] = nBefore * nBefore * y * (p.U[j] / nAfter - u / nBefore);
        }

        for (int k = 1; k <= last; k++)
        {
            Scalar dn = p.N[k] - p.N[k - 1];
            Scalar yk = p.Y[k];
            Scalar ybark = p.Ybar[k];

            Scalar coma = 0.0, lin = 0.0, con = 0.0;

            for (int j = k; j <= last; j++)
            {
                // xi is one at the perturbed surface itself and carries the transfer beyond it.
                Scalar xi = j == k ? 1.0
                          : (i[j] * ybark - ibar[j] * yk) * dn / invariant;

                coma += i[j] * g[j] * xi;
                lin += ibar[j] * g[j] * xi;
                con += g[j] * xi * xi;
            }

            result[k] = new Kernel(ComaNorm * coma, AstLinNorm * lin, AstConNorm * con);
        }

        return result;
    }

    /// <summary>
    /// The RMS wavefront error the stated tolerances induce, averaged over the field.
    ///
    /// <para>Every surface may be decentred by <paramref name="decentre"/> and tilted by
    /// <paramref name="tilt"/>; the two enter separately, as Gu's Eq. (2) has them, because a
    /// decentre and a tilt are independent errors even though a spherical surface responds to
    /// the same combination <c>T + c D</c> of them.</para>
    /// </summary>
    /// <param name="decentre">Decentre tolerance, in the design's length units.</param>
    /// <param name="tilt">Tilt tolerance, in RADIANS.</param>
    /// <param name="meanSquareField">
    /// The mean of the squared fractional field height over the field points being weighed.
    /// Only the field-linear astigmatism sees it, and it enters squared, so the average of the
    /// square is the exact thing wanted and not an approximation of it.
    /// </param>
    public static Scalar AsBuilt(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                                 Scalar decentre, Scalar tilt, Scalar meanSquareField)
    {
        var kernels = PerSurface(system, indices, p);
        int last = system.LastOpticalSurface();

        Scalar total = 0.0;
        for (int k = 1; k <= last; k++)
        {
            var kern = kernels[k];
            Scalar c = system.Surfaces[k].VertexCurvature;

            // The two error types, as their own equivalent tilts.
            Scalar et = tilt;
            Scalar ed = c * decentre;

            total += Contribution(kern, et, meanSquareField)
                   + Contribution(kern, ed, meanSquareField);
        }

        return total > 0.0 ? SMath.Sqrt(total) : 0.0;
    }

    /// <summary>
    /// One surface's mean-square contribution for one equivalent tilt, the three induced terms
    /// summed in quadrature.
    /// </summary>
    private static Scalar Contribution(Kernel k, Scalar equivalentTilt, Scalar meanSquareField)
    {
        Scalar e2 = equivalentTilt * equivalentTilt;
        Scalar coma = k.Coma * k.Coma * e2;
        Scalar lin = k.AstigmatismLinear * k.AstigmatismLinear * e2 * meanSquareField;
        Scalar con = k.AstigmatismConstant * k.AstigmatismConstant * e2 * e2;
        return coma + lin + con;
    }
}
