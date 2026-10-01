extern alias Ad;

using System;

using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

using AdM = Ad::AberrationCalculator.Core.Models;
using AdE = Ad::AberrationCalculator.Core.Enums;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// Carries a design across into the differentiating arithmetic, with one variable seeded.
///
/// <para>The two <c>OpticalSystem</c> types are the same source compiled twice - Core's holds
/// <c>double</c>, the aliased one holds <see cref="Dual"/> - so this is a field-for-field copy
/// and nothing more, except at one place. The variable named by <paramref name="seed"/> is
/// planted as <see cref="Dual.Seed"/>, value unchanged and derivative one. Every quantity the
/// aberration chain then computes from that system arrives carrying its exact derivative with
/// respect to that one variable: the paraxial trace, all thirty-seven coefficients, the
/// predicted spot, a real ray's intercept at any surface.</para>
///
/// <para>One pass, one variable, one column of the Jacobian. The columns are independent, so
/// the whole Jacobian is that pass run once per variable, and they run in parallel.</para>
/// </summary>
public static class AdBridge
{
    /// <summary>
    /// Rewrites an already-built dual system to match the core one, instead of building it again.
    ///
    /// <para><b>Almost nothing about a design changes during an optimisation.</b> The surface
    /// count, the glasses, the wavelengths, the fields, the aperture, the stop, the aspheric
    /// slots - all fixed. What moves is a handful of curvatures and thicknesses, and which one
    /// carries the seed. Rebuilding the whole system to change seventeen numbers cost about 830
    /// microseconds of the 880 a dual evaluation took, measured: the aberration chain itself was
    /// six per cent of the bill and the other ninety-four was allocating a lens.</para>
    ///
    /// <para>Each seed keeps its own system, so the parallel passes still touch nothing in
    /// common - the isolation that makes them safe to run at once is preserved, it is simply no
    /// longer paid for out of the allocator on every call.</para>
    /// </summary>
    public static void Refresh(AdM.OpticalSystem target, OpticalSystem core,
                               VariableSet variables, int seed)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (core == null) throw new ArgumentNullException(nameof(core));

        var planted = seed >= 0 && seed < variables.Count ? variables[seed] : null;

        for (int i = 0; i < core.Surfaces.Count && i < target.Surfaces.Count; i++)
        {
            var s = core.Surfaces[i];
            var d = target.Surfaces[i];

            d.Curvature = Plant(s.Curvature, planted, VariableKind.Curvature, i);
            d.Thickness = Plant(s.Thickness, planted, VariableKind.Thickness, i);

            // The figuring. A conic or an r^4 term that is a variable has to arrive here
            // seeded, exactly as a curvature does, or the column of the Jacobian belonging to
            // it comes back zero - which reads as a variable the merit function does not
            // depend on rather than as a derivative that was never planted.
            d.Conic = Plant(s.Conic, planted, VariableKind.Conic, i);
            PlantAspheric(s.AsphericCoefficients, d.AsphericCoefficients, planted, i);

            // Glass moves in the hopping, and the semi-diameter follows the beam, so neither is
            // as invariant as the rest. Both are plain assignments and cost nothing to keep.
            d.Material = s.Material;
            d.CatalogName = s.CatalogName;
            d.SemiDiameter = s.SemiDiameter;
        }
    }

    /// <summary>
    /// The design in dual arithmetic. <paramref name="seed"/> indexes
    /// <paramref name="variables"/>; pass -1 for a pass that wants values and no derivative.
    /// </summary>
    public static AdM.OpticalSystem Build(OpticalSystem core, VariableSet variables, int seed)
    {
        if (core == null) throw new ArgumentNullException(nameof(core));

        var sys = new AdM.OpticalSystem
        {
            Title = core.Title,
            Designer = core.Designer,
            Notes = core.Notes,
            FieldType = (AdE.FieldType)(int)core.FieldType,
            IsAfocal = core.IsAfocal,
            TelecentricObjectSpace = core.TelecentricObjectSpace,
            RayAiming = (AdE.RayAimingMode)(int)core.RayAiming,
            SemiDiameterSolve = (AdE.SemiDiameterSolve)(int)core.SemiDiameterSolve,
            Aperture = new AdM.Aperture((AdE.ApertureType)(int)core.Aperture.Type,
                                        core.Aperture.Value),
        };

        foreach (var w in core.Wavelengths)
            sys.Wavelengths.Add(new AdM.Wavelength(w.Value, w.Weight, w.IsPrimary));
        foreach (var f in core.Fields)
            sys.Fields.Add(new AdM.Field(f.Y, f.Weight) { X = f.X });

        var target = seed >= 0 && seed < variables.Count ? variables[seed] : null;

        for (int i = 0; i < core.Surfaces.Count; i++)
        {
            var s = core.Surfaces[i];
            var d = new AdM.Surface
            {
                Index = s.Index,
                Type = (AdE.SurfaceType)(int)s.Type,
                Curvature = Plant(s.Curvature, target, VariableKind.Curvature, i),
                Thickness = Plant(s.Thickness, target, VariableKind.Thickness, i),
                Conic = Plant(s.Conic, target, VariableKind.Conic, i),
                Material = s.Material,
                CatalogName = s.CatalogName,
                IsStop = s.IsStop,
                SemiDiameter = s.SemiDiameter,
                SemiDiameterMode = (AdE.SemiDiameterMode)(int)s.SemiDiameterMode,
                ClearAperturePercent = s.ClearAperturePercent,
                ObscurationRadius = s.ObscurationRadius,
                Comment = s.Comment,
                ModelIndexEnabled = s.ModelIndexEnabled,
                ModelNd = s.ModelNd,
                ModelVd = s.ModelVd,
                ModelDPgF = s.ModelDPgF,
                FocalLength = s.FocalLength,
                FloatingApertureRadius = s.FloatingApertureRadius,
                ClapOuterRadius = s.ClapOuterRadius,
                InnerRadius = s.InnerRadius,
                HasMarginalRaySolve = s.HasMarginalRaySolve,
            };

            PlantAspheric(s.AsphericCoefficients, d.AsphericCoefficients, target, i);

            for (int k = 0; k < s.Parameters.Length; k++) d.SetParameter(k, s.Parameters[k]);
            for (int k = 0; k < s.Settings.Length; k++) d.SetSetting(k, s.Settings[k]);

            sys.Surfaces.Add(d);
        }

        return sys;
    }

    /// <summary>
    /// The value, seeded if this is the parameter being differentiated with respect to and a
    /// plain constant otherwise.
    /// </summary>
    private static Dual Plant(double value, Variable? target, VariableKind kind, int surface)
    {
        if (target != null && target.Kind == kind && target.Surface == surface)
            return Dual.Seed(value);
        return value;
    }

    /// <summary>
    /// The even-asphere coefficients, with one slot seeded if that is what is being
    /// differentiated with respect to.
    ///
    /// <para>The whole array is copied every time rather than only the seeded slot, because the
    /// coefficients are what the surface IS: a pass that copied only the seed would trace a
    /// sphere and report the derivative of a lens that is not on the bench. Only slots 1 to 3
    /// can be seeded - r^4, r^6, r^8 - but all of them are carried.</para>
    /// </summary>
    private static void PlantAspheric(double[] source, Dual[] into, Variable? target, int surface)
    {
        int seed = target != null && target.Surface == surface ? target.AsphericIndex : -1;
        for (int k = 0; k < source.Length && k < into.Length; k++)
            into[k] = k == seed ? Dual.Seed(source[k]) : source[k];
    }

    /// <summary>
    /// Refractive indices as constants of the problem.
    ///
    /// <para>They carry no derivative because no continuous variable moves them: glass enters
    /// this optimiser as a discrete substitution inside the basin hopping, never as a gradient.
    /// A model glass whose index and dispersion were themselves variable would need seeds here
    /// too.</para>
    /// </summary>
    public static Dual[] Constants(double[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var d = new Dual[values.Length];
        for (int i = 0; i < values.Length; i++) d[i] = values[i];
        return d;
    }
}
