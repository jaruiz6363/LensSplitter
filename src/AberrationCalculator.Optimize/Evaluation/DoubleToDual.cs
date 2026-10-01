extern alias Ad;

using System;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.RayTrace;

using AdA = Ad::AberrationCalculator.Core.Aberrations;
using AdR = Ad::AberrationCalculator.Core.RayTrace;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// Results computed in ordinary arithmetic, lifted into dual numbers with zero derivatives.
///
/// <para><b>Why a value-only evaluation should not be a dual one.</b> The aberration chain in
/// dual arithmetic costs about ten times what it costs in doubles - measured at 880 microseconds
/// against 90 on an eleven-surface triplet, nine tenths of it the seventh-order tertiary scheme.
/// A pass that wants no derivative pays all of that and throws the derivative away, and the
/// pattern search inside every hop does it a thousand times.</para>
///
/// <para>So the expensive part - the trace, the coefficients, the tertiary terms - runs in
/// doubles, and only the operand formulas stay dual, where they work on a few dozen numbers and
/// cost nothing. The operands themselves are not duplicated: they read the same types they
/// always did, holding the same values, with every derivative zero.</para>
///
/// <para><b>Every field is listed explicitly, on purpose.</b> A conversion that misses one
/// leaves a zero where a number should be, which no exception reports and which looks exactly
/// like an aberration that has been corrected. <c>TheDoubleAndDualPathsAgree</c> holds every
/// operand to the two paths agreeing, so a field left out of this file fails a test rather than
/// quietly improving the merit.</para>
/// </summary>
internal static class DoubleToDual
{
    public static Dual[] Lift(double[] values)
    {
        var d = new Dual[values.Length];
        for (int i = 0; i < values.Length; i++) d[i] = values[i];
        return d;
    }

    public static AdR.ParaxialResult Paraxial(ParaxialResult p) => new()
    {
        Y = Lift(p.Y),
        U = Lift(p.U),
        Ybar = Lift(p.Ybar),
        Ubar = Lift(p.Ubar),
        N = Lift(p.N),
        Efl = p.Efl,
        Power = p.Power,
        Bfl = p.Bfl,
        Epd = p.Epd,
        EntrancePupilPosition = p.EntrancePupilPosition,
        ExitPupilPosition = p.ExitPupilPosition,
        ExitPupilFromLastSurface = p.ExitPupilFromLastSurface,
        ExitPupilDiameter = p.ExitPupilDiameter,
        FNumber = p.FNumber,
        ImageHeight = p.ImageHeight,
        ParaxialFocusDistance = p.ParaxialFocusDistance,
        ParaxialImageHeight = p.ParaxialImageHeight,
        Magnification = p.Magnification,
        LagrangeInvariant = p.LagrangeInvariant,
        InvariantDrift = p.InvariantDrift,
        InfiniteConjugate = p.InfiniteConjugate,
    };
    /// <summary>
    /// A whole coefficient result: the totals, and the per-surface contributions with their
    /// intrinsic, figuring and induced parts.
    ///
    /// <para>All four arrays are carried because all four are targetable. They were not, while
    /// only the totals were - lifting arrays nothing reads would have given back the saving this
    /// path exists for - and the cost of carrying them was measured before they were added rather
    /// than assumed to be small.</para>
    ///
    /// <para><b>A null aspheric entry stays null.</b> It means a SPHERICAL surface, which has no
    /// figuring rather than figuring of zero, and the difference matters where the two are summed:
    /// an absent term contributes nothing and a zero-valued one would too, but only the first is
    /// honest about a surface having no figuring to speak of.</para>
    /// </summary>
    public static AdA.BuchdahlResult Result(BuchdahlResult r)
    {
        return new AdA.BuchdahlResult
        {
            Totals = Coefficients(r.Totals),
            PerSurface = Many(r.PerSurface),
            Intrinsic = Many(r.Intrinsic),
            Induced = Many(r.Induced),
            Aspheric = ManyOrNull(r.Aspheric),
            FNumber = r.FNumber,
            Lagrange = r.Lagrange,
            TertiaryUnattributed = r.TertiaryUnattributed,
        };
    }

    private static AdA.BuchdahlTerms[] Many(BuchdahlTerms[] source)
    {
        var d = new AdA.BuchdahlTerms[source.Length];
        for (int i = 0; i < d.Length; i++) d[i] = Coefficients(source[i]);
        return d;
    }

    private static AdA.BuchdahlTerms?[] ManyOrNull(BuchdahlTerms?[] source)
    {
        var d = new AdA.BuchdahlTerms?[source.Length];
        for (int i = 0; i < d.Length; i++)
            d[i] = source[i] == null ? null : Coefficients(source[i]!);
        return d;
    }

    public static AdA.BuchdahlTerms Coefficients(BuchdahlTerms t)
    {
        var d = new AdA.BuchdahlTerms();

        d.B = t.B; d.F = t.F; d.C = t.C; d.Pi = t.Pi; d.E = t.E;

        d.B5 = t.B5; d.F1 = t.F1; d.F2 = t.F2;
        d.M1 = t.M1; d.M2 = t.M2; d.M3 = t.M3;
        d.N1 = t.N1; d.N2 = t.N2; d.N3 = t.N3;
        d.C5 = t.C5; d.Pi5 = t.Pi5; d.E5 = t.E5;

        d.B7 = t.B7;
        d.Tau2 = t.Tau2; d.Tau3 = t.Tau3; d.Tau4 = t.Tau4; d.Tau5 = t.Tau5;
        d.Tau6 = t.Tau6; d.Tau7 = t.Tau7; d.Tau8 = t.Tau8; d.Tau9 = t.Tau9;
        d.Tau10 = t.Tau10; d.Tau11 = t.Tau11; d.Tau12 = t.Tau12; d.Tau13 = t.Tau13;
        d.Tau14 = t.Tau14; d.Tau15 = t.Tau15; d.Tau16 = t.Tau16; d.Tau17 = t.Tau17;
        d.Tau18 = t.Tau18; d.Tau19 = t.Tau19; d.Tau20 = t.Tau20;

        d.E5b = t.E5b;
        d.Bb = t.Bb; d.Fb = t.Fb; d.Cb = t.Cb; d.Eb = t.Eb;

        return d;
    }

    public static AdR.RealRayTrace.SurfaceHit[] Ray(RealRayTrace.SurfaceHit[] hits)
    {
        var d = new AdR.RealRayTrace.SurfaceHit[hits.Length];
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            d[i] = new AdR.RealRayTrace.SurfaceHit(h.X, h.Y, h.Z, h.L, h.M, h.N, h.Ok);
        }
        return d;
    }
}
