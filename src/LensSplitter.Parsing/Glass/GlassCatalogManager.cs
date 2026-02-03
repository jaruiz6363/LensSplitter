using LensSplitter.Core.Models;

namespace LensSplitter.Parsing.Glass;

/// <summary>
/// Manages glass catalogs and provides glass lookup functionality.
/// </summary>
public class GlassCatalogManager
{
    private readonly Dictionary<string, Core.Models.Glass> _glasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadedCatalogs = new();
    private readonly AgfParser _parser = new();

    /// <summary>
    /// Gets the default catalog folder relative to the executable.
    /// </summary>
    public static string DefaultCatalogFolder
    {
        get
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "catalogs");
        }
    }

    /// <summary>
    /// Gets the list of loaded catalog names.
    /// </summary>
    public IReadOnlyList<string> LoadedCatalogs => _loadedCatalogs.AsReadOnly();

    /// <summary>
    /// Gets the total number of glasses loaded.
    /// </summary>
    public int GlassCount => _glasses.Count;

    /// <summary>
    /// Loads all AGF files from the default catalog folder.
    /// </summary>
    public void LoadDefaultCatalogs()
    {
        if (Directory.Exists(DefaultCatalogFolder))
        {
            LoadCatalogsFromFolder(DefaultCatalogFolder);
        }
    }

    /// <summary>
    /// Loads all AGF files from a folder.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing AGF files.</param>
    public void LoadCatalogsFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var file in Directory.GetFiles(folderPath, "*.agf", SearchOption.TopDirectoryOnly))
        {
            LoadCatalog(file);
        }
    }

    /// <summary>
    /// Loads a single AGF catalog file.
    /// </summary>
    /// <param name="path">Path to the AGF file.</param>
    /// <returns>The number of glasses loaded from this file.</returns>
    public int LoadCatalog(string path)
    {
        if (!File.Exists(path)) return 0;

        try
        {
            var glasses = _parser.Parse(path);
            var catalogName = Path.GetFileNameWithoutExtension(path);

            foreach (var kvp in glasses)
            {
                // Use catalog:name as key to avoid conflicts
                var fullKey = $"{catalogName}:{kvp.Key}";
                _glasses[fullKey] = kvp.Value;

                // Also add with just the name for simple lookup
                if (!_glasses.ContainsKey(kvp.Key))
                {
                    _glasses[kvp.Key] = kvp.Value;
                }
            }

            if (!_loadedCatalogs.Contains(catalogName, StringComparer.OrdinalIgnoreCase))
            {
                _loadedCatalogs.Add(catalogName);
            }

            return glasses.Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a glass by name.
    /// </summary>
    /// <param name="name">The glass name (optionally with catalog prefix like "SCHOTT:N-BK7").</param>
    /// <returns>The glass, or null if not found.</returns>
    public Core.Models.Glass? GetGlass(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // Handle air/empty
        if (name.Equals("AIR", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(name))
        {
            return Core.Models.Glass.Air;
        }

        // Try exact match first
        if (_glasses.TryGetValue(name, out var glass))
        {
            return glass;
        }

        // Try without catalog prefix
        if (name.Contains(':'))
        {
            var justName = name.Split(':').Last();
            if (_glasses.TryGetValue(justName, out glass))
            {
                return glass;
            }
        }

        // Try searching all catalogs
        foreach (var catalog in _loadedCatalogs)
        {
            var fullKey = $"{catalog}:{name}";
            if (_glasses.TryGetValue(fullKey, out glass))
            {
                return glass;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a glass by name, returning Air if not found.
    /// </summary>
    /// <param name="name">The glass name.</param>
    /// <returns>The glass, or Air if not found.</returns>
    public Core.Models.Glass GetGlassOrAir(string? name)
    {
        return GetGlass(name) ?? Core.Models.Glass.Air;
    }

    /// <summary>
    /// Checks if a glass exists in the loaded catalogs.
    /// </summary>
    /// <param name="name">The glass name.</param>
    /// <returns>True if the glass exists.</returns>
    public bool HasGlass(string name)
    {
        return GetGlass(name) != null;
    }

    /// <summary>
    /// Gets all glasses from a specific catalog.
    /// </summary>
    /// <param name="catalogName">The catalog name.</param>
    /// <returns>List of glasses from that catalog.</returns>
    public IEnumerable<Core.Models.Glass> GetGlassesFromCatalog(string catalogName)
    {
        return _glasses.Values.Where(g => g.Catalog.Equals(catalogName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears all loaded glasses and catalogs.
    /// </summary>
    public void Clear()
    {
        _glasses.Clear();
        _loadedCatalogs.Clear();
    }

    /// <summary>
    /// Adds a glass manually (not from a catalog file).
    /// </summary>
    /// <param name="glass">The glass to add.</param>
    public void AddGlass(Core.Models.Glass glass)
    {
        if (!string.IsNullOrEmpty(glass.Name))
        {
            _glasses[glass.Name] = glass;
            if (!string.IsNullOrEmpty(glass.Catalog))
            {
                _glasses[$"{glass.Catalog}:{glass.Name}"] = glass;
            }
        }
    }
}
