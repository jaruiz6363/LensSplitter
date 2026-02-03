using LensSplitter.Core.Models;
using LensSplitter.Parsing.Glass;
using LensSplitter.Parsing.Optiland;
using LensSplitter.Parsing.Zmx;

namespace LensSplitter.Parsing;

/// <summary>
/// Unified loader for optical system files (ZMX, Optiland JSON).
/// </summary>
public class OpticalSystemLoader
{
    private readonly GlassCatalogManager _glassCatalog;
    private readonly ZmxParser _zmxParser;
    private readonly OptilandJsonParser _optilandParser;

    public OpticalSystemLoader(GlassCatalogManager? glassCatalog = null, bool autoLoadCatalogs = true)
    {
        _glassCatalog = glassCatalog ?? new GlassCatalogManager();

        // Automatically load default catalogs if none provided
        if (glassCatalog == null && autoLoadCatalogs)
        {
            _glassCatalog.LoadDefaultCatalogs();
        }

        _zmxParser = new ZmxParser(_glassCatalog);
        _optilandParser = new OptilandJsonParser(_glassCatalog);
    }

    /// <summary>
    /// Gets the glass catalog manager.
    /// </summary>
    public GlassCatalogManager GlassCatalog => _glassCatalog;

    /// <summary>
    /// Loads an optical system from a file.
    /// The file type is determined by extension.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>The loaded optical system.</returns>
    public OpticalSystem Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".zmx" => _zmxParser.Parse(path),
            ".json" => _optilandParser.Parse(path),
            _ => throw new NotSupportedException($"File type '{extension}' is not supported. Use .zmx or .json files.")
        };
    }

    /// <summary>
    /// Loads an optical system from JSON content.
    /// </summary>
    /// <param name="json">The JSON content.</param>
    /// <param name="name">Optional name for the system.</param>
    /// <returns>The loaded optical system.</returns>
    public OpticalSystem LoadFromJson(string json, string? name = null)
    {
        return _optilandParser.ParseJson(json, name);
    }

    /// <summary>
    /// Determines the file type from the path.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>The detected file type.</returns>
    public static FileType GetFileType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".zmx" => FileType.Zmx,
            ".json" => FileType.OptilandJson,
            ".agf" => FileType.Agf,
            _ => FileType.Unknown
        };
    }
}

/// <summary>
/// Supported file types.
/// </summary>
public enum FileType
{
    Unknown,
    Zmx,
    OptilandJson,
    Agf
}
