extern alias Ad;

using System;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

using AdM = Ad::AberrationCalculator.Core.Models;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;
using VarKind = AberrationCalculator.Optimize.Variables.VariableKind;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// The design being optimised, and the machinery for asking it questions.
///
/// <para>It owns the working <see cref="OpticalSystem"/> - the one the variables are written
/// into and the one that is handed back when the run finishes - together with the refractive
/// indices, the field the coefficients are referred to, and the decision about which route the
/// seventh order comes by. Everything downstream reads those through a
/// <see cref="DesignProbe"/>.</para>
/// </summary>
public sealed class Design
{
    private readonly GlassCatalog _catalog;
    private double[][] _indices = Array.Empty<double[]>();
    private Dual[][] _adIndices = Array.Empty<Dual[]>();

    public Design(OpticalSystem system, GlassCatalog catalog, VariableSet variables)
    {
        System = system ?? throw new ArgumentNullException(nameof(system));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Variables = variables ?? throw new ArgumentNullException(nameof(variables));

        // What cannot be optimised honestly is settled here, before anything runs.
        RefuseWhatCannotBeOptimisedHonestly();

        // A BOUND IS AN INVARIANT, NOT A REQUEST. Bounds here are held by reflection, and
        // reflection is total: every real number folds to a value inside the interval. So there
        // is no state in which a bounded variable is out of range, and the one moment that could
        // create one - a design arriving with a value outside a bound the user has since imposed
        // - is closed here, at construction, rather than by each caller remembering to fold.
        //
        // It was not, and the two callers that forgot were both wrong in ways nobody would have
        // guessed from the symptom: a hopping run seeded its global best with the unfolded
        // design, which no chain working inside the bounds could then beat, so the run returned
        // a lens that violated its own constraints AND reported that it had found nothing.
        // Every consumer of a variable goes through this class, so making it true here makes it
        // true everywhere.
        Variables.Write(System, Variables.Read(System));

        // One slot for the seedless pass and one per variable.
        _adSystems = new AdM.OpticalSystem?[Variables.Count + 1];

        // Every material resolved once, here, and never looked up by name again. Past this point
        // the optimisation touches no catalogue and no file.
        _materials = new GlassData?[System.Surfaces.Count];
        for (int i = 0; i < _materials.Length; i++)
        {
            var s = System.Surfaces[i];
            if (string.IsNullOrEmpty(s.Material) || s.IsMirror
                || (s.ModelIndexEnabled && s.ModelNd > 0.0)) continue;
            _materials[i] = _catalog.Find(s.Material, System.GlassCatalogs);
        }

        RefreshIndices();

        // Everything that could make an evaluation ask a question about the DESIGN rather than
        // about the numbers is settled here, once, before anything runs. It needs the indices,
        // so it comes last in the constructor rather than first.
        SupportedDesign.Require(this);
    }

    /// <summary>The working design. Variables are written straight into this.</summary>
    public OpticalSystem System { get; }

    public VariableSet Variables { get; }

    /// <summary>One sentence for the report on where the coefficients came from.</summary>
    public string RouteExplanation => SupportedDesign.Explanation;

    /// <summary>Materials that could not be resolved when the indices were last built.</summary>
    public System.Collections.Generic.List<string> Unresolved { get; } = new();

    /// <summary>
    /// The largest field the design defines, sign kept.
    ///
    /// <para>The coefficients are referred to this field and the fractional field heights of the
    /// predicted spot are measured against it, exactly as the report's PRMSA section does - so
    /// the optimiser and the report describe the same quantity.</para>
    /// </summary>
    public double MaxField
    {
        get
        {
            double m = 0.0;
            foreach (var f in System.Fields) if (Math.Abs(f.Y) > Math.Abs(m)) m = f.Y;
            return m;
        }
    }

    public int WaveCount => Math.Max(1, System.Wavelengths.Count);

    public int PrimaryWave => Math.Max(0, System.PrimaryWavelengthIndex);

    /// <summary>Refractive index after each surface, per wavelength.</summary>
    public double[] Indices(int wave) =>
        _indices[wave < 0 ? PrimaryWave : Math.Min(wave, _indices.Length - 1)];

    /// <summary>
    /// Rebuilds the refractive indices from the catalog.
    ///
    /// <para>Only needed when the GLASSES change, which happens when the basin hopping swaps
    /// one - the continuous variables never move an index, so the ordinary optimisation loop
    /// builds these once and reuses them.</para>
    /// </summary>
    public void RefreshIndices()
    {
        int waves = WaveCount;
        _indices = new double[waves][];
        _adIndices = new Dual[waves][];
        Unresolved.Clear();

        for (int w = 0; w < waves; w++)
        {
            double lambda = w < System.Wavelengths.Count ? System.Wavelengths[w].Value : 0.5875618;
            _indices[w] = IndexResolver.Build(System, _materials, lambda, Unresolved);
            _adIndices[w] = AdBridge.Constants(_indices[w]);
        }
    }

    /// <summary>
    /// The material at each surface, resolved ONCE when the design is opened.
    ///
    /// <para>A name is not a material. Resolving one searches every loaded catalogue, and an
    /// optimiser has no reason to ask the same question twice: the glasses are fixed for the
    /// whole run except where the search swaps one, and a swap is a choice between glasses that
    /// are already in hand. Nothing here reads a file, and nothing looks a name up again.</para>
    /// </summary>
    private readonly GlassData?[] _materials;

    /// <summary>
    /// Points a surface at a different glass.
    ///
    /// <para>The pointer IS the change. The name and catalogue are written alongside it so the
    /// design can be saved and read back, but nothing resolves them again - the indices are
    /// rebuilt from the glass itself, which is arithmetic and no lookup.</para>
    /// </summary>
    public void SetMaterial(int surface, GlassData glass)
    {
        if (glass == null) throw new ArgumentNullException(nameof(glass));
        if (surface < 0 || surface >= _materials.Length)
            throw new ArgumentOutOfRangeException(nameof(surface));

        _materials[surface] = glass;
        System.Surfaces[surface].Material = glass.Name;
        System.Surfaces[surface].CatalogName = glass.Catalog;
    }

    /// <summary>The glass a surface currently points at, or null where there is none.</summary>
    public GlassData? MaterialAt(int surface) =>
        surface >= 0 && surface < _materials.Length ? _materials[surface] : null;

    /// <summary>Points a surface back at a glass it held before - the rejected-hop restore.</summary>
    public void RestoreMaterial(int surface, GlassData? glass, string? name, string? catalog)
    {
        if (surface < 0 || surface >= _materials.Length) return;
        _materials[surface] = glass;
        System.Surfaces[surface].Material = name;
        System.Surfaces[surface].CatalogName = catalog;
    }

    /// <summary>
    /// Refuses, up front, what this optimiser would otherwise get quietly wrong.
    ///
    /// <para>A refusal rather than a warning, and deliberately so. A warning printed at the start
    /// of a run that then produces a plausible-looking lens is a warning nobody reads twice, and
    /// the failure it describes does not announce itself in the result.</para>
    ///
    /// <para>A variable that drives a surface a PICKUP depends on. Pickups are resolved when the
    /// file is read and are not maintained afterwards, so moving one end of a cemented pair would
    /// part the cement, or a mirror's radius would come loose from the one it is tied to - and
    /// the prescription that came out would be a lens nobody can build.</para>
    /// </summary>
    private void RefuseWhatCannotBeOptimisedHonestly()
    {
        foreach (var p in System.Pickups)
        {
            foreach (var v in Variables.Items)
            {
                if (v.Surface != p.TargetSurfaceIndex && v.Surface != p.SourceSurfaceIndex)
                    continue;
                if (!DrivesSameParameter(v.Kind, p.Parameter)) continue;

                throw new NotSupportedException(
                    $"Variable {v.Name} touches surface {v.Surface}, which is one end of a "
                  + $"pickup tying surface {p.TargetSurfaceIndex} to surface "
                  + $"{p.SourceSurfaceIndex}. Pickups are resolved when a file is read and are "
                  + "not maintained while the design changes, so optimising this would part the "
                  + "two. Remove the variable, or remove the pickup and constrain the pair with "
                  + "operands instead.");
            }
        }
    }

    // VarKind and not Variables.VariableKind: this class has a property called Variables, and
    // inside it that name means the property rather than the namespace.
    private static bool DrivesSameParameter(VarKind kind, Core.Enums.PickupParameter parameter) =>
        (kind, parameter) switch
        {
            (VarKind.Curvature, Core.Enums.PickupParameter.Curvature) => true,
            (VarKind.Thickness, Core.Enums.PickupParameter.Thickness) => true,
            _ => false,
        };

    /// <summary>Writes a variable vector into the design, folding it inside its bounds.</summary>
    public void Apply(double[] x) => Variables.Write(System, x);

    /// <summary>The design's present variable values.</summary>
    public double[] Read() => Variables.Read(System);

    /// <summary>
    /// A probe seeded on one variable, or on none when <paramref name="seed"/> is negative.
    ///
    /// <para>Building one copies the design across into dual arithmetic, which is a handful of
    /// small objects and is nothing beside the chain that is about to run on it.</para>
    /// </summary>
    /// <summary>
    /// One dual system per seed, built once and refreshed thereafter.
    ///
    /// <para>Slot 0 is the seedless pass; slot v+1 carries the seed on variable v. A pass only
    /// ever touches its own slot, so the passes remain as isolated from one another as they were
    /// when each built its own system - which is what lets them run in parallel - without paying
    /// to construct a lens on every call. See <see cref="AdBridge.Refresh"/> for what that cost
    /// measured.</para>
    /// </summary>
    private readonly AdM.OpticalSystem?[] _adSystems;

    public DesignProbe Probe(int seed)
    {
        int slot = seed + 1;
        if (slot < 0 || slot >= _adSystems.Length)
            return new DesignProbe(AdBridge.Build(System, Variables, seed),
                                   _adIndices, MaxField, PrimaryWave);

        var sys = _adSystems[slot];
        if (sys == null) _adSystems[slot] = sys = AdBridge.Build(System, Variables, seed);
        else AdBridge.Refresh(sys, System, Variables, seed);

        // A seedless pass wants values and no derivative, and the chain in dual arithmetic costs
        // about ten times what it costs in doubles. Hand it the ordinary design to work from.
        return seed < 0
            ? new DesignProbe(sys, _adIndices, MaxField, PrimaryWave, System, _indices)
            : new DesignProbe(sys, _adIndices, MaxField, PrimaryWave);
    }
}
