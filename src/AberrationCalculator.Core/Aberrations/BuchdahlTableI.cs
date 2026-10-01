using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The quantities Buchdahl's computing scheme produces for one surface.
///
/// Named after the paper rather than after what they mean, because most of them do not
/// mean anything on their own - they are intermediate products of a long chain. The ones
/// that do have a meaning are named for it.
/// </summary>
public sealed class BuchdahlTableIRow
{
    /// <summary>The scheme's numbered entries, indexed as Buchdahl numbers them, t1..t155.</summary>
    public readonly Scalar[] T = new Scalar[Count];

    /// <summary>
    /// The primed ray angles at this surface - Buchdahl's trailing asterisk. Kept because
    /// later entries reference them (t99 uses t9*, t100 uses t81*) and recomputing would
    /// mean carrying the index around a second time.
    /// </summary>
    public Scalar VpPrime, VqPrime;

    /// <summary>
    /// The primary coefficient WITHOUT any aspheric figuring. The scheme's own quantities -
    /// w1, w2 and the intrinsic secondary - are built from this rather than from t10, so
    /// that the figuring enters once, as its own increment, and not twice.
    /// </summary>
    public Scalar ApSpherical;

    /// <summary>The figuring's share of the primary, and the height ratio it travels on.</summary>
    public Scalar ApFigured, Rho;

    /// <summary>
    /// The figuring's share of the barred primary entry t14, and of the sums t19 and t24
    /// built from it. t24 is a RECURSION, carrying the previous surface's ratio, so its two
    /// halves have to be accumulated separately or the figuring travels on q.
    /// </summary>
    public Scalar C14Figured, T19Figured, T24Figured;

    /// <summary>The figured half of this surface's six BARRED secondary coefficients - t42,
    /// t47, t53, t57, t63 and t68 - as <see cref="BuchdahlTableI"/> formed them, already
    /// carried on this surface's height ratio. The dagger recursions of the surface AFTER
    /// this one need it; see the note there.</summary>
    public readonly Scalar[] SecBarFig = new Scalar[6];

    /// <summary>The LIFT half of <see cref="SecBarFig"/>: the part of the figured barred
    /// secondary that is this surface height ratio applied once to its own unbarred figured
    /// secondary. The remainder is the induced bracket in d6..d8, every term of which carries
    /// the figured primary and so vanishes with c1.</summary>
    public readonly Scalar[] SecBarFigLift = new Scalar[6];

    /// <summary>
    /// The D half of the figured secondary increment, which the barred entry carries on the
    /// INCIDENCE ratio while the L half goes on the height ratio - (60.3) giving the figuring
    /// two halves like everything else.
    ///
    /// <para>Recorded, not used: it is already added into <see cref="SecBarFig"/> where it is
    /// formed, and nothing here reads it back. It is kept so that the identity it completes can
    /// be checked - <c>s-_1p^fig = q D + q~ L + alpha(2q~ A_(Y) + A-_(Y))</c> - which is M
    /// (68.8) written out, and which cannot be tested from the row without it. On a spherical
    /// system it is never even computed.</para>
    /// </summary>
    public readonly Scalar[] SecDFigured = new Scalar[6];

    /// <summary>
    /// The surface's OWN quantities split into the two halves of M (65.7), which the Sec. 85
    /// two-pass needs: the hat half rides i_p, the check half y_p. Spherical content is
    /// entirely hat, a sphere having no check half at all.
    /// </summary>
    public Scalar C13Spherical, C13Figured;

    /// <summary>
    /// q times the spherical a_p, formed as <c>g i i_q</c> rather than as a product with q, so
    /// that it survives a surface where the marginal incidence vanishes and q does not exist.
    /// </summary>
    public Scalar QApSpherical;

    /// <summary>
    /// <c>c/i</c> and the q-ray incidence, kept so the secondary block can rebuild the reduced
    /// forms of Sec. 84(e) without recomputing the ray data.
    /// </summary>
    public Scalar COverI, Iq, Q2ApSpherical, QC13Spherical, QOmega, Lagrange, KRatio;

    /// <summary>
    /// <c>q</c> times the increment in t23 across this surface, and the index before it. The
    /// tertiary dagger recursion needs both, and the first is not recoverable from q where q
    /// does not exist.
    /// </summary>
    public Scalar QDelta23, NBefore;

    /// <summary>
    /// <c>q t152</c>, carried on the incidences. The barred tenth tertiary is
    /// <c>q t155 + induced</c>, and on a flat surface facing collimated space that q is
    /// infinite while t155 vanishes with the incidence. Set only where that happens.
    /// </summary>
    public Scalar QT152;

    /// <summary>True where the marginal ray meets this surface at zero incidence with zero
    /// angle - a flat face turned toward collimated space, where q is infinite.</summary>
    public bool FlatInCollimatedSpace;
    public readonly Scalar[] SecSph = new Scalar[7], SecFig = new Scalar[7];
    public readonly Scalar[] MSph = new Scalar[6], MFig = new Scalar[6];
    public readonly Scalar[] ZHat = new Scalar[11], ZCheck = new Scalar[11];

    /// <summary>
    /// The (Y) family of M (85.1): the SAME p and q quantities as <see cref="T"/>, combined on
    /// the height ratio rho instead of the incidence ratio q. Buchdahl is explicit that the two
    /// are computed alongside each other and never substituted - only which ratio joins them
    /// changes. Indices match T: 25..33 at secondary order, 101..120 at tertiary.
    /// </summary>
    public readonly Scalar[] Y = new Scalar[156];

    /// <summary>
    /// The TOTAL tertiary coefficients for this surface, index 1..10 - intrinsic plus what
    /// is inherited from upstream. Their sums over the system are Buchdahl's T1..T10, which
    /// Table II of the same paper converts into Robb's tau.
    ///
    /// Kept separately from <see cref="T"/> because Buchdahl numbers the unbarred ones but
    /// not the barred ones, so there is no entry number to index the latter by.
    /// </summary>
    public readonly Scalar[] TertiaryTotal = new Scalar[11];

    /// <summary>
    /// The INTRINSIC half of this surface's tertiary - what it generates on its own, before the
    /// terms induced by the aberration already present when light reaches it.
    ///
    /// <para>Table I separates the two and always has; it simply never recorded the separation.
    /// Each total is its intrinsic plus a run of induced products, and t155 says so outright -
    /// <c>4 t19 t65 + t152 + t153 + t154</c>, with t152 the intrinsic. The ten intrinsics are
    /// t121, t134, t136, t138, t140, t143, t145, t147, t149 and t152; the induced part is
    /// whatever the total has beyond them.</para>
    ///
    /// <para>The barred partner is the intrinsic times the pass ratio, which is M (84.23)'s own
    /// rule for reading a barred entry off an unbarred one: replace the intrinsic by the ratio
    /// times itself and each intermediate by its barred form. The second of those is induced by
    /// construction, so the first is the whole of the barred intrinsic.</para>
    /// </summary>
    public readonly Scalar[] TertiaryIntrinsicTotal = new Scalar[11];

    /// <summary>The barred partner of <see cref="TertiaryIntrinsicTotal"/>. Unlike the
    /// <c>TertiaryIntrinsic(i)</c> accessor above, which reads the entries left by the final
    /// single pass and so is right only where the check half is empty, these two are summed over
    /// both passes and hold for a figured surface as well.</summary>
    public readonly Scalar[] TertiaryIntrinsicTotalBar = new Scalar[11];

    /// <summary>The barred totals, index 1..10. Their sums are Buchdahl's T1bar..T10bar.</summary>
    public readonly Scalar[] TertiaryTotalBar = new Scalar[11];

    /// <summary>Highest entry number, plus one. Table I runs to t155.</summary>
    public const int Count = 156;

    public Scalar this[int j] => T[j];

    // Convenience accessors, so downstream code need not carry entry numbers around.
    public Scalar Yp => T[1];
    public Scalar Vp => T[2];
    public Scalar Ip => T[3];
    public Scalar Yq => T[4];
    public Scalar Vq => T[5];
    public Scalar Q => T[6];
    public Scalar J => T[7];
    public Scalar Omega => T[8];

    /// <summary>Buchdahl's w - the quantity his tertiary intrinsic coefficients are built on.</summary>
    public Scalar W => T[34];

    public Scalar Phi1 => T[35];
    public Scalar Phi2 => T[36];
    public Scalar Phi3 => T[37];

    /// <summary>w1..w5 of paper II Eq. (8.3), which feed z1..z10.</summary>
    public Scalar W1 => T[43];
    public Scalar W2 => T[48];
    public Scalar W3 => T[49];
    public Scalar W4 => T[58];
    public Scalar W5 => T[64];

    /// <summary>z1..z10 of paper II Eq. (8.3). Index 1..10; index 0 unused.</summary>
    public Scalar Z(int i) => T[120 + i];

    /// <summary>
    /// The ten INTRINSIC tertiary coefficients, t1p..t10p - what this surface generates on
    /// its own, before anything inherited from upstream. Index 1..10.
    /// </summary>
    public Scalar TertiaryIntrinsic(int i) => i switch
    {
        1 => T[121], 2 => T[134], 3 => T[136], 4 => T[138], 5 => T[140],
        6 => T[143], 7 => T[145], 8 => T[147], 9 => T[149], 10 => T[152],
        _ => throw new ArgumentOutOfRangeException(nameof(i), i, "tertiary index is 1..10"),
    };
}

/// <summary>
/// Buchdahl's computing scheme for the primary, secondary AND tertiary aberration
/// coefficients - <i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958), Table I.
///
/// <para>This is the route to seventh order. <see cref="BuchdahlScheme"/> reaches the same
/// primary and secondary coefficients by a different path, but stops there; the tertiary
/// coefficients need the intermediate quantities that only this scheme produces - w, the
/// three phi, and w1..w5.</para>
///
/// <para><b>Spherical surfaces only, and Sec. 85 is the way to change that.</b> Figuring is
/// handled by the general route instead - <see cref="TertiaryCubics"/> and
/// <see cref="TertiaryScriptT"/> - whose increment is applied below.
///
/// <para>Sec. 85, "Condensed iteration when the system is aspherical", is the aspheric
/// extension of THIS scheme, and it was passed over on a misreading. The remark quoted
/// against it - that extending the condensed scheme to aspherics is "of doubtful value" - is
/// about computational economy, and Sec. 85 itself supplies the exception in its opening
/// sentence: the extension "brings with it little or no advantages UNLESS tertiary or
/// higher-order coefficients are to be calculated", which is exactly what this class is for.
/// Its equations are in the monograph, Secs. 84 and 85.</para>
///
/// <para><b>The increment is now exact for the seventh-order spherical term</b>, on every
/// figuring tried - conics from -6 to +1, three decades of A4, sixth-order figuring up to a
/// 33 um aberration, and mixtures - against a closed-form conic surface. Getting there took
/// the ratio split in the barred entries below and a convention fix in Figuring.From. The
/// other nine tertiary coefficients are still NOT independently checked on an asphere: there
/// is no closed-form off-axis solution to check them against. See `docs/verification.md`.</para>
///
/// <para><b>Notation.</b> Buchdahl uses the asterisk in two senses, distinguished only by
/// which side of the symbol it sits, and confusing them produces plausible wrong numbers:
/// a leading asterisk means the value at the PRECEDING surface, a trailing one means the
/// PRIMED value at this surface, after refraction. Both appear here as explicit locals
/// (<c>prev</c> and the <c>...Prime</c> variables) rather than as array lookups, so the
/// distinction cannot be lost.</para>
///
/// <para><b>Working in units of the focal length.</b> Curvatures are multiplied by it and
/// separations divided, so the p ray starts at unit height. Buchdahl's own triplet has
/// c1 = 4.82439, which is that surface's curvature times its focal length.</para>
///
/// <para><b>His separations are indexed differently from ours.</b> Buchdahl's d for surface
/// j is the separation BEFORE it, between j-1 and j. <see cref="Models.Surface.Thickness"/>
/// is the separation after. The conversion is done once, at the top of the loop.</para>
/// </summary>
public static class BuchdahlTableI
{
    /// <summary>
    /// Runs the scheme. <paramref name="indices"/> holds the refractive index of the medium
    /// after each surface, indexed like the surfaces.
    /// </summary>
    /// <param name="stopParameter">
    /// Buchdahl's p, the entrance pupil position in units of the focal length, which is the
    /// starting height of the q ray. His own triplet has p = 0.113227.
    /// <see cref="BuchdahlScheme"/> computes it from the stop surface.
    /// </param>
    /// <param name="wCoordinates">
    /// Run the scheme in Buchdahl's <b>W coordinates</b> rather than the paracanonical ones,
    /// by applying the modified rows of paper VI Table I, p.536.
    ///
    /// <para>The two systems differ in which end the pupil variable is referenced to:
    /// paracanonical takes the ray's height on the object side, W coordinates take it at the
    /// exit pupil (VII Eq. (2.2), <c>S = e^-1 Y_E'</c>). In this scheme that shows up as a
    /// change of reference point and nothing more - every paracanonical row here carries its
    /// running sums back to the FIRST surface, and every W row carries them forward to the
    /// IMAGE SPACE, which is Buchdahl's double prime.</para>
    ///
    /// <para>It matters because the wave front coefficients of VII Eq. (6.6) are stated for W
    /// coordinates, and the fifth-order aberration coefficients differ between the two systems
    /// by up to six per cent (VI Table II). The primary coefficients do not differ at all.</para>
    /// </param>
    /// <param name="dual">
    /// Start the two paraxial rays with their data INTERCHANGED, (y_p1, v_p1) and (y_q1, v_q1),
    /// which is paper XII (6.9). By the Principle of Duality XII (6.8) the same scheme then yields
    /// the dual of every coefficient, <c>k_(mu nu)p# = kbar_(n-nu, n-mu)q</c> by XII (6.2) - its
    /// entry nominally s_1p is sbar_6q. XII Sec. 6(iii) requires the refractive indices to be
    /// passed NEGATED as well, so that g/N1 keeps the value unity the scheme is built on; every
    /// coefficient then comes out as its dual with the sign reversed. The caller does both.
    /// </param>
    public static BuchdahlTableIRow[] Compute(
        IReadOnlyList<Models.Surface> surfaces, Scalar[] indices, Scalar efl,
        Scalar stopParameter, IReadOnlyList<Scalar[]>? aspheric = null,
        bool tertiaryHatOnly = false, Scalar iota = default, bool wCoordinates = false,
        bool dual = false)
    {
        if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
        if (indices == null) throw new ArgumentNullException(nameof(indices));

        int count = surfaces.Count;
        var rows = new BuchdahlTableIRow[count];
        for (int i = 0; i < count; i++) rows[i] = new BuchdahlTableIRow();
        if (count < 3) return rows;

        Scalar scale = SMath.Abs(efl) > 1e-300 ? efl : 1.0;

        // The same system with no figuring at all, which is what the barred tertiary entries
        // below need in order to separate the two halves of what they propagate. Taking the
        // difference is the same device DCubicIncrement uses, and it costs one extra pass.
        // The reference the barred tertiary is carried against. It is NOT the unfigured
        // system: it is the fully figured system with the TERTIARY figuring reduced to its
        // hat half - the half riding the incidence ratio, M (65.7). The primary and secondary
        // figuring stay whole, because their own splits are already right: the primary rides
        // rho entire by (65.5), and the secondary D half is moved across by its own barred
        // correction a few blocks below.
        //
        // Carrying a half through a second RUN rather than differencing finished totals is the
        // point. The figured excess passes through the accumulation, where it enters the
        // induced terms bilinearly, so the split has to be made before the accumulation and
        // not after it. Differencing against the unfigured system is right only at the surface
        // where the figuring sits, and wrong at every surface after it.
        // THERE USED TO BE A SECOND RUN HERE. A full recursive Compute with tertiaryHatOnly,
        // assigned to a local that nothing ever read - the arrangement above was built to carry
        // the half through its own run instead, and the difference-against-the-unfigured-system
        // approach it belonged to was abandoned before it was finished. It cost an entire extra
        // pass of the scheme on every evaluation of every figured design, which was invisible
        // while figured designs were only ever analysed one at a time, and is not invisible now
        // that the optimiser evaluates them tens of thousands of times in a run.

        // Carried between surfaces: the primed angles, which are what the ray transfer
        // actually propagates.
        Scalar vpPrimePrev = 0.0, vqPrimePrev = 0.0;

        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            var prev = rows[i - 1].T;

            Scalar nPrev = i - 1 < indices.Length ? indices[i - 1] : 1.0;
            Scalar nCurr = i < indices.Length ? indices[i] : 1.0;
            if (SMath.Abs(nCurr) < 1e-12) nCurr = 1.0;

            Scalar k = nPrev / nCurr;
            Scalar c = surfaces[i].VertexCurvature * scale;
            Scalar d = surfaces[i - 1].Thickness / scale;   // Buchdahl's d is the gap BEFORE
            Scalar n = nPrev;

            // ── t1..t9: the p and q rays ────────────────────────────────────────────────
            // The angle is transferred first, then the height, then the invariant.
            // Buchdahl writes t2 as (1 - *k)(*t3) + *t2, which is algebraically the
            // PRIMED value of v_p at the preceding surface: expanding (1-k)(c y - v) + v
            // gives (1-k) c y + k v. Same for the q ray. So the transfer is simply
            // "carry the primed angle forward", and both are taken from the previous row.
            if (i == 1)
            {
                // M (13.4), reduced OT-coordinates. iota = 1/l_01 in focal lengths is
                // the only quantity in the whole scheme that knows where the object is;
                // at iota = 0 these are the four values that were previously hard-coded,
                // and the p ray does then enter parallel to the axis.
                Scalar gOE = 1.0 - stopParameter * iota;
                if (SMath.Abs(gOE) < 1e-12) gOE = 1.0;

                // THE PAIR MUST CARRY A LAGRANGE INVARIANT OF ONE, and in an immersed object
                // space it does not unless the q ray is scaled.
                //
                // The invariant of these four starting values is N_0 (v_q y_p - v_p y_q), which
                // works out to N_0 (1 - p iota)/g = N_0 exactly - one when object space is air
                // and N_0 when it is not. The scheme is not homogeneous in the q ray's scale:
                // a_p DIVIDES by that ray combination (see the note on the Lagrange invariant
                // below) while the field terms multiply by powers of it, so a pair whose
                // invariant is not one cannot be absorbed by any later normalisation. It comes
                // out instead as a different error in every coefficient - some tau out by
                // exactly 1/N_0, some by more, two of them changing SIGN at N_0 = 1.3.
                //
                // The comment above names the convention: M (13.4) is in REDUCED coordinates,
                // where the angle is N u, so v_q = 1 means a plain angle of 1/N_0. This
                // recurrence propagates plain angles, so the reduction has to be done here, in
                // the starting values. At N_0 = 1 it is a division by exactly 1.0 and nothing
                // moves, which is why Buchdahl's own printed triplet - in air, like every
                // published example and every fixture here - validated this for as long as it
                // stood.
                //
                // The field normalisation carries the other half: the physical chief ray's
                // plain angle is now N_0 times this rescaled q ray, so hmax is multiplied by
                // N_0 where the transverse conversion is assembled. See TertiaryCoefficients.
                //
                // ABS because the dual run of the scheme negates every index (paper XII
                // Sec. 6), and what is wanted is the medium's index, not that run's sign.
                Scalar nObject = indices.Length > 0 ? SMath.Abs(indices[0]) : 1.0;
                if (nObject < 1e-12) nObject = 1.0;

                t[1] = 1.0;                       // y_p starts at unit height
                t[2] = iota;                      // v_p: the object is at 1/iota
                t[4] = stopParameter / gOE / nObject;   // y_q at the entrance pupil, reduced
                t[5] = 1.0 / gOE / nObject;             // v_q, the same ray and the same scale
                if (dual)
                {
                    (t[1], t[4]) = (t[4], t[1]);
                    (t[2], t[5]) = (t[5], t[2]);
                }
            }
            else
            {
                t[2] = vpPrimePrev;
                t[1] = prev[1] - d * t[2];
                t[5] = vqPrimePrev;
                t[4] = prev[4] - d * t[5];
            }

            t[3] = c * t[1] - t[2];
            t[6] = SMath.Abs(t[3]) > 1e-30 ? (c * t[4] - t[5]) / t[3] : 0.0;
            t[7] = -t[2] * t[6] + t[5];
            t[8] = (k - 1.0) * c / n;
            t[9] = t[2] * t[2];

            // The primed values - Buchdahl's trailing asterisk.
            Scalar vpPrime = (1.0 - k) * c * t[1] + k * t[2];
            Scalar vqPrime = (1.0 - k) * c * t[4] + k * t[5];
            vpPrimePrev = vpPrime;
            vqPrimePrev = vqPrime;
            rows[i].VpPrime = vpPrime;
            rows[i].VqPrime = vqPrime;
            Scalar t2Star = vpPrime;
            Scalar t9Star = vpPrime * vpPrime;
            Scalar t81Star = vpPrime * vqPrime;

            // ── t10..t14: the intrinsic primary coefficients ────────────────────────────
            // a_p carries Omega/j, and BOTH vanish on a plane: Omega = (k-1)c/n has the
            // curvature as a factor, and so does j. Substituting q = (c y_q - v_q)/(c y_p - v_p)
            // into j = -v_p q + v_q gives
            //
            //     j = c (v_q y_p - v_p y_q) / i_p ,
            //
            // exactly proportional to c, the bracket being the Lagrange invariant. So c/j has
            // the finite, non-zero limit i_p/(v_q y_p - v_p y_q) and a_p is perfectly well
            // defined on a plane - which it has to be, a plane surface in a converging beam
            // contributing spherical aberration like any other. Guarding on j and returning
            // zero deleted the surface from the tertiary entirely: every coefficient of a
            // plano-convex singlet past the first was out by tens of per cent, and no
            // spherical test caught it because Buchdahl printed triplet has no flat face.
            //
            // Written through the invariant the expression is non-singular everywhere, and on
            // a curved surface it is algebraically the same quantity as before.
            Scalar lagrange = t[5] * t[1] - t[2] * t[4];
            Scalar apSpherical = SMath.Abs(lagrange) > 1e-30
                               ? 0.5 * (t2Star - t[3]) * (k - 1.0) * t[1] * t[3] * t[3] / lagrange
                               : 0.0;

            // Aspheric figuring, when supplied. The two parts run down the chain on
            // DIFFERENT ratios and have to be kept apart.
            //
            // A sphere's primary comes entirely from the D half of (60.3), whose p and q
            // components go as the incidences, so its chain ratio is q = i_q/i_p. The
            // figuring's primary comes entirely from the L half - the fifth-order code forms
            // it as c1b times y^4, y^3 y_c, y^2 y_c^2, y y_c^3, pure ray heights with no
            // incidence anywhere - so its ratio is the HEIGHT ratio y_q/y_p instead. Running
            // the figuring down the q chain gets even the sign wrong.
            Scalar apFigured = aspheric != null && i < aspheric.Count && aspheric[i] != null
                             ? aspheric[i][0] : 0.0;
            Scalar rho = SMath.Abs(t[1]) > 1e-30 ? t[4] / t[1] : 0.0;

            rows[i].ApSpherical = apSpherical;
            rows[i].ApFigured = apFigured;
            rows[i].Rho = rho;
            // M Sec. 84(e) is what makes the following possible. Buchdahl observes there that
            // the hatted quantities "appear only in the combinations i_p t_mu-p, i_q t_mu-p",
            // and gives (84.51, 52) for those PRODUCTS with no q in them; (84.54) is only the
            // condensed rewrite, reached by substituting (26.2), b_p = 2 q a_p and
            // c_p = q^2 a_p - varpi/2. The q-free form is the primary one and it is regular.
            //
            // So the powers of q are carried on the incidences instead. With
            // a_p = g i^2, g = (1/2)(v" - i)(k - 1) y / L, the family is
            //
            //     q a_p = g i i_q ,    q^2 a_p = g i_q^2 ,
            //
            // no division anywhere. On a curved surface these are the same numbers as before;
            // on a flat one in collimated space, where i = 0 and q is infinite, they are the
            // finite values the old form threw away.
            Scalar iq = c * t[4] - t[5];
            Scalar g = SMath.Abs(lagrange) > 1e-30
                     ? 0.5 * (t2Star - t[3]) * (k - 1.0) * t[1] / lagrange
                     : 0.0;

            t[10] = apSpherical + apFigured;
            t[11] = g * t[3] * iq + rho * apFigured;
            t[12] = 2.0 * (g * iq * iq + rho * rho * apFigured);
            // t13 mixes the two, so they are split again for t14. The Petzval entry t8 is
            // untouched: figuring does not move it, and the fifth-order code agrees, giving
            // an aspheric Petzval of exactly zero.
            Scalar c13Spherical = g * iq * iq - 0.5 * t[8];
            Scalar c13Figured = rho * rho * apFigured;
            t[13] = c13Spherical + c13Figured;
            // The barred entry is the one place a division by the incidence survives, and it
            // survives only through c/i. Writing v" - i = c y - (1 + k) i,
            //
            //     q c13 = (1/2)(k-1)[ (c/i)( y^2 i_q^3 / L - i_q / n ) - (1+k) y i_q^3 / L ] ,
            //
            // and c/i = c/(c y - v) is finite wherever v is: on a flat surface facing
            // collimated space v = 0 and c/i is exactly 1/y, which is the limit the ray oracle
            // picks out. It diverges only where i vanishes with v NOT zero - a surface
            // concentric about the marginal focus - and there q is unbounded and the condensed
            // scheme has no value to offer, so the old guard is kept for that case alone.
            Scalar cOverI = SMath.Abs(t[3]) > 1e-30 ? c / t[3]
                          : (SMath.Abs(t[2]) < 1e-30 && SMath.Abs(t[1]) > 1e-30 ? 1.0 / t[1] : 0.0);
            Scalar iq3 = iq * iq * iq;
            Scalar qc13Spherical = SMath.Abs(lagrange) > 1e-30
                ? 0.5 * (k - 1.0)
                  * (cOverI * (t[1] * t[1] * iq3 / lagrange - iq / n)
                     - (1.0 + k) * t[1] * iq3 / lagrange)
                : 0.0;
            t[14] = qc13Spherical + rho * c13Figured;
            rows[i].C14Figured = rho * c13Figured;
            rows[i].C13Spherical = c13Spherical;
            rows[i].QApSpherical = g * t[3] * iq;
            rows[i].Q2ApSpherical = g * iq * iq;
            rows[i].QC13Spherical = qc13Spherical;
            rows[i].QOmega = (k - 1.0) * cOverI * iq / n;
            rows[i].COverI = cOverI;
            rows[i].Iq = iq;
            rows[i].Lagrange = lagrange;
            rows[i].KRatio = k;
            rows[i].NBefore = n;
            rows[i].FlatInCollimatedSpace =
                SMath.Abs(t[3]) < 1e-30 && SMath.Abs(t[2]) < 1e-30
                && SMath.Abs(lagrange) > 1e-30 && SMath.Abs(t[1]) > 1e-30;
            rows[i].C13Figured = c13Figured;

            // ── t34..t37: w and the three phi ───────────────────────────────────────────
            t[34] = ((k * k + 1.0) * t[3] * t[3] + t9Star - 3.0 * t[9]) / 8.0;
            // phi1. NO factor of one half - paper II Eq. (8.1) gives (k-1) i v, and the
            // half I first read off Table I was spurious. It cannot be settled on surface
            // 1, where v is zero and phi1 vanishes either way; on surface 2 the half puts
            // z3 + z4 at 16.162 where Buchdahl's printed columns require 15.249, and
            // without it the value is 15.2499.
            t[35] = (k - 1.0) * t[2] * t[3];
            t[36] = 0.5 * (-k * t[3] * t[3] - t[9] + 8.0 * t[34] + 3.0 * t[35]);
            t[37] = 2.0 * t[9] + 8.0 * t[34] + 5.0 * t[35] - t[36];

            // ── w1..w5, the inputs paper II Eq. (8.3) consumes ──────────────────────────
            // w1 and w2 are built from the SPHERICAL a_p. The figuring's contribution to the
            // secondary comes in as its own increment below; letting it through here as well
            // would count it twice.
            t[43] = (t2Star - 3.0 * t[2]) * t[7] * apSpherical;                 // w1
            t[48] = 0.5 * t[7] * t[7] * apSpherical;                            // w2
            t[49] = 0.25 * (t9Star - t[9]) * t2Star * t[7];                     // w3
            t[58] = 0.5 * t[8] * t[8];                                          // w4
            t[64] = t[7] * t[7] * t[8] / 8.0;                                   // w5

            t[81] = t[2] * t[5];
            t[82] = t[5] * t[5];

            // ── z1..z10, paper II Eq. (8.3), as Table I writes them ─────────────────────
            // The INTRINSIC z are built from the spherical a_p, for the same reason w1 and w2
            // are: the figuring enters below as its own increment and would otherwise be
            // counted twice. The induced terms further down do take the full t10.
            Scalar apS = rows[i].ApSpherical;
            t[121] = (0.25 * (2.0 * t2Star - 5.0 * t[2]) * t[7] * apS
                      + 10.0 * t[34] * t[34]) * apS;
            t[122] = 0.5 * ((t2Star - t[3]) * c * t[1] * t[2] * t[7] * apS
                            + 10.0 * t[34] * t[43]);
            t[123] = ((2.0 * t[34] + 0.5 * t[36] - 0.75 * t[35]) * t[35]
                      - t[34] * t[37]) * 0.75 * t[8];
            t[124] = (5.0 * t[9] - 10.0 * t[34] + 2.0 * t[36]) * 0.25 * t[48];
            t[125] = -0.5 * (t[2] + t2Star) * t[7] * t[8] * t[36];
            t[126] = 2.0 * (t[9] + t[34] - 5.0 * t[35] / 4.0) * t[64];
            t[127] = -t[7] * t[7] * t[43];
            t[128] = -2.0 * (4.0 * t[34] + 5.0 * t[35] - 2.0 * t[9]) * t[64];
            t[129] = -0.75 * t[7] * t[7] * t[58];
            t[130] = -0.5 * t[7] * t[7] * t[64];

            // ── q t152, for a flat surface facing collimated space ──────────────────────
            //
            // The barred tenth tertiary of (84.23) is q t155 plus induced terms, and where the
            // marginal incidence vanishes that q is infinite. Unrolling the intrinsic chain,
            //
            //     t152 = q^6 t121 + q^5 t122 + q^4(t123 + 9 t124) + q^3(t125 + t127)
            //          + q^2(t126 + t128) + q t129 + t130
            //
            // and the ten z carry the powers 7, 6, 5, 5, 4, 3, 4, 3, 2, 1 of the incidence, so
            // EVERY term is of order i exactly - there is no cancellation to track here, unlike
            // the layers below. Reading q^m X = i_q^m (X/i^p) i^(p-m) and dividing out the
            // common i leaves a polynomial in i_q whose coefficients are the reduced z. Checked
            // against the curvature ladder from R = 1e4 to 1e10 it reproduces t152/i to eight
            // figures at every radius, so these are identities and not limits.
            if (rows[i].FlatInCollimatedSpace)
            {
                Scalar cyi = rows[i].COverI * t[1], eq = rows[i].Iq, jj0 = t[7];
                Scalar uu = cyi - (1.0 + k);
                Scalar a3 = 0.5 * uu * (k - 1.0) * t[1] / lagrange;      // a_p / i^3
                Scalar vr = cyi - 1.0;                                    // v_p / i
                Scalar vpr = cyi - k;                                     // v" / i
                Scalar tt2 = ((k * k - 1.0) - cyi * cyi + cyi * (3.0 - k)) / 4.0;
                Scalar f1 = (k - 1.0) * vr;                               // phi1 / i^2
                Scalar f2 = 0.5 * (-k - vr * vr + 8.0 * tt2 + 3.0 * f1);  // phi2 / i^2
                Scalar f3 = 2.0 * vr * vr + 8.0 * tt2 + 5.0 * f1 - f2;    // phi3 / i^2
                Scalar om = (k - 1.0) * cyi / (t[1] * n);                 // omega / i
                Scalar u1 = (vpr - 3.0 * vr) * jj0 * a3;                  // w1 / i^4
                Scalar u2 = 0.5 * jj0 * jj0 * a3;                         // w2 / i^3
                Scalar u4 = 0.5 * om * om;                                // w4 / i^2
                Scalar u5 = jj0 * jj0 * om / 8.0;                         // w5 / i

                Scalar z1 = a3 * (0.25 * (2.0 * vpr - 5.0 * vr) * jj0 * a3 + 10.0 * tt2 * tt2);
                Scalar z2 = 0.5 * ((vpr - 1.0) * cyi * vr * jj0 * a3 + 10.0 * tt2 * u1);
                Scalar z3 = ((2.0 * tt2 + 0.5 * f2 - 0.75 * f1) * f1 - tt2 * f3) * 0.75 * om;
                Scalar z4 = (5.0 * vr * vr - 10.0 * tt2 + 2.0 * f2) * 0.25 * u2;
                Scalar z5 = -0.5 * (vr + vpr) * jj0 * om * f2;
                Scalar z6 = 2.0 * (vr * vr + tt2 - 1.25 * f1) * u5;
                Scalar z7 = -jj0 * jj0 * u1;
                Scalar z8 = -2.0 * (4.0 * tt2 + 5.0 * f1 - 2.0 * vr * vr) * u5;
                Scalar z9 = -0.75 * jj0 * jj0 * u4;
                Scalar z10 = -0.5 * jj0 * jj0 * u5;

                Scalar e2 = eq * eq, e3 = e2 * eq, e4 = e3 * eq, e5 = e4 * eq, e6 = e5 * eq;
                rows[i].QT152 = eq * (e6 * z1 + e5 * z2 + e4 * (z3 + 9.0 * z4)
                                    + e3 * (z5 + z7) + e2 * (z6 + z8) + eq * z9 + z10);
            }

            // A sphere has no check half, so all of this belongs to the hat pass.
            for (int m = 1; m <= 10; m++) rows[i].ZHat[m] = t[120 + m];

            // ── Aspheric figuring, tertiary ────────────────────────────────────────────
            // From (60.3) a figured surface adds D(3) I + L(3) Y, and every L is proportional
            // to a figuring coefficient, so a sphere has none - which is why paper II, dealing
            // only with spherical surfaces, writes its (2.1) with no L term at all.
            //
            // The normalisation is paper II's own (6.1), with the ray height beside L where
            // the incidence sits beside D. It is not fitted: with i = 0 and c0 = 0 it
            // reproduces Buchdahl's published (73.7) for a figured plate exactly, and with
            // L = 0 it reproduces this scheme's own z.
            var vf = surfaces[i].VertexForm();
            var fig = TertiaryCubics.Figuring.From(
                vf.Conic, vf.A4, vf.A6, vf.A8, vf.Curvature, scale);

            // A flat figured surface - a corrector plate - used to be skipped here, because the
            // assembly forms (j/c)^r and dD/c and both are 0/0 at c = 0. Neither has to be.
            //
            //   j = c (v_q y_p - v_p y_q) / i_p exactly, so j/c = L/i_p, wanting only a
            //   marginal incidence;
            //
            //   every term of the D cubic carries c0 - Y = c0 y in the first and c0 v" in the
            //   second - so DCubicOverC0 removes it by hand, and since the expansion is linear
            //   in the cubic coefficients, expanding that IS dD/c.
            //
            // The L side never needed either, which is why Buchdahl's plate of (73.7) came out
            // right through the general route while this one did not.
            if (fig.Present && SMath.Abs(t[3]) > 1e-30)
            {
                Scalar yy = t[1], vv = t[2], ii = t[3], jj = t[7];
                Scalar norm = n * (1.0 - k) / 16.0;
                Scalar jOverC = lagrange / ii;

                var dDOverC = TertiaryScriptT.ExpandCubicPhysical(
                    TertiaryCubics.DCubicIncrementOverC0(k, fig.C1, fig.C2, c, yy, vv),
                    yy, vv, c);
                var dL = TertiaryScriptT.ExpandCubicPhysical(
                    TertiaryCubics.LCubic(k, fig.C1, fig.C2, fig.C3, c, yy, vv), yy, vv, c);

                // No factor here, and there was one for a while. This assembly used to come
                // out at exactly twice what a closed-form conic surface requires, and carried
                // a 0.5 to correct it. The 2 was real but it was not this assembly's: it came
                // from Figuring.From handing the cubics the fifth-order code's c1, which is
                // twice Buchdahl's by (56.5). Fixing the convention at the source removed the
                // factor from here and fixed the c1-squared part as well, which no factor
                // here could ever have done - a doubled c scales terms linear in c by two and
                // c1-squared terms by four.
                // Kept apart, because M (85.3) puts them in different passes: dz is
                // i_p (dD/c) + y_p dL, so dD/c is the hat half and dL the check half.
                var dzH = new Scalar[11];
                var dzC = new Scalar[11];
                for (int m = 1; m <= 10; m++)
                {
                    Scalar w = norm * SMath.Pow(jOverC, TertiaryScriptT.JPower[m]);
                    dzH[m] = w * ii * dDOverC[m];
                    dzC[m] = tertiaryHatOnly ? 0.0 : w * yy * dL[m];
                }

                // z3 and z4 are packed: script-T_3 is z3 + z4 and script-T_4 is 8 z4. The
                // packing is linear, so each half may be packed on its own.
                foreach (var half in new[] { dzH, dzC })
                {
                    Scalar q4 = half[4] / 8.0;
                    half[3] -= q4;
                    half[4] = q4;
                }
                for (int m = 1; m <= 10; m++)
                {
                    rows[i].ZHat[m] += dzH[m];
                    rows[i].ZCheck[m] += dzC[m];
                    t[120 + m] += dzH[m] + dzC[m];
                }
            }

            // ── the ten intrinsic tertiary coefficients ─────────────────────────────────
            // t1p is z1 itself; the rest are short recurrences on the earlier ones.
        }

        // ── The image space, for W coordinates ──────────────────────────────────────────
        // Buchdahl's double prime: "the value of the quantity in question in the image space"
        // (VI Sec. 5(b)(ii)). For this scheme that has one concrete meaning - a running sum
        // taken over ALL surfaces instead of stopping short of the current one, and an angle
        // product built from the FINAL primed angles. These are the only extra quantities the
        // W rows of VI Table I need beyond what the paracanonical scheme already computes.
        Scalar dpA = 0, dpAbar = 0, dpB = 0, dpC = 0, dpCbar = 0;
        Scalar dp9 = 0, dp81 = 0, dp82 = 0;
        Scalar dpPi = 0;                 // Buchdahl's varpi in the image space
        if (wCoordinates)
        {
            for (int j = 1; j < count - 1; j++)
            {
                var tj = rows[j].T;
                dpA += tj[10]; dpAbar += tj[11]; dpB += tj[12]; dpC += tj[13]; dpCbar += tj[14];
                dpPi += tj[8];
            }
            Scalar vpEnd = rows[count - 2].VpPrime, vqEnd = rows[count - 2].VqPrime;
            dp9 = vpEnd * vpEnd; dp81 = vpEnd * vqEnd; dp82 = vqEnd * vqEnd;
        }

        // ── The induced mechanism ───────────────────────────────────────────────────────
        // Buchdahl's Sum_1^{j-1}: what a surface inherits from all those before it. This
        // is a second pass because each surface needs totals over its predecessors, which
        // are not known while the first pass is still walking forward.
        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            Scalar a = 0, ab = 0, b = 0, cc = 0, cb = 0, cbFigured = 0;
            for (int j = 1; j < i; j++)
            {
                a  += rows[j].T[10];
                ab += rows[j].T[11];
                b  += rows[j].T[12];
                cc += rows[j].T[13];
                cb += rows[j].T[14];
                cbFigured += rows[j].C14Figured;
            }
            rows[i].T19Figured = cbFigured;
            t[15] = a;      // A_p
            t[16] = ab;     // Abar_p
            t[17] = b;      // B_p
            t[18] = cc;     // C_p
            t[19] = cb;     // Cbar_p

            // t20 = (1/2)(t9 at surface 1 - t9 here) + t16 = A_q.
            //
            // A THIRD notation: a subscript 1 means the value at the FIRST surface, not a
            // running sum and not a primed value. Buchdahl now has three decorations that
            // all look similar - leading asterisk, trailing asterisk, subscript one - and
            // each means something different. This entry is not a running sum at all,
            // which is why it never fitted the pattern of t15..t19.
            //
            // VI Table I replaces it with the same quantity referred to the IMAGE SPACE:
            //     t20 = t16 - (1/2)t9 + K,   K = ((1/2)t9 - t16)''
            // which is the whole of the difference between the two coordinate systems at this
            // row - the reference point moves from the first surface to the image space.
            t[20] = wCoordinates
                ? (t[16] - 0.5 * t[9]) + (0.5 * dp9 - dpAbar)
                : 0.5 * (rows[1].T[9] - t[9]) + t[16];
        }

        // ── The q-side primary coefficients and the dagger family ───────────────────────
        // t24 carries its own previous-surface value, so this must run in surface order.
        Scalar first9 = rows[1].T[9];
        Scalar first81 = rows[1].T[2] * rows[1].T[5];
        Scalar first82 = rows[1].T[5] * rows[1].T[5];

        // ── The same four rows in W coordinates, VI Table I ─────────────────────────────
        //     t21 = -(1/2)t2 t5 + t18 + K,   K = ((1/2)t2 t5 - t18)''
        //     t22 = 2(t21 - t18) + t17 + K,  K = (2 t18 - t17)''
        //     t23 = -(1/2)t5^2 + t19 + K,    K = ((1/2)t5^2 - t19)''
        //     t24 = (t23 - t23*)t6 + t24*                                             [*]
        //
        // t24 is marked [*], which by Sec. 5(b)(iv) means its entries are calculated starting
        // from the LAST column rather than the first - the paracanonical recursion runs forward
        // from surface one, this one runs backward from the image space. Sec. 5(b)(v) supplies
        // the seed: a starred quantity at the last surface is to be read as zero. Because that
        // recursion needs t23 everywhere before it can start, these four are computed in their
        // own pass here rather than inside the loop below.
        if (wCoordinates)
        {
            for (int i = 1; i < count - 1; i++)
            {
                var t = rows[i].T;
                t[21] = (t[18] - 0.5 * t[2] * t[5]) + (0.5 * dp81 - dpC);
                t[22] = 2.0 * (t[21] - t[18]) + t[17] + (2.0 * dpC - dpB);
                t[23] = (t[19] - 0.5 * t[5] * t[5]) + (0.5 * dp82 - dpCbar);
            }
            for (int i = count - 2; i >= 1; i--)
            {
                var t = rows[i].T;
                Scalar next23 = i + 1 < count - 1 ? rows[i + 1].T[23] : 0.0;
                Scalar next24 = i + 1 < count - 1 ? rows[i + 1].T[24] : 0.0;
                t[24] = (t[23] - next23) * t[6] + next24;
            }
        }

        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            var prev = rows[i - 1].T;

            if (!wCoordinates)
            {
            t[21] = 0.5 * (first81 - t[2] * t[5]) + t[18];
            t[22] = 2.0 * (t[21] - t[18]) + t[17];
            t[23] = 0.5 * (first82 - t[5] * t[5]) + t[19];
            // t24 is a recursion on the increment in t23, and it carries the PREVIOUS
            // surface's ratio. Its figured half must therefore accumulate on that surface's
            // height ratio, not on its q - which is what s5p and s6p were feeling, they being
            // the only two whose formulae reach t24 rather than the dagger family.
            Scalar t23Figured = rows[i].T19Figured;
            Scalar prev23Figured = i > 1 ? rows[i - 1].T19Figured : 0.0;
            Scalar prev24Figured = i > 1 ? rows[i - 1].T24Figured : 0.0;
            Scalar prevRho = i > 1 ? rows[i - 1].Rho : 0.0;

            rows[i].T24Figured = (t23Figured - prev23Figured) * prevRho + prev24Figured;

            // The increment times the PREVIOUS surface's q. Where that surface is flat and
            // faces collimated space its q is infinite, and the increment vanishes against it:
            // the two halves of the increment are
            //
            //     -(1/2)(v_q" ^2 - v_q^2) = (1/2)(k-1) i_q [ 2 c y_q - (1+k) i_q ]
            //     c-bar_p                 = q c13
            //
            // each of order one - 0.310 and -0.310 on the ladder - and they cancel to order i.
            // They have to be regularised TOGETHER; either alone still diverges.
            //
            // Over the common factor i_q^2/i the brace is
            //
            //     2 (c/i) i y_q - (1+k) i_q + u y i_q^2 / L - (c/i)/n ,   u = (c/i) y - (1+k)
            //
            // whose i-free part is -i_q - 1/(y n). That vanishes identically here, and not by
            // accident: with v_p = 0 the invariant gives L = -i_q y and n L = H, so
            // i_q = -H/(n y), and the scheme works in units where H = 1. What is left is the
            // first-order term, and since the only i-dependence under v_p = 0 is through
            // i_q = (c/i) i y_q - v_q, differentiating gives
            //
            //     q delta = (1/2)(k-1) i_q^2 (c/i) y_q [ (1-k) + 2 y i_q u / L ] .
            //
            // On any ordinary surface the plain product is used; this is only for the case the
            // plain product cannot express.
            Scalar incrementSpherical = (t[23] - t23Figured) - (prev[23] - prev23Figured);
            Scalar carried;
            if (i > 1 && rows[i - 1].FlatInCollimatedSpace)
            {
                Scalar kp = rows[i - 1].KRatio;
                Scalar ciP = rows[i - 1].COverI;
                Scalar iqP = rows[i - 1].Iq;
                Scalar up = ciP * prev[1] - (1.0 + kp);
                carried = 0.5 * (kp - 1.0) * iqP * iqP * ciP * prev[4]
                        * ((1.0 - kp) + 2.0 * prev[1] * iqP * up / rows[i - 1].Lagrange);
            }
            else
            {
                carried = incrementSpherical * prev[6];
            }

            rows[i].QDelta23 = carried;
            Scalar t24Spherical = carried + (prev[24] - prev24Figured);
            t[24] = t24Spherical + rows[i].T24Figured;
            }

            t[25] = t[6] * t[15] - t[20];
            t[26] = t[6] * t[16] - t[21];
            t[27] = 2.0 * t[6] * t[16] - t[22];
            t[28] = t[6] * t[17] - 2.0 * t[23];
            t[29] = t[6] * t[18] - t[23];
            t[30] = t[6] * t[19] - t[24];

            t[31] = -t[6] * t[25] + t[26];
            t[32] = -t[6] * t[27] + t[28];
            t[33] = -t[6] * t[29] + t[30];

            // (85.1): the same six, on the height ratio.
            Scalar y = rows[i].Rho;
            var Y = rows[i].Y;
            Y[25] = y * t[15] - t[20];
            Y[26] = y * t[16] - t[21];
            Y[27] = 2.0 * y * t[16] - t[22];
            Y[28] = y * t[17] - 2.0 * t[23];
            Y[29] = y * t[18] - t[23];
            Y[30] = y * t[19] - t[24];

            Y[31] = -y * Y[25] + Y[26];
            Y[32] = -y * Y[27] + Y[28];
            Y[33] = -y * Y[29] + Y[30];
        }

        // ── The secondary coefficients S1p..S6p, with their dagger and barred forms ─────
        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;

            Scalar nBefore = i - 1 < indices.Length ? indices[i - 1] : 1.0;
            Scalar nAfter = i < indices.Length ? indices[i] : 1.0;
            if (SMath.Abs(nAfter) < 1e-12) nAfter = 1.0;
            Scalar kk = nBefore / nAfter;
            Scalar cc = surfaces[i].VertexCurvature * scale;
            var vf = surfaces[i].VertexForm();
            var figNow = TertiaryCubics.Figuring.From(
                vf.Conic, vf.A4, vf.A6, vf.A8, vf.Curvature, scale);

            // The six intrinsic secondary coefficients. They are chained - t44 is built
            // from t38, t50 from t44, and so on - so an aspheric increment cannot be added
            // as each is formed, or it would propagate down the chain as though it were part
            // of the spherical surface. The chain is run first, then the increments applied.
            Scalar s1 = 3.0 * rows[i].ApSpherical * t[34];
            Scalar s2 = 4.0 * t[6] * s1 + t[43];
            Scalar s3 = 0.5 * t[6] * (t[43] + s2) + t[48] + t[49];
            Scalar s4 = 2.0 * (s3 - 2.0 * t[48] - t[49]);
            Scalar s5 = (-t[6] * s2 + 2.0 * s3 + s4 - 2.0 * t[48]) * t[6] + t[58];
            Scalar s6 = 0.5 * (0.25 * (2.0 * t[49] - 2.0 * s3 - s4) * t[6]
                               + t[58] + s5) * t[6] + t[64];

            var da = aspheric != null && i < aspheric.Count ? aspheric[i] : null;
            var sphericalSix = new[] { s1, s2, s3, s4, s5, s6 };
            var figuredSix = new[]
            {
                da?[1] ?? 0.0, da?[2] ?? 0.0, da?[3] ?? 0.0,
                da?[4] ?? 0.0, da?[5] ?? 0.0, da?[6] ?? 0.0,
            };

            // ── The six q-lifts, by Sec. 84(e) ──────────────────────────────────────────
            //
            // The barred secondary is s-bar_mu = q s_mu + (84.42), so the only singular piece
            // is q s_mu, and q is infinite wherever the marginal incidence vanishes - a flat
            // surface facing collimated space. Sec. 84(e) is the way out: the hatted
            // quantities "appear only in the combinations i_p t^_mu-p, i_q t^_mu-p", so the
            // powers of q are to be carried on the incidences and never formed alone.
            //
            // Every quantity in the chain is a fixed power of i times something regular, and
            // the powers are what make it work. Writing v = c y - i and v" = c y - k i,
            //
            //     P    = (v" - 3v) t7 = i (3 - k - 2 c y/i) t7          so P    ~ i
            //     t34  = [ (k^2-1) i^2 - c^2 y^2 + c y i (3-k) ] / 4     so t34  ~ i^2
            //     t49  = (1/4)(v"^2 - v^2) v" t7, and v"^2 - v^2
            //          = i^2 (1-k)(2 c y/i - (1+k))                      so t49  ~ i^3
            //     t58  = omega^2/2, omega = (k-1) c / n                  so t58  ~ i^2
            //     t64  = t7^2 omega / 8                                  so t64  ~ i
            //     a_p  = g i^2, g = (1/2)(v" - i)(k-1) y / L             so a_p  ~ i^3
            //
            // Carrying each as X/i^p and reading q^m X = i_q^m (X/i^p) i^(p-m), the six come
            // out with p = 5, 4, 3, 3, 2, 1, so q s_mu = S_mu i_q i^(p-1) is regular for every
            // one of them. The apparent divergences - q^4 a_p among them - all cancel against
            // one of the factors of i above, which is why a term-by-term lift of the chain
            // fails while this does not.
            Scalar ci = rows[i].COverI, iqq = rows[i].Iq, ii = t[3];
            Scalar cyi = ci * t[1];
            Scalar aoi3 = SMath.Abs(rows[i].Lagrange) > 1e-30
                        ? 0.5 * (cyi - (1.0 + kk)) * (kk - 1.0) * t[1] / rows[i].Lagrange
                        : 0.0;
            Scalar omoi = SMath.Abs(t[1]) > 1e-30 ? (kk - 1.0) * cyi / (t[1] * nBefore) : 0.0;
            Scalar poi = (3.0 - kk - 2.0 * cyi) * t[7];
            Scalar t34r = ((kk * kk - 1.0) - cyi * cyi + cyi * (3.0 - kk)) / 4.0;
            Scalar t48r = 0.5 * t[7] * t[7] * aoi3;
            Scalar t49r = 0.25 * (1.0 - kk) * (2.0 * cyi - (1.0 + kk)) * (cyi - kk) * t[7];
            Scalar t58r = 0.5 * omoi * omoi;
            Scalar t64r = t[7] * t[7] * omoi / 8.0;

            Scalar s2r = aoi3 * (12.0 * t34r * iqq + poi);
            Scalar s3r = 0.5 * iqq * (poi * aoi3 + s2r) + t48r + t49r;
            Scalar s4r = 2.0 * (s3r - 2.0 * t48r - t49r);
            Scalar brr = 12.0 * aoi3 * t34r * iqq * iqq + 3.0 * poi * aoi3 * iqq
                       - 2.0 * t48r + 2.0 * t49r;
            Scalar s5r = brr * iqq + t58r;
            Scalar wr = 0.25 * iqq * (2.0 * t49r - 2.0 * s3r - s4r) + t58r + s5r;
            Scalar s6r = 0.5 * wr * iqq + t64r;

            Scalar i2 = ii * ii;
            var qSec = new[]
            {
                3.0 * aoi3 * t34r * iqq * i2 * i2,   // q s1, p = 5
                s2r * iqq * ii * i2,                 // q s2, p = 4
                s3r * iqq * i2,                      // q s3, p = 3
                s4r * iqq * i2,                      // q s4, p = 3
                s5r * iqq * ii,                      // q s5, p = 2
                s6r * iqq,                           // q s6, p = 1
            };

            rows[i].SecSph[1] = s1; rows[i].SecSph[2] = s2; rows[i].SecSph[3] = s3;
            rows[i].SecSph[4] = s4; rows[i].SecSph[5] = s5; rows[i].SecSph[6] = s6;
            for (int q = 0; q < 6; q++) rows[i].SecFig[q + 1] = figuredSix[q];

            t[38] = s1 + figuredSix[0];
            t[44] = s2 + figuredSix[1];
            t[50] = s3 + figuredSix[2];
            t[54] = s4 + figuredSix[3];
            t[59] = s5 + figuredSix[4];
            t[65] = s6 + figuredSix[5];

            // Two passes: the spherical half on the incidence ratio, the figured half on the
            // height ratio. See Secondary below for why they cannot share one.
            Scalar ap = rows[i].ApSpherical, af = rows[i].ApFigured, rr = rows[i].Rho;
            Scalar[] tS = new Scalar[6], bS = new Scalar[6], mS = new Scalar[6];
            Scalar[] tF = new Scalar[6], bF = new Scalar[6], mF = new Scalar[6];

            // The dagger family, twice. Its formulae apply the CURRENT surface's ratio to
            // the accumulated sums, so the figured pass needs its own set built on the height
            // ratio. Without them the induced term of a figured surface that has figuring
            // upstream is wrong - which is exactly, and only, where the disagreement was.
            var dSpherical = new[]
            {
                t[25], t[26], t[27], t[28], t[29], t[30], t[31], t[32], t[33],
            };

            Scalar f0 = rr * t[15] - t[20];
            Scalar f1 = rr * t[16] - t[21];
            Scalar f2 = 2.0 * rr * t[16] - t[22];
            Scalar f3 = rr * t[17] - 2.0 * t[23];
            Scalar f4 = rr * t[18] - t[23];
            Scalar f5 = rr * t[19] - t[24];
            var dFigured = new[]
            {
                f0, f1, f2, f3, f4, f5,
                -rr * f0 + f1, -rr * f2 + f3, -rr * f4 + f5,
            };

            // The first three are a_p, q a_p and c13, and all three now come from the q-free
            // forms of Sec. 84(e), so a flat surface in collimated space carries its real
            // values into the secondary instead of zeros. The RATIO argument is still q, which
            // is genuinely infinite there - that part of the chain is not yet regularised.
            var qFigured = new Scalar[6];
            for (int m = 0; m < 6; m++) qFigured[m] = rr * figuredSix[m];

            Secondary(t, ap, rows[i].QApSpherical, rows[i].C13Spherical,
                      rows[i].Q2ApSpherical, rows[i].QC13Spherical, rows[i].QOmega, qSec,
                      dSpherical, sphericalSix, standalone: true, tS, bS, mS, dpPi);
            Secondary(t, af, rr * af, rr * rr * af,
                      rr * rr * af, rr * (rr * rr * af), rr * t[8], qFigured,
                      dFigured, figuredSix, standalone: false, tF, bF, mF, dpPi);

            // The figuring's D half travels on the INCIDENCE ratio, not the height ratio.
            //
            // M (65.5) puts no figuring in D at first order, which is why the primary is exact
            // with the whole increment on the height ratio. M (65.6) puts it there at second
            // order, so from the secondary on, the increment is q*D + rho*L where this scheme
            // forms rho*(D+L). The difference is (q - rho)*D, and it lands on the BARRED entry
            // alone: the unbarred total is D + L either way and is already right.
            //
            // Correcting the whole chain instead - routing the D half through its own pass -
            // changes the unbarred totals and the induced terms too, and is much worse. What
            // says so is Buchdahl's own identity: solving (7.1) for the S1bar_p it requires
            // leaves a residual that is constant between figured surfaces and steps at each
            // one, and each step is exactly (q - rho) times that surface's D half.
            if (aspheric != null && figNow.Present)
            {
                // SecondaryDHalf forms s1 (t7/c)^p dTheta / sTheta0, in which every power of c
                // cancels - but only after it has been formed, so a flat surface gives 0/0 there.
                // The D half itself is finite at c = 0: the R = 1e10 twin of a figured flat comes
                // out exact against Forbes with it, which it could not with a divergent D. So a
                // flat surface takes the limit, symmetrically, from c = +eps and c = -eps; the
                // first-order term cancels and what is left is O(eps^2). The incidence, q and j
                // are re-formed at each eps rather than read off the flat row, because j =
                // c L / i_p carries the curvature as a factor: on the flat row t7 is exactly zero
                // and t7/eps would be zero where its limit L/i_p is not.
                Scalar[] dOnly;
                if (SMath.Abs(cc) > 1e-12)
                {
                    dOnly = SecondaryDHalf(t, kk, cc, figNow, s1);
                }
                else
                {
                    Scalar[] At(Scalar e)
                    {
                        var near = (Scalar[])t.Clone();
                        near[3] = e * t[1] - t[2];
                        near[6] = SMath.Abs(near[3]) > 1e-30 ? (e * t[4] - t[5]) / near[3] : 0.0;
                        near[7] = -t[2] * near[6] + t[5];
                        return SecondaryDHalf(near, kk, e, figNow, s1);
                    }
                    const double eps = 1e-6;
                    var above = At(eps);
                    var below = At(-eps);
                    dOnly = new Scalar[6];
                    for (int m = 0; m < 6; m++) dOnly[m] = 0.5 * (above[m] + below[m]);
                }
                Scalar lead = t[6] - rr;
                for (int m = 0; m < 6; m++) bF[m] += lead * dOnly[m];
                for (int m = 0; m < 6; m++) rows[i].SecDFigured[m] = dOnly[m];
            }

            for (int q = 1; q <= 5; q++) { rows[i].MSph[q] = mS[q]; rows[i].MFig[q] = mF[q]; }
            for (int m = 0; m < 6; m++) rows[i].SecBarFig[m] = bF[m];

            // Split by ratio power. Running Secondary with a = af, ab = rho af, cc = rho^2 af,
            // q2a = rho^2 af, qcc = rho^3 af and qs = rho * figuredSix, every q_N collapses to
            // rho s_N exactly, so
            //
            //     bar[m] = rho s_m + (a bracket in d6..d8, every term carrying af)
            //
            // The first is the LIFT - this surface own ratio applied once to its own unbarred
            // figured secondary - and it is the only part on which the dagger correction below
            // is a single factor of (q~ - q). The bracket is induced, A-bar_(I) a_v and its
            // partners, and takes its ratio through the accumulated dagger family instead.
            // Every bracket term is proportional to af, so the two coincide exactly when
            // c1 = 0 - which is why shifting the whole of bF was exact on the figured-sphere
            // fixtures and wrong on the r^4 ones.
            for (int m = 0; m < 6; m++) rows[i].SecBarFigLift[m] = rr * tF[m];

            t[45] = mS[1] + mF[1];
            t[51] = mS[2] + mF[2];
            t[55] = mS[3] + mF[3];
            t[61] = mS[4] + mF[4];
            t[66] = mS[5] + mF[5];

            t[39] = 2.0 * t[10] * t[25];
            t[40] = t[38] + t[39];
            t[41] = tS[0] + tF[0];                                         // S1p
            t[42] = bS[0] + bF[0];                                         // S1p bar
            t[46] = tS[1] + tF[1];
            t[47] = bS[1] + bF[1];
            t[52] = tS[2] + tF[2];
            t[53] = bS[2] + bF[2];
            t[56] = tS[3] + tF[3];
            t[57] = bS[3] + bF[3];
            t[62] = tS[4] + tF[4];
            t[63] = bS[4] + bF[4];
            t[67] = tS[5] + tF[5];
            t[68] = bS[5] + bF[5];
        }

        // ── Running sums of the secondary coefficients, and the q-side secondaries ──────
        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            foreach (var (target, source) in new[]
                     { (69, 41), (70, 42), (71, 46), (72, 47), (73, 52), (74, 53),
                       (75, 56), (76, 57), (77, 62), (78, 63), (79, 67), (80, 68) })
            {
                Scalar a = 0.0;
                for (int j = 1; j < i; j++) a += rows[j].T[source];
                t[target] = a;
            }
        }

        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            var prev = rows[i - 1].T;
            Scalar t9Star = rows[i].VpPrime * rows[i].VpPrime;
            Scalar t81Star = rows[i].VpPrime * rows[i].VqPrime;

            t[83] = t[16] - t[20];
            t[84] = t[17] - t[22];
            t[85] = t[19] - t[23];

            t[86] = -1.5 * t[83] * t[83] + t[9] * t[16] - t[16] * t[20]
                    + t[15] * t[21] - t[15] * t[81] + t[70];                       // S1q
            t[87] = (2.0 * t[21] - t[22] - t[81]) * t[16] + t[9] * t[21] - t[20] * t[81];
            t[88] = (2.0 * t[23] - t[82]) * t[15] - t[17] * t[20] + t[9] * t[17];
            t[89] = -3.0 * t[83] * t[84] + t[87] + t[88] + t[72];                  // S2q
            t[90] = -0.5 * t[81] * t[84] + t[9] * t[19] - t[20] * t[82];
            t[91] = t[18] * t[21] + t[15] * t[24] - t[16] * t[23] - t[19] * t[20];
            t[92] = -3.0 * t[83] * t[85] + t[74] + t[90] + t[91];                  // S3q
            t[93] = 2.0 * (2.0 * t[16] * t[23] - t[16] * t[82] + t[9] * t[23]) - t[17] * t[22];
            t[94] = -1.5 * t[84] * t[84] + t[81] * t[84] + t[76] + t[93];          // S4q
            t[95] = (2.0 * t[18] - t[17] + t[81]) * t[23] + t[19] * t[81] - t[22] * t[82];
            t[96] = (2.0 * t[16] + t[9]) * t[24] - t[19] * t[22] - t[18] * t[82];
            t[97] = -3.0 * t[84] * t[85] + t[95] + t[96] + t[78];                  // S5q
            t[98] = -1.5 * t[85] * t[85] + t[24] * t[81] + t[18] * t[24]
                    - t[23] * t[82] - t[19] * t[23] + t[80];                       // S6q

            t[99] = 0.5 * (t[9] - t9Star) + t[11];                                 // a_q
            t[100] = t[81] - t81Star + t[12];                                      // b_q

            // The dagger family. Several of these carry their own previous-surface value,
            // so this loop must stay in surface order.
            //
            // Each of the six recursions below multiplies an INCREMENT in the accumulated
            // q-side secondary by the PREVIOUS surface's q. Reading t102, the bracket is
            //
            //     prev70 - prev86 + t86  =  prev70 + (t86 - prev86)
            //
            // and t86 - prev86 is what surface i-1 just added to 'S1q. That increment already
            // carries surface i-1's own ratio inside it - M (65.7) put the figured half of a
            // q-side quantity on the height ratio when t42 and its five partners were formed -
            // so multiplying by q a second time gives the figured half q q~ where it should
            // have q~ squared. Writing the term as
            //
            //     -q_prev (dQ - dQ_fig) - q~_prev dQ_fig = -q_prev dQ - (q~_prev - q_prev) dQ_fig
            //
            // leaves the one extra product appended to each line. This is the same correction
            // t24 already makes one order down, and in the same shape: on the INCREMENT, not
            // on the accumulation.
            //
            // The accumulated halves of the bracket need no such term. Setting the increments
            // aside, -q_prev P_prev + q_i P_i + prev102 telescopes to q_i P_i, so the previous
            // q there is a summation partner that cancels rather than a per-surface tag; it
            // was measured, and correcting it as though it were one costs a factor of ten
            // (Triplet24 3.14 to 42.4 per cent).
            //
            // dQ_fig is the figured half of that increment, which is exactly the previous
            // surface's own figured barred secondary. The closed form for t86 carries other
            // figured content through products of accumulations, but a product has no additive
            // figured half to speak of, and taking one - by shadowing the accumulations or by
            // differencing a spherical twin - measures worse on every multi-surface design
            // tried (Triplet24 5.29 against 2.72, SPOTM 5.30 against 4.06).
            Scalar dPrevRatio = i > 1 ? rows[i - 1].Rho - prev[6] : 0.0;
            var prv = rows[i - 1];

            t[101] = t[6] * t[69] - t[86];
            t[102] = -prev[6] * (prev[70] - prev[86] + t[86]) + t[6] * t[70]
                     - prev[31] * prev[99] + prev[102]
                     - dPrevRatio * prv.SecBarFigLift[0];
            t[103] = t[6] * t[71] - t[89];
            t[104] = -prev[6] * (prev[72] - prev[89] + t[89]) + t[6] * t[72]
                     - prev[31] * prev[100] - prev[32] * prev[99] + prev[104]
                     - dPrevRatio * prv.SecBarFigLift[1];
            t[105] = t[6] * t[73] - t[92];
            t[106] = -0.5 * prev[100] * prev[31] * prev[6] + t[74] * t[6] - prev[33] * prev[99];
            t[107] = -prev[6] * (prev[74] - prev[92] + t[92]) + t[106] + prev[107]
                     - dPrevRatio * prv.SecBarFigLift[2];
            t[108] = t[6] * t[75] - t[94];
            t[109] = -prev[6] * (prev[76] - prev[94] + t[94]) - prev[32] * prev[100]
                     + t[6] * t[76] + prev[109] - dPrevRatio * prv.SecBarFigLift[3];
            t[110] = t[6] * t[77] - t[97];
            t[111] = -((0.5 * prev[6] * prev[32] + prev[33]) * prev[100]) + t[6] * t[78];
            t[112] = -prev[6] * (prev[78] - prev[97] + t[97]) + t[111] + prev[112]
                     - dPrevRatio * prv.SecBarFigLift[4];
            t[113] = t[6] * t[79] - t[98];
            // The last of the recursions that carry the PREVIOUS surface's q. Where that
            // surface is flat and faces collimated space the bracket collapses to -t98, its
            // other terms being products of quantities the surface has not accumulated, and
            // t98 vanishes with the incidence by a cancellation five terms deep. Writing
            // t85 = t19 - t23 and expanding,
            //
            //     t98 = (t80 - (3/2) t19^2) + 2 t19 t23 - (3/2) t23^2
            //         + t24(t81 + t18) - t23 t82
            //
            // and the first bracket is the one that cancels: t80 = (3/2) t19^2 to order i.
            // What survives, per unit incidence, is
            //
            //     t98/i = (t80 - (3/2)t19^2)/i + (t23/i)(2 t19 - t82) + t24 (t81 + t18)/i
            //
            // Three of the four are already at hand. t23/i is the increment the t24 block just
            // regularised, divided by i_q. t81/i is (1-k)(c/i) y times the current v_q, the
            // previous surface leaving v_p" = (1-k)(c/i) y i. t18/i is that surface's c13/i.
            //
            // The fourth needs the cancelling bracket expanded, and it comes out closed:
            // the reduced quantities a, P, T, A, B, C, D of the Sec. 84(e) chain do not depend
            // on i_q at all, so the intrinsic sixth is a QUARTIC in it,
            //
            //     s6/i = 3 a T i_q^4 + a P i_q^3 + (B - A) i_q^2 + C i_q + D ,
            //
            // t80 is that times i_q, and c-bar_p = alpha i_q^3 + beta i_q. Under v_p = 0 the
            // only i-dependence anywhere is i_q = (c/i) i y_q - v_q, so
            // X/i = (c/i) y_q X'(i_q) with X = t80 - (3/2) c-bar_p^2. Checked against the
            // R = 1e10 twin, the quartic reproduces t80 exactly and X/i to seven figures.
            Scalar t114Carried;
            if (i > 1 && rows[i - 1].FlatInCollimatedSpace)
            {
                var pr = rows[i - 1];
                Scalar kp = pr.KRatio, ciP = pr.COverI, iqP = pr.Iq;
                Scalar yP = prev[1], yqP = prev[4], lP = pr.Lagrange, t7P = prev[7];
                Scalar nP = SMath.Abs(pr.NBefore) > 1e-12 ? pr.NBefore : 1.0;
                Scalar cyiP = ciP * yP;

                Scalar aR = 0.5 * (cyiP - (1.0 + kp)) * (kp - 1.0) * yP / lP;
                Scalar pR = (3.0 - kp - 2.0 * cyiP) * t7P;
                Scalar tR = ((kp * kp - 1.0) - cyiP * cyiP + cyiP * (3.0 - kp)) / 4.0;
                Scalar aa = 0.5 * t7P * t7P * aR;
                Scalar bb = 0.25 * (1.0 - kp) * (2.0 * cyiP - (1.0 + kp)) * (cyiP - kp) * t7P;
                Scalar omR = (kp - 1.0) * ciP / nP;
                Scalar ccR = 0.5 * omR * omR;
                Scalar ddR = t7P * t7P * omR / 8.0;

                Scalar uP = cyiP - (1.0 + kp);
                Scalar alpha = 0.5 * (kp - 1.0) * yP * uP / lP;
                Scalar beta = -0.5 * (kp - 1.0) * ciP / nP;

                Scalar e2 = iqP * iqP, e3 = e2 * iqP, e4 = e3 * iqP;
                Scalar xPrime = 15.0 * aR * tR * e4 + 4.0 * aR * pR * e3
                              + 3.0 * (bb - aa) * e2 + 2.0 * ccR * iqP + ddR
                              - 3.0 * (alpha * e3 + beta * iqP) * (3.0 * alpha * e2 + beta);

                Scalar xOverI = ciP * yqP * xPrime;
                Scalar t23OverI = SMath.Abs(iqP) > 1e-30 ? rows[i].QDelta23 / iqP : 0.0;
                Scalar t81OverI = (1.0 - kp) * ciP * yP * t[5];
                Scalar t18OverI = aR * e2 - 0.5 * omR;

                Scalar t98OverI = xOverI + t23OverI * (2.0 * t[19] - t[82])
                                + t[24] * (t81OverI + t18OverI);

                t114Carried = prev[6] * (-0.5 * prev[33] * prev[100] - prev[80] + prev[98])
                            - iqP * t98OverI;
            }
            else
            {
                t114Carried = prev[6] * (-0.5 * prev[33] * prev[100] - prev[80] + prev[98]
                                         - t[98])
                            - dPrevRatio * prv.SecBarFigLift[5];
            }

            t[114] = t114Carried + t[6] * t[80] + prev[114];

            t[115] = -t[6] * t[101] + t[102];
            t[116] = -t[6] * t[103] + t[104];
            t[117] = -t[6] * t[105] + t[107];
            t[118] = -t[6] * t[108] + t[109];
            t[119] = -t[6] * t[110] + t[112];
            t[120] = -t[6] * t[113] + t[114];

            // (85.1) one order up: the SAME p quantities t69..t80 and q quantities t86..t98,
            // joined on rho. Where the recursion carries a previous surface value it carries
            // the previous (Y) value, and where it carries the previous surface RATIO it
            // carries that surface height ratio - the same distinction t24 already makes.
            var Y = rows[i].Y;
            var prevY = rows[i - 1].Y;
            Scalar y = rows[i].Rho;
            Scalar py = i > 1 ? rows[i - 1].Rho : 0.0;

            Y[101] = y * t[69] - t[86];
            Y[102] = -py * (prev[70] - prev[86] + t[86]) + y * t[70]
                     - prevY[31] * prev[99] + prevY[102];
            Y[103] = y * t[71] - t[89];
            Y[104] = -py * (prev[72] - prev[89] + t[89]) + y * t[72]
                     - prevY[31] * prev[100] - prevY[32] * prev[99] + prevY[104];
            Y[105] = y * t[73] - t[92];
            Y[106] = -0.5 * prev[100] * prevY[31] * py + t[74] * y - prevY[33] * prev[99];
            Y[107] = -py * (prev[74] - prev[92] + t[92]) + Y[106] + prevY[107];
            Y[108] = y * t[75] - t[94];
            Y[109] = -py * (prev[76] - prev[94] + t[94]) - prevY[32] * prev[100]
                     + y * t[76] + prevY[109];
            Y[110] = y * t[77] - t[97];
            Y[111] = -((0.5 * py * prevY[32] + prevY[33]) * prev[100]) + y * t[78];
            Y[112] = -py * (prev[78] - prev[97] + t[97]) + Y[111] + prevY[112];
            Y[113] = y * t[79] - t[98];
            Y[114] = py * (-0.5 * prevY[33] * prev[100] - prev[80] + prev[98] - t[98])
                     + y * t[80] + prevY[114];

            Y[115] = -y * Y[101] + Y[102];
            Y[116] = -y * Y[103] + Y[104];
            Y[117] = -y * Y[105] + Y[107];
            Y[118] = -y * Y[108] + Y[109];
            Y[119] = -y * Y[110] + Y[112];
            Y[120] = -y * Y[113] + Y[114];
        }

        // ── The tertiary totals, by the two passes of M Sec. 85 ─────────────────────────
        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            var r = rows[i];

            // The surface's own quantities, to be swapped for each half and put back after.
            Scalar o10 = t[10], o13 = t[13], o40 = t[40];
            Scalar o38 = t[38], o44 = t[44], o50 = t[50], o54 = t[54], o59 = t[59], o65 = t[65];
            Scalar o45 = t[45], o51 = t[51], o55 = t[55], o61 = t[61], o66 = t[66];
            var oz = new Scalar[11];
            for (int m = 1; m <= 10; m++) oz[m] = t[120 + m];

            var hat = new Scalar[11];
            var check = new Scalar[11];
            var residue = new Scalar[11];
            var zero = new Scalar[11];
            var hatBar = new Scalar[11];
            var checkBar = new Scalar[11];
            var residueBarQ = new Scalar[11];
            var residueBarRho = new Scalar[11];
            var residueCheck = new Scalar[11];

            // M (85.1): the hat pass is combined by the (I) family, the check pass by the
            // (Y) family - the same p and q quantities on the height ratio instead of the
            // incidence ratio. Both are already built, alongside each other, in the loops
            // above; here each pass is simply given the one that belongs to it, at BOTH
            // orders. Giving it only the secondary six leaves the computation a hybrid, which
            // Sec. 85 does not admit: every coefficient splits or none does.
            var own = new Scalar[156];
            for (int m = 25; m <= 33; m++) own[m] = t[m];
            for (int m = 101; m <= 120; m++) own[m] = t[m];

            void Family(bool checkHalf)
            {
                var src = checkHalf ? r.Y : own;
                for (int m = 25; m <= 33; m++) t[m] = src[m];
                for (int m = 101; m <= 120; m++) t[m] = src[m];
            }

            void Load(Scalar ap, Scalar c13, IReadOnlyList<Scalar> sec, IReadOnlyList<Scalar> mm,
                      IReadOnlyList<Scalar> z)
            {
                t[10] = ap;
                t[13] = c13;
                t[38] = sec[1]; t[44] = sec[2]; t[50] = sec[3];
                t[54] = sec[4]; t[59] = sec[5]; t[65] = sec[6];
                t[45] = mm[1]; t[51] = mm[2]; t[55] = mm[3]; t[61] = mm[4]; t[66] = mm[5];
                for (int m = 1; m <= 10; m++) t[120 + m] = z[m];
                t[40] = t[38] + 2.0 * t[10] * t[25];
            }

            // Some induced terms involve NO quantity of this surface at all - they are built
            // from the accumulated coefficients alone. Those belong to one pass, not both, so
            // the all-zero residue is measured and taken off the check half. On a sphere the
            // check inputs are all zero and this makes the check half vanish identically, as
            // it must, a sphere having no check half.
            // The residue is measured with the SAME intermediates as the pass it is taken
            // off, or it removes accumulated-only terms of the wrong half.
            Family(checkHalf: true);
            Load(0.0, 0.0, zero, zero, zero);
            TertiaryPass(t, r.Rho, residueCheck, residueBarRho);

            Family(checkHalf: false);
            Load(0.0, 0.0, zero, zero, zero);
            TertiaryPass(t, t[6], residue, residueBarQ);

            Load(r.ApSpherical, r.C13Spherical, r.SecSph, r.MSph, r.ZHat);
            var hatIntrinsic = new Scalar[11];
            var hatIntrinsicBar = new Scalar[11];
            TertiaryPass(t, t[6], hat, hatBar, r.FlatInCollimatedSpace, r.QT152,
                         hatIntrinsic, hatIntrinsicBar);

            Family(checkHalf: true);
            Load(r.ApFigured, r.C13Figured, r.SecFig, r.MFig, r.ZCheck);
            var checkIntrinsic = new Scalar[11];
            var checkIntrinsicBar = new Scalar[11];
            TertiaryPass(t, r.Rho, check, checkBar, false, 0.0,
                         checkIntrinsic, checkIntrinsicBar);
            for (int k = 1; k <= 10; k++)
            {
                check[k] -= residueCheck[k];
                checkBar[k] -= residueBarRho[k];
            }

            Family(checkHalf: false);

            t[10] = o10; t[13] = o13; t[40] = o40;
            t[38] = o38; t[44] = o44; t[50] = o50; t[54] = o54; t[59] = o59; t[65] = o65;
            t[45] = o45; t[51] = o51; t[55] = o55; t[61] = o61; t[66] = o66;
            for (int m = 1; m <= 10; m++) t[120 + m] = oz[m];

            // One more pass with the quantities put back, so that t131..t155 are left holding
            // the conventional single-pass values. Nothing above reads them - the totals come
            // from the two passes - but the published-table tests do, and so may a reader.
            var spare = new Scalar[11];
            var spareBar = new Scalar[11];
            TertiaryPass(t, t[6], spare, spareBar);

            // (85.3): the total is the sum of the two halves. The barred entry is the sum of
            // the two barred halves, each formed by (84.23) within its own pass - not the
            // total times a ratio, which would drop the barred intermediates entirely.
            for (int k = 1; k <= 10; k++)
            {
                rows[i].TertiaryTotal[k] = hat[k] + check[k];
                rows[i].TertiaryTotalBar[k] = hatBar[k] + checkBar[k];

                // The residue carries no intrinsic - it is measured with the z quantities zeroed,
                // and every intrinsic is built from those - so nothing is taken off here.
                rows[i].TertiaryIntrinsicTotal[k] = hatIntrinsic[k] + checkIntrinsic[k];
                rows[i].TertiaryIntrinsicTotalBar[k] = hatIntrinsicBar[k] + checkIntrinsicBar[k];
            }
        }

        return rows;
    }




    /// <summary>
    /// The secondary coefficients and their barred partners, for ONE half of the surface's
    /// contribution — the spherical half or the figured one.
    ///
    /// <para>Both halves run down these same formulae. What differs is the ratio the bar
    /// travels on: the spherical half comes from the D term of (60.3), whose p and q
    /// components go as the incidences, so it uses q; the figured half comes from the L term,
    /// whose components go as the ray heights, so it uses y_q/y_p. Running the figuring on q
    /// is what left the second and later figured surfaces of a system wrong by tens of per
    /// cent.</para>
    ///
    /// <para><paramref name="standalone"/> selects the terms that carry no primary factor -
    /// the Petzval-weighted ones. They belong to the spherical half alone and would otherwise
    /// be counted twice.</para>
    /// </summary>
    /// <summary>
    /// The D half of the aspheric secondary increment, in the scheme's own units.
    ///
    /// <para>Derived rather than bridged: M (65.1-2) give the two halves, (218.31)'s
    /// substitution puts them in the theta basis, (218.6) reads that back as s_1p and w1..w5,
    /// and (218.7) runs those forward. The normalising scalar is taken from the spherical run
    /// of the same surface, where it is exact, and it cannot bias anything here because the
    /// D half is all that is wanted. See DerivedSecondaryTests, which holds the two halves
    /// together against the bridged total they must reproduce.</para>
    ///
    /// <para>The L half carries one power of c less than the D half - the weighting
    /// <see cref="TertiaryCubics.LCubic"/> documents at third order - which is why only the D
    /// half is built here and the L half is left to the bridge that already gets the total
    /// right.</para>
    /// </summary>
    /// <summary>
    /// The powers of j/c carried by (218.6)'s six quantities: s_1p, w1, w2+w3, -2 w2, w4, w5.
    /// The second-order counterpart of Table IV's j-powers, straight from the equation - w1
    /// appears with j^-1, w2 and w3 with j^-2, w4 with j^-3, w5 with j^-4 - with the c powers
    /// following from the scaled theta the scheme works in.
    /// </summary>
    private static readonly int[] SecondaryJPower = { 0, 1, 2, 2, 3, 4 };

    private static Scalar[] SecondaryDHalf(Scalar[] t, Scalar k, Scalar c,
                                           TertiaryCubics.Figuring fig, Scalar sphericalS1)
    {
        var (dRaw, _) = TertiaryCubics.SecondaryHalves(k, fig.C1, fig.C2, c, t[1], t[2]);
        var dTheta = TertiaryScriptT.ExpandQuadraticPhysical(dRaw, t[1], t[2], c);
        var sTheta = TertiaryScriptT.ExpandQuadraticPhysical(
            TertiaryCubics.SecondaryDSpherical(k, c, t[1], t[2]), t[1], t[2], c);

        Scalar baseline = sTheta[0] * t[3] / c;
        if (SMath.Abs(baseline) < 1e-25) return new Scalar[6];
        Scalar norm = sphericalS1 / baseline;
        Scalar jc = t[7] / c;

        var g = new Scalar[6];
        for (int m = 0; m < 6; m++)
            g[m] = norm * SMath.Pow(jc, SecondaryJPower[m]) * (t[3] / c) * dTheta[m];

        Scalar w2 = -0.5 * g[3];
        var w = new[] { g[0], g[1], w2, g[2] - w2, g[4], g[5] };

        Scalar q1 = t[6], q2 = q1 * q1, q3 = q2 * q1, q4 = q3 * q1;
        Scalar a1 = w[0];
        return new[]
        {
            a1,
            4.0 * q1 * a1 + w[1],
            2.0 * q2 * a1 + q1 * w[1] + w[2] + w[3],
            4.0 * q2 * a1 + 2.0 * q1 * w[1] - 2.0 * w[2],
            4.0 * q3 * a1 + 3.0 * q2 * w[1] - 2.0 * q1 * w[2] + 2.0 * q1 * w[3] + w[4],
            q4 * a1 + q3 * w[1] - q2 * w[2] + q2 * w[3] + q1 * w[4] + w[5],
        };
    }

    private static void Secondary(Scalar[] t, Scalar a, Scalar ab, Scalar cc,
                                  Scalar q2a, Scalar qcc, Scalar qom, Scalar[] qs,
                                  Scalar[] d, Scalar[] s, bool standalone,
                                  Scalar[] total, Scalar[] bar, Scalar[] mid, Scalar piImage)
    {
        // Every barred entry is (84.42), s-bar_mu = q s_mu + the bracket in d6..d8, so the
        // only place q appears is the lift q s_mu. It is never formed as a product with q:
        // the caller supplies q a (as ab), q^2 a, q c13 and q omega already carried on the
        // incidences, and the lifts of the intrinsic six in qs. Each line below is the line
        // above it with every factor advanced one power of q, which is why they pair off.
        Scalar s1 = s[0] + 3.0 * a * d[0];
        Scalar q1 = qs[0] + 3.0 * ab * d[0];
        total[0] = s1;
        bar[0] = a * d[6] + q1;

        Scalar s2mid = 2.0 * (ab * d[0] + (d[1] + d[2]) * a)
                     + (standalone ? -t[8] * t[15] : 0.0) + s[1];
        Scalar q2mid = 2.0 * (q2a * d[0] + (d[1] + d[2]) * ab)
                     + (standalone ? -qom * t[15] : 0.0) + qs[1];
        mid[1] = s2mid;
        Scalar s2 = 2.0 * ab * d[0] + a * d[2] + s2mid;
        Scalar q2 = 2.0 * q2a * d[0] + ab * d[2] + q2mid;
        total[1] = s2;
        bar[1] = 2.0 * ab * d[6] + a * d[7] + q2;

        Scalar s3mid = 2.0 * (ab * d[1] + d[4] * a)
                     + (standalone ? -t[8] * t[16] : 0.0) + s[2];
        Scalar q3mid = 2.0 * (q2a * d[1] + d[4] * ab)
                     + (standalone ? -qom * t[16] : 0.0) + qs[2];
        mid[2] = s3mid;
        Scalar s3 = cc * d[0] + a * d[4] + s3mid;
        Scalar q3 = qcc * d[0] + ab * d[4] + q3mid;
        total[2] = s3;
        bar[2] = a * d[8] + cc * d[6] + q3;

        // t55 in W coordinates, VI Table I:  t55 = 2(pi'' t11 + t51 - t50) + t54, against the
        // paracanonical 2(t51 - t50) + t54. The whole modification is the one extra term
        // 2 pi'' t11, and because t55 enters s4 additively with coefficient one it shifts BOTH
        // members of the fourth pair by the same amount - which is how it was found. The barred
        // line takes the same term with every factor advanced one power of q, and t12 is the
        // q-advanced t11 up to the factor of two carried in its own definition.
        Scalar s4mid = 2.0 * (s3mid - s[2]) + s[3]
                     + (standalone ? 2.0 * piImage * t[11] : 0.0);
        Scalar q4mid = 2.0 * (q3mid - qs[2]) + qs[3]
                     + (standalone ? piImage * t[12] : 0.0);
        mid[3] = s4mid;
        Scalar s4 = 2.0 * ab * d[2] + s4mid;
        Scalar q4 = 2.0 * q2a * d[2] + q4mid;
        total[3] = s4;
        bar[3] = 2.0 * ab * d[7] + q4;

        Scalar s5mid = (standalone ? -(t[17] + t[18]) * t[8] : 0.0) + s[4]
                     + 2.0 * (ab * (d[3] + d[4]) + d[5] * a);
        Scalar q5mid = (standalone ? -(t[17] + t[18]) * qom : 0.0) + qs[4]
                     + 2.0 * (q2a * (d[3] + d[4]) + d[5] * ab);
        mid[4] = s5mid;
        Scalar s5 = 2.0 * ab * d[4] + cc * d[2] + s5mid;
        Scalar q5 = 2.0 * q2a * d[4] + qcc * d[2] + q5mid;
        total[4] = s5;
        bar[4] = 2.0 * ab * d[8] + cc * d[7] + q5;

        Scalar s6mid = 2.0 * (cc * t[19] - ab * t[24]) + s[5];
        Scalar q6mid = 2.0 * (qcc * t[19] - q2a * t[24]) + qs[5];
        mid[5] = s6mid;
        Scalar s6 = cc * d[4] + s6mid;
        Scalar q6 = qcc * d[4] + q6mid;
        total[5] = s6;
        bar[5] = cc * d[8] + q6;
    }

    /// <summary>Deformation coefficient, or zero where the surface carries none.</summary>

    /// <summary>
    /// One PASS of the tertiary totals: the intrinsic chain and the induced terms, computed
    /// from whatever is currently in <paramref name="t"/>.
    ///
    /// <para>M (85.3) makes each total i_p(hat bracket) + y_p(check bracket), and (85.5) says
    /// both brackets obey the SAME equations - only the surface's own figuring halves differ,
    /// the accumulated coefficients being common. So this is called twice, once with the hat
    /// halves of a_p, c13, the intrinsic secondary, the M family and z, and once with their
    /// check halves. Because the induced terms are PRODUCTS of accumulated quantities with
    /// the surface's own, a single pass with mixed inputs is not the sum of the two, which is
    /// why no reference-swap on the barred entry alone could ever be right.</para>
    /// </summary>
    private static void TertiaryPass(Scalar[] t, Scalar carry, Scalar[] outTotals,
                                     Scalar[] outBarred, bool carryIsInfinite = false,
                                     Scalar qT152 = default, Scalar[]? outIntrinsic = null,
                                     Scalar[]? outIntrinsicBar = null)
    {
        // M (26.2) gives b_p = 2 q a_p, and that is how b reaches Table I: as a bare ratio
        // beside an accumulated family member, the pair multiplying this surface own a. In the
        // CHECK half the primary is a_v and its partner is b_v = 2 q~ a_v - the height ratio,
        // not the incidence ratio - which is exactly what the SECONDARY already does, the
        // figured Secondary() call being handed rr * af where the spherical one gets q a_p.
        // The tertiary was still forming b on q in both halves.
        //
        // This is why tau1 alone was right. By (84.23) t1 = i_p(t1^ + A s1^) + S1 a is the one
        // tertiary coefficient with no b in it; t2 is the first to carry one, in S1 b + S2 a.
        // t132 holds no bare ratio and t135 onwards each hold one.
        //
        // In the hat pass carry is q, so every spherical result is bit-identical.
        Scalar bq = carry;

        t[134] = 6.0 * t[6] * t[121] + t[122];
        t[136] = 0.5 * (t[134] + t[122]) * t[6] + t[123] + t[124];
        t[138] = 4.0 * (t[136] - t[123] + t[124]);
        t[140] = (-2.0 * t[6] * t[134] + 4.0 * t[136] + t[138] + 8.0 * t[124]) * t[6] + t[125];
        t[143] = ((t[123] + t[124] - t[136]) * t[6] + 0.5 * t[140] + 0.5 * t[125]) * t[6] + t[126];
        t[145] = (2.0 / 3.0) * (4.0 * (t[124] - t[123]) * t[6] + t[140] - t[125]) + t[127];
        t[147] = (4.0 * (t[124] - t[123]) * t[6] + 3.0 * t[127] - 2.0 * t[125]) * t[6]
                 + t[128] - 4.0 * t[126] + 4.0 * t[143];
        t[149] = ((4.0 * t[6] * t[124] + t[125] + 0.75 * t[127] - 0.75 * t[145]) * t[6]
                  + t[147] + 2.0 * t[126] + t[128]) * t[6] + t[129];
        t[151] = ((t[6] * t[121] + t[122]) * t[6] + t[123] + 9.0 * t[124]) * t[6]
                 + t[125] + t[127];
        t[152] = ((t[6] * t[151] + t[126] + t[128]) * t[6] + t[129]) * t[6] + t[130];

        t[131] = 4.0 * ((-t[8] * t[15] + 2.0 * t[44]) * 0.125 * t[15] - t[20] * t[38]);
        t[132] = (t[25] * t[25] + 3.0 * t[101]) * t[10] + t[25] * t[40] + t[121] + t[131];
        t[133] = t[6] * t[132] + t[10] * t[115] + t[31] * t[40];

        // The pattern for every total: the intrinsic coefficient plus what this
        // surface inherits. t155 states it outright - 4 t19 t65 + t152 + t153 + t154,
        // with t152 the intrinsic - which is what fixes the reading of the others,
        // where Buchdahl writes the induced part as several rows ending in "+ Sum".
        t[135] = t[134]
               + 4.0 * (0.5 * (t[26] + t[27]) * t[25] + bq * t[101]
                        + 0.5 * t[102] + 0.75 * t[103]) * t[10]
               // Buchdahl writes this as 3[-(...)t8 + t44 t83], with the minus INSIDE
               // the bracket. Reading it as -3[(...)t8 + t44 t83] flips the sign of
               // the second term and throws the total out by more than a factor of two.
               + 3.0 * (-(t[15] * t[16] + t[69] / 3.0) * t[8] + t[44] * t[83])
               + 2.0 * (t[50] + t[54]) * t[15] + t[25] * t[45] + t[27] * t[40]
               - 4.0 * (t[21] + t[22]) * t[38];

        t[137] = t[136]
               + (2.0 * (bq * t[102] + t[25] * t[29]) + t[26] * t[26] + 3.0 * t[105]) * t[10]
               - (0.5 * t[16] * t[16] + t[15] * t[18] + t[70]) * t[8]
               + t[25] * t[51] + t[29] * t[40]
               + 2.0 * (-2.0 * t[23] * t[38] + t[50] * t[83]) + t[15] * t[59]
               + 0.5 * t[44] * t[84] + t[13] * t[101];

        t[139] = t[138]
               + (2.0 * (2.0 * bq * t[103] + t[25] * t[28] + t[26] * t[27])
                  + t[27] * t[27] + 2.0 * t[104] + 3.0 * t[108]) * t[10]
               - (4.0 * t[16] * t[16] + t[15] * t[17] + t[71]) * t[8]
               // Two further lines, which continue at the START of page 751 after the
               // first two end page 750. Missing them left T4 short by 1013 out of
               // 3477 - the induced part was less than half accounted for.
               + 4.0 * (-2.0 * t[23] * t[38] + t[16] * t[50]) + t[27] * t[45] + t[25] * t[55]
               + 2.0 * ((3.0 * t[16] - t[20]) * t[54] + t[15] * t[59])
               + (t[17] - 2.0 * t[21] - 3.0 * t[22]) * t[44];

        t[141] = t[140]
               + 2.0 * ((2.0 * t[105] + t[104]) * bq + t[107] + 1.5 * t[110]) * t[10]
                 + t[13] * t[103]
               + 2.0 * ((t[28] + t[29]) * t[26] + t[25] * t[30] + t[27] * t[29]) * t[10]
               - ((3.0 * t[18] + t[17]) * t[16] + t[15] * t[19] + t[72] + t[73]) * t[8]
               + (t[19] - 5.0 * t[23]) * t[44] + t[29] * t[45]
               + (3.0 * t[50] + t[54]) * t[84] + t[27] * t[51]
               + (5.0 * t[16] - t[20]) * t[59] + t[25] * t[61]
               + 4.0 * (t[15] * t[65] - t[24] * t[38]);

        t[144] = t[143]
               + (2.0 * (bq * t[107] + t[26] * t[30]) + t[29] * t[29]
                  + 3.0 * t[113]) * t[10]
               - (0.5 * t[18] * t[18] + t[16] * t[19] + t[74]) * t[8]
                 + t[29] * t[51] + t[25] * t[66]
               + 2.0 * (2.0 * t[16] * t[65] + t[50] * t[85])
                 - t[24] * t[44] + t[13] * t[105]
               + 0.5 * t[59] * t[84];

        t[146] = t[145]
               + 2.0 * ((2.0 * bq * t[108] + t[27] * t[28] + t[109]) * t[10]
                        + t[54] * t[84])
               - (2.0 * t[16] * t[17] + t[75]) * t[8] + t[27] * t[55]
               + 4.0 * (t[16] * t[59] - t[23] * t[44]);

        t[148] = t[147]
               - ((0.5 * t[17] + t[18]) * t[17] + t[76] + t[77]) * t[8] + t[13] * t[108]
               + 2.0 * (-t[8] * t[16] + t[54]) * t[19] + t[29] * t[55] + t[27] * t[61]
               + (2.0 * ((2.0 * t[110] + t[109]) * bq + t[28] * t[29]
                         + t[27] * t[30] + t[112]) + t[28] * t[28]) * t[10]
               + 8.0 * (-0.25 * ((2.0 * t[50] + 3.0 * t[54]) * t[23] + t[24] * t[44])
                        + t[16] * t[65])
               + (3.0 * t[17] + 2.0 * t[18] - t[22]) * t[59];

        t[150] = t[149]
               + 2.0 * ((2.0 * t[113] + t[112]) * bq + t[28] * t[30]
                        + t[29] * t[30] + t[114]) * t[10]
               - ((t[17] + t[18]) * t[19] + t[78] + t[79]) * t[8] + t[13] * t[110]
               + 4.0 * ((t[17] + t[18]) * t[65] + 0.75 * t[59] * t[85])
                 + t[29] * t[61] + t[27] * t[66]
               - 2.0 * (t[50] + t[54]) * t[24];

        t[153] = (2.0 * bq * t[114] + t[30] * t[30]) * t[10] + t[13] * t[113];
        t[154] = -(0.5 * t[19] * t[19] + t[80]) * t[8] + t[29] * t[66] - t[24] * t[59];
        t[155] = 4.0 * t[19] * t[65] + t[152] + t[153] + t[154];

        if (outIntrinsic != null)
        {
            // t121 is the first intrinsic outright; the rest are the chain t134..t152, each of
            // which its total is written as "that plus the induced terms".
            outIntrinsic[1] = t[121];  outIntrinsic[2] = t[134];  outIntrinsic[3] = t[136];
            outIntrinsic[4] = t[138];  outIntrinsic[5] = t[140];  outIntrinsic[6] = t[143];
            outIntrinsic[7] = t[145];  outIntrinsic[8] = t[147];  outIntrinsic[9] = t[149];
            outIntrinsic[10] = t[152];
            if (outIntrinsicBar != null)
                for (int k = 1; k <= 10; k++) outIntrinsicBar[k] = carry * outIntrinsic[k];
        }

        outTotals[1] = t[132];  outTotals[2] = t[135];  outTotals[3] = t[137];
        outTotals[4] = t[139];  outTotals[5] = t[141];  outTotals[6] = t[144];
        outTotals[7] = t[146];  outTotals[8] = t[148];  outTotals[9] = t[150];
        outTotals[10] = t[155];

        // M (84.23): read the barred entries off the unbarred ones by replacing the intrinsic
        // part with the ratio times itself and the intermediate coefficients by their barred
        // forms. With homogeneous inputs the ratio is a single scalar for the pass - q for the
        // hat half, q-tilde for the check - which is what makes the reference run of the old
        // arrangement unnecessary.
        outBarred[1] = carry * t[132] + t[10] * t[115] + t[31] * t[40];
        outBarred[2] = carry * (2.0 * t[10] * t[115] + t[135])
                     + t[10] * t[116] + t[31] * t[45] + t[32] * t[40];
        outBarred[3] = carry * t[137]
                     + t[10] * t[117] + t[13] * t[115] + t[31] * t[51] + t[33] * t[40];
        outBarred[4] = carry * (2.0 * t[116] * t[10] + t[139])
                     + t[118] * t[10] + t[31] * t[55] + t[32] * t[45];
        outBarred[5] = carry * (2.0 * t[117] * t[10] + t[141])
                     + t[119] * t[10] + t[13] * t[116]
                     + t[31] * t[61] + t[32] * t[51] + t[33] * t[45];
        outBarred[6] = carry * t[144]
                     + t[10] * t[120] + t[13] * t[117] + t[31] * t[66] + t[33] * t[51];
        outBarred[7] = carry * (2.0 * t[10] * t[118] + t[146]) + t[32] * t[55];
        outBarred[8] = carry * (2.0 * t[10] * t[119] + t[148])
                     + t[13] * t[118] + t[32] * t[61] + t[33] * t[55];
        outBarred[9] = carry * (2.0 * t[10] * t[120] + t[150])
                     + t[13] * t[119] + t[32] * t[66] + t[33] * t[61];
        // Where the ratio is infinite, q t155 cannot be formed as a product. t155 differs from
        // t152 by 4 t19 t65 + t153 + t154, which a surface with nothing accumulated ahead of it
        // does not have; the remainder keeps the plain product, so nothing else moves.
        Scalar carriedT155 = carryIsInfinite
                           ? qT152 + carry * (t[155] - t[152])
                           : carry * t[155];
        outBarred[10] = carriedT155 + t[13] * t[120] + t[33] * t[66];
    }

    private static Scalar Coefficient(Models.Surface s, int index) =>
        index >= 0 && index < s.AsphericCoefficients.Length ? s.AsphericCoefficients[index] : 0.0;
}
