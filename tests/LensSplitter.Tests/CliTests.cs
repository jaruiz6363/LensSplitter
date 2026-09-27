using AberrationCalculator.Core.IO;
using Xunit;

namespace LensSplitter.Tests;

/// <summary>The command line, end to end.</summary>
public class CliTests
{
    [Fact]
    public async Task SplitWritesTheLensInTheFormatsAskedAReportAndADrawing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lenssplitter_cli_" + Guid.NewGuid().ToString("N"));
        try
        {
            int code = await LensSplitter.Cli.Program.Main(new[]
            {
                "split", "-i", Lenses.Path("TestData/Cooke_40deg_FC.zmx"), "-o", dir, "-e", "3", "--format", "seq,len,lhlt",
            });
            Assert.Equal(0, code);
            foreach (var ext in new[] { ".seq", ".len", ".lhlt", ".txt", ".svg" })
                Assert.True(File.Exists(Path.Combine(dir, "Cooke_40deg_FC_split3" + ext)), ext);
            Assert.Contains("Predicted spot (PRMSA)", File.ReadAllText(Path.Combine(dir, "Cooke_40deg_FC_split3.txt")));
            var back = LensFile.Read(Path.Combine(dir, "Cooke_40deg_FC_split3.lhlt"), Lenses.Catalog);
            Assert.Equal(10, back.Surfaces.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task MeritWritesTheLensesDefault()
    {
        string file = Path.Combine(Path.GetTempPath(), "lenssplitter_" + Guid.NewGuid().ToString("N") + ".mf");
        try
        {
            Assert.Equal(0, await LensSplitter.Cli.Program.Main(new[] { "merit", "-i", Lenses.Path("TestData/Cooke_40deg_FC.zmx"), "-o", file }));
            string text = File.ReadAllText(file);
            Assert.Contains("PRMSA, 1, TAR 0", text);
            Assert.Contains("AXC,", text);
        }
        finally { File.Delete(file); }
    }
}
