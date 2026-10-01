using System;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The Optalix reader against what Optalix writes - its fictitious-glass codes, PRI glasses, FH
/// clipping apertures, RAIM codes and lens modules (pairs of SUT L surfaces) - and the in-place
/// save, which must leave all of that as it found it while writing the design's new values into
/// the right SUR blocks.
/// </summary>
public class OptalixFileTests
{
    // In Optalix's own line forms (the fictitious glass and PRI of Eye/EYE_NEW_CHROMATIC.OTX, the
    // tube lens of Gross-HOS/Misc/45-132_Chromat-with-tube-lens.otx), with a private glass 'GE'.
    private const string OptalixStyle = @"VERS 11.82
RAIM  2
EPD  10.0000
WL   0.54600     0.48600     0.65000
WTW  100 100 100
REF    1
FTYP    1
NFLD    2
FLD    1   0.000000000       0.000000000      100  1        2594861
FLD    2   0.000000000       2.000000000      100  1        2594861
SUR   0
SUT S
CUY 0.0000000000000
THI  0.1000000000E+21
SUR   1
SUT S
CUY  0.0100000000000
THI   5.000000000
GLA 613369
APE  1   10.00000000       10.00000000      0.000000000      0.000000000      0.000000000        1   0   0   1
FH    1  1
SUR   2
SUT S
CUY -0.0100000000000
THI   2.000000000
GLA 'GE'
APE  1   10.00000000       10.00000000      0.000000000      0.000000000      0.000000000        1   0   0   1
SUR   3
SUT L
CUY 0.0000000000000
THI 0.000000000
LMOD  0.1000000000E-01   0.000000000       0.000000000       0.000000000       0.000000000
STO
SUR   4
SUT L
CUY 0.0000000000000
THI 100.0000000
LMOD   0.000000000       0.000000000       0.000000000       0.000000000       0.000000000
SUR   5
SUT S
CUY  0.0020000000000
THI  50.00000000
PRI   1.336000000       1.336000000       1.336000000
SUR   6
SUT S
CUY 0.0000000000000
THI 0.000000000
";

    // A Mangin mirror as Optalix writes one (Telescopes/43-84_Mangin-mirror.otx): the mirror is the
    // back of the glass, and its GLA names the medium the reflected light goes on in - the glass.
    private const string Mangin = @"VERS 11.82
RAIM  2
EPD  20.0000
WL   0.5875618
WTW  100
REF    1
FTYP    1
NFLD    1
FLD    1   0.000000000       0.000000000      100  1        0
SUR   0
  SUT S
  CUY 0.0000000000000
  THI  0.1000000000E+21
SUR   1
  SUT S
  CUY -0.2000000000000E-02
  THI   10.00000000
  GLA N-BK7
  STO
SUR   2
  SUT SM
  CUY -0.4000000000000E-02
  THI  -10.00000000
  GLA N-BK7
SUR   3
  SUT S
  CUY -0.2000000000000E-02
  THI  -100.0000000
SUR   4
  SUT S
  CUY 0.0000000000000
  THI 0.000000000
";

    /// <summary>The same Mangin mirror, built here: the reflection a plain mirror, inside the glass.</summary>
    private static OpticalSystem ManginByHand()
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = -0.002, Thickness = 10.0, Material = "N-BK7", IsStop = true });
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -0.004, Thickness = -10.0, Material = "MIRROR" });
        sys.Surfaces.Add(new Surface { Index = 3, Curvature = -0.002, Thickness = -100.0 });
        sys.Surfaces.Add(new Surface { Index = 4 });
        return sys;
    }

    private static double Power(OpticalSystem sys)
    {
        var catalog = CatalogLocator.LoadBundled();
        var n = IndexResolver.Build(sys, catalog, 0.5875618, new System.Collections.Generic.List<string>());
        return ParaxialTrace.Trace(sys, n, 0.0).Power;
    }

    /// <summary>A mirror with a glass named on it is still a mirror: the glass is the medium it is in.</summary>
    [Fact]
    public void AManginMirrorIsAMirrorInGlass()
    {
        string path = Temp(Mangin);
        try
        {
            var sys = OptalixReader.Read(path);
            Assert.True(sys.Surfaces[2].IsMirror);
            Assert.Equal("N-BK7", sys.Surfaces[1].Material);
            Assert.True(string.IsNullOrEmpty(sys.Surfaces[3].Material));
            Assert.Equal(Power(ManginByHand()), Power(sys), 12);
            Assert.NotEqual(0.0, Power(sys));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// A mirror inside glass is written with that glass on it, as Optalix writes one: left off, as it
    /// was, Optalix put the reflected light in air - every ghost reflected inside a lens came out
    /// with the wrong focal length. A mirror in air has none.
    /// </summary>
    [Fact]
    public void AMirrorInGlassIsWrittenWithItsGlass()
    {
        string path = Path.ChangeExtension(Temp(""), ".otx");
        try
        {
            var sys = ManginByHand();
            OptalixWriter.Write(sys, path, CatalogLocator.LoadBundled());
            var lines = File.ReadAllLines(path).Select(l => l.Trim()).ToList();
            int mirror = lines.IndexOf("SUT SM");
            int next = lines.FindIndex(mirror, l => l.StartsWith("SUR"));
            Assert.Contains("GLA N-BK7", lines.GetRange(mirror, next - mirror));

            var back = OptalixReader.Read(path);
            Assert.True(back.Surfaces[2].IsMirror);
            Assert.Equal(Power(sys), Power(back), 12);

            // A model glass: the mirror carries its fictitious-glass code too.
            sys.Surfaces[1].Material = null;
            sys.Surfaces[1].ModelIndexEnabled = true;
            sys.Surfaces[1].ModelNd = 1.5168;
            sys.Surfaces[1].ModelVd = 64.17;
            OptalixWriter.Write(sys, path, CatalogLocator.LoadBundled());
            Assert.Equal(2, File.ReadAllLines(path).Count(l => l.Trim() == "GLA 5168.6417"));
            Assert.Equal(Power(sys), Power(OptalixReader.Read(path)), 12);

            // In air, nothing.
            sys.Surfaces[1].ModelIndexEnabled = false;
            OptalixWriter.Write(sys, path, CatalogLocator.LoadBundled());
            Assert.DoesNotContain(File.ReadAllLines(path), l => l.Trim().StartsWith("GLA"));
        }
        finally { File.Delete(path); }
    }

    /// <summary>Saving a Mangin design back into its file keeps the glass on the mirror.</summary>
    [Fact]
    public void SavingAManginMirrorKeepsItsGlass()
    {
        string path = Temp(Mangin);
        string output = Path.ChangeExtension(Temp(""), ".otx");
        try
        {
            var sys = OptalixReader.Read(path);
            sys.Surfaces[2].Curvature = -0.0041;
            LensPatcher.Save(sys, path, output);
            var back = OptalixReader.Read(output);
            Assert.Equal(-0.0041, back.Surfaces[2].Curvature, 12);
            Assert.True(back.Surfaces[2].IsMirror);
            Assert.Equal(2, File.ReadAllLines(output).Count(l => l.Trim() == "GLA N-BK7"));
            Assert.Equal(Power(sys), Power(back), 12);
        }
        finally { File.Delete(path); File.Delete(output); }
    }

    private static string Temp(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"optalix_{Guid.NewGuid():N}.otx");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ReadsWhatOptalixWrites()
    {
        string path = Temp(OptalixStyle);
        try
        {
            var sys = OptalixReader.Read(path);
            Assert.Equal(RayAimingMode.Real, sys.RayAiming);                       // RAIM 2: the real stop
            Assert.True(sys.Surfaces[1].ModelIndexEnabled);                         // GLA 613369
            Assert.Equal(1.613, sys.Surfaces[1].ModelNd, 12);
            Assert.Equal(36.9, sys.Surfaces[1].ModelVd, 12);
            Assert.Equal(SemiDiameterMode.Fixed, sys.Surfaces[1].SemiDiameterMode); // FH 1
            Assert.Equal(SemiDiameterMode.Auto, sys.Surfaces[2].SemiDiameterMode);  // no FH
            Assert.Equal("GE", sys.Surfaces[2].Material);

            Assert.Equal(6, sys.Surfaces.Count);                                    // the L pair is one lens
            Assert.Equal(SurfaceType.Paraxial, sys.Surfaces[3].Type);
            Assert.Equal(100.0, sys.Surfaces[3].FocalLength, 9);                   // LMOD is a power
            Assert.Equal(100.0, sys.Surfaces[3].Thickness, 12);
            Assert.True(sys.Surfaces[3].IsStop);
            Assert.True(sys.Surfaces[4].ModelIndexEnabled);                         // PRI
            Assert.Equal(1.336, sys.Surfaces[4].ModelNd, 12);

            Assert.True(OptalixReader.TryFictitiousGlass("6204.603", out double nd, out double vd));
            Assert.Equal(1.6204, nd, 12);
            Assert.Equal(60.3, vd, 12);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SavingInPlaceWritesEachValueIntoItsOwnSurface()
    {
        string path = Temp(OptalixStyle);
        string output = Path.ChangeExtension(Temp(""), ".otx");
        try
        {
            var sys = OptalixReader.Read(path);
            sys.Surfaces[1].Curvature = 0.011;   // file SUR 1
            sys.Surfaces[3].Thickness = 110.0;   // the ideal lens: file SUR 4's THI
            sys.Surfaces[4].Curvature = 0.003;   // file SUR 5, a number above it here

            LensPatcher.Save(sys, path, output);
            var lines = File.ReadAllLines(output).Select(l => l.Trim()).ToArray();
            string[] Block(int n)
            {
                int start = Array.FindIndex(lines, l => l.StartsWith("SUR") && l.EndsWith(" " + n));
                int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("SUR"));
                return lines[start..(end < 0 ? lines.Length : end)];
            }
            double Number(string[] block, string keyword) =>
                double.Parse(block.First(l => l.StartsWith(keyword + " ")).Split(' ', StringSplitOptions.RemoveEmptyEntries)[1],
                             System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(0.011, Number(Block(1), "CUY"), 12);
            Assert.Contains("GLA 613369", Block(1));      // the fictitious glass stands
            Assert.Contains("FH    1  1", Block(1));
            Assert.Contains("GLA 'GE'", Block(2));        // the private glass keeps its quotes
            Assert.Equal(0.0, Number(Block(3), "THI"), 12); // the gap between the principal planes is untouched
            Assert.Equal(110.0, Number(Block(4), "THI"), 12);
            Assert.Equal(0.0, Number(Block(4), "CUY"), 12);                // the lens module stays flat
            Assert.Equal(0.003, Number(Block(5), "CUY"), 12);
            Assert.Contains("PRI   1.336000000       1.336000000       1.336000000", Block(5));

            // A model glass the design changed is rewritten, in Optalix's code.
            sys.Surfaces[1].ModelNd = 1.62;
            LensPatcher.Save(sys, path, output);
            Assert.Contains("GLA 620.369", File.ReadAllLines(output).Select(l => l.Trim()));
        }
        finally { File.Delete(path); File.Delete(output); }
    }
}
