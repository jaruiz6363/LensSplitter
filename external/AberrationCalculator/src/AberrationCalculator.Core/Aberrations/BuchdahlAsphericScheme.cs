using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The tertiary coefficients of a FIGURED system, by M Sec. 85 - the aspherical form of the
/// condensed iteration - written as a routine of its own.
///
/// <para><b>Why this is separate from <see cref="BuchdahlTableI"/>.</b> The spherical seventh
/// order there is established: twenty tau against Buchdahl's own printed numbers, against an
/// independent implementation, and against Forbes' series trace to 2E-13 at both conjugates.
/// None of that is in question and none of it is touched here. What is in question is the
/// ASPHERICAL arrangement, which Buchdahl specifies in Sec. 85 but never published an arranged
/// table for, and which is the one part of this subject with no printed answer to check
/// against. Changing the working scheme to chase it would put the established half at risk of
/// the same experiment; so this routine takes the scheme's per-surface quantities - which are
/// right, and are shared - and does the tertiary arrangement over again on its own terms.</para>
///
/// <para><b>Where the defect is, measured rather than argued.</b> The ladder fixtures switch
/// the induced terms on and off (see <c>AsphericInducedLadderTests</c>), and a second control
/// says how much work the figuring is doing on each rung. Ranked by error per unit of that
/// work, the rungs separate cleanly:</para>
///
/// <code>
///   one powered surface, any figuring    no induced     0.03 - 0.13 %   (the oracle's floor)
///   figured SPHERE, two surfaces         induced        0.006 - 0.008 %
///   r^4 figured, two surfaces            induced        1.3 - 7.8 %
///   figured flat                         induced        28 - 61 %
/// </code>
///
/// <para>A figured sphere in Buchdahl's sense (Sec. 66a) is figuring built so that
/// <c>8 A4 + K c^3 = 0</c> - its PRIMARY contribution vanishes while its sixth-order content
/// does not. Those rungs carry as much figuring through the induced stage as the others and
/// come out at the ray oracle's own noise floor, some sixty times better per unit of figuring
/// than the r^4 rungs. So the induced stage does not fail on figuring as such: <b>it fails on
/// the figuring's PRIMARY content</b>, and that is what this routine is aimed at.</para>
///
/// <para><b>What Sec. 85 requires.</b> (60.3) replaces the spherical <c>dLambda = I D</c> with
/// <c>dLambda = D I + L Y</c>: a figured surface has a second half that rides the ray HEIGHT
/// where the first rides the incidence. Every coefficient splits into a hat half and a check
/// half, the two are carried down the chain on their own ratios - <c>q = i_q/i_p</c> for the
/// hat, <c>q~ = y_q/y_p</c> for the check - and they are added only at the end. Sec. 85 is
/// all-or-nothing: every coefficient splits or none does, and a computation that splits some
/// and not others is worse than one that splits neither.</para>
///
/// <para><b>On a spherical surface the check half is identically zero</b>, L being zero, so
/// this routine must reproduce <see cref="BuchdahlTableI"/> exactly on a system of spheres.
/// That is the gate it is built against, not an afterthought - see
/// <c>BuchdahlAsphericSchemeTests</c>.</para>
/// </summary>
public static class BuchdahlAsphericScheme
{
    /// <summary>
    /// Which ratio the check pass carries where the arrangement uses one.
    ///
    /// <para>These are readings of Sec. 85, not parameters to be tuned. Each names a site where
    /// the text is compatible with more than one arrangement and the ladder can say which,
    /// which is how the (Y) family was settled. They are here so that the reading in force is
    /// visible and can be changed in one place when the source page that settles it is read;
    /// they are not a fit, and none of them has a continuous knob.</para>
    /// </summary>
    public sealed record Options
    {
        /// <summary>
        /// Whether the INTRINSIC chain - t134..t152, which builds the surface's own tertiary
        /// from its z quantities - runs on the pass's own ratio in the check half, or on the
        /// incidence ratio q in both halves.
        ///
        /// <para><c>false</c> reproduces <see cref="BuchdahlTableI"/> exactly and is the
        /// starting point, so that any move can be attributed. <c>true</c> is what Sec. 85
        /// requires if the chain is read as part of the split, which is how the SECONDARY is
        /// already treated: the figured half of <c>Secondary()</c> runs on the height ratio.
        /// </para>
        /// </summary>
        public bool IntrinsicChainOnPassRatio { get; init; }

        /// <summary>
        /// Whether the FIGURED half of the accumulated primary travels on the height ratio
        /// wherever it is combined into a family, rather than on the ratio of the family that
        /// is combining it.
        ///
        /// <para><b>What this is aimed at.</b> On <c>Ladder2_A4_First</c> the figured surface is
        /// the first powered one and the second is a sphere. A sphere has no check half, so the
        /// second surface's whole contribution comes from the HAT pass, and the only figured
        /// thing it can see is what has accumulated ahead of it. That rung is wrong by 1.3 per
        /// cent. The fault therefore has to be in how accumulated figuring reaches a pass, and
        /// nowhere else - there is no other route by which it could reach that surface.</para>
        ///
        /// <para><b>Why the height ratio.</b> (60.3) splits the surface contribution as
        /// <c>D I + L Y</c>. The figuring is the L half, whose components go as the ray heights,
        /// so its chain ratio is <c>q~ = y_q/y_p</c>; that is already why the figured primary's
        /// barred partners are formed on rho rather than on q, and why the figured half of the
        /// secondary runs on rho. What this reading says is that the L half keeps that ratio
        /// when it is being combined by (84.15) as well - it does not acquire the incidence
        /// ratio by being accumulated into a sum that a later surface reads.</para>
        ///
        /// <para><b>It is already in the scheme, for one entry.</b> <c>t24</c> is a recursion
        /// carrying the previous surface's ratio, and its figured half is accumulated on that
        /// surface's rho rather than its q - the working scheme does this and records that the
        /// fifth and sixth secondary coefficients were wrong without it. This reading is that
        /// same treatment carried to the rest of the family instead of to one entry of it.</para>
        ///
        /// <para>It cannot move a sphere, which has no figured half, and it cannot move a
        /// figured SPHERE in Buchdahl's sense, whose figured primary is zero by construction -
        /// which is exactly the pattern the ladder shows.</para>
        /// </summary>
        public bool AccumulatedFiguringOnHeightRatio { get; init; }

        /// <summary>
        /// DIAGNOSTIC, not a parameter. How much of the height-ratio shift above to apply:
        /// 0 is the arrangement as built, 1 is the full swap.
        ///
        /// <para>It exists to answer one question that cannot be answered by trying readings one
        /// at a time - whether a SINGLE number applied at this one site can bring all twenty tau
        /// to the rays at once. If it can, the site and the structure are right and the number
        /// names which reading; if the twenty disagree about where the zero is, the structure is
        /// wrong and no reading of this site will fix it. Fitting it and shipping the fitted
        /// value would be worthless - a constant chosen to make one ladder agree tells you
        /// nothing about the next design.</para>
        /// </summary>
        public Scalar HeightRatioShiftFraction { get; init; } = 1.0;

        /// <summary>
        /// Whether the WHOLE of a figured surface's barred secondary travels on the height ratio
        /// in the dagger recursions of the surface after it, rather than only the part of it
        /// that is that surface's own height ratio applied once.
        ///
        /// <para><b>This is the site the ladder points at.</b> The dagger family t102, t104,
        /// t107, t109, t112 and t114 each carry a correction on the previous surface's
        /// <c>rho - q</c>, and the working scheme applies it to the LIFT half of the figured
        /// barred secondary alone. Its own note says why the rest is left out - the remainder is
        /// an induced bracket "every term of which carries the figured primary and so vanishes
        /// with c1" - and that sentence is the ladder's finding stated in advance: a figured
        /// SPHERE has c1 = 0, so for it the two halves coincide and the scheme is right to
        /// 0.006 per cent; an r^4 figuring has c1 != 0, so for it a term that should travel on
        /// the height ratio travels on the incidence ratio instead, and the scheme is wrong by
        /// 1.3 to 7.8 per cent.</para>
        ///
        /// <para>The note records that two earlier attempts on this measured worse. Both
        /// ESTIMATED the induced half - by shadowing the accumulations, or by differencing a
        /// spherical twin. This one does not estimate it: the figured barred secondary and its
        /// lift half are both already on the row, so their difference is the bracket exactly.
        /// </para>
        ///
        /// <para>It cannot move a sphere, which has no figured secondary, nor a figured sphere,
        /// whose bracket vanishes identically - so it is inert on every rung that is already
        /// right, by construction rather than by luck.</para>
        /// </summary>
        public bool FullFiguredBarredSecondaryInDagger { get; init; }

        /// <summary>
        /// The second member of M (68.8)'s <c>(q~ - q)</c> group, which the scheme drops.
        ///
        /// <para>M p.116 gives the barred first secondary outright:</para>
        ///
        /// <code>
        ///   s-_1p = q s_1p - ['A_q - q('A-_p + 'A_q) + q^2 'A_p] a_p
        ///                  + (q~ - q){ y_p s^v_1p + c-_1 y_p^4 [('A-_p - 2'A_q)
        ///                                                       + (2q~ - q)'A_p] }
        /// </code>
        ///
        /// <para>with <c>alpha = c-_1 y_p^4</c> by (67.1). The group in <c>q~ - q</c> has TWO
        /// members. The scheme carries the first - that is <c>SecBarFigLift</c>, the check
        /// secondary - and drops the second, which is <b>alpha times a bracket of accumulated
        /// primaries</b>. Buchdahl's own sentence after the equation says the <c>q~ - q</c> term
        /// "is characteristic of the complications referred to at the end of Sec. 65".</para>
        ///
        /// <para><b>This is the ladder's finding in print.</b> The dropped member carries alpha,
        /// which vanishes identically for figuring with <c>c_1 = 0</c> - a figured sphere - and
        /// not otherwise. That is precisely the class the ladder separates: figured spheres
        /// right to 0.006 per cent, r^4 figuring wrong by 1.3 to 7.8.</para>
        ///
        /// <para>Printed for <c>s_1p</c> only. The other five are said to follow from (29.8),
        /// which is not on these pages, so this reading supplies the first alone and leaves the
        /// rest as they are rather than guessing at them by pattern.</para>
        ///
        /// <para><b>The transcription is pinned.</b> The three accumulations this forms the
        /// bracket from - <c>t16</c>, <c>t20</c> and <c>t15</c> as <c>'A-_p</c>, <c>'A_q</c> and
        /// <c>'A_p</c> - are established by measurement in
        /// <c>BuchdahlSymbolDictionaryTests</c>: M (68.6), two equations earlier on the same
        /// page, closes at 2.3E-16 with exactly that reading. They were once thought to be
        /// guessed, and this reading was withdrawn as void on that ground; the withdrawal was
        /// wrong and the reading stands as measured.</para>
        ///
        /// <para>What IS misprinted in (68.8) is its spherical part, whose standalone
        /// <c>'A_q</c> has to be <c>'A-_q</c> for the equation to agree with Buchdahl's own
        /// Table I. That half of the equation is not used here, so the misprint does not touch
        /// this reading - see <c>TheStandaloneTermOf688IsBarred</c>.</para>
        ///
        /// <para><b>THE LADDER CANNOT TEST THIS, and that is the finding.</b> The bracket is
        /// built from what has accumulated AHEAD of the figured surface, and it is read by the
        /// surface AFTER it. Every ladder rung fails one of those two conditions:
        /// <c>Ladder2_A4_First</c> figures the first powered surface, so the accumulations in
        /// the bracket are all zero; <c>Ladder2_A4_Second</c> figures the last, so no surface
        /// ever reads it. The bracket is identically zero on all seventeen rungs, and the
        /// reading moves nothing. On the triplets, where it is not zero, it moves the rms by a
        /// per cent or two in either direction depending on its sign - which settles nothing
        /// either.</para>
        ///
        /// <para><b>What is missing is a fixture, not a reading:</b> a figured surface with
        /// powered surfaces both before and after it, which no design in
        /// <c>tests/fixtures/lenses</c> provides in isolation. Until that exists this reading
        /// cannot be confirmed or refuted, and it is left off.</para>
        /// </summary>
        public bool Equation688BracketInDagger { get; init; }

        /// <summary>
        /// Whether the dagger recursions carry the figured half of the WHOLE increment on the
        /// height ratio, rather than the lift half of the barred secondary alone.
        ///
        /// <para><b>The scheme's own note states the requirement and then gives up on it.</b>
        /// Each of the six recursions multiplies an increment in the accumulated q-side
        /// secondary by the PREVIOUS surface's q; that increment already carries the surface's
        /// own ratio inside it, M (65.7) having put the figured half of a q-side quantity on the
        /// height ratio, so multiplying by q a second time gives the figured half <c>q q~</c>
        /// where it should have <c>q~</c> squared. The remedy is one extra product per line, on
        /// the INCREMENT rather than the accumulation - the same correction <c>t24</c> makes one
        /// order down.</para>
        ///
        /// <para>The note then says the increment's figured half "is exactly the previous
        /// surface's own figured barred secondary", and adds that the closed form for t86 also
        /// "carries other figured content through products of accumulations, but a product has no
        /// additive figured half to speak of, and taking one - by shadowing the accumulations or
        /// by differencing a spherical twin - measures worse on every multi-surface design
        /// tried".</para>
        ///
        /// <para><b>Both halves of that are wrong.</b> A product of accumulations has a perfectly
        /// definite additive figured half, because M (67.1-2) splits every accumulation exactly:
        /// for <c>X = X_s + X_f</c> and <c>Y = Y_s + Y_f</c> the figured half of <c>XY</c> is
        /// <c>XY - X_s Y_s</c>, which needs no shadowing and no twin system - both halves of
        /// every accumulation are already on the row. And the measurement that rejected it was
        /// made with the instrument that divided each error by the largest coefficient in the
        /// set, which is blind to this arrangement's actual failures: the two designs it quotes
        /// at 5.29 and 5.30 per cent are at 467 and 284 per cent when each coefficient is asked
        /// about on its own terms.</para>
        ///
        /// <para>So this evaluates the six q-side secondaries twice - once as they stand, once
        /// with every accumulation reduced to its spherical half - and differences them. On a
        /// sphere, and on any figuring whose primary contribution vanishes, the two evaluations
        /// are identical and the correction is exactly zero.</para>
        /// </summary>
        public bool DaggerIncrementFiguredHalfOnHeightRatio { get; init; }

        /// <summary>
        /// Whether the dagger recursions carry NO figured correction at all - the opposite of
        /// every other reading here.
        ///
        /// <para><b>Why this follows from the derivation.</b> The figured barred secondary is now
        /// known exactly and shown to be what the scheme computes:</para>
        /// <code>
        ///   s-_mu^fig = q D_mu + q~ L_mu + alpha (bracket_mu)
        /// </code>
        /// <para>It ALREADY carries the incidence ratio on its D half and the height ratio on its
        /// L half. The scheme's note argues that the increment "already carries surface i-1's own
        /// ratio inside it ... so multiplying by q a second time gives the figured half q q~ where
        /// it should have q~ squared", and subtracts <c>(q~ - q)</c> times the lift to repair it.
        /// But if the accumulated barred secondary is already right, the recursion's q is doing
        /// the same job for the figured half that it does for the spherical one, and there is
        /// nothing to repair - the subtraction is then an over-correction rather than a
        /// correction.</para>
        ///
        /// <para>Five readings have tried to put MORE into this site, and every one was adding a
        /// second copy of something already present. This asks the question the other way round.
        /// </para>
        /// </summary>
        public bool NoFiguredCorrectionInDagger { get; init; }

        /// <summary>
        /// Whether the CLOSED FORMS for the q-side secondaries evaluate their products of
        /// accumulations on the spherical halves alone, the figuring reaching them only through
        /// the accumulated barred secondary they already carry.
        ///
        /// <para><b>Why this site, now that the others are cleared.</b> The figured secondary is
        /// verified against (68.8). The dagger correction is confirmed from both sides - adding
        /// alpha content to it breaks the r^4 rungs, removing it breaks the figured-sphere rungs
        /// that were exact. What is left in the chain between a figured surface and the surface
        /// that reads it is <c>t86</c> and its five partners, which express the accumulated
        /// q-side secondary as products of the accumulated PRIMARIES.</para>
        ///
        /// <para>Those closed forms are identities for a spherical system, where each surface's
        /// q-side primary is its p-side one carried on <c>q</c>. For a figured surface it is
        /// carried on <c>q~</c> instead - that is the whole of (67.2) - so the identity that
        /// justifies the closed form does not hold for the figured content, and the products
        /// carry it anyway.</para>
        ///
        /// <para><b>It predicts the ladder exactly.</b> A figured sphere has <c>alpha = 0</c>, so
        /// the primary accumulations have no figured content at all, the products are untouched,
        /// and those rungs stay exact - which they are, 0 of 20. An r^4 figuring puts alpha into
        /// the accumulations and the products mis-carry it - and those rungs are the broken ones.
        /// No other candidate left standing distinguishes the two classes this way.</para>
        /// </summary>
        public bool QSideProductsOnSphericalHalves { get; init; }

        /// <summary>
        /// Whether the BARRED members of the (Y) secondary-order family are formed from the same
        /// accumulations as their (I) partners, with only the ratio changed - which is what
        /// (85.1) says, if it governs them at all.
        ///
        /// <para><b>Derived rather than guessed at.</b> M (84.24) gives the pattern one order up,
        /// <c>q1 = i_p(q1^ + A_(I) t1^ + S1_(I) s1^) + T1_(I) a</c>, so the family carried at each
        /// order pairs with the intrinsic of the order below: <c>t101..t114</c> is the
        /// SECONDARY-order family <c>S_(I)</c>. Its dagger is formed exactly as the primary one
        /// is - <c>t115 = -q t101 + t102</c> against <c>t31 = -q t25 + t26</c> - which identifies
        /// <c>t102</c> as the BARRED member <c>S-1_(I)</c>.</para>
        ///
        /// <para>The primitive is <c>t101 = q t69 - 'S1_q</c> on the unbarred accumulation, and
        /// the scheme's own <c>+ q t70</c> term shows <c>t102 = q t70 - 'S-1_q</c> on the barred
        /// one. (84.15) and (85.1) then give the (Y) member from the SAME two accumulations with
        /// <c>q~</c> in place of <c>q</c>, so</para>
        /// <code>
        ///   Y102 = q~ t70 - 'S-1_q = t102 + (q~ - q) t70
        /// </code>
        /// <para>and likewise Y104, Y107, Y109, Y112 and Y114 on t72, t74, t76, t78 and t80. The
        /// scheme instead rebuilds each recursion with the previous surface's rho and its own
        /// previous (Y) value, which constructs a DIFFERENT <c>'S-1_q</c> - and 'S-1_q is a q-side
        /// accumulation, not a family member, so (85.1) says both families share it.</para>
        ///
        /// <para>The primitive members already do exactly this: <c>Y101 = q~ t69 - t86</c> is
        /// <c>t101 + (q~ - q) t69</c> written out. This is that same rule applied to the barred
        /// members, which is the one place the scheme departs from it.</para>
        ///
        /// <para><b>It was tried before and rejected on the wrong instrument.</b> The working
        /// notes record this exact form measuring worse - "Ladder2_A4_Second goes from 6.85 to
        /// 10.59 per cent" - but those are share-of-the-largest figures, and that design is at
        /// 39.9 per cent when each coefficient is asked about on its own terms.</para>
        /// </summary>
        public bool YBarredFromSharedAccumulations { get; init; }

        /// <summary>
        /// Whether the accumulated q-side barred secondary splits the q-side PRIMARY it carries,
        /// pairing its figured half with the (Y) dagger as Sec. 85 requires - and, with that
        /// fixed, whether the (Y) barred members follow from the (I) ones by (85.1).
        ///
        /// <para><b>The derivation.</b> <c>t102 = q t70 - 'S-1_q</c> inverts, and substituting
        /// the scheme's recursion and the same relation one surface back gives</para>
        /// <code>
        ///   'S-1_q|i = 'S-1_q|i-1 + q dS1_q + t31 t99 + (q~ - q) lift     (at surface i-1)
        /// </code>
        /// <para>so each surface contributes <c>s-1q = q s1q + a_q t31 + ...</c> - the q-side
        /// mirror of the p-side relation the dictionary verified at 1.6E-15,
        /// <c>s-1p = q s1p + a_p t31</c>, with <c>t99 = a_q</c> where that has <c>a_p</c>.</para>
        ///
        /// <para><b>And there is the defect.</b> <c>a_q</c> has a figured half - <c>q~ alpha</c>
        /// by (67.2) - and Sec. 85 requires the check half of a surface's own quantity to pair
        /// with the (Y) family, not the (I) one. The scheme pairs the whole of <c>a_q</c>, and of
        /// <c>b_q = t100</c>, with the (I) daggers t31, t32, t33. Every site is a product of one
        /// of those daggers with t99 or t100, and each is corrected by the difference of the two
        /// daggers times the figured half alone.</para>
        ///
        /// <para>Because <c>'S-1_q</c> is an accumulation shared by BOTH families rather than a
        /// family member, the correction belongs in the (I) recursion as much as the (Y) one -
        /// so unlike <see cref="YBarredFromSharedAccumulations"/> this reaches the HAT pass, and
        /// can move the rungs whose figuring is on the first powered surface. Nothing tried so
        /// far has moved those at all.</para>
        ///
        /// <para>The two halves are applied together because they are one correction: with
        /// <c>'S-1_q</c> shared and correct, (85.1) gives the (Y) member as
        /// <c>q~ t70 - 'S-1_q</c> outright, which is the earlier reading's form over a corrected
        /// accumulation.</para>
        /// </summary>
        public bool SharedQBarWithSplitPrimary { get; init; }

        /// <summary>
        /// DIAGNOSTIC. Suppress, in the CHECK half only, the terms of the barred tertiary that
        /// pair the surface's own primary with the tertiary-order family - <c>t10 x t115</c> and
        /// its partners.
        /// </summary>
        /// <remarks>
        /// The barred rule derived from (85.3) is <c>t-_1 = q t_1 + t31 s^_1 + t115 a^</c>, which
        /// is the code's structure exactly, so the formula is not at fault. Its check half has
        /// two families of term - own primary times tertiary family, and secondary dagger times
        /// own secondary - and on <c>Ladder2_A4_Second</c> the whole error of T-bar1 is in that
        /// one check half, every other contribution to it being spherical. Suppressing each
        /// family in turn therefore says which carries the error, with no other design's
        /// behaviour mixed in. Suppression is not a correction and is never a candidate
        /// arrangement; it is a probe.
        /// </remarks>
        public bool DropOwnPrimaryTimesTertiaryFamilyInCheckBarred { get; init; }

        /// <summary>DIAGNOSTIC. The other family: secondary dagger times the surface's own
        /// secondary, <c>t31 x t40</c> and its partners, in the check half only.</summary>
        public bool DropDaggerTimesOwnSecondaryInCheckBarred { get; init; }

        /// <summary>
        /// Whether the check half's barred tertiary pairs its dagger with the INTRINSIC first
        /// secondary <c>t38</c> rather than with <c>t40 = t38 + 2 a A_(I)</c>.
        ///
        /// <para><b>Where the candidate comes from.</b> Barring (85.3) gives</para>
        /// <code>
        ///   t-_1 = q t_1 + t31 s^_1 + t115 a^
        /// </code>
        /// <para>in which the dagger multiplies the INTRINSIC secondary. The code multiplies
        /// <c>t40</c>, which is that intrinsic plus a family term - <c>2 a A_(I)</c> in the hat
        /// half, <c>2 alpha A_(Y)</c> in the check. The probe
        /// <c>WhichTermOfTheBarredRuleCarriesDefectTwo</c> shows this is the term carrying the
        /// error: suppressing it takes T-bar1 from 79 to 15 per cent on the two designs that
        /// figure their last powered surface, while suppressing the other family makes things
        /// slightly worse.</para>
        ///
        /// <para><b>The objection to it, which is why it is measured and not assumed.</b> The
        /// same <c>t40</c> is verified in the HAT half by the published tertiary totals, and
        /// (85.5) says the bracketed factors are the same in both halves - "for the lengthy
        /// factors, composed of the intermediate coefficients, in the equations for g^(m) and
        /// gv(m) respectively are the same". A reading that used t38 in one half and t40 in the
        /// other would contradict that unless the difference is absorbed elsewhere. So this is
        /// a test of a candidate, not a proposal.</para>
        ///
        /// <para>Only the first secondary can be tested this way: <c>t40</c> is the one of the
        /// six whose relation to its intrinsic counterpart is explicit. The others - t45, t51,
        /// t55, t61, t66 - are Secondary's "mid" quantities, which are not the intrinsic plus a
        /// family term, so no equivalent substitution exists for them.</para>
        /// </summary>
        public bool IntrinsicSecondaryInCheckBarred { get; init; }

        /// <summary>
        /// Whether the figured intrinsic secondary is split between the halves by (60.3) - its
        /// D part into the HAT half, its L part into the check - rather than going wholly into
        /// the check half.
        ///
        /// <para><b>Why this is the candidate.</b> (60.3) is <c>dLambda = D I + L Y</c>: the D
        /// part rides the incidence and the L part the height, and (85.3) puts the hat
        /// quantities on <c>i_p</c> and the check on <c>y_p</c>. So a figuring's D half belongs
        /// in the HAT half. The scheme loads the whole figured secondary - D and L together -
        /// into the check pass. It knows about the split: it forms <c>SecondaryDHalf</c> and uses
        /// it to correct the BARRED secondary by <c>(q - q~) D</c>, which is the same fact
        /// applied in one place and not the other.</para>
        ///
        /// <para><b>It predicts the blindness exactly.</b> The suspect product is
        /// <c>Y31 x s^v_1</c>, and every passing gate is blind to it:</para>
        /// <list type="bullet">
        /// <item>one powered surface - <c>t15</c> and <c>t20</c> are zero, so every term carrying
        /// <c>t38</c> vanishes and <c>Ladder1_A4</c> cannot see it whatever it holds;</item>
        /// <item>figured SPHERE - if the D half is proportional to <c>c1</c> it is zero there,
        /// so the split is a no-op and those rungs stay exact;</item>
        /// <item>r^4 figuring with something accumulated ahead - neither escape applies, and
        /// those are precisely the broken rungs.</item>
        /// </list>
        ///
        /// <para>The total is preserved either way, <c>(sph + D) + (fig - D) = sph + fig</c>, so
        /// nothing moves on a design where the halves are not separately used.</para>
        /// </summary>
        public bool FiguredSecondarySplitByDandL { get; init; }

        /// <summary>
        /// DIAGNOSTIC. Give the DIRECT uses of the accumulated primaries - t15..t24 as they
        /// appear in the induced terms t131, t135 and the rest - their spherical halves only,
        /// leaving the (I) and (Y) family members built from the full ones.
        ///
        /// <para>The accumulated figured primary reaches a downstream surface by two routes: it
        /// is combined into the family by (84.15), and it appears directly in the induced terms
        /// as products with the surface's own quantities. On <c>Ladder2_A4_First</c> the whole
        /// error is in one spherical surface's HAT pass, where those are the only two routes
        /// figuring can take at all - so suppressing one says which carries it. A probe, not a
        /// candidate arrangement: the halves do not sum to the total under it.</para>
        /// </summary>
        public bool SphericalAccumulationsInDirectUses { get; init; }

        /// <summary>
        /// Whether the LIFT the dagger recursions carry is split by (60.3) as well - <c>q D +
        /// q~ L</c> rather than <c>q~ (D + L)</c>.
        ///
        /// <para><b>The same fact one place further on.</b> The figured barred secondary is
        /// <c>q D + q~ L + alpha(...)</c> - derived, verified at 1E-10, and the reason the
        /// scheme adds <c>(q - q~) D</c> to it. Its LIFT half, which the dagger recursions carry
        /// on <c>(q~ - q)</c>, is formed as <c>rr * tF</c>, that is <c>q~ (D + L)</c>. If the
        /// barred secondary splits that way then so does its lift, and the two differ by
        /// <c>(q~ - q) D</c>.</para>
        ///
        /// <para><b>It is live exactly where nothing else is.</b> The correction it changes is
        /// non-zero only when the PREVIOUS surface is figured, and it scales with D, which is
        /// proportional to <c>c1</c>. So it cannot move a figured sphere, and it CAN move
        /// <c>Ladder2_A4_First</c> and <c>Ladder3_A4_First</c> - the rungs whose figuring is on
        /// the first powered surface, which no reading so far has touched at all, because every
        /// other correction needs something accumulated ahead of the figured surface and there
        /// is nothing.</para>
        ///
        /// <para><b>REFUTED.</b> It moves those rungs the wrong way - <c>Ladder2_A4_First</c>
        /// from 31.1 to 103.2 per cent and <c>Ladder3_A4_First</c> from 233.5 to 352.5 - and
        /// costs the Cooke triplet 467 to 522. The selectivity was right and the direction
        /// wrong, which by now is a familiar shape. Kept for the record, and because the
        /// argument for it remains the best one anybody has for that site.</para>
        ///
        /// <para>NOTE it cannot be combined with <see cref="SharedQBarWithSplitPrimary"/> or
        /// <see cref="YBarredFromSharedAccumulations"/>: those ASSIGN the dagger entries where
        /// this one adds to them, so setting both silently discards this. A combined column in
        /// the survey is therefore not what it claims and should not be read.</para>
        /// </summary>
        public bool LiftSplitByDandL { get; init; }

        /// <summary>
        /// Whether the dagger correction carries the figured half of the INCREMENT ALONE - the
        /// per-surface q-side secondary - rather than the p-side lift standing in for it.
        ///
        /// <para><b>Derived, not guessed.</b> Inverting <c>'S-_1q = q t70 - t102</c> through the
        /// scheme's recursion gives, per surface,</para>
        /// <code>
        ///   s-1q = q s1q + a_q t31 + (q~ - q) lift
        /// </code>
        /// <para>whose first two terms are the exact q-side mirror of the p-side relation the
        /// dictionary verified at 1.6E-15, <c>s-1p = q s1p + a_p t31</c>, with <c>t99 = a_q</c>
        /// standing where that has <c>a_p</c>. The third has no spherical counterpart; its job,
        /// in the scheme's own words, is that the figured half of the increment "already carries
        /// surface i-1's own ratio inside it ... so multiplying by q a second time gives the
        /// figured half q q~ where it should have q~ squared". The repair to that is
        /// <c>+(q~ - q)</c> times the FIGURED HALF OF s1q - and <c>lift</c>, which is
        /// <c>q~</c> times the figured P-SIDE secondary, is a stand-in for it.</para>
        ///
        /// <para><b>How this differs from reading 5</b>, which used the figured half of the whole
        /// bracket <c>prev70 + s1q</c> and was refuted at 467 to 10252 per cent: it drops
        /// <c>prev70</c>. The scheme's note argues for exactly that - "setting the increments
        /// aside ... the previous q there is a summation partner that cancels rather than a
        /// per-surface tag, and correcting it as though it were one costs a factor of ten". That
        /// factor of ten is what reading 5 paid.</para>
        ///
        /// <para>The figured half of <c>s1q</c> is had by differencing the figured half of
        /// <c>'S1_q</c> between surfaces, which <see cref="QSideHalves"/> computes exactly -
        /// every accumulation entering the closed form is split by (67.1-2), so the products
        /// have definite halves.</para>
        /// </summary>
        public bool DaggerCorrectionOnIncrementAlone { get; init; }

        /// <summary>
        /// Whether the barred members of the secondary-order family take the accumulated barred
        /// q-side secondary from M Sec. 22's identities instead of from the dagger recursion.
        ///
        /// <para><b>Why this is possible at all.</b> M Sec. 19: "when all the p-coefficients are
        /// known, all but one of the q-coefficients ... can be obtained from them by means of the
        /// identities". <see cref="BuchdahlSecondaryQ"/> recovers <c>Sbar_1q .. Sbar_5q</c> that
        /// way, and the (I) members are <c>t102 = q t70 - 'S-1_q</c> and partners - which closes
        /// to 1E-15 on every spherical fixture, so the correspondence is measured, not assumed.
        /// </para>
        ///
        /// <para><b>Why it is aimed here.</b> The identities are properties of the characteristic
        /// function, not of spheres, and on figured systems they reproduce the closed form for
        /// <c>t86</c> to 1E-14 on every surface - so the q-side accumulation is right and its
        /// increment IS the per-surface contribution. What the recursion builds from it is not:
        /// the barred accumulation it reaches differs from the identities' by 24 per cent in the
        /// first member on <c>Ladder2_A4_First</c>, and by tens of per cent on the triplets. The
        /// figured-sphere rungs agree to 5E-16, which is why they were exact all along.</para>
        ///
        /// <para>(85.1) gives the (Y) member from the same accumulation on the height ratio, so
        /// both families are set. <c>Sbar_6q</c> is the one the identities cannot supply, so
        /// <c>t114</c> keeps the recursion.</para>
        ///
        /// <para><b>MEASURED - DEFECT 3 IS HERE.</b> Worst relative error against Forbes / count
        /// over one per cent:</para>
        /// <code>
        ///   design                    as-built   identities   + D-half
        ///   Ladder2_A4_First           31.1/ 8     0.2/ 0      0.2/ 0
        ///   Ladder2_A4_First_FlatRear   5.9/ 7     0.2/ 0      0.2/ 0
        ///   Ladder2_A4_Then_FigSphere  31.2/ 8     0.2/ 0      0.2/ 0
        ///   Ladder3_A4_First          233.5/ 6     0.6/ 0      0.6/ 0
        ///   Ladder3_A4_Middle          18.7/12    20.5/ 4     13.5/ 2
        ///   Ladder2_A4_Second          39.9/19   520.7/18    172.2/16
        ///   CookeTriplet_PRMSA         17.3/12    16.1/11     16.1/ 9
        ///   CookeTriplet_SPOTM        467.4/ 8   399.4/ 8    210.1/ 7
        /// </code>
        /// <para>Every rung that figures the first powered surface goes to the oracle's floor -
        /// the rungs no reading had moved at all. Spheres and figured spheres stay exact.</para>
        ///
        /// <para>On the two rungs that figure the LAST powered surface it reproduces the (85.1)
        /// reading's numbers to the decimal, alone and with the D half - so the accumulation
        /// that reading built was already right there, and what is left on them is defect 2 in
        /// the check half's barred rule, which this does not reach. The triplets carry both
        /// faults, and are worse here than under D half plus (85.1), whose wrong accumulation
        /// presumably offset part of defect 2; that is an inference, not a measurement.</para>
        ///
        /// <para>Not adopted: the flat-in-collimated-space surfaces, where q is infinite, keep the
        /// recursion, and on <c>Ladder2_FlatFigured</c> the recovery's own (22.42)/(22.53) check
        /// fails at 76 per cent, so the 812 to 710 it measures there means nothing either way.
        /// </para>
        /// </summary>
        public bool BarredQAccumulationFromIdentities { get; init; }

        /// <summary>
        /// Whether the check half's SIXTH barred member is formed by (85.1) from the (I) one -
        /// <c>Y114 = q~ t80 - 'S-6_q</c> with <c>'S-6_q = q t80 - t114</c> - instead of by the
        /// (Y) recursion.
        ///
        /// <para><b>The gap it closes.</b> <see cref="BarredQAccumulationFromIdentities"/> sets
        /// the first five members of both families from the shared accumulation, and the
        /// identities cannot supply <c>Sbar_6q</c>, so t114 was left alone - in BOTH halves. In
        /// the check half that leaves Y114 on the (Y) recursion, which for the other five members
        /// builds a different quantity altogether (80 per cent from (85.1)'s even on a sphere).
        /// Measured over that arrangement, T10 is wrong by exactly what it was as built on every
        /// design, and T10 is the one total whose formula carries t114 and nothing the injection
        /// moved. The four barred totals that improve when the tertiary-family terms are dropped
        /// - T-bar6, 8, 9 and 10 - are the four that carry t120.</para>
        ///
        /// <para>The (I) recursion's accumulation is exact wherever nothing figured lies ahead,
        /// which is every rung that figures its last powered surface.</para>
        ///
        /// <para><b>MEASURED, over identities plus D half</b> - relative error of each total
        /// against Forbes, per cent:</para>
        /// <code>
        ///                          T9          T10         T-bar6      T-bar9      T-bar10
        ///   Ladder2_A4_Second      6.08 3.25   5.05 1.56   3.41 1.99   9.94 7.05   6.02 3.36
        ///   FiguredSphere_Then_A4  6.28 3.35   5.09 1.57   3.50 2.04   9.71 6.89   5.99 3.35
        ///   Ladder2_A4_Both        6.63 3.74   5.12 1.55   3.19 1.92  11.09 7.92   6.28 3.61
        ///   Ladder3_A4_Middle      0.18 0.24   0.68 0.31   0.13 0.18   2.20 1.30   0.40 0.37
        ///   CookeTriplet_SPOTM    18.74 11.00  0.64 7.13  11.87 7.87   0.17 0.08  31.45 23.55
        /// </code>
        /// <para>Right in direction everywhere it should be, and nothing else moves. The triplet's
        /// T10 goes the other way, which is the expected failure: it has figuring AHEAD of later
        /// surfaces, where the (I) recursion's sixth accumulation is wrong for the same reason
        /// the other five were - and the identities cannot replace that one.</para>
        ///
        /// <para>What is left is not one wrong value. <see cref="ScaleOneEntry"/> fitted over every
        /// entry of both passes explains at best 64 per cent of the remaining error on
        /// <c>Ladder2_A4_Second</c>, with the leader changing from rung to rung and the leaders
        /// dominated by what reaches T-bar8, the largest relative error. The remainder - T5 and
        /// T-bar5 with t110 or t119 in them, and T-bar8 - is a matter of terms, not of an entry.
        /// </para>
        /// </summary>
        public bool SixthBarredMemberByEquation851 { get; init; }

        /// <summary>
        /// Whether the D half of the figured secondary is moved into the hat pass in the M
        /// entries - t45, t51, t55, t61, t66 - as well as in the six secondaries themselves.
        ///
        /// <para><b>Why the D split is incomplete without it.</b>
        /// <see cref="FiguredSecondarySplitByDandL"/> moves <c>D_m</c> from the check half of
        /// t38..t65 to the hat half. But the M entries are the <c>mid</c> outputs of the figured
        /// <c>Secondary()</c> pass, and each carries the intrinsic secondary it follows from with
        /// coefficient one - <c>s2mid = ... + s[1]</c>, and likewise down to
        /// <c>s4mid = 2(s3mid - s[2]) + s[3]</c>, where s[2] cancels - so every one of them still
        /// holds <c>D_m</c> in the check pass. A pass whose s entries and M partners disagree about
        /// where D lives is the half-split hybrid Sec. 85 rules out.</para>
        ///
        /// <para><b>What it predicts.</b> The move is exactly <c>D_m</c>, so a product
        /// <c>G x M_m</c> summed over the two passes shifts by <c>(G_(I) - G_(Y)) D_m</c> - a
        /// multiple of <c>(q - q~)</c> times an accumulation. It is zero on a figured sphere,
        /// D being proportional to c1, and wherever nothing is accumulated ahead; it is live on
        /// the rungs that figure their last powered surface. Every total still wrong there - T5,
        /// T-bar5, T-bar8, T9, T-bar9 - carries t61.</para>
        /// </summary>
        public bool FiguredMSplitByDandL { get; init; }

        /// <summary>
        /// Whether the sixth barred q accumulation <c>'S-6_q</c> is taken from the DUAL run of the
        /// scheme, paper XII Sec. 6 - the same scheme on the interchanged ray data with the indices
        /// negated, whose accumulated s_1p is <c>-'S-6_q</c> by (6.2). Both families then carry
        /// <c>t114 = ratio t80 - 'S-6_q</c>.
        ///
        /// <para>The identities cannot supply this member (M Sec. 19). Duality can, and it is
        /// gated: on every spherical system the dual run reproduces the recursion's S-bar_q, all
        /// six, to 6E-13; on every figured system with a figured dual input it reproduces the five
        /// barred q accumulations the identities recover, to 2E-13. Requires the dual figured
        /// increments, passed to <see cref="Tau"/>.</para>
        /// </summary>
        public bool SixthBarredMemberFromDuality { get; init; }

        /// <summary>
        /// DIAGNOSTIC reading of the sixth barred q accumulation. Adds, per surface, the pass-ratio
        /// difference <c>(q~ - q)</c> times ONE part of the figured increment of <c>t98</c> - the
        /// part the lift does not reach - to <c>'S-6_q</c>, in both families.
        ///
        /// <para>0 none; 1 figured x figured products; 2 spherical x figured products; 3 paraxial x
        /// figured terms; 4 the bracket <c>SecBarFig - lift</c> of the linear term. The sign is
        /// <see cref="SixthMemberExtraSign"/>. Part 1 is the selective one: it vanishes unless a
        /// figured surface has figuring ahead of it, which is the only case still failing. None of
        /// these is a candidate arrangement until it is derived.</para>
        /// </summary>
        public int SixthMemberExtraPart { get; init; }

        /// <summary>The sign <see cref="SixthMemberExtraPart"/> is applied with, plus or minus one.</summary>
        public Scalar SixthMemberExtraSign { get; init; } = 1.0;

        /// <summary>
        /// DIAGNOSTIC, not a parameter. Multiply one entry by a factor in one pass only, just
        /// before that pass runs, with the entries derived from it re-formed.
        ///
        /// <para>It exists for a structure test: the totals are polynomial in every entry, so a
        /// small scaling gives each entry's contribution to all twenty at once, and a single
        /// wrong entry must explain every total's error with ONE multiplier. An entry whose fitted
        /// multiplier leaves most of the error unexplained is not the fault, however large its
        /// share of any one total. Never a candidate arrangement.</para>
        /// </summary>
        public (int Index, Scalar Factor, bool CheckHalf)? ScaleOneEntry { get; init; }

        /// <summary>The arrangement as <see cref="BuchdahlTableI"/> has it. The parity gate.</summary>
        public static readonly Options AsBuilt = new();

        /// <summary>
        /// <b>The aspheric arrangement.</b> Members one to five of the barred q accumulation from
        /// the identities of M Sec. 22, the sixth from the dual run of XII Sec. 6, and the
        /// figuring's D half carried in the hat pass in both the secondaries and the M entries.
        ///
        /// <para>Against Forbes' series trace it gives all twenty tau to between 2E-13 and 2E-10
        /// relative on every figured design in the ladder and on the three aspheric triplets,
        /// where the arrangement as built was out by 17 to 467 per cent; spheres, one-surface
        /// designs and figured spheres stay exact. At a figured flat facing collimated light q is
        /// infinite and the four are skipped here; <see cref="TertiaryCoefficients.Attach"/>
        /// takes such a system through the Laurent-series build of this routine instead, which
        /// is exact there (1.2E-9 against Forbes).</para>
        /// </summary>
        public static readonly Options Default = new()
        {
            BarredQAccumulationFromIdentities = true,
            FiguredSecondarySplitByDandL = true,
            FiguredMSplitByDandL = true,
            SixthBarredMemberFromDuality = true,
        };
    }

    /// <summary>
    /// tau1..tau20 for a figured system, indexed 1..20 to match the literature.
    ///
    /// <para>The per-surface quantities come from the shared scheme - they are not recomputed
    /// here and not disputed - and only the tertiary arrangement over them is this routine's
    /// own.</para>
    /// </summary>
    public static Scalar[] Tau(
        IReadOnlyList<Models.Surface> surfaces, Scalar[] indices, Scalar efl,
        Scalar stopParameter, IReadOnlyList<Scalar[]>? aspheric = null,
        Scalar iota = default, Options? options = null,
        IReadOnlyList<Scalar[]>? dualAspheric = null,
        Action<int, Scalar[], Scalar[], Scalar[], Scalar[]>? perSurface = null)
    {
        if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
        options ??= Options.Default;
        if (options.SixthBarredMemberFromDuality && aspheric != null && dualAspheric == null)
            throw new ArgumentException(
                "The sixth barred member by duality needs the dual figured increments - "
              + "AsphericSchemeIncrements.BuildDual. Without them the dual run would be unfigured "
              + "and S-bar_6q silently wrong.", nameof(dualAspheric));

        var rows = BuchdahlTableI.Compute(surfaces, indices, efl, stopParameter, aspheric,
                                          iota: iota);

        // XII Sec. 6(iii): the interchanged ray data, the indices negated, and the figuring as
        // the fifth-order code gives it for those rays.
        BuchdahlTableIRow[]? dualRows = null;
        if (options.SixthBarredMemberFromDuality)
        {
            var negated = new Scalar[indices.Length];
            for (int k = 0; k < indices.Length; k++) negated[k] = -indices[k];
            dualRows = BuchdahlTableI.Compute(surfaces, negated, efl, stopParameter, dualAspheric,
                                              iota: iota, dual: true);
        }

        var totals = Totals(rows, surfaces.Count, options, dualRows, perSurface);

        return TertiaryCoefficients.AssembleTau(totals.T, totals.Tbar);
    }

    /// <summary>The ten tertiary totals over the system, unbarred and barred.</summary>
    public readonly record struct SystemTotals(Scalar[] T, Scalar[] Tbar);

    /// <summary>
    /// The two passes of Sec. 85, surface by surface, summed over the system.
    ///
    /// <para>The rows carry both halves of every quantity already - the scheme computes them
    /// and then adds them together, one line too early for this purpose - so what happens here
    /// is that each pass is given its own half and its own family, and the two are added only
    /// at the end.</para>
    /// </summary>
    /// <param name="perSurface">
    /// DIAGNOSTIC: handed each surface's four pass results - hat, hat barred, check, check barred,
    /// indexed 1..10 - as they are added into the totals. Copies; nothing done with them reaches
    /// the result.
    /// </param>
    public static SystemTotals Totals(BuchdahlTableIRow[] rows, int count, Options options,
                                      BuchdahlTableIRow[]? dualRows = null,
                                      Action<int, Scalar[], Scalar[], Scalar[], Scalar[]>? perSurface = null)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        options ??= Options.AsBuilt;
        if (options.SixthBarredMemberFromDuality && dualRows == null)
            throw new ArgumentException(
                "The sixth barred member by duality needs the dual run's rows.", nameof(dualRows));

        var T = new Scalar[11];
        var Tbar = new Scalar[11];
        var figured = AccumulatedFiguredPrimary(rows, count);
        var qSide = options.QSideProductsOnSphericalHalves ? QSideDelta(rows, count)
                  : default((Scalar[][], Scalar[][], Scalar[][], Scalar[][]));
        var qBar = options.SharedQBarWithSplitPrimary ? SharedQBarDelta(rows, count) : null;
        var dagger = options.FullFiguredBarredSecondaryInDagger
                     || options.Equation688BracketInDagger
                     || options.DaggerIncrementFiguredHalfOnHeightRatio
                     || options.NoFiguredCorrectionInDagger
                     || options.DaggerCorrectionOnIncrementAlone
                   ? DaggerDelta(rows, count, options) : null;
        var sixthExtra = options.SixthMemberExtraPart != 0
                       ? SixthMemberExtra(rows, count, options) : null;

        for (int i = 1; i < count - 1; i++)
        {
            var r = rows[i];
            var t = r.T;

            // The surface's own quantities, swapped per half and put back after. The row is
            // shared with the scheme that produced it, so it is left exactly as it was found.
            Scalar o10 = t[10], o13 = t[13], o40 = t[40];
            Scalar o38 = t[38], o44 = t[44], o50 = t[50], o54 = t[54], o59 = t[59], o65 = t[65];
            Scalar o45 = t[45], o51 = t[51], o55 = t[55], o61 = t[61], o66 = t[66];
            var oz = new Scalar[11];
            for (int m = 1; m <= 10; m++) oz[m] = t[120 + m];
            var oFamily = new Scalar[156];
            for (int m = 25; m <= 33; m++) oFamily[m] = t[m];
            for (int m = 101; m <= 120; m++) oFamily[m] = t[m];

            var hat = new Scalar[11];
            var hatBar = new Scalar[11];
            var check = new Scalar[11];
            var checkBar = new Scalar[11];
            var residueCheck = new Scalar[11];
            var residueCheckBar = new Scalar[11];
            var zero = new Scalar[11];

            // M (85.1): the hat pass is combined by the (I) family, the check pass by the (Y)
            // family - the same p and q quantities on the height ratio instead of the incidence
            // ratio. Both are built alongside each other by the scheme; here each pass is given
            // the one that belongs to it, at BOTH orders.
            void Family(bool checkHalf)
            {
                var src = checkHalf ? r.Y : oFamily;
                for (int m = 25; m <= 33; m++) t[m] = src[m];
                for (int m = 101; m <= 120; m++) t[m] = src[m];

                // (85.1) on the barred members of the secondary-order family: the same two
                // accumulations as the (I) partner, joined on the height ratio instead of the
                // incidence ratio. The six p-side accumulations are the BARRED secondary sums
                // t70, t72, t74, t76, t78 and t80, which is what the (I) members carry.
                // The shared q-side barred accumulation with its primary split, and - that being
                // shared rather than a family member - the (Y) barred members read off the
                // corrected (I) ones by (85.1). One correction in two halves.
                // The lift split by (60.3): q D + q~ L in place of q~ (D + L). The correction
                // the recursions carry is -(q~ - q) x lift, so the move is +(q~ - q)^2 D.
                if (options.LiftSplitByDandL && i > 1)
                {
                    var prv2 = rows[i - 1];
                    Scalar dr = prv2.Rho - prv2.T[6];
                    Scalar shift2 = dr * dr;

                    t[102] += shift2 * prv2.SecDFigured[0];
                    t[104] += shift2 * prv2.SecDFigured[1];
                    t[107] += shift2 * prv2.SecDFigured[2];
                    t[109] += shift2 * prv2.SecDFigured[3];
                    t[112] += shift2 * prv2.SecDFigured[4];
                    t[114] += shift2 * prv2.SecDFigured[5];

                    Scalar ratio2 = checkHalf ? r.Rho : t[6];
                    t[115] = -ratio2 * t[101] + t[102];
                    t[116] = -ratio2 * t[103] + t[104];
                    t[117] = -ratio2 * t[105] + t[107];
                    t[118] = -ratio2 * t[108] + t[109];
                    t[119] = -ratio2 * t[110] + t[112];
                    t[120] = -ratio2 * t[113] + t[114];
                }

                if (options.SharedQBarWithSplitPrimary || options.YBarredFromSharedAccumulations)
                {
                    // The two halves are separable, and worth separating: the split-primary
                    // delta is zero wherever the previous surface is unfigured or has nothing
                    // accumulated ahead of it, so the two reach different designs.
                    // qBar is non-null exactly when SharedQBarWithSplitPrimary is set - see where it
                    // is built. Testing the ARRAY rather than the option says the same thing once
                    // instead of twice, and is the form the compiler can check, as the dagger and
                    // sixth-member deltas below already do.
                    var d = qBar is not null ? qBar[i] : new Scalar[6];
                    Scalar qbShift = checkHalf && options.YBarredFromSharedAccumulations
                                   ? r.Rho - t[6] : 0.0;

                    t[102] = oFamily[102] + d[0] + qbShift * t[70];
                    t[104] = oFamily[104] + d[1] + qbShift * t[72];
                    t[107] = oFamily[107] + d[2] + qbShift * t[74];
                    t[109] = oFamily[109] + d[3] + qbShift * t[76];
                    t[112] = oFamily[112] + d[4] + qbShift * t[78];
                    t[114] = oFamily[114] + d[5] + qbShift * t[80];

                    Scalar ratio = checkHalf ? r.Rho : t[6];
                    t[115] = -ratio * t[101] + t[102];
                    t[116] = -ratio * t[103] + t[104];
                    t[117] = -ratio * t[105] + t[107];
                    t[118] = -ratio * t[108] + t[109];
                    t[119] = -ratio * t[110] + t[112];
                    t[120] = -ratio * t[113] + t[114];
                }


                // DIAGNOSTIC: the sixth barred q accumulation moved by one part of its figured
                // increment. 'S-6q grows by the correction, so t114 = ratio t80 - 'S-6q falls by it
                // in either family, and t120 with it.
                if (sixthExtra != null && !r.FlatInCollimatedSpace && SMath.Abs(t[6]) < 1e6)
                {
                    t[114] -= sixthExtra[i];
                    t[120] -= sixthExtra[i];
                }

                // The q-side closed forms with their products taken on the spherical halves.
                // Both families move: each is built from the same t86..t98, differing only in
                // the ratio that combines them.
                if (options.QSideProductsOnSphericalHalves)
                {
                    var prim = checkHalf ? qSide.Item3[i] : qSide.Item1[i];
                    var rec = checkHalf ? qSide.Item4[i] : qSide.Item2[i];
                    Scalar ratio = checkHalf ? r.Rho : t[6];

                    t[101] += prim[0]; t[103] += prim[1]; t[105] += prim[2];
                    t[108] += prim[3]; t[110] += prim[4]; t[113] += prim[5];

                    t[102] += rec[0]; t[104] += rec[1]; t[107] += rec[2];
                    t[109] += rec[3]; t[112] += rec[4]; t[114] += rec[5];

                    t[115] = -ratio * t[101] + t[102];
                    t[116] = -ratio * t[103] + t[104];
                    t[117] = -ratio * t[105] + t[107];
                    t[118] = -ratio * t[108] + t[109];
                    t[119] = -ratio * t[110] + t[112];
                    t[120] = -ratio * t[113] + t[114];
                }

                // The induced bracket of the figured barred secondary, put back on the height
                // ratio. The (Y) family is already combined on that ratio, so its correction
                // would be rho - rho and there is nothing to put back; this moves the (I)
                // family alone.
                if (dagger != null && !checkHalf)
                {
                    var d = dagger[i];
                    t[102] += d[0]; t[104] += d[1]; t[107] += d[2];
                    t[109] += d[3]; t[112] += d[4]; t[114] += d[5];
                    t[115] += d[0]; t[116] += d[1]; t[117] += d[2];
                    t[118] += d[3]; t[119] += d[4]; t[120] += d[5];
                }

                // The barred q-side accumulation from the identities rather than the recursion,
                // shared by both families and joined on each one's own ratio by (85.1). Where the
                // ratio is infinite the members cannot be formed as products and are left alone.
                if (options.BarredQAccumulationFromIdentities && !r.FlatInCollimatedSpace
                    && SMath.Abs(t[6]) < 1e6)
                {
                    var s = BuchdahlSecondaryQ.At(rows, i);
                    Scalar ratio = checkHalf ? r.Rho : t[6];

                    t[102] = ratio * t[70] - s[1];
                    t[104] = ratio * t[72] - s[2];
                    t[107] = ratio * t[74] - s[3];
                    t[109] = ratio * t[76] - s[4];
                    t[112] = ratio * t[78] - s[5];

                    t[115] = -ratio * t[101] + t[102];
                    t[116] = -ratio * t[103] + t[104];
                    t[117] = -ratio * t[105] + t[107];
                    t[118] = -ratio * t[108] + t[109];
                    t[119] = -ratio * t[110] + t[112];
                }

                // The sixth member from the dual run: its accumulated s_1p is -'S-6q, XII (6.2).
                // Where either run meets an infinite ratio the member is left as built.
                if (options.SixthBarredMemberFromDuality && dualRows != null
                    && !r.FlatInCollimatedSpace && !dualRows[i].FlatInCollimatedSpace
                    && SMath.Abs(t[6]) < 1e6 && SMath.Abs(dualRows[i].T[6]) < 1e6)
                {
                    Scalar ratio = checkHalf ? r.Rho : t[6];
                    t[114] = ratio * t[80] + dualRows[i].T[69];
                    t[120] = -ratio * t[113] + t[114];
                }

                // The sixth member, which the identities cannot supply: the (I) recursion's
                // accumulation 'S-6q = q t80 - t114, joined on the height ratio by (85.1). The hat
                // half is the (I) member already and does not move.
                if (options.SixthBarredMemberByEquation851 && checkHalf
                    && !r.FlatInCollimatedSpace && SMath.Abs(t[6]) < 1e6)
                {
                    t[114] = oFamily[114] + (r.Rho - t[6]) * t[80];
                    t[120] = -r.Rho * t[113] + t[114];
                }

                // The accumulated figuring re-combined on the height ratio. The (Y) family is
                // already combined on it, so this moves the (I) family alone - which is what
                // makes the reading testable: a spherical surface downstream of a figured one
                // contributes through the hat pass only, and this is the sole thing that
                // changes for it.
                if (!options.AccumulatedFiguringOnHeightRatio || checkHalf) return;

                var f = figured[i];
                Scalar shift = options.HeightRatioShiftFraction * (r.Rho - t[6]);
                if (SMath.Vanishes(shift)) return;

                t[25] += shift * f[15];
                t[26] += shift * f[16];
                t[27] += 2.0 * shift * f[16];
                t[28] += shift * f[17];
                t[29] += shift * f[18];
                t[30] += shift * f[19];

                // The dagger pair follows from the six, by the same (84.15) it always did.
                t[31] = -t[6] * t[25] + t[26];
                t[32] = -t[6] * t[27] + t[28];
                t[33] = -t[6] * t[29] + t[30];
            }

            void Load(Scalar ap, Scalar c13, IReadOnlyList<Scalar> sec,
                      IReadOnlyList<Scalar> mm, IReadOnlyList<Scalar> z)
            {
                t[10] = ap;
                t[13] = c13;
                t[38] = sec[1]; t[44] = sec[2]; t[50] = sec[3];
                t[54] = sec[4]; t[59] = sec[5]; t[65] = sec[6];
                t[45] = mm[1]; t[51] = mm[2]; t[55] = mm[3]; t[61] = mm[4]; t[66] = mm[5];
                for (int m = 1; m <= 10; m++) t[120 + m] = z[m];
                t[40] = t[38] + 2.0 * t[10] * t[25];
            }

            // DIAGNOSTIC: the direct uses of the accumulated primaries on their spherical halves
            // only. The family members are already built and stored on the row, so they keep the
            // full accumulations - which is the point, the two routes being what this separates.
            var oAcc = new Scalar[25];
            for (int m = 15; m <= 24; m++) oAcc[m] = t[m];
            if (options.SphericalAccumulationsInDirectUses)
                for (int m = 15; m <= 24; m++) t[m] -= figured[i][m];

            // Some induced terms involve NO quantity of this surface at all - they are built
            // from the accumulated coefficients alone, and belong to ONE pass rather than to
            // both. The all-zero residue measures them so they can be taken off the check half,
            // with the same intermediates as the pass it is taken off or it removes
            // accumulated-only terms of the wrong family. On a sphere every check input is zero
            // and this makes the whole check half vanish identically, which it must.
            Family(checkHalf: true);
            Load(0.0, 0.0, zero, zero, zero);
            Pass(t, r.Rho, residueCheck, residueCheckBar, options, checkHalf: true);

            Family(checkHalf: false);
            // (60.3) puts the figuring's D half on the incidence and its L half on the height,
            // so the D half belongs with the hat quantities. The total is unchanged.
            var hatSec = r.SecSph;
            var checkSec = r.SecFig;
            if (options.FiguredSecondarySplitByDandL)
            {
                var h = new Scalar[7];
                var c = new Scalar[7];
                for (int m = 1; m <= 6; m++)
                {
                    Scalar d = r.SecDFigured[m - 1];
                    h[m] = r.SecSph[m] + d;
                    c[m] = r.SecFig[m] - d;
                }
                hatSec = h;
                checkSec = c;
            }

            // The same D half inside the M entries, each of which carries its intrinsic secondary
            // with coefficient one. SecDFigured is zero on a spherical surface, so this is inert
            // there.
            var hatM = r.MSph;
            var checkM = r.MFig;
            if (options.FiguredMSplitByDandL)
            {
                var h = new Scalar[6];
                var c = new Scalar[6];
                for (int m = 1; m <= 5; m++)
                {
                    Scalar d = r.SecDFigured[m];
                    h[m] = r.MSph[m] + d;
                    c[m] = r.MFig[m] - d;
                }
                hatM = h;
                checkM = c;
            }

            // DIAGNOSTIC: one entry scaled in one pass, its derived entries re-formed on that
            // pass's ratio. The row is put back below exactly as for every other swap.
            void ScaleOne(bool checkHalf, Scalar ratio)
            {
                if (options.ScaleOneEntry is not { } s || s.CheckHalf != checkHalf) return;
                t[s.Index] *= s.Factor;
                if (SMath.Abs(ratio) < 1e6)
                {
                    if (s.Index >= 25 && s.Index <= 30)
                    {
                        t[31] = -ratio * t[25] + t[26];
                        t[32] = -ratio * t[27] + t[28];
                        t[33] = -ratio * t[29] + t[30];
                    }
                    if (s.Index >= 101 && s.Index <= 114)
                    {
                        t[115] = -ratio * t[101] + t[102];
                        t[116] = -ratio * t[103] + t[104];
                        t[117] = -ratio * t[105] + t[107];
                        t[118] = -ratio * t[108] + t[109];
                        t[119] = -ratio * t[110] + t[112];
                        t[120] = -ratio * t[113] + t[114];
                    }
                }
                t[40] = t[38] + 2.0 * t[10] * t[25];
            }

            Load(r.ApSpherical, r.C13Spherical, hatSec, hatM, r.ZHat);
            ScaleOne(checkHalf: false, t[6]);
            Pass(t, t[6], hat, hatBar, options, r.FlatInCollimatedSpace, r.QT152);

            Family(checkHalf: true);
            Load(r.ApFigured, r.C13Figured, checkSec, checkM, r.ZCheck);
            ScaleOne(checkHalf: true, r.Rho);
            Pass(t, r.Rho, check, checkBar, options, checkHalf: true);
            for (int k = 1; k <= 10; k++)
            {
                check[k] -= residueCheck[k];
                checkBar[k] -= residueCheckBar[k];
            }

            for (int m = 15; m <= 24; m++) t[m] = oAcc[m];

            // Put the row back exactly as it was found.
            Family(checkHalf: false);
            t[10] = o10; t[13] = o13; t[40] = o40;
            t[38] = o38; t[44] = o44; t[50] = o50; t[54] = o54; t[59] = o59; t[65] = o65;
            t[45] = o45; t[51] = o51; t[55] = o55; t[61] = o61; t[66] = o66;
            for (int m = 1; m <= 10; m++) t[120 + m] = oz[m];

            // (85.3): the total is the sum of the two halves, and the barred entry the sum of
            // the two BARRED halves - each formed by (84.23) within its own pass, not the total
            // times a ratio, which would drop the barred intermediates entirely.
            perSurface?.Invoke(i, (Scalar[])hat.Clone(), (Scalar[])hatBar.Clone(),
                               (Scalar[])check.Clone(), (Scalar[])checkBar.Clone());

            for (int k = 1; k <= 10; k++)
            {
                T[k] += hat[k] + check[k];
                Tbar[k] += hatBar[k] + checkBar[k];
            }
        }

        return new SystemTotals(T, Tbar);
    }

    /// <summary>
    /// The figured half of the accumulated primary coefficients, surface by surface: what has
    /// been accumulated AHEAD of each surface from the L term of (60.3) alone.
    ///
    /// <para>Indexed to match the scheme's entries - <c>[15]</c> the running sum of a,
    /// <c>[16]</c> of a-bar, <c>[17]</c> of b, <c>[18]</c> of c, <c>[19]</c> of c-bar, and then
    /// the q-side entries <c>[20]</c> to <c>[22]</c>, which differ from those only by terms the
    /// figuring does not reach.</para>
    ///
    /// <para>Nothing new is computed here. Each surface's figured primary and the height ratio
    /// it travels on are already on the row, and the barred partners are formed from them the
    /// same way the scheme forms them; the two halves are simply kept apart instead of being
    /// added together. <c>[19]</c> is cross-checked against the scheme's own
    /// <c>T19Figured</c>, which it must equal.</para>
    /// </summary>
    /// <summary>
    /// The six q-side secondary coefficients - the scheme's t86, t89, t92, t94, t97 and t98 -
    /// from the accumulations they are built out of.
    ///
    /// <para>A transcription of the closed forms, so that they can be evaluated a second time on
    /// the SPHERICAL half of every accumulation and the two differenced. It is checked against
    /// the scheme's own entries by <c>BuchdahlAsphericSchemeTests</c>; a transcription that has
    /// drifted would otherwise produce a plausible correction out of nothing.</para>
    ///
    /// <param name="a">The primary accumulations t15..t24, indexed from zero.</param>
    /// <param name="s">The accumulated BARRED secondaries t70, t72, t74, t76, t78, t80.</param>
    /// </summary>
    private static Scalar[] QSideSecondaries(Scalar t9, Scalar t81, Scalar t82,
                                             Scalar[] a, Scalar[] s)
    {
        Scalar t15 = a[0], t16 = a[1], t17 = a[2], t18 = a[3], t19 = a[4];
        Scalar t20 = a[5], t21 = a[6], t22 = a[7], t23 = a[8], t24 = a[9];

        Scalar t83 = t16 - t20, t84 = t17 - t22, t85 = t19 - t23;

        Scalar q86 = -1.5 * t83 * t83 + t9 * t16 - t16 * t20
                     + t15 * t21 - t15 * t81 + s[0];

        Scalar t87 = (2.0 * t21 - t22 - t81) * t16 + t9 * t21 - t20 * t81;
        Scalar t88 = (2.0 * t23 - t82) * t15 - t17 * t20 + t9 * t17;
        Scalar q89 = -3.0 * t83 * t84 + t87 + t88 + s[1];

        Scalar t90 = -0.5 * t81 * t84 + t9 * t19 - t20 * t82;
        Scalar t91 = t18 * t21 + t15 * t24 - t16 * t23 - t19 * t20;
        Scalar q92 = -3.0 * t83 * t85 + s[2] + t90 + t91;

        Scalar t93 = 2.0 * (2.0 * t16 * t23 - t16 * t82 + t9 * t23) - t17 * t22;
        Scalar q94 = -1.5 * t84 * t84 + t81 * t84 + s[3] + t93;

        Scalar t95 = (2.0 * t18 - t17 + t81) * t23 + t19 * t81 - t22 * t82;
        Scalar t96 = (2.0 * t16 + t9) * t24 - t19 * t22 - t18 * t82;
        Scalar q97 = -3.0 * t84 * t85 + t95 + t96 + s[4];

        Scalar q98 = -1.5 * t85 * t85 + t24 * t81 + t18 * t24
                     - t23 * t82 - t19 * t23 + s[5];

        return new[] { q86, q89, q92, q94, q97, q98 };
    }

    /// <summary>
    /// The running correction <see cref="Options.SixthMemberExtraPart"/> makes to 'S-6q, surface
    /// by surface: each surface j adds <c>(q~_j - q_j)</c> times the chosen part's increment
    /// between j and the surface after it.
    /// </summary>
    private static Scalar[] SixthMemberExtra(BuchdahlTableIRow[] rows, int count, Options options)
    {
        var figuredPrimary = AccumulatedFiguredPrimary(rows, count);
        var figuredSecondary = AccumulatedFiguredSecondary(rows, count);
        var part = new Scalar[count];
        var zero = new Scalar[6];

        for (int i = 1; i < count - 1; i++)
        {
            var t = rows[i].T;
            var aFull = new Scalar[10];
            var aF = new Scalar[10];
            var aS = new Scalar[10];
            for (int k = 0; k < 10; k++)
            {
                aFull[k] = t[15 + k];
                aF[k] = figuredPrimary[i][15 + k];
                aS[k] = aFull[k] - aF[k];
            }
            var sFull = new[] { t[70], t[72], t[74], t[76], t[78], t[80] };
            var sS = new Scalar[6];
            for (int k = 0; k < 6; k++) sS[k] = sFull[k] - figuredSecondary[i][k];

            Scalar full = QSideSecondaries(t[9], t[81], t[82], aFull, sFull)[5];
            Scalar sph = QSideSecondaries(t[9], t[81], t[82], aS, sS)[5];
            Scalar linQuad = QSideSecondaries(t[9], t[81], t[82], aF, zero)[5];
            Scalar quad = QSideSecondaries(0.0, 0.0, 0.0, aF, zero)[5];

            part[i] = options.SixthMemberExtraPart switch
            {
                1 => quad,
                2 => full - sph - figuredSecondary[i][5] - linQuad,
                3 => linQuad - quad,
                _ => 0.0,
            };
        }

        var result = new Scalar[count];
        Scalar running = 0.0;
        for (int i = 1; i < count - 1; i++)
        {
            if (i > 1)
            {
                var prv = rows[i - 1];
                bool singular = prv.FlatInCollimatedSpace || SMath.Abs(prv.T[6]) > 1e6
                              || rows[i].FlatInCollimatedSpace;
                Scalar dr = prv.Rho - prv.T[6];

                // Parts 5 and 6 are surface j's OWN: the products of the q-side closed form that
                // carry j's figured primaries, read at the surface after it. 5 pairs them with the
                // spherical accumulations only; 6 with everything, figured ahead included.
                Scalar own = 0.0;
                if (options.SixthMemberExtraPart is 5 or 6)
                {
                    var t = rows[i].T;
                    var aFull = new Scalar[10];
                    var aSph = new Scalar[10];
                    var mine = new Scalar[10];
                    var withoutMine = new Scalar[10];
                    var sphPlusMine = new Scalar[10];
                    for (int k = 0; k < 10; k++)
                    {
                        aFull[k] = t[15 + k];
                        aSph[k] = aFull[k] - figuredPrimary[i][15 + k];
                        mine[k] = figuredPrimary[i][15 + k] - figuredPrimary[i - 1][15 + k];
                        withoutMine[k] = aFull[k] - mine[k];
                        sphPlusMine[k] = aSph[k] + mine[k];
                    }
                    own = options.SixthMemberExtraPart == 5
                        ? QSideSecondaries(0.0, 0.0, 0.0, sphPlusMine, zero)[5]
                          - QSideSecondaries(0.0, 0.0, 0.0, aSph, zero)[5]
                          - QSideSecondaries(0.0, 0.0, 0.0, mine, zero)[5]
                        : QSideSecondaries(0.0, 0.0, 0.0, aFull, zero)[5]
                          - QSideSecondaries(0.0, 0.0, 0.0, withoutMine, zero)[5];
                }

                Scalar step = options.SixthMemberExtraPart switch
                {
                    4 => dr * (prv.SecBarFig[5] - prv.SecBarFigLift[5]),
                    5 or 6 => dr * own,
                    _ => dr * (part[i] - part[i - 1]),
                };
                if (!singular) running += options.SixthMemberExtraSign * step;
            }
            result[i] = running;
        }
        return result;
    }

    /// <summary>
    /// The q-side secondaries as they stand, and again on the spherical half of every
    /// accumulation. Their difference is the figured half of each, exactly - products included,
    /// since M (67.1-2) splits every accumulation that enters them.
    /// </summary>
    public static (Scalar[] Full, Scalar[] Figured) QSideHalves(
        BuchdahlTableIRow row, Scalar[] figuredPrimary, Scalar[] figuredSecondary)
    {
        var t = row.T;
        var a = new[] { t[15], t[16], t[17], t[18], t[19],
                        t[20], t[21], t[22], t[23], t[24] };
        var s = new[] { t[70], t[72], t[74], t[76], t[78], t[80] };

        var aS = new Scalar[10];
        for (int k = 0; k < 10; k++) aS[k] = a[k] - figuredPrimary[15 + k];
        var sS = new Scalar[6];
        for (int k = 0; k < 6; k++) sS[k] = s[k] - figuredSecondary[k];

        var full = QSideSecondaries(t[9], t[81], t[82], a, s);
        var spherical = QSideSecondaries(t[9], t[81], t[82], aS, sS);

        var figured = new Scalar[6];
        for (int k = 0; k < 6; k++) figured[k] = full[k] - spherical[k];
        return (full, figured);
    }

    /// <summary>
    /// The figured half of each surface's accumulated BARRED secondaries - t70, t72, t74, t76,
    /// t78 and t80 - which is the running sum of the figured barred secondary the scheme already
    /// forms per surface.
    /// </summary>
    private static Scalar[][] AccumulatedFiguredSecondary(BuchdahlTableIRow[] rows, int count)
    {
        var result = new Scalar[count][];
        var running = new Scalar[6];

        for (int i = 1; i < count - 1; i++)
        {
            var mine = new Scalar[6];
            Array.Copy(running, mine, 6);
            result[i] = mine;
            for (int m = 0; m < 6; m++) running[m] += rows[i].SecBarFig[m];
        }
        return result;
    }

    private static Scalar[][] AccumulatedFiguredPrimary(BuchdahlTableIRow[] rows, int count)
    {
        var result = new Scalar[count][];
        var running = new Scalar[25];

        for (int i = 1; i < count - 1; i++)
        {
            var mine = new Scalar[25];
            Array.Copy(running, mine, running.Length);

            // t23 and t24 the scheme already splits for itself - t23's figured half is the same
            // running sum of c-bar_p that t19 carries, and t24's is the recursion on the previous
            // surface's height ratio that (67.2) requires. Taken from the row rather than
            // recomputed, so the two cannot drift apart.
            mine[23] = rows[i].T19Figured;
            mine[24] = rows[i].T24Figured;

            // t20 = (1/2)(t9 at surface one - t9 here) + t16, t21 = (...) + t18 and
            // t22 = 2(t21 - t18) + t17. The leading terms are ray-angle constructions with no
            // figuring in them, so each q-side entry inherits the figured half of the p-side
            // entry it carries and nothing else.
            mine[20] = mine[16];
            mine[21] = mine[18];
            mine[22] = mine[17];
            result[i] = mine;

            var r = rows[i];
            Scalar a = r.ApFigured, rho = r.Rho;
            running[15] += a;
            running[16] += rho * a;
            running[17] += 2.0 * rho * rho * a;
            running[18] += r.C13Figured;
            running[19] += r.C14Figured;
        }

        return result;
    }

    /// <summary>
    /// How much each dagger entry moves when the whole figured barred secondary is carried on
    /// the height ratio instead of its lift half alone.
    ///
    /// <para>Computed as a DIFFERENCE from the scheme's own values rather than by rebuilding the
    /// recursions. Each of the six is of the form <c>(terms) + previous + correction</c>, so a
    /// change to the correction propagates additively and nothing else in the recursion has to
    /// be reproduced - which also means the delta is exactly zero wherever the bracket is, and
    /// the arrangement cannot be disturbed by rounding on a design this reading does not
    /// touch.</para>
    ///
    /// <para>The six are t102, t104, t107, t109, t112 and t114, in that order; t115 to t120
    /// follow them one for one, each being the same entry less a multiple of a quantity this
    /// does not move.</para>
    /// </summary>
    /// <summary>
    /// How the (I) and (Y) tertiary families move when the q-side closed forms take their
    /// products on the spherical halves of the accumulations.
    ///
    /// <para>Propagated as a DIFFERENCE rather than rebuilt. Writing <c>g</c> for the figured
    /// half the products carry - the whole figured half of <c>t86</c> less the part that comes in
    /// through the accumulated barred secondary, which stays - the primitive members move by
    /// <c>+g</c> and each recursion by <c>ratio (g_i - g_{i-1})</c> carried forward, because
    /// every one is of the form <c>-ratio(prev_sec - prev_q + q) + ... + previous</c>. Nothing
    /// else in them touches the q-side forms.</para>
    ///
    /// <para>Returns six deltas per surface for the (I) family and six for the (Y): in order
    /// t101/t102, t103/t104, t105/t107, t108/t109, t110/t112, t113/t114.</para>
    /// </summary>
    private static (Scalar[][] Primitive, Scalar[][] Recursion,
                    Scalar[][] PrimitiveY, Scalar[][] RecursionY)
        QSideDelta(BuchdahlTableIRow[] rows, int count)
    {
        var figuredPrimary = AccumulatedFiguredPrimary(rows, count);
        var figuredSecondary = AccumulatedFiguredSecondary(rows, count);

        var primitive = new Scalar[count][];
        var recursion = new Scalar[count][];
        var primitiveY = new Scalar[count][];
        var recursionY = new Scalar[count][];

        var g = new Scalar[count][];
        for (int i = 1; i < count - 1; i++)
        {
            var (_, figured) = QSideHalves(rows[i], figuredPrimary[i], figuredSecondary[i]);
            var mine = new Scalar[6];
            for (int m = 0; m < 6; m++)
                mine[m] = figured[m] - figuredSecondary[i][m];   // the products' share alone
            g[i] = mine;
        }

        var runningI = new Scalar[6];
        var runningY = new Scalar[6];

        for (int i = 1; i < count - 1; i++)
        {
            var p = new Scalar[6];
            var pY = new Scalar[6];
            for (int m = 0; m < 6; m++) { p[m] = g[i][m]; pY[m] = g[i][m]; }
            primitive[i] = p;
            primitiveY[i] = pY;

            var r = new Scalar[6];
            var rY = new Scalar[6];
            if (i > 1)
            {
                Scalar qPrev = rows[i - 1].T[6];
                Scalar rhoPrev = rows[i - 1].Rho;
                for (int m = 0; m < 6; m++)
                {
                    Scalar step = g[i][m] - g[i - 1][m];
                    r[m] = runningI[m] + qPrev * step;
                    rY[m] = runningY[m] + rhoPrev * step;
                }
            }
            recursion[i] = r;
            recursionY[i] = rY;
            Array.Copy(r, runningI, 6);
            Array.Copy(rY, runningY, 6);
        }

        return (primitive, recursion, primitiveY, recursionY);
    }

    /// <summary>
    /// How the six barred members of the secondary-order family move when the q-side primary
    /// they carry is split, its figured half pairing with the (Y) dagger instead of the (I) one.
    ///
    /// <para>Every site is a product of one dagger with <c>t99</c> or <c>t100</c> - the q-side
    /// primary and its partner, whose figured halves are <c>q~ alpha</c> and
    /// <c>2 q~^2 alpha</c> by (67.2). Writing <c>e = Y_dagger - dagger</c>, each site moves by
    /// <c>e</c> times that figured half, and every recursion carries its own previous value, so
    /// the moves accumulate.</para>
    ///
    /// <para>Returned in the order t102, t104, t107, t109, t112, t114; t115 to t120 follow one
    /// for one, each being the same entry less a multiple of a primitive member this does not
    /// move.</para>
    /// </summary>
    private static Scalar[][] SharedQBarDelta(BuchdahlTableIRow[] rows, int count)
    {
        var delta = new Scalar[count][];
        var running = new Scalar[6];

        for (int i = 1; i < count - 1; i++)
        {
            var d = new Scalar[6];
            if (i > 1)
            {
                var prv = rows[i - 1];
                var pt = prv.T;
                var pY = prv.Y;

                Scalar e31 = pY[31] - pt[31];
                Scalar e32 = pY[32] - pt[32];
                Scalar e33 = pY[33] - pt[33];

                Scalar f99 = prv.Rho * prv.ApFigured;                      // a_q figured half
                Scalar f100 = 2.0 * prv.Rho * prv.Rho * prv.ApFigured;     // b_q figured half
                Scalar qp = pt[6];

                // t102: - prev31 prev99
                d[0] = running[0] - e31 * f99;
                // t104: - prev31 prev100 - prev32 prev99
                d[1] = running[1] - e31 * f100 - e32 * f99;
                // t107, through t106: -0.5 prev6 prev31 prev100 - prev33 prev99
                d[2] = running[2] - 0.5 * qp * e31 * f100 - e33 * f99;
                // t109: - prev32 prev100
                d[3] = running[3] - e32 * f100;
                // t112, through t111: -(0.5 prev6 prev32 + prev33) prev100
                d[4] = running[4] - (0.5 * qp * e32 + e33) * f100;
                // t114: prev6 (-0.5 prev33 prev100)
                d[5] = running[5] - 0.5 * qp * e33 * f100;
            }

            delta[i] = d;
            Array.Copy(d, running, 6);
        }

        return delta;
    }

    private static Scalar[][] DaggerDelta(BuchdahlTableIRow[] rows, int count, Options options)
    {
        var delta = new Scalar[count][];
        var running = new Scalar[6];
        var figuredPrimary = AccumulatedFiguredPrimary(rows, count);
        var figuredSecondary = AccumulatedFiguredSecondary(rows, count);

        for (int i = 1; i < count - 1; i++)
        {
            var d = new Scalar[6];
            if (i > 1)
            {
                var prv = rows[i - 1];
                Scalar dPrevRatio = prv.Rho - prv.T[6];

                if (options.NoFiguredCorrectionInDagger)
                {
                    // Take the existing -(q~ - q) x lift straight back out.
                    for (int m = 0; m < 6; m++)
                    {
                        bool regularised = m == 5 && prv.FlatInCollimatedSpace;
                        d[m] = running[m]
                             + (regularised ? 0.0 : dPrevRatio * prv.SecBarFigLift[m]);
                    }
                    delta[i] = d;
                    Array.Copy(d, running, 6);
                    continue;
                }

                if (options.DaggerCorrectionOnIncrementAlone)
                {
                    // The figured half of the per-surface q-side secondary, by differencing the
                    // figured half of its accumulation. prev70 is deliberately absent: it
                    // telescopes, and correcting it as though it were a per-surface tag is what
                    // cost reading 5 a factor of ten.
                    var fpPrev2 = figuredPrimary[i - 1];
                    var fsPrev2 = figuredSecondary[i - 1];
                    var (_, figuredAtPrev) = QSideHalves(rows[i - 1], fpPrev2, fsPrev2);
                    var (_, figuredAtHere) = QSideHalves(rows[i], figuredPrimary[i],
                                                         figuredSecondary[i]);

                    for (int m = 0; m < 6; m++)
                    {
                        bool regularised = m == 5 && prv.FlatInCollimatedSpace;
                        Scalar increment = figuredAtHere[m] - figuredAtPrev[m];
                        Scalar moved = increment - prv.SecBarFigLift[m];
                        d[m] = running[m] + (regularised ? 0.0 : -dPrevRatio * moved);
                    }
                    delta[i] = d;
                    Array.Copy(d, running, 6);
                    continue;
                }

                if (options.DaggerIncrementFiguredHalfOnHeightRatio)
                {
                    // The bracket each recursion multiplies by the previous surface's q is
                    //     prev70 + (t86 - prev86)
                    // and its five partners. Its figured half is taken exactly: every
                    // accumulation entering the closed forms is split by (67.1-2), so the
                    // products have definite halves and nothing is estimated.
                    //
                    // The scheme already subtracts dPrevRatio times the LIFT half; what is
                    // wanted is dPrevRatio times the whole increment's figured half, so the
                    // move is the difference of the two.
                    var fpPrev = figuredPrimary[i - 1];
                    var fsPrev = figuredSecondary[i - 1];
                    var fpHere = figuredPrimary[i];
                    var fsHere = figuredSecondary[i];

                    var (_, figuredPrev) = QSideHalves(rows[i - 1], fpPrev, fsPrev);
                    var (_, figuredHere) = QSideHalves(rows[i], fpHere, fsHere);

                    for (int m = 0; m < 6; m++)
                    {
                        bool regularised = m == 5 && prv.FlatInCollimatedSpace;
                        Scalar increment = fsPrev[m] - figuredPrev[m] + figuredHere[m];
                        Scalar moved = increment - prv.SecBarFigLift[m];
                        d[m] = running[m] + (regularised ? 0.0 : -dPrevRatio * moved);
                    }
                    delta[i] = d;
                    Array.Copy(d, running, 6);
                    continue;
                }

                if (options.Equation688BracketInDagger)
                {
                    // M (68.8), second member of the (q~ - q) group, for s_1p:
                    //     alpha [ ('A-_p - 2'A_q) + (2q~ - q)'A_p ]
                    // with alpha = c-_1 y_p^4, which is the scheme's figured primary, and the
                    // three accumulations as the surface found them.
                    var pt = prv.T;
                    Scalar alpha = prv.ApFigured;
                    Scalar bracket = alpha * ((pt[16] - 2.0 * pt[20])
                                              + (2.0 * prv.Rho - pt[6]) * pt[15]);
                    d[0] = running[0] - dPrevRatio * bracket;
                    for (int m = 1; m < 6; m++) d[m] = running[m];
                    delta[i] = d;
                    Array.Copy(d, running, 6);
                    continue;
                }

                for (int m = 0; m < 6; m++)
                {
                    // Where the previous surface is flat and faces collimated space its q is
                    // infinite and the whole correction is absent from the arrangement - the
                    // bracket there is regularised as a limit rather than formed as a product -
                    // so there is nothing to change and the accumulated delta simply carries.
                    bool regularised = m == 5 && prv.FlatInCollimatedSpace;
                    Scalar bracket = prv.SecBarFig[m] - prv.SecBarFigLift[m];
                    d[m] = running[m] + (regularised ? 0.0 : -dPrevRatio * bracket);
                }
            }

            delta[i] = d;
            Array.Copy(d, running, 6);
        }

        return delta;
    }

    /// <summary>
    /// One pass of (84.23) / (85.3): ten tertiary coefficients and their barred partners, from
    /// whichever half's quantities have been loaded and whichever family has been selected.
    ///
    /// <para><paramref name="carry"/> is the pass's ratio - q for the hat half, q-tilde for the
    /// check. With homogeneous inputs it is a single scalar for the whole pass, which is what
    /// lets the barred entries be read off the unbarred ones.</para>
    /// </summary>
    private static void Pass(Scalar[] t, Scalar carry, Scalar[] outTotals, Scalar[] outBarred,
                             Options options, bool carryIsInfinite = false,
                             Scalar qT152 = default, bool checkHalf = false)
    {
        // M (26.2) gives b_p = 2 q a_p, and that is how b reaches the arrangement: a bare ratio
        // beside an accumulated family member, the pair multiplying this surface's own a. In the
        // CHECK half the primary is a_v and its partner is b_v = 2 q~ a_v - the height ratio,
        // not the incidence ratio.
        Scalar bq = carry;

        // The ratio the INTRINSIC chain travels on. Reading it as part of the split makes it the
        // pass's own ratio; in the hat pass the two are the same number, so the spherical result
        // is bit-identical either way.
        Scalar chain = options.IntrinsicChainOnPassRatio ? carry : t[6];

        t[134] = 6.0 * chain * t[121] + t[122];
        t[136] = 0.5 * (t[134] + t[122]) * chain + t[123] + t[124];
        t[138] = 4.0 * (t[136] - t[123] + t[124]);
        t[140] = (-2.0 * chain * t[134] + 4.0 * t[136] + t[138] + 8.0 * t[124]) * chain + t[125];
        t[143] = ((t[123] + t[124] - t[136]) * chain + 0.5 * t[140] + 0.5 * t[125]) * chain
                 + t[126];
        t[145] = (2.0 / 3.0) * (4.0 * (t[124] - t[123]) * chain + t[140] - t[125]) + t[127];
        t[147] = (4.0 * (t[124] - t[123]) * chain + 3.0 * t[127] - 2.0 * t[125]) * chain
                 + t[128] - 4.0 * t[126] + 4.0 * t[143];
        t[149] = ((4.0 * chain * t[124] + t[125] + 0.75 * t[127] - 0.75 * t[145]) * chain
                  + t[147] + 2.0 * t[126] + t[128]) * chain + t[129];
        t[151] = ((chain * t[121] + t[122]) * chain + t[123] + 9.0 * t[124]) * chain
                 + t[125] + t[127];
        t[152] = ((chain * t[151] + t[126] + t[128]) * chain + t[129]) * chain + t[130];

        t[131] = 4.0 * ((-t[8] * t[15] + 2.0 * t[44]) * 0.125 * t[15] - t[20] * t[38]);
        t[132] = (t[25] * t[25] + 3.0 * t[101]) * t[10] + t[25] * t[40] + t[121] + t[131];

        t[135] = t[134]
               + 4.0 * (0.5 * (t[26] + t[27]) * t[25] + bq * t[101]
                        + 0.5 * t[102] + 0.75 * t[103]) * t[10]
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
               + (2.0 * (bq * t[107] + t[26] * t[30]) + t[29] * t[29] + 3.0 * t[113]) * t[10]
               - (0.5 * t[18] * t[18] + t[16] * t[19] + t[74]) * t[8]
                 + t[29] * t[51] + t[25] * t[66]
               + 2.0 * (2.0 * t[16] * t[65] + t[50] * t[85])
                 - t[24] * t[44] + t[13] * t[105]
               + 0.5 * t[59] * t[84];

        t[146] = t[145]
               + 2.0 * ((2.0 * bq * t[108] + t[27] * t[28] + t[109]) * t[10] + t[54] * t[84])
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

        outTotals[1] = t[132];  outTotals[2] = t[135];  outTotals[3] = t[137];
        outTotals[4] = t[139];  outTotals[5] = t[141];  outTotals[6] = t[144];
        outTotals[7] = t[146];  outTotals[8] = t[148];  outTotals[9] = t[150];
        outTotals[10] = t[155];

        // M (84.23): the barred entries follow from the unbarred ones by replacing the intrinsic
        // part with the ratio times itself and every intermediate by its barred form.
        // The two families of term in the barred rule, separable so that either can be probed.
        // In the hat half both are always present; the flags act on the check half alone.
        Scalar pt = checkHalf && options.DropOwnPrimaryTimesTertiaryFamilyInCheckBarred
                  ? 0.0 : 1.0;                                  // own primary x tertiary family
        Scalar ds = checkHalf && options.DropDaggerTimesOwnSecondaryInCheckBarred
                  ? 0.0 : 1.0;                                  // dagger x own secondary

        // Each product is scaled in place, leaving the order and association of the sums exactly
        // as they were - a regrouping here changes the rounding and the parity gate fails, which
        // is how the first attempt at this probe was caught.
        // The surface's own first secondary, as the dagger terms pair with it. Identical to
        // t[40] unless the candidate is in force, so the parity gate is untouched.
        Scalar ownS1 = checkHalf && options.IntrinsicSecondaryInCheckBarred ? t[38] : t[40];

        outBarred[1] = carry * t[132] + pt * t[10] * t[115] + ds * t[31] * ownS1;
        outBarred[2] = carry * (pt * 2.0 * t[10] * t[115] + t[135])
                     + pt * t[10] * t[116] + ds * t[31] * t[45] + ds * t[32] * ownS1;
        outBarred[3] = carry * t[137]
                     + pt * t[10] * t[117] + pt * t[13] * t[115]
                     + ds * t[31] * t[51] + ds * t[33] * ownS1;
        outBarred[4] = carry * (pt * 2.0 * t[116] * t[10] + t[139])
                     + pt * t[118] * t[10] + ds * t[31] * t[55] + ds * t[32] * t[45];
        outBarred[5] = carry * (pt * 2.0 * t[117] * t[10] + t[141])
                     + pt * t[119] * t[10] + pt * t[13] * t[116]
                     + ds * t[31] * t[61] + ds * t[32] * t[51] + ds * t[33] * t[45];
        outBarred[6] = carry * t[144]
                     + pt * t[10] * t[120] + pt * t[13] * t[117]
                     + ds * t[31] * t[66] + ds * t[33] * t[51];
        outBarred[7] = carry * (pt * 2.0 * t[10] * t[118] + t[146]) + ds * t[32] * t[55];
        outBarred[8] = carry * (pt * 2.0 * t[10] * t[119] + t[148])
                     + pt * t[13] * t[118] + ds * t[32] * t[61] + ds * t[33] * t[55];
        outBarred[9] = carry * (pt * 2.0 * t[10] * t[120] + t[150])
                     + pt * t[13] * t[119] + ds * t[32] * t[66] + ds * t[33] * t[61];

        // Where the ratio is infinite, q t155 cannot be formed as a product. t155 differs from
        // t152 by 4 t19 t65 + t153 + t154, which a surface with nothing accumulated ahead of it
        // does not have; the remainder keeps the plain product, so nothing else moves.
        Scalar carriedT155 = carryIsInfinite ? qT152 + carry * (t[155] - t[152])
                                             : carry * t[155];
        outBarred[10] = carriedT155 + pt * t[13] * t[120] + ds * t[33] * t[66];
    }
}
