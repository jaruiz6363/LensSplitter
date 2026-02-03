using System.Linq;
using LensSplitter.Core.Models;

namespace LensSplitter.Core.Paraxial;

/// <summary>
/// Performs paraxial ray tracing through optical systems.
/// Based on proven implementation patterns from ZEMAX_INTERFACE.
/// Uses direction cosines (m, n) internally where u = m/n (paraxial approximation).
/// </summary>
public class ParaxialRayTracer
{
    /// <summary>
    /// Traces a paraxial ray through the optical system starting from surface 0.
    /// Uses the paraxial equations:
    /// - Refraction: n'u' = nu - yφ where φ = (n' - n) / R
    /// - Transfer: y' = y + t * u'
    /// </summary>
    public ParaxialRay Trace(OpticalSystem system, double initialHeight, double initialSlope, double wavelengthMicrons)
    {
        var ray = new ParaxialRay { Wavelength = wavelengthMicrons };

        if (system.Surfaces.Count < 2)
            return ray;

        double y = initialHeight;
        double nu = initialSlope;
        double n = 1.0; // Object space refractive index

        // Add initial state at surface 0 (object surface)
        ray.States.Add(new ParaxialRayState(0, y, nu, n));

        // Trace through each surface
        for (int i = 1; i < system.Surfaces.Count; i++)
        {
            var prevSurface = system.Surfaces[i - 1];
            var currentSurface = system.Surfaces[i];

            // Transfer from previous surface to current surface
            double t = prevSurface.Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t))
            {
                double u = nu / n; // Actual angle
                y = y + t * u;
            }
            // If thickness is infinite, y stays the same (object at infinity)

            // Get refractive index after this surface
            double nPrime = currentSurface.GetRefractiveIndex(wavelengthMicrons);

            // Calculate surface power: φ = (n' - n) / R
            double phi = 0.0;
            if (!currentSurface.IsFlat && !double.IsInfinity(currentSurface.Radius))
            {
                phi = (nPrime - n) / currentSurface.Radius;
            }

            // Refraction: n'u' = nu - yφ
            double nuPrime = nu - y * phi;

            // Store state after refraction
            ray.States.Add(new ParaxialRayState(i, y, nuPrime, nPrime));

            // Update for next iteration
            n = nPrime;
            nu = nuPrime;
        }

        return ray;
    }

    /// <summary>
    /// Traces a paraxial ray starting from a specific surface index.
    /// The initial height and slope are in the space BEFORE the starting surface.
    /// </summary>
    public ParaxialRay TraceFromSurface(OpticalSystem system, int startSurfaceIndex,
        double initialHeight, double initialSlope, double wavelengthMicrons)
    {
        var ray = new ParaxialRay { Wavelength = wavelengthMicrons };

        if (system.Surfaces.Count < 2 || startSurfaceIndex < 0 || startSurfaceIndex >= system.Surfaces.Count)
            return ray;

        double y = initialHeight;
        double nu = initialSlope;

        // n is the refractive index of the medium BEFORE the starting surface
        double n = startSurfaceIndex > 0
            ? system.Surfaces[startSurfaceIndex - 1].GetRefractiveIndex(wavelengthMicrons)
            : 1.0;

        // First, refract at the starting surface
        var startSurface = system.Surfaces[startSurfaceIndex];
        double nPrime = startSurface.GetRefractiveIndex(wavelengthMicrons);
        double phi = 0.0;
        if (!startSurface.IsFlat && !double.IsInfinity(startSurface.Radius))
        {
            phi = (nPrime - n) / startSurface.Radius;
        }
        double nuPrime = nu - y * phi;

        // Store state AFTER refraction at start surface
        ray.States.Add(new ParaxialRayState(startSurfaceIndex, y, nuPrime, nPrime));

        n = nPrime;
        nu = nuPrime;

        // Trace forward through remaining surfaces
        for (int i = startSurfaceIndex + 1; i < system.Surfaces.Count; i++)
        {
            var prevSurface = system.Surfaces[i - 1];
            var currentSurface = system.Surfaces[i];

            double t = prevSurface.Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t))
            {
                double u = nu / n;
                y = y + t * u;
            }

            nPrime = currentSurface.GetRefractiveIndex(wavelengthMicrons);
            phi = 0.0;
            if (!currentSurface.IsFlat && !double.IsInfinity(currentSurface.Radius))
            {
                phi = (nPrime - n) / currentSurface.Radius;
            }

            nuPrime = nu - y * phi;
            ray.States.Add(new ParaxialRayState(i, y, nuPrime, nPrime));

            n = nPrime;
            nu = nuPrime;
        }

        return ray;
    }

    /// <summary>
    /// Calculates the entrance pupil position and radius using forward ray tracing.
    /// Based on the ZEMAX_INTERFACE.EntrancePupil implementation.
    /// </summary>
    /// <returns>Tuple of (entrance pupil position relative to surface 1, entrance pupil radius).</returns>
    public (double Position, double Radius) CalculateEntrancePupil(OpticalSystem system, double wavelengthMicrons)
    {
        int stopIndex = system.StopIndex;

        // If no stop defined or stop is at surface 1, entrance pupil is at surface 1
        if (stopIndex <= 1 || stopIndex >= system.Surfaces.Count - 1)
        {
            // Stop at surface 1: EP is at surface 1
            double radius = GetStopRadius(system);
            return (0.0, radius);
        }

        // Get stop radius
        double stopRadius = system.Surfaces[stopIndex].SemiDiameter;
        if (stopRadius <= 0)
        {
            stopRadius = GetStopRadius(system);
        }

        double objectThickness = system.Surfaces[0].Thickness;
        bool isInfiniteConjugate = double.IsInfinity(objectThickness) || double.IsNaN(objectThickness);

        if (isInfiniteConjugate)
        {
            // Infinite conjugate: trace parallel ray from object space
            // Ray 1: parallel ray at height = stopRadius + 0.5, find where it hits stop
            double startHeight = stopRadius + 0.5;
            var ray1 = TraceToSurface(system, startHeight, 0.0, wavelengthMicrons, stopIndex);
            if (ray1.Count <= stopIndex)
                return (0.0, stopRadius);

            double yAtStop = ray1[stopIndex].Height;
            double yRatio = yAtStop / stopRadius;
            double epDiameter = 2.0 * startHeight / yRatio;

            // Find EP position using rays at angles
            double u_test = 0.1;
            double n_dc = Math.Sqrt(1.0 / (1.0 + u_test * u_test));
            double m_dc = Math.Sqrt(1.0 - n_dc * n_dc);

            // Trace ray from z=10 with angle
            var ray2 = TraceToSurface(system, 0.0, m_dc / n_dc, wavelengthMicrons, stopIndex, 10.0);
            if (ray2.Count <= stopIndex)
                return (0.0, epDiameter / 2.0);
            double y2 = ray2[stopIndex].Height;

            // Trace ray from z=-10 with same angle
            var ray3 = TraceToSurface(system, 0.0, m_dc / n_dc, wavelengthMicrons, stopIndex, -10.0);
            if (ray3.Count <= stopIndex)
                return (0.0, epDiameter / 2.0);
            double y3 = ray3[stopIndex].Height;

            // Linear interpolation to find z where y=0 at stop
            double slope_a = (y3 - y2) / (-10.0 - 10.0);
            double intercept_a = y3 - slope_a * (-10.0);
            double epPosition = intercept_a / slope_a;

            return (epPosition, epDiameter / 2.0);
        }
        else
        {
            // Finite conjugate: different calculation
            double startHeight = stopRadius + 0.5;
            var ray1 = TraceToSurface(system, startHeight, 0.0, wavelengthMicrons, stopIndex);
            if (ray1.Count <= stopIndex)
                return (0.0, stopRadius);

            double yAtStop = ray1[stopIndex].Height;
            double yRatio = yAtStop / stopRadius;
            double epDiameter = 2.0 * startHeight / yRatio;
            double targetY = epDiameter / 2.0;

            // Calculate slope for ray from object to stop
            double u_stop = stopRadius / objectThickness;
            double n_dc = Math.Sqrt(1.0 / (1.0 + u_stop * u_stop));
            double m_dc = Math.Sqrt(1.0 - n_dc * n_dc);

            // Trace ray with this slope to find actual y at stop
            var ray2 = TraceToSurface(system, 0.0, m_dc / n_dc, wavelengthMicrons, stopIndex);
            if (ray2.Count <= stopIndex)
                return (0.0, epDiameter / 2.0);

            double y2 = ray2[stopIndex].Height;

            // Adjust slope
            double u_adjusted = u_stop / (y2 / stopRadius);

            // EP position = distance from first surface where EP would appear
            double epPosition = (targetY / u_adjusted) - objectThickness;

            return (epPosition, epDiameter / 2.0);
        }
    }

    /// <summary>
    /// Helper to trace to a specific surface and return all states.
    /// </summary>
    private List<ParaxialRayState> TraceToSurface(OpticalSystem system, double y0, double u0,
        double wavelengthMicrons, int targetSurface, double zOffset = 0.0)
    {
        var states = new List<ParaxialRayState>();

        double y = y0;
        double nu = u0; // n*u where n=1 in object space
        double n = 1.0;

        // Handle z-offset by adjusting initial y based on slope
        if (Math.Abs(zOffset) > 1e-12)
        {
            y = y0 + zOffset * u0;
        }

        states.Add(new ParaxialRayState(0, y, nu, n));

        for (int i = 1; i <= targetSurface && i < system.Surfaces.Count; i++)
        {
            var prevSurface = system.Surfaces[i - 1];
            var currentSurface = system.Surfaces[i];

            double t = prevSurface.Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t))
            {
                double u = nu / n;
                y = y + t * u;
            }

            double nPrime = currentSurface.GetRefractiveIndex(wavelengthMicrons);
            double phi = 0.0;
            if (!currentSurface.IsFlat && !double.IsInfinity(currentSurface.Radius))
            {
                phi = (nPrime - n) / currentSurface.Radius;
            }

            double nuPrime = nu - y * phi;
            states.Add(new ParaxialRayState(i, y, nuPrime, nPrime));

            n = nPrime;
            nu = nuPrime;
        }

        return states;
    }

    /// <summary>
    /// Gets the stop radius from system parameters.
    /// </summary>
    private double GetStopRadius(OpticalSystem system)
    {
        int stopIndex = system.StopIndex > 0 ? system.StopIndex : 1;
        if (stopIndex < system.Surfaces.Count)
        {
            double semiDia = system.Surfaces[stopIndex].SemiDiameter;
            if (semiDia > 0)
                return semiDia;
        }

        // Fallback to aperture value
        if (system.ApertureType == ApertureType.EntrancePupilDiameter)
            return system.ApertureValue / 2.0;

        // Default
        return 5.0;
    }

    /// <summary>
    /// Traces the marginal ray through the system.
    /// Based on ZEMAX_INTERFACE.MarginalRay implementation.
    /// For infinite conjugate: parallel ray at EP edge height.
    /// For finite conjugate: ray from origin with slope toward EP edge.
    /// </summary>
    public ParaxialRay TraceMarginalRay(OpticalSystem system, double wavelengthMicrons)
    {
        // Get entrance pupil info
        var (epPosition, epRadius) = CalculateEntrancePupil(system, wavelengthMicrons);

        // Handle aperture type
        if (system.ApertureType == ApertureType.EntrancePupilDiameter)
        {
            epRadius = system.ApertureValue / 2.0;
        }

        double objectThickness = system.Surfaces[0].Thickness;
        bool isInfiniteConjugate = double.IsInfinity(objectThickness) || double.IsNaN(objectThickness);

        double y0, nu0;

        if (isInfiniteConjugate)
        {
            // Infinite conjugate: parallel ray at EP edge
            // RayInfo(0, ep.Item2, 0, 0, 0, 1) → y = epRadius, u = 0
            y0 = epRadius;
            nu0 = 0.0;
        }
        else
        {
            // Finite conjugate: ray from origin to EP edge
            // z1 = epPosition + objectThickness (distance from object to first surface via EP)
            double z1 = epPosition + objectThickness;

            if (Math.Abs(z1) < 1e-12)
            {
                // Degenerate case
                y0 = epRadius;
                nu0 = 0.0;
            }
            else
            {
                // Slope from origin to EP edge
                double u = epRadius / z1;
                // At surface 1 (after propagating objectThickness), y = 0 + objectThickness * u
                y0 = 0.0; // Ray starts at object (y=0 for marginal on axis)
                nu0 = u;  // n*u where n=1
            }
        }

        return Trace(system, y0, nu0, wavelengthMicrons);
    }

    /// <summary>
    /// Traces the chief ray through the system.
    /// Based on ZEMAX_INTERFACE.ChiefRay implementation.
    /// Chief ray passes through EP center.
    /// </summary>
    public ParaxialRay TraceChiefRay(OpticalSystem system, Field field, double wavelengthMicrons)
    {
        double objectThickness = system.Surfaces[0].Thickness;
        bool isInfiniteConjugate = double.IsInfinity(objectThickness) || double.IsNaN(objectThickness);

        // Get entrance pupil position
        var (epPosition, _) = CalculateEntrancePupil(system, wavelengthMicrons);

        // Get max field value
        double fieldValue = GetFieldValue(field);
        if (fieldValue < 1e-12)
            fieldValue = 0.1; // Minimum field to avoid degenerate case

        double y0, nu0;

        if (isInfiniteConjugate)
        {
            // Infinite conjugate cases
            // Chief ray passes through EP center (y=0 at z=-epPosition) with field-dependent slope.
            // Reference pattern: RayInfo(0, 0, -epPosition, m, 0, n) where m/n = tan(angle)
            // After propagation using thickness = z = -epPosition:
            //   y_at_surface1 = 0 + (-epPosition) * tan(angle) = -epPosition * tan(angle)
            switch (field.FieldType)
            {
                case FieldType.Angle:
                    double angleRad = fieldValue * Math.PI / 180.0;
                    double u1 = Math.Tan(angleRad);
                    y0 = -epPosition * u1;
                    nu0 = u1;
                    break;

                case FieldType.ObjectHeight:
                    // For infinite conjugate with object height - unusual case, treat as angle
                    double approxAngle = Math.Atan(fieldValue / 1000.0);
                    y0 = -epPosition * Math.Tan(approxAngle);
                    nu0 = Math.Tan(approxAngle);
                    break;

                case FieldType.ParaxialImageHeight:
                case FieldType.ImageHeight:
                    // y_image = efl * tan(angle) → angle = atan(y_image/efl)
                    double efl = CalculateEflFromParallelRay(system, wavelengthMicrons);
                    if (Math.Abs(efl) < 1e-10) efl = 100.0;
                    double u_img = fieldValue / efl;
                    y0 = -epPosition * u_img;
                    nu0 = u_img;
                    break;

                default:
                    y0 = 0;
                    nu0 = 0;
                    break;
            }
        }
        else
        {
            // Finite conjugate cases
            // Chief ray from object height through EP center
            double z1a = epPosition + objectThickness; // Distance from object to EP

            switch (field.FieldType)
            {
                case FieldType.ObjectHeight:
                    // Object at height y_max, ray goes through EP center (y=0 at z=epPosition from surface 1)
                    // Object is at z = -objectThickness from surface 1
                    // EP is at z = -epPosition from surface 1 (if epPosition > 0)
                    // Actually EP at z = epPosition in the convention where surface 1 is at z=0
                    // and object is at z = -objectThickness
                    //
                    // From your code:
                    // z1a = ep.Item1 + Surfaces[0].Thickness
                    // u5a = y1a / z1a
                    // return new RayInfo(0, y_max, 0, -m111a, 0, n111a)
                    //
                    // So: ray starts at (0, y_max, 0) with direction (-m, 0, n)
                    // This is at the OBJECT plane (z = -objectThickness in my coords, but
                    // your code seems to use z=0 as object position).
                    //
                    // The slope u = y_max / (epPosition + objectThickness)
                    // The ray starts at object height y_max and goes TOWARD EP center (y=0).
                    // So slope is negative: -m111a/n111a ≈ -u
                    //
                    // In my coord system where surface 1 is at z=0:
                    // Object is at z = -objectThickness, y = y_max
                    // EP center is at z = -epPosition, y = 0
                    // Ray travels from object to EP.
                    // Slope = (0 - y_max) / (-epPosition - (-objectThickness))
                    //       = -y_max / (objectThickness - epPosition)
                    //
                    // Hmm, this doesn't match z1a = epPosition + objectThickness exactly.
                    // Let me re-read your code...
                    //
                    // Oh I see: in your coordinate system, z=0 is at the object,
                    // and surface 1 is at z = objectThickness.
                    // EP is at z = epPosition + objectThickness (from the object).
                    //
                    // Ray from (0, y_max, 0) to (0, 0, z1a) where z1a = epPosition + objectThickness.
                    // Slope = (0 - y_max) / z1a = -y_max / z1a
                    //
                    // At surface 1 (z = objectThickness in your coords):
                    // y = y_max + slope * objectThickness = y_max - y_max * objectThickness / z1a
                    //   = y_max * (1 - objectThickness / z1a)
                    //   = y_max * (z1a - objectThickness) / z1a
                    //   = y_max * epPosition / z1a
                    //
                    // And the slope is -y_max / z1a (negative).

                    if (Math.Abs(z1a) < 1e-12)
                    {
                        y0 = fieldValue;
                        nu0 = 0;
                    }
                    else
                    {
                        // Object height IS the starting y position at the object plane
                        // Ray goes from (y=fieldValue) toward EP center (y=0 at z=z1a)
                        // Slope = (0 - fieldValue) / z1a = -fieldValue / z1a
                        y0 = fieldValue;
                        nu0 = -fieldValue / z1a;
                    }
                    break;

                case FieldType.Angle:
                    // Object angle for finite conjugate
                    // Chief ray passes through EP center (y=0 at EP)
                    // Object height: Y_obj = -tan(angle) * (objectThickness + epPosition)
                    // Trace() will propagate: Y_surf1 = Y_obj + tan(angle) * objectThickness
                    //                                 = -tan(angle) * epPosition
                    double angleRad2 = fieldValue * Math.PI / 180.0;
                    double u_angle = Math.Tan(angleRad2);
                    // y0 is the height at OBJECT, which Trace() will propagate to surface 1
                    y0 = -u_angle * z1a;  // z1a = epPosition + objectThickness
                    nu0 = u_angle;
                    break;

                case FieldType.ParaxialImageHeight:
                case FieldType.ImageHeight:
                    // Calculate object height from image height using magnification
                    var marginal = TraceMarginalRay(system, wavelengthMicrons);
                    double mag = CalculateLateralMagnification(marginal);
                    if (Math.Abs(mag) < 1e-12) mag = -1.0;
                    double y_obj = fieldValue / mag;
                    if (Math.Abs(z1a) < 1e-12)
                    {
                        y0 = y_obj;
                        nu0 = 0;
                    }
                    else
                    {
                        double u_m = y_obj / z1a;
                        y0 = y_obj * epPosition / z1a;
                        nu0 = -u_m;
                    }
                    break;

                default:
                    y0 = 0;
                    nu0 = 0;
                    break;
            }
        }

        return Trace(system, y0, nu0, wavelengthMicrons);
    }

    /// <summary>
    /// Gets the field value (max of X and Y) from a field specification.
    /// </summary>
    private double GetFieldValue(Field field)
    {
        double val = Math.Max(Math.Abs(field.X), Math.Abs(field.Y));
        if (val < 1e-12 && Math.Abs(field.Y) > 0)
            val = Math.Abs(field.Y);
        return val;
    }

    /// <summary>
    /// Calculates lateral magnification from a traced marginal ray.
    /// m = (n0 * u0) / (nf * uf)
    /// </summary>
    private double CalculateLateralMagnification(ParaxialRay marginalRay)
    {
        if (marginalRay.States.Count < 2)
            return -1.0;

        var first = marginalRay.States[0];
        var last = marginalRay.States[^1];

        double n0 = first.RefractiveIndex;
        double u0 = Math.Abs(n0) > 1e-12 ? first.Slope / n0 : first.Slope;

        double nf = last.RefractiveIndex;
        double uf = Math.Abs(nf) > 1e-12 ? last.Slope / nf : last.Slope;

        if (Math.Abs(uf) < 1e-12)
            return -1.0;

        return (n0 * u0) / (nf * uf);
    }

    /// <summary>
    /// Traces the chief ray using field angle in degrees (backward compatible).
    /// </summary>
    public ParaxialRay TraceChiefRay(OpticalSystem system, double fieldAngleDegrees, double wavelengthMicrons)
    {
        // Determine field type from system
        FieldType fieldType = FieldType.Angle;
        if (system.Fields.Count > 0)
        {
            fieldType = system.Fields[0].FieldType;
        }

        var field = new Field(0, fieldAngleDegrees, fieldType);
        return TraceChiefRay(system, field, wavelengthMicrons);
    }

    /// <summary>
    /// Traces both marginal and chief rays through the system.
    /// </summary>
    public ParaxialRayPair TraceRayPair(OpticalSystem system, double fieldAngleDegrees, double wavelengthMicrons)
    {
        return new ParaxialRayPair
        {
            MarginalRay = TraceMarginalRay(system, wavelengthMicrons),
            ChiefRay = TraceChiefRay(system, fieldAngleDegrees, wavelengthMicrons)
        };
    }

    /// <summary>
    /// Calculates the effective focal length (EFL) of the system.
    /// </summary>
    public double CalculateEfl(OpticalSystem system, double wavelengthMicrons)
    {
        return CalculateEflFromParallelRay(system, wavelengthMicrons);
    }

    /// <summary>
    /// Calculates EFL using the Lagrange invariant formula with both marginal and chief rays.
    /// This matches the user's proven formula exactly.
    /// </summary>
    private double CalculateEflFromParallelRay(OpticalSystem system, double wavelengthMicrons)
    {
        // Get entrance pupil
        var (epPosition, epRadius) = CalculateEntrancePupil(system, wavelengthMicrons);
        if (system.ApertureType == ApertureType.EntrancePupilDiameter)
        {
            epRadius = system.ApertureValue / 2.0;
        }

        // Get field angle (use max field or default to 20 degrees)
        double fieldAngleDeg = 20.0;
        if (system.Fields.Count > 0)
        {
            var maxField = system.Fields.MaxBy(f => Math.Max(Math.Abs(f.X), Math.Abs(f.Y)));
            if (maxField != null)
            {
                double fv = Math.Max(Math.Abs(maxField.X), Math.Abs(maxField.Y));
                if (fv > 0.1) fieldAngleDeg = fv;
            }
        }
        double fieldAngleRad = fieldAngleDeg * Math.PI / 180.0;

        // Initial angles in object space (n=1)
        // For infinite conjugate:
        // - Marginal ray: parallel (u0_m = 0), height = epRadius
        // - Chief ray: angle = tan(field), height = 0 at EP center
        double u0_m = 0.0;  // Parallel marginal ray
        double u0_c = Math.Tan(fieldAngleRad);  // Chief ray angle

        // Initial heights at surface 1
        // For parallel marginal ray at EP: y = epRadius (or use 1.0 for normalized)
        double y0_m = epRadius;
        double y0_c = -epPosition * u0_c;  // Chief ray at EP center projects to surface 1

        // Trace both rays from surface 1
        var marginalRay = TraceFromSurface(system, 1, y0_m, u0_m, wavelengthMicrons);
        var chiefRay = TraceFromSurface(system, 1, y0_c, u0_c, wavelengthMicrons);

        if (marginalRay.States.Count < 2 || chiefRay.States.Count < 2)
            return double.PositiveInfinity;

        // Get values at the LAST OPTICAL SURFACE (second-to-last state)
        // This matches the user's approach: Surfaces[Count-2]
        var mState = marginalRay.States[^2];
        var cState = chiefRay.States[^2];

        double n_k = mState.RefractiveIndex;  // Index after last optical surface (should be 1.0)
        double y_m = mState.Height;
        double y_c = cState.Height;
        double ul_m = mState.Slope / n_k;  // Final angle of marginal ray
        double ul_c = cState.Slope / n_k;  // Final angle of chief ray

        // User's formula (Lagrange invariant method):
        // L = y_m * n * u_c - y_c * n * u_m
        // efl = L / (n * (u0_m * ul_c - u0_c * ul_m))
        double L = y_m * n_k * ul_c - y_c * n_k * ul_m;
        double denom = n_k * (u0_m * ul_c - u0_c * ul_m);

        if (Math.Abs(denom) < 1e-12)
        {
            // Fallback to simple formula
            if (Math.Abs(ul_m) < 1e-12)
                return double.PositiveInfinity;
            return -y0_m / ul_m;
        }

        return L / denom;
    }

    /// <summary>
    /// Calculates EFL using the simple formula (for comparison/debugging).
    /// </summary>
    public double CalculateEflSimple(OpticalSystem system, double wavelengthMicrons)
    {
        var marginalRay = TraceMarginalRay(system, wavelengthMicrons);

        if (marginalRay.States.Count < 2)
            return double.PositiveInfinity;

        double y1 = marginalRay.InitialHeight;
        var finalState = marginalRay.States[^1];

        double nFinal = finalState.RefractiveIndex;
        double uPrimeK = nFinal > 0 ? finalState.Slope / nFinal : finalState.Slope;

        if (Math.Abs(uPrimeK) < 1e-12)
            return double.PositiveInfinity;

        return -y1 / uPrimeK;
    }

    /// <summary>
    /// Calculates the back focal length (BFL) of the system.
    /// </summary>
    public double CalculateBfl(OpticalSystem system, double wavelengthMicrons)
    {
        double y0 = 1.0;
        double nu0 = 0.0;

        var ray = TraceFromSurface(system, 1, y0, nu0, wavelengthMicrons);

        if (ray.States.Count < 2)
            return double.PositiveInfinity;

        var lastOpticalState = ray.States[^2];
        double y = lastOpticalState.Height;
        double n = lastOpticalState.RefractiveIndex;
        double u = n > 0 ? lastOpticalState.Slope / n : lastOpticalState.Slope;

        if (Math.Abs(u) < 1e-12)
            return double.PositiveInfinity;

        return -y / u;
    }

    /// <summary>
    /// Calculates the Lagrange invariant (optical invariant).
    /// H = n(y_m * u_c - y_c * u_m)
    /// </summary>
    public double CalculateLagrangeInvariant(OpticalSystem system, double fieldAngleDegrees, double wavelengthMicrons)
    {
        var marginalRay = TraceMarginalRay(system, wavelengthMicrons);
        var chiefRay = TraceChiefRay(system, fieldAngleDegrees, wavelengthMicrons);

        if (marginalRay.States.Count < 2 || chiefRay.States.Count < 2)
            return 0;

        var mState = marginalRay.States[1];
        var cState = chiefRay.States[1];

        double n = mState.RefractiveIndex;
        double y_m = mState.Height;
        double y_c = cState.Height;
        double u_m = mState.Slope / n;
        double u_c = cState.Slope / n;

        return n * (y_m * u_c - y_c * u_m);
    }

    /// <summary>
    /// Calculates the image height for a given field.
    /// </summary>
    public double CalculateImageHeight(OpticalSystem system, double fieldAngleDegrees, double wavelengthMicrons)
    {
        var chiefRay = TraceChiefRay(system, fieldAngleDegrees, wavelengthMicrons);
        return chiefRay.FinalHeight;
    }

    /// <summary>
    /// Gets the object distance from the system configuration.
    /// </summary>
    public static double GetObjectDistance(OpticalSystem system)
    {
        if (system.Surfaces.Count < 2)
            return double.NegativeInfinity;

        var objectSurface = system.Surfaces[0];
        double thickness = objectSurface.Thickness;

        if (double.IsInfinity(thickness) || double.IsNaN(thickness) || thickness <= 0)
        {
            return double.NegativeInfinity;
        }

        return -thickness;
    }

    /// <summary>
    /// Gets the maximum field value from the system.
    /// </summary>
    public static (double Value, FieldType Type) GetMaxField(OpticalSystem system)
    {
        if (system.Fields.Count == 0)
            return (0, FieldType.Angle);

        double maxValue = 0;
        FieldType type = system.Fields[0].FieldType;

        foreach (var field in system.Fields)
        {
            double val = Math.Max(Math.Abs(field.X), Math.Abs(field.Y));
            if (val > maxValue)
            {
                maxValue = val;
                type = field.FieldType;
            }
        }

        return (maxValue, type);
    }
}
