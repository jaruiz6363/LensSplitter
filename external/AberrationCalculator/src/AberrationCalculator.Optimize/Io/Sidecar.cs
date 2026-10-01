using System;
using System.Collections.Generic;
using System.IO;

using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Io;

/// <summary>Everything an optimisation run needs to be told, however it was told.</summary>
public sealed class OptimizationSetup
{
    public VariableSet Variables { get; } = new();
    public List<Pickup> Pickups { get; } = new();
    public List<Operand> Operands { get; } = new();

    /// <summary>Where each part came from, for the report and for an error message.</summary>
    public string VariableSource { get; set; } = string.Empty;
    public string MeritSource { get; set; } = string.Empty;
}

/// <summary>
/// The files that travel beside a lens.
///
/// <para><b>Two of them, and the split is not arbitrary.</b> The MERIT FUNCTION says what the
/// design should be; the VARIABLES say what may change about it. The first belongs to the problem
/// and can be carried from one design to another; the second belongs to the design and means
/// nothing away from it. So:</para>
///
/// <list type="bullet">
/// <item><c>&lt;lens&gt;.mf</c> - the merit function. Common to every format.</item>
/// <item><c>&lt;lens&gt;.var</c> - variables, bounds and pickups, for formats that cannot keep
/// them. A .lhlt can, and does, so it has no .var file: they are read from the lens and written
/// back to it.</item>
/// </list>
///
/// <para>Named for the lens file INCLUDING its extension - <c>triplet.zmx.mf</c>, not
/// <c>triplet.mf</c> - so a folder holding <c>triplet.zmx</c> and <c>triplet.seq</c> keeps their
/// settings apart.</para>
///
/// <para><b>A .lhlt's own merit function is never read.</b> This program optimises a different
/// one, and adopting somebody else's targets unasked would be the wrong kind of helpful. It is
/// left untouched in the file.</para>
/// </summary>
public static class Sidecar
{
    public const string MeritExtension = ".mf";
    public const string VariableExtension = ".var";

    public static string MeritPathFor(string lensPath) =>
        (lensPath ?? throw new ArgumentNullException(nameof(lensPath))) + MeritExtension;

    public static string VariablePathFor(string lensPath) =>
        (lensPath ?? throw new ArgumentNullException(nameof(lensPath))) + VariableExtension;

    /// <summary>
    /// True when this format keeps its variables and pickups in the lens file itself.
    ///
    /// <para>Only .lhlt does. Every other format this program reads was defined by somebody else
    /// for a different purpose, and none has a place to record what THIS optimiser may change.</para>
    /// </summary>
    public static bool KeepsVariablesInTheLensFile(string lensPath) =>
        string.Equals(Path.GetExtension(lensPath), ".lhlt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gathers the whole setup for a lens: variables and pickups from wherever that format keeps
    /// them, and the merit function from the .mf file.
    /// </summary>
    /// <param name="meritOverride">An explicitly named merit file, used instead of the sidecar.</param>
    public static OptimizationSetup Load(OpticalSystem system, string lensPath,
                                         string? meritOverride = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (lensPath == null) throw new ArgumentNullException(nameof(lensPath));

        var setup = new OptimizationSetup();

        if (KeepsVariablesInTheLensFile(lensPath))
        {
            setup.Variables.AddRange(SurfaceVariables.Read(system).Items);
            setup.Pickups.AddRange(system.Pickups);
            setup.VariableSource = setup.Variables.Count > 0
                ? "Variables and pickups came from the lens file."
                : "The lens file declares no variables.";
        }
        else
        {
            string path = VariablePathFor(lensPath);
            if (File.Exists(path))
            {
                var spec = VarFile.Read(path);
                setup.Variables.AddRange(spec.Variables.Items);
                setup.Pickups.AddRange(spec.Pickups);
                setup.VariableSource = "Variables read from " + Path.GetFileName(path) + ".";
            }
            else
            {
                setup.VariableSource = "No variables file was found ("
                                     + Path.GetFileName(path) + ").";
            }
        }

        string merit = meritOverride ?? MeritPathFor(lensPath);
        if (File.Exists(merit))
        {
            setup.Operands.AddRange(MeritFile.Read(merit));
            setup.MeritSource = "Merit function read from " + Path.GetFileName(merit) + ".";
        }
        else
        {
            setup.MeritSource = "No merit function file was found ("
                              + Path.GetFileName(merit) + ").";
        }
        return setup;
    }

    /// <summary>
    /// Writes the setup beside a lens, and returns the files it wrote.
    ///
    /// <para>The variables go into a .var file only when the lens format cannot hold them. For a
    /// .lhlt they have already been stamped onto the design and travel in the lens itself.</para>
    /// </summary>
    public static List<string> Save(string lensPath, OptimizationSetup setup)
    {
        if (lensPath == null) throw new ArgumentNullException(nameof(lensPath));
        if (setup == null) throw new ArgumentNullException(nameof(setup));

        var written = new List<string>();
        string? dir = Path.GetDirectoryName(Path.GetFullPath(lensPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string merit = MeritPathFor(lensPath);
        File.WriteAllText(merit, MeritFile.Write(setup.Operands, Path.GetFileName(lensPath)));
        written.Add(merit);

        if (!KeepsVariablesInTheLensFile(lensPath))
        {
            var spec = new VarSpecification();
            spec.Variables.AddRange(setup.Variables.Items);
            spec.Pickups.AddRange(setup.Pickups);

            string vars = VariablePathFor(lensPath);
            File.WriteAllText(vars, VarFile.Write(spec, Path.GetFileName(lensPath)));
            written.Add(vars);
        }
        return written;
    }
}

/// <summary>
/// The bridge between a design's own statement of what may be optimised and the optimiser's
/// variable list.
///
/// <para>A .lhlt records, per surface, whether its curvature and its thickness may move and
/// between what limits. That is the design author's declaration, and this reads it rather than
/// asking the user to say it again. Writing it back is how an optimised design returns to
/// the program that wrote it, with its variables intact.</para>
/// </summary>
public static class SurfaceVariables
{
    /// <summary>The variables a design declares on its own surfaces.</summary>
    public static VariableSet Read(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        var set = new VariableSet();
        int last = system.LastOpticalSurface();

        for (int i = 1; i <= last && i < system.Surfaces.Count; i++)
        {
            var s = system.Surfaces[i];

            if (s.CurvatureVariable)
                set.Add(new Variable
                {
                    Kind = VariableKind.Curvature, Surface = i,
                    Min = s.CurvatureMin, Max = s.CurvatureMax,
                });

            if (s.ThicknessVariable)
                set.Add(new Variable
                {
                    Kind = VariableKind.Thickness, Surface = i,
                    Min = s.ThicknessMin, Max = s.ThicknessMax,
                });

            // The figuring. A .lhlt records which of these its author made variable but has
            // nowhere to put a bound on them, so they come back unbounded; a designer who
            // wants one states it in the .var file, which every format can carry.
            if (s.ConicVariable)
                set.Add(new Variable { Kind = VariableKind.Conic, Surface = i });

            if (s.AsphericVariable != null)
            {
                if (s.AsphericVariable.Length > 1 && s.AsphericVariable[1])
                    set.Add(new Variable { Kind = VariableKind.Asphere4, Surface = i });
                if (s.AsphericVariable.Length > 2 && s.AsphericVariable[2])
                    set.Add(new Variable { Kind = VariableKind.Asphere6, Surface = i });
                if (s.AsphericVariable.Length > 3 && s.AsphericVariable[3])
                    set.Add(new Variable { Kind = VariableKind.Asphere8, Surface = i });
            }
        }
        return set;
    }

    /// <summary>
    /// Stamps a variable list back onto the surfaces, so it can be written into the lens file.
    ///
    /// <para>Every surface is cleared first. A variable the user REMOVED has to disappear from
    /// the design, and leaving the old flag standing because nothing replaced it is how a design
    /// accumulates variables nobody asked for.</para>
    /// </summary>
    public static void Write(VariableSet variables, OpticalSystem system)
    {
        if (variables == null) throw new ArgumentNullException(nameof(variables));
        if (system == null) throw new ArgumentNullException(nameof(system));

        foreach (var s in system.Surfaces)
        {
            s.CurvatureVariable = false;
            s.ThicknessVariable = false;
            s.ConicVariable = false;
            if (s.AsphericVariable != null)
                for (int k = 0; k < s.AsphericVariable.Length; k++) s.AsphericVariable[k] = false;
            s.CurvatureMin = double.NegativeInfinity;
            s.CurvatureMax = double.PositiveInfinity;
            s.ThicknessMin = double.NegativeInfinity;
            s.ThicknessMax = double.PositiveInfinity;
        }

        foreach (var v in variables.Items)
        {
            if (v.Surface < 0 || v.Surface >= system.Surfaces.Count) continue;
            var s = system.Surfaces[v.Surface];

            switch (v.Kind)
            {
                case VariableKind.Curvature:
                    s.CurvatureVariable = true;
                    s.CurvatureMin = v.Min;
                    s.CurvatureMax = v.Max;
                    break;
                case VariableKind.Thickness:
                    s.ThicknessVariable = true;
                    s.ThicknessMin = v.Min;
                    s.ThicknessMax = v.Max;
                    break;
                case VariableKind.Conic:
                    s.ConicVariable = true;
                    break;
                default:
                    // A bound on a figuring variable is dropped here rather than silently
                    // mangled: the format has nowhere to keep it. It survives in the .var file.
                    int k = v.AsphericIndex;
                    if (s.AsphericVariable != null && k >= 0 && k < s.AsphericVariable.Length)
                        s.AsphericVariable[k] = true;
                    break;
            }
        }
    }
}
