using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Forbes;

/// <summary>
/// A truncated power series in the three rotational invariants of a symmetric system,
///
/// <code>
///     p = y0 . y0 ,     k = y0 . b0 ,     u = b0 . b0
/// </code>
///
/// where <c>y0</c> is the ray's initial height and <c>b0</c> its initial direction, both
/// two-vectors. This is Forbes' reduction (JOSA 73, 782, Eq. 3.1): for a rotationally symmetric
/// system every quantity carried through a ray trace is either a scalar function of these three,
/// or a vector of the form <c>S y0 + T b0</c> with <c>S</c> and <c>T</c> such scalars. So the
/// whole trace reduces to arithmetic on this type.
///
/// <para><b>Why degree three is enough.</b> Each invariant is quadratic in the ray coordinates,
/// so a term of degree <c>m</c> here multiplying <c>y0</c> or <c>b0</c> is of order <c>2m + 1</c>
/// in them. Degree 1 is therefore the primary aberrations, degree 2 the secondary and degree 3
/// the tertiary - the order this program exists to compute. Forbes' order doubling, which gets
/// the characteristic function to order <c>2M</c> from a trace carried only to order <c>M</c>,
/// is an economy for orders far beyond that; at seventh order the direct trace needs twenty
/// coefficients per series and the economy is not worth its complexity.</para>
///
/// <para>Every series that arises in the trace has either zero constant term - <c>p_i</c>,
/// <c>k_i</c>, <c>u_i</c> all vanish with the ray - or a constant term of one, as the direction
/// cosine <c>a = (1 - u)^(1/2)</c> does. <see cref="Sqrt"/> and <see cref="Inverse"/> are written
/// for the second case and throw for a constant term of zero, which in this scheme means the
/// caller has made an error rather than met a singularity.</para>
/// </summary>
public sealed class InvariantSeries
{
    /// <summary>Highest total degree kept. Everything above it is discarded on every operation.</summary>
    public int Degree { get; }

    private readonly double[] _c;
    private readonly int[,,] _index;
    private readonly (int A, int B, int C)[] _powers;

    private InvariantSeries(int degree, int[,,] index, (int, int, int)[] powers, double[] c)
    {
        Degree = degree;
        _index = index;
        _powers = powers;
        _c = c;
    }

    /// <summary>Number of monomials of total degree at most <paramref name="degree"/> in three
    /// variables - twenty for the tertiary.</summary>
    public static int TermCount(int degree) => (degree + 1) * (degree + 2) * (degree + 3) / 6;

    // The monomial tables depend only on the degree, so they are built once and shared. They
    // are cached PER DEGREE rather than one at a time: a trace runs at degree D while the surface
    // figures are built at D + 1, so a single-slot cache is rebuilt twice per surface and the
    // trace spends most of its time laying out index tables. That cost is real - it made the
    // trace measure an order of magnitude slower than it is.
    private static readonly object Gate = new();
    private static readonly Dictionary<int, (int[,,] Index, (int, int, int)[] Powers)> Cache = new();

    private static (int[,,] Index, (int, int, int)[] Powers) Tables(int degree)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(degree, out var hit)) return hit;

            var index = new int[degree + 1, degree + 1, degree + 1];
            for (int a = 0; a <= degree; a++)
                for (int b = 0; b <= degree; b++)
                    for (int c = 0; c <= degree; c++)
                        index[a, b, c] = -1;

            var powers = new (int, int, int)[TermCount(degree)];
            int next = 0;
            for (int total = 0; total <= degree; total++)
                for (int a = total; a >= 0; a--)
                    for (int b = total - a; b >= 0; b--)
                    {
                        int c = total - a - b;
                        index[a, b, c] = next;
                        powers[next] = (a, b, c);
                        next++;
                    }

            var built = (index, powers);
            Cache[degree] = built;
            return built;
        }
    }

    /// <summary>The zero series.</summary>
    public static InvariantSeries Zero(int degree)
    {
        var (index, powers) = Tables(degree);
        return new InvariantSeries(degree, index, powers, new double[TermCount(degree)]);
    }

    /// <summary>A constant.</summary>
    public static InvariantSeries Constant(int degree, double value)
    {
        var s = Zero(degree);
        s._c[0] = value;
        return s;
    }

    /// <summary>The invariant <c>p = y0 . y0</c>.</summary>
    public static InvariantSeries P(int degree) => Unit(degree, 1, 0, 0);

    /// <summary>The invariant <c>k = y0 . b0</c>.</summary>
    public static InvariantSeries K(int degree) => Unit(degree, 0, 1, 0);

    /// <summary>The invariant <c>u = b0 . b0</c>.</summary>
    public static InvariantSeries U(int degree) => Unit(degree, 0, 0, 1);

    private static InvariantSeries Unit(int degree, int a, int b, int c)
    {
        var s = Zero(degree);
        if (a + b + c <= degree) s._c[s._index[a, b, c]] = 1.0;
        return s;
    }

    /// <summary>The coefficient of <c>p^a k^b u^c</c>, or zero if that monomial is past the
    /// truncation.</summary>
    public double this[int a, int b, int c]
    {
        get
        {
            if (a < 0 || b < 0 || c < 0 || a + b + c > Degree) return 0.0;
            return _c[_index[a, b, c]];
        }
    }

    /// <summary>The constant term.</summary>
    public double ConstantTerm => _c[0];

    /// <summary>True when every coefficient of total degree <paramref name="degree"/> or less
    /// is zero - used by the tests to state that a term is absent rather than small.</summary>
    public bool IsZero(double tolerance = 0.0)
    {
        foreach (double v in _c) if (Math.Abs(v) > tolerance) return false;
        return true;
    }

    private InvariantSeries Like() => new(Degree, _index, _powers, new double[_c.Length]);

    private void RequireSameShape(InvariantSeries other)
    {
        if (other.Degree != Degree)
            throw new ArgumentException(
                $"series truncated at degree {other.Degree} combined with one at degree {Degree}; " +
                "mixing truncations silently loses terms, so it is refused");
    }

    public static InvariantSeries operator +(InvariantSeries x, InvariantSeries y)
    {
        x.RequireSameShape(y);
        var r = x.Like();
        for (int i = 0; i < r._c.Length; i++) r._c[i] = x._c[i] + y._c[i];
        return r;
    }

    public static InvariantSeries operator -(InvariantSeries x, InvariantSeries y)
    {
        x.RequireSameShape(y);
        var r = x.Like();
        for (int i = 0; i < r._c.Length; i++) r._c[i] = x._c[i] - y._c[i];
        return r;
    }

    public static InvariantSeries operator -(InvariantSeries x)
    {
        var r = x.Like();
        for (int i = 0; i < r._c.Length; i++) r._c[i] = -x._c[i];
        return r;
    }

    public static InvariantSeries operator *(InvariantSeries x, double s)
    {
        var r = x.Like();
        for (int i = 0; i < r._c.Length; i++) r._c[i] = x._c[i] * s;
        return r;
    }

    public static InvariantSeries operator *(double s, InvariantSeries x) => x * s;

    public static InvariantSeries operator +(InvariantSeries x, double s)
    {
        var r = x.Like();
        Array.Copy(x._c, r._c, x._c.Length);
        r._c[0] += s;
        return r;
    }

    public static InvariantSeries operator +(double s, InvariantSeries x) => x + s;

    public static InvariantSeries operator -(InvariantSeries x, double s) => x + (-s);

    public static InvariantSeries operator -(double s, InvariantSeries x) => (-x) + s;

    /// <summary>Truncated product. Any monomial whose total degree exceeds the truncation is
    /// dropped, which is what makes the trace finite.</summary>
    public static InvariantSeries operator *(InvariantSeries x, InvariantSeries y)
    {
        x.RequireSameShape(y);
        var r = x.Like();
        for (int i = 0; i < x._c.Length; i++)
        {
            double xi = x._c[i];
            if (xi == 0.0) continue;
            var (ax, bx, cx) = x._powers[i];
            int room = x.Degree - (ax + bx + cx);
            for (int j = 0; j < y._c.Length; j++)
            {
                double yj = y._c[j];
                if (yj == 0.0) continue;
                var (ay, by, cy) = y._powers[j];
                if (ay + by + cy > room) continue;
                r._c[r._index[ax + ay, bx + by, cx + cy]] += xi * yj;
            }
        }
        return r;
    }

    /// <summary>Non-negative integer power, by repeated multiplication.</summary>
    public InvariantSeries Pow(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "use Inverse for negative powers");
        var r = Constant(Degree, 1.0);
        for (int i = 0; i < n; i++) r *= this;
        return r;
    }

    /// <summary>
    /// <c>1 / this</c>, for a series whose constant term is not zero. Writing
    /// <c>this = c (1 + v)</c> with <c>v</c> of zero constant term, the geometric series in
    /// <c>v</c> terminates at the truncation, so the result is exact for the degree kept.
    /// </summary>
    public InvariantSeries Inverse()
    {
        double c0 = ConstantTerm;
        if (c0 == 0.0)
            throw new DivideByZeroException(
                "InvariantSeries.Inverse on a series with no constant term. In a ray trace every " +
                "quantity inverted has a constant term of one, so this means a quantity has been " +
                "built wrongly, not that a singularity has been met.");

        var v = (this * (1.0 / c0)) - 1.0;
        var term = Constant(Degree, 1.0);
        var sum = Constant(Degree, 1.0);
        for (int n = 1; n <= Degree; n++)
        {
            term *= -v;
            sum += term;
        }
        return sum * (1.0 / c0);
    }

    /// <summary>
    /// The positive square root, for a series with a positive constant term. Same reduction as
    /// <see cref="Inverse"/>, with the binomial series for <c>(1 + v)^(1/2)</c>.
    /// </summary>
    public InvariantSeries Sqrt()
    {
        double c0 = ConstantTerm;
        if (c0 <= 0.0)
            throw new ArgumentException(
                $"InvariantSeries.Sqrt on a series whose constant term is {c0}. The only square " +
                "roots a symmetric trace takes are of quantities that are one on axis.");

        var v = (this * (1.0 / c0)) - 1.0;
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

    /// <summary>
    /// Substitute this series - which must have no constant term - into the univariate power
    /// series <c>g(t) = sum g[j] t^j</c>. This is how a surface figure enters the trace: Forbes
    /// writes the <c>i</c>th surface as <c>x = f_i(y . y)</c> with <c>f_i</c> a power series, and
    /// <c>y . y</c> is itself a series in the invariants. A conic, an even asphere and a sphere
    /// differ only in the coefficients of <c>g</c>, which is the whole reason this route has no
    /// separate aspheric machinery.
    /// </summary>
    public InvariantSeries Compose(IReadOnlyList<double> g)
    {
        if (ConstantTerm != 0.0)
            throw new ArgumentException(
                "Compose expects a series with no constant term; the argument of a surface " +
                "figure vanishes on axis by construction.");

        var sum = Constant(Degree, g.Count > 0 ? g[0] : 0.0);
        var power = Constant(Degree, 1.0);
        for (int j = 1; j < g.Count && j <= Degree; j++)
        {
            power *= this;
            if (g[j] != 0.0) sum += power * g[j];
        }
        return sum;
    }

    /// <summary>Coefficients in the fixed monomial order, for tests and diagnostics.</summary>
    public double[] Coefficients() => (double[])_c.Clone();

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _c.Length; i++)
        {
            if (_c[i] == 0.0) continue;
            var (a, b, c) = _powers[i];
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(_c[i].ToString("G6"));
            if (a > 0) sb.Append(" p").Append(a > 1 ? "^" + a : "");
            if (b > 0) sb.Append(" k").Append(b > 1 ? "^" + b : "");
            if (c > 0) sb.Append(" u").Append(c > 1 ? "^" + c : "");
        }
        return sb.Length == 0 ? "0" : sb.ToString();
    }
}
