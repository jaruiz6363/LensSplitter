using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Fields a lens file gives as image heights, turned into the object fields that reach them.
    ///
    /// <para>This program's fields are object angles or object heights. OpticStudio and Code V can
    /// also give a field as the image height it lands at, paraxial or real, and the readers used to
    /// take those numbers as object angles with nothing said: 0-20 mm of image height became 0-20
    /// degrees. Each image height is now turned into the object field that reaches it at the
    /// primary wavelength - an angle for an object at infinity, else an object height.</para>
    ///
    /// <para>A paraxial image height scales with tan(angle), or with object height, so one
    /// paraxial trace gives every field. A real image height is solved for with the real chief
    /// ray, caught on the image surface the file defines and aimed at the stop's centre if the
    /// file aims rays, as the program that wrote it would trace it. Either way the conversion is
    /// reported in <see cref="OpticalSystem.ImportWarnings"/>: from here on the fields are fixed
    /// object fields, and a design that changes no longer holds the image heights the file asked
    /// for.</para>
    /// </summary>
    internal static class ImageHeightFields
    {
        /// <summary>
        /// Converts <paramref name="system"/>'s fields, which hold image heights in millimetres,
        /// into object fields. Call it once the lens is complete and in millimetres.
        /// </summary>
        /// <param name="real">True for real image heights, false for paraxial ones.</param>
        /// <param name="aimAtStop">
        /// Whether the real chief ray is aimed at the stop's centre: true for Code V, which always
        /// aims; left null, it follows <see cref="OpticalSystem.RayAiming"/>, as OpticStudio does.
        /// </param>
        public static void Convert(OpticalSystem system, bool real, Glass.GlassCatalog? glass,
                                   bool? aimAtStop = null)
        {
            var inv = CultureInfo.InvariantCulture;
            string kind = real ? "real image heights" : "paraxial image heights";
            bool atInfinity = system.Surfaces.Count > 0
                              && (double.IsInfinity(system.Surfaces[0].Thickness) || Math.Abs(system.Surfaces[0].Thickness) >= 1e12);
            system.FieldType = atInfinity ? FieldType.ObjectAngle : FieldType.ObjectHeight;
            string what = atInfinity ? "object angles" : "object heights";
            string unit = atInfinity ? " deg" : " mm";
            var heights = system.Fields.Select(f => f.Y).ToList();
            string given = string.Join(", ", heights.Select(h => h.ToString("0.####", inv)));

            if (glass == null)
            {
                system.ImportWarnings.Add($"The fields are {kind} ({given} mm). With no glass catalog to trace with they could not be converted, and were read as {what}, which they are not.");
                return;
            }

            int wave = Math.Max(0, system.PrimaryWavelengthIndex);
            double lambda = system.Wavelengths.Count > 0 ? system.Wavelengths[wave].Value : 0.5875618;
            var unresolved = new List<string>();
            double[] n = Glass.IndexResolver.Build(system, glass, lambda, unresolved);

            // The paraxial image height of a reference field: 1 degree, or 1 mm of object.
            const double refField = 1.0;
            RayTrace.ParaxialResult paraxial;
            try { paraxial = RayTrace.ParaxialTrace.Trace(system, n, refField); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                system.ImportWarnings.Add($"The fields are {kind} ({given} mm), and the paraxial trace that would convert them failed ({ex.Message}); they were read as {what}, which they are not.");
                return;
            }
            double paraxialAtRef = paraxial.ParaxialImageHeight;
            if (!(Math.Abs(paraxialAtRef) > 1e-12))
            {
                system.ImportWarnings.Add($"The fields are {kind} ({given} mm), and the lens forms no paraxial image height to convert them with; they were read as {what}, which they are not.");
                return;
            }

            // Paraxial image height per unit of tan(angle), or per mm of object height.
            double perUnit = Math.Abs(paraxialAtRef) / (atInfinity ? Math.Tan(refField * Math.PI / 180.0) : refField);
            double Paraxial(double h) => atInfinity
                ? Math.Atan(Math.Abs(h) / perUnit) * 180.0 / Math.PI * Math.Sign(h)
                : Math.Abs(h) / perUnit * Math.Sign(h);

            RayTrace.StopAimer? aimer = null;
            if (real && (aimAtStop ?? system.RayAiming != RayAimingMode.Off))
            {
                try { aimer = RayTrace.StopAimer.ForSystem(system, n, paraxial); }
                catch (ArgumentException) { }
            }

            double? RealImageY(double field)
            {
                try
                {
                    double py = 0.0, pz = 0.0;
                    if (aimer != null)
                    {
                        var launch = aimer.Launch(field, 0.0, 0.0);
                        if (launch == null) return null;
                        (py, pz) = launch.Value;
                    }
                    var hit = RayTrace.RealRayTrace.Trace(system, n, paraxial, field, py, pz, atParaxialFocus: false);
                    return hit.Ok ? Math.Abs(hit.Y) : null;
                }
                catch (InvalidOperationException) { return null; }
            }

            int unsolved = 0;
            for (int i = 0; i < system.Fields.Count; i++)
            {
                double h = heights[i];
                double field = Paraxial(h);
                if (real && h != 0)
                {
                    // Secant on |y_image(field)| = |h|, starting from the paraxial answer.
                    double target = Math.Abs(h), x0 = field, x1 = field * 1.001;
                    double? y0 = RealImageY(x0), y1 = RealImageY(x1);
                    bool solved = false;
                    for (int it = 0; it < 30 && y0.HasValue && y1.HasValue; it++)
                    {
                        double f0 = y0.Value - target, f1 = y1.Value - target;
                        if (Math.Abs(f1) <= 1e-9 * Math.Max(1.0, target)) { field = x1; solved = true; break; }
                        if (f1 == f0) break;
                        double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
                        x0 = x1; y0 = y1; x1 = x2; y1 = RealImageY(x1);
                    }
                    if (!solved) unsolved++;
                }
                system.Fields[i].Y = field;
            }

            string converted = string.Join(", ", system.Fields.Select(f => f.Y.ToString("0.######", inv) + unit));
            string how = real
                ? (aimer != null ? "by the real chief ray, aimed at the stop's centre" : "by the real chief ray")
                : "paraxially";
            system.ImportWarnings.Add(
                $"The fields were {kind} ({given} mm), which this program does not have. They were converted {how}, at the primary wavelength, to the {what} that reach them: {converted}. " +
                "They stay fixed from here on, so a design that changes no longer holds those image heights." +
                (unsolved > 0 ? $" {unsolved} could not be solved with a real ray and use the paraxial value." : "") +
                (unresolved.Count > 0 ? $" The glass {string.Join(", ", unresolved.Distinct())} did not resolve, so the conversion is wrong for it." : ""));
        }
    }
}
