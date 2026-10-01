using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Optimize.Operands;

namespace AberrationCalculator.Optimize.Io;

/// <summary>
/// The merit function file (.mf): what the design is being asked to be, and nothing else.
///
/// <para>Common to every lens format. It does NOT hold variables or pickups - those describe what
/// may CHANGE rather than what is wanted, they belong with the design, and they live in the lens
/// file for a .lhlt and in a .var file for everything else. Keeping the two apart means a merit
/// function can be moved from one design to another without dragging along surface numbers that
/// meant something else.</para>
///
/// <para>One operand per line:</para>
///
/// <code>
///     TYPE, WEIGHT, TAR x, INPUTS
///     TYPE, WEIGHT, MIN x, INPUTS
///     TYPE, WEIGHT, MAX x, INPUTS
///     TYPE, WEIGHT, MIN x, MAX x, INPUTS
/// </code>
///
/// <code>
///     EFL,   100, TAR 50,           2          # focal length, in wavelength 2
///     EGT,    10, MIN 1,            2, 4       # glass edges over surfaces 2 to 4
///     EAT,    10, MIN 0.1,          2, 4       # and the air gaps
///     DTRGT,  10, MIN 1.5, MAX 12,  2, 4       # diameter-to-thickness ratio
///     PRMSA,   1, TAR 0                        # the predicted spot: no inputs
///     TTL,     5, MAX 60                       # total track
///     AXC,     2, TAR 0                        # axial colour
///     LCF,     5, TAR 0,            1.0        # lateral colour at the full field
///     DISTF,  10, MIN -2, MAX 2,    0.7        # distortion at seven tenths
///     RY,      1, TAR 0,            7, 0, 1, 0, 1
/// </code>
///
/// <para><b>The inputs are positional</b>, and which ones an operand takes is stated in exactly
/// one place - <see cref="OperandInputs"/> - so the parser, the writer and the error messages
/// cannot disagree. Trailing inputs may be left off and take their defaults: <c>RY, 1, TAR 0, 7</c>
/// is surface seven at the reference colour, the maximum field and the chief ray.</para>
///
/// <para><b>Targets and boundaries are different things.</b> An operand with <c>TAR</c> is driven
/// to it and weighed against everything else. One with <c>MIN</c> or <c>MAX</c> costs exactly
/// zero - in the merit and in the Jacobian - while it is satisfied. It does not pull the design
/// gently toward the middle of its range; it is simply not there until it is threatened.</para>
/// </summary>
public static class MeritFile
{
    public static List<Operand> Read(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllLines(path), path);
    }

    public static List<Operand> Parse(IEnumerable<string> lines, string? origin = null)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));

        var operands = new List<Operand>();
        int number = 0;

        foreach (string raw in lines)
        {
            number++;
            string line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            try
            {
                operands.Add(ParseOperand(line));
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new FormatException(
                    $"{origin ?? "merit function"}, line {number}: {ex.Message}\n  {raw.Trim()}");
            }
        }
        return operands;
    }

    internal static string StripComment(string line)
    {
        int hash = line.IndexOf('#');
        return hash >= 0 ? line.Substring(0, hash) : line;
    }

    /// <summary>
    /// <summary>The suffix a part is written with, empty for the whole contribution.</summary>
    internal static string PartSuffix(CoefficientPart part) => part switch
    {
        CoefficientPart.Intrinsic => ".INT",
        CoefficientPart.Figuring => ".FIG",
        CoefficientPart.Induced => ".IND",
        _ => "",
    };

    /// <summary>
    /// Splits <c>M2.IND</c> into its coefficient and its part. A bare name is the whole
    /// contribution.
    ///
    /// <para>The part is a suffix on the NAME rather than another positional input because the
    /// inputs are numbers and a part is not one - and because the coefficient's own name is how a
    /// coefficient operand is written, so the part belongs where a reader is already looking.</para>
    /// </summary>
    internal static (string? Name, CoefficientPart Part) CoefficientAndPart(string text)
    {
        int dot = text.LastIndexOf('.');
        if (dot < 0) return (CoefficientNamed(text), CoefficientPart.Total);

        string? name = CoefficientNamed(text.Substring(0, dot));
        if (name == null) return (null, CoefficientPart.Total);

        return text.Substring(dot + 1).ToUpperInvariant() switch
        {
            "INT" or "INTRINSIC" => (name, CoefficientPart.Intrinsic),
            "FIG" or "FIGURING" => (name, CoefficientPart.Figuring),
            "IND" or "INDUCED" => (name, CoefficientPart.Induced),
            var other => throw new FormatException(
                $"'{other}' is not a part of a coefficient. The parts are INT (what the surface "
              + "generates on its own), FIG (what its figuring adds) and IND (what the aberration "
              + $"already reaching it generates in it); leaving the suffix off gives all three. So "
              + $"'{name}.INT', or '{name}' for the whole contribution"),
        };
    }

    /// The canonical spelling of an aberration coefficient, or null if the text is not one.
    ///
    /// <para>Case-insensitive, and it returns the name as <c>BuchdahlTerms.Names</c> spells it
    /// rather than as the user typed it, so that everything downstream - the indexer, the label,
    /// the file the merit function is written back to - sees one spelling. A user may type
    /// <c>tau15</c>, <c>TAU15</c> or <c>Tau15</c>; only <c>Tau15</c> travels.</para>
    /// </summary>
    internal static string? CoefficientNamed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (string name in BuchdahlTerms.Names)
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) return name;
        return null;
    }

    private static Operand ParseOperand(string line)
    {
        var field = new List<string>();
        foreach (string piece in line.Split(',')) field.Add(piece.Trim());

        if (field.Count < 2)
            throw new FormatException(
                "an operand needs at least a type and a weight, separated by a comma");

        // The type, or the name of an aberration coefficient. A coefficient is written as
        // itself - `TAU15, 1, TAR 0` - so that the name in the report and the name in the merit
        // function are one name with no table between them. ABER is accepted too, for anything
        // generating these mechanically, but then the coefficient has nowhere to come from.
        string? coefficient = null;
        var part = CoefficientPart.Total;
        if (!Enum.TryParse<OperandType>(field[0], true, out var type))
        {
            (coefficient, part) = CoefficientAndPart(field[0]);
            if (coefficient == null)
                throw new FormatException(
                    $"'{field[0]}' is not an operand and not an aberration coefficient.\n"
                  + "  The operands are: "
                  + string.Join(", ", Enum.GetNames(typeof(OperandType))) + "\n"
                  + "  The coefficients are: "
                  + string.Join(", ", BuchdahlTerms.Names));
            type = OperandType.ABER;
        }
        else if (type == OperandType.ABER)
        {
            throw new FormatException(
                "ABER does not say WHICH coefficient. Write the coefficient's own name as the "
              + "operand instead - `B, 1, TAR 0` or `TAU15, 1, TAR 0` - which is how the report "
              + "spells it. The coefficients are: " + string.Join(", ", BuchdahlTerms.Names));
        }

        if (!TryNumber(field[1], out double weight))
            throw new FormatException($"'{field[1]}' is not a weight");

        // MIN, MAX and TAR come next, in any order and any combination bar the contradictory one.
        double target = 0.0;
        double? min = null, max = null;
        bool haveTarget = false;
        int i = 2;

        for (; i < field.Count; i++)
        {
            var (word, rest) = SplitKeyword(field[i]);
            if (word == null) break;

            switch (word)
            {
                case "TAR": target = Value(rest, "TAR"); haveTarget = true; break;
                case "MIN": min = Value(rest, "MIN"); break;
                case "MAX": max = Value(rest, "MAX"); break;
                default: throw new FormatException($"unexpected '{field[i]}'");
            }
        }

        if (!haveTarget && min == null && max == null)
            throw new FormatException(
                $"{type} has no TAR, MIN or MAX, so it is not asking for anything");
        if (haveTarget && (min != null || max != null))
            throw new FormatException(
                $"{type} has both a TAR and a limit. An operand is either driven to a value or "
              + "held inside a range, and asking for both is ambiguous");
        if (min != null && max != null && min > max)
            throw new FormatException($"MIN {min} is above MAX {max}");

        // Whatever is left is the input list, positionally.
        var wanted = OperandInputs.For(type);
        int given = field.Count - i;
        if (given > wanted.Count)
            throw new FormatException(
                $"{type} takes {(wanted.Count == 0 ? "no inputs" : wanted.Count + " inputs (" + OperandInputs.Describe(type) + ")")}"
              + $", and {given} were given");

        int surface = 0, surface2 = 0, wave = 0;
        double hy = 1.0, px = 0.0, py = 0.0;
        double decentre = 0.0, tilt = 0.0;

        for (int k = 0; k < given; k++)
        {
            string text = field[i + k];
            if (!TryNumber(text, out double v))
                throw new FormatException($"'{text}' is not a number, for the "
                                        + $"{wanted[k].ToString().ToLowerInvariant()} of {type}");

            switch (wanted[k])
            {
                case OperandInput.Surface1: surface = Whole(v, "surface"); break;
                case OperandInput.Surface2: surface2 = Whole(v, "surface2"); break;

                case OperandInput.Wave:
                    wave = Whole(v, "wave");
                    // Wavelengths are numbered from one. A merit function should never contain
                    // an index that is not an index: to mean the reference colour, leave the
                    // wavelength off altogether.
                    if (wave < 1)
                        throw new FormatException(
                            $"wavelengths are numbered from 1, so {wave} is not one. Leave the "
                          + "wavelength off to use the design's reference colour");
                    break;

                case OperandInput.Hy: hy = v; break;
                case OperandInput.Px: px = v; break;
                case OperandInput.Py: py = v; break;

                case OperandInput.Decentre: decentre = v; break;
                case OperandInput.Tilt: tilt = v; break;
            }
        }

        // A span given only its first surface is that surface alone.
        if (surface2 == 0) surface2 = surface;

        // A tertiary coefficient is a SYSTEM operand. TertiaryCoefficients.Attach does split the
        // twenty by surface, for the report's shares, but only as each surface's part of the
        // total: there is no intrinsic, figuring or induced split, so a part would be a silent
        // zero, and on a figured flat facing collimated light a surface's own share has no finite
        // value, so a surface would be too. Either reads exactly like a surface that contributes
        // nothing.
        if (type == OperandType.ABER && coefficient != null
            && coefficient.StartsWith("Tau", StringComparison.Ordinal)
            && (surface != 0 || part != CoefficientPart.Total))
            throw new FormatException(
                $"{coefficient} is a SYSTEM operand: it takes no surface and no intrinsic, "
              + "figuring or induced part. The report splits the tertiary coefficients by "
              + "surface, but only as a surface's share of the total, and on a figured flat "
              + "facing collimated light not at all; per-surface operands run B to B7. Write "
              + $"'{coefficient}, ... ' on its own for the system's value.");

        // There is no induced third order. A third-order contribution is built from the surface's
        // own quantities alone, so nothing earlier can act on it, and B.IND is identically zero on
        // every design. Refused rather than answered, because a zero here is indistinguishable
        // from an aberration that has been corrected.
        if (type == OperandType.ABER && part == CoefficientPart.Induced && coefficient != null
            && AberrationNames.Order(coefficient) == 3)
            throw new FormatException(
                $"{coefficient} is a THIRD-ORDER coefficient and has no induced part - a "
              + "third-order contribution is built from that surface's own quantities alone, so "
              + "there is nothing for an earlier surface to induce in it. The answer would be "
              + $"zero on every design, which is not distinguishable from a corrected one. Use "
              + $"'{coefficient}.INT' or '{coefficient}.FIG', or the fifth order, where induced "
              + "terms are real.");


        return new Operand
        {
            Type = type,
            Coefficient = coefficient,
            Part = part,
            Surface = surface,
            Surface2 = surface2,
            Wave = wave,
            Hy = hy,
            Px = px,
            Py = py,
            Decentre = decentre,
            Tilt = tilt,
            Target = target,
            Weight = weight,
            Min = min,
            Max = max,
        };
    }

    /// <summary>
    /// Splits <c>MIN 1.5</c> into its keyword and its value. Returns a null keyword for a field
    /// that is not one of these, which is how the parser knows the inputs have started.
    /// </summary>
    private static (string? Word, string Value) SplitKeyword(string field)
    {
        int space = field.IndexOf(' ');
        string head = (space < 0 ? field : field.Substring(0, space)).ToUpperInvariant();
        if (head != "TAR" && head != "MIN" && head != "MAX") return (null, string.Empty);
        return (head, space < 0 ? string.Empty : field.Substring(space + 1).Trim());
    }

    private static double Value(string text, string keyword)
    {
        if (!TryNumber(text, out double v))
            throw new FormatException($"{keyword} needs a number, not '{text}'");
        return v;
    }

    private static int Whole(double v, string what)
    {
        int i = (int)Math.Round(v);
        if (Math.Abs(v - i) > 1e-9)
            throw new FormatException($"the {what} has to be a whole number, not {v}");
        return i;
    }

    internal static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    // ── Writing ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the merit function out. What comes out parses back to what went in, so a merit
    /// function built by a command can be saved, read and argued with like any other.
    /// </summary>
    public static string Write(IEnumerable<Operand> operands, string? header = null)
    {
        if (operands == null) throw new ArgumentNullException(nameof(operands));

        var sb = new StringBuilder();
        sb.AppendLine("# abcalc merit function.");
        if (!string.IsNullOrWhiteSpace(header)) sb.AppendLine("# " + header);
        sb.AppendLine("# TYPE, WEIGHT, TAR x | MIN x | MAX x, INPUTS");
        sb.AppendLine();

        foreach (var op in operands) sb.AppendLine(Line(op));
        return sb.ToString();
    }

    /// <summary>
    /// One operand as its merit-function line. Exposed because a listing should show the user
    /// exactly what is in the file, not a paraphrase of it - the line they see is the line they
    /// could have typed.
    /// </summary>
    public static string Line(Operand op)
    {
        if (op == null) throw new ArgumentNullException(nameof(op));

        var sb = new StringBuilder();

        // A coefficient operand writes as its coefficient, which is how it was read. Writing
        // ABER would produce a file this parser refuses, by its own rule that ABER does not say
        // which coefficient - the round trip is the test that keeps the two honest.
        sb.Append(op.Type == OperandType.ABER && !string.IsNullOrEmpty(op.Coefficient)
                  ? op.Coefficient! + PartSuffix(op.Part)
                  : op.Type.ToString())
          .Append(", ").Append(N(op.Weight));

        if (op.IsBoundary)
        {
            if (op.Min.HasValue) sb.Append(", MIN ").Append(N(op.Min.Value));
            if (op.Max.HasValue) sb.Append(", MAX ").Append(N(op.Max.Value));
        }
        else
        {
            sb.Append(", TAR ").Append(N(op.Target));
        }

        // Inputs are written only as far as the last one that says anything. Padding the rest out
        // with their defaults would mean writing a wavelength of zero for an operand that never
        // named a wavelength - a number the parser rightly refuses, so the file would not read
        // back. Since the inputs are positional and omission is always a trailing run, stopping
        // at the last meaningful one is exactly what round-trips.
        var inputs = OperandInputs.For(op.Type);
        int last = -1;
        for (int k = 0; k < inputs.Count; k++)
            if (Says(op, inputs[k])) last = k;

        for (int k = 0; k <= last; k++)
        {
            sb.Append(", ");
            sb.Append(inputs[k] switch
            {
                OperandInput.Surface1 => N(op.Surface),
                OperandInput.Surface2 => N(op.Surface2),
                // Forced to write a wavelength for an operand that named none - which only a
                // command-built operand can be - the first is the honest guess.
                OperandInput.Wave => N(op.Wave < 1 ? 1 : op.Wave),
                OperandInput.Hy => N(op.Hy),
                OperandInput.Px => N(op.Px),
                OperandInput.Decentre => N(op.Decentre),
                OperandInput.Tilt => N(op.Tilt),
                _ => N(op.Py),
            });
        }
        return sb.ToString();
    }

    /// <summary>Parses a single operand line, for a command that adds one.</summary>
    public static Operand ParseLine(string line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        var parsed = Parse(new[] { line });
        if (parsed.Count != 1)
            throw new FormatException("expected one operand, found " + parsed.Count);
        return parsed[0];
    }

    /// <summary>
    /// Whether this input carries information, or is merely sitting at the value it would take
    /// if it were left out.
    ///
    /// <para>A surface always says something - an operand that takes one is meaningless without
    /// it. The rest are compared against what omission would have given, so an operand written
    /// with every default writes none of them and reads back identical.</para>
    /// </summary>
    private static bool Says(Operand op, OperandInput input) => input switch
    {
        OperandInput.Surface1 => true,
        OperandInput.Surface2 => op.Surface2 != op.Surface,
        OperandInput.Wave => op.Wave >= 1,
        OperandInput.Hy => op.Hy != 1.0,
        OperandInput.Px => op.Px != 0.0,
        // Both tolerances are what the operand is FOR, so both are always written.
        OperandInput.Decentre => true,
        OperandInput.Tilt => true,
        _ => op.Py != 0.0,
    };

    private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
}
