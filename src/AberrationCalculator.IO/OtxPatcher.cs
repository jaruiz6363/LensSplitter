using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the .otx it came from.
    ///
    /// <para><c>SUR n</c> opens a surface and the indented keywords beneath it - <c>CUY</c>,
    /// <c>THI</c>, <c>GLA</c> - set its properties. The surface NUMBERS itself, so the blocks are
    /// matched by the number on the SUR line rather than by counting them.</para>
    ///
    /// <para>The surface shape is given as a CURVATURE, not a radius, which is the same quantity
    /// this program optimises - so a plane is an honest zero here and needs none of the
    /// zero-means-infinity translation the other two formats do.</para>
    ///
    /// <para><c>VAR</c> lines say which parameters that program was allowed to vary. They are left
    /// alone: what this optimiser was allowed to move is recorded in the sidecar, and rewriting
    /// somebody else's variable list from it would be answering a question nobody asked.</para>
    /// </summary>
    internal static class OtxPatcher
    {
        /// <summary>How the format spells an infinite object distance.</summary>
        private const string Infinite = "1.0000000000000000E+020";

        private const string DefaultIndent = "  ";

        public static void Patch(OpticalSystem system, string originalPath, string outputPath)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            var insertions = new List<(int Index, string Text)>();
            var map = SurfaceMap(file.Lines);

            int surface = -1;
            var role = Role.Plain;
            var block = new Block();

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;
                if (line.TrimStart().StartsWith("!", StringComparison.Ordinal)) continue;

                string keyword = LineEdit.Keyword(line);

                if (keyword == "SUR")
                {
                    if (role == Role.Plain)
                        Flush(system, surface, block, i, scale, insertions);
                    int number = LineEdit.ArgumentAsInt(line, -1);
                    (surface, role) = map.TryGetValue(number, out var m) ? m : (number, Role.Plain);
                    block = new Block();
                    continue;
                }

                if (surface < 0 || surface >= system.Surfaces.Count) continue;
                var s = system.Surfaces[surface];

                // An ideal lens is Optalix's lens module, two L surfaces the reader made one: the
                // first holds the gap between its principal planes, which is not ours, and the
                // second the distance on to the next surface - the ideal lens's thickness here.
                if (role != Role.Plain)
                {
                    if (keyword == "THI" && role == Role.ModuleExit)
                        file.Lines[i] = PatchThickness(line, s, scale);
                    continue;
                }

                switch (keyword)
                {
                    case "CUY":
                        block.Curvature = i;
                        block.Indent ??= LineEdit.Indent(line);
                        file.Lines[i] = LineEdit.ReplaceNumberIfChanged(line, s.Curvature * scale);
                        break;

                    case "THI":
                        block.Thickness = i;
                        block.Indent ??= LineEdit.Indent(line);
                        file.Lines[i] = PatchThickness(line, s, scale);
                        break;

                    case "GLA":
                        block.Glass = i;
                        block.Indent ??= LineEdit.Indent(line);
                        // A mirror's glass is the medium it sits in: the design's glass there.
                        file.Lines[i] = s.IsMirror
                            ? (Medium(system, surface) is { } medium ? PatchGlass(line, medium) : null)
                            : PatchGlass(line, s);
                        break;

                    case "PRI":
                        // A glass given as its index at each wavelength: this program read it as a
                        // model glass. It has a glass, so none is added, and the indices stand.
                        block.Glass = i;
                        block.Indent ??= LineEdit.Indent(line);
                        break;

                    case "SUT":
                    case "STO":
                    case "APE":
                    case "ASP":
                    case "COM":
                    case "VAR":
                        // Not ours to move, but they mark how far this surface's block reaches.
                        block.Last = i;
                        block.Indent ??= LineEdit.Indent(line);
                        break;
                }
            }

            if (role == Role.Plain)
                Flush(system, surface, block, file.Lines.Count, scale, insertions);

            file.InsertAll(insertions);
            file.Write(outputPath);
        }

        private enum Role { Plain, ModuleEntrance, ModuleExit }

        /// <summary>
        /// The surface each SUR number is here. The reader makes a lens module - two consecutive
        /// SUT L surfaces - one ideal lens, so every surface after one sits a number lower than the
        /// file's.
        /// </summary>
        private static Dictionary<int, (int Surface, Role Role)> SurfaceMap(IReadOnlyList<string?> lines)
        {
            var order = new List<(int Number, bool Module)>();
            int current = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string? line = lines[i];
                if (line == null || line.TrimStart().StartsWith("!", StringComparison.Ordinal)) continue;
                string keyword = LineEdit.Keyword(line);
                if (keyword == "SUR")
                {
                    order.Add((LineEdit.ArgumentAsInt(line, -1), false));
                    current = order.Count - 1;
                }
                else if (keyword == "SUT" && current >= 0
                         && LineEdit.Argument(line).ToUpperInvariant().Contains('L'))
                    order[current] = (order[current].Number, true);
            }

            var map = new Dictionary<int, (int, Role)>();
            int merged = 0;
            for (int k = 0; k < order.Count; k++)
            {
                if (order[k].Module && k + 1 < order.Count && order[k + 1].Module)
                {
                    map[order[k].Number] = (k - merged, Role.ModuleEntrance);
                    map[order[k + 1].Number] = (k - merged, Role.ModuleExit);
                    merged++;
                    k++;
                }
                else
                    map[order[k].Number] = (k - merged, Role.Plain);
            }
            return map;
        }

        /// <summary>
        /// The glass line. A catalog name is replaced when the design's differs - compared without
        /// the quotes a private glass carries, so 'GE' stays 'GE' - and removed when the surface
        /// became air. A model glass stands, as Optalix's fictitious-glass code, rewritten only if
        /// the design changed its nd or Vd.
        /// </summary>
        private static string? PatchGlass(string line, Surface s)
        {
            string current = LineEdit.Argument(line);
            if (s.ModelIndexEnabled)
            {
                if (!OptalixReader.TryFictitiousGlass(current, out double nd, out double vd)
                    || (Math.Abs(nd - s.ModelNd) < 1e-9 && Math.Abs(vd - s.ModelVd) < 1e-9))
                    return line;
                string? code = OptalixReader.FictitiousGlassCode(s.ModelNd, s.ModelVd, s.ModelDPgF);
                return code == null ? line : LineEdit.ReplaceArgument(line, code);
            }

            string material = s.IsMirror ? string.Empty : s.Material ?? string.Empty;
            if (string.IsNullOrWhiteSpace(material))
                return current.Equals("AIR", StringComparison.OrdinalIgnoreCase) ? line : null;
            return current.Trim('\'').Equals(material, StringComparison.Ordinal)
                ? line
                : LineEdit.ReplaceArgument(line, material);
        }

        /// <summary>
        /// The thickness, with the format's two sentinels left standing.
        ///
        /// <para>An object at infinity is a very large number, and the image surface is written as
        /// <c>-999</c> - a flag, not a distance, which the reader turns into a zero. Replacing that
        /// flag with the zero it was read as would throw away what the file was saying.</para>
        /// </summary>
        private static string PatchThickness(string line, Surface s, double scale)
        {
            string current = LineEdit.Argument(line);

            if (double.IsInfinity(s.Thickness))
                return IsInfinite(current) ? line : LineEdit.ReplaceArgument(line, Infinite);

            if (LineEdit.ArgumentAsDouble(line, out double have) && have <= -900.0
                && Math.Abs(s.Thickness) <= 0.0)
                return line;

            return LineEdit.ReplaceNumberIfChanged(line, s.Thickness / scale);
        }

        /// <summary>
        /// Adds the lines this surface needs and does not have - a glass where the substitution
        /// put one into an air space, a thickness or a curvature the file never wrote out.
        /// </summary>
        private static void Flush(OpticalSystem system, int surface, Block block, int terminator,
                                  double scale, List<(int, string)> insertions)
        {
            if (block.Done) return;
            block.Done = true;
            if (surface < 0 || surface >= system.Surfaces.Count) return;

            var s = system.Surfaces[surface];
            string indent = block.Indent ?? DefaultIndent;
            int at = Math.Max(block.Last,
                              Math.Max(block.Curvature, Math.Max(block.Thickness, block.Glass)));
            at = at < 0 ? terminator : at + 1;

            if (block.Curvature < 0 && Math.Abs(s.Curvature) > 0.0)
                insertions.Add((at, indent + "CUY " + LineEdit.Number(s.Curvature * scale)));

            if (block.Thickness < 0)
            {
                if (double.IsInfinity(s.Thickness))
                    insertions.Add((at, indent + "THI " + Infinite));
                else if (Math.Abs(s.Thickness) > 0.0)
                    insertions.Add((at, indent + "THI " + LineEdit.Number(s.Thickness / scale)));
            }

            var glass = s.IsMirror ? Medium(system, surface) : s;
            if (block.Glass < 0 && glass != null && !glass.ModelIndexEnabled && !string.IsNullOrWhiteSpace(glass.Material))
                insertions.Add((at, indent + "GLA " + glass.Material));
        }

        /// <summary>The surface whose glass is the medium a mirror sits in; null for air.</summary>
        private static Surface? Medium(OpticalSystem system, int mirror)
        {
            int j = OptalixWriter.MediumSurface(system, mirror);
            return j >= 0 ? system.Surfaces[j] : null;
        }

        private static bool IsInfinite(string argument) =>
            double.TryParse(argument, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double v)
            && Math.Abs(v) > 1e18;

        /// <summary>Where one surface's lines are, as the file is walked.</summary>
        private sealed class Block
        {
            public int Curvature = -1;
            public int Thickness = -1;
            public int Glass = -1;
            public int Last = -1;
            public string? Indent;
            public bool Done;
        }
    }
}
