using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Io;

/// <summary>What may change about a design, and what is tied to what.</summary>
public sealed class VarSpecification
{
    public VariableSet Variables { get; } = new();
    public List<Pickup> Pickups { get; } = new();

    public bool IsEmpty => Variables.Count == 0 && Pickups.Count == 0;
}

/// <summary>
/// The variables file (.var): which construction parameters may be optimised, between what
/// limits, and which follow others.
///
/// <code>
///     VAR TH 2 MIN 1.0 MAX 25.0
///     VAR CV 4
///     PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1
/// </code>
///
/// <para><b>Only for formats that cannot keep this themselves.</b> A .lhlt states its own
/// variables, bounds and pickups, and they are read from and written back to the lens; writing
/// them here as well would give a design two statements of what may move and nothing to say
/// which wins. Every other format has nowhere to record it, so it comes here.</para>
///
/// <para><b>A VAR line MERGES into what is already known.</b> <c>VAR TH 2 MIN 1.0</c> followed
/// by <c>VAR TH 2 MAX 25.0</c> leaves surface 2's thickness variable with both limits, not with
/// the second one alone. For a file written by this program the rule never comes up - it emits
/// one line per variable - but it is what makes setting a limit from a command safe, since a
/// command that named only the maximum would otherwise silently discard a minimum set a moment
/// earlier. <c>FREE</c> drops the limits again.</para>
/// </summary>
public static class VarFile
{
    public static VarSpecification Read(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllLines(path), path);
    }

    public static VarSpecification Parse(IEnumerable<string> lines, string? origin = null)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));

        // Gathered by (kind, surface) so that repeated lines merge rather than replace.
        var variables = new List<(VariableKind Kind, int Surface, double Min, double Max)>();
        var pickups = new List<Pickup>();
        int number = 0;

        foreach (string raw in lines)
        {
            number++;
            string line = MeritFile.StripComment(raw).Trim();
            if (line.Length == 0) continue;

            var token = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string keyword = token[0].ToUpperInvariant();

            try
            {
                if (keyword == "VAR") MergeVariable(variables, token);
                else if (keyword == "PICKUP") MergePickup(pickups, token);
                else throw new FormatException($"expected VAR or PICKUP, found '{token[0]}'");
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new FormatException(
                    $"{origin ?? "variables"}, line {number}: {ex.Message}\n  {raw.Trim()}");
            }
        }

        var spec = new VarSpecification();
        foreach (var v in variables)
            spec.Variables.Add(new Variable
            {
                Kind = v.Kind, Surface = v.Surface, Min = v.Min, Max = v.Max,
            });
        spec.Pickups.AddRange(pickups);
        return spec;
    }

    /// <summary>
    /// <c>VAR CV|TH|CC|A4|A6|A8 n [MIN x] [MAX x] [FREE]</c>, merged into whatever this variable already had.
    /// </summary>
    private static void MergeVariable(
        List<(VariableKind Kind, int Surface, double Min, double Max)> into, string[] token)
    {
        if (token.Length < 3) throw new FormatException("VAR needs a kind and a surface");

        var kind = ParseKind(token[1].ToUpperInvariant());
        int surface = Whole(token[2], "surface");

        int at = into.FindIndex(v => v.Kind == kind && v.Surface == surface);
        double min = at >= 0 ? into[at].Min : double.NegativeInfinity;
        double max = at >= 0 ? into[at].Max : double.PositiveInfinity;

        for (int i = 3; i < token.Length; i++)
        {
            switch (token[i].ToUpperInvariant())
            {
                case "MIN": min = Number(token, ++i, "MIN"); break;
                case "MAX": max = Number(token, ++i, "MAX"); break;

                // The way back out of a bound. Merging means a limit cannot be removed by
                // leaving it off, so there has to be a word that says to drop it.
                case "FREE":
                    min = double.NegativeInfinity;
                    max = double.PositiveInfinity;
                    break;

                default: throw new FormatException($"unexpected '{token[i]}' on a VAR line");
            }
        }

        if (min > max) throw new FormatException($"MIN {min} is above MAX {max}");

        var entry = (kind, surface, min, max);
        if (at >= 0) into[at] = entry; else into.Add(entry);
    }

    /// <summary>
    /// <c>PICKUP CV|TH|CC|SD|GLASS n INDEX m [SCALE k] [OFFSET o]</c> - surface n's parameter is
    /// surface m's, times the scale, plus the offset.
    ///
    /// <para>This is how a cemented pair keeps its shared surface, or a mirror its radius. The
    /// optimiser refuses to vary either end of one, rather than parting the cement.</para>
    /// </summary>
    private static void MergePickup(List<Pickup> into, string[] token)
    {
        if (token.Length < 5)
            throw new FormatException("PICKUP needs a parameter, a surface, INDEX and a source");

        var parameter = token[1].ToUpperInvariant() switch
        {
            "CV" or "CURVATURE" => PickupParameter.Curvature,
            "TH" or "THICKNESS" => PickupParameter.Thickness,
            "CC" or "CONIC" => PickupParameter.Conic,
            "SD" or "SEMIDIAMETER" => PickupParameter.SemiDiameter,
            "GLASS" or "MATERIAL" => PickupParameter.Material,
            _ => throw new FormatException(
                     $"'{token[1]}' is not a pickup parameter (CV, TH, CC, SD, GLASS)"),
        };

        int target = Whole(token[2], "surface");
        if (!token[3].Equals("INDEX", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"expected INDEX, found '{token[3]}'");
        int source = Whole(token[4], "source surface");

        double scale = 1.0, offset = 0.0;
        for (int i = 5; i < token.Length; i++)
        {
            switch (token[i].ToUpperInvariant())
            {
                case "SCALE": scale = Number(token, ++i, "SCALE"); break;
                case "OFFSET": offset = Number(token, ++i, "OFFSET"); break;
                default: throw new FormatException($"unexpected '{token[i]}' on a PICKUP line");
            }
        }

        var pickup = new Pickup
        {
            TargetSurfaceIndex = target,
            SourceSurfaceIndex = source,
            Parameter = parameter,
            ScaleFactor = scale,
            Offset = offset,
        };

        int at = into.FindIndex(p => p.TargetSurfaceIndex == target && p.Parameter == parameter);
        if (at >= 0) into[at] = pickup; else into.Add(pickup);
    }

    /// <summary>
    /// <c>CV</c>, <c>TH</c>, <c>CC</c>, <c>A4</c>, <c>A6</c> or <c>A8</c>.
    ///
    /// <para>The aspheric terms stop at <c>A8</c> and that is not an omission. r^10 and beyond do
    /// not appear in the third, fifth or seventh order at all, so a variable driving one would
    /// move the lens without moving anything the merit function can see - a variable with an
    /// identically zero column in the Jacobian, which is worse than a refusal because it looks
    /// like it is working.</para>
    /// </summary>
    private static VariableKind ParseKind(string text) => text switch
    {
        "CV" or "CURVATURE" => VariableKind.Curvature,
        "TH" or "THICKNESS" => VariableKind.Thickness,
        "CC" or "CONIC" => VariableKind.Conic,
        "A4" => VariableKind.Asphere4,
        "A6" => VariableKind.Asphere6,
        "A8" => VariableKind.Asphere8,

        _ => text.Length > 1 && text[0] == 'A' && int.TryParse(text.Substring(1), out int order)
            ? throw new FormatException(
                  order % 2 == 0 && order >= 10
                  ? $"'{text}' is beyond r^8, and the aberration coefficients cannot see it. "
                  + "Buchdahl's scheme reaches the seventh order, and r^10 and above contribute "
                  + "nothing to the third, fifth or seventh order - they are not approximated "
                  + "there, they are absent. A variable driving one would have an identically "
                  + "zero column in the Jacobian. Vary A4, A6 or A8"
                  : $"'{text}' is not an even-asphere term; this optimiser takes A4, A6 and A8")
            : throw new FormatException(
                  $"'{text}' is not a variable kind; this optimiser takes CV, TH, CC, A4, A6 "
                + "and A8"),
    };

    private static int Whole(string text, string what)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            throw new FormatException($"'{text}' is not a {what}");
        return v;
    }

    private static double Number(string[] token, int index, string what)
    {
        if (index >= token.Length) throw new FormatException($"{what} needs a value");
        if (!MeritFile.TryNumber(token[index], out double v))
            throw new FormatException($"'{token[index]}' is not a number, after {what}");
        return v;
    }

    // ── Writing ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes one canonical line per variable and per pickup, so a file this program produces
    /// never depends on the merge rule to be understood.
    /// </summary>
    public static string Write(VarSpecification spec, string? header = null)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var sb = new StringBuilder();
        sb.AppendLine("# abcalc variables and pickups.");
        if (!string.IsNullOrWhiteSpace(header)) sb.AppendLine("# " + header);
        sb.AppendLine();

        foreach (var v in spec.Variables.Items) sb.AppendLine(Line(v));
        foreach (var p in spec.Pickups) sb.AppendLine(Line(p));
        return sb.ToString();
    }

    /// <summary>
    /// One variable as its file line. A listing shows the user exactly what is in the file rather
    /// than a paraphrase of it, so the line they see is the line they could have typed.
    /// </summary>
    public static string Line(Variable v)
    {
        if (v == null) throw new ArgumentNullException(nameof(v));

        var sb = new StringBuilder();
        sb.Append("VAR ").Append(Variable.Prefix(v.Kind))
          .Append(' ').Append(N(v.Surface));
        if (!double.IsNegativeInfinity(v.Min)) sb.Append(" MIN ").Append(N(v.Min));
        if (!double.IsPositiveInfinity(v.Max)) sb.Append(" MAX ").Append(N(v.Max));
        return sb.ToString();
    }

    /// <summary>One pickup as its file line.</summary>
    public static string Line(Pickup p)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));

        var sb = new StringBuilder();
        sb.Append("PICKUP ").Append(Text(p.Parameter))
          .Append(' ').Append(N(p.TargetSurfaceIndex))
          .Append(" INDEX ").Append(N(p.SourceSurfaceIndex));
        if (p.ScaleFactor != 1.0) sb.Append(" SCALE ").Append(N(p.ScaleFactor));
        if (p.Offset != 0.0) sb.Append(" OFFSET ").Append(N(p.Offset));
        return sb.ToString();
    }

    private static string Text(PickupParameter p) => p switch
    {
        PickupParameter.Curvature => "CV",
        PickupParameter.Thickness => "TH",
        PickupParameter.Conic => "CC",
        PickupParameter.SemiDiameter => "SD",
        PickupParameter.Material => "GLASS",
        _ => "?",
    };

    private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
}
