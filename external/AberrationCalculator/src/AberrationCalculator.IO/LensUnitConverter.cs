using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Converts an optical system from arbitrary lens units to millimeters.
    /// Scales all distance-based properties: radii, thicknesses, semi-diameters,
    /// aperture radii, aspheric coefficients, EPD, and ObjectHeight fields.
    /// </summary>
    public static class LensUnitConverter
    {
        /// <summary>
        /// Scale all distance-based properties from file lens units to mm.
        /// Call with scale = 25.4 for inches, 10.0 for cm, 1000.0 for meters.
        /// </summary>
        public static void ConvertToMm(OpticalSystem system, double scale)
        {
            if (scale == 1.0) return;

            // Aperture: EPD is a distance, F/# is dimensionless
            if (system.Aperture.Type == ApertureType.EPD)
                system.Aperture = new Aperture(ApertureType.EPD, system.Aperture.Value * scale);

            // Fields: ObjectHeight is in lens units, ObjectAngle is in degrees (no conversion)
            if (system.FieldType == FieldType.ObjectHeight)
            {
                for (int i = 0; i < system.Fields.Count; i++)
                    system.Fields[i] = new Field(system.Fields[i].Y * scale, system.Fields[i].Weight);
            }

            // Surfaces: radius, thickness, semi-diameter, aperture radii, aspheric coefficients
            foreach (var surface in system.Surfaces)
            {
                if (!double.IsInfinity(surface.Radius))
                    surface.Radius *= scale;

                if (!double.IsInfinity(surface.Thickness))
                    surface.Thickness *= scale;

                surface.SemiDiameter *= scale;
                surface.InnerRadius *= scale;
                surface.ClapOuterRadius *= scale;
                surface.ObscurationRadius *= scale;
                surface.FloatingApertureRadius *= scale;

                // Aspheric coefficients: alpha_i for r^(2i) has units 1/length^(2i-1).
                // Converting: alpha_mm = alpha_orig / scale^(2i-1)
                for (int i = 0; i < surface.AsphericCoefficients.Length; i++)
                {
                    if (surface.AsphericCoefficients[i] != 0)
                    {
                        int power = 2 * (i + 1) - 1; // exponent: 1, 3, 5, 7, ...
                        surface.AsphericCoefficients[i] /= Math.Pow(scale, power);
                    }
                }
            }
        }
    }
}
