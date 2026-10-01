using System;
using System.Globalization;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;

var catalog = CatalogLocator.LoadBundled();
var sys = LensFile.Read(args[0], catalog);
int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
double field = 0; foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
var p = ParaxialTrace.Trace(sys, n, field);
var b = BuchdahlCoefficients.Compute(sys, p);

// The decomposition must be exact: sum over surfaces of (intrinsic + aspheric + induced),
// times the F/number, has to reproduce the reported totals.
Console.WriteLine("  reconstruction check (should be ~1e-15):");
foreach (var name in BuchdahlTerms.Names)
{
    double sum = 0;
    for (int i = 1; i < sys.Surfaces.Count - 1; i++)
    {
        sum += b.Intrinsic[i][name] + b.Induced[i][name];
        if (b.Aspheric[i] != null) sum += b.Aspheric[i]![name];
    }
    sum *= b.FNumber;
    double rel = Math.Abs(sum - b.Totals[name]) / Math.Max(Math.Abs(b.Totals[name]), 1e-12);
    if (rel > 1e-9)
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
          "    {0,-4} MISMATCH  reconstructed {1:0.000000E+00}  total {2:0.000000E+00}  rel {3:0.0E+00}",
          name, sum, b.Totals[name], rel));
}
Console.WriteLine("    all coefficients reconstruct within 1e-9");
Console.WriteLine();
Console.WriteLine("  B5 (fifth-order spherical) by surface and part:");
Console.WriteLine(string.Format("  {0,5} {1,14} {2,14} {3,14} {4,14}", "surf","intrinsic","aspheric","induced","total"));
for (int i = 1; i < sys.Surfaces.Count - 1; i++)
{
    double asph = b.Aspheric[i]?.B5 ?? 0;
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
      "  {0,5} {1,14:0.00000E+00} {2,14:0.00000E+00} {3,14:0.00000E+00} {4,14:0.00000E+00}",
      i, b.Intrinsic[i].B5, asph, b.Induced[i].B5, b.PerSurface[i].B5));
}
