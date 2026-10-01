using System;
using System.Collections.Generic;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Saves a design: back into the format it was read from, by editing that file, or into another
    /// format, by writing a whole new lens.
    ///
    /// <para><b>The same format is edited, never regenerated.</b> A real lens file carries far more
    /// than this program models - solves, coatings, tolerances, configurations - and
    /// <see cref="LensPatcher"/> changes only what the optimiser moved, keeping every other byte.</para>
    ///
    /// <para><b>Another format is written whole</b>, by <see cref="LensFile.Write"/>: what this
    /// program models goes across - surfaces, glasses, conics and aspheric terms, aperture, fields,
    /// wavelengths - and nothing else can, since the target format has never seen the rest. The
    /// returned note says so. A design the target format cannot carry - an r^2 aspheric term in
    /// CODE V, OSLO or OPTALIX - is refused with the reason.</para>
    ///
    /// <para>This used to go through <see cref="LensPatcher"/> whatever the output was called, so
    /// saving a ZEMAX design "as" <c>out.len</c> wrote ZEMAX text into a file named for OSLO.</para>
    /// </summary>
    public static class LensSave
    {
        /// <summary>
        /// Saves <paramref name="system"/>, read from <paramref name="originalPath"/>, to
        /// <paramref name="outputPath"/>.
        /// </summary>
        /// <returns>Notes for the user; empty for a save into the same format.</returns>
        /// <exception cref="NotSupportedException">The output format cannot carry the design, or
        /// is not one this program writes. Nothing is written.</exception>
        public static IReadOnlyList<string> Save(OpticalSystem system, string originalPath, string outputPath,
                                                 GlassCatalog? catalog = null, bool installOptilandGlasses = true)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (originalPath == null) throw new ArgumentNullException(nameof(originalPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            string from = Format(originalPath), to = Format(outputPath);
            if (from == to)
            {
                LensPatcher.Save(system, originalPath, outputPath, catalog);
                Verify(system, outputPath, catalog, sameFormat: true);
                return Array.Empty<string>();
            }

            if (Array.IndexOf(LensFile.WritableExtensions, to) < 0)
                throw new NotSupportedException(
                    $"'{Path.GetExtension(outputPath)}' is not a lens format this program writes. "
                    + $"Supported: {string.Join(", ", LensFile.WritableExtensions)}.");

            var notes = new List<string>();
            try
            {
                notes.AddRange(LensFile.Write(system, outputPath, catalog, installOptilandGlasses));
            }
            catch (InvalidOperationException ex)
            {
                throw new NotSupportedException($"The design cannot be written as {Name(to)}: {ex.Message}", ex);
            }
            Verify(system, outputPath, catalog, sameFormat: false);
            notes.Insert(0,
                $"Written as {Name(to)}, a new lens made from the design, not an edit of the {Name(from)} file: "
                + "its surfaces, glasses, conics and aspheric terms, aperture, fields and wavelengths go across, "
                + $"and anything else the {Name(from)} file held (solves, coatings, tolerances, configurations) does not.");
            return notes;
        }

        /// <summary>
        /// Reads the file just written and checks that it says what the design says: every
        /// surface's curvature, thickness, conic and aspheric terms, and - for a file edited in its
        /// own format - which surfaces have glass after them.
        ///
        /// <para><b>A save is not done until it reads back.</b> The editor changes a file line by
        /// line, and a line it cannot find it cannot change; for as long as that went unchecked, a
        /// file it could not parse into lines came back unchanged and the run still printed
        /// "Written" (September 2026). Reading it back costs one parse and turns that into an error
        /// naming the surface and the value that did not arrive.</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">The file does not carry the design.</exception>
        internal static void Verify(OpticalSystem system, string path, GlassCatalog? catalog, bool sameFormat)
        {
            var back = LensFile.Read(path, catalog ?? CatalogLocator.LoadBundled());
            var wrong = new List<string>();
            if (back.Surfaces.Count != system.Surfaces.Count)
                wrong.Add($"{back.Surfaces.Count} surfaces where the design has {system.Surfaces.Count}");
            int count = Math.Min(back.Surfaces.Count, system.Surfaces.Count);
            for (int i = 0; i < count; i++)
            {
                var want = system.Surfaces[i];
                var got = back.Surfaces[i];
                if (!Same(want.Curvature, got.Curvature))
                    wrong.Add($"surface {i} curvature {got.Curvature:R} where the design has {want.Curvature:R}");
                // The image surface's thickness is not a distance to anything, and some formats
                // do not keep it.
                if (i < system.Surfaces.Count - 1 && !Same(want.Thickness, got.Thickness))
                    wrong.Add($"surface {i} thickness {got.Thickness:R} where the design has {want.Thickness:R}");
                if (!Same(want.Conic, got.Conic))
                    wrong.Add($"surface {i} conic {got.Conic:R} where the design has {want.Conic:R}");
                int terms = Math.Max(want.AsphericCoefficients.Length, got.AsphericCoefficients.Length);
                for (int k = 0; k < terms; k++)
                {
                    double a = k < want.AsphericCoefficients.Length ? want.AsphericCoefficients[k] : 0.0;
                    double b = k < got.AsphericCoefficients.Length ? got.AsphericCoefficients[k] : 0.0;
                    if (!Same(a, b)) wrong.Add($"surface {i} A{2 * k + 2} {b:R} where the design has {a:R}");
                }
                if (sameFormat && HasGlass(want) != HasGlass(got))
                    wrong.Add($"surface {i} {(HasGlass(got) ? "has glass" : "has no glass")} where the design {(HasGlass(want) ? "has" : "has none")}");
            }
            if (wrong.Count == 0) return;

            throw new InvalidOperationException(
                $"The design was written to {path}, but the file does not read back as the design: "
                + string.Join("; ", wrong.Count > 6 ? wrong.GetRange(0, 6) : wrong)
                + (wrong.Count > 6 ? $"; and {wrong.Count - 6} more" : "")
                + ". Do not use the file.");
        }

        /// <summary>The same number, to what a file can carry: relative, with infinities equal.</summary>
        private static bool Same(double a, double b)
        {
            if (double.IsInfinity(a) || double.IsInfinity(b)) return a == b || Math.Abs(a) >= 1e12 && Math.Abs(b) >= 1e12;
            return Math.Abs(a - b) <= 1e-9 * Math.Max(Math.Abs(a), Math.Abs(b)) + 1e-15;
        }

        private static bool HasGlass(Surface s) => s.ModelIndexEnabled && s.ModelNd > 0.0 || !string.IsNullOrWhiteSpace(s.Material);

        /// <summary>A format by its extension, two extensions that name one format being one.</summary>
        private static string Format(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".osl" ? ".len" : ext == ".opt" ? ".otx" : ext;
        }

        private static string Name(string ext) => ext switch
        {
            ".zmx" => "ZEMAX",
            ".seq" => "CODE V",
            ".len" => "OSLO",
            ".otx" => "OPTALIX",
            ".json" => "Optiland",
            ".lhlt" => "LensHH-LT",
            _ => ext,
        };
    }
}
