using System;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// An exact skew ray trace: no series, no small-angle approximation, no aberration theory.
///
/// <para><b>Why this exists.</b> Every traced number in this repository has until now come from
/// outside it, which makes the coefficients checkable only against files someone pasted in. The
/// real purpose is bigger: a transverse aberration polynomial can be INVERTED. Trace real rays,
/// separate the orders by how they scale, and each coefficient falls out on its own rather than
/// collapsed into an RMS spot where errors cancel. Hopkins (JOSA 66, 405) did exactly that
/// against ACCOS V and found agreement to roundoff. See <see cref="CoefficientInversion"/>.</para>
///
/// <para>Either conjugate. The trace itself never asks where the object is - it is handed a ray
/// at surface one's vertex plane and propagates it - so only the aiming has to know, and
/// <see cref="Launch"/> is where that knowledge lives.</para>
/// </summary>
public static class RealRayTrace
{
    /// <summary>Where a traced ray landed, and whether it got there.</summary>
    public readonly record struct Landing(Scalar Y, Scalar Z, bool Ok);

    /// <summary>
    /// Where a ray met one surface, and which way it left.
    ///
    /// <para>The position is in THAT surface's own vertex frame - x sagittal, y meridional,
    /// z along the axis, so z is the sag at the point of incidence and is zero on a plane. The
    /// direction cosines are the ones the ray carries AFTER refracting - or reflecting - there, which is what a
    /// merit function asking for an angle of emergence wants. At the image plane there is no
    /// refraction and they are simply the direction of arrival.</para>
    ///
    /// <para>Note that <see cref="Landing"/> names the sagittal coordinate Z, for historical
    /// reasons; here x is sagittal and z is axial, which is the convention the operand names
    /// RX, RY, RZ follow.</para>
    /// </summary>
    public readonly record struct SurfaceHit(Scalar X, Scalar Y, Scalar Z,
                                             Scalar L, Scalar M, Scalar N, bool Ok);

    /// <summary>
    /// Traces one ray to the image plane and returns its intercept.
    ///
    /// <para>The ray is launched at the paraxial entrance pupil, crossing it at the fractional
    /// coordinates given - no ray aiming, which is the convention the traced fans this is
    /// checked against were measured under.</para>
    /// </summary>
    /// <param name="py">Pupil coordinate along y, as a fraction of the pupil radius.</param>
    /// <param name="pz">Pupil coordinate along the perpendicular, same units.</param>
    /// <param name="fieldDeg">Field angle in degrees, in the y-z meridian.</param>
    /// <param name="atParaxialFocus">
    /// Where to catch the ray. True puts it on the paraxial image plane, which is what the
    /// aberration coefficients are referred to and what the traced fans this is checked
    /// against were measured on; false uses the plane the file itself specifies. The two
    /// differ by a real defocus on any design optimised to best focus, and confusing them
    /// shows up as an error strictly linear in the pupil.
    /// </param>
    public static Landing Trace(OpticalSystem system, Scalar[] indices, ParaxialResult paraxial,
                                Scalar fieldDeg, Scalar py, Scalar pz,
                                bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        var (x, y, dx, dy, dz) = Launch(system, paraxial, fieldDeg, py, pz);
        return TraceFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus);
    }

    /// <summary>
    /// <see cref="Trace"/>, keeping every surface the ray met. See
    /// <see cref="TraceRecordFrom"/> for how the result is indexed.
    /// </summary>
    public static SurfaceHit[] TraceRecord(OpticalSystem system, Scalar[] indices,
                                           ParaxialResult paraxial,
                                           Scalar fieldDeg, Scalar py, Scalar pz,
                                           bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        var (x, y, dx, dy, dz) = Launch(system, paraxial, fieldDeg, py, pz);
        return TraceRecordFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus);
    }

    /// <summary>
    /// <see cref="TraceRecord(OpticalSystem, Scalar[], ParaxialResult, Scalar, Scalar, Scalar,
    /// bool)"/> reporting the per-surface INCIDENCE as well - see the overload of
    /// <see cref="TraceRecordFrom"/> that fills the same arrays.
    /// </summary>
    public static SurfaceHit[] TraceRecord(OpticalSystem system, Scalar[] indices,
                                           ParaxialResult paraxial,
                                           Scalar fieldDeg, Scalar py, Scalar pz,
                                           bool atParaxialFocus,
                                           Scalar[]? incidenceX, Scalar[]? incidenceY)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        var (x, y, dx, dy, dz) = Launch(system, paraxial, fieldDeg, py, pz);
        return TraceRecordFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus,
                               incidenceX, incidenceY);
    }

    /// <summary>
    /// A ray of the given field and pupil coordinates, expressed at surface 1's vertex plane.
    ///
    /// <para>The ray crosses the entrance pupil at the fractional coordinates asked for. No ray
    /// aiming: it is the PARAXIAL pupil that is aimed at, which is the convention the traced fans
    /// in this repository were measured under.</para>
    ///
    /// <para><b>This is the only part of the trace that cares where the object is</b>, and it is
    /// why the field-and-pupil form once refused a finite conjugate. A collimated beam has its
    /// direction fixed by the field angle alone; light from a finite object does not, because the
    /// direction from the object point to the pupil depends on which pupil point. Given the
    /// object distance both are the same construction - a line through two known points - and
    /// everything downstream of here is conjugate-agnostic already.</para>
    /// </summary>
    private static (Scalar X, Scalar Y, Scalar Dx, Scalar Dy, Scalar Dz) Launch(
        OpticalSystem system, ParaxialResult paraxial, Scalar fieldDeg, Scalar py, Scalar pz)
    {
        Scalar epr = 0.5 * paraxial.Epd;
        Scalar ep = paraxial.EntrancePupilPosition;
        Scalar objectThickness = system.Surfaces[0].Thickness;

        if (Scalar.IsInfinity(objectThickness) || SMath.Abs(objectThickness) >= 1e12)
        {
            // Collimated. The field is in the y-z meridian, so the ray tilts in y only, and the
            // direction follows from the field angle with nothing else to know. Launch on the
            // pupil plane and walk BACK to surface one, so the first transfer of the trace proper
            // is the ordinary one.
            Scalar alpha = fieldDeg * SMath.PI / 180.0;
            Scalar idx = 0.0, idy = SMath.Sin(alpha), idz = SMath.Cos(alpha);
            Scalar back = -ep / idz;
            return (pz * epr + back * idx, py * epr + back * idy, idx, idy, idz);
        }

        // Finite. The ray is the line from the object point to the pupil point: the object sits
        // one object distance to the LEFT of surface one, at the height the field asks for, and
        // the pupil point is where the fractional coordinates put it on the entrance pupil.
        Scalar distance = SMath.Abs(objectThickness);
        Scalar height = ObjectHeight(system, fieldDeg, ep, distance);
        Scalar span = ep + distance;                 // object to pupil, along the axis

        Scalar dx = pz * epr;
        Scalar dy = py * epr - height;
        Scalar dz = span;

        // Slide along that line from the object to surface one's vertex plane. The fraction is
        // the same in every coordinate, the line being straight.
        Scalar fraction = SMath.Abs(span) > 1e-14 ? distance / span : 0.0;
        return (fraction * dx, height + fraction * dy, dx, dy, dz);
    }

    /// <summary>
    /// Where the object point of this field sits, as a height above the axis.
    ///
    /// <para>An object HEIGHT field states it outright. An object ANGLE field states it only
    /// implicitly, and the height it implies is the one the paraxial chief ray of that angle
    /// comes from: that ray crosses the axis at the entrance pupil, so at the object it stands
    /// <c>-tan(theta)</c> times the object-to-pupil distance off it. Taking it the same way the
    /// paraxial trace does is what keeps the real chief ray and the paraxial one describing the
    /// same field point.</para>
    /// </summary>
    private static Scalar ObjectHeight(OpticalSystem system, Scalar fieldDeg,
                                       Scalar entrancePupil, Scalar distance)
    {
        if (system.FieldType == Enums.FieldType.ObjectHeight) return fieldDeg;
        return -SMath.Tan(fieldDeg * SMath.PI / 180.0) * (entrancePupil + distance);
    }

    /// <summary>
    /// Traces a ray given explicitly at SURFACE 1's VERTEX PLANE, rather than by field angle
    /// and pupil coordinate.
    ///
    /// <para>This is what a finite conjugate needs. The field-and-pupil form above has to
    /// assume the ray is collimated in object space in order to know its direction, which is
    /// true only for an object at infinity; given the ray itself there is nothing left to
    /// assume, and the trace below never asks where the object is.</para>
    ///
    /// <para>Surface 1's vertex plane is also <see cref="Forbes.ForbesTrace"/>'s input base
    /// plane, so a ray expressed for one is expressed for the other without conversion.</para>
    /// </summary>
    /// <param name="x">Sagittal height at surface 1's vertex plane.</param>
    /// <param name="y">Meridional height at surface 1's vertex plane.</param>
    /// <param name="dx">Direction cosines, which need not be normalised.</param>
    public static Landing TraceFrom(OpticalSystem system, Scalar[] indices,
                                    ParaxialResult paraxial,
                                    Scalar x, Scalar y, Scalar dx, Scalar dy, Scalar dz,
                                    bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        var hits = TraceRecordFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus);
        var end = hits[hits.Length - 1];
        // Landing names the sagittal coordinate Z; SurfaceHit names it X.
        return new Landing(end.Y, end.X, end.Ok);
    }

    /// <summary>
    /// The same trace, keeping every surface the ray met on the way.
    ///
    /// <para>Indexed like <see cref="OpticalSystem.Surfaces"/>: entry 0 is the object and is
    /// never filled, entries 1 through <see cref="OpticalSystem.LastOpticalSurface"/> are the
    /// refracting and reflecting surfaces, and the final entry is the image plane. A ray that fails - misses a
    /// surface, or is totally internally reflected - leaves that entry and every later one with
    /// <c>Ok</c> false, so a caller can see HOW FAR it got rather than only that it did not
    /// arrive.</para>
    ///
    /// <para>This is what the optimiser's real-ray operands read: RX, RY and RZ are the
    /// position of one entry, RL, RM and RN its direction cosines. <see cref="TraceFrom"/> is
    /// this method keeping only the last entry, so the two cannot disagree.</para>
    /// </summary>
    public static SurfaceHit[] TraceRecordFrom(OpticalSystem system, Scalar[] indices,
                                               ParaxialResult paraxial,
                                               Scalar x, Scalar y,
                                               Scalar dx, Scalar dy, Scalar dz,
                                               bool atParaxialFocus = true)
        => TraceRecordFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus,
                           null, null);

    /// <summary>
    /// <see cref="TraceRecordFrom(OpticalSystem, Scalar[], ParaxialResult, Scalar, Scalar,
    /// Scalar, Scalar, Scalar, bool)"/>, additionally reporting the ray's INCIDENCE at each
    /// surface, in that surface's own frame.
    ///
    /// <para>The incidence is <c>c (x, y) + (u_x, u_y)</c> - the vector form of the paraxial
    /// <c>i = y c + u</c>, evaluated on the real ray before it refracts. It vanishes exactly
    /// when the ray points at the surface's centre of curvature, which is the geometric content
    /// of a nodal aberration theory sigma; on a plane it reduces to the ray slope, which
    /// vanishes when the ray is parallel to the axis, and that is the same statement with the
    /// centre of curvature at infinity.</para>
    ///
    /// <para>Reported separately rather than added to <see cref="SurfaceHit"/> because that type
    /// documents the direction AFTER refraction, which is what a merit-function operand asking
    /// for an angle of emergence wants. These are the directions before.</para>
    /// </summary>
    public static SurfaceHit[] TraceRecordFrom(OpticalSystem system, Scalar[] indices,
                                               ParaxialResult paraxial,
                                               Scalar x, Scalar y,
                                               Scalar dx, Scalar dy, Scalar dz,
                                               bool atParaxialFocus,
                                               Scalar[]? incidenceX, Scalar[]? incidenceY)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        int count = system.Surfaces.Count;
        var hits = new SurfaceHit[count];

        Scalar len = SMath.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-300) return hits;
        dx /= len; dy /= len; dz /= len;

        int last = system.LastOpticalSurface();
        Scalar z = 0.0;
        Scalar nBefore = indices.Length > 0 ? indices[0] : 1.0;

        for (int i = 1; i <= last; i++)
        {
            var s = system.Surfaces[i];

            // A tilted or decentred surface is traced in its OWN frame: the ray goes in, meets
            // an ordinary surface there, and comes back out. Nothing below learns that anything
            // was perturbed, so a sphere, a conic and a figured surface are all handled by the
            // same two calls. An aligned surface pays one boolean for the privilege.
            bool perturbed = LocalFrame.IsPerturbed(s);
            if (perturbed) LocalFrame.Into(s, ref x, ref y, ref z, ref dx, ref dy, ref dz);

            if (!Intersect(s, ref x, ref y, ref z, dx, dy, dz)) return hits;

            // The incidence, before refraction and in this surface's own frame. Zero when the
            // ray points at the centre of curvature.
            if (incidenceX != null && incidenceY != null && i < incidenceX.Length)
            {
                Scalar c = s.VertexCurvature;
                incidenceX[i] = c * x + dx / dz;
                incidenceY[i] = c * y + dy / dz;
            }

            Scalar nAfter = i < indices.Length ? indices[i] : 1.0;
            if (!Refract(s, x, y, nBefore, nAfter, ref dx, ref dy, ref dz))
                return hits;
            nBefore = nAfter;

            // Recorded in the surface's own vertex frame, which is what this type documents and
            // what an operand asking for an angle of emergence wants - so the hit is taken
            // BEFORE the ray is moved back out.
            hits[i] = new SurfaceHit(x, y, z, dx, dy, dz, true);

            if (perturbed) LocalFrame.OutOf(s, ref x, ref y, ref z, ref dx, ref dy, ref dz);

            // Into the next surface's vertex frame.
            Scalar t = s.Thickness;
            if (Scalar.IsInfinity(t) || Scalar.IsNaN(t)) return hits;
            z -= t;
        }

        // Transfer to the image plane. z is measured from the vertex plane of the surface
        // after the last optical one, so the paraxial focus sits at the difference between the
        // back focal length and the last thickness.
        // ParaxialFocusDistance and not Bfl. Bfl is where a COLLIMATED beam comes to focus,
        // which is the paraxial image only when the object is at infinity; on a 250 mm Cooke
        // triplet the two are 11.76 mm apart, and catching the rays there would be measuring a
        // defocused spot. They are equal to the bit at infinite conjugate, so this changes
        // nothing that was previously reachable.
        Scalar target = atParaxialFocus
            ? paraxial.ParaxialFocusDistance - system.Surfaces[last].Thickness
            : 0.0;
        Scalar tImage = (target - z) / dz;
        hits[count - 1] = new SurfaceHit(x + tImage * dx, y + tImage * dy, 0.0, dx, dy, dz, true);
        return hits;
    }

    /// <summary>
    /// Advances the ray to its intersection with the surface, by Newton iteration on the sag.
    /// The starting guess is the flat-surface crossing, which is exact for a plane and close
    /// for anything this program handles.
    /// </summary>
    private static bool Intersect(Surface s, ref Scalar x, ref Scalar y, ref Scalar z,
                                  Scalar dx, Scalar dy, Scalar dz)
    {
        if (SMath.Abs(dz) < 1e-14) return false;

        Scalar t = -z / dz;
        for (int k = 0; k < 64; k++)
        {
            Scalar xi = x + t * dx, yi = y + t * dy, zi = z + t * dz;
            Scalar r = SMath.Sqrt(xi * xi + yi * yi);
            Scalar sag = s.Sag(r);
            if (Scalar.IsNaN(sag)) return false;

            Scalar f = zi - sag;

            // d/dt of (z - sag(r)) = dz - S'(r) * (x dx + y dy) / r
            Scalar sp = SagSlope(s, r);
            Scalar drdt = r > 1e-14 ? (xi * dx + yi * dy) / r : 0.0;
            Scalar d = dz - sp * drdt;
            if (SMath.Abs(d) < 1e-14) return false;

            // Whether the RESIDUAL has converged. The correction below is applied anyway, and
            // that ordering is load-bearing.
            //
            // Newton converges on the value quadratically, so the last correction moves it by
            // nothing and looks like waste - which is why it is tempting to return before taking
            // it. It is not waste. At a point where f is zero, that correction is what sets
            // dt/dp: it evaluates to -(df/dp)/(df/dt), which is the implicit function theorem
            // applied to f(t, p) = 0 and is EXACT whatever dt/dp was before it. Return one step
            // early and the intersection carries a derivative one Newton step stale.
            //
            // On a curved surface the staleness is small and merely inexact. On a PLANE it is
            // total: the flat-surface starting guess is already exact, the loop would return on
            // its first pass, and a plano surface whose curvature is being optimised would report
            // that bending it does not move the ray at all.
            bool converged = SMath.Abs(f) < 1e-13;
            Scalar before = t;
            t -= f / d;

            // Or it has STALLED: the correction no longer moves t at all, so no further
            // iteration can change anything. The residual test above is absolute, and after a
            // long gap the roundoff in z + t dz alone is larger than it - Thompson's telescope
            // reaches its secondary 7490 mm after the primary, where the residual settles at
            // 4E-13 and never goes under 1E-13, and every off-axis ray used to be reported as a
            // miss after sixty-four iterations. A stalled ray is one the old test failed
            // forever, so this changes no ray that ever converged, to the bit. The guard on f
            // keeps it from accepting a stall far from the surface.
            if (!converged && t == before && SMath.Abs(f) < 1e-9 * (1.0 + SMath.Abs(t)))
                converged = true;

            if (converged)
            {
                x += t * dx; y += t * dy; z += t * dz;
                return true;
            }
        }
        return false;
    }

    /// <summary>dz/dr of the sag: the conic part in closed form, then the polynomial.</summary>
    private static Scalar SagSlope(Surface s, Scalar r)
    {
        Scalar slope = 0.0;
        Scalar c = s.Curvature;
        if (!SMath.Vanishes(c, 1e-15))
        {
            Scalar disc = 1.0 - (1.0 + s.Conic) * c * c * r * r;
            if (disc <= 0.0) return Scalar.NaN;
            slope = c * r / SMath.Sqrt(disc);
        }

        var a = s.AsphericCoefficients;
        Scalar rp = r;                                   // r, then r^3, r^5 ...
        for (int k = 0; k < a.Length; k++)
        {
            slope += 2.0 * (k + 1) * a[k] * rp;
            rp *= r * r;
        }
        return slope;
    }

    /// <summary>
    /// Snell's law in vector form. The surface normal comes from the implicit form
    /// F = z - S(r), whose gradient is (-S'(r) x/r, -S'(r) y/r, 1).
    /// </summary>
    private static bool Refract(Surface s, Scalar x, Scalar y, Scalar nBefore, Scalar nAfter,
                                ref Scalar dx, ref Scalar dy, ref Scalar dz)
    {
        if (SMath.Abs(nAfter) < 1e-15) return false;

        Scalar r = SMath.Sqrt(x * x + y * y);
        Scalar sp = SagSlope(s, r);
        if (Scalar.IsNaN(sp)) return false;

        Scalar nx = 0.0, ny = 0.0, nz = 1.0;
        if (r > 1e-14) { nx = -sp * x / r; ny = -sp * y / r; }
        Scalar len = SMath.Sqrt(nx * nx + ny * ny + nz * nz);
        nx /= len; ny /= len; nz /= len;

        // A mirror reflects: d' = d - 2 (d.n) n. The indices either side of it are the same
        // medium, and Snell's law between equal indices would carry the ray straight through -
        // which it did, until this branch, landing every ray on the parabola at the height it
        // was launched with. The reflected ray travels towards -z; everything downstream (the
        // intersection, the transfer by the file's negative thickness, the image plane) is
        // written along the ray and needs nothing more.
        if (s.IsMirror)
        {
            Scalar dn = dx * nx + dy * ny + dz * nz;
            dx -= 2.0 * dn * nx;
            dy -= 2.0 * dn * ny;
            dz -= 2.0 * dn * nz;
            return true;
        }

        Scalar mu = nBefore / nAfter;
        Scalar cosI = -(dx * nx + dy * ny + dz * nz);
        // Normal must oppose the ray, or the geometry below picks the wrong root.
        if (cosI < 0.0) { nx = -nx; ny = -ny; nz = -nz; cosI = -cosI; }

        Scalar k = 1.0 - mu * mu * (1.0 - cosI * cosI);
        if (k < 0.0) return false;                        // total internal reflection
        Scalar cosT = SMath.Sqrt(k);

        dx = mu * dx + (mu * cosI - cosT) * nx;
        dy = mu * dy + (mu * cosI - cosT) * ny;
        dz = mu * dz + (mu * cosI - cosT) * nz;

        Scalar d = SMath.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= d; dy /= d; dz /= d;
        return true;
    }
}
