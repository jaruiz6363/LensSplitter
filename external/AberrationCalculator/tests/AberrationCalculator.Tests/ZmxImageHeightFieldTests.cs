using System;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// OpticStudio fields given as paraxial (FTYP 2) or real (FTYP 3) image height were read as object
/// angles with nothing said: a lens with 0-20 mm of image height was analysed at 0-20 degrees. They
/// are now converted to the object angles that reach them, and the conversion is a warning.
/// </summary>
public class ZmxImageHeightFieldTests
{
    // A paraboloid, f = 200, stop on the mirror: the chief ray leaves the vertex at the field
    // angle, so the image height is exactly 200 tan(angle), paraxial and real alike.
    private static string Paraboloid(int ftyp) => $@"VERS 190513 80 123457 L123457
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD 50
FTYP {ftyp} 0 3 1 0 0 0
XFLN 0 0 0
YFLN 0 5 10
WAVM 1 0.55 1
PWAV 1
SURF 0
  CURV 0
  DISZ INFINITY
SURF 1
  STOP
  CURV -0.0025
  CONI -1
  DISZ -200
  GLAS MIRROR 0 0 0 0 0
SURF 2
  CURV 0
  DISZ 0
";

    // A BK7 singlet behind a stop: it distorts, so the real and paraxial answers differ.
    private static string Singlet(int ftyp, int aiming) => $@"VERS 190513 80 123457 L123457
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD 10
FTYP {ftyp} 0 3 1 0 0 0
XFLN 0 0 0
YFLN 0 10 20
WAVM 1 0.5875618 1
PWAV 1
RAIM 0 {aiming} 1 1 0 0 0 0 0 1
GCAT SCHOTT
SURF 0
  CURV 0
  DISZ INFINITY
SURF 1
  STOP
  CURV 0
  DISZ 20
SURF 2
  CURV 0.02
  DISZ 5
  GLAS N-BK7 0 0 1.5168 64.17 0 0 0 0 0 0
SURF 3
  CURV 0
  DISZ 93
SURF 4
  CURV 0
  DISZ 0
";

    private static string Temp(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), "acfield_" + Guid.NewGuid().ToString("N") + ".zmx");
        File.WriteAllText(path, text, Encoding.UTF8);
        return path;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ImageHeightsBecomeTheObjectAnglesThatReachThem(int ftyp)
    {
        string path = Temp(Paraboloid(ftyp));
        try
        {
            var sys = ZmxReader.Read(path, CatalogLocator.LoadBundled());

            Assert.Equal(FieldType.ObjectAngle, sys.FieldType);
            Assert.Equal(3, sys.Fields.Count);
            Assert.Equal(0.0, sys.Fields[0].Y, 12);
            Assert.Equal(Math.Atan(5.0 / 200.0) * 180.0 / Math.PI, sys.Fields[1].Y, 6);
            Assert.Equal(Math.Atan(10.0 / 200.0) * 180.0 / Math.PI, sys.Fields[2].Y, 6);
            Assert.Contains(sys.ImportWarnings, w => w.Contains("image heights") && w.Contains("converted"));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RealImageHeightsAreReachedByTheRealChiefRay(int aiming)
    {
        string path = Temp(Singlet(3, aiming));
        try
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = ZmxReader.Read(path, catalog);
            var n = IndexResolver.Build(sys, catalog, 0.5875618);
            var paraxial = ParaxialTrace.Trace(sys, n, 1.0);

            foreach (var (field, h) in sys.Fields.Zip(new[] { 0.0, 10.0, 20.0 }))
            {
                var hit = RealRayTrace.Trace(sys, n, paraxial, field.Y, 0.0, 0.0, atParaxialFocus: false);
                Assert.True(hit.Ok);
                Assert.Equal(h, Math.Abs((double)hit.Y), 6);
            }

            // The lens distorts, so the paraxial answer for 20 mm would have been a different angle.
            double paraxialAngle = Math.Atan(20.0 / Math.Abs((double)paraxial.ParaxialImageHeight)
                                             * Math.Tan(Math.PI / 180.0)) * 180.0 / Math.PI;
            Assert.True(Math.Abs(sys.Fields[2].Y - paraxialAngle) > 1e-3);
            Assert.Contains(sys.ImportWarnings, w => w.Contains("real image heights"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParaxialImageHeightsAreReachedByTheParaxialChiefRay()
    {
        string path = Temp(Singlet(2, 0));
        try
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = ZmxReader.Read(path, catalog);
            var n = IndexResolver.Build(sys, catalog, 0.5875618);
            Assert.Equal(20.0, Math.Abs((double)ParaxialTrace.Trace(sys, n, sys.Fields[2].Y).ParaxialImageHeight), 9);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WithNoGlassTheFieldsAreKeptAndTheReaderSaysSo()
    {
        string path = Temp(Paraboloid(3));
        try
        {
            var sys = ZmxReader.Read(path, null);
            Assert.Equal(10.0, sys.Fields[2].Y, 12);
            Assert.Contains(sys.ImportWarnings, w => w.Contains("could not be converted"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AngleFieldsAreLeftAlone()
    {
        string path = Temp(Paraboloid(0));
        try
        {
            var sys = ZmxReader.Read(path, CatalogLocator.LoadBundled());
            Assert.Equal(FieldType.ObjectAngle, sys.FieldType);
            Assert.Equal(5.0, sys.Fields[1].Y, 12);
            Assert.Empty(sys.ImportWarnings);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TheReportCarriesTheWarning()
    {
        string path = Temp(Paraboloid(3));
        try
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = ZmxReader.Read(path, catalog);
            string report = new AberrationCalculator.Core.Report.ReportWriter(sys, catalog, path).BuildReport();
            Assert.Contains("real image heights", report);
        }
        finally { File.Delete(path); }
    }
}
