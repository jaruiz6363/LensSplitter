using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// Thompson's aberration field vectors for one wave aberration coefficient: the moments of the
/// per-surface sigma vectors, weighted by each surface's contribution to that coefficient.
///
/// <para>Source: Thompson, K. P., "Multinodal fifth-order optical aberrations of optical systems
/// without rotational symmetry: the comatic aberrations," <i>J. Opt. Soc. Am. A</i> <b>27</b>,
/// 1490 (2010), <b>Appendix A</b>:</para>
/// <code>
///     W_klm   = sum_j W_klmj                  H_klm = H - a_klm
///     A_klm   = sum_j W_klmj sigma_j          a_klm = A_klm / W_klm
///     B_klm   = sum_j W_klmj (sigma_j . sigma_j)
///                                             b_klm = B_klm / W_klm - (a_klm . a_klm)
///     B^2_klm = sum_j W_klmj sigma_j^2        b^2_klm = B^2_klm / W_klm - a_klm^2
///     C_klm   = sum_j W_klmj (sigma_j . sigma_j) sigma_j
///                                             c_klm = C_klm / W_klm - (a_klm . a_klm) a_klm
///     C^3_klm = sum_j W_klmj sigma_j^3        c^3_klm = C^3_klm / W_klm - a_klm^3
/// </code>
///
/// <para><b>The distinction that matters.</b> <c>B</c> takes the DOT product of sigma with itself
/// and is a scalar; <c>B^2</c> takes the VECTOR square and is a vector. The same split repeats at
/// the next order in <c>C</c> and <c>C^3</c>. Confusing them produces a plausible wrong node
/// position rather than an obvious error.</para>
///
/// <para><b>Which moments an aberration needs</b> follows from its power of the field. The
/// displacement acts only on <c>H</c>, so a coefficient with no field dependence at all - W040,
/// W060 - is not displaced and has no field vectors. Beyond that, expanding <c>(H - sigma_j)</c>
/// to the k-th power brings in moments up to the k-th.</para>
///
/// <para><b>There is no intrinsic/induced split here, and that is Thompson's own convention.</b>
/// Appendix A defines <c>W_klmj</c> with "j, surface number" and nothing else: it is the plain
/// contribution of surface j, intrinsic and induced together, exactly as a rotationally symmetric
/// computation produces it. Thompson does use the word "induced" in these papers, but for a
/// different thing - the lower-order terms thrown off when a fifth-order term is expanded about
/// its displaced field centre, which he then folds into the third-order expressions.</para>
/// </summary>
public readonly struct FieldMoments
{
    /// <summary>The coefficient itself, <c>sum_j W_klmj</c>.</summary>
    public readonly Scalar W;

    /// <summary>First moment, <c>sum_j W_klmj sigma_j</c>.</summary>
    public readonly Vec2 A;

    /// <summary>Second moment by DOT product, a scalar: <c>sum_j W_klmj (sigma_j . sigma_j)</c>.</summary>
    public readonly Scalar B;

    /// <summary>Second moment by VECTOR square: <c>sum_j W_klmj sigma_j^2</c>.</summary>
    public readonly Vec2 B2;

    /// <summary>Third moment, mixed: <c>sum_j W_klmj (sigma_j . sigma_j) sigma_j</c>.</summary>
    public readonly Vec2 C;

    /// <summary>Third moment by VECTOR cube: <c>sum_j W_klmj sigma_j^3</c>.</summary>
    public readonly Vec2 C3;

    /// <summary>
    /// Fourth moment by DOT product, a scalar: <c>sum_j W_klmj (sigma_j . sigma_j)^2</c>. Added
    /// by the 2011 paper's Appendix A, which the field-quartic aberrations need.
    /// </summary>
    public readonly Scalar D;

    /// <summary>
    /// Fourth moment, mixed: <c>sum_j W_klmj (sigma_j . sigma_j) sigma_j^2</c>.
    /// </summary>
    public readonly Vec2 D2;

    /// <summary>
    /// Fifth moment: <c>sum_j W_klmj (sigma_j . sigma_j)^2 sigma_j</c>. Needed only by
    /// fifth-order distortion, whose field dependence is the highest in the expansion.
    /// </summary>
    public readonly Vec2 E;

    public FieldMoments(Scalar w, Vec2 a, Scalar b, Vec2 b2, Vec2 c, Vec2 c3,
                        Scalar d = default, Vec2 d2 = default, Vec2 e = default)
    {
        W = w; A = a; B = b; B2 = b2; C = c; C3 = c3; D = d; D2 = d2; E = e;
    }

    /// <summary>
    /// Whether the coefficient is large enough for the normalised vectors to mean anything.
    /// Dividing by a vanishing <c>W</c> sends the "node" to infinity, which is the correct
    /// physical statement - the aberration has no node because it has no magnitude - but it is
    /// not a number to hand to a caller.
    /// </summary>
    public bool HasField => SMath.Abs(W) > 1e-300;

    /// <summary>The field centre, <c>a_klm = A_klm / W_klm</c>.</summary>
    public Vec2 a => HasField ? (1.0 / W) * A : Vec2.Zero;

    /// <summary>The normalised scalar second moment about the field centre.</summary>
    public Scalar b => HasField ? B / W - Vec2.Dot(a, a) : 0.0;

    /// <summary>The normalised vector second moment about the field centre.</summary>
    public Vec2 b2 => HasField ? (1.0 / W) * B2 - a.Squared : Vec2.Zero;

    /// <summary>The normalised mixed third moment about the field centre.</summary>
    public Vec2 c => HasField ? (1.0 / W) * C - Vec2.Dot(a, a) * a : Vec2.Zero;

    /// <summary>The normalised vector third moment about the field centre.</summary>
    public Vec2 c3 => HasField ? (1.0 / W) * C3 - a.Squared * a : Vec2.Zero;

    /// <summary>The normalised scalar fourth moment about the field centre.</summary>
    public Scalar d => HasField ? D / W - Vec2.Dot(a, a) * Vec2.Dot(a, a) : 0.0;

    /// <summary>The normalised mixed fourth moment about the field centre.</summary>
    public Vec2 d2 => HasField ? (1.0 / W) * D2 - Vec2.Dot(a, a) * a.Squared : Vec2.Zero;

    /// <summary>The normalised fifth moment about the field centre.</summary>
    public Vec2 e => HasField ? (1.0 / W) * E - (Vec2.Dot(a, a) * Vec2.Dot(a, a)) * a : Vec2.Zero;

    /// <summary>
    /// Accumulate the moments over the surfaces.
    /// </summary>
    /// <param name="contribution">Surface j's contribution to this coefficient, <c>W_klmj</c>.</param>
    /// <param name="sigma">Surface j's sigma vector.</param>
    /// <param name="count">Number of surfaces; indices 0 to count-1 are passed to the callbacks.</param>
    public static FieldMoments Accumulate(Func<int, Scalar> contribution, Func<int, Vec2> sigma,
                                          int count)
    {
        if (contribution == null) throw new ArgumentNullException(nameof(contribution));
        if (sigma == null) throw new ArgumentNullException(nameof(sigma));

        Scalar w = 0.0, b = 0.0, d = 0.0;
        Vec2 a = Vec2.Zero, b2 = Vec2.Zero, c = Vec2.Zero, c3 = Vec2.Zero, d2 = Vec2.Zero, e = Vec2.Zero;

        for (int j = 0; j < count; j++)
        {
            Scalar wj = contribution(j);
            Vec2 s = sigma(j);
            Scalar dot = Vec2.Dot(s, s);
            Vec2 sq = s.Squared;

            w += wj;
            a += wj * s;
            b += wj * dot;
            b2 += wj * sq;
            c += (wj * dot) * s;
            c3 += wj * (sq * s);
            d += wj * dot * dot;
            d2 += (wj * dot) * sq;
            e += (wj * dot * dot) * s;
        }

        return new FieldMoments(w, a, b, b2, c, c3, d, d2, e);
    }

    /// <summary>
    /// The same moments with the vector cube replaced. A freeform overlay adds to <c>C^3</c>
    /// without touching the others - Fuerschbach 2014 Table 2 - so this is the one substitution
    /// worth having rather than a general-purpose mutable struct.
    /// </summary>
    public FieldMoments WithC3(Vec2 c3) => new(W, A, B, B2, C, c3, D, D2, E);

    /// <summary>The same moments with the first moment replaced - Fuerschbach 2014 Table 4.</summary>
    public FieldMoments WithA(Vec2 a) => new(W, a, B, B2, C, C3, D, D2, E);

    /// <summary>The same moments with the vector square replaced - Table 3.</summary>
    public FieldMoments WithB2(Vec2 b2) => new(W, A, B, b2, C, C3, D, D2, E);

    /// <summary>Accumulate from parallel lists.</summary>
    public static FieldMoments Accumulate(IReadOnlyList<Scalar> contributions,
                                          IReadOnlyList<Vec2> sigmas)
    {
        if (contributions == null) throw new ArgumentNullException(nameof(contributions));
        if (sigmas == null) throw new ArgumentNullException(nameof(sigmas));
        int n = Math.Min(contributions.Count, sigmas.Count);
        return Accumulate(j => contributions[j], j => sigmas[j], n);
    }
}
