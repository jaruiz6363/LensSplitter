using System;
using System.IO;
using System.Text.Json.Nodes;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Mcp;
using AberrationCalculator.Optimize.Io;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The two ways a person actually reaches the optimiser: the command line and the MCP tool,
/// which mirrors it.
///
/// <para>What a flag is wired to is not visible from anywhere else. The optimiser itself is
/// covered on its own, and a flag hooked up to the wrong method - or one that forgets to write
/// the result - would pass every one of those tests.</para>
/// </summary>
public class OptimizeCommandTests
{
    private const string Merit = @"
PRMSA, 1,  TAR 0
EFL,   10, TAR 50
EGT,   1,  MIN 0.5, 1, 6
";

    private sealed class Scratch : IDisposable
    {
        public string Dir { get; }
        public string Lens { get; }
        public string MeritPath { get; }

        /// <param name="declareVariables">
        /// Whether the copied .lhlt says which surfaces may move. A .lhlt keeps that in the lens
        /// itself, so a test wanting "no variables declared" simply asks for a copy without them.
        /// </param>
        public Scratch(bool declareVariables = true)
        {
            Dir = Path.Combine(Path.GetTempPath(), "abcalc-opt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
            Lens = Path.Combine(Dir, "CookeTriplet.lhlt");

            string json = File.ReadAllText(Fixtures.Lens("CookeTriplet"));
            if (declareVariables) json = DeclareVariables(json);
            File.WriteAllText(Lens, json);

            MeritPath = Sidecar.MeritPathFor(Lens);
            File.WriteAllText(MeritPath, Merit);
        }

        /// <summary>
        /// Turns on the variable flags the fixture ships with set to false. Editing the JSON
        /// rather than inserting keys matters: the file already HAS a CurvatureVariable on every
        /// surface, and adding a second one would leave a duplicate key whose last occurrence -
        /// the original false - is the one that wins.
        /// </summary>
        private static string DeclareVariables(string json)
        {
            foreach (int n in new[] { 1, 2, 4, 6 })
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, "(\"Index\": " + n + ",.*?)\"CurvatureVariable\": false",
                    "$1\"CurvatureVariable\": true",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

            return System.Text.RegularExpressions.Regex.Replace(
                json, "(\"Index\": 2,.*?)\"ThicknessVariable\": false",
                "$1\"ThicknessVariable\": true, \"ThicknessMin\": 1.0, \"ThicknessMax\": 12.0",
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        public string At(string name) => Path.Combine(Dir, name);
        public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
    }

    [Fact]
    public void CommandLineOptimisesAndWritesTheResult()
    {
        using var s = new Scratch();

        int code = Cli.Program.Run(new[]
        {
            s.Lens, "--optimize", "--method", "psd3", "--iterations", "40", "-q",
        });

        Assert.Equal(0, code);

        string lensOut = s.At("CookeTriplet.optimised.lhlt");
        string reportOut = s.At("CookeTriplet.lhlt.optimisation.txt");
        Assert.True(File.Exists(lensOut), "the optimised lens was not written");
        Assert.True(File.Exists(reportOut), "the report was not written");

        // The merit function travels with it; a .lhlt keeps its variables in the lens, so there
        // is no .var file beside it.
        Assert.True(File.Exists(Sidecar.MeritPathFor(lensOut)));
        Assert.False(File.Exists(Sidecar.VariablePathFor(lensOut)));

        string report = File.ReadAllText(reportOut);
        Assert.Contains("Dilworth PSD III", report);
        Assert.Contains("MERIT", report);

        // The design that was read must NOT have been altered on disk.
        var catalog = CatalogLocator.LoadBundled();
        var optimised = LensFile.Read(lensOut, catalog);
        Assert.Equal(8, optimised.Surfaces.Count);

        // The variables it was given came back with it.
        Assert.True(optimised.Surfaces[1].CurvatureVariable);
        Assert.True(optimised.Surfaces[2].ThicknessVariable);
        Assert.Equal(1.0, optimised.Surfaces[2].ThicknessMin);
    }

    /// <summary>
    /// The report says what MOVED, in the terms a prescription is written in.
    ///
    /// <para>A variables table saying a curvature went from 0.0454 to 0.0466 is true and nearly
    /// unreadable; what gets typed into another program, or handed to a shop, is a radius. Both
    /// are in the report, and the radii come first.</para>
    /// </summary>
    [Fact]
    public void TheReportSaysWhatMovedAndWhereTheMeritWent()
    {
        using var s = new Scratch();

        Assert.Equal(0, Cli.Program.Run(new[]
        {
            s.Lens, "--optimize", "--iterations", "40", "-q",
        }));

        string report = File.ReadAllText(s.At("CookeTriplet.lhlt.optimisation.txt"));

        Assert.Contains("WHAT CHANGED", report);
        Assert.Contains("radius", report);

        // The radius, not the curvature: surface 1 is near 22 and its curvature near 0.045, so
        // the number that appears says which of the two is being reported.
        Assert.Matches(@"1\s+radius\s+22\.0", report);

        // Both ends of the merit, so a run can be judged without rerunning it.
        Assert.Matches(@"MERIT\s+[\d.E+-]+\s+->\s+[\d.E+-]+", report);

        // A surface nothing touched is not listed. Surface 5 is not a variable here.
        Assert.DoesNotMatch(@"\n  5\s+radius", report);
    }

    [Fact]
    public void AReportForARunThatMovedNothingSaysSo()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

        var outcome = new Optimize.RunOutcome
        {
            Best = system,
            Start = Optimize.Evaluation.DesignCopy.Deep(system),
            InitialMerit = 0.1,
            FinalMerit = 0.1,
        };

        string report = Optimize.Io.OptimizationReport.Build(outcome, "x.lhlt");
        Assert.Contains("WHAT CHANGED", report);
        Assert.Contains("Nothing.", report);
    }

    [Fact]
    public void EveryStepMethodIsReachableFromTheCommandLine()
    {
        foreach (string method in new[] { "lm", "psd2", "psd3", "hj" })
        {
            using var s = new Scratch();
            Assert.Equal(0, Cli.Program.Run(new[]
            {
                s.Lens, "--optimize", "--method", method, "--iterations", "10", "-q",
            }));
        }
    }

    [Fact]
    public void BasinHoppingIsReachableFromTheCommandLine()
    {
        using var s = new Scratch();
        Assert.Equal(0, Cli.Program.Run(new[]
        {
            s.Lens, "--optimize", "--hops", "3", "--chains", "1",
            "--iterations", "10", "--seed", "5", "-q",
        }));

        string report = File.ReadAllText(s.At("CookeTriplet.lhlt.optimisation.txt"));
        Assert.Contains("hops over", report);
    }

    [Fact]
    public void SaveGoesWhereItIsTold()
    {
        using var s = new Scratch();
        string target = s.At(Path.Combine("elsewhere", "mine.lhlt"));

        Assert.Equal(0, Cli.Program.Run(new[]
        {
            s.Lens, "--optimize", "--iterations", "10", "--save", target, "-q",
        }));

        Assert.True(File.Exists(target), "--save did not put the lens where it was told");
        Assert.True(File.Exists(Sidecar.MeritPathFor(target)));
    }

    /// <summary>
    /// A merit function named explicitly is used instead of the sidecar.
    /// </summary>
    [Fact]
    public void AnExplicitMeritFileWinsOverTheSidecar()
    {
        using var s = new Scratch();
        string other = s.At("other.mf");
        File.WriteAllText(other, "EFL, 1, TAR 55\n");

        Assert.Equal(0, Cli.Program.Run(new[]
        {
            s.Lens, "--optimize", other, "--iterations", "20", "-q",
        }));

        string report = File.ReadAllText(s.At("CookeTriplet.lhlt.optimisation.txt"));
        Assert.Contains("EFL", report);
        Assert.DoesNotContain("PRMSA", report);
    }

    [Theory]
    [InlineData("--method")]
    [InlineData("--iterations")]
    [InlineData("--hops")]
    public void AFlagMissingItsValueIsRefused(string flag)
    {
        using var s = new Scratch();
        Assert.ThrowsAny<ArgumentException>(() =>
            Cli.Program.Run(new[] { s.Lens, "--optimize", flag }));
    }

    [Fact]
    public void AnUnknownMethodIsRefused()
    {
        using var s = new Scratch();
        Assert.ThrowsAny<ArgumentException>(() =>
            Cli.Program.Run(new[] { s.Lens, "--optimize", "--method", "banana" }));
    }

    // ── MCP ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheMcpToolOptimisesFromInlineMeritText()
    {
        using var s = new Scratch();

        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["merit"] = Merit,
            ["method"] = "psd2",
            ["iterations"] = 30,
        });

        Assert.Contains("OPTIMISATION", text);
        Assert.Contains("MERIT", text);
        Assert.Contains("Nothing was written", text);
        Assert.False(File.Exists(s.At("CookeTriplet.optimised.lhlt")));
    }

    /// <summary>Variables may be given inline too, which is the CLI's .var file by another route.</summary>
    [Fact]
    public void TheMcpToolAcceptsVariablesInline()
    {
        using var s = new Scratch(declareVariables: false);

        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["variables"] = "VAR CV 1\nVAR CV 4\nVAR TH 2 MIN 1 MAX 12\n",
            ["merit"] = Merit,
            ["iterations"] = 25,
        });

        Assert.Contains("CV1", text);
        Assert.Contains("TH2", text);
    }

    [Fact]
    public void TheMcpToolWritesWhenAskedTo()
    {
        using var s = new Scratch();
        string target = s.At("from-mcp.lhlt");

        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["merit"] = Merit,
            ["iterations"] = 20,
            ["save_to"] = target,
        });

        Assert.Contains("Written:", text);
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(Sidecar.MeritPathFor(target)));
    }

    [Fact]
    public void TheMcpToolRefusesASetupThatAsksForNothing()
    {
        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");

        // No variables: the lens declares none and none were given.
        using (var s = new Scratch(declareVariables: false))
        {
            File.Delete(s.MeritPath);
            var ex = Assert.ThrowsAny<ArgumentException>(() => tool.Run(new JsonObject
            {
                ["lens_file"] = s.Lens,
                ["merit"] = Merit,
            }));
            Assert.Contains("nothing is declared variable", ex.Message,
                            StringComparison.OrdinalIgnoreCase);
        }

        // No operands: variables declared, but nothing to optimise towards.
        using (var s = new Scratch())
        {
            File.Delete(s.MeritPath);
            var ex = Assert.ThrowsAny<ArgumentException>(() => tool.Run(new JsonObject
            {
                ["lens_file"] = s.Lens,
            }));
            Assert.Contains("no operands", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The MCP mirrors the command line, so basin hopping keeps EVERY chain here too. save_to is
    /// a folder in that case, because chains land in different valleys and which of them is
    /// interesting is a judgement only a designer can make.
    /// </summary>
    [Fact]
    public void TheMcpToolKeepsOneDesignPerHoppingChain()
    {
        using var s = new Scratch();
        string folder = s.At("hop");

        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["merit"] = Merit,
            ["iterations"] = 10,
            ["hops"] = 4,
            ["chains"] = 3,
            ["seed"] = 5,
            ["save_to"] = folder,
        });

        var designs = Directory.GetFiles(folder, "CookeTriplet.chain*.lhlt");
        Assert.Equal(3, designs.Length);
        foreach (string d in designs)
        {
            Assert.Contains(Path.GetFullPath(d), text);
            Assert.True(File.Exists(Sidecar.MeritPathFor(d)));
        }
    }

    /// <summary>
    /// A mistyped catalogue name is caught before the run rather than surfacing minutes in as an
    /// aggregate exception out of the hopping threads - and the message says what there is.
    /// </summary>
    [Fact]
    public void TheMcpToolNamesTheSubstitutionCatalogueAndRefusesAnUnknownOne()
    {
        using var s = new Scratch();
        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");

        var ex = Assert.ThrowsAny<Exception>(() => tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["merit"] = Merit,
            ["hops"] = 2,
            ["glass_substitution"] = "NotACatalogue",
        }));
        Assert.Contains("CoreSet28", ex.Message);

        // And the real name is accepted. Enough hops to see a swap actually taken: the per-hop
        // kick is a tenth of a per cent, so glass moves through the substitution proposal rather
        // than by being flung somewhere, and that needs hops to show up.
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = s.Lens,
            ["merit"] = Merit,
            ["iterations"] = 20,
            ["hops"] = 60,
            ["chains"] = 2,
            ["seed"] = 7,
            ["glass_substitution"] = "CoreSet28",
        });
        Assert.Contains("glass substitutions accepted", text);
    }

    [Fact]
    public void EveryActionToolAdvertisesItselfProperly()
    {
        Assert.NotEmpty(ActionTools.All);
        foreach (var tool in ActionTools.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.NotEmpty(tool.Arguments);
            foreach (var arg in tool.Arguments)
            {
                Assert.False(string.IsNullOrWhiteSpace(arg.Name));
                Assert.False(string.IsNullOrWhiteSpace(arg.Description));
                Assert.Contains(arg.Type, new[] { "string", "integer", "number", "boolean" });
            }
        }
    }

    /// <summary>
    /// The action tools must not collide with the reporting ones, or a call goes to whichever
    /// list happens to be searched first.
    /// </summary>
    [Fact]
    public void ActionToolNamesDoNotCollideWithReportingToolNames()
    {
        foreach (var action in ActionTools.All)
            foreach (var report in Tools.All)
                Assert.NotEqual(report.Name, action.Name);
    }
}
