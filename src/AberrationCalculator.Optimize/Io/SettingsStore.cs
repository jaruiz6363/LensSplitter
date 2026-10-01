using System;
using System.Collections.Generic;
using System.IO;

using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Optimize.Io;

/// <summary>
/// Reads and writes a lens's optimisation settings, wherever that format keeps them.
///
/// <para>The one place that knows the difference. A .lhlt holds its variables and pickups in the
/// lens file, so saving them means editing the design; every other format keeps them in a .var
/// beside it. The merit function is always a .mf. Everything above this - the commands, the
/// optimiser, the MCP - asks for "the settings for this lens" and never has to care.</para>
///
/// <para><b>Settings are saved in place.</b> A command that sets a variable is not an
/// optimisation run: it edits the thing the user is building, the way an editor saves a file.
/// The design itself is only ever changed by <c>--save</c> or <c>--saveas</c>, and only after an
/// optimisation asked for one.</para>
/// </summary>
public static class SettingsStore
{
    public static OptimizationSetup Load(OpticalSystem system, string lensPath) =>
        Sidecar.Load(system, lensPath);

    /// <summary>
    /// Writes the settings back and returns the files that changed.
    ///
    /// <para><paramref name="system"/> is the design the settings belong to; for a .lhlt its
    /// surfaces are stamped with the variables and the lens file is edited in place.</para>
    /// </summary>
    public static List<string> Save(OpticalSystem system, string lensPath,
                                    OptimizationSetup setup)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (lensPath == null) throw new ArgumentNullException(nameof(lensPath));
        if (setup == null) throw new ArgumentNullException(nameof(setup));

        var written = new List<string>();

        if (Sidecar.KeepsVariablesInTheLensFile(lensPath))
        {
            SurfaceVariables.Write(setup.Variables, system);
            system.Pickups.Clear();
            system.Pickups.AddRange(setup.Pickups);

            LhltPatcher.Patch(system, lensPath, lensPath);
            written.Add(lensPath);

            File.WriteAllText(Sidecar.MeritPathFor(lensPath),
                              MeritFile.Write(setup.Operands, Path.GetFileName(lensPath)));
            written.Add(Sidecar.MeritPathFor(lensPath));
        }
        else
        {
            written.AddRange(Sidecar.Save(lensPath, setup));
        }
        return written;
    }

    /// <summary>Where the variables for this lens live, as a phrase for a report line.</summary>
    public static string VariableHome(string lensPath) =>
        Sidecar.KeepsVariablesInTheLensFile(lensPath)
            ? Path.GetFileName(lensPath)
            : Path.GetFileName(Sidecar.VariablePathFor(lensPath));
}
