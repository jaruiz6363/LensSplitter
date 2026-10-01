using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// The paraxial marginal and chief rays through a system, plus the first-order quantities
/// that come with them.
///
/// Everything the aberration coefficients need arrives here: at each surface the two rays
/// give (y, u) and (ybar, ubar), and those four numbers with the refractive indices are
/// what the third-, fifth- and seventh-order sums are built from. The pupils, focal length
/// and F/number fall out of the same trace rather than being computed separately, so they
/// cannot disagree with the rays.
/// </summary>
public sealed class ParaxialResult
{
    /// <summary>Marginal-ray height at each surface, indexed like <see cref="OpticalSystem.Surfaces"/>.</summary>
    public Scalar[] Y { get; init; } = Array.Empty<Scalar>();

    /// <summary>Marginal-ray slope in the medium AFTER each surface.</summary>
    public Scalar[] U { get; init; } = Array.Empty<Scalar>();

    /// <summary>Chief-ray height at each surface.</summary>
    public Scalar[] Ybar { get; init; } = Array.Empty<Scalar>();

    /// <summary>Chief-ray slope in the medium after each surface.</summary>
    public Scalar[] Ubar { get; init; } = Array.Empty<Scalar>();

    /// <summary>
    /// Refractive index of the medium after each surface, signed: the sign flips at every
    /// mirror so that a folded system traces with the same recurrence as a straight one.
    /// </summary>
    public Scalar[] N { get; init; } = Array.Empty<Scalar>();

    /// <summary>Effective focal length.</summary>
    public Scalar Efl { get; init; }

    /// <summary>
    /// System power, n_object / EFL, formed without a branch so it stays finite and
    /// differentiable at zero - which is where a design of parallel plates begins.
    /// </summary>
    public Scalar Power { get; init; }

    /// <summary>Back focal length: last surface to the paraxial focus of a collimated beam.</summary>
    public Scalar Bfl { get; init; }

    /// <summary>Entrance pupil diameter, whichever way the file stated its aperture.</summary>
    public Scalar Epd { get; init; }

    /// <summary>Entrance pupil position, measured from the first surface, positive to the right.</summary>
    public Scalar EntrancePupilPosition { get; init; }

    /// <summary>
    /// Exit pupil position, measured from the IMAGE plane and positive to the right - the
    /// convention every program this one reads files from uses when it reports the number,
    /// so the two can be compared directly.
    /// </summary>
    public Scalar ExitPupilPosition { get; init; }

    /// <summary>
    /// Exit pupil position measured from the last surface instead. Same pupil; this is the
    /// reference the aberration sums work in, and it avoids re-deriving it there.
    /// </summary>
    public Scalar ExitPupilFromLastSurface { get; init; }

    /// <summary>Exit pupil diameter.</summary>
    public Scalar ExitPupilDiameter { get; init; }

    /// <summary>Image-space F/number, EFL/EPD for an object at infinity.</summary>
    public Scalar FNumber { get; init; }

    /// <summary>Chief-ray height at the image surface: the paraxial image height of this field.</summary>
    public Scalar ImageHeight { get; init; }

    /// <summary>
    /// Distance from the last surface to where the paraxial marginal ray crosses the axis.
    /// This is where the image actually is, which need not be where the file put its image
    /// surface - a design saved at best focus for one conjugate and then used at another
    /// will differ, and the gap between this and the image surface is that defocus.
    /// </summary>
    public Scalar ParaxialFocusDistance { get; init; }

    /// <summary>
    /// Chief-ray height at the paraxial focus. Programs that report a "paraxial image
    /// height" for a finite conjugate mean this one; <see cref="ImageHeight"/> is the same
    /// ray measured at the image surface the file defines.
    /// </summary>
    public Scalar ParaxialImageHeight { get; init; }

    /// <summary>
    /// Transverse magnification, from the marginal ray as n*u / (n'*u'). A property of
    /// the conjugates, so it is defined for any finite-conjugate system whether or not it
    /// has an off-axis field and however its fields are stated. Zero for an object at
    /// infinity, where magnification does not apply.
    /// </summary>
    public Scalar Magnification { get; init; }

    /// <summary>
    /// Lagrange invariant, n(ubar*y - u*ybar). Conserved by the paraxial equations, so a
    /// varying value would mean the trace is wrong - see <see cref="InvariantDrift"/>.
    /// </summary>
    public Scalar LagrangeInvariant { get; init; }

    /// <summary>
    /// Largest relative departure of the Lagrange invariant from its object-space value
    /// across the system. A self-check on the trace: it should sit at rounding level.
    /// </summary>
    public Scalar InvariantDrift { get; init; }

    /// <summary>True when the object is at infinity.</summary>
    public bool InfiniteConjugate { get; init; }
}

/// <summary>Traces the two paraxial reference rays.</summary>
public static class ParaxialTrace
{
    /// <summary>Object distances at or beyond this are treated as infinite.</summary>
    private static readonly Scalar InfiniteObject = 1e12;

    /// <summary>
    /// Traces the marginal and chief rays for one field point.
    /// </summary>
    /// <param name="system">The lens. Surface 0 is the object, the last surface the image.</param>
    /// <param name="n">Index after each surface, from <c>IndexResolver.Build</c> at one wavelength.</param>
    /// <param name="field">
    /// The field point, in the units <see cref="OpticalSystem.FieldType"/> names: degrees of
    /// object angle, or object height in lens units.
    /// </param>
    public static ParaxialResult Trace(OpticalSystem system, Scalar[] n, Scalar field)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (n == null) throw new ArgumentNullException(nameof(n));

        int count = system.Surfaces.Count;
        if (count < 2) throw new InvalidOperationException("A system needs at least an object and an image surface.");
        if (n.Length < count) throw new ArgumentException("Index array is shorter than the surface list.", nameof(n));

        // Reflection is carried in the sign of the index rather than by turning the geometry
        // around: after an odd number of mirrors the ray runs the other way, and negating n
        // (against the already-negative thicknesses such a file stores) reproduces that
        // exactly while leaving one recurrence to trace forwards.
        var ns = new Scalar[count];
        Scalar sign = 1.0;
        for (int i = 0; i < count; i++)
        {
            if (system.Surfaces[i].IsMirror) sign = -sign;
            ns[i] = sign * SMath.Abs(n[i]);
        }

        int last = system.LastOpticalSurface();
        int stop = system.StopSurfaceIndex;
        if (stop < 1 || stop > last) stop = last;      // no stop marked: the last surface bounds the beam

        Scalar objectThickness = system.Surfaces[0].Thickness;
        bool infinite = Scalar.IsInfinity(objectThickness) || SMath.Abs(objectThickness) >= InfiniteObject;
        Scalar t0 = infinite ? 0.0 : SMath.Abs(objectThickness);

        // Two basis rays span every paraxial ray, so each ray this method needs is found by
        // solving a linear combination rather than by aiming and iterating.
        var basisA = Propagate(system, ns, 1.0, 0.0, last);     // unit height, parallel
        var basisB = Propagate(system, ns, 0.0, 1.0, last);     // on axis, unit slope

        // The chief ray crosses the axis at the stop; where a ray must have come from in
        // object space to do that is the entrance pupil.
        Scalar aStop = basisA.Y[stop], bStop = basisB.Y[stop];
        Scalar entrancePupil = SMath.Abs(aStop) > 1e-15 ? bStop / aStop : 0.0;

        // Focal length from the system's POWER: EFL = n_object / phi, with
        // phi = -n' u' / y. Written with the reduced angle n'u' rather than u' alone, this
        // is the number every design program prints, and the two agree exactly whenever
        // image space is air. They part company on a design whose last medium is glass -
        // -y/u' would then report the rear focal length in that glass, a factor n' larger.
        Scalar omegaLast = ns[last] * basisA.U[last];
        Scalar efl = SMath.Abs(omegaLast) > 1e-15 ? -ns[0] / omegaLast : Scalar.PositiveInfinity;

        // THE POWER, unbranched, because it is the quantity that survives a flat design.
        //
        // A system of parallel plates has no power, so its focal length is infinite - and an
        // infinite focal length is useless to an optimiser twice over: the residual is not a
        // number, and the derivative of 1/x at infinity is zero, so even a finite residual would
        // report that curvature cannot change the focal length. The power says the same thing
        // about the lens and stays finite and differentiable through zero, which is exactly the
        // state a design started from flats has to be pulled out of.
        Scalar power = -omegaLast / ns[0];
        Scalar bfl = SMath.Abs(basisA.U[last]) > 1e-15 ? -basisA.Y[last] / basisA.U[last] : Scalar.PositiveInfinity;

        Scalar epd = EntrancePupilDiameter(system, efl, t0, entrancePupil, infinite, ns[0]);

        // Marginal ray: from the axial object point to the edge of the entrance pupil. With
        // the object at infinity that is simply a parallel ray at the pupil edge.
        Scalar yMarg, uMarg;
        if (infinite)
        {
            yMarg = 0.5 * epd;
            uMarg = 0.0;
        }
        else
        {
            Scalar objectToPupil = t0 + entrancePupil;
            uMarg = SMath.Abs(objectToPupil) > 1e-15 ? 0.5 * epd / objectToPupil : 0.0;
            yMarg = uMarg * t0;
        }

        // Chief ray: from the edge of the field through the centre of the stop. A ray leaving
        // surface 1 at height y1 with object-space slope u0 is y1*A + u0*B, so it crosses the
        // axis at the stop when y1*aStop + u0*bStop = 0. Solved in that form, with no division
        // by aStop, it holds when the stop is imaged at infinity in object space (aStop = 0):
        // the chief ray is then parallel to the axis there. It used to be aimed at an entrance
        // pupil found as bStop/aStop, which such a lens does not have, and fell back to one at
        // surface 1 - a chief ray through the wrong point, and every field term built on it.
        bool pupilAtInfinity = SMath.Abs(aStop) <= 1e-12 * SMath.Abs(bStop);
        Scalar yChief, uChief;
        if (!infinite && system.FieldType == FieldType.ObjectHeight)
        {
            Scalar denominator = t0 * aStop + bStop;
            uChief = SMath.Abs(denominator) > 1e-15 * (SMath.Abs(t0 * aStop) + SMath.Abs(bStop))
                ? -field * aStop / denominator
                : 0.0;
            yChief = field + uChief * t0;
        }
        else
        {
            uChief = SMath.Tan(field * SMath.PI / 180.0);
            if (pupilAtInfinity && uChief != 0.0)
                throw new InvalidOperationException(
                    "The stop is imaged at infinity in object space, so every chief ray is parallel "
                    + "to the axis there and none leaves at a field angle. Give the field as an object "
                    + "height, or move the stop.");
            yChief = pupilAtInfinity ? 0.0 : -uChief * bStop / aStop;
        }

        var marginal = Propagate(system, ns, yMarg, uMarg, count - 1);
        var chief = Propagate(system, ns, yChief, uChief, count - 1);

        // The invariant is conserved by the paraxial equations, so tracking it costs nothing
        // and catches a bad index array or a mis-signed mirror.
        Scalar h0 = ns[0] * (chief.U[0] * marginal.Y[1] - marginal.U[0] * chief.Y[1]);
        Scalar drift = 0.0;
        if (SMath.Abs(h0) > 1e-15)
        {
            for (int i = 1; i <= last; i++)
            {
                Scalar hi = ns[i] * (chief.U[i] * marginal.Y[i] - marginal.U[i] * chief.Y[i]);
                drift = SMath.Max(drift, SMath.Abs((hi - h0) / h0));
            }
        }

        // The pupil is found relative to the last surface, then reported relative to the
        // image plane, which is where every program that prints this number measures from.
        Scalar lastToImage = 0.0;
        for (int i = last; i < count - 1; i++)
        {
            Scalar t = system.Surfaces[i].Thickness;
            if (!Scalar.IsInfinity(t) && !Scalar.IsNaN(t)) lastToImage += t;
        }
        // The exit pupil is the image of the stop: a property of the lens, not of the field.
        // Deriving it from the traced chief ray would lose it on a design whose only field
        // is on axis, where that ray is identically zero - so it comes from a pupil ray of
        // unit slope aimed through the stop, which exists whatever the fields are.
        // Through the stop's centre by the same combination as the chief ray, so that it exists
        // when the stop is imaged at infinity in object space too.
        var pupilRay = Propagate(system, ns, bStop, -aStop, last);
        Scalar exitPupil = SMath.Abs(pupilRay.U[last]) > 1e-15
            ? -pupilRay.Y[last] / pupilRay.U[last]
            : Scalar.PositiveInfinity;

        // Its diameter is the marginal ray's own height where that pupil sits.
        Scalar exitPupilDia = Scalar.IsInfinity(exitPupil)
            ? Scalar.PositiveInfinity
            : 2.0 * SMath.Abs(marginal.Y[last] + marginal.U[last] * exitPupil);
        // Where the image actually forms, and the chief-ray height there.
        Scalar focusDistance = SMath.Abs(marginal.U[last]) > 1e-15
            ? -marginal.Y[last] / marginal.U[last]
            : Scalar.PositiveInfinity;
        Scalar paraxialImageHeight = Scalar.IsInfinity(focusDistance)
            ? chief.Y[count - 1]
            : chief.Y[last] + chief.U[last] * focusDistance;
        // Transverse magnification from the MARGINAL ray: m = n*u / (n'*u').
        //
        // Magnification is a property of the conjugates, not of the field. Deriving it
        // from the chief ray as (image height / object height) needs a non-zero object
        // height to divide by, so it collapses to nothing on an on-axis-only design and
        // on any system whose fields are stated as angles - both of which are finite
        // conjugates with a perfectly well-defined magnification. The marginal-ray form
        // has no such dependency and agrees with the chief-ray ratio wherever that ratio
        // is computable.
        Scalar magnification = 0.0;
        if (!infinite)
        {
            Scalar omegaObject = ns[0] * marginal.U[0];
            Scalar omegaImage  = ns[last] * marginal.U[last];
            if (SMath.Abs(omegaImage) > 1e-15) magnification = omegaObject / omegaImage;
        }

        return new ParaxialResult
        {
            Y = marginal.Y,
            U = marginal.U,
            Ybar = chief.Y,
            Ubar = chief.U,
            N = ns,
            Efl = efl,
            Power = power,
            Bfl = bfl,
            Epd = epd,
            EntrancePupilPosition = entrancePupil,
            ExitPupilPosition = Scalar.IsInfinity(exitPupil) ? exitPupil : exitPupil - lastToImage,
            ExitPupilFromLastSurface = exitPupil,
            ExitPupilDiameter = exitPupilDia,
            FNumber = SMath.Abs(epd) > 1e-15 ? efl / epd : Scalar.PositiveInfinity,
            ImageHeight = chief.Y[count - 1],
            ParaxialFocusDistance = focusDistance,
            ParaxialImageHeight = paraxialImageHeight,
            Magnification = magnification,
            LagrangeInvariant = h0,
            InvariantDrift = drift,
            InfiniteConjugate = infinite,
        };
    }

    /// <summary>
    /// The entrance pupil diameter, whichever way the file chose to state the aperture.
    /// F/number and object-space NA are both converted here so that everything downstream
    /// sees one quantity.
    /// </summary>
    private static Scalar EntrancePupilDiameter(OpticalSystem system, Scalar efl, Scalar t0,
                                                Scalar entrancePupil, bool infinite, Scalar n0)
    {
        switch (system.Aperture.Type)
        {
            case ApertureType.FNumber:
                Scalar fno = system.Aperture.Value;
                return fno > 1e-12 ? SMath.Abs(efl) / fno : 0.0;

            case ApertureType.ObjectSpaceNA:
                // NA = n sin(theta), theta the marginal ray's angle leaving the axial object point,
                // and the pupil it fills is that ray carried to the entrance pupil: radius
                // z tan(theta). (This took NA/n itself as the slope - sin for tan - and filled a
                // pupil 0.12 % small at NA 0.05, 13 % at NA 0.5; OSLO and LensHH-LT take tan.)
                if (infinite) return 0.0;            // an object-space NA means nothing from infinity
                Scalar sinU = system.Aperture.Value / SMath.Abs(n0 == 0.0 ? 1.0 : n0);
                if (sinU >= 1.0)
                    throw new NotSupportedException($"Object-space NA {system.Aperture.Value} is not below the object-space index.");
                Scalar u = sinU / SMath.Sqrt(1.0 - sinU * sinU);
                return 2.0 * u * (t0 + entrancePupil);

            case ApertureType.EPD:
            default:
                return system.Aperture.Value;
        }
    }

    /// <summary>
    /// A chief ray for a lens that has no field: the paraxial ray that crosses the axis at the
    /// stop, scaled so that its Lagrange invariant with the given marginal ray is exactly one.
    ///
    /// <para>No field angle or object height is involved, so none is invented. The ray is the
    /// combination of the two basis rays that is zero at the stop, which exists for every lens -
    /// including one whose stop is imaged at infinity in object space, where the chief ray is
    /// parallel to the axis and no entrance-pupil position could be used to aim it. Its scale is
    /// a normalisation, the one the tertiary coefficients already use, not a field.</para>
    ///
    /// <para>Null when there is no such ray to be had: when the marginal ray itself crosses the
    /// axis at the stop, which is a lens with no aperture, and the invariant is zero for every
    /// ray.</para>
    /// </summary>
    /// <param name="n">Index after each surface, signed or not; it is signed here as in <see cref="Trace"/>.</param>
    /// <param name="yMarginal">Marginal-ray heights, as <see cref="ParaxialResult.Y"/>.</param>
    /// <param name="uMarginal">Marginal-ray slopes, as <see cref="ParaxialResult.U"/>.</param>
    public static (Scalar[] Ybar, Scalar[] Ubar)? UnitInvariantChiefRay(
        OpticalSystem system, Scalar[] n, Scalar[] yMarginal, Scalar[] uMarginal)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        int count = system.Surfaces.Count;
        if (count < 2 || n.Length < count || yMarginal.Length < 2 || uMarginal.Length < 1) return null;

        var ns = new Scalar[count];
        Scalar sign = 1.0;
        for (int i = 0; i < count; i++)
        {
            if (system.Surfaces[i].IsMirror) sign = -sign;
            ns[i] = sign * SMath.Abs(n[i]);
        }

        int last = system.LastOpticalSurface();
        int stop = system.StopSurfaceIndex;
        if (stop < 1 || stop > last) stop = last;

        // y1*A + u0*B is zero at the stop when (y1, u0) = (B's height there, -A's height there).
        var basisA = Propagate(system, ns, 1.0, 0.0, last);
        var basisB = Propagate(system, ns, 0.0, 1.0, last);
        var ray = Propagate(system, ns, basisB.Y[stop], -basisA.Y[stop], count - 1);

        Scalar h = ns[0] * (ray.U[0] * yMarginal[1] - uMarginal[0] * ray.Y[1]);
        Scalar scale = SMath.Abs(ns[0]) * (SMath.Abs(ray.U[0] * yMarginal[1]) + SMath.Abs(uMarginal[0] * ray.Y[1]));
        if (Scalar.IsNaN(h) || Scalar.IsInfinity(h) || !(SMath.Abs(h) > 1e-12 * scale)) return null;

        var ybar = new Scalar[count];
        var ubar = new Scalar[count];
        for (int i = 0; i < count; i++)
        {
            ybar[i] = ray.Y[i] / h;
            ubar[i] = ray.U[i] / h;
        }
        return (ybar, ubar);
    }

    /// <summary>
    /// Runs the paraxial recurrence from object space through surface
    /// <paramref name="through"/>, starting from a ray at height <paramref name="y1"/> on
    /// the first surface with object-space slope <paramref name="u0"/>.
    ///
    /// Reduced angles (omega = n*u) are used inside the loop because refraction is then a
    /// subtraction and transfer a multiplication, with no division by an index a mirror may
    /// have made negative. The slopes returned are ordinary u = omega/n.
    /// </summary>
    private static (Scalar[] Y, Scalar[] U) Propagate(OpticalSystem system, Scalar[] ns,
                                                      Scalar y1, Scalar u0, int through)
    {
        int count = system.Surfaces.Count;
        var y = new Scalar[count];
        var u = new Scalar[count];

        // The surface-0 entries describe the ray in object space, which is what the
        // aberration sums and the invariant check expect to find there.
        u[0] = u0;
        y[0] = Scalar.IsInfinity(system.Surfaces[0].Thickness)
            ? 0.0
            : y1 - u0 * SMath.Abs(system.Surfaces[0].Thickness);

        Scalar omega = ns[0] * u0;
        if (count > 1) y[1] = y1;

        int limit = SMath.Min(through, count - 1);
        for (int i = 1; i <= limit; i++)
        {
            Scalar power = SurfacePower(system.Surfaces[i], ns[i - 1], ns[i]);

            omega -= y[i] * power;
            u[i] = omega / ns[i];

            if (i + 1 < count)
            {
                Scalar t = system.Surfaces[i].Thickness;
                if (Scalar.IsInfinity(t) || Scalar.IsNaN(t)) t = 0.0;
                y[i + 1] = y[i] + t * u[i];
            }
        }

        // Past the requested surface nothing refracts, so the ray keeps its last state and
        // callers that asked only for the optical part still get a fully filled array.
        for (int i = limit + 1; i < count; i++) u[i] = u[limit];

        return (y, u);
    }

    /// <summary>
    /// Refracting power of one surface. A real surface takes it from its curvature and the
    /// index step; an ideal paraxial surface states it directly as a focal length; the
    /// index-transparent surface types have none.
    /// </summary>
    private static Scalar SurfacePower(Surface s, Scalar nBefore, Scalar nAfter)
    {
        switch (s.Type)
        {
            case SurfaceType.Paraxial:
                return SMath.Abs(s.FocalLength) > 1e-15 ? nBefore / s.FocalLength : 0.0;

            case SurfaceType.CoordinateBreak:
            case SurfaceType.Abcd:
            case SurfaceType.Unsupported:
                return 0.0;

            default:
                return s.VertexCurvature * (nAfter - nBefore);
        }
    }
}
