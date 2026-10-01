using System.Collections.Generic;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Data transfer object representing the complete state a .lhlt file carries.
    /// Serialized as JSON with the .lhlt extension.
    /// </summary>
    public class LhltFile
    {
        public int FormatVersion { get; set; } = 1;
        public string Title { get; set; } = string.Empty;

        /// <summary>Free-form multi-line description (newline-joined).</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>Designer / source attribution.</summary>
        public string Designer { get; set; } = string.Empty;

        // Optical system
        public LhltAperture Aperture { get; set; } = new LhltAperture();
        public FieldType FieldType { get; set; }
        public List<LhltSurface> Surfaces { get; set; } = new List<LhltSurface>();
        public List<LhltWavelength> Wavelengths { get; set; } = new List<LhltWavelength>();
        public List<LhltField> Fields { get; set; } = new List<LhltField>();
        public List<LhltPickup> Pickups { get; set; } = new List<LhltPickup>();
        public RayAimingMode RayAiming { get; set; }
        public bool IsAfocal { get; set; }
        /// <summary>Object-space telecentric (entrance pupil at infinity). Only meaningful with the
        /// Object Space NA aperture. Default false = conventional (non-telecentric).</summary>
        public bool TelecentricObjectSpace { get; set; }
        /// <summary>
        /// When true, the merit-function evaluator emits a stiff per-ray
        /// penalty for every vignetted ray (on- AND off-axis). Default false
        /// preserves the legacy "off-axis vignetting is free" behavior. See
        /// OpticalSystem.PenalizeVignetting for the runtime semantics.
        /// </summary>

        /// <summary>
        /// How Auto semi-diameters are derived: from real traced rays (RealRay, the default and
        /// the historical behaviour) or from the paraxial beam footprint (Paraxial). SYSTEM-WIDE,
        /// and orthogonal to the per-surface Auto/Fixed mode — Fixed surfaces are unaffected.
        /// </summary>
        /// <remarks>
        /// Absent from files written before 1.0.152, where it deserializes to RealRay (0) and the
        /// design behaves exactly as it always did. Serialized by NAME via JsonStringEnumConverter,
        /// so the enum can be reordered without breaking saved files.
        /// </remarks>
        public SemiDiameterSolve SemiDiameterSolve { get; set; } = SemiDiameterSolve.RealRay;

        /// <summary>
        /// When true, per-field vignetting factors are auto-computed after each semi-diameter solve
        /// and applied as an entrance-pupil remap. The factors themselves are derived state and are
        /// NOT serialized — only this flag is; the factors are recomputed on load. See
        /// OpticalSystem.UseAutomaticVignettingFactors.
        /// </summary>
        public List<string> GlassCatalogs { get; set; } = new List<string>();

        // Glass substitution settings

        // Multi-configuration (MCE) is an advanced-edition feature — the shared .lhlt
        // format is single-config. The advanced edition persists configurations separately.
    }

    public class LhltAperture
    {
        public ApertureType Type { get; set; }
        public double Value { get; set; }
    }

    public class LhltSurface
    {
        public int Index { get; set; }
        public SurfaceType Type { get; set; }
        public string Comment { get; set; } = string.Empty;
        public double Radius { get; set; } = double.PositiveInfinity;
        public double Thickness { get; set; }
        public string Material { get; set; } = string.Empty;
        public double SemiDiameter { get; set; }
        public SemiDiameterMode SemiDiameterMode { get; set; }
        public double ClearAperturePercent { get; set; } = 100.0;
        public double Conic { get; set; }
        public bool IsStop { get; set; }

        // Aperture properties
        public double InnerRadius { get; set; }
        public double ObscurationRadius { get; set; }
        public double FloatingApertureRadius { get; set; }

        // Aspheric coefficients (only serialized if non-zero)
        public double[]? AsphericCoefficients { get; set; }

        // ── Variable flags ───────────────────────────────────────────────────────────
        //
        // Which construction parameters an optimiser is allowed to move. These are the format's
        // own, and they were read past for as long as this program only reported on a design.
        // Now that it can change one, they are the design's own statement of what may change and
        // are honoured rather than re-invented: a lens opened here arrives with its variables
        // already declared, exactly as its author left them.
        //
        // The kinds this optimiser cannot use are still deserialized, so that a file carrying
        // them keeps them when it is written back.

        public bool CurvatureVariable { get; set; }
        public bool ThicknessVariable { get; set; }
        public bool ConicVariable { get; set; }
        public bool[]? AsphericVariable { get; set; }
        public bool SemiDiameterVariable { get; set; }
        public bool ClearAperturePercentVariable { get; set; }
        public bool FocalLengthVariable { get; set; }
        public bool ModelNdVariable { get; set; }
        public bool ModelVdVariable { get; set; }
        public bool ModelDPgFVariable { get; set; }

        /// <summary>OSLO CALLBACK 1 marginal-ray-height solve marker.</summary>
        public bool HasMarginalRaySolve { get; set; }

        // ── Variable bounds ──────────────────────────────────────────────────────────
        // Absent from a file means unbounded, which is why these are nullable rather than
        // defaulted to an infinity that would then be written back into a file that never
        // asked for one.

        public double? CurvatureMin { get; set; }
        public double? CurvatureMax { get; set; }
        public double? ThicknessMin { get; set; }
        public double? ThicknessMax { get; set; }

        // Model glass (Nd/Vd/dPgF) — when enabled the refractive index is computed
        // from these three parameters instead of a catalog Material. Each can be a
        // Variable (bounds below) or a Pickup (carried in the pickups list).
        public bool ModelIndexEnabled { get; set; }
        public double ModelNd { get; set; }
        public double ModelVd { get; set; }
        public double ModelDPgF { get; set; }

        // Paraxial (ideal thin lens) — only meaningful when Type == Paraxial.
        // FocalLength = the single shape parameter f (PositiveInfinity = zero power).
        // Can be a Variable (bounds below) or a Pickup (carried in the pickups list).
        public double FocalLength { get; set; } = double.PositiveInfinity;
        // Optimization bounds are on the power (diopters), not the focal length.

        // Generic indexed parameters for PRO surface types (Coordinate Break, …).
        // Null when unused (standard surfaces) to keep files clean. 0-based arrays;
        // the file format numbers them from one. See Surface.Parameters/Settings.
        public double[]? Parameters { get; set; }
        public int[]? Settings { get; set; }
    }

    public class LhltWavelength
    {
        public double Value { get; set; }
        public double Weight { get; set; } = 1.0;
        public bool IsPrimary { get; set; }
    }

    public class LhltField
    {
        public double Y { get; set; }
        public double Weight { get; set; } = 1.0;
    }

    public class LhltPickup
    {
        public int TargetSurfaceIndex { get; set; }
        public PickupParameter Parameter { get; set; }
        public int SourceSurfaceIndex { get; set; }
        public int SourceConfigurationIndex { get; set; } = -1;
        public double ScaleFactor { get; set; } = 1.0;
        public double Offset { get; set; }
        // 0-based generic-parameter slot; only used when Parameter == SurfaceParameter
        // (PRO Coordinate Break). Defaults to 0 for all legacy single-value pickups.
        public int ParameterIndex { get; set; }
    }

}
