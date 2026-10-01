using System;
using System.Collections.Generic;

namespace AberrationCalculator.Optimize.Operands;

/// <summary>One thing an operand needs to be told, beyond what it is aiming for.</summary>
public enum OperandInput
{
    /// <summary>A surface index, or the first of a span.</summary>
    Surface1,

    /// <summary>The last surface of a span.</summary>
    Surface2,

    /// <summary>
    /// Wavelength, numbered from ONE in the order the lens file lists them.
    ///
    /// <para>There is no zero. Left out, an operand uses the design's reference colour, and that
    /// is expressed by leaving it out rather than by typing a number that means "not a
    /// wavelength" - a merit function should never contain an index that is not an index.</para>
    /// </summary>
    Wave,

    /// <summary>Field height as a fraction of the maximum field, 0 to 1.</summary>
    Hy,

    /// <summary>Pupil coordinate, sagittal, as a fraction of the pupil radius.</summary>
    Px,

    /// <summary>Pupil coordinate, meridional, as a fraction of the pupil radius.</summary>
    Py,

    /// <summary>
    /// A decentre tolerance, in the design's length units: how far off axis any one surface may
    /// end up.
    /// </summary>
    Decentre,

    /// <summary>
    /// A tilt tolerance, in DEGREES - the same unit the .align sidecar uses. One angular unit
    /// throughout, so that a number copied from one place to the other means what it said.
    /// </summary>
    Tilt,
}

/// <summary>
/// What each operand takes, and in what order.
///
/// <para>A merit function line is <c>TYPE, WEIGHT, TAR x, INPUTS</c>, and the inputs are
/// positional - which is short to write and unreadable unless the order is stated in exactly one
/// place. This is that place. The parser reads it, the writer writes by it, and the error message
/// for a line with the wrong number of inputs is generated from it, so the three cannot disagree
/// about what <c>RY, 1, TAR 0, 5, 2, 1, 0, 1</c> means.</para>
///
/// <para>Trailing inputs may be left off and take their defaults, so <c>RY, 1, TAR 0, 5</c> is
/// surface five at the reference colour, the maximum field and the chief ray. Leaving off a
/// LEADING input is not possible, which is why the essential one comes first in every signature:
/// a ray operand is meaningless without a surface, and a distortion without a field.</para>
/// </summary>
public static class OperandInputs
{
    private static readonly OperandInput[] None = Array.Empty<OperandInput>();

    private static readonly OperandInput[] WaveOnly = { OperandInput.Wave };

    private static readonly OperandInput[] CoefficientInputs =
        { OperandInput.Surface1, OperandInput.Wave };

    private static readonly OperandInput[] HyOnly = { OperandInput.Hy };

    private static readonly OperandInput[] Span =
        { OperandInput.Surface1, OperandInput.Surface2 };

    private static readonly OperandInput[] Ray =
        { OperandInput.Surface1, OperandInput.Wave, OperandInput.Hy,
          OperandInput.Px, OperandInput.Py };

    private static readonly OperandInput[] Tolerance =
        { OperandInput.Decentre, OperandInput.Tilt, OperandInput.Wave };

    /// <summary>The inputs this operand takes, in the order a merit-function line gives them.</summary>
    public static IReadOnlyList<OperandInput> For(OperandType type) => type switch
    {
        // The composite predicted spot is already the weighted average over every field and
        // every wavelength the design defines, so there is nothing left to tell it.
        OperandType.PRMSA => None,

        // A length along the axis, and a colour difference. Neither is a function of where you
        // look from.
        OperandType.TTL => None,
        OperandType.AXC => None,

        OperandType.EFL => WaveOnly,

        // A coefficient is either the system's or one surface's share of it. Surface FIRST, as in
        // every other signature that takes one - a trailing input may be left off and a leading
        // one may not, and the surface is the thing a coefficient operand is most often given.
        // Surface 0 is the object surface and contributes to nothing, so it is free to mean
        // "the whole system", which is also what leaving it off gives.
        OperandType.ABER => CoefficientInputs,

        // Lateral colour is a difference between the extreme wavelengths, so it needs no colour
        // of its own - only the field to measure it at. Distortion is quoted in the reference
        // colour by convention and takes the same one input.
        OperandType.LCF => HyOnly,
        OperandType.DISTF => HyOnly,

        // Edge thicknesses and the diameter-to-thickness ratio are scanned over a run of
        // surfaces, and produce one residual for each one they apply to.
        OperandType.EGT => Span,
        OperandType.EAT => Span,
        OperandType.DTRGT => Span,

        // The two tolerances the design is to be judged against, and the colour to judge in.
        // It averages over the field itself, so there is no field to give it.
        OperandType.ASBLT => Tolerance,

        _ => Ray,
    };

    /// <summary>A readable list of the inputs, for an error message.</summary>
    public static string Describe(OperandType type)
    {
        var inputs = For(type);
        if (inputs.Count == 0) return "no inputs";

        var names = new string[inputs.Count];
        for (int i = 0; i < inputs.Count; i++)
            names[i] = inputs[i] switch
            {
                OperandInput.Surface1 => "surface",
                OperandInput.Surface2 => "surface2",
                OperandInput.Wave => "wave",
                OperandInput.Hy => "hy",
                OperandInput.Px => "px",
                OperandInput.Py => "py",
                OperandInput.Decentre => "decentre",
                OperandInput.Tilt => "tilt-deg",
                _ => "?",
            };
        return string.Join(", ", names);
    }
}
