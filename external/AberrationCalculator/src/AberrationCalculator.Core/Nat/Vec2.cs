using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The two-dimensional vector of nodal aberration theory, with Thompson's multiplication.
///
/// <para>NAT is written in a vector product that is complex multiplication in disguise, and the
/// disguise is the whole difficulty. <b>The angle is measured CLOCKWISE from the y axis</b>,
/// which makes <c>y</c> the real axis and <c>x</c> the imaginary one - the opposite of the
/// arrangement everyone reaches for. Every sign in the theory follows from that choice, and
/// getting it backwards produces node patterns that look plausible and sit ninety degrees from
/// where they belong.</para>
///
/// <para>So, for a vector of magnitude <c>m</c> at orientation <c>phi</c>,</para>
/// <code>
///     X = m sin(phi)        Y = m cos(phi)
/// </code>
/// <para>and the product of <c>a</c> and <c>b</c>, which must carry orientation
/// <c>phi_a + phi_b</c>, is</para>
/// <code>
///     (a b).Y = a.Y b.Y - a.X b.X
///     (a b).X = a.Y b.X + a.X b.Y
/// </code>
/// <para>with the conjugate negating the IMAGINARY part, which here is x:</para>
/// <code>
///     a* = -a.X xhat + a.Y yhat
/// </code>
/// <para>That last line is Fuerschbach 2014 Eq. (12) and 2012 Eq. (13) verbatim, and it is the
/// cheapest way to check an implementation of this file is the right way round.</para>
///
/// <para>Written in <c>Scalar</c> throughout so it can join the differentiated compile without
/// change; see <c>Numerics/ScalarAlias.cs</c>.</para>
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    /// <summary>Sagittal component. The IMAGINARY axis in this algebra.</summary>
    public readonly Scalar X;

    /// <summary>Meridional component. The REAL axis in this algebra.</summary>
    public readonly Scalar Y;

    public Vec2(Scalar x, Scalar y) { X = x; Y = y; }

    /// <summary>The zero vector: an aligned system's sigma, and the origin of the field.</summary>
    public static Vec2 Zero => new Vec2(0.0, 0.0);

    /// <summary>
    /// A vector from its magnitude and its orientation, the orientation measured clockwise
    /// from the y axis in the manner of NAT.
    /// </summary>
    public static Vec2 FromPolar(Scalar magnitude, Scalar orientation) =>
        new Vec2(magnitude * SMath.Sin(orientation), magnitude * SMath.Cos(orientation));

    /// <summary>Length of the vector.</summary>
    public Scalar Magnitude => SMath.Sqrt(X * X + Y * Y);

    /// <summary>Squared length, which avoids a root where only a comparison is wanted.</summary>
    public Scalar MagnitudeSquared => X * X + Y * Y;

    /// <summary>
    /// Orientation, clockwise from the y axis, in radians. Uses the two-argument arctangent so
    /// the quadrant survives; <c>Atan2(X, Y)</c> and not the other order, because y is real.
    /// </summary>
    public Scalar Orientation => SMath.Atan2(X, Y);

    /// <summary>
    /// The conjugate. <b>Negates X, not Y</b> - see the note on this type. This is the operation
    /// that produces the field-conjugate dependences that are unique to NAT, and the one most
    /// often implemented backwards.
    /// </summary>
    public Vec2 Conjugate => new Vec2(-X, Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

    public static Vec2 operator *(Scalar s, Vec2 a) => new Vec2(s * a.X, s * a.Y);

    public static Vec2 operator *(Vec2 a, Scalar s) => s * a;

    /// <summary>
    /// Thompson's vector multiplication: magnitudes multiply and orientations ADD.
    /// </summary>
    public static Vec2 operator *(Vec2 a, Vec2 b) =>
        new Vec2(a.Y * b.X + a.X * b.Y, a.Y * b.Y - a.X * b.X);

    /// <summary>The scalar product, which is the ordinary one.</summary>
    public static Scalar Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>The vector squared - the operation that turns astigmatism binodal.</summary>
    public Vec2 Squared => this * this;

    /// <summary>
    /// The square ROOT, taking the branch with orientation half the original's. Both roots are
    /// wanted wherever this is used - the two astigmatic nodes are <c>+/-</c> this - so the
    /// caller negates it for the other one.
    /// </summary>
    public Vec2 Sqrt() => FromPolar(SMath.Sqrt(Magnitude), 0.5 * Orientation);

    /// <summary>
    /// The principal cube ROOT, orientation divided by three. The other two roots are this one
    /// turned by 120 and 240 degrees, which is <see cref="TurnedByThird"/>.
    /// </summary>
    public Vec2 CubeRoot() =>
        FromPolar(SMath.Pow(Magnitude, 1.0 / 3.0), Orientation / 3.0);

    /// <summary>This vector turned through 120 degrees, <c>n</c> times.</summary>
    public Vec2 TurnedByThird(int n) =>
        FromPolar(Magnitude, Orientation + n * (2.0 * SMath.PI / 3.0));

    /// <summary>
    /// Multiplication by <c>i</c>: a quarter turn. Thompson's imaginary unit is the vector of
    /// unit magnitude at ninety degrees, and the trinodal solutions are written with it.
    /// </summary>
    public Vec2 TimesI => new Vec2(Y, -X);

    /// <summary>
    /// Division, which exists because the multiplication is the complex one: the identity is the
    /// unit vector along <c>y</c>, and <c>b * b.Conjugate</c> is <c>|b|^2</c> times it.
    /// </summary>
    public static Vec2 operator /(Vec2 a, Vec2 b)
    {
        Scalar m = b.MagnitudeSquared;
        if (m < 1e-300) return Zero;
        return (1.0 / m) * (a * b.Conjugate);
    }

    public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);

    public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();

    public override string ToString() =>
        "(" + X.ToString() + ", " + Y.ToString() + ")";
}
