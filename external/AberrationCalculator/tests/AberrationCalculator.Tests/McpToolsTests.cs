using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Mcp;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The MCP server's tools.
///
/// <para>The protocol layer is three methods of JSON-RPC and is exercised by running the
/// server; what is worth pinning here is the layer underneath - that every advertised tool
/// actually runs on a real lens and returns something, and that the failures a caller will
/// hit come back as clear messages rather than as an empty result.</para>
/// </summary>
public class McpToolsTests
{
    private static string Lens => Fixtures.Lens("CookeTriplet");

    /// <summary>
    /// Every tool in the list runs and produces output. A tool that is advertised and then
    /// throws is worse than one that is not advertised at all, because the caller has no way
    /// to tell the difference from a lens it cannot analyse.
    /// </summary>
    [Fact]
    public void EveryAdvertisedToolRuns()
    {
        var writer = Tools.Open(Lens, null);

        Assert.NotEmpty(Tools.All);
        foreach (var tool in Tools.All)
        {
            string text = tool.Run(writer, null);
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"tool '{tool.Name}' returned nothing");
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"tool '{tool.Name}' has no description");
        }
    }

    /// <summary>Every tool is named once, since the client dispatches by name.</summary>
    [Fact]
    public void ToolNamesAreUnique()
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in Tools.All)
            Assert.True(seen.Add(tool.Name), $"duplicate tool name: {tool.Name}");
    }

    /// <summary>
    /// The tables really are tab-separated, because a caller will parse them. A report that
    /// quietly stopped emitting tabs would still look fine to a reader and break every
    /// consumer.
    /// </summary>
    [Fact]
    public void TheTableToolsEmitTabs()
    {
        var writer = Tools.Open(Lens, null);
        foreach (var tool in Tools.All)
        {
            // Prose, not tables: the whole report, a verdict with reasons, and the
            // coefficient breakdown, which is a nest of groups and int/fig/ind/tot rows that
            // reads far better ruled than flattened into one row per cell.
            if (tool.Name is "analyse_lens" or "aspheric_screen" or "seventh_order"
                          or "distortion_from_coefficients" or "quaternary_spherical") continue;
            Assert.Contains('\t', tool.Run(writer, null));
        }
    }

    /// <summary>A missing lens is named in the message, not swallowed.</summary>
    [Fact]
    public void AMissingLensFileSaysSo()
    {
        var e = Assert.Throws<FileNotFoundException>(
            () => Tools.Open(Path.Combine(Fixtures.LensDir, "no-such-lens.zmx"), null));
        Assert.Contains("no-such-lens.zmx", e.Message);
    }

    /// <summary>
    /// So is a missing glass folder. Silently falling back to the bundled catalogs would be
    /// worse than failing: the caller asked for particular glasses, and getting different
    /// ones changes the answer without changing its appearance.
    /// </summary>
    [Fact]
    public void AMissingGlassFolderSaysSo()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => Tools.Open(Lens, Path.Combine(Fixtures.LensDir, "no-such-catalog-folder")));
    }

    /// <summary>An empty path is a caller error and is reported as one.</summary>
    [Fact]
    public void AnEmptyPathIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Tools.Open("", null));
    }

    /// <summary>
    /// Every tool the server advertises is named in <c>docs/mcp.md</c>, which claims to list
    /// them.
    ///
    /// <para>Mechanical, for the reason <c>OperandDocumentationTests</c> is: <c>ASBLT</c> was
    /// written, wired in and tested, and left out of the operand table, and nothing failed
    /// because nothing was looking. A tool is easier to forget still - it is registered in one
    /// file and documented in another - and an undocumented tool is one a caller will never
    /// think to ask for.</para>
    /// </summary>
    [Fact]
    public void EveryToolIsNamedInTheDocument()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "mcp.md")))
            dir = dir.Parent;
        if (dir == null) return;   // sources are not here; nothing to check, not something to fail

        string doc = File.ReadAllText(Path.Combine(dir.FullName, "docs", "mcp.md"));

        var missing = new System.Collections.Generic.List<string>();
        foreach (var tool in Tools.All)
            if (!doc.Contains("`" + tool.Name + "`", StringComparison.Ordinal))
                missing.Add(tool.Name);
        foreach (var tool in ActionTools.All)
            if (!doc.Contains("`" + tool.Name + "`", StringComparison.Ordinal))
                missing.Add(tool.Name);

        Assert.True(missing.Count == 0,
            "docs/mcp.md does not mention: " + string.Join(", ", missing));
    }

    /// <summary>
    /// And every action tool's arguments are documented too, since those are the ones a caller
    /// cannot guess from a lens path.
    /// </summary>
    [Fact]
    public void EveryActionToolArgumentIsNamedInTheDocument()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "mcp.md")))
            dir = dir.Parent;
        if (dir == null) return;

        string doc = File.ReadAllText(Path.Combine(dir.FullName, "docs", "mcp.md"));

        var missing = new System.Collections.Generic.List<string>();
        foreach (var tool in ActionTools.All)
            foreach (var arg in tool.Arguments)
                if (!doc.Contains("`" + arg.Name + "`", StringComparison.Ordinal))
                    missing.Add(tool.Name + "." + arg.Name);

        Assert.True(missing.Count == 0,
            "docs/mcp.md does not mention: " + string.Join(", ", missing));
    }

    /// <summary>
    /// <b>Every merit-function example in the optimize tool's description actually parses.</b>
    ///
    /// <para>That description is the only thing a model reads before writing a merit function, so
    /// an example that does not parse is not a documentation slip - it is an instruction to do
    /// something the program refuses. The check is mechanical for the same reason the operand
    /// documentation check is: written out by hand it drifted the moment a signature changed.</para>
    /// </summary>
    [Fact]
    public void EveryMeritExampleInTheOptimizeDescriptionParses()
    {
        var optimize = ActionTools.All.Single(t => t.Name == "optimize");

        foreach (string raw in optimize.Description.Split('\n'))
        {
            string line = raw.Trim();

            // The example lines are the indented ones carrying a TAR, MIN or MAX. Prose about
            // them is not indented, and the VAR lines are handled by the test below.
            if (!raw.StartsWith("  ") || !line.Contains(",")) continue;
            if (line.StartsWith("VAR ") || line.StartsWith("PICKUP ")) continue;
            if (!line.Contains("TAR ") && !line.Contains("MIN ") && !line.Contains("MAX ")) continue;

            var parsed = MeritFile.Parse(new[] { line });
            Assert.True(parsed.Count == 1, $"this example does not parse: {line}");
        }
    }

    /// <summary>And every VARIABLES example, against the .var parser.</summary>
    [Fact]
    public void EveryVariableExampleInTheOptimizeDescriptionParses()
    {
        var optimize = ActionTools.All.Single(t => t.Name == "optimize");
        int seen = 0;

        foreach (string raw in optimize.Description.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("VAR ") && !line.StartsWith("PICKUP ")) continue;

            var spec = VarFile.Parse(new[] { line });
            Assert.True(spec.Variables.Count + spec.Pickups.Count == 1,
                        $"this example does not parse: {line}");
            seen++;
        }

        Assert.True(seen >= 4, "the variables examples have gone missing from the description");
    }

    /// <summary>
    /// <b>The description does not tell a caller to write ABER.</b> It is the internal name of the
    /// coefficient operand and the parser refuses it, because it does not say which coefficient -
    /// a coefficient is written as its own name. The generated INPUTS BY TYPE line is built from
    /// the operand enum, so ABER would appear there unless it is deliberately left out.
    /// </summary>
    [Fact]
    public void TheOptimizeDescriptionDoesNotOfferABERAsAType()
    {
        var optimize = ActionTools.All.Single(t => t.Name == "optimize");

        Assert.DoesNotContain("ABER takes", optimize.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("ABER take ", optimize.Description, StringComparison.Ordinal);

        // And it says positively what to write instead, or a caller learns nothing from the
        // absence.
        Assert.Contains("Tau15", optimize.Description, StringComparison.Ordinal);
        Assert.Contains("Do NOT write ABER", optimize.Description, StringComparison.Ordinal);
    }

    /// <summary>
    /// The figuring variables are offered. They exist, they are what the last two pieces of work
    /// were for, and a model that is never told about them cannot use them.
    /// </summary>
    [Fact]
    public void TheOptimizeDescriptionOffersTheFiguringVariables()
    {
        var optimize = ActionTools.All.Single(t => t.Name == "optimize");

        Assert.Contains("VAR CC 3", optimize.Description, StringComparison.Ordinal);
        Assert.Contains("A4", optimize.Description, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The Forbes degree reaches the report.</b> The CLI has taken `--forbes 3..8` for a long
    /// time and the MCP tool was fixed at 3, so a model could not ask the question
    /// `docs/distortion-prediction.md` answers - whether a design's residual is seventh order at
    /// all, which is found by carrying the series further and watching the prediction move.
    /// </summary>
    [Fact]
    public void TheSeventhOrderToolTakesADegreeAndUsesIt()
    {
        var writer = Tools.Open(Lens, null);
        var tool = Tools.All.Single(t => t.Name == "seventh_order");

        Assert.NotNull(tool.Extra);
        Assert.Contains(tool.Extra!, s => s.Name == "degree");

        string atThree = tool.Run(writer, new JsonObject { ["degree"] = 3 });
        string atFive = tool.Run(Tools.Open(Lens, null), new JsonObject { ["degree"] = 5 });

        Assert.False(string.IsNullOrWhiteSpace(atThree));
        Assert.NotEqual(atThree, atFive);          // the argument changed the answer

        // And the default is still three, so nothing that omits it moves.
        Assert.Equal(atThree, tool.Run(Tools.Open(Lens, null), null));
    }

    /// <summary>A degree outside 3 to 8 is refused rather than clamped.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public void AnImpossibleForbesDegreeIsRefused(int degree)
    {
        var writer = Tools.Open(Lens, null);
        var tool = Tools.All.Single(t => t.Name == "seventh_order");

        var ex = Assert.Throws<ArgumentException>(
            () => tool.Run(writer, new JsonObject { ["degree"] = degree }));
        Assert.Contains("between 3 and 8", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Every optimiser setting the CLI exposes has an MCP argument.</b> The MCP must be able to
    /// do everything the CLI can: a caller should not have to discover that the answer to their
    /// question exists but only through the other door. `hop_sigma` was the one that had been
    /// missed, and this is the check that would have said so.
    /// </summary>
    [Fact]
    public void TheOptimizeToolExposesEveryOptimiserSetting()
    {
        var optimize = ActionTools.All.Single(t => t.Name == "optimize");
        var offered = optimize.Arguments.Select(x => x.Name).ToList();

        foreach (string expected in new[]
                 { "method", "iterations", "hops", "chains", "seed", "hop_sigma",
                   "glass_substitution" })
            Assert.True(offered.Contains(expected),
                $"the optimize tool does not offer '{expected}', which the CLI does");
    }
}
