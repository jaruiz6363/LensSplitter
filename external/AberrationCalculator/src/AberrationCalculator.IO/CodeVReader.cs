using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Reads Code V .seq lens files.
    ///
    /// <para>A .seq is a command stream: <c>;</c> separates commands on a line, <c>&amp;</c>
    /// continues one onto the next, <c>!</c> starts a comment. The commands read are the ones
    /// Code V writes for a lens (as Zemax's CODEV-to-OpticStudio macro, v2.06, reads them): the
    /// surfaces <c>SO</c>/<c>S</c>/<c>SI</c> with <c>STO</c>, <c>CIR</c>, <c>ASP</c>/<c>CON</c>/
    /// <c>SPH</c> and the <c>K</c> and <c>A</c>..<c>G</c> lines beneath them; <c>RDM</c>
    /// (radius or curvature) and <c>DIM</c>; the aperture <c>EPD</c>, <c>FNO</c> or <c>NAO</c>;
    /// fields <c>XAN</c>/<c>YAN</c> or <c>XOB</c>/<c>YOB</c>; <c>WL</c>, <c>WTW</c>, <c>REF</c>,
    /// <c>WTF</c>; glass by catalog name, as Code V's fictitious-glass code
    /// (<c>516800.641700</c>) or MIL code (<c>517.642</c>), or from a private catalog
    /// (<c>PRV</c> ... <c>END</c>). Tilts, decenters, special surfaces and zoom data are not
    /// converted; the lens's notes list what was left out.</para>
    /// </summary>
    public static class CodeVReader
    {
        /// <summary>
        /// Read a Code V .seq file. When <paramref name="glassMgr"/> is
        /// provided, glass names are resolved against the loaded catalogs, which
        /// is the only way to undo Code V's punctuation-free spelling: NBK7 has
        /// to become N-BK7 while Hoya's NBFD10 must be left alone. Without a
        /// manager we fall back to the old assumption that a leading N before an
        /// uppercase letter is a Schott N-prefix -- fine for Schott, wrong for
        /// the 28 Hoya NBF/NBFD glasses.
        /// </summary>
        public static OpticalSystem Read(string filePath, GlassCatalog? glassMgr = null)
        {
            var system = new OpticalSystem();
            var resolver = new CodeVGlassResolver(glassMgr);

            var surfaces = new List<Surface>();
            var glassTokens = new Dictionary<Surface, string>();
            var wavelengths = new List<double>();
            var wavelengthWeights = new List<double>();
            var fieldsY = new List<double>();
            var fieldWeights = new List<double>();
            var privateWavelengths = new List<double>();
            var privateGlasses = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            var notConverted = new List<string>();
            int refWavelength = 1; // 1-indexed
            double unitScale = 1.0; // default mm
            bool radiusMode = true;
            bool inPrivateCatalog = false;
            Surface? currentSurface = null;

            void NotConverted(string what)
            {
                string where = currentSurface != null ? $" (S{surfaces.Count - 1})" : "";
                notConverted.Add(what + where);
            }

            foreach (var command in Commands(File.ReadAllLines(filePath)))
            {
                var parts = Tokens(command);
                if (parts.Count == 0) continue;
                string keyword = parts[0].ToUpperInvariant();

                // A private glass catalog: PWL gives its wavelengths, then one line per glass, its
                // quoted name and its index at each of them.
                if (inPrivateCatalog)
                {
                    if (keyword == "END")
                        inPrivateCatalog = false;
                    else if (keyword == "PWL")
                        privateWavelengths = Numbers(parts, 1).Select(nm => nm / 1000.0).ToList();
                    else if (parts[0].StartsWith("'"))
                        privateGlasses[Unquote(parts[0])] = Numbers(parts, 1);
                    continue;
                }

                // Surface-specific form: RDY S3 50, GLA S3 NBK7 ... acts on the surface it names.
                Surface? target = currentSurface;
                int arg = 1;
                if (parts.Count > 1 && TrySurfaceQualifier(parts[1], surfaces, out var named))
                {
                    target = named;
                    arg = 2;
                }
                string? Arg(int k) => arg + k < parts.Count ? parts[arg + k] : null;
                bool TryArg(int k, out double v)
                {
                    v = 0;
                    var a = Arg(k);
                    return a != null && TryParseDouble(a, out v);
                }

                if (IsSurfaceKeyword(keyword))
                {
                    currentSurface = new Surface { Index = surfaces.Count };
                    surfaces.Add(currentSurface);
                    if (parts.Count > 1 && TryParseDouble(parts[1], out double rc))
                        SetShape(currentSurface, rc, radiusMode);
                    if (parts.Count > 2 && TryParseDouble(parts[2], out double th))
                        currentSurface.Thickness = Thickness(th);
                    if (parts.Count > 3)
                        glassTokens[currentSurface] = parts[3];
                    continue;
                }

                switch (keyword)
                {
                    case "TIT":
                    case "TITLE":
                    {
                        int q1 = command.IndexOf('\'');
                        int q2 = command.LastIndexOf('\'');
                        system.Title = q1 >= 0 && q2 > q1
                            ? command.Substring(q1 + 1, q2 - q1 - 1)
                            : command.Substring(parts[0].Length).Trim();
                        break;
                    }

                    case "PRV":
                        inPrivateCatalog = true;
                        break;

                    case "RDM":
                        radiusMode = !(parts.Count > 1 && parts[1].StartsWith("N", StringComparison.OrdinalIgnoreCase));
                        break;

                    case "DIM":
                    case "DDM":
                        // DIM M = millimeters, DIM C = centimeters, DIM I = inches
                        if (parts.Count > 1)
                        {
                            switch (parts[1].ToUpperInvariant())
                            {
                                case "M": case "MM": unitScale = 1.0; break;
                                case "C": case "CM": unitScale = 10.0; break;
                                case "I": unitScale = 25.4; break;
                            }
                        }
                        break;

                    case "EPD":
                        if (TryArg(0, out double epd)) system.Aperture = new Aperture(ApertureType.EPD, epd);
                        break;
                    case "FNO":
                        if (TryArg(0, out double fno)) system.Aperture = new Aperture(ApertureType.FNumber, fno);
                        break;
                    case "NAO":
                        if (TryArg(0, out double nao)) system.Aperture = new Aperture(ApertureType.ObjectSpaceNA, nao);
                        break;
                    case "NA":
                        NotConverted("NA (image-space numerical aperture)");
                        break;

                    case "WL":
                        // Wavelengths in nanometers
                        wavelengths = Numbers(parts, 1).Select(nm => nm / 1000.0).ToList();
                        break;
                    case "WTW":
                        wavelengthWeights = Numbers(parts, 1);
                        break;
                    case "REF":
                        if (parts.Count > 1 && int.TryParse(parts[1], out int r)) refWavelength = r;
                        break;

                    case "YAN":
                    case "YOB":
                        system.FieldType = keyword == "YOB" ? FieldType.ObjectHeight : FieldType.ObjectAngle;
                        fieldsY = Numbers(parts, 1);
                        break;
                    case "XAN":
                    case "XOB":
                        if (Numbers(parts, 1).Any(x => x != 0))
                            NotConverted(keyword + " (x fields: this lens is meridional)");
                        break;
                    case "YIM":
                    case "YRI":
                    case "XIM":
                    case "XRI":
                        NotConverted(keyword + " (fields as image heights)");
                        break;
                    case "WTF":
                        fieldWeights = Numbers(parts, 1);
                        break;
                    case "VUX":
                    case "VLX":
                    case "VUY":
                    case "VLY":
                        if (Numbers(parts, 1).Any(v => v != 0))
                            NotConverted(keyword + " (vignetting factors)");
                        break;

                    case "RDY":
                    case "CUY":
                        if (target != null && TryArg(0, out double shape))
                            SetShape(target, shape, keyword == "RDY");
                        break;
                    case "THI":
                        if (target != null && TryArg(0, out double thi))
                            target.Thickness = Thickness(thi);
                        break;
                    case "GLA":
                        if (target != null && Arg(0) != null)
                            glassTokens[target] = Arg(0)!;
                        break;

                    case "STO":
                        if (target != null) target.IsStop = true;
                        break;

                    case "CIR":
                        if (target == null) break;
                        string? kind = Arg(0)?.ToUpperInvariant();
                        if (TryArg(0, out double cir))
                        {
                            target.SemiDiameter = cir;
                            target.SemiDiameterMode = SemiDiameterMode.Fixed;
                        }
                        else if ((kind == "OBS" || kind == "HOL") && TryArg(1, out double obs))
                        {
                            // An obscuration blocks the centre of the beam; on a mirror, or given
                            // as a hole, it is the hole in the middle.
                            if (kind == "HOL" || target.IsMirror || glassTokens.TryGetValue(target, out var g) && IsReflect(g))
                                target.InnerRadius = obs;
                            else
                                target.ObscurationRadius = obs;
                        }
                        break;
                    case "REX":
                    case "REY":
                    case "ELX":
                    case "ELY":
                        NotConverted(keyword + " (rectangular or elliptical aperture)");
                        break;

                    case "SPH":
                        if (target != null)
                        {
                            target.Type = SurfaceType.Standard;
                            target.Conic = 0;
                            Array.Clear(target.AsphericCoefficients, 0, target.AsphericCoefficients.Length);
                        }
                        break;
                    case "ASP":
                    case "CON":
                        if (target != null) target.Type = SurfaceType.EvenAsphere;
                        break;
                    case "K":
                        if (target != null && TryArg(0, out double k))
                        {
                            target.Conic = k;
                            target.Type = SurfaceType.EvenAsphere;
                        }
                        break;
                    case "A": case "B": case "C": case "D": case "E": case "F": case "G":
                    {
                        // Code V's A is r⁴: A→[1], B→[2] ...
                        int idx = keyword[0] - 'A' + 1;
                        if (target != null && TryArg(0, out double c) && idx < target.AsphericCoefficients.Length)
                        {
                            target.AsphericCoefficients[idx] = c;
                            target.Type = SurfaceType.EvenAsphere;
                        }
                        break;
                    }
                    case "H":
                    case "J":
                        if (TryArg(0, out double high) && high != 0)
                            NotConverted(keyword + " (aspheric term beyond r¹⁶)");
                        break;

                    case "XDE": case "YDE": case "ZDE":
                    case "ADE": case "BDE": case "CDE":
                        if (TryArg(0, out double de) && de != 0)
                            NotConverted(keyword + " (tilt or decenter)");
                        break;
                    case "DAR": case "BEN": case "RET": case "GLB":
                        NotConverted(keyword + " (coordinate change)");
                        break;
                    case "SPS":
                        NotConverted("SPS " + (Arg(0) ?? "") + " (special surface; read as its base sphere)");
                        break;
                    case "CYL": case "XTO": case "YTO":
                        NotConverted(keyword + " (toroidal surface)");
                        break;
                    case "HOE": case "DOE": case "GRT": case "DIF":
                        NotConverted(keyword + " (diffractive surface)");
                        break;
                    case "ZOO":
                        NotConverted("ZOO (zoom data; the first position is read)");
                        break;
                }
            }

            // Glass, now that any private catalog has been read.
            foreach (var kv in glassTokens)
                ApplyGlass(kv.Key, kv.Value, resolver, privateGlasses, privateWavelengths);

            // Cemented-interface SD inheritance: any surface with glass on
            // both sides (= a cemented joint) is by construction the same
            // physical aperture as its neighbours. Code V doesn't always
            // write a CIR for the middle surface of a doublet; without one
            // the surface ends up Auto-SD with value 0, which renders the
            // 2D layout incorrectly. If the previous surface has Fixed-SD,
            // inherit it.
            for (int i = 1; i < surfaces.Count; i++)
            {
                bool isCementedInterface =
                    IsGlassMaterial(surfaces[i - 1]) &&
                    IsGlassMaterial(surfaces[i]);
                if (!isCementedInterface) continue;

                var prev = surfaces[i - 1];
                var cur  = surfaces[i];
                if (prev.SemiDiameterMode == SemiDiameterMode.Fixed &&
                    cur.SemiDiameterMode != SemiDiameterMode.Fixed &&
                    cur.SemiDiameter <= 0)
                {
                    cur.SemiDiameter = prev.SemiDiameter;
                    cur.SemiDiameterMode = SemiDiameterMode.Fixed;
                }
            }

            // Populate system
            system.Surfaces = surfaces;

            // Wavelengths
            for (int i = 0; i < wavelengths.Count; i++)
            {
                double wt = i < wavelengthWeights.Count ? wavelengthWeights[i] : 1.0;
                bool isPrimary = (i + 1) == refWavelength;
                system.Wavelengths.Add(new Wavelength(wavelengths[i], wt, isPrimary));
            }

            // Fields
            for (int i = 0; i < fieldsY.Count; i++)
            {
                double wt = i < fieldWeights.Count ? fieldWeights[i] / 100.0 : 1.0;
                if (i == 0 && fieldsY[i] == 0 && wt == 0) wt = 1.0;
                system.Fields.Add(new Field(fieldsY[i], wt > 0 ? wt : 1.0));
            }
            if (system.Fields.Count == 0)
                system.Fields.Add(new Field(0, 1.0));

            FieldValidation.FilterImportedFields(system);

            // Record the catalogs the file's glasses actually bound to. Without
            // this the system carries no preference and every later lookup falls
            // back to scanning all catalogs, where a bare name does not always
            // identify a glass: SCHOTT and SUMITA both hold an SK16, same n_d
            // but different dispersion formulas.
            foreach (var catalog in resolver.SeenCatalogs)
            {
                if (!system.GlassCatalogs.Contains(catalog, StringComparer.OrdinalIgnoreCase))
                    system.GlassCatalogs.Add(catalog);
            }

            // What the file holds that this lens cannot, so that it is not lost unseen.
            if (notConverted.Count > 0)
            {
                string note = "Code V import - not converted: " + string.Join(", ", notConverted.Distinct()) + ".";
                system.Notes = string.IsNullOrEmpty(system.Notes) ? note : system.Notes + Environment.NewLine + note;
            }

            // DEDUCED, NOT DECLARED. The file named no catalog; these were worked out from the
            // glass names, and the working can be wrong - a bare F4 binds here to one catalog's
            // and the design may have meant another's, which is a per-cent of focal length. The
            // flag keeps the guess distinguishable from a declaration so the report can say so.
            if (system.GlassCatalogs.Count > 0) system.GlassCatalogsAreInferred = true;

            // Convert from file units to mm. The scale is REMEMBERED as well as applied: an
            // optimised design has to be able to go back to the file it came from, in the units
            // that file is written in.
            system.FileUnitScale = unitScale;
            if (unitScale != 1.0)
                LensUnitConverter.ConvertToMm(system, unitScale);

            return system;
        }

        /// <summary>
        /// The file as a list of commands: comments removed, <c>&amp;</c> continuations joined,
        /// and each line split at the semicolons outside quotes.
        /// </summary>
        internal static IEnumerable<string> Commands(IEnumerable<string> lines)
        {
            var pending = new StringBuilder();
            foreach (var raw in lines)
            {
                string line = StripComment(raw).TrimEnd();
                if (line.EndsWith("&"))
                {
                    pending.Append(line, 0, line.Length - 1).Append(' ');
                    continue;
                }
                pending.Append(line);
                string whole = pending.ToString();
                pending.Clear();

                foreach (var command in SplitOutsideQuotes(whole, ';'))
                {
                    string c = command.Trim();
                    if (c.Length > 0) yield return c;
                }
            }
            if (pending.Length > 0 && pending.ToString().Trim().Length > 0)
                yield return pending.ToString().Trim();
        }

        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\'' || line[i] == '"') quoted = !quoted;
                else if (line[i] == '!' && !quoted) return line.Substring(0, i);
            }
            return line;
        }

        private static IEnumerable<string> SplitOutsideQuotes(string text, char separator)
        {
            bool quoted = false;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\'' || text[i] == '"') quoted = !quoted;
                else if (text[i] == separator && !quoted)
                {
                    yield return text.Substring(start, i - start);
                    start = i + 1;
                }
            }
            yield return text.Substring(start);
        }

        /// <summary>A command's words, a quoted string being one word (quotes kept).</summary>
        private static List<string> Tokens(string command)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            char quote = '\0';
            foreach (char ch in command)
            {
                if (quote != '\0')
                {
                    current.Append(ch);
                    if (ch == quote) quote = '\0';
                }
                else if (ch == '\'' || ch == '"')
                {
                    current.Append(ch);
                    quote = ch;
                }
                else if (char.IsWhiteSpace(ch))
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                }
                else current.Append(ch);
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        private static string Unquote(string token) => token.Trim().Trim('\'', '"');

        // SO, S, SI - and S followed by its number, which Code V also accepts.
        private static bool IsSurfaceKeyword(string keyword) =>
            keyword == "SO" || keyword == "S" || keyword == "SI" || Regex.IsMatch(keyword, @"^S\d+$");

        private static bool TrySurfaceQualifier(string token, List<Surface> surfaces, out Surface? surface)
        {
            surface = null;
            string t = token.ToUpperInvariant();
            if (t == "SO") surface = surfaces.Count > 0 ? surfaces[0] : null;
            else if (t == "SI") surface = surfaces.Count > 0 ? surfaces[surfaces.Count - 1] : null;
            else if (Regex.IsMatch(t, @"^S\d+$"))
            {
                int n = int.Parse(t.Substring(1), CultureInfo.InvariantCulture);
                surface = n < surfaces.Count ? surfaces[n] : null;
            }
            else return false;
            return true;
        }

        // A radius (RDM, Code V's default) or a curvature (RDM N); zero is a plane either way.
        private static void SetShape(Surface surface, double value, bool isRadius)
        {
            if (Math.Abs(value) <= 1e-15) return;   // default infinity
            if (isRadius) surface.Radius = value;
            else surface.Curvature = value;
        }

        // Code V writes an infinite distance as a large number - 1e10, 0.1E+14, 1e20.
        private static double Thickness(double value) =>
            Math.Abs(value) >= 1e9 ? double.PositiveInfinity : value;

        private static bool IsReflect(string token) =>
            Unquote(token).Equals("REFL", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The medium after a surface, from its glass token: air, a mirror, a private glass, a
        /// fictitious or MIL glass code (a model glass here), or a catalog name.
        /// </summary>
        private static void ApplyGlass(Surface surface, string token, CodeVGlassResolver resolver,
            Dictionary<string, List<double>> privateGlasses, List<double> privateWavelengths)
        {
            string material = Unquote(token);
            if (material.Length == 0
                || material.Equals("AIR", StringComparison.OrdinalIgnoreCase)
                || material.Equals("REFR", StringComparison.OrdinalIgnoreCase))
                return;

            if (material.Equals("REFL", StringComparison.OrdinalIgnoreCase))
            {
                surface.Material = "MIRROR";
                return;
            }

            if (privateGlasses.TryGetValue(material, out var indices) && indices.Count > 0)
            {
                var (nd, vd) = OsloReader.ModelFromIndices(privateWavelengths, indices);
                SetModel(surface, nd, vd);
                return;
            }

            if (TryGlassCode(material, out double cnd, out double cvd))
            {
                SetModel(surface, cnd, cvd);
                return;
            }

            // Code V writes glass names without punctuation and may
            // qualify them as GLASS_CATALOG. Resolving that against the
            // loaded catalogs -- rather than guessing where a dash used
            // to be -- is what keeps NBK7 and Hoya's NBFD10 apart.
            surface.Material = resolver.Resolve(material);
        }

        private static void SetModel(Surface surface, double nd, double vd)
        {
            surface.Material = null;
            surface.ModelIndexEnabled = true;
            surface.ModelNd = nd;
            surface.ModelVd = vd;
        }

        /// <summary>
        /// A glass given by numbers, as Zemax's converter reads Code V's: with at least four
        /// digits before the point it is the fictitious-glass code, nd's digits after "1." and
        /// then Vd/100 (<c>516800.641700</c> is 1.5168 / 64.17); with three or fewer it is the
        /// MIL code, nd's three digits and Vd's three (<c>517.642</c> is 1.517 / 64.2).
        /// </summary>
        public static bool TryGlassCode(string token, out double nd, out double vd)
        {
            nd = vd = 0;
            var m = Regex.Match(token, @"^(\d+)\.(\d+)$");
            if (!m.Success) return false;
            string left = m.Groups[1].Value, right = m.Groups[2].Value;
            var inv = CultureInfo.InvariantCulture;
            if (left.Length >= 4)
            {
                nd = double.Parse("1." + left, inv);
                vd = double.Parse("0." + right, inv) * 100.0;
                return true;
            }
            if (token.Length < 8)
            {
                string r3 = right.Length >= 3 ? right.Substring(0, 3) : right.PadRight(3, '0');
                nd = 1.0 + double.Parse(left.PadRight(3, '0'), inv) / 1000.0;
                vd = double.Parse(r3, inv) / 10.0;
                return true;
            }
            return false;
        }

        private static List<double> Numbers(List<string> parts, int from)
        {
            var values = new List<double>();
            for (int i = from; i < parts.Count; i++)
                if (TryParseDouble(parts[i], out double v))
                    values.Add(v);
            return values;
        }

        private static bool TryParseDouble(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool IsGlassMaterial(Surface surface)
        {
            if (surface.ModelIndexEnabled) return true;
            if (string.IsNullOrEmpty(surface.Material)) return false;
            if (surface.Material.Equals("MIRROR", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
    }
}
