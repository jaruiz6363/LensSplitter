using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// Buchdahl's deformation and retardation coefficients of the wave front, and their
/// identification with Thompson's <c>Wklm</c>.
///
/// <para>Source: Buchdahl, H. A., "Optical Aberration Coefficients. VII. The Primary, Secondary,
/// and Tertiary Deformation and Retardation of the Wave Front," <i>J. Opt. Soc. Am.</i>
/// <b>50</b>, 539 (1960). Referred to below by equation number alone.</para>
///
/// <para><b>Why this exists.</b> Nodal aberration theory displaces each surface's rotationally
/// symmetric wave aberration by that surface's sigma vector, so it needs <c>Wklm</c>. This
/// program computes transverse aberration coefficients in Rimmer's notation. The bridge between
/// them is NOT a gradient: the attempt to make it one is recorded, and refuted, in
/// <c>docs/nat-development.md</c>. Buchdahl supplies the real relation.</para>
///
/// <para><b>The identification is forced by counting.</b> Eq. (2.3) defines the rotational
/// invariants</para>
/// <code>
///     lambda = y^2 + z^2      mu = y hy' + z hz'      nu = hy'^2 + hz'^2
/// </code>
/// <para>which in polar pupil coordinates are <c>rho^2</c>, <c>rho H cos(theta)</c> and
/// <c>H^2</c>. Eq. (2.8) expands the deformation as a sum of monomials
/// <c>lambda^a mu^b nu^c</c>, and one such monomial is one Hopkins term:</para>
/// <code>
///     lambda^a mu^b nu^c  ->  W[k = b + 2c, l = 2a + b, m = b]
/// </code>
/// <para>There are five such monomials of degree two, nine of degree three and fourteen of
/// degree four once the field-only piston term is dropped - and Eq. (2.8) has exactly five
/// <c>pi</c>, nine <c>sigma</c> and fourteen <c>tau</c>. Buchdahl states the count himself: the
/// system "is effectively specified by 5+9+14 = 28 coefficients". So <c>sigma1..sigma9</c> IS
/// Thompson's fifth-order set, with no freedom left over.</para>
///
/// <para><b>Deformation is not retardation.</b> <c>D</c> is the geometric deformation of the
/// wave front; <c>R</c> is its optical retardation, Eq. (3.1). Thompson's <c>W</c> is a wave
/// aberration, so it is <c>R</c>. The two agree at third order but not at fifth, and in
/// Buchdahl's own Table I they differ by 21% at <c>sigma6</c> - a plausible wrong number rather
/// than an obvious one. <see cref="ToRetardation"/> is the conversion.</para>
/// </summary>
public readonly struct Deformation
{
    /// <summary>Primary coefficients of Eq. (2.8): the terms in lambda^2 .. mu nu.</summary>
    public readonly Scalar Pi1, Pi2, Pi3, Pi4, Pi5;

    /// <summary>Secondary coefficients of Eq. (2.8): the terms in lambda^3 .. mu nu^2.</summary>
    public readonly Scalar Sigma1, Sigma2, Sigma3, Sigma4, Sigma5, Sigma6, Sigma7, Sigma8, Sigma9;

    public Deformation(
        Scalar pi1, Scalar pi2, Scalar pi3, Scalar pi4, Scalar pi5,
        Scalar s1, Scalar s2, Scalar s3, Scalar s4, Scalar s5,
        Scalar s6, Scalar s7, Scalar s8, Scalar s9)
    {
        Pi1 = pi1; Pi2 = pi2; Pi3 = pi3; Pi4 = pi4; Pi5 = pi5;
        Sigma1 = s1; Sigma2 = s2; Sigma3 = s3; Sigma4 = s4; Sigma5 = s5;
        Sigma6 = s6; Sigma7 = s7; Sigma8 = s8; Sigma9 = s9;
    }

    // Eq. (2.8) read as Hopkins terms:
    //
    //   pi1 lambda^2   -> rho^4             W040     sigma1 lambda^3     -> rho^6             W060
    //   pi2 lambda mu  -> rho^3 H cos       W131     sigma2 lambda^2 mu  -> rho^5 H cos       W151
    //   pi3 lambda nu  -> rho^2 H^2         W220     sigma3 lambda^2 nu  -> rho^4 H^2         W240
    //   pi4 mu^2       -> rho^2 H^2 cos^2   W222     sigma4 lambda mu^2  -> rho^4 H^2 cos^2   W242
    //   pi5 mu nu      -> rho H^3 cos       W311     sigma5 lambda mu nu -> rho^3 H^3 cos     W331
    //                                                sigma6 lambda nu^2  -> rho^2 H^4         W420
    //                                                sigma7 mu^3         -> rho^3 H^3 cos^3   W333
    //                                                sigma8 mu^2 nu      -> rho^2 H^4 cos^2   W422
    //                                                sigma9 mu nu^2      -> rho H^5 cos       W511

    /// <summary>Spherical aberration, <c>pi1</c>.</summary>
    public Scalar W040 => Pi1;

    /// <summary>Coma, <c>pi2</c>.</summary>
    public Scalar W131 => Pi2;

    /// <summary>
    /// Field curvature, <c>pi3</c>. This is the term of Eq. (2.8) as written; NAT uses the
    /// medial one, <see cref="W220M"/>.
    /// </summary>
    public Scalar W220 => Pi3;

    /// <summary>Astigmatism, <c>pi4</c>.</summary>
    public Scalar W222 => Pi4;

    /// <summary>Distortion, <c>pi5</c>.</summary>
    public Scalar W311 => Pi5;

    /// <summary>
    /// Thompson's MEDIAL field curvature, <c>W220 + W222/2</c> - the average of the tangential
    /// and sagittal surfaces, and the one his 2011 Sec. 2 relations are written in.
    ///
    /// <para><b>This is not the quantity <see cref="WaveCoefficients.Third"/> calls
    /// <c>W220M</c>,</b> and the clash is worth stating once rather than being rediscovered. That
    /// one is <c>W220P + W222/2 = (S3 + S4)/4</c>, which is the plain <c>rho^2 H^2</c>
    /// coefficient - <see cref="W220"/> here, and equal to <c>Pi3</c> alone. Measured across five
    /// fixtures, <c>Pi3</c> converted into the design's units reproduces it to every printed
    /// digit. The two differ by <c>W222/2</c>, so a reader who assumes one name means one thing
    /// is out by half the astigmatism - a plausible amount, not an obvious one.</para>
    /// </summary>
    public Scalar W220M => Pi3 + 0.5 * Pi4;

    /// <summary>Fifth-order spherical aberration, <c>sigma1</c>.</summary>
    public Scalar W060 => Sigma1;

    /// <summary>Linear coma, <c>sigma2</c>.</summary>
    public Scalar W151 => Sigma2;

    /// <summary>Oblique spherical aberration, the field-constant part, <c>sigma3</c>.</summary>
    public Scalar W240 => Sigma3;

    /// <summary>Oblique spherical aberration, the astigmatic part, <c>sigma4</c>.</summary>
    public Scalar W242 => Sigma4;

    /// <summary>Elliptical coma, <c>sigma5</c>.</summary>
    public Scalar W331 => Sigma5;

    /// <summary>Fifth-order field curvature, <c>sigma6</c>.</summary>
    public Scalar W420 => Sigma6;

    /// <summary>Elliptical coma, the trefoil part, <c>sigma7</c>.</summary>
    public Scalar W333 => Sigma7;

    /// <summary>Fifth-order astigmatism, <c>sigma8</c>.</summary>
    public Scalar W422 => Sigma8;

    /// <summary>Fifth-order distortion, <c>sigma9</c>.</summary>
    public Scalar W511 => Sigma9;

    /// <summary>
    /// Eq. (3.4): the retardation coefficients from the deformation coefficients.
    ///
    /// <para>The primary coefficients are unchanged. Four of the nine secondary ones are
    /// unchanged too; the other five pick up <c>-1/2 e^-2</c> times a primary coefficient. The
    /// <c>e^-2</c> is the factor Eq. (3.3) carries on the <c>nu D</c> term,</para>
    /// <code>
    ///     R = D - (1/2) e^-2 nu D + (1/2)((3/4) e^-4 nu^2 D + e^-1 D^2) + O(10)
    /// </code>
    /// <para>and it is written out here rather than assumed to be unity, because Eq. (3.4) is
    /// stated under the convention of Eq. (4.1) that <c>e</c> is taken as the unit of length.
    /// Buchdahl's own Table I was computed with <c>e</c> not equal to unity, which is exactly
    /// the trap his Sec. 7(a) warns about.</para>
    ///
    /// <para>The <c>D^2</c> term of Eq. (3.3) contributes only at seventh order and above -
    /// Buchdahl notes its effect "will generally be quite negligible, but not so those due to
    /// the other terms" - so it does not enter the secondary relations at all.</para>
    /// </summary>
    /// <param name="e">The distance from the axial point of the paraxial exit pupil to the axial
    /// point of the ideal image plane: <c>(E'O0') = e</c> of Sec. 2.</param>
    public Deformation ToRetardation(Scalar e)
    {
        Scalar k = 0.5 / (e * e);
        return new Deformation(
            Pi1, Pi2, Pi3, Pi4, Pi5,
            Sigma1,
            Sigma2,
            Sigma3 - k * Pi1,
            Sigma4,
            Sigma5 - k * Pi2,
            Sigma6 - k * Pi3,
            Sigma7,
            Sigma8 - k * Pi4,
            Sigma9 - k * Pi5);
    }

    /// <summary>
    /// Eqs. (6.5) and (6.6): the deformation coefficients from the aberration coefficients
    /// <b>in W coordinates</b>.
    ///
    /// <code>
    ///     4 pi1 = A        pi2 = Ab      2 pi3 = C      2 pi4 = Bb        pi5 = Cb
    ///
    ///     12 sigma1 = 2 S1 + 3A            2 sigma2 = 2 S1b + A + 2Ab
    ///      8 sigma3 = 2 S3 + 2Ab + C
    ///      2 sigma4 = S4 + 2A + 4Ab        2 sigma5 = S5 + 2Ab + C      2 sigma6 = S6
    ///      3 sigma7 = S4b + 4Ab + Bb       2 sigma8 = S5b + Bb + C        sigma9 = S6b
    /// </code>
    ///
    /// <para><b>Each wavefront coefficient is its aberration coefficient plus third-order
    /// terms.</b> That is the whole reason a gradient does not work, and why twelve transverse
    /// coefficients were never in conflict with nine wavefront ones.</para>
    ///
    /// <para><b>W coordinates, not paracanonical.</b> The arguments must already have been
    /// transformed by VI Ch. 6. At third order that transformation is the identity - VI Eq. (6.6)
    /// reads <c>Aa = Ap</c> and so on, and VI Table II shows every primary coefficient equal in
    /// the two systems - so a third-order-only caller may pass paracanonical values unchanged.
    /// The fifth-order arguments may not: in VI Table II they differ by up to 6%.</para>
    /// </summary>
    public static Deformation FromWCoordinates(
        Scalar a, Scalar ab, Scalar bb, Scalar c, Scalar cb,
        Scalar s1, Scalar s1b, Scalar s3, Scalar s4, Scalar s4b,
        Scalar s5, Scalar s5b, Scalar s6, Scalar s6b)
        => new(
            0.25 * a,                       // 4 pi1 = A
            ab,                             //   pi2 = Ab
            0.5 * c,                        // 2 pi3 = C
            0.5 * bb,                       // 2 pi4 = Bb
            cb,                             //   pi5 = Cb
            (2 * s1 + 3 * a) / 12.0,        // 12 sigma1 = 2 S1 + 3A
            (2 * s1b + a + 2 * ab) / 2.0,   //  2 sigma2 = 2 S1b + A + 2Ab
            (2 * s3 + 2 * ab + c) / 8.0,    //  8 sigma3 = 2 S3 + 2Ab + C
            (s4 + 2 * a + 4 * ab) / 2.0,    //  2 sigma4 = S4 + 2A + 4Ab
            (s5 + 2 * ab + c) / 2.0,        //  2 sigma5 = S5 + 2Ab + C
            s6 / 2.0,                       //  2 sigma6 = S6
            (s4b + 4 * ab + bb) / 3.0,      //  3 sigma7 = S4b + 4Ab + Bb
            (s5b + bb + c) / 2.0,           //  2 sigma8 = S5b + Bb + C
            s6b);                           //    sigma9 = S6b
}
