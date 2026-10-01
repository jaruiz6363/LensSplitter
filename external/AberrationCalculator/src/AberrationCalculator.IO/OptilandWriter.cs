using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>Where an Optiland export put the lens's glasses.</summary>
    public sealed class OptilandExport
    {
        /// <summary>The folder of glasses beside the lens file, or null when it has none.</summary>
        public string? GlassFolder { get; set; }

        /// <summary>The Optiland catalogs folder the glasses were installed in, or null.</summary>
        public string? InstalledTo { get; set; }

        public int InstalledGlasses { get; set; }

        /// <summary>Why installing failed, or null.</summary>
        public string? InstallError { get; set; }

        /// <summary>Glasses the catalogs do not have, written by name only.</summary>
        public List<string> ByNameOnly { get; } = new();

        /// <summary>Surfaces with an r^2 aspheric term, which Optiland's paraxial values leave out.</summary>
        public List<int> R2Surfaces { get; } = new();

        /// <summary>One or two lines saying where the glasses went.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            if (GlassFolder == null) sb.Append("The lens has no glass, so there were no glasses to write.");
            else
            {
                if (InstalledTo != null)
                    sb.Append($"The glasses are installed for Optiland in {InstalledTo}; Optiland reads them when it "
                            + "starts (restart a Python session that already had Optiland loaded). ");
                else if (InstallError != null)
                    sb.Append(InstallError + ". Copy the folders inside the folder below into ~/.optiland/catalogs/ by hand. ");
                sb.Append($"A copy is in {GlassFolder}, to take with the lens to another machine.");
            }
            if (ByNameOnly.Count > 0)
                sb.Append($" Not in the glass catalogs, so written by name only: {string.Join(", ", ByNameOnly)}.");
            if (R2Surfaces.Count > 0)
                sb.Append($" Surface{(R2Surfaces.Count > 1 ? "s" : "")} {string.Join(", ", R2Surfaces)} "
                        + $"{(R2Surfaces.Count > 1 ? "have" : "has")} an r² aspheric term. Optiland traces it in real "
                        + "rays, but its paraxial values - focal length, pupils - leave it out, so they differ from "
                        + "the lens's; a real ray near the axis gives the true focal length.");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Writes a whole lens as an Optiland .json file, with its glasses as <see cref="OptilandGlass"/>
    /// describes: each named strictly with its own catalog, its dispersion data beside the lens and
    /// installed where Optiland reads it. Optiland's even asphere starts at r^2, as this program's
    /// coefficients do, so every term is written as it stands.
    ///
    /// <para>Ported from LensHH-LT's writer (MIT, Synapse Optics). Two things differ from it: an
    /// object-space NA is written as Optiland's <c>objectNA</c> (it went out as an EPD of the same
    /// number), and each field's x is written (it went out as 0).</para>
    /// </summary>
    public static class OptilandWriter
    {
        private const string Air = "{\"type\": \"IdealMaterial\", \"index\": 1.0, \"absorp\": 0.0}";

        /// <param name="install">Whether to install the glasses into this machine's Optiland
        /// catalogs as well as writing them beside the lens.</param>
        public static OptilandExport Write(OpticalSystem system, string filePath, GlassCatalog? glass, bool install = true)
        {
            new WriterSupport(system, glass, "Optiland").RefuseUnwritable(paraxialAllowed: false);
            var result = new OptilandExport();
            var materials = Materials(system, glass, result, out var ymls);
            const string I1 = "    ", I2 = I1 + I1, I3 = I2 + I1, I4 = I3 + I1, I5 = I4 + I1, I6 = I5 + I1;
            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine($"{I1}\"version\": 1.0,");
            string apertureType = system.Aperture.Type switch
            {
                ApertureType.FNumber => "imageFNO",
                ApertureType.ObjectSpaceNA => "objectNA",
                _ => "EPD",
            };
            sb.AppendLine($"{I1}\"aperture\": {{");
            sb.AppendLine($"{I2}\"type\": \"{apertureType}\",");
            sb.AppendLine($"{I2}\"value\": {Num(system.Aperture.Value)},");
            sb.AppendLine($"{I2}\"object_space_telecentric\": {Bool(system.TelecentricObjectSpace)}");
            sb.AppendLine($"{I1}}},");

            string fieldType = system.FieldType == FieldType.ObjectHeight ? "object_height" : "angle";
            sb.AppendLine($"{I1}\"fields\": {{");
            sb.AppendLine($"{I2}\"fields\": [");
            for (int i = 0; i < system.Fields.Count; i++)
            {
                var f = system.Fields[i];
                sb.AppendLine($"{I3}{{\"field_type\": \"{fieldType}\", \"x\": {Num(f.X)}, \"y\": {Num(f.Y)}, \"vx\": 0.0, \"vy\": 0.0}}"
                              + (i < system.Fields.Count - 1 ? "," : ""));
            }
            sb.AppendLine($"{I2}],");
            sb.AppendLine($"{I2}\"telecentric\": false,");
            sb.AppendLine($"{I2}\"field_type\": \"{fieldType}\",");
            sb.AppendLine($"{I2}\"object_space_telecentric\": {Bool(system.TelecentricObjectSpace)}");
            sb.AppendLine($"{I1}}},");

            int primary = system.PrimaryWavelengthIndex;
            sb.AppendLine($"{I1}\"wavelengths\": {{");
            sb.AppendLine($"{I2}\"wavelengths\": [");
            for (int i = 0; i < system.Wavelengths.Count; i++)
            {
                var w = system.Wavelengths[i];
                sb.AppendLine($"{I3}{{\"value\": {Num(w.Value)}, \"is_primary\": {Bool(i == primary)}, \"unit\": \"um\", \"weight\": {Num(w.Weight)}}}"
                              + (i < system.Wavelengths.Count - 1 ? "," : ""));
            }
            sb.AppendLine($"{I2}],");
            sb.AppendLine($"{I2}\"polarization\": \"ignore\"");
            sb.AppendLine($"{I1}}},");
            sb.AppendLine($"{I1}\"pickups\": [],");
            sb.AppendLine($"{I1}\"solves\": {{\"solves\": []}},");

            sb.AppendLine($"{I1}\"surface_group\": {{");
            sb.AppendLine($"{I2}\"surfaces\": [");
            double z = 0.0;
            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                bool isObject = i == 0;
                double zi = isObject && double.IsInfinity(s.Thickness) ? double.NegativeInfinity : z;
                if (!double.IsInfinity(s.Thickness) && !double.IsNaN(s.Thickness)) z += s.Thickness;

                bool asphere = s.AsphericCoefficients.Any(c => c != 0.0);
                // Measured in Optiland 0.6.2: a near-axis real ray sees the r^2 term, its paraxial
                // trace does not.
                if (s.AsphericCoefficients.Length > 0 && s.AsphericCoefficients[0] != 0.0) result.R2Surfaces.Add(i);
                string geometry = asphere ? "EvenAsphere" : double.IsInfinity(s.Radius) && s.Conic == 0 ? "Plane" : "StandardGeometry";

                sb.AppendLine($"{I3}{{");
                sb.AppendLine($"{I4}\"type\": \"{(isObject ? "ObjectSurface" : "Surface")}\",");
                sb.AppendLine($"{I4}\"geometry\": {{");
                sb.AppendLine($"{I5}\"type\": \"{geometry}\",");
                sb.AppendLine($"{I5}\"cs\": {{");
                sb.AppendLine($"{I6}\"x\": 0.0, \"y\": 0.0, \"z\": {Num(zi)},");
                sb.AppendLine($"{I6}\"rx\": 0.0, \"ry\": 0.0, \"rz\": 0.0,");
                sb.AppendLine($"{I6}\"reference_cs\": null");
                sb.AppendLine($"{I5}}},");
                sb.Append($"{I5}\"radius\": {Num(s.Radius)}");
                if (geometry != "Plane")
                    sb.Append($",\n{I5}\"conic\": {Num(s.Conic)}");
                if (asphere)
                {
                    // Trailing zeros dropped; the list starts at r^2, as Optiland's does.
                    int last = Array.FindLastIndex(s.AsphericCoefficients, c => c != 0.0);
                    sb.Append($",\n{I5}\"coefficients\": [" + string.Join(", ",
                        s.AsphericCoefficients.Take(last + 1).Select(Num)) + "]");
                }
                sb.AppendLine();
                sb.AppendLine($"{I4}}},");

                if (!isObject)
                    sb.AppendLine($"{I4}\"material_pre\": {materials[i - 1] ?? Air},");
                sb.AppendLine($"{I4}\"material_post\": {materials[i] ?? Air}{(isObject ? "" : ",")}");
                if (!isObject)
                {
                    sb.AppendLine($"{I4}\"is_stop\": {Bool(s.IsStop)},");
                    sb.AppendLine($"{I4}\"aperture\": null,");
                    sb.AppendLine($"{I4}\"coating\": null,");
                    sb.AppendLine($"{I4}\"bsdf\": null,");
                    sb.AppendLine($"{I4}\"is_reflective\": {Bool(s.IsMirror)}");
                }
                sb.AppendLine($"{I3}}}" + (i < system.Surfaces.Count - 1 ? "," : ""));
            }
            sb.AppendLine($"{I2}]");
            sb.AppendLine($"{I1}}}");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
            result.GlassFolder = WriteGlassFolder(filePath, ymls);
            if (install && ymls.Count > 0) Install(ymls, result);
            return result;
        }

        // The material_post of each surface, null for air and mirrors; and each glass's .yml, by
        // catalog then name.
        private static string?[] Materials(OpticalSystem system, GlassCatalog? glass, OptilandExport result,
            out SortedDictionary<string, SortedDictionary<string, string>> ymls)
        {
            ymls = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var materials = new string?[system.Surfaces.Count];
            IReadOnlyList<string>? preferred = system.GlassCatalogs.Count > 0 ? system.GlassCatalogs : null;

            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                if (s.IsMirror) continue;
                if (s.ModelIndexEnabled)
                {
                    string name = OptilandGlass.ModelName(s.ModelNd, s.ModelVd, s.ModelDPgF);
                    var (formula, c) = OptilandGlass.ModelDispersion(s.ModelNd, s.ModelVd, s.ModelDPgF);
                    string description = string.Format(WriterSupport.Inv, "Model glass: nd {0:R}, Vd {1:R}, dPgF {2:R}",
                        s.ModelNd, s.ModelVd, s.ModelDPgF);
                    Add(ymls, OptilandGlass.ModelCatalog, name, OptilandGlass.Yml(description, formula, c, 0.2, 5.0));
                    materials[i] = MaterialJson(name, OptilandGlass.ModelCatalog);
                    continue;
                }
                if (string.IsNullOrEmpty(s.Material) || s.Material.Equals("AIR", StringComparison.OrdinalIgnoreCase)) continue;

                var g = glass?.Find(s.Material, preferred);
                var dispersion = g != null ? OptilandGlass.Dispersion(g) : null;
                if (g != null && dispersion != null && !string.IsNullOrEmpty(g.Catalog) && OptilandGlass.IsFileName(g.Name))
                {
                    string catalog = OptilandGlass.DataCatalog(g.Catalog);
                    var (formula, c) = dispersion.Value;
                    double lo = g.LambdaMin > 0 ? g.LambdaMin : 0.2;
                    double hi = g.LambdaMax > lo ? g.LambdaMax : 5.0;
                    Add(ymls, catalog, g.Name, OptilandGlass.Yml($"{g.Catalog} {g.Name}", formula, c, lo, hi));
                    materials[i] = MaterialJson(g.Name, catalog);
                }
                else
                {
                    // Not in any catalog here: the name alone, still strict, so Optiland either has
                    // exactly this glass or says it does not.
                    string? catalog = system.GlassCatalogs.Count == 1 ? OptilandGlass.VendorCatalog(system.GlassCatalogs[0]) : null;
                    materials[i] = MaterialJson(s.Material, catalog);
                    if (!result.ByNameOnly.Contains(s.Material)) result.ByNameOnly.Add(s.Material);
                }
            }
            return materials;
        }

        private static void Add(SortedDictionary<string, SortedDictionary<string, string>> ymls, string catalog, string name, string yml)
        {
            if (!ymls.TryGetValue(catalog, out var names))
                ymls[catalog] = names = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            names[name] = yml;
        }

        private static string MaterialJson(string name, string? catalog) =>
            "{\"type\": \"Material\", \"name\": " + JsonString(name)
            + ", \"reference\": null, \"catalog\": " + (catalog == null ? "null" : JsonString(catalog))
            + ", \"match_policy\": \"strict\", \"robust_search\": null"
            + ", \"min_wavelength\": null, \"max_wavelength\": null}";

        private static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in s)
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
                else sb.Append(ch);
            }
            return sb.Append('"').ToString();
        }

        // The glasses as Optiland user catalogs, in a folder named after the lens file, rewritten
        // whole so a glass the lens no longer uses does not linger.
        private static string? WriteGlassFolder(string filePath, SortedDictionary<string, SortedDictionary<string, string>> ymls)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
            string folder = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + "_glass");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            if (ymls.Count == 0) return null;
            foreach (var catalog in ymls)
            {
                string sub = Path.Combine(folder, catalog.Key);
                Directory.CreateDirectory(sub);
                foreach (var g in catalog.Value)
                    File.WriteAllText(Path.Combine(sub, g.Key + ".yml"), g.Value, new UTF8Encoding(false));
            }
            File.WriteAllText(Path.Combine(folder, "README.txt"), OptilandGlass.ReadMe(Path.GetFileName(filePath), ymls.Keys));
            return folder;
        }

        // Into this machine's Optiland user catalogs: only the lenshh- folders, nothing deleted -
        // other lenses may use the same glasses. A failure leaves the export itself good.
        private static void Install(SortedDictionary<string, SortedDictionary<string, string>> ymls, OptilandExport result)
        {
            string target = OptilandGlass.UserCatalogsFolder;
            try
            {
                int count = 0;
                foreach (var catalog in ymls)
                {
                    string sub = Path.Combine(target, catalog.Key);
                    Directory.CreateDirectory(sub);
                    foreach (var g in catalog.Value)
                    {
                        File.WriteAllText(Path.Combine(sub, g.Key + ".yml"), g.Value, new UTF8Encoding(false));
                        count++;
                    }
                }
                result.InstalledTo = target;
                result.InstalledGlasses = count;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                       || ex is System.Security.SecurityException || ex is ArgumentException)
            {
                result.InstallError = $"The glasses could not be installed in {target}: {ex.Message}";
            }
        }

        private static string Bool(bool b) => b ? "true" : "false";

        // Optiland's JSON takes the literal tokens Infinity and -Infinity, as Python's json does.
        private static string Num(double v) =>
            double.IsPositiveInfinity(v) ? "Infinity"
            : double.IsNegativeInfinity(v) ? "-Infinity"
            : v.ToString("R", WriterSupport.Inv);
    }
}
