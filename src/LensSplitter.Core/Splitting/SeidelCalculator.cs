using LensSplitter.Core.Aberrations;
using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Calculates all five Seidel aberration coefficients (S1-S5) plus chromatic aberrations
/// using surface-by-surface ray tracing.
/// </summary>
public class SeidelCalculator
{
    private readonly ParaxialRayTracer _tracer = new();

    /// <summary>
    /// Returns diagnostic information about the paraxial ray data used for Seidel calculations.
    /// Useful for debugging aberration issues.
    /// </summary>
    public string GetDiagnostics(OpticalSystem system, double wavelengthMicrons)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Seidel Calculation Diagnostics ===");
        sb.AppendLine();

        // System info
        sb.AppendLine($"Surfaces: {system.Surfaces.Count}");
        sb.AppendLine($"Stop index: {system.StopIndex}");
        sb.AppendLine($"Aperture type: {system.ApertureType}");
        sb.AppendLine($"Aperture value: {system.ApertureValue}");
        sb.AppendLine($"Object thickness: {system.Surfaces[0].Thickness}");

        // Entrance pupil
        var (epPosition, epRadius) = _tracer.CalculateEntrancePupil(system, wavelengthMicrons);
        sb.AppendLine();
        sb.AppendLine($"EP position (from surface 1): {epPosition:F4} mm");
        sb.AppendLine($"EP radius: {epRadius:F4} mm");

        // Get field info
        FieldType fieldType = FieldType.Angle;
        double fieldValue = GetMaxFieldValue(system, out fieldType);
        if (Math.Abs(fieldValue) < 1e-10) fieldValue = 1.0;
        sb.AppendLine();
        sb.AppendLine($"Field type: {fieldType}");
        sb.AppendLine($"Field value: {fieldValue}");

        // Trace rays
        var marginalRay = _tracer.TraceMarginalRay(system, wavelengthMicrons);
        var field = new Field(0, fieldValue, fieldType);
        var chiefRay = _tracer.TraceChiefRay(system, field, wavelengthMicrons);

        sb.AppendLine();
        sb.AppendLine("Marginal ray heights at surfaces:");
        for (int i = 0; i < Math.Min(marginalRay.States.Count, 5); i++)
        {
            var state = marginalRay.States[i];
            sb.AppendLine($"  Surface {state.SurfaceIndex}: h={state.Height:F4}, u={state.Slope / state.RefractiveIndex:F6}");
        }

        sb.AppendLine();
        sb.AppendLine("Chief ray heights at surfaces:");
        for (int i = 0; i < Math.Min(chiefRay.States.Count, 5); i++)
        {
            var state = chiefRay.States[i];
            sb.AppendLine($"  Surface {state.SurfaceIndex}: h_bar={state.Height:F4}, u_bar={state.Slope / state.RefractiveIndex:F6}");
        }

        // Surface curvatures
        sb.AppendLine();
        sb.AppendLine("Surface curvatures:");
        for (int i = 1; i < Math.Min(system.Surfaces.Count - 1, 5); i++)
        {
            var surf = system.Surfaces[i];
            double c = surf.IsFlat || double.IsInfinity(surf.Radius) ? 0 : 1.0 / surf.Radius;
            sb.AppendLine($"  Surface {i}: R={surf.Radius:F2}, c={c:F6}");
        }

        // Refraction invariants at first optical surface
        if (marginalRay.States.Count >= 2 && chiefRay.States.Count >= 2 && system.Surfaces.Count >= 3)
        {
            var mState = marginalRay.States[1];
            var cState = chiefRay.States[1];
            var prevSurf = system.Surfaces[0];
            var surf = system.Surfaces[1];

            double n1 = prevSurf.GetRefractiveIndex(wavelengthMicrons);
            double c = surf.IsFlat || double.IsInfinity(surf.Radius) ? 0 : 1.0 / surf.Radius;
            double u_m = marginalRay.States[0].Slope / marginalRay.States[0].RefractiveIndex;
            double u_c = chiefRay.States[0].Slope / chiefRay.States[0].RefractiveIndex;
            double h = mState.Height;
            double h_bar = cState.Height;

            double A = n1 * (h * c + u_m);
            double A_bar = n1 * (h_bar * c + u_c);

            sb.AppendLine();
            sb.AppendLine("At surface 1:");
            sb.AppendLine($"  n1={n1:F4}, c={c:F6}");
            sb.AppendLine($"  h={h:F4}, u={u_m:F6}");
            sb.AppendLine($"  h_bar={h_bar:F4}, u_bar={u_c:F6}");
            sb.AppendLine($"  A={A:F6}");
            sb.AppendLine($"  A_bar={A_bar:F6}");
            if (Math.Abs(A) > 1e-10)
                sb.AppendLine($"  A_bar/A={A_bar / A:F4}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Calculates all Seidel aberration coefficients for an optical system.
    /// </summary>
    /// <param name="system">The optical system to analyze.</param>
    /// <param name="wavelengthMicrons">Wavelength for calculation.</param>
    /// <param name="fieldValue">Field value for chief ray. If 0, uses system max field.</param>
    /// <returns>Complete Seidel aberration results.</returns>
    public SeidelResult Calculate(OpticalSystem system, double wavelengthMicrons, double fieldValue = 0)
    {
        // Determine field type and value
        FieldType fieldType = FieldType.Angle;
        if (Math.Abs(fieldValue) < 1e-10)
        {
            fieldValue = GetMaxFieldValue(system, out fieldType);
        }
        else if (system.Fields.Count > 0)
        {
            fieldType = system.Fields[0].FieldType;
        }

        // Only default to 1 degree if no fields are defined at all.
        // If the system explicitly defines field=0 (on-axis), respect it:
        // chief ray will have zero angle/height, giving S2=S3=S4=S5=0.
        if (Math.Abs(fieldValue) < 1e-10 && (system.Fields == null || system.Fields.Count == 0))
        {
            fieldValue = 1.0; // Default 1 degree if no field definitions
        }

        // For display, convert to angle equivalent
        double fieldAngleForDisplay = fieldValue;
        if (fieldType == FieldType.ObjectHeight)
        {
            double objDist = ParaxialRayTracer.GetObjectDistance(system);
            if (!double.IsInfinity(objDist) && Math.Abs(objDist) > 1e-10)
            {
                fieldAngleForDisplay = Math.Atan(fieldValue / Math.Abs(objDist)) * 180.0 / Math.PI;
            }
        }

        var result = new SeidelResult
        {
            Wavelength = wavelengthMicrons,
            FieldAngle = fieldAngleForDisplay
        };

        if (system.Surfaces.Count < 3)
        {
            return result; // Need at least object, one surface, and image
        }

        // Create field object with proper type
        var field = new Field(0, fieldValue, fieldType);

        // Trace marginal and chief rays
        var marginalRay = _tracer.TraceMarginalRay(system, wavelengthMicrons);
        var chiefRay = _tracer.TraceChiefRay(system, field, wavelengthMicrons);

        if (marginalRay.States.Count < 2 || chiefRay.States.Count < 2)
        {
            return result;
        }

        // Get wavelength indices for chromatic calculation
        var (shortWavelength, longWavelength) = GetChromaticWavelengths(system, wavelengthMicrons);

        // Track cumulative sums
        double s1_total = 0, s2_total = 0, s3_total = 0, s4_total = 0, s5_total = 0;
        double cl_total = 0, ct_total = 0;

        // Store final ray angles for transverse aberration calculation
        double uf = 0, nf = 1;

        // Process each optical surface (skip object at index 0 and image at last index)
        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            var surface = system.Surfaces[i];
            var prevSurface = system.Surfaces[i - 1];

            // Get refractive indices
            double n1 = prevSurface.GetRefractiveIndex(wavelengthMicrons);
            double n2 = surface.GetRefractiveIndex(wavelengthMicrons);

            // Get ray data at this surface
            if (i >= marginalRay.States.Count || i >= chiefRay.States.Count)
                continue;

            var marginalState = marginalRay.States[i];
            var chiefState = chiefRay.States[i];

            // Get angles before refraction (from previous state)
            double u = i > 0 && i - 1 < marginalRay.States.Count
                ? marginalRay.States[i - 1].Slope / marginalRay.States[i - 1].RefractiveIndex
                : 0;
            double u_chief = i > 0 && i - 1 < chiefRay.States.Count
                ? chiefRay.States[i - 1].Slope / chiefRay.States[i - 1].RefractiveIndex
                : 0;

            // Get angles after refraction
            double u_prime = marginalState.Slope / marginalState.RefractiveIndex;
            double u_chief_prime = chiefState.Slope / chiefState.RefractiveIndex;

            // Ray heights at surface
            double h = marginalState.Height;
            double h_bar = chiefState.Height;

            // Surface curvature
            double c = surface.IsFlat || double.IsInfinity(surface.Radius)
                ? 0
                : 1.0 / surface.Radius;

            // Refraction invariants
            double A = n1 * (h * c + u);
            double A_bar = n1 * (h_bar * c + u_chief);

            // Avoid division by zero
            if (Math.Abs(A) < 1e-20)
                continue;

            double A_bar_over_A = A_bar / A;

            // Change in (u/n)
            double delta_up_np = u_prime / n2 - u / n1;

            // S1: Spherical aberration (spherical surface contribution)
            double s1 = -h * A * A * delta_up_np;

            // S2: Coma
            double s2 = s1 * A_bar_over_A;

            // Lagrange invariant at this surface
            double L = h * n1 * u_chief - h_bar * n1 * u;

            // S4: Petzval field curvature
            double s4 = -c * L * L * (1.0 / n2 - 1.0 / n1);

            // S3: Astigmatism
            double s3 = s2 * A_bar_over_A;

            // S5: Distortion
            double s5 = A_bar_over_A * (s3 + s4);

            // Conic (aspherical) contributions
            // For a conic surface with constant K, the aspherical deformation adds:
            //   a4 = K * (n' - n) * c^3  (note: some references include a negative sign,
            //   but Seidel convention with S1 = -h*A²*Δ(u/n) requires positive K*(n'-n)*c³)
            //   ΔS1 = a4 * h^4,  ΔS2 = a4 * h^3 * h_bar,  ΔS3 = a4 * h^2 * h_bar^2
            //   ΔS4 = 0,  ΔS5 = a4 * h * h_bar^3
            double K = surface.Conic;
            if (Math.Abs(K) > 1e-12 && Math.Abs(c) > 1e-12)
            {
                double a4 = K * (n2 - n1) * c * c * c;
                s1 += a4 * h * h * h * h;
                s2 += a4 * h * h * h * h_bar;
                s3 += a4 * h * h * h_bar * h_bar;
                // s4 unchanged (Petzval unaffected by asphericity)
                s5 += a4 * h * h_bar * h_bar * h_bar;
            }

            // Chromatic aberrations
            double cl = 0, ct = 0;
            if (Math.Abs(shortWavelength - longWavelength) > 0.001)
            {
                double n1_short = prevSurface.GetRefractiveIndex(shortWavelength);
                double n1_long = prevSurface.GetRefractiveIndex(longWavelength);
                double n2_short = surface.GetRefractiveIndex(shortWavelength);
                double n2_long = surface.GetRefractiveIndex(longWavelength);

                double n1_diff = Math.Abs(n1_short - n1_long);
                double n2_diff = Math.Abs(n2_short - n2_long);

                double delta_color = (n2_diff / n2) - (n1_diff / n1);

                // CL: Axial chromatic aberration
                cl = -A * h * delta_color;

                // CT: Lateral chromatic aberration
                ct = A_bar_over_A * cl;
            }

            // Store per-surface data
            result.SurfaceCoefficients.Add(new SeidelSurfaceCoefficients
            {
                SurfaceIndex = i,
                S1 = s1,
                S2 = s2,
                S3 = s3,
                S4 = s4,
                S5 = s5,
                CL = cl,
                CT = ct,
                Height = h,
                ChiefHeight = h_bar,
                A = A,
                A_bar = A_bar,
                Curvature = c
            });

            // Accumulate totals
            s1_total += s1;
            s2_total += s2;
            s3_total += s3;
            s4_total += s4;
            s5_total += s5;
            cl_total += cl;
            ct_total += ct;

            // Store final angles for last optical surface
            if (i == system.Surfaces.Count - 2)
            {
                uf = u_prime;
                nf = n2;
            }
        }

        // Store totals
        result.S1 = s1_total;
        result.S2 = s2_total;
        result.S3 = s3_total;
        result.S4 = s4_total;
        result.S5 = s5_total;
        result.CL = cl_total;
        result.CT = ct_total;

        // Calculate transverse aberrations (for reference)
        if (Math.Abs(uf) > 1e-12)
        {
            double factor = 1.0 / (2.0 * nf * uf);
            result.TransverseSpherical = -s1_total * factor;
            result.TransverseComa = -s2_total * factor;
            result.TransverseAstigmatism = -s3_total / (nf * uf);
            result.TransversePetzval = -s4_total * factor;
            result.TransverseDistortion = -s5_total * factor;
        }

        return result;
    }

    /// <summary>
    /// Calculates a weighted merit function from Seidel aberrations.
    /// </summary>
    /// <param name="result">The Seidel calculation result.</param>
    /// <param name="weights">Aberration weights.</param>
    /// <param name="buchdahl">Optional Buchdahl 5th-order result. When provided and weights are non-zero, adds Buchdahl terms.</param>
    /// <returns>Weighted merit function value.</returns>
    public static double CalculateMeritFunction(SeidelResult result, AberrationWeights weights, BuchdahlResult? buchdahl = null)
    {
        double merit = weights.W1 * Math.Abs(result.S1) +
               weights.W2 * Math.Abs(result.S2) +
               weights.W3 * Math.Abs(result.S3) +
               weights.W4 * Math.Abs(result.S4) +
               weights.W5 * Math.Abs(result.S5) +
               weights.WCL * Math.Abs(result.CL) +
               weights.WCT * Math.Abs(result.CT);

        if (buchdahl != null && weights.IncludeBuchdahl && weights.HasNonZeroBuchdahlWeights)
        {
            // Buchdahl coefficients are computed in EFL-normalized coordinates.
            // Divide by EFL to bring them onto the same scale as Seidel coefficients.
            double eflNorm = Math.Abs(buchdahl.Efl) > 1e-6 ? Math.Abs(buchdahl.Efl) : 1.0;
            merit += (weights.WBSph * Math.Abs(buchdahl.ApEffective) +
                      weights.WBCma * Math.Abs(buchdahl.AqEffective) +
                      weights.WBObl * Math.Abs(buchdahl.BpEffective) +
                      weights.WBEll * Math.Abs(buchdahl.BqEffective) +
                      weights.WBAst * Math.Abs(buchdahl.CpEffective) +
                      weights.WBDst * Math.Abs(buchdahl.CqEffective)) / eflNorm;
        }

        return merit;
    }

    private static double GetMaxFieldValue(OpticalSystem system, out FieldType fieldType)
    {
        fieldType = FieldType.Angle;

        if (system.Fields == null || system.Fields.Count == 0)
            return 0;

        double maxField = 0;
        foreach (var field in system.Fields)
        {
            double fieldVal = Math.Max(Math.Abs(field.X), Math.Abs(field.Y));
            if (fieldVal > maxField)
            {
                maxField = fieldVal;
                fieldType = field.FieldType;
            }
        }
        return maxField;
    }

    private static double GetMaxFieldAngle(OpticalSystem system)
    {
        double maxField = GetMaxFieldValue(system, out FieldType fieldType);

        // If field is not angle type, we need to convert or use a default
        if (fieldType != FieldType.Angle)
        {
            // For object height, convert using object distance
            if (fieldType == FieldType.ObjectHeight)
            {
                double objectDistance = Paraxial.ParaxialRayTracer.GetObjectDistance(system);
                if (!double.IsInfinity(objectDistance) && Math.Abs(objectDistance) > 1e-10)
                {
                    // angle = atan(height / distance)
                    return Math.Atan(maxField / Math.Abs(objectDistance)) * 180.0 / Math.PI;
                }
            }
            // For image height types, use the value directly as a proxy
            // (proper conversion would require knowing magnification)
            return maxField > 0 ? maxField : 0;
        }

        return maxField;
    }

    private static (double shortWave, double longWave) GetChromaticWavelengths(OpticalSystem system, double primaryWavelength)
    {
        // Standard F and C lines
        const double wavelengthF = 0.4861;
        const double wavelengthC = 0.6563;

        if (system.Wavelengths == null || system.Wavelengths.Count < 2)
        {
            return (wavelengthF, wavelengthC);
        }

        // Find shortest and longest wavelengths in system
        double minWave = system.Wavelengths.Min(w => w.ValueMicrons);
        double maxWave = system.Wavelengths.Max(w => w.ValueMicrons);

        if (Math.Abs(maxWave - minWave) < 0.001)
        {
            return (wavelengthF, wavelengthC);
        }

        return (minWave, maxWave);
    }
}

/// <summary>
/// Complete Seidel aberration calculation result.
/// </summary>
public class SeidelResult
{
    public double Wavelength { get; set; }
    public double FieldAngle { get; set; }

    // Total Seidel sums
    public double S1 { get; set; } // Spherical aberration
    public double S2 { get; set; } // Coma
    public double S3 { get; set; } // Astigmatism
    public double S4 { get; set; } // Petzval field curvature
    public double S5 { get; set; } // Distortion

    // Chromatic aberrations
    public double CL { get; set; } // Axial (longitudinal) chromatic
    public double CT { get; set; } // Transverse (lateral) chromatic

    // Transverse aberrations (in mm, for reference)
    public double TransverseSpherical { get; set; }
    public double TransverseComa { get; set; }
    public double TransverseAstigmatism { get; set; }
    public double TransversePetzval { get; set; }
    public double TransverseDistortion { get; set; }

    // Per-surface coefficients
    public List<SeidelSurfaceCoefficients> SurfaceCoefficients { get; set; } = new();

    /// <summary>
    /// Gets the total aberration magnitude (RMS of all Seidels).
    /// </summary>
    public double TotalMagnitude => Math.Sqrt(S1 * S1 + S2 * S2 + S3 * S3 + S4 * S4 + S5 * S5);

    public override string ToString()
    {
        return $"S1={S1:E3}, S2={S2:E3}, S3={S3:E3}, S4={S4:E3}, S5={S5:E3}, CL={CL:E3}, CT={CT:E3}";
    }
}

/// <summary>
/// Per-surface Seidel coefficients.
/// </summary>
public class SeidelSurfaceCoefficients
{
    public int SurfaceIndex { get; set; }
    public double S1 { get; set; }
    public double S2 { get; set; }
    public double S3 { get; set; }
    public double S4 { get; set; }
    public double S5 { get; set; }
    public double CL { get; set; }
    public double CT { get; set; }

    // Intermediate values for debugging
    public double Height { get; set; }
    public double ChiefHeight { get; set; }
    public double A { get; set; }
    public double A_bar { get; set; }
    public double Curvature { get; set; }
}

/// <summary>
/// Weights for each aberration type in the merit function.
/// </summary>
public class AberrationWeights
{
    /// <summary>
    /// Weight for spherical aberration (S1).
    /// </summary>
    public double W1 { get; set; } = 1.0;

    /// <summary>
    /// Weight for coma (S2).
    /// </summary>
    public double W2 { get; set; } = 1.0;

    /// <summary>
    /// Weight for astigmatism (S3).
    /// </summary>
    public double W3 { get; set; } = 1.0;

    /// <summary>
    /// Weight for Petzval field curvature (S4). Default 1.0.
    /// </summary>
    public double W4 { get; set; } = 1.0;

    /// <summary>
    /// Weight for distortion (S5).
    /// </summary>
    public double W5 { get; set; } = 0.0;

    /// <summary>
    /// Weight for longitudinal (axial) chromatic aberration.
    /// Default 1.0 when chromatic aberrations are included.
    /// </summary>
    public double WCL { get; set; } = 1.0;

    /// <summary>
    /// Weight for lateral (transverse) chromatic aberration.
    /// Default 1.0 when chromatic aberrations are included.
    /// </summary>
    public double WCT { get; set; } = 1.0;

    /// <summary>
    /// Whether chromatic aberrations are included in the merit function.
    /// Set to false automatically when only one wavelength is available.
    /// </summary>
    public bool IncludeChromatic { get; set; } = true;

    /// <summary>
    /// Whether Buchdahl 5th-order aberrations are included in the merit function.
    /// </summary>
    public bool IncludeBuchdahl { get; set; } = true;

    /// <summary>
    /// Weight for Buchdahl 5th-order spherical aberration (Ap).
    /// </summary>
    public double WBSph { get; set; } = 1.0;

    /// <summary>
    /// Weight for Buchdahl 5th-order coma (Aq).
    /// </summary>
    public double WBCma { get; set; } = 1.0;

    /// <summary>
    /// Weight for Buchdahl 5th-order oblique spherical aberration (Bp).
    /// </summary>
    public double WBObl { get; set; } = 1.0;

    /// <summary>
    /// Weight for Buchdahl 5th-order elliptical coma (Bq).
    /// </summary>
    public double WBEll { get; set; } = 1.0;

    /// <summary>
    /// Weight for Buchdahl 5th-order astigmatism (Cp).
    /// </summary>
    public double WBAst { get; set; } = 1.0;

    /// <summary>
    /// Weight for Buchdahl 5th-order distortion (Cq).
    /// </summary>
    public double WBDst { get; set; } = 0.0;

    /// <summary>
    /// Creates default weights: all non-chromatic weights 1.0, distortion 0.0.
    /// Includes both chromatic and Buchdahl aberrations by default.
    /// </summary>
    public static AberrationWeights Default => new();

    /// <summary>
    /// Creates default weights without chromatic aberrations.
    /// Use when only one wavelength is defined.
    /// </summary>
    public static AberrationWeights DefaultMonochromatic => new()
    {
        WCL = 0.0,
        WCT = 0.0,
        IncludeChromatic = false
    };

    /// <summary>
    /// Creates weights with distortion set to zero (both Seidel and Buchdahl).
    /// </summary>
    public static AberrationWeights NoDistortion => new() { W5 = 0.0, WBDst = 0.0 };

    /// <summary>
    /// Creates equal weights for all Seidel aberrations (no chromatic).
    /// </summary>
    public static AberrationWeights Equal => new()
    {
        W1 = 1.0,
        W2 = 1.0,
        W3 = 1.0,
        W4 = 1.0,
        W5 = 1.0,
        WCL = 0.0,
        WCT = 0.0,
        IncludeChromatic = false
    };

    /// <summary>
    /// Creates weights focusing only on spherical aberration (3rd and 5th order).
    /// </summary>
    public static AberrationWeights SphericalOnly => new()
    {
        W1 = 1.0,
        W2 = 0.0,
        W3 = 0.0,
        W4 = 0.0,
        W5 = 0.0,
        WCL = 0.0,
        WCT = 0.0,
        IncludeChromatic = false,
        WBSph = 1.0,
        WBCma = 0.0,
        WBObl = 0.0,
        WBEll = 0.0,
        WBAst = 0.0,
        WBDst = 0.0
    };

    /// <summary>
    /// Returns a copy of these weights with chromatic aberrations disabled.
    /// </summary>
    public AberrationWeights WithoutChromatic() => new()
    {
        W1 = W1,
        W2 = W2,
        W3 = W3,
        W4 = W4,
        W5 = W5,
        WCL = 0.0,
        WCT = 0.0,
        IncludeChromatic = false,
        IncludeBuchdahl = IncludeBuchdahl,
        WBSph = WBSph,
        WBCma = WBCma,
        WBObl = WBObl,
        WBEll = WBEll,
        WBAst = WBAst,
        WBDst = WBDst
    };

    /// <summary>
    /// Returns true if any Buchdahl weight is non-zero.
    /// </summary>
    public bool HasNonZeroBuchdahlWeights =>
        WBSph != 0.0 || WBCma != 0.0 || WBObl != 0.0 ||
        WBEll != 0.0 || WBAst != 0.0 || WBDst != 0.0;

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"W1={W1}, W2={W2}, W3={W3}, W4={W4}, W5={W5}");
        if (IncludeChromatic)
            sb.Append($", WCL={WCL}, WCT={WCT}");
        else
            sb.Append(" (no chromatic)");
        if (IncludeBuchdahl)
            sb.Append($", WBSph={WBSph}, WBCma={WBCma}, WBObl={WBObl}, WBEll={WBEll}, WBAst={WBAst}, WBDst={WBDst}");
        return sb.ToString();
    }
}
