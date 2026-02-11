using System.Globalization;
using System.Linq;
using System.Text;
using LensSplitter.Core.Models;
using LensSplitter.Parsing.Glass;

namespace LensSplitter.Parsing.Zmx;

/// <summary>
/// Parses ZEMAX ZMX lens files.
/// </summary>
public class ZmxParser
{
    private const double Epsilon = 1e-12;
    private readonly GlassCatalogManager? _glassCatalog;

    public ZmxParser(GlassCatalogManager? glassCatalog = null)
    {
        _glassCatalog = glassCatalog;
    }

    /// <summary>
    /// Parses a ZMX file and returns an OpticalSystem.
    /// </summary>
    /// <param name="path">Path to the ZMX file.</param>
    /// <returns>The parsed optical system.</returns>
    public OpticalSystem Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The ZMX path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Unable to locate ZMX file.", path);
        }

        var system = new OpticalSystem
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Notes = $"Imported from ZMX: {Path.GetFileName(path)}"
        };

        ParseInternal(path, system);

        // Ensure minimum valid system
        if (system.Surfaces.Count == 0)
        {
            system.Surfaces.Add(new Surface { Index = 0, SurfaceType = SurfaceType.Object });
            system.Surfaces.Add(new Surface { Index = 1, SurfaceType = SurfaceType.Image });
        }

        system.EnsureWavelengths();
        system.EnsureFields();

        // Resolve glass references
        ResolveGlasses(system);

        return system;
    }

    private List<string> _preferredGlassCatalogs = new(); // Stores GCAT catalog list from ZMX file

    private void ParseInternal(string path, OpticalSystem system)
    {
        Surface? currentSurface = null;
        var surfaceLookup = new Dictionary<int, Surface>();
        int fieldIndex = 0;
        int stopSurface = -1;
        int primaryWavelengthIndex = 1; // ZEMAX uses 1-based indexing, default is 1
        int numWavelengths = 24; // Default max, will be updated from FTYP
        FieldType fieldType = FieldType.Angle; // Default to angle
        var wavelengthsWithIndex = new List<(int index, double wavelength, double weight)>();
        _preferredGlassCatalogs.Clear();

        // ZMX files are often UTF-16 LE encoded
        var lines = ReadZmxFile(path);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("!"))
            {
                continue;
            }

            // Surface definition
            if (line.StartsWith("SURF", StringComparison.OrdinalIgnoreCase))
            {
                currentSurface = CreateSurface(line, system, surfaceLookup);
                continue;
            }

            // Surface type
            if (line.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                var typeToken = line.Length > 4 ? line.Substring(4).Trim() : string.Empty;
                currentSurface.SurfaceType = ParseSurfaceType(typeToken);
                continue;
            }

            // Curvature (CURV)
            if (line.StartsWith("CURV", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    currentSurface.Radius = Math.Abs(value.Value) < Epsilon
                        ? double.PositiveInfinity
                        : 1.0 / value.Value;
                }
                continue;
            }

            // Thickness (THIC or DISZ)
            if ((line.StartsWith("THIC", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("DISZ", StringComparison.OrdinalIgnoreCase)) && currentSurface != null)
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    currentSurface.Thickness = value.Value;
                }
                continue;
            }

            // Conic constant
            if (line.StartsWith("CONI", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    currentSurface.Conic = value.Value;
                }
                continue;
            }

            // Glass
            if (line.StartsWith("GLAS", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                var tokens = SplitTokens(line);
                if (tokens.Length > 1)
                {
                    var glassName = tokens[1];
                    if (glassName.Equals("AIR", StringComparison.OrdinalIgnoreCase) ||
                        glassName.Equals("___", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSurface.GlassName = null;
                        currentSurface.Glass = null;
                    }
                    else if (glassName.StartsWith("___", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 5)
                    {
                        // Model glass (e.g. ___BLANK): GLAS name status mil_number Nd Vd ...
                        // Create ideal glass directly from Nd value
                        if (double.TryParse(tokens[4], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double nd) && nd > 1.0)
                        {
                            currentSurface.GlassName = $"MODEL_{nd:F6}";
                            currentSurface.Glass = Core.Models.Glass.Ideal(nd);
                            currentSurface.Glass.Name = $"MODEL_{nd:F6}";
                        }
                        else
                        {
                            currentSurface.GlassName = glassName;
                        }
                    }
                    else
                    {
                        currentSurface.GlassName = glassName;
                    }
                }
                continue;
            }

            // Semi-diameter (DIAM)
            if (line.StartsWith("DIAM", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    currentSurface.SemiDiameter = value.Value;
                }
                continue;
            }

            // Marginal ray height solve (MAZH)
            if (line.StartsWith("MAZH", StringComparison.OrdinalIgnoreCase) && currentSurface != null)
            {
                currentSurface.HasMarginalRayHeightSolve = true;
                // Store the parameters (everything after "MAZH ")
                var tokens = SplitTokens(line);
                if (tokens.Length > 1)
                {
                    currentSurface.MarginalRayHeightSolveParams = string.Join(" ", tokens.Skip(1));
                }
                else
                {
                    currentSurface.MarginalRayHeightSolveParams = "0 0";
                }
                continue;
            }

            // Stop surface - in ZEMAX format, "STOP" appears within a surface block
            // to indicate that surface is the aperture stop
            if (line.StartsWith("STOP", StringComparison.OrdinalIgnoreCase))
            {
                if (currentSurface != null)
                {
                    // STOP within a surface block - mark current surface as stop
                    currentSurface.IsStop = true;
                }
                else
                {
                    // STOP with a surface number (less common format)
                    var value = ExtractNumeric(line);
                    if (value.HasValue)
                    {
                        stopSurface = (int)value.Value;
                    }
                }
                continue;
            }

            // Primary wavelength index (PWAV)
            if (line.StartsWith("PWAV", StringComparison.OrdinalIgnoreCase))
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    primaryWavelengthIndex = (int)value.Value;
                }
                continue;
            }

            // Wavelength (WAVM or WAVE)
            if (line.StartsWith("WAVM", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("WAVE", StringComparison.OrdinalIgnoreCase))
            {
                ParseWavelengthWithIndex(line, wavelengthsWithIndex);
                continue;
            }

            // Field type definition (also contains number of wavelengths)
            // FTYP format: FTYP fieldtype normalization numfields numwaves ...
            if (line.StartsWith("FTYP", StringComparison.OrdinalIgnoreCase))
            {
                fieldType = ParseFieldType(line);
                // Extract number of wavelengths from token index 4 (0-based index 3)
                var ftypTokens = SplitTokens(line);
                if (ftypTokens.Length > 4 && int.TryParse(ftypTokens[4], out int nwav))
                {
                    numWavelengths = nwav;
                }
                continue;
            }

            if (line.StartsWith("XFLN", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("YFLN", StringComparison.OrdinalIgnoreCase))
            {
                ParseFieldXY(line, system, fieldType, ref fieldIndex);
                continue;
            }

            // Entrance pupil diameter (only supported aperture type)
            if (line.StartsWith("ENPD", StringComparison.OrdinalIgnoreCase))
            {
                var value = ExtractNumeric(line);
                if (value.HasValue)
                {
                    system.ApertureType = ApertureType.EntrancePupilDiameter;
                    system.ApertureValue = value.Value;
                }
                continue;
            }

            // Unsupported aperture types - detect and reject
            if (line.StartsWith("FLOA", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Aperture type 'Float by Stop Size' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD) in ZEMAX.");
            }
            if (line.StartsWith("OBNA", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Aperture type 'Object Space NA' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD) in ZEMAX.");
            }
            if (line.StartsWith("FNUM", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("IMNA", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Aperture type 'Image F/#' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD) in ZEMAX.");
            }

            // Unit specification - UNIT lens_unit source_unit source_prefix analysis_unit ...
            // We only support MM (millimeters) for lens units
            if (line.StartsWith("UNIT", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = SplitTokens(line);
                if (tokens.Length >= 2)
                {
                    var lensUnit = tokens[1].ToUpperInvariant();
                    if (lensUnit != "MM")
                    {
                        string unitName = lensUnit switch
                        {
                            "CM" => "Centimeters",
                            "IN" => "Inches",
                            "M" => "Meters",
                            _ => lensUnit
                        };
                        throw new InvalidOperationException(
                            $"Unsupported lens unit: {unitName}. " +
                            $"LensSplitter only supports millimeters (MM). " +
                            $"Please change the lens units in your optical design software to millimeters.");
                    }
                }
                continue;
            }

            // Glass catalog specification - GCAT catalogname1 catalogname2 ...
            // This tells us which catalog(s) to prefer when looking up glasses (in order)
            if (line.StartsWith("GCAT", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = SplitTokens(line);
                // All tokens after GCAT are catalog names
                for (int i = 1; i < tokens.Length; i++)
                {
                    var catalog = tokens[i].Trim();
                    if (!string.IsNullOrEmpty(catalog))
                    {
                        _preferredGlassCatalogs.Add(catalog);
                    }
                }
                continue;
            }
        }

        // Sort surfaces and set stop
        system.Surfaces.Sort((a, b) => a.Index.CompareTo(b.Index));

        if (stopSurface >= 0 && stopSurface < system.Surfaces.Count)
        {
            system.Surfaces[stopSurface].IsStop = true;
        }

        // Process wavelengths: only keep active ones and set primary
        ProcessWavelengths(system, wavelengthsWithIndex, numWavelengths, primaryWavelengthIndex);
    }

    private void ProcessWavelengths(OpticalSystem system,
        List<(int index, double wavelength, double weight)> wavelengthsWithIndex,
        int numWavelengths, int primaryWavelengthIndex)
    {
        // Sort by index
        wavelengthsWithIndex.Sort((a, b) => a.index.CompareTo(b.index));

        // Only keep the first numWavelengths
        var activeWavelengths = wavelengthsWithIndex.Take(numWavelengths).ToList();

        // Add to system with correct primary flag
        foreach (var (index, wavelength, weight) in activeWavelengths)
        {
            bool isPrimary = index == primaryWavelengthIndex;
            system.Wavelengths.Add(new Wavelength(wavelength, isPrimary, weight));
        }
    }

    private IEnumerable<string> ReadZmxFile(string path)
    {
        // Try UTF-16 LE first (most common for ZMX)
        try
        {
            using var reader = new StreamReader(path, Encoding.Unicode);
            var content = reader.ReadToEnd();
            if (content.Contains("SURF") || content.Contains("surf"))
            {
                return content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
        catch { }

        // Try UTF-8
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            var content = reader.ReadToEnd();
            if (content.Contains("SURF") || content.Contains("surf"))
            {
                return content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
        catch { }

        // Fall back to default encoding
        return File.ReadAllLines(path);
    }

    private Surface CreateSurface(string line, OpticalSystem system, Dictionary<int, Surface> lookup)
    {
        var tokens = SplitTokens(line);
        int index = system.Surfaces.Count;

        if (tokens.Length > 1 && int.TryParse(tokens[1], out int parsed))
        {
            index = parsed;
        }

        if (lookup.TryGetValue(index, out var existing))
        {
            return existing;
        }

        var surface = new Surface
        {
            Index = index,
            SurfaceType = index == 0 ? SurfaceType.Object : SurfaceType.Standard,
            Radius = double.PositiveInfinity,
            Thickness = 0,
            SemiDiameter = 0,
            Conic = 0
        };

        system.Surfaces.Add(surface);
        lookup[index] = surface;
        return surface;
    }

    private void ParseWavelengthWithIndex(string line, List<(int index, double wavelength, double weight)> wavelengths)
    {
        var tokens = SplitTokens(line);
        if (tokens.Length < 3) return;

        // WAVM format: WAVM index wavelength_um weight
        if (!int.TryParse(tokens[1], out int waveIndex))
        {
            return;
        }

        if (!double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double wavelength))
        {
            return;
        }

        // Skip if wavelength is 0 (placeholder)
        if (wavelength < 0.001) return;

        double weight = 1.0;
        if (tokens.Length > 3)
        {
            double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out weight);
        }

        wavelengths.Add((waveIndex, wavelength, weight));
    }

    private void ParseFieldXY(string line, OpticalSystem system, FieldType fieldType, ref int fieldIndex)
    {
        var tokens = SplitTokens(line);
        if (tokens.Length < 2) return;

        bool isX = line.StartsWith("XFLN", StringComparison.OrdinalIgnoreCase);

        for (int i = 1; i < tokens.Length; i++)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                continue;
            }

            int fieldIdx = i - 1;

            // Ensure we have enough fields
            while (system.Fields.Count <= fieldIdx)
            {
                system.Fields.Add(new Field(0, 0, fieldType));
            }

            var field = system.Fields[fieldIdx];
            field.FieldType = fieldType;

            if (isX)
            {
                field.X = value;
            }
            else
            {
                field.Y = value;
            }
        }
    }

    /// <summary>
    /// Parses the FTYP line to determine field type.
    /// ZEMAX field types: 0=Angle, 1=Object Height, 2=Paraxial Image Height, 3=Real Image Height
    /// </summary>
    private static FieldType ParseFieldType(string line)
    {
        var tokens = SplitTokens(line);
        if (tokens.Length < 2) return FieldType.Angle;

        if (!int.TryParse(tokens[1], out int ftypCode))
        {
            return FieldType.Angle;
        }

        return ftypCode switch
        {
            0 => FieldType.Angle,
            1 => FieldType.ObjectHeight,
            2 => FieldType.ParaxialImageHeight,
            3 => FieldType.ImageHeight, // Real image height - map to ImageHeight
            _ => FieldType.Angle
        };
    }

    private void ResolveGlasses(OpticalSystem system)
    {
        var missingGlasses = new List<string>();

        foreach (var surface in system.Surfaces)
        {
            if (!string.IsNullOrEmpty(surface.GlassName))
            {
                // Skip surfaces that already have glass resolved (e.g. model glasses)
                if (surface.Glass != null)
                    continue;

                surface.Glass = ResolveGlass(surface.GlassName);

                if (surface.Glass == null)
                {
                    missingGlasses.Add($"Surface {surface.Index}: '{surface.GlassName}'");
                }
            }
        }

        if (missingGlasses.Count > 0)
        {
            var catalogInfo = _glassCatalog != null
                ? $"Loaded catalogs: {(_glassCatalog.LoadedCatalogs.Count > 0 ? string.Join(", ", _glassCatalog.LoadedCatalogs) : "NONE")}"
                : "No glass catalog loaded";

            var preferredInfo = _preferredGlassCatalogs.Count > 0
                ? $"File specifies catalogs: {string.Join(", ", _preferredGlassCatalogs)}"
                : "No preferred catalogs specified in file";

            throw new InvalidOperationException(
                $"Glass not found in catalog for the following surfaces:\n" +
                $"  {string.Join("\n  ", missingGlasses)}\n\n" +
                $"{catalogInfo}\n" +
                $"{preferredInfo}\n\n" +
                $"Please load glass catalogs (.agf files) before parsing the lens file.\n" +
                $"Example: Place .agf files in the 'catalogs' folder next to the executable,\n" +
                $"or specify a catalog folder path.");
        }
    }

    /// <summary>
    /// Resolves a glass by name, trying preferred catalogs first (from GCAT line).
    /// </summary>
    private Core.Models.Glass? ResolveGlass(string glassName)
    {
        if (_glassCatalog == null || string.IsNullOrEmpty(glassName))
            return null;

        // Handle air/empty
        if (glassName.Equals("AIR", StringComparison.OrdinalIgnoreCase))
            return Core.Models.Glass.Air;

        // Try preferred catalogs first (in order specified by GCAT)
        foreach (var catalog in _preferredGlassCatalogs)
        {
            // Try to match catalog name (case-insensitive, partial match)
            foreach (var loadedCatalog in _glassCatalog.LoadedCatalogs)
            {
                if (loadedCatalog.Contains(catalog, StringComparison.OrdinalIgnoreCase))
                {
                    var glass = _glassCatalog.GetGlass($"{loadedCatalog}:{glassName}");
                    if (glass != null)
                        return glass;
                }
            }
        }

        // Fall back to generic lookup (first match from any catalog)
        return _glassCatalog.GetGlass(glassName);
    }

    private static SurfaceType ParseSurfaceType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return SurfaceType.Standard;

        var normalized = raw.Trim().Replace("-", "").Replace("_", "").Replace(" ", "").ToUpperInvariant();

        return normalized switch
        {
            "STANDARD" or "SPHERICAL" => SurfaceType.Standard,
            "EVENASPHERE" or "EVENASPH" => SurfaceType.EvenAsphere,
            "COORDBRK" or "COORDINATEBREAK" => SurfaceType.CoordinateBreak,
            "PARAXIAL" => SurfaceType.Paraxial,
            _ => SurfaceType.Standard
        };
    }

    private static string[] SplitTokens(string line)
    {
        return line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static double? ExtractNumeric(string line)
    {
        var tokens = SplitTokens(line);
        if (tokens.Length < 2) return null;

        if (double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        return null;
    }
}
