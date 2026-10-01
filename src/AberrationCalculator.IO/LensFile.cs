using System;
using System.Collections.Generic;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Opens a lens file without the caller having to know which program wrote it.
    ///
    /// Dispatch is by extension, which is how every one of these formats identifies
    /// itself in practice. A file whose extension does not name a supported format is a
    /// clear error rather than a guess: the formats are similar enough textually that
    /// sniffing one for another produces a plausible but wrong lens.
    /// </summary>
    public static class LensFile
    {
        /// <summary>Extensions this program can open, for a file-picker filter.</summary>
        public static readonly string[] SupportedExtensions =
            { ".zmx", ".seq", ".otx", ".opt", ".len", ".osl", ".json", ".lhlt" };

        /// <summary>
        /// Reads <paramref name="path"/> into a system. <paramref name="glass"/> is used
        /// by the formats that need refractive indices to recover an aperture the file
        /// states only indirectly; the others ignore it.
        /// </summary>
        public static OpticalSystem Read(string path, GlassCatalog? glass = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("No file given.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Lens file not found.", path);

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".zmx": return ZmxReader.Read(path, glass);
                case ".seq": return CodeVReader.Read(path, glass);
                case ".otx":
                case ".opt": return OptalixReader.Read(path, glass);
                case ".len":
                case ".osl": return OsloReader.Read(path);
                case ".json": return OptilandReader.Read(path, glass);
                case ".lhlt": return LhltReader.Read(path).System;
                default:
                    throw new NotSupportedException(
                        $"'{Path.GetExtension(path)}' is not a lens format this program reads. " +
                        $"Supported: {string.Join(", ", SupportedExtensions)}");
            }
        }

        /// <summary>Extensions <see cref="Write"/> writes a whole lens as.</summary>
        public static readonly string[] WritableExtensions = { ".zmx", ".seq", ".otx", ".len", ".json", ".lhlt" };

        /// <summary>
        /// Writes <paramref name="system"/> as a whole new lens file, in the format the extension
        /// names. Unlike <see cref="LensPatcher"/>, which edits a lens file in place and keeps what
        /// this program does not model, this needs no original: it is for a lens whose surfaces
        /// have changed in number, or that has no file yet.
        ///
        /// <para>A lens a format cannot carry is refused with the reason, not written as another
        /// lens: an r^2 aspheric term in Code V, OSLO or Optalix, an ideal lens in Code V or
        /// Optiland, a tilt anywhere.</para>
        /// </summary>
        /// <param name="glass">The glass catalogs. Code V, OSLO and Optalix need indices for some
        /// conversions (an F-number at a finite object, a model or private glass); Optiland writes
        /// each glass's dispersion data from them.</param>
        /// <param name="installOptilandGlasses">For Optiland: install the glasses into this
        /// machine's Optiland catalogs as well as beside the lens.</param>
        /// <returns>Notes to show the user; empty when there are none.</returns>
        public static IReadOnlyList<string> Write(OpticalSystem system, string path, GlassCatalog? glass,
                                                  bool installOptilandGlasses = true)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("No file given.", nameof(path));
            var notes = new List<string>();
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".zmx": ZmxWriter.Write(system, path); break;
                case ".seq": CodeVWriter.Write(system, path, glass); break;
                case ".otx": OptalixWriter.Write(system, path, glass); break;
                case ".len": OsloWriter.Write(system, path, glass); break;
                case ".json": notes.Add(OptilandWriter.Write(system, path, glass, installOptilandGlasses).Describe()); break;
                case ".lhlt": LhltWriter.Write(system, path); break;
                default:
                    throw new NotSupportedException(
                        $"'{Path.GetExtension(path)}' is not a lens format this program writes. " +
                        $"Supported: {string.Join(", ", WritableExtensions)}");
            }
            return notes;
        }
    }
}
