using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Calculates third-order (Seidel) aberration coefficients.
/// </summary>
public class AberrationCalculator
{
    private readonly ParaxialRayTracer _tracer = new();

    /// <summary>
    /// Calculates the conic contribution to S₁ for a single surface.
    /// ΔS₁ = -(n' - n) · K · c³ · y⁴
    /// where K is the conic constant and c = 1/R is the curvature.
    /// </summary>
    /// <param name="rayHeight">Marginal ray height at the surface.</param>
    /// <param name="radius">Surface radius of curvature.</param>
    /// <param name="conic">Conic constant (K). K=0 sphere, K=-1 parabola.</param>
    /// <param name="nBefore">Refractive index before surface.</param>
    /// <param name="nAfter">Refractive index after surface.</param>
    /// <returns>The conic contribution to S₁.</returns>
    public static double CalculateS1ConicContribution(
        double rayHeight,
        double radius,
        double conic,
        double nBefore,
        double nAfter)
    {
        if (Math.Abs(conic) < 1e-12) return 0.0; // Spherical surface
        if (double.IsInfinity(radius) || Math.Abs(radius) < 1e-12) return 0.0; // Flat surface

        double c = 1.0 / radius;
        double y4 = Math.Pow(rayHeight, 4);
        double c3 = Math.Pow(c, 3);

        // ΔS₁ = -(n' - n) · K · c³ · y⁴  (thin-lens convention)
        return -(nAfter - nBefore) * conic * c3 * y4;
    }

    /// <summary>
    /// Calculates total S₁ for a lens element including conic contributions.
    /// </summary>
    /// <param name="rayHeight">Marginal ray height at the lens.</param>
    /// <param name="power">Lens power.</param>
    /// <param name="refractiveIndex">Glass refractive index.</param>
    /// <param name="shapeFactor">Coddington shape factor.</param>
    /// <param name="positionFactor">Position factor.</param>
    /// <param name="r1">Front surface radius.</param>
    /// <param name="r2">Rear surface radius.</param>
    /// <param name="conic1">Front surface conic constant.</param>
    /// <param name="conic2">Rear surface conic constant.</param>
    /// <returns>Total S₁ including spherical and conic contributions.</returns>
    public static double CalculateS1WithConic(
        double rayHeight,
        double power,
        double refractiveIndex,
        double shapeFactor,
        double positionFactor,
        double r1,
        double r2,
        double conic1,
        double conic2)
    {
        // Base spherical contribution
        double S1_spherical = CalculateS1ThinLens(rayHeight, power, refractiveIndex, shapeFactor, positionFactor);

        // Conic contributions from each surface
        double S1_conic1 = CalculateS1ConicContribution(rayHeight, r1, conic1, 1.0, refractiveIndex);
        double S1_conic2 = CalculateS1ConicContribution(rayHeight, r2, conic2, refractiveIndex, 1.0);

        return S1_spherical + S1_conic1 + S1_conic2;
    }

    /// <summary>
    /// Calculates the third-order spherical aberration coefficient (S₁) for a thin lens.
    /// S₁ = -y⁴ · φ³ · G(n, X, Y)
    /// where G is the shape-position function.
    /// Note: This formula is for spherical surfaces only. Use CalculateS1WithConic for aspheric surfaces.
    /// </summary>
    /// <param name="rayHeight">Marginal ray height at the lens (y).</param>
    /// <param name="power">Lens power (φ).</param>
    /// <param name="refractiveIndex">Refractive index (n).</param>
    /// <param name="shapeFactor">Coddington shape factor X = (R2+R1)/(R2-R1).</param>
    /// <param name="positionFactor">Position factor Y = (u'+u)/(u'-u), Y=-1 for object at infinity.</param>
    /// <returns>The S₁ coefficient (negative = undercorrected, positive = overcorrected).</returns>
    public static double CalculateS1ThinLens(
        double rayHeight,
        double power,
        double refractiveIndex,
        double shapeFactor,
        double positionFactor)
    {
        if (Math.Abs(power) < 1e-12) return 0.0;

        double n = refractiveIndex;
        double X = shapeFactor;
        double Y = positionFactor;

        // G function (shape-position function for spherical aberration)
        // G = A·X² + B·X·Y + C·Y² + D
        // where:
        // A = (n+2) / (4n(n-1)²)
        // B = (n+1) / (n(n-1))
        // C = (3n+1)(n-1) / (4n)
        // D = n² / (n-1)²

        double nm1 = n - 1.0;
        double nm1sq = nm1 * nm1;

        double A = (n + 2.0) / (4.0 * n * nm1sq);
        double B = (n + 1.0) / (n * nm1);
        double C = (3.0 * n + 1.0) * nm1 / (4.0 * n);
        double D = n * n / nm1sq;

        double G = A * X * X + B * X * Y + C * Y * Y + D;

        // S₁ = -y⁴ · φ³ · G
        double y4 = Math.Pow(rayHeight, 4);
        double phi3 = Math.Pow(power, 3);

        return -y4 * phi3 * G;
    }

    /// <summary>
    /// Calculates the optimal shape factor for minimum spherical aberration.
    /// X_opt = -B·Y / (2A) = -(n+1)(n-1)·Y / (n+2)
    /// </summary>
    /// <param name="refractiveIndex">Refractive index.</param>
    /// <param name="positionFactor">Position factor Y.</param>
    /// <returns>Optimal shape factor.</returns>
    public static double CalculateOptimalShapeFactor(double refractiveIndex, double positionFactor)
    {
        double n = refractiveIndex;
        double Y = positionFactor;

        // X_opt = -(n²-1)·Y / (n+2) = -(n+1)(n-1)·Y / (n+2)
        return -(n * n - 1.0) * Y / (n + 2.0);
    }

    /// <summary>
    /// Calculates the minimum possible S₁ for a lens at optimal shape.
    /// S₁_min = -y⁴ · φ³ · G_min
    /// where G_min = C·Y² + D - B²Y²/(4A)
    /// </summary>
    public static double CalculateMinimumS1(
        double rayHeight,
        double power,
        double refractiveIndex,
        double positionFactor)
    {
        if (Math.Abs(power) < 1e-12) return 0.0;

        double n = refractiveIndex;
        double Y = positionFactor;
        double nm1 = n - 1.0;
        double nm1sq = nm1 * nm1;

        double A = (n + 2.0) / (4.0 * n * nm1sq);
        double B = (n + 1.0) / (n * nm1);
        double C = (3.0 * n + 1.0) * nm1 / (4.0 * n);
        double D = n * n / nm1sq;

        // G_min at optimal shape
        double G_min = C * Y * Y + D - (B * B * Y * Y) / (4.0 * A);

        double y4 = Math.Pow(rayHeight, 4);
        double phi3 = Math.Pow(power, 3);

        return -y4 * phi3 * G_min;
    }

    /// <summary>
    /// Calculates the position factor Y for a lens given object and image distances.
    /// Y = (u' + u) / (u' - u) = (s' + s) / (s' - s)
    /// For object at infinity: Y = -1
    /// For 1:1 imaging: Y = 0
    /// For image at infinity: Y = +1
    /// </summary>
    /// <param name="objectDistance">Object distance s (negative for real object).</param>
    /// <param name="imageDistance">Image distance s' (positive for real image).</param>
    /// <returns>Position factor Y.</returns>
    public static double CalculatePositionFactor(double objectDistance, double imageDistance)
    {
        if (double.IsInfinity(objectDistance)) return -1.0;
        if (double.IsInfinity(imageDistance)) return 1.0;

        double s = objectDistance;
        double sPrime = imageDistance;
        double denom = sPrime - s;

        if (Math.Abs(denom) < 1e-12) return 0.0;

        return (sPrime + s) / denom;
    }

    /// <summary>
    /// Calculates the position factor Y from magnification.
    /// Y = (m + 1) / (m - 1)
    /// For m = 0 (object at infinity): Y = -1
    /// For m = -1 (1:1 imaging): Y = 0
    /// For m → ∞ (image at infinity): Y = +1
    /// </summary>
    /// <param name="magnification">Lateral magnification (negative for inverted image).</param>
    /// <returns>Position factor Y.</returns>
    public static double CalculatePositionFactorFromMagnification(double magnification)
    {
        if (Math.Abs(magnification) < 1e-12) return -1.0; // Object at infinity
        if (double.IsInfinity(magnification)) return 1.0; // Image at infinity

        double denom = magnification - 1.0;
        if (Math.Abs(denom) < 1e-12) return double.PositiveInfinity; // m = 1 is undefined

        return (magnification + 1.0) / denom;
    }

    /// <summary>
    /// Determines if a system is finite conjugate based on object surface thickness.
    /// </summary>
    public static bool IsFiniteConjugate(Models.OpticalSystem system)
    {
        if (system.Surfaces.Count < 2) return false;

        var objectSurface = system.Surfaces[0];
        return !double.IsInfinity(objectSurface.Thickness) && objectSurface.Thickness > 0;
    }

    /// <summary>
    /// Gets the object distance from the system (distance from object to first optical surface).
    /// Returns negative infinity for infinite conjugate.
    /// </summary>
    public static double GetObjectDistance(Models.OpticalSystem system)
    {
        if (system.Surfaces.Count < 2) return double.NegativeInfinity;

        var objectSurface = system.Surfaces[0];
        if (double.IsInfinity(objectSurface.Thickness))
        {
            return double.NegativeInfinity;
        }

        // Object distance is negative (object to the left of first surface)
        return -objectSurface.Thickness;
    }

    /// <summary>
    /// Calculates total S₁ for a split lens system (two thin lenses with air gap).
    /// </summary>
    /// <param name="y1">Ray height at first lens.</param>
    /// <param name="phi1">Power of first lens.</param>
    /// <param name="n1">Refractive index of first lens.</param>
    /// <param name="X1">Shape factor of first lens.</param>
    /// <param name="Y1">Position factor of first lens.</param>
    /// <param name="y2">Ray height at second lens.</param>
    /// <param name="phi2">Power of second lens.</param>
    /// <param name="n2">Refractive index of second lens.</param>
    /// <param name="X2">Shape factor of second lens.</param>
    /// <param name="Y2">Position factor of second lens.</param>
    /// <returns>Total S₁ = S₁₁ + S₁₂.</returns>
    public static double CalculateTotalS1(
        double y1, double phi1, double n1, double X1, double Y1,
        double y2, double phi2, double n2, double X2, double Y2)
    {
        double S1_1 = CalculateS1ThinLens(y1, phi1, n1, X1, Y1);
        double S1_2 = CalculateS1ThinLens(y2, phi2, n2, X2, Y2);

        return S1_1 + S1_2;
    }

    /// <summary>
    /// Calculates the ray height at the second lens after passing through the first.
    /// y₂ = y₁ - d·φ₁·y₁ = y₁(1 - d·φ₁)
    /// </summary>
    /// <param name="y1">Ray height at first lens.</param>
    /// <param name="phi1">Power of first lens.</param>
    /// <param name="airGap">Air gap between lenses.</param>
    /// <returns>Ray height at second lens.</returns>
    public static double CalculateRayHeightAtSecondLens(double y1, double phi1, double airGap)
    {
        // After first lens: u' = u - y₁·φ₁ = -y₁·φ₁ (for u=0 from infinity)
        // At second lens: y₂ = y₁ + d·u' = y₁ - d·y₁·φ₁ = y₁(1 - d·φ₁)
        return y1 * (1.0 - airGap * phi1);
    }

    /// <summary>
    /// Calculates the position factor for the second lens.
    /// The object for the second lens is the virtual image from the first lens.
    /// </summary>
    /// <param name="phi1">Power of first lens.</param>
    /// <param name="phi2">Power of second lens.</param>
    /// <param name="airGap">Air gap between lenses.</param>
    /// <returns>Position factor Y₂ for second lens.</returns>
    public static double CalculateSecondLensPositionFactor(double phi1, double phi2, double airGap)
    {
        // First lens: object at infinity → image at f₁ = 1/φ₁ from lens
        // For second lens at distance d from first:
        //   Object distance s₂ = f₁ - d = 1/φ₁ - d
        //   Use thin lens equation to find s₂'
        //   Then calculate Y₂

        if (Math.Abs(phi1) < 1e-12) return -1.0; // First lens has no power

        double f1 = 1.0 / phi1;
        double s2 = f1 - airGap; // Object distance for second lens (can be negative = virtual)

        if (Math.Abs(s2) < 1e-12) return 0.0; // Object at lens

        // From thin lens equation: 1/s₂' - 1/s₂ = φ₂
        // s₂' = 1/(φ₂ + 1/s₂) = s₂/(s₂·φ₂ + 1)
        double s2Prime = s2 / (s2 * phi2 + 1.0);

        return CalculatePositionFactor(s2, s2Prime);
    }

    /// <summary>
    /// Calculates the power scaling factor needed to preserve combined power when splitting.
    /// When two lenses are separated by distance d, their combined power is:
    /// P_combined = φ₁ + φ₂ - d·φ₁·φ₂
    /// This is less than φ₁ + φ₂, so we need to scale up individual powers.
    /// </summary>
    /// <param name="totalPower">Desired combined power.</param>
    /// <param name="powerRatio">Power ratio φ₁/φ_total.</param>
    /// <param name="airGap">Air gap between lenses.</param>
    /// <returns>Scale factor S such that scaled powers yield correct combined power.</returns>
    public static double CalculatePowerScaleFactor(double totalPower, double powerRatio, double airGap)
    {
        // We want: P = S*r*P + S*(1-r)*P - d*(S*r*P)*(S*(1-r)*P)
        //          P = S*P - d*S²*r*(1-r)*P²
        //          1 = S - d*S²*r*(1-r)*P
        // Let a = d*r*(1-r)*P, solving quadratic: a*S² - S + 1 = 0
        // S = (1 - sqrt(1 - 4a)) / (2a)  or  S = 2 / (1 + sqrt(1 - 4a))

        double a = airGap * powerRatio * (1.0 - powerRatio) * totalPower;

        if (Math.Abs(a) < 1e-12)
        {
            return 1.0; // No separation effect
        }

        double discriminant = 1.0 - 4.0 * a;
        if (discriminant < 0)
        {
            // Air gap too large for this power/ratio - use approximation
            return 1.0 / (1.0 - a); // First-order approximation
        }

        // Use the form that's numerically stable for small a
        return 2.0 / (1.0 + Math.Sqrt(discriminant));
    }

    /// <summary>
    /// Evaluates all Seidel aberrations for a split configuration with given parameters.
    /// Powers are automatically scaled to preserve the combined power equal to totalPower.
    /// </summary>
    /// <param name="totalPower">Total power of the element being split.</param>
    /// <param name="powerRatio">Power ratio φ₁/φ_total, between 0 and 1.</param>
    /// <param name="airGap">Air gap between split elements.</param>
    /// <param name="refractiveIndex">Refractive index of the glass.</param>
    /// <param name="entrancePupilRadius">Entrance pupil radius.</param>
    /// <param name="objectPositionFactor">Position factor Y for the object conjugate.
    /// Y = -1 for infinite conjugate (default), Y = 0 for 1:1, Y between -1 and 0 for finite conjugate.</param>
    /// <param name="fieldAngleRadians">Field angle for chief ray (radians). Default is 0.1 rad (~5.7°).</param>
    /// <param name="customX1">Custom shape factor for first element (null = use S1-optimal).</param>
    /// <param name="customX2">Custom shape factor for second element (null = use S1-optimal).</param>
    public SplitAberrationResult EvaluateSplitConfiguration(
        double totalPower,
        double powerRatio,
        double airGap,
        double refractiveIndex,
        double entrancePupilRadius,
        double objectPositionFactor = -1.0,
        double fieldAngleRadians = 0.1,
        double? customX1 = null,
        double? customX2 = null)
    {
        // Calculate scale factor to preserve combined power
        double scaleFactor = CalculatePowerScaleFactor(totalPower, powerRatio, airGap);

        // Scale individual powers so combined power equals totalPower
        double phi1 = scaleFactor * totalPower * powerRatio;
        double phi2 = scaleFactor * totalPower * (1.0 - powerRatio);

        // First lens: use provided position factor (default: object at infinity)
        double Y1 = objectPositionFactor;
        double X1_optimal = CalculateOptimalShapeFactor(refractiveIndex, Y1);
        double X1 = customX1 ?? X1_optimal;
        double y1 = entrancePupilRadius;

        // Second lens
        double y2 = CalculateRayHeightAtSecondLens(y1, phi1, airGap);
        double Y2 = CalculateSecondLensPositionFactor(phi1, phi2, airGap);
        double X2_optimal = CalculateOptimalShapeFactor(refractiveIndex, Y2);
        double X2 = customX2 ?? X2_optimal;

        // Calculate S₁ for each lens at the specified shape
        double S1_1 = CalculateS1ThinLens(y1, phi1, refractiveIndex, X1, Y1);
        double S1_2 = CalculateS1ThinLens(y2, phi2, refractiveIndex, X2, Y2);
        double S1_total = S1_1 + S1_2;

        // Calculate S2-S5 using thin lens approximations
        // For object at infinity, chief ray has angle = field angle
        var (S2_total, S3_total, S4_total, S5_total) = CalculateHigherOrderSeidel(
            y1, phi1, refractiveIndex, X1, Y1, S1_1,
            y2, phi2, X2, Y2, S1_2,
            airGap, fieldAngleRadians);

        // Calculate effective power considering separation
        // φ_eff = φ₁ + φ₂ - d·φ₁·φ₂
        double effectivePower = phi1 + phi2 - airGap * phi1 * phi2;

        return new SplitAberrationResult
        {
            PowerRatio = powerRatio,
            AirGap = airGap,
            Phi1 = phi1,
            Phi2 = phi2,
            Y1 = Y1,
            Y2 = Y2,
            X1_Optimal = X1,  // Store the actual shape factor used
            X2_Optimal = X2,  // Store the actual shape factor used
            RayHeight1 = y1,
            RayHeight2 = y2,
            S1_Element1 = S1_1,
            S1_Element2 = S1_2,
            S1_Total = S1_total,
            S2_Total = S2_total,
            S3_Total = S3_total,
            S4_Total = S4_total,
            S5_Total = S5_total,
            EffectivePower = effectivePower,
            PowerScaleFactor = scaleFactor,
            TargetPower = totalPower
        };
    }

    /// <summary>
    /// Calculates higher-order Seidel aberrations (S2-S5) for a two-lens split system.
    /// Uses thin-lens approximations with chief ray data.
    /// </summary>
    private static (double S2, double S3, double S4, double S5) CalculateHigherOrderSeidel(
        double y1, double phi1, double n, double X1, double Y1, double S1_1,
        double y2, double phi2, double X2, double Y2, double S1_2,
        double airGap, double fieldAngleRadians)
    {
        // For thin lens at object at infinity:
        // Marginal ray: height = y, angle u = 0 before, u' = -y*phi after
        // Chief ray: height h_bar = 0 at entrance pupil, angle u_bar = field angle

        // Chief ray height at first lens (assuming EP at first lens): h_bar1 = 0
        double h_bar1 = 0;
        double u_chief1 = fieldAngleRadians; // Chief ray angle in object space

        // Chief ray after first lens
        double u_chief1_prime = u_chief1 - h_bar1 * phi1; // For h_bar1 = 0, unchanged

        // Transfer to second lens
        double h_bar2 = h_bar1 + airGap * u_chief1_prime;
        double u_chief2 = u_chief1_prime;

        // Chief ray after second lens
        double u_chief2_prime = u_chief2 - h_bar2 * phi2;

        // Calculate A and A_bar for each lens
        // A = n * (h * c + u) where c = power/(n-1) for a thin lens
        // For object at infinity, u = 0 for marginal ray at first lens

        double u1 = 0; // Marginal ray angle before first lens (from infinity)
        double u1_prime = -y1 * phi1; // Marginal ray angle after first lens
        double u2 = u1_prime; // Transfer preserves angle
        double u2_prime = u2 - y2 * phi2; // Marginal ray angle after second lens

        // For thin lens: c = phi1 / (n - 1), but we use combined expression
        // A = n * h * c + n * u = n * h * phi/(n-1) + n*u for thin lens
        // Simplify: A ≈ y * phi / (n-1) for object at infinity (u=0)

        double nm1 = n - 1.0;

        // First lens
        double A1 = y1 * phi1; // Simplified refraction invariant (normalized)
        double A_bar1 = h_bar1 * phi1 + u_chief1 * nm1; // Chief ray invariant
        double A_ratio1 = Math.Abs(A1) > 1e-20 ? A_bar1 / A1 : 0;

        // Second lens
        double A2 = y2 * phi2;
        double A_bar2 = h_bar2 * phi2 + u_chief2 * nm1;
        double A_ratio2 = Math.Abs(A2) > 1e-20 ? A_bar2 / A2 : 0;

        // Coma (S2) = S1 * (A_bar/A)
        double S2_1 = S1_1 * A_ratio1;
        double S2_2 = S1_2 * A_ratio2;
        double S2_total = S2_1 + S2_2;

        // Astigmatism (S3) = S2 * (A_bar/A)
        double S3_1 = S2_1 * A_ratio1;
        double S3_2 = S2_2 * A_ratio2;
        double S3_total = S3_1 + S3_2;

        // Petzval (S4) = -c * L² * (1/n' - 1/n) = -phi * L² / n for thin lens in air
        // Lagrange invariant L = h * n * u_bar - h_bar * n * u
        // For thin lens in air: L1 = y1 * u_chief1 - h_bar1 * u1 = y1 * u_chief1
        double L1 = y1 * u_chief1 - h_bar1 * u1;
        double L2 = y2 * u_chief2 - h_bar2 * u2;

        // S4 for thin lens: S4 = -phi * L² * (1/n - 1) / n = phi * L² * (n-1) / n
        // Using the standard formula: S4 = -c * L² * (1/n' - 1/n)
        // For lens in air: 1/1 - 1/n = (n-1)/n, and c = phi/(n-1)
        // So S4 = -phi/(n-1) * L² * (n-1)/n = -phi * L² / n
        double S4_1 = -phi1 * L1 * L1 / n;
        double S4_2 = -phi2 * L2 * L2 / n;
        double S4_total = S4_1 + S4_2;

        // Distortion (S5) = (A_bar/A) * (S3 + S4)
        double S5_1 = A_ratio1 * (S3_1 + S4_1);
        double S5_2 = A_ratio2 * (S3_2 + S4_2);
        double S5_total = S5_1 + S5_2;

        return (S2_total, S3_total, S4_total, S5_total);
    }
}

/// <summary>
/// Results from evaluating a split lens configuration for aberrations.
/// </summary>
public class SplitAberrationResult
{
    public double PowerRatio { get; set; }
    public double AirGap { get; set; }
    public double Phi1 { get; set; }
    public double Phi2 { get; set; }
    public double Y1 { get; set; }
    public double Y2 { get; set; }
    public double X1_Optimal { get; set; }
    public double X2_Optimal { get; set; }
    public double RayHeight1 { get; set; }
    public double RayHeight2 { get; set; }
    public double S1_Element1 { get; set; }
    public double S1_Element2 { get; set; }
    public double S1_Total { get; set; }
    public double EffectivePower { get; set; }

    // Full Seidel aberrations (from system ray trace)
    public double S2_Total { get; set; }
    public double S3_Total { get; set; }
    public double S4_Total { get; set; }
    public double S5_Total { get; set; }
    public double CL_Total { get; set; }
    public double CT_Total { get; set; }

    /// <summary>
    /// Scale factor applied to individual powers to preserve combined power.
    /// </summary>
    public double PowerScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Target combined power (original element power).
    /// </summary>
    public double TargetPower { get; set; }

    /// <summary>
    /// Weighted merit function value (set during optimization).
    /// </summary>
    public double MeritFunction { get; set; }

    /// <summary>
    /// Gets the aberration balance ratio (S1_1 / S1_total).
    /// Ideally close to 0.5 for balanced contribution.
    /// </summary>
    public double AberrationBalance =>
        Math.Abs(S1_Total) > 1e-20 ? S1_Element1 / S1_Total : 0.5;

    /// <summary>
    /// Calculates merit function using given weights.
    /// </summary>
    public double CalculateMeritFunction(AberrationWeights weights)
    {
        return weights.W1 * Math.Abs(S1_Total) +
               weights.W2 * Math.Abs(S2_Total) +
               weights.W3 * Math.Abs(S3_Total) +
               weights.W4 * Math.Abs(S4_Total) +
               weights.W5 * Math.Abs(S5_Total) +
               weights.WCL * Math.Abs(CL_Total) +
               weights.WCT * Math.Abs(CT_Total);
    }

    public override string ToString() =>
        $"Ratio={PowerRatio:F3}, Gap={AirGap:F2}, S1={S1_Total:E3}, S2={S2_Total:E3}, " +
        $"S3={S3_Total:E3}, S4={S4_Total:E3}, S5={S5_Total:E3}, MF={MeritFunction:E3}";
}
