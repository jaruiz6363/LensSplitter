using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes a design out as .lhlt.
    ///
    /// <para>Until there was an optimiser this repository had no reason to write a lens file at
    /// all - it read what somebody else wrote and reported on it. A program that CHANGES a design
    /// has to be able to hand the result back, and it should hand it back in a form the tools the
    /// designer already uses will open.</para>
    ///
    /// <para>The file is the same DTO <see cref="LhltReader"/> reads, serialised with the same
    /// options, so what comes out goes back in unchanged. <c>LhltRoundTripTests</c> holds that to
    /// account rather than leaving it as an intention.</para>
    /// </summary>
    public static class LhltWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = true,
        };

        public static void Write(OpticalSystem system, string path)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (path == null) throw new ArgumentNullException(nameof(path));

            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(system));
        }

        public static string ToJson(OpticalSystem system) =>
            JsonSerializer.Serialize(ToLhltFile(system), JsonOptions);

        public static LhltFile ToLhltFile(OpticalSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            var file = new LhltFile
            {
                FormatVersion = 1,
                Title = system.Title ?? string.Empty,
                Notes = system.Notes ?? string.Empty,
                Designer = system.Designer ?? string.Empty,
                Aperture = new LhltAperture
                {
                    Type = system.Aperture.Type,
                    Value = system.Aperture.Value,
                },
                FieldType = system.FieldType,
                RayAiming = system.RayAiming,
                IsAfocal = system.IsAfocal,
                TelecentricObjectSpace = system.TelecentricObjectSpace,
                SemiDiameterSolve = system.SemiDiameterSolve,
                GlassCatalogs = new List<string>(system.GlassCatalogs),
            };

            foreach (var s in system.Surfaces)
            {
                var ls = new LhltSurface
                {
                    Index = s.Index,
                    Type = s.Type,
                    Comment = s.Comment ?? string.Empty,
                    Radius = s.Radius,
                    Thickness = s.Thickness,
                    Material = s.Material ?? string.Empty,
                    SemiDiameter = s.SemiDiameter,
                    SemiDiameterMode = s.SemiDiameterMode,
                    ClearAperturePercent = s.ClearAperturePercent,
                    Conic = s.Conic,
                    IsStop = s.IsStop,
                    InnerRadius = s.InnerRadius,
                    ObscurationRadius = s.ObscurationRadius,
                    FloatingApertureRadius = s.FloatingApertureRadius,
                    HasMarginalRaySolve = s.HasMarginalRaySolve,
                    ModelIndexEnabled = s.ModelIndexEnabled,
                    ModelNd = s.ModelNd,
                    ModelVd = s.ModelVd,
                    ModelDPgF = s.ModelDPgF,
                    FocalLength = s.FocalLength,
                    AsphericCoefficients = NonZeroOrNull(s.AsphericCoefficients),
                    Parameters = NonZeroOrNull(s.Parameters),
                    Settings = NonZeroOrNull(s.Settings),
                };
                file.Surfaces.Add(ls);
            }

            foreach (var w in system.Wavelengths)
                file.Wavelengths.Add(new LhltWavelength
                {
                    Value = w.Value,
                    Weight = w.Weight,
                    IsPrimary = w.IsPrimary,
                });

            foreach (var f in system.Fields)
                file.Fields.Add(new LhltField { Y = f.Y, Weight = f.Weight });

            foreach (var p in system.Pickups)
                file.Pickups.Add(new LhltPickup
                {
                    TargetSurfaceIndex = p.TargetSurfaceIndex,
                    Parameter = p.Parameter,
                    SourceSurfaceIndex = p.SourceSurfaceIndex,
                    SourceConfigurationIndex = p.SourceConfigurationIndex,
                    ScaleFactor = p.ScaleFactor,
                    Offset = p.Offset,
                    ParameterIndex = p.ParameterIndex,
                });

            return file;
        }

        /// <summary>
        /// An array of zeros is written as nothing at all, which is what the reader expects and
        /// what keeps a file of spherical surfaces free of eight zeros per surface.
        /// </summary>
        private static double[]? NonZeroOrNull(double[] values)
        {
            foreach (double v in values) if (v != 0.0) return (double[])values.Clone();
            return null;
        }

        private static int[]? NonZeroOrNull(int[] values)
        {
            foreach (int v in values) if (v != 0) return (int[])values.Clone();
            return null;
        }
    }
}
