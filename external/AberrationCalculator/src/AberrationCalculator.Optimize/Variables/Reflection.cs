using System;

namespace AberrationCalculator.Optimize.Variables;

/// <summary>
/// Bounds enforced by reflection: a value that steps outside its interval is folded back in,
/// as light off a mirror.
///
/// <para>Two limits make the interval a resonator, and a value far outside bounces between them
/// until it lands inside - which is the triangle wave</para>
///
/// <code>
///     u = (x - lo) mod 2(hi - lo)          x' = lo + u        while u &lt;= hi - lo
///                                          x' = hi - (u - (hi - lo))   above it
/// </code>
///
/// <para>One limit reflects about itself alone, <c>x' = lo + |x - lo|</c>. No limits, and the
/// value passes through untouched.</para>
///
/// <para><b>Why this and not a sigmoid.</b> The usual alternative maps the bounded interval onto
/// an unbounded internal coordinate through a smooth squashing function, and optimises that.
/// It is differentiable everywhere, which is tidy, but its derivative goes to zero AT the
/// bound: a variable driven against a limit stops responding, and no amount of gradient will
/// bring it back when the design later wants it. Reflection has derivative of magnitude one
/// everywhere - the sign flips on each fold and nothing else changes - so a variable sitting on
/// a limit still feels the full force of the merit function. That matters most in exactly the
/// two places this optimiser lives: constrained descent, and a stochastic search that throws
/// large steps about on purpose.</para>
///
/// <para>The value stays in physical units throughout. Nothing here rescales anything, and the
/// Jacobian the optimiser sees is the derivative with respect to the real curvature or the real
/// thickness, not with respect to some internal surrogate.</para>
/// </summary>
public static class Reflection
{
    /// <summary>Folds <paramref name="x"/> into [<paramref name="lo"/>, <paramref name="hi"/>].</summary>
    public static double Fold(double x, double lo, double hi)
    {
        if (double.IsNaN(x)) return double.IsNegativeInfinity(lo) ? 0.0 : lo;

        bool hasLo = !double.IsNegativeInfinity(lo);
        bool hasHi = !double.IsPositiveInfinity(hi);

        if (!hasLo && !hasHi) return x;

        if (hasLo && hasHi)
        {
            if (hi <= lo) return lo;                 // a degenerate interval pins the variable
            if (x >= lo && x <= hi) return x;

            double span = hi - lo;
            double period = 2.0 * span;
            double u = (x - lo) % period;
            if (u < 0.0) u += period;
            return u <= span ? lo + u : hi - (u - span);
        }

        // A single limit reflects about itself: everything beyond it comes back the same
        // distance on the legal side.
        if (hasLo) return x >= lo ? x : lo + (lo - x);
        return x <= hi ? x : hi - (x - hi);
    }

    /// <summary>
    /// The sign the derivative picks up from the fold: +1 where the value passed through or
    /// came back the right way up, -1 where the reflection turned it over.
    ///
    /// <para>The optimiser does not need this while it keeps its variables inside their bounds,
    /// which <see cref="Fold"/> guarantees after every step - the point of recording it is that
    /// the magnitude is always one, whichever way the sign fell.</para>
    /// </summary>
    public static double FoldSign(double x, double lo, double hi)
    {
        bool hasLo = !double.IsNegativeInfinity(lo);
        bool hasHi = !double.IsPositiveInfinity(hi);
        if (!hasLo && !hasHi) return 1.0;

        if (hasLo && hasHi)
        {
            if (hi <= lo) return 0.0;
            double span = hi - lo, period = 2.0 * span;
            double u = (x - lo) % period;
            if (u < 0.0) u += period;
            return u <= span ? 1.0 : -1.0;
        }
        if (hasLo) return x >= lo ? 1.0 : -1.0;
        return x <= hi ? 1.0 : -1.0;
    }
}
