using AberrationCalculator.Core.IO;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Tests;

/// <summary>Whole splits, optimised. Each takes a few seconds.</summary>
public class SplitterTests
{
    [Fact]
    public void SplittingTheCookeTripletsRearElementShrinksTheSpotAndHoldsTheFocalLength()
    {
        var lens = Lenses.Cooke();
        var r = Splitter.Split(lens, Lenses.Catalog, 2);
        Assert.Equal(r.Original.Efl, r.Split.Efl, 9);
        Assert.True(r.Split.Prmsa < 0.9 * r.Original.Prmsa, $"predicted spot {r.Original.Prmsa} -> {r.Split.Prmsa}");
        Assert.True(r.SplitMerit < r.OriginalMerit);

        // The glass edges are kept, measured as the optimiser measures them: at the paraxial beam.
        var p = r.Split.Paraxial;
        var s = r.Split.System;
        foreach (int i in new[] { r.Front, r.Front + 2 })
        {
            double y = Math.Max(Math.Abs(p.Y[i]) + Math.Abs(p.Ybar[i]), Math.Abs(p.Y[i + 1]) + Math.Abs(p.Ybar[i + 1]));
            double edge = s.Surfaces[i].Thickness - s.Surfaces[i].Sag(y) + s.Surfaces[i + 1].Sag(y);
            Assert.True(edge > 0.49, $"edge after surface {i}: {edge}");
        }
    }

    [Fact]
    public void AFiguredSingletIsSplitWithItsFiguringUntouched()
    {
        var lens = Lenses.FiguredSinglet();
        var r = Splitter.Split(lens, Lenses.Catalog, 0);
        var front = r.Split.System.Surfaces[1];
        Assert.Equal(-0.6, front.Conic, 12);
        Assert.Equal(lens.Surfaces[1].AsphericCoefficients, front.AsphericCoefficients);
        Assert.Equal(r.Original.Efl, r.Split.Efl, 9);
        Assert.True(r.Split.Prmsa < r.Original.Prmsa, $"predicted spot {r.Original.Prmsa} -> {r.Split.Prmsa}");
    }

    /// <summary>The split, written in every format and read back, is the same lens.</summary>
    [Fact]
    public void TheSplitIsWrittenInEveryFormat()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lenssplitter_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var r = Splitter.Split(Lenses.Cooke(), Lenses.Catalog, 2, new SplitOptions { Refine = 2 });
            foreach (var ext in LensFile.WritableExtensions)
            {
                string path = Path.Combine(dir, "split" + ext);
                LensFile.Write(r.Split.System, path, Lenses.Catalog, installOptilandGlasses: false);
                var back = new LensOptics(LensFile.Read(path, Lenses.Catalog), Lenses.Catalog);
                Assert.True(Math.Abs(back.Efl - r.Split.Efl) < 1e-9 * Math.Abs(r.Split.Efl), $"{ext}: EFL {back.Efl} against {r.Split.Efl}");
                // The coefficients at the full field. (Not the predicted spot: an OSLO file keeps the
                // full field but not the lens's list of field points, and the spot is averaged over them.
                // And in OSLO in size only: at a finite object OSLO takes the field as an object height,
                // on the other side of the axis from a positive field angle, so a term odd in the
                // field - coma, distortion - changes sign. The lens is the same.)
                foreach (var c in new[] { "B", "F", "C", "Pi", "E", "B5", "B7" })
                {
                    double a = r.Split.Buchdahl.Totals[c], b = back.Buchdahl.Totals[c];
                    if (ext == ".len") { a = Math.Abs(a); b = Math.Abs(b); }
                    Assert.True(Math.Abs(a - b) < 1e-6 * Math.Abs(a) + 1e-15, $"{ext}: {c} {b} against {a}");
                }
                if (ext != ".len")
                    Assert.True(Math.Abs(back.Prmsa - r.Split.Prmsa) < 1e-6 * r.Split.Prmsa, $"{ext}: spot {back.Prmsa} against {r.Split.Prmsa}");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
