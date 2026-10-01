using System;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The third-order aberration field of a system without symmetry, and where its nodes are.
///
/// <para>Thompson, <i>J. Opt. Soc. Am. A</i> <b>22</b>, 1389 (2005), Sec. 4. Every surface still
/// contributes the rotationally symmetric field it always did; only the centre of that field
/// moves, to <c>sigma_j</c>. Summing displaced fields instead of concentric ones is the whole of
/// the theory, and what falls out is that the ZEROS of an aberration leave the axis and scatter
/// through the field in patterns characteristic of what went wrong.</para>
///
/// <para>The displacement vectors, his Eqs. (4.7), (4.15)-(4.18), (4.27)-(4.30):</para>
/// <code>
///     A131   = sum_j W131_j  sigma_j              a131  = A131 / W131
///     A222   = sum_j W222_j  sigma_j              a222  = A222 / W222
///     B222^2 = sum_j W222_j  sigma_j^2            b222^2 = B222^2 / W222 - a222^2
///     A220M  = sum_j W220M_j sigma_j              a220M = A220M / W220M
///     B220M  = sum_j W220M_j (sigma_j . sigma_j)  b220M = B220M / W220M - a220M . a220M
/// </code>
/// <para><b>Note that <c>B222^2</c> takes the VECTOR square of sigma and <c>B220M</c> the DOT
/// product.</b> Same vector, different operation, and interchanging them is easy to do and hard
/// to see afterwards.</para>
///
/// <para>Thompson's own remark under Eq. (4.31) is the implementation instruction: the
/// displacement vectors "for each aberration are identical", so <c>sigma</c> is computed once
/// per surface and only the weights differ. The vertex of the medial focal surface still does
/// not coincide with the coma node, because the weights are different sets.</para>
///
/// <h3>Why the sums are not formed the way they are written</h3>
///
/// <para><c>sigma_j</c> diverges where the chief-ray incidence vanishes. The PRODUCTS above do
/// not, because <c>W131</c> carries one factor of <c>ibar</c> and <c>W222</c> two. Writing
/// <c>nu_j = ibar_j sigma_j</c> and <c>G_j</c> for the Seidel kernel with both incidences
/// removed,</para>
/// <code>
///     W131_j sigma_j   = -(1/2) i_j G_j nu_j
///     W222_j sigma_j   = -(1/2) ibar_j G_j nu_j
///     W222_j sigma_j^2 = -(1/2) G_j nu_j^2
/// </code>
/// <para>with no division anywhere. Field curvature is the exception - <c>W220P</c> carries no
/// <c>ibar</c> - so that one term is formed from <c>sigma</c> directly and is reported as
/// unavailable at a surface where sigma could not be formed.</para>
/// </summary>
public sealed class NatField
{
    /// <summary>System third-order wave coefficients, the rotationally symmetric part.</summary>
    public WaveCoefficients.Third Totals { get; init; }

    /// <summary>The field decentre vectors this was built from.</summary>
    public SigmaVector Sigmas { get; init; } = null!;

    // ── Coma ────────────────────────────────────────────────────────────────────────────

    /// <summary>Unnormalised comatic displacement, Eq. (4.7).</summary>
    public Vec2 A131 { get; init; }

    /// <summary>
    /// The comatic node, Eq. (4.8) - the one point in the field with no third-order coma.
    /// Meaningless, and returned as zero, on a system with no third-order coma to displace.
    /// </summary>
    public Vec2 ComaNode { get; init; }

    /// <summary>False when <c>W131</c> is too near zero for the node to be located.</summary>
    public bool ComaNodeExists { get; init; }

    // ── Astigmatism ─────────────────────────────────────────────────────────────────────

    /// <summary>Unnormalised astigmatic displacement, Eq. (4.15).</summary>
    public Vec2 A222 { get; init; }

    /// <summary>The node-splitting vector's unnormalised form, Eq. (4.16).</summary>
    public Vec2 B222Squared { get; init; }

    /// <summary>Midpoint of the two astigmatic nodes, Eq. (4.17).</summary>
    public Vec2 A222Normalised { get; init; }

    /// <summary>Half the separation of the nodes, squared, Eq. (4.18).</summary>
    public Vec2 B222Squared_Normalised { get; init; }

    /// <summary>
    /// The two astigmatic nodes, Eq. (4.22): <c>H = a222 +/- i b222</c>. Shack's binodal
    /// astigmatism, and the discovery the whole theory is built on.
    /// </summary>
    public Vec2 AstigmatismNode1 { get; init; }

    /// <summary>The second astigmatic node.</summary>
    public Vec2 AstigmatismNode2 { get; init; }

    /// <summary>False when <c>W222</c> is too near zero for the nodes to be located.</summary>
    public bool AstigmatismNodesExist { get; init; }

    // ── Medial field curvature ──────────────────────────────────────────────────────────

    /// <summary>Unnormalised medial field-curvature displacement, Eq. (4.27).</summary>
    public Vec2 A220M { get; init; }

    /// <summary>The vertex of the medial focal surface, Eq. (4.29).</summary>
    public Vec2 MedialVertex { get; init; }

    /// <summary>The scalar offset of the medial surface, Eq. (4.30).</summary>
    public Scalar B220M { get; init; }

    /// <summary>False when the medial term could not be formed - see the note on the type.</summary>
    public bool MedialExists { get; init; }

    /// <summary>True when no surface is perturbed, so every node sits at the field centre.</summary>
    public bool IsAligned { get; init; }

    /// <summary>
    /// Builds the aberration field. <paramref name="seidel"/> supplies the per-surface wave
    /// coefficients and must have been computed on the same paraxial trace.
    /// </summary>
    public static NatField Compute(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                                   SeidelResult seidel)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));

        var sig = SigmaVector.Compute(system, indices, p);
        int last = system.LastOpticalSurface();
        var totals = WaveCoefficients.OfSystem(seidel);

        Vec2 a131 = Vec2.Zero, a222 = Vec2.Zero, b222sq = Vec2.Zero, a220m = Vec2.Zero;
        Scalar b220m = 0.0;
        bool medialOk = true;
        bool aligned = true;

        for (int j = 1; j <= last; j++)
        {
            if (system.Surfaces[j].IsPerturbed) aligned = false;

            // ── The spherical base curve, centred at sigma ──────────────────────────────
            Scalar g = sig.Kernel[j];
            var nu = sig.Reduced[j];

            // The three that carry their own ibar and so need no division.
            a131 += (-0.5 * sig.MarginalIncidence[j] * g) * nu;
            a222 += (-0.5 * sig.ChiefIncidence[j] * g) * nu;
            b222sq += (-0.5 * g) * nu.Squared;

            // ── The aspheric cap, centred somewhere else entirely ──────────────────────
            //
            // A figured surface contributes TWO displaced fields, not one, because the aspheric
            // departure behaves as a zero-power plate and is centred by where the optical axis
            // ray crosses it rather than by an angle of incidence. Thompson (2009) Eq. (11); on
            // his telescope the two centres differ by a factor of two, so using the spherical
            // one for both would put every node of a conic telescope in the wrong place.
            //
            // The aspheric Seidel shares are all one quantity times a power of ybar/y, so the
            // division by ybar that sigma_asph carries cancels the same way ibar does above:
            //
            //     W131_a sigma_a   = S1a nu_a / (2 y)
            //     W222_a sigma_a   = S1a ybar nu_a / (2 y^2)
            //     W222_a sigma_a^2 = S1a nu_a^2 / (2 y^2)
            //
            // and S1a goes as y^4, so nothing here is singular where the marginal ray vanishes.
            Scalar s1a = seidel.S1Aspheric.Length > j ? seidel.S1Aspheric[j] : 0.0;
            Scalar s3a = seidel.S3Aspheric.Length > j ? seidel.S3Aspheric[j] : 0.0;
            Scalar yj = p.Y[j], ybarj = p.Ybar[j];
            bool figured = s1a != 0.0 && SMath.Abs(yj) > 1e-15;

            if (figured)
            {
                var nuA = sig.ReducedAspheric[j];
                a131 += (s1a / (2.0 * yj)) * nuA;
                a222 += (s1a * ybarj / (2.0 * yj * yj)) * nuA;
                b222sq += (s1a / (2.0 * yj * yj)) * nuA.Squared;
            }

            // ── Medial field curvature, which has no such cancellation ─────────────────
            //
            // W220P carries no ibar, so this term goes through sigma itself and can be
            // unavailable. The PETZVAL part has no aspheric share at all - it depends only on
            // curvature and index step, and figuring changes neither - so the split here is
            // S4/4 plus the spherical half of S3/4, with the aspheric half taken at the
            // aspheric centre.
            // The SPHERICAL half needs sigma itself, so it is unavailable exactly where the
            // chief-ray incidence vanishes - and nowhere else. Testing the aspheric condition
            // here instead, as this once did, refused the medial vertex on any ordinary surface
            // that happened to sit at the stop, where sigma is perfectly well defined.
            if (System.Array.IndexOf(sig.SigmaSuppressedAt, j) >= 0) { medialOk = false; continue; }

            // The MEDIAL weight is W220P + W222 = S4/4 + S3/2, not S4/4 + S3/4. The second is the
            // sagittal surface, and this line carried it - an inline copy of the same error
            // WaveCoefficients.Third.W220M had, which survived the fix there because it does not
            // go through that property.
            var s = sig.Sigma[j];
            Scalar w220mSph = seidel.S4[j] / 4.0 + (seidel.S3[j] - s3a) / 2.0;
            a220m += w220mSph * s;
            b220m += w220mSph * Vec2.Dot(s, s);

            if (figured)
            {
                // And the ASPHERIC half needs the aspheric sigma, which fails at a pupil. Only a
                // figured surface can be caught by it, which is why the test sits inside here.
                if (System.Array.IndexOf(sig.SigmaAsphericSuppressedAt, j) >= 0)
                {
                    medialOk = false;
                    continue;
                }

                var sA = sig.SigmaAspheric[j];
                Scalar w220mAsph = s3a / 2.0;
                a220m += w220mAsph * sA;
                b220m += w220mAsph * Vec2.Dot(sA, sA);
            }

            // ── A Zernike departure on this surface ────────────────────────────────────
            //
            // Fuerschbach, Rolland and Thompson, Opt. Express 22, 26585 (2014). A non-symmetric
            // figure error or freeform overlay contributes a FIELD CONSTANT aberration when the
            // surface is at the stop, and as the surface moves off the stop the beam walks
            // across it by ybar/y, which turns some of that contribution field dependent.
            //
            // Their central result is that no new aberration TYPES appear: every term a Zernike
            // overlay generates is one nodal aberration theory already had, so it is added to
            // the displacement vectors above rather than carried separately.
            var surf = system.Surfaces[j];
            if (surf.HasZernike)
            {
                Scalar nBefore = p.N[j - 1], nAfter = p.N[j];
                Scalar walk = Conventions.BeamDisplacement(yj, ybarj);

                // Z5/6, astigmatism. Its ONLY image-degrading term is field constant, and
                // uniquely among the overlays that is true wherever the surface sits: moving it
                // off the stop generates a tilt and a piston, which shift the image and its
                // phase without blurring anything. Fuerschbach Eq. (13).
                //
                // A raw Fringe Z12 CARRIES astigmatism - it is 4 rho^4 cos2phi - 3 rho^2 cos2phi,
                // and the quadratic half is exactly -3 Z5. The sidecar states raw Fringe sag, so
                // that half is routed here rather than dropped. Fuerschbach works with an
                // "adjusted" Zernike, Z12 + 3 Z5, which is the same statement from the other side.
                Scalar z5 = surf.Zernike(5)
                          + Conventions.AstigmatismCarriedByObliqueSpherical(surf.Zernike(12));
                Scalar z6 = surf.Zernike(6)
                          + Conventions.AstigmatismCarriedByObliqueSpherical(surf.Zernike(13));
                if (z5 != 0.0 || z6 != 0.0)
                    b222sq += Conventions.AstigmatismOverlay(z5, z6, nBefore, nAfter);

                // Z7/8, coma. Field constant coma at the stop; away from it, also field-linear
                // astigmatism and field-linear medial field curvature, both scaled by the beam
                // walk. Fuerschbach Table 1.
                //
                // And a raw Fringe Z14 carries coma the same way: its remainder after the
                // quintic is -4 Z7 less a pupil tilt, and only the first of those blurs.
                Scalar z7 = surf.Zernike(7)
                          + Conventions.ComaCarriedByFifthOrderComa(surf.Zernike(14));
                Scalar z8 = surf.Zernike(8)
                          + Conventions.ComaCarriedByFifthOrderComa(surf.Zernike(15));
                if (z7 != 0.0 || z8 != 0.0)
                {
                    var ffComa = Conventions.ComaOverlay(z7, z8, nBefore, nAfter);
                    a131 += ffComa;
                    a222 += walk * ffComa;
                    a220m += walk * ffComa;
                }
            }
        }

        // A Zernike overlay makes the system non-symmetric even when nothing is tilted, so it
        // counts as a perturbation for the purposes of the report.
        for (int j = 1; j <= last; j++) if (system.Surfaces[j].HasZernike) aligned = false;

        bool comaOk = SMath.Abs(totals.W131) > 1e-15;
        bool astOk = SMath.Abs(totals.W222) > 1e-15;
        bool medOk = medialOk && SMath.Abs(totals.W220M) > 1e-15;

        var a131n = comaOk ? (1.0 / totals.W131) * a131 : Vec2.Zero;

        var a222n = astOk ? (1.0 / totals.W222) * a222 : Vec2.Zero;
        var b222n = astOk ? (1.0 / totals.W222) * b222sq - a222n.Squared : Vec2.Zero;

        // H = a222 +/- i b222. Multiplying by i is a quarter turn in this algebra, which the
        // unit vector at ninety degrees performs: (1, 0). Thompson Eq. (4.25).
        var quarter = new Vec2(1.0, 0.0);
        var b222 = astOk ? b222n.Sqrt() : Vec2.Zero;
        var ib222 = b222 * quarter;

        var a220n = medOk ? (1.0 / totals.W220M) * a220m : Vec2.Zero;

        return new NatField
        {
            Totals = totals,
            Sigmas = sig,
            IsAligned = aligned,

            A131 = a131,
            ComaNode = a131n,
            ComaNodeExists = comaOk,

            A222 = a222,
            B222Squared = b222sq,
            A222Normalised = a222n,
            B222Squared_Normalised = b222n,
            AstigmatismNode1 = a222n + ib222,
            AstigmatismNode2 = a222n - ib222,
            AstigmatismNodesExist = astOk,

            A220M = a220m,
            MedialVertex = a220n,
            B220M = medOk ? b220m / totals.W220M - Vec2.Dot(a220n, a220n) : 0.0,
            MedialExists = medOk,
        };
    }

    /// <summary>
    /// The comatic aberration vector at a field point, Eq. (4.9). Its magnitude is the amount of
    /// third-order coma there and its orientation the direction the flare points.
    /// </summary>
    public Vec2 ComaAt(Vec2 h) => Totals.W131 * (h - ComaNode);

    /// <summary>
    /// The astigmatic aberration vector at a field point, Eq. (4.19). Its magnitude is the
    /// astigmatism there; HALF its orientation is the azimuth of the line image, because the
    /// aberration is a squared-vector quantity.
    /// </summary>
    public Vec2 AstigmatismAt(Vec2 h) =>
        (0.5 * Totals.W222) * ((h - A222Normalised).Squared + B222Squared_Normalised);
}
