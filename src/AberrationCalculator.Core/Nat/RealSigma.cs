using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The aberration field decentre vectors measured from REAL RAYS, independently of the paraxial
/// perturbation formula.
///
/// <para>Thompson (2005) Sec. 3 defines <c>sigma_j</c> geometrically rather than by a formula:
/// it is where the line joining the centre of the pupil for surface <c>j</c> to that surface's
/// centre of curvature meets the image plane. The optical axis ray - from the centre of the
/// object field through the centre of the aperture stop - passes through every pupil centre, so
/// the definition has an operational reading:</para>
///
/// <blockquote><c>sigma_j</c> is the field at which the chief ray points straight at surface
/// <c>j</c>'s centre of curvature.</blockquote>
///
/// <para>That is measurable. The incidence of a real chief ray at surface <c>j</c>, taken in
/// that surface's own frame, is <c>c (x, y) + (u_x, u_y)</c>, and it vanishes exactly when the
/// ray points at the centre of curvature. In an aligned system it is <c>ibar_j H</c> and
/// vanishes on axis; in a perturbed one it is <c>ibar_j (H - sigma_j)</c>, so</para>
/// <code>
///     sigma_j = - incidence_j(H = 0) / (d incidence_j / dH)
/// </code>
/// <para>with both quantities read off traced rays. <b>Nothing here uses Gu's transfer
/// expression</b>, which is the point: this is the independent route against which the paraxial
/// one is checked, and it owes nothing to any program outside this repository.</para>
///
/// <h3>Aiming</h3>
///
/// <para>The chief ray has to be AIMED. <see cref="RealRayTrace"/> launches at the paraxial
/// entrance pupil with no aiming, which in a perturbed system does not pass through the centre
/// of the stop - and the optical axis ray is defined by passing through it. So the launch
/// coordinates are solved for by Newton iteration until the ray crosses the stop surface at its
/// own local origin, which is what "the centre of the aperture stop" means for a stop that has
/// itself been moved.</para>
/// </summary>
public sealed class RealSigma
{
    /// <summary>Measured field decentre vector of each surface, indexed like the surfaces.</summary>
    public Vec2[] Sigma { get; }

    /// <summary>Incidence of the optical axis ray at each surface, in that surface's frame.</summary>
    public Vec2[] AxisRayIncidence { get; }

    /// <summary>Measured field sensitivity of the incidence - the real-ray <c>ibar_j</c>.</summary>
    public Scalar[] FieldSensitivity { get; }

    /// <summary>True when the optical axis ray could be aimed at the stop centre.</summary>
    public bool Aimed { get; }

    /// <summary>Where the optical axis ray crossed the stop, in the stop's own frame.</summary>
    public Vec2 StopMiss { get; }

    private RealSigma(Vec2[] sigma, Vec2[] incidence, Scalar[] sensitivity, bool aimed, Vec2 miss)
    {
        Sigma = sigma; AxisRayIncidence = incidence; FieldSensitivity = sensitivity;
        Aimed = aimed; StopMiss = miss;
    }

    /// <summary>
    /// Measures the field decentre vectors. <paramref name="fieldStep"/> is the field, in
    /// degrees, used to measure the sensitivity of the incidence; it wants to be small enough to
    /// be linear and large enough not to be noise.
    /// </summary>
    public static RealSigma Measure(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                                    Scalar fieldStep = 0.01)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();
        int stop = StopSurface(system, last);

        var sigma = new Vec2[count];
        var incidence = new Vec2[count];
        var sensitivity = new Scalar[count];

        // Aim the optical axis ray at the centre of the stop.
        var aim = Aim(system, indices, p, 0.0, stop, out bool aimed, out Vec2 miss);

        var i0x = new Scalar[count]; var i0y = new Scalar[count];
        Trace(system, indices, p, 0.0, aim, i0x, i0y);

        // And again at a small field, to measure how the incidence responds to it. The chief
        // ray is re-aimed there too: a field point's chief ray is the one through the stop
        // centre at THAT field, not the axis ray tilted over.
        var aimF = Aim(system, indices, p, fieldStep, stop, out _, out _);
        var i1x = new Scalar[count]; var i1y = new Scalar[count];
        Trace(system, indices, p, fieldStep, aimF, i1x, i1y);

        // The field in the units sigma is expressed in: a fraction of the maximum field.
        Scalar maxField = MaxField(system);
        Scalar dH = SMath.Abs(maxField) > 1e-15 ? fieldStep / maxField : 0.0;

        for (int j = 1; j <= last; j++)
        {
            incidence[j] = new Vec2(i0x[j], i0y[j]);

            if (SMath.Abs(dH) < 1e-15) continue;
            Scalar dIdH = (i1y[j] - i0y[j]) / dH;
            sensitivity[j] = dIdH;

            if (SMath.Abs(dIdH) > 1e-12)
                sigma[j] = new Vec2(-i0x[j] / dIdH, -i0y[j] / dIdH);
        }

        return new RealSigma(sigma, incidence, sensitivity, aimed, miss);
    }

    /// <summary>The stop surface, or the last optical one if the design names none.</summary>
    private static int StopSurface(OpticalSystem system, int last)
    {
        for (int j = 1; j <= last; j++) if (system.Surfaces[j].IsStop) return j;
        return last;
    }

    private static Scalar MaxField(OpticalSystem system)
    {
        Scalar max = 0.0;
        foreach (var f in system.Fields) if (SMath.Abs(f.Y) > SMath.Abs(max)) max = f.Y;
        return max;
    }

    /// <summary>
    /// Solves for the launch pupil coordinates whose ray crosses the stop at its own origin,
    /// by Newton iteration on a numerically formed two-by-two Jacobian.
    ///
    /// <para>A difference quotient is honest here in a way it would not be inside the optimiser:
    /// this is a root find on a traced ray, not a gradient the search descends, and the answer
    /// is checked by the residual it achieves rather than trusted.</para>
    /// </summary>
    private static Vec2 Aim(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                            Scalar fieldDeg, int stop, out bool aimed, out Vec2 miss)
    {
        Scalar px = 0.0, py = 0.0;
        const double step = 1e-6;
        miss = Vec2.Zero;
        aimed = false;

        for (int k = 0; k < 40; k++)
        {
            var m0 = StopCrossing(system, indices, p, fieldDeg, px, py, stop);
            miss = m0;
            if (m0.Magnitude < 1e-12) { aimed = true; return new Vec2(px, py); }

            var mx = StopCrossing(system, indices, p, fieldDeg, px + step, py, stop);
            var my = StopCrossing(system, indices, p, fieldDeg, px, py + step, stop);

            Scalar a = (mx.X - m0.X) / step, b = (my.X - m0.X) / step;
            Scalar c = (mx.Y - m0.Y) / step, d = (my.Y - m0.Y) / step;
            Scalar det = a * d - b * c;
            if (SMath.Abs(det) < 1e-18) break;

            px -= (d * m0.X - b * m0.Y) / det;
            py -= (-c * m0.X + a * m0.Y) / det;
        }
        return new Vec2(px, py);
    }

    /// <summary>Where a ray launched at these pupil coordinates crosses the stop, in its frame.</summary>
    private static Vec2 StopCrossing(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                                     Scalar fieldDeg, Scalar px, Scalar py, int stop)
    {
        var hits = RealRayTrace.TraceRecord(system, indices, p, fieldDeg, py, px);
        if (stop >= hits.Length || !hits[stop].Ok) return new Vec2(1e6, 1e6);
        return new Vec2(hits[stop].X, hits[stop].Y);
    }

    private static void Trace(OpticalSystem system, Scalar[] indices, ParaxialResult p,
                             Scalar fieldDeg, Vec2 aim, Scalar[] ix, Scalar[] iy)
        => RealRayTrace.TraceRecord(system, indices, p, fieldDeg, aim.Y, aim.X, true, ix, iy);

}
