using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    public static class LhltReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() },
            PropertyNameCaseInsensitive = true
        };

        public static LhltReadResult Read(string path)
        {
            var json = File.ReadAllText(path);
            json = MigrateJson(json);
            var file = JsonSerializer.Deserialize<LhltFile>(json, JsonOptions);
            if (file == null)
                throw new InvalidOperationException("Failed to deserialize LHLT file.");

            return FromLhltFile(file);
        }

        /// <summary>
        /// Migrate JSON before deserialization. Currently only handles the
        /// removed Paraxial ray-aiming mode → Off rewrite. The earlier
        /// operand-name table (EFFL→EFL, MAX_CV→CV, etc.) was dropped on
        /// 2026-05-04 because no in-the-wild .lhlt files use those names.
        /// </summary>
        private static string MigrateJson(string json)
        {
            // Ray aiming: the old "Paraxial" mode was removed → map to Off.
            // Scope the rewrite to the RayAiming property ONLY — a blanket
            // replace of "Paraxial" would also corrupt the SurfaceType.Paraxial
            // (ideal thin lens) surface value added in 1.0.140, which serializes
            // as "Type": "Paraxial" and must round-trip intact.
            json = System.Text.RegularExpressions.Regex.Replace(
                json, "(\"RayAiming\"\\s*:\\s*)\"Paraxial\"", "$1\"Off\"");
            return json;
        }

        public static LhltReadResult FromLhltFile(LhltFile file)
        {
            var system = new OpticalSystem
            {
                Title = file.Title,
                Notes = file.Notes ?? string.Empty,
                Designer = file.Designer ?? string.Empty,
                Aperture = new Aperture(file.Aperture.Type, file.Aperture.Value),
                FieldType = file.FieldType,
                RayAiming = file.RayAiming,
                IsAfocal = file.IsAfocal,
                TelecentricObjectSpace = file.TelecentricObjectSpace,
                SemiDiameterSolve = file.SemiDiameterSolve,
                GlassCatalogs = file.GlassCatalogs ?? new System.Collections.Generic.List<string>()
            };

            // Surfaces
            foreach (var ls in file.Surfaces)
            {
                var s = new Surface
                {
                    Index = ls.Index,
                    Type = ls.Type,
                    Comment = ls.Comment ?? string.Empty,
                    Radius = ls.Radius,
                    // Legacy .lhlt files (and any written before importers
                    // normalized 1e20) store the object-at-infinity sentinel
                    // numerically. Normalize on read so the engine sees
                    // double.PositiveInfinity.
                    Thickness = Math.Abs(ls.Thickness) > 1e18 ? double.PositiveInfinity : ls.Thickness,
                    Material = ls.Material ?? string.Empty,
                    SemiDiameter = ls.SemiDiameter,
                    SemiDiameterMode = ls.SemiDiameterMode,
                    ClearAperturePercent = ls.ClearAperturePercent > 0 ? ls.ClearAperturePercent : 100.0,
                    Conic = ls.Conic,
                    IsStop = ls.IsStop,
                    InnerRadius = ls.InnerRadius,
                    ObscurationRadius = ls.ObscurationRadius,
                    FloatingApertureRadius = ls.FloatingApertureRadius,
                    HasMarginalRaySolve = ls.HasMarginalRaySolve,
                    ModelIndexEnabled = ls.ModelIndexEnabled,
                    ModelNd = ls.ModelNd, ModelVd = ls.ModelVd, ModelDPgF = ls.ModelDPgF,
                    FocalLength = ls.FocalLength,

                    // What the design's author said may be optimised, and how far. A .lhlt
                    // states this itself, so it is honoured rather than re-invented - the lens
                    // arrives with its variables already declared. The MERIT FUNCTION in the
                    // file is deliberately NOT read: this program optimises a different one,
                    // and quietly adopting someone else's targets would be the wrong kind of
                    // helpful. It is left untouched in the file when the design is written back.
                    CurvatureVariable = ls.CurvatureVariable,
                    ThicknessVariable = ls.ThicknessVariable,
                    ConicVariable = ls.ConicVariable,
                    CurvatureMin = ls.CurvatureMin ?? double.NegativeInfinity,
                    CurvatureMax = ls.CurvatureMax ?? double.PositiveInfinity,
                    ThicknessMin = ls.ThicknessMin ?? double.NegativeInfinity,
                    ThicknessMax = ls.ThicknessMax ?? double.PositiveInfinity,
                };

                if (ls.AsphericCoefficients != null)
                {
                    int len = Math.Min(ls.AsphericCoefficients.Length, s.AsphericCoefficients.Length);
                    Array.Copy(ls.AsphericCoefficients, s.AsphericCoefficients, len);
                }

                if (ls.AsphericVariable != null)
                {
                    int len = Math.Min(ls.AsphericVariable.Length, s.AsphericVariable.Length);
                    Array.Copy(ls.AsphericVariable, s.AsphericVariable, len);
                }

                // Generic indexed parameters (PRO surface types). Length-clamped so a
                // file authored with a different MaxParameters can't overrun the arrays.
                if (ls.Parameters != null)
                    Array.Copy(ls.Parameters, s.Parameters, Math.Min(ls.Parameters.Length, s.Parameters.Length));
                if (ls.Settings != null)
                    Array.Copy(ls.Settings, s.Settings, Math.Min(ls.Settings.Length, s.Settings.Length));

                // ABCD / Paraxial / Coordinate Break are index-transparent black boxes with no
                // refractive medium. A glass name left over from a Standard->ABCD conversion (older
                // files) is invalid: it would (wrongly) make the medium after the surface glass —
                // corrupting the index array AND counting the surface as glass in CTG/DTRG/Glass-
                // Substitution. Normalize it away on load so the loaded system is self-consistent.
                if (s.Type is AberrationCalculator.Core.Enums.SurfaceType.Abcd
                    or AberrationCalculator.Core.Enums.SurfaceType.Paraxial
                    or AberrationCalculator.Core.Enums.SurfaceType.CoordinateBreak)
                {
                    s.Material = string.Empty;
                    s.ModelIndexEnabled = false;
                }

                system.Surfaces.Add(s);
            }

            // Wavelengths
            foreach (var w in file.Wavelengths)
            {
                system.Wavelengths.Add(new Wavelength(w.Value, w.Weight, w.IsPrimary));
            }

            // Fields
            foreach (var f in file.Fields)
            {
                system.Fields.Add(new Field(f.Y, f.Weight)
                {
                });
            }
            FieldValidation.FilterImportedFields(system);

            // Pickups
            foreach (var p in file.Pickups)
            {
                system.Pickups.Add(new Pickup
                {
                    TargetSurfaceIndex = p.TargetSurfaceIndex,
                    Parameter = p.Parameter,
                    SourceSurfaceIndex = p.SourceSurfaceIndex,
                    SourceConfigurationIndex = p.SourceConfigurationIndex,
                    ScaleFactor = p.ScaleFactor,
                    Offset = p.Offset,
                    ParameterIndex = p.ParameterIndex
                });
            }

            // Glass substitution settings

            // Multi-configuration (MCE) is a PRO feature — the shared .lhlt format is
            // single-config. Any config block from a PRO/legacy file is ignored on load.

            return new LhltReadResult
            {
                System = system
            };
        }
    }

    public class LhltReadResult
    {
        public OpticalSystem System { get; set; } = null!;
    }
}
