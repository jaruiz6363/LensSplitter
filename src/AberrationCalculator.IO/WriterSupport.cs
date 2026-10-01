using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// What the whole-lens writers share: the refractive indices a conversion needs, the
    /// first-order quantities they convert with - from this program's own paraxial trace - and
    /// the checks that refuse a lens a format cannot carry rather than write a different one.
    /// </summary>
    internal sealed class WriterSupport
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly OpticalSystem _system;
        private readonly GlassCatalog? _glass;
        private readonly string _format;
        private readonly Dictionary<double, double[]> _indices = new();

        public WriterSupport(OpticalSystem system, GlassCatalog? glass, string format)
        {
            _system = system;
            _glass = glass;
            _format = format;
        }

        public bool InfiniteObject =>
            _system.Surfaces.Count == 0
            || double.IsInfinity(_system.Surfaces[0].Thickness)
            || Math.Abs(_system.Surfaces[0].Thickness) >= 1e10;

        public double PrimaryUm =>
            _system.Wavelengths.Count > 0
                ? _system.Wavelengths[Math.Max(0, _system.PrimaryWavelengthIndex)].Value
                : 0.5875618;

        /// <summary>The index after each surface at a wavelength; a model glass by its own dispersion.</summary>
        public double[] IndicesAt(double wavelengthUm)
        {
            if (!_indices.TryGetValue(wavelengthUm, out var n))
            {
                if (_glass == null)
                    throw new InvalidOperationException(
                        $"Writing this lens as {_format} needs its refractive indices, and no glass catalog was given.");
                var unresolved = new List<string>();
                n = IndexResolver.Build(_system, _glass, wavelengthUm, unresolved);
                if (unresolved.Count > 0)
                    throw new InvalidOperationException(
                        $"Writing this lens as {_format} needs the index of {string.Join(", ", unresolved.Distinct())}, "
                        + "which the glass catalogs do not have.");
                _indices[wavelengthUm] = n;
            }
            return n;
        }

        public ParaxialResult Paraxial() => ParaxialTrace.Trace(_system, IndicesAt(PrimaryUm), 0.0);

        /// <summary>The focal length, for turning an F-number into an entrance pupil.</summary>
        public double Efl()
        {
            double efl = Paraxial().Efl;
            if (double.IsInfinity(efl) || double.IsNaN(efl))
                throw new InvalidOperationException("The lens is afocal, so an F-number gives it no entrance pupil.");
            return efl;
        }

        /// <summary>The entrance pupil diameter the lens's aperture gives, whatever its type.</summary>
        public double EntrancePupilDiameter() =>
            _system.Aperture.Type == ApertureType.EPD ? _system.Aperture.Value : Paraxial().Epd;

        /// <summary>
        /// Object to entrance pupil, for an object at a finite distance: its size, or - signed - as
        /// the real trace takes it to place a field angle's object point (negative when the pupil
        /// lies before the object).
        /// </summary>
        public double ObjectToPupil(bool signed = false)
        {
            var p = Paraxial();
            double d = p.EntrancePupilPosition + _system.Surfaces[0].Thickness;
            if (!signed) d = Math.Abs(d);
            if (_system.TelecentricObjectSpace || double.IsInfinity(p.EntrancePupilPosition))
                throw new InvalidOperationException(
                    $"A telecentric object space has no finite entrance pupil to state this aperture or field with in {_format}; "
                    + "give an object NA and object heights.");
            return d;
        }

        /// <summary>
        /// Wavelengths with the primary first, the rest short to long - for a format whose primary
        /// is simply its first wavelength (OSLO).
        /// </summary>
        public List<Wavelength> PrimaryFirst()
        {
            if (_system.Wavelengths.Count == 0) return new List<Wavelength>();
            int primary = _system.PrimaryWavelengthIndex;
            if (primary < 0 || primary >= _system.Wavelengths.Count) primary = 0;
            return _system.Wavelengths
                .Where((_, i) => i != primary)
                .OrderBy(w => w.Value)
                .Prepend(_system.Wavelengths[primary])
                .ToList();
        }

        /// <summary>
        /// Refuses a surface this program can read but no whole-lens writer here writes: a tilt,
        /// a decentre, a coordinate break, an ABCD matrix. This program analyses rotationally
        /// symmetric lenses; writing one of these as something else would hand over a different lens.
        /// </summary>
        public void RefuseUnwritable(bool paraxialAllowed)
        {
            for (int i = 0; i < _system.Surfaces.Count; i++)
            {
                var s = _system.Surfaces[i];
                if (s.IsPerturbed)
                    throw new InvalidOperationException($"Surface {i} is tilted or decentred, which is not written as {_format}.");
                switch (s.Type)
                {
                    case SurfaceType.Standard:
                    case SurfaceType.EvenAsphere:
                        break;
                    case SurfaceType.Paraxial when paraxialAllowed:
                        break;
                    case SurfaceType.Paraxial:
                        throw new InvalidOperationException(
                            $"Surface {i} is an ideal (paraxial) lens, which a {_format} file cannot carry.");
                    default:
                        throw new InvalidOperationException($"Surface {i} is a {s.Type} surface, which is not written as {_format}.");
                }
            }
        }

        /// <summary>
        /// Refuses an r^2 aspheric term, for a format whose even asphere starts at r^4. Written
        /// without it, the file would be a different surface.
        /// </summary>
        public void RefuseR2(string why)
        {
            for (int i = 0; i < _system.Surfaces.Count; i++)
            {
                var a = _system.Surfaces[i].AsphericCoefficients;
                if (a != null && a.Length > 0 && a[0] != 0.0)
                    throw new InvalidOperationException($"Surface {i} has an r² aspheric term, which {why}.");
            }
        }

        /// <summary>A clear aperture that blocks rays: a fixed one, or an automatic one held under 100 %.</summary>
        public static bool Clips(Surface s) =>
            s.SemiDiameterMode == SemiDiameterMode.Fixed
            || (s.ClearAperturePercent > 0 && s.ClearAperturePercent < 100.0);

        public static bool HasFiguring(Surface s) =>
            s.Conic != 0.0 || (s.AsphericCoefficients != null && s.AsphericCoefficients.Any(c => c != 0.0));
    }
}
