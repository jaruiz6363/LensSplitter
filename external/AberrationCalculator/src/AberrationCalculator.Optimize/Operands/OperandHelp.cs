using System;
using System.Collections.Generic;

namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// What each operand measures, in one line.
///
/// <para>The companion to <see cref="OperandInputs"/>, which says what an operand must be TOLD.
/// This says what it gives back. Between them a help page can be generated rather than written,
/// so the twenty-two operands cannot drift away from their own documentation the way a
/// hand-maintained list always eventually does.</para>
///
/// <para>These are deliberately one line each. The full account of an operand - why real lateral
/// colour is measured between the extreme wavelengths and not against the reference, what makes
/// PRMSA a prediction rather than a measurement - lives in the XML documentation on
/// <see cref="OperandType"/> and in <c>docs/optimizer.md</c>, where there is room for it.</para>
/// </summary>
public static class OperandHelp
{
    /// <summary>Every operand, in the order the enum declares them.</summary>
    public static IReadOnlyList<OperandType> All =>
        (OperandType[])Enum.GetValues(typeof(OperandType));

    /// <summary>What this operand measures.</summary>
    public static string Summary(OperandType type) => type switch
    {
        OperandType.PRMSA =>
            "Predicted RMS spot radius over every field and wavelength at once, weighted. "
          + "No rays are traced to get it.",
        OperandType.TTL => "Total track: first surface to image plane, along the axis.",
        OperandType.EFL => "Effective focal length.",

        OperandType.PX => "Paraxial ray height at the surface, sagittal.",
        OperandType.PY => "Paraxial ray height at the surface, meridional.",
        OperandType.PZ =>
            "Paraxial ray position along the axis at the surface. Always zero - a paraxial ray "
          + "meets a surface at its vertex plane.",
        OperandType.PL => "Paraxial direction cosine, sagittal.",
        OperandType.PM => "Paraxial direction cosine, meridional.",
        OperandType.PN => "Paraxial direction cosine, axial.",

        OperandType.RX => "Real ray height at the surface, sagittal.",
        OperandType.RY => "Real ray height at the surface, meridional.",
        OperandType.RZ =>
            "Real ray position along the axis: the sag at the point of incidence.",
        OperandType.RL => "Real direction cosine after refraction, sagittal.",
        OperandType.RM => "Real direction cosine after refraction, meridional.",
        OperandType.RN => "Real direction cosine after refraction, axial.",

        OperandType.EGT => "Edge thickness of a GLASS element, at the clear aperture.",
        OperandType.EAT => "Edge thickness of an AIR space, at the clear aperture.",
        OperandType.DTRGT =>
            "Diameter-to-thickness ratio: clear diameter over centre thickness. Hold it under a "
          + "limit - a blank too wide for its thickness will not survive being ground.",

        OperandType.LCF =>
            "Real lateral colour: the spread in real chief-ray image height between the extreme "
          + "wavelengths, at the field asked for.",
        OperandType.AXC =>
            "Real axial colour: the axial distance between the real marginal ray's axis "
          + "crossings at the extreme wavelengths.",
        OperandType.DISTF =>
            "Real distortion, per cent: real chief-ray height against the paraxial height it "
          + "should have had.",

        OperandType.ABER =>
            "One named aberration coefficient, in transverse measure, for the system, for ONE "
          + "SURFACE, or for one PART of a surface's contribution - .INT, .FIG or .IND - "
          + "written as the "
          + "coefficient's own name, which is how the report spells it: B, F, C, Pi, E; B5, F1, "
          + "F2, M1, M2, M3, N1, N2, N3, C5, Pi5, E5; B7 and Tau2 to Tau20.",

        OperandType.ASBLT =>
            "RMS wavefront error a decentre and tilt tolerance would induce, averaged over the "
          + "field. Nodal aberration theory: what survives being BUILT, not what was drawn.",

        _ => string.Empty,
    };

    /// <summary>
    /// A merit-function line that would actually work, for this operand.
    ///
    /// <para>An example is worth more than a signature here, because the inputs are positional:
    /// seeing <c>RY, 1, TAR 0, 7, 1, 1, 0, 1</c> settles what the five trailing numbers are in a
    /// way that "surface, wave, hy, px, py" alone does not.</para>
    /// </summary>
    public static string Example(OperandType type) => type switch
    {
        OperandType.PRMSA => "PRMSA, 1, TAR 0",
        OperandType.TTL => "TTL, 5, MAX 60",
        OperandType.EFL => "EFL, 100, TAR 50, 2",
        OperandType.AXC => "AXC, 5, TAR 0",
        OperandType.LCF => "LCF, 5, TAR 0, 1.0",
        OperandType.DISTF => "DISTF, 10, MIN -2, MAX 2, 0.7",
        OperandType.EGT => "EGT, 10, MIN 1, 2, 4",
        OperandType.EAT => "EAT, 10, MIN 0.5, 2, 4",
        OperandType.DTRGT => "DTRGT, 10, MIN 1.5, MAX 12, 2, 4",
        OperandType.ABER => "M2, 1, TAR 0, 5        # surface 5's share; drop the 5 for the system",
        OperandType.ASBLT => "ASBLT, 10, TAR 0, 0.04, 0.15",
        _ => type + ", 1, TAR 0, 7, 1, 1, 0, 1",
    };

    /// <summary>The operand of that name, or null. Case does not matter.</summary>
    public static OperandType? Find(string word)
    {
        foreach (var type in All)
            if (string.Equals(type.ToString(), word, StringComparison.OrdinalIgnoreCase))
                return type;

        // A coefficient's own name is how a coefficient operand is written, so it has to be how
        // one is asked about. Without this, HELP B and HELP TAU15 - the two things a designer is
        // most likely to type after reading a coefficient off the report - answer that no such
        // operand exists, which is both wrong and discouraging in the same breath.
        foreach (string name in AberrationCalculator.Core.Aberrations.BuchdahlTerms.Names)
            if (string.Equals(name, word, StringComparison.OrdinalIgnoreCase))
                return OperandType.ABER;

        return null;
    }
}
