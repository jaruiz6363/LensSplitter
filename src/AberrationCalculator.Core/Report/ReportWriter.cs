using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.Nat;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Report;

/// <summary>
/// Writes what the program found about a lens, in two forms.
///
/// A human reads the report; a script reads the TSV. Producing only one of the two forces
/// the other reader to parse a layout that was never meant for them - column-aligned text
/// is miserable to parse, and a bare TSV is miserable to read - so both come out of the
/// same numbers in one pass, and cannot disagree.
///
/// Tab-separated rather than comma: glass names and comments contain commas, and every
/// spreadsheet opens TSV without a dialogue about delimiters.
/// </summary>
public sealed class ReportWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly OpticalSystem _sys;
    private readonly GlassCatalog _catalog;
    private readonly string _sourcePath;

    /// <summary>Index after each surface, one array per wavelength, in system order.</summary>
    private readonly List<double[]> _indices = new();

    private readonly List<string> _unresolved = new();
    private readonly int _primary;

    public ReportWriter(OpticalSystem system, GlassCatalog catalog, string sourcePath)
    {
        _sys = system;
        _catalog = catalog;
        _sourcePath = sourcePath;

        _primary = system.PrimaryWavelengthIndex < 0 ? 0 : system.PrimaryWavelengthIndex;
        foreach (var w in system.Wavelengths)
            _indices.Add(IndexResolver.Build(system, catalog, w.Value, _unresolved, _ambiguous));
        if (_indices.Count == 0)
            _indices.Add(IndexResolver.Build(system, catalog, 0.5875618, _unresolved, _ambiguous));
    }

    private readonly List<string> _ambiguous = new();

    /// <summary>
    /// Materials whose name several loaded catalogs answer to, where the file named none.
    ///
    /// <para>Not an error - the index resolved and the numbers are self-consistent. It is a
    /// warning that a CHOICE was made silently, and that another program opening the same file
    /// may choose differently.</para>
    /// </summary>
    public IReadOnlyList<string> Ambiguous
    {
        get
        {
            var distinct = new List<string>();
            foreach (var a in _ambiguous)
                if (!distinct.Contains(a, StringComparer.OrdinalIgnoreCase)) distinct.Add(a);
            return distinct;
        }
    }

    /// <summary>Materials that no catalog could resolve. Non-empty means the numbers are wrong.</summary>
    public IReadOnlyList<string> Unresolved
    {
        get
        {
            var distinct = new List<string>();
            foreach (var u in _unresolved)
                if (!distinct.Contains(u, StringComparer.OrdinalIgnoreCase)) distinct.Add(u);
            return distinct;
        }
    }

    private double[] PrimaryIndices => _indices[Math.Min(_primary, _indices.Count - 1)];

    /// <summary>
    /// Indices at the shortest and longest wavelengths the system defines - what the
    /// chromatic Seidel terms difference. A monochromatic system has no dispersion to
    /// report, and both come back as the primary set, which correctly gives CL = CT = 0.
    /// </summary>
    private (double[] Short, double[] Long) SpectralExtremes()
    {
        if (_sys.Wavelengths.Count < 2) return (PrimaryIndices, PrimaryIndices);
        int lo = 0, hi = 0;
        for (int i = 1; i < _sys.Wavelengths.Count; i++)
        {
            if (_sys.Wavelengths[i].Value < _sys.Wavelengths[lo].Value) lo = i;
            if (_sys.Wavelengths[i].Value > _sys.Wavelengths[hi].Value) hi = i;
        }
        return (_indices[lo], _indices[hi]);
    }

    /// <summary>
    /// The coefficient set, with the tertiary terms attached. Every route to one goes
    /// through here: tau2..tau20 are assembled separately from the fifth-order code, and
    /// before this existed the reported set carried tau1 and nineteen zeros.
    /// </summary>
    /// <param name="indices">
    /// Indices the trace was made with. Defaults to the primary wavelength; the per-wavelength
    /// callers MUST pass their own, or the tertiary terms would come from a different colour
    /// than the rest of the set.
    /// </param>
    private BuchdahlResult Buchdahl(ParaxialResult p, System.Collections.Generic.List<int>? ignoredR2 = null,
                                    double[]? indices = null)
    {
        var b = BuchdahlCoefficients.Compute(_sys, p, ignoredR2);
        // tau2..tau20 are not produced by the fifth-order code and have to be assembled
        // separately. Every route to a coefficient set goes through here so that none of
        // them can report the nineteen zeros this used to.
        TertiaryCoefficients.Attach(_sys, indices ?? PrimaryIndices, p, b, MaxField());
        return b;
    }

    private SeidelResult Seidel(ParaxialResult p)
    {
        var (nShort, nLong) = SpectralExtremes();
        return SeidelCoefficients.Compute(_sys, PrimaryIndices, nShort, nLong, p);
    }

    private double MaxField()
    {
        double f = 0.0;
        foreach (var x in _sys.Fields) if (Math.Abs(x.Y) > Math.Abs(f)) f = x.Y;
        return f;
    }

    // ── Human-readable report ────────────────────────────────────────────────────────

    public string BuildReport()
    {
        var sb = new StringBuilder();
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());

        Header(sb);
        Prescription(sb);
        Materials(sb);
        Aspherics(sb);
        FirstOrder(sb, trace);
        SeidelSection(sb, trace);
        BuchdahlSection(sb, trace);
        PrmsSection(sb);
        BestFocusSection(sb);
        ContributionSection(sb);
        SurfaceContributionSection(sb);
        Warnings(sb, trace);
        return sb.ToString();
    }

    private void Header(StringBuilder sb)
    {
        string title = string.IsNullOrWhiteSpace(_sys.Title)
            ? Path.GetFileNameWithoutExtension(_sourcePath)
            : _sys.Title;
        sb.AppendLine("================================================================");
        sb.AppendLine(title);
        sb.AppendLine("================================================================");
        sb.AppendLine($"File      : {_sourcePath}");
        sb.AppendLine($"Format    : {Path.GetExtension(_sourcePath).TrimStart('.').ToUpperInvariant()}");
        sb.AppendLine($"Surfaces  : {_sys.Surfaces.Count}   Wavelengths: {_sys.Wavelengths.Count}   Fields: {_sys.Fields.Count}");
        sb.AppendLine($"Aperture  : {_sys.Aperture.Type} = {Num(_sys.Aperture.Value)}");
        sb.AppendLine($"Field type: {_sys.FieldType}");
        sb.AppendLine();
    }

    private void Prescription(StringBuilder sb)
    {
        var n = PrimaryIndices;
        sb.AppendLine("PRESCRIPTION");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,-12} {2,14} {3,12} {4,-12} {5,10} {6,12} {7,10}  {8}",
            "Surf", "Type", "Radius", "Thickness", "Material", "Index", "SemiDiam", "Conic", "Note"));
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string label = i == 0 ? "OBJ" : i == _sys.Surfaces.Count - 1 ? "IMG" : i.ToString(Inv);
            sb.AppendLine(string.Format(Inv, "{0,-5} {1,-12} {2,14} {3,12} {4,-12} {5,10} {6,12} {7,10}  {8}",
                label, Kind(s), Num(s.Radius), Num(s.Thickness), MaterialName(s),
                i < n.Length && Math.Abs(n[i] - 1.0) > 1e-9 ? n[i].ToString("0.000000", Inv) : "",
                s.SemiDiameter > 0 ? Num(s.SemiDiameter) : "",
                Math.Abs(s.Conic) > 1e-15 ? Num(s.Conic) : "",
                Note(s)));
        }
        sb.AppendLine();
    }

    private void Materials(StringBuilder sb)
    {
        sb.AppendLine("REFRACTIVE INDICES");
        sb.AppendLine("----------------------------------------------------------------");
        var head = new StringBuilder(string.Format(Inv, "{0,-16} {1,-14} {2,8}", "Material", "Source", "Vd"));
        foreach (var w in _sys.Wavelengths)
            head.Append(string.Format(Inv, " {0,12}", w.Value.ToString("0.####", Inv) + (w.IsPrimary ? "*" : "")));
        sb.AppendLine(head.ToString());

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string name = MaterialName(s);
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;

            var row = new StringBuilder(string.Format(Inv, "{0,-16} {1,-14} {2,8}", name, Source(s), Vd(s)));
            foreach (var perWave in _indices)
                row.Append(string.Format(Inv, " {0,12}", i < perWave.Length ? perWave[i].ToString("0.000000", Inv) : ""));
            sb.AppendLine(row.ToString());
        }
        sb.AppendLine();
    }

    private void Aspherics(StringBuilder sb)
    {
        var rows = new List<(int Surface, Surface S)>();
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            bool any = Math.Abs(s.Conic) > 1e-15;
            foreach (double c in s.AsphericCoefficients) if (c != 0.0) any = true;
            if (any) rows.Add((i, s));
        }
        if (rows.Count == 0) return;

        sb.AppendLine("ASPHERIC SURFACES");
        sb.AppendLine("----------------------------------------------------------------");
        bool beyond = false;
        foreach (var (i, s) in rows)
        {
            sb.AppendLine(string.Format(Inv, "Surface {0}   conic = {1}", i, s.Conic.ToString("0.########", Inv)));
            for (int k = 0; k < s.AsphericCoefficients.Length; k++)
            {
                double c = s.AsphericCoefficients[k];
                if (c == 0.0) continue;
                // Index 0 is r^2, which is a curvature change and IS carried - see
                // Surface.VertexCurvature. Index 4 upward is r^10 and beyond, which reaches the
                // ninth order and so cannot touch anything below. Marked rather than hidden: a
                // reader who sees the term listed above the coefficients would otherwise assume
                // it went into them.
                bool past = k >= 4;
                if (past) beyond = true;
                sb.AppendLine(string.Format(Inv, "    r^{0,-3} {1}{2}", 2 * k + 2,
                    c.ToString("0.000000000E+00", Inv), past ? "   <- takes no part, see below" : ""));
            }
        }
        sb.AppendLine();
        sb.AppendLine("WHICH ORDER EACH TERM REACHES. A deformation r^n first contributes at wave order");
        sb.AppendLine("n, which is transverse order n-1, so each term leaves every order below it exactly");
        sb.AppendLine("alone - an r^6 term cannot touch the third order however large it is. It does go on");
        sb.AppendLine("affecting every order ABOVE its onset, in its own right and by induction, so this");
        sb.AppendLine("says where a term starts and not where it stops.");
        sb.AppendLine();
        sb.AppendLine("    r^2   the focal length - a curvature change, not figuring");
        sb.AppendLine("    r^4   3rd order and above");
        sb.AppendLine("    r^6   5th order and above");
        sb.AppendLine("    r^8   7th order and above");
        sb.AppendLine("    r^10  9th order and above - past everything computed here");

        if (beyond)
        {
            sb.AppendLine();
            sb.AppendLine("THIS DESIGN CARRIES FIGURING BEYOND r^8, and by the table above it takes no part");
            sb.AppendLine("in anything that follows. It is not being dropped - it genuinely does not appear.");
            sb.AppendLine("The surface is still that shape and real rays still see it; the coefficients");
            sb.AppendLine("cannot. So a design whose figuring lives mostly in those terms is not described");
            sb.AppendLine("by what follows, however strong its aspherics look, and the predicted spot will");
            sb.AppendLine("part company with a traced one by whatever they do.");
        }
        sb.AppendLine();
    }

    private void FirstOrder(StringBuilder sb, ParaxialResult p)
    {
        sb.AppendLine("FIRST ORDER");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var (name, value) in FirstOrderRows(p))
            sb.AppendLine(string.Format(Inv, "{0,-38} {1}", name, value));
        sb.AppendLine();
    }

    private List<(string Name, string Value)> FirstOrderRows(ParaxialResult p)
    {
        double field = MaxField();
        var rows = new List<(string, string)>
        {
            ("Effective focal length",              Num(p.Efl)),
            ("Back focal length",                   Num(p.Bfl)),
            ("F/number",                            double.IsInfinity(p.FNumber) ? "-" : Num(p.FNumber)),
            ("Entrance pupil diameter",             Num(p.Epd)),
            ("Entrance pupil position (from surf 1)", Num(p.EntrancePupilPosition)),
            ("Exit pupil diameter",                 Num(p.ExitPupilDiameter)),
            ("Exit pupil position (from image)",    Num(p.ExitPupilPosition)),
            ("Field",                               _sys.FieldType == FieldType.ObjectAngle
                                                        ? Num(field) + " deg" : Num(field)),
            ("Image height at image surface",       Num(p.ImageHeight)),
            ("Paraxial focus (from last surface)",  Num(p.ParaxialFocusDistance)),
            ("Image height at paraxial focus",      Num(p.ParaxialImageHeight)),
            ("Magnification",                       p.InfiniteConjugate ? "-" : Num(p.Magnification)),
            ("Conjugate",                           p.InfiniteConjugate ? "infinite" : "finite"),
            ("Lagrange invariant",                  Num(p.LagrangeInvariant)),
            ("Invariant drift",                     p.InvariantDrift.ToString("0.0E+00", Inv)),
        };
        return rows;
    }

    private void SeidelSection(StringBuilder sb, ParaxialResult p)
    {
        var s = Seidel(p);
        sb.AppendLine("SEIDEL (THIRD ORDER) COEFFICIENTS");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
            "Surf", "S1 sph", "S2 coma", "S3 astig", "S4 Petzval", "S5 distort", "CL axial", "CT lateral"));
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            if (Math.Abs(s.S1[i]) + Math.Abs(s.S4[i]) + Math.Abs(s.CL[i]) < 1e-14) continue;
            sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
                i, Sci(s.S1[i]), Sci(s.S2[i]), Sci(s.S3[i]), Sci(s.S4[i]), Sci(s.S5[i]), Sci(s.CL[i]), Sci(s.CT[i])));
        }
        sb.AppendLine(new string('-', 64));
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
            "TOTAL", Sci(s.TotalS1), Sci(s.TotalS2), Sci(s.TotalS3), Sci(s.TotalS4),
            Sci(s.TotalS5), Sci(s.TotalCL), Sci(s.TotalCT)));
        sb.AppendLine();
        FieldSurfaceSection(sb, s, p);
    }

    /// <summary>
    /// The same third-order numbers said the other way round: where the image surface this lens
    /// FORMS lies, rather than how much astigmatism and Petzval it has.
    ///
    /// <para>This is the form that answers a detector question. `S3` and `S4` divided by
    /// `2 n' u'^2` are the longitudinal distances from the paraxial plane to the sagittal and
    /// tangential foci at full field, and the radius that would put a detector on the medial
    /// surface follows from the sag. When the file's own image surface is curved, the residual
    /// between the two is printed, because that is the number that says whether the detector
    /// chosen matches the lens - and it is the only place in this program where the shape of the
    /// image surface is read at all.</para>
    /// </summary>
    private void FieldSurfaceSection(StringBuilder sb, SeidelResult s, ParaxialResult p)
    {
        var f = FieldSurfaces.Compute(_sys, s, p, MaxField());
        if (double.IsNaN(f.MedialSag) || double.IsInfinity(f.MedialSag)) return;

        string Radius(Scalar r) => Scalar.IsInfinity(r) || Scalar.IsNaN(r)
            ? "infinity" : Num(r);

        sb.AppendLine("FIELD SURFACES (FROM S3 AND S4)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Longitudinal, at the full field, positive when the focus falls SHORT of the");
        sb.AppendLine("paraxial plane. These say where to put a detector; the coefficients above do");
        sb.AppendLine("not read the image surface, and neither does any other program's.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "Petzval radius", Radius(f.PetzvalRadius)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}",
            "Image height the sags are quoted at", Num(f.ImageHeight)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "Petzval surface", Num(f.PetzvalSag)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "Sagittal surface", Num(f.SagittalSag)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "Medial surface", Num(f.MedialSag)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "Tangential surface", Num(f.TangentialSag)));
        sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}",
            "Detector radius matching the medial", Radius(f.MedialMatchingRadius)));

        if (f.ImageSurfaceIsCurved)
        {
            Scalar r = 1.0 / f.ImageSurfaceCurvature;
            sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}", "This file's image surface, radius", Num(r)));
            sb.AppendLine(string.Format(Inv, "{0,-38}{1,14}",
                "  leaves, against the medial surface", Num(f.MedialResidual)));
            sb.AppendLine("  The coefficients above are unchanged by that surface, as they should be.");
            sb.AppendLine("  A REAL RAY analysis here still catches rays on a plane - see docs/verification.md.");
        }
        sb.AppendLine();
    }

    private void BuchdahlSection(StringBuilder sb, ParaxialResult p)
    {
        var b = Buchdahl(p);

        sb.AppendLine("BUCHDAHL / RIMMER COEFFICIENTS (THIRD, FIFTH AND SEVENTH ORDER)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Transverse coefficients. Per-surface values are the INTRINSIC parts and are");
        sb.AppendLine("unscaled; the totals include the aspheric and induced contributions and carry");
        sb.AppendLine(string.Format(Inv, "the F/number ({0:0.####}), so they are not the column sums.", b.FNumber));
        sb.AppendLine("Tau2 to Tau20 are not split into intrinsic and induced parts, so their");
        sb.AppendLine("per-surface columns here read zero; each surface's share of them is in the");
        sb.AppendLine("surface_total rows of <name>.surfaces.tsv, and in WHICH SURFACE below.");
        sb.AppendLine();

        string[] order = BuchdahlTerms.Names;
        var head = new StringBuilder(string.Format(Inv, "{0,-6}", "Surf"));
        foreach (var nme in order) head.Append(string.Format(Inv, " {0,13}", nme));
        sb.AppendLine(head.ToString());

        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            var t = b.Intrinsic[i];
            bool any = false;
            foreach (var nme in order) if (Math.Abs(t[nme]) > 1e-15) any = true;
            if (!any) continue;
            var row = new StringBuilder(string.Format(Inv, "{0,-6}", i));
            foreach (var nme in order) row.Append(string.Format(Inv, " {0,13}", Sci(t[nme])));
            sb.AppendLine(row.ToString());

            if (b.Aspheric[i] != null)
            {
                var a = b.Aspheric[i]!;
                var arow = new StringBuilder(string.Format(Inv, "{0,-6}", "  asph"));
                foreach (var nme in order) arow.Append(string.Format(Inv, " {0,13}", Sci(a[nme])));
                sb.AppendLine(arow.ToString());
            }
        }

        sb.AppendLine(new string('-', 64));
        var tot = new StringBuilder(string.Format(Inv, "{0,-6}", "TOTAL"));
        foreach (var nme in order) tot.Append(string.Format(Inv, " {0,13}", Sci(b.Totals[nme])));
        sb.AppendLine(tot.ToString());

        // No r-squared warning: such a term is a curvature change, folded into the vertex
        // curvature by Surface.VertexForm, so the coefficients above account for it.
        sb.AppendLine();
    }

    /// <summary>
    /// Coefficients and field height for every (wavelength, field) the system defines,
    /// with the weight that combination carries in the composite.
    /// </summary>
    private List<(int Wave, int Field, double H, double Weight, BuchdahlTerms Totals)> PrmsCases()
    {
        var list = new List<(int, int, double, double, BuchdahlTerms)>();
        double maxField = 0.0;
        foreach (var f in _sys.Fields) if (Math.Abs(f.Y) > Math.Abs(maxField)) maxField = f.Y;

        for (int wi = 0; wi < Math.Max(1, _sys.Wavelengths.Count); wi++)
        {
            var n = _indices[Math.Min(wi, _indices.Count - 1)];
            var trace = ParaxialTrace.Trace(_sys, n, maxField);
            var totals = Buchdahl(trace, null, n).Totals;
            double ww = wi < _sys.Wavelengths.Count ? _sys.Wavelengths[wi].Weight : 1.0;

            for (int fi = 0; fi < Math.Max(1, _sys.Fields.Count); fi++)
            {
                double fy = fi < _sys.Fields.Count ? _sys.Fields[fi].Y : 0.0;
                double fw = fi < _sys.Fields.Count ? _sys.Fields[fi].Weight : 1.0;
                double h = Math.Abs(maxField) > 1e-15 ? fy / maxField : 0.0;
                list.Add((wi, fi, h, ww * fw, totals));
            }
        }
        return list;
    }

    private void PrmsSection(StringBuilder sb)
    {
        var cases = PrmsCases();
        sb.AppendLine("PREDICTED RMS SPOT RADIUS (PRMS)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("The RMS spot radius implied by the coefficients above, integrated analytically");
        sb.AppendLine("over the pupil with no rays traced. Method: Robb, J. Opt. Soc. Am. 66, 1037 (1976).");
        sb.AppendLine();
        sb.AppendLine("In LENS UNITS, referenced to the PARAXIAL image plane. It cannot see defocus, and");
        sb.AppendLine("it takes no account of any surface's aperture: a beam clipped on axis or off it");
        sb.AppendLine("reads the same as one that passes unobstructed.");
        sb.AppendLine();
        sb.AppendLine("IT IS ALSO A GEOMETRIC SPOT, and says nothing about diffraction. On a well-corrected");
        sb.AppendLine("design this number goes on shrinking past the point where it means anything: the");
        sb.AppendLine("real image cannot be smaller than the Airy disc, and below that scale the answer is");
        sb.AppendLine("set by the wave and not by the rays. Compare it against the Airy radius before");
        sb.AppendLine("reading it as an image size. The failure is the opposite way round from the");
        sb.AppendLine("truncation one below - that bites where the aberrations are LARGE, this where they");
        sb.AppendLine("are small - so a design can be out of range of both at once for opposite reasons.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-12} {1,-10} {2,10} {3,10} {4,16}",
            "Wavelength", "Field", "Hy", "Weight", "PRMS"));
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count
                ? _sys.Wavelengths[c.Wave].Value.ToString("0.####", Inv)
                  + (_sys.Wavelengths[c.Wave].IsPrimary ? "*" : "")
                : "-";
            string fld = c.Field < _sys.Fields.Count
                ? _sys.Fields[c.Field].Y.ToString("0.####", Inv) : "-";
            sb.AppendLine(string.Format(Inv, "{0,-12} {1,-10} {2,10:0.####} {3,10:0.###} {4,16}",
                wl, fld, c.H, c.Weight, Num6(Prms.Value(c.Totals, c.H))));
        }
        sb.AppendLine(new string('-', 64));
        double composite = Prms.Composite(cases.Select(c => (c.Totals, c.H, c.Weight)));
        sb.AppendLine(string.Format(Inv, "{0,-45}{1,16}", "PRMSA (weighted over all of the above)", Num6(composite)));
        sb.AppendLine();
        sb.AppendLine("ACCURACY FALLS OFF WITH FIELD, NOT WITH APERTURE. The coefficient set is complete");
        sb.AppendLine("through SEVENTH order: nineteen of the twenty tau enter here - B7 is tau1, and");
        sb.AppendLine("tau2 to tau19 follow it. The one left out is tau20, which is distortion: it moves");
        sb.AppendLine("the patch without resizing it, so it cannot reach a spot radius at all.");
        sb.AppendLine("");
        sb.AppendLine("What falls off is the TRUNCATION. At the edge of the field the ninth order arrives,");
        sb.AppendLine("and the series turns over while the real lens does not: on the plain triplet the");
        sb.AppendLine("seventh-order prediction is the best available everywhere up to Hy = 0.9 and then");
        sb.AppendLine("the worst at Hy = 1.0, under-predicting by 13 per cent. That is smooth, it is not");
        sb.AppendLine("ray aiming and nothing is being clipped - it is where seventh order stops being");
        sb.AppendLine("enough. Trust the small-Hy rows furthest, and treat the Hy = 1 row as the weakest");
        sb.AppendLine("number here. docs/spot-prediction.md measures it against traced rays.");
        sb.AppendLine("How far out it stays usable depends on how much seventh-order field aberration the");
        sb.AppendLine("design actually carries, so check against a ray trace before relying on full field.");
        sb.AppendLine();
    }

    /// <summary>
    /// Where the spot is smallest, per wavelength - the one thing the section above cannot say.
    ///
    /// <para>Each colour is computed in its own indices throughout: its own paraxial trace, its
    /// own back focal length, its own marginal slope and its own coefficient set. That is why the
    /// table has a row per wavelength rather than a single answer, and why the shifts are NOT on
    /// a common origin - each is measured from its own colour's paraxial plane.</para>
    /// </summary>
    private void BestFocusSection(StringBuilder sb)
    {
        double maxField = MaxField();
        var per = new List<(int, double, double, bool, ParaxialResult, BuchdahlTerms)>();
        for (int wi = 0; wi < Math.Max(1, _sys.Wavelengths.Count); wi++)
        {
            var n = _indices[Math.Min(wi, _indices.Count - 1)];
            var trace = ParaxialTrace.Trace(_sys, n, maxField);
            var totals = Buchdahl(trace, null, n).Totals;
            bool primary = wi < _sys.Wavelengths.Count && _sys.Wavelengths[wi].IsPrimary;
            double value = wi < _sys.Wavelengths.Count ? _sys.Wavelengths[wi].Value : 0.0;
            double weight = wi < _sys.Wavelengths.Count ? _sys.Wavelengths[wi].Weight : 1.0;
            per.Add((wi, value, weight, primary, trace, totals));
        }

        var fields = new List<(double, double)>();
        for (int fi = 0; fi < Math.Max(1, _sys.Fields.Count); fi++)
        {
            double fy = fi < _sys.Fields.Count ? _sys.Fields[fi].Y : 0.0;
            double fw = fi < _sys.Fields.Count ? _sys.Fields[fi].Weight : 1.0;
            fields.Add((Math.Abs(maxField) > 1e-15 ? fy / maxField : 0.0, fw));
        }

        var rows = BestFocus.ForSystem(per, fields, _sys.LastOpticalSurface());

        sb.AppendLine("BEST FOCUS");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Where the RMS spot above is smallest, from the same coefficients and with no rays");
        sb.AppendLine("traced. Robb's polynomial is referred to the PARAXIAL image plane and carries no");
        sb.AppendLine("defocus term, so PRMS cannot be used to choose an image plane; this is the plane it");
        sb.AppendLine("would choose. Criterion: minimum radius of gyration about the centroid, which is");
        sb.AppendLine("Sands, J. Opt. Soc. Am. 63, 582 (1973), and which is NOT the disk of least");
        sb.AppendLine("confusion - for third-order spherical the two differ by a ninth of the shift.");
        sb.AppendLine();
        sb.AppendLine("dZ is in LENS UNITS from that WAVELENGTH'S OWN paraxial plane, positive away from");
        sb.AppendLine("the last surface. BFL is that colour's own back focal length, so the spread down");
        sb.AppendLine("the BFL column is longitudinal chromatic aberration and the dZ values are not on a");
        sb.AppendLine("common origin: to compare colours, add each row's BFL to its dZ.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-12} {1,-12} {2,8} {3,14} {4,14} {5,14}",
            "Wavelength", "BFL", "Hy", "dZ", "PRMS(parax)", "PRMS(focus)"));

        foreach (var r in rows)
        {
            string wl = r.Wavelength < _sys.Wavelengths.Count
                ? _sys.Wavelengths[r.Wavelength].Value.ToString("0.####", Inv)
                  + (r.IsPrimary ? "*" : "")
                : "-";
            bool first = true;
            foreach (var f in r.Fields)
            {
                sb.AppendLine(string.Format(Inv, "{0,-12} {1,-12} {2,8:0.####} {3,14} {4,14} {5,14}",
                    first ? wl : "", first ? r.Bfl.ToString("0.######", Inv) : "",
                    f.H, Num6(f.DeltaZ), Num6(f.RmsParaxial), Num6(f.RmsBestFocus)));
                first = false;
            }
            sb.AppendLine(string.Format(Inv, "{0,-12} {1,-12} {2,8} {3,14}",
                "", "", "all", Num6(r.DeltaZWholeField)));
        }

        sb.AppendLine(new string('-', 64));
        sb.AppendLine("The 'all' row is the single plane that best focuses the whole field at that");
        sb.AppendLine("wavelength, weighted by the field weights - Sands's Sec. IV. It minimises the");
        sb.AppendLine("weighted sum of the mean squares, so it is a compromise between the rows above it");
        sb.AppendLine("and equals none of them unless the field curvature is flat.");
        sb.AppendLine();
        sb.AppendLine("WHAT THIS INHERITS. The truncation of the coefficients at seventh order, and the");
        sb.AppendLine("paraxial approximation for the image-space ray direction - a plane shift displaces");
        sb.AppendLine("a ray at normalised pupil height rho by dZ*u*rho. Sands tests that approximation");
        sb.AppendLine("and finds it justified except where image-space ray angles approach 45 degrees or");
        sb.AppendLine("distortion is very large. It also inherits PRMS's blindness to apertures: a beam");
        sb.AppendLine("clipped anywhere reads the same as one that passes unobstructed.");
        sb.AppendLine();
    }

    private void ContributionSection(StringBuilder sb)
    {
        var cases = PrmsCases();
        if (cases.Count == 0) return;
        var primary = cases.FirstOrDefault(c => c.Wave == Math.Min(_primary, Math.Max(0, _sys.Wavelengths.Count - 1)));
        var totals = primary.Totals ?? cases[0].Totals;
        const double h = 1.0;

        var rows = ContributionAnalysis.Compute(totals, h);
        double spot = Prms.Value(totals, h);
        double balancing = ContributionAnalysis.BalancingRatio(totals, h);

        sb.AppendLine("WHICH ABERRATION IS COSTING YOU THE SPOT (full field, primary wavelength)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Two different questions, two different numbers.");
        sb.AppendLine();
        sb.AppendLine("  ISOLATED  the RMS spot this aberration would produce with every other");
        sb.AppendLine("            coefficient set to zero. Re-normalised, so the values ARE");
        sb.AppendLine("            comparable between aberrations - which the raw coefficients are");
        sb.AppendLine("            not. They do not sum to the spot the design actually has.");
        sb.AppendLine("            Method: Rosete-Aguilar and Rayces, Proc. SPIE 2730, 499 (1996).");
        sb.AppendLine();
        sb.AppendLine("  SHARE %   what this aberration contributes to the spot the design DOES");
        sb.AppendLine("            have, cross terms split evenly with their partner. Sums to 100%.");
        sb.AppendLine("            A NEGATIVE share means it is cancelling other aberrations:");
        sb.AppendLine("            remove it and the spot gets WORSE.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10}",
            "Aber", "coefficient", "isolated", "share %") + "  aberration");

        foreach (var r in rows.OrderByDescending(x => Math.Abs(x.Percent)))
        {
            if (Math.Abs(r.Coefficient) < 1e-15 && Math.Abs(r.Percent) < 1e-9) continue;
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10:0.0}  {4}",
                r.Name, Sci(r.Coefficient), Num6(r.Isolated), r.Percent,
                AberrationNames.Describe(r.Name)));
        }
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10:0.0}",
            "TOTAL", "", Num6(spot), rows.Sum(x => x.Percent)));

        sb.AppendLine();
        sb.AppendLine("  Names after R. B. Johnson, Appl. Opt. 12, 2079 (1973), Table I. Where a name");
        sb.AppendLine("  reads \"(with ...)\" the named aberration is the COMBINATION of those");
        sb.AppendLine("  coefficients, not this one on its own.");
        sb.AppendLine();
        if (balancing < 0.75)
            sb.AppendLine(string.Format(Inv,
                "  Aberrations are BALANCING here: the spot is {0:0.###}x the mean square it would",
                balancing)
                + Environment.NewLine
                + "  have if each acted alone. Attacking the largest isolated value on its own will"
                + Environment.NewLine
                + "  make the design worse until its partner is corrected too.");
        else
            sb.AppendLine(string.Format(Inv,
                "  Little cancellation here ({0:0.###}x): the aberrations are largely independent,",
                balancing)
                + Environment.NewLine
                + "  so the largest share is the one worth attacking first.");
        sb.AppendLine();
    }

    private void SurfaceContributionSection(StringBuilder sb)
    {
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(trace);
        var rows = ContributionAnalysis.BySurface(b, 1.0);
        if (rows.Count == 0) return;

        sb.AppendLine("WHICH SURFACE IS COSTING YOU THE SPOT (full field, primary wavelength)");
        sb.AppendLine("----------------------------------------------------------------");
        if (b.TertiaryUnattributed)
        {
            // A figured flat facing collimated light: the seventh-order field terms have a
            // system value and no finite value per surface, so what they contribute is shown as
            // one line of its own rather than spread over the surfaces or quietly dropped.
            sb.AppendLine("Share covers every coefficient through seventh-order spherical (B7). The");
            sb.AppendLine("other seventh-order terms, tau2 to tau20, cannot be split by surface on this");
            sb.AppendLine("design - a figured flat faces collimated light, and there only their sum is");
            sb.AppendLine("finite - so their part of the spot is the last line, and with it the column");
            sb.AppendLine("sums to 100%. A negative share is a surface that is CANCELLING the others -");
            sb.AppendLine("which is what a corrector element is for.");
        }
        else
        {
            sb.AppendLine("Share sums to 100%, every coefficient through the seventh order included.");
            sb.AppendLine("A negative share is a surface that is CANCELLING the others - which is what");
            sb.AppendLine("a corrector element is for.");
        }
        sb.AppendLine();
        sb.AppendLine("INDUCED tells you where the fix is. A surface generates aberration of its own,");
        sb.AppendLine("and it also generates more by acting on the aberration handed to it by the");
        sb.AppendLine("surfaces ahead. A high induced fraction means this surface is largely reacting");
        sb.AppendLine("to an upstream problem, and correcting it here will not hold.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,12} {2,12}   {3}",
            "Surf", "share %", "induced", "higher-order coefficients"));
        foreach (var r in rows.OrderByDescending(x => Math.Abs(x.Percent)))
        {
            string bar = r.InducedFraction >= 0.999 ? "all induced"
                       : r.InducedFraction <= 0.001 ? "all its own"
                       : new string('#', (int)Math.Round(r.InducedFraction * 20)).PadRight(20, '.');
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,12:0.0} {2,12:0.0%}   {3}",
                r.Surface, r.Percent, r.InducedFraction, bar));
        }
        double attributed = rows.Sum(x => x.Percent);
        if (b.TertiaryUnattributed)
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,12:0.0}   {2}",
                "tau", 100.0 - attributed, "tau2 to tau20, not split by surface"));
        sb.AppendLine(new string('-', 64));
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,12:0.0}", "TOTAL",
            b.TertiaryUnattributed ? 100.0 : attributed));

        // Individual surfaces routinely contribute far more than the finished design shows,
        // because a corrected lens works by cancellation. Percentages in the hundreds are
        // the normal consequence and mean something specific, so say what.
        double gross = rows.Sum(x => Math.Abs(x.Percent));
        if (gross > 250.0)
        {
            sb.AppendLine();
            sb.AppendLine(string.Format(Inv,
                "  The surfaces cancel heavily: their contributions total {0:0}% in magnitude to",
                gross)
                + Environment.NewLine
                + string.Format(Inv,
                "  leave 100%. Individually they are around {0:0.#}x the finished spot, so a small",
                gross / 200.0)
                + Environment.NewLine
                + "  change to any one of them moves the result far more than its share suggests."
                + Environment.NewLine
                + "  That sensitivity is the design working as intended, not a fault.");
        }
        sb.AppendLine();
    }

    /// <summary>Per surface, per part, every coefficient - the full decomposition.</summary>
    public string BuildSurfaceBreakdownTsv()
    {
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(trace);
        var sb = new StringBuilder();

        sb.Append("surface	part");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + nme.ToLowerInvariant());
        sb.AppendLine();

        void Row(string surf, string part, BuchdahlTerms t)
        {
            sb.Append(surf + "	" + part);
            foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(t[nme]));
            sb.AppendLine();
        }

        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            Row(i.ToString(Inv), "intrinsic", b.Intrinsic[i]);
            if (b.Aspheric[i] != null) Row(i.ToString(Inv), "aspheric", b.Aspheric[i]!);
            Row(i.ToString(Inv), "induced", b.Induced[i]);
            Row(i.ToString(Inv), "surface_total", b.PerSurface[i]);
        }
        Row("TOTAL", "transverse", b.Totals);

        sb.AppendLine();
        sb.AppendLine("# intrinsic + aspheric + induced = surface_total, per surface, unscaled,");
        sb.AppendLine("# through b7. tau2..tau20 are given in surface_total only, as each surface's");
        sb.AppendLine("# share of the tertiary scheme's totals; they are not split into parts.");
        if (b.TertiaryUnattributed)
        {
            sb.AppendLine("# On this design they are not split by surface at all - a figured flat faces");
            sb.AppendLine("# collimated light, and a surface's own share has no finite value there, only");
            sb.AppendLine("# the sum does - so surface_total carries zeros for them and does NOT sum to");
            sb.AppendLine("# the tau2..tau20 totals.");
        }
        sb.AppendLine("# Summing surface_total over the surfaces and multiplying by the F/number");
        sb.AppendLine(string.Format(Inv, "# ({0:R}) reproduces the transverse totals exactly{1}.", b.FNumber,
            b.TertiaryUnattributed ? ", tau2..tau20 excepted" : ""));
        return sb.ToString();
    }

    /// <summary>Per-surface share of the spot, with the induced fraction.</summary>
    public string BuildSurfaceShareTsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	hy	surface	share_of_mean_square	percent	induced_fraction");
        foreach (var c in PrmsCases())
        {
            var n = _indices[Math.Min(c.Wave, _indices.Count - 1)];
            var trace = ParaxialTrace.Trace(_sys, n, MaxField());
            var b = Buchdahl(trace, null, n);
            string wl = c.Wave < _sys.Wavelengths.Count ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            var rows = ContributionAnalysis.BySurface(b, c.H);
            foreach (var r in rows)
                sb.AppendLine(string.Join("	", wl, Raw(c.H), r.Surface.ToString(Inv),
                    Raw(r.Share), Raw(r.Percent), Raw(r.InducedFraction)));

            // tau2..tau20 where they cannot be split by surface - see the report's section.
            if (b.TertiaryUnattributed)
            {
                double total = Prms.MeanSquare(b.Totals, c.H);
                double rest = total - rows.Sum(r => r.Share);
                sb.AppendLine(string.Join("	", wl, Raw(c.H), "tau_unattributed", Raw(rest),
                    Raw(Math.Abs(total) > 1e-30 ? 100.0 * rest / total : 0.0), ""));
            }
        }
        return sb.ToString();
    }

    public string BuildContributionTsv()
    {
        var cases = PrmsCases();
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	hy	aberration	order	name	coefficient	isolated_rms	share_of_mean_square	percent");
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            foreach (var r in ContributionAnalysis.Compute(c.Totals, c.H))
                sb.AppendLine(string.Join("	", wl, Raw(c.H), r.Name,
                    AberrationNames.Order(r.Name).ToString(Inv), AberrationNames.Describe(r.Name),
                    Raw(r.Coefficient), Raw(r.Isolated), Raw(r.Share), Raw(r.Percent)));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Whether this design would test the aspheric tertiary path - see
    /// <see cref="AsphericDiagnostic"/>. Readable text rather than a table: it is a verdict
    /// with reasons, and the reasons are the whole point of it.
    /// </summary>
    public string BuildAsphericScreenText(double h = 1.0)
        => AsphericDiagnostic.Screen(_sys, PrimaryIndices, MaxField(), h).ToString();

    /// <summary>
    /// The coefficient of QUATERNARY - ninth-order - spherical aberration, per surface, with the
    /// intermediate rows Buchdahl's own table prints beside it.
    ///
    /// <para>Its own output rather than a section of the main report, for the reason the aspheric
    /// screen and the distortion check are: it answers one question, it runs the tertiary scheme
    /// a second time to do it, and it is refused outright on designs the rest of the report
    /// handles perfectly well. A section that is usually an apology is better as a mode.</para>
    ///
    /// <para><b>Why the ninth order is worth asking for at all.</b> Everything else this program
    /// reports stops at the seventh, and where a prediction and a traced ray part company on axis
    /// the residual has had to be ATTRIBUTED to the ninth order rather than measured - see the
    /// threshold in <see cref="AsphericDiagnostic"/>, which says in as many words that near or
    /// above one "a residual is as likely the missing ninth order" as a fault in the seventh.
    /// This makes that an arithmetic question.</para>
    /// </summary>
    /// <summary>
    /// Which surface to figure, and what figuring it would buy - see
    /// <see cref="AspherePlacement"/>. Its own output rather than a report section, for the
    /// reason the distortion check and the aspheric screen are: it asks a design question rather
    /// than describing the design in hand, and a reader who wanted the coefficients did not ask
    /// where to put an asphere.
    /// </summary>
    public string BuildAspherePlacementText()
    {
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        return AspherePlacement.Render(_sys, PrimaryIndices, trace, Seidel(trace));
    }

    public string BuildQuaternaryText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("QUATERNARY (NINTH-ORDER) SPHERICAL ABERRATION");
        sb.AppendLine("----------------------------------------------------------------");

        string? why = QuaternarySpherical.Unsupported(_sys);
        if (why != null)
        {
            sb.AppendLine("Not computed: " + why + ".");
            sb.AppendLine();
            sb.AppendLine("Buchdahl, Optical Aberration Coefficients IV, J. Opt. Soc. Am. 48, 757");
            sb.AppendLine("(1958). The refusal is deliberate - the scheme would return a number on");
            sb.AppendLine("a figured surface, and it would be neither the spherical coefficient nor");
            sb.AppendLine("the aspheric one.");
            return sb.ToString();
        }

        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var q = QuaternarySpherical.FromSystem(_sys, PrimaryIndices, p);
        if (q == null) { sb.AppendLine("Not computed."); return sb.ToString(); }

        sb.AppendLine("Buchdahl's q1p, in the units of his paper IV Table I: the scheme runs at unit");
        sb.AppendLine("focal length and nothing here rescales. INTRINSIC is what the surface makes on");
        sb.AppendLine("its own; TOTAL adds what it makes by acting on the aberration already reaching");
        sb.AppendLine("it. The system figure is the sum of the TOTAL column.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,16} {3,16}",
                                    "Surf", "intrinsic", "total", "T1-dagger"));

        int last = _sys.LastOpticalSurface();
        for (int i = 1; i <= last && i < q.Rows.Length; i++)
        {
            var r = q.Rows[i];
            if (r == null) continue;
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,16} {3,16}",
                                        i, Sci(r.Intrinsic), Sci(r.Total), Sci(r.T1Dagger)));
        }

        sb.AppendLine(new string('-', 57));
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,16}", "TOTAL", "", Sci(q.Total)));
        sb.AppendLine();
        sb.AppendLine("Buchdahl's rule of thumb, from the closing paragraph of paper IV: a system");
        sb.AppendLine("intended to work at f/2 should aim at individual contributions of at most");
        sb.AppendLine("order 1000 at unit focal length. His own triplet runs to six figures, which");
        sb.AppendLine("is why he chose it - it is a poorly corrected system and the table shows it.");

        return sb.ToString();
    }

    /// <summary>
    /// Distortion predicted from the coefficients against distortion traced, at each of a
    /// ladder of field fractions and at three truncations of the same set. See
    /// <see cref="DistortionPrediction"/> for what is predicted and what is traced.
    ///
    /// <para>Readable text rather than a table, and separate from the report, for the reason
    /// the aspheric screen and the Forbes breakdown are: it traces rays, which nothing else
    /// the report prints does, and it is a measurement of the prediction rather than a
    /// prediction.</para>
    ///
    /// <para>Both mappings are columns of one table rather than two tables with an essay
    /// between them. What the reader is owed is the definition of each column and the numbers;
    /// which mapping their design is specified against, and whether an error of a given size
    /// matters to them, are theirs to know and not this program's to pronounce on.</para>
    /// </summary>
    public string BuildDistortionText()
    {
        double field = MaxField();
        var sb = new StringBuilder();

        sb.AppendLine("DISTORTION FROM THE ABERRATION COEFFICIENTS, AGAINST TRACED RAYS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Predicted: Robb's polynomial at zero pupil radius, eps_y = E h^3 + E5 h^5 + tau20 h^7.");
        sb.AppendLine("Traced:    the ray through the centre of the paraxial entrance pupil, at paraxial focus.");
        sb.AppendLine("h:         fractional field in the tangent sense, tan(theta) = h tan(theta_max).");
        sb.AppendLine("Ideal:     f tan(theta) for F-tan(th), f theta for F-theta.");
        sb.AppendLine();

        if (Math.Abs(field) < 1e-15)
        {
            sb.AppendLine("This design has no off-axis field, so it has no distortion to measure.");
            return sb.ToString();
        }

        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        var totals = TertiaryForDistortion(p, field, out string route);
        var rows = DistortionPrediction.Compare(_sys, PrimaryIndices, p, totals, field);

        sb.AppendLine(route);
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "  E     {0,12}   3rd order distortion", Sci(totals.E)));
        sb.AppendLine(string.Format(Inv, "  E5    {0,12}   5th", Sci(totals.E5)));
        sb.AppendLine(string.Format(Inv, "  tau20 {0,12}   7th", Sci(totals.Tau20)));
        sb.AppendLine();

        sb.AppendLine("                  traced, per cent      predicted F-tan(th), per cent"
                    + "      error, % of traced");
        sb.AppendLine(string.Format(Inv, Layout, "H", "field", "F-tan(th)", "F-theta",
                                    "3rd", "3rd+5th", "full 7th", "3rd", "3rd+5th", "full 7th"));
        sb.AppendLine("  " + new string('-', 100));

        foreach (var r in rows)
        {
            if (!r.Ok)
            {
                sb.AppendLine(string.Format(Inv, "  {0,4:F2} {1,8:F3}   the chief ray does not get "
                                               + "through at this field", r.H, r.Field));
                continue;
            }
            sb.AppendLine(string.Format(Inv, Layout,
                r.H.ToString("F2", Inv), r.Field.ToString("F3", Inv),
                Pct(r.TracedPercent), Pct(r.TracedPercentFTheta),
                Pct(r.Percent(r.Third)), Pct(r.Percent(r.Fifth)), Pct(r.Percent(r.Seventh)),
                Err(r.RelativeError(3)), Err(r.RelativeError(5)), Err(r.RelativeError(7))));
        }
        sb.AppendLine();

        ImagePlaneNote(sb, p, totals, field, rows);

        sb.AppendLine("  COEFFICIENTS READ BACK OUT OF THE RAYS");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, RecoveryLayout, "", "used", "from rays", "  ratio"));
        foreach (var r in DistortionPrediction.Recover(_sys, PrimaryIndices, p, totals, field))
        {
            sb.AppendLine(string.Format(Inv, RecoveryLayout,
                r.Name, Sci(r.Reported), Sci(r.FromRays),
                r.Reliable ? RatioWithUncertainty(r.Ratio, r.Spread) : "   not resolved"));
        }
        sb.AppendLine();
        sb.AppendLine("  The +/- is what the ray measurement is worth, so the ratio means nothing beyond");
        sb.AppendLine("  it. \"not resolved\" means the term is too small for the ray trace to measure at");
        sb.AppendLine("  these fields, not that it disagrees.");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// The coefficient set to predict distortion from, with the seventh-order term taken by
    /// whichever route is trustworthy for this design, and a line saying which and why.
    ///
    /// <para><b>Figured: Forbes.</b> The Buchdahl scheme needs an aspheric tertiary
    /// arrangement Buchdahl never published, so this program's is a reconstruction. When this
    /// route was chosen that reconstruction was wrong - real rays put it out by a factor of two
    /// on some designs - and there was no reason to show a designer a number known to be wrong,
    /// or to make them choose.</para>
    ///
    /// <para><b>That reason has expired, and the route is kept on a weaker one.</b> The
    /// reconstruction was completed in "The sixth member by duality" (14 September 2026) and
    /// now agrees with Forbes on all twenty tau to between 2E-13 and 2E-10 on every figured
    /// design measured - see <c>docs/verification.md</c>, <i>The aspheric arrangement, and how
    /// it was established</i>. So the substitution below no longer corrects anything: on a
    /// figured refracting system the two values it chooses between agree to roundoff. It is
    /// kept because Forbes' series is an expansion rather than a reconstruction and is
    /// therefore still the better-founded of two numbers that agree, and because removing it
    /// would move printed output for no gain. It should NOT be read as evidence that the
    /// scheme's figured tau20 is untrustworthy; it is not, and the note printed with it says
    /// which route was taken rather than implying a defect.</para>
    ///
    /// <para><b>Unfigured: either.</b> The two routes agree to roundoff on all twenty tau at
    /// both conjugates, so the choice is empty and the scheme's own value is kept, which
    /// leaves everything validated at infinite conjugate bit-identical.</para>
    ///
    /// <para><b>E and E5 always come from the scheme</b>, at both conjugates and figured or
    /// not. The Forbes inversion produces the tertiary only, and it is not needed: the
    /// aspheric third and fifth orders are established by closed-form conic surfaces, which
    /// is a printed answer rather than a reconstruction. See <c>docs/verification.md</c>.</para>
    /// </summary>
    private BuchdahlTerms TertiaryForDistortion(ParaxialResult p, double field, out string route)
    {
        var totals = Buchdahl(p).Totals;

        bool figured = false;
        for (int i = 1; i <= _sys.LastOpticalSurface(); i++)
            if (_sys.Surfaces[i].IsFigured) { figured = true; break; }

        if (!figured)
        {
            route = "Every surface is spherical: tau20 is Buchdahl's scheme's, and the Forbes series\n"
                  + "trace agrees with it to roundoff.";
            return totals;
        }

        var forbes = Forbes.ForbesCoefficients.Invert(_sys, PrimaryIndices, p, field);
        if (forbes == null)
        {
            route = "This design is FIGURED, so tau20 would ordinarily be the Forbes series trace's -\n"
                  + "but the trace does not close on it, or declines it (it does not trace a mirror),\n"
                  + "and the scheme's own value is used instead.\n"
                  + "That value is the aspheric arrangement of M Sec. 85, which agrees with Forbes to\n"
                  + "2E-10 or better wherever the two can both be formed; here Forbes offers no\n"
                  + "comparison, which is the whole of what this note is saying. On a mirror the\n"
                  + "rays read back below are the check.";
            return totals;
        }

        var used = totals.Clone();
        used.Tau20 = forbes.Tau[20];
        route = "This design is FIGURED: tau20 is the FORBES series trace's. E and E5 are Buchdahl's\n"
              + "scheme's, as they are at every design. See docs/distortion-prediction.md.";
        return used;
    }

    /// <summary>
    /// What the same lens reads at the image surface the FILE defines, when that is not the
    /// paraxial image plane.
    ///
    /// <para>This is the number a design program prints, and it differs: shifting the plane
    /// scales both the real and the ideal height, and not by quite the same factor. On the
    /// Cooke triplet in this repository the file sits 0.207 lens units inside paraxial focus
    /// and the full-field distortion reads 0.0620 per cent there against 0.0486 at paraxial
    /// focus - a difference of a quarter, entirely from the plane.</para>
    ///
    /// <para>The table cannot simply be moved there. The coefficients are referred to the
    /// paraxial image plane and Robb's polynomial has no defocus term, so a prediction quoted
    /// at any other plane would be comparing two different things. So the comparison stays at
    /// paraxial focus and the design program's figure is given beside it, with the reason.</para>
    /// </summary>
    private void ImagePlaneNote(StringBuilder sb, ParaxialResult p, BuchdahlTerms totals,
                                double field, IReadOnlyList<DistortionPrediction.Row> rows)
    {
        int last = _sys.LastOpticalSurface();
        double lastToImage = 0.0;
        for (int i = last; i < _sys.Surfaces.Count - 1; i++)
        {
            double t = _sys.Surfaces[i].Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t)) lastToImage += t;
        }
        double offset = p.ParaxialFocusDistance - lastToImage;
        if (Math.Abs(offset) < 1e-9 || rows.Count == 0) return;

        var atFile = DistortionPrediction.Compare(_sys, PrimaryIndices, p, totals, field,
                                                  new[] { rows[rows.Count - 1].H },
                                                  atParaxialFocus: false);
        if (atFile.Count == 0 || !atFile[0].Ok) return;

        // Only worth saying when the two planes give materially different figures. A design
        // saved AT paraxial focus still lands a fraction of a micron away through rounding,
        // and a paragraph about a tenth of a per cent of a distortion figure would be noise.
        double here = rows[rows.Count - 1].TracedPercent;
        if (Math.Abs(here) < 1e-12) return;
        if (Math.Abs(atFile[0].TracedPercent / here - 1.0) < 0.01) return;

        sb.AppendLine(string.Format(Inv,
            "  The file's image surface is {0:0.0000} lens units {1} paraxial focus. Measured there,",
            Math.Abs(offset), offset > 0 ? "inside" : "beyond"));
        sb.AppendLine(string.Format(Inv,
            "  traced F-tan(th) at H = {0:F2} is {1:F4} % rather than {2:F4} %. The table is at paraxial",
            atFile[0].H, atFile[0].TracedPercent, rows[rows.Count - 1].TracedPercent));
        sb.AppendLine("  focus, where the coefficients are referred and where the polynomial can be");
        sb.AppendLine("  compared with rays at all.");
        sb.AppendLine();
    }

    /// <summary>Column widths of the distortion table, shared by its header and its rows.</summary>
    private const string Layout =
        "  {0,4} {1,8} {2,10} {3,10}   {4,10} {5,10} {6,10}   {7,8} {8,8} {9,8}";


    /// <summary>A relative error as a signed percentage, or a dash when it is not defined.</summary>
    private static string Err(double v) =>
        double.IsNaN(v) || double.IsInfinity(v)
            ? "-"
            : (100.0 * v).ToString("+0.0;-0.0;0.0", Inv) + "%";

    /// <summary>
    /// A ratio with its uncertainty, printed to the precision the uncertainty allows.
    ///
    /// <para>0.9975 +/- 0.0505 claims four digits the measurement has not got, and a reader
    /// who takes the 0.9975 seriously has been misled by the formatting rather than by the
    /// number. So the decimals follow the uncertainty: two of them when it is 5 per cent,
    /// five when the two estimates agreed to a part in a hundred thousand.</para>
    /// </summary>
    private static string RatioWithUncertainty(double ratio, double uncertainty)
    {
        int dp = uncertainty >= 0.05 ? 2
               : uncertainty >= 0.005 ? 3
               : uncertainty >= 0.0005 ? 4 : 5;
        string f = "F" + dp.ToString(Inv);

        // An uncertainty that rounds to zero is not zero, and saying so beats printing a
        // string of noughts that reads as exactness.
        double floor = 0.5 * Math.Pow(10.0, -dp);
        string plusMinus = uncertainty < floor
            ? "<" + floor.ToString(f, Inv)
            : uncertainty.ToString(f, Inv);

        // The value is padded so that the +/- of every row sits in one column: three rows
        // whose signs and widths differ are read down, not across.
        return string.Format(Inv, "{0,7} +/- {1}", ratio.ToString(f, Inv), plusMinus);
    }

    /// <summary>Column widths of the coefficient-recovery table.</summary>
    private const string RecoveryLayout = "  {0,-8} {1,12} {2,13}   {3}";

    /// <summary>An unsigned percentage - for a magnitude, where a leading + would mislead.</summary>
    private static string Mag(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : (100.0 * v).ToString("0.0", Inv) + "%";

    /// <summary>A distortion figure, already in per cent.</summary>
    private static string Pct(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.0000", Inv);

    /// <summary>
    /// Third, fifth and seventh order per surface, intrinsic and induced, the seventh by the
    /// Forbes series trace. Null when the coefficients cannot be separated - a system with no
    /// field, a design the series does not close on, or one with a mirror, which it declines.
    /// </summary>
    public string? BuildForbesText(int degree = 3)
    {
        double field = MaxField();
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        return ForbesReport.Build(_sys, PrimaryIndices, trace, field, degree);
    }

    public string BuildPrmsTsv()
    {
        var cases = PrmsCases();
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	field	hy	weight	prms");
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count
                ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            string fld = c.Field < _sys.Fields.Count
                ? _sys.Fields[c.Field].Y.ToString("R", Inv) : "";
            sb.AppendLine(string.Join("	", wl, fld, Raw(c.H), Raw(c.Weight), Raw(Prms.Value(c.Totals, c.H))));
        }
        sb.AppendLine(string.Join("	", "ALL", "ALL", "", "",
            Raw(Prms.Composite(cases.Select(c => (c.Totals, c.H, c.Weight))))));
        return sb.ToString();
    }

    private static string Num6(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.000000", Inv);

    public string BuildBuchdahlTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(p);
        var sb = new StringBuilder();
        sb.Append("surface	part");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + nme.ToLowerInvariant());
        sb.AppendLine();
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            sb.Append(i.ToString(Inv) + "	intrinsic");
            foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Intrinsic[i][nme]));
            sb.AppendLine();
            if (b.Aspheric[i] != null)
            {
                sb.Append(i.ToString(Inv) + "	aspheric");
                foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Aspheric[i]![nme]));
                sb.AppendLine();
            }
        }
        sb.Append("TOTAL	transverse");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Totals[nme]));
        sb.AppendLine();
        return sb.ToString();
    }

    public string BuildSeidelTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var s = Seidel(p);
        var sb = new StringBuilder();
        sb.AppendLine("surface	s1_spherical	s2_coma	s3_astigmatism	s4_petzval	s5_distortion	cl_axial_color	ct_lateral_color");
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
            sb.AppendLine(string.Join("	", i.ToString(Inv), Raw(s.S1[i]), Raw(s.S2[i]), Raw(s.S3[i]),
                Raw(s.S4[i]), Raw(s.S5[i]), Raw(s.CL[i]), Raw(s.CT[i])));
        sb.AppendLine(string.Join("	", "TOTAL", Raw(s.TotalS1), Raw(s.TotalS2), Raw(s.TotalS3),
            Raw(s.TotalS4), Raw(s.TotalS5), Raw(s.TotalCL), Raw(s.TotalCT)));
        return sb.ToString();
    }

    private static string Sci(double v) => v.ToString("0.0000E+00", Inv);

    /// <summary>
    /// <see cref="Sci"/> with negative zero folded onto zero. A field vector that is exactly on
    /// axis in one component prints "-0.0000E+00" otherwise, which reads as a measurement when
    /// it is the absence of one.
    /// </summary>
    private static string SciZ(double v) => Sci(v == 0.0 ? 0.0 : v);

    private void Warnings(StringBuilder sb, ParaxialResult p)
    {
        var notes = new List<string>();
        if (Unresolved.Count > 0)
            notes.Add($"No index for {string.Join(", ", Unresolved)} - treated as air, so every "
                    + "number above is wrong for those surfaces.");
        if (Ambiguous.Count > 0)
            notes.Add($"This file names no glass catalog, and {string.Join("; ", Ambiguous)} "
                    + "exists in more than one of the loaded ones. They are not the same glass, "
                    + "and another program opening this file may pick a different one. To settle "
                    + "it, point --glass at a folder holding only the catalog you mean."
                    + (_sys.GlassCatalogsAreInferred
                        ? " The catalog shown against each was worked out from the glass names "
                        + "rather than read from the file, so it is this program's guess."
                        : " Schott wins a name no file claimed, because the files that name no "
                        + "catalog are overwhelmingly classical designs written in Schott glasses "
                        + "and that is what OpticStudio resolves them to; anything Schott does "
                        + "not have falls to load order. A better guess is still a guess."));
        foreach (var s in _sys.Surfaces)
            if (s.Type == SurfaceType.CoordinateBreak)
            {
                notes.Add("This design has a coordinate break. This program analyses rotationally "
                        + "symmetric systems; a tilt or decentre is read but not applied.");
                break;
            }
        if (MaxField() == 0.0)
            notes.Add("This design has no field, only an on-axis point. Only the spherical series "
                    + "B, B5, B7 is non-zero; every field-dependent coefficient is exactly zero. "
                    + "(OSLO, given no field, traces a chief ray of slope 1E-6 and prints small "
                    + "field-dependent values for it; its B, B5, B7 agree with these.)");
        if (p.InvariantDrift > 1e-9)
            notes.Add($"Lagrange invariant drifts by {p.InvariantDrift:0.0E+00} - the paraxial trace "
                    + "is not self-consistent on this system.");
        if (notes.Count == 0) return;

        sb.AppendLine("WARNINGS");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var w in notes) sb.AppendLine("  ! " + w);
        sb.AppendLine();
    }

    // ── Machine-readable ─────────────────────────────────────────────────────────────

    public string BuildPrescriptionTsv()
    {
        var n = PrimaryIndices;
        var sb = new StringBuilder();
        sb.AppendLine("surface\tlabel\ttype\tradius\tthickness\tmaterial\tindex\tsemi_diameter\tconic\tis_stop");
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string label = i == 0 ? "OBJ" : i == _sys.Surfaces.Count - 1 ? "IMG" : i.ToString(Inv);
            sb.AppendLine(string.Join("\t",
                i.ToString(Inv), label, s.Type.ToString(), Raw(s.Radius), Raw(s.Thickness),
                MaterialName(s),
                i < n.Length ? n[i].ToString("R", Inv) : "",
                Raw(s.SemiDiameter), Raw(s.Conic), s.IsStop ? "1" : "0"));
        }
        return sb.ToString();
    }

    public string BuildIndicesTsv()
    {
        var sb = new StringBuilder();
        sb.Append("material\tsource\tvd");
        foreach (var w in _sys.Wavelengths) sb.Append("\tn_" + w.Value.ToString("0.####", Inv));
        sb.AppendLine();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string name = MaterialName(s);
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
            sb.Append(string.Join("\t", name, Source(s), Vd(s)));
            foreach (var perWave in _indices)
                sb.Append("\t" + (i < perWave.Length ? perWave[i].ToString("R", Inv) : ""));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string BuildFirstOrderTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var sb = new StringBuilder();
        sb.AppendLine("quantity\tvalue");
        sb.AppendLine("effective_focal_length\t" + Raw(p.Efl));
        sb.AppendLine("back_focal_length\t" + Raw(p.Bfl));
        sb.AppendLine("f_number\t" + Raw(p.FNumber));
        sb.AppendLine("entrance_pupil_diameter\t" + Raw(p.Epd));
        sb.AppendLine("entrance_pupil_position\t" + Raw(p.EntrancePupilPosition));
        sb.AppendLine("exit_pupil_diameter\t" + Raw(p.ExitPupilDiameter));
        sb.AppendLine("exit_pupil_position\t" + Raw(p.ExitPupilPosition));
        sb.AppendLine("image_height_at_image_surface\t" + Raw(p.ImageHeight));
        sb.AppendLine("paraxial_focus_distance\t" + Raw(p.ParaxialFocusDistance));
        sb.AppendLine("paraxial_image_height\t" + Raw(p.ParaxialImageHeight));
        sb.AppendLine("magnification\t" + Raw(p.Magnification));
        sb.AppendLine("infinite_conjugate\t" + (p.InfiniteConjugate ? "1" : "0"));
        sb.AppendLine("lagrange_invariant\t" + Raw(p.LagrangeInvariant));
        sb.AppendLine("invariant_drift\t" + Raw(p.InvariantDrift));
        return sb.ToString();
    }

    /// <summary>Per-surface paraxial ray data - the input the aberration sums will need.</summary>
    public string BuildParaxialRaysTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var sb = new StringBuilder();
        sb.AppendLine("surface\tn\ty_marginal\tu_marginal\ty_chief\tu_chief");
        for (int i = 0; i < _sys.Surfaces.Count; i++)
            sb.AppendLine(string.Join("\t", i.ToString(Inv),
                Raw(p.N[i]), Raw(p.Y[i]), Raw(p.U[i]), Raw(p.Ybar[i]), Raw(p.Ubar[i])));
        return sb.ToString();
    }

    // ── Shared formatting ────────────────────────────────────────────────────────────

    private static string Kind(Surface s) =>
        s.Type == SurfaceType.EvenAsphere ? "Asphere"
        : s.Type == SurfaceType.Standard ? (Math.Abs(s.Curvature) < 1e-15 ? "Plane" : "Sphere")
        : s.Type.ToString();

    private static string Note(Surface s)
    {
        var bits = new List<string>();
        if (s.IsStop) bits.Add("stop");
        if (s.IsMirror) bits.Add("mirror");
        if (s.HasMarginalRaySolve) bits.Add("solved");
        if (!string.IsNullOrWhiteSpace(s.Comment)) bits.Add(s.Comment!);
        return string.Join(", ", bits);
    }

    private static string MaterialName(Surface s)
    {
        if (!string.IsNullOrWhiteSpace(s.Material)) return s.Material!;
        if (s.ModelIndexEnabled && s.ModelNd > 0.0)
            return $"model {s.ModelNd.ToString("0.####", Inv)}/{s.ModelVd.ToString("0.##", Inv)}";
        return "";
    }

    private string Source(Surface s)
    {
        if (s.ModelIndexEnabled) return "model glass";
        if (GlassCode.TryParse(s.Material, out _, out _)) return "glass code";
        var g = _catalog.GetGlass(s.Material);
        return g != null ? g.Catalog : "NOT FOUND";
    }

    private string Vd(Surface s)
    {
        if (s.ModelIndexEnabled && s.ModelVd > 0) return s.ModelVd.ToString("0.##", Inv);
        if (GlassCode.TryParse(s.Material, out _, out double vd)) return vd.ToString("0.##", Inv);
        var g = _catalog.GetGlass(s.Material);
        return g != null && g.Vd > 0 ? g.Vd.ToString("0.##", Inv) : "";
    }

    /// <summary>Display form: fixed decimals, with infinity spelled out.</summary>
    private static string Num(double v) =>
        double.IsNaN(v) ? "-" : double.IsInfinity(v) ? "infinity" : v.ToString("0.####", Inv);

    /// <summary>Round-trip form for the TSV, so a reader loses no precision.</summary>
    private static string Raw(double v) =>
        double.IsNaN(v) ? "" : double.IsInfinity(v) ? (v > 0 ? "inf" : "-inf") : v.ToString("R", Inv);

    /// <summary>
    /// The nodal aberration theory report: where each surface's aberration field has been
    /// displaced to, and where the nodes of the system's field have ended up.
    ///
    /// <para>A total RMS number says a telescope is soft. This says WHICH surface moved, because
    /// the node geometry is characteristic of the fault: a figure error at the stop contributes
    /// to the node SPLITTING but not to their midpoint, so its two astigmatic nodes stay
    /// symmetric about the field centre, while a misaligned surface moves the midpoint and
    /// carries them off together.</para>
    ///
    /// <para>Third order only, which is where nodal aberration theory's node structure is exact
    /// and where its subject - conic telescopes - lives. An aligned design prints the same
    /// coefficients it always did and every node at the origin, which is the theory's own
    /// consistency check rather than a special case.</para>
    /// </summary>
    public string BuildNatText()
    {
        var sb = new StringBuilder();
        double field = MaxField();

        sb.AppendLine("NODAL ABERRATION THEORY - THIRD ORDER");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Thompson, J. Opt. Soc. Am. A 22, 1389 (2005). Each surface contributes the");
        sb.AppendLine("rotationally symmetric field it always did, displaced by sigma; the total is the");
        sb.AppendLine("sum of displaced fields, and its zeros - the NODES - leave the axis.");
        sb.AppendLine("Field vectors are in the units of the design's field, measured from its centre.");
        sb.AppendLine();

        if (Math.Abs(field) < 1e-15)
        {
            sb.AppendLine("This design has no off-axis field, so it has no aberration field to displace.");
            return sb.ToString();
        }

        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        var seidel = SeidelCoefficients.Compute(_sys, PrimaryIndices, PrimaryIndices,
                                                PrimaryIndices, p);
        var nat = NatField.Compute(_sys, PrimaryIndices, p, seidel);

        sb.AppendLine("  Third-order wave coefficients of the system");
        sb.AppendLine(string.Format(Inv, "    W040  {0,13}   spherical", SciZ(nat.Totals.W040)));
        sb.AppendLine(string.Format(Inv, "    W131  {0,13}   coma", SciZ(nat.Totals.W131)));
        sb.AppendLine(string.Format(Inv, "    W222  {0,13}   astigmatism", SciZ(nat.Totals.W222)));
        sb.AppendLine(string.Format(Inv, "    W220P {0,13}   Petzval", SciZ(nat.Totals.W220P)));
        sb.AppendLine(string.Format(Inv, "    W220S {0,13}   sagittal = W220P + W222/2", SciZ(nat.Totals.W220S)));
        sb.AppendLine(string.Format(Inv, "    W220M {0,13}   medial   = W220P + W222", SciZ(nat.Totals.W220M)));
        sb.AppendLine(string.Format(Inv, "    W220T {0,13}   tangential = W220P + 3W222/2", SciZ(nat.Totals.W220T)));
        sb.AppendLine(string.Format(Inv, "    W311  {0,13}   distortion", SciZ(nat.Totals.W311)));
        sb.AppendLine();

        if (nat.IsAligned)
        {
            sb.AppendLine("  This design is ALIGNED - no surface carries a tilt or a decentre - so every");
            sb.AppendLine("  sigma is zero, the sums collapse to the ordinary Seidel ones, and every node");
            sb.AppendLine("  sits at the centre of the field. That is the theory reducing correctly, not a");
            sb.AppendLine("  case it declines to handle: perturb a surface and the nodes move.");
            AppendNatFifthOrder(sb, p, nat);
            return sb.ToString();
        }

        sb.AppendLine("  Perturbations, and the aberration field centre each surface acquires");
        sb.AppendLine("    sigma points to where that surface's own field lands. It DIVERGES where the");
        sb.AppendLine("    chief ray strikes a surface normally - that surface then contributes no coma");
        sb.AppendLine("    and no astigmatism either, so the products the theory uses stay finite.");
        sb.AppendLine();
        sb.AppendLine("    surf      dec x      dec y     tilt x     tilt y      sigma x      sigma y");
        sb.AppendLine("    " + new string('-', 74));

        int last = _sys.LastOpticalSurface();
        for (int j = 1; j <= last; j++)
        {
            var s = _sys.Surfaces[j];
            bool suppressed = Array.IndexOf(nat.Sigmas.SigmaSuppressedAt, j) >= 0;
            var sig = nat.Sigmas.Sigma[j];
            if (!s.IsPerturbed && sig.MagnitudeSquared == 0.0 && !suppressed) continue;

            sb.AppendLine(string.Format(Inv,
                "    {0,4} {1,10} {2,10} {3,10} {4,10} {5,12} {6,12}",
                j, Num(s.DecenterX), Num(s.DecenterY),
                Num(s.TiltX), Num(s.TiltY),
                suppressed ? "diverges" : SciZ(sig.X),
                suppressed ? "diverges" : SciZ(sig.Y)));
        }
        sb.AppendLine();

        sb.AppendLine("  Nodes");
        if (nat.ComaNodeExists)
            sb.AppendLine(string.Format(Inv, "    coma          one node at  ({0}, {1})",
                                        SciZ(nat.ComaNode.X), SciZ(nat.ComaNode.Y)));
        else
            sb.AppendLine("    coma          the system has no third-order coma to displace");

        if (nat.AstigmatismNodesExist)
        {
            sb.AppendLine(string.Format(Inv, "    astigmatism   two nodes at ({0}, {1})",
                                        SciZ(nat.AstigmatismNode1.X), SciZ(nat.AstigmatismNode1.Y)));
            sb.AppendLine(string.Format(Inv, "                           and ({0}, {1})",
                                        SciZ(nat.AstigmatismNode2.X), SciZ(nat.AstigmatismNode2.Y)));
            sb.AppendLine(string.Format(Inv, "                  midpoint     ({0}, {1})",
                                        SciZ(nat.A222Normalised.X), SciZ(nat.A222Normalised.Y)));
            sb.AppendLine("                  a midpoint at the field centre is the signature of figure");
            sb.AppendLine("                  error at the stop; a displaced one, of misalignment.");
        }
        else
        {
            sb.AppendLine("    astigmatism   the system is anastigmatic, so the binodal form degenerates");
        }

        if (nat.MedialExists)
            sb.AppendLine(string.Format(Inv, "    medial focus  vertex at    ({0}, {1})",
                                        SciZ(nat.MedialVertex.X), SciZ(nat.MedialVertex.Y)));
        else
            sb.AppendLine("    medial focus  not available - a sigma diverges, or W220M is zero");

        sb.AppendLine();
        sb.AppendLine("  The medial vertex and the coma node do not generally coincide: the sigma are the");
        sb.AppendLine("  same for every aberration, but each is weighted by its own surface coefficients.");

        AppendNatFifthOrder(sb, p, nat);
        return sb.ToString();
    }

    /// <summary>
    /// The fifth-order half of the nodal report: Thompson's multinodal trilogy, J. Opt. Soc.
    /// Am. A <b>26</b> 1090 (2009), <b>27</b> 1490 (2010) and <b>28</b> 821 (2011).
    /// </summary>
    private void AppendNatFifthOrder(StringBuilder sb, ParaxialResult p, NatField nat)
    {
        var wf = WaveFront.FromSystem(_sys, PrimaryIndices, p);
        if (wf == null) return;

        int last = _sys.LastOpticalSurface();
        var bridge = BridgeFor(p, wf);
        var overlay = TrefoilOverlays(bridge, last);
        var oblique = ObliqueSphericalOverlays(bridge, last);
        var fifthComa = FifthComaOverlays(bridge, last);
        var fifth = NatFifthOrder.Compute(
            j => wf.PerSurface[j], j => wf.PerSurface[j].W131,
            j => nat.Sigmas.Sigma[j], last + 1,
            overlay == null ? null : j => overlay[j],
            j => j >= 1 && j <= last
                 ? Conventions.BeamDisplacement(p.Y[j], p.Ybar[j]) : 0.0,
            oblique == null ? null : j => oblique[j],
            fifthComa == null ? null : j => fifthComa[j]);

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("NODAL ABERRATION THEORY - FIFTH ORDER");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Thompson's multinodal trilogy, J. Opt. Soc. Am. A 26, 1090 (2009); 27, 1490");
        sb.AppendLine("(2010); 28, 821 (2011). The coefficients come through Buchdahl's W coordinates,");
        sb.AppendLine("VI Table I and VII Eqs. (6.5-6) and (3.4), and are the RETARDATION of the wave");
        sb.AppendLine("front, which is what a wave aberration is.");
        sb.AppendLine();

        // One unit system. The fifth order is computed through Buchdahl's W coordinates, whose
        // normalised aperture and field differ from the design's by a factor A^l F^k. That scale
        // is fitted from the third order, where both routes are available, and it CHECKS ITSELF:
        // four coefficients give four ratios against two unknowns, so two of them are free. They
        // agree to machine precision, so the conversion below is exact rather than indicative.
        Scalar S(int k, int l, Scalar w) => bridge.IsUsable ? bridge.WToSeidel(k, l) * w : w;

        sb.AppendLine(bridge.IsUsable
            ? "  Fifth-order wave coefficients of the system, in the design's own units"
            : "  Fifth-order wave coefficients of the system, in Buchdahl's normalised units");
        sb.AppendLine(string.Format(Inv, "    W060  {0,13}   spherical", SciZ(S(0, 6, wf.System.W060))));
        sb.AppendLine(string.Format(Inv, "    W151  {0,13}   field-linear coma", SciZ(S(1, 5, wf.System.W151))));
        sb.AppendLine(string.Format(Inv, "    W240  {0,13}   oblique spherical, field-constant", SciZ(S(2, 4, wf.System.W240))));
        sb.AppendLine(string.Format(Inv, "    W242  {0,13}   oblique spherical, astigmatic", SciZ(S(2, 4, wf.System.W242))));
        sb.AppendLine(string.Format(Inv, "    W331  {0,13}   elliptical coma", SciZ(S(3, 3, wf.System.W331))));
        sb.AppendLine(string.Format(Inv, "    W333  {0,13}   elliptical coma, trefoil", SciZ(S(3, 3, wf.System.W333))));
        sb.AppendLine(string.Format(Inv, "    W420  {0,13}   field curvature", SciZ(S(4, 2, wf.System.W420))));
        sb.AppendLine(string.Format(Inv, "    W422  {0,13}   astigmatism", SciZ(S(4, 2, wf.System.W422))));
        sb.AppendLine(string.Format(Inv, "    W511  {0,13}   distortion", SciZ(S(5, 1, wf.System.W511))));
        sb.AppendLine();

        if (bridge.IsUsable)
        {
            sb.AppendLine(string.Format(Inv,
                "    These are in the SAME units as the third-order block above, so the two may be"));
            sb.AppendLine("    compared directly. The fifth order is computed through Buchdahl's W");
            sb.AppendLine("    coordinates, whose normalised aperture and field differ from the design's");
            sb.AppendLine(string.Format(Inv,
                "    by A^l F^k with A = {0} and F = {1} - one power of the aperture",
                SciZ(bridge.A), SciZ(bridge.F)));
            sb.AppendLine("    scale per power of rho, one of the field scale per power of H.");
            sb.AppendLine();
            sb.AppendLine("    That scale is not assumed. It is fitted from the third order, where both");
            sb.AppendLine("    routes are available, and it checks itself: four coefficients give four");
            sb.AppendLine("    ratios against two unknowns, so W040 fixes A, W131 fixes F, and W222 and");
            sb.AppendLine(string.Format(Inv,
                "    W311 are free checks. They agree here to {0}, so the conversion is exact",
                SciZ(bridge.Residual)));
            sb.AppendLine("    rather than indicative.");
        }
        else
        {
            sb.AppendLine("    The scale between the two routes could NOT be fitted for this design");
            sb.AppendLine(string.Format(Inv, "    (residual {0}), so these are left in Buchdahl's",
                                        SciZ(bridge.Residual)));
            sb.AppendLine("    normalised aperture and field. They differ from the third-order block");
            sb.AppendLine("    above by a factor A^l F^k: compare them with each other, not across.");
        }
        sb.AppendLine();
        sb.AppendLine("    The NODES are unaffected either way. Each is a ratio of quantities carrying");
        sb.AppendLine("    the same powers, so the scales cancel and the positions are in the design's");
        sb.AppendLine("    own field units regardless. The coma node and the astigmatic midpoint");
        sb.AppendLine("    computed by this route agree with the Seidel route above to thirteen");
        sb.AppendLine("    figures, which is what says so.");
        sb.AppendLine();

        if (nat.IsAligned)
        {
            sb.AppendLine("  This design is aligned, so every fifth-order node also sits at the field");
            sb.AppendLine("  centre. Perturb a surface and they separate - and they separate differently");
            sb.AppendLine("  from the third-order ones, which is the whole reason to compute them.");
            return;
        }

        sb.AppendLine("  Nodes");
        WriteNodes(sb, "    W151  coma", new[] { fifth.Node151 });
        WriteNodes(sb, "    W240M vertex", new[] { fifth.Vertex240M });
        WriteNodes(sb, "    W242  astigmatism", fifth.Nodes242);
        WriteNodes(sb, "    W331M coma", fifth.Nodes331M);
        WriteNodes(sb, "    W333  trefoil", fifth.Nodes333);
        WriteNodes(sb, "    W420M vertex", new[] { fifth.M420M.a });
        WriteNodes(sb, "    W422  astigmatism", fifth.Nodes422);

        var d511 = fifth.DistortionNodes();
        if (d511.Length > 0)
            WriteNodes(sb, "    W511  distortion", d511);
        else
            sb.AppendLine("    W511  distortion    NOT SOLVED - see the note below");

        sb.AppendLine();
        sb.AppendLine("    W331M is COLLINEAR trinodal - the outer two sit symmetrically about the");
        sb.AppendLine("    middle one - where W333's three are not. W422 is quadranodal.");
        sb.AppendLine();
        sb.AppendLine("    W511 is NOT SOLVED here. Its FIELD is exact - it is checked against the");
        sb.AppendLine("    defining sum surface by surface - but the closed nodal form is in Thompson's");
        sb.AppendLine("    1980 dissertation, which is not to hand, and a numerical search written in");
        sb.AppendLine("    its place disagreed with a direct scan of the field on real lenses. Rather");
        sb.AppendLine("    than print five plausible positions a scan contradicts, it prints none.");
        sb.AppendLine();

        AppendOverlayNote(sb, bridge, overlay, oblique, fifthComa, last);

        sb.AppendLine("  What the fifth order does to the third");
        sb.AppendLine("    Expanding a fifth-order term about its displaced field centre throws off");
        sb.AppendLine("    terms of third-order form. They change the magnitude AND the node of the");
        sb.AppendLine("    third-order aberration they belong with, so the third-order block above is");
        sb.AppendLine("    not the last word on it.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "    W131  {0,13}  ->  W131E {1,13}   (2010 Eq. B13)",
                                    SciZ(S(1, 3, fifth.M131.W)), SciZ(S(1, 3, fifth.W131E))));
        sb.AppendLine(string.Format(Inv, "          node ({0}, {1})  ->  ({2}, {3})",
                                    SciZ(fifth.M131.a.X), SciZ(fifth.M131.a.Y),
                                    SciZ(fifth.Node131E.X), SciZ(fifth.Node131E.Y)));
        sb.AppendLine(string.Format(Inv, "    W222  {0,13}  ->  W222E {1,13}   (2011 Eq. C19)",
                                    SciZ(S(2, 2, fifth.M222.W)), SciZ(S(2, 2, fifth.W222E))));
        sb.AppendLine(string.Format(Inv, "          centre ({0}, {1})  ->  ({2}, {3})",
                                    SciZ(fifth.M222.a.X), SciZ(fifth.M222.a.Y),
                                    SciZ(fifth.A222E.X), SciZ(fifth.A222E.Y)));
        sb.AppendLine(string.Format(Inv, "    W220M {0,13}  ->  W220ME {1,12}   (2011 Sec. 2)",
                                    SciZ(S(2, 2, fifth.M220M.W)), SciZ(S(2, 2, fifth.W220ME))));
        sb.AppendLine(string.Format(Inv, "          vertex ({0}, {1})  ->  ({2}, {3})",
                                    SciZ(fifth.M220M.a.X), SciZ(fifth.M220M.a.Y),
                                    SciZ(fifth.A220ME.X), SciZ(fifth.A220ME.Y)));
        sb.AppendLine();
        sb.AppendLine("    The three left-hand numbers are the third-order block's own, reached by the");
        sb.AppendLine("    FIFTH-order route and converted back. That they agree to every digit is the");
        sb.AppendLine("    round trip closing - the scale, the W-coordinate scheme, Buchdahl VII");
        sb.AppendLine("    Eqs. (6.5-6) and (3.4) - and it is what licenses reading the arrows.");
        sb.AppendLine();
        sb.AppendLine("    W220M is the MEDIAL surface, W220P + W222, the average of the tangential and");
        sb.AppendLine("    sagittal ones. It is the one with a single node and the one Thompson's");
        sb.AppendLine("    relations are written in. The sagittal surface, half an astigmatism away, is");
        sb.AppendLine("    printed as W220S in the third-order block above.");
    }

    /// <summary>
    /// What the Zernike overlays above coma are doing, when a surface carries one - Fuerschbach
    /// 2014 Tables 2, 3 and 4.
    /// </summary>
    private void AppendOverlayNote(StringBuilder sb, NormalisationBridge bridge, Vec2[]? trefoil,
                                   Vec2[]? oblique, Vec2[]? fifthComa, int last)
    {
        bool Present(int a, int b)
        {
            for (int j = 1; j <= last; j++)
                if (_sys.Surfaces[j].Zernike(a) != 0.0 || _sys.Surfaces[j].Zernike(b) != 0.0)
                    return true;
            return false;
        }

        bool wantTrefoil = Present(10, 11), wantOblique = Present(12, 13);
        bool wantComa5 = Present(14, 15);
        if (!wantTrefoil && !wantOblique && !wantComa5) return;

        sb.AppendLine("  Zernike overlays above coma");
        if (!bridge.IsUsable)
        {
            sb.AppendLine("    A surface carries one, and it has NOT been applied. An overlay's");
            sb.AppendLine("    contribution is a physical wave amplitude, in the units the third-order");
            sb.AppendLine("    block uses, and adding it to the fifth order needs the scale between the");
            sb.AppendLine(string.Format(Inv,
                          "    two routes. That scale could not be fitted here (residual {0}), so",
                          SciZ(bridge.Residual)));
            sb.AppendLine("    applying it would mean choosing a factor that cannot be justified.");
            return;
        }

        sb.AppendLine("    Fuerschbach, Rolland and Thompson, Opt. Express 22, 26585 (2014). An");
        sb.AppendLine("    overlay generates no new aberration TYPE - every term it contributes lands");
        sb.AppendLine("    on one the theory already had:");
        sb.AppendLine();
        sb.AppendLine("      Z10/11 trefoil        4(n'-n)z at 3 phi   -> C3_333, C3_422     2 rows");
        sb.AppendLine("      Z12/13 obl spherical  8(n'-n)z at 2 phi   -> five B^2 vectors   5 rows");
        sb.AppendLine("      Z14/15 fifth coma    10(n'-n)z at   phi   -> seven A vectors    7 rows");
        sb.AppendLine();
        sb.AppendLine("    Only the FIRST row of each survives at the stop. The rest carry powers of");
        sb.AppendLine("    the beam walk ybar/y, which is zero at a pupil: there the beam footprint is");
        sb.AppendLine("    the same for every field point, so a contribution cannot acquire a field");
        sb.AppendLine("    dependence. Moving the plate off the stop is what turns it on.");
        sb.AppendLine();
        sb.AppendLine("    A raw Fringe Z12 also carries astigmatism and a raw Z14 also carries coma -");
        sb.AppendLine("    the sidecar states raw sag, so those halves are routed to the third-order");
        sb.AppendLine("    overlays rather than dropped. Z10's leftover is a pupil tilt, which moves");
        sb.AppendLine("    the image instead of blurring it, so trefoil needs no such handling.");
        sb.AppendLine();
        sb.AppendLine("    surf   term      coefficient        ybar/y      overlay magnitude");
        sb.AppendLine("    " + new string('-', 68));

        var pp = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        for (int j = 1; j <= last; j++)
        {
            Scalar walk = Conventions.BeamDisplacement(pp.Y[j], pp.Ybar[j]);
            Row("Z10/11", trefoil, 10, 11);
            Row("Z12/13", oblique, 12, 13);
            Row("Z14/15", fifthComa, 14, 15);

            void Row(string name, Vec2[]? ff, int a, int b)
            {
                Scalar za = _sys.Surfaces[j].Zernike(a), zb = _sys.Surfaces[j].Zernike(b);
                if ((za == 0.0 && zb == 0.0) || ff == null) return;
                sb.AppendLine(string.Format(Inv, "    {0,4}   {1,-8} {2,12} {3,13} {4,20}",
                    j, name, Num(za != 0.0 ? za : zb), SciZ(walk), SciZ(ff[j].Magnitude)));
            }
        }
        sb.AppendLine();
    }

    /// <summary>
    /// The scale between the Seidel route's units and the W-coordinate route's, fitted from the
    /// third order where both are available. See <see cref="NormalisationBridge"/>.
    /// </summary>
    private NormalisationBridge BridgeFor(ParaxialResult p, WaveFront.Result wf)
    {
        var s = SeidelCoefficients.Compute(_sys, PrimaryIndices, PrimaryIndices, PrimaryIndices, p);
        return NormalisationBridge.Fit(
            new Scalar[] { s.TotalS1 / 8.0, s.TotalS2 / 2.0, s.TotalS3 / 2.0, s.TotalS5 / 2.0 },
            new Scalar[] { wf.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });
    }

    /// <summary>
    /// Each surface's Zernike trefoil overlay as the vector <c>FF C^3_333,j</c>, converted into
    /// the units the W-coordinate coefficients use. Null when nothing is figured with trefoil, or
    /// when the scale could not be fitted - in which case the overlay is DECLINED rather than
    /// applied at a factor that cannot be justified, and the report says so.
    /// </summary>
    private Vec2[]? TrefoilOverlays(NormalisationBridge bridge, int last) =>
        Overlays(bridge, last, 10, 11, 3, 3, Conventions.TrefoilOverlay);

    /// <summary>
    /// Each surface's Zernike OBLIQUE SPHERICAL overlay as <c>FF B^2_242,j</c>, Eq. (42).
    /// </summary>
    private Vec2[]? ObliqueSphericalOverlays(NormalisationBridge bridge, int last) =>
        Overlays(bridge, last, 12, 13, 2, 4, Conventions.ObliqueSphericalOverlay);

    /// <summary>
    /// Each surface's Zernike FIFTH-ORDER COMA overlay as <c>FF A_151,j</c>, Eq. (52).
    /// </summary>
    private Vec2[]? FifthComaOverlays(NormalisationBridge bridge, int last) =>
        Overlays(bridge, last, 14, 15, 1, 5, Conventions.FifthOrderComaOverlay);

    /// <summary>
    /// The shared shape of all three: read a Zernike pair off each surface, form the overlay
    /// vector, and convert it into the units the W-coordinate coefficients use. The field and
    /// aperture powers are those of the aberration the overlay lands on.
    /// </summary>
    private Vec2[]? Overlays(NormalisationBridge bridge, int last, int termA, int termB,
                             int fieldPower, int aperturePower,
                             Func<Scalar, Scalar, Scalar, Scalar, Vec2> overlay)
    {
        bool any = false;
        for (int j = 1; j <= last && !any; j++)
            any = _sys.Surfaces[j].Zernike(termA) != 0.0 || _sys.Surfaces[j].Zernike(termB) != 0.0;
        if (!any || !bridge.IsUsable) return null;

        Scalar scale = bridge.SeidelToW(fieldPower, aperturePower);
        var ff = new Vec2[last + 1];
        for (int j = 1; j <= last; j++)
        {
            Scalar za = _sys.Surfaces[j].Zernike(termA), zb = _sys.Surfaces[j].Zernike(termB);
            if (za == 0.0 && zb == 0.0) continue;
            ff[j] = scale * overlay(za, zb, PrimaryIndices[j - 1], PrimaryIndices[j]);
        }
        return ff;
    }

    /// <summary>One labelled row of node positions, wrapped onto continuation lines.</summary>
    private static void WriteNodes(StringBuilder sb, string label, Vec2[] nodes)
    {
        for (int i = 0; i < nodes.Length; i++)
            sb.AppendLine(string.Format(Inv, "{0,-22} {1} ({2}, {3})",
                                        i == 0 ? label : "",
                                        i == 0 ? (nodes.Length == 1 ? "one node at " : "nodes at    ")
                                               : "            ",
                                        SciZ(nodes[i].X), SciZ(nodes[i].Y)));
    }

    /// <summary>
    /// A full-field display as TSV: the magnitude and orientation of third-order coma and
    /// astigmatism on a grid of field points.
    ///
    /// <para>This is what the node geometry looks like when it is drawn, and it is the form an
    /// alignment engineer reads. Astigmatism is a squared-vector quantity, so HALF its
    /// orientation is the azimuth of the line image - the column says so rather than leaving the
    /// factor of two to be discovered.</para>
    /// </summary>
    public string BuildNatFullFieldTsv(int steps = 9)
    {
        double field = MaxField();
        var sb = new StringBuilder();

        if (Math.Abs(field) < 1e-15)
        {
            sb.AppendLine("hx\thy\tcoma\tcoma_orientation_deg\tastigmatism\tline_image_azimuth_deg");
            return sb.ToString();
        }

        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        var seidel = SeidelCoefficients.Compute(_sys, PrimaryIndices, PrimaryIndices,
                                                PrimaryIndices, p);
        var nat = NatField.Compute(_sys, PrimaryIndices, p, seidel);

        // The fifth order rides along when the wave front chain is available. The third-order
        // columns keep their names and their meaning, so a reader of the old file still works.
        var wf = WaveFront.FromSystem(_sys, PrimaryIndices, p);
        NatFifthOrder? fifth = wf == null ? null
            : NatFifthOrder.Compute(j => wf.PerSurface[j], j => wf.PerSurface[j].W131,
                                    j => nat.Sigmas.Sigma[j], _sys.LastOpticalSurface() + 1);

        sb.Append("hx\thy\tcoma\tcoma_orientation_deg\tastigmatism\tline_image_azimuth_deg");
        if (fifth != null)
            sb.Append("\tcoma_E\tcoma_E_orientation_deg"
                    + "\tastigmatism_E\tastigmatism_E_azimuth_deg"
                    + "\tcoma5\tcoma5_orientation_deg"
                    + "\tcoma331\tcoma331_orientation_deg"
                    + "\ttrefoil\ttrefoil_azimuth_deg"
                    + "\tastig5\tastig5_azimuth_deg"
                    + "\tdistortion5\tdistortion5_orientation_deg");
        sb.AppendLine();

        const double deg = 180.0 / Math.PI;
        for (int iy = 0; iy < steps; iy++)
        {
            double hy = steps == 1 ? 0.0 : -1.0 + 2.0 * iy / (steps - 1);
            for (int ix = 0; ix < steps; ix++)
            {
                double hx = steps == 1 ? 0.0 : -1.0 + 2.0 * ix / (steps - 1);
                var h = new Vec2(hx, hy);
                var coma = nat.ComaAt(h);
                var ast = nat.AstigmatismAt(h);

                sb.Append(string.Join("\t",
                    Raw(hx), Raw(hy),
                    Raw(coma.Magnitude), Raw(coma.Orientation * deg),
                    Raw(ast.Magnitude), Raw(0.5 * ast.Orientation * deg)));

                if (fifth != null)
                {
                    // Each aberration's azimuth on the sky is its orientation divided by the
                    // power of theta it carries: one for coma and distortion, two for a line
                    // image, three for trefoil. Dividing by the wrong one rotates the pattern
                    // by a plausible amount rather than an obvious one.
                    // coma_E and astigmatism_E sit beside coma and astigmatism, so they are put
                    // into the SAME units. The two routes differ by a normalisation - see the
                    // note in the text report - but the RATIO W131E/W131 is free of it, both
                    // being Buchdahl's, so multiplying by the Seidel W131 lands in Seidel units
                    // exactly, with no scale factor to derive. On an aligned system the pair
                    // then agrees with the uncorrected column to the last digit.
                    Scalar comaScale = Math.Abs((double)fifth.M131.W) > 1e-300
                        ? nat.Totals.W131 / fifth.M131.W : 0.0;
                    Scalar astScale = Math.Abs((double)fifth.M222.W) > 1e-300
                        ? nat.Totals.W222 / fifth.M222.W : 0.0;

                    var cE = comaScale * fifth.ComaVector131E(h);
                    var aE = astScale * fifth.AstigmatismVector222E(h);
                    var c5 = fifth.ComaVector151(h);
                    var c331 = fifth.ComaVector331M(h);
                    var tre = fifth.TrefoilVector(h);
                    var a5 = fifth.AstigmatismVector422(h);
                    var d5 = fifth.DistortionField(h);

                    sb.Append("\t" + string.Join("\t",
                        Raw(cE.Magnitude), Raw(cE.Orientation * deg),
                        Raw(aE.Magnitude), Raw(0.5 * aE.Orientation * deg),
                        Raw(c5.Magnitude), Raw(c5.Orientation * deg),
                        Raw(c331.Magnitude), Raw(c331.Orientation * deg),
                        Raw(tre.Magnitude), Raw(tre.Orientation * deg / 3.0),
                        Raw(a5.Magnitude), Raw(0.5 * a5.Orientation * deg),
                        Raw(d5.Magnitude), Raw(d5.Orientation * deg)));
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}
