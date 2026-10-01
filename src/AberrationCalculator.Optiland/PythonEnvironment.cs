using System;
using System.IO;

namespace AberrationCalculator.Optiland;

/// <summary>
/// Finds the embedded Python that <c>tools/setup-python.ps1</c> installs Optiland into.
///
/// <para>The layout and the lookup are taken from RelativeIlluminationCalculator: an embeddable Python unpacked into <c>python-embed/</c> with
/// pip bootstrapped and optiland installed, found by walking up from the assembly or the working
/// directory.</para>
/// </summary>
public static class PythonEnvironment
{
    /// <summary>Environment variable that overrides the search.</summary>
    public const string OverrideVariable = "ABCALC_PYTHON_HOME";

    /// <summary>The embedded Python directory, whether or not it exists.</summary>
    public static string Home
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrWhiteSpace(overridden)) return Path.GetFullPath(overridden!);

            string assemblyDir = Path.GetDirectoryName(typeof(PythonEnvironment).Assembly.Location) ?? ".";
            return FindUpward(assemblyDir) ?? FindUpward(Directory.GetCurrentDirectory())
                   ?? Path.Combine(assemblyDir, "python-embed");
        }
    }

    private static string? FindUpward(string startDir)
    {
        string? dir = startDir;
        for (int up = 0; up < 10 && !string.IsNullOrEmpty(dir); up++)
        {
            string candidate = Path.Combine(dir!, "python-embed");
            if (File.Exists(Path.Combine(candidate, "python.exe"))
                || File.Exists(Path.Combine(candidate, "bin", "python3")))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>The versioned Python shared library, which Python.NET has to be told about.</summary>
    public static string PythonDll
    {
        get
        {
            string home = Home;
            if (Directory.Exists(home))
            {
                // python312.dll, not python3.dll: the latter is the stable-ABI forwarder.
                foreach (string dll in Directory.GetFiles(home, "python3*.dll"))
                    if (Path.GetFileNameWithoutExtension(dll).Length > "python3".Length)
                        return dll;
                string lib = Path.Combine(home, "lib");
                if (Directory.Exists(lib))
                    foreach (string so in Directory.GetFiles(lib, "libpython3*.so*"))
                        return so;
            }
            return Path.Combine(home, "python312.dll");
        }
    }

    /// <summary>Whether the environment exists and has optiland in it.</summary>
    public static bool IsReady
    {
        get
        {
            string home = Home;
            if (!File.Exists(Path.Combine(home, "python.exe")) && !File.Exists(Path.Combine(home, "bin", "python3")))
                return false;
            foreach (string site in new[] { Path.Combine(home, "Lib", "site-packages"),
                                            Path.Combine(home, "lib", "site-packages") })
                if (Directory.Exists(Path.Combine(site, "optiland"))) return true;
            return false;
        }
    }

    /// <summary>What to tell the user when it is not set up.</summary>
    public static string SetupHint =>
        $"Optiland is not available: no embedded Python with optiland in '{Home}'. " +
        $"Run tools/setup-python.ps1, or set {OverrideVariable} to a Python that has optiland installed.";
}
