using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>Third-order sums, per surface and totalled.</summary>
public sealed class SeidelResult
{
    /// <summary>Spherical aberration contribution of each surface.</summary>
    public double[] S1 { get; init; } = Array.Empty<double>();

    /// <summary>Coma.</summary>
    public double[] S2 { get; init; } = Array.Empty<double>();

    /// <summary>Astigmatism.</summary>
    public double[] S3 { get; init; } = Array.Empty<double>();

    /// <summary>Petzval field curvature.</summary>
    public double[] S4 { get; init; } = Array.Empty<double>();

    /// <summary>Distortion.</summary>
    public double[] S5 { get; init; } = Array.Empty<double>();

    /// <summary>Longitudinal (axial) chromatic aberration.</summary>
    public double[] CL { get; init; } = Array.Empty<double>();

    /// <summary>Transverse (lateral) chromatic aberration.</summary>
    public double[] CT { get; init; } = Array.Empty<double>();

    public double TotalS1 { get; init; }
    public double TotalS2 { get; init; }
    public double TotalS3 { get; init; }
    public double TotalS4 { get; init; }
    public double TotalS5 { get; init; }
    public double TotalCL { get; init; }
    public double TotalCT { get; init; }

    // ── The aspheric share of each sum ──────────────────────────────────────────────────
    //
    // The figuring's contribution ALONE, already included in S1..S5 above and repeated here
    // so that it can be separated again. Zero on an unfigured surface.
    //
    // Nodal aberration theory is what wants this. An aspheric surface has TWO aberration field
    // centres - one for the spherical base curve and one for the aspheric cap, which behaves as
    // a zero-power plate and so is centred by where the optical axis ray CROSSES it rather than
    // by an angle of incidence. Summing them needs the two contributions apart, which no other
    // consumer of this type has ever had a reason to ask for.

    /// <summary>Aspheric share of the spherical aberration sum, per surface.</summary>
    public double[] S1Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>Aspheric share of coma.</summary>
    public double[] S2Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>Aspheric share of astigmatism.</summary>
    public double[] S3Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Aspheric share of distortion. There is no aspheric share of PETZVAL: that term depends
    /// only on the surface's curvature and index step, and figuring changes neither.
    /// </summary>
    public double[] S5Aspheric { get; init; } = Array.Empty<double>();
}

/// <summary>
/// The Seidel (third-order) aberration coefficients, surface by surface.
///
/// These are sums over the paraxial marginal and chief rays, so everything needed comes
/// from <see cref="ParaxialTrace"/> and the refractive indices. The per-surface breakdown
/// is the point of computing them at all: a spot diagram says the design is soft, the
/// coefficients say which surface is making it soft.
///
/// Formulation follows the standard treatment (Welford, <i>Aberrations of Optical
/// Systems</i>, ch. 8). Per surface, with the marginal ray (y, u) and chief ray
/// (ybar, ubar) and the refraction invariants
///
///   A    = n (y c + u)          the marginal ray's n*i
///   Abar = n (ybar c + ubar)    the chief ray's n*ibar
///
/// the contributions are
///
///   S1 = -A^2 y d(u/n)          S2 = -A Abar y d(u/n)      S3 = -Abar^2 y d(u/n)
///   S4 = -H^2 c d(1/n)          S5 = (Abar/A)(S3 + S4)
///   CL = -A y d(dn/n)           CT = -Abar y d(dn/n)
///
/// where d(x) is the change in x across the surface and dn = n_short - n_long. Where A = 0
/// the same S5 is taken in the form with the A divided out, which is finite there:
///
///   S5 = -Abar^3 y d(1/n^2) + Abar ybar c (2 Abar y - A ybar) d(1/n)
/// </summary>
public static class SeidelCoefficients
{
    /// <summary>
    /// Computes the third-order sums.
    /// </summary>
    /// <param name="system">The lens.</param>
    /// <param name="n">Index after each surface at the primary wavelength.</param>
    /// <param name="nShort">Index after each surface at the short wavelength (F line).</param>
    /// <param name="nLong">Index after each surface at the long wavelength (C line).</param>
    /// <param name="p">The paraxial trace, which supplies both rays and the invariant.</param>
    public static SeidelResult Compute(OpticalSystem system, double[] n, double[] nShort, double[] nLong,
                                       ParaxialResult p)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();

        var s1 = new double[count]; var s2 = new double[count]; var s3 = new double[count];
        var a1 = new double[count]; var a2 = new double[count]; var a3 = new double[count];
        var a5 = new double[count];
        var s4 = new double[count]; var s5 = new double[count];
        var cl = new double[count]; var ct = new double[count];

        double H = p.LagrangeInvariant;

        for (int j = 1; j <= last; j++)
        {
            var surf = system.Surfaces[j];

            // The signed indices from the trace, so a mirror's reversal is already carried.
            double nBefore = p.N[j - 1];
            double nAfter  = p.N[j];
            if (Math.Abs(nBefore) < 1e-15 || Math.Abs(nAfter) < 1e-15) continue;

            double c = surf.VertexCurvature;

            // (y, u) at this surface: the height here, the slope in the medium BEFORE it.
            double y    = p.Y[j];
            double u    = p.U[j - 1];
            double ybar = p.Ybar[j];
            double ubar = p.Ubar[j - 1];

            double A    = nBefore * (y * c + u);
            double Abar = nBefore * (ybar * c + ubar);

            double dUoverN  = p.U[j] / nAfter - u / nBefore;
            double dOneOverN = 1.0 / nAfter - 1.0 / nBefore;

            s1[j] = -A * A * y * dUoverN;
            s2[j] = -A * Abar * y * dUoverN;
            s3[j] = -Abar * Abar * y * dUoverN;
            s4[j] = -H * H * c * dOneOverN;

            // Distortion as (Abar/A)(S3 + S4) carries a 1/A, and A is zero where the marginal
            // ray meets the surface at normal incidence - a flat face in collimated light, say.
            // The 1/A is only apparent. With u = A/n - yc on both sides, d(u/n) =
            // A d(1/n^2) - yc d(1/n), and with H = Abar y - A ybar, Abar^2 y^2 - H^2 =
            // A ybar (2 Abar y - A ybar); so
            //
            //   S5 = -Abar^3 y d(1/n^2) + Abar ybar c (2 Abar y - A ybar) d(1/n)
            //
            // with the A divided out exactly. It is finite everywhere. This branch used to set
            // S5 to zero instead, which on Ladder2_FlatPlain dropped a contribution of
            // +1.06E-3 and turned the total's sign; real rays, and the same face bent to
            // R = 1E10, both give the finite value.
            //
            // The quotient form is kept wherever it is defined, so every other design computes
            // exactly the bits it always did.
            if (Math.Abs(A) > 1e-12)
            {
                s5[j] = (Abar / A) * (s3[j] + s4[j]);
            }
            else
            {
                double dOneOverN2 = 1.0 / (nAfter * nAfter) - 1.0 / (nBefore * nBefore);
                s5[j] = -Abar * Abar * Abar * y * dOneOverN2
                      + Abar * ybar * c * (2.0 * Abar * y - A * ybar) * dOneOverN;
            }

            // Aspheric figuring adds to every term except Petzval, which depends only on
            // the surface's curvature and index step.
            //
            // The quantity wanted is the surface's departure from its VERTEX SPHERE at r^4,
            // because the vertex sphere is what the paraxial trace above has already
            // accounted for. Writing cb for the base curvature and c for the vertex one, the
            // surface's own r^4 coefficient is (1+k) cb^3 / 8 + A4 and the vertex sphere's is
            // c^3 / 8, so the departure is their difference.
            //
            // THE TWO CURVATURES ARE NOT THE SAME WHEN THERE IS AN r^2 TERM. An r^2
            // coefficient is a curvature change - c = cb + 2 A2 - so a surface carrying one
            // departs from its vertex sphere at r^4 even with no r^4 coefficient at all. This
            // line used to read `Conic * c^3 / 8 + A4`, which is the same thing whenever
            // A2 = 0 and wrong whenever it is not: the (cb^3 - c^3)/8 piece was missing.
            //
            // It was wrong for as long as the file existed and nothing caught it, because
            // every fixture here has A2 = 0. What caught it was writing the same surface two
            // ways - once as (cb, A2) and once as the equivalent (c, A4) shifted sphere - and
            // getting two different answers. OpticStudio's Seidel analysis gives one answer
            // for both, and so does this program's own Buchdahl route, which does the vertex
            // conversion explicitly. See AsphericR2TermTests.
            // GROUPED SO THAT THE A2 = 0 CASE IS BIT-IDENTICAL TO WHAT THIS ALWAYS COMPUTED.
            // Writing the departure as (1+k)cb^3/8 - c^3/8 is the same algebra and is NOT the
            // same arithmetic: 1 + k rounds, so every existing conic result would shift in its
            // last bits and every bit-identity test in this suite would have to be loosened to
            // accommodate a change that is supposed to affect nothing. Split out instead, the
            // conic keeps its own term untouched and the correction is a difference of two
            // cubes that is EXACTLY zero when cb and c are the same double - which they are
            // whenever A2 is zero, since VertexCurvature is then Curvature + 0.0.
            double cBase = surf.Curvature;
            double a4 = 0.0;
            if (Math.Abs(surf.Conic) > 1e-15) a4 += surf.Conic * cBase * cBase * cBase / 8.0;
            if (surf.AsphericCoefficients.Length > 1) a4 += surf.AsphericCoefficients[1];
            a4 += cBase * cBase * cBase / 8.0 - c * c * c / 8.0;
            if (Math.Abs(a4) > 1e-30)
            {
                double sAsph = 8.0 * (nAfter - nBefore) * a4 * y * y * y * y;
                double ratio = Math.Abs(y) > 1e-15 ? ybar / y : 0.0;

                // Kept separately as well as added in - see the note on SeidelResult.
                a1[j] = sAsph;
                a2[j] = sAsph * ratio;
                a3[j] = sAsph * ratio * ratio;
                a5[j] = sAsph * ratio * ratio * ratio;

                s1[j] += a1[j];
                s2[j] += a2[j];
                s3[j] += a3[j];
                s5[j] += a5[j];
            }

            // Chromatic terms use the dispersion of the medium after each surface.
            double dispBefore = DispersionAt(j - 1, nShort, nLong);
            double dispAfter  = DispersionAt(j, nShort, nLong);
            double dDispOverN = dispAfter / nAfter - dispBefore / nBefore;

            cl[j] = -A * y * dDispOverN;
            ct[j] = -Abar * y * dDispOverN;
        }

        return new SeidelResult
        {
            S1 = s1, S2 = s2, S3 = s3, S4 = s4, S5 = s5, CL = cl, CT = ct,
            TotalS1 = Sum(s1), TotalS2 = Sum(s2), TotalS3 = Sum(s3), TotalS4 = Sum(s4),
            TotalS5 = Sum(s5), TotalCL = Sum(cl), TotalCT = Sum(ct),
            S1Aspheric = a1, S2Aspheric = a2, S3Aspheric = a3, S5Aspheric = a5,
        };
    }

    private static double DispersionAt(int i, double[] nShort, double[] nLong)
    {
        if (nShort == null || nLong == null) return 0.0;
        if (i < 0 || i >= nShort.Length || i >= nLong.Length) return 0.0;
        return nShort[i] - nLong[i];
    }

    private static double Sum(double[] v)
    {
        double t = 0.0;
        foreach (double x in v) t += x;
        return t;
    }
}
