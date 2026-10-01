using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes a whole lens as an OSLO .len file, as OSLO 6.6 writes one:
    /// <list type="bullet">
    /// <item>an object at infinity takes <c>EBR</c> and <c>ANG</c>, a finite one <c>NAO</c> and
    /// <c>OBH</c>, converted from the lens's own aperture and field by a paraxial trace;</item>
    /// <item>the primary wavelength goes first on the <c>WV</c> line, which is how OSLO knows it;</item>
    /// <item>a model glass is <c>GLA MOD</c> with its index at each of those wavelengths;</item>
    /// <item>an ideal lens is OSLO's perfect lens, <c>PFL</c>, with <c>PFM</c> at a finite conjugate;</item>
    /// <item>only an aperture that clips is checked (<c>AP CHK</c>); the others go out as <c>AP</c>;</item>
    /// <item>no word of the <c>LEN NEW</c> name may be a number, or OSLO reads it as the surface count.</item>
    /// </list>
    /// A surface with an r^2 aspheric term is refused: OSLO's standard asphere starts at r^4.
    ///
    /// <para>Ported from LensHH-LT's writer (MIT, Synapse Optics).</para>
    /// </summary>
    public static class OsloWriter
    {
        public static void Write(OpticalSystem system, string filePath, GlassCatalog? glass)
        {
            var w = new WriterSupport(system, glass, "OSLO");
            w.RefuseUnwritable(paraxialAllowed: true);
            w.RefuseR2("OSLO's standard asphere has no place for");
            var inv = WriterSupport.Inv;
            var wavelengths = w.PrimaryFirst();

            var sb = new StringBuilder();
            sb.AppendLine("// OSLO 5.10");
            sb.AppendLine("// Exported from AberrationCalculator");
            string title = string.IsNullOrEmpty(system.Title) ? "Untitled" : system.Title;
            sb.AppendLine($"LEN NEW \"{LenName(title)}\"");
            sb.AppendLine($"SNO1 \"{title.Replace("\"", "'")}\"");
            if (!string.IsNullOrEmpty(system.Notes))
            {
                int slot = 2;
                foreach (var line in system.Notes.Replace("\r\n", "\n").Split('\n'))
                {
                    if (slot > 10) break;   // OSLO keeps nine more note lines
                    sb.AppendLine($"SNO{slot++} \"{line.Replace("\"", "'")}\"");
                }
            }
            if (!string.IsNullOrWhiteSpace(system.Designer))
                sb.AppendLine($"DES \"{system.Designer.Replace("\"", "'")}\"");
            sb.AppendLine("UNI 1.0");

            double maxField = system.Fields.Count == 0 ? 0.0 : system.Fields.Max(f => Math.Abs(f.Y));
            if (w.InfiniteObject)
            {
                if (system.Aperture.Type == ApertureType.ObjectSpaceNA)
                    throw new InvalidOperationException("An object-space NA needs a finite object; OSLO cannot take one for an object at infinity.");
                if (system.FieldType == FieldType.ObjectHeight)
                    throw new InvalidOperationException("Fields given as object heights need a finite object; OSLO takes a field angle for an object at infinity.");
                sb.AppendLine(string.Format(inv, "EBR {0:R}", w.EntrancePupilDiameter() / 2.0));
                sb.AppendLine(string.Format(inv, "ANG {0:R}", maxField));
            }
            else
            {
                double nao = system.Aperture.Type == ApertureType.ObjectSpaceNA
                    ? system.Aperture.Value
                    : Math.Abs(w.IndicesAt(w.PrimaryUm)[0]) * Math.Sin(Math.Atan(w.EntrancePupilDiameter() / 2.0 / w.ObjectToPupil()));
                // OSLO's OBH is the object point's y (checked in OSLO 6.6: OBH 10, 100 before a
                // singlet, sends the chief ray down through the stop to an image at y = -9.5). A
                // field angle here aims the chief ray UP at the pupil, from an object BELOW the
                // axis (RealRayTrace), so the height is negative - positive only when the pupil
                // lies before the object. (This wrote +|d| tan(angle): the object on the wrong
                // side, every image in OSLO mirrored.)
                double obh = system.FieldType == FieldType.ObjectHeight
                    ? maxField
                    : -w.ObjectToPupil(signed: true) * Math.Tan(maxField * Math.PI / 180.0);
                sb.AppendLine(string.Format(inv, "NAO {0:R}", nao));
                sb.AppendLine(string.Format(inv, "OBH {0:R}", obh));
            }

            // The medium after a surface. A model glass as OSLO writes one: the wavelengths, then
            // its index at each, by this program's model dispersion, so OSLO traces the lens this
            // program traces. Surface 0's is the object space's, when that is not air.
            void Material(Surface s, int i)
            {
                if (s.IsMirror)
                    sb.AppendLine("  RFH");
                else if (s.ModelIndexEnabled)
                {
                    sb.AppendLine("  WV " + string.Join(" ", wavelengths.Select(x => x.Value.ToString("R", inv))));
                    sb.AppendLine($"  GLA MOD MODEL{i} " + string.Join(" ", wavelengths.Select(x =>
                        w.IndicesAt(x.Value)[i].ToString("R", inv))));
                }
                else if (!string.IsNullOrEmpty(s.Material) && !s.Material.Equals("AIR", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"  GLA {s.Material}");
            }

            sb.AppendLine("// SRF 0");
            if (system.Surfaces.Count > 0)
            {
                var s0 = system.Surfaces[0];
                if (!double.IsInfinity(s0.Radius))
                    sb.AppendLine(string.Format(inv, "  RD {0:G15}", s0.Radius));
                Material(s0, 0);
                sb.AppendLine(string.Format(inv, "  TH {0:R}", double.IsPositiveInfinity(s0.Thickness) ? 1e20 : s0.Thickness));
            }

            for (int i = 1; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                sb.AppendLine($"NXT // SRF {i}");
                if (!double.IsInfinity(s.Radius))
                    sb.AppendLine(string.Format(inv, "  RD {0:G15}", s.Radius));
                Material(s, i);

                if (s.Type == SurfaceType.Paraxial)
                {
                    sb.AppendLine(string.Format(inv, "  PFL {0:G15}", s.FocalLength));
                    if (!w.InfiniteObject)
                        sb.AppendLine(string.Format(inv, "  PFM {0:R}", PerfectLensMagnification(w, i)));
                }

                if (s.IsStop)
                    sb.AppendLine("  AST");
                if (s.Conic != 0)
                    sb.AppendLine(string.Format(inv, "  CC {0:R}", s.Conic));

                // AD..AJ are r^4..r^16, this program's slots 1..7.
                string[] keys = { "AD", "AE", "AF", "AG", "AH", "AI", "AJ" };
                var a = s.AsphericCoefficients;
                for (int c = 0; c < keys.Length; c++)
                    if (c + 1 < a.Length && a[c + 1] != 0)
                        sb.AppendLine(string.Format(inv, "  {0} {1:R}", keys[c], a[c + 1]));
                if (a.Length > 8 && a.Skip(8).Any(c => c != 0))
                    throw new InvalidOperationException($"Surface {i} has an aspheric term beyond r^16, which OSLO's standard asphere has no place for.");

                // Checked only where the aperture clips; OSLO uses an unchecked one to draw the
                // surface and never to block a ray. An automatic stop is left to OSLO, which sizes
                // it from EBR.
                if (s.SemiDiameter > 0)
                {
                    if (WriterSupport.Clips(s))
                        sb.AppendLine(string.Format(inv, "  AP CHK {0:G15}", s.SemiDiameter));
                    else if (!s.IsStop)
                        sb.AppendLine(string.Format(inv, "  AP {0:G15}", s.SemiDiameter));
                }

                double obscuration = s.InnerRadius > 0 ? s.InnerRadius : s.ObscurationRadius;
                if (obscuration > 0)
                {
                    sb.AppendLine("  APN 1");
                    sb.AppendLine(string.Format(inv, "  AY1 A {0:G10}", -obscuration));
                    sb.AppendLine(string.Format(inv, "  AY2 A {0:G10}", obscuration));
                    sb.AppendLine(string.Format(inv, "  AX1 A {0:G10}", -obscuration));
                    sb.AppendLine(string.Format(inv, "  AX2 A {0:G10}", obscuration));
                    sb.AppendLine("  ATP A 1");
                    sb.AppendLine("  AAC A 2");
                }

                // A thickness pickup of scale +1 or -1 is OSLO's PK TH / PK THM.
                var pickup = system.Pickups?.FirstOrDefault(p => p.TargetSurfaceIndex == i
                    && p.Parameter == PickupParameter.Thickness
                    && (Math.Abs(p.ScaleFactor - 1.0) < 1e-12 || Math.Abs(p.ScaleFactor + 1.0) < 1e-12));
                if (pickup != null)
                    sb.AppendLine(string.Format(inv, "  PK {0} {1} {2:G15}",
                        pickup.ScaleFactor < 0 ? "THM" : "TH", pickup.SourceSurfaceIndex - i, pickup.Offset));
                else
                    sb.AppendLine(string.Format(inv, "  TH {0:G15}", i < system.Surfaces.Count - 1 ? s.Thickness : 0.0));

                if (s.HasMarginalRaySolve)
                    sb.AppendLine("CALLBACK  1");
            }

            if (wavelengths.Count > 0)
            {
                // Every digit: rounded to five places, d at 0.5875618 became 0.58756 and moved the
                // index, and the focal length, in the seventh figure.
                sb.AppendLine("WV  " + string.Join(" ", wavelengths.Select(x => x.Value.ToString("R", inv))));
                sb.AppendLine("WW  " + string.Join(" ", wavelengths.Select(x => x.Weight.ToString("G", inv))));
            }
            sb.AppendLine($"END {system.Surfaces.Count - 1}");
            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        // The magnification a perfect lens is perfect at: m = n u / (n' u') across it, from the
        // axial ray of the paraxial trace.
        private static double PerfectLensMagnification(WriterSupport w, int s)
        {
            var p = w.Paraxial();
            double uPrime = p.N[s] * p.U[s];
            if (Math.Abs(uPrime) < 1e-300) return 1e7;   // imaged at infinity; OSLO caps it there
            return p.N[s - 1] * p.U[s - 1] / uPrime;
        }

        /// <summary>
        /// OSLO's LEN NEW name: at most 32 characters, no double quotes, and no word that is a
        /// number - OSLO reads one as the surface count and refuses the file. Such words are
        /// dropped, and dropped again after the cut, which can leave a new one at the end
        /// ("... DIA; 100.00MM EFL" cut to "... DIA; 1"). SNO1 keeps the full title.
        /// </summary>
        public static string LenName(string title)
        {
            static string NoNumbers(string t) => string.Join(" ", t
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out _)));
            string s = NoNumbers(title.Replace("\"", "'"));
            while (s.Length > 32)
                s = NoNumbers(s.Substring(0, 32));
            return s.Length == 0 ? "Untitled" : s;
        }
    }
}
