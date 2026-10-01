using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The fifth-order aberration field of a perturbed system: where the nodes are, and what the
/// wave aberration is at any field point.
///
/// <para>Source: Thompson, K. P., "Multinodal fifth-order optical aberrations of optical systems
/// without rotational symmetry: the comatic aberrations," <i>J. Opt. Soc. Am. A</i> <b>27</b>,
/// 1490 (2010), Appendix B. Equation numbers below are his.</para>
///
/// <para><b>Three of the five fifth-order types need no new machinery.</b> His Eq. (B1) writes
/// the full expansion with <c>W240M</c> and <c>W242</c> already in reduced form,</para>
/// <code>
///     + W240M[(H - a240M).(H - a240M) + b240M](rho.rho)^2
///     + (1/2) W242{[(H - a242)^2 + b242^2].rho^2}(rho.rho)
/// </code>
/// <para>which are the third-order <c>W220M</c> and <c>W222</c> forms exactly - a vertex plus a
/// scalar, and a binodal pair. Eq. (B2) collapses <c>W151</c> to a single node like
/// <c>W131</c>. Only <c>W331M</c> and <c>W333</c> are new, and both are trinodal.</para>
///
/// <para><b>The unnormalised form is the one to compute with.</b> Thompson says so directly of
/// Eq. (B5): it "is complete and, in fact, the most useful form for programming into a simulation
/// environment, the most insightful form for recognizing nodal positions is to normalize this
/// equation". So <see cref="ComaticWave"/> evaluates the unnormalised expansion and the node
/// properties below come from the normalised one - and each is checked against the other, which
/// needs no oracle at all.</para>
/// </summary>
public sealed class NatFifthOrder
{
    /// <summary>Field vectors for field-linear, fifth-order aperture coma.</summary>
    public FieldMoments M151 { get; init; }

    /// <summary>Field vectors for field-cubed, third-order aperture coma, medial.</summary>
    public FieldMoments M331M { get; init; }

    /// <summary>Field vectors for elliptical coma (trefoil).</summary>
    public FieldMoments M333 { get; init; }

    /// <summary>Field vectors for fifth-order medial field curvature.</summary>
    public FieldMoments M240M { get; init; }

    /// <summary>Field vectors for fifth-order astigmatism.</summary>
    public FieldMoments M242 { get; init; }

    /// <summary>Third-order coma, carried because Eqs. (B13-14) modify it.</summary>
    public FieldMoments M131 { get; init; }

    /// <summary>
    /// The single node of field-linear fifth-order coma, Eq. (B9): <c>W = W151(H151.rho)(rho.rho)^2</c>
    /// with <c>H151 = H - a151</c>. It behaves exactly like third-order coma.
    /// </summary>
    public Vec2 Node151 => M151.a;

    /// <summary>
    /// The three COLLINEAR nodes of field-cubed, third-order aperture coma. Eq. (B10) leaves the
    /// field-cubed group as
    /// <code>
    ///     W331M[(H331M^2 + b331M^2) H331M*].rho (rho.rho)
    /// </code>
    /// which vanishes where the conjugate does - at the field centre - and where
    /// <c>H331M^2 = -b331M^2</c>, giving a symmetric pair about it. The three are collinear
    /// because the pair is <c>+/-</c> the same square root.
    /// </summary>
    public Vec2[] Nodes331M
    {
        get
        {
            Vec2 a = M331M.a, half = (-M331M.b2).Sqrt();
            return new[] { a, a + half, a - half };
        }
    }

    /// <summary>
    /// The three nodes of elliptical coma. Eq. (B11) is
    /// <code>
    ///     W = (1/4) W333 [H333^3 + 3 H333 b333^2 - c333^3].rho^3
    /// </code>
    /// so the nodes are the roots of a depressed cubic <c>x^3 + 3 b^2 x - c^3 = 0</c> in
    /// Thompson's vector algebra, which is Cardano's solution with every product and root taken
    /// vectorially. Writing <c>x = R + S</c> and choosing <c>R S = -b^2</c> leaves
    /// <c>R^3 + S^3 = c^3</c> and <c>R^3 S^3 = -b^6</c>, so <c>R^3</c> and <c>S^3</c> are the
    /// roots of a quadratic. The three cube roots then give
    /// <code>
    ///     2 X,    -X + i sqrt(3) X~,    -X - i sqrt(3) X~
    /// </code>
    /// with <c>X = (R + S)/2</c> and <c>X~ = (R - S)/2</c>, which is the form Thompson's Fig. 10
    /// draws.
    /// </summary>
    public Vec2[] Nodes333 => CubicNodes(M333, M333.c3);

    /// <summary>
    /// The three roots of <c>x^3 + 3 b^2 x - c^3 = 0</c> in Thompson's vector algebra, offset to
    /// the field centre. Both elliptical coma, Eq. (B11) of 2010, and the fifth-order astigmatic
    /// group of Eq. (C23) of 2011 reduce to exactly this cubic - the astigmatic one with
    /// <c>c^3</c> replaced by <c>(c^3)' = c^3 - 3 b^2 a</c> of Eq. (C24), which is why the
    /// effective cube is passed in rather than read off the moments.
    ///
    /// <para><b>The branch matters.</b> Writing <c>x = R + S</c> and imposing <c>R S = -b^2</c>
    /// leaves <c>R^3 + S^3 = c^3</c> and <c>R^3 S^3 = -b^6</c>, so the two cubes are roots of a
    /// quadratic; taking the principal cube root of one and recovering the other by division
    /// enforces the pairing, where cube-rooting both independently would not.</para>
    ///
    /// <para><b>And the degenerate case must be caught.</b> A cube root raises relative error to
    /// the one-third power, so a <c>b^2</c> or <c>c^3</c> that ought to be zero and instead sits
    /// at the round-off floor of the subtraction producing it - both are differences of nearly
    /// equal quantities - returns a node splitting of order 1e-6 rather than 1e-16. That is noise
    /// reported as physics. Below the floor of their own construction the three nodes coincide.</para>
    /// </summary>
    private static Vec2[] CubicNodes(FieldMoments m, Vec2 c3)
    {
        Vec2 a = m.a, b2 = m.b2;

        Scalar w = SMath.Abs(m.W) > 1e-300 ? SMath.Abs(m.W) : 1.0;
        Scalar aMag = a.Magnitude;
        Scalar floor2 = 1e-13 * (m.B2.Magnitude / w + aMag * aMag);
        Scalar floor3 = 1e-13 * (m.C3.Magnitude / w + aMag * aMag * aMag);
        if (b2.Magnitude <= floor2 && c3.Magnitude <= floor3)
            return new[] { a, a, a };

        Vec2 disc = (c3.Squared + 4.0 * (b2.Squared * b2)).Sqrt();
        Vec2 r = (0.5 * (c3 + disc)).CubeRoot();
        Vec2 s;
        if (r.MagnitudeSquared > 1e-300)
            s = (-b2) / r;                  // the pairing R S = -b^2 picks the branch
        else
        {
            r = Vec2.Zero;
            s = (0.5 * (c3 - disc)).CubeRoot();
        }

        Vec2 xbar = 0.5 * (r + s), xtilde = 0.5 * (r - s);
        Vec2 turn = SMath.Sqrt(3.0) * xtilde.TimesI;
        return new[] { a + 2.0 * xbar, a - xbar + turn, a - xbar - turn };
    }

    /// <summary>The vertex of fifth-order medial field curvature, and its scalar offset.</summary>
    public Vec2 Vertex240M => M240M.a;

    /// <inheritdoc cref="Vertex240M"/>
    public Scalar B240M => M240M.b;

    /// <summary>
    /// The two nodes of fifth-order astigmatism - the same binodal form Shack found at third
    /// order, with fifth-order coefficients.
    /// </summary>
    public Vec2[] Nodes242
    {
        get
        {
            Vec2 a = M242.a, half = (-M242.b2).Sqrt();
            return new[] { a + half, a - half };
        }
    }

    /// <summary>
    /// Eq. (B13). Field-cubed coma generates terms that belong with field-linear coma and change
    /// its MAGNITUDE: <c>W131E = W131 + 2 W331M b331M</c>.
    ///
    /// <para>This is what Thompson means by an induced term - a lower-order term thrown off when
    /// a higher-order one is expanded about its displaced field centre. It is a consequence of
    /// the nodal algebra, not a surface-interaction effect, and it is the reason third-order coma
    /// in a perturbed system is not simply the third-order coma of the aligned one.</para>
    /// </summary>
    public Scalar W131E => M131.W + 2.0 * M331M.W * M331M.b;

    /// <summary>
    /// Eq. (B14). The same generated terms also move the third-order coma NODE:
    /// <code>
    ///     a131E = (1/W131E)[W131 a131 + W331M(c331M - b331M^2 a331M*)]
    /// </code>
    /// </summary>
    public Vec2 Node131E
    {
        get
        {
            Scalar w = W131E;
            if (SMath.Abs(w) < 1e-300) return Vec2.Zero;
            Vec2 inner = M331M.c - M331M.b2 * M331M.a.Conjugate;
            return (1.0 / w) * (M131.W * M131.a + M331M.W * inner);
        }
    }

    /// <summary>
    /// The comatic wave aberration at a field point and pupil point, Eq. (B5), unnormalised - the
    /// form Thompson recommends for computation. The <c>W151</c> term of Eq. (B2) is included.
    ///
    /// <para>The piston and defocus term <c>delta W11</c> of his opening line is not: it is a
    /// focus adjustment rather than an aberration, and this program keeps focus decisions
    /// separate.</para>
    /// </summary>
    public Scalar ComaticWave(Vec2 h, Vec2 rho)
    {
        Scalar rr = Vec2.Dot(rho, rho);
        return Vec2.Dot(ComaVector131(h), rho) * rr
             + Vec2.Dot(ComaVector151(h), rho) * rr * rr
             + Vec2.Dot(ComaVector331M(h), rho) * rr
             + Vec2.Dot(TrefoilVector(h), rho.Squared * rho);
    }

    /// <summary>
    /// Third-order coma at a field point: <c>W131 H - A131</c>, the vector Eq. (B5) dots with
    /// <c>rho</c>. Its magnitude is the size of the coma and its orientation the direction the
    /// flare points.
    /// </summary>
    public Vec2 ComaVector131(Vec2 h) => M131.W * h - M131.A;

    /// <summary>
    /// Third-order coma WITH the terms field-cubed coma generates, Eqs. (B13-14). This is what a
    /// perturbed system actually has; <see cref="ComaVector131"/> is what it would have if the
    /// fifth order were absent.
    /// </summary>
    public Vec2 ComaVector131E(Vec2 h) => W131E * (h - Node131E);

    /// <summary>Field-linear fifth-order coma, Eq. (B2).</summary>
    public Vec2 ComaVector151(Vec2 h) => M151.W * h - M151.A;

    /// <summary>Field-cubed third-order aperture coma, Eq. (B3), unnormalised.</summary>
    public Vec2 ComaVector331M(Vec2 h)
    {
        Scalar hh = Vec2.Dot(h, h);
        return M331M.W * hh * h
             - 2.0 * Vec2.Dot(h, M331M.A) * h
             + 2.0 * M331M.B * h
             - hh * M331M.A
             + M331M.B2 * h.Conjugate
             - M331M.C;
    }

    /// <summary>
    /// Elliptical coma, Eq. (B4), the vector dotted with <c>rho^3</c>. Being a three-theta
    /// quantity, its azimuth on the sky is a THIRD of its orientation.
    /// </summary>
    public Vec2 TrefoilVector(Vec2 h)
    {
        Vec2 hSq = h.Squared;
        return 0.25 * (M333.W * (hSq * h) - 3.0 * (hSq * M333.A)
                     + 3.0 * (h * M333.B2) - M333.C3);
    }

    /// <summary>
    /// Fifth-order astigmatism, Eq. (C23)'s fifth-order group, the vector dotted with
    /// <c>rho^2</c>. A two-theta quantity, so its line-image azimuth is HALF its orientation.
    /// </summary>
    public Vec2 AstigmatismVector422(Vec2 h)
    {
        Vec2 hn = h - M422.a;
        Vec2 cubic = hn.Squared * hn + 3.0 * (hn * M422.b2) - C3Prime422;
        return (0.5 * M422.W) * (cubic * hn.Conjugate);
    }

    /// <summary>
    /// Third-order astigmatism WITH the terms fifth-order astigmatism generates, Eqs. (C19-22),
    /// as Eq. (C23)'s first group.
    /// </summary>
    public Vec2 AstigmatismVector222E(Vec2 h)
    {
        Vec2 hn = h - A222E;
        return (0.5 * W222E) * (hn.Squared + B2222ENormalised);
    }

    // ── The astigmatic types, Thompson 2011 ─────────────────────────────────────────────────

    /// <summary>Field vectors for fifth-order medial field curvature, field-quartic.</summary>
    public FieldMoments M420M { get; init; }

    /// <summary>Field vectors for fifth-order astigmatism, field-quartic.</summary>
    public FieldMoments M422 { get; init; }

    /// <summary>Third-order astigmatism, carried because Eqs. (C19-22) modify it.</summary>
    public FieldMoments M222 { get; init; }

    /// <summary>Third-order medial field curvature, carried because the 2011 Sec. 2 set modifies it.</summary>
    public FieldMoments M220M { get; init; }

    /// <summary>
    /// Eq. (C24): the field-quartic astigmatic cube, adjusted so that the nodal group of
    /// Eq. (C23) is the same depressed cubic as elliptical coma's.
    /// </summary>
    public Vec2 C3Prime422 => M422.c3 - 3.0 * (M422.b2 * M422.a);

    /// <summary>
    /// The FOUR nodes of fifth-order astigmatism - quadranodal, the signature this aberration
    /// was named for. Eq. (C23) leaves the fifth-order group as
    /// <code>
    ///     (1/2) W422 {[H422^3 + 3 H422 b422^2 - (c422^3)'] H422*}.rho^2
    /// </code>
    /// which vanishes where the conjugate does - one node at the field centre - and at the three
    /// roots of the cubic, which is elliptical coma's cubic with Eq. (C24)'s adjusted cube.
    /// </summary>
    public Vec2[] Nodes422
    {
        get
        {
            var cubic = CubicNodes(M422, C3Prime422);
            return new[] { M422.a, cubic[0], cubic[1], cubic[2] };
        }
    }

    /// <summary>Eq. (C19): fifth-order astigmatism changes the magnitude of the third-order.</summary>
    public Scalar W222E => M222.W + 3.0 * M422.W * M422.b;

    /// <summary>Eq. (C20): and its field centre.</summary>
    public Vec2 A222E
    {
        get
        {
            Scalar w = W222E;
            if (SMath.Abs(w) < 1e-300) return Vec2.Zero;
            Vec2 inner = M422.c - M422.b2 * M422.a.Conjugate;
            return (1.0 / w) * (M222.A + 1.5 * M422.W * inner);
        }
    }

    /// <summary>Eq. (C21) and (C22): and its binodal separation.</summary>
    public Vec2 B2222E => M222.B2 + M422.W * (M422.d2 - M422.c3 * M422.a.Conjugate);

    /// <inheritdoc cref="B2222E"/>
    public Vec2 B2222ENormalised
    {
        get
        {
            Scalar w = W222E;
            if (SMath.Abs(w) < 1e-300) return Vec2.Zero;
            return (1.0 / w) * B2222E - A222E.Squared;
        }
    }

    /// <summary>
    /// The two nodes of third-order astigmatism AS MODIFIED by the fifth order - Eq. (C23)'s
    /// first group, <c>(1/2) W222E (H222E^2 + b222E^2).rho^2</c>. Shack's binodal astigmatism,
    /// with the field-quartic term's contribution folded in.
    /// </summary>
    public Vec2[] Nodes222E
    {
        get
        {
            Vec2 a = A222E, half = (-B2222ENormalised).Sqrt();
            return new[] { a + half, a - half };
        }
    }

    /// <summary>
    /// Thompson 2011 Sec. 2: fifth-order medial field curvature modifies the third-order medial
    /// surface's magnitude, vertex and scalar offset.
    /// <code>
    ///     W220ME = W220M + 4 W420M b420M
    ///     a220ME = [A220M + W420M(2 c420M - 2 b420M^2 a420M*)] / W220ME
    ///     B220ME = B220M + W420M(d420M - 2 a420M^2 . b420M^2)
    ///     b220ME = B220ME/W220ME - a220ME . a220ME
    /// </code>
    /// </summary>
    public Scalar W220ME => M220M.W + 4.0 * M420M.W * M420M.b;

    /// <inheritdoc cref="W220ME"/>
    public Vec2 A220ME
    {
        get
        {
            Scalar w = W220ME;
            if (SMath.Abs(w) < 1e-300) return Vec2.Zero;
            Vec2 inner = 2.0 * M420M.c - 2.0 * (M420M.b2 * M420M.a.Conjugate);
            return (1.0 / w) * (M220M.A + M420M.W * inner);
        }
    }

    /// <inheritdoc cref="W220ME"/>
    public Scalar B220ME =>
        M220M.B + M420M.W * (M420M.d - 2.0 * Vec2.Dot(M420M.a.Squared, M420M.b2));

    /// <inheritdoc cref="W220ME"/>
    public Scalar B220MENormalised
    {
        get
        {
            Scalar w = W220ME;
            if (SMath.Abs(w) < 1e-300) return 0.0;
            return B220ME / w - Vec2.Dot(A220ME, A220ME);
        }
    }

    /// <summary>
    /// Third- and fifth-order astigmatism from the UNNORMALISED expansion: the third-order term
    /// of Eq. (C8) plus Eq. (C13), which Thompson calls "the most effective for numerical
    /// computations". Nothing here knows where a node is.
    /// </summary>
    public Scalar AstigmaticWaveUnnormalised(Vec2 h, Vec2 rho)
    {
        Scalar hh = Vec2.Dot(h, h);
        Vec2 hSq = h.Squared, rSq = rho.Squared;

        Vec2 third = M222.W * hSq - 2.0 * (h * M222.A) + M222.B2;

        Vec2 fifth = M422.W * hh * hSq
                   - 2.0 * hh * (h * M422.A)
                   + 3.0 * hh * M422.B2
                   - 2.0 * Vec2.Dot(h, M422.A) * hSq
                   - M422.C3 * h.Conjugate
                   + 3.0 * M422.B * hSq
                   - 3.0 * (h * M422.C)
                   + M422.D2;

        return 0.5 * Vec2.Dot(third, rSq) + 0.5 * Vec2.Dot(fifth, rSq);
    }

    /// <summary>
    /// The same thing from the NORMALISED nodal form, Eq. (C23), which is where the node
    /// positions come from. Agreement between the two is what verifies Eqs. (C19-24) - the
    /// modified third-order vectors and the adjusted cube - since nothing else checks them.
    /// </summary>
    public Scalar AstigmaticWaveNodal(Vec2 h, Vec2 rho)
    {
        Vec2 rSq = rho.Squared;

        Vec2 h222 = h - A222E;
        Scalar third = 0.5 * W222E * Vec2.Dot(h222.Squared + B2222ENormalised, rSq);

        Vec2 h422 = h - M422.a;
        Vec2 cubic = h422.Squared * h422 + 3.0 * (h422 * M422.b2) - C3Prime422;
        Scalar fifth = 0.5 * M422.W * Vec2.Dot(cubic * h422.Conjugate, rSq);

        return third + fifth;
    }

    /// <summary>
    /// The vector whose vanishing gives the fifth-order astigmatic nodes, Eq. (C23)'s
    /// fifth-order group. Exposed so the analytic nodes can be checked against it.
    /// </summary>
    public Vec2 FifthAstigmatismResidual(Vec2 h)
    {
        Vec2 hn = h - M422.a;
        Vec2 cubic = hn.Squared * hn + 3.0 * (hn * M422.b2) - C3Prime422;
        return cubic * hn.Conjugate;
    }

    // ── Fifth-order distortion ──────────────────────────────────────────────────────────────

    /// <summary>Field vectors for fifth-order distortion.</summary>
    public FieldMoments M511 { get; init; }

    /// <summary>
    /// The vector whose vanishing gives the fifth-order distortion nodes, and whose dot product
    /// with <c>rho</c> is the aberration itself.
    ///
    /// <para><b>Provenance.</b> Thompson's closed nodal form for this term is in his 1980
    /// dissertation, reference [10] of the 2010 paper, which is not in the archive. What IS
    /// published is the definition, Eq. (B1):</para>
    /// <code>
    ///     W = sum_j W511j [(H - sigma_j).(H - sigma_j)]^2 [(H - sigma_j).rho]
    /// </code>
    /// <para>and the expansion below is derived from it here rather than transcribed. Writing
    /// <c>u = H - sigma_j</c> and using <c>(u.u)^2 u = u^3 u*^2</c>,</para>
    /// <code>
    ///     V = sum_j W511j (H - sigma_j)^3 (H* - sigma_j*)^2
    /// </code>
    /// <para>which multiplies out into the standard moments once every mixed product is reduced:
    /// <c>sigma sigma*^2 = (sigma.sigma) sigma*</c>, <c>sigma^2 sigma*^2 = (sigma.sigma)^2</c>,
    /// <c>sigma^3 sigma* = (sigma.sigma) sigma^2</c> and <c>sigma^3 sigma*^2 =
    /// (sigma.sigma)^2 sigma</c>. Only the last needs a moment beyond the published set, which is
    /// <see cref="FieldMoments.E"/>.</para>
    ///
    /// <para>Being a derivation rather than a transcription, it is checked the strongest way
    /// available: against the defining sum evaluated surface by surface, which must agree exactly.</para>
    /// </summary>
    public Vec2 DistortionField(Vec2 h)
    {
        Scalar hh = Vec2.Dot(h, h);
        Vec2 hs = h.Conjugate, hSq = h.Squared;

        return M511.W * (hh * hh) * h
             - 2.0 * hh * (hSq * M511.A.Conjugate)
             + (hSq * h) * M511.B2.Conjugate
             - 3.0 * (hh * hh) * M511.A
             + 6.0 * hh * M511.B * h
             - 3.0 * (hSq * M511.C.Conjugate)
             + 3.0 * hh * (hs * M511.B2)
             - 6.0 * hh * M511.C
             + 3.0 * M511.D * h
             - hs.Squared * M511.C3
             + 2.0 * (hs * M511.D2)
             - M511.E;
    }

    /// <summary>
    /// The same thing straight from the definition, Eq. (B1), summed over the surfaces. Slower,
    /// and the reference <see cref="DistortionField"/> is checked against.
    /// </summary>
    public static Vec2 DistortionFieldDirect(Func<int, Scalar> w511, Func<int, Vec2> sigma,
                                             int count, Vec2 h)
    {
        Vec2 v = Vec2.Zero;
        for (int j = 0; j < count; j++)
        {
            Vec2 u = h - sigma(j);
            Scalar uu = Vec2.Dot(u, u);
            v += (w511(j) * uu * uu) * u;
        }
        return v;
    }

    /// <summary>
    /// The nodes of fifth-order distortion, WHERE THEY CAN BE HAD EXACTLY - which is the aligned
    /// and the uniformly displaced case, both of which put five coincident nodes at the field
    /// centre. Every other case returns EMPTY, and that is a deliberate refusal rather than a
    /// gap waiting to be filled in silently.
    ///
    /// <para><b>Why it is not solved.</b> Thompson's closed nodal solution is in his 1980
    /// dissertation, reference [10] of the 2010 paper, which this archive does not hold. A
    /// multi-start Newton search over <see cref="DistortionField"/> was written in its place, and
    /// it does not survive checking: on a tilted Cooke triplet a direct scan of the field over
    /// the whole disc finds four roots, at positions the search does not report, while the search
    /// returns five elsewhere; on a tilted double Gauss the two disagree again. The answer moved
    /// each time the acceptance tolerance was retuned, which is the signature of a criterion
    /// doing the deciding rather than the mathematics. Five plausible coordinates that a
    /// brute-force scan contradicts are worse than none.</para>
    ///
    /// <para><b>The field itself is exact</b> and is not affected by any of this -
    /// <see cref="DistortionField"/> is checked against the defining sum surface by surface. A
    /// caller that wants the nodes can find them from it with a solver it trusts, and will at
    /// least know it is doing so.</para>
    ///
    /// <para>The count is also not simply five. The equation carries <c>H*</c> as well as
    /// <c>H</c>, being built from <c>(H - sigma)^3 (H* - sigma*)^2</c>, so it is a harmonic-type
    /// system whose real root count depends on the design; five is an upper bound, and the scans
    /// above found four and zero.</para>
    /// </summary>
    public Vec2[] DistortionNodes()
    {
        Vec2 centre = M511.a;

        // With every sigma equal the quintuple root sits at the centre and there is nothing to
        // search for.
        Scalar w = SMath.Abs(M511.W) > 1e-300 ? SMath.Abs(M511.W) : 1.0;
        Scalar floor = 1e-13 * (SMath.Abs(M511.B) / w + Vec2.Dot(centre, centre));
        if (SMath.Abs(M511.b) <= floor)
            return new[] { centre, centre, centre, centre, centre };

        // Every root lies inside a disc set by the coefficients, not by the spread of the sigmas:
        // when W511 nearly cancels between surfaces - which it does, the contributions being far
        // larger than their sum - the field centre and the roots move far outside the sigmas.
        // Writing the field as a sum of terms of total degree k in H, a root needs
        // |W| R^5 <= sum_k coeff_k R^k, so twice the largest (coeff_k/|W|)^(1/(5-k)) bounds them.
        Scalar radius = RootBound();

        // Anything else is NOT SOLVED, and returns empty rather than a guess. See the remarks on
        // this method: a multi-start Newton search was written, and a direct scan of the field
        // over the whole disc disagreed with it on real lenses - different positions, different
        // counts, and an answer that moved every time the acceptance tolerance was retuned. Five
        // plausible coordinates that a brute-force scan contradicts are worse than none.
        _ = radius;
        return Array.Empty<Vec2>();
    }

    /// <summary>
    /// A radius containing every zero of <see cref="DistortionField"/>, from the magnitudes of its
    /// terms. Each term has a total degree in <c>H</c> - counting <c>H</c> and <c>H*</c> alike -
    /// and the leading one is degree five.
    /// </summary>
    private Scalar RootBound()
    {
        Scalar w = SMath.Abs(M511.W);
        if (w < 1e-300) return 1.0;

        // coefficient magnitude, and total degree in H, term by term
        var terms = new (Scalar mag, int degree)[]
        {
            (2.0 * M511.A.Magnitude, 4), (3.0 * M511.A.Magnitude, 4),
            (M511.B2.Magnitude, 3), (6.0 * SMath.Abs(M511.B), 3), (3.0 * M511.B2.Magnitude, 3),
            (3.0 * M511.C.Magnitude, 2), (6.0 * M511.C.Magnitude, 2), (M511.C3.Magnitude, 2),
            (3.0 * SMath.Abs(M511.D), 1), (2.0 * M511.D2.Magnitude, 1),
            (M511.E.Magnitude, 0),
        };

        Scalar bound = 0.0;
        foreach (var (mag, degree) in terms)
        {
            if (mag <= 0.0) continue;
            Scalar r = SMath.Pow(mag / w, 1.0 / (5 - degree));
            if (r > bound) bound = r;
        }
        return bound > 0.0 ? 2.0 * bound : 1.0;
    }

    /// <summary>
    /// Newton on the two real components, with a finite-difference Jacobian.
    ///
    /// <para>The acceptance test is measured against the size the field itself has over the
    /// search region, <c>|W| (R + |z|)^5</c>, not against an absolute epsilon. Newton stalling
    /// near a shallow minimum that is not a root will leave a residual many orders above that,
    /// and accepting it would report a node that does not exist - which a loose absolute
    /// tolerance did, on the first system tried.</para>
    /// </summary>
    private bool NewtonToZero(ref Vec2 z, Scalar bound)
    {
        Scalar scale = SMath.Max(1.0, z.Magnitude);
        for (int it = 0; it < 200; it++)
        {
            Vec2 f = DistortionField(z);
            if (IsZero(f, z, bound)) return true;

            Scalar h = 1e-7 * scale;
            Vec2 fx = DistortionField(new Vec2(z.X + h, z.Y));
            Vec2 fy = DistortionField(new Vec2(z.X, z.Y + h));
            Scalar j11 = (fx.X - f.X) / h, j12 = (fy.X - f.X) / h;
            Scalar j21 = (fx.Y - f.Y) / h, j22 = (fy.Y - f.Y) / h;
            Scalar det = j11 * j22 - j12 * j21;
            if (SMath.Abs(det) < 1e-300) return false;

            Scalar dx = (j22 * f.X - j12 * f.Y) / det;
            Scalar dy = (-j21 * f.X + j11 * f.Y) / det;
            z = new Vec2(z.X - dx, z.Y - dy);
            if (z.Magnitude > 1e6 * scale) return false;
        }
        return IsZero(DistortionField(z), z, bound);
    }

    /// <summary>
    /// Whether a field value is zero, measured against how big the field's own terms are AT THAT
    /// POINT.
    ///
    /// <para>The scale has to be local. Using the global search radius instead makes the
    /// tolerance enormous near the origin, and a single root then passes the test at five nearby
    /// points and is reported five times - which is exactly what it did, on a tilted triplet,
    /// before this was measured term by term.</para>
    /// </summary>
    private bool IsZero(Vec2 f, Vec2 z, Scalar bound)
    {
        Scalar r = z.Magnitude;
        Scalar r2 = r * r, r3 = r2 * r, r4 = r3 * r;
        Scalar scale = SMath.Abs(M511.W) * r4 * r
                     + 5.0 * M511.A.Magnitude * r4
                     + (4.0 * M511.B2.Magnitude + 6.0 * SMath.Abs(M511.B)) * r3
                     + (9.0 * M511.C.Magnitude + M511.C3.Magnitude) * r2
                     + (3.0 * SMath.Abs(M511.D) + 2.0 * M511.D2.Magnitude) * r
                     + M511.E.Magnitude;
        return f.Magnitude <= 1e-12 * SMath.Max(scale, 1e-300);
    }

    /// <summary>
    /// The vector whose vanishing defines the elliptical-coma nodes, Eq. (B11)'s bracket. Exposed
    /// so that the analytic nodes can be checked against it rather than against a second program.
    /// </summary>
    public Vec2 TrefoilResidual(Vec2 h)
    {
        Vec2 hn = h - M333.a;
        return hn.Squared * hn + 3.0 * (hn * M333.b2) - M333.c3;
    }

    /// <summary>
    /// The vector whose vanishing defines the field-cubed coma nodes, Eq. (B10)'s field-cubed
    /// group. Exposed for the same reason as <see cref="TrefoilResidual"/>.
    /// </summary>
    public Vec2 FieldCubedComaResidual(Vec2 h)
    {
        Vec2 hn = h - M331M.a;
        return (hn.Squared + M331M.b2) * hn.Conjugate;
    }

    /// <summary>
    /// Build the fifth-order field from the per-surface wave coefficients and sigma vectors.
    /// </summary>
    /// <param name="perSurface">Surface j's wave front coefficients, from <see cref="WaveFront"/>.</param>
    /// <param name="thirdW131">Surface j's third-order coma coefficient.</param>
    /// <param name="sigma">Surface j's sigma vector.</param>
    /// <param name="count">Number of surfaces addressed by the callbacks.</param>
    /// <param name="trefoilOverlay">
    /// Surface j's Zernike trefoil overlay as the vector <c>FF C^3_333,j</c> of
    /// <see cref="Conventions.TrefoilOverlay"/>, ALREADY IN THE SAME UNITS as the coefficients
    /// <paramref name="perSurface"/> supplies. Null when no surface is figured.
    ///
    /// <para>Table 2 of Fuerschbach 2014 says where it lands, and it lands in two places:</para>
    /// <code>
    ///     C^3_333 = ALIGN C^3_333 - sum_j FF C^3_333,j
    ///     C^3_422 = ALIGN C^3_422 - (3/2) sum_j (ybar_j / y_j) FF C^3_333,j
    /// </code>
    /// <para>The first is field constant and is there wherever the surface sits. The second is
    /// the field-linear astigmatism a trefoil surface generates only when it is AWAY from the
    /// stop, which is why it carries the beam displacement - at a pupil the beam does not walk
    /// and the term vanishes. It is the term Fuerschbach's Schmidt-telescope experiment was built
    /// to show, and the one a three-point mount error produces.</para>
    ///
    /// <para><b>This is why trefoil overlays needed the fifth order.</b> <c>C^3_422</c> is the
    /// cubic vector of fifth-order astigmatism, Eq. (C24); before <c>W422</c> existed there was
    /// nowhere for the second row to go.</para>
    /// </param>
    /// <param name="beamDisplacement">Surface j's <c>ybar_j / y_j</c>; zero at a pupil.</param>
    public static NatFifthOrder Compute(Func<int, Deformation> perSurface,
                                        Func<int, Scalar> thirdW131,
                                        Func<int, Vec2> sigma, int count,
                                        Func<int, Vec2>? trefoilOverlay = null,
                                        Func<int, Scalar>? beamDisplacement = null,
                                        Func<int, Vec2>? obliqueSphericalOverlay = null,
                                        Func<int, Vec2>? fifthComaOverlay = null)
    {
        if (perSurface == null) throw new ArgumentNullException(nameof(perSurface));
        if (sigma == null) throw new ArgumentNullException(nameof(sigma));

        // Eq. (B6): the medial equivalent for coma, as W220M is for field curvature.
        Scalar W331M(int j) => perSurface(j).W331 + 0.75 * perSurface(j).W333;

        // The medial combination comes from cos^2 = (1 + cos 2phi)/2, so it is the same one at
        // every order: W220M = W220 + W222/2, and likewise for the two fifth-order pairs.
        Scalar W240M(int j) => perSurface(j).W240 + 0.5 * perSurface(j).W242;
        Scalar W420M(int j) => perSurface(j).W420 + 0.5 * perSurface(j).W422;
        Scalar W220M(int j) => perSurface(j).W220M;

        var m151 = FieldMoments.Accumulate(j => perSurface(j).W151, sigma, count);
        var m331m = FieldMoments.Accumulate(W331M, sigma, count);
        var m333 = FieldMoments.Accumulate(j => perSurface(j).W333, sigma, count);
        var m240m = FieldMoments.Accumulate(W240M, sigma, count);
        var m242 = FieldMoments.Accumulate(j => perSurface(j).W242, sigma, count);
        var m420m = FieldMoments.Accumulate(W420M, sigma, count);
        var m422 = FieldMoments.Accumulate(j => perSurface(j).W422, sigma, count);

        // Each overlay is summed once, weighted by the powers of the beam displacement its table
        // calls for. ybar/y is zero at a pupil, so every row beyond the first vanishes there -
        // which is the whole distinction between a plate at the stop and one away from it.
        Vec2 Sum(Func<int, Vec2>? overlay, int power)
        {
            if (overlay == null) return Vec2.Zero;
            Vec2 total = Vec2.Zero;
            for (int j = 0; j < count; j++)
            {
                Vec2 ff = overlay(j);
                if (ff.MagnitudeSquared == 0.0) continue;
                Scalar d = beamDisplacement == null ? 0.0 : beamDisplacement(j);
                Scalar w = 1.0;
                for (int k = 0; k < power; k++) w *= d;
                total += w * ff;
            }
            return total;
        }

        // ── Table 2: trefoil, Z10/11. Both rows SUBTRACTED, onto the vector CUBES. ──────────
        if (trefoilOverlay != null)
        {
            m333 = m333.WithC3(m333.C3 - Sum(trefoilOverlay, 0));
            m422 = m422.WithC3(m422.C3 - 1.5 * Sum(trefoilOverlay, 1));
        }

        // ── Table 3: oblique spherical, Z12/13. Five rows ADDED, onto the vector SQUARES. ──
        if (obliqueSphericalOverlay != null)
        {
            Vec2 s0 = Sum(obliqueSphericalOverlay, 0);
            Vec2 s1 = Sum(obliqueSphericalOverlay, 1);
            Vec2 s2 = Sum(obliqueSphericalOverlay, 2);
            m242 = m242.WithB2(m242.B2 + s0);
            m333 = m333.WithB2(m333.B2 + (2.0 / 3.0) * s1);
            m331m = m331m.WithB2(m331m.B2 + 1.5 * s1);
            m422 = m422.WithB2(m422.B2 + s2);
            m420m = m420m.WithB2(m420m.B2 + 0.75 * s2);
        }

        // ── Table 4: fifth-order coma, Z14/15. Seven rows SUBTRACTED, onto the FIRST moments. ─
        if (fifthComaOverlay != null)
        {
            Vec2 s0 = Sum(fifthComaOverlay, 0);
            Vec2 s1 = Sum(fifthComaOverlay, 1);
            Vec2 s2 = Sum(fifthComaOverlay, 2);
            Vec2 s3 = Sum(fifthComaOverlay, 3);
            m151 = m151.WithA(m151.A - s0);
            m240m = m240m.WithA(m240m.A - 1.5 * s1);
            m242 = m242.WithA(m242.A - 2.0 * s1);
            m333 = m333.WithA(m333.A - (4.0 / 3.0) * s2);
            m331m = m331m.WithA(m331m.A - 3.0 * s2);
            m422 = m422.WithA(m422.A - 2.0 * s3);
            m420m = m420m.WithA(m420m.A - 1.5 * s3);
        }

        return new NatFifthOrder
        {
            M131 = FieldMoments.Accumulate(thirdW131, sigma, count),
            M222 = FieldMoments.Accumulate(j => perSurface(j).W222, sigma, count),
            M220M = FieldMoments.Accumulate(W220M, sigma, count),
            M151 = m151,
            M331M = m331m,
            M333 = m333,
            M240M = m240m,
            M242 = m242,
            M420M = m420m,
            M422 = m422,
            M511 = FieldMoments.Accumulate(j => perSurface(j).W511, sigma, count),
        };
    }
}
