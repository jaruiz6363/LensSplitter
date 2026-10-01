namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// What a merit function operand measures.
///
/// <para>Every one of these is differentiated analytically. The paraxial and first-order
/// quantities come from the differentiated paraxial recurrence, the real-ray ones from the
/// differentiated skew trace - which is Feder's differential ray tracing arrived at by carrying
/// dual numbers through the ordinary trace rather than by writing the differentiated equations
/// out by hand - and <see cref="PRMSA"/> from the differentiated Buchdahl chain. There is no
/// finite difference anywhere in this optimiser.</para>
/// </summary>
public enum OperandType
{
    /// <summary>
    /// The predicted RMS spot radius over every field and wavelength at once, weighted.
    ///
    /// <para>Robb's analytic merit function: a quadratic form in the thirty-seven Buchdahl
    /// coefficients, with the weighted mean taken over the mean-SQUARE radii and the root
    /// taken once at the end. No rays are traced to get it. This is the same quantity the
    /// report prints as PRMSA, computed the same way, so optimising it and reading it back
    /// cannot disagree.</para>
    /// </summary>
    PRMSA = 0,

    /// <summary>Total track: the first surface to the image plane, along the axis.</summary>
    TTL,

    /// <summary>Effective focal length.</summary>
    EFL,

    // ── Paraxial ray ────────────────────────────────────────────────────────────────────
    // Where the paraxial ray of a given field and pupil coordinate is at a surface, and which
    // way it is going. X is sagittal, Y meridional, Z along the axis; L, M and N are the
    // corresponding direction cosines.

    /// <summary>Paraxial ray height, sagittal. Zero for a meridional ray.</summary>
    PX,
    /// <summary>Paraxial ray height, meridional.</summary>
    PY,
    /// <summary>Paraxial ray position along the axis at the surface. Zero: the paraxial ray
    /// meets a surface at its vertex plane, which is what makes it paraxial.</summary>
    PZ,
    /// <summary>Paraxial direction cosine, sagittal.</summary>
    PL,
    /// <summary>Paraxial direction cosine, meridional.</summary>
    PM,
    /// <summary>Paraxial direction cosine, axial.</summary>
    PN,

    // ── Real ray ────────────────────────────────────────────────────────────────────────

    /// <summary>Real ray height at the surface, sagittal.</summary>
    RX,
    /// <summary>Real ray height at the surface, meridional.</summary>
    RY,
    /// <summary>Real ray position along the axis at the surface: the sag at the point of
    /// incidence, which is what separates a real ray from a paraxial one.</summary>
    RZ,
    /// <summary>Real direction cosine after refraction, sagittal.</summary>
    RL,
    /// <summary>Real direction cosine after refraction, meridional.</summary>
    RM,
    /// <summary>Real direction cosine after refraction, axial.</summary>
    RN,

    // ── Manufacturability ───────────────────────────────────────────────────────────────

    /// <summary>Edge thickness of a GLASS element, at the clear aperture.</summary>
    EGT,

    /// <summary>Edge thickness of an AIR space, at the clear aperture.</summary>
    EAT,

    /// <summary>
    /// Diameter-to-thickness ratio of an element: clear diameter over centre thickness. A
    /// blank too wide for its thickness will not survive being ground, so this is held under
    /// a limit rather than driven to a target.
    /// </summary>
    DTRGT,

    // ── Chromatic and distortion, on real rays ──────────────────────────────────────────

    /// <summary>
    /// Real lateral colour: the spread in real chief-ray image height between the extreme
    /// wavelengths, at the field asked for.
    /// </summary>
    LCF,

    /// <summary>
    /// Real axial colour: the axial distance between where the real marginal ray crosses the
    /// axis at the extreme wavelengths. A longitudinal quantity, positive when the longer
    /// wavelength focuses further away.
    /// </summary>
    AXC,

    /// <summary>
    /// Real distortion, per cent: the real chief-ray height at the image against the paraxial
    /// height it should have had, as a percentage of that height.
    /// </summary>
    DISTF,

    // ── As built ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The RMS wavefront error a stated decentre and tilt tolerance will induce, averaged over
    /// the field. Nodal aberration theory, after Gu 2020.
    ///
    /// <para>Every other operand here measures the design that was DRAWN. This one measures how
    /// much of that performance survives being built: a misaligned surface still contributes its
    /// own rotationally symmetric aberration field, only displaced, and the displacement is
    /// linear in the perturbation - so the induced coma and astigmatism follow in closed form
    /// from the paraxial marginal and chief rays. No rays are traced for it beyond those two,
    /// and its derivative is exact like everything else here.</para>
    ///
    /// <para>It is a companion to <see cref="PRMSA"/> and not a replacement: PRMSA says how good
    /// the design is, this says how much of that is real. A design can always be pushed to a
    /// smaller predicted spot by making itself more delicate, and without this term in the merit
    /// function nothing notices.</para>
    ///
    /// <para>Units are the design's length units, not waves. The two tolerances are given as
    /// inputs - decentre first, then tilt in DEGREES, the same unit the .align sidecar states a
    /// tilt in and the only angular unit this program uses.</para>
    /// </summary>
    ASBLT,

    /// <summary>
    /// ONE NAMED ABERRATION COEFFICIENT, in transverse measure - the quantity the report prints
    /// under that name, computed the same way, so targeting one and reading it back cannot
    /// disagree.
    ///
    /// <para>Which coefficient is carried on <see cref="Operand.Coefficient"/> rather than by a
    /// member per coefficient, because there are thirty-seven of them and
    /// <c>BuchdahlTerms.Names</c> already holds the list. In a merit function the coefficient's
    /// own name IS the operand: <c>B, 1, TAR 0</c> and <c>TAU15, 2, TAR 0</c>. That is
    /// deliberate - the name a designer reads in the report is the name they type here, with no
    /// table in between.</para>
    ///
    /// <para><b>Why this is worth having beside PRMSA.</b> The predicted spot mixes eighteen
    /// coefficients into one number, and a spot is a poor instrument for asking about any single
    /// one of them: two designs whose tau15 differs by a factor of five predict the same spot to
    /// one part in ten thousand, which is measured in <c>docs/verification.md</c> and not
    /// assumed. A designer correcting a NAMED aberration - flattening the field, balancing
    /// oblique spherical against fifth-order astigmatism - is asking about the coefficient
    /// itself, and asking PRMSA instead is asking a question that cannot hear the answer.</para>
    ///
    /// <para>It is also what makes a merit function with no rays in it practical. Shafer's
    /// argument for that, "it is much quicker to try out many different configurations and ideas
    /// if there are no rays in the merit function and you are only correcting the 3rd and
    /// 5th-order aberrations", needs the individual coefficients to be targetable, not only
    /// their weighted sum. See <c>docs/references.md</c>.</para>
    ///
    /// <para><b>The system's, or one surface's share of it.</b> A surface number picks out the
    /// contribution of that surface; surface 0, or none, is the whole system. The shares add to
    /// the total exactly, which is what lets a designer compare one surface with another and with
    /// the system. That is the question Shafer says a design is decided by and a total cannot be
    /// asked: not whether the design is wrong but WHICH SURFACE, and whether it is that surface's
    /// own doing. <c>Tau2</c> to <c>Tau20</c> are system operands only: the report splits them by
    /// surface as a share of the total, but with no intrinsic, figuring or induced split, and on a
    /// figured flat facing collimated light not at all, so a surface or a part is refused rather
    /// than answered with a zero.</para>
    /// </summary>
    ABER,
}
