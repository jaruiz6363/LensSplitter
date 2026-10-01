using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Io;

/// <summary>
/// The commands that build up a merit function and a variable list, a line at a time.
///
/// <para><b>The command IS the file line.</b> <c>VAR "TH 2 MIN 1.0 MAX 25.0"</c> writes
/// <c>VAR TH 2 MIN 1.0 MAX 25.0</c>, and that is the whole of the translation. One grammar to
/// learn, one to document, one to test; a transcript of commands is a valid settings file and a
/// settings file is a script of commands. The alternative - inventing <c>--min</c> to stand for
/// <c>MIN</c> - is a second dialect for the same ideas that then has to be kept in step
/// forever.</para>
///
/// <para><b>The settings file is the session.</b> Every command reads what is there, changes it,
/// and writes it back, so a one-shot command line behaves like a program that remembers - and
/// what it remembers survives restarts and can be opened in an editor.</para>
/// </summary>
public static class SettingsCommands
{
    /// <summary>
    /// A command a user can type, whether it takes an argument, and whether it is about a
    /// particular lens.
    ///
    /// <para>Nearly all of them are: a variable list means nothing away from the design it names.
    /// <c>BASE</c> is not, which is why it is the one command that can be given with no lens at
    /// all - and it has to be, since its whole purpose is to stop you having to type the path to
    /// one.</para>
    /// </summary>
    public readonly record struct Definition(string Keyword, bool TakesArgument, string Summary,
                                             bool NeedsLens = true,
                                             bool ArgumentOptional = false);

    /// <summary>
    /// Everything recognised. Listed in one place so that the command line, the MCP tool and the
    /// help text cannot disagree about what exists.
    /// </summary>
    public static readonly IReadOnlyList<Definition> All = new[]
    {
        new Definition("VAR", true,
            "Declare a variable, or change its bounds: VAR \"TH 2 MIN 1.0 MAX 25.0\". "
          + "Merges with what is already there; VAR \"TH 2 FREE\" drops the bounds."),
        new Definition("VARLIST", false, "List the variables, numbered."),
        new Definition("VARREMOVE", true, "Remove a variable by its number from VARLIST."),

        new Definition("PICKUP", true,
            "Tie one surface to another: PICKUP \"TH 2 INDEX 1 SCALE 1 OFFSET -0.1\"."),
        new Definition("PICKUPLIST", false, "List the pickups, numbered."),
        new Definition("PICKUPREMOVE", true, "Remove a pickup by its number from PICKUPLIST."),

        new Definition("TILT", true,
            "Say a surface is tilted, in DEGREES: TILT \"2 X 0.115 Y 0\". Merges; "
          + "TILT \"2 FREE\" drops the tilt and leaves any decentre alone."),
        new Definition("DEC", true,
            "Say a surface is decentred, in the design's length units: DEC \"2 Y 0.05\". "
          + "Merges; DEC \"2 FREE\" drops it."),
        new Definition("ZERN", true,
            "Say a surface carries a Zernike figure error, as surface SAG in lens units: "
          + "ZERN \"1 Z5 0.0001 Z6 0\". Terms are named by Fringe number; ZERN \"1 FREE\" drops them."),
        new Definition("ALIGNLIST", false,
            "List the perturbations - what is out of place, and by how much."),
        new Definition("ALIGNREMOVE", true,
            "Remove a surface's perturbation by its number from ALIGNLIST."),

        new Definition("OP", true,
            "Add a merit function operand: OP \"EFL, 100, TAR 50, 2\"."),
        new Definition("OPLIST", false, "List the merit function, numbered."),
        new Definition("OPREMOVE", true, "Remove an operand by its number from OPLIST."),

        new Definition("BASE", true,
            "Set the folder bare file names are taken to mean: BASE \"C:\\lenses\\project7\". "
          + "Kept until it is changed, so it holds in the next shell too.", NeedsLens: false),
        new Definition("BASELIST", false,
            "Show the base folder in force, and where it came from.", NeedsLens: false),
        new Definition("BASEREMOVE", false,
            "Forget the base folder, leaving the current directory in charge again. Takes no "
          + "number, there being only one of it.", NeedsLens: false),

        new Definition("HELP", true,
            "Explain one command or one merit function operand: HELP VAR, HELP EFL. On its own, "
          + "list everything there is.", NeedsLens: false, ArgumentOptional: true),
    };

    public static bool IsCommand(string word)
    {
        foreach (var d in All)
            if (string.Equals(d.Keyword, word, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool TakesArgument(string word)
    {
        foreach (var d in All)
            if (string.Equals(d.Keyword, word, StringComparison.OrdinalIgnoreCase))
                return d.TakesArgument;
        return false;
    }

    /// <summary>
    /// Whether a command is about a particular lens. The ones that are not can be given on their
    /// own - <c>abcalc BASE "C:\lenses"</c> - and are run by
    /// <see cref="ExecuteGlobal"/> rather than <see cref="Execute"/>.
    /// </summary>
    public static bool NeedsLens(string word)
    {
        foreach (var d in All)
            if (string.Equals(d.Keyword, word, StringComparison.OrdinalIgnoreCase))
                return d.NeedsLens;
        return true;
    }

    /// <summary>
    /// Whether a command's argument may be left off. Only <c>HELP</c>'s may: everything else
    /// either needs its text or takes none at all.
    /// </summary>
    public static bool ArgumentIsOptional(string word)
    {
        foreach (var d in All)
            if (string.Equals(d.Keyword, word, StringComparison.OrdinalIgnoreCase))
                return d.ArgumentOptional;
        return false;
    }

    /// <summary>What a command did, and whether the settings need writing back.</summary>
    public readonly record struct Result(string Output, bool Changed);

    /// <summary>
    /// Runs one command against a setup, in memory. The caller saves.
    ///
    /// <para>Listing after a REMOVE is deliberate rather than tidy: the numbers shift when
    /// something is taken out, so a user who removes two things in a row would be working from
    /// stale numbers otherwise. Showing the new list is the cheapest way to make that
    /// impossible.</para>
    /// </summary>
    public static Result Execute(string keyword, string? argument, OptimizationSetup setup,
                                 string lensPath)
    {
        if (setup == null) throw new ArgumentNullException(nameof(setup));

        switch (keyword.ToUpperInvariant())
        {
            case "VAR":
                AddVariable(Require(argument, "VAR"), setup);
                return new Result(ListVariables(setup, lensPath), true);

            case "VARLIST":
                return new Result(ListVariables(setup, lensPath), false);

            case "VARREMOVE":
                RemoveAt(setup.Variables, Index(argument, "VARREMOVE", setup.Variables.Count),
                         "variable");
                return new Result(ListVariables(setup, lensPath), true);

            case "PICKUP":
                AddPickup(Require(argument, "PICKUP"), setup);
                return new Result(ListPickups(setup, lensPath), true);

            case "PICKUPLIST":
                return new Result(ListPickups(setup, lensPath), false);

            case "PICKUPREMOVE":
                setup.Pickups.RemoveAt(
                    Index(argument, "PICKUPREMOVE", setup.Pickups.Count) - 1);
                return new Result(ListPickups(setup, lensPath), true);

            case "OP":
                setup.Operands.Add(MeritFile.ParseLine(Require(argument, "OP")));
                return new Result(ListOperands(setup, lensPath), true);

            case "OPLIST":
                return new Result(ListOperands(setup, lensPath), false);

            case "OPREMOVE":
                setup.Operands.RemoveAt(Index(argument, "OPREMOVE", setup.Operands.Count) - 1);
                return new Result(ListOperands(setup, lensPath), true);

            case "BASE": case "BASELIST": case "BASEREMOVE": case "HELP":
                throw new ArgumentException(
                    $"{keyword.ToUpperInvariant()} is not about a particular lens; "
                  + "it is run by ExecuteGlobal");

            case "TILT": case "DEC": case "ZERN": case "ALIGNLIST": case "ALIGNREMOVE":
                throw new ArgumentException(
                    $"{keyword.ToUpperInvariant()} is about how a lens was BUILT rather than "
                  + "what may be optimised about it; it is run by ExecuteAlignment");

            default:
                throw new ArgumentException($"'{keyword}' is not a command");
        }
    }

    /// <summary>
    /// Runs an alignment command - one that says how a lens was BUILT rather than what may be
    /// optimised about it.
    ///
    /// <para>Separated from <see cref="Execute"/> because it takes no <c>OptimizationSetup</c>:
    /// a perturbation is not a variable, not a pickup and not an operand, and putting it in that
    /// container would tie a statement about the workshop to a statement about the search. It
    /// lives in its own sidecar for the same reason - see <c>AlignmentFile</c>.</para>
    /// </summary>
    public static Result ExecuteAlignment(string keyword, string? argument,
                                          AlignmentSpecification alignment, string lensPath)
    {
        if (alignment == null) throw new ArgumentNullException(nameof(alignment));

        switch (keyword.ToUpperInvariant())
        {
            case "TILT":
                AlignmentFile.MergeLine(alignment, "TILT " + Require(argument, "TILT"));
                return new Result(ListAlignment(alignment, lensPath), true);

            case "DEC":
                AlignmentFile.MergeLine(alignment, "DEC " + Require(argument, "DEC"));
                return new Result(ListAlignment(alignment, lensPath), true);

            case "ZERN":
                AlignmentFile.MergeLine(alignment, "ZERN " + Require(argument, "ZERN"));
                return new Result(ListAlignment(alignment, lensPath), true);

            case "ALIGNLIST":
                return new Result(ListAlignment(alignment, lensPath), false);

            case "ALIGNREMOVE":
                alignment.Perturbations.RemoveAt(
                    Index(argument, "ALIGNREMOVE", alignment.Perturbations.Count) - 1);
                return new Result(ListAlignment(alignment, lensPath), true);

            default:
                throw new ArgumentException($"'{keyword}' is not an alignment command");
        }
    }

    /// <summary>Whether this keyword is one <see cref="ExecuteAlignment"/> handles.</summary>
    public static bool IsAlignmentCommand(string word) =>
        word != null && word.ToUpperInvariant() switch
        {
            "TILT" or "DEC" or "ZERN" or "ALIGNLIST" or "ALIGNREMOVE" => true,
            _ => false,
        };

    /// <summary>The perturbations, numbered so that ALIGNREMOVE has something to name.</summary>
    public static string ListAlignment(AlignmentSpecification alignment, string lensPath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        // The name comes from AlignmentFile, not from a second copy of the rule here.
        sb.AppendLine("ALIGNMENT  (" + Path.GetFileName(AlignmentFile.PathFor(lensPath)) + ")");
        sb.AppendLine();

        if (alignment.Perturbations.Count == 0)
        {
            sb.AppendLine("  Nothing is out of place: this is the design as drawn.");
            return sb.ToString();
        }

        var ordered = new List<Perturbation>(alignment.Perturbations);
        ordered.Sort((a, b) => a.Surface.CompareTo(b.Surface));

        sb.AppendLine("      surf     tilt x     tilt y      dec x      dec y");
        sb.AppendLine("             degrees    degrees");
        sb.AppendLine("  " + new string('-', 54));
        for (int i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,2}  {1,4} {2,10} {3,10} {4,10} {5,10}",
                i + 1, p.Surface, N(p.TiltX), N(p.TiltY), N(p.DecenterX), N(p.DecenterY)));
        }
        return sb.ToString();

        static string N(double v) => v == 0.0 ? "-" : v.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs a command that is not about any particular lens.
    ///
    /// <para>Separated from <see cref="Execute"/> because it takes no setup and no lens path -
    /// there is nothing about a design it could read or change. <paramref name="flag"/> is
    /// whatever <c>--dir</c> said on this command line, so <c>BASELIST</c> reports the base that
    /// is actually in force rather than only the stored one.</para>
    /// </summary>
    public static Result ExecuteGlobal(string keyword, string? argument, string? flag = null)
    {
        switch (keyword.ToUpperInvariant())
        {
            case "BASE":
                BasePath.Store(Require(argument, "BASE"));
                return new Result(ShowBase(flag), true);

            case "BASELIST":
                return new Result(ShowBase(flag), false);

            case "BASEREMOVE":
                BasePath.Clear();
                return new Result(ShowBase(flag), true);

            case "HELP":
                return new Result(Help(argument), false);

            default:
                throw new ArgumentException(
                    $"'{keyword}' is not a command that can be given without a lens");
        }
    }

    // ── Help ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Explains one command, one operand, or - given nothing - everything there is.
    ///
    /// <para><b>Generated, not written.</b> The commands come from <see cref="All"/> and the
    /// operands from <see cref="OperandHelp"/> and <see cref="OperandInputs"/>, so an operand
    /// added without a line of documentation shows up here as an obvious blank rather than
    /// silently not existing. A help page maintained by hand beside the thing it describes is a
    /// page that is wrong within a release or two.</para>
    /// </summary>
    public static string Help(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return Everything();

        string word = topic!.Trim();
        foreach (var d in All)
            if (string.Equals(d.Keyword, word, StringComparison.OrdinalIgnoreCase))
                return OneCommand(d);

        var operand = OperandHelp.Find(word);
        if (operand != null) return OneOperand(operand.Value);

        // Refused rather than shrugged at, so that HELP in a script fails when it asks about
        // something that does not exist - a typo in a name is worth hearing about.
        throw new ArgumentException(
            $"'{word}' is not a command or an operand. HELP on its own lists both.");
    }

    private static string OneCommand(Definition d)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append(d.Keyword);
        if (d.TakesArgument) sb.Append(d.ArgumentOptional ? " [\"...\"]" : " \"...\"");
        sb.AppendLine();
        sb.AppendLine();
        foreach (string line in Wrap(d.Summary, 74)) sb.Append("  ").AppendLine(line);
        sb.AppendLine();

        if (d.TakesArgument)
        {
            sb.Append("  Example:  abcalc ")
              .Append(d.NeedsLens ? "lens.zmx " : string.Empty)
              .AppendLine(Example(d.Keyword));
            sb.AppendLine();
        }

        if (!d.NeedsLens)
        {
            sb.AppendLine("  Takes no lens: it is not about any particular design.");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string OneOperand(Operands.OperandType type)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append(type).AppendLine("  (merit function operand)");
        sb.AppendLine();
        foreach (string line in Wrap(OperandHelp.Summary(type), 74))
            sb.Append("  ").AppendLine(line);
        sb.AppendLine();
        sb.Append("  Takes:    ").AppendLine(OperandInputs.Describe(type));
        sb.Append("  Example:  OP \"").Append(OperandHelp.Example(type)).AppendLine("\"");
        sb.AppendLine();
        sb.AppendLine("  A line is TYPE, WEIGHT, then TAR / MIN / MAX, then the inputs. Trailing");
        sb.AppendLine("  inputs may be left off and take their defaults.");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Everything()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("COMMANDS");
        sb.AppendLine();

        int width = 0;
        foreach (var d in All) width = Math.Max(width, d.Keyword.Length);

        foreach (var d in All)
        {
            var wrapped = Wrap(d.Summary, 72 - width);
            for (int i = 0; i < wrapped.Count; i++)
                sb.Append("  ")
                  .Append((i == 0 ? d.Keyword : string.Empty).PadRight(width))
                  .Append("  ")
                  .AppendLine(wrapped[i]);
        }

        sb.AppendLine();
        sb.AppendLine("MERIT FUNCTION OPERANDS");
        sb.AppendLine();

        foreach (var type in OperandHelp.All)
            sb.Append("  ")
              .Append(type.ToString().PadRight(6))
              .Append("  ")
              .AppendLine(OperandInputs.Describe(type));

        sb.AppendLine();
        sb.AppendLine("  HELP <name> explains any one of these - HELP VAR, HELP EFL.");
        sb.AppendLine("  abcalc --help is the whole command line.");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Breaks a summary at word boundaries so a terminal does not do it mid-word.</summary>
    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) { lines.Add(string.Empty); return lines; }

        var line = new StringBuilder();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }

    /// <summary>
    /// The base folder and, just as importantly, <b>where it came from</b>. Four things can set
    /// it and they override each other; "why is it looking there?" is the only hard question this
    /// feature raises, so the answer is printed every time it is asked.
    /// </summary>
    private static string ShowBase(string? flag)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("BASE");
        sb.AppendLine();
        sb.Append("  ").AppendLine(BasePath.Current(flag));
        sb.Append("  from ").AppendLine(BasePath.Source(flag));
        sb.AppendLine();
        sb.AppendLine("  Bare file names are taken to mean this folder. An absolute path always");
        sb.AppendLine("  means what it says.");
        sb.AppendLine();
        return sb.ToString();
    }

    // ── Adding ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Merges a VAR line into the variables that are already declared.
    ///
    /// <para>Parsing the existing ones back alongside the new line is what makes the merge work:
    /// <c>VAR "TH 2 MAX 25"</c> after <c>VAR "TH 2 MIN 1"</c> has to keep the minimum, and the
    /// file parser already knows how to do that.</para>
    /// </summary>
    private static void AddVariable(string argument, OptimizationSetup setup)
    {
        var lines = new List<string>();
        foreach (var v in setup.Variables.Items) lines.Add(VarFile.Line(v));
        foreach (var p in setup.Pickups) lines.Add(VarFile.Line(p));
        lines.Add("VAR " + argument);

        var merged = VarFile.Parse(lines, "VAR");
        setup.Variables.Clear();
        setup.Variables.AddRange(merged.Variables.Items);
        setup.Pickups.Clear();
        setup.Pickups.AddRange(merged.Pickups);
    }

    private static void AddPickup(string argument, OptimizationSetup setup)
    {
        var lines = new List<string>();
        foreach (var v in setup.Variables.Items) lines.Add(VarFile.Line(v));
        foreach (var p in setup.Pickups) lines.Add(VarFile.Line(p));
        lines.Add("PICKUP " + argument);

        var merged = VarFile.Parse(lines, "PICKUP");
        setup.Variables.Clear();
        setup.Variables.AddRange(merged.Variables.Items);
        setup.Pickups.Clear();
        setup.Pickups.AddRange(merged.Pickups);
    }

    // ── Listing ──────────────────────────────────────────────────────────────────────────

    public static string ListOperands(OptimizationSetup setup, string lensPath)
    {
        var lines = new List<string>();
        foreach (var op in setup.Operands) lines.Add(MeritFile.Line(op));

        return Listing("MERIT FUNCTION", lines,
                       Path.GetFileName(Sidecar.MeritPathFor(lensPath)),
                       "No operands. Add one with: OP \"EFL, 100, TAR 50\"");
    }

    public static string ListVariables(OptimizationSetup setup, string lensPath)
    {
        var lines = new List<string>();
        foreach (var v in setup.Variables.Items) lines.Add(VarFile.Line(v));

        return Listing("VARIABLES", lines, SettingsStore.VariableHome(lensPath),
                       "No variables. Add one with: VAR \"CV 3\"");
    }

    public static string ListPickups(OptimizationSetup setup, string lensPath)
    {
        var lines = new List<string>();
        foreach (var p in setup.Pickups) lines.Add(VarFile.Line(p));

        return Listing("PICKUPS", lines, SettingsStore.VariableHome(lensPath),
                       "No pickups.");
    }

    /// <summary>
    /// A numbered listing. The number is what REMOVE takes, and the text beside it is the exact
    /// line in the file - so nothing has to be translated back before it can be edited by hand.
    /// </summary>
    private static string Listing(string title, List<string> lines, string where, string ifEmpty)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append(title).Append("  (").Append(where).AppendLine(")");
        sb.AppendLine();

        if (lines.Count == 0)
        {
            sb.Append("  ").AppendLine(ifEmpty);
            sb.AppendLine();
            return sb.ToString();
        }

        int width = lines.Count.ToString(CultureInfo.InvariantCulture).Length;
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append("  ")
              .Append((i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width))
              .Append("  ")
              .AppendLine(lines[i]);
        }
        sb.AppendLine();
        return sb.ToString();
    }

    // ── Arguments ────────────────────────────────────────────────────────────────────────

    private static string Require(string? argument, string keyword) =>
        string.IsNullOrWhiteSpace(argument)
            ? throw new ArgumentException(
                  $"{keyword} needs its text in quotes, e.g. {Example(keyword)}")
            : argument!;

    private static string Example(string keyword) => keyword.ToUpperInvariant() switch
    {
        "VAR" => "VAR \"TH 2 MIN 1.0 MAX 25.0\"",
        "PICKUP" => "PICKUP \"TH 2 INDEX 1 SCALE 1 OFFSET -0.1\"",
        "VARREMOVE" => "VARREMOVE 2",
        "PICKUPREMOVE" => "PICKUPREMOVE 1",
        "OPREMOVE" => "OPREMOVE 3",
        "BASE" => "BASE \"C:\\lenses\\project7\"",
        "HELP" => "HELP EFL",
        _ => "OP \"EFL, 100, TAR 50, 2\"",
    };

    /// <summary>
    /// A 1-based index from a REMOVE command, checked against what is actually there.
    ///
    /// <para>Out of range is refused by number rather than clamped. Removing "the last one" when
    /// you asked for the fourth of three is the kind of helpfulness that deletes the wrong
    /// thing.</para>
    /// </summary>
    private static int Index(string? argument, string keyword, int count)
    {
        if (string.IsNullOrWhiteSpace(argument))
            throw new ArgumentException($"{keyword} needs a number, from the listing");

        if (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture,
                          out int index))
            throw new ArgumentException($"'{argument}' is not a number");

        if (count == 0)
            throw new ArgumentException($"there is nothing to remove");
        if (index < 1 || index > count)
            throw new ArgumentException(
                $"there is no {index}: the numbers run from 1 to {count}");

        return index;
    }

    private static void RemoveAt(VariableSet set, int oneBased, string what)
    {
        var kept = new List<Variable>(set.Items);
        kept.RemoveAt(oneBased - 1);
        set.Clear();
        set.AddRange(kept);
    }
}
