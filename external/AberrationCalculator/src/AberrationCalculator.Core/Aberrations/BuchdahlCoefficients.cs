using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>The eighteen Buchdahl/Rimmer coefficients of one surface, or of a whole system.</summary>
public sealed class BuchdahlTerms
{
    // Third order.
    public Scalar B, F, C, Pi, E;

    // Fifth order.
    public Scalar B5, F1, F2, M1, M2, M3, N1, N2, N3, C5, Pi5, E5;

    // Seventh order. B7 is Robb's tau1, and for a long time was the only one of the
    // twenty this program had - as it was the only one any of the six programs Johnson
    // surveyed in 1972 exposed. Tau2 onward come from Buchdahl's Table I scheme and are
    // present only for systems of spherical surfaces.
    public Scalar B7;
    public Scalar Tau2, Tau3, Tau4, Tau5, Tau6, Tau7, Tau8, Tau9, Tau10;
    public Scalar Tau11, Tau12, Tau13, Tau14, Tau15, Tau16, Tau17, Tau18, Tau19, Tau20;

    /// <summary>
    /// A second distortion-like fifth-order accumulator the recursion needs but which is
    /// not itself reported. It feeds the induced corrections to B7.
    /// </summary>
    public Scalar E5b;

    /// <summary>Companion third-order sums over the chief ray, used by the induced terms.</summary>
    public Scalar Bb, Fb, Cb, Eb;

    public BuchdahlTerms Clone() => (BuchdahlTerms)MemberwiseClone();

    /// <summary>Sets tau<paramref name="k"/>, for k from 2 to 20.</summary>
    public void SetTau(int k, Scalar value)
    {
        switch (k)
        {
            case 2: Tau2 = value; break;   case 3: Tau3 = value; break;
            case 4: Tau4 = value; break;   case 5: Tau5 = value; break;
            case 6: Tau6 = value; break;   case 7: Tau7 = value; break;
            case 8: Tau8 = value; break;   case 9: Tau9 = value; break;
            case 10: Tau10 = value; break; case 11: Tau11 = value; break;
            case 12: Tau12 = value; break; case 13: Tau13 = value; break;
            case 14: Tau14 = value; break; case 15: Tau15 = value; break;
            case 16: Tau16 = value; break; case 17: Tau17 = value; break;
            case 18: Tau18 = value; break; case 19: Tau19 = value; break;
            case 20: Tau20 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(k), k, "tau index is 2..20");
        }
    }

    public Scalar this[string name] => name switch
    {
        "B" => B, "F" => F, "C" => C, "Pi" => Pi, "E" => E,
        "B5" => B5, "F1" => F1, "F2" => F2, "M1" => M1, "M2" => M2, "M3" => M3,
        "N1" => N1, "N2" => N2, "N3" => N3, "C5" => C5, "Pi5" => Pi5, "E5" => E5,
        "B7" => B7,
        "Tau2" => Tau2, "Tau3" => Tau3, "Tau4" => Tau4, "Tau5" => Tau5, "Tau6" => Tau6,
        "Tau7" => Tau7, "Tau8" => Tau8, "Tau9" => Tau9, "Tau10" => Tau10,
        "Tau11" => Tau11, "Tau12" => Tau12, "Tau13" => Tau13, "Tau14" => Tau14,
        "Tau15" => Tau15, "Tau16" => Tau16, "Tau17" => Tau17, "Tau18" => Tau18,
        "Tau19" => Tau19, "Tau20" => Tau20,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown coefficient"),
    };

    /// <summary>The names this program reports, in the order the reference tabulates them.</summary>
    public static readonly string[] Names =
        { "B", "F", "C", "Pi", "E", "B5", "F1", "F2", "M1", "M2", "M3",
          "N1", "N2", "N3", "C5", "Pi5", "E5", "B7",
          "Tau2", "Tau3", "Tau4", "Tau5", "Tau6", "Tau7", "Tau8", "Tau9", "Tau10",
          "Tau11", "Tau12", "Tau13", "Tau14", "Tau15", "Tau16", "Tau17", "Tau18",
          "Tau19", "Tau20" };
}

/// <summary>Third-, fifth- and seventh-order coefficients for a system.</summary>
public sealed class BuchdahlResult
{
    /// <summary>
    /// Per-surface INTRINSIC contributions: what the surface generates on its own, before
    /// its aspheric figuring and before the corrections induced by the surfaces ahead of it.
    /// Unscaled.
    /// </summary>
    public BuchdahlTerms[] Intrinsic { get; init; } = Array.Empty<BuchdahlTerms>();

    /// <summary>
    /// Per-surface ASPHERIC contributions, isolated. Null entries are spherical surfaces.
    /// Unscaled.
    /// </summary>
    public BuchdahlTerms?[] Aspheric { get; init; } = Array.Empty<BuchdahlTerms?>();

    /// <summary>
    /// System totals: intrinsic plus aspheric plus induced, multiplied by the F/number so
    /// that they are transverse aberration coefficients.
    ///
    /// These are NOT the sum of <see cref="Intrinsic"/>. Each surface's fifth- and
    /// seventh-order terms pick up corrections induced by the third-order aberration
    /// already present when light reaches it, and those corrections are part of the total.
    /// </summary>
    public BuchdahlTerms Totals { get; init; } = new();

    /// <summary>
    /// Per-surface INDUCED contributions: what the surface generates by acting on the
    /// aberration already present in the beam when light reaches it. Zero at the first
    /// surface, since nothing precedes it.
    ///
    /// This is the part a designer cannot see any other way. A surface can be blameless in
    /// isolation and still be a large contributor because of what it does to the aberration
    /// handed to it - and the fix for that is usually upstream.
    /// </summary>
    public BuchdahlTerms[] Induced { get; init; } = Array.Empty<BuchdahlTerms>();

    /// <summary>
    /// Per-surface TOTAL, unscaled. Summing these over the surfaces and multiplying by the
    /// F/number reproduces <see cref="Totals"/> exactly.
    ///
    /// <para>Through B7 each is intrinsic + aspheric + induced. Tau2..tau20 are the surface's
    /// share of the tertiary scheme's totals, set by <see cref="TertiaryCoefficients.Attach"/>;
    /// they are NOT split into those three parts, which stay zero for them. They are zero
    /// where <see cref="TertiaryUnattributed"/> is set.</para>
    /// </summary>
    public BuchdahlTerms[] PerSurface { get; init; } = Array.Empty<BuchdahlTerms>();

    /// <summary>
    /// True when tau2..tau20 have system totals but could not be split by surface: a figured flat
    /// facing collimated light, where a surface's own share has no finite value and only the sum
    /// does. The per-surface tau are then zero, and do not sum to the totals.
    /// </summary>
    public bool TertiaryUnattributed { get; set; }

    /// <summary>Image-space F/number, the factor applied to the totals.</summary>
    public Scalar FNumber { get; init; }

    /// <summary>The optical invariant used throughout.</summary>
    public Scalar Lagrange { get; init; }

    /// <summary>
    /// True for a lens with no field. Only the spherical series B, B5, B7 is then non-zero;
    /// every field-dependent coefficient is exactly zero, set rather than computed.
    /// </summary>
    public bool OnAxisOnly { get; init; }
}

/// <summary>
/// Buchdahl/Rimmer third-, fifth- and seventh-order aberration coefficients for a centred
/// system of spherical and even-aspheric surfaces.
///
/// The coefficients are Buchdahl's (<i>Optical Aberration Coefficients</i>, Oxford, 1954;
/// Dover reprint 1968) in the notation M. Rimmer recast them into (M.S. Thesis, Institute
/// of Optics, University of Rochester, 1963), which is why they are usually called the
/// Buchdahl-Rimmer coefficients.
///
/// This computation follows that method as realised in the widely circulated FIFTHORD macro by
/// M. MacFarlane (1998), with the mirror index-sign correction of T. A. Mitchell (2003)
/// and the Lagrange-invariant correction of J. Sasian (2019). See docs/references.md for
/// the full chain and for what the macro did and did not contribute.
/// The macro's variable names are kept (pai, x73, aS1p1, j0a) so this stays reviewable
/// line-by-line against the reference rather than being a paraphrase of it.
///
/// Three things about this routine surprise people, and all three are deliberate:
///
/// 1. <b>The totals are not the sum of the per-surface parts.</b> A surface bends a beam
///    that already carries the aberration of everything ahead of it, and that interaction
///    produces induced higher-order terms. They are folded in per surface, using running
///    sums of the third-order coefficients accumulated so far.
///
/// 2. <b>Per-surface values are unscaled; the totals carry the F/number.</b> The totals are
///    transverse aberration coefficients, which is the macro's convention.
///
/// 3. <b>The r-squared aspheric term is a curvature change, not figuring.</b> The treatment
///    has no second-order deformation coefficient and does not need one: expanding the sag
///    gives z = (c/2 + A2) r^2 + ..., so such a surface is exactly the sphere of curvature
///    c + 2 A2 carrying the leftover figuring. <c>Surface.VertexForm</c> makes that swap and
///    everything downstream sees an ordinary asphere. It used to be reported as unhandled.
/// </summary>
public static class BuchdahlCoefficients
{
    private const double Eps = 1e-10;

    /// <summary>
    /// Computes the coefficients at one wavelength.
    /// </summary>
    /// <param name="system">The lens.</param>
    /// <param name="p">
    /// The paraxial trace at that wavelength, whose marginal ray must be traced at the full
    /// pupil and chief ray at the full field - the coefficients are normalised to those.
    /// </param>
/// <param name="ignoredR2Surfaces">
    /// Retained so callers need not change; always left empty. The r-squared term is now
    /// folded into the vertex curvature rather than being reported as unrepresentable.
    /// </param>
    public static BuchdahlResult Compute(OpticalSystem system, ParaxialResult p,
                                         System.Collections.Generic.List<int>? ignoredR2Surfaces = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (p == null) throw new ArgumentNullException(nameof(p));

        if (ChiefRayIsZero(p))
            return OnAxisOnly(system, p, ignoredR2Surfaces);

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();

        // The macro takes the F/number from the raw index, WITHOUT the mirror sign flip it
        // applies inside the loop. On a system with an odd number of mirrors the signed
        // index would flip the F/number, and with it every total.
        Scalar fnum = -0.5 / SMath.Abs(p.N[last]) / p.U[last];
        Scalar lagrange = p.LagrangeInvariant;

        var intrinsic = new BuchdahlTerms[count];
        var aspheric = new BuchdahlTerms?[count];
        var induced = new BuchdahlTerms[count];
        var perSurface = new BuchdahlTerms[count];
        for (int i = 0; i < count; i++)
        {
            intrinsic[i] = new BuchdahlTerms();
            induced[i] = new BuchdahlTerms();
            perSurface[i] = new BuchdahlTerms();
        }

        // Running sums over the surfaces already passed. The induced corrections at surface
        // i are functions of these, so they must be updated only AFTER surface i is done.
        var acc = new BuchdahlTerms();

        for (int i = 1; i <= last; i++)
        {
            var surf = system.Surfaces[i];

            Scalar cv = surf.VertexCurvature;
            Scalar index = p.N[i - 1];        // already sign-flipped for mirror spaces
            Scalar indexp = p.N[i];
            if (SMath.Abs(index) < 1e-15 || SMath.Abs(indexp) < 1e-15) continue;

            Scalar k = index / indexp;
            Scalar km1 = k - 1.0;
            Scalar deln = index - indexp;

            Scalar py = p.Y[i], pu = p.U[i - 1], pup = p.U[i];
            Scalar pai = cv * py + pu;
            Scalar paip = pai * k;

            Scalar pyc = p.Ybar[i], puc = p.Ubar[i - 1], pucp = p.Ubar[i];
            Scalar paic = cv * pyc + puc;
            Scalar paicp = paic * k;

            // ── Third order ──────────────────────────────────────────────────────────
            Scalar P = cv * km1 * lagrange * lagrange / index;
            Scalar Sa = index * km1 * py * (pai + pup);
            Scalar Sb = index * km1 * pyc * (paic + pucp);

            Scalar Ba = Sa * pai * pai;
            Scalar Fa = Sa * pai * paic;
            Scalar Ca = Sa * paic * paic;
            Scalar Bb = Sb * paic * paic;
            Scalar Fb = Sb * pai * paic;
            Scalar Cb = Sb * pai * pai;
            Scalar Ea = Fb + lagrange * km1 * paic * (pucp + puc);
            Scalar Eb = Fa - lagrange * km1 * pai * (pup + pu);

            // ── Fifth order, intrinsic ───────────────────────────────────────────────
            Scalar w = (pai * pai + paip * paip + pup * pup - 3 * pu * pu) / 8.0;
            Scalar x73 = 3 * pai * paip + 2 * pup * pup - 3 * pu * pu;
            Scalar x74 = 3 * pai * paicp + 2 * pup * pucp - 3 * pu * puc;
            Scalar x75 = 3 * paic * paicp + 2 * pucp * pucp - 3 * puc * puc;
            Scalar x76 = pai * (3 * pu - pup);
            Scalar x77 = paic * (2 * pu - pup) + pai * puc;
            Scalar x78 = paic * (3 * puc - pucp);
            Scalar x42 = pyc * pai * (paic - puc) + py * paic * (pucp + puc);
            Scalar x82 = pyc * pu * (paic - puc) - py * paicp * (pucp + puc);
            Scalar x42b = py * paic * (pai - pu) + pyc * pai * (pup + pu);
            Scalar x82b = py * puc * (pai - pu) - pyc * paip * (pup + pu);

            Scalar S1p = 3 * w * Sa * pai;
            Scalar S2p = Sa * (paic * x73 + pai * x74 - pucp * x76 - pup * x77) / 4.0;
            Scalar S3p = index * km1 * (x42 * x73 + x76 * x82
                                        + py * (pai + pup) * (pai * x75 - pup * x78)) / 4.0;
            Scalar S4p = Sa * (paic * x74 - pucp * x77);
            Scalar S5p = index * km1 * (x42 * x74 + x77 * x82
                                        + py * (pai + pup) * (paic * x75 - pucp * x78)) / 4.0;
            Scalar S6p = index * km1 * (x42 * x75 + x78 * x82) / 4.0;
            Scalar S1q = index * km1 * (x42b * x73 + x76 * x82b) / 4.0;
            Scalar t1p = 10 * w * w + Sa * pai * cv * (2 * pup - 5 * pu) / index / 8.0;

            Scalar B5 = pai * S1p;
            Scalar F1 = paic * S1p + pai * S2p;
            Scalar F2 = pai * S2p;
            Scalar M1 = 2 * paic * S2p;
            Scalar M2 = pai * S3p;
            Scalar M3 = pai * S4p;
            Scalar N1 = paic * S3p;
            Scalar N2 = paic * S4p + 2 * pai * S5p;
            Scalar N3 = pai * S5p;
            Scalar C5 = 0.5 * paic * S5p;
            Scalar P5 = pai * S6p - 0.5 * paic * S5p;
            Scalar E5 = paic * S6p;
            Scalar E5b = pai * S1q;
            Scalar B7 = Ba * t1p;

            // Record the intrinsic contribution before anything is added to it.
            intrinsic[i] = new BuchdahlTerms
            {
                B = Ba, F = Fa, C = Ca, Pi = P, E = Ea,
                B5 = B5, F1 = F1, F2 = F2, M1 = M1, M2 = M2, M3 = M3,
                N1 = N1, N2 = N2, N3 = N3, C5 = C5, Pi5 = P5, E5 = E5, B7 = B7,
                E5b = E5b, Bb = Bb, Fb = Fb, Cb = Cb, Eb = Eb,
            };

            // ── Aspheric figuring ────────────────────────────────────────────────────
            bool isAspheric = false;
            Scalar conic = 0.0, aterm = 0.0, bterm = 0.0, cterm = 0.0;

// Vertex form: any r-squared coefficient has already been folded into cv above, and
            // the figuring below is measured from THAT sphere. Reading the raw conic and r^4
            // against a shifted curvature would measure it from a sphere the surface does not
            // have, which shows up in B - primary spherical - and nowhere else.
            var vf = surf.VertexForm();
            bool hasR2 = !SMath.Vanishes(Coef(surf, 0));

            // The polynomial figuring the scheme consumes: r^4, r^6, r^8. It is read whenever
            // it is PRESENT, not only when the surface carries the label for it. Every reader
            // sets EvenAsphere on seeing aspheric data, so a file is safe either way, but a
            // hand-edited one or a system built through the API need not, and a surface
            // silently treated as spherical while the ray trace honours its figuring is the
            // worst kind of disagreement to debug: it presents as a scheme error of over a
            // hundred per cent, in a coefficient that is in fact computed correctly. The
            // conic already behaves this way, through the branch above, and the asymmetry
            // between the two was what made it look like a real finding.
            bool hasPolynomial = !SMath.Vanishes(Coef(surf, 1))
                              || !SMath.Vanishes(Coef(surf, 2))
                              || !SMath.Vanishes(Coef(surf, 3));

            if (!SMath.Vanishes(surf.Conic, Eps))
            {
                conic = vf.Conic;
                isAspheric = true;
            }
            if (surf.Type == SurfaceType.EvenAsphere || hasR2 || hasPolynomial)
            {
                conic = vf.Conic;
                aterm = vf.A4;              // r^4
                bterm = vf.A6;              // r^6
                cterm = vf.A8;              // r^8
                isAspheric = true;
            }

            Scalar aS1p1 = 0.0;             // needed by the B7 aspheric term below
            if (isAspheric)
            {
                Scalar cv2 = cv * cv, cv3 = cv2 * cv;
                Scalar c1 = 8 * aterm + conic * cv3;
                Scalar c2 = 12 * bterm + 0.75 * cv2 * (cv3 * conic * (conic + 2) - 2 * c1);
                Scalar temp = cv3 * conic * (conic * conic + 3 * conic + 3) - 3 * c1;
                temp = cv2 * (5 * cv2 * temp - 12 * c2);
                temp = (-6 * cv * c1 * c1 + temp) / 8.0;
                Scalar c3 = 16 * cterm + temp;

                Scalar c1b = deln * c1, c2b = deln * c2, c3b = deln * c3;
                Scalar pysq = py * py, pycsq = pyc * pyc;

                Scalar aBa = c1b * pysq * pysq;
                Scalar aFa = c1b * pysq * py * pyc;
                Scalar aEb = aFa;
                Scalar aCa = c1b * pysq * pycsq;
                Scalar aCb = aCa;
                Scalar aEa = c1b * py * pycsq * pyc;
                Scalar aFb = aEa;
                Scalar aBb = c1b * pycsq * pycsq;

                Ba += aBa; Fa += aFa; Ca += aCa; Ea += aEa;
                Bb += aBb; Fb += aFb; Cb += aCb; Eb += aEb;

                Scalar mm = k * lagrange / indexp;
                Scalar la = (3 * paip - 2 * (1 - 2 * k) * pup) / 4.0;
                Scalar lb = (3 * paicp - 2 * (1 - 2 * k) * pucp) / 4.0;

                aS1p1 = aBa * la;
                Scalar aS2p1 = 2 * aFa * la + 0.5 * c1b * pysq * py * mm;
                Scalar aS3p1 = 2 * aCa * la + c1b * pysq * pyc * mm;
                Scalar aS4p1 = 2 * aS3p1;
                Scalar aS5p1 = 2 * aEa * la + 1.5 * c1b * py * pycsq * mm;
                Scalar aS6p1 = aBb * la + c1b * pycsq * pyc * mm;
                Scalar aS1q1 = aBa * lb - c1b * pysq * py * mm;

                Scalar j0a = c2b * py - 0.25 * cv * c1b * (3 * paip - 5 * pup);
                Scalar j0b = c2b * pyc - 0.25 * cv * c1b * (3 * paicp - 5 * pucp);

                Scalar alpha = 0.5 * (pup * (pup - pai) + pai * (3 * paip - pup));
                Scalar beta = pup * (pucp - paic) + pai * (3 * paicp - pucp);
                Scalar gamma = 0.5 * (pucp * (pucp - paic) + paic * (3 * paicp - pucp));

                Scalar lambda = alpha * c1b + j0a * py;
                Scalar mu = beta * c1b + 2 * j0a * pyc;
                Scalar nu = py * gamma * c1b + j0a * pycsq;

                Scalar aS1p2 = pysq * py * lambda;
                Scalar aS2p2 = pysq * pyc * lambda + 0.5 * pysq * py * mu;
                Scalar aS3p2 = py * pycsq * lambda + pysq * nu;
                Scalar aS4p2 = 2 * pysq * pyc * mu;
                Scalar aS5p2 = 0.5 * py * pycsq * mu + py * pyc * nu;
                Scalar aS6p2 = pycsq * nu;
                Scalar aS1q2 = pysq * (alpha * c1b * pyc + j0b * pysq);

                Scalar aS1pa = pai * aS1p1 + py * aS1p2;
                Scalar aS2pa = pai * aS2p1 + py * aS2p2;
                Scalar aS3pa = pai * aS3p1 + py * aS3p2;
                Scalar aS4pa = pai * aS4p1 + py * aS4p2;
                Scalar aS5pa = pai * aS5p1 + py * aS5p2;
                Scalar aS6pa = pai * aS6p1 + py * aS6p2;

                Scalar aS1pb = paic * aS1p1 + pyc * aS1p2;
                Scalar aS2pb = paic * aS2p1 + pyc * aS2p2;
                Scalar aS3pb = paic * aS3p1 + pyc * aS3p2;
                Scalar aS4pb = paic * aS4p1 + pyc * aS4p2;
                Scalar aS5pb = paic * aS5p1 + pyc * aS5p2;
                Scalar aS6pb = paic * aS6p1 + pyc * aS6p2;

                Scalar aS1qa = pai * aS1q1 + py * aS1q2;

                Scalar aB5 = aS1pa;
                Scalar aF1 = aS1pb + aS2pa;
                Scalar aF2 = aS2pa;
                Scalar aM1 = 2 * aS2pb;
                Scalar aM2 = aS3pa;
                Scalar aM3 = aS4pa;
                Scalar aN1 = aS3pb;
                Scalar aN2 = aS4pb + 2 * aS5pa;
                Scalar aN3 = aS5pa;
                Scalar aC5 = 0.5 * aS5pb;
                Scalar aP5 = aS6pa - 0.5 * aS5pb;
                Scalar aE5 = aS6pb;
                Scalar aE5b = aS1qa;

                B5 += aB5; F1 += aF1; F2 += aF2; M1 += aM1; M2 += aM2; M3 += aM3;
                N1 += aN1; N2 += aN2; N3 += aN3; C5 += aC5; P5 += aP5; E5 += aE5;
                E5b += aE5b;

                Scalar gamma1 = c1 * pysq;
                Scalar gamma2 = py * pysq * (c2 * py + 0.25 * cv * c1 * (pai + 3 * pu));
                Scalar t = cv2 * c1 * (pai * (pai + 5 * pu) - pu * (pai - 5 * pu)) / 8.0;
                // HALF what (77.2) prints for both c1^2 terms, and deliberately so. A
                // parabola images infinity perfectly, so its spherical aberration must
                // vanish at every order; it does with these values and does not with the
                // printed ones. See ParabolicMirrorTests.
                t += c1 * c1 * py * pai / 4.0;
                Scalar gamma3 = pysq * pysq * (t + c3 * pysq
                                 + (1.0 / 3.0) * cv * c2 * py * (pai + 5 * pu));

                Scalar d3 = cv * py * (4 * cv * py * (pai + pu)
                            + paip * (5 * (2 * pup + pai) + paip));
                Scalar t2 = k * pup * (3 * pai * pai - 10 * pu * pu
                            + paip * (4 * (2 * pup + paip + pai) + pai));
                d3 = c1 * (gamma1 * py * (1 + 2 * k * km1) + d3 + t2) / 8.0;
                d3 = deln * pysq * pysq * ((1.0 / 6.0) * c2 * pysq
                            * (4 * paip + 3 * pup * (2 * k - 1)) + d3);

                Scalar L3 = py * gamma3 * deln + 0.5 * (gamma1 * (S1p + aS1p1) + gamma2 * Sa * pai);
                Scalar aB7 = pai * d3 + py * L3;
                B7 += aB7;

                aspheric[i] = new BuchdahlTerms
                {
                    B = aBa, F = aFa, C = aCa, Pi = 0.0, E = aEa,
                    B5 = aB5, F1 = aF1, F2 = aF2, M1 = aM1, M2 = aM2, M3 = aM3,
                    N1 = aN1, N2 = aN2, N3 = aN3, C5 = aC5, Pi5 = aP5, E5 = aE5, B7 = aB7,
                    E5b = aE5b, Bb = aBb, Fb = aFb, Cb = aCb, Eb = aEb,
                };
            }

            // ── Induced corrections from everything ahead of this surface ────────────
            // Snapshot first, so the induced part can be reported separately from what the
            // surface would have produced on its own.
            Scalar p0B5 = B5, p0F1 = F1, p0F2 = F2, p0M1 = M1, p0M2 = M2, p0M3 = M3;
            Scalar p0N1 = N1, p0N2 = N2, p0N3 = N3, p0C5 = C5, p0P5 = P5, p0E5 = E5, p0B7 = B7;

            Scalar L = lagrange;
            Scalar tmp;

            tmp = 0.5 * acc.B * acc.B * (P + 3 * Ca) / L;
            tmp += 3 * (acc.B5 - acc.B * acc.Eb / L) * Fa;
            tmp += 3 * (0.5 * acc.Eb * acc.Eb / L - acc.E5b) * Ba;
            tmp = (tmp + acc.B * (F1 + F2) - 5 * acc.Eb * B5) / (2 * L);
            B7 += tmp;

            B5 += 1.5 * (acc.B * Fa - acc.Eb * Ba) / L;

            tmp = acc.B * (P + 4 * Ca) + (5 * acc.F - 4 * acc.Eb) * Fa
                  - (2 * acc.Pi + 5 * acc.Cb) * Ba;
            F1 += tmp / (2 * L);

            tmp = acc.B * (P + 2 * Ca) + 2 * (2 * acc.F - acc.Eb) * Fa
                  - (acc.Pi + 4 * acc.Cb) * Ba;
            F2 += tmp / (2 * L);

            tmp = acc.B * Ea + (4 * acc.F - acc.Eb) * Ca - acc.Fb * Ba;
            tmp += (acc.C - 4 * acc.Cb - 2 * acc.Pi) * Fa;
            M1 += tmp / L;

            tmp = acc.B * Ea + (2 * acc.F - acc.Eb) * (P + Ca)
                  + (3 * acc.C - 2 * acc.Cb + acc.Pi) * Fa - 3 * acc.Fb * Ba;
            M2 += tmp / (2 * L);

            tmp = acc.F * (P + 2 * Ca) - acc.Fb * Ba + (acc.C - 2 * acc.Cb) * Fa;
            M3 += 2 * tmp / L;

            tmp = 3 * acc.F * Ea - (acc.Pi + acc.Cb) * (P + Ca) + 2 * (acc.C - acc.Cb) * Ca;
            tmp += (acc.E - 2 * acc.Fb) * Fa - acc.Bb * Ba;
            N1 += tmp / (2 * L);

            tmp = 3 * acc.F * Ea + (acc.Pi - acc.Cb + 3 * acc.C) * (P + 3 * Ca);
            tmp -= acc.Bb * Ba;
            tmp -= (acc.Pi + acc.C) * Ca;
            tmp += (acc.E - 8 * acc.Fb) * Fa;
            N2 += tmp / L;

            tmp = acc.F * Ea + (3 * acc.C + acc.Pi - acc.Cb) * (P + Ca) - acc.Bb * Ba;
            tmp += (acc.Pi + acc.C) * Ca + (acc.E - 4 * acc.Fb) * Fa;
            N3 += tmp / (2 * L);

            tmp = (4 * acc.C + acc.Pi) * Ea - acc.Fb * P + 2 * (acc.E - 2 * acc.Fb) * Ca;
            tmp -= 2 * acc.Bb * Fa;
            C5 += tmp / (4 * L);

            tmp = (acc.Pi - 2 * acc.C) * Ea + (4 * acc.E - acc.Fb) * P;
            tmp -= 2 * acc.Bb * Fa;
            tmp += 2 * (acc.E + acc.Fb) * Ca;
            P5 += tmp / (4 * L);

            E5 += (3 * acc.E * Ea - acc.Bb * (P + 3 * Ca)) / (2 * L);
            E5b += (acc.B * (P + 3 * Cb) - 3 * acc.Eb * Eb) / (2 * L);

            induced[i] = new BuchdahlTerms
            {
                B5 = B5 - p0B5, F1 = F1 - p0F1, F2 = F2 - p0F2,
                M1 = M1 - p0M1, M2 = M2 - p0M2, M3 = M3 - p0M3,
                N1 = N1 - p0N1, N2 = N2 - p0N2, N3 = N3 - p0N3,
                C5 = C5 - p0C5, Pi5 = P5 - p0P5, E5 = E5 - p0E5, B7 = B7 - p0B7,
            };

            perSurface[i] = new BuchdahlTerms
            {
                B = Ba, F = Fa, C = Ca, Pi = P, E = Ea,
                B5 = B5, F1 = F1, F2 = F2, M1 = M1, M2 = M2, M3 = M3,
                N1 = N1, N2 = N2, N3 = N3, C5 = C5, Pi5 = P5, E5 = E5, B7 = B7,
                E5b = E5b, Bb = Bb, Fb = Fb, Cb = Cb, Eb = Eb,
            };

            // ── Accumulate, now that this surface's induced terms are done ───────────
            acc.B += Ba; acc.F += Fa; acc.C += Ca; acc.E += Ea; acc.Pi += P;
            acc.Bb += Bb; acc.Fb += Fb; acc.Cb += Cb; acc.Eb += Eb;
            acc.B5 += B5; acc.F1 += F1; acc.F2 += F2;
            acc.M1 += M1; acc.M2 += M2; acc.M3 += M3;
            acc.N1 += N1; acc.N2 += N2; acc.N3 += N3;
            acc.C5 += C5; acc.Pi5 += P5; acc.E5 += E5; acc.E5b += E5b;
            acc.B7 += B7;
        }

        // Totals are transverse coefficients: unconverted sums times the F/number.
        var totals = new BuchdahlTerms
        {
            B = acc.B * fnum, F = acc.F * fnum, C = acc.C * fnum,
            Pi = acc.Pi * fnum, E = acc.E * fnum,
            B5 = acc.B5 * fnum, F1 = acc.F1 * fnum, F2 = acc.F2 * fnum,
            M1 = acc.M1 * fnum, M2 = acc.M2 * fnum, M3 = acc.M3 * fnum,
            N1 = acc.N1 * fnum, N2 = acc.N2 * fnum, N3 = acc.N3 * fnum,
            C5 = acc.C5 * fnum, Pi5 = acc.Pi5 * fnum, E5 = acc.E5 * fnum,
            B7 = acc.B7 * fnum,
            E5b = acc.E5b * fnum,
            Bb = acc.Bb * fnum, Fb = acc.Fb * fnum, Cb = acc.Cb * fnum, Eb = acc.Eb * fnum,
        };

        return new BuchdahlResult
        {
            Intrinsic = intrinsic,
            Aspheric = aspheric,
            Induced = induced,
            PerSurface = perSurface,
            Totals = totals,
            FNumber = fnum,
            Lagrange = lagrange,
        };
    }

    private static bool ChiefRayIsZero(ParaxialResult p)
    {
        for (int i = 0; i < p.Ybar.Length; i++)
            if (p.Ybar[i] != 0.0) return false;
        for (int i = 0; i < p.Ubar.Length; i++)
            if (p.Ubar[i] != 0.0) return false;
        return true;
    }

    /// <summary>
    /// A lens with no field: the chief ray is zero everywhere, and so is the Lagrange invariant
    /// the induced fifth- and seventh-order terms divide by. Evaluated as they stand those
    /// terms are 0/0, and NaN spread into the B5 and B7 totals.
    ///
    /// <para>Both limits are exact and neither needs a field. Every field-dependent
    /// coefficient is zero: there is no field for it to multiply. The spherical series B, B5,
    /// B7 does not depend on the chief ray at all - in each induced term the chief ray enters
    /// the numerator and the invariant to the same power, so its size cancels. So those three
    /// are computed with the ray that crosses the axis at the stop, scaled to a Lagrange invariant
    /// of one - a normalisation, not a field: no angle or height is invented - and everything
    /// else is set to zero rather than evaluated.</para>
    ///
    /// <para>This is deliberately not OSLO's way. OSLO, given a field of zero, traces a chief ray
    /// of slope 1e-6 instead, which gets the same B, B5, B7 but reports its field-dependent
    /// coefficients at a field the lens does not have - numbers such as F = 4e-8 or
    /// tau20 = -2e-43 that look like results and are not. Nothing small is traced here.</para>
    /// </summary>
    private static BuchdahlResult OnAxisOnly(OpticalSystem system, ParaxialResult p,
                                             System.Collections.Generic.List<int>? ignoredR2Surfaces)
    {
        // The marginal ray stays the caller's; only the chief ray is supplied.
        var chief = ParaxialTrace.UnitInvariantChiefRay(system, p.N, p.Y, p.U);
        if (chief == null)
            throw new InvalidOperationException(
                "This lens has no field, and its marginal ray crosses the axis at the stop - it has "
                + "no aperture - so there is no Lagrange invariant to form the fifth- and "
                + "seventh-order coefficients with. Check the aperture.");
        var withChief = new ParaxialResult
        {
            Y = p.Y, U = p.U, N = p.N,
            Ybar = chief.Value.Ybar, Ubar = chief.Value.Ubar,
            LagrangeInvariant = 1.0,
        };
        var r = Compute(system, withChief, ignoredR2Surfaces);

        foreach (var t in r.Intrinsic) KeepSpherical(t);
        foreach (var t in r.Aspheric) if (t != null) KeepSpherical(t);
        foreach (var t in r.Induced) KeepSpherical(t);
        foreach (var t in r.PerSurface) KeepSpherical(t);
        KeepSpherical(r.Totals);

        return new BuchdahlResult
        {
            Intrinsic = r.Intrinsic,
            Aspheric = r.Aspheric,
            Induced = r.Induced,
            PerSurface = r.PerSurface,
            Totals = r.Totals,
            FNumber = r.FNumber,
            Lagrange = p.LagrangeInvariant,
            OnAxisOnly = true,
        };
    }

    /// <summary>Zeroes every coefficient that multiplies a power of the field, and the chief-ray sums.</summary>
    private static void KeepSpherical(BuchdahlTerms t)
    {
        t.F = 0.0; t.C = 0.0; t.Pi = 0.0; t.E = 0.0;
        t.F1 = 0.0; t.F2 = 0.0; t.M1 = 0.0; t.M2 = 0.0; t.M3 = 0.0;
        t.N1 = 0.0; t.N2 = 0.0; t.N3 = 0.0; t.C5 = 0.0; t.Pi5 = 0.0; t.E5 = 0.0;
        for (int k = 2; k <= 20; k++) t.SetTau(k, 0.0);
        t.E5b = 0.0; t.Bb = 0.0; t.Fb = 0.0; t.Cb = 0.0; t.Eb = 0.0;
    }

    private static Scalar Coef(Surface s, int index) =>
        index >= 0 && index < s.AsphericCoefficients.Length ? s.AsphericCoefficients[index] : 0.0;
}
