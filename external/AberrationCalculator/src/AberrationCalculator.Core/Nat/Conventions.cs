using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The two coordinate frames nodal aberration theory has to live in at once, and the conversion
/// between them.
///
/// <para>A freeform or figure error arrives as Fringe Zernike coefficients from an
/// interferometer, and optical testing describes that set in a right-handed frame with the
/// azimuth <b>theta measured counter-clockwise from the x axis</b>. NAT, to agree with the
/// commercial ray-trace programs, measures its <b>phi clockwise from the y axis</b>. The two
/// differ by a reflection and a quarter turn, and the conversion is NOT the same for every
/// Zernike pair: it depends on the azimuthal frequency.</para>
///
/// <para>Every one of the three Rochester papers spends a paragraph on this and each states the
/// conversion as a rewritten arctangent rather than as an offset:</para>
///
/// <list type="table">
/// <item><term>astigmatism Z5/6</term>
///       <description><c>(1/2)[pi/2 - atan(z6/z5)]</c> - Fuerschbach 2014 Eq. (7)</description></item>
/// <item><term>coma Z7/8</term>
///       <description><c>pi/2 - atan(z8/z7)</c> - Fuerschbach 2014 Eq. (18)</description></item>
/// <item><term>trefoil Z10/11</term>
///       <description><c>(1/3) atan(z10/z11)</c> - Fuerschbach 2012 Eq. (8)</description></item>
/// </list>
///
/// <para><b>Note the trefoil row: the arguments are swapped, not offset.</b> Schmid 2010 Eq. (8)
/// carries the same reversal for astigmatism as a sign flip inside the arctangent. A reader who
/// assumes one rule for all three gets two of them wrong.</para>
///
/// <para>The single-argument arctangent the papers print folds a full turn onto a half one, so
/// the two-argument form is used here throughout. That is a strict improvement and not a
/// departure: it agrees with the printed formula wherever the printed formula is defined.</para>
///
/// <para>Nothing in this file is guessed. Where a paper prints an orientation, that orientation
/// is what is returned. The tests pin them against the published surface maps - Fuerschbach 2012
/// Fig. 3 and 2014 Fig. 3 both mark where the surface is a peak rather than a valley, which
/// fixes the answer unambiguously.</para>
/// </summary>
public static class Conventions
{
    private const double HalfPi = Math.PI / 2.0;

    /// <summary>
    /// Magnitude of a Zernike pair: the root of the sum of squares, which is the same in either
    /// frame because a rotation does not change a length.
    /// </summary>
    public static Scalar Magnitude(Scalar a, Scalar b) => SMath.Sqrt(a * a + b * b);

    // ── Optical testing frame: theta counter-clockwise from x ────────────────────────────

    /// <summary>Astigmatism orientation as an interferogram reports it (Fuerschbach 2014 Eq. 5).</summary>
    public static Scalar TestAstigmatism(Scalar z5, Scalar z6) => 0.5 * SMath.Atan2(z6, z5);

    /// <summary>Coma orientation as an interferogram reports it (Fuerschbach 2014 Eq. 17-18).</summary>
    public static Scalar TestComa(Scalar z7, Scalar z8) => SMath.Atan2(z8, z7);

    /// <summary>Trefoil orientation as an interferogram reports it (Fuerschbach 2012 Eq. 6).</summary>
    public static Scalar TestTrefoil(Scalar z10, Scalar z11) =>
        (1.0 / 3.0) * SMath.Atan2(z11, z10);

    // ── NAT frame: phi clockwise from y ──────────────────────────────────────────────────

    /// <summary>
    /// Astigmatism orientation in the NAT frame - Fuerschbach 2014 Eq. (7).
    /// </summary>
    public static Scalar NatAstigmatism(Scalar z5, Scalar z6) =>
        0.5 * (HalfPi - SMath.Atan2(z6, z5));

    /// <summary>
    /// Coma orientation in the NAT frame - Fuerschbach 2014 Eq. (18).
    /// </summary>
    public static Scalar NatComa(Scalar z7, Scalar z8) => HalfPi - SMath.Atan2(z8, z7);

    /// <summary>
    /// Trefoil orientation in the NAT frame - Fuerschbach 2012 Eq. (8).
    ///
    /// <para><b>The arguments are exchanged relative to the testing form</b>, which is what the
    /// paper prints and is not a transcription slip: for threefold symmetry the reflection
    /// between the frames cannot be absorbed into an offset.</para>
    /// </summary>
    public static Scalar NatTrefoil(Scalar z10, Scalar z11) =>
        (1.0 / 3.0) * SMath.Atan2(z10, z11);

    // ── The field-constant vectors a surface at the stop contributes ─────────────────────

    /// <summary>
    /// The field-constant astigmatism a Zernike astigmatism overlay contributes when the surface
    /// is at the stop - Fuerschbach 2014 Eq. (9), <c>2(n' - n) z5/6 exp(i 2 phi)</c>.
    ///
    /// <para>Returned as the vector <c>B222^2</c> of NAT, which already carries the doubled
    /// orientation; it is added straight to the misalignment-induced <c>B222^2</c> as Schmid
    /// 2010 Eq. (9) does.</para>
    /// </summary>
    public static Vec2 AstigmatismOverlay(Scalar z5, Scalar z6, Scalar nBefore, Scalar nAfter) =>
        Vec2.FromPolar(2.0 * (nAfter - nBefore) * Magnitude(z5, z6),
                       2.0 * NatAstigmatism(z5, z6));

    /// <summary>
    /// The field-constant coma a Zernike coma overlay contributes at the stop - Fuerschbach 2014
    /// Eq. (21), <c>3(n' - n) z7/8 exp(i phi)</c>. Returned as the vector <c>A131</c>.
    /// </summary>
    public static Vec2 ComaOverlay(Scalar z7, Scalar z8, Scalar nBefore, Scalar nAfter) =>
        Vec2.FromPolar(3.0 * (nAfter - nBefore) * Magnitude(z7, z8), NatComa(z7, z8));

    /// <summary>
    /// The field-constant elliptical coma a Zernike TREFOIL overlay contributes at the stop -
    /// Fuerschbach, Rolland and Thompson, <i>Opt. Express</i> <b>22</b>, 26585 (2014), Eq. (34):
    /// <c>4(n' - n) z10/11 exp(i 3 phi)</c>. Returned as the cubic vector <c>C^3_333</c>.
    ///
    /// <para><b>The coefficient is four, not three.</b> Fringe <c>Z10/11</c> is <c>rho^3 cos3phi</c>
    /// as a sag, and <c>cos 3t = 4 cos^3 t - 3 cos t</c>, so the part landing on <c>W333</c> -
    /// whose pupil dependence is <c>cos^3</c> - carries the four. The leftover <c>-3 cos t</c> is
    /// a pupil tilt, which displaces the image rather than blurring it. Coma's overlay carries
    /// three for the same reason and astigmatism's carries two; a reader who assumes one rule
    /// gets two of them wrong.</para>
    ///
    /// <para>How it enters the field is Table 2 of that paper, and the sign there is NEGATIVE:
    /// <c>C^3_333 = ALIGN C^3_333 - sum_j FF C^3_333,j</c>. That is not a misprint - the NAT form
    /// it is being matched against, Thompson 2010 Eq. (B11), carries <c>- c^3</c>.</para>
    /// </summary>
    public static Vec2 TrefoilOverlay(Scalar z10, Scalar z11, Scalar nBefore, Scalar nAfter) =>
        Vec2.FromPolar(4.0 * (nAfter - nBefore) * Magnitude(z10, z11),
                       3.0 * NatTrefoil(z10, z11));

    /// <summary>
    /// Oblique spherical aberration orientation in the NAT frame - Fuerschbach 2014 Eq. (39).
    /// A two-theta quantity, so it takes astigmatism's form.
    /// </summary>
    public static Scalar NatObliqueSpherical(Scalar z12, Scalar z13) =>
        0.5 * (HalfPi - SMath.Atan2(z13, z12));

    /// <summary>
    /// Fifth-order aperture coma orientation in the NAT frame - Fuerschbach 2014 Eq. (49). A
    /// one-theta quantity, so it takes coma's form.
    /// </summary>
    public static Scalar NatFifthOrderComa(Scalar z14, Scalar z15) =>
        HalfPi - SMath.Atan2(z15, z14);

    /// <summary>
    /// The field-constant OBLIQUE SPHERICAL aberration a Zernike <c>Z12/13</c> overlay contributes
    /// at the stop - Fuerschbach 2014 Eq. (42), <c>8(n' - n) z12/13 exp(i 2 phi)</c>. Returned as
    /// the vector <c>B^2_242</c>.
    /// </summary>
    public static Vec2 ObliqueSphericalOverlay(Scalar z12, Scalar z13,
                                               Scalar nBefore, Scalar nAfter) =>
        Vec2.FromPolar(8.0 * (nAfter - nBefore) * Magnitude(z12, z13),
                       2.0 * NatObliqueSpherical(z12, z13));

    /// <summary>
    /// The field-constant FIFTH-ORDER APERTURE COMA a Zernike <c>Z14/15</c> overlay contributes at
    /// the stop - Fuerschbach 2014 Eq. (52), <c>10(n' - n) z14/15 exp(i phi)</c>. Returned as the
    /// vector <c>A_151</c>.
    /// </summary>
    public static Vec2 FifthOrderComaOverlay(Scalar z14, Scalar z15,
                                             Scalar nBefore, Scalar nAfter) =>
        Vec2.FromPolar(10.0 * (nAfter - nBefore) * Magnitude(z14, z15),
                       NatFifthOrderComa(z14, z15));

    /// <summary>
    /// The ASTIGMATISM a raw Fringe <c>Z12</c> carries, which must not be dropped.
    ///
    /// <para>Fringe <c>Z12 = 4 rho^4 cos2phi - 3 rho^2 cos2phi</c>. Only the quartic part is
    /// oblique spherical; the quadratic part is exactly <c>-3 Z5</c>, and it blurs. Fuerschbach
    /// works with an ADJUSTED Zernike, <c>Z12 + 3 Z5</c>, to isolate the quartic - which is the
    /// same statement seen from the other side. The <c>.align</c> sidecar states raw Fringe sag,
    /// so the leftover astigmatism is routed to the astigmatism overlay here rather than
    /// silently lost.</para>
    ///
    /// <para>Coma's own overlay needs no such thing: Fringe <c>Z7</c>'s leftover is a pupil tilt,
    /// which displaces the image instead of blurring it.</para>
    /// </summary>
    public static Scalar AstigmatismCarriedByObliqueSpherical(Scalar z12) => -3.0 * z12;

    /// <summary>
    /// The THIRD-ORDER COMA a raw Fringe <c>Z14</c> carries.
    ///
    /// <para><c>Z14 = 10 rho^5 cos - 12 rho^3 cos + 3 rho cos</c>, and with
    /// <c>Z7 = 3 rho^3 cos - 2 rho cos</c> the remainder after the quintic is
    /// <c>-4 Z7 - 5 rho cos</c>. The first blurs and is returned; the second is a tilt and does
    /// not.</para>
    /// </summary>
    public static Scalar ComaCarriedByFifthOrderComa(Scalar z14) => -4.0 * z14;

    /// <summary>
    /// The beam displacement across a surface away from the stop, <c>ybar / y</c> - Fuerschbach
    /// 2014 Eq. (1). Multiplying it by the field vector gives the decentre the beam sees, and it
    /// is what turns every field-constant overlay contribution into a field-dependent one.
    ///
    /// <para>Zero at a surface where the marginal ray height vanishes, which is a pupil: there
    /// the beam does not walk and the contribution stays field constant, which is the whole
    /// content of the distinction.</para>
    /// </summary>
    public static Scalar BeamDisplacement(Scalar marginalHeight, Scalar chiefHeight) =>
        SMath.Abs(marginalHeight) < 1e-15 ? 0.0 : chiefHeight / marginalHeight;
}
