using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the .len it came from.
    ///
    /// <para>The file is a stream of commands rather than a table: <c>NXT</c> moves to the next
    /// surface and the keywords beneath it set that surface's properties. A property that has
    /// nothing to say is simply not written, so a plane has no <c>RD</c> line and an air space no
    /// <c>GLA</c> line - which means patching this format is not only editing values but
    /// occasionally ADDING a line that was never there, when a surface the optimiser bent used to
    /// be flat or a glass substitution put glass where there was none.</para>
    ///
    /// <para>Insertions go at the end of the surface's own block, where the reader and the program
    /// that wrote the file both take them as belonging to that surface, and are applied from the
    /// bottom of the file upwards so no earlier position shifts under a later one.</para>
    /// </summary>
    internal static class LenPatcher
    {
        /// <summary>How the format spells an infinite object distance.</summary>
        private const string Infinite = "1.0000000E+020";

        private const string DefaultIndent = "  ";

        public static void Patch(OpticalSystem system, string originalPath, string outputPath)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            var insertions = new List<(int Index, string Text)>();

            // Surface 0 is open from the first line: the object's properties precede any NXT,
            // exactly as the reader takes them.
            int surface = 0;
            var block = new Block();

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;

                string keyword = LineEdit.Keyword(line);

                // NXT closes the surface before it; so does the first of the system-wide
                // keywords that follow the last surface. Either way the block has to be finished
                // before its number changes, or a line inserted for it would land in the next.
                if (keyword == "NXT" || keyword == "WV" || keyword == "WW" || keyword == "END")
                {
                    Flush(system, surface, block, i, scale, insertions);
                    if (keyword != "NXT") break;
                    surface++;
                    block = new Block();
                    continue;
                }

                if (surface >= system.Surfaces.Count) continue;
                var s = system.Surfaces[surface];

                switch (keyword)
                {
                    case "RD":
                        block.Radius = i;
                        block.Indent ??= LineEdit.Indent(line);
                        // A PLANE IS A ZERO HERE. The format has no infinity for a radius and the
                        // reader reads a zero back as one, so a surface the optimiser flattened
                        // goes out as RD 0 rather than as this program's infinity.
                        file.Lines[i] = LineEdit.ReplaceNumberIfChanged(
                            line, double.IsInfinity(s.Radius) ? 0.0 : s.Radius / scale);
                        break;

                    case "TH":
                        block.Thickness = i;
                        block.Indent ??= LineEdit.Indent(line);
                        file.Lines[i] = double.IsInfinity(s.Thickness)
                            ? (IsInfinite(LineEdit.Argument(line)) ? line
                                                     : LineEdit.ReplaceArgument(line, Infinite))
                            : LineEdit.ReplaceNumberIfChanged(line, s.Thickness / scale);
                        break;

                    case "GLA":
                        block.Glass = i;
                        block.Indent ??= LineEdit.Indent(line);
                        string material = s.IsMirror ? string.Empty : s.Material ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(material))
                        {
                            // The glass is gone. A GLA line naming nothing is not something a
                            // well-formed file contains, so the line goes rather than emptying.
                            file.Lines[i] = null;
                        }
                        else if (!SameGlass(LineEdit.Argument(line), material))
                        {
                            file.Lines[i] = LineEdit.ReplaceArgument(line, material);
                        }
                        break;

                    case "AST":
                    case "AP":
                    case "CC":
                    case "RFH":
                    case "PK":
                    case "CALLBACK":
                        // Not ours to move, but they mark where this surface's block reaches, so
                        // a line inserted for it goes after them rather than in the middle.
                        block.Last = i;
                        block.Indent ??= LineEdit.Indent(line);
                        break;
                }
            }

            // A file that ends without END or WV leaves its last surface open.
            Flush(system, surface, block, file.Lines.Count, scale, insertions);

            file.InsertAll(insertions);
            file.Write(outputPath);
        }

        /// <summary>
        /// Adds the lines this surface needs and does not have. <paramref name="terminator"/> is
        /// the index of the line that closed the block, so an insertion at the end of the block
        /// goes immediately before it.
        /// </summary>
        private static void Flush(OpticalSystem system, int surface, Block block, int terminator,
                                  double scale, List<(int, string)> insertions)
        {
            if (block.Done) return;
            block.Done = true;
            if (surface >= system.Surfaces.Count) return;

            var s = system.Surfaces[surface];
            string indent = block.Indent ?? DefaultIndent;
            int at = Math.Max(block.Last, Math.Max(block.Radius, Math.Max(block.Thickness, block.Glass)));
            at = at < 0 ? terminator : at + 1;

            // A surface that was flat and is not any more has no RD line to edit.
            if (block.Radius < 0 && !double.IsInfinity(s.Radius) && !double.IsNaN(s.Radius))
                insertions.Add((at, indent + "RD " + LineEdit.Number(s.Radius / scale)));

            if (block.Thickness < 0)
            {
                if (double.IsInfinity(s.Thickness))
                    insertions.Add((at, indent + "TH " + Infinite));
                else if (Math.Abs(s.Thickness) > 0.0)
                    insertions.Add((at, indent + "TH " + LineEdit.Number(s.Thickness / scale)));
            }

            // A glass substitution can put glass into what used to be an air space.
            if (block.Glass < 0 && !s.IsMirror && !string.IsNullOrWhiteSpace(s.Material))
                insertions.Add((at, indent + "GLA " + s.Material));
        }

        /// <summary>
        /// Whether a glass name in the file means the glass the design is now carrying.
        ///
        /// <para>The format qualifies some names with an <c>H_</c> prefix and the reader strips it,
        /// so the name that came out of the file and the name on the surface can differ by that
        /// prefix while naming the same glass. Rewriting the line then would change a line that
        /// did not need changing.</para>
        /// </summary>
        private static bool SameGlass(string inFile, string material)
        {
            if (inFile.Equals(material, StringComparison.OrdinalIgnoreCase)) return true;
            return inFile.StartsWith("H_", StringComparison.OrdinalIgnoreCase)
                && inFile.Substring(2).Equals(material, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInfinite(string argument) =>
            double.TryParse(argument, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double v)
            && Math.Abs(v) > 1e18;

        /// <summary>Where one surface's lines are, as the file is walked.</summary>
        private sealed class Block
        {
            public int Radius = -1;
            public int Thickness = -1;
            public int Glass = -1;
            public int Last = -1;
            public string? Indent;
            public bool Done;
        }
    }
}
