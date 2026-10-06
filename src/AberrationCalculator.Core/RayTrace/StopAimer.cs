using System;
using System.Collections.Concurrent;
using System.Linq;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// Ray aiming: where in the PARAXIAL entrance pupil to launch the real ray that crosses the real
/// stop at a given point.
///
/// <para><see cref="RealRayTrace"/> launches every ray at the paraxial entrance pupil, which is
/// right for a lens whose pupil is well corrected and wrong for one whose is not: a fast lens's
/// pupil aberrations make the paraxial pupil overfill the real stop, so its rim rays are light
/// the stop does not let in, and its chief ray misses the stop's centre. The cure is to aim
/// each ray at the stop and solve for where it must start.</para>
///
/// <para>The answer is a launch point in the same coordinates <see cref="RealRayTrace.Trace"/>
/// takes - fractions of the paraxial entrance pupil's radius - so an aimed ray is traced exactly
/// as an unaimed one is, from the point found here. The target is in the stop's own frame, as a
/// fraction of <see cref="StopHeight"/>, signed - by default the PARAXIAL marginal ray's height
/// there, so that a target of (1, 0) is the ray through the stop where the paraxial marginal ray
/// crosses it.</para>
///
/// <para><b>The method.</b> Newton's, on the map from launch point to stop crossing, which is
/// smooth and nearly linear: the inverse Jacobian of the chief ray starts every search, Broyden's
/// update improves it with each trace, and a fresh one is taken by differences where that
/// converges slowly. To a billionth of the stop height. Ported unchanged from GhostAnalysis,
/// where it was written and verified, so that GhostAnalysis can use this copy and give the same
/// answers to the bit.</para>
///
/// <para>How a ray is carried to the stop is a parameter, <see cref="StopCrossing"/>. For an
/// ordinary lens <see cref="ForSystem"/> supplies it, tracing a copy of the lens cut off just
/// after the stop. A caller whose light does something the trace cannot - a ghost that diffracts
/// on the way to its stop - supplies its own.</para>
/// </summary>
public sealed class StopAimer
{
    /// <summary>
    /// Where the ray launched at (<paramref name="py"/>, <paramref name="pz"/>) in the paraxial
    /// entrance pupil crosses the stop, in the stop's own frame - meridional, then sagittal - or
    /// null if it does not get there.
    /// </summary>
    public delegate (double Y, double X)? StopCrossing(double fieldDeg, double py, double pz);

    /// <summary>
    /// How the rays are aimed at one field: the launch point of the ray through the stop's centre,
    /// and the inverse of the Jacobian there - launch point against stop crossing - which starts
    /// every other ray's search.
    /// </summary>
    private sealed record Aim(double Y0, double X0, double Iyy, double Iyx, double Ixy, double Ixx);

    private readonly StopCrossing _crossing;
    private readonly ConcurrentDictionary<double, Aim?> _aims = new();

    /// <param name="crossing">How a launched ray is carried to the stop.</param>
    /// <param name="stopHeight">
    /// The stop's radius, signed like the marginal ray's height there: the length a target
    /// coordinate of 1 stands for. Must not be zero.
    /// </param>
    public StopAimer(StopCrossing crossing, double stopHeight)
    {
        _crossing = crossing ?? throw new ArgumentNullException(nameof(crossing));
        if (stopHeight == 0.0 || double.IsNaN(stopHeight))
            throw new ArgumentException("The stop height must be a nonzero number.", nameof(stopHeight));
        StopHeight = stopHeight;
    }

    /// <summary>The stop's radius, signed: the length a target coordinate of 1 stands for.</summary>
    public double StopHeight { get; }

    /// <summary>
    /// An aimer for an ordinary lens: rays are carried to <paramref name="stop"/> by tracing the
    /// lens as far as it and no further, which is all the search needs and a fraction of the work
    /// of tracing to the image.
    /// </summary>
    /// <param name="stop">The stop surface; the system's own (<see cref="OpticalSystem.StopSurfaceIndex"/>) if negative.</param>
    /// <param name="stopHeight">
    /// What a target coordinate of 1 stands for, signed; the paraxial marginal ray's height at the
    /// stop if null. <see cref="RealAxialHeight"/> gives the real stop's.
    /// </param>
    public static StopAimer ForSystem(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                      int stop = -1, double? stopHeight = null)
    {
        var crossing = ToStop(system, indices, paraxial, ref stop);
        return new StopAimer(crossing, stopHeight ?? paraxial.Y[stop]);
    }

    /// <summary>
    /// Where the real axial marginal ray - launched at the rim of the paraxial entrance pupil -
    /// crosses the stop, signed: the radius of the stop the beam actually fills. Aimed at this
    /// radius, an axial ray is the ray launched at the same pupil coordinates, so aiming changes
    /// nothing on axis. Zemax OpticStudio's real ray aiming scales the stop so, by the primary
    /// wavelength's ray, for every wavelength. Null if the ray does not get there.
    /// </summary>
    public static double? RealAxialHeight(OpticalSystem system, double[] indices, ParaxialResult paraxial, int stop = -1)
    {
        var crossing = ToStop(system, indices, paraxial, ref stop);
        return crossing(0.0, 1.0, 0.0) is (double y, _) && y != 0.0 ? y : null;
    }

    private static StopCrossing ToStop(OpticalSystem system, double[] indices, ParaxialResult paraxial, ref int stop)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (stop < 0) stop = system.StopSurfaceIndex;
        if (stop < 1 || stop > system.LastOpticalSurface())
            throw new ArgumentException("The system has no stop among its optical surfaces.", nameof(stop));
        int at = stop;

        // The lens as far as the stop, and a plane behind it that serves as the image. The
        // surfaces are the lens's own, shared rather than copied.
        var surfaces = system.Surfaces.Take(at + 1).Append(new Surface { Thickness = 0.0 }).ToList();
        var toStop = new OpticalSystem { FieldType = system.FieldType, Surfaces = surfaces };
        var toStopN = indices.Take(at + 1).Append(indices[at]).ToArray();

        return (field, py, pz) =>
        {
            try
            {
                var hits = RealRayTrace.TraceRecord(toStop, toStopN, paraxial, field, py, pz,
                                                    atParaxialFocus: false);
                for (int i = 1; i <= at; i++)
                    if (!hits[i].Ok) return null;
                return (hits[at].Y, hits[at].X);
            }
            catch (InvalidOperationException) { return null; }
        };
    }

    /// <summary>
    /// Whether any ray of this field reaches the stop's centre - false when the chief ray itself
    /// cannot be found, in which case <see cref="Launch"/> and <see cref="Predict"/> return null
    /// for every target.
    /// </summary>
    public bool CanAim(double fieldDeg) => _aims.GetOrAdd(fieldDeg, AimAt) != null;

    /// <summary>
    /// Where in the paraxial entrance pupil to launch the ray that crosses the stop at
    /// (<paramref name="stopY"/>, <paramref name="stopX"/>) times <see cref="StopHeight"/>,
    /// solved to a billionth of the stop height. Null where no ray gets there.
    /// </summary>
    public (double Py, double Pz)? Launch(double fieldDeg, double stopY, double stopX)
    {
        var aim = _aims.GetOrAdd(fieldDeg, AimAt);
        if (aim == null) return null;
        double ty = stopY * StopHeight, tx = stopX * StopHeight;
        double ly = aim.Y0 + aim.Iyy * ty + aim.Iyx * tx;
        double lx = aim.X0 + aim.Ixy * ty + aim.Ixx * tx;
        return Solve(fieldDeg, ty, tx, ly, lx, aim);
    }

    /// <summary>
    /// The launch point the chief ray's Jacobian PREDICTS for a target, with no search: exact
    /// to first order in the distance from the stop's centre, so for a ray a whisker from the
    /// chief ray it lands on the target to a part in a million of the whisker. Null when the
    /// field cannot be aimed.
    /// </summary>
    public (double Py, double Pz)? Predict(double fieldDeg, double stopY, double stopX)
    {
        var aim = _aims.GetOrAdd(fieldDeg, AimAt);
        if (aim == null) return null;
        double ty = stopY * StopHeight, tx = stopX * StopHeight;
        return (aim.Y0 + aim.Iyy * ty + aim.Iyx * tx, aim.X0 + aim.Ixy * ty + aim.Ixx * tx);
    }

    /// <summary>The chief ray's launch point at one field, and the Jacobian there; null if no ray reaches the stop's centre.</summary>
    private Aim? AimAt(double field)
    {
        var start = Jacobian(field, 0.0, 0.0);
        if (start == null) return null;
        var chief = Solve(field, 0.0, 0.0, 0.0, 0.0, start);
        if (chief is not (double y0, double x0)) return null;
        var j = Jacobian(field, y0, x0);
        return j == null ? null : j with { Y0 = y0, X0 = x0 };
    }

    /// <summary>
    /// The launch point whose ray crosses the stop at (tx, ty), from (lx, ly): Broyden's method,
    /// its inverse Jacobian that of <paramref name="j"/> to start with and bettered by each step,
    /// one trace a step; a fresh one taken by differences if it stalls. To a billionth of the
    /// stop's height.
    /// </summary>
    private (double Py, double Pz)? Solve(double field, double ty, double tx, double ly, double lx, Aim j)
    {
        double tolerance = 1e-9 * Math.Abs(StopHeight);
        double hyy = j.Iyy, hyx = j.Iyx, hxy = j.Ixy, hxx = j.Ixx;
        double last = double.PositiveInfinity, fy = 0, fx = 0, dy = 0, dx = 0;
        for (int k = 0; k < 40; k++)
        {
            if (_crossing(field, ly, lx) is not (double sy, double sx)) return null;
            double ry = sy - ty, rx = sx - tx;
            double r = Math.Sqrt(ry * ry + rx * rx);
            if (r <= tolerance) return (ly, lx);
            if (r > 0.5 * last && Jacobian(field, ly, lx) is { } fresh)
            {
                (hyy, hyx, hxy, hxx) = (fresh.Iyy, fresh.Iyx, fresh.Ixy, fresh.Ixx);
            }
            else if (k > 0)
            {
                // Broyden's good update of the inverse: H += (Δx - H Δf) (Δxᵀ H) / (Δxᵀ H Δf).
                double gy = ry - fy, gx = rx - fx;                              // Δf
                double hgy = hyy * gy + hyx * gx, hgx = hxy * gy + hxx * gx;    // H Δf
                double denom = dy * hgy + dx * hgx;
                if (Math.Abs(denom) > 1e-300)
                {
                    double uy = (dy - hgy) / denom, ux = (dx - hgx) / denom;
                    double vy = dy * hyy + dx * hxy, vx = dy * hyx + dx * hxx;  // Δxᵀ H
                    hyy += uy * vy; hyx += uy * vx; hxy += ux * vy; hxx += ux * vx;
                }
            }
            last = r;
            fy = ry; fx = rx;
            dy = -(hyy * ry + hyx * rx);
            dx = -(hxy * ry + hxx * rx);
            ly += dy;
            lx += dx;
        }
        return null;
    }

    /// <summary>The inverse Jacobian at one launch point, by central differences; null where it cannot be taken.</summary>
    private Aim? Jacobian(double field, double ly, double lx)
    {
        const double d = 1e-5;
        if (_crossing(field, ly + d, lx) is not (double y1, double x1) || _crossing(field, ly - d, lx) is not (double y2, double x2) ||
            _crossing(field, ly, lx + d) is not (double y3, double x3) || _crossing(field, ly, lx - d) is not (double y4, double x4))
            return null;
        double a = (y1 - y2) / (2 * d), c = (x1 - x2) / (2 * d);      // d(stop y, stop x) / d(launch y)
        double b = (y3 - y4) / (2 * d), e = (x3 - x4) / (2 * d);      // d(stop y, stop x) / d(launch x)
        double det = a * e - b * c;
        if (!IsFinite(det) || Math.Abs(det) < 1e-300) return null;
        return new Aim(ly, lx, e / det, -b / det, -c / det, a / det);
    }

    // double.IsFinite is not in netstandard2.0.
    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
