using System;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Saving a design: into the format it was read, by editing that file; into another, by writing a
/// whole lens. The second went through the editor too, whatever the output was called, so a ZEMAX
/// design saved "as" a .len became ZEMAX text in a file named for OSLO.
/// </summary>
public class LensSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "abcalc_save_" + Guid.NewGuid().ToString("N"));
    private static readonly GlassCatalog Catalog = CatalogLocator.LoadBundled();

    public LensSaveTests()
    {
        Directory.CreateDirectory(_dir);
        OptilandGlass.UserCatalogsFolderOverride = Path.Combine(_dir, "optiland-home");
    }

    public void Dispose()
    {
        OptilandGlass.UserCatalogsFolderOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Fixture(string name, string folder) => Designs.PathOf(name, folder);

    private static double Efl(OpticalSystem s)
    {
        int pw = Math.Max(0, s.PrimaryWavelengthIndex);
        return ParaxialTrace.Trace(s, IndexResolver.Build(s, Catalog, s.Wavelengths[pw].Value), 0.0).Efl;
    }

    [Fact]
    public void AZemaxDesignSavedAsOsloIsAnOsloLens()
    {
        string original = Fixture("KingslakeDG.zmx", "lenses");
        var design = LensFile.Read(original, Catalog);
        design.Surfaces[2].Curvature *= 1.01;           // as an optimiser would move it
        string output = Path.Combine(_dir, "better.len");

        var notes = LensSave.Save(design, original, output, Catalog);

        string text = File.ReadAllText(output);
        Assert.StartsWith("// OSLO", text);
        Assert.Contains("LEN NEW", text);
        Assert.DoesNotContain("SURF ", text);           // no ZEMAX in it
        Assert.Contains(notes, n => n.Contains("Written as OSLO") && n.Contains("does not"));

        var back = LensFile.Read(output, Catalog);
        Assert.Equal(Efl(design), Efl(back), 9);
        Assert.Equal(design.Surfaces[2].Curvature, back.Surfaces[2].Curvature, 12);
    }

    [Theory]
    [InlineData(".seq")]
    [InlineData(".otx")]
    [InlineData(".json")]
    [InlineData(".lhlt")]
    [InlineData(".zmx")]
    public void AnOsloDesignSavedAsAnotherFormatReadsBackTheSame(string ext)
    {
        string original = Fixture("KingslakeDG.len", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "out" + ext);

        LensSave.Save(design, original, output, Catalog);

        var back = LensFile.Read(output, Catalog);
        Assert.Equal(Efl(design), Efl(back), 9);
        Assert.Equal(design.Surfaces.Count, back.Surfaces.Count);
    }

    /// <summary>The same format is still edited: nothing is said, and what the program does not model survives.</summary>
    [Fact]
    public void TheSameFormatIsEditedNotRewritten()
    {
        string original = Path.Combine(_dir, "lens.zmx");
        File.Copy(Fixture("KingslakeDG.zmx", "lenses"), original);
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.optimised.zmx");

        var notes = LensSave.Save(design, original, output, Catalog);

        Assert.Empty(notes);
        Assert.Equal(File.ReadAllText(original), File.ReadAllText(output));
    }

    /// <summary>Two extensions that name one format are one format: .osl is OSLO as .len is.</summary>
    [Fact]
    public void AnOsloFileSavedAsOslIsEdited()
    {
        string original = Fixture("KingslakeDG.len", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.osl");

        var notes = LensSave.Save(design, original, output, Catalog);

        Assert.Empty(notes);
        Assert.Equal(File.ReadAllText(original), File.ReadAllText(output));
    }

    [Fact]
    public void ADesignTheFormatCannotCarryIsRefusedAndNothingWritten()
    {
        string original = Fixture("G1_finite_r2.zmx", "coefficient-reference");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "r2.len");

        var ex = Assert.Throws<NotSupportedException>(() => LensSave.Save(design, original, output, Catalog));
        Assert.Contains("OSLO", ex.Message);
        Assert.Contains("r²", ex.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>A small ZEMAX singlet, written here line by line so its line endings can be chosen.</summary>
    private static readonly string[] Singlet =
    {
        "VERS 221026 6 20120530 20120530", "MODE SEQ", "NAME SINGLET", "UNIT MM X W X CM MR CPMM", "ENPD 10",
        "YFLN 0", "WAVM 1 0.5875618 1", "PWAV 1",
        "SURF 0", "  TYPE STANDARD", "  CURV 0.0 0 0 0 0 \"\"", "  DISZ INFINITY",
        "SURF 1", "  STOP", "  TYPE STANDARD", "  CURV 0.01 0 0 0 0 \"\"", "  DISZ 5", "  GLAS N-BK7 0 0 1.5168 64.17 0 0 0 0 0 0",
        "SURF 2", "  TYPE STANDARD", "  CURV -0.01 0 0 0 0 \"\"", "  DISZ 95",
        "SURF 3", "  TYPE STANDARD", "  CURV 0.0 0 0 0 0 \"\"", "  DISZ 0",
    };

    /// <summary>
    /// A file whose lines end both ways - written by one tool, edited by another - is edited like
    /// any other. The editor used to take the file's line ending from whether it held a single
    /// CRLF and split on that alone, so every LF-only line stayed glued to its neighbours, its
    /// keyword unseen: the save changed nothing and said it had written the design.
    /// </summary>
    [Fact]
    public void AFileWithMixedLineEndingsIsEdited()
    {
        string original = Path.Combine(_dir, "mixed.zmx");
        // Mostly LF, with a few CRLF - as the hand-built file that found this was.
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < Singlet.Length; i++) text.Append(Singlet[i]).Append(i % 7 == 3 ? "\r\n" : "\n");
        File.WriteAllText(original, text.ToString());

        var design = LensFile.Read(original, Catalog);
        design.Surfaces[2].Curvature = -0.012;
        design.Surfaces[1].Thickness = 5.5;
        string output = Path.Combine(_dir, "mixed.out.zmx");
        LensSave.Save(design, original, output, Catalog);

        var back = LensFile.Read(output, Catalog);
        Assert.Equal(-0.012, back.Surfaces[2].Curvature, 15);
        Assert.Equal(5.5, back.Surfaces[1].Thickness, 15);
        // Written back with one line ending, the one most of its lines had.
        string written = File.ReadAllText(output);
        Assert.DoesNotContain("\r\n", written);
        Assert.Equal(Singlet.Length, written.Split('\n').Length - 1);
    }

    /// <summary>
    /// A save that cannot put a value into the file says so, naming it, and does not claim to
    /// have written the design. A ZEMAX surface with no CURV line has nowhere for the editor to
    /// write a new curvature.
    /// </summary>
    [Fact]
    public void ASaveThatDoesNotArriveIsAnError()
    {
        string original = Path.Combine(_dir, "nocurv.zmx");
        File.WriteAllLines(original, Singlet.Where(l => l != "  CURV -0.01 0 0 0 0 \"\"").ToArray());

        var design = LensFile.Read(original, Catalog);
        Assert.Equal(0.0, design.Surfaces[2].Curvature);
        design.Surfaces[2].Curvature = -0.012;
        string output = Path.Combine(_dir, "nocurv.out.zmx");

        var ex = Assert.Throws<InvalidOperationException>(() => LensSave.Save(design, original, output, Catalog));
        Assert.Contains("does not read back as the design", ex.Message);
        Assert.Contains("surface 2 curvature", ex.Message);
    }

    [Fact]
    public void AFormatItDoesNotWriteIsRefused()
    {
        string original = Fixture("KingslakeDG.zmx", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.txt");

        var ex = Assert.Throws<NotSupportedException>(() => LensSave.Save(design, original, output, Catalog));
        Assert.Contains(".txt", ex.Message);
        Assert.False(File.Exists(output));
    }
}
