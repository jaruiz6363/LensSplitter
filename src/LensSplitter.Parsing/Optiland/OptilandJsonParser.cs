using System.Text.Json;
using LensSplitter.Core.Models;
using LensSplitter.Parsing.Glass;

namespace LensSplitter.Parsing.Optiland;

/// <summary>
/// Parses Optiland JSON optical system files.
/// </summary>
public class OptilandJsonParser
{
    private readonly GlassCatalogManager? _glassCatalog;

    public OptilandJsonParser(GlassCatalogManager? glassCatalog = null)
    {
        _glassCatalog = glassCatalog;
    }

    /// <summary>
    /// Parses an Optiland JSON file and returns an OpticalSystem.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>The parsed optical system.</returns>
    public OpticalSystem Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The JSON path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Unable to locate Optiland JSON file.", path);
        }

        var json = File.ReadAllText(path);
        return ParseJson(json, Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>
    /// Parses Optiland JSON content.
    /// </summary>
    /// <param name="json">The JSON content.</param>
    /// <param name="name">Optional name for the system.</param>
    /// <returns>The parsed optical system.</returns>
    public OpticalSystem ParseJson(string json, string? name = null)
    {
        // Preprocess JSON to handle non-standard Infinity values
        // Python's json library can output Infinity/-Infinity which isn't valid JSON
        json = PreprocessJson(json);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var system = new OpticalSystem
        {
            Name = name ?? "Optiland System"
        };

        // Parse aperture
        if (root.TryGetProperty("aperture", out var aperture))
        {
            ParseAperture(aperture, system);
        }

        // Parse wavelengths
        if (root.TryGetProperty("wavelengths", out var wavelengths))
        {
            ParseWavelengths(wavelengths, system);
        }

        // Parse fields
        if (root.TryGetProperty("fields", out var fields))
        {
            ParseFields(fields, system);
        }

        // Parse surfaces
        if (root.TryGetProperty("surface_group", out var surfaceGroup) &&
            surfaceGroup.TryGetProperty("surfaces", out var surfaces))
        {
            ParseSurfaces(surfaces, system);
        }

        system.EnsureWavelengths();
        system.EnsureFields();

        return system;
    }

    private void ParseAperture(JsonElement aperture, OpticalSystem system)
    {
        if (aperture.TryGetProperty("type", out var typeElem))
        {
            var typeStr = typeElem.GetString()?.ToUpperInvariant();

            // Only EPD is supported - reject other aperture types
            if (typeStr == "EPD")
            {
                system.ApertureType = ApertureType.EntrancePupilDiameter;
            }
            else if (typeStr == "IMAGEFNO" || typeStr == "FNO")
            {
                throw new NotSupportedException(
                    "Aperture type 'Image F/#' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD).");
            }
            else if (typeStr == "NA")
            {
                throw new NotSupportedException(
                    "Aperture type 'Object Space NA' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD).");
            }
            else if (typeStr == "FLOAT" || typeStr == "FLOATBYSTOP")
            {
                throw new NotSupportedException(
                    "Aperture type 'Float by Stop Size' is not supported. " +
                    "Please convert the system to use Entrance Pupil Diameter (EPD).");
            }
            else
            {
                // Unknown type - default to EPD but warn
                system.ApertureType = ApertureType.EntrancePupilDiameter;
            }
        }

        if (aperture.TryGetProperty("value", out var valueElem))
        {
            system.ApertureValue = valueElem.GetDouble();
        }
    }

    private void ParseWavelengths(JsonElement wavelengths, OpticalSystem system)
    {
        if (!wavelengths.TryGetProperty("wavelengths", out var waveArray))
        {
            return;
        }

        foreach (var wave in waveArray.EnumerateArray())
        {
            double value = 0.5876; // Default to d-line
            bool isPrimary = false;
            double weight = 1.0;

            if (wave.TryGetProperty("value", out var valueElem))
            {
                value = valueElem.GetDouble();
            }

            if (wave.TryGetProperty("is_primary", out var primaryElem))
            {
                isPrimary = primaryElem.GetBoolean();
            }

            // Convert to microns if necessary (Optiland uses um)
            if (wave.TryGetProperty("unit", out var unitElem))
            {
                var unit = unitElem.GetString()?.ToLowerInvariant();
                if (unit == "nm")
                {
                    value /= 1000.0;
                }
            }

            system.Wavelengths.Add(new Wavelength(value, isPrimary, weight));
        }
    }

    private void ParseFields(JsonElement fields, OpticalSystem system)
    {
        if (!fields.TryGetProperty("fields", out var fieldArray))
        {
            return;
        }

        var fieldType = FieldType.Angle;
        if (fields.TryGetProperty("field_type", out var typeElem))
        {
            var typeStr = typeElem.GetString()?.ToLowerInvariant();
            fieldType = typeStr switch
            {
                "angle" => FieldType.Angle,
                "object_height" => FieldType.ObjectHeight,
                "paraxial_image_height" => FieldType.ParaxialImageHeight,
                "image_height" => FieldType.ImageHeight, // Real image height
                _ => FieldType.Angle
            };
        }

        foreach (var field in fieldArray.EnumerateArray())
        {
            double x = 0, y = 0;
            double weight = 1.0;

            if (field.TryGetProperty("x", out var xElem))
            {
                x = xElem.GetDouble();
            }

            if (field.TryGetProperty("y", out var yElem))
            {
                y = yElem.GetDouble();
            }

            system.Fields.Add(new Field(x, y, fieldType, weight));
        }
    }

    private void ParseSurfaces(JsonElement surfaces, OpticalSystem system)
    {
        int index = 0;
        double? previousZ = null;

        foreach (var surfaceElem in surfaces.EnumerateArray())
        {
            var surface = new Surface { Index = index };

            // Get surface type
            if (surfaceElem.TryGetProperty("type", out var typeElem))
            {
                var typeStr = typeElem.GetString();
                surface.SurfaceType = typeStr switch
                {
                    "ObjectSurface" => SurfaceType.Object,
                    "ImageSurface" => SurfaceType.Image,
                    _ => SurfaceType.Standard
                };
            }

            // Parse geometry
            if (surfaceElem.TryGetProperty("geometry", out var geometry))
            {
                // Get radius
                if (geometry.TryGetProperty("radius", out var radiusElem))
                {
                    var radius = ParseInfinityDouble(radiusElem);
                    surface.Radius = radius;
                }

                // Get conic
                if (geometry.TryGetProperty("conic", out var conicElem))
                {
                    surface.Conic = conicElem.GetDouble();
                }

                // Get z position from coordinate system
                if (geometry.TryGetProperty("cs", out var cs) &&
                    cs.TryGetProperty("z", out var zElem))
                {
                    var z = ParseInfinityDouble(zElem);
                    surface.AbsoluteZ = z;

                    // Calculate thickness from previous surface
                    if (previousZ.HasValue && !double.IsInfinity(previousZ.Value) && !double.IsInfinity(z))
                    {
                        // Set thickness on previous surface
                        if (index > 0 && system.Surfaces.Count > 0)
                        {
                            system.Surfaces[index - 1].Thickness = z - previousZ.Value;
                        }
                    }

                    previousZ = z;
                }
            }

            // Parse material (post-surface material is the glass after this surface)
            if (surfaceElem.TryGetProperty("material_post", out var materialPost))
            {
                ParseMaterial(materialPost, surface);
            }

            // Check if stop
            if (surfaceElem.TryGetProperty("is_stop", out var isStopElem))
            {
                surface.IsStop = isStopElem.GetBoolean();
            }

            system.Surfaces.Add(surface);
            index++;
        }
    }

    private void ParseMaterial(JsonElement material, Surface surface)
    {
        if (material.TryGetProperty("type", out var typeElem))
        {
            var typeStr = typeElem.GetString();

            if (typeStr == "IdealMaterial")
            {
                // Ideal material with constant index
                if (material.TryGetProperty("index", out var indexElem))
                {
                    var n = indexElem.GetDouble();
                    if (Math.Abs(n - 1.0) < 0.001)
                    {
                        // Air
                        surface.Glass = null;
                        surface.GlassName = null;
                    }
                    else
                    {
                        surface.Glass = Core.Models.Glass.Ideal(n);
                        surface.GlassName = surface.Glass.Name;
                    }
                }
            }
            else if (typeStr == "Material")
            {
                // Named material - try to get from catalog
                if (material.TryGetProperty("name", out var nameElem))
                {
                    var glassName = nameElem.GetString();
                    surface.GlassName = glassName;

                    // Get catalog reference hint (e.g., "schott", "ohara")
                    string? catalogHint = null;
                    if (material.TryGetProperty("reference", out var refElem) &&
                        refElem.ValueKind == JsonValueKind.String)
                    {
                        catalogHint = refElem.GetString();
                    }

                    // Look up glass with catalog preference
                    surface.Glass = ResolveGlass(glassName, catalogHint);

                    if (surface.Glass == null && glassName != null)
                    {
                        // Create placeholder
                        surface.Glass = Core.Models.Glass.Ideal(1.5);
                        surface.Glass.Name = glassName;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves a glass by name, trying the preferred catalog first (from reference field).
    /// </summary>
    private Core.Models.Glass? ResolveGlass(string? glassName, string? catalogHint)
    {
        if (_glassCatalog == null || string.IsNullOrEmpty(glassName))
            return null;

        // Handle air/empty
        if (glassName.Equals("AIR", StringComparison.OrdinalIgnoreCase))
            return Core.Models.Glass.Air;

        // If we have a catalog hint, try that catalog first
        if (!string.IsNullOrEmpty(catalogHint))
        {
            // Try to match catalog name (case-insensitive, partial match)
            foreach (var loadedCatalog in _glassCatalog.LoadedCatalogs)
            {
                if (loadedCatalog.Contains(catalogHint, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Preprocesses JSON to handle non-standard values like Infinity and NaN.
    /// These are valid in Python but not in standard JSON.
    /// </summary>
    private static string PreprocessJson(string json)
    {
        // Replace unquoted Infinity/-Infinity/NaN with quoted strings
        // Use regex to avoid replacing inside strings
        // Pattern matches: number context (after : or , or [ with optional whitespace)

        // Replace -Infinity (must be before Infinity to avoid partial match)
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(?<=[:\[,\s])-Infinity(?=[,\]\}\s]|$)",
            "\"-Infinity\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace +Infinity
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(?<=[:\[,\s])\+Infinity(?=[,\]\}\s]|$)",
            "\"Infinity\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace Infinity (without sign)
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(?<=[:\[,\s])Infinity(?=[,\]\}\s]|$)",
            "\"Infinity\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace NaN
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(?<=[:\[,\s])NaN(?=[,\]\}\s]|$)",
            "\"NaN\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return json;
    }

    private static double ParseInfinityDouble(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number)
        {
            return elem.GetDouble();
        }

        if (elem.ValueKind == JsonValueKind.String)
        {
            var str = elem.GetString()?.Trim();
            var strLower = str?.ToLowerInvariant();

            if (strLower == "infinity" || strLower == "inf" || strLower == "+infinity" || strLower == "+inf")
            {
                return double.PositiveInfinity;
            }
            if (strLower == "-infinity" || strLower == "-inf")
            {
                return double.NegativeInfinity;
            }
            if (strLower == "nan")
            {
                return double.NaN;
            }
            if (double.TryParse(str, out var result))
            {
                return result;
            }
        }

        // Handle JSON literal Infinity (non-standard) - fallback
        var raw = elem.GetRawText();
        if (raw.Contains("Infinity", StringComparison.OrdinalIgnoreCase))
        {
            return raw.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
        }

        return double.PositiveInfinity;
    }
}
