extern alias Ad;

using System;
using System.Collections.Generic;

using AdM = Ad::AberrationCalculator.Core.Models;
using AdR = Ad::AberrationCalculator.Core.RayTrace;
using AdA = Ad::AberrationCalculator.Core.Aberrations;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// One pass of the aberration chain, seeded on one variable, with everything an operand might
/// ask of it computed on demand and kept.
///
/// <para>A probe is cheap to make and expensive to interrogate, so nothing is computed until
/// something asks for it. Two operands wanting the coefficients at the same wavelength get the
/// same Buchdahl run; two wanting the same ray get the same trace. A probe is built per
/// variable and thrown away, so the caching is per column of the Jacobian - which is exactly
/// the scope over which those answers are the same.</para>
///
/// <para>Every quantity that comes out is a <see cref="Dual"/>: the value, and its exact
/// derivative with respect to the one variable this probe was seeded on.</para>
/// </summary>
public sealed class DesignProbe
{
    private readonly AdM.OpticalSystem _sys;
    private readonly Dual[][] _indices;          // [wavelength][surface]
    private readonly double _maxField;
    private readonly int _primary;

    private readonly Dictionary<long, AdR.ParaxialResult> _paraxial = new();
    private readonly Dictionary<int, AdA.BuchdahlResult> _coefficients = new();
    private readonly Dictionary<long, AdR.RealRayTrace.SurfaceHit[]> _rays = new();
    private Dual[]? _apertureRadius;

    // The plain-double design, present only on a probe that has been asked for VALUES and no
    // derivative. When it is here the chain runs in ordinary arithmetic and the results are
    // lifted into duals with zero derivatives - see DoubleToDual for why that is worth doing.
    private readonly Core.Models.OpticalSystem? _core;
    private readonly double[][]? _coreIndices;

    public DesignProbe(AdM.OpticalSystem system, Dual[][] indices, double maxField,
                       int primaryWavelength)
    {
        _sys = system ?? throw new ArgumentNullException(nameof(system));
        _indices = indices ?? throw new ArgumentNullException(nameof(indices));
        _maxField = maxField;
        _primary = primaryWavelength;
    }

    /// <summary>
    /// A probe for a pass that wants values and no derivative.
    ///
    /// <para>It still answers in dual numbers, because the operands are written once and read
    /// one set of types; every derivative it returns is zero, which is the truth for a pass that
    /// was seeded on nothing.</para>
    /// </summary>
    public DesignProbe(AdM.OpticalSystem system, Dual[][] indices, double maxField,
                       int primaryWavelength,
                       Core.Models.OpticalSystem core, double[][] coreIndices)
        : this(system, indices, maxField, primaryWavelength)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _coreIndices = coreIndices ?? throw new ArgumentNullException(nameof(coreIndices));
    }

    public AdM.OpticalSystem System => _sys;

    /// <summary>Number of wavelengths in the evaluation set; at least one.</summary>
    public int WaveCount => _indices.Length;

    /// <summary>Index of the reference wavelength.</summary>
    public int PrimaryWave => _primary;

    /// <summary>The largest field the design defines, in the units its field type names.</summary>
    public double MaxField => _maxField;

    public int LastOpticalSurface => _sys.LastOpticalSurface();

    public int ImageSurface => _sys.Surfaces.Count - 1;

    /// <summary>The paraxial trace at one wavelength, taken at the maximum field.</summary>
    /// <summary>
    /// Refractive index after each surface, at one wavelength. Exposed for the operands that
    /// need the prescription itself rather than a quantity derived from it.
    /// </summary>
    public Dual[] Indices(int wave) => _indices[Clamp(wave)];

    public AdR.ParaxialResult Paraxial(int wave) => Paraxial(wave, _maxField);

    /// <summary>The paraxial trace at one wavelength and one field.</summary>
    public AdR.ParaxialResult Paraxial(int wave, double field)
    {
        long key = ((long)Clamp(wave) << 32) ^ (uint)field.GetHashCode();
        if (_paraxial.TryGetValue(key, out var cached)) return cached;

        var p = _core != null
            ? DoubleToDual.Paraxial(Core.RayTrace.ParaxialTrace.Trace(
                  _core, _coreIndices![Clamp(wave)], field))
            : AdR.ParaxialTrace.Trace(_sys, _indices[Clamp(wave)], field);

        _paraxial[key] = p;
        return p;
    }

    /// <summary>
    /// The thirty-seven transverse aberration coefficients at one wavelength, by Buchdahl's
    /// computing scheme.
    ///
    /// <para><b>Figuring is carried here, and the route is not chosen in this file.</b>
    /// <c>TertiaryCoefficients.Attach</c> sends a figured system to the aspheric arrangement of
    /// Sec. 85 and a spherical one to Buchdahl's own published table, and it makes that choice
    /// once per evaluation out of data it has already computed - not per surface, and not inside
    /// the arithmetic. A spherical design therefore travels exactly the path it always did, bit
    /// for bit. <see cref="SupportedDesign"/> refuses no class of design: the figured flat facing
    /// collimated light, once refused because its route existed only in the plain-double build,
    /// now takes the differentiated series route (<c>DualSeries</c>).</para>
    /// </summary>
    public AdA.BuchdahlTerms Coefficients(int wave) => Whole(Clamp(wave)).Totals;

    public AdA.BuchdahlTerms SurfaceCoefficients(int wave, int surface) =>
        SurfaceCoefficients(wave, surface, Operands.CoefficientPart.Total);

    /// <summary>
    /// One surface's contribution, or one PART of it, in the same transverse measure as the
    /// system totals. Surface 0 means the whole system: for the total that is
    /// <see cref="Coefficients(int)"/>, and for a part it is that part summed over the surfaces.
    ///
    /// <para><b>The scaling is the whole of why this is not an array lookup.</b> The chain keeps
    /// per-surface contributions unscaled and multiplies only the totals by the F/number. Handing
    /// the raw array out would give a number in different units from the system value printed
    /// beside it, agreeing with nothing and summing to nothing. Scaled here, the surfaces add to
    /// the total and the three parts add to the contribution.</para>
    ///
    /// <para><b>Tau2 to Tau20 are not here.</b> The per-surface terms do carry them, as each
    /// surface's share of the total, but with no intrinsic, figuring or induced split and, on a
    /// figured flat facing collimated light, as zeros; the parser refuses a tertiary coefficient
    /// with a surface or a part for that reason, a silent zero being the one answer worse than a
    /// refusal, and so only the eighteen through B7 are summed here.</para>
    /// </summary>
    public AdA.BuchdahlTerms SurfaceCoefficients(int wave, int surface,
                                                 Operands.CoefficientPart part)
    {
        var whole = Whole(Clamp(wave));

        if (part == Operands.CoefficientPart.Total && surface == 0) return whole.Totals;

        int last = _sys.LastOpticalSurface();
        if (surface < 0 || surface >= whole.PerSurface.Length)
            throw new ArgumentOutOfRangeException(
                nameof(surface), surface,
                $"surface {surface} is not one this design has a contribution for");

        var sum = new AdA.BuchdahlTerms();
        if (surface == 0)
            for (int i = 1; i <= last && i < whole.PerSurface.Length; i++) Add(sum, Pick(whole, i, part));
        else
            Add(sum, Pick(whole, surface, part));

        return Scale(sum, whole.FNumber);
    }

    /// <summary>The array a part comes from. A spherical surface has no aspheric entry at all.</summary>
    private static AdA.BuchdahlTerms? Pick(AdA.BuchdahlResult r, int i, Operands.CoefficientPart part)
        => part switch
        {
            Operands.CoefficientPart.Intrinsic => i < r.Intrinsic.Length ? r.Intrinsic[i] : null,
            Operands.CoefficientPart.Figuring => i < r.Aspheric.Length ? r.Aspheric[i] : null,
            Operands.CoefficientPart.Induced => i < r.Induced.Length ? r.Induced[i] : null,
            _ => i < r.PerSurface.Length ? r.PerSurface[i] : null,
        };

    /// <summary>
    /// Adds one set of terms into another. Only the eighteen through B7 are carried; tau2 to
    /// tau20 are system operands and are refused before they reach here.
    /// </summary>
    private static void Add(AdA.BuchdahlTerms into, AdA.BuchdahlTerms? t)
    {
        if (t == null) return;         // a spherical surface's figuring: absent, not zero-valued

        into.B += t.B;     into.F += t.F;     into.C += t.C;
        into.Pi += t.Pi;   into.E += t.E;
        into.B5 += t.B5;   into.F1 += t.F1;   into.F2 += t.F2;
        into.M1 += t.M1;   into.M2 += t.M2;   into.M3 += t.M3;
        into.N1 += t.N1;   into.N2 += t.N2;   into.N3 += t.N3;
        into.C5 += t.C5;   into.Pi5 += t.Pi5; into.E5 += t.E5;
        into.B7 += t.B7;
    }

    private static AdA.BuchdahlTerms Scale(AdA.BuchdahlTerms t, Dual f)
    {
        var s = new AdA.BuchdahlTerms();
        s.B = t.B * f;     s.F = t.F * f;     s.C = t.C * f;
        s.Pi = t.Pi * f;   s.E = t.E * f;
        s.B5 = t.B5 * f;   s.F1 = t.F1 * f;   s.F2 = t.F2 * f;
        s.M1 = t.M1 * f;   s.M2 = t.M2 * f;   s.M3 = t.M3 * f;
        s.N1 = t.N1 * f;   s.N2 = t.N2 * f;   s.N3 = t.N3 * f;
        s.C5 = t.C5 * f;   s.Pi5 = t.Pi5 * f; s.E5 = t.E5 * f;
        s.B7 = t.B7 * f;
        return s;
    }

    /// <summary>
    /// The whole coefficient result at one wavelength, computed once and kept.
    ///
    /// <para>A probe is built per variable and thrown away, so this caching is per column of the
    /// Jacobian - exactly the scope over which the answer is the same. Twenty coefficient
    /// operands over six surfaces cost one run of the scheme between them.</para>
    /// </summary>
    private AdA.BuchdahlResult Whole(int w)
    {
        if (_coefficients.TryGetValue(w, out var cached)) return cached;

        // Nine tenths of an evaluation is the tertiary scheme below. On a value-only probe it
        // runs in ordinary arithmetic, which measured ten times faster, and is lifted after.
        if (_core != null)
        {
            var pd = Core.RayTrace.ParaxialTrace.Trace(_core, _coreIndices![w], _maxField);
            var bd = Core.Aberrations.BuchdahlCoefficients.Compute(_core, pd);
            Core.Aberrations.TertiaryCoefficients.Attach(_core, _coreIndices[w], pd, bd, _maxField);

            var lifted = DoubleToDual.Result(bd);
            _coefficients[w] = lifted;
            return lifted;
        }

        var p = Paraxial(w);
        var b = AdA.BuchdahlCoefficients.Compute(_sys, p);
        AdA.TertiaryCoefficients.Attach(_sys, _indices[w], p, b, _maxField);

        _coefficients[w] = b;
        return b;
    }

    /// <summary>
    /// A real ray, kept at every surface it met. <paramref name="py"/> and
    /// <paramref name="px"/> are pupil coordinates as fractions of the pupil radius,
    /// meridional and sagittal; the chief ray is (0, 0) and the upper marginal ray (1, 0).
    /// </summary>
    /// <param name="atParaxialFocus">
    /// Where the ray is caught. False - the default for a merit function - uses the image
    /// surface the design defines, which is ONE plane shared by every wavelength. True uses
    /// each colour's own paraxial focus, which is what the aberration coefficients are referred
    /// to but is a different plane for every wavelength: measuring lateral colour there would
    /// fold the axial colour into it.
    /// </param>
    public AdR.RealRayTrace.SurfaceHit[] Ray(int wave, double fieldDeg, double py, double px,
                                             bool atParaxialFocus = false)
    {
        int w = Clamp(wave);
        long key = ((long)w << 48) ^ ((long)fieldDeg.GetHashCode() << 24)
                 ^ ((long)py.GetHashCode() * 31) ^ (uint)px.GetHashCode()
                 ^ (atParaxialFocus ? 1L << 62 : 0L);
        if (_rays.TryGetValue(key, out var cached)) return cached;

        // The paraxial trace the launch is built on has to be the one at THIS ray's field, or
        // the pupil it is aimed through belongs to a different field point.
        if (_core != null)
        {
            var pd = Core.RayTrace.ParaxialTrace.Trace(_core, _coreIndices![w], fieldDeg);
            var lifted = DoubleToDual.Ray(Core.RayTrace.RealRayTrace.TraceRecord(
                _core, _coreIndices[w], pd, fieldDeg, py, px, atParaxialFocus));
            _rays[key] = lifted;
            return lifted;
        }

        var p = Paraxial(w, fieldDeg);
        var hits = AdR.RealRayTrace.TraceRecord(_sys, _indices[w], p, fieldDeg, py, px,
                                                atParaxialFocus);
        _rays[key] = hits;
        return hits;
    }

    /// <summary>
    /// The paraxial beam half-height at each surface: |marginal| + |chief| at the maximum
    /// field, in the reference colour.
    ///
    /// <para>This is the clear aperture a surface has to have, and it is where the
    /// edge-thickness and diameter-to-thickness operands are measured. Taking it from the trace
    /// rather than from the semi-diameter the file declared is the point: a declared
    /// semi-diameter is a constant, and it would tell the optimiser that thinning a lens costs
    /// nothing at its edge when in fact the beam is still the size it was.</para>
    /// </summary>
    public Dual ApertureRadius(int surface)
    {
        if (_apertureRadius == null)
        {
            var p = Paraxial(_primary);
            int n = _sys.Surfaces.Count;
            var r = new Dual[n];
            for (int i = 0; i < n; i++)
            {
                Dual y = i < p.Y.Length ? p.Y[i] : default;
                Dual yb = i < p.Ybar.Length ? p.Ybar[i] : default;
                r[i] = Abs(y) + Abs(yb);
            }
            _apertureRadius = r;
        }
        return surface >= 0 && surface < _apertureRadius.Length ? _apertureRadius[surface] : default;
    }

    private static Dual Abs(Dual d) => d.Value >= 0.0 ? d : -d;

    private int Clamp(int wave) => wave < 0 ? _primary
                                 : wave >= _indices.Length ? _indices.Length - 1 : wave;
}
