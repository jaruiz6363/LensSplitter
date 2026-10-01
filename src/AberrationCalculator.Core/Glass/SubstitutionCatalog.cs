using System;
using System.Collections.Generic;
using System.IO;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// The glasses the optimiser may substitute IN, kept apart from the ones used to read a design.
///
/// <para><b>Why a separate folder.</b> Reading a lens and choosing a glass are different
/// questions. Reading needs every vendor catalogue present, because the file names a glass and
/// the index has to resolve or the whole analysis is quietly wrong. Choosing needs the
/// opposite - a search allowed to pick from every catalogue at once will wander into glasses
/// nobody stocks, and hand back a design that cannot be built. So substitution draws from
/// <c>catalogs/Substitution</c> alone, and the two never mix.</para>
///
/// <para>The set shipped is <c>CoreSet28</c>. Others can be dropped in beside it and
/// named on the command line.</para>
/// </summary>
public static class SubstitutionCatalog
{
    /// <summary>Environment variable that overrides the search, for unusual layouts.</summary>
    public const string OverrideVariable = "ABCALC_SUBSTITUTION_DIR";

    public const string FolderName = "Substitution";

    /// <summary>The folder holding the substitution catalogues, or null if it cannot be found.</summary>
    public static string? Find()
    {
        var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden) && HasCatalogs(overridden!)) return overridden;

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (int up = 0; up < 8 && !string.IsNullOrEmpty(dir); up++)
            {
                string candidate = Path.Combine(dir, "catalogs", FolderName);
                if (HasCatalogs(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
        }
        return null;
    }

    /// <summary>The catalogues available to substitute from, by bare name.</summary>
    public static IReadOnlyList<string> Available()
    {
        string? dir = Find();
        if (dir == null) return Array.Empty<string>();

        var names = new List<string>();
        foreach (string path in Directory.GetFiles(dir, "*.agf"))
            names.Add(Path.GetFileNameWithoutExtension(path));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>
    /// Loads one named substitution catalogue - <c>CoreSet28</c>, say - and nothing else.
    ///
    /// <para>Loading only the one named is the point. A catalog object holding every vendor's
    /// glasses would let the search pick any of them, which is exactly what naming a set is
    /// meant to prevent.</para>
    /// </summary>
    public static GlassCatalog Load(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("a substitution catalogue has to be named", nameof(name));

        string? dir = Find();
        if (dir == null)
            throw new DirectoryNotFoundException(
                "no substitution catalogues were found. They ship in catalogs/" + FolderName
              + "; set " + OverrideVariable + " if they live somewhere else.");

        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + ".AGF");
        if (!File.Exists(path))
        {
            var available = Available();
            throw new FileNotFoundException(
                $"'{name}' is not a substitution catalogue. "
              + (available.Count == 0
                    ? "There are none in " + dir + "."
                    : "Available: " + string.Join(", ", available) + "."),
                path);
        }

        var catalog = new GlassCatalog();
        catalog.LoadFile(path);
        return catalog;
    }

    private static bool HasCatalogs(string folder)
    {
        try
        {
            return Directory.Exists(folder) && Directory.GetFiles(folder, "*.agf").Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
