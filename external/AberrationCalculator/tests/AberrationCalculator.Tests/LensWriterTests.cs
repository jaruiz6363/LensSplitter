using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The whole-lens writers, by round trip: every design this repository keeps, written in each
/// format and read back by this program's own reader, must be the same lens - the same focal
/// length and the same coefficients - or be refused with a reason the format explains.
/// </summary>
public class LensWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "abcalc_writer_" + Guid.NewGuid().ToString("N"));

    public LensWriterTests()
    {
        Directory.CreateDirectory(_dir);
        OptilandGlass.UserCatalogsFolderOverride = Path.Combine(_dir, "optiland-home");
    }

    public void Dispose()
    {
        OptilandGlass.UserCatalogsFolderOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    public static IEnumerable<object[]> DesignsByFormat()
    {
        foreach (var d in Designs.All())
            foreach (var ext in LensFile.WritableExtensions)
                yield return new[] { d[0], d[1], ext };
    }

    private static (double Efl, BuchdahlTerms Totals, double[] N) Analyse(OpticalSystem sys, GlassCatalog catalog)
    {
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        return (p.Efl, BuchdahlCoefficients.Compute(sys, p).Totals, n);
    }

    [Theory]
    [MemberData(nameof(DesignsByFormat))]
    public void EveryDesignRoundTripsThroughEveryFormat(string name, string folder, string ext)
    {
        if (Designs.IsNearSingular(name)) return;
        var catalog = CatalogLocator.LoadBundled();
        var original = LensFile.Read(Designs.PathOf(name, folder), catalog);
        string path = Path.Combine(_dir, Path.GetFileNameWithoutExtension(name) + ext);
        try
        {
            LensFile.Write(original, path, catalog);
        }
        catch (InvalidOperationException ex)
        {
            // Refused only for what the format cannot carry, and saying so. Anything else - a
            // glass the catalogs lack, a conversion that failed - is a fault in the writer.
            string[] cannotCarry =
            {
                "r² aspheric term", "ideal (paraxial) lens", "aspheric term beyond",
                "tilted or decentred", "CoordinateBreak surface", "Abcd surface", "needs a finite object",
            };
            Assert.True(cannotCarry.Any(ex.Message.Contains), $"{name} as {ext} refused: {ex.Message}");
            return;
        }

        var back = LensFile.Read(path, catalog);
        var a = Analyse(original, catalog);
        var b = Analyse(back, catalog);
        Assert.True(Math.Abs(a.Efl - b.Efl) <= 1e-9 * Math.Abs(a.Efl) + 1e-12,
            $"{name} as {ext}: EFL {a.Efl:R} became {b.Efl:R}");
        foreach (string c in new[] { "B", "F", "C", "Pi", "E", "B5", "F1", "M1", "N1", "C5", "E5", "B7" })
        {
            double x = a.Totals[c], y = b.Totals[c];
            Assert.True(Math.Abs(x - y) <= 1e-7 * Math.Max(Math.Abs(x), 1e-12 * Math.Abs(a.Totals.B)) + 1e-15,
                $"{name} as {ext}: {c} {x:R} became {y:R}");
        }
    }

    /// <summary>
    /// A glass name with small letters goes into a .zmx in capitals, which is how OpticStudio writes
    /// its own files and the only form it resolved in testing (GLAS K-LaSFn9 left the glass unfound,
    /// GLAS K-LASFN9 did not); and the capitalised name still reads back as the same catalog glass.
    /// </summary>
    [Fact]
    public void ZmxWritesGlassNamesInCapitals()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = new OpticalSystem { Aperture = new Aperture(Core.Enums.ApertureType.EPD, 10.0) };
        sys.GlassCatalogs.Add("SUMITA");
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Material = "K-LaSFn9", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Radius = -50.0, Thickness = 40.0 });
        sys.Surfaces.Add(new Surface { Index = 3 });

        string path = Path.Combine(_dir, "mixedcase.zmx");
        ZmxWriter.Write(sys, path);

        Assert.Contains("GLAS K-LASFN9 ", File.ReadAllText(path));
        var back = ZmxReader.Read(path, catalog);
        var glass = catalog.Find(back.Surfaces[1].Material, back.GlassCatalogs);
        Assert.NotNull(glass);
        Assert.Equal("SUMITA", glass!.Catalog);
        Assert.Equal(Analyse(sys, catalog).Efl, Analyse(back, catalog).Efl, 12);
    }

    /// <summary>
    /// OSLO reads a number standing as a word in the LEN NEW name as the surface count and refuses
    /// the file, so numbers are dropped - again after cutting the name to 32 characters, which can
    /// leave a new one at the end (Ross Optical's doublets: "... DIA; 100.00MM EFL" cut to "... DIA; 1").
    /// </summary>
    [Theory]
    [InlineData("62478 Negative Achromatic Lens", "Negative Achromatic Lens")]
    [InlineData("POSITIVE DOUBLET; 26.50MM DIA; 100.00MM EFL", "POSITIVE DOUBLET; 26.50MM DIA;")]
    [InlineData("12 34", "Untitled")]
    public void TheOsloNameHasNoNumberInIt(string title, string expected) =>
        Assert.Equal(expected, OsloWriter.LenName(title));

    /// <summary>
    /// OSLO refuses a system note longer than 80 characters and stops loading the file ("Error at
    /// sno_: system note: Argument can not contain more than 80 chars"; OSLO EDU, 2026-10-29). The
    /// note here is the 140-character one that MonoSearch export failed on. Notes are wrapped at
    /// words into notes 2 to 9, and the words all come back when the file is read.
    /// </summary>
    [Fact]
    public void OsloNotesAreAtMost80Characters()
    {
        const string note = "MonoSearch (Stamenov, Agurok, Ford, Appl. Opt. 51, 7648 (2012)). Outer OHARA:S-LAH79, " +
                            "inner SUMITA:K-LaSFn9. dPhi2 = 0.00686.";
        var catalog = CatalogLocator.LoadBundled();
        var sys = new OpticalSystem { Title = "Monocentric rank 1: S-LAH79 / K-LaSFn9, f=12, F/1.71", Notes = note };
        sys.Aperture = new Aperture(Core.Enums.ApertureType.EPD, 7.0);
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Radius = 50.0, Thickness = 5.0, Material = "N-BK7", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Radius = -50.0, Thickness = 40.0 });
        sys.Surfaces.Add(new Surface { Index = 3 });

        string path = Path.Combine(_dir, "notes.len");
        OsloWriter.Write(sys, path, catalog);

        var sno = File.ReadAllLines(path).Where(l => l.StartsWith("SNO")).ToList();
        Assert.Equal(3, sno.Count);
        foreach (var l in sno)
            Assert.True(l.Substring(l.IndexOf('"') + 1).TrimEnd('"').Length <= OsloWriter.NoteLength, l);
        var back = OsloReader.Read(path);
        Assert.Equal(sys.Title, back.Title);
        Assert.Equal(note, back.Notes!.Replace("\n", " "));
    }

    /// <summary>
    /// A .len has no field points set, so the fields go to OSLO as lens drawing fans, in the form
    /// OSLO 6.6 saves them after END (index from 0): 15 rays across the whole pupil from each field,
    /// at the fraction of the full field that OSLO's tan-based object height gives (30 deg of 60 is
    /// tan 30 / tan 60 = 1/3), the rays drawn to the image surface (DLRS 3) and that surface drawn
    /// (DRW ON), as OSLO saved them once set by hand (OSLO EDU, 2026-10-29). The lens still reads back.
    /// </summary>
    [Fact]
    public void OsloDrawsFifteenRaysFromEveryFieldAndTheImageSurface()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = new OpticalSystem { Title = "fans", Aperture = new Aperture(Core.Enums.ApertureType.EPD, 7.0) };
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        foreach (var a in new[] { 0.0, 30.0, 60.0 }) sys.Fields.Add(new Field(a));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Radius = 9.0, Thickness = 5.0, Material = "N-BK7" });
        sys.Surfaces.Add(new Surface { Index = 2, Thickness = 5.0, Material = "N-BK7", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 3, Radius = -9.0, Thickness = 10.0 });
        sys.Surfaces.Add(new Surface { Index = 4, Radius = -14.0 });

        string path = Path.Combine(_dir, "fans.len");
        OsloWriter.Write(sys, path, catalog);
        var lines = File.ReadAllLines(path);
        int end = Array.FindIndex(lines, l => l.StartsWith("END"));
        Assert.Equal("  DRW ON", lines[end - 3]);   // the image surface, drawn though it is a dummy
        Assert.Equal(new[]
        {
            "DLRS 3", "DLNF  3",
            "DLNR  0 15", "DLFP  0 0.0", "DLMN  0 -1.0", "DLMX  0 1.0",
            "DLNR  1 15", "DLFP  1 0.33333", "DLMN  1 -1.0", "DLMX  1 1.0",
            "DLNR  2 15", "DLFP  2 1.0", "DLMN  2 -1.0", "DLMX  2 1.0",
        }, lines.Skip(end + 1).ToArray());

        var back = OsloReader.Read(path);
        Assert.Equal(Analyse(sys, catalog).Efl, Analyse(back, catalog).Efl, 10);
    }

    [Fact]
    public void OsloKeepsAtMostNineNotesAndCutsALongTitle()
    {
        var notes = OsloWriter.SystemNotes(new string('T', 100) + " end", string.Join("\n", Enumerable.Repeat("line", 20)));
        Assert.Equal(9, notes.Count);
        Assert.Equal(new string('T', 80), notes[0]);
        Assert.All(notes, n => Assert.True(n.Length <= OsloWriter.NoteLength));
    }
}
