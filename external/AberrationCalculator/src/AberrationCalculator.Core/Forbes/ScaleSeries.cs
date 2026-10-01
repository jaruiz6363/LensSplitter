using System;

namespace AberrationCalculator.Core.Forbes;

/// <summary>
/// A truncated power series in the RAY SCALE - the single parameter <c>s</c> by which the
/// inversion shrinks a ray, sending pupil <c>rho -> s rho</c> and field <c>h -> s h</c> together.
///
/// <para><b>Why this exists.</b> The coefficients are defined as the homogeneous degree-seven
/// part of the transverse aberration in the normalised pupil and field, which is exactly the
/// <c>s^7</c> term of a ray shrunk this way. The real-ray route recovers it by tracing a ladder of
/// scales and fitting; a series trace can write it down. But it cannot simply be read off the
/// degree-three part of <c>S</c> and <c>T</c>, because the ray a shape defines is not linear in
/// <c>s</c>: the tangent of the field angle is, and the direction cosine
/// <c>sin = tan / sqrt(1 + tan^2)</c> is not. So the <c>s^7</c> term picks up lower-degree parts
/// of <c>S</c> and <c>T</c> multiplied by the cubic and quintic corrections to that cosine, and
/// dropping them would be a quiet error of order the field squared. Carrying the whole
/// construction as a series in <c>s</c> and reading the seventh coefficient takes all of it
/// exactly, with no ladder and no fit.</para>
/// </summary>
public sealed class ScaleSeries
{
    private readonly double[] _c;

    /// <summary>Highest power of the scale kept.</summary>
    public int Degree => _c.Length - 1;

    private ScaleSeries(double[] c) { _c = c; }

    /// <summary>The zero series.</summary>
    public static ScaleSeries Zero(int degree) => new(new double[degree + 1]);

    /// <summary>A constant.</summary>
    public static ScaleSeries Constant(int degree, double value)
    {
        var s = Zero(degree);
        s._c[0] = value;
        return s;
    }

    /// <summary>The scale itself, <c>s</c>.</summary>
    public static ScaleSeries S(int degree)
    {
        var s = Zero(degree);
        if (degree >= 1) s._c[1] = 1.0;
        return s;
    }

    /// <summary>The coefficient of <c>s^n</c>, or zero beyond the truncation.</summary>
    public double this[int n] => n < 0 || n > Degree ? 0.0 : _c[n];

    private void RequireSameShape(ScaleSeries other)
    {
        if (other.Degree != Degree)
            throw new ArgumentException(
                $"scale series truncated at {other.Degree} combined with one at {Degree}");
    }

    public static ScaleSeries operator +(ScaleSeries x, ScaleSeries y)
    {
        x.RequireSameShape(y);
        var r = Zero(x.Degree);
        for (int i = 0; i <= x.Degree; i++) r._c[i] = x._c[i] + y._c[i];
        return r;
    }

    public static ScaleSeries operator -(ScaleSeries x, ScaleSeries y)
    {
        x.RequireSameShape(y);
        var r = Zero(x.Degree);
        for (int i = 0; i <= x.Degree; i++) r._c[i] = x._c[i] - y._c[i];
        return r;
    }

    public static ScaleSeries operator *(ScaleSeries x, double v)
    {
        var r = Zero(x.Degree);
        for (int i = 0; i <= x.Degree; i++) r._c[i] = x._c[i] * v;
        return r;
    }

    public static ScaleSeries operator *(double v, ScaleSeries x) => x * v;

    public static ScaleSeries operator +(ScaleSeries x, double v)
    {
        var r = Zero(x.Degree);
        Array.Copy(x._c, r._c, x._c.Length);
        r._c[0] += v;
        return r;
    }

    public static ScaleSeries operator +(double v, ScaleSeries x) => x + v;

    /// <summary>Truncated product.</summary>
    public static ScaleSeries operator *(ScaleSeries x, ScaleSeries y)
    {
        x.RequireSameShape(y);
        var r = Zero(x.Degree);
        for (int i = 0; i <= x.Degree; i++)
        {
            double xi = x._c[i];
            if (xi == 0.0) continue;
            for (int j = 0; i + j <= x.Degree; j++)
            {
                double yj = y._c[j];
                if (yj != 0.0) r._c[i + j] += xi * yj;
            }
        }
        return r;
    }

    /// <summary>Non-negative integer power.</summary>
    public ScaleSeries Pow(int n)
    {
        var r = Constant(Degree, 1.0);
        for (int i = 0; i < n; i++) r *= this;
        return r;
    }

    /// <summary><c>1 / this</c>, for a series with a non-zero constant term.</summary>
    public ScaleSeries Inverse()
    {
        double c0 = _c[0];
        if (c0 == 0.0)
            throw new DivideByZeroException(
                "ScaleSeries.Inverse on a series with no constant term; everything inverted in " +
                "this construction is one on axis.");

        var v = (this * (1.0 / c0)) - Constant(Degree, 1.0);
        var term = Constant(Degree, 1.0);
        var sum = Constant(Degree, 1.0);
        for (int n = 1; n <= Degree; n++)
        {
            term *= v * -1.0;
            sum += term;
        }
        return sum * (1.0 / c0);
    }

    /// <summary>The positive square root, for a series with a positive constant term.</summary>
    public ScaleSeries Sqrt()
    {
        double c0 = _c[0];
        if (c0 <= 0.0)
            throw new ArgumentException(
                $"ScaleSeries.Sqrt on a series whose constant term is {c0}");

        var v = (this * (1.0 / c0)) - Constant(Degree, 1.0);
        var term = Constant(Degree, 1.0);
        var sum = Constant(Degree, 1.0);
        double binom = 1.0;
        for (int n = 1; n <= Degree; n++)
        {
            binom *= (0.5 - (n - 1)) / n;
            term *= v;
            sum += term * binom;
        }
        return sum * Math.Sqrt(c0);
    }
}
