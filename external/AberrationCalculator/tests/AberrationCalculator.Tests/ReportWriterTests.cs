using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The report is a formatter over everything else, and a formatter fails quietly: a section
/// that stops being emitted leaves a shorter report rather than an error. These check that
/// a loaded lens actually reaches every part of the output, and that the TSVs carry enough
/// precision to be re-read without loss.
/// </summary>
public class ReportWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "abcalc-rep-" + Guid.NewGuid().ToString("N"));

    public ReportWriterTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>A doublet with three wavelengths and an aspheric back, so every section has content.</summary>
    private string WriteLens(string glass = "N-BK7")
    {
        var z = new StringBuilder();
        z.AppendLine("TITL Report Fixture");
        z.AppendLine("ENPD 20");
        z.AppendLine("WAVM 1 0.4861 1.0");
        z.AppendLine("WAVM 2 0.5876 1.0");
        z.AppendLine("WAVM 3 0.6563 1.0");
        z.AppendLine("PWAV 2");
        // A field, or every field-dependent coefficient is zero and the sections that name them
        // are empty. (Without one they used to come out NaN, and still had rows.)
        z.AppendLine("FTYP 0 0 2");
        z.AppendLine("YFLN 0 5");
        z.AppendLine("SURF 0");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ INFINITY");
        z.AppendLine("SURF 1");
        z.AppendLine("  STOP");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0.02");
        z.AppendLine("  DISZ 6");
        z.AppendLine($"  GLAS {glass}");
        z.AppendLine("SURF 2");
        z.AppendLine("  TYPE EVENASPH");
        z.AppendLine("  CURV -0.005");
        z.AppendLine("  CONI -0.4");
        z.AppendLine("  PARM 2 -6.0e-07");
        z.AppendLine("  DISZ 90");
        z.AppendLine("SURF 3");
        z.AppendLine("  TYPE STANDARD");
        z.AppendLine("  CURV 0");
        z.AppendLine("  DISZ 0");

        string path = Path.Combine(_dir, "fixture.zmx");
        File.WriteAllText(path, z.ToString());
        return path;
    }

    private ReportWriter Writer(string path)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        return new ReportWriter(sys, catalog, path);
    }

    /// <summary>Every section must appear, and with numbers rather than empty headings.</summary>
    [Fact]
    public void TheReportContainsEverySection()
    {
        string report = Writer(WriteLens()).BuildReport();

        foreach (var heading in new[]
                 {
                     "PRESCRIPTION",
                     "REFRACTIVE INDICES",
                     "ASPHERIC SURFACES",
                     "FIRST ORDER",
                     "SEIDEL (THIRD ORDER) COEFFICIENTS",
                     "BUCHDAHL / RIMMER COEFFICIENTS",
                     "PREDICTED RMS SPOT RADIUS (PRMS)",
                     "WHICH ABERRATION IS COSTING YOU THE SPOT",
                     "WHICH SURFACE IS COSTING YOU THE SPOT",
                 })
            Assert.Contains(heading, report);

        Assert.Contains("Effective focal length", report);
        Assert.Contains("PRMSA", report);
        Assert.Contains("N-BK7", report);
    }

    /// <summary>
    /// The PRMS table's weakest number is its full-field row, because seventh order
    /// carries spherical aberration alone and has no field terms. A reader who takes the
    /// Hy = 1 row at face value is misled, so the caveat has to travel with the numbers.
    /// </summary>
    [Fact]
    public void ThePrmsTableCarriesItsFieldAccuracyCaveat()
    {
        string report = Writer(WriteLens()).BuildReport();

        Assert.Contains("ACCURACY FALLS OFF WITH FIELD", report);
        Assert.Contains("B7", report);

        // It must sit with the PRMS numbers, not orphaned in some later section.
        Assert.InRange(report.IndexOf("ACCURACY FALLS OFF WITH FIELD", StringComparison.Ordinal),
                       report.IndexOf("PREDICTED RMS SPOT RADIUS", StringComparison.Ordinal),
                       report.IndexOf("WHICH ABERRATION", StringComparison.Ordinal));
    }

    /// <summary>
    /// The coefficient labels are Rimmer's and say nothing about what the aberration is.
    /// The published names have to travel with them, and a name that is really a
    /// combination must say so - claiming M1 alone is oblique spherical would be wrong.
    /// </summary>
    [Fact]
    public void TheContributionTableNamesTheAberrations()
    {
        string report = Writer(WriteLens()).BuildReport();

        Assert.Contains("3rd spherical", report);
        Assert.Contains("Petzval", report);
        Assert.Contains("oblique spherical", report);       // Buchdahl's problem term
        Assert.Contains("Johnson, Appl. Opt. 12, 2079 (1973)", report);

        // A combination must be flagged as one, not presented as the whole aberration.
        Assert.Contains("oblique spherical, tangential (with", report);
        Assert.Contains("oblique spherical, sagittal", report);   // M2 alone genuinely is this

        // And the names must reach the machine-readable form too.
        var tsv = Writer(WriteLens()).BuildContributionTsv();
        Assert.Contains("order", tsv.Split('\n')[0]);
        Assert.Contains("5th elliptical coma", tsv);
    }

    /// <summary>
    /// A glass no catalog knows must be named. Treating it as air yields a full set of
    /// confident, wrong numbers, which is the one failure mode a calculator must not have.
    /// </summary>
    [Fact]
    public void AnUnresolvedGlassIsReportedRatherThanTreatedAsAir()
    {
        var w = Writer(WriteLens("NOSUCHGLASS"));

        Assert.Contains("NOSUCHGLASS", w.Unresolved);
        Assert.Contains("No index for", w.BuildReport());
    }

    /// <summary>A recognised glass leaves nothing unresolved.</summary>
    [Fact]
    public void AKnownGlassLeavesNothingUnresolved()
    {
        Assert.Empty(Writer(WriteLens()).Unresolved);
    }

    /// <summary>
    /// The TSVs exist so a script can re-read the numbers, which only works if they carry
    /// full precision and one row per surface. A display-rounded file would silently lose
    /// digits that the report itself never had.
    /// </summary>
    [Fact]
    public void TheTsvFilesAreParseableAndCarryFullPrecision()
    {
        var w = Writer(WriteLens());

        foreach (var (name, tsv) in new[]
                 {
                     ("prescription", w.BuildPrescriptionTsv()),
                     ("indices", w.BuildIndicesTsv()),
                     ("firstorder", w.BuildFirstOrderTsv()),
                     ("paraxial", w.BuildParaxialRaysTsv()),
                     ("seidel", w.BuildSeidelTsv()),
                     ("buchdahl", w.BuildBuchdahlTsv()),
                     ("prms", w.BuildPrmsTsv()),
                     ("contributions", w.BuildContributionTsv()),
                     ("surfaces", w.BuildSurfaceBreakdownTsv()),
                     ("surface-share", w.BuildSurfaceShareTsv()),
                 })
        {
            // Trim the carriage return before testing for emptiness: these files are written
            // with CRLF, so a blank separator line splits to "\r", which RemoveEmptyEntries
            // does not consider empty and which would otherwise read as a one-column row.
            var lines = tsv.Split('\n')
                           .Select(l => l.TrimEnd('\r'))
                           .Where(l => l.Length > 0 && !l.StartsWith("#"))   // "#" = explanation
                           .ToArray();
            Assert.True(lines.Length >= 2, $"{name}.tsv has no data rows");
            Assert.Contains("\t", lines[0]);

            int columns = lines[0].Split('\t').Length;
            foreach (var line in lines.Skip(1))
                Assert.Equal(columns, line.Split('\t').Length);
        }

        // Round-trip precision: an index written to the TSV must re-read to full accuracy.
        var pres = w.BuildPrescriptionTsv().Split('\n')[2].TrimEnd('\r').Split('\t');
        double index = double.Parse(pres[6], CultureInfo.InvariantCulture);
        Assert.True(index > 1.5 && index < 1.6, $"expected an N-BK7 index, got {index}");
        Assert.True(pres[6].Length > 8, $"index written with too few digits to re-read: '{pres[6]}'");
    }

    /// <summary>A file that is not a lens is an error a caller can report, not a crash.</summary>
    [Fact]
    public void ANonLensFileRaisesAClearError()
    {
        string path = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(path, "this is not a lens");

        var ex = Assert.Throws<NotSupportedException>(() => LensFile.Read(path));
        Assert.Contains(".txt", ex.Message);
    }
}
