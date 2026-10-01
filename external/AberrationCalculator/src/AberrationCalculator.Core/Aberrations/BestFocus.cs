using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>Best focus at one field of one wavelength.</summary>
public sealed class BestFocusField
{
    /// <summary>Index into the system's field list.</summary>
    public int Field;

    /// <summary>Fractional field height, the <c>h</c> the coefficients are normalised to.</summary>
    public Scalar H;

    /// <summary>The field's own weight, as the file states it.</summary>
    public Scalar Weight;

    /// <summary>
    /// Shift from the PARAXIAL image plane to the plane of minimum RMS spot radius, in lens
    /// units, positive away from the last surface.
    /// </summary>
    public Scalar DeltaZ;

    /// <summary>RMS spot radius at the paraxial plane - what <c>Prms</c> reports today.</summary>
    public Scalar RmsParaxial;

    /// <summary>RMS spot radius at <see cref="DeltaZ"/>. Never larger than the above.</summary>
    public Scalar RmsBestFocus;
}

/// <summary>Best focus for one wavelength, across the fields.</summary>
public sealed class BestFocusWavelength
{
    /// <summary>Index into the system's wavelength list.</summary>
    public int Wavelength;

    /// <summary>The wavelength itself, in the file's units.</summary>
    public Scalar Value;

    /// <summary>Its weight, as the file states it.</summary>
    public Scalar Weight;

    /// <summary>True for the file's primary wavelength.</summary>
    public bool IsPrimary;

    /// <summary>
    /// Back focal length AT THIS WAVELENGTH: last surface to the paraxial focus of a collimated
    /// beam traced in this colour's indices. The spread of these across the set is the
    /// longitudinal chromatic aberration, measured paraxially.
    /// </summary>
    public Scalar Bfl;

    /// <summary>
    /// Last surface to the paraxial focus of the marginal ray at this wavelength. Equals
    /// <see cref="Bfl"/> at an infinite conjugate and is the quantity <see cref="BestFocusField.DeltaZ"/>
    /// is measured from at either.
    /// </summary>
    public Scalar ParaxialFocusDistance;

    /// <summary>Paraxial marginal slope in image space, the <c>u</c> of <c>dZ u rho</c>.</summary>
    public Scalar MarginalSlope;

    /// <summary>Per field.</summary>
    public List<BestFocusField> Fields = new();

    /// <summary>
    /// The single plane that best focuses the WHOLE field at this wavelength, weighted by the
    /// field weights. Sands's Sec. IV, in his notation the quantity he calls the plane of best
    /// focus for all field positions.
    /// </summary>
    public Scalar DeltaZWholeField;
}

/// <summary>
/// Where the image actually is sharpest, predicted from the aberration coefficients with no rays
/// traced - and therefore the one thing Robb's analytic spot cannot tell you.
///
/// <para><b>The gap this closes.</b> <see cref="Prms"/> is referred to the paraxial image plane
/// and has no defocus term, which its author says plainly: the image plane "ceases to become a
/// design variable", and focus must be adjusted afterwards "by the method of Sands (1973)". So a
/// design whose spot is dominated by defocus reads better than it deserves, and a PRMS number
/// cannot be used to choose an image plane. This class is that method, taken in the program's
/// own convention rather than transcribed from Sands's.</para>
///
/// <para><b>Why it is not a transcription.</b> Sands tabulates his focal shift as combinations of
/// his own sigma, mu, tau and eta coefficients, in his own normalisation. Converting that to
/// Rimmer's is exactly the class of step this repository has twice lost days to. It is also
/// unnecessary: the criterion is the minimum of the mean square radius, the mean square radius is
/// a quadratic in the plane shift, and both of its non-constant terms are pupil averages of
/// Robb's own polynomial - see <see cref="Prms.DefocusCoupling"/>. The result is derived here and
/// checked against Sands's published third-order identity rather than copied from it.</para>
///
/// <para><b>What it inherits.</b> The series truncation of the coefficients, and Sands's paraxial
/// approximation for the image-space ray direction, which he tests and finds justified except in
/// systems with image-space ray angles near 45 degrees or very large distortion. It is
/// monochromatic, one wavelength at a time, which is why <see cref="ForSystem"/> returns a row
/// per wavelength rather than a single answer.</para>
/// </summary>
public static class BestFocus
{
    /// <summary>
    /// The shift from the paraxial plane to minimum RMS spot, at one field.
    ///
    /// <para>The mean square radius at a plane <c>dZ</c> away is
    /// <c>M + 2 (dZ u) L + (dZ u)^2 / 2</c>, a quadratic whose minimum is at
    /// <c>dZ u = -2L</c>. Nothing is searched and nothing is traced.</para>
    /// </summary>
    /// <param name="u">
    /// Paraxial marginal slope in image space. Zero for an afocal system, where no such plane
    /// exists and the result is zero rather than infinite.
    /// </param>
    public static Scalar DeltaZ(BuchdahlTerms totals, Scalar h, Scalar u)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));
        if (SMath.Abs(u) < 1e-15) return 0.0;
        return -2.0 * Prms.DefocusCoupling(totals, h) / u;
    }

    /// <summary>
    /// The single plane that best focuses a set of weighted fields at once.
    ///
    /// <para>Minimising the weighted SUM of the mean squares, not the sum of the separate minima:
    /// the quadratic in <c>dZ</c> adds term by term, so the answer is the weighted mean of the
    /// couplings and one division. A weight of zero on axis reproduces Sands's second example,
    /// where he weights the axial image out of the average deliberately.</para>
    /// </summary>
    public static Scalar DeltaZWholeField(BuchdahlTerms totals, IReadOnlyList<Scalar> h,
                                          IReadOnlyList<Scalar> weights, Scalar u)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));
        if (h == null) throw new ArgumentNullException(nameof(h));
        if (weights == null) throw new ArgumentNullException(nameof(weights));
        if (h.Count != weights.Count)
            throw new ArgumentException("one weight per field", nameof(weights));
        if (SMath.Abs(u) < 1e-15) return 0.0;

        Scalar num = 0.0, den = 0.0;
        for (int i = 0; i < h.Count; i++)
        {
            num += weights[i] * Prms.DefocusCoupling(totals, h[i]);
            den += weights[i];
        }
        return SMath.Abs(den) < 1e-15 ? 0.0 : -2.0 * (num / den) / u;
    }

    /// <summary>
    /// RMS spot radius at an arbitrary plane. Guarded against the rounding that can take a mean
    /// square a few parts in 1E16 below zero when the spot at best focus is essentially perfect.
    /// </summary>
    public static Scalar RmsAt(BuchdahlTerms totals, Scalar h, Scalar deltaZ, Scalar u)
    {
        Scalar ms = Prms.MeanSquareDefocused(totals, h, deltaZ, u);
        return ms > 0.0 ? SMath.Sqrt(ms) : 0.0;
    }

    /// <summary>
    /// The whole procedure, wavelength by wavelength: the paraxial focus for that colour, the
    /// best-focus shift at each field, and the one plane that serves the whole field.
    ///
    /// <para><b>Each wavelength gets its own everything.</b> Its own indices, its own paraxial
    /// trace, its own back focal length, its own marginal slope, and its own coefficient set -
    /// the caller supplies the coefficients per wavelength for exactly that reason, since taking
    /// the tertiary terms from one colour and the rest from another is a fault this report has
    /// had before. The <c>dZ</c> of each row is measured from ITS OWN paraxial plane, so the
    /// rows are not on a common origin: to compare colours, add
    /// <see cref="BestFocusWavelength.ParaxialFocusDistance"/> to each.</para>
    /// </summary>
    /// <param name="perWavelength">
    /// For each wavelength: its index, value, weight, whether it is primary, the paraxial trace
    /// made in its own indices, and the coefficient totals computed from that trace.
    /// </param>
    /// <param name="fields">Fractional field heights and their weights, shared across colours.</param>
    /// <param name="lastSurface">Index of the last optical surface, whose U is image space.</param>
    public static List<BestFocusWavelength> ForSystem(
        IReadOnlyList<(int Index, Scalar Value, Scalar Weight, bool IsPrimary,
                       RayTrace.ParaxialResult Paraxial, BuchdahlTerms Totals)> perWavelength,
        IReadOnlyList<(Scalar H, Scalar Weight)> fields,
        int lastSurface)
    {
        if (perWavelength == null) throw new ArgumentNullException(nameof(perWavelength));
        if (fields == null) throw new ArgumentNullException(nameof(fields));

        var hs = new List<Scalar>();
        var ws = new List<Scalar>();
        foreach (var f in fields) { hs.Add(f.H); ws.Add(f.Weight); }

        var result = new List<BestFocusWavelength>();
        foreach (var w in perWavelength)
        {
            Scalar u = lastSurface >= 0 && lastSurface < w.Paraxial.U.Length
                     ? w.Paraxial.U[lastSurface] : 0.0;

            var row = new BestFocusWavelength
            {
                Wavelength = w.Index,
                Value = w.Value,
                Weight = w.Weight,
                IsPrimary = w.IsPrimary,
                Bfl = w.Paraxial.Bfl,
                ParaxialFocusDistance = w.Paraxial.ParaxialFocusDistance,
                MarginalSlope = u,
                DeltaZWholeField = DeltaZWholeField(w.Totals, hs, ws, u),
            };

            for (int i = 0; i < fields.Count; i++)
            {
                Scalar dz = DeltaZ(w.Totals, fields[i].H, u);
                row.Fields.Add(new BestFocusField
                {
                    Field = i,
                    H = fields[i].H,
                    Weight = fields[i].Weight,
                    DeltaZ = dz,
                    RmsParaxial = RmsAt(w.Totals, fields[i].H, 0.0, u),
                    RmsBestFocus = RmsAt(w.Totals, fields[i].H, dz, u),
                });
            }

            result.Add(row);
        }
        return result;
    }
}
