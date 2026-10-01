using System;
using System.Collections.Generic;
using System.Text;
using AberrationCalculator.Core.Glass;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Glass-name translation between the .agf catalogs and Code V.
    ///
    /// Two things travel with a glass in a .seq file: the name, and the catalog
    /// it came from. Code V writes them as <c>NAME_CATALOG</c>.
    ///
    /// The name loses its punctuation. The catalogs' <c>N-BK7</c>, <c>S-FPL51</c>
    /// and <c>H-ZF52</c> are <c>NBK7</c>, <c>SFPL51</c> and <c>HZF52</c> there.
    /// This is a general rule, not a Schott one: of the 1515 glasses in
    /// catalogs/Glass, 754 contain punctuation and only 115 carry the Schott
    /// N-prefix.
    ///
    /// The catalog is not decoration either. A bare name does not always
    /// identify a glass -- <c>SK16</c> is in both SCHOTT and SUMITA with the
    /// same n_d but different dispersion formulas, so which one a bare name
    /// binds to depends on the order catalogs happen to be searched in.
    ///
    /// Restoring the punctuation on import is not a string operation. Hoya ships
    /// 28 real names of the form <c>NBF1</c>, <c>NBFD10</c>, <c>NBFD265</c> that
    /// must survive untouched, while <c>NBK7</c> has to become <c>N-BK7</c>;
    /// nothing in the spelling separates them. <see cref="CodeVGlassResolver"/>
    /// looks the name up instead of transforming it.
    /// </summary>
    internal static class CodeVGlassNames
    {
        /// <summary>
        /// Catalog name to Code V name: drop every character Code V does not
        /// accept in a glass name. The underscore goes too, because Code V
        /// reads it as the separator in <c>GLASS_CATALOG</c> -- left in place,
        /// Corning's <c>HPFS_7980</c> would be taken as glass HPFS from a
        /// catalog named 7980.
        /// </summary>
        public static string ToCodeV(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
            }

            // A name that is nothing but punctuation cannot be helped, and an
            // empty material would corrupt the surface line. Pass the original
            // through and let Code V report it.
            return sb.Length > 0 ? sb.ToString() : name;
        }

        /// <summary>
        /// The vendor catalogs Code V ships in its own GLASS folder, and so the
        /// only ones a <c>GLASS_CATALOG</c> qualifier may name. Naming a catalog
        /// Code V does not have would make the material unresolvable there,
        /// which is worse than the ambiguity the qualifier removes -- so side
        /// catalogs such as MISC, PATENTMODEL and LIGHTPATH, and any custom
        /// catalog of the user's, are written bare.
        /// </summary>
        public static readonly string[] CodeVCatalogs =
            { "HOYA", "OHARA", "SCHOTT", "CDGM", "SUMITA", "HIKARI", "CORNING" };

        public static bool IsCodeVCatalog(string? catalog)
        {
            if (string.IsNullOrEmpty(catalog)) return false;
            foreach (var c in CodeVCatalogs)
            {
                if (c.Equals(catalog, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// One of our catalog names to the Code V catalog that holds the same
        /// glasses, or null when Code V has no such catalog and the name must
        /// therefore be written bare.
        /// </summary>
        public static string? ToCodeVCatalog(string? catalog)
        {
            if (string.IsNullOrEmpty(catalog)) return null;

            // We split Corning across CORNING_B (borosilicate) and CORNING_FS
            // (fused silica); Code V keeps them in one CORNING catalog.
            if (catalog!.StartsWith("CORNING", StringComparison.OrdinalIgnoreCase))
                return "CORNING";

            return IsCodeVCatalog(catalog) ? catalog.ToUpperInvariant() : null;
        }

        /// <summary>
        /// The pre-1.0.153 import rule, kept only for the path where no catalog
        /// manager is available: assume a leading N before an uppercase letter
        /// is a de-punctuated Schott N-prefix. Right for <c>NBK7</c>, wrong for
        /// Hoya's <c>NBFD10</c>, and without a catalog there is no way to tell.
        /// </summary>
        public static string LegacyNPrefix(string name)
        {
            if (name.Length >= 2 && name[0] == 'N' && char.IsUpper(name[1]))
                return "N-" + name.Substring(1);
            return name;
        }
    }

    /// <summary>
    /// Resolves a Code V material token back to a catalog glass name, by looking
    /// it up rather than by transforming it, and reports which catalogs the file
    /// named so the caller can record them on the system. Built once per read.
    /// </summary>
    internal sealed class CodeVGlassResolver
    {
        private readonly GlassCatalog? _mgr;

        // Stripped Code V name -> the catalog name it came from, globally and
        // per loaded catalog. The per-catalog map is what lets a qualifier pick
        // Sumita P-SK50 over Schott PSK50.
        private readonly Dictionary<string, (string Name, string Catalog)> _byStripped;
        private readonly Dictionary<string, Dictionary<string, string>> _byCatalogStripped;

        // Tokens that may legitimately follow the underscore. Anything else is
        // part of the glass name, not a catalog.
        private readonly HashSet<string> _catalogTokens;

        private readonly List<string> _seenCatalogs = new List<string>();

        /// <summary>Catalogs this file bound glasses to, in order of first use.</summary>
        public IReadOnlyList<string> SeenCatalogs => _seenCatalogs;

        public CodeVGlassResolver(GlassCatalog? mgr)
        {
            _mgr = mgr;
            _byStripped = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            _byCatalogStripped = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _catalogTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in CodeVGlassNames.CodeVCatalogs) _catalogTokens.Add(c);
            if (mgr == null) return;

            foreach (var catalog in mgr.LoadedCatalogs)
            {
                _catalogTokens.Add(catalog);

                var perCatalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var glass in mgr.GetGlassesInCatalog(catalog))
                {
                    if (string.IsNullOrEmpty(glass.Name)) continue;
                    var stripped = CodeVGlassNames.ToCodeV(glass.Name);

                    if (!perCatalog.ContainsKey(stripped))
                        perCatalog[stripped] = glass.Name;

                    // First catalog in load order wins a contested stripped
                    // form; a qualifier overrides it before this is consulted.
                    if (!_byStripped.ContainsKey(stripped))
                        _byStripped[stripped] = (glass.Name, catalog);
                }
                _byCatalogStripped[catalog] = perCatalog;
            }
        }

        private void Record(string? catalog)
        {
            if (string.IsNullOrEmpty(catalog)) return;
            foreach (var c in _seenCatalogs)
            {
                if (c.Equals(catalog, StringComparison.OrdinalIgnoreCase)) return;
            }
            _seenCatalogs.Add(catalog!);
        }

        // The loaded catalogs a file's catalog token refers to. Usually itself;
        // a Code V catalog we split into several (CORNING) expands to all of
        // them, so CORNING finds glasses in CORNING_B and CORNING_FS.
        private IEnumerable<string> LoadedCatalogsFor(string catalog)
        {
            if (_mgr == null) yield break;

            foreach (var loaded in _mgr.LoadedCatalogs)
            {
                if (loaded.Equals(catalog, StringComparison.OrdinalIgnoreCase)) { yield return loaded; yield break; }
            }
            foreach (var loaded in _mgr.LoadedCatalogs)
            {
                var mapped = CodeVGlassNames.ToCodeVCatalog(loaded);
                if (mapped != null && mapped.Equals(catalog, StringComparison.OrdinalIgnoreCase))
                    yield return loaded;
            }
        }

        /// <summary>
        /// Translate one Code V material token to a catalog glass name. Returns
        /// the token unchanged when no loaded catalog claims it.
        /// </summary>
        public string Resolve(string material)
        {
            if (string.IsNullOrEmpty(material)) return material;

            // Split only when what follows the underscore actually names a
            // catalog. A mould-stress extension writes glasses called
            // MS_PMMA and MS_POLYSTYR; splitting those on the first underscore
            // would invent a glass MS in a catalog PMMA.
            string name = material;
            string? catalog = null;
            int underscoreIdx = material.IndexOf('_');
            if (underscoreIdx > 0)
            {
                var suffix = material.Substring(underscoreIdx + 1);
                if (_catalogTokens.Contains(suffix))
                {
                    name = material.Substring(0, underscoreIdx);
                    catalog = suffix;
                }
            }

            if (_mgr == null)
            {
                Record(catalog);
                return CodeVGlassNames.LegacyNPrefix(name);
            }

            // A named catalog is the strongest evidence there is: it is what
            // separates SCHOTT SK16 from SUMITA SK16, and Sumita P-SK50 from
            // Schott PSK50.
            if (catalog != null)
            {
                foreach (var loaded in LoadedCatalogsFor(catalog))
                {
                    var exact = _mgr.GetGlass(loaded.ToUpperInvariant() + ":" + name);
                    if (exact != null) { Record(loaded); return exact.Name; }

                    if (_byCatalogStripped.TryGetValue(loaded, out var perCatalog) &&
                        perCatalog.TryGetValue(name, out var inCatalog))
                    {
                        Record(loaded);
                        return inCatalog;
                    }
                }
            }

            // The name as written wins next. Hoya's NBFD10 and its 27 siblings
            // are real catalog names that merely look like de-punctuated
            // N-prefix glasses, and transforming them breaks a working import.
            var asWritten = _mgr.GetGlass(name);
            if (asWritten != null)
            {
                Record(string.IsNullOrEmpty(asWritten.Catalog) ? catalog : asWritten.Catalog);
                return name;
            }

            // Otherwise it lost its punctuation on the way out to Code V; find
            // the catalog entry that strips down to it.
            if (_byStripped.TryGetValue(name, out var hit))
            {
                Record(hit.Catalog);
                return hit.Name;
            }

            // Unknown to every loaded catalog. Leave it as the file spelled it
            // rather than decorating it with a dash we cannot justify.
            Record(catalog);
            return name;
        }
    }

    /// <summary>
    /// Decides how a glass is written into a .seq: bare, or qualified as
    /// <c>GLASS_CATALOG</c>.
    ///
    /// The catalog is written whenever we can say which one owns the glass and
    /// Code V has that catalog, because a bare name does not reliably identify a
    /// glass on the way back in -- SCHOTT and SUMITA both answer to SK16 with
    /// different dispersion formulas, and stripping punctuation adds collisions
    /// of its own (Sumita P-SK50 and Schott PSK50 both become PSK50).
    /// </summary>
    internal sealed class CodeVGlassQualifier
    {
        private readonly GlassCatalog? _mgr;
        private readonly IList<string> _systemCatalogs;

        public CodeVGlassQualifier(GlassCatalog? mgr, IList<string>? systemCatalogs)
        {
            _mgr = mgr;
            _systemCatalogs = systemCatalogs ?? new List<string>();
        }

        /// <summary>The material token for a catalog glass name.</summary>
        public string ToCodeVMaterial(string name)
        {
            var stripped = CodeVGlassNames.ToCodeV(name);
            var owner = OwningCatalog(name);
            if (owner == null) return stripped;

            var codeVCatalog = CodeVGlassNames.ToCodeVCatalog(owner);
            return codeVCatalog == null ? stripped : stripped + "_" + codeVCatalog;
        }

        // Which catalog this glass came from, or null when that cannot be said.
        private string? OwningCatalog(string name)
        {
            if (_mgr == null)
            {
                // No catalogs loaded. A system carrying exactly one catalog
                // (a ZMX GCAT line, typically) leaves no room for doubt; more
                // than one and we would be guessing, so we write the name bare.
                return _systemCatalogs.Count == 1 ? _systemCatalogs[0] : null;
            }

            string? owner = null;
            var owners = new List<string>();
            foreach (var catalog in _mgr.LoadedCatalogs)
            {
                if (_mgr.GetGlass(catalog.ToUpperInvariant() + ":" + name) == null) continue;
                owners.Add(catalog);
                owner = catalog;
            }

            if (owners.Count == 0) return _systemCatalogs.Count == 1 ? _systemCatalogs[0] : null;
            if (owners.Count == 1) return owner;

            // Several catalogs answer to this name -- exactly the SK16 case.
            // The system's own preference order decides; without one there is
            // nothing to justify a choice, so the name goes out bare.
            foreach (var preferred in _systemCatalogs)
            {
                foreach (var candidate in owners)
                {
                    if (candidate.Equals(preferred, StringComparison.OrdinalIgnoreCase)) return candidate;
                }
            }
            return null;
        }
    }
}
