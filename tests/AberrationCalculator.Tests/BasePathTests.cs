using System;
using System.IO;

using AberrationCalculator.Core.IO;
using AberrationCalculator.Mcp;
using AberrationCalculator.Optimize.Io;

using System.Text.Json.Nodes;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The folder bare file names are taken to mean.
///
/// <para>The base path is held in environment variables and a file in a well-known place, which
/// is process-wide state, so this class joins <see cref="ProcessWideState"/> - the collection of
/// classes that never run alongside each other. Everything else in the suite passes absolute
/// paths, which <see cref="BasePath.Resolve"/> returns untouched without ever consulting the
/// base, so nothing outside that collection can be disturbed by it either.</para>
///
/// <para>No test here changes the working directory, for the same reason: it is process-wide.
/// That the base is what did the work is shown instead by putting the lens ONLY in the base
/// folder - a bare name that resolves at all could not have come from anywhere else.</para>
/// </summary>
[Collection(ProcessWideState.Name)]
public class BasePathTests : IDisposable
{
    private readonly string _root;
    private readonly string? _home;
    private readonly string? _dir;

    public BasePathTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "abcalc-base-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        // The real setting lives in the user's profile. A test that wrote there would change the
        // machine it ran on, so it is pointed somewhere disposable first - and anything the
        // developer happens to have set is put aside for the duration.
        _home = Environment.GetEnvironmentVariable(BasePath.HomeVariable);
        _dir = Environment.GetEnvironmentVariable(BasePath.OverrideVariable);
        Environment.SetEnvironmentVariable(BasePath.HomeVariable, Path.Combine(_root, "home"));
        Environment.SetEnvironmentVariable(BasePath.OverrideVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(BasePath.HomeVariable, _home);
        Environment.SetEnvironmentVariable(BasePath.OverrideVariable, _dir);
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    /// <summary>A folder holding one copy of the Cooke triplet, and nothing else.</summary>
    private string FolderWithALens(string name, string lens = "L.lhlt")
    {
        string folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        File.Copy(Fixtures.Lens("CookeTriplet"), Path.Combine(folder, lens));
        return folder;
    }

    private static string Capture(Func<int> run, out int code)
    {
        var output = new StringWriter();
        var previous = Console.Out;
        try { Console.SetOut(output); code = run(); }
        finally { Console.SetOut(previous); }
        return output.ToString();
    }

    // ── Setting and showing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// BASE takes no lens, and must not: the whole point of it is to stop you typing the path to
    /// one. Saying it on its own is a complete instruction, so it does not get the usage text.
    /// </summary>
    [Fact]
    public void BaseIsGivenWithNoLensAtAll()
    {
        string folder = FolderWithALens("project");

        string text = Capture(() => Cli.Program.Run(new[] { "BASE", folder }), out int code);

        Assert.Equal(0, code);
        Assert.Contains("BASE", text);
        Assert.Contains(folder, text);
        Assert.DoesNotContain("USAGE", text);
        Assert.Equal(Path.GetFullPath(folder), BasePath.Stored());
    }

    /// <summary>
    /// Four things can set the base and they override each other, so "why is it looking there?"
    /// is the question this feature raises. BASELIST answers it every time.
    /// </summary>
    [Fact]
    public void BaselistSaysWhereTheBaseCameFrom()
    {
        Assert.Contains("no base is set",
                        Capture(() => Cli.Program.Run(new[] { "BASELIST" }), out _));

        string folder = FolderWithALens("project");
        Cli.Program.Run(new[] { "BASE", folder, "-q" });

        string text = Capture(() => Cli.Program.Run(new[] { "BASELIST" }), out _);
        Assert.Contains(folder, text);
        Assert.Contains("the BASE command", text);
    }

    [Fact]
    public void BaseremoveGivesTheWorkingDirectoryBackAgain()
    {
        string folder = FolderWithALens("project");
        Cli.Program.Run(new[] { "BASE", folder, "-q" });
        Assert.NotNull(BasePath.Stored());

        string text = Capture(() => Cli.Program.Run(new[] { "BASEREMOVE" }), out int code);

        Assert.Equal(0, code);
        Assert.Null(BasePath.Stored());
        Assert.Contains("no base is set", text);
        Assert.Equal(Directory.GetCurrentDirectory(), BasePath.Current());
    }

    /// <summary>
    /// A base naming somewhere that does not exist would only show up later, as every file under
    /// it failing to be found, by which time the connection is hard to see.
    /// </summary>
    [Fact]
    public void ABaseThatDoesNotExistIsRefused()
    {
        Assert.ThrowsAny<Exception>(
            () => Cli.Program.Run(new[] { "BASE", Path.Combine(_root, "nowhere"), "-q" }));
        Assert.Null(BasePath.Stored());
    }

    // ── What it does to paths ────────────────────────────────────────────────────────────

    [Fact]
    public void ALensIsFoundByBareNameOnceABaseIsSet()
    {
        string folder = FolderWithALens("project");
        Cli.Program.Run(new[] { "BASE", folder, "-q" });

        // The lens exists ONLY in the base folder, so a bare name that resolves at all could
        // not have come from the working directory.
        Assert.False(File.Exists("L.lhlt"));

        string text = Capture(() => Cli.Program.Run(new[] { "L.lhlt", "VARLIST" }), out int code);
        Assert.Equal(0, code);
        Assert.Contains("VARIABLES", text);
    }

    /// <summary>
    /// A base that silently re-rooted absolute paths would be a trap rather than a convenience.
    /// </summary>
    [Fact]
    public void AnAbsolutePathIsNeverReRooted()
    {
        string here = FolderWithALens("project");
        string elsewhere = FolderWithALens("elsewhere", "other.lhlt");
        Cli.Program.Run(new[] { "BASE", here, "-q" });

        string absolute = Path.Combine(elsewhere, "other.lhlt");
        Assert.Equal(0, Cli.Program.Run(new[] { absolute, "VARLIST", "-q" }));

        Assert.Equal(absolute, BasePath.Resolve(absolute));
    }

    /// <summary>
    /// --dir overrides the stored base for one run WITHOUT un-setting it, which is the point of
    /// having layers at all: a quick look at another folder should not cost you your setting.
    /// </summary>
    [Fact]
    public void DirBeatsTheStoredBaseAndLeavesItAlone()
    {
        string stored = FolderWithALens("project");
        string other = FolderWithALens("other", "only-here.lhlt");
        Cli.Program.Run(new[] { "BASE", stored, "-q" });

        Assert.Equal(0, Cli.Program.Run(
            new[] { "only-here.lhlt", "--dir", other, "VARLIST", "-q" }));

        Assert.Equal(Path.GetFullPath(stored), BasePath.Stored());
        Assert.Contains("--dir on this command line", BasePath.Source(other));
    }

    [Fact]
    public void TheEnvironmentBeatsTheStoredBase()
    {
        string stored = FolderWithALens("project");
        string other = FolderWithALens("other", "only-here.lhlt");
        Cli.Program.Run(new[] { "BASE", stored, "-q" });

        Environment.SetEnvironmentVariable(BasePath.OverrideVariable, other);
        try
        {
            Assert.Equal(Path.GetFullPath(other), BasePath.Current());
            Assert.Contains(BasePath.OverrideVariable, BasePath.Source());
            Assert.Equal(0, Cli.Program.Run(new[] { "only-here.lhlt", "VARLIST", "-q" }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(BasePath.OverrideVariable, null);
        }
    }

    /// <summary>Where a run WRITES is taken against the base as well, not only where it reads.</summary>
    [Fact]
    public void OutputPathsResolveAgainstTheBaseToo()
    {
        string folder = FolderWithALens("project");
        Cli.Program.Run(new[] { "BASE", folder, "-q" });

        Cli.Program.Run(new[] { "L.lhlt", "VAR", "CV 1", "VAR", "CV 4",
                                "OP", "EFL, 10, TAR 50", "-q" });
        Assert.Equal(0, Cli.Program.Run(
            new[] { "L.lhlt", "--optimize", "--iterations", "20", "--saveas", "better.lhlt",
                    "-q" }));

        Assert.True(File.Exists(Path.Combine(folder, "better.lhlt")));
    }

    /// <summary>
    /// A lens command with no lens used to read as a filename and fail with "no such file:
    /// VARLIST", which says nothing about what actually went wrong.
    /// </summary>
    [Fact]
    public void ALensCommandGivenWithoutALensSaysWhatIsMissing()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => Cli.Program.Run(new[] { "VARLIST" }));

        Assert.Contains("about a particular lens", ex.Message);
        Assert.Contains("abcalc lens.zmx VARLIST", ex.Message);
    }

    // ── The MCP, which is where it matters most ──────────────────────────────────────────

    /// <summary>
    /// An MCP server's working directory is whatever the client started it in, so without a base
    /// every path an assistant passes has to be absolute. It reads the same setting the command
    /// line writes.
    /// </summary>
    [Fact]
    public void TheMcpToolSetsTheSameBaseTheCommandLineDoes()
    {
        string folder = FolderWithALens("project");
        var tool = Assert.Single(ActionTools.All, t => t.Name == "base_path");

        string text = tool.Run(new JsonObject { ["path"] = folder });
        Assert.Contains(folder, text);
        Assert.Equal(Path.GetFullPath(folder), BasePath.Stored());

        // And the command line sees it.
        Assert.Contains(folder, Capture(() => Cli.Program.Run(new[] { "BASELIST" }), out _));

        // Reporting tools now take a bare name.
        var analyse = Assert.Single(Tools.All, t => t.Name == "first_order");
        Assert.NotEmpty(analyse.Run(Tools.Open("L.lhlt", null), null));

        tool.Run(new JsonObject { ["clear"] = true });
        Assert.Null(BasePath.Stored());
    }

    [Fact]
    public void TheMcpToolRefusesToSetAndClearAtOnce()
    {
        var tool = Assert.Single(ActionTools.All, t => t.Name == "base_path");
        var ex = Assert.ThrowsAny<ArgumentException>(() => tool.Run(new JsonObject
        {
            ["path"] = FolderWithALens("project"),
            ["clear"] = true,
        }));
        Assert.Contains("opposite", ex.Message);
    }

    [Fact]
    public void TheMcpOptimiseToolTakesBareNamesAgainstTheBase()
    {
        string folder = FolderWithALens("project");
        Cli.Program.Run(new[] { "BASE", folder, "-q" });

        var tool = Assert.Single(ActionTools.All, t => t.Name == "optimize");
        string text = tool.Run(new JsonObject
        {
            ["lens_file"] = "L.lhlt",
            ["variables"] = "VAR CV 1\nVAR CV 4\n",
            ["merit"] = "EFL, 10, TAR 50\n",
            ["iterations"] = 15,
            ["save_to"] = "from-mcp.lhlt",
        });

        Assert.Contains("Written", text);
        Assert.True(File.Exists(Path.Combine(folder, "from-mcp.lhlt")));
    }
}
