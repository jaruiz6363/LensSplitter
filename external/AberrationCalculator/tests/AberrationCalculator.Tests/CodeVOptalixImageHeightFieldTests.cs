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
/// Code V's image-height fields, YIM (paraxial) and YRI (real), were left out with a note nobody
/// saw, and the lens arrived with only an on-axis field. They are now converted to object fields
/// as OpticStudio's are. Optalix's image-height field types are not converted - which code is
/// paraxial and which real is not established - but are now said to be what they are.
/// </summary>
public class CodeVOptalixImageHeightFieldTests
{
    // A BK7 singlet behind a stop, as in ZmxImageHeightFieldTests: it distorts, so real and
    // paraxial differ. Written in inches when asked, to check the heights are scaled with the lens.
    private static string Singlet(string fields, bool inches)
    {
        double s = inches ? 1.0 / 25.4 : 1.0;
        string F(double v) => (v * s).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return $@"RDM;LEN
DIM {(inches ? "I" : "M")}
EPD {F(10)}
WL 587.5618
REF 1
{fields}
SO 0 1E+20 AIR
S 0 {F(20)} AIR
  STO
S {F(50)} {F(5)} NBK7
S 0 {F(93)} AIR
SI 0 0 AIR
GO
";
    }

    private static string Temp(string text, string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), "acimgh_" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, text, Encoding.UTF8);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CodeVRealImageHeightsAreReachedByTheAimedChiefRay(bool inches)
    {
        double s = inches ? 1.0 / 25.4 : 1.0;
        string yri = "YRI " + string.Join(" ", new[] { 0.0, 10.0, 20.0 }.Select(h => (h * s).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        string path = Temp(Singlet(yri, inches), ".seq");
        try
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = CodeVReader.Read(path, catalog);
            Assert.Equal(FieldType.ObjectAngle, sys.FieldType);
            Assert.Equal(3, sys.Fields.Count);

            var n = IndexResolver.Build(sys, catalog, 0.5875618);
            var paraxial = ParaxialTrace.Trace(sys, n, 1.0);
            var aimer = StopAimer.ForSystem(sys, n, paraxial);
            foreach (var (field, h) in sys.Fields.Zip(new[] { 0.0, 10.0, 20.0 }))
            {
                var (py, pz) = aimer.Launch(field.Y, 0.0, 0.0)!.Value;
                var hit = RealRayTrace.Trace(sys, n, paraxial, field.Y, py, pz, atParaxialFocus: false);
                Assert.True(hit.Ok);
                Assert.Equal(h, Math.Abs((double)hit.Y), 6);
            }
            Assert.Contains(sys.ImportWarnings, w => w.Contains("real image heights") && w.Contains("aimed"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CodeVParaxialImageHeightsAreReachedByTheParaxialChiefRay()
    {
        string path = Temp(Singlet("YIM 0 10 20", inches: false), ".seq");
        try
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = CodeVReader.Read(path, catalog);
            var n = IndexResolver.Build(sys, catalog, 0.5875618);
            Assert.Equal(20.0, Math.Abs((double)ParaxialTrace.Trace(sys, n, sys.Fields[2].Y).ParaxialImageHeight), 9);
            Assert.Contains(sys.ImportWarnings, w => w.Contains("paraxial image heights"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WhatCodeVLeavesOutIsAWarning()
    {
        string path = Temp(Singlet("YAN 0 5 10\nVUY 0 0.2 0.3", inches: false), ".seq");
        try
        {
            var sys = CodeVReader.Read(path, CatalogLocator.LoadBundled());
            Assert.Equal(10.0, sys.Fields[2].Y, 12);
            Assert.Contains(sys.ImportWarnings, w => w.Contains("vignetting factors"));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void OptalixImageHeightFieldsAreSaidToBeWhatTheyAre(int ftyp)
    {
        string path = Temp($@"VERS 11.82
EPD  10.0000
WL   0.5875618
WTW  100
REF    1
FTYP    {ftyp}
NFLD    2
FLD    1   0.000000000       0.000000000      100  1        0
FLD    2   0.000000000       10.000000000     100  1        0
SUR   0
SUT S
CUY 0.0000000000000
THI  0.1000000000E+21
SUR   1
SUT S
CUY  0.0200000000000
THI   5.000000000
GLA N-BK7
STO
SUR   2
SUT S
CUY 0.0000000000000
THI   93.00000000
SUR   3
SUT S
CUY 0.0000000000000
THI 0.000000000
", ".otx");
        try
        {
            var sys = OptalixReader.Read(path, CatalogLocator.LoadBundled());
            Assert.Contains(sys.ImportWarnings, w => w.Contains($"FTYP {ftyp}") && w.Contains("image heights"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OptalixAngleFieldsBringNoWarning()
    {
        string path = Temp(@"VERS 11.82
EPD  10.0000
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
CUY  0.0200000000000
THI   5.000000000
GLA N-BK7
STO
SUR   2
SUT S
CUY 0.0000000000000
THI   93.00000000
SUR   3
SUT S
CUY 0.0000000000000
THI 0.000000000
", ".otx");
        try { Assert.Empty(OptalixReader.Read(path, CatalogLocator.LoadBundled()).ImportWarnings); }
        finally { File.Delete(path); }
    }
}
