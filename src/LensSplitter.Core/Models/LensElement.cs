namespace LensSplitter.Core.Models;

/// <summary>
/// Represents a lens element (single piece of glass with two surfaces).
/// </summary>
public class LensElement
{
    /// <summary>
    /// Gets or sets the index of this element in the system (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the front (first) surface of the element.
    /// </summary>
    public Surface FrontSurface { get; set; } = null!;

    /// <summary>
    /// Gets or sets the rear (second) surface of the element.
    /// </summary>
    public Surface RearSurface { get; set; } = null!;

    /// <summary>
    /// Gets or sets the glass material of the element.
    /// </summary>
    public Glass Glass { get; set; } = null!;

    /// <summary>
    /// Gets the center thickness of the element (front surface thickness).
    /// </summary>
    public double CenterThickness => FrontSurface.Thickness;

    /// <summary>
    /// Gets the front radius of curvature (R1).
    /// </summary>
    public double R1 => FrontSurface.Radius;

    /// <summary>
    /// Gets the rear radius of curvature (R2).
    /// </summary>
    public double R2 => RearSurface.Radius;

    /// <summary>
    /// Calculates the thin lens power at the specified wavelength.
    /// P = (n-1) * [1/R1 - 1/R2]
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The thin lens power in inverse mm (diopters if mm converted to meters).</returns>
    public double GetThinLensPower(double wavelengthMicrons)
    {
        double n = Glass.GetRefractiveIndex(wavelengthMicrons);
        double c1 = FrontSurface.Curvature;
        double c2 = RearSurface.Curvature;

        return (n - 1.0) * (c1 - c2);
    }

    /// <summary>
    /// Calculates the thick lens power at the specified wavelength using the lensmaker's equation.
    /// P = (n-1) * [1/R1 - 1/R2 + (n-1)*d/(n*R1*R2)]
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The thick lens power in inverse mm.</returns>
    public double GetThickLensPower(double wavelengthMicrons)
    {
        double n = Glass.GetRefractiveIndex(wavelengthMicrons);
        double d = CenterThickness;
        double c1 = FrontSurface.Curvature;
        double c2 = RearSurface.Curvature;

        // For thick lens: P = φ1 + φ2 - d*φ1*φ2/n
        // where φ1 = (n-1)/R1 = (n-1)*c1 and φ2 = -(n-1)/R2 = -(n-1)*c2
        // Or using lensmaker's equation directly:
        // P = (n-1) * [c1 - c2 + (n-1)*d*c1*c2/n]

        double phi1 = (n - 1.0) * c1;
        double phi2 = -(n - 1.0) * c2; // Note: phi2 is refraction from glass to air

        return phi1 + phi2 - (d * phi1 * phi2 / n);
    }

    /// <summary>
    /// Calculates the shape factor (Coddington shape factor) of the element.
    /// X = (R2 + R1) / (R2 - R1)
    /// </summary>
    /// <returns>The shape factor. X=0 for equiconvex, X=+1 for plano-convex, X=-1 for plano-concave.</returns>
    public double GetShapeFactor()
    {
        double r1 = R1;
        double r2 = R2;

        // Handle infinite radii
        if (double.IsInfinity(r1) && double.IsInfinity(r2)) return 0.0;
        if (double.IsInfinity(r1)) return -1.0; // Plano-convex (flat first surface)
        if (double.IsInfinity(r2)) return 1.0;  // Plano-concave (flat second surface)

        double denominator = r2 - r1;
        if (Math.Abs(denominator) < 1e-12) return 0.0;

        return (r2 + r1) / denominator;
    }

    /// <summary>
    /// Calculates the optimal shape factor for minimum spherical aberration
    /// at infinite conjugate.
    /// X_opt = -2(n² - 1) / (n + 2)
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The optimal shape factor.</returns>
    public double GetOptimalShapeFactor(double wavelengthMicrons)
    {
        double n = Glass.GetRefractiveIndex(wavelengthMicrons);
        return -2.0 * (n * n - 1.0) / (n + 2.0);
    }

    /// <summary>
    /// Calculates the position factor (Coddington position factor).
    /// Y = (s' + s) / (s' - s)
    /// For infinite object: Y = -1
    /// </summary>
    /// <param name="objectDistance">Object distance (negative for real objects).</param>
    /// <param name="imageDistance">Image distance (positive for real images).</param>
    /// <returns>The position factor.</returns>
    public static double GetPositionFactor(double objectDistance, double imageDistance)
    {
        if (double.IsInfinity(objectDistance)) return -1.0;

        double s = objectDistance;
        double sPrime = imageDistance;
        double denominator = sPrime - s;

        if (Math.Abs(denominator) < 1e-12) return 0.0;

        return (sPrime + s) / denominator;
    }

    /// <summary>
    /// Gets the effective focal length of this element.
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The focal length in mm.</returns>
    public double GetFocalLength(double wavelengthMicrons)
    {
        double power = GetThickLensPower(wavelengthMicrons);
        return Math.Abs(power) > 1e-12 ? 1.0 / power : double.PositiveInfinity;
    }

    public override string ToString()
    {
        return $"Element {Index}: R1={R1:F3}, R2={R2:F3}, d={CenterThickness:F3}, Glass={Glass.Name}";
    }
}
