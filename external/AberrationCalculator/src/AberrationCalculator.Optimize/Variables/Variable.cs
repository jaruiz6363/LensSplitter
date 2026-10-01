using System;
using System.Globalization;

namespace AberrationCalculator.Optimize.Variables;

/// <summary>
/// Which construction parameter a variable drives.
///
/// <para><b>The figuring kinds were deliberately absent until the arrangement was settled.</b>
/// Buchdahl's aspheric seventh order needs an arrangement he never published, and this
/// repository's reconstruction of it was for a long time one real rays rejected, by up to a
/// factor of four in <c>tau20</c>. Descending a quantity wrong by a factor of four is not slow,
/// it is aimed wrongly, so the kinds were left out altogether and the refusal could not be
/// forgotten. That arrangement is now established - all twenty tau against Forbes to between
/// 2E-13 and 2E-10 on every figured design, the rays agreeing with both, and an independent
/// transcription in <c>macros/BUCH7_ASPH.ZPL</c> reproducing <c>FORBES.ZPL</c> inside
/// OpticStudio. See <c>docs/verification.md</c>. The reason for the absence is gone, and with
/// it the absence.</para>
/// </summary>
public enum VariableKind
{
    /// <summary>Surface curvature, 1/radius. The workhorse.</summary>
    Curvature = 0,

    /// <summary>Axial thickness after the surface: a glass centre thickness or an air space.</summary>
    Thickness = 1,

    /// <summary>
    /// Conic constant. 0 is a sphere, -1 a paraboloid, below -1 a hyperboloid.
    ///
    /// <para>It is a different kind of variable from the three below, and not merely another
    /// figuring term. A conic deforms the surface at EVERY even order at once, in a ratio fixed
    /// by the curvature: its r^4 contribution is K c^3 h^4 / 8 and the r^6 and r^8 ones follow
    /// from the same K. So it cannot correct one order without moving the rest, and on a flat
    /// surface it does nothing whatever. The r^4 term can do what a conic cannot, and costs a
    /// figure the optician has to make to a different tolerance. Which of the two a design wants
    /// is a real decision, and giving the optimiser both on one surface asks it to make that
    /// decision by least squares.</para>
    /// </summary>
    Conic = 2,

    /// <summary>The r^4 figuring term, <c>AsphericCoefficients[1]</c>.</summary>
    Asphere4 = 3,

    /// <summary>The r^6 figuring term, <c>AsphericCoefficients[2]</c>.</summary>
    Asphere6 = 4,

    /// <summary>
    /// The r^8 figuring term, <c>AsphericCoefficients[3]</c>, and the last that reaches the
    /// orders this program computes. r^10 and beyond do not appear in the third, fifth or
    /// seventh order at all - they are not approximated, they are absent - so they are not
    /// offered as variables.
    /// </summary>
    Asphere8 = 5,
}

/// <summary>
/// One thing the optimiser may change, and how far it may change it.
///
/// <para><b>Bounds are enforced by reflection.</b> A step that would carry the variable past a
/// limit is folded back inside, as light off a mirror, and the variable keeps its physical units
/// throughout. The alternative - mapping the bounded interval onto an unbounded internal
/// coordinate through a sigmoid - has a gradient that vanishes as the bound is approached, so a
/// variable pressed against a limit stops responding to the optimiser and stays stuck there even
/// when the design later wants it back. Under reflection the gradient keeps its magnitude
/// everywhere, which is what a constrained or stochastic search needs. See
/// <see cref="Reflection"/>.</para>
/// </summary>
public sealed class Variable
{
    public VariableKind Kind { get; init; }

    /// <summary>Surface this variable belongs to, indexed as the prescription indexes it.</summary>
    public int Surface { get; init; }

    /// <summary>Lower limit, or negative infinity for none.</summary>
    public double Min { get; init; } = double.NegativeInfinity;

    /// <summary>Upper limit, or positive infinity for none.</summary>
    public double Max { get; init; } = double.PositiveInfinity;

    /// <summary>True when either limit is finite, so reflection has something to fold against.</summary>
    public bool IsBounded => !double.IsNegativeInfinity(Min) || !double.IsPositiveInfinity(Max);

    /// <summary>
    /// A name that reads back as what it is: <c>CV3</c>, <c>TH2</c>, <c>CC1</c>, <c>A46</c>.
    /// This is what the merit file writes and what a report column is headed with.
    ///
    /// <para>Every prefix is exactly two characters, which is what makes the surface number
    /// unambiguous without a separator: <c>A410</c> is the r^4 term of surface 10 and cannot be
    /// read as anything else.</para>
    /// </summary>
    public string Name => Prefix(Kind) + Surface.ToString(CultureInfo.InvariantCulture);

    /// <summary>The two-letter tag for a kind, as the .var file spells it.</summary>
    public static string Prefix(VariableKind kind) => kind switch
    {
        VariableKind.Curvature => "CV",
        VariableKind.Thickness => "TH",
        VariableKind.Conic => "CC",
        VariableKind.Asphere4 => "A4",
        VariableKind.Asphere6 => "A6",
        VariableKind.Asphere8 => "A8",
        _ => "??",
    };

    /// <summary>
    /// True for the kinds that FIGURE a surface - conic and the three even-asphere terms.
    /// Setting any of them non-zero makes a spherical surface aspheric, which is what sends the
    /// tertiary down the aspheric arrangement instead of Buchdahl's own table.
    /// </summary>
    public bool Figures => Kind == VariableKind.Conic
                        || Kind == VariableKind.Asphere4
                        || Kind == VariableKind.Asphere6
                        || Kind == VariableKind.Asphere8;

    /// <summary>
    /// For the three even-asphere kinds, the index into
    /// <see cref="Core.Models.Surface.AsphericCoefficients"/>; -1 for anything else.
    /// </summary>
    public int AsphericIndex => Kind switch
    {
        VariableKind.Asphere4 => 1,
        VariableKind.Asphere6 => 2,
        VariableKind.Asphere8 => 3,
        _ => -1,
    };

    public override string ToString() => Name;
}
