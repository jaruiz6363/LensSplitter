using System;
using System.Globalization;

using AberrationCalculator.Core.SeriesArithmetic;

namespace AberrationCalculator.Core.SeriesAd;

/// <summary>
/// A dual number whose value and derivative are each a truncated LAURENT SERIES.
///
/// <para><b>What this is for.</b> A figured flat facing collimated light has an identically zero
/// marginal incidence, so the incidence ratio is infinite and the finite aberration coefficients
/// arrive only after terms carrying different powers of it cancel. The answer is reached by
/// running the whole chain with that surface's curvature as a series variable and reading the
/// e^0 coefficient - which Core has done for a long time. What it could not do was differentiate
/// it, so the OPTIMISER refused such a design: the route exists in plain double alone, and in the
/// differentiating build the call compiled away to nothing, leaving a right value and a silently
/// wrong derivative. This is the arithmetic that removes that restriction.</para>
///
/// <para><b>Why a dual OF series rather than a series OF duals.</b> Both would work and they
/// compute the same thing. The other way round means making <see cref="LaurentSeries"/> generic
/// over its coefficient type - some three hundred lines of delicate, already-verified arithmetic
/// edited for a case it was not written for, with the sparse-skip hazard waiting at every
/// <c>== 0.0</c> inside it. This way <see cref="LaurentSeries"/> is not touched at all. Every
/// operation below is the ordinary dual-number rule with series arithmetic underneath, so the
/// series half stays exactly the code that was validated against Forbes, and what is new is one
/// small struct whose rules are the ones in every textbook.</para>
///
/// <para><b>Why it commutes with taking the limit.</b> The answer wanted is the e^0 coefficient,
/// and extracting a coefficient is linear. So the e^0 coefficient of the derivative series IS the
/// derivative of the e^0 coefficient, and the value and the derivative can be read off
/// independently at the end. Nothing has to be re-derived to justify that; it is linearity.</para>
///
/// <para><b>Branches are decided on the value, as everywhere else in this repository.</b> A
/// comparison asks the value series, which asks its own evaluation point. The derivative never
/// decides a branch - the same rule <c>Dual</c> follows, for the same reason: a derivative is
/// about how a quantity moves, not about which side of a test it is on. The one exception is
/// <see cref="DualSeriesMath.Vanishes"/>, which asks about both, because a term that is zero only
/// because nothing has moved it yet is not absent.</para>
/// </summary>
public readonly struct DualSeries : IEquatable<DualSeries>, IComparable<DualSeries>, IFormattable
{
    /// <summary>The quantity itself, as a series in the flat surface's curvature.</summary>
    public readonly LaurentSeries Value;

    /// <summary>Its derivative with respect to the one variable being differentiated.</summary>
    public readonly LaurentSeries Deriv;

    public DualSeries(LaurentSeries value, LaurentSeries deriv)
    {
        Value = value;
        Deriv = deriv;
    }

    public DualSeries(LaurentSeries value)
    {
        Value = value;
        Deriv = default;
    }

    /// <summary>The variable being differentiated with respect to: value v, derivative one.</summary>
    public static DualSeries Seed(LaurentSeries value) => new(value, 1.0);

    /// <summary>A constant of the problem: no derivative.</summary>
    public static implicit operator DualSeries(double value) => new(value);

    /// <summary>A series that carries no derivative - the series variable itself, for one.</summary>
    public static implicit operator DualSeries(LaurentSeries value) => new(value);

    // ── Arithmetic: the ordinary dual rules, over series ────────────────────────────────────

    public static DualSeries operator +(DualSeries a, DualSeries b) =>
        new(a.Value + b.Value, a.Deriv + b.Deriv);

    public static DualSeries operator -(DualSeries a, DualSeries b) =>
        new(a.Value - b.Value, a.Deriv - b.Deriv);

    public static DualSeries operator -(DualSeries a) => new(-a.Value, -a.Deriv);

    public static DualSeries operator +(DualSeries a) => a;

    public static DualSeries operator *(DualSeries a, DualSeries b) =>
        new(a.Value * b.Value, a.Deriv * b.Value + a.Value * b.Deriv);

    /// <summary>
    /// The quotient rule, written as <c>(a' - q b') / b</c> with <c>q</c> the quotient already
    /// formed, rather than as <c>(a' b - a b') / b^2</c>.
    ///
    /// <para>They are the same in exact arithmetic and not the same here. Squaring the divisor
    /// doubles the order of its leading term, and this divisor is a series whose leading term may
    /// already be far down the window that is carried - so <c>b^2</c> can push the quotient's
    /// terms past <see cref="LaurentSeries.MinOrder"/> and lose them, when the single division
    /// would have kept them. One division rather than two is also the cheaper of the two, but
    /// that is not why it is written this way.</para>
    /// </summary>
    public static DualSeries operator /(DualSeries a, DualSeries b)
    {
        LaurentSeries q = a.Value / b.Value;
        return new(q, (a.Deriv - q * b.Deriv) / b.Value);
    }

    // ── Comparison: on the value, as everywhere else ────────────────────────────────────────

    public static bool operator <(DualSeries a, DualSeries b) => a.Value < b.Value;
    public static bool operator >(DualSeries a, DualSeries b) => a.Value > b.Value;
    public static bool operator <=(DualSeries a, DualSeries b) => a.Value <= b.Value;
    public static bool operator >=(DualSeries a, DualSeries b) => a.Value >= b.Value;
    public static bool operator ==(DualSeries a, DualSeries b) => a.Value == b.Value;
    public static bool operator !=(DualSeries a, DualSeries b) => a.Value != b.Value;

    public bool Equals(DualSeries other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is DualSeries d && Equals(d);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(DualSeries other) => Value.CompareTo(other.Value);

    // ── The special values the chain asks about ─────────────────────────────────────────────

    public static readonly DualSeries PositiveInfinity = new(double.PositiveInfinity);
    public static readonly DualSeries NegativeInfinity = new(double.NegativeInfinity);
    public static readonly DualSeries NaN = new(double.NaN, double.NaN);

    public static bool IsInfinity(DualSeries d) => LaurentSeries.IsInfinity(d.Value);
    public static bool IsNaN(DualSeries d) => LaurentSeries.IsNaN(d.Value);

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? provider) =>
        Value.ToString(format, provider) + " + " + Deriv.ToString(format, provider) + " d";
}
