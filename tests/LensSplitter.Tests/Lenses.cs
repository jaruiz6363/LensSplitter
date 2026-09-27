using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;

namespace LensSplitter.Tests;

/// <summary>The lenses the tests split.</summary>
internal static class Lenses
{
    private static readonly Lazy<GlassCatalog> _catalog = new(CatalogLocator.LoadBundled);
    public static GlassCatalog Catalog => _catalog.Value;

    public static string Path(string relative) => System.IO.Path.Combine(AppContext.BaseDirectory, relative);

    public static OpticalSystem Read(string relative) => LensFile.Read(Path(relative), Catalog);

    /// <summary>A Cooke triplet at a finite object, LensSplitter's own test lens.</summary>
    public static OpticalSystem Cooke() => Read("TestData/Cooke_40deg_FC.zmx");

    /// <summary>A singlet whose front face has a conic and r^4, r^6, r^8 terms.</summary>
    public static OpticalSystem FiguredSinglet() => Read("reference/F3_conic_a4_a6_a8.zmx");

    /// <summary>A singlet at a finite object whose front face has an r^2 term as well.</summary>
    public static OpticalSystem R2Singlet() => Read("reference/G1_finite_r2.zmx");

    /// <summary>A cemented doublet in air, built here: its elements are corrected as a unit and must not be split.</summary>
    public static OpticalSystem CementedDoublet()
    {
        var s = new OpticalSystem { Title = "cemented doublet", Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(3.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1 / 61.47, Thickness = 6.0, Material = "N-BK7", IsStop = true });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1 / 44.64, Thickness = 2.5, Material = "N-SF5" });
        s.Surfaces.Add(new Surface { Index = 3, Curvature = -1 / 129.94, Thickness = 97.0 });
        s.Surfaces.Add(new Surface { Index = 4 });
        return s;
    }

    /// <summary>Axial position of every surface from surface 1.</summary>
    public static double[] Positions(OpticalSystem s)
    {
        var z = new double[s.Surfaces.Count];
        for (int i = 2; i < s.Surfaces.Count; i++) z[i] = z[i - 1] + s.Surfaces[i - 1].Thickness;
        return z;
    }
}
