using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Glass;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// The user's own glass catalogs: <c>Documents\AberrationCalculator\Glass</c>.
    /// The shipped catalogs sit beside the program, which a user may not be able to write to.
    /// Glasses brought in with a lens go here instead:
    /// <list type="bullet">
    /// <item>a table glass read from an OpticStudio <c>.ZTG</c> file, written into
    /// <c>TABLE.AGF</c>;</item>
    /// <item>a catalog a .zmx names on its GCAT line that isn't loaded, copied in whole.</item>
    /// </list>
    /// <see cref="CatalogLocator.LoadBundled"/> loads the folder after the shipped catalogs, so a
    /// lens that needed one opens with it again. The files are standard AGF, which OpticStudio
    /// reads as well.
    /// </summary>
    public static class UserGlassCatalog
    {
        /// <summary>The catalog table glasses are written to.</summary>
        public const string TableCatalog = "TABLE";

        /// <summary>For tests: a folder to use instead of the Documents one.</summary>
        public static string? FolderOverride { get; set; }

        /// <summary>Documents\AberrationCalculator\Glass (or the override).</summary>
        public static string Folder => FolderOverride ?? CatalogLocator.UserFolder;

        public static string TableCatalogPath => Path.Combine(Folder, TableCatalog + ".AGF");

        /// <summary>
        /// Write a glass given by the Schott formula into the TABLE catalog, replacing one of the
        /// same name, and load the catalog into <paramref name="glass"/> when there is one.
        /// </summary>
        public static void AddSchottGlass(string name, double[] coefficients, double wlMinUm, double wlMaxUm,
                                          string source, GlassCatalog? glass)
        {
            var inv = CultureInfo.InvariantCulture;
            double nd = TableGlass.Schott(coefficients, IndexResolver.LambdaD);
            double nF = TableGlass.Schott(coefficients, IndexResolver.LambdaF);
            double nC = TableGlass.Schott(coefficients, IndexResolver.LambdaC);
            double vd = Math.Abs(nF - nC) > 1e-12 ? (nd - 1) / (nF - nC) : 0;

            var block = new List<string>
            {
                string.Format(inv, "NM {0} 1 0 {1:F6} {2:F4} 0 0 0", name, nd, vd),   // formula 1 = Schott
                "GC " + source,
                "ED 0 0 0 0 0",
                "CD " + string.Join(" ", coefficients.Select(c => c.ToString("E14", inv))),
                "TD 0 0 0 0 0 0 20",
                "OD -1 -1 -1 -1 -1 -1",
                string.Format(inv, "LD {0:F6} {1:F6}", wlMinUm, wlMaxUm),
            };

            Directory.CreateDirectory(Folder);
            var lines = File.Exists(TableCatalogPath) ? File.ReadAllLines(TableCatalogPath).ToList()
                                                      : new List<string> { "CC Table glasses (converted from OpticStudio .ZTG files)" };
            RemoveGlass(lines, name);
            lines.AddRange(block);
            File.WriteAllLines(TableCatalogPath, lines, new UTF8Encoding(false));

            glass?.LoadFile(TableCatalogPath);
        }

        /// <summary>
        /// Copy a catalog file into the user's glass folder, replacing an earlier copy of the same
        /// name, and load it into <paramref name="glass"/>. Returns the copy's path.
        /// </summary>
        public static string AddCatalogFile(string sourcePath, GlassCatalog? glass)
        {
            Directory.CreateDirectory(Folder);
            string dest = Path.Combine(Folder, Path.GetFileName(sourcePath));
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourcePath, dest, overwrite: true);
            glass?.LoadFile(dest);
            return dest;
        }

        // Drop an existing NM block of this name: its NM line and every line up to the next NM.
        private static void RemoveGlass(List<string> lines, string name)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var p = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2 || p[0] != "NM" || !p[1].Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                int end = i + 1;
                while (end < lines.Count && !lines[end].TrimStart().StartsWith("NM ")) end++;
                lines.RemoveRange(i, end - i);
                return;
            }
        }
    }
}
