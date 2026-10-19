using System;
using System.IO;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The note in the folder of glasses beside an Optiland lens says where the glasses were installed, or that they
/// were not. It used to say they were installed even when the writer had been told not to install them.
/// </summary>
public class OptilandGlassReadMeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acreadme_" + Guid.NewGuid().ToString("N"));

    public OptilandGlassReadMeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void InstalledGlassesAreSaidToBeInstalledWhereTheyAre()
    {
        string note = OptilandGlass.ReadMe("lens.optiland.json", new[] { "a-catalog" }, @"C:\Users\someone\.optiland\catalogs");
        Assert.Contains("They were installed for Optiland", note);
        Assert.Contains(@"C:\Users\someone\.optiland\catalogs; Optiland loads them when it starts", note);
        Assert.DoesNotContain("not installed", note);
    }

    [Fact]
    public void GlassesNotInstalledAreSaidNotToBe()
    {
        string note = OptilandGlass.ReadMe("lens.optiland.json", new[] { "a-catalog" }, null);
        Assert.Contains("They were not installed.", note);
        Assert.DoesNotContain("They were installed", note);
        Assert.Contains("load_catalog", note);   // the way to load them in a session is still given
    }

    /// <summary>A lens written without installing its glasses: the note beside it says they were not installed.</summary>
    [Fact]
    public void TheWriterSaysWhatItDid()
    {
        File.WriteAllText(Path.Combine(_dir, "SCHOTT.AGF"),
            "NM N-BK7 2 517642 1.5168 64.17 0 1 0\r\n" +
            "CD 1.03961212 0.00600069867 0.231792344 0.0200179144 1.01046945 103.560653 0 0 0 0\r\n" +
            "LD 0.3 2.5\r\n");
        var glass = new GlassCatalog();
        glass.LoadFile(Path.Combine(_dir, "SCHOTT.AGF"));

        var system = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        system.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        system.Fields.Add(new Field(0.0));
        system.GlassCatalogs.Add("SCHOTT");
        system.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        system.Surfaces.Add(new Surface { Index = 1, Curvature = 0.02, Thickness = 4.0, Material = "N-BK7", IsStop = true, SemiDiameter = 6.0 });
        system.Surfaces.Add(new Surface { Index = 2, Curvature = -0.02, Thickness = 45.0, SemiDiameter = 6.0 });
        system.Surfaces.Add(new Surface { Index = 3 });

        string path = Path.Combine(_dir, "singlet.optiland.json");
        var result = OptilandWriter.Write(system, path, glass, install: false);
        Assert.Null(result.InstalledTo);
        Assert.NotNull(result.GlassFolder);
        string note = File.ReadAllText(Path.Combine(result.GlassFolder!, "README.txt"));
        Assert.Contains("They were not installed.", note);
        Assert.DoesNotContain("They were installed", note);
    }
}
