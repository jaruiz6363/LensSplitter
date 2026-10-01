using System;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// The transform between the mechanical axis and one surface's own frame, for a surface that
/// has been tilted or decentred.
///
/// <para>A perturbed surface is traced by moving the RAY into the surface's frame, intersecting
/// and refracting there exactly as an aligned surface is, and moving the ray back out again.
/// That is the whole mechanism: the surface code never learns that anything is tilted, so a
/// figured surface, a conic and a sphere are all perturbed by the same few lines and none of
/// them acquires a special case.</para>
///
/// <h3>Provenance, and why the conventions are copied rather than derived</h3>
///
/// <para>The rotation construction, its composition order and its sign convention are ported
/// from <c>coordinate_break_C</c> in this repository author's own earlier C++ ray tracer -
/// the same codebase <c>references.md</c> already cites for its independent implementation of
/// Buchdahl's coefficients. It is a tested implementation of a transform that is easy to get
/// subtly wrong, and reproducing it is worth more than re-deriving it.</para>
///
/// <para>What that source fixes, and what is therefore not a choice made here:</para>
/// <list type="bullet">
/// <item><description>Angles in DEGREES, which is what <c>.align</c> states and what every lens
/// format states.</description></item>
/// <item><description><b>The tilt about x carries a minus sign where the tilt about y does
/// not.</b> That asymmetry is the single most error-prone detail in the whole transform.</description></item>
/// <item><description>The rotations compose as <c>R = Rz . Rx . Ry</c>.</description></item>
/// <item><description>A DIRECTION is rotated and never offset; a POSITION is both.</description></item>
/// </list>
///
/// <para>The tilt about z is not carried. A rotationally symmetric surface is unchanged by one,
/// so it would be a parameter that could only ever be zero or wrong.</para>
///
/// <para>The ORDER convention likewise follows the source: decentre first, then rotate. The
/// inverse therefore rotates back first and then undoes the decentre, which is derived here
/// rather than copied - it is matrix inversion, not convention, and <c>R</c> is orthogonal so
/// its inverse is its transpose.</para>
/// </summary>
public static class LocalFrame
{
    private const double Degrees = Math.PI / 180.0;

    /// <summary>
    /// The rotation of a surface's frame, as a 3x3 matrix in row-major order.
    /// <paramref name="tiltX"/> and <paramref name="tiltY"/> are in RADIANS here - the degrees
    /// the file speaks are converted once, when the alignment is applied to the model.
    /// </summary>
    private readonly struct Matrix
    {
        public readonly Scalar M0, M1, M2, M3, M4, M5, M6, M7, M8;

        public Matrix(Scalar m0, Scalar m1, Scalar m2, Scalar m3, Scalar m4,
                      Scalar m5, Scalar m6, Scalar m7, Scalar m8)
        {
            M0 = m0; M1 = m1; M2 = m2; M3 = m3; M4 = m4; M5 = m5; M6 = m6; M7 = m7; M8 = m8;
        }
    }

    private static Matrix Rotation(Scalar tiltX, Scalar tiltY)
    {
        // The sign of the x tilt is negated and the y tilt is not. Copied, not chosen.
        Scalar cb = SMath.Cos(-tiltX), sb = SMath.Sin(-tiltX);
        Scalar ca = SMath.Cos(tiltY), sa = SMath.Sin(tiltY);

        // Rx(beta) . Ry(alpha), with Rz omitted because tilt about z is not carried.
        //
        //  Rx = [ 1   0     0   ]     Ry = [ ca  0  -sa ]
        //       [ 0   cb  -sb   ]          [ 0   1   0  ]
        //       [ 0   sb   cb   ]          [ sa  0   ca ]
        return new Matrix(ca,      0.0,  -sa,
                          sb * sa, cb,   sb * ca,
                          cb * sa, -sb,  cb * ca);
    }

    /// <summary>Whether this surface needs the transform at all.</summary>
    public static bool IsPerturbed(Surface s) => s != null && s.IsPerturbed;

    /// <summary>
    /// Takes a ray from the mechanical frame into the surface's own: decentre first, then
    /// rotate, following the source convention.
    /// </summary>
    public static void Into(Surface s, ref Scalar x, ref Scalar y, ref Scalar z,
                            ref Scalar dx, ref Scalar dy, ref Scalar dz)
    {
        var r = Rotation(s.TiltX, s.TiltY);

        Scalar px = x - s.DecenterX, py = y - s.DecenterY, pz = z;
        x = r.M0 * px + r.M1 * py + r.M2 * pz;
        y = r.M3 * px + r.M4 * py + r.M5 * pz;
        z = r.M6 * px + r.M7 * py + r.M8 * pz;

        Scalar ux = dx, uy = dy, uz = dz;
        dx = r.M0 * ux + r.M1 * uy + r.M2 * uz;
        dy = r.M3 * ux + r.M4 * uy + r.M5 * uz;
        dz = r.M6 * ux + r.M7 * uy + r.M8 * uz;
    }

    /// <summary>
    /// Takes a ray back out into the mechanical frame. The inverse of <see cref="Into"/>:
    /// rotate back, then undo the decentre. <c>R</c> is orthogonal, so the inverse rotation is
    /// the transpose and the indices below are simply read the other way.
    /// </summary>
    public static void OutOf(Surface s, ref Scalar x, ref Scalar y, ref Scalar z,
                             ref Scalar dx, ref Scalar dy, ref Scalar dz)
    {
        var r = Rotation(s.TiltX, s.TiltY);

        Scalar px = x, py = y, pz = z;
        x = r.M0 * px + r.M3 * py + r.M6 * pz + s.DecenterX;
        y = r.M1 * px + r.M4 * py + r.M7 * pz + s.DecenterY;
        z = r.M2 * px + r.M5 * py + r.M8 * pz;

        Scalar ux = dx, uy = dy, uz = dz;
        dx = r.M0 * ux + r.M3 * uy + r.M6 * uz;
        dy = r.M1 * ux + r.M4 * uy + r.M7 * uz;
        dz = r.M2 * ux + r.M5 * uy + r.M8 * uz;
    }
}
