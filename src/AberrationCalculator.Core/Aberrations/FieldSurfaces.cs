using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Where the image surface this lens FORMS actually lies, as against the paraxial plane the
/// coefficients are referred to.
///
/// <para>The third-order sums already contain this; they just state it in a form that answers a
/// different question. <c>S3</c> and <c>S4</c> say how much astigmatism and Petzval curvature
/// there is, and dividing them by <c>2 n' u'^2</c> turns that into the LONGITUDINAL distance the
/// sagittal and tangential foci stand from the paraxial plane at full field - which is what a
/// designer choosing a detector, or a field flattener, actually wants to read.</para>
///
/// <para><b>This is also the answer to "why do the coefficients ignore a curved image surface".</b>
/// They do, and so does OpticStudio - the sums are a property of the LENS, referred to the
/// paraxial image point. The shape of the surface you catch the light on is not an input to them;
/// it is what they are telling you to choose. Feeding the detector back in would make the
/// coefficients redundant rather than richer. Real ray analyses are the ones that must read the
/// detector, because they answer the other question: what you get on the surface you picked.</para>
/// </summary>
public sealed class FieldSurfaces
{
    /// <summary>Field the sags were computed at, in the units the system states its fields in.</summary>
    public double Field { get; init; }

    /// <summary>Paraxial image height at that field - the radius the sags are quoted at.</summary>
    public double ImageHeight { get; init; }

    /// <summary>
    /// Radius of the Petzval surface. Negative means it curves toward the lens, which is the
    /// usual case and the reason a flat detector costs something. Infinite when the Petzval sum
    /// vanishes, which is what a flattened design is aiming at.
    /// </summary>
    public double PetzvalRadius { get; init; }

    /// <summary>
    /// Longitudinal distance from the paraxial plane to the Petzval surface at
    /// <see cref="Field"/>, positive when the focus falls SHORT of the paraxial plane - the sign
    /// convention every longitudinal aberration here and in OpticStudio uses.
    /// </summary>
    public double PetzvalSag { get; init; }

    /// <summary>The same for the sagittal focal surface, <c>(S3 + S4) / (2 n' u'^2)</c>.</summary>
    public double SagittalSag { get; init; }

    /// <summary>The medial surface, halfway between sagittal and tangential.</summary>
    public double MedialSag { get; init; }

    /// <summary>The tangential focal surface, <c>(3 S3 + S4) / (2 n' u'^2)</c>.</summary>
    public double TangentialSag { get; init; }

    /// <summary>
    /// Radius a detector would need in order to sit on the MEDIAL surface - the best single
    /// compromise between the two astigmatic foci. Positive sags give a negative radius, a
    /// detector concave toward the lens. Infinite when there is no field curvature to match.
    /// </summary>
    public double MedialMatchingRadius { get; init; }

    /// <summary>Curvature of the image surface the file describes, including any r-squared term.</summary>
    public double ImageSurfaceCurvature { get; init; }

    /// <summary>True when the file's image surface is not a plane.</summary>
    public bool ImageSurfaceIsCurved => Math.Abs(ImageSurfaceCurvature) > 1e-15;

    /// <summary>
    /// How far the file's image surface stands from the medial surface at full field: what the
    /// detector actually chosen leaves uncorrected. Zero when the two match. Meaningless, and
    /// left at zero, when the image surface is a plane - the medial sag is then the answer on
    /// its own.
    /// </summary>
    public double MedialResidual { get; init; }

    /// <summary>
    /// Computes the field surfaces from a third-order result and the trace it came from.
    /// </summary>
    /// <param name="system">The lens, read only for the image surface's own shape.</param>
    /// <param name="seidel">Third-order sums at the field of <paramref name="p"/>.</param>
    /// <param name="p">The paraxial trace, made at the field the sags are wanted at.</param>
    public static FieldSurfaces Compute(OpticalSystem system, SeidelResult seidel, ParaxialResult p,
                                        double field)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int last = system.LastOpticalSurface();
        double nPrime = p.N[last], uPrime = p.U[last];
        double scale = 2.0 * nPrime * uPrime * uPrime;

        double petzval = 0.0, sagittal = 0.0, medial = 0.0, tangential = 0.0;
        if (Math.Abs(scale) > 1e-18)
        {
            petzval    = seidel.TotalS4 / scale;
            sagittal   = (seidel.TotalS3 + seidel.TotalS4) / scale;
            medial     = (2.0 * seidel.TotalS3 + seidel.TotalS4) / scale;
            tangential = (3.0 * seidel.TotalS3 + seidel.TotalS4) / scale;
        }

        // The Petzval RADIUS is a property of the lens and carries no field: H^2 / S4 is the
        // same number whatever field the trace was made at, since S4 carries H^2 itself.
        double H = p.LagrangeInvariant;
        double petzvalRadius = Math.Abs(seidel.TotalS4) > 1e-18
            ? -H * H / seidel.TotalS4
            : double.PositiveInfinity;

        // A detector matching a focal surface curves TOWARD the lens when the sag is positive,
        // so its radius takes the opposite sign: sag = -c h^2 / 2.
        double h = p.ParaxialImageHeight;
        double matching = Math.Abs(medial) > 1e-15 && Math.Abs(h) > 1e-15
            ? -h * h / (2.0 * medial)
            : double.PositiveInfinity;

        double cImage = system.Surfaces[system.Surfaces.Count - 1].VertexCurvature;
        double residual = 0.0;
        if (Math.Abs(cImage) > 1e-15)
        {
            // The detector's own sag at that height, in the same sign convention as the
            // aberrations: a surface curving toward the lens brings the focus forward.
            double detector = -cImage * h * h / 2.0;
            residual = medial - detector;
        }

        return new FieldSurfaces
        {
            Field = field,
            ImageHeight = h,
            PetzvalRadius = petzvalRadius,
            PetzvalSag = petzval,
            SagittalSag = sagittal,
            MedialSag = medial,
            TangentialSag = tangential,
            MedialMatchingRadius = matching,
            ImageSurfaceCurvature = cImage,
            MedialResidual = residual,
        };
    }
}
