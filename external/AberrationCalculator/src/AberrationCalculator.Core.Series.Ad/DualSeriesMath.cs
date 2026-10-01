using System;

using AberrationCalculator.Core.SeriesArithmetic;

namespace AberrationCalculator.Core.SeriesAd;

/// <summary>
/// The functions the aberration chain calls, for <see cref="DualSeries"/>.
///
/// <para>Every one is the chain rule with <see cref="LaurentMath"/> underneath: the value comes
/// from the series routine that was already validated, and the derivative is that routine's own
/// derivative times the incoming one. Nothing about the series arithmetic is restated here, which
/// is the point - a correction to <c>Sqrt</c> on a series lands in this derivative the moment it
/// lands in the value.</para>
/// </summary>
public static class DualSeriesMath
{
    public const double PI = Math.PI;

    /// <summary>
    /// Absolute value. The derivative follows the branch the VALUE takes, which is the only
    /// choice available: |x| has no derivative at zero, and every other convention here decides
    /// a branch on the value.
    /// </summary>
    public static DualSeries Abs(DualSeries x) =>
        x.Value < 0.0 ? new DualSeries(-x.Value, -x.Deriv) : x;

    /// <summary>Square root, derivative <c>x' / (2 sqrt x)</c>.</summary>
    public static DualSeries Sqrt(DualSeries x)
    {
        LaurentSeries r = LaurentMath.Sqrt(x.Value);
        return new DualSeries(r, x.Deriv / (2.0 * r));
    }

    /// <summary>
    /// A power. Only the case the chain actually uses is differentiated as a general power:
    /// a series raised to a CONSTANT exponent, <c>d(x^y) = y x^(y-1) x'</c>. A varying exponent
    /// would need a logarithm of a Laurent series, which is not defined for a series with a pole
    /// and which nothing here asks for; that case keeps its value and reports no derivative
    /// rather than inventing one.
    /// </summary>
    public static DualSeries Pow(DualSeries x, DualSeries y)
    {
        LaurentSeries v = LaurentMath.Pow(x.Value, y.Value);
        if (!y.Deriv.IsZero) return new DualSeries(v);

        LaurentSeries dx = LaurentMath.Pow(x.Value, y.Value - 1.0);
        return new DualSeries(v, y.Value * dx * x.Deriv);
    }

    // The trigonometric functions are called on FIELD ANGLES, which are constants of the problem
    // - never on a quantity the optimiser varies and never on one carrying a pole. LaurentMath
    // returns NaN for a non-constant argument for exactly that reason, and the derivative here
    // follows the same rule: the ordinary one, which is right wherever these are legitimately
    // reached.
    public static DualSeries Sin(DualSeries x) =>
        new(LaurentMath.Sin(x.Value), LaurentMath.Cos(x.Value) * x.Deriv);

    public static DualSeries Cos(DualSeries x) =>
        new(LaurentMath.Cos(x.Value), -LaurentMath.Sin(x.Value) * x.Deriv);

    public static DualSeries Tan(DualSeries x)
    {
        LaurentSeries c = LaurentMath.Cos(x.Value);
        return new DualSeries(LaurentMath.Tan(x.Value), x.Deriv / (c * c));
    }

    public static DualSeries Atan(DualSeries x) =>
        new(LaurentMath.Atan(x.Value), x.Deriv / (1.0 + x.Value * x.Value));

    public static DualSeries Atan2(DualSeries a, DualSeries b)
    {
        LaurentSeries d = a.Value * a.Value + b.Value * b.Value;
        return new DualSeries(LaurentMath.Atan2(a.Value, b.Value),
                              (b.Value * a.Deriv - a.Value * b.Deriv) / d);
    }

    /// <summary>Rounding is piecewise constant, so its derivative is zero where it exists.</summary>
    public static DualSeries Round(DualSeries x) => new(LaurentMath.Round(x.Value));

    public static int Sign(DualSeries x) => LaurentMath.Sign(x.Value);

    public static DualSeries Min(DualSeries a, DualSeries b) => a.Value <= b.Value ? a : b;
    public static DualSeries Max(DualSeries a, DualSeries b) => a.Value >= b.Value ? a : b;
    public static int Min(int a, int b) => Math.Min(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);

    /// <summary>
    /// Absent in BOTH senses - no value and no derivative - which is the rule this repository
    /// already follows for <c>Dual</c> and for the same reason. A quantity that is zero because
    /// nobody has moved it yet has value nothing and a derivative of something, and dropping its
    /// term leaves the value perfectly right while the gradient goes silently short. That fault
    /// has been found here twice; it is not found a third time by hoping.
    /// </summary>
    public static bool Vanishes(DualSeries x) => x.Value.IsZero && x.Deriv.IsZero;

    /// <summary>The same, against a tolerance.</summary>
    public static bool Vanishes(DualSeries x, double tolerance) =>
        LaurentMath.Vanishes(x.Value, tolerance) && x.Deriv.IsZero;
}
