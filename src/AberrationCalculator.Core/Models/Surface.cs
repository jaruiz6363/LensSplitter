using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Enums;

namespace AberrationCalculator.Core.Models;

/// <summary>
/// One surface of the prescription.
///
/// Curvature rather than radius is the stored quantity, because a plane is c = 0 and needs
/// no special case, whereas r = infinity does. <see cref="Radius"/> is a view onto it for
/// reading and printing, where radius is what an optical prescription conventionally shows.
/// </summary>
public class Surface
{
    /// <summary>Position in the system, 0 = object.</summary>
    public int Index { get; set; }

    public SurfaceType Type { get; set; } = SurfaceType.Standard;

    /// <summary>1/radius, in reciprocal lens units. Zero is a plane.</summary>
    public Scalar Curvature { get; set; }

    /// <summary>
    /// Radius of curvature. Infinite for a plane, in both directions: reading infinity back
    /// gives c = 0, so a plane round-trips instead of producing a division by zero.
    /// </summary>
    public Scalar Radius
    {
        get => SMath.Abs(Curvature) < 1e-15 ? Scalar.PositiveInfinity : 1.0 / Curvature;
        set => Curvature = Scalar.IsInfinity(value) || value == 0.0 ? 0.0 : 1.0 / value;
    }

    /// <summary>Axial distance to the next surface.</summary>
    public Scalar Thickness { get; set; }

    /// <summary>Conic constant. 0 = sphere, −1 = paraboloid, &lt; −1 = hyperboloid.</summary>
    public Scalar Conic { get; set; }

    /// <summary>
    /// Even-asphere coefficients. Index k multiplies r^(2k+2), so [0] is the r² term, [1] is
    /// r⁴, and so on. The r² term is separate from curvature and some formats do not write it.
    /// </summary>
    public Scalar[] AsphericCoefficients { get; set; } = new Scalar[8];

    /// <summary>Catalog glass name, or null/empty for air. "MIRROR" reflects.</summary>
    public string? Material { get; set; }

    /// <summary>Catalog the material was resolved from, when a file names one.</summary>
    public string? CatalogName { get; set; }

    /// <summary>True when this surface is the aperture stop.</summary>
    public bool IsStop { get; set; }

    // ── Perturbation ─────────────────────────────────────────────────────────────────
    // How this surface sits relative to the mechanical axis. Zero for every surface of an
    // aligned design, which is what a lens file normally describes, so nothing downstream
    // changes unless something sets these. Nodal aberration theory is the only consumer:
    // the paraxial trace is a rotationally symmetric construction and does not read them.

    /// <summary>Decentre along x, in lens units.</summary>
    public Scalar DecenterX { get; set; }

    /// <summary>Decentre along y, in lens units.</summary>
    public Scalar DecenterY { get; set; }

    /// <summary>
    /// Tilt about the x axis, in RADIANS. A tilt about x swings the surface in the y-z
    /// meridian, so it pairs with <see cref="DecenterY"/> - see the note on
    /// <see cref="IsPerturbed"/>.
    /// </summary>
    public Scalar TiltX { get; set; }

    /// <summary>Tilt about the y axis, in RADIANS.</summary>
    public Scalar TiltY { get; set; }

    /// <summary>
    /// Fringe Zernike departure of this surface from its nominal shape, indexed BY FRINGE TERM
    /// NUMBER - so <c>[5]</c> and <c>[6]</c> are astigmatism, <c>[7]</c> and <c>[8]</c> coma.
    /// Empty when the surface is the shape the prescription says it is.
    ///
    /// <para>This is a SURFACE SAG departure, in lens units, not a wavefront. An interferogram
    /// measures the wavefront, which is larger by the index step the light crosses - a factor of
    /// <c>(n' - n)</c>, or 2 for a mirror at normal incidence - and the conversion is the
    /// reader's to make, because only they know how the interferogram was taken.</para>
    ///
    /// <para>Indexed by term number rather than named, because nodal aberration theory extends
    /// upwards through the same list: Z10/11 trefoil, Z12/13 oblique spherical, Z14/15 secondary
    /// coma, Z17/18 tetrafoil. Only 5 to 8 are acted on today; the rest are carried and ignored
    /// rather than refused, so a file that states them survives a round trip.</para>
    /// </summary>
    public Scalar[] FringeZernike { get; set; } = Array.Empty<Scalar>();

    /// <summary>The Fringe Zernike coefficient of that term, or zero if none is stated.</summary>
    public Scalar Zernike(int term) =>
        FringeZernike != null && term >= 0 && term < FringeZernike.Length ? FringeZernike[term] : 0.0;

    /// <summary>True when any Zernike departure has been stated for this surface.</summary>
    public bool HasZernike
    {
        get
        {
            if (FringeZernike == null) return false;
            foreach (var z in FringeZernike) if (z != 0.0) return true;
            return false;
        }
    }

    /// <summary>
    /// Whether this surface departs from the mechanical axis at all.
    ///
    /// <para>For a SPHERICAL surface a decentre is a tilt about the centre of curvature, so the
    /// two enter nodal aberration theory only through the combination <c>T + c D</c> - the
    /// "equivalent tilt". They are kept apart here anyway, because a conic or a figured surface
    /// breaks that equivalence and because a tolerance budget quotes them separately.</para>
    /// </summary>
    public bool IsPerturbed =>
        DecenterX != 0.0 || DecenterY != 0.0 || TiltX != 0.0 || TiltY != 0.0;

    public bool IsMirror => !string.IsNullOrEmpty(Material)
                            && Material!.Equals("MIRROR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Clear semi-diameter.</summary>
    public Scalar SemiDiameter { get; set; }

    public SemiDiameterMode SemiDiameterMode { get; set; } = SemiDiameterMode.Auto;

    /// <summary>Clear aperture as a percentage of the solved semi-diameter; 100 = full.</summary>
    public Scalar ClearAperturePercent { get; set; } = 100.0;

    /// <summary>Central obstruction radius, 0 for none.</summary>
    public Scalar ObscurationRadius { get; set; }

    /// <summary>Free-text note carried through from the file.</summary>
    public string? Comment { get; set; }

    // ── Model ("fictitious") glass ────────────────────────────────────────────────
    // Some files give dispersion directly instead of naming a catalog glass.

    public bool ModelIndexEnabled { get; set; }
    public Scalar ModelNd { get; set; }
    public Scalar ModelVd { get; set; }
    public Scalar ModelDPgF { get; set; }

    /// <summary>Focal length of an ideal thin lens, for <see cref="SurfaceType.Paraxial"/>.</summary>
    public Scalar FocalLength { get; set; }

    // ── Format-specific extras ───────────────────────────────────────────────────
    // Readers set these; the analysis does not use them, but dropping them would lose
    // information when a file is opened and its prescription printed.

    public Scalar FloatingApertureRadius { get; set; }
    public Scalar ClapOuterRadius { get; set; }
    public Scalar InnerRadius { get; set; }

    /// <summary>
    /// Numbered surface parameters as a format wrote them (coordinate-break tilts, ABCD
    /// terms, and so on). Kept so an opened file prints back what it said, even for a
    /// surface type this program does not analyse.
    /// </summary>
    public Scalar[] Parameters { get; } = new Scalar[8];

    /// <summary>Integer surface settings, same purpose as <see cref="Parameters"/>.</summary>
    public int[] Settings { get; } = new int[8];

    /// <summary>
    /// The thickness after this surface is solved to put the paraxial marginal ray on
    /// axis. Recorded because the stored thickness alone does not say it was solved.
    /// </summary>
    public bool HasMarginalRaySolve { get; set; }

    // ── What the optimiser may change here, and how far ──────────────────────────
    //
    // These live on the surface because that is where a designer thinks of them and where
    // the .lhlt format keeps them, so a design opened from one arrives with its
    // variables already declared and goes back with them intact.
    //
    // Plain `double`, deliberately: a bound is a limit on a variable, never a quantity
    // anything is differentiated with respect to, so it does not want to be a dual number
    // in the differentiating build. Wavelength and Field are `double` for the same reason.

    /// <summary>Whether the optimiser may change this surface's curvature.</summary>
    public bool CurvatureVariable { get; set; }

    /// <summary>Whether the optimiser may change the thickness after this surface.</summary>
    public bool ThicknessVariable { get; set; }

    /// <summary>Whether the optimiser may change this surface's conic constant.</summary>
    public bool ConicVariable { get; set; }

    /// <summary>
    /// Whether the optimiser may change each even-asphere coefficient, indexed as
    /// <see cref="AsphericCoefficients"/> is: [1] is the r^4 term, [2] r^6, [3] r^8.
    ///
    /// <para>Slot [0] is the r^2 term, which is folded into the vertex curvature before any
    /// coefficient is computed and so would duplicate the curvature variable; and [4] upward
    /// are r^10 and beyond, which do not appear in the third, fifth or seventh order at all.
    /// Neither is offered. The array is the full length so that it lines up with the
    /// coefficients it describes and with the .lhlt field of the same name, rather than being
    /// three bools a reader has to map by hand.</para>
    /// </summary>
    public bool[] AsphericVariable { get; set; } = new bool[8];

    /// <summary>Lower limit on the curvature, or negative infinity for none.</summary>
    public double CurvatureMin { get; set; } = double.NegativeInfinity;

    /// <summary>Upper limit on the curvature, or positive infinity for none.</summary>
    public double CurvatureMax { get; set; } = double.PositiveInfinity;

    /// <summary>Lower limit on the thickness, or negative infinity for none.</summary>
    public double ThicknessMin { get; set; } = double.NegativeInfinity;

    /// <summary>Upper limit on the thickness, or positive infinity for none.</summary>
    public double ThicknessMax { get; set; } = double.PositiveInfinity;

    public void SetParameter(int index, Scalar value)
    {
        if (index >= 0 && index < Parameters.Length) Parameters[index] = value;
    }

    public void SetSetting(int index, int value)
    {
        if (index >= 0 && index < Settings.Length) Settings[index] = value;
    }

    /// <summary>
    /// The curvature the surface actually has at its vertex, which is what sets its paraxial
    /// power.
    ///
    /// <para>The even-asphere polynomial starts at r-squared, not r-to-the-fourth, and that
    /// first term is NOT figuring: expanding the sag gives z = (c/2 + A2) r^2 + ..., so a
    /// nonzero A2 shifts the vertex curvature to c + 2 A2 and changes the surface power. Every
    /// paraxial and aberration path must use this rather than <see cref="Curvature"/>, or it
    /// silently analyses a different surface from the one <see cref="Sag"/> describes.</para>
    ///
    /// <para>Identical to <see cref="Curvature"/> whenever the r-squared coefficient is zero,
    /// which it is in every design shipped with this program.</para>
    /// </summary>
    public Scalar VertexCurvature =>
        Curvature + 2.0 * (AsphericCoefficients.Length > 0 ? AsphericCoefficients[0] : 0.0);

    /// <summary>
    /// Whether the surface is figured: a conic, or any aspheric term.
    ///
    /// <para>This is the test that decides which route may be trusted for the seventh order.
    /// Buchdahl's scheme and Forbes' series trace agree to roundoff on all twenty tertiary
    /// coefficients wherever every surface is unfigured; where one is not, the scheme needs
    /// an aspheric arrangement Buchdahl never published and which is reconstructed here, and
    /// real rays say the reconstruction is wrong. See <c>docs/distortion-prediction.md</c>.</para>
    /// </summary>
    public bool IsFigured
    {
        get
        {
            // Vanishes, not a magnitude test, and the difference is the whole of whether a
            // figuring variable works. In the differentiating build a conic of exactly 0 with a
            // derivative of 1 - a spherical surface whose conic the optimiser has just been
            // handed as a variable - is NOT absent: it is the quantity being moved. A magnitude
            // test reads it from the value alone, calls the surface spherical, and every
            // aberration coefficient comes back with a right value and a zero derivative, so
            // the optimiser concludes that figuring the surface cannot help because it cannot
            // see that it would. In plain double this is the same comparison it always was.
            if (!SMath.Vanishes(Conic, 1e-12)) return true;
            foreach (Scalar a in AsphericCoefficients) if (!SMath.Vanishes(a, 1e-30)) return true;
            return false;
        }
    }

    /// <summary>
    /// The surface rewritten as a sphere at its own vertex curvature plus polynomial figuring,
    /// which is the form every aberration treatment here wants.
    ///
    /// <para>An r-squared coefficient is not a deformation - it is a curvature change wearing
    /// a polynomial coat - so it cannot be handled as figuring and does not need to be.
    /// Matching the sag series term by term to r^8,</para>
    ///
    /// <code>
    ///   th1 = c/2 + A2                      th2 = (1+k) c^3/8        + A4
    ///   th3 = (1+k)^2 c^5/16 + A6           th4 = 5 (1+k)^3 c^7/128  + A8
    /// </code>
    ///
    /// <para>the equivalent surface is the sphere of curvature 2*th1 = c + 2 A2, conic zero,
    /// carrying whatever is left once that sphere is own conic series is subtracted. The two
    /// descriptions are the same surface to r^8, which is every order this program computes.
    /// </para>
    ///
    /// <para>With no r-squared term the surface is returned untouched, so the ordinary path is
    /// bit-for-bit what it was.</para>
    /// </summary>
    public (Scalar Curvature, Scalar Conic, Scalar A4, Scalar A6, Scalar A8) VertexForm()
    {
        var a = AsphericCoefficients;
        Scalar a2 = a.Length > 0 ? a[0] : 0.0;
        Scalar a4 = a.Length > 1 ? a[1] : 0.0;
        Scalar a6 = a.Length > 2 ? a[2] : 0.0;
        Scalar a8 = a.Length > 3 ? a[3] : 0.0;
        if (a2 == 0.0) return (Curvature, Conic, a4, a6, a8);

        Scalar c = Curvature, c2 = c * c, c3 = c2 * c, c5 = c3 * c2, c7 = c5 * c2;
        Scalar k1 = 1.0 + Conic;
        Scalar th2 = k1 * c3 / 8.0 + a4;
        Scalar th3 = k1 * k1 * c5 / 16.0 + a6;
        Scalar th4 = 5.0 * k1 * k1 * k1 * c7 / 128.0 + a8;

        Scalar v = c + 2.0 * a2;
        Scalar v2 = v * v, v3 = v2 * v, v5 = v3 * v2, v7 = v5 * v2;
        return (v, 0.0, th2 - v3 / 8.0, th3 - v5 / 16.0, th4 - 5.0 * v7 / 128.0);
    }

    /// <summary>Sag z(r) along the axis, positive toward the image.</summary>
    public Scalar Sag(Scalar r)
    {
        Scalar r2 = r * r;
        Scalar sag = 0.0;

        if (!SMath.Vanishes(Curvature, 1e-15))
        {
            // Standard conic sag. The radicand goes negative outside the surface, which is a
            // real question about the geometry rather than a rounding artefact, so it is
            // reported as NaN instead of being clamped to something plausible.
            Scalar disc = 1.0 - (1.0 + Conic) * Curvature * Curvature * r2;
            if (disc < 0.0) return Scalar.NaN;
            sag = Curvature * r2 / (1.0 + SMath.Sqrt(disc));
        }

        Scalar rp = r2;                                   // r², then r⁴, r⁶ …
        for (int k = 0; k < AsphericCoefficients.Length; k++)
        {
            sag += AsphericCoefficients[k] * rp;
            rp *= r2;
        }
        return sag;
    }
}
