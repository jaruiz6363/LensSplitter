namespace LensSplitter.Core.Models;

/// <summary>
/// Represents an optical surface in a lens system.
/// </summary>
public class Surface
{
    /// <summary>
    /// Gets or sets the surface index (0 = object, last = image).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the surface type.
    /// </summary>
    public SurfaceType SurfaceType { get; set; } = SurfaceType.Standard;

    /// <summary>
    /// Gets or sets the radius of curvature in mm. Positive = center of curvature to the right.
    /// Use double.PositiveInfinity for a flat surface.
    /// </summary>
    public double Radius { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// Gets the curvature (1/Radius). Returns 0 for infinite radius.
    /// </summary>
    public double Curvature => double.IsInfinity(Radius) ? 0.0 : 1.0 / Radius;

    /// <summary>
    /// Gets or sets the thickness to the next surface in mm.
    /// </summary>
    public double Thickness { get; set; }

    /// <summary>
    /// Gets or sets the semi-diameter (clear aperture radius) in mm.
    /// </summary>
    public double SemiDiameter { get; set; }

    /// <summary>
    /// Gets or sets the conic constant (k). k=0 for sphere, k=-1 for parabola.
    /// </summary>
    public double Conic { get; set; }

    /// <summary>
    /// Gets or sets the glass material after this surface.
    /// Null or Air for air space.
    /// </summary>
    public Glass? Glass { get; set; }

    /// <summary>
    /// Gets or sets the glass name reference (used during parsing before glass is resolved).
    /// </summary>
    public string? GlassName { get; set; }

    /// <summary>
    /// Gets or sets whether this surface is the aperture stop.
    /// </summary>
    public bool IsStop { get; set; }

    /// <summary>
    /// Gets or sets a comment or description for this surface.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the absolute Z position (used in Optiland format).
    /// </summary>
    public double? AbsoluteZ { get; set; }

    /// <summary>
    /// Gets or sets whether this surface has a marginal ray height solve on thickness.
    /// MAZH in ZEMAX format.
    /// </summary>
    public bool HasMarginalRayHeightSolve { get; set; }

    /// <summary>
    /// Gets or sets the marginal ray height solve parameters (typically "0 0").
    /// </summary>
    public string? MarginalRayHeightSolveParams { get; set; }

    /// <summary>
    /// Gets whether this surface is flat (infinite radius).
    /// </summary>
    public bool IsFlat => double.IsInfinity(Radius) || Math.Abs(Curvature) < 1e-12;

    /// <summary>
    /// Calculates the sag (sagitta) of the surface at a given height.
    /// Sag is positive if the surface bulges in the +z direction (direction of light travel).
    /// For R > 0 (center of curvature to the right), surface bulges right (positive sag).
    /// For R < 0 (center of curvature to the left), surface bulges left (negative sag).
    /// </summary>
    /// <param name="height">The radial height from the optical axis (mm).</param>
    /// <returns>The sag in mm. Positive = bulges forward, negative = recedes back.</returns>
    public double GetSag(double height)
    {
        if (IsFlat || height <= 0) return 0.0;

        double r = Radius;
        double y = Math.Abs(height);

        // Check if height exceeds radius (would be outside the sphere)
        if (y >= Math.Abs(r))
        {
            // Return maximum possible sag (hemisphere)
            return r > 0 ? r : -Math.Abs(r);
        }

        // Exact sag formula: sag = R * (1 - sqrt(1 - (y/R)²))
        double ratio = y / r;
        double sag = r * (1.0 - Math.Sqrt(1.0 - ratio * ratio));

        return sag;
    }

    /// <summary>
    /// Calculates the edge thickness (clearance) between this surface and the next surface.
    /// </summary>
    /// <param name="nextSurface">The next surface in the optical path.</param>
    /// <param name="clearAperture">The clear aperture radius to check (mm).</param>
    /// <returns>The edge separation in mm. Negative values indicate collision.</returns>
    public double GetEdgeClearance(Surface nextSurface, double clearAperture)
    {
        // Center separation is this surface's thickness
        double centerGap = Thickness;

        // Sag of this surface (at its back, relative to its vertex)
        double sagThis = GetSag(clearAperture);

        // Sag of next surface (at its front, relative to its vertex)
        double sagNext = nextSurface.GetSag(clearAperture);

        // Edge clearance = center_gap + sag_next - sag_this
        // (sag_next is added because positive sag means the next surface recedes from us)
        // (sag_this is subtracted because positive sag means this surface extends toward the next)
        return centerGap - sagThis + sagNext;
    }

    /// <summary>
    /// Calculates the minimum center thickness needed to maintain a given edge clearance.
    /// </summary>
    /// <param name="nextSurface">The next surface in the optical path.</param>
    /// <param name="clearAperture">The clear aperture radius (mm).</param>
    /// <param name="minEdgeClearance">The minimum required edge clearance (mm).</param>
    /// <returns>The minimum center thickness in mm.</returns>
    public double GetMinimumCenterThickness(Surface nextSurface, double clearAperture, double minEdgeClearance = 0.5)
    {
        double sagThis = GetSag(clearAperture);
        double sagNext = nextSurface.GetSag(clearAperture);

        // Edge clearance = center_gap - sag_this + sag_next >= minEdgeClearance
        // center_gap >= minEdgeClearance + sag_this - sag_next
        return minEdgeClearance + sagThis - sagNext;
    }

    /// <summary>
    /// Gets the refractive index of the material after this surface at the specified wavelength.
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The refractive index (1.0 for air).</returns>
    public double GetRefractiveIndex(double wavelengthMicrons)
    {
        return Glass?.GetRefractiveIndex(wavelengthMicrons) ?? 1.0;
    }

    /// <summary>
    /// Calculates the surface power (phi) at the given wavelength.
    /// φ = (n' - n) / R, where n' is the index after and n is the index before.
    /// </summary>
    /// <param name="indexBefore">Refractive index before the surface.</param>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The surface power in inverse mm.</returns>
    public double GetSurfacePower(double indexBefore, double wavelengthMicrons)
    {
        if (IsFlat) return 0.0;

        double indexAfter = GetRefractiveIndex(wavelengthMicrons);
        return (indexAfter - indexBefore) / Radius;
    }

    /// <summary>
    /// Creates a deep copy of this surface.
    /// </summary>
    public Surface Clone()
    {
        return new Surface
        {
            Index = Index,
            SurfaceType = SurfaceType,
            Radius = Radius,
            Thickness = Thickness,
            SemiDiameter = SemiDiameter,
            Conic = Conic,
            Glass = Glass,
            GlassName = GlassName,
            IsStop = IsStop,
            Comment = Comment,
            AbsoluteZ = AbsoluteZ,
            HasMarginalRayHeightSolve = HasMarginalRayHeightSolve,
            MarginalRayHeightSolveParams = MarginalRayHeightSolveParams
        };
    }

    public override string ToString()
    {
        string radiusStr = IsFlat ? "Infinity" : $"{Radius:F4}";
        string glassStr = Glass?.Name ?? GlassName ?? "AIR";
        return $"S{Index}: R={radiusStr}, T={Thickness:F4}, Glass={glassStr}";
    }
}
