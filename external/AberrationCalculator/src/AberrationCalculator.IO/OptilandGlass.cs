using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Glass;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Glass as Optiland needs it written.
    ///
    /// <para><b>A bare glass name is not safe in Optiland.</b> It finds a <c>Material</c> by a
    /// fuzzy search over the refractiveindex.info database and, by default, takes the nearest
    /// name from any catalog: many common glasses come back as a different glass, some names are
    /// not there at all, and a model glass has no name it could know. Naming the catalog is not
    /// enough either: Optiland files glasses under groups of equivalents ("BK7" holds Schott's
    /// N-BK7, Ohara's S-BSL7 and others), so BK7 in Schott answers to N-BK7.</para>
    ///
    /// <para><b>So each glass is written in two parts.</b> Beside the lens file, its own dispersion
    /// data as a refractiveindex.info <c>.yml</c>, in a folder named for its catalog prefixed
    /// <c>lenshh-</c> (<c>lenshh-schott</c>, <c>lenshh-model</c>) - Optiland's user-catalog layout,
    /// under names its own database does not use; and in the lens file a <c>Material</c> naming that
    /// catalog with <c>match_policy: "strict"</c>. Optiland then uses exactly this program's index,
    /// or stops naming the catalog it is missing; it never substitutes another glass. The
    /// <c>lenshh-</c> names are LensHH-LT's, so the lens files and glass folders of the two
    /// programs are interchangeable.</para>
    ///
    /// <para>Every AGF dispersion formula has an exact refractiveindex.info equivalent, and the
    /// model glass, a Conrady curve, is formula 5.</para>
    ///
    /// <para>Ported from LensHH-LT (MIT, Synapse Optics).</para>
    /// </summary>
    public static class OptilandGlass
    {
        /// <summary>
        /// Where Optiland reads user catalogs: <c>~/.optiland/catalogs</c> (the user profile folder
        /// on Windows, <c>$HOME</c> elsewhere). Optiland has no setting to move it.
        /// </summary>
        public static string UserCatalogsFolder =>
            UserCatalogsFolderOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".optiland", "catalogs");

        /// <summary>For tests: a folder to use instead of <see cref="UserCatalogsFolder"/>.</summary>
        public static string? UserCatalogsFolderOverride { get; set; }

        public const string CatalogPrefix = "lenshh-";
        public const string ModelCatalog = CatalogPrefix + "model";
        private const string ModelPrefix = "MODEL_";

        /// <summary>A model glass's name: its parameters, which come back on import.</summary>
        public static string ModelName(double nd, double vd, double dPgF) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1:F8}_{2:F6}_{3:F8}", ModelPrefix, nd, vd, dPgF);

        public static string DataCatalog(string catalog) => CatalogPrefix + catalog.ToLowerInvariant();

        /// <summary>Optiland's own name for a catalog, for a glass whose data is not here.</summary>
        public static string VendorCatalog(string catalog) =>
            catalog.StartsWith("CORNING", StringComparison.OrdinalIgnoreCase) ? "corning" : catalog.ToLowerInvariant();

        /// <summary>Whether a glass name can be a file name, as Optiland's user catalogs need.</summary>
        public static bool IsFileName(string name) =>
            name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0
            && name.Trim() == name && name != "." && name != "..";

        /// <summary>The refractiveindex.info formula and coefficients giving exactly this glass's index.</summary>
        public static (int Formula, double[] Coefficients)? Dispersion(GlassData g)
        {
            double C(int i) => i < g.Coefficients.Length ? g.Coefficients[i] : 0.0;
            switch (g.Formula)
            {
                case DispersionFormula.Schott:
                    return (3, new[] { C(0), C(1), 2, C(2), -2, C(3), -4, C(4), -6, C(5), -8 });
                case DispersionFormula.Sellmeier1:
                    return (2, new[] { 0.0, C(0), C(1), C(2), C(3), C(4), C(5) });
                case DispersionFormula.Herzberger:
                    return (7, new[] { C(0), C(1), C(2), C(3), C(4), C(5) });
                case DispersionFormula.Sellmeier2:
                    return (4, new[] { 1.0 + C(0), C(1), 2, C(2), 2, C(3), 0, C(4), 2 });
                case DispersionFormula.Conrady:
                    return (5, new[] { C(0), C(1), -1, C(2), -3.5 });
                case DispersionFormula.Sellmeier3:
                    return (2, new[] { 0.0, C(0), C(1), C(2), C(3), C(4), C(5), C(6), C(7) });
                case DispersionFormula.Handbook1:
                    return (4, new[] { C(0), C(1), 0, C(2), 1, 0, 0, 0, 1, -C(3), 2 });
                case DispersionFormula.Handbook2:
                    return (4, new[] { C(0), C(1), 2, C(2), 1, 0, 0, 0, 1, -C(3), 2 });
                case DispersionFormula.Sellmeier4:
                    return (2, new[] { C(0) - 1.0, C(1), C(2), C(3), C(4) });
                case DispersionFormula.Extended:
                    return (3, new[] { C(0), C(1), 2, C(2), -2, C(3), -4, C(4), -6, C(5), -8, C(6), -10, C(7), -12 });
                case DispersionFormula.Sellmeier5:
                    return (2, new[] { 0.0, C(0), C(1), C(2), C(3), C(4), C(5), C(6), C(7), C(8), C(9) });
                case DispersionFormula.Extended2:
                    return (3, new[] { C(0), C(1), 2, C(2), -2, C(3), -4, C(4), -6, C(5), -8, C(6), 4, C(7), 6 });
                case DispersionFormula.Extended3:
                    return (3, new[] { C(0), C(1), 2, C(2), 4, C(3), -2, C(4), -4, C(5), -6, C(6), -8, C(7), -10, C(8), -12 });
                default:
                    return null;
            }
        }

        /// <summary>
        /// The model glass as refractiveindex.info formula 5: it is the Conrady curve
        /// n0 + a/L + b/L^3.5, whose constants are solved here from three of its own indices.
        /// </summary>
        public static (int Formula, double[] Coefficients) ModelDispersion(double nd, double vd, double dPgF)
        {
            double[] l = { 0.4, 0.6, 1.0 };
            var a = new double[3, 3];
            var b = new double[3];
            for (int i = 0; i < 3; i++)
            {
                a[i, 0] = 1.0;
                a[i, 1] = 1.0 / l[i];
                a[i, 2] = Math.Pow(l[i], -3.5);
                b[i] = IndexResolver.ModelIndex(nd, vd, dPgF, l[i]);
            }
            double Det(double[,] m) =>
                m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
              - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
              + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
            double d = Det(a);
            var c = new double[3];
            for (int k = 0; k < 3; k++)
            {
                var m = (double[,])a.Clone();
                for (int i = 0; i < 3; i++) m[i, k] = b[i];
                c[k] = Det(m) / d;
            }
            return (5, new[] { c[0], c[1], -1, c[2], -3.5 });
        }

        /// <summary>A refractiveindex.info material file for one dispersion.</summary>
        public static string Yml(string description, int formula, double[] coefficients, double lambdaMin, double lambdaMax)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("REFERENCES: \"").Append(description.Replace("\"", "'")).Append("\"\n");
            sb.Append("DATA:\n");
            sb.Append("  - type: formula ").Append(formula.ToString(inv)).Append('\n');
            sb.Append("    wavelength_range: ").Append(lambdaMin.ToString("R", inv)).Append(' ')
              .Append(lambdaMax.ToString("R", inv)).Append('\n');
            sb.Append("    coefficients:");
            foreach (var c in coefficients) sb.Append(' ').Append(c.ToString("R", inv));
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>The note written into the folder of glasses beside a lens file.</summary>
        public static string ReadMe(string lensFile, IEnumerable<string> catalogs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"The glasses of {lensFile}.");
            sb.AppendLine();
            sb.AppendLine("Each folder is an Optiland user catalog: one refractiveindex.info .yml per glass,");
            sb.AppendLine("holding that glass's dispersion data exactly. The lens file names each glass with");
            sb.AppendLine("one of these catalogs and match_policy \"strict\", so Optiland uses exactly these");
            sb.AppendLine("glasses, and never substitutes another.");
            sb.AppendLine();
            sb.AppendLine("They were installed for Optiland on the machine the lens was written on, in");
            sb.AppendLine("~/.optiland/catalogs/ (on Windows, %USERPROFILE%\\.optiland\\catalogs\\); Optiland");
            sb.AppendLine("loads them when it starts. On another machine, copy the folders here into that");
            sb.AppendLine("folder. Folders of the same name from other lenses merge: a glass is the same file.");
            sb.AppendLine();
            sb.AppendLine("Or load them in the session, before loading the lens:");
            sb.AppendLine("    from optiland.materials.registry import MaterialRegistry");
            var quoted = new List<string>();
            foreach (var c in catalogs) quoted.Add("\"" + c + "\"");
            sb.AppendLine("    for d in [" + string.Join(", ", quoted) + "]:");
            sb.AppendLine("        MaterialRegistry.instance().load_catalog(\"<this folder>/\" + d)");
            sb.AppendLine("Load a catalog once per session: loaded twice, each of its glasses has two entries,");
            sb.AppendLine("and a strict lookup refuses both.");
            sb.AppendLine();
            sb.AppendLine("Without them, Optiland stops with an error naming the catalog it is missing.");
            return sb.ToString();
        }
    }
}
