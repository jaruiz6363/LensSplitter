using System;
using System.Collections.Generic;
using System.Globalization;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// One line of the merit function: what to measure, where, and what it should be.
///
/// <para>An operand is either a TARGET - drive this quantity to that value, weighted - or a
/// BOUNDARY - keep this quantity inside these limits and cost nothing while it is. A boundary
/// contributes exactly zero to the merit function and exactly zero to the Jacobian while it is
/// satisfied, which is what lets a design carry twenty manufacturability constraints without
/// any of them pulling on the solution until one is actually threatened.</para>

/// <summary>
/// Which PART of a coefficient an <see cref="OperandType.ABER"/> operand measures.
///
/// <para>A surface's contribution is three things, and a designer needs them apart because they
/// answer to different actions. What the surface generates out of its own curvature and the rays
/// reaching it is corrected by bending THIS surface. What its figuring adds is corrected by
/// changing the figuring. What was INDUCED in it by the aberration already accumulated ahead of
/// it is not the surface's doing at all, and correcting it here is a second wrong balancing a
/// first - the fix is upstream.</para>
///
/// <para>Shafer's case for the split is that the limiting aberrations of a corrected design
/// cannot be controlled without it: "This can only be done effectively, however, if the 5th-order
/// aberration surface contributions are broken into two components: the intrinsic component and
/// the induced component." See <c>docs/references.md</c>.</para>
///
/// <para><b>There is no induced part at third order</b>, and that is arithmetic rather than an
/// omission: a third-order contribution is built from the surface's own quantities alone, so
/// there is nothing for an earlier surface to act on. <c>B.IND</c> is legitimately zero
/// everywhere, and the parser says so rather than letting it be mistaken for a corrected
/// design.</para>
/// </summary>
public enum CoefficientPart
{
    /// <summary>Intrinsic plus figuring plus induced. The default, written by naming no part.</summary>
    Total = 0,

    /// <summary>What the surface generates on its own, before figuring and before anything induced.</summary>
    Intrinsic = 1,

    /// <summary>What its conic and r^4, r^6, r^8 terms add. Zero on a sphere.</summary>
    Figuring = 2,

    /// <summary>What the aberration already reaching the surface generates in it.</summary>
    Induced = 3,
}

/// </summary>
public sealed class Operand
{
    public OperandType Type { get; init; }

    /// <summary>The surface, or the first surface of a span.</summary>
    public int Surface { get; init; }

    /// <summary>The last surface of a span. Equal to <see cref="Surface"/> for a single one.</summary>
    public int Surface2 { get; init; }

    /// <summary>Wavelength: 0 is the reference colour, 1..n the nth in the file's order.</summary>
    public int Wave { get; init; }

    /// <summary>
    /// Field height as a FRACTION of the design's maximum field, 0 on axis and 1 at the corner.
    ///
    /// <para>A fraction rather than an index into the field list, so a merit function can ask for
    /// seven tenths of the field whether or not the design happens to define a field point there.
    /// The fields the design defines are what PRMSA averages over; they are not the only places
    /// a ray may be traced.</para>
    /// </summary>
    public double Hy { get; init; }

    /// <summary>Meridional pupil coordinate as a fraction of the pupil radius.</summary>
    public double Py { get; init; }

    /// <summary>Sagittal pupil coordinate as a fraction of the pupil radius.</summary>
    public double Px { get; init; }

    /// <summary>Decentre tolerance, in the design's length units. See <see cref="OperandType.ASBLT"/>.</summary>
    public double Decentre { get; init; }

    /// <summary>Tilt tolerance, in DEGREES. See <see cref="OperandType.ASBLT"/>.</summary>
    public double Tilt { get; init; }

    /// <summary>The value a target operand is driven to. Ignored by a boundary operand.</summary>
    public double Target { get; init; }

    /// <summary>Relative importance. Zero switches the operand off without deleting it.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>Lower limit of a boundary operand, if it has one.</summary>
    public double? Min { get; init; }

    /// <summary>Upper limit of a boundary operand, if it has one.</summary>
    public double? Max { get; init; }

    /// <summary>Free text carried through to the report.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Which aberration coefficient an <see cref="OperandType.ABER"/> operand measures, spelled
    /// as <c>BuchdahlTerms.Names</c> spells it: <c>B</c>, <c>Pi5</c>, <c>Tau15</c>. Null for
    /// every other type.
    /// </summary>
    public string? Coefficient { get; init; }

    /// <summary>
    /// Which part of that coefficient: the whole contribution, or the intrinsic, figuring or
    /// induced share of it. <see cref="CoefficientPart.Total"/> for every other operand type.
    /// </summary>
    public CoefficientPart Part { get; init; }

    /// <summary>
    /// Whether the quantity this operand DIFFERENTIATES is the reciprocal of the one it is
    /// declared and reported in.
    ///
    /// <para>Only the focal length, which is targeted through the power: a flat design has
    /// infinite focal length and a derivative of zero there, so an optimiser asked to make a
    /// lens out of parallel plates could neither evaluate the residual nor see which way to
    /// move. The power is finite through zero and linear in curvature to first order.</para>
    /// </summary>
    public bool IsPower => Type == OperandType.EFL;

    /// <summary>The value as the user declared it, from the quantity actually evaluated.</summary>
    public double AsDeclared(double evaluated) =>
        IsPower ? (Math.Abs(evaluated) > 1e-12 ? 1.0 / evaluated : double.PositiveInfinity)
                : evaluated;

    /// <summary>True when this is a boundary rather than a target.</summary>
    public bool IsBoundary => Min.HasValue || Max.HasValue;

    /// <summary>True for the operands that scan a range of surfaces.</summary>
    public bool IsSpan => Type == OperandType.EGT || Type == OperandType.EAT
                       || Type == OperandType.DTRGT;

    /// <summary>
    /// A short label for the report, showing only the inputs this operand actually takes so that
    /// a TTL is not padded out with a wavelength and a pupil it never asked for.
    /// </summary>
    public string Label
    {
        get
        {
            // A coefficient operand is labelled by the coefficient, not by ABER - the label goes
            // into the report beside the value, and "ABER" in a column of thirty-seven of them
            // would tell a reader nothing at all.
            var s = Type == OperandType.ABER && !string.IsNullOrEmpty(Coefficient)
                  ? Coefficient! + Io.MeritFile.PartSuffix(Part)
                  : Type.ToString();
            foreach (var input in OperandInputs.For(Type))
            {
                switch (input)
                {
                    case OperandInput.Surface1:
                        // Surface 0 on a coefficient means the whole system, and labelling that
                        // " s0" would read as the object surface rather than as the totals.
                        if (Type != OperandType.ABER || Surface != 0) s += " s" + N(Surface);
                        break;
                    case OperandInput.Surface2:
                        if (Surface2 != Surface) s += ".." + N(Surface2);
                        break;
                    case OperandInput.Wave:
                        if (Wave != 0) s += " w" + N(Wave);
                        break;
                    case OperandInput.Hy:
                        s += " hy" + N(Hy);
                        break;
                    case OperandInput.Px:
                        if (Px != 0.0) s += " px" + N(Px);
                        break;
                    case OperandInput.Py:
                        if (Py != 0.0) s += " py" + N(Py);
                        break;
                    case OperandInput.Decentre:
                        s += " dec" + N(Decentre);
                        break;
                    case OperandInput.Tilt:
                        s += " tilt" + N(Tilt) + "deg";
                        break;
                }
            }
            return s;
        }
    }

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public override string ToString() => Label;

    /// <summary>
    /// A span operand written out as one operand per surface it actually applies to.
    ///
    /// <para>Expanding rather than reducing the span to its worst member is deliberate. A
    /// minimum over surfaces has a gradient only at whichever surface happens to be worst,
    /// so the optimiser fixes that one, the next becomes worst, and the search chatters
    /// between them. One residual per surface gives every threatened surface its own column
    /// of the Jacobian and they are relieved together.</para>
    ///
    /// <para>Surfaces where the operand has no meaning are dropped: an edge-thickness-of-glass
    /// over a span finds only the glass gaps in it, and the air gaps are not silently scored
    /// as zero.</para>
    /// </summary>
    // The evaluation set, read the same way OperandContext reads it. Kept here rather than
    // taking a context because Expand runs when the merit function is assembled, before any
    // probe exists - and because two readings of "which fields and wavelengths are there" that
    // could disagree is one too many.
    private static int WaveCount(OpticalSystem s) => Math.Max(1, s.Wavelengths.Count);

    private static int FieldCount(OpticalSystem s) => Math.Max(1, s.Fields.Count);

    private static double WaveWeight(OpticalSystem s, int w) =>
        w < s.Wavelengths.Count ? s.Wavelengths[w].Weight : 1.0;

    private static double FieldWeight(OpticalSystem s, int f) =>
        f - 1 < s.Fields.Count ? s.Fields[f - 1].Weight : 1.0;

    private static double FieldAngle(OpticalSystem s, int f) =>
        f - 1 < s.Fields.Count ? s.Fields[f - 1].Y : 0.0;

    private static double MaximumField(OpticalSystem s)
    {
        double max = 0.0;
        foreach (var f in s.Fields) if (Math.Abs(f.Y) > Math.Abs(max)) max = f.Y;
        return max;
    }

    public IEnumerable<Operand> Expand(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        // PRMSA IS A SET OF RESIDUALS, NOT ONE. It is the weighted RMS over every field and
        // wavelength, and handing a least-squares optimiser the aggregate throws away the
        // structure it works with: one row in the Jacobian instead of one per case, so
        // Gauss-Newton sees a single direction where there are nine. Expanding it costs nothing
        // - the coefficients for a wavelength are computed once and shared by its fields - and
        // the merit is UNCHANGED, because the sub-weights are chosen so the sum of squares comes
        // to exactly what the aggregate would have given.
        if (Type == OperandType.PRMSA)
        {
            double max = MaximumField(system);
            double total = 0.0;
            for (int w = 0; w < WaveCount(system); w++)
                for (int f = 1; f <= FieldCount(system); f++)
                    total += WaveWeight(system, w) * FieldWeight(system, f);
            if (total <= 0.0) { yield return this; yield break; }

            for (int w = 0; w < WaveCount(system); w++)
                for (int f = 1; f <= FieldCount(system); f++)
                {
                    double share = WaveWeight(system, w) * FieldWeight(system, f);
                    if (share <= 0.0) continue;

                    // sqrt(w) multiplies the residual, so the weight carries the share of the
                    // mean-square this case is responsible for. Sum them and the total is the
                    // aggregate's weight times the aggregate's square, exactly.
                    yield return new Operand
                    {
                        Type = Type,
                        Surface = Surface,
                        Surface2 = Surface2,
                        Wave = w + 1,
                        Hy = Math.Abs(max) > 1e-15 ? FieldAngle(system, f) / max : 0.0,
                        Py = Py,
                        Px = Px,
                        Target = Target,
                        Weight = Weight * share / total,
                        Min = Min,
                        Max = Max,
                        Comment = Comment,
                    };
                }
            yield break;
        }

        if (!IsSpan)
        {
            yield return this;
            yield break;
        }

        int a = Surface, b = Surface2 == 0 ? Surface : Surface2;
        if (b < a) (a, b) = (b, a);

        int last = system.LastOpticalSurface();
        for (int i = Math.Max(1, a); i <= Math.Min(b, last); i++)
        {
            bool glass = IsGlassAfter(system, i);
            bool wanted = Type switch
            {
                OperandType.EGT => glass,
                OperandType.DTRGT => glass,
                OperandType.EAT => !glass,
                _ => true,
            };
            if (!wanted) continue;
            yield return With(i);
        }
    }

    /// <summary>The same operand, pinned to one surface.</summary>
    private Operand With(int surface) => new()
    {
        Type = Type,
        Surface = surface,
        Surface2 = surface,
        Wave = Wave,
        Hy = Hy,
        Py = Py,
        Px = Px,
        Target = Target,
        Weight = Weight,
        Min = Min,
        Max = Max,
        Comment = Comment,
    };

    /// <summary>
    /// Whether the gap after surface <paramref name="i"/> is glass.
    ///
    /// <para>A mirror is not glass for this purpose: the space after it is where the light
    /// goes, not a blank to be ground.</para>
    /// </summary>
    public static bool IsGlassAfter(OpticalSystem system, int i)
    {
        if (i < 0 || i >= system.Surfaces.Count) return false;
        var s = system.Surfaces[i];
        return !string.IsNullOrWhiteSpace(s.Material) && !s.IsMirror;
    }
}
