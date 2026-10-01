using System;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace AberrationCalculator.Core.Ad;

/// <summary>
/// A number carrying its own derivative: forward-mode automatic differentiation in the
/// smallest form that does the job.
///
/// <para>A <see cref="Dual"/> is the pair <c>(v, dv)</c> where <c>v</c> is the quantity and
/// <c>dv</c> its derivative with respect to ONE design variable. Every arithmetic operator
/// carries the derivative along by the chain rule, so a function computed on duals returns
/// its own exact derivative with it - not a difference quotient, and not a truncation. There
/// is no step size to choose and no subtractive cancellation to lose digits to.</para>
///
/// <para><b>Why one derivative and not a gradient vector.</b> The obvious alternative is to
/// carry an array of partials so that a single pass yields the whole gradient. That allocates
/// an array per arithmetic operation, and the Buchdahl chain performs tens of thousands of
/// them per evaluation. This form is sixteen bytes, lives entirely in registers, allocates
/// nothing, and the n variables are n INDEPENDENT passes - so they run in parallel, on as
/// many cores as there are. Same arithmetic count, no garbage, and it scales sideways.</para>
///
/// <para><b>There is deliberately no implicit conversion back to <c>double</c>.</b> One would
/// make <c>double x = someDual;</c> compile and silently discard the derivative, which is the
/// one failure this whole scheme has to be proof against. Going the other way is implicit, so
/// the five thousand numeric literals of the aberration chain need no adornment at all.</para>
/// </summary>
public readonly struct Dual : IEquatable<Dual>, IComparable<Dual>, IFormattable
{
    /// <summary>The quantity itself.</summary>
    public readonly double Value;

    /// <summary>Its derivative with respect to the variable this pass is seeded on.</summary>
    public readonly double Deriv;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dual(double value, double deriv = 0.0) { Value = value; Deriv = deriv; }

    /// <summary>The variable being differentiated with respect to: value v, derivative 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Seed(double value) => new(value, 1.0);

    /// <summary>A constant of the design: it has a value and no derivative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Dual(double value) => new(value, 0.0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator +(Dual a, Dual b) => new(a.Value + b.Value, a.Deriv + b.Deriv);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator -(Dual a, Dual b) => new(a.Value - b.Value, a.Deriv - b.Deriv);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator -(Dual a) => new(-a.Value, -a.Deriv);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator +(Dual a) => a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator *(Dual a, Dual b) =>
        new(a.Value * b.Value, a.Deriv * b.Value + a.Value * b.Deriv);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual operator /(Dual a, Dual b)
    {
        double q = a.Value / b.Value;
        return new(q, (a.Deriv - q * b.Deriv) / b.Value);
    }

    // Ordering is on the value alone. The chain compares quantities to decide branches -
    // whether a surface is a plane, whether an incidence has vanished, whether a discriminant
    // has gone negative - and those are questions about the number, not about its slope.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Dual a, Dual b) => a.Value < b.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Dual a, Dual b) => a.Value > b.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Dual a, Dual b) => a.Value <= b.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Dual a, Dual b) => a.Value >= b.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Dual a, Dual b) => a.Value == b.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Dual a, Dual b) => a.Value != b.Value;

    public bool Equals(Dual other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is Dual d && Equals(d);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(Dual other) => Value.CompareTo(other.Value);

    // The `double.` statics the chain uses, under the names it uses them by, so that
    // `Scalar.IsInfinity(t)` reads the same in both arithmetics.
    public static readonly Dual PositiveInfinity = new(double.PositiveInfinity);
    public static readonly Dual NegativeInfinity = new(double.NegativeInfinity);
    public static readonly Dual NaN = new(double.NaN, double.NaN);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInfinity(Dual d) => double.IsInfinity(d.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(Dual d) => double.IsNaN(d.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPositiveInfinity(Dual d) => double.IsPositiveInfinity(d.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegativeInfinity(Dual d) => double.IsNegativeInfinity(d.Value);

    public override string ToString() =>
        Value.ToString("G17", CultureInfo.InvariantCulture) + " d=" +
        Deriv.ToString("G17", CultureInfo.InvariantCulture);

    // A formatted dual prints its VALUE alone. The chain formats numbers only to describe
    // itself - a series written out term by term, a diagnostic line - where the quantity is
    // what is being shown and a derivative beside every coefficient would be noise. The
    // parameterless form above, which is what a debugger shows, prints both.
    public string ToString(string? format) => Value.ToString(format, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? provider) => Value.ToString(format, provider);
}
