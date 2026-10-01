using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Forbes;

/// <summary>
/// Where each surface's share of the tertiary comes from, by Forbes' route.
///
/// <para><b>The decomposition, and why it adds up.</b> Write <c>A(i)</c> for the aberration of the
/// system when surfaces 1 to <c>i</c> act in full and everything after them is linearised, so
/// <c>A(0)</c> is the all-paraxial system and <c>A(N)</c> the real one. Then
///
/// <code>
///     surface i contributes  A(i) - A(i-1)
/// </code>
///
/// which telescopes to the total exactly, by construction rather than by arrangement. Write
/// <c>B(i)</c> for the aberration when surface <c>i</c> alone acts and everything else is
/// linearised - the surface working on a perfect beam, its result carried to the image without
/// further aberration. Then</para>
///
/// <code>
///     intrinsic(i) = B(i) with the figuring taken off surface i
///     aspheric(i)  = B(i) - intrinsic(i)
///     induced(i)   = A(i) - A(i-1) - B(i)
/// </code>
///
/// <para>with <c>A(0)</c> subtracted from <c>B(i)</c> so that both are measured from the same
/// place. The three then sum to the surface contribution identically, and the first surface -
/// having nothing before it, so that <c>A(1) = B(1)</c> - has an induced part of exactly zero,
/// which is the convention <see cref="BuchdahlResult.Induced"/> states, arrived at here rather
/// than imposed.</para>
///
/// <para><c>A(0)</c> itself is NOT zero, and is reported separately as
/// <see cref="Breakdown.Reference"/> rather than charged to a surface. The surfaces and the
/// reference together reproduce the totals exactly.</para>
///
/// <para><b>What makes this natural in Forbes' formulation.</b> "The aberration already present
/// when light reaches this surface" is not a quantity that has to be derived: it is the
/// non-constant part of the trace state. Suppressing it is truncation, and suppressing a
/// surface's own contribution is linearising its step. Neither needs a scheme.</para>
/// </summary>
public static class ForbesPerSurface
{
    /// <summary>One surface's share, split three ways. All in the same units as the system
    /// totals, and <c>Intrinsic + Aspheric + Induced = Total</c> term by term.</summary>
    public sealed class Contribution
    {
        public int Surface { get; init; }
        public double[] Intrinsic { get; init; } = new double[21];
        public double[] Aspheric { get; init; } = new double[21];
        public double[] Induced { get; init; } = new double[21];
        public double[] Total { get; init; } = new double[21];
    }

    /// <summary>
    /// A whole breakdown: the surfaces, and the reference term that belongs to none of them.
    /// The surfaces plus the reference reproduce the system totals exactly.
    /// </summary>
    public sealed class Breakdown
    {
        /// <summary>
        /// <c>A(0)</c> - what the coefficients come to when EVERY step is linearised, which is
        /// not zero and should not be expected to be.
        ///
        /// <para>It is one coefficient: tau20, the pure-field term, seventh-order distortion. A
        /// paraxially perfect system launched with direction cosines sends a ray at angle theta
        /// to about <c>efl sin theta</c>, while the coefficients are referred to
        /// <c>efl tan theta</c>. The difference is odd and carries no aperture, so it lands
        /// wholly in tau20. On CookeTriplet it is -1.32E-2 against a system total of 1.08E-3, so
        /// it is not small and hiding it in the first surface would misreport that surface
        /// badly. Both routes share the convention - it is why their totals agree - so this is a
        /// property of the reference and not of the optics.</para>
        /// </summary>
        public double[] Reference { get; init; } = new double[21];

        public IReadOnlyList<Contribution> Surfaces { get; init; } = Array.Empty<Contribution>();
    }

    /// <summary>
    /// The per-surface breakdown, or null if the model cannot be formed - a system with no field,
    /// as elsewhere, or one with a mirror, which the series trace declines.
    /// </summary>
    public static Breakdown? Compute(
        OpticalSystem system, double[] indices, ParaxialResult paraxial, double maxFieldDeg,
        int degree = 3)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        int last = system.LastOpticalSurface();

        double[]? Tau(IReadOnlyList<bool> aberrating, int flatten)
        {
            var r = ForbesCoefficients.Invert(system, indices, paraxial, maxFieldDeg, degree,
                                              aberrating, flatten);
            return r?.Tau;
        }

        // A(i): surfaces 1..i in full, the rest linearised.
        var a = new double[last + 1][];
        for (int i = 0; i <= last; i++)
        {
            var flags = new bool[last + 2];
            for (int j = 1; j <= i; j++) flags[j] = true;
            var tau = Tau(flags, 0);
            if (tau == null) return null;
            a[i] = tau;
        }

        var result = new List<Contribution>();
        for (int i = 1; i <= last; i++)
        {
            // B(i): surface i alone, and the same with its figuring taken off. Both are measured
            // from the same reference the differences above are, so the three parts add up and
            // the first surface has an induced part of exactly nothing.
            var only = new bool[last + 2];
            only[i] = true;
            var b = Tau(only, 0);
            var bSphere = Tau(only, i);
            if (b == null || bSphere == null) return null;

            var c = new Contribution { Surface = i };
            for (int k = 1; k <= 20; k++)
            {
                double total = a[i][k] - a[i - 1][k];
                double alone = b[k] - a[0][k];
                double aloneSpherical = bSphere[k] - a[0][k];
                c.Intrinsic[k] = aloneSpherical;
                c.Aspheric[k] = alone - aloneSpherical;
                c.Induced[k] = total - alone;
                c.Total[k] = total;
            }
            result.Add(c);
        }
        return new Breakdown { Reference = a[0], Surfaces = result };
    }
}
