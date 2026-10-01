using System;
using System.IO;

namespace AberrationCalculator.Core.IO;

/// <summary>
/// The folder a bare file name is taken to mean.
///
/// <para><b>Why this exists at all, given that shells have <c>cd</c>.</b> In a terminal, a
/// relative path already resolves against the working directory and this adds nothing. It earns
/// its keep in two places: across shells, so a base named once on Monday still holds on Tuesday
/// in a window that was never <c>cd</c>'d anywhere; and over MCP, where the server's working
/// directory is whatever the client happened to start it in - not something the user chose, and
/// not something they can change - so without this every path an assistant passes has to be
/// absolute.</para>
///
/// <para><b>Four layers, most specific first:</b> <c>--dir</c> on the command line, then
/// <c>ABCALC_DIR</c> in the environment, then the stored setting, then the process's working
/// directory. Each is easier to change than the one below it, which is the order that lets a
/// stored base be overridden for one run without being un-set.</para>
///
/// <para>An absolute path is never touched. Whatever the base is, <c>C:\elsewhere\L.zmx</c>
/// means what it says - a base that silently re-rooted absolute paths would be a trap rather
/// than a convenience.</para>
/// </summary>
public static class BasePath
{
    /// <summary>Overrides the stored base for one process.</summary>
    public const string OverrideVariable = "ABCALC_DIR";

    /// <summary>
    /// Where the setting itself is kept. Overridable so a test can have its own, and so a
    /// portable install can keep its settings beside itself rather than in a user profile.
    /// </summary>
    public const string HomeVariable = "ABCALC_HOME";

    private static string Home()
    {
        string? overridden = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(overridden)) return overridden!;

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(profile))
            profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "abcalc");
    }

    /// <summary>The file the base path is stored in. Plain text, one line, editable by hand.</summary>
    public static string SettingsFile => Path.Combine(Home(), "base");

    /// <summary>The stored base, or null if none has been set.</summary>
    public static string? Stored()
    {
        string file = SettingsFile;
        if (!File.Exists(file)) return null;

        string text = File.ReadAllText(file).Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Stores a base path. The folder has to exist: a base naming somewhere that does not is a
    /// mistake that would otherwise only show up later, as every file under it failing to be
    /// found.
    /// </summary>
    public static string Store(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("a base path has to be a folder", nameof(path));

        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"no such folder: {full}");

        Directory.CreateDirectory(Home());
        File.WriteAllText(SettingsFile, full + Environment.NewLine);
        return full;
    }

    /// <summary>Forgets the stored base, leaving the working directory in charge again.</summary>
    public static void Clear()
    {
        string file = SettingsFile;
        if (File.Exists(file)) File.Delete(file);
    }

    /// <summary>
    /// The base in force, given an optional <c>--dir</c> for this one invocation.
    /// </summary>
    public static string Current(string? flag = null)
    {
        if (!string.IsNullOrWhiteSpace(flag)) return Path.GetFullPath(flag!);

        string? environment = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(environment)) return Path.GetFullPath(environment!);

        string? stored = Stored();
        if (!string.IsNullOrWhiteSpace(stored)) return Path.GetFullPath(stored!);

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Where the base in force came from, in words. Said out loud whenever the base is shown,
    /// because "why is it looking there?" is the only hard question this feature raises.
    /// </summary>
    public static string Source(string? flag = null)
    {
        if (!string.IsNullOrWhiteSpace(flag)) return "--dir on this command line";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OverrideVariable)))
            return OverrideVariable + " in the environment";
        if (!string.IsNullOrWhiteSpace(Stored()))
            return "the BASE command, kept in " + SettingsFile;
        return "the current directory; no base is set";
    }

    /// <summary>
    /// Makes one path absolute against the base. Absolute paths are returned as they are.
    /// </summary>
    public static string Resolve(string path, string? flag = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(Current(flag), path));
    }

    /// <summary>Resolves a path that may not have been given at all.</summary>
    public static string? ResolveIfGiven(string? path, string? flag = null) =>
        string.IsNullOrWhiteSpace(path) ? path : Resolve(path!, flag);
}
