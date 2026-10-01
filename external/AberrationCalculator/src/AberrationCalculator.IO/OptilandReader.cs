using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Reads Optiland .json lens files.
    /// Optiland uses JSON with non-standard Infinity/-Infinity literals.
    /// Surface positions are cumulative Z coordinates (thicknesses must be computed as deltas).
    ///
    /// <para>Materials are read as Optiland writes them, and as LensHH-LT writes them for it:</para>
    /// <list type="bullet">
    /// <item><c>Material</c>: a catalog glass, by name. Its <c>catalog</c> goes into the system's
    /// catalog preference, ordered so that each glass resolves to its own catalog's glass.
    /// LensHH-LT writes its catalogs prefixed <c>lenshh-</c>, which is dropped.</item>
    /// <item><c>MaterialFile</c>: the same, named by its file, which lives in a folder named
    /// after its catalog.</item>
    /// <item><c>AbbeMaterial</c>: a model glass of the same nd and Vd.</item>
    /// <item><c>IdealMaterial</c> of index other than 1: a model glass of constant index.</item>
    /// <item>A LensHH-LT model glass (<c>MODEL_nd_Vd_dPgF</c>): that model glass.</item>
    /// </list>
    /// <para>Earlier versions kept only a <c>Material</c>'s name, and turned every other material
    /// into air. What does not carry over exactly is said in <see cref="OpticalSystem.Notes"/>.</para>
    /// </summary>
    public static class OptilandReader
    {
        /// <param name="glass">The glass catalogs, to order the catalog preference and to say
        /// which glasses they do not have.</param>
        public static OpticalSystem Read(string filePath, GlassCatalog? glass = null)
        {
            var notes = new List<string>();
            var named = new List<(int Surface, string Name, string Catalog)>();
            // Optiland JSON uses literal Infinity/-Infinity which is not valid JSON.
            // Replace with numeric placeholders before parsing.
            string rawJson = File.ReadAllText(filePath);
            rawJson = rawJson.Replace("-Infinity", "-1e308").Replace("Infinity", "1e308");

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var system = new OpticalSystem();

            // Aperture
            if (root.TryGetProperty("aperture", out var apertureProp) && apertureProp.ValueKind == JsonValueKind.Object)
            {
                string apertureType = GetString(apertureProp, "type", "EPD");
                double apertureValue = GetDouble(apertureProp, "value", 10.0);

                if (apertureType.Equals("EPD", StringComparison.OrdinalIgnoreCase))
                    system.Aperture = new Aperture(ApertureType.EPD, apertureValue);
                else if (apertureType.Equals("imageFNO", StringComparison.OrdinalIgnoreCase))
                    system.Aperture = new Aperture(ApertureType.FNumber, apertureValue);
                else if (apertureType.Equals("objectNA", StringComparison.OrdinalIgnoreCase))
                    system.Aperture = new Aperture(ApertureType.ObjectSpaceNA, apertureValue);
                else
                    system.Aperture = new Aperture(ApertureType.EPD, apertureValue);
            }

            // Fields
            if (root.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Object)
            {
                // field_type can be at top level or inside field_definition
                string fieldType = GetString(fieldsProp, "field_type", "");
                if (string.IsNullOrEmpty(fieldType) && fieldsProp.TryGetProperty("field_definition", out var fdProp) && fdProp.ValueKind == JsonValueKind.Object)
                    fieldType = GetString(fdProp, "field_type", "angle");
                if (string.IsNullOrEmpty(fieldType)) fieldType = "angle";

                system.FieldType = (fieldType.IndexOf("height", StringComparison.OrdinalIgnoreCase) >= 0
                    || fieldType.Equals("ObjectHeight", StringComparison.OrdinalIgnoreCase))
                    ? FieldType.ObjectHeight
                    : FieldType.ObjectAngle;

                if (fieldsProp.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in fieldArray.EnumerateArray())
                    {
                        double y = GetDouble(f, "y", 0);
                        system.Fields.Add(new Field(y, 1.0));
                    }
                }
            }
            if (system.Fields.Count == 0)
                system.Fields.Add(new Field(0, 1.0));

            FieldValidation.FilterImportedFields(system);

            // Wavelengths
            if (root.TryGetProperty("wavelengths", out var wlProp) && wlProp.ValueKind == JsonValueKind.Object)
            {
                if (wlProp.TryGetProperty("wavelengths", out var wlArray) && wlArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in wlArray.EnumerateArray())
                    {
                        double value = GetDouble(w, "value", 0.55);
                        bool isPrimary = GetBool(w, "is_primary", false);
                        double weight = GetDouble(w, "weight", 1.0);
                        string unit = GetString(w, "unit", "um");

                        // Convert to micrometers
                        if (unit.Equals("nm", StringComparison.OrdinalIgnoreCase))
                            value /= 1000.0;
                        else if (unit.Equals("mm", StringComparison.OrdinalIgnoreCase))
                            value *= 1000.0;

                        system.Wavelengths.Add(new Wavelength(value, weight, isPrimary));
                    }
                }
            }
            if (system.Wavelengths.Count == 0)
                system.Wavelengths.Add(new Wavelength(0.55, 1.0, true));

            // Surfaces
            if (root.TryGetProperty("surface_group", out var sgProp) && sgProp.ValueKind == JsonValueKind.Object)
            {
                if (sgProp.TryGetProperty("surfaces", out var surfArray) && surfArray.ValueKind == JsonValueKind.Array)
                {
                    var surfElements = new List<JsonElement>();
                    foreach (var s in surfArray.EnumerateArray())
                        surfElements.Add(s);

                    // First pass: extract Z coordinates for thickness computation
                    var zCoords = new List<double>();
                    foreach (var s in surfElements)
                    {
                        double z = 0;
                        if (s.TryGetProperty("geometry", out var geom) && geom.ValueKind == JsonValueKind.Object)
                        {
                            if (geom.TryGetProperty("cs", out var cs) && cs.ValueKind == JsonValueKind.Object)
                                z = GetDouble(cs, "z", 0);
                        }
                        zCoords.Add(z);
                    }

                    // Second pass: build surfaces
                    for (int i = 0; i < surfElements.Count; i++)
                    {
                        var s = surfElements[i];
                        var surface = new Surface { Index = i };

                        // Geometry
                        if (s.TryGetProperty("geometry", out var geom) && geom.ValueKind == JsonValueKind.Object)
                        {
                            string geomType = GetString(geom, "type", "Plane");
                            double radius = GetDouble(geom, "radius", 1e308);

                            // Convert any infinity sentinel back to real
                            // PositiveInfinity (writer now uses 1e30; older
                            // exports use 1e308 or string-replaced Infinity).
                            if (Math.Abs(radius) >= 1e10)
                                surface.Radius = double.PositiveInfinity;
                            else
                                surface.Radius = radius;

                            if (geomType.Equals("EvenAsphere", StringComparison.OrdinalIgnoreCase))
                            {
                                surface.Type = SurfaceType.EvenAsphere;
                                double conic = GetDouble(geom, "conic", 0);
                                surface.Conic = conic;

                                if (geom.TryGetProperty("coefficients", out var coeffs) && coeffs.ValueKind == JsonValueKind.Array)
                                {
                                    // Optiland's coefficients start at r^2, exactly as this
                                    // program's AsphericCoefficients do, so they copy across
                                    // position for position. MEASURED: an even asphere built in
                                    // Optiland 0.6.2 with coefficients [1e-7, 2e-11, 3e-15] has
                                    // the sag of 1e-7 r^2 + 2e-11 r^4 + 3e-15 r^6 to twelve
                                    // figures, and Optiland's own writer stores the list exactly
                                    // so. This reader used to start them at r^4, on the strength
                                    // of a hand-written fixture that Optiland itself cannot even
                                    // load, so every aspheric Optiland file came in one power too
                                    // high (September 2026).
                                    int idx = 0;
                                    foreach (var c in coeffs.EnumerateArray())
                                    {
                                        if (idx < surface.AsphericCoefficients.Length)
                                            surface.AsphericCoefficients[idx] = c.GetDouble();
                                        idx++;
                                    }
                                }
                            }
                            else if (geomType.Equals("StandardGeometry", StringComparison.OrdinalIgnoreCase))
                            {
                                double conic = GetDouble(geom, "conic", 0);
                                surface.Conic = conic;
                                if (conic != 0)
                                    surface.Type = SurfaceType.EvenAsphere;
                            }
                            // "Plane" → default Standard with infinite radius (already set)
                        }

                        // Thickness: prefer explicit "thickness" field, fall back to Z deltas
                        double explicitThickness = GetDouble(s, "thickness", double.NaN);
                        if (!double.IsNaN(explicitThickness))
                        {
                            surface.Thickness = explicitThickness;
                        }
                        else if (i < surfElements.Count - 1)
                        {
                            double zCurrent = zCoords[i];
                            double zNext = zCoords[i + 1];

                            // Catch any sentinel ≥ 1e10. We write 1e30 for
                            // infinity now; some converted files use 1e20;
                            // older tools use 1e10 / 1e11. All bigger than
                            // any real track distance.
                            if (Math.Abs(zCurrent) >= 1e10) // object at infinity
                                surface.Thickness = double.PositiveInfinity;
                            else
                                surface.Thickness = zNext - zCurrent;
                        }
                        else
                        {
                            surface.Thickness = 0; // image surface
                        }

                        // The object: at infinity when Optiland puts it there (z = -inf), otherwise
                        // at its distance from the first surface. Optiland writes a finite object at
                        // z = -d with the first surface at 0. (Every object was read as at infinity,
                        // so a lens at a finite conjugate came in as another lens.)
                        // An explicit object thickness wins; one of 1e10 or more is infinity. With
                        // none, and no separation from the first surface, the object is at infinity
                        // as it always was.
                        if (i == 0)
                        {
                            double z0 = zCoords[0];
                            double t = !double.IsNaN(explicitThickness) ? explicitThickness
                                     : double.IsInfinity(z0) || double.IsNaN(z0) || zCoords.Count < 2 ? double.PositiveInfinity
                                     : zCoords[1] - z0;
                            surface.Thickness = double.IsInfinity(t) || double.IsNaN(t) || Math.Abs(t) >= 1e10 || t <= 0.0
                                ? double.PositiveInfinity
                                : t;
                        }

                        // Material (from material_post)
                        if (s.TryGetProperty("material_post", out var matPost) && matPost.ValueKind == JsonValueKind.Object)
                            ReadMaterial(matPost, surface, i, named, notes);

                        // Mirror: check top-level is_reflective and interaction_model.is_reflective
                        bool isReflective = GetBool(s, "is_reflective", false);
                        if (!isReflective && s.TryGetProperty("interaction_model", out var imProp) && imProp.ValueKind == JsonValueKind.Object)
                            isReflective = GetBool(imProp, "is_reflective", false);
                        if (isReflective)
                            surface.Material = "MIRROR";

                        // Stop
                        surface.IsStop = GetBool(s, "is_stop", false);

                        // Semi-diameter (if aperture specified)
                        if (s.TryGetProperty("aperture", out var ap) && ap.ValueKind == JsonValueKind.Object)
                        {
                            double sd = GetDouble(ap, "semi_diameter", 0);
                            if (sd > 0)
                            {
                                surface.SemiDiameter = sd;
                                surface.SemiDiameterMode = SemiDiameterMode.Fixed;
                            }
                        }

                        system.Surfaces.Add(surface);
                    }
                }
            }

            OrderCatalogs(system, named, glass, notes);

            if (glass != null)
                foreach (var n in named)
                    if (glass.Find(n.Name, system.GlassCatalogs) == null)
                        notes.Add($"Surface {n.Surface}: glass {n.Name} is not in the loaded catalogs.");

            if (notes.Count > 0)
            {
                string text = string.Join(Environment.NewLine, notes);
                system.Notes = string.IsNullOrEmpty(system.Notes) ? text : system.Notes + Environment.NewLine + text;
            }

            // Ensure at least object + image
            if (system.Surfaces.Count < 2)
            {
                system.Surfaces.Clear();
                system.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
                system.Surfaces.Add(new Surface { Index = 1 });
            }

            return system;
        }

        private static void ReadMaterial(JsonElement mat, Surface surface, int index,
                                         List<(int Surface, string Name, string Catalog)> named, List<string> notes)
        {
            var inv = CultureInfo.InvariantCulture;
            string type = GetString(mat, "type", "IdealMaterial");
            string name = "", catalog = "";

            if (type.Equals("Material", StringComparison.OrdinalIgnoreCase))
            {
                name = GetString(mat, "name", "");
                catalog = GetString(mat, "catalog", "");
            }
            else if (type.Equals("MaterialFile", StringComparison.OrdinalIgnoreCase))
            {
                // A catalog's file: <catalog>/<name>.yml.
                string file = GetString(mat, "filename", "").Replace('\\', '/');
                name = Path.GetFileNameWithoutExtension(file);
                catalog = Path.GetFileName(Path.GetDirectoryName(file) ?? "") ?? "";
            }
            else if (type.Equals("AbbeMaterial", StringComparison.OrdinalIgnoreCase))
            {
                double n = GetDouble(mat, "index", double.NaN), v = GetDouble(mat, "abbe", double.NaN);
                if (n > 1.0 && v > 0.0)
                {
                    SetModel(surface, n, v, 0.0);
                    notes.Add(string.Format(inv,
                        "Surface {0}: Optiland's Abbe material nd {1}, Vd {2} is the LensHH-LT model glass of that nd and Vd; the two dispersion models differ away from the d line.",
                        index, n, v));
                }
                return;
            }
            else if (type.Equals("IdealMaterial", StringComparison.OrdinalIgnoreCase))
            {
                double n = GetDouble(mat, "index", 1.0);
                if (Math.Abs(n - 1.0) > 1e-12)
                {
                    SetModel(surface, n, 0.0, 0.0);   // Vd 0: a constant index
                    notes.Add(string.Format(inv, "Surface {0}: an ideal material of index {1}, a model glass without dispersion.",
                        index, n));
                }
                return;
            }
            else
            {
                notes.Add($"Surface {index}: an Optiland {type} material, not imported.");
                return;
            }

            if (TryParseModelName(name, out double nd, out double vd, out double dPgF))
            {
                SetModel(surface, nd, vd, dPgF);
                return;
            }
            if (string.IsNullOrEmpty(name)) return;

            surface.Material = name;
            if (!string.IsNullOrEmpty(catalog))
            {
                if (catalog.StartsWith("lenshh-", StringComparison.OrdinalIgnoreCase)) catalog = catalog.Substring(7);
                named.Add((index, name, catalog));
            }
        }

        // LensHH-LT writes a model glass for Optiland as MODEL_<nd>_<Vd>_<dPgF>.
        private static bool TryParseModelName(string name, out double nd, out double vd, out double dPgF)
        {
            nd = vd = dPgF = 0.0;
            if (!name.StartsWith("MODEL_", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = name.Substring(6).Split('_');
            var inv = CultureInfo.InvariantCulture;
            return parts.Length == 3
                && double.TryParse(parts[0], NumberStyles.Float, inv, out nd)
                && double.TryParse(parts[1], NumberStyles.Float, inv, out vd)
                && double.TryParse(parts[2], NumberStyles.Float, inv, out dPgF)
                && nd > 1.0;
        }

        private static void SetModel(Surface surface, double nd, double vd, double dPgF)
        {
            surface.Material = null;
            surface.ModelIndexEnabled = true;
            surface.ModelNd = nd;
            surface.ModelVd = vd;
            surface.ModelDPgF = dPgF;
        }

        /// <summary>
        /// The system's catalog preference, from the catalogs the file names for its glasses.
        /// A surface carries a glass name only, and a name resolves by the catalog order. So the
        /// order puts each glass's own catalog ahead of any other listed catalog that also has
        /// that name. Where two glasses ask for opposite orders, each glass that then resolves to
        /// another catalog's glass is noted.
        /// </summary>
        private static void OrderCatalogs(OpticalSystem system, List<(int Surface, string Name, string Catalog)> named,
                                          GlassCatalog? glass, List<string> notes)
        {
            // Optiland's catalog name to ours: the loaded catalog of that name, both halves of
            // Corning for "corning", and otherwise the name in capitals.
            List<string> Ours(string catalog)
            {
                var found = new List<string>();
                if (glass != null)
                    foreach (var loaded in glass.LoadedCatalogs)
                        if (loaded.Equals(catalog, StringComparison.OrdinalIgnoreCase)
                            || (catalog.Equals("corning", StringComparison.OrdinalIgnoreCase)
                                && loaded.StartsWith("CORNING", StringComparison.OrdinalIgnoreCase)))
                            found.Add(loaded);
                if (found.Count == 0) found.Add(catalog.ToUpperInvariant());
                return found;
            }
            bool Has(string catalog, string name) => glass?.Find(catalog.ToUpperInvariant() + ":" + name) != null;

            var order = new List<string>();
            foreach (var n in named)
                foreach (var c in Ours(n.Catalog))
                    if (!order.Contains(c, StringComparer.OrdinalIgnoreCase)) order.Add(c);
            if (order.Count == 0) return;

            if (glass != null)
            {
                var before = order.ToDictionary(c => c, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                                                StringComparer.OrdinalIgnoreCase);
                foreach (var n in named)
                {
                    var own = Ours(n.Catalog);
                    if (!own.Any(c => Has(c, n.Name))) continue;
                    foreach (var other in order)
                        if (!own.Contains(other, StringComparer.OrdinalIgnoreCase) && Has(other, n.Name))
                            foreach (var c in own) before[other].Add(c);
                }

                // Kahn's order, the file's order breaking ties; a cycle leaves the rest in file order.
                var sorted = new List<string>();
                var left = new List<string>(order);
                while (left.Count > 0)
                {
                    var next = left.FirstOrDefault(c => before[c].All(p => sorted.Contains(p, StringComparer.OrdinalIgnoreCase)
                                                                         || !left.Contains(p, StringComparer.OrdinalIgnoreCase)))
                               ?? left[0];
                    sorted.Add(next);
                    left.Remove(next);
                }
                order = sorted;
            }

            foreach (var c in order)
                if (!system.GlassCatalogs.Contains(c, StringComparer.OrdinalIgnoreCase)) system.GlassCatalogs.Add(c);

            if (glass == null) return;
            foreach (var n in named)
            {
                var own = Ours(n.Catalog);
                var got = glass.Find(n.Name, system.GlassCatalogs);
                if (got != null && !own.Contains(got.Catalog, StringComparer.OrdinalIgnoreCase) && own.Any(c => Has(c, n.Name)))
                    notes.Add($"Surface {n.Surface}: glass {n.Name} is {n.Catalog}'s in the file; "
                            + $"with this lens's other glasses, the catalog order gives {got.Catalog}'s.");
            }
        }

        private static string GetString(JsonElement el, string prop, string defaultValue)
        {
            if (el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString() ?? defaultValue;
            return defaultValue;
        }

        private static double GetDouble(JsonElement el, string prop, double defaultValue)
        {
            if (el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
                return val.GetDouble();
            return defaultValue;
        }

        private static bool GetBool(JsonElement el, string prop, bool defaultValue)
        {
            if (el.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.True) return true;
                if (val.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }
    }
}
