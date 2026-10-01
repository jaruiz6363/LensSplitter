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
}
