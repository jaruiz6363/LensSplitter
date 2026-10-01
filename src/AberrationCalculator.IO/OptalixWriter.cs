using System;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes a whole lens as an Optalix .otx file, as Optalix writes one (surveyed across the
    /// lens files that ship with it, and its reference manual §32.2):
    /// <list type="bullet">
    /// <item>one <c>RAIM</c> line - 2, aim at the real stop, Optalix's own default; 1 for the
    /// paraxial pupil; 3 for a telecentric object space;</item>
    /// <item>the aperture as <c>EPD</c>, <c>FNO</c> or <c>NAO</c>; <c>FTYP</c> 1 for field angles,
    /// 2 for object heights; <c>PIM 0</c>, so the image stays where the lens puts it;</item>
    /// <item>a model glass as Optalix's fictitious-glass code, or its index at each wavelength
    /// (<c>PRI</c>); an ideal lens as Optalix's lens module, two <c>SUT L</c> surfaces;</item>
    /// <item><c>FH 1</c> on a surface whose aperture clips - Optalix's apertures otherwise never
    /// block a ray - and <c>ASP</c> as the conic, eight even terms from r^4, and a zero.</item>
    /// </list>
    /// A surface with an r^2 aspheric term is refused: Optalix's even asphere starts at r^4.
    ///
    /// <para>Ported from LensHH-LT's writer (MIT, Synapse Optics).</para>
    /// </summary>
    public static class OptalixWriter
    {
        public static void Write(OpticalSystem system, string filePath, GlassCatalog? glass)
        {
            var w = new WriterSupport(system, glass, "Optalix");
            w.RefuseUnwritable(paraxialAllowed: true);
            w.RefuseR2("Optalix's even asphere has no place for");
            var inv = WriterSupport.Inv;

            var sb = new StringBuilder();
            sb.AppendLine("VERS 11.82");
            sb.AppendLine($"FILE {filePath}");
            if (!string.IsNullOrEmpty(system.Title))
                sb.AppendLine($"REM 1 {system.Title}");
            int raim = system.TelecentricObjectSpace ? 3 : system.RayAiming == RayAimingMode.Off ? 1 : 2;
            sb.AppendLine($"RAIM {raim}");
            if (system.IsAfocal)
                sb.AppendLine("AFO 1000.0");

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
                        throw new InvalidOperationException("An object-space NA needs a finite object; Optalix cannot take one for an object at infinity.");
                    sb.AppendLine(string.Format(inv, "NAO {0:R}", system.Aperture.Value));
                    break;
            }

            if (system.Wavelengths.Count > 0)
            {
                sb.AppendLine("WL " + string.Join(" ", system.Wavelengths.Select(x => x.Value.ToString("F7", inv))));
                sb.AppendLine("WTW " + string.Join(" ", system.Wavelengths.Select(x => Weight(x.Weight))));
                sb.AppendLine($"REF {Math.Max(0, system.PrimaryWavelengthIndex) + 1}");
            }

            sb.AppendLine($"FTYP {(system.FieldType == FieldType.ObjectHeight ? 2 : 1)}");
            sb.AppendLine($"NFLD {system.Fields.Count}");
            for (int i = 0; i < system.Fields.Count; i++)
            {
                var f = system.Fields[i];
                sb.AppendLine(string.Format(inv, "FLD {0} {1:R} {2:R} {3} 1 0", i + 1, f.X, f.Y, Weight(f.Weight)));
            }
            sb.AppendLine("PIM 0");

            sb.AppendLine("! Surface data :");
            int k = 0;   // Optalix's surface number: an ideal lens takes two
            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                bool isImage = i == system.Surfaces.Count - 1;
                double thi = isImage ? 0.0 : double.IsPositiveInfinity(s.Thickness) ? 1e20 : s.Thickness;

                if (s.Type == SurfaceType.Paraxial)
                {
                    // Optalix's lens module: two L surfaces, the principal planes, here coincident;
                    // the power on the first. It is perfect at an object at infinity.
                    double power = double.IsInfinity(s.FocalLength) || s.FocalLength == 0 ? 0.0 : 1.0 / s.FocalLength;
                    sb.AppendLine($"SUR {k++}");
                    sb.AppendLine("  SUT L");
                    sb.AppendLine("  CUY 0");
                    sb.AppendLine("  THI 0");
                    if (s.IsStop) sb.AppendLine("  STO");
                    Apertures(sb, s);
                    sb.AppendLine(string.Format(inv, "  LMOD {0:R} 0 0 0 0", power));
                    sb.AppendLine($"SUR {k++}");
                    sb.AppendLine("  SUT L");
                    sb.AppendLine("  CUY 0");
                    sb.AppendLine(string.Format(inv, "  THI {0:R}", thi));
                    Material(sb, s, i, w, system);
                    sb.AppendLine("  LMOD 0 0 0 0 0");
                    continue;
                }

                sb.AppendLine($"SUR {k++}");
                bool figured = WriterSupport.HasFiguring(s);
                sb.AppendLine($"  SUT {(figured ? "A" : "S")}{(s.IsMirror ? "M" : "")}");
                sb.AppendLine(string.Format(inv, "  CUY {0:E16}", s.Curvature));
                sb.AppendLine(string.Format(inv, "  THI {0:E16}", thi));
                // A mirror names the medium the light goes on in after it, which is the one it came
                // in: glass, for a mirror inside an element (a Mangin mirror, a ghost reflected
                // inside a lens). Left out, Optalix puts the reflected light in air.
                if (!s.IsMirror) Material(sb, s, i, w, system);
                else if (MediumSurface(system, i) is int m and >= 0) Material(sb, system.Surfaces[m], m, w, system);
                if (s.IsStop) sb.AppendLine("  STO");
                Apertures(sb, s);
                if (!string.IsNullOrWhiteSpace(s.Comment))
                    sb.AppendLine($"  COM {s.Comment.Trim()}");

                if (figured)
                {
                    var a = s.AsphericCoefficients;
                    if (a.Length > 9 && a.Skip(9).Any(c => c != 0))
                        throw new InvalidOperationException($"Surface {i} has an aspheric term beyond r^18, which Optalix's even asphere has no place for.");
                    var asp = new StringBuilder("  ASP");
                    asp.Append(string.Format(inv, " {0:R}", s.Conic));
                    for (int c = 1; c <= 8; c++)
                        asp.Append(string.Format(inv, " {0:E15}", c < a.Length ? a[c] : 0.0));
                    asp.Append(" 0");
                    sb.AppendLine(asp.ToString());
                }
            }

            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// The surface whose material is the medium a mirror at <paramref name="i"/> sits in: the
        /// last surface before it that is not a mirror itself. -1 when there is none.
        /// </summary>
        internal static int MediumSurface(OpticalSystem system, int i)
        {
            int j = i - 1;
            while (j >= 0 && system.Surfaces[j].IsMirror) j--;
            return j;
        }

        private static string Weight(double w) =>
            Math.Max(0, Math.Min(100, (int)Math.Round(w * 100.0))).ToString(WriterSupport.Inv);

        // The medium after the surface: a catalog glass by name; a model glass as Optalix's
        // fictitious-glass code where that carries it, else as its index at each wavelength.
        private static void Material(StringBuilder sb, Surface s, int i, WriterSupport w, OpticalSystem system)
        {
            if (s.ModelIndexEnabled)
            {
                string? code = FictitiousGlassCode(s.ModelNd, s.ModelVd, s.ModelDPgF);
                if (code != null)
                    sb.AppendLine($"  GLA {code}");
                else
                    sb.AppendLine("  PRI " + string.Join(" ", system.Wavelengths.Select(x =>
                        w.IndicesAt(x.Value)[i].ToString("R", WriterSupport.Inv))));
            }
            else if (!string.IsNullOrEmpty(s.Material) && !s.Material.Equals("AIR", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"  GLA {s.Material}");
        }

        /// <summary>
        /// Optalix's fictitious-glass code for (nd, Vd): the digits of nd - 1 after the point, a
        /// point, then Vd's two integer digits and its decimals - 1.6201 / 60.4 is <c>6201.604</c>.
        /// Null when the code cannot carry the glass.
        /// </summary>
        public static string? FictitiousGlassCode(double nd, double vd, double dPgF)
        {
            if (dPgF != 0.0 || nd <= 1.0 || nd >= 2.0 || vd < 10.0 || vd >= 100.0) return null;
            if (Math.Abs(Math.Round(nd, 6) - nd) > 1e-12 || Math.Abs(Math.Round(vd, 4) - vd) > 1e-10) return null;
            string ndDigits = Math.Round(nd - 1.0, 6).ToString("0.000000", WriterSupport.Inv).Substring(2).TrimEnd('0');
            if (ndDigits.Length < 3) ndDigits = ndDigits.PadRight(3, '0');
            string vdText = Math.Round(vd, 4).ToString("00.####", WriterSupport.Inv);
            return ndDigits + "." + vdText.Replace(".", "");
        }

        private static void Apertures(StringBuilder sb, Surface s)
        {
            var inv = WriterSupport.Inv;
            if (s.SemiDiameter > 0)
                sb.AppendLine(string.Format(inv, "  APE  1 {0:G15} {0:G15} 0 0 0 1 0 0 1 ''", s.SemiDiameter));
            double obscuration = s.InnerRadius > 0 ? s.InnerRadius : s.ObscurationRadius;
            if (obscuration > 0)
                sb.AppendLine(string.Format(inv, "  APE  2 {0:G15} {0:G15} 0 0 0 1 0 1 1 ''", obscuration));
            if (WriterSupport.Clips(s) && s.SemiDiameter > 0)
                sb.AppendLine("  FH 1 1");
        }
    }
}
