using System;
using System.IO;

using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The help, which is GENERATED rather than written.
///
/// <para>These tests exist because documentation kept beside the thing it describes is
/// documentation that is wrong within a release or two. The command list comes from
/// <see cref="SettingsCommands.All"/> and the operand list from <see cref="OperandHelp"/> and
/// <see cref="OperandInputs"/>, so what is checked here is mostly that nothing can be added to
/// either without also being explained.</para>
/// </summary>
[Collection(ProcessWideState.Name)]
public class HelpTests
{
    private static string Run(params string[] args)
    {
        var output = new StringWriter();
        var previous = Console.Out;
        try { Console.SetOut(output); Cli.Program.Run(args); }
        finally { Console.SetOut(previous); }
        return output.ToString();
    }

    // ── Nothing may go undocumented ──────────────────────────────────────────────────────

    /// <summary>
    /// Every command appears in the listing, with something said about it. A keyword added to
    /// the table with an empty summary is a keyword nobody can discover.
    /// </summary>
    [Fact]
    public void EveryCommandIsListedAndExplained()
    {
        string all = Run("HELP");

        foreach (var d in SettingsCommands.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Summary),
                         $"{d.Keyword} has no summary");
            Assert.Contains(d.Keyword, all);

            // And each can be asked about on its own.
            string one = Run("HELP", d.Keyword);
            Assert.Contains(d.Keyword, one);
            Assert.Contains(d.Summary.Split(' ')[0], one);
        }
    }

    /// <summary>
    /// Every operand too - including its inputs, which are positional and therefore unreadable
    /// unless something says what order they come in.
    /// </summary>
    [Fact]
    public void EveryOperandIsListedWithWhatItTakes()
    {
        string all = Run("HELP");

        foreach (var type in OperandHelp.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(OperandHelp.Summary(type)),
                         $"{type} has no summary");
            Assert.Contains(type.ToString(), all);

            string one = Run("HELP", type.ToString());
            Assert.Contains(OperandInputs.Describe(type), one);
        }
    }

    /// <summary>
    /// The example given for an operand has to be a line the parser actually accepts. An example
    /// that does not parse is worse than none: it is followed, and then it fails.
    ///
    /// <para>It is not required to come back out byte for byte, because the writer omits
    /// trailing inputs that are at their defaults - <c>LCF, 5, TAR 0, 1.0</c> is written back as
    /// <c>LCF, 5, TAR 0</c>, and the example spells the field out on purpose to show where it
    /// goes. What must hold is that the MEANING survives a write and a read.</para>
    /// </summary>
    [Fact]
    public void EveryOperandExampleParsesAndSurvivesARoundTrip()
    {
        foreach (var type in OperandHelp.All)
        {
            string line = OperandHelp.Example(type);
            var operand = MeritFile.ParseLine(line);
            Assert.Equal(type, operand.Type);

            string written = MeritFile.Line(operand);
            Assert.Equal(written, MeritFile.Line(MeritFile.ParseLine(written)));
        }
    }

    /// <summary>
    /// The hand-written COMMANDS block in the usage text is the one place a command could be
    /// forgotten, since it is prose rather than a generated list.
    /// </summary>
    [Fact]
    public void TheUsageTextMentionsEveryCommand()
    {
        string usage = Run("--help");
        foreach (var d in SettingsCommands.All)
            Assert.Contains(d.Keyword, usage);
    }

    // ── Asking about one thing ───────────────────────────────────────────────────────────

    [Fact]
    public void HelpForACommandShowsAnExampleWithTheLensWhereOneIsNeeded()
    {
        Assert.Contains("abcalc lens.zmx VAR \"TH 2 MIN 1.0 MAX 25.0\"", Run("HELP", "VAR"));

        // BASE is not about a lens, so its example does not pretend otherwise.
        string b = Run("HELP", "BASE");
        Assert.Contains("abcalc BASE", b);
        Assert.Contains("Takes no lens", b);
    }

    [Fact]
    public void HelpForAnOperandSaysWhatItTakesAndShowsALine()
    {
        string text = Run("HELP", "DTRGT");

        Assert.Contains("surface, surface2", text);
        Assert.Contains("OP \"DTRGT, 10, MIN 1.5, MAX 12, 2, 4\"", text);
        Assert.Contains("diameter", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaseDoesNotMatter()
    {
        Assert.Equal(Run("HELP", "EFL"), Run("help", "efl"));
    }

    /// <summary>
    /// A topic that does not exist is refused, so HELP in a script fails on a typo rather than
    /// printing something reassuring and carrying on.
    /// </summary>
    [Fact]
    public void AnUnknownTopicIsRefused()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => Cli.Program.Run(new[] { "HELP", "wibble" }));
        Assert.Contains("not a command or an operand", ex.Message);
        Assert.Contains("HELP on its own", ex.Message);
    }

    // ── Where it can be asked ────────────────────────────────────────────────────────────

    /// <summary>
    /// HELP takes no lens - the point of it is to tell someone who does not yet know what to
    /// type - and its argument is optional, which no other command's is.
    /// </summary>
    [Fact]
    public void HelpNeedsNeitherALensNorAnArgument()
    {
        Assert.False(SettingsCommands.NeedsLens("HELP"));
        Assert.True(SettingsCommands.ArgumentIsOptional("HELP"));

        Assert.Equal(0, Cli.Program.Run(new[] { "HELP" }));
        Assert.DoesNotContain("USAGE", Run("HELP"));
    }

    /// <summary>
    /// The topic is usually itself a keyword, so the parser has to take the token after HELP as
    /// its argument rather than reading it as the next command.
    /// </summary>
    [Fact]
    public void TheTopicMayBeACommandName()
    {
        string text = Run("HELP", "OPREMOVE");

        Assert.Contains("OPREMOVE", text);
        Assert.Contains("number", text);
        Assert.DoesNotContain("MERIT FUNCTION OPERANDS", text);   // not the whole listing
    }
}
