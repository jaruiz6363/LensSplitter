using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the file it came from, by EDITING that file rather
    /// than regenerating it.
    ///
    /// <para><b>Why editing.</b> This program models a slice of every format it reads - the .zmx
    /// reader recognises twenty-three directives, and a real .zmx has many times that in solves,
    /// coatings, apertures, tolerances and multi-configuration data. Writing a fresh file from
    /// this program's <see cref="OpticalSystem"/> would silently drop all of it, and the user
    /// would find out when they next opened their design. So the original file is read, the
    /// values the optimiser actually moved are changed, and every other byte is left alone -
    /// including the encoding and the line endings, which for a .zmx are UTF-16 and CRLF.</para>
    ///
    /// <para><b>Only what the optimiser can move ever changes:</b> curvatures, thicknesses, glass
    /// names, and conics and aspheric terms. Everything else in the file is somebody else's. The
    /// figuring goes back into .lhlt, .zmx and Optiland .json; for CODE V, OSLO and Optalix it is
    /// not written, and a save whose figuring moved is REFUSED rather than written without it -
    /// a file that reads back as the unoptimised figuring, with nothing said, is how this was
    /// found (September 2026).</para>
    ///
    /// <para><b>Units go back the way they came.</b> Everything inside this program is in
    /// millimetres; a file written in inches gets inches back, through
    /// <see cref="OpticalSystem.FileUnitScale"/>. A design returned in the wrong units would be
    /// wrong by a factor of twenty-five and look perfectly reasonable.</para>
    /// </summary>
    public static class LensPatcher
    {
        /// <summary>Formats this can currently write back.</summary>
        public static readonly string[] SupportedExtensions =
            { ".lhlt", ".zmx", ".json", ".seq", ".len", ".osl", ".otx", ".opt" };

        /// <summary>True when an optimised design can be written back in this file's format.</summary>
        public static bool CanSave(string path) =>
            Array.IndexOf(SupportedExtensions,
                          Path.GetExtension(path).ToLowerInvariant()) >= 0;

        /// <summary>
        /// Applies <paramref name="system"/> to the file at <paramref name="originalPath"/> and
        /// writes the result to <paramref name="outputPath"/>.
        ///
        /// <para><paramref name="catalog"/> is wanted by one format only. A .seq writes a glass
        /// without its punctuation and may qualify it with its catalog, and deciding which
        /// catalog owns a glass means asking the loaded ones. Without it the names still go back
        /// correctly for glasses that were never punctuated, which is most of them, and the
        /// qualifier is omitted rather than guessed at.</para>
        /// </summary>
        public static void Save(OpticalSystem system, string originalPath, string outputPath,
                                GlassCatalog? catalog = null)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (originalPath == null) throw new ArgumentNullException(nameof(originalPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));
            if (!File.Exists(originalPath))
                throw new FileNotFoundException("the original lens file is needed in order to "
                                              + "edit it", originalPath);

            switch (Path.GetExtension(originalPath).ToLowerInvariant())
            {
                case ".lhlt": LhltPatcher.Patch(system, originalPath, outputPath); return;
                case ".zmx": PatchZmx(system, originalPath, outputPath); return;
                case ".json": PatchOptiland(system, originalPath, outputPath); return;
                case ".seq":
                    RequireFiguringUnchanged(system, originalPath, catalog, "CODE V");
                    SeqPatcher.Patch(system, originalPath, outputPath, catalog); return;

                // Two extensions apiece, one format apiece: the reader treats them the same and
                // so does this.
                case ".len":
                case ".osl":
                    RequireFiguringUnchanged(system, originalPath, catalog, "OSLO");
                    LenPatcher.Patch(system, originalPath, outputPath); return;
                case ".otx":
                case ".opt":
                    RequireFiguringUnchanged(system, originalPath, catalog, "OPTALIX");
                    OtxPatcher.Patch(system, originalPath, outputPath); return;

                default:
                    throw new NotSupportedException(
                        $"Writing an optimised design back to '{Path.GetExtension(originalPath)}' "
                      + "is not implemented yet. Editing a file in place has to be done format by "
                      + "format and checked against real examples of it, and there are none of "
                      + "this one in the repository to check against. The optimised prescription "
                      + "is in the report, and the settings are in the sidecar; save as .lhlt to "
                      + "keep the design itself.");
            }
        }

        // ── Figuring: conics and aspheric terms ─────────────────────────────────────────
        //
        // The optimiser moves conics and the r^4, r^6 and r^8 terms as well as curvatures,
        // thicknesses and glasses. Until September 2026 only the .lhlt writer carried them: every
        // other format wrote back the three things it always had, and an optimised conic simply
        // did not arrive - on F1_conic_singlet.zmx the optimiser found k = -1.46 and halved the
        // merit, and the saved file still said -0.6, with nothing to say so.
        //
        // .zmx and Optiland's .json now carry them, and are checked against the programs
        // themselves. CODE V, OSLO and OPTALIX are REFUSED when figuring has moved: there is no
        // real aspheric file of any of the three in this repository to check a writer against,
        // and a writer checked only against this program's own reader can agree with it and still
        // write something the program itself misreads - which is exactly how the Optiland
        // coefficients were read one power too high for as long as that reader existed.

        /// <summary>
        /// Whether a figuring coefficient has changed. Relative, because the aspheric terms are
        /// small numbers: an absolute tolerance of the size curvatures use would pass an r^4
        /// coefficient changed in its sixth figure as unchanged.
        /// </summary>
        internal static bool FiguringMoved(double before, double after)
        {
            if (before == after) return false;
            return Math.Abs(after - before) > 1e-12 * Math.Max(Math.Abs(before), Math.Abs(after));
        }

        /// <summary>
        /// A coefficient in the file's own units. The r^(2k+2) coefficient carries a length to the
        /// power -(2k+1), so going from millimetres to file units MULTIPLIES by that power of the
        /// scale - the reverse of <see cref="LensUnitConverter.ConvertToMm"/>.
        /// </summary>
        private static double CoefficientInFileUnits(double millimetres, int index, double scale) =>
            millimetres * Math.Pow(scale, 2 * (index + 1) - 1);

        /// <summary>
        /// Refuses a save that would lose figuring. The original file is read again and compared,
        /// surface by surface, with the design about to be written; any conic or aspheric
        /// coefficient that moved stops the save with the reason, rather than letting it vanish.
        /// </summary>
        private static void RequireFiguringUnchanged(OpticalSystem system, string originalPath,
                                                     GlassCatalog? catalog, string format)
        {
            var original = LensFile.Read(originalPath, catalog ?? CatalogLocator.LoadBundled());
            var moved = new List<string>();
            int count = Math.Min(original.Surfaces.Count, system.Surfaces.Count);
            for (int i = 0; i < count; i++)
            {
                var a = original.Surfaces[i];
                var b = system.Surfaces[i];
                if (FiguringMoved(a.Conic, b.Conic)) moved.Add($"surface {i} conic");
                int n = Math.Min(a.AsphericCoefficients.Length, b.AsphericCoefficients.Length);
                for (int k = 0; k < n; k++)
                    if (FiguringMoved(a.AsphericCoefficients[k], b.AsphericCoefficients[k]))
                        moved.Add($"surface {i} A{2 * k + 2}");
            }
            if (moved.Count == 0) return;

            throw new NotSupportedException(
                $"The optimised design changes figuring ({string.Join(", ", moved)}), and writing "
              + $"conics and aspheric terms back into a {format} file is not implemented: there is "
              + "no real aspheric example of the format in this repository to check a writer "
              + "against, so it is refused rather than saved without them. The optimised values "
              + "are in the optimisation report; .zmx, Optiland .json and .lhlt carry figuring.");
        }

        // ── ZEMAX ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A .zmx is a flat list of keyword lines, with <c>SURF n</c> opening each surface and
        /// its properties indented beneath. Only the first argument of CURV, DISZ and GLAS is
        /// ever touched; the trailing fields on those lines - solve codes, index and Abbe hints -
        /// belong to the format and are left exactly as found.
        /// </summary>
        private static void PatchZmx(OpticalSystem system, string originalPath, string outputPath)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            int surface = -1;

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;              // already deleted
                string keyword = LineEdit.Keyword(line);

                if (keyword == "SURF")
                {
                    surface = LineEdit.ArgumentAsInt(line, -1);
                    continue;
                }
                if (surface < 0 || surface >= system.Surfaces.Count) continue;
                var s = system.Surfaces[surface];

                switch (keyword)
                {
                    case "CURV":
                        // A curvature is a reciprocal length, so it scales the other way from a
                        // distance: per millimetre becomes per file unit by MULTIPLYING.
                        file.Lines[i] = LineEdit.ReplaceNumberIfChanged(line, s.Curvature * scale);
                        break;

                    case "DISZ":
                        file.Lines[i] = double.IsInfinity(s.Thickness)
                            ? (LineEdit.Argument(line).Equals("INFINITY", StringComparison.OrdinalIgnoreCase)
                                 ? line : LineEdit.ReplaceArgument(line, "INFINITY"))
                            : LineEdit.ReplaceNumberIfChanged(line, s.Thickness / scale);
                        break;

                    case "GLAS":
                        string material = s.Material ?? string.Empty;
                        if (s.ModelIndexEnabled && s.ModelNd > 0.0)
                        {
                            // A MODEL glass, and it must not be mistaken for no glass at all.
                            // The reader leaves Material BLANK for one deliberately - the index
                            // comes from (Nd, Vd, dPgF) rather than from a catalogue lookup, and
                            // GlassCatalog gives the model precedence - so the emptiness below
                            // is not the absence of a glass, and deleting the line here turned
                            // the element into air. Silently: the patched file reads back with
                            // one fewer surface of glass and no complaint from anything.
                            //
                            // The name token stays as the file wrote it (___BLANK) and only the
                            // two model numbers are touched, in the fields the reader takes them
                            // from: GLAS <name> <flag> <flag> <Nd> <Vd> ...
                            file.Lines[i] =
                                LineEdit.Argument(line).Equals("___BLANK", StringComparison.OrdinalIgnoreCase)
                                    ? LineEdit.ReplaceNumberIfChanged(
                                          LineEdit.ReplaceNumberIfChanged(line, s.ModelNd, 3),
                                          s.ModelVd, 4)
                                    : line;
                        }
                        else if (string.IsNullOrWhiteSpace(material))
                        {
                            // The glass is gone. Removing the line is the only honest edit; a
                            // GLAS line naming nothing is not a thing a well-formed file contains.
                            file.Lines[i] = null;
                        }
                        else if (!LineEdit.Argument(line).Equals(material, StringComparison.Ordinal))
                        {
                            file.Lines[i] = LineEdit.ReplaceArgument(line, material);
                        }
                        break;
                }
            }

            PatchZmxFiguring(system, file, scale);
            file.Write(outputPath);
        }

        /// <summary>
        /// Conics and aspheric terms, in OpticStudio's own layout - taken from files it wrote:
        ///
        /// <code>
        ///   TYPE EVENASPH            TYPE STANDARD on a sphere or a plain conic
        ///   CURV ...
        ///   MIRR 2 0
        ///   PARM 1 0                 all eight, r^2 to r^16, on an even asphere only
        ///   ...
        ///   DISZ ...
        ///   GLAS ...
        ///   CONI -0.6                present only when non-zero
        ///   DIAM ...
        /// </code>
        ///
        /// <para>A line that exists is edited when its value moved, and left byte for byte when it
        /// did not. A missing CONI is inserted after GLAS (or DISZ). A STANDARD surface that the
        /// optimiser gave an aspheric term becomes EVENASPH, with its eight PARM lines inserted
        /// before DISZ, as OpticStudio writes them. Any other surface type's PARM lines mean
        /// something else, so figuring that moved on one is refused rather than written into them.
        /// </para>
        /// </summary>
        private static void PatchZmxFiguring(OpticalSystem system, PatchText file, double scale)
        {
            var insertions = new List<(int Index, string Text)>();

            for (int start = 0; start < file.Lines.Count; start++)
            {
                string? head = file.Lines[start];
                if (head == null || LineEdit.Keyword(head) != "SURF") continue;
                int surface = LineEdit.ArgumentAsInt(head, -1);
                if (surface < 1 || surface >= system.Surfaces.Count - 1) continue;
                var s = system.Surfaces[surface];

                // The block: every indented line after SURF, up to the first that is not.
                int typeLine = -1, curvLine = -1, mirrLine = -1, diszLine = -1, glasLine = -1,
                    coniLine = -1, end = start + 1;
                var parm = new Dictionary<int, int>();
                for (; end < file.Lines.Count; end++)
                {
                    string? line = file.Lines[end];
                    if (line == null) continue;
                    if (line.Length == 0 || (line[0] != ' ' && line[0] != '\t')) break;
                    switch (LineEdit.Keyword(line))
                    {
                        case "TYPE": typeLine = end; break;
                        case "CURV": curvLine = end; break;
                        case "MIRR": mirrLine = end; break;
                        case "DISZ": diszLine = end; break;
                        case "GLAS": glasLine = end; break;
                        case "CONI": coniLine = end; break;
                        case "PARM":
                            int n = LineEdit.ArgumentAsInt(line, -1);
                            if (n >= 1) parm[n] = end;
                            break;
                    }
                }
                string indent = curvLine >= 0 ? LineEdit.Indent(file.Lines[curvLine]!) : "  ";

                // Conic: dimensionless, so no unit conversion.
                double conicInFile = 0.0;
                if (coniLine >= 0) LineEdit.ArgumentAsDouble(file.Lines[coniLine]!, out conicInFile);
                if (FiguringMoved(conicInFile, s.Conic))
                {
                    if (coniLine >= 0)
                        file.Lines[coniLine] = LineEdit.ReplaceArgument(file.Lines[coniLine]!, LineEdit.Number(s.Conic));
                    else
                    {
                        int after = glasLine >= 0 ? glasLine : diszLine >= 0 ? diszLine
                                  : curvLine >= 0 ? curvLine : end - 1;
                        insertions.Add((after + 1, indent + "CONI " + LineEdit.Number(s.Conic)));
                    }
                }

                // Aspheric terms, PARM 1..8 = r^2..r^16.
                string type = typeLine >= 0 ? LineEdit.Argument(file.Lines[typeLine]!).ToUpperInvariant() : "STANDARD";
                int count = Math.Min(8, s.AsphericCoefficients.Length);
                var wanted = new double[8];
                for (int k = 0; k < count; k++)
                    wanted[k] = CoefficientInFileUnits(s.AsphericCoefficients[k], k, scale);

                if (type == "EVENASPH")
                {
                    int lastParm = parm.Count > 0 ? Max(parm.Values) : -1;
                    for (int k = 0; k < 8; k++)
                    {
                        double current = 0.0;
                        if (parm.TryGetValue(k + 1, out int at))
                            LineEdit.ArgumentAsDouble(file.Lines[at]!, out current, 1);
                        if (!FiguringMoved(current, wanted[k])) continue;
                        if (parm.ContainsKey(k + 1))
                            file.Lines[at] = LineEdit.ReplaceArgument(file.Lines[at]!, LineEdit.Number(wanted[k]), 1);
                        else
                        {
                            int after = lastParm >= 0 ? lastParm : diszLine >= 0 ? diszLine - 1
                                      : mirrLine >= 0 ? mirrLine : curvLine >= 0 ? curvLine : end - 1;
                            insertions.Add((after + 1, indent + "PARM " + (k + 1) + " " + LineEdit.Number(wanted[k])));
                        }
                    }
                }
                else
                {
                    bool any = false;
                    for (int k = 0; k < 8; k++) if (wanted[k] != 0.0) any = true;
                    if (!any) continue;

                    if (type != "STANDARD")
                        throw new NotSupportedException(
                            $"Surface {surface} is a {type} surface in the .zmx, whose PARM lines do not "
                          + "hold aspheric coefficients, and the optimised design gives it an aspheric "
                          + "term. It is refused rather than written into the wrong parameters.");

                    // A sphere or a plain conic that the optimiser figured: it becomes an even
                    // asphere, with all eight PARM lines where OpticStudio writes them.
                    if (typeLine >= 0)
                        file.Lines[typeLine] = LineEdit.ReplaceArgument(file.Lines[typeLine]!, "EVENASPH");
                    else
                        insertions.Add((start + 1, indent + "TYPE EVENASPH"));

                    int before = diszLine >= 0 ? diszLine : mirrLine >= 0 ? mirrLine + 1
                               : curvLine >= 0 ? curvLine + 1 : end;
                    for (int k = 0; k < 8; k++)
                        insertions.Add((before, indent + "PARM " + (k + 1) + " " + LineEdit.Number(wanted[k])));
                }
            }

            // The eight PARM lines of a surface share one index and must land in order, which
            // PatchText.InsertAll does not promise; this does.
            StableInsert(file, insertions);
        }

        private static int Max(IEnumerable<int> values)
        {
            int m = int.MinValue;
            foreach (int v in values) if (v > m) m = v;
            return m;
        }

        /// <summary>
        /// Inserts at the original indices, bottom-up so no earlier index shifts, and keeps lines
        /// that share an index in the order they were listed.
        /// </summary>
        private static void StableInsert(PatchText file, List<(int Index, string Text)> insertions)
        {
            // Groups are inserted bottom-up, so no earlier index shifts; each group goes in as
            // one range, in the order it was listed.
            var groups = new SortedDictionary<int, List<string>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            foreach (var (index, text) in insertions)
            {
                if (!groups.TryGetValue(index, out var list)) groups[index] = list = new List<string>();
                list.Add(text);
            }
            foreach (var group in groups)
            {
                int at = Math.Max(0, Math.Min(group.Key, file.Lines.Count));
                file.Lines.InsertRange(at, group.Value);
            }
        }

        // ── Optiland ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Optiland's JSON records each surface's ABSOLUTE position along the axis rather than
        /// its thickness, so changing one thickness moves every surface after it. The z of the
        /// first surface is left where it was and the rest are re-accumulated from the
        /// thicknesses - which is what the reader does in reverse.
        /// </summary>
        private static void PatchOptiland(OpticalSystem system, string originalPath,
                                          string outputPath)
        {
            string json = HideInfinities(File.ReadAllText(originalPath));
            var root = JsonNode.Parse(json, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidOperationException($"'{originalPath}' is not JSON.");

            if (root["surface_group"]?["surfaces"] is not JsonArray surfaces)
                throw new InvalidOperationException(
                    $"'{originalPath}' has no surface_group.surfaces to edit.");

            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            int count = Math.Min(surfaces.Count, system.Surfaces.Count);

            double z = 0.0;
            bool haveZ = false;

            for (int i = 0; i < count; i++)
            {
                if (surfaces[i] is not JsonObject node) continue;
                if (node["geometry"] is not JsonObject geometry) continue;
                var s = system.Surfaces[i];

                // Radius. The format writes a plane as a very large number rather than an
                // infinity, and the reader treats anything past 1e10 as one, so a plane that was
                // already a plane is left spelled however the file spelled it.
                if (geometry["radius"] != null)
                {
                    double current = AsDouble(geometry["radius"]);
                    bool currentIsPlane = double.IsNaN(current) || Math.Abs(current) >= 1e10;
                    if (double.IsInfinity(s.Radius))
                    {
                        if (!currentIsPlane) geometry["radius"] = 1e30;
                    }
                    else
                    {
                        double wanted = s.Radius / scale;
                        if (currentIsPlane || Math.Abs(wanted - current) > 1e-12 * Math.Max(1.0, Math.Abs(current)))
                            geometry["radius"] = wanted;
                    }
                }

                PatchOptilandFiguring(geometry, s, scale, i);

                // The format records a surface's position BOTH ways: an explicit thickness, and
                // an absolute z. The reader prefers the explicit one, so a patch that updated
                // only the positions would appear to do nothing - and one that updated only the
                // thicknesses would leave the two disagreeing, which is a trap for anything else
                // that reads the file. Both are written, and kept consistent.
                double t = s.Thickness;
                bool finite = !double.IsInfinity(t) && !double.IsNaN(t);

                if (node["thickness"] != null && finite)
                {
                    double current = AsDouble(node["thickness"]);
                    double wanted = t / scale;
                    if (double.IsNaN(current) || double.IsInfinity(current)
                        || Math.Abs(wanted - current) > 1e-12 * Math.Max(1.0, Math.Abs(current)))
                        node["thickness"] = wanted;
                }

                if (geometry["cs"] is JsonObject cs)
                {
                    // THE RUN STARTS AT THE FIRST SURFACE THAT HAS A POSITION. An object at
                    // infinity is written at z = -Infinity, and accumulating from there would
                    // put every surface in the lens at minus infinity - and then fail on the
                    // way out, because an infinity is not a number JSON can carry.
                    double currentZ = AsDouble(cs["z"]);
                    if (!haveZ)
                    {
                        if (!double.IsNaN(currentZ) && !double.IsInfinity(currentZ))
                        {
                            z = currentZ;
                            haveZ = true;
                        }
                    }
                    else
                    {
                        cs["z"] = z;
                    }
                }

                // An object at infinity has no distance to accumulate; the surface after it stays
                // where the file put it.
                if (finite) z += t / scale;

                SetMaterial(node, s.Material);
            }

            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, ShowInfinities(
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
        }

        /// <summary>
        /// Conic and aspheric terms, as Optiland writes them: a <c>StandardGeometry</c> or
        /// <c>Plane</c> carries <c>conic</c>, an <c>EvenAsphere</c> adds <c>coefficients</c> - the
        /// multipliers of r^2, r^4, r^6, ..., in that order. Measured, not assumed: an even asphere
        /// built in Optiland 0.6.2 with coefficients [1e-7, 2e-11, 3e-15] has the sag of
        /// 1e-7 r^2 + 2e-11 r^4 + 3e-15 r^6 to twelve figures, and its own writer stores the list
        /// exactly so. A surface given an aspheric term becomes an EvenAsphere, which Optiland
        /// builds from its radius and cs alone.
        /// </summary>
        private static void PatchOptilandFiguring(JsonObject geometry, Surface s, double scale, int index)
        {
            string type = geometry["type"] is JsonValue tv && tv.TryGetValue(out string? t) && t != null ? t : "";
            bool asphere = type.Equals("EvenAsphere", StringComparison.OrdinalIgnoreCase);
            bool standard = type.Equals("StandardGeometry", StringComparison.OrdinalIgnoreCase);
            bool plane = type.Equals("Plane", StringComparison.OrdinalIgnoreCase);

            // The coefficients as the file should hold them, trailing zeros dropped.
            var wanted = new List<double>();
            for (int k = 0; k < s.AsphericCoefficients.Length; k++)
                wanted.Add(CoefficientInFileUnits(s.AsphericCoefficients[k], k, scale));
            while (wanted.Count > 0 && wanted[wanted.Count - 1] == 0.0) wanted.RemoveAt(wanted.Count - 1);

            var current = new List<double>();
            if (geometry["coefficients"] is JsonArray have)
                foreach (var c in have) current.Add(AsDouble(c));

            bool coefficientsMoved = false;
            for (int k = 0; k < Math.Max(wanted.Count, current.Count); k++)
            {
                double a = k < current.Count ? current[k] : 0.0;
                double b = k < wanted.Count ? wanted[k] : 0.0;
                if (FiguringMoved(a, b)) coefficientsMoved = true;
            }

            double conicInFile = geometry["conic"] != null ? AsDouble(geometry["conic"]) : 0.0;
            bool conicMoved = FiguringMoved(double.IsNaN(conicInFile) ? 0.0 : conicInFile, s.Conic);

            if (!asphere && !standard && !plane)
            {
                if (conicMoved || coefficientsMoved)
                    throw new NotSupportedException(
                        $"Surface {index} is an Optiland '{type}' geometry, which this program does not "
                      + "write figuring into, and the optimised design changes its conic or aspheric "
                      + "terms. It is refused rather than saved without them.");
                return;
            }

            if (conicMoved) geometry["conic"] = s.Conic;
            if (!coefficientsMoved) return;

            if (!asphere) geometry["type"] = "EvenAsphere";
            if (geometry["conic"] == null) geometry["conic"] = s.Conic;
            var array = new JsonArray();
            // Written to the length the file already had, if longer, so a trailing zero the file
            // carried is kept rather than dropped.
            for (int k = 0; k < Math.Max(wanted.Count, current.Count); k++)
                array.Add(JsonValue.Create(k < wanted.Count ? wanted[k] : 0.0));
            geometry["coefficients"] = array;
        }

        // ── Optiland's infinities ────────────────────────────────────────────────────────
        //
        // The format writes an object at infinity as the bare word `Infinity`, which Python's
        // json module accepts and the JSON specification does not - so a parser that follows the
        // specification stops on it. The reader deals with this by substituting a huge number;
        // that is fine for reading and wrong for writing back, because it would hand the user a
        // file in which their infinity had quietly become 1e308.
        //
        // So the words are turned into STRINGS on the way in, which survive a parse and a
        // re-serialisation byte for byte, and turned back into words on the way out.

        private const string PosInf = "\"__abcalc_inf__\"";
        private const string NegInf = "\"__abcalc_neg_inf__\"";

        private static string HideInfinities(string json) =>
            json.Replace("-Infinity", NegInf).Replace("Infinity", PosInf);

        private static string ShowInfinities(string json) =>
            json.Replace(NegInf, "-Infinity").Replace(PosInf, "Infinity");

        /// <summary>
        /// A number that may be one of the hidden infinities, or NaN when the node is neither a
        /// number nor one of them.
        /// </summary>
        private static double AsDouble(JsonNode? node)
        {
            if (node is not JsonValue value) return double.NaN;
            if (value.TryGetValue(out double d)) return d;
            if (value.TryGetValue(out string? s) && s != null)
            {
                if (s == "__abcalc_inf__") return double.PositiveInfinity;
                if (s == "__abcalc_neg_inf__") return double.NegativeInfinity;
            }
            return double.NaN;
        }

        /// <summary>
        /// Renames the glass. Optiland puts it in <c>material_post</c> - the medium AFTER the
        /// surface - as an object carrying a name.
        ///
        /// <para>A material described by its own dispersion coefficients rather than by a name is
        /// left alone. Renaming one would leave the name saying one thing and the coefficients
        /// another, and a glass substitution that cannot be written faithfully is better not
        /// written at all.</para>
        /// </summary>
        private static void SetMaterial(JsonObject node, string? material)
        {
            if (string.IsNullOrWhiteSpace(material)) return;
            if (node["material_post"] is not JsonObject post) return;
            if (post["name"] is not JsonValue value) return;
            if (!value.TryGetValue(out string? existing) || existing == null) return;
            if (string.Equals(existing, material, StringComparison.Ordinal)) return;
            post["name"] = material;
        }

    }
}
