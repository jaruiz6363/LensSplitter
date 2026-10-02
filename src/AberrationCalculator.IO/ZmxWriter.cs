using System;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes a whole lens as a ZEMAX .zmx file, as ZEMAX writes one: UTF-16 with a byte-order
    /// mark; the aperture as <c>ENPD</c>, <c>FNUM</c> or <c>OBNA</c>; fields as angles or object
    /// heights on <c>FTYP</c>; <c>PWAV</c> naming the primary wavelength; a model glass as
    /// <c>GLAS ___BLANK</c> with its nd, Vd and dPgF; an even asphere as <c>EVENASPH</c> with its
    /// terms from <c>PARM 1</c>, the r^2 term, upward; and <c>DIAM</c> only for a fixed aperture.
    ///
    /// <para>Ported from LensHH-LT's writer (MIT, Synapse Optics). <see cref="LensPatcher"/>
    /// edits a lens file in place and keeps everything the program does not model; this writes a
    /// lens that has no file yet, or whose surfaces have changed in number.</para>
    /// </summary>
    public static class ZmxWriter
    {
        public static void Write(OpticalSystem system, string filePath)
        {
            new WriterSupport(system, null, "ZEMAX").RefuseUnwritable(paraxialAllowed: true);
            var inv = WriterSupport.Inv;
            var sb = new StringBuilder();

            sb.AppendLine("VERS 140228 258 40400");
            sb.AppendLine("MODE SEQ");
            if (!string.IsNullOrEmpty(system.Title))
                sb.AppendLine($"TITL {system.Title}");
            sb.AppendLine("UNIT MM X W X CM MR CPMM");
            if (system.GlassCatalogs.Count > 0)
                sb.AppendLine("GCAT " + string.Join(" ", system.GlassCatalogs));

            switch (system.Aperture.Type)
            {
                case ApertureType.EPD:
                    sb.AppendLine("ENPD " + G(system.Aperture.Value));
                    break;
                case ApertureType.FNumber:
                    sb.AppendLine("FNUM " + G(system.Aperture.Value));
                    break;
                case ApertureType.ObjectSpaceNA:
                    // The second field is not the telecentric flag; that is on FTYP.
                    sb.AppendLine("OBNA " + G(system.Aperture.Value) + " 0");
                    break;
            }

            int ftype = system.FieldType == FieldType.ObjectAngle ? 0 : 1;
            int telecentric = system.TelecentricObjectSpace ? 1 : 0;
            int afocal = system.IsAfocal ? 1 : 0;
            sb.AppendLine($"FTYP {ftype} {telecentric} {system.Fields.Count} {system.Wavelengths.Count} 0 0 {afocal} 0 0");
            bool real = system.RayAiming == RayAimingMode.Real || system.RayAiming == RayAimingMode.Robust;
            sb.AppendLine($"RAIM 0 {(real ? 2 : 0)} 1 1 0 {(system.RayAiming == RayAimingMode.Robust ? 1 : 0)} 0 0 0 1");

            sb.Append("XFLN");
            foreach (var f in system.Fields) sb.Append(' ').Append(f.X.ToString("R", inv));
            sb.AppendLine();
            sb.Append("YFLN");
            foreach (var f in system.Fields) sb.Append(' ').Append(f.Y.ToString("R", inv));
            sb.AppendLine();
            sb.Append("FWGN");
            foreach (var f in system.Fields) sb.Append(' ').Append(f.Weight.ToString("R", inv));
            sb.AppendLine();

            for (int i = 0; i < system.Wavelengths.Count; i++)
            {
                var w = system.Wavelengths[i];
                sb.AppendLine($"WAVM {i + 1} {w.Value.ToString("R", inv)} {w.Weight.ToString("R", inv)}");
            }
            sb.AppendLine($"PWAV {Math.Max(0, system.PrimaryWavelengthIndex) + 1}");

            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                sb.AppendLine($"SURF {i}");
                if (!string.IsNullOrEmpty(s.Comment))
                    sb.AppendLine($"  COMM {s.Comment}");
                if (s.IsStop)
                    sb.AppendLine("  STOP");

                // Any surface with a polynomial term goes out as an even asphere, whatever type it
                // was read as - an optimised sphere can have gained terms. It carries a conic too.
                bool evenAsph = s.Type != SurfaceType.Paraxial && Array.Exists(s.AsphericCoefficients, c => c != 0.0);
                sb.AppendLine(s.Type == SurfaceType.Paraxial ? "  TYPE PARAXIAL"
                            : evenAsph ? "  TYPE EVENASPH"
                            : "  TYPE STANDARD");

                sb.AppendLine("  CURV " + G(s.Curvature));
                // ZEMAX reads INFINITY in capitals and refuses "Infinity".
                sb.AppendLine(double.IsPositiveInfinity(s.Thickness) ? "  DISZ INFINITY" : "  DISZ " + G(s.Thickness));

                if (s.ModelIndexEnabled)
                    sb.AppendLine($"  GLAS ___BLANK 1 0 {G(s.ModelNd)} {G(s.ModelVd)} {G(s.ModelDPgF)} 0 0 0 0 0");
                else if (!string.IsNullOrEmpty(s.Material))
                    sb.AppendLine($"  GLAS {s.Material} 0 0 0 0 0");

                if (s.SemiDiameterMode == SemiDiameterMode.Fixed && s.SemiDiameter > 0)
                    sb.AppendLine($"  DIAM {G(s.SemiDiameter)} 1 0 0 1 \"\"");
                if (s.Conic != 0)
                    sb.AppendLine("  CONI " + G(s.Conic));
                if (s.InnerRadius > 0 || s.ClapOuterRadius > 0)
                {
                    double outer = s.ClapOuterRadius > 0 ? s.ClapOuterRadius : s.SemiDiameter;
                    sb.AppendLine($"  CLAP {G(s.InnerRadius)} {G(outer)} 0");
                }
                if (s.ObscurationRadius > 0)
                    sb.AppendLine($"  OBSC 0 {G(s.ObscurationRadius)} 0");
                if (s.FloatingApertureRadius > 0)
                    sb.AppendLine($"  FLAP 0 {G(s.FloatingApertureRadius)} 0");

                if (evenAsph)
                {
                    // PARM n is the r^(2n) term: PARM 1 is r^2, as this program's slot 0.
                    for (int k = 0; k < s.AsphericCoefficients.Length; k++)
                        if (s.AsphericCoefficients[k] != 0)
                            sb.AppendLine($"  PARM {k + 1} {s.AsphericCoefficients[k].ToString("E16", inv)}");
                }
                else if (s.Type == SurfaceType.Paraxial)
                {
                    sb.AppendLine("  PARM 1 " + G(s.FocalLength));
                    sb.AppendLine("  PARM 2 1");   // compute the OPD as a real lens would
                }
            }

            // ZEMAX writes its own files as UTF-16 LE with a byte-order mark, and misreads UTF-8's.
            File.WriteAllText(filePath, sb.ToString(), Encoding.Unicode);
        }

        private static string G(double v) => v.ToString("G17", WriterSupport.Inv);
    }
}
