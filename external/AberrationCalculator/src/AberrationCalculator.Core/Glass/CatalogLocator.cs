using System;
using System.Collections.Generic;
using System.IO;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Finds the bundled glass catalogs without being told where they are.
///
/// The catalogs ship with the program, so needing to point at a folder before a lens can
/// be read is a setup step that should not exist - and getting it wrong is not a visible
/// failure. Every glass resolves as air, every index reads 1.0, and the program returns a
/// full set of confident, wrong numbers. That failure mode is the reason this looks in
/// several places rather than one.
/// </summary>
public static class CatalogLocator
{
    /// <summary>Environment variable that overrides the search, for unusual layouts.</summary>
    public const string OverrideVariable = "ABCALC_GLASS_DIR";

    /// <summary>
    /// Returns the first folder that holds AGF catalogs, or null if none is found.
    ///
    /// Order: an explicit override, then beside the executable (how a published build is
    /// laid out), then walking up from the executable and from the working directory (how
    /// it looks when run from a source tree, where the binary sits several levels below
    /// the repository root).
    /// </summary>
    public static string? Find()
    {
        var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden) && HasCatalogs(overridden!)) return overridden;

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (int up = 0; up < 8 && !string.IsNullOrEmpty(dir); up++)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(dir, "catalogs", "Glass"),
                             Path.Combine(dir, "catalogs"),
                             Path.Combine(dir, "Glass"),
                         })
                {
                    if (HasCatalogs(candidate)) return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }

        return null;
    }

    /// <summary>Every folder searched, in order - for an error message that can be acted on.</summary>
    public static IReadOnlyList<string> SearchedPlaces()
    {
        var seen = new List<string>();
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (int up = 0; up < 8 && !string.IsNullOrEmpty(dir); up++)
            {
                seen.Add(Path.Combine(dir, "catalogs", "Glass"));
                dir = Path.GetDirectoryName(dir);
            }
        }
        return seen;
    }

    private static bool HasCatalogs(string dir)
    {
        try
        {
            return Directory.Exists(dir) && Directory.GetFiles(dir, "*.agf").Length > 0;
        }
        catch
        {
            return false;   // unreadable path is simply not a match
        }
    }

    /// <summary>
    /// Loads the bundled catalogs into a new <see cref="GlassCatalog"/>. Throws when none
    /// are found: continuing without them would silently treat every glass as air.
    /// </summary>
    public static GlassCatalog LoadBundled()
    {
        var dir = Find();
        if (dir == null)
        {
            throw new DirectoryNotFoundException(
                "No glass catalogs found. They normally ship in 'catalogs/Glass' beside the program. "
                + $"Set {OverrideVariable} to a folder of .agf files to override. Looked in:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", SearchedPlaces()));
        }

        var catalog = new GlassCatalog();
        catalog.LoadFolder(dir);

        // The user's own catalogs: glasses brought in with a lens (OpticStudio table glasses,
        // catalogs a .zmx named that this program does not ship), so a lens that needed one
        // opens with it again. After the shipped catalogs, which they never replace.
        try { catalog.LoadFolder(UserFolder); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return catalog;
    }

    /// <summary>The user's own glass folder: Documents\AberrationCalculator\Glass.</summary>
    public static string UserFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AberrationCalculator", "Glass");
}
