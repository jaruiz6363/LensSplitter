using System;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// A deep copy of a design.
///
/// <para>Needed twice over. The basin hopping runs several chains at once and each has to be
/// free to wreck its own copy without the others noticing, and every optimiser here has to be
/// able to hand back the design it STARTED from if the run turns out worse than where it began.
/// Both want a lens that shares nothing with the original, down to the aspheric coefficient
/// arrays.</para>
/// </summary>
public static class DesignCopy
{
    public static OpticalSystem Deep(OpticalSystem source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var copy = new OpticalSystem
        {
            Title = source.Title,
            Designer = source.Designer,
            Notes = source.Notes,
            FieldType = source.FieldType,
            IsAfocal = source.IsAfocal,
            TelecentricObjectSpace = source.TelecentricObjectSpace,
            RayAiming = source.RayAiming,
            SemiDiameterSolve = source.SemiDiameterSolve,
            Aperture = new Aperture(source.Aperture.Type, source.Aperture.Value),
        };

        foreach (var w in source.Wavelengths)
            copy.Wavelengths.Add(new Wavelength(w.Value, w.Weight, w.IsPrimary));
        foreach (var f in source.Fields)
            copy.Fields.Add(new Field(f.Y, f.Weight) { X = f.X });
        foreach (var c in source.GlassCatalogs) copy.GlassCatalogs.Add(c);
        copy.GlassCatalogsAreInferred = source.GlassCatalogsAreInferred;

        foreach (var p in source.Pickups)
            copy.Pickups.Add(new Pickup
            {
                TargetSurfaceIndex = p.TargetSurfaceIndex,
                SourceSurfaceIndex = p.SourceSurfaceIndex,
                Parameter = p.Parameter,
                ScaleFactor = p.ScaleFactor,
                Offset = p.Offset,
                ParameterIndex = p.ParameterIndex,
                SourceConfigurationIndex = p.SourceConfigurationIndex,
            });

        foreach (var s in source.Surfaces) copy.Surfaces.Add(Deep(s));
        return copy;
    }

    public static Surface Deep(Surface s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));

        var d = new Surface
        {
            Index = s.Index,
            Type = s.Type,
            Curvature = s.Curvature,
            Thickness = s.Thickness,
            Conic = s.Conic,
            Material = s.Material,
            CatalogName = s.CatalogName,
            IsStop = s.IsStop,
            SemiDiameter = s.SemiDiameter,
            SemiDiameterMode = s.SemiDiameterMode,
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
            // A fresh array, not the same one: sharing it would let one chain's aspheric term
            // appear in another's lens, which is the kind of fault that only shows up as a
            // parallel run giving a different answer from a serial one.
            AsphericCoefficients = (double[])s.AsphericCoefficients.Clone(),

            // The same reasoning as the coefficients above: a shared flag array would let one
            // chain's declaration of what may move appear in another's lens.
            CurvatureVariable = s.CurvatureVariable,
            ThicknessVariable = s.ThicknessVariable,
            ConicVariable = s.ConicVariable,
            AsphericVariable = (bool[])s.AsphericVariable.Clone(),
            CurvatureMin = s.CurvatureMin,
            CurvatureMax = s.CurvatureMax,
            ThicknessMin = s.ThicknessMin,
            ThicknessMax = s.ThicknessMax,
        };

        for (int k = 0; k < s.Parameters.Length; k++) d.SetParameter(k, s.Parameters[k]);
        for (int k = 0; k < s.Settings.Length; k++) d.SetSetting(k, s.Settings[k]);
        return d;
    }

    /// <summary>Copies one design's surface values over another's, in place.</summary>
    public static void CopyInto(OpticalSystem source, OpticalSystem target)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (source.Surfaces.Count != target.Surfaces.Count)
            throw new ArgumentException("the two designs do not have the same surfaces.");

        for (int i = 0; i < source.Surfaces.Count; i++)
        {
            var s = source.Surfaces[i];
            var t = target.Surfaces[i];
            t.Type = s.Type;
            t.Curvature = s.Curvature;
            t.Thickness = s.Thickness;
            t.Conic = s.Conic;
            t.Material = s.Material;
            t.CatalogName = s.CatalogName;
            t.SemiDiameter = s.SemiDiameter;
            t.ModelIndexEnabled = s.ModelIndexEnabled;
            t.ModelNd = s.ModelNd;
            t.ModelVd = s.ModelVd;
            t.ModelDPgF = s.ModelDPgF;
            for (int k = 0; k < s.AsphericCoefficients.Length
                            && k < t.AsphericCoefficients.Length; k++)
                t.AsphericCoefficients[k] = s.AsphericCoefficients[k];

            // Copied for the same reason the coefficients are: with the figuring variable, a
            // run that ends worse than it began hands back the design it started from, and a
            // conic left behind by the abandoned attempt would be a design nobody asked for.
            t.ConicVariable = s.ConicVariable;
            for (int k = 0; k < s.AsphericVariable.Length
                            && k < t.AsphericVariable.Length; k++)
                t.AsphericVariable[k] = s.AsphericVariable[k];
        }
    }
}
