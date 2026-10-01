using System;
using System.Runtime.CompilerServices;

namespace AberrationCalculator.Core.Ad;

/// <summary>
/// The elementary functions of <see cref="Dual"/>, standing where <see cref="Math"/> stands in
/// the ordinary arithmetic. Each is the function and its derivative, by the chain rule.
///
/// <para>The integer overloads of <see cref="Min(int,int)"/> and <see cref="Max(int,int)"/> are
/// here because the aberration chain uses them on surface indices as well as on ray heights,
/// and an index has no derivative.</para>
/// </summary>
public static class DualMath
{
    public const double PI = Math.PI;

    /// <summary>
    /// Absolute value. Zero is a corner, where no derivative exists; nought is returned there.
    ///
    /// <para>Every use of this in the aberration chain is a magnitude test - is this curvature
    /// a plane, has this incidence vanished, how far has the invariant drifted - so the corner
    /// is a place the answer is being compared against a tolerance, not differentiated.</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Abs(Dual x) =>
        x.Value > 0.0 ? x : x.Value < 0.0 ? -x : new Dual(0.0, 0.0);

    /// <summary>
    /// Square root, derivative <c>dx / (2 sqrt x)</c>.
    ///
    /// <para>At exactly zero that derivative is infinite, and nought is returned instead. This
    /// is not a fudge: the only place the chain takes the root of something that can be
    /// identically zero is the radial coordinate <c>r = sqrt(x^2 + y^2)</c> of a ray at the
    /// vertex, which is the apex of a cone and genuinely has no derivative. Every use of
    /// <c>r</c> downstream is either <c>r^2</c>, which is smooth there, or is guarded by
    /// <c>r &gt; 1e-14</c> and never reached. Letting an infinity out here would poison a whole
    /// Jacobian column for the on-axis ray, whose spot is zero and whose derivative is zero.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Sqrt(Dual x)
    {
        double s = Math.Sqrt(x.Value);
        if (x.Value <= 0.0) return new Dual(s, 0.0);
        return new Dual(s, x.Deriv / (2.0 * s));
    }

    /// <summary>
    /// Power. The exponent is a constant at every site in the aberration chain - a pupil or
    /// field degree - and that case takes the simple rule <c>y x^(y-1) dx</c>, which stays
    /// finite for a negative base where the logarithmic form would not. The general form is
    /// carried anyway so that no caller has to know which it is getting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Pow(Dual x, Dual y)
    {
        double v = Math.Pow(x.Value, y.Value);
        if (x.Deriv == 0.0 && y.Deriv == 0.0) return new Dual(v, 0.0);

        if (y.Deriv == 0.0)
        {
            // y x^(y-1) dx, written so that x = 0 with y >= 1 gives nought rather than NaN.
            double p = y.Value == 1.0 ? 1.0 : Math.Pow(x.Value, y.Value - 1.0);
            return new Dual(v, y.Value * p * x.Deriv);
        }

        double d = v * (y.Deriv * Math.Log(x.Value) + y.Value * x.Deriv / x.Value);
        return new Dual(v, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Sin(Dual x) => new(Math.Sin(x.Value), Math.Cos(x.Value) * x.Deriv);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Cos(Dual x) => new(Math.Cos(x.Value), -Math.Sin(x.Value) * x.Deriv);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Tan(Dual x)
    {
        double t = Math.Tan(x.Value);
        return new Dual(t, (1.0 + t * t) * x.Deriv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Atan(Dual x) =>
        new(Math.Atan(x.Value), x.Deriv / (1.0 + x.Value * x.Value));

    /// <summary>
    /// The two-argument arctangent. <c>d/dt atan2(a, b) = (b a' - a b') / (a^2 + b^2)</c>, which
    /// is the ordinary quotient rule through <c>atan</c> and is continuous across the branch cut
    /// the VALUE jumps at - the derivative of an angle does not care which turn it is on.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Atan2(Dual a, Dual b)
    {
        double d = a.Value * a.Value + b.Value * b.Value;
        return new(Math.Atan2(a.Value, b.Value),
                   d == 0.0 ? 0.0 : (b.Value * a.Deriv - a.Value * b.Deriv) / d);
    }

    /// <summary>Rounding is piecewise constant, so its derivative is nought everywhere it has one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Round(Dual x) => new(Math.Round(x.Value), 0.0);

    /// <summary>Sign is an integer, as it is in <see cref="Math"/>, and carries no derivative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(Dual x) => Math.Sign(x.Value);

    // Min and Max select a branch, and the selected branch brings its derivative with it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Min(Dual a, Dual b) => a.Value <= b.Value ? a : b;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dual Max(Dual a, Dual b) => a.Value >= b.Value ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Min(int a, int b) => Math.Min(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Max(int a, int b) => Math.Max(a, b);

    /// <summary>
    /// Whether a quantity is nothing at all, and its term can be dropped.
    ///
    /// <para><b>A zero that is being varied is not nothing.</b> The chain skips terms whose
    /// coefficient is zero, which is sound arithmetic and a trap here. A PLANE surface whose
    /// curvature the optimiser is bending has a sag whose conic term is zero and whose derivative
    /// is <c>r^2/2</c>; drop the term and the sag is still right while the gradient is short -
    /// the worst kind of wrong, because the merit function stays correct and only the direction
    /// the search walks in goes astray.</para>
    ///
    /// <para>So a term is dropped only when it is absent in BOTH senses. A structural zero has no
    /// derivative either, so the sparse skips that make the chain fast keep working exactly as
    /// they did.</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Vanishes(Dual x) => x.Value == 0.0 && x.Deriv == 0.0;

    /// <summary>The same, for quantities compared against a tolerance rather than exactly.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Vanishes(Dual x, double tolerance) =>
        Math.Abs(x.Value) <= tolerance && x.Deriv == 0.0;
}
