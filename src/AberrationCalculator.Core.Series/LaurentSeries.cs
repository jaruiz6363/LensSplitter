using System;
using System.Globalization;
using System.Text;

namespace AberrationCalculator.Core.SeriesArithmetic;

/// <summary>
/// A truncated Laurent series in one small parameter e: <c>sum c_k e^k</c> for k from
/// <see cref="MinOrder"/> to <see cref="MaxOrder"/>.
///
/// <para><b>What it is for.</b> Some surfaces make the aberration chain divide by a quantity that
/// vanishes identically - a figured flat facing collimated light has zero marginal incidence, so
/// its incidence ratio q is infinite - while every coefficient the chain finally reports is
/// perfectly finite. Seeding the vanishing quantity with e turns each division by it into an
/// explicit negative power, the chain's ordinary, validated formulas run unchanged, and the finite
/// answer is the coefficient of e^0. The negative powers cancel between terms, coefficient by
/// coefficient, instead of between large floating-point numbers, so nothing is lost to
/// cancellation and no limit is taken numerically.</para>
///
/// <para><b>Arithmetic is exact to truncation.</b> Sums and products are formed coefficient by
/// coefficient. A term that would fall below <see cref="MinOrder"/> cannot be represented and is
/// counted in <see cref="Underflows"/>; a caller that finds any has no answer. Terms above
/// <see cref="MaxOrder"/> are dropped silently, which can reach the e^0 coefficient only through a
/// later product with a deep negative power - so a caller confirms convergence by running at two
/// truncations and comparing.</para>
///
/// <para><b>Branches are decided at a real point.</b> The chain compares numbers to choose a
/// branch - is this incidence zero, is this ratio finite. Those comparisons evaluate the series at
/// <see cref="EvaluationPoint"/>, a small real e, so the chain takes the branches a real surface of
/// that small curvature takes: the general ones. Equality and <see cref="LaurentMath.Vanishes(LaurentSeries)"/>
/// are coefficient-wise instead, so a structural zero is still skipped as the chain intends.</para>
///
/// <para>The default value is the zero series, so a freshly allocated <c>Scalar[]</c> is zeros as
/// it is in double arithmetic.</para>
/// </summary>
public readonly struct LaurentSeries : IEquatable<LaurentSeries>, IComparable<LaurentSeries>, IFormattable
{
    [ThreadStatic] private static int _minOrder, _maxOrder;
    [ThreadStatic] private static double _evaluationPoint;
    [ThreadStatic] private static int _underflows;
    [ThreadStatic] private static double _worstDroppedLeading;

    /// <summary>Lowest order carried, on this thread. Default -24.</summary>
    public static int MinOrder
    {
        get => _minOrder == 0 && _maxOrder == 0 ? -24 : _minOrder;
        set { _minOrder = value; if (_maxOrder == 0) _maxOrder = 24; }
    }

    /// <summary>Highest order carried, on this thread. Default 24.</summary>
    public static int MaxOrder
    {
        get => _minOrder == 0 && _maxOrder == 0 ? 24 : _maxOrder;
        set { _maxOrder = value; if (_minOrder == 0) _minOrder = -24; }
    }

    /// <summary>The real e at which comparisons are decided, on this thread. Default 1E-3.</summary>
    public static double EvaluationPoint
    {
        get => _evaluationPoint == 0.0 ? 1e-3 : _evaluationPoint;
        set => _evaluationPoint = value;
    }

    /// <summary>How many terms have fallen below <see cref="MinOrder"/> on this thread.</summary>
    public static int Underflows => _underflows;

    /// <summary>
    /// The largest leading coefficient, relative to its series, that a division has treated as
    /// rounding noise and stepped past. Anything but a tiny number means a division is not to be
    /// trusted.
    /// </summary>
    public static double WorstDroppedLeading => _worstDroppedLeading;

    /// <summary>Clears <see cref="Underflows"/> and <see cref="WorstDroppedLeading"/>.</summary>
    public static void ResetDiagnostics() { _underflows = 0; _worstDroppedLeading = 0.0; }

    private readonly double[]? _c;
    private readonly int _lo;

    private LaurentSeries(double[] coefficients, int lowestOrder)
    {
        // Trim exact zeros at both ends so constants stay one coefficient long.
        int first = 0, last = coefficients.Length - 1;
        while (first <= last && coefficients[first] == 0.0) first++;
        while (last >= first && coefficients[last] == 0.0) last--;
        int lo = lowestOrder + first;

        // Clip to the carried window.
        int min = MinOrder, max = MaxOrder;
        if (lo < min)
        {
            for (int k = first; k <= last && lowestOrder + k < min; k++)
                if (coefficients[k] != 0.0) _underflows++;
            first += min - lo;
            lo = min;
        }
        if (lowestOrder + last > max) last = max - lowestOrder;
        while (first <= last && coefficients[first] == 0.0) { first++; lo++; }
        while (last >= first && coefficients[last] == 0.0) last--;

        if (first > last) { _c = null; _lo = 0; return; }
        var c = new double[last - first + 1];
        Array.Copy(coefficients, first, c, 0, c.Length);
        _c = c;
        _lo = lo;
    }

    /// <summary>The series variable itself: 1 e^1.</summary>
    public static LaurentSeries Variable => new(new[] { 1.0 }, 1);

    /// <summary>A value and a multiple of the variable: <c>value + slope e</c>.</summary>
    public static LaurentSeries Linear(double value, double slope) => new(new[] { value, slope }, 0);

    /// <summary>A constant of the problem.</summary>
    public static implicit operator LaurentSeries(double value) =>
        value == 0.0 ? default : new(new[] { value }, 0);

    /// <summary>Coefficient of e^order; zero outside what is carried.</summary>
    public double Coefficient(int order)
    {
        if (_c == null) return 0.0;
        int k = order - _lo;
        return k >= 0 && k < _c.Length ? _c[k] : 0.0;
    }

    /// <summary>The finite part: the coefficient of e^0.</summary>
    public double Constant => Coefficient(0);

    /// <summary>Lowest order with a non-zero coefficient; zero for the zero series.</summary>
    public int LowestOrder => _c == null ? 0 : _lo;

    /// <summary>Highest order with a non-zero coefficient; zero for the zero series.</summary>
    public int HighestOrder => _c == null ? 0 : _lo + _c.Length - 1;

    /// <summary>True for the zero series.</summary>
    public bool IsZero => _c == null;

    /// <summary>True when only e^0 is present, or nothing is.</summary>
    public bool IsConstant => _c == null || (_lo == 0 && _c.Length == 1);

    /// <summary>The series evaluated at <see cref="EvaluationPoint"/>: what branches are decided on.</summary>
    public double Value => Evaluate(EvaluationPoint);

    /// <summary>The series evaluated at a given e.</summary>
    public double Evaluate(double e)
    {
        if (_c == null) return 0.0;
        double sum = 0.0;
        for (int k = 0; k < _c.Length; k++) sum += _c[k] * Math.Pow(e, _lo + k);
        return sum;
    }

    public static LaurentSeries operator +(LaurentSeries a, LaurentSeries b)
    {
        if (a._c == null) return b;
        if (b._c == null) return a;
        int lo = Math.Min(a._lo, b._lo);
        int hi = Math.Max(a._lo + a._c.Length, b._lo + b._c.Length);
        var c = new double[hi - lo];
        for (int k = 0; k < a._c.Length; k++) c[a._lo - lo + k] += a._c[k];
        for (int k = 0; k < b._c.Length; k++) c[b._lo - lo + k] += b._c[k];
        return new(c, lo);
    }

    public static LaurentSeries operator -(LaurentSeries a) => a._c == null ? a : Scale(a, -1.0);
    public static LaurentSeries operator +(LaurentSeries a) => a;
    public static LaurentSeries operator -(LaurentSeries a, LaurentSeries b) => a + (-b);

    private static LaurentSeries Scale(LaurentSeries a, double s)
    {
        if (a._c == null) return s == 0.0 || !double.IsNaN(s) ? a : (LaurentSeries)double.NaN;
        var c = new double[a._c.Length];
        for (int k = 0; k < c.Length; k++) c[k] = a._c[k] * s;
        return new(c, a._lo);
    }

    public static LaurentSeries operator *(LaurentSeries a, LaurentSeries b)
    {
        if (a._c == null || b._c == null) return default;
        if (b.IsConstant) return Scale(a, b._c[0]);
        if (a.IsConstant) return Scale(b, a._c[0]);

        int lo = a._lo + b._lo;
        int length = a._c.Length + b._c.Length - 1;
        var c = new double[length];
        int max = MaxOrder;
        for (int i = 0; i < a._c.Length; i++)
        {
            double ai = a._c[i];
            if (ai == 0.0) continue;
            int limit = Math.Min(b._c.Length, max - lo - i + 1);
            for (int j = 0; j < limit; j++) c[i + j] += ai * b._c[j];
        }
        return new(c, lo);
    }

    public static LaurentSeries operator /(LaurentSeries a, LaurentSeries b)
    {
        if (b._c == null)
            return a._c == null ? (LaurentSeries)double.NaN
                                : (LaurentSeries)(a.Value >= 0 ? double.PositiveInfinity
                                                               : double.NegativeInfinity);
        if (b.IsConstant) return Scale(a, 1.0 / b._c[0]);
        if (a._c == null) return default;

        // Leading coefficient of the divisor. A leading term that is rounding noise from a
        // cancellation would make the quotient enormous and wrong, so such terms are stepped past
        // and the worst one recorded. "Noise" is judged by each term's size AT the evaluation
        // point, |b_k| e0^k, and not by its bare coefficient: the variable is a curvature, the
        // coefficients grow with order like a length to that power, and against a bare order-20
        // coefficient of 1E40 a perfectly real leading term looks like nothing.
        double e0 = EvaluationPoint;
        double biggest = 0.0;
        for (int k = 0; k < b._c.Length; k++)
            biggest = Math.Max(biggest, Math.Abs(b._c[k]) * Math.Pow(e0, b._lo + k));
        int lead = 0;
        while (lead < b._c.Length - 1
               && Math.Abs(b._c[lead]) * Math.Pow(e0, b._lo + lead) <= 1e-13 * biggest)
        {
            if (b._c[lead] != 0.0)
                _worstDroppedLeading = Math.Max(_worstDroppedLeading,
                    Math.Abs(b._c[lead]) * Math.Pow(e0, b._lo + lead) / biggest);
            lead++;
        }

        int m = b._lo + lead;
        int n = b._c.Length - lead;
        int terms = MaxOrder - MinOrder + 1;
        var inv = new double[terms];
        double b0 = b._c[lead];
        inv[0] = 1.0 / b0;
        for (int k = 1; k < terms; k++)
        {
            double s = 0.0;
            for (int j = 1; j <= k && j < n; j++) s += b._c[lead + j] * inv[k - j];
            inv[k] = -s / b0;
        }
        return a * new LaurentSeries(inv, -m);
    }

    // Branches are decided at the evaluation point; equality is coefficient-wise.
    public static bool operator <(LaurentSeries a, LaurentSeries b) => a.Value < b.Value;
    public static bool operator >(LaurentSeries a, LaurentSeries b) => a.Value > b.Value;
    public static bool operator <=(LaurentSeries a, LaurentSeries b) => a.Value <= b.Value;
    public static bool operator >=(LaurentSeries a, LaurentSeries b) => a.Value >= b.Value;
    public static bool operator ==(LaurentSeries a, LaurentSeries b) => a.Equals(b);
    public static bool operator !=(LaurentSeries a, LaurentSeries b) => !a.Equals(b);

    public bool Equals(LaurentSeries other)
    {
        if (_c == null || other._c == null) return _c == null && other._c == null;
        if (_lo != other._lo || _c.Length != other._c.Length) return false;
        for (int k = 0; k < _c.Length; k++) if (!_c[k].Equals(other._c[k])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is LaurentSeries s && Equals(s);
    public override int GetHashCode() => Constant.GetHashCode();
    public int CompareTo(LaurentSeries other) => Value.CompareTo(other.Value);

    // The `double.` statics the chain uses, under the names it uses them by.
    public static readonly LaurentSeries PositiveInfinity = double.PositiveInfinity;
    public static readonly LaurentSeries NegativeInfinity = double.NegativeInfinity;
    public static readonly LaurentSeries NaN = double.NaN;

    public static bool IsInfinity(LaurentSeries s) => s.Any(double.IsInfinity);
    public static bool IsNaN(LaurentSeries s) => s.Any(double.IsNaN);
    public static bool IsPositiveInfinity(LaurentSeries s) => IsInfinity(s) && s.Value > 0;
    public static bool IsNegativeInfinity(LaurentSeries s) => IsInfinity(s) && s.Value < 0;

    private bool Any(Func<double, bool> test)
    {
        if (_c == null) return false;
        foreach (double v in _c) if (test(v)) return true;
        return false;
    }

    public override string ToString()
    {
        if (_c == null) return "0";
        var sb = new StringBuilder();
        for (int k = 0; k < _c.Length; k++)
        {
            if (_c[k] == 0.0) continue;
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(_c[k].ToString("G6", CultureInfo.InvariantCulture)).Append(" e^").Append(_lo + k);
        }
        return sb.ToString();
    }

    // A formatted series prints its finite part alone, as a formatted dual prints its value.
    public string ToString(string? format) => Constant.ToString(format, CultureInfo.InvariantCulture);
    public string ToString(string? format, IFormatProvider? provider) => Constant.ToString(format, provider);
}
