using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Calculates chromatic aberrations (longitudinal and lateral color).
/// </summary>
public class ChromaticAberrationCalculator
{
    private readonly ParaxialRayTracer _tracer = new();

    // Standard spectral lines (wavelengths in microns)
    public static readonly double WavelengthF = 0.4861;  // F line (blue, hydrogen)
    public static readonly double WavelengthD = 0.5876;  // d line (yellow, helium)
    public static readonly double WavelengthC = 0.6563;  // C line (red, hydrogen)

    /// <summary>
    /// Calculates chromatic aberrations for an optical system using its defined fields.
    /// Uses the maximum field value from the system's field definitions.
    /// </summary>
    /// <param name="system">The optical system to analyze.</param>
    /// <returns>Chromatic aberration results.</returns>
    public ChromaticAberrationResult Calculate(OpticalSystem system)
    {
        // Get the maximum field value from the system
        var (fieldValue, fieldType) = GetMaxFieldValue(system);
        return CalculateInternal(system, fieldValue, fieldType);
    }

    /// <summary>
    /// Calculates chromatic aberrations for an optical system.
    /// </summary>
    /// <param name="system">The optical system to analyze.</param>
    /// <param name="fieldAngleDegrees">Field angle for lateral color calculation (degrees).</param>
    /// <returns>Chromatic aberration results.</returns>
    public ChromaticAberrationResult Calculate(OpticalSystem system, double fieldAngleDegrees)
    {
        return CalculateInternal(system, fieldAngleDegrees, FieldType.Angle);
    }

    /// <summary>
    /// Calculates chromatic aberrations with explicit field type specification.
    /// </summary>
    /// <param name="system">The optical system to analyze.</param>
    /// <param name="fieldValue">The field value.</param>
    /// <param name="fieldType">The type of field specification.</param>
    /// <returns>Chromatic aberration results.</returns>
    public ChromaticAberrationResult Calculate(OpticalSystem system, double fieldValue, FieldType fieldType)
    {
        return CalculateInternal(system, fieldValue, fieldType);
    }

    private ChromaticAberrationResult CalculateInternal(OpticalSystem system, double fieldValue, FieldType fieldType)
    {
        // Calculate EFL at each wavelength
        double eflF = _tracer.CalculateEfl(system, WavelengthF);
        double eflD = _tracer.CalculateEfl(system, WavelengthD);
        double eflC = _tracer.CalculateEfl(system, WavelengthC);

        // Longitudinal chromatic aberration (axial color)
        // Positive means blue focuses shorter than red
        double longitudinalColor = eflF - eflC;

        // Calculate BFL at each wavelength for focus shift
        double bflF = _tracer.CalculateBfl(system, WavelengthF);
        double bflD = _tracer.CalculateBfl(system, WavelengthD);
        double bflC = _tracer.CalculateBfl(system, WavelengthC);

        double focusShift = bflF - bflC;

        // Convert field to angle if necessary for lateral color calculation
        double fieldAngleDegrees = ConvertFieldToAngle(fieldValue, fieldType, eflD);

        // Lateral color (chromatic difference of magnification)
        double lateralColor = 0;
        double imageHeightF = 0, imageHeightD = 0, imageHeightC = 0;

        if (Math.Abs(fieldAngleDegrees) > 1e-10)
        {
            imageHeightF = _tracer.CalculateImageHeight(system, fieldAngleDegrees, WavelengthF);
            imageHeightD = _tracer.CalculateImageHeight(system, fieldAngleDegrees, WavelengthD);
            imageHeightC = _tracer.CalculateImageHeight(system, fieldAngleDegrees, WavelengthC);

            lateralColor = imageHeightF - imageHeightC;
        }

        // Secondary spectrum (deviation from linear dispersion)
        // For an achromat, eflF = eflC, but eflD may differ
        double secondarySpectrum = 0;
        if (Math.Abs(eflF - eflC) < Math.Abs(longitudinalColor) * 0.1) // Nearly achromatic
        {
            secondarySpectrum = eflD - (eflF + eflC) / 2.0;
        }

        return new ChromaticAberrationResult
        {
            EflF = eflF,
            EflD = eflD,
            EflC = eflC,
            BflF = bflF,
            BflD = bflD,
            BflC = bflC,
            LongitudinalColor = longitudinalColor,
            FocusShift = focusShift,
            LateralColor = lateralColor,
            SecondarySpectrum = secondarySpectrum,
            ImageHeightF = imageHeightF,
            ImageHeightD = imageHeightD,
            ImageHeightC = imageHeightC,
            FieldAngle = fieldAngleDegrees,
            FieldValue = fieldValue,
            FieldType = fieldType
        };
    }

    /// <summary>
    /// Gets the maximum field value from the system's field definitions.
    /// </summary>
    private static (double value, FieldType type) GetMaxFieldValue(OpticalSystem system)
    {
        if (system.Fields == null || system.Fields.Count == 0)
        {
            return (0, FieldType.Angle);
        }

        var maxField = system.Fields.MaxBy(f => Math.Max(Math.Abs(f.X), Math.Abs(f.Y)));
        if (maxField == null)
        {
            return (0, FieldType.Angle);
        }

        double maxValue = Math.Max(Math.Abs(maxField.X), Math.Abs(maxField.Y));
        return (maxValue, maxField.FieldType);
    }

    /// <summary>
    /// Converts a field value to angle in degrees based on field type.
    /// </summary>
    /// <param name="fieldValue">The field value.</param>
    /// <param name="fieldType">The field type.</param>
    /// <param name="efl">Effective focal length (for height-to-angle conversion).</param>
    /// <returns>Field angle in degrees.</returns>
    private static double ConvertFieldToAngle(double fieldValue, FieldType fieldType, double efl)
    {
        if (Math.Abs(fieldValue) < 1e-12) return 0;

        return fieldType switch
        {
            FieldType.Angle => fieldValue, // Already in degrees
            FieldType.ObjectHeight => ConvertObjectHeightToAngle(fieldValue, efl),
            FieldType.ParaxialImageHeight => ConvertImageHeightToAngle(fieldValue, efl),
            FieldType.ImageHeight => ConvertImageHeightToAngle(fieldValue, efl),
            _ => fieldValue // Default to treating as angle
        };
    }

    /// <summary>
    /// Converts object height to field angle.
    /// For finite conjugate: angle = atan(objectHeight / objectDistance)
    /// For infinite conjugate: needs EFL, angle ≈ atan(objectHeight / EFL)
    /// </summary>
    private static double ConvertObjectHeightToAngle(double objectHeight, double efl)
    {
        if (!double.IsFinite(efl) || Math.Abs(efl) < 1e-10) return 0;

        // For infinite conjugate systems, object height defines the angular field
        // The angle is approximately atan(h/f) in the paraxial approximation
        double angleRad = Math.Atan(objectHeight / Math.Abs(efl));
        return angleRad * 180.0 / Math.PI;
    }

    /// <summary>
    /// Converts paraxial image height to field angle.
    /// h' = -f * tan(θ) for infinite conjugate → θ = atan(h'/f)
    /// </summary>
    private static double ConvertImageHeightToAngle(double imageHeight, double efl)
    {
        if (!double.IsFinite(efl) || Math.Abs(efl) < 1e-10) return 0;

        // For paraxial image height: h' = f * tan(θ) → θ = atan(h'/f)
        double angleRad = Math.Atan(Math.Abs(imageHeight) / Math.Abs(efl));
        return angleRad * 180.0 / Math.PI;
    }

    /// <summary>
    /// Calculates the theoretical achromatic condition for a doublet.
    /// For achromatism: φ₁/V₁ + φ₂/V₂ = 0
    /// </summary>
    /// <param name="totalPower">Total system power.</param>
    /// <param name="V1">Abbe number of first glass.</param>
    /// <param name="V2">Abbe number of second glass.</param>
    /// <returns>Power ratio (φ₁/φ_total) for achromatic condition.</returns>
    public static double CalculateAchromaticPowerRatio(double totalPower, double V1, double V2)
    {
        // φ₁/V₁ + φ₂/V₂ = 0
        // φ₁/V₁ + (φ_total - φ₁)/V₂ = 0
        // φ₁(1/V₁ - 1/V₂) = -φ_total/V₂
        // φ₁ = -φ_total/V₂ / (1/V₁ - 1/V₂)
        // φ₁ = φ_total * V₁ / (V₁ - V₂)

        if (Math.Abs(V1 - V2) < 1e-10)
        {
            return 0.5; // Same glass, can't achromatize
        }

        double ratio = V1 / (V1 - V2);

        // Clamp to reasonable range
        return Math.Clamp(ratio, 0.1, 0.9);
    }

    /// <summary>
    /// Estimates chromatic aberration for a thin lens doublet without full ray tracing.
    /// </summary>
    /// <param name="phi1">Power of first element.</param>
    /// <param name="phi2">Power of second element.</param>
    /// <param name="V1">Abbe number of first glass.</param>
    /// <param name="V2">Abbe number of second glass.</param>
    /// <param name="efl">Effective focal length at d-line.</param>
    /// <returns>Estimated longitudinal color.</returns>
    public static double EstimateLongitudinalColor(double phi1, double phi2, double V1, double V2, double efl)
    {
        // Longitudinal color ≈ -f² * (φ₁/V₁ + φ₂/V₂)
        double chromaticPower = phi1 / V1 + phi2 / V2;
        return -efl * efl * chromaticPower;
    }
}

/// <summary>
/// Results from chromatic aberration calculation.
/// </summary>
public class ChromaticAberrationResult
{
    // EFL at each wavelength
    public double EflF { get; set; }
    public double EflD { get; set; }
    public double EflC { get; set; }

    // BFL at each wavelength
    public double BflF { get; set; }
    public double BflD { get; set; }
    public double BflC { get; set; }

    /// <summary>
    /// Longitudinal chromatic aberration (EFL_F - EFL_C).
    /// Positive means blue focuses shorter than red.
    /// </summary>
    public double LongitudinalColor { get; set; }

    /// <summary>
    /// Focus shift (BFL_F - BFL_C).
    /// </summary>
    public double FocusShift { get; set; }

    /// <summary>
    /// Lateral chromatic aberration (image height difference F - C).
    /// </summary>
    public double LateralColor { get; set; }

    /// <summary>
    /// Secondary spectrum (deviation from linear dispersion).
    /// </summary>
    public double SecondarySpectrum { get; set; }

    // Image heights at each wavelength
    public double ImageHeightF { get; set; }
    public double ImageHeightD { get; set; }
    public double ImageHeightC { get; set; }

    /// <summary>
    /// Field angle used for lateral color calculation (in degrees).
    /// </summary>
    public double FieldAngle { get; set; }

    /// <summary>
    /// Original field value from the system definition.
    /// </summary>
    public double FieldValue { get; set; }

    /// <summary>
    /// Type of field specification used.
    /// </summary>
    public FieldType FieldType { get; set; }

    /// <summary>
    /// Whether the system is approximately achromatic (|longitudinal color| < 0.1mm).
    /// </summary>
    public bool IsAchromatic => Math.Abs(LongitudinalColor) < 0.1;

    public override string ToString()
    {
        return $"Longitudinal={LongitudinalColor:F4}mm, Lateral={LateralColor:F4}mm, " +
               $"EFL(F/d/C)={EflF:F2}/{EflD:F2}/{EflC:F2}mm";
    }
}
