using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes a whole lens as a Code V .seq file, as Code V writes one (and as Zemax's
    /// CODEV-to-OpticStudio converter reads it):
    /// <list type="bullet">
    /// <item>the aperture as <c>EPD</c>, <c>FNO</c> or <c>NAO</c> - at a finite object an F-number
    /// goes out as the EPD it gives, Code V's FNO being defined at infinity;</item>
    /// <item>fields as angles (<c>XAN</c>/<c>YAN</c>) or object heights (<c>XOB</c>/<c>YOB</c>),
    /// with <c>REF</c> naming the primary wavelength;</item>
    /// <item>an asphere as <c>ASP</c> followed by its <c>K</c> and <c>A</c>..<c>G</c> lines, a
    /// conic alone as <c>CON</c> and <c>K</c>;</item>
    /// <item>a glass as Code V names it, <c>NAME_CATALOG</c> when its catalog is one Code V ships;
    /// a glass from any other catalog, and a model glass Code V's fictitious-glass code cannot
    /// carry, as a private glass (<c>PRV</c>) given by its index at each wavelength.</item>
    /// </list>
    /// A surface with an r^2 aspheric term is refused, and so is an ideal lens: Code V's asphere
    /// starts at r^4, and a .seq has no ideal lens to name.
    ///
    /// <para>Ported from LensHH-LT's writer (MIT, Synapse Optics).</para>
    /// </summary>
    public static class CodeVWriter
    {
        /// <summary>Code V's names for the r^4, r^6 ... r^16 terms.</summary>
        private static readonly string[] CoefficientNames = { "A", "B", "C", "D", "E", "F", "G" };

        public static void Write(OpticalSystem system, string filePath, GlassCatalog? glass)
        {
            var w = new WriterSupport(system, glass, "Code V");
            w.RefuseUnwritable(paraxialAllowed: false);
            w.RefuseR2("Code V's asphere has no place for");
            var inv = WriterSupport.Inv;
            var qualifier = new CodeVGlassQualifier(glass, system.GlassCatalogs);

            var sb = new StringBuilder();
            sb.AppendLine("! Lens exported from AberrationCalculator");
            sb.AppendLine("RDM;LEN");
            if (!string.IsNullOrEmpty(system.Title))
                sb.AppendLine($"TIT '{system.Title.Replace("'", "")}'");   // an apostrophe would end it
            sb.AppendLine("DIM M");

            switch (system.Aperture.Type)
            {
                case ApertureType.EPD:
                    sb.AppendLine(string.Format(inv, "EPD {0:R}", system.Aperture.Value));
                    break;
                case ApertureType.FNumber:
                    sb.AppendLine(w.InfiniteObject
                        ? string.Format(inv, "FNO {0:R}", system.Aperture.Value)
                        : string.Format(inv, "EPD {0:R}", w.EntrancePupilDiameter()));
                    break;
                case ApertureType.ObjectSpaceNA:
                    if (w.InfiniteObject)
                        throw new InvalidOperationException("An object-space NA needs a finite object; Code V cannot take one for an object at infinity.");
                    sb.AppendLine(string.Format(inv, "NAO {0:R}", system.Aperture.Value));
                    break;
            }

            if (system.Wavelengths.Count > 0)
            {
                sb.AppendLine("WL  " + string.Join(" ", system.Wavelengths.Select(x => (x.Value * 1000.0).ToString("F4", inv))));
                sb.AppendLine("WTW " + string.Join(" ", system.Wavelengths.Select(x => x.Weight.ToString("G", inv))));
                sb.AppendLine($"REF {Math.Max(0, system.PrimaryWavelengthIndex) + 1}");
            }

            if (system.Fields.Count > 0)
            {
                bool heights = system.FieldType == FieldType.ObjectHeight;
                if (heights && w.InfiniteObject)
                    throw new InvalidOperationException("Fields given as object heights need a finite object; Code V takes a field angle for an object at infinity.");
                sb.AppendLine((heights ? "XOB " : "XAN ") + string.Join(" ", system.Fields.Select(f => f.X.ToString("R", inv))));
                sb.AppendLine((heights ? "YOB " : "YAN ") + string.Join(" ", system.Fields.Select(f => f.Y.ToString("R", inv))));
                sb.AppendLine("WTF " + string.Join(" ", system.Fields.Select(f => (f.Weight * 100).ToString("F0", inv))));
            }

            // Private glasses: a model glass the fictitious-glass code cannot carry, and a glass
            // from a catalog Code V does not ship. By name the latter would not resolve in Code V,
            // and stripping its punctuation can make it another glass's name: LightPath has both
            // D-ZLAF52LA_M and D-ZLAF52LAM.
            var privateName = new Dictionary<int, string>();
            var privateRows = new List<(string Name, int Surface)>();
            var byMaterial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string>? preferred = system.GlassCatalogs.Count > 0 ? system.GlassCatalogs : null;
            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                if (s.IsMirror) continue;
                if (s.ModelIndexEnabled)
                {
                    if (FictitiousGlassCode(s.ModelNd, s.ModelVd, s.ModelDPgF) != null) continue;
                    string name = $"MODEL{i}";
                    privateName[i] = name;
                    privateRows.Add((name, i));
                    continue;
                }
                if (string.IsNullOrEmpty(s.Material) || s.Material.Equals("AIR", StringComparison.OrdinalIgnoreCase)) continue;
                var g = glass?.Find(s.Material, preferred);
                if (g == null || CodeVGlassNames.IsCodeVCatalog(g.Catalog)) continue;
                if (!byMaterial.TryGetValue(s.Material, out var prv))
                {
                    string stem = CodeVGlassNames.ToCodeV(s.Material);
                    if (stem.Length > 16) stem = stem.Substring(0, 16);
                    prv = stem;
                    for (int k = 2; privateRows.Any(r => r.Name.Equals(prv, StringComparison.OrdinalIgnoreCase)); k++)
                        prv = stem + k.ToString(inv);
                    byMaterial[s.Material] = prv;
                    privateRows.Add((prv, i));
                }
                privateName[i] = prv;
            }
            if (privateRows.Count > 0)
            {
                if (system.Wavelengths.Count == 0)
                    throw new InvalidOperationException("A private glass needs the lens's wavelengths to be written for Code V.");
                sb.AppendLine("PRV");
                sb.AppendLine("PWL " + string.Join(" ", system.Wavelengths.Select(x => (x.Value * 1000.0).ToString("F4", inv))));
                foreach (var (name, surface) in privateRows)
                    sb.AppendLine($"'{name}' " + string.Join(" ", system.Wavelengths.Select(x =>
                        w.IndicesAt(x.Value)[surface].ToString("R", inv))));
                sb.AppendLine("END");
            }

            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                bool isImage = i == system.Surfaces.Count - 1;
                double radius = double.IsInfinity(s.Radius) ? 0.0 : s.Radius;
                double thickness = isImage ? 0.0 : double.IsPositiveInfinity(s.Thickness) ? 1e20 : s.Thickness;

                string material = s.IsMirror ? "REFL"
                    : privateName.TryGetValue(i, out var prv) ? prv
                    : s.ModelIndexEnabled ? FictitiousGlassCode(s.ModelNd, s.ModelVd, s.ModelDPgF)!
                    : string.IsNullOrEmpty(s.Material) ? "AIR"
                    : qualifier.ToCodeVMaterial(s.Material!);

                if (isImage)
                    sb.AppendLine(string.Format(inv, "SI {0:G15} 0", radius));
                else
                    sb.AppendLine(string.Format(inv, "{0} {1:G15} {2:G15} {3}", i == 0 ? "SO" : "S", radius, thickness, material));

                if (s.IsStop)
                    sb.AppendLine("  STO");
                if (s.SemiDiameter > 0 && s.SemiDiameterMode == SemiDiameterMode.Fixed)
                    sb.AppendLine(string.Format(inv, "  CIR {0:G15}", s.SemiDiameter));
                double obscuration = s.InnerRadius > 0 ? s.InnerRadius : s.ObscurationRadius;
                if (obscuration > 0)
                    sb.AppendLine(string.Format(inv, "  CIR OBS {0:G15}", obscuration));

                // ASP, then its conic and terms. (ASP followed by CON makes the surface a plain
                // conic in Code V, dropping the terms.)
                var a = s.AsphericCoefficients;
                if (a.Length > 8 && a.Skip(8).Any(c => c != 0))
                    throw new InvalidOperationException($"Surface {i} has an aspheric term beyond r^16, which Code V's asphere has no place for.");
                if (a.Skip(1).Any(c => c != 0))
                {
                    sb.AppendLine("  ASP");
                    sb.AppendLine(string.Format(inv, "  K {0:R}", s.Conic));
                    var terms = new List<string>();
                    for (int c = 1; c < a.Length && c <= CoefficientNames.Length; c++)
                        terms.Add(string.Format(inv, "{0} {1:E15}", CoefficientNames[c - 1], a[c]));
                    for (int t = 0; t < terms.Count; t += 4)
                        sb.AppendLine("  " + string.Join(" ; ", terms.Skip(t).Take(4)));
                }
                else if (s.Conic != 0)
                {
                    sb.AppendLine("  CON");
                    sb.AppendLine(string.Format(inv, "  K {0:R}", s.Conic));
                }
            }

            sb.AppendLine("GO");
            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Code V's fictitious-glass code for (nd, Vd): six digits of nd after "1.", a point, and
        /// six digits of Vd/100 - 1.5168 / 64.17 is <c>516800.641700</c>. Null when the code cannot
        /// carry the glass: a partial-dispersion offset, or nd or Vd out of its range.
        /// </summary>
        public static string? FictitiousGlassCode(double nd, double vd, double dPgF)
        {
            if (dPgF != 0.0 || nd <= 1.0 || nd >= 2.0 || vd <= 0.0 || vd >= 100.0) return null;
            long n = (long)Math.Round((nd - 1.0) * 1e6);
            long v = (long)Math.Round(vd * 1e4);
            if (n >= 1_000_000 || v >= 1_000_000) return null;
            // The code keeps six decimals of each; a glass given more precisely would change.
            if (Math.Abs(n / 1e6 + 1.0 - nd) > 1e-12 || Math.Abs(v / 1e4 - vd) > 1e-10) return null;
            return n.ToString("000000", WriterSupport.Inv) + "." + v.ToString("000000", WriterSupport.Inv);
        }
    }
}
