using System;

namespace AberrationCalculator.Core.SeriesArithmetic;

/// <summary>
/// The elementary functions of <see cref="LaurentSeries"/>, standing where <see cref="Math"/> stands
/// in the ordinary arithmetic.
///
/// <para>Only what the aberration chain can meet with a genuinely varying argument is carried
/// through as a series: absolute value, square root and power. The trigonometric functions appear
/// only on field angles and tilts, which do not depend on a surface's curvature; they are formed
/// on constants and return NaN for anything else, so a chain that ever did pass a varying argument
/// would produce an answer the caller refuses rather than a wrong one.</para>
/// </summary>
public static class LaurentMath
{
    public const double PI = Math.PI;

    /// <summary>Absolute value, decided at the evaluation point.</summary>
    public static LaurentSeries Abs(LaurentSeries x) => x.Value < 0.0 ? -x : x;

    /// <summary>
    /// Square root. The leading order must be even and its coefficient positive; the root of
    /// <c>e^(2m) (b0 + b1 e + ...)</c> is <c>e^m</c> times the binomial series of the bracket.
    /// </summary>
    public static LaurentSeries Sqrt(LaurentSeries x)
    {
        if (x.IsZero) return 0.0;
        if (x.IsConstant) return Math.Sqrt(x.Constant);

        int lo = x.LowestOrder;
        double b0 = x.Coefficient(lo);
        if (lo % 2 != 0 || b0 < 0.0) return double.NaN;

        int terms = LaurentSeries.MaxOrder - lo / 2 + 1;
        var s = new double[terms];
        s[0] = Math.Sqrt(b0);
        for (int n = 1; n < terms; n++)
        {
            double sum = x.Coefficient(lo + n);
            for (int j = 1; j < n; j++) sum -= s[j] * s[n - j];
            s[n] = sum / (2.0 * s[0]);
        }
        return Build(s, lo / 2);
    }

    /// <summary>
    /// Power, for a constant exponent - which it is at every site in the chain. An integral
    /// exponent is repeated multiplication; any other needs a base whose finite part is positive
    /// and nothing below it, and goes through the logarithm.
    /// </summary>
    public static LaurentSeries Pow(LaurentSeries x, LaurentSeries y)
    {
        if (!y.IsConstant) return double.NaN;
        double p = y.Constant;
        if (x.IsConstant) return Math.Pow(x.Constant, p);

        if (p == Math.Round(p) && Math.Abs(p) <= 64)
        {
            int n = (int)Math.Abs(p);
            LaurentSeries result = 1.0, square = x;
            while (n > 0)
            {
                if ((n & 1) != 0) result *= square;
                n >>= 1;
                if (n > 0) square *= square;
            }
            return p < 0 ? 1.0 / result : result;
        }

        if (x.LowestOrder != 0 || x.Constant <= 0.0) return double.NaN;
        return Exp(Log(x) * p);
    }

    private static LaurentSeries Build(double[] coefficients, int lowestOrder)
    {
        LaurentSeries e = LaurentSeries.Variable;
        LaurentSeries power = 1.0;
        if (lowestOrder >= 0) for (int k = 0; k < lowestOrder; k++) power *= e;
        else for (int k = 0; k < -lowestOrder; k++) power /= e;

        LaurentSeries sum = 0.0, term = 1.0;
        for (int k = 0; k < coefficients.Length; k++)
        {
            if (coefficients[k] != 0.0) sum += coefficients[k] * term;
            term *= e;
        }
        return sum * power;
    }

    /// <summary>log of a series with positive finite part and no negative orders.</summary>
    private static LaurentSeries Log(LaurentSeries x)
    {
        double a0 = x.Constant;
        LaurentSeries u = x / a0 - 1.0;              // positive orders only
        LaurentSeries sum = Math.Log(a0), power = u;
        for (int k = 1; k <= LaurentSeries.MaxOrder; k++)
        {
            sum += power * ((k % 2 == 1 ? 1.0 : -1.0) / k);
            power *= u;
            if (power.IsZero) break;
        }
        return sum;
    }

    /// <summary>exp of a series with no negative orders.</summary>
    private static LaurentSeries Exp(LaurentSeries x)
    {
        double a0 = x.Constant;
        LaurentSeries u = x - a0;
        LaurentSeries sum = 1.0, term = 1.0;
        for (int k = 1; k <= LaurentSeries.MaxOrder; k++)
        {
            term *= u / k;
            if (term.IsZero) break;
            sum += term;
        }
        return sum * Math.Exp(a0);
    }

    // Constant arguments only; see the class note.
    public static LaurentSeries Sin(LaurentSeries x) => x.IsConstant ? Math.Sin(x.Constant) : double.NaN;
    public static LaurentSeries Cos(LaurentSeries x) => x.IsConstant ? Math.Cos(x.Constant) : double.NaN;
    public static LaurentSeries Tan(LaurentSeries x) => x.IsConstant ? Math.Tan(x.Constant) : double.NaN;
    public static LaurentSeries Atan(LaurentSeries x) => x.IsConstant ? Math.Atan(x.Constant) : double.NaN;
    public static LaurentSeries Atan2(LaurentSeries a, LaurentSeries b) =>
        a.IsConstant && b.IsConstant ? Math.Atan2(a.Constant, b.Constant) : double.NaN;
    public static LaurentSeries Round(LaurentSeries x) => x.IsConstant ? Math.Round(x.Constant) : double.NaN;

    public static int Sign(LaurentSeries x) => Math.Sign(x.Value);
    public static LaurentSeries Min(LaurentSeries a, LaurentSeries b) => a.Value <= b.Value ? a : b;
    public static LaurentSeries Max(LaurentSeries a, LaurentSeries b) => a.Value >= b.Value ? a : b;
    public static int Min(int a, int b) => Math.Min(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);

    /// <summary>Whether a quantity is nothing at all - the zero series, coefficient by coefficient.</summary>
    public static bool Vanishes(LaurentSeries x) => x.IsZero;

    /// <summary>The same against a tolerance: a constant, and a small one.</summary>
    public static bool Vanishes(LaurentSeries x, double tolerance) =>
        x.IsConstant && Math.Abs(x.Constant) <= tolerance;
}
