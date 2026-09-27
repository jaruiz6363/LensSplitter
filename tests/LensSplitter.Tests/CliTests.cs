using AberrationCalculator.Core.IO;
using LensSplitter.Cli;
using Xunit;

namespace LensSplitter.Tests;

/// <summary>
/// The command line captures the console, which is one per process, so these run one at a time
/// and apart from the rest.
/// </summary>
[CollectionDefinition("console", DisableParallelization = true)]
public class ConsoleCollection { }

/// <summary>The command line, end to end.</summary>
[Collection("console")]
public class CliTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lenssplitter_cli_" + Guid.NewGuid().ToString("N"));
    private static readonly string Cooke = Lenses.Path("TestData/Cooke_40deg_FC.zmx");

    public CliTests()
    {
        Directory.CreateDirectory(_dir);
        // An Optiland export installs its glasses for Optiland; in a test, not in the user's own folder.
        OptilandGlass.UserCatalogsFolderOverride = Path.Combine(_dir, "optiland-home");
    }

    public void Dispose()
    {
        OptilandGlass.UserCatalogsFolderOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>Runs the program, and returns its exit code with everything it printed.</summary>
    private static Task<(int Code, string Out)> Run(params string[] args) => Typed(null, args);

    private static async Task<(int Code, string Out)> Typed(string? stdin, string[] args)
    {
        var outWas = Console.Out;
        var errWas = Console.Error;
        var inWas = Console.In;
        var text = new StringWriter();
        Console.SetOut(text);
        Console.SetError(text);
        if (stdin != null) Console.SetIn(new StringReader(stdin));
        try
        {
            int code = await Program.Main(args);
            await Task.Delay(50);   // progress lines are posted, not written, so let them land
            return (code, text.ToString());
        }
        finally
        {
            Console.SetOut(outWas);
            Console.SetError(errWas);
            Console.SetIn(inWas);
        }
    }

    private string Out(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task AnalyzeShowsTheLensItsElementsAndTheRecommendation()
    {
        var (code, output) = await Run("analyze", "-i", Cooke);
        Assert.Equal(0, code);
        Assert.Contains("A SIMPLE COOKE TRIPLET", output);
        Assert.Contains("Focal length:  49.9999995", output);
        Assert.Contains("no: negative power", output);
        Assert.Contains("Recommended: element 3", output);
    }

    [Fact]
    public async Task SplitWritesTheInputsOwnFormatByDefaultWithAReportAndADrawing()
    {
        var (code, output) = await Run("split", "-i", Cooke, "-o", _dir, "-e", "3");
        Assert.Equal(0, code);
        Assert.Equal(new[] { ".svg", ".txt", ".zmx" },
            Directory.GetFiles(_dir, "Cooke_40deg_FC_split3.*").Select(Path.GetExtension).OrderBy(x => x));
        string report = File.ReadAllText(Out("Cooke_40deg_FC_split3.txt"));
        Assert.Contains("Split of element 3", report);
        Assert.Contains("Predicted spot (PRMSA)", report);
        Assert.Contains("<svg", File.ReadAllText(Out("Cooke_40deg_FC_split3.svg")));
        Assert.Contains("Written:", output);

        // The file written is the split: two more surfaces, the same focal length.
        var back = LensFile.Read(Out("Cooke_40deg_FC_split3.zmx"), Lenses.Catalog);
        Assert.Equal(10, back.Surfaces.Count);
        Assert.Equal(49.9999995, new LensSplitter.Core.Lens.LensOptics(back, Lenses.Catalog).Efl, 6);
    }

    [Fact]
    public async Task SplitWritesEveryFormatAskedAndTheOptilandGlasses()
    {
        var (code, _) = await Run("split", "-i", Cooke, "-o", _dir, "-e", "3", "--format", "all");
        Assert.Equal(0, code);
        foreach (var ext in LensFile.WritableExtensions)
            Assert.True(File.Exists(Out("Cooke_40deg_FC_split3" + ext)), ext);
        Assert.True(File.Exists(Path.Combine(_dir, "Cooke_40deg_FC_split3_glass", "lenshh-schott", "SK16.yml")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "optiland-home", "lenshh-schott")));
    }

    [Fact]
    public async Task SplitRecommendsTheElementWhenNoneIsNamed()
    {
        var (code, output) = await Run("split", "-i", Cooke, "-o", _dir, "--format", "lhlt");
        Assert.Equal(0, code);
        Assert.Contains("Splitting element 3", output);
        Assert.True(File.Exists(Out("Cooke_40deg_FC_split3.lhlt")));
    }

    /// <summary>A merit function written by the merit command, edited, and handed back.</summary>
    [Fact]
    public async Task SplitUsesAMeritFileItIsGiven()
    {
        string mf = Out("my.mf");
        Assert.Equal(0, (await Run("merit", "-i", Cooke, "-o", mf)).Code);
        File.WriteAllText(mf, "PRMSA, 1, TAR 0\nB7, 1, TAR 0\n");

        var (code, _) = await Run("split", "-i", Cooke, "-o", _dir, "-e", "3", "--merit", mf, "--format", "lhlt");
        Assert.Equal(0, code);
        string report = File.ReadAllText(Out("Cooke_40deg_FC_split3.txt"));
        Assert.Contains("B7, 1, TAR 0", report);
        Assert.DoesNotContain("AXC,", report);
    }

    [Fact]
    public async Task MeritWritesTheLensesDefault()
    {
        string file = Out("cooke.mf");
        var (code, output) = await Run("merit", "-i", Cooke, "-o", file);
        Assert.Equal(0, code);
        Assert.Contains("Written:", output);
        string text = File.ReadAllText(file);
        Assert.Contains("PRMSA, 1, TAR 0", text);
        Assert.Contains("AXC,", text);
        Assert.Contains("LCF, 1, TAR 0, 1.0", text);
    }

    [Fact]
    public async Task AnElementThatDoesNotExistIsAnError()
    {
        var (code, output) = await Run("split", "-i", Cooke, "-o", _dir, "-e", "7");
        Assert.Equal(1, code);
        Assert.Contains("there is no element 7", output);
        Assert.Empty(Directory.GetFiles(_dir, "*.zmx"));
    }

    [Fact]
    public async Task AFormatItDoesNotWriteIsAnError()
    {
        var (code, output) = await Run("split", "-i", Cooke, "-o", _dir, "-e", "3", "--format", "zmx,xyz");
        Assert.Equal(1, code);
        Assert.Contains("'xyz' is not a format LensSplitter writes", output);
    }

    [Fact]
    public async Task ACementedElementIsRefusedWithTheReason()
    {
        string lens = Out("doublet.lhlt");
        LensFile.Write(Lenses.CementedDoublet(), lens, Lenses.Catalog);
        var (code, output) = await Run("split", "-i", lens, "-o", _dir, "-e", "1");
        Assert.Equal(1, code);
        Assert.Contains("cemented doublet", output);
    }

    [Fact]
    public async Task NoRecommendationWithoutAnElementIsAnError()
    {
        string lens = Out("doublet.lhlt");
        LensFile.Write(Lenses.CementedDoublet(), lens, Lenses.Catalog);
        var (code, output) = await Run("split", "-i", lens, "-o", _dir);
        Assert.Equal(1, code);
        Assert.Contains("No element is recommended", output);
    }

    [Fact]
    public async Task AMissingLensFileIsAnError()
    {
        var (code, _) = await Run("analyze", "-i", Out("nothing-here.zmx"));
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task GlassRanksThePairsAndWritesTheBest()
    {
        var (code, output) = await Run("glass", "-i", Cooke, "-o", _dir, "-e", "3",
                                       "-g", "N-BK7,N-SSK5", "--refine", "2", "--top", "1", "--format", "lhlt");
        Assert.Equal(0, code);
        Assert.Contains("Glass pairs, best first", output);

        // N-BK7, N-SSK5 and the element's own SK16: nine ordered pairs, all in the CSV.
        var rows = File.ReadAllLines(Out("Cooke_40deg_FC_glass3.csv"));
        Assert.Equal("rank,glass_a,glass_b,merit,predicted_spot,screened", rows[0]);
        Assert.Equal(9, rows.Length - 1);
        // The two refined come first, with a merit; the rest were only screened.
        Assert.All(rows.Skip(1).Take(2), r => Assert.NotEqual("", r.Split(',')[3]));
        Assert.All(rows.Skip(3), r => Assert.Equal("", r.Split(',')[3]));

        var written = Directory.GetFiles(_dir, "Cooke_40deg_FC_glass3_1_*.lhlt");
        Assert.Single(written);
        string[] pair = rows[1].Split(',');
        Assert.EndsWith($"_{pair[1]}+{pair[2]}.lhlt", written[0]);
        var lens = LensFile.Read(written[0], Lenses.Catalog);
        Assert.Equal(pair[1], lens.Surfaces[5].Material);
        Assert.Equal(pair[2], lens.Surfaces[7].Material);
        Assert.Contains($"#1: {pair[1]} + {pair[2]}", File.ReadAllText(Out("Cooke_40deg_FC_glass3.txt")));
    }

    [Fact]
    public async Task AGlassTheCatalogsLackIsAnError()
    {
        var (code, output) = await Run("glass", "-i", Cooke, "-o", _dir, "-e", "3", "-g", "N-BK7,NOSUCHGLASS");
        Assert.Equal(1, code);
        Assert.Contains("NOSUCHGLASS", output);
    }

    [Fact]
    public async Task InteractiveModeAnalysesALensAndQuits()
    {
        var (code, output) = await Typed($"L\n{Cooke}\n1\nQ\n", Array.Empty<string>());
        Assert.True(code == 0, output);
        Assert.Contains("LensSplitter 2", output);
        Assert.Contains("Recommended: element 3", output);
    }

    [Fact]
    public async Task InteractiveModeWritesTheDefaultMerit()
    {
        string mf = Out("interactive.mf");
        var (code, _) = await Typed($"L\n{Cooke}\n4\n{mf}\nQ\n", Array.Empty<string>());
        Assert.Equal(0, code);
        Assert.Contains("PRMSA, 1, TAR 0", File.ReadAllText(mf));
    }
}
