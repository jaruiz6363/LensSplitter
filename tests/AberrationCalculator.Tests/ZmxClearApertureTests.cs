using System;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The stop's CLAP sets the entrance pupil only when it is smaller than ENPD (a stock lens, whose
/// ENPD is the part's full diameter); a Ritchey-Chretien primary, CLAP 26 80 under ENPD 150, was
/// read with a 160 mm pupil. And MEMA, the part's mechanical size, is not the clear aperture: it
/// used to replace the CLAP on its surface, and went back out as the CLAP on export.
/// </summary>
public class ZmxClearApertureTests
{
    private static string Zmx(double enpd, double clap, double mema) => $@"VERS 190513 80 123457 L123457
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD {enpd.ToString(CultureInfo.InvariantCulture)}
FTYP 0 0 1 1 0 0 0
YFLN 0
WAVM 1 0.55 1
PWAV 1
SURF 0
  CURV 0
  DISZ INFINITY
SURF 1
  STOP
  CURV 0
  DISZ 10
  MEMA {mema.ToString(CultureInfo.InvariantCulture)} 0 0 0 1 """"
  CLAP 0 {clap.ToString(CultureInfo.InvariantCulture)} 0
SURF 2
  TYPE PARAXIAL
  PARM 1 100
  DISZ 100
SURF 3
  CURV 0
  DISZ 0
";

    private static string Temp(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), "acclap_" + Guid.NewGuid().ToString("N") + ".zmx");
        File.WriteAllText(path, text, Encoding.UTF8);
        return path;
    }

    [Theory]
    [InlineData(150.0, 80.0, 150.0)]
    [InlineData(25.4, 11.43, 22.86)]
    public void TheStopClapOnlyNarrowsThePupil(double enpd, double clap, double expected)
    {
        string path = Temp(Zmx(enpd, clap, clap + 1.0));
        try { Assert.Equal(expected, (double)ZmxReader.Read(path, new GlassCatalog()).Aperture.Value, 9); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MemaDoesNotReplaceTheClap()
    {
        string path = Temp(Zmx(10.0, 4.0, 6.0));
        string back = Path.Combine(Path.GetTempPath(), "acclap_" + Guid.NewGuid().ToString("N") + ".zmx");
        try
        {
            var sys = ZmxReader.Read(path, new GlassCatalog());
            Assert.Equal(4.0, (double)sys.Surfaces[1].ClapOuterRadius, 9);
            ZmxWriter.Write(sys, back);
            Assert.Equal(4.0, (double)ZmxReader.Read(back, new GlassCatalog()).Surfaces[1].ClapOuterRadius, 9);
        }
        finally { File.Delete(path); File.Delete(back); }
    }
}
