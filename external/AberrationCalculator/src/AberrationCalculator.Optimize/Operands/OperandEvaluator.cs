extern alias Ad;

using System;
using System.Collections.Generic;

using AberrationCalculator.Optimize.Evaluation;

using AdA = Ad::AberrationCalculator.Core.Aberrations;
using AdN = Ad::AberrationCalculator.Core.Nat;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;
using DMath = Ad::AberrationCalculator.Core.Ad.DualMath;

namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// What each operand measures, evaluated on one dual-number pass of the aberration chain.
///
/// <para>Everything returned is a <see cref="Dual"/> - the quantity and its exact derivative
/// with respect to the variable the pass was seeded on. Nothing here differences anything, and
/// no step size appears in this file.</para>
/// </summary>
public static class OperandEvaluator
{
    /// <summary>
    /// The operand's raw value, in its own units, before any target or weight is applied.
    /// <paramref name="op"/> must already have had its surface sentinels resolved by
    /// <see cref="Operand.Expand"/>.
    /// </summary>
    public static Dual Evaluate(Operand op, DesignProbe probe, OperandContext ctx)
    {
        if (op == null) throw new ArgumentNullException(nameof(op));
        if (probe == null) throw new ArgumentNullException(nameof(probe));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return op.Type switch
        {
            OperandType.PRMSA => Prmsa(op, probe, ctx),
            OperandType.TTL => TotalTrack(probe),
            // THE POWER, not the focal length - see Operand.IsPower. The operand is still
            // declared and reported as a focal length; only the quantity the solver
            // differentiates changes, because a focal length is infinite on a flat design and
            // its derivative there is zero.
            OperandType.EFL => probe.Paraxial(ctx.WaveIndex(op.Wave)).Power,

            OperandType.PX or OperandType.PY or OperandType.PZ or
            OperandType.PL or OperandType.PM or OperandType.PN => Paraxial(op, probe, ctx),

            OperandType.RX or OperandType.RY or OperandType.RZ or
            OperandType.RL or OperandType.RM or OperandType.RN => Real(op, probe, ctx),

            OperandType.EGT or OperandType.EAT => EdgeThickness(op, probe),
            OperandType.DTRGT => DiameterToThickness(op, probe),

            OperandType.LCF => LateralColour(op, probe, ctx),
            OperandType.AXC => AxialColour(probe, ctx),
            OperandType.DISTF => Distortion(op, probe, ctx),

            OperandType.ABER => Coefficient(op, probe, ctx),

            OperandType.ASBLT => AsBuilt(op, probe, ctx),

            _ => throw new NotSupportedException("unknown operand type " + op.Type),
        };
    }

    /// <summary>
    /// The predicted RMS spot over the whole evaluation set.
    ///
    /// <para>Assembled exactly as the report's PRMSA section assembles it: one coefficient set
    /// per wavelength taken at the maximum field, each field entering as its fractional height
    /// against that maximum, the weighted mean taken over the mean-SQUARE radii and the root
    /// taken once at the end. Averaging the RMS values instead would be a different quantity,
    /// and one the report does not print.</para>
    /// </summary>
    private static Dual Prmsa(Operand op, DesignProbe probe, OperandContext ctx)
    {
        // A sub-operand: ONE field at ONE wavelength, which is what Expand hands us. The
        // aggregate is not formed here at all - it is what the sum of squares of these comes to,
        // which is the point of splitting them. Wave is 1-based on an expanded operand, and zero
        // only on the unexpanded form below.
        if (op.Wave > 0)
            return AdA.Prms.Value(probe.Coefficients(ctx.WaveIndex(op.Wave)), op.Hy);

        // The unexpanded form, kept for a design with no fields or wavelengths to split over,
        // and so that asking for PRMSA outside a merit function still means what it always did.
        var cases = new List<(AdA.BuchdahlTerms Totals, Dual H, Dual Weight)>();
        double max = ctx.MaxField;

        for (int w = 0; w < ctx.WaveCount; w++)
        {
            var totals = probe.Coefficients(w);
            double ww = ctx.WaveWeights[w];
            for (int f = 1; f <= ctx.FieldCount; f++)
            {
                double h = Math.Abs(max) > 1e-15 ? ctx.Fields[f] / max : 0.0;
                cases.Add((totals, h, ww * ctx.FieldWeights[f]));
            }
        }
        return AdA.Prms.Composite(cases);
    }

    /// <summary>
    /// The RMS wavefront error the stated tolerances would induce, averaged over the field.
    ///
    /// <para>Nodal aberration theory, after Gu 2020; the derivation and the two modelling
    /// choices it rests on are in <c>Core/Nat/Sensitivity.cs</c>. It costs one paraxial trace
    /// and a double loop over surfaces - no rays, no coefficients, no Buchdahl chain.</para>
    ///
    /// <para>The field average is taken over the fields the design defines, weighted as they
    /// are weighted, exactly as PRMSA averages over them. Only the field-linear astigmatic term
    /// sees the field, and it enters squared, so the mean of the SQUARED fractional height is
    /// the exact quantity wanted rather than an approximation of it.</para>

    /// <summary>
    /// One named aberration coefficient, in transverse measure.
    ///
    /// <para>The same <c>BuchdahlTerms</c> the report prints, indexed by the same name, so an
    /// operand and the report cannot disagree about what <c>Tau15</c> is. The whole cost is the
    /// coefficient run the probe has already cached for this wavelength, so twenty coefficient
    /// operands in one merit function cost what one costs - which is what makes a merit function
    /// built out of them practical rather than merely possible.</para>
    /// </summary>
    private static Dual Coefficient(Operand op, DesignProbe probe, OperandContext ctx)
    {
        string name = op.Coefficient ?? "";
        if (name.Length == 0)
            throw new InvalidOperationException(
                "a coefficient operand carries no coefficient name. It should not have parsed.");

        int wave = ctx.WaveIndex(op.Wave);

        // Surface 0 is the object surface and contributes to nothing, so it means the system -
        // which is also what leaving the input off gives. For a PART, the system's value is that
        // part summed over the surfaces, which is the decomposition a designer asking "how much
        // of this is induced" wants.
        return op.Surface == 0 && op.Part == CoefficientPart.Total
             ? probe.Coefficients(wave)[name]
             : probe.SurfaceCoefficients(wave, op.Surface, op.Part)[name];
    }
    /// </summary>
    private static Dual AsBuilt(Operand op, DesignProbe probe, OperandContext ctx)
    {
        int w = ctx.WaveIndex(op.Wave);
        var p = probe.Paraxial(w);

        // Degrees on the page, radians in the theory - the one conversion, in one place.
        double tilt = op.Tilt * Math.PI / 180.0;

        double max = ctx.MaxField;
        double num = 0.0, den = 0.0;
        for (int f = 1; f <= ctx.FieldCount; f++)
        {
            double h = Math.Abs(max) > 1e-15 ? ctx.Fields[f] / max : 0.0;
            double weight = ctx.FieldWeights[f];
            num += weight * h * h;
            den += weight;
        }
        double meanSquareField = den > 0.0 ? num / den : 1.0;

        return AdN.Sensitivity.AsBuilt(probe.System, probe.Indices(w), p,
                                       op.Decentre, tilt, meanSquareField);
    }

    /// <summary>
    /// Total track: the first surface to the image plane.
    ///
    /// <para>The object distance is not part of it - a track is the length of the instrument,
    /// not of the scene in front of it - so the sum starts at surface one. Every thickness in
    /// it is a variable's worth of derivative if it was declared one.</para>
    /// </summary>
    private static Dual TotalTrack(DesignProbe probe)
    {
        Dual t = default;
        int image = probe.ImageSurface;
        for (int i = 1; i < image; i++) t += probe.System.Surfaces[i].Thickness;
        return t;
    }

    /// <summary>
    /// The paraxial ray of a given field and pupil coordinate, at a surface.
    ///
    /// <para>A paraxial ray is the marginal ray scaled by the pupil fraction plus the chief ray
    /// at that field, which is what makes the pair a basis. It meets a surface at the VERTEX
    /// plane by definition, so PZ is identically zero - the departure of a real ray from that
    /// plane is the sag, and RZ is where to read it.</para>
    ///
    /// <para>The direction cosines are normalised, so PN is not quite one and PL, PM are not
    /// quite the slopes. That is what makes them comparable with RL, RM and RN, which is the
    /// only reason to ask for a paraxial direction as a cosine at all.</para>
    /// </summary>
    private static Dual Paraxial(Operand op, DesignProbe probe, OperandContext ctx)
    {
        int w = ctx.WaveIndex(op.Wave);
        double field = ctx.FieldFor(op.Hy);
        var p = probe.Paraxial(w, field);
        int s = Clamp(op.Surface, p.Y.Length);

        // Meridional: pupil fraction on the marginal ray, plus the chief ray entire.
        // Sagittal: the marginal ray alone - a field in the meridian gives the chief ray no
        // sagittal component.
        Dual y = op.Py * p.Y[s] + p.Ybar[s];
        Dual x = op.Px * p.Y[s];
        Dual uy = op.Py * p.U[s] + p.Ubar[s];
        Dual ux = op.Px * p.U[s];

        switch (op.Type)
        {
            case OperandType.PX: return x;
            case OperandType.PY: return y;
            case OperandType.PZ: return default;
        }

        Dual norm = DMath.Sqrt(1.0 + ux * ux + uy * uy);
        return op.Type switch
        {
            OperandType.PL => ux / norm,
            OperandType.PM => uy / norm,
            _ => 1.0 / norm,                       // PN
        };
    }

    /// <summary>
    /// A real ray at a surface: where it struck, and which way it left.
    ///
    /// <para>The position is in that surface's own vertex frame, so RZ is the sag at the point
    /// of incidence. The direction cosines are the ones the ray carries AFTER refracting there.
    /// At the image plane nothing refracts and they are the direction of arrival.</para>
    /// </summary>
    private static Dual Real(Operand op, DesignProbe probe, OperandContext ctx)
    {
        int w = ctx.WaveIndex(op.Wave);
        double field = ctx.FieldFor(op.Hy);
        var hits = probe.Ray(w, field, op.Py, op.Px);
        int s = Clamp(op.Surface, hits.Length);
        var h = hits[s];

        if (!h.Ok)
            throw new RayFailureException(
                "the real ray for operand " + op.Label + " does not reach surface " + s
              + ". It missed a surface, or was totally internally reflected.");

        return op.Type switch
        {
            OperandType.RX => h.X,
            OperandType.RY => h.Y,
            OperandType.RZ => h.Z,
            OperandType.RL => h.L,
            OperandType.RM => h.M,
            _ => h.N,                              // RN
        };
    }

    /// <summary>
    /// Edge thickness of the gap after a surface, measured at the clear aperture.
    ///
    /// <para>The two surfaces bounding a gap are generally different sizes, and the blank has
    /// to survive at the LARGER of them, so both sags are taken at the greater of the two
    /// radii. With the vertex of the second surface a thickness downstream of the first, the
    /// gap at radius r is</para>
    ///
    /// <code>
    ///     edge = t + sag_next(r) - sag_this(r)
    /// </code>
    ///
    /// <para>which is the centre thickness for two planes and gets smaller as a positive lens
    /// steepens - which is the whole reason to constrain it.</para>
    ///
    /// <para>The radius comes from the paraxial beam rather than from the declared
    /// semi-diameter, so it follows the design as the optimiser changes it. See
    /// <c>DesignProbe.ApertureRadius</c>.</para>
    /// </summary>
    private static Dual EdgeThickness(Operand op, DesignProbe probe)
    {
        int i = op.Surface;
        int next = i + 1;
        if (next >= probe.System.Surfaces.Count) return default;

        Dual r = DMath.Max(probe.ApertureRadius(i), probe.ApertureRadius(next));
        var a = probe.System.Surfaces[i];
        var b = probe.System.Surfaces[next];

        Dual sagA = a.Sag(r), sagB = b.Sag(r);
        // Outside the surface the sag is not a real number. Falling back to the vertex keeps a
        // wildly over-large aperture from poisoning the whole Jacobian with NaN, and the centre
        // thickness it then reports is an over-estimate of the edge, which a boundary operand
        // reads as "no violation" - the honest answer when the geometry has stopped existing.
        if (Dual.IsNaN(sagA)) sagA = default;
        if (Dual.IsNaN(sagB)) sagB = default;

        return a.Thickness + sagB - sagA;
    }

    /// <summary>
    /// Clear diameter over centre thickness, for the element after a surface.
    ///
    /// <para>A ratio rather than a difference because that is how a shop states the limit: a
    /// blank much wider than it is thick flexes under the tool and will not hold its figure,
    /// whatever its absolute size.</para>
    /// </summary>
    private static Dual DiameterToThickness(Operand op, DesignProbe probe)
    {
        int i = op.Surface;
        int next = i + 1;
        if (next >= probe.System.Surfaces.Count) return default;

        Dual r = DMath.Max(probe.ApertureRadius(i), probe.ApertureRadius(next));
        Dual t = probe.System.Surfaces[i].Thickness;
        if (DMath.Abs(t) < 1e-12) return 1e12;      // a vanishing thickness is infinitely bad
        return 2.0 * r / t;
    }

    /// <summary>
    /// Real lateral colour: the spread in real chief-ray height at the image between the
    /// extreme wavelengths.
    ///
    /// <para>Both rays are caught on the IMAGE SURFACE the design defines, not on each colour's
    /// own paraxial focus. Two different planes would fold the axial colour into the answer,
    /// and axial colour has its own operand.</para>
    /// </summary>
    private static Dual LateralColour(Operand op, DesignProbe probe, OperandContext ctx)
    {
        // A monochromatic design has no colour to spread, and differencing a wavelength against
        // itself would say so in a roundabout way.
        if (!ctx.HasSpectrum) return default;

        double field = ctx.FieldFor(op.Hy);
        int image = probe.ImageSurface;

        var shortHits = probe.Ray(ctx.WaveShort, field, 0.0, 0.0);
        var longHits = probe.Ray(ctx.WaveLong, field, 0.0, 0.0);
        if (!shortHits[image].Ok || !longHits[image].Ok)
            throw new RayFailureException("the chief ray for LCF does not reach the image.");

        return shortHits[image].Y - longHits[image].Y;
    }

    /// <summary>
    /// Real axial colour: how far apart the extreme wavelengths cross the axis.
    ///
    /// <para>The marginal ray is traced on axis in each colour and followed from the last
    /// refracting surface to where it meets the axis, which is a distance rather than a height
    /// - so this is the longitudinal quantity a designer means by axial colour, and not the
    /// transverse blur it produces. Positive when the longer wavelength focuses further from
    /// the lens.</para>
    /// </summary>
    private static Dual AxialColour(DesignProbe probe, OperandContext ctx)
    {
        if (!ctx.HasSpectrum) return default;

        return AxisCrossing(probe, ctx.WaveLong) - AxisCrossing(probe, ctx.WaveShort);
    }

    /// <summary>
    /// Where the on-axis marginal ray meets the axis, measured from the last refracting
    /// surface's vertex plane and positive toward the image.
    /// </summary>
    private static Dual AxisCrossing(DesignProbe probe, int wave)
    {
        int last = probe.LastOpticalSurface;
        var hits = probe.Ray(wave, 0.0, 1.0, 0.0);
        var h = hits[last];
        if (!h.Ok) throw new RayFailureException("the marginal ray for AXC does not emerge.");
        if (DMath.Abs(h.M) < 1e-15) return default;         // a collimated emergent beam

        return h.Z - h.Y * h.N / h.M;
    }

    /// <summary>
    /// Real distortion, as a percentage of the paraxial image height.
    ///
    /// <para>The real chief ray against where the paraxial one put the same field point, both
    /// on the image surface the design defines. This is the F-tan(theta) mapping, which is what
    /// "distortion" means unqualified.</para>
    ///
    /// <para>Zero on axis, where there is no height to be a percentage of.</para>
    /// </summary>
    private static Dual Distortion(Operand op, DesignProbe probe, OperandContext ctx)
    {
        int w = ctx.WaveIndex(op.Wave);
        double field = ctx.FieldFor(op.Hy);
        var p = probe.Paraxial(w, field);
        Dual reference = p.ImageHeight;
        if (DMath.Abs(reference) < 1e-12) return default;

        int image = probe.ImageSurface;
        var hits = probe.Ray(w, field, 0.0, 0.0);
        if (!hits[image].Ok)
            throw new RayFailureException("the chief ray for DISTF does not reach the image.");

        return 100.0 * (hits[image].Y - reference) / reference;
    }

    private static int Clamp(int surface, int count) =>
        surface < 0 ? 0 : surface >= count ? count - 1 : surface;
}

/// <summary>
/// Thrown when a real ray needed by an operand does not get through the system.
///
/// <para>A ray that misses a surface or is totally internally reflected is a real fact about
/// the design the optimiser has just proposed, not a program fault, and the search has to be
/// able to treat it as a very bad point and step back rather than falling over. That is why it
/// has a type of its own.</para>
/// </summary>
public sealed class RayFailureException : Exception
{
    public RayFailureException(string message) : base(message) { }
}
