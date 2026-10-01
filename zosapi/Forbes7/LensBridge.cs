using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using ZOSAPI;
using ZOSAPI.Editors.LDE;

namespace AberrationCalculator.Forbes7
{
    /// <summary>
    /// Reads the lens OpticStudio has open into the form the aberration code works in.
    ///
    /// <para>The refractive indices come from OpticStudio itself, through
    /// <c>ILensDataEditor.GetIndex</c>, and not from a glass catalogue of our own. That is
    /// deliberate: it means the coefficients are computed for the same glass data the design
    /// was made with, including catalogues we do not ship and model glasses we could not
    /// resolve, and it removes a whole class of disagreement that would otherwise have to be
    /// chased before any number here could be trusted.</para>
    /// </summary>
    internal static class LensBridge
    {
        /// <summary>A thickness at or beyond this is an infinite conjugate.</summary>
        private const double Infinite = 1e10;

        internal sealed class Read
        {
            public OpticalSystem System = new OpticalSystem();
            public double[] Indices = Array.Empty<double>();
            public double MaxFieldDegrees;
            public double Wavelength;
            public string Notes = string.Empty;
        }

        /// <summary>
        /// Refuses rather than misleads. The Forbes trace handles spheres, conics and even
        /// aspheres at either conjugate; a mirror, a coordinate break or a surface type it has
        /// no series for would silently be treated as something it is not, so it is turned away
        /// with the reason given. NOT a tilt or decentre on an ordinary surface: those
        /// properties are not read at all, so such a design is analysed as if centred.
        /// </summary>
        internal static Read Build(IOpticalSystem zos)
        {
            var lde = zos.LDE;
            int count = lde.NumberOfSurfaces;
            if (count < 3) throw new InvalidOperationException("The lens needs at least two optical surfaces.");

            var sys = new OpticalSystem { Title = zos.SystemName ?? string.Empty };
            var indices = new double[count];

            int primary = 1;
            for (int w = 1; w <= zos.SystemData.Wavelengths.NumberOfWavelengths; w++)
                if (zos.SystemData.Wavelengths.GetWavelength(w).IsPrimary) { primary = w; break; }
            double wavelength = 0.0;
            for (int w = 1; w <= zos.SystemData.Wavelengths.NumberOfWavelengths; w++)
            {
                var wl = zos.SystemData.Wavelengths.GetWavelength(w);
                sys.Wavelengths.Add(new Wavelength { Value = wl.Wavelength, Weight = wl.Weight,
                                                     IsPrimary = w == primary });
                if (w == primary) wavelength = wl.Wavelength;
            }
            if (sys.Wavelengths.Count == 0)
                throw new InvalidOperationException("The system has no wavelengths.");

            // Indices at the primary wavelength, from OpticStudio's own glass data. GetIndex
            // fills one value per wavelength for the region AFTER the surface it is asked about.
            var buffer = new double[Math.Max(1, zos.SystemData.Wavelengths.NumberOfWavelengths)];

            for (int i = 0; i < count; i++)
            {
                ILDERow row = lde.GetSurfaceAt(i);
                string type = row.TypeName ?? "";

                bool standard = type.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0;
                bool evenAsphere = type.IndexOf("Even Asphere", StringComparison.OrdinalIgnoreCase) >= 0;
                if (i > 0 && i < count - 1 && !standard && !evenAsphere)
                    throw new InvalidOperationException(
                        "Surface " + i + " is a " + type + ". This reads Standard and Even Asphere " +
                        "surfaces; anything else would have to be approximated by one of them, and " +
                        "reporting the coefficients of a surface the design does not have would be " +
                        "worse than declining.");

                double radius = row.Radius;
                double curvature = (double.IsInfinity(radius) || double.IsNaN(radius)
                                    || Math.Abs(radius) < 1e-12 || Math.Abs(radius) > Infinite)
                                 ? 0.0 : 1.0 / radius;

                double thickness = row.Thickness;
                if (double.IsInfinity(thickness) || Math.Abs(thickness) >= Infinite)
                    thickness = double.PositiveInfinity;

                var surface = new Surface
                {
                    Index = i,
                    Curvature = curvature,
                    Thickness = thickness,
                    Conic = row.Conic,
                    Material = string.IsNullOrWhiteSpace(row.Material) ? null : row.Material,
                    IsStop = row.IsStop,
                    SemiDiameter = row.SemiDiameter,
                    Comment = row.Comment,
                };

                if (evenAsphere)
                {
                    // Zemax's Even Asphere parameters are the coefficients of r^2, r^4, ...,
                    // which is the order this program stores them in as well.
                    for (int k = 0; k < surface.AsphericCoefficients.Length; k++)
                    {
                        var column = (SurfaceColumn)((int)SurfaceColumn.Par1 + k);
                        try { surface.AsphericCoefficients[k] = row.GetSurfaceCell(column).DoubleValue; }
                        catch (Exception) { break; }
                    }
                }

                // A mirror is refused by name. GetIndex reports the magnitude of the index, as
                // ZPL's INDX does, so nothing downstream would learn that the surface reflects:
                // until September 2026 the series trace took it as a refraction into the same
                // medium and returned near-zero coefficients. The shared trace now declines a
                // mirror itself, but then the report comes back empty with a reason about fields
                // and convergence, which is not the reason. This says the real one.
                if (surface.IsMirror)
                    throw new InvalidOperationException(
                        "Surface " + i + " is a mirror. The Forbes series trace does not trace a " +
                        "reflection - the root that picks the reflected cosine is not written - so " +
                        "it declines. BUCH7_ASPH.ZPL and RAYINV.ZPL handle mirrors, and BUCH7.ZPL " +
                        "does on a spherical one.");

                sys.Surfaces.Add(surface);

                int got = lde.GetIndex(i, buffer.Length, buffer);
                indices[i] = got > 0 && primary - 1 < got ? buffer[primary - 1] : 1.0;
            }

            // The stop. OpticStudio always has one; if the flag did not come through, the
            // aperture stop is meaningless and so is Buchdahl's p.
            int stop = -1;
            for (int i = 0; i < sys.Surfaces.Count; i++) if (sys.Surfaces[i].IsStop) { stop = i; break; }
            if (stop < 0) throw new InvalidOperationException("No surface is flagged as the stop.");

            // Aperture. Only an entrance-pupil diameter is carried across as such; the others
            // are converted by the paraxial trace from the value OpticStudio reports.
            sys.Aperture = new Aperture(MapAperture(zos.SystemData.Aperture.ApertureType),
                                        zos.SystemData.Aperture.ApertureValue);

            // Fields.
            var fields = zos.SystemData.Fields;
            sys.FieldType = MapFieldType(fields.GetFieldType());
            double maxField = 0.0;
            for (int f = 1; f <= fields.NumberOfFields; f++)
            {
                var fd = fields.GetField(f);
                sys.Fields.Add(new Field { X = fd.X, Y = fd.Y, Weight = fd.Weight });
                if (Math.Abs(fd.Y) > Math.Abs(maxField)) maxField = fd.Y;
            }
            if (Math.Abs(maxField) < 1e-12)
                throw new InvalidOperationException(
                    "Every field is zero. The seventh order is mostly field-dependent, so there " +
                    "would be nothing to report but spherical aberration.");

            return new Read
            {
                System = sys,
                Indices = indices,
                MaxFieldDegrees = maxField,
                Wavelength = wavelength,
                Notes = double.IsInfinity(sys.Surfaces[0].Thickness)
                      ? "object at infinity"
                      : "finite conjugate, object " + sys.Surfaces[0].Thickness.ToString("0.###") + " away",
            };
        }

        // The three aperture kinds and two field kinds this program's paraxial trace knows.
        // The rest are refused rather than approximated by the nearest one: a wrong pupil or a
        // wrong field scales every coefficient here, and silently.
        private static ApertureType MapAperture(ZOSAPI.SystemData.ZemaxApertureType t)
        {
            switch (t)
            {
                case ZOSAPI.SystemData.ZemaxApertureType.EntrancePupilDiameter: return ApertureType.EPD;
                case ZOSAPI.SystemData.ZemaxApertureType.ImageSpaceFNum: return ApertureType.FNumber;
                case ZOSAPI.SystemData.ZemaxApertureType.ObjectSpaceNA: return ApertureType.ObjectSpaceNA;
                default:
                    throw new InvalidOperationException(
                        "The aperture is defined as " + t + ". This reads an entrance pupil " +
                        "diameter, an image-space F/number or an object-space NA; the others " +
                        "would have to be guessed at, and the pupil scales every coefficient.");
            }
        }

        private static FieldType MapFieldType(ZOSAPI.SystemData.FieldType t)
        {
            switch (t)
            {
                case ZOSAPI.SystemData.FieldType.Angle: return FieldType.ObjectAngle;
                case ZOSAPI.SystemData.FieldType.ObjectHeight: return FieldType.ObjectHeight;
                default:
                    throw new InvalidOperationException(
                        "The field is defined as " + t + ". This reads an angle or an object " +
                        "height. An image height would have to be converted back through the " +
                        "system, and getting that wrong would move every field-bearing " +
                        "coefficient without any sign that it had.");
            }
        }
    }
}
