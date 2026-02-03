namespace LensSplitter.Core.Models;

/// <summary>
/// Represents the state of a paraxial ray at a surface.
/// </summary>
public class ParaxialRayState
{
    /// <summary>
    /// Gets or sets the surface index.
    /// </summary>
    public int SurfaceIndex { get; set; }

    /// <summary>
    /// Gets or sets the ray height (y) at the surface.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Gets or sets the ray slope (u) after the surface.
    /// The slope is n*u where n is the refractive index and u is the paraxial angle.
    /// </summary>
    public double Slope { get; set; }

    /// <summary>
    /// Gets or sets the refractive index after the surface.
    /// </summary>
    public double RefractiveIndex { get; set; } = 1.0;

    public ParaxialRayState() { }

    public ParaxialRayState(int surfaceIndex, double height, double slope, double refractiveIndex = 1.0)
    {
        SurfaceIndex = surfaceIndex;
        Height = height;
        Slope = slope;
        RefractiveIndex = refractiveIndex;
    }

    /// <summary>
    /// Gets the actual angle (u) of the ray: Slope / RefractiveIndex.
    /// </summary>
    public double Angle => RefractiveIndex > 0 ? Slope / RefractiveIndex : Slope;

    public override string ToString() => $"S{SurfaceIndex}: y={Height:F6}, u={Slope:F6}";
}

/// <summary>
/// Represents a complete paraxial ray trace through an optical system.
/// </summary>
public class ParaxialRay
{
    /// <summary>
    /// Gets or sets the ray states at each surface.
    /// </summary>
    public List<ParaxialRayState> States { get; set; } = new();

    /// <summary>
    /// Gets or sets the wavelength used for this trace.
    /// </summary>
    public double Wavelength { get; set; }

    /// <summary>
    /// Gets the initial ray height.
    /// </summary>
    public double InitialHeight => States.Count > 0 ? States[0].Height : 0.0;

    /// <summary>
    /// Gets the initial ray slope.
    /// </summary>
    public double InitialSlope => States.Count > 0 ? States[0].Slope : 0.0;

    /// <summary>
    /// Gets the final ray height (at image surface).
    /// </summary>
    public double FinalHeight => States.Count > 0 ? States[^1].Height : 0.0;

    /// <summary>
    /// Gets the final ray slope (at image surface).
    /// </summary>
    public double FinalSlope => States.Count > 0 ? States[^1].Slope : 0.0;

    /// <summary>
    /// Gets the ray state at a specific surface.
    /// </summary>
    /// <param name="surfaceIndex">The surface index.</param>
    /// <returns>The ray state, or null if not found.</returns>
    public ParaxialRayState? GetStateAt(int surfaceIndex)
    {
        return States.FirstOrDefault(s => s.SurfaceIndex == surfaceIndex);
    }

    /// <summary>
    /// Gets the ray height at a specific surface.
    /// </summary>
    /// <param name="surfaceIndex">The surface index.</param>
    /// <returns>The ray height.</returns>
    public double GetHeightAt(int surfaceIndex)
    {
        return GetStateAt(surfaceIndex)?.Height ?? 0.0;
    }

    /// <summary>
    /// Gets the ray slope after a specific surface.
    /// </summary>
    /// <param name="surfaceIndex">The surface index.</param>
    /// <returns>The ray slope.</returns>
    public double GetSlopeAt(int surfaceIndex)
    {
        return GetStateAt(surfaceIndex)?.Slope ?? 0.0;
    }
}

/// <summary>
/// Contains both marginal and chief rays for a complete paraxial analysis.
/// </summary>
public class ParaxialRayPair
{
    /// <summary>
    /// Gets or sets the marginal ray (from center of object through edge of stop).
    /// </summary>
    public ParaxialRay MarginalRay { get; set; } = new();

    /// <summary>
    /// Gets or sets the chief ray (from edge of field through center of stop).
    /// </summary>
    public ParaxialRay ChiefRay { get; set; } = new();

    /// <summary>
    /// Calculates the Lagrange invariant.
    /// H = n * (u_m * y_c - u_c * y_m)
    /// This should be constant through the system.
    /// </summary>
    /// <param name="surfaceIndex">The surface index to evaluate at.</param>
    /// <returns>The Lagrange invariant.</returns>
    public double GetLagrangeInvariant(int surfaceIndex)
    {
        var marginalState = MarginalRay.GetStateAt(surfaceIndex);
        var chiefState = ChiefRay.GetStateAt(surfaceIndex);

        if (marginalState == null || chiefState == null)
            return 0.0;

        double n = marginalState.RefractiveIndex;
        double y_m = marginalState.Height;
        double u_m = marginalState.Slope / n;
        double y_c = chiefState.Height;
        double u_c = chiefState.Slope / n;

        return n * (u_m * y_c - u_c * y_m);
    }
}
