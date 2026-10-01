using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Mcp;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The <c>nodal_aberrations</c> tool, and the thing it exists to guarantee: that a lens reads the
/// same through the server as through the command line.
///
/// <para>Nodal aberration theory reached the CLI first and had no MCP tool at all, so over the
/// server the whole subject was invisible - a caller could ask for Seidel coefficients but not
/// for where their nodes had gone, and could not tilt anything to find out. Worse,
/// <c>Tools.Open</c> did not read the <c>.align</c> sidecar, so a perturbed lens analysed
/// perfectly aligned without saying so.</para>
/// </summary>
public class McpNodalToolTests
{
    private static ActionTool Tool =>
        ActionTools.All.Single(t => t.Name == "nodal_aberrations");

    /// <summary>A lens with an alignment sidecar beside it, in a folder of its own.</summary>
    private static string LensWithSidecar(string? alignment)
    {
        string dir = Path.Combine(Path.GetTempPath(), "abcalc-nat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string lens = Path.Combine(dir, "lens.lhlt");
        File.Copy(Fixtures.Lens("CookeTriplet"), lens);
        if (alignment != null) File.WriteAllText(AlignmentFile.PathFor(lens), alignment);
        return lens;
    }

    private static string Run(string lens, string? alignment = null, bool grid = false)
    {
        var args = new JsonObject { ["lens_file"] = lens };
        if (alignment != null) args["alignment"] = alignment;
        if (grid) args["full_field"] = true;
        return Tool.Run(args);
    }

    private static string NodeLine(string report, string label) =>
        report.Split('\n').First(l => l.Contains(label, StringComparison.Ordinal)).Trim();

    /// <summary>
    /// <b>The sidecar is read, and gives what the command line gives.</b> The CLI applies
    /// <c>.align</c> to every analysis; the server must do the same or the two disagree about
    /// what a lens is.
    /// </summary>
    [Fact]
    public void TheSidecarIsReadAndAgreesWithTheInlineForm()
    {
        const string perturbation = "TILT 2 Y 0.15\nDEC 3 X 0.02";

        string withFile = LensWithSidecar(perturbation);
        string fromSidecar = Run(withFile);

        string noFile = LensWithSidecar(null);
        string fromInline = Run(noFile, perturbation);

        foreach (string label in new[] { "coma          one node at",
                                         "astigmatism   two nodes at",
                                         "medial focus  vertex at" })
            Assert.Equal(NodeLine(fromSidecar, label), NodeLine(fromInline, label));

        // and it really is perturbed, or the two would agree by both being the nominal design
        Assert.DoesNotContain("This design is ALIGNED", fromSidecar, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no sidecar and no inline text the nominal design is analysed, and the report says so
    /// rather than pretending there is nothing to report.
    /// </summary>
    [Fact]
    public void WithNoPerturbationItAnalysesTheNominalDesignAndSaysSo()
    {
        string report = Run(LensWithSidecar(null));
        Assert.Contains("This design is ALIGNED", report, StringComparison.Ordinal);
        Assert.Contains("NODAL ABERRATION THEORY", report, StringComparison.Ordinal);
    }

    /// <summary>The inline text overrides a sidecar, as <c>optimize</c>'s merit text overrides a
    /// <c>.mf</c>.</summary>
    [Fact]
    public void InlineTextOverridesTheSidecar()
    {
        string lens = LensWithSidecar("TILT 2 Y 0.15");
        string overridden = Run(lens, "TILT 2 Y 0.40");
        string asFiled = Run(lens);

        Assert.NotEqual(NodeLine(asFiled, "coma          one node at"),
                        NodeLine(overridden, "coma          one node at"));
    }

    /// <summary><c>full_field</c> returns the grid, tab separated, with a row per field point.</summary>
    [Fact]
    public void TheFullFieldGridIsATabSeparatedTable()
    {
        string tsv = Run(LensWithSidecar(null), "TILT 2 Y 0.15", grid: true);
        var lines = tsv.TrimEnd().Split('\n');

        Assert.StartsWith("hx\thy\tcoma", lines[0], StringComparison.Ordinal);
        Assert.True(lines.Length > 1, "the grid has no rows");

        int columns = lines[0].Trim().Split('\t').Length;
        foreach (string line in lines.Skip(1))
            Assert.Equal(columns, line.Trim().Split('\t').Length);
    }

    /// <summary>
    /// A malformed alignment is reported with its LINE NUMBER and the line itself, because the
    /// text came from the caller and they have no other way to see what the parser made of it.
    /// It goes through the same parser the sidecar uses, so the grammar cannot drift between the
    /// two ways of stating a perturbation.
    /// </summary>
    [Fact]
    public void ABadAlignmentSaysWhichLineAndWhy()
    {
        var bad = Assert.ThrowsAny<Exception>(
            () => Run(LensWithSidecar(null), "TILT 2 Y 0.15\nTILT 3 BANANA"));

        Assert.Contains("line 2", bad.Message, StringComparison.Ordinal);
        Assert.Contains("BANANA", bad.Message, StringComparison.Ordinal);
    }

    /// <summary>And a missing lens is refused the same way every other tool refuses one.</summary>
    [Fact]
    public void AMissingLensSaysSo()
    {
        Assert.ThrowsAny<Exception>(() => Run("no-such-lens-anywhere.zmx"));
        Assert.ThrowsAny<Exception>(() => Tool.Run(new JsonObject()));
    }
}
