using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the .lhlt it came from, by EDITING that file rather
    /// than regenerating it.
    ///
    /// <para><b>Why editing and not writing afresh.</b> A .lhlt carries a
    /// great deal this program has no model for - a merit function, vignetting settings, glass
    /// substitution lists, whatever the next version adds. Serialising this program's
    /// <see cref="OpticalSystem"/> over the top would silently drop every one of them, and the
    /// user would discover it when they next opened their design. So the original document is
    /// parsed, the handful of values this program is entitled to change are changed, and
    /// everything else is written back exactly as it was found.</para>
    ///
    /// <para><b>The merit function is deliberately untouched.</b> This program does not read it -
    /// it optimises a merit function of its own - and a setting one does not understand is a
    /// setting one has no business overwriting. This program's merit function goes to a sidecar
    /// file beside the lens instead.</para>
    ///
    /// <para>What IS written: curvatures, thicknesses, glass names, conics and aspheric terms
    /// where the optimiser moved them; the variable flags and bounds, which are the user's statement of what may move; and
    /// the pickups. Nothing else.</para>
    /// </summary>
    public static class LhltPatcher
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        /// <summary>
        /// Applies <paramref name="system"/> to the document in <paramref name="originalPath"/>
        /// and writes the result to <paramref name="outputPath"/>. The two may be the same file.
        /// </summary>
        public static void Patch(OpticalSystem system, string originalPath, string outputPath)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (originalPath == null) throw new ArgumentNullException(nameof(originalPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            string json = File.ReadAllText(originalPath);
            var root = JsonNode.Parse(json, nodeOptions: null,
                                      documentOptions: new JsonDocumentOptions
                                      {
                                          AllowTrailingCommas = true,
                                          CommentHandling = JsonCommentHandling.Skip,
                                      })
                       ?? throw new InvalidOperationException(
                              $"'{originalPath}' is not a JSON document this program can edit.");

            PatchSurfaces(system, root);
            PatchPickups(system, root);

            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, root.ToJsonString(WriteOptions));
        }

        private static void PatchSurfaces(OpticalSystem system, JsonNode root)
        {
            if (root["Surfaces"] is not JsonArray surfaces) return;

            int count = Math.Min(surfaces.Count, system.Surfaces.Count);
            for (int i = 0; i < count; i++)
            {
                if (surfaces[i] is not JsonObject node) continue;
                var s = system.Surfaces[i];

                // Distances go back in the file's own units. A .lhlt is written in millimetres,
                // so the scale is one - but going through it costs nothing and means the rule is
                // stated in one place for every format.
                double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;

                SetIfChanged(node, "Radius", Divide(s.Radius, scale));
                SetIfChanged(node, "Thickness", Divide(s.Thickness, scale));
                SetIfChanged(node, "Conic", s.Conic);
                SetTextIfChanged(node, "Material", s.Material ?? string.Empty);

                // The figuring, which the optimiser can now move. Written back unscaled, exactly
                // as the reader takes it in: an r^4 coefficient is 1/length^3 and an r^8 one is
                // 1/length^7, so a unit scale applied to them is not the file's scale but its
                // cube or its seventh power, and getting that wrong would be silent. The reader
                // does not scale them either, and the two have to agree.
                //
                // THIS BLOCK IS NEW WITH THE FIGURING VARIABLES, and its absence was harmless
                // only for as long as nothing could change a coefficient: an optimised design
                // would have been written back carrying the figuring it started with, with no
                // error anywhere and the result quietly discarded.
                SetArray(node, "AsphericCoefficients", s.AsphericCoefficients,
                         Array.Exists(s.AsphericCoefficients, a => a != 0.0));

                // The user's own statement of what may be optimised, and how far, written back
                // whether or not the optimiser moved anything - it is a setting, not a result.
                node["CurvatureVariable"] = s.CurvatureVariable;
                node["ThicknessVariable"] = s.ThicknessVariable;
                node["ConicVariable"] = s.ConicVariable;

                SetArray(node, "AsphericVariable", s.AsphericVariable,
                         Array.Exists(s.AsphericVariable, b => b));

                SetBound(node, "CurvatureMin", s.CurvatureMin, Divide: false);
                SetBound(node, "CurvatureMax", s.CurvatureMax, Divide: false);
                SetBound(node, "ThicknessMin", s.ThicknessMin, Divide: true, scale);
                SetBound(node, "ThicknessMax", s.ThicknessMax, Divide: true, scale);
            }
        }

        /// <summary>
        /// Replaces the pickup list wholesale.
        ///
        /// <para>A pickup is a relationship rather than a value, so there is no sensible way to
        /// patch one in place: adding or removing one changes the list's shape. The list is this
        /// program's to own once the user has edited it, and it is small.</para>
        /// </summary>
        private static void PatchPickups(OpticalSystem system, JsonNode root)
        {
            var array = new JsonArray();
            foreach (var p in system.Pickups)
            {
                array.Add(new JsonObject
                {
                    ["TargetSurfaceIndex"] = p.TargetSurfaceIndex,
                    ["Parameter"] = p.Parameter.ToString(),
                    ["SourceSurfaceIndex"] = p.SourceSurfaceIndex,
                    ["SourceConfigurationIndex"] = p.SourceConfigurationIndex,
                    ["ScaleFactor"] = p.ScaleFactor,
                    ["Offset"] = p.Offset,
                    ["ParameterIndex"] = p.ParameterIndex,
                });
            }
            root["Pickups"] = array;
        }

        /// <summary>
        /// Writes a number only if it differs from what the file already says.
        ///
        /// <para>"Only change what changed" is the whole contract of this writer, and the cheapest
        /// way to keep it is to compare. It also preserves how the file chose to SPELL a value -
        /// an object distance stored as the string "Infinity" stays that string rather than being
        /// re-rendered, because nothing about it changed.</para>
        /// </summary>
        /// <summary>
        /// Writes an array back, creating the key if the file has none and there is something to
        /// record.
        ///
        /// <para><b>Creating a key is a departure from this file's rule</b> that only what the
        /// optimiser moved may change, so it is done under one condition: the design carries a
        /// value the key would hold. A .lhlt written for a spherical lens has no
        /// AsphericCoefficients at all, and if the optimiser has just figured one of its
        /// surfaces then leaving the key out is not preservation, it is losing the answer.
        /// Where there is nothing to record and no key, nothing is added - a file that was
        /// clean stays clean.</para>
        /// </summary>
        /// <para><b>Not generic, and that is not an oversight.</b> A generic
        /// <c>JsonValue.Create&lt;T&gt;</c> binds to the customized-value overload rather than to
        /// the primitive one, and the node it builds needs a TypeInfoResolver that this
        /// serializer does not set - so it writes nothing and throws only when the whole
        /// document is serialised, several frames away from the call that caused it. Two
        /// concrete overloads bind to the primitive Create and the problem cannot arise.</para>
        private static void SetArray(JsonObject node, string key, double[] values, bool hasContent)
        {
            var existing = node[key] as JsonArray;
            if (existing == null && !hasContent) return;

            if (existing == null)
            {
                var fresh = new JsonArray();
                for (int k = 0; k < values.Length; k++) fresh.Add(JsonValue.Create(values[k]));
                node[key] = fresh;
                return;
            }

            int n = Math.Min(existing.Count, values.Length);
            for (int k = 0; k < n; k++) existing[k] = JsonValue.Create(values[k]);
        }

        private static void SetArray(JsonObject node, string key, bool[] values, bool hasContent)
        {
            var existing = node[key] as JsonArray;
            if (existing == null && !hasContent) return;

            if (existing == null)
            {
                var fresh = new JsonArray();
                for (int k = 0; k < values.Length; k++) fresh.Add(JsonValue.Create(values[k]));
                node[key] = fresh;
                return;
            }

            int n = Math.Min(existing.Count, values.Length);
            for (int k = 0; k < n; k++) existing[k] = JsonValue.Create(values[k]);
        }

        private static void SetIfChanged(JsonObject node, string key, double value)
        {
            double existing = ReadNumber(node, key);

            if (double.IsInfinity(value) && double.IsInfinity(existing)
                && Math.Sign(value) == Math.Sign(existing)) return;
            if (double.IsNaN(value) && double.IsNaN(existing)) return;

            if (!double.IsInfinity(value) && !double.IsInfinity(existing)
                && !double.IsNaN(value) && !double.IsNaN(existing))
            {
                double tolerance = 1e-12 * Math.Max(1.0, Math.Abs(existing));
                if (Math.Abs(value - existing) <= tolerance) return;
            }

            // An infinity has to go back the way this format writes one: as a string, which is
            // what JsonNumberHandling.AllowNamedFloatingPointLiterals reads back.
            node[key] = double.IsInfinity(value) || double.IsNaN(value)
                      ? JsonValue.Create(value > 0 ? "Infinity" : "-Infinity")
                      : JsonValue.Create(value);
        }

        private static void SetTextIfChanged(JsonObject node, string key, string value)
        {
            string existing = node[key]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(existing, value, StringComparison.Ordinal)) return;
            node[key] = value;
        }

        /// <summary>
        /// Writes a bound, or REMOVES the key when the bound is infinite.
        ///
        /// <para>An absent key is how this format says "unbounded". Writing an infinity instead
        /// would leave a file full of limits nobody set, and a reader that does not expect the
        /// named literal would choke on it.</para>
        /// </summary>
        private static void SetBound(JsonObject node, string key, double value,
                                     bool Divide, double scale = 1.0)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                node.Remove(key);
                return;
            }
            node[key] = Divide && scale > 0.0 ? value / scale : value;
        }

        private static double ReadNumber(JsonObject node, string key)
        {
            var value = node[key];
            if (value == null) return double.NaN;

            // The format writes an infinite distance as a string and everything else as a number,
            // so both spellings have to be understood here.
            if (value is JsonValue v)
            {
                if (v.TryGetValue(out double d)) return d;
                if (v.TryGetValue(out string? text) && text != null)
                {
                    if (text.Equals("Infinity", StringComparison.OrdinalIgnoreCase))
                        return double.PositiveInfinity;
                    if (text.Equals("-Infinity", StringComparison.OrdinalIgnoreCase))
                        return double.NegativeInfinity;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                                        out double parsed))
                        return parsed;
                }
            }
            return double.NaN;
        }

        private static double Divide(double value, double scale) =>
            double.IsInfinity(value) || double.IsNaN(value) || scale <= 0.0 ? value : value / scale;
    }
}
