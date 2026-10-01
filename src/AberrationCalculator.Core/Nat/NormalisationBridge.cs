using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The scale between the two routes this program computes wave coefficients by.
///
/// <para>The third order comes from the Seidel sums; the fifth comes through Buchdahl's W
/// coordinates. They do not agree in magnitude, and not by a single constant either: measured
/// across four designs the ratio is</para>
/// <code>
///     W(Buchdahl) / W(Seidel) = A^l F^k
/// </code>
/// <para>one power of an aperture scale per power of <c>rho</c> and one of a field scale per
/// power of <c>H</c>. It is a normalisation, not an error - Buchdahl works in units of the focal
/// length with the p ray at unit height, the Seidel route in the design's own aperture and field.
/// Node positions are immune, being ratios of same-power quantities, which is why the reports can
/// print them side by side. MAGNITUDES are not.</para>
///
/// <para><b>Why it is needed.</b> A freeform overlay contributes a physical wavefront amplitude -
/// <c>4(n' - n)z</c> for trefoil - which is in the Seidel route's units. Adding it to a moment
/// built from Buchdahl-route coefficients needs this factor, and getting it wrong scales an
/// aberration by a plausible amount rather than an obvious one.</para>
///
/// <para><b>It checks itself.</b> Four third-order coefficients give four ratios, and there are
/// only two unknowns, so two of them are free checks. <c>W040</c> fixes <c>A</c> (it is the only
/// one with no field dependence), <c>W131</c> then fixes <c>F</c>, and <c>W222</c> and
/// <c>W311</c> must follow. If they do not, <see cref="IsUsable"/> is false and the caller must
/// decline rather than scale by a number it cannot justify. The <c>W311</c> check is the one that
/// catches a wrong SIGN for <c>A</c>, carrying an odd power of it where the others do not.</para>
/// </summary>
public readonly struct NormalisationBridge
{
    /// <summary>The aperture scale, one power per power of <c>rho</c>.</summary>
    public readonly Scalar A;

    /// <summary>The field scale, one power per power of <c>H</c>.</summary>
    public readonly Scalar F;

    /// <summary>The worst relative disagreement among the coefficients not used to fit.</summary>
    public readonly Scalar Residual;

    /// <summary>Whether the two free checks passed and the scale may be applied.</summary>
    public readonly bool IsUsable;

    private NormalisationBridge(Scalar a, Scalar f, Scalar residual, bool usable)
    {
        A = a; F = f; Residual = residual; IsUsable = usable;
    }

    /// <summary>The factor taking a Seidel-route coefficient to the Buchdahl route's units.</summary>
    /// <param name="fieldPower">k, the power of the field.</param>
    /// <param name="aperturePower">l, the power of the aperture.</param>
    public Scalar SeidelToW(int fieldPower, int aperturePower) =>
        SMath.Pow(A, aperturePower) * SMath.Pow(F, fieldPower);

    /// <summary>
    /// The factor taking a W-coordinate coefficient back into the design's own aperture and
    /// field - the units the third-order block and the Seidel sums are already in.
    /// </summary>
    public Scalar WToSeidel(int fieldPower, int aperturePower)
    {
        Scalar f = SeidelToW(fieldPower, aperturePower);
        return SMath.Abs(f) < 1e-300 ? 0.0 : 1.0 / f;
    }

    /// <summary>
    /// Fit the scale from the two routes' third-order coefficients. Pass them in the same order
    /// from both: W040, W131, W222, W311.
    /// </summary>
    /// <param name="tolerance">
    /// How closely the two free checks must agree. Loose by the standards of the rest of this
    /// file, because the fit divides small numbers by small numbers on a well corrected system.
    /// </param>
    public static NormalisationBridge Fit(Scalar[] seidel, Scalar[] buchdahl,
                                          Scalar tolerance = default)
    {
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        if (buchdahl == null) throw new ArgumentNullException(nameof(buchdahl));
        if (seidel.Length < 4 || buchdahl.Length < 4)
            throw new ArgumentException("four third-order coefficients are needed");

        Scalar tol = tolerance.Equals(default(Scalar)) ? 1e-3 : tolerance;
        var none = new NormalisationBridge(1.0, 1.0, Scalar.PositiveInfinity, false);

        // The fit divides, so a coefficient too small to carry information disqualifies it. The
        // scale is relative to the largest of the four: a system with no spherical aberration at
        // all cannot fix A, and saying so is better than dividing by its round-off.
        Scalar biggest = 0.0;
        for (int i = 0; i < 4; i++) biggest = SMath.Max(biggest, SMath.Abs(seidel[i]));
        if (biggest < 1e-300) return none;

        var ratio = new Scalar[4];
        for (int i = 0; i < 4; i++)
        {
            if (SMath.Abs(seidel[i]) < 1e-6 * biggest) return none;
            ratio[i] = buchdahl[i] / seidel[i];
        }

        if (ratio[0] <= 0.0) return none;                 // A^4 cannot be negative
        Scalar a = SMath.Pow(ratio[0], 0.25);             // W040: k = 0, l = 4
        Scalar a3 = a * a * a;
        if (SMath.Abs(a3) < 1e-300) return none;
        Scalar f = ratio[1] / a3;                         // W131: k = 1, l = 3

        // W222 (k=2, l=2) and W311 (k=3, l=1) are then not free.
        Scalar want222 = a * a * f * f;
        Scalar want311 = a * f * f * f;
        Scalar r222 = SMath.Abs(want222 - ratio[2]) / SMath.Max(SMath.Abs(ratio[2]), 1e-300);
        Scalar r311 = SMath.Abs(want311 - ratio[3]) / SMath.Max(SMath.Abs(ratio[3]), 1e-300);
        Scalar worst = SMath.Max(r222, r311);

        return new NormalisationBridge(a, f, worst, worst <= tol);
    }
}
