using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Reads .AGF glass catalogs and resolves names to <see cref="GlassData"/>.
///
/// AGF is a line-oriented text format. Only the records needed to evaluate and print an
/// index are read:
///
///   NM  name  formula  MIL  nd  vd  exclude  status  melt
///   CD  c1 … c10                       dispersion coefficients
///   LD  lambda_min lambda_max          validity range, micrometres
///   GC  free text                      comment
///
/// Anything else (TD thermal, OD cost, IT transmission) is skipped. A malformed line is
/// skipped rather than aborting the catalog: one bad entry in a vendor file should not cost
/// you the other six hundred glasses in it.
/// </summary>
public class GlassCatalog
{
    // Keyed "CATALOG:NAME" so two vendors can ship the same name without collision.
    private readonly Dictionary<string, GlassData> _byQualifiedName =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _catalogs = new();

    /// <summary>Catalog names loaded, in load order.</summary>
    public IReadOnlyList<string> Catalogs => _catalogs;

    public int Count => _byQualifiedName.Count;

    /// <summary>Loads every .agf in a folder. Missing folder is not an error.</summary>
    public void LoadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        // Enumerate and filter by extension rather than globbing "*.agf": Linux filesystems
        // are case-sensitive and vendor files ship as .AGF as often as .agf.
        foreach (var file in Directory.GetFiles(folder))
            if (Path.GetExtension(file).Equals(".agf", StringComparison.OrdinalIgnoreCase))
                LoadFile(file);
    }

    /// <summary>Loads one .agf file. The catalog name is the file name without extension.</summary>
    public void LoadFile(string path)
    {
        string catalog = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        GlassData? current = null;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;

            string tag = line.Substring(0, 2).ToUpperInvariant();
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            switch (tag)
            {
                case "NM":
                {
                    current = null;
                    if (parts.Length < 3) break;

                    var g = new GlassData { Name = parts[1], Catalog = catalog };
                    if (TryNum(parts, 2, out double formula))
                        g.Formula = (DispersionFormula)(int)formula;
                    if (TryNum(parts, 4, out double nd)) g.Nd = nd;
                    if (TryNum(parts, 5, out double vd)) g.Vd = vd;

                    _byQualifiedName[catalog + ":" + g.Name] = g;
                    current = g;
                    break;
                }

                case "CD":
                {
                    if (current == null) break;
                    var c = new List<double>();
                    for (int i = 1; i < parts.Length; i++)
                        c.Add(TryNum(parts, i, out double v) ? v : 0.0);
                    current.Coefficients = c.ToArray();
                    break;
                }

                case "LD":
                {
                    if (current == null) break;
                    if (TryNum(parts, 1, out double lo)) current.LambdaMin = lo;
                    if (TryNum(parts, 2, out double hi)) current.LambdaMax = hi;
                    break;
                }

                case "GC":
                {
                    if (current == null) break;
                    current.Comment = line.Length > 2 ? line.Substring(2).Trim() : null;
                    break;
                }
            }
        }

        foreach (var g in InCatalog(catalog))
        {
            var note = ReconcileListed(g);
            if (note != null && !_listedValueCorrections.Contains(note)) _listedValueCorrections.Add(note);
        }

        if (!_catalogs.Contains(catalog)) _catalogs.Add(catalog);
    }

    private readonly List<string> _listedValueCorrections = new();

    /// <summary>
    /// Glasses whose listed nd/Vd disagreed with their own dispersion data, and were given the
    /// data's values on loading. One note each.
    /// </summary>
    public IReadOnlyList<string> ListedValueCorrections => _listedValueCorrections;

    /// <summary>
    /// A catalog lists each glass's nd and Vd, and separately gives the dispersion data the
    /// index is computed from, and the two can disagree. For example:
    /// <list type="bullet">
    /// <item>CDGM's H-TK9 lists 1.587166 / 75.90, another glass's values, while its data and its
    /// MIL code 621603 give 1.620750 / 60.30;</item>
    /// <item>HOYA's MC-TAF115 lists 1.777047 for data giving 1.770473.</item>
    /// </list>
    /// Where the data covers the d, F and C lines and differs by more than 1e-3 in nd or 0.5 in
    /// Vd, the data wins. That passes rounding (Sumita lists Vd to one decimal) and catches a
    /// listing that belongs to another glass. A glass whose data does not reach the visible, an
    /// infrared or laser-line material, is left as listed.
    /// </summary>
    private static string? ReconcileListed(GlassData g)
    {
        if (g.LambdaMax <= 0 || g.LambdaMin > IndexResolver.LambdaF + 1e-6 || g.LambdaMax < IndexResolver.LambdaC - 1e-6)
            return null;
        double nd = g.IndexAt(IndexResolver.LambdaD);
        double dn = g.IndexAt(IndexResolver.LambdaF) - g.IndexAt(IndexResolver.LambdaC);
        if (!(nd > 1.0) || !(Math.Abs(dn) > 1e-12)) return null;
        double vd = (nd - 1.0) / dn;
        if (double.IsNaN(vd) || double.IsInfinity(vd) || vd <= 0) return null;
        if (Math.Abs(nd - g.Nd) <= 1e-3 && Math.Abs(vd - g.Vd) <= 0.5) return null;

        string note = string.Format(CultureInfo.InvariantCulture,
            "{0}:{1}: listed nd {2:F6} / Vd {3:F2}, its dispersion data gives nd {4:F6} / Vd {5:F2}; using the data.",
            g.Catalog, g.Name, g.Nd, g.Vd, nd, vd);
        g.Nd = nd;
        g.Vd = vd;
        return note;
    }

    /// <summary>
    /// Finds a glass by name. Accepts "CATALOG:NAME" for an exact hit; a bare name is looked
    /// up in <paramref name="preferred"/> order first, then across every loaded catalog.
    /// Returns null when nothing matches.
    /// </summary>
    public GlassData? Find(string? name, IReadOnlyList<string>? preferred = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (_byQualifiedName.TryGetValue(name!, out var exact)) return exact;

        if (preferred != null)
            foreach (var cat in preferred)
                if (_byQualifiedName.TryGetValue(cat.ToUpperInvariant() + ":" + name, out var p))
                    return p;

        foreach (var cat in FallbackPreference)
            if (_byQualifiedName.TryGetValue(cat.ToUpperInvariant() + ":" + name, out var d))
                return d;

        foreach (var cat in _catalogs)
            if (_byQualifiedName.TryGetValue(cat + ":" + name, out var any))
                return any;

        return null;
    }

    /// <summary>
    /// Which catalogue wins a bare name that no file claimed, before load order decides it.
    ///
    /// <para><b>Load order is alphabetical, which made CDGM the authority on every name it
    /// shares.</b> Nothing recommends CDGM for that job, and the consequence is not a slightly
    /// different glass. Its F series is RENUMBERED against Schott's, so the wrong catalogue hands
    /// back a neighbour from the same family - CDGM's F3 is 1.616592, which is Schott's F4 exactly,
    /// and CDGM's F4 is 1.620047, which is Schott's F2 to five decimals. Nine names collide and
    /// every one of them differs, from 0.21 per cent to 1.43. A glass that is wrong by a
    /// neighbouring catalogue number is the hardest kind to notice.</para>
    ///
    /// <para><b>Schott is the defensible default</b> for the files that name no catalogue at all:
    /// they are overwhelmingly classical designs from a literature written in Schott glasses, and
    /// it is what OpticStudio resolves them to - checked on Kingslake's double Gauss, where the
    /// indices recovered from OpticStudio's own paraxial data are Schott's to six figures.</para>
    ///
    /// <para><b>This does not make the answer certain, and the warning still fires.</b> The file
    /// still did not say, <see cref="CatalogsContaining"/> still reports that it could not have
    /// known, and the report still prints which catalogue was used. A better guess is not a
    /// substitute for saying it was a guess.</para>
    /// </summary>
    public static IReadOnlyList<string> FallbackPreference { get; set; } = new[] { "SCHOTT" };

    /// <summary>
    /// Every loaded catalog that has a glass of this name.
    ///
    /// <para><b>A glass name does not say whose glass it is.</b> Three catalogs ship an F4 and
    /// they are not the same glass - 1.620047, 1.616592 and 1.616590 at the d line. A file that
    /// names a catalog resolves unambiguously; several formats do not carry one at all, and then
    /// <see cref="Find"/> returns whichever catalog happens to have loaded first. That is a
    /// defensible answer and an undetectable wrong one, so this lets a caller say when the
    /// question had more than one.</para>
    /// </summary>
    public IReadOnlyList<string> CatalogsContaining(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Array.Empty<string>();

        var found = new List<string>();
        foreach (var cat in _catalogs)
            if (_byQualifiedName.ContainsKey(cat + ":" + name)) found.Add(cat);

        return found;
    }

    /// <summary>Every glass in one catalog.</summary>
    public IEnumerable<GlassData> InCatalog(string catalog)
    {
        string prefix = catalog.ToUpperInvariant() + ":";
        foreach (var kv in _byQualifiedName)
            if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return kv.Value;
    }

    // ── Names the file readers use ───────────────────────────────────────────────

    /// <summary>Catalogs currently loaded, in load order.</summary>
    public IReadOnlyList<string> LoadedCatalogs => _catalogs;

    /// <summary>
    /// Looks a glass up by name, optionally qualified "CATALOG:NAME". Returns null when
    /// no loaded catalog has it — the caller decides whether that is fatal.
    /// </summary>
    public GlassData? GetGlass(string? name) => Find(name);

    /// <summary>Every glass in one catalog.</summary>
    public IEnumerable<GlassData> GetGlassesInCatalog(string catalog) => InCatalog(catalog);

    private static bool TryNum(string[] parts, int i, out double value)
    {
        value = 0.0;
        return i < parts.Length
            && double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
