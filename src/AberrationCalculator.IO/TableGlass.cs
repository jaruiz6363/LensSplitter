using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// OpticStudio's table glass: a <c>.ZTG</c> file that defines a material only by its index
    /// at a list of wavelengths — <c>!</c> comment lines, an optional <c>DENSITY</c> line, then
    /// one "wavelength index" pair per line, wavelength in µm (further columns, such as
    /// transmission data, are ignored). OpticStudio writes these in UTF-16 or plain text; a
    /// <c>.zmx</c> names one by its file name alone (<c>GLAS NAME.ZTG</c>), so the file has to
    /// travel with the lens.
    ///
    /// <para>This program has no table-glass type. A table of six or more points is fitted with
    /// the Schott dispersion formula (n² = a0 + a1·λ² + a2/λ² + a3/λ⁴ + a4/λ⁶ + a5/λ⁸), which
    /// follows real glass to about 1e-6 over the visible; a shorter table is fitted with a
    /// Conrady curve (n = c0 + c1/λ + c2/λ^3.5), which is exactly a model glass and passes
    /// through up to three points exactly.</para>
    /// </summary>
    public static class TableGlass
    {
        /// <summary>The fewest points the six-constant Schott formula is fitted to.</summary>
        public const int MinSchottPoints = 6;

        /// <summary>The table's (wavelength µm, index) pairs, sorted by wavelength.</summary>
        public static List<(double Um, double N)> Read(string path)
        {
            var points = new List<(double, double)>();
            foreach (var raw in File.ReadAllLines(path))   // detects the UTF-16 BOM
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("!")) continue;
                var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double um) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                    continue;   // DENSITY and anything else that is not a pair
                if (um > 0 && n > 0) points.Add((um, n));
            }
            return points.OrderBy(p => p.Item1).ToList();
        }

        /// <summary>
        /// The <c>.ZTG</c> file a lens names: beside the lens file first, then in OpticStudio's
        /// glass folder (Documents\Zemax\Glasscat). Null when it is in neither.
        /// </summary>
        public static string? Find(string ztgName, string lensPath) => FindCatalog(ztgName, lensPath);

        /// <summary>
        /// A glass file (a <c>.ZTG</c> table or an <c>.AGF</c> catalog) a lens refers to: beside
        /// the lens file first, then in OpticStudio's glass folder. Null when it is in neither.
        /// </summary>
        public static string? FindCatalog(string fileName, string lensPath)
        {
            string file = Path.GetFileName(fileName);
            var dirs = new List<string>();
            string? lensDir = Path.GetDirectoryName(Path.GetFullPath(lensPath));
            if (lensDir != null) dirs.Add(lensDir);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docs))
            {
                dirs.Add(Path.Combine(docs, "Zemax", "Glasscat"));
                dirs.Add(Path.Combine(docs, "Zemax", "Glasscat", "Tables"));
            }
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                var hit = Directory.GetFiles(dir).FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(file, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>
        /// The Schott formula through the table, least squares in n². Returns its six
        /// coefficients (in the order of an AGF CD line for formula 1) and the largest index
        /// error at the table's points.
        /// </summary>
        public static (double[] Coefficients, double MaxError) FitSchott(IReadOnlyList<(double Um, double N)> points)
        {
            if (points.Count < MinSchottPoints)
                throw new ArgumentException($"The Schott formula needs {MinSchottPoints} points; the table has {points.Count}.");
            var rows = points.Select(p =>
            {
                double l2 = p.Um * p.Um;
                return new[] { 1.0, l2, 1 / l2, 1 / (l2 * l2), 1 / (l2 * l2 * l2), 1 / (l2 * l2 * l2 * l2) };
            }).ToList();
            var c = LeastSquares(rows, points.Select(p => p.N * p.N).ToArray());
            double max = points.Max(p => Math.Abs(Schott(c, p.Um) - p.N));
            return (c, max);
        }

        /// <summary>
        /// A Conrady curve n = c0 + c1/λ + c2/λ^3.5 through the table — exact for up to three
        /// points; one point gives a constant index, two a curve in 1/λ alone. Returns the three
        /// constants and the largest index error at the table's points.
        /// </summary>
        public static (double C0, double C1, double C2, double MaxError) FitConrady(IReadOnlyList<(double Um, double N)> points)
        {
            if (points.Count == 0) throw new ArgumentException("The table has no points.");
            double c0, c1 = 0, c2 = 0;
            if (points.Count == 1)
                c0 = points[0].N;
            else if (points.Count == 2)
            {
                var c = LeastSquares(points.Select(p => new[] { 1.0, 1 / p.Um }).ToList(), points.Select(p => p.N).ToArray());
                c0 = c[0]; c1 = c[1];
            }
            else
            {
                var c = LeastSquares(points.Select(p => new[] { 1.0, 1 / p.Um, Math.Pow(p.Um, -3.5) }).ToList(), points.Select(p => p.N).ToArray());
                c0 = c[0]; c1 = c[1]; c2 = c[2];
            }
            double max = points.Max(p => Math.Abs(c0 + c1 / p.Um + c2 * Math.Pow(p.Um, -3.5) - p.N));
            return (c0, c1, c2, max);
        }

        /// <summary>The Schott formula's index at <paramref name="um"/>.</summary>
        public static double Schott(double[] c, double um)
        {
            double l2 = um * um;
            double n2 = c[0] + c[1] * l2 + c[2] / l2 + c[3] / (l2 * l2) + c[4] / (l2 * l2 * l2) + c[5] / (l2 * l2 * l2 * l2);
            return Math.Sqrt(n2);
        }

        // Householder least squares; the columns are scaled to unit length first, since the
        // Schott basis spans many orders of magnitude.
        private static double[] LeastSquares(List<double[]> rows, double[] y)
        {
            int m = rows.Count, n = rows[0].Length;
            var scale = new double[n];
            for (int j = 0; j < n; j++) { double s = 0; for (int i = 0; i < m; i++) s += rows[i][j] * rows[i][j]; scale[j] = s > 0 ? Math.Sqrt(s) : 1; }
            var a = new double[m, n]; var b = (double[])y.Clone();
            for (int i = 0; i < m; i++) for (int j = 0; j < n; j++) a[i, j] = rows[i][j] / scale[j];
            for (int k = 0; k < n; k++)
            {
                double norm = 0; for (int i = k; i < m; i++) norm += a[i, k] * a[i, k]; norm = Math.Sqrt(norm);
                double alpha = a[k, k] > 0 ? -norm : norm;
                var v = new double[m]; for (int i = k; i < m; i++) v[i] = a[i, k]; v[k] -= alpha;
                double vv = 0; for (int i = k; i < m; i++) vv += v[i] * v[i];
                if (vv == 0) continue;
                for (int j = k; j < n; j++) { double s = 0; for (int i = k; i < m; i++) s += v[i] * a[i, j]; s = 2 * s / vv; for (int i = k; i < m; i++) a[i, j] -= s * v[i]; }
                { double s = 0; for (int i = k; i < m; i++) s += v[i] * b[i]; s = 2 * s / vv; for (int i = k; i < m; i++) b[i] -= s * v[i]; }
            }
            var x = new double[n];
            for (int k = n - 1; k >= 0; k--) { double s = b[k]; for (int j = k + 1; j < n; j++) s -= a[k, j] * x[j]; x[k] = s / a[k, k]; }
            for (int j = 0; j < n; j++) x[j] /= scale[j];
            return x;
        }
    }
}
