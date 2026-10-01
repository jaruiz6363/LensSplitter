using System;

namespace AberrationCalculator.Core.Numerics;

/// <summary>
/// <see cref="Math"/> under the name the aberration chain calls it by.
///
/// <para>Every member here forwards straight to <see cref="Math"/> and is inlined away, so
/// this costs nothing and changes nothing. It exists so that the same source files compile
/// against the dual-number arithmetic in AberrationCalculator.Core.Ad, where <c>SMath</c>
/// resolves to the differentiating version instead. See <c>Numerics/ScalarAlias.cs</c>.</para>
///
/// <para>The integer overloads are here because the chain uses <c>Math.Min</c> and
/// <c>Math.Max</c> on surface indices as well as on ray heights, and an index is not a
/// quantity anything is differentiated with respect to.</para>
/// </summary>
public static class DoubleMath
{
    public const double PI = Math.PI;

    public static double Abs(double x) => Math.Abs(x);
    public static double Sqrt(double x) => Math.Sqrt(x);
    public static double Pow(double x, double y) => Math.Pow(x, y);
    public static double Sin(double x) => Math.Sin(x);
    public static double Cos(double x) => Math.Cos(x);
    public static double Tan(double x) => Math.Tan(x);
    public static double Atan(double x) => Math.Atan(x);

    /// <summary>
    /// The two-argument arctangent, <c>atan(a/b)</c> with the quadrant kept. Nodal aberration
    /// theory needs it because an orientation there runs over a full turn, not half of one, and
    /// the single-argument form would fold opposite vectors onto each other.
    /// </summary>
    public static double Atan2(double a, double b) => Math.Atan2(a, b);
    public static double Round(double x) => Math.Round(x);
    public static double Min(double a, double b) => Math.Min(a, b);
    public static double Max(double a, double b) => Math.Max(a, b);
    public static int Sign(double x) => Math.Sign(x);

    public static int Min(int a, int b) => Math.Min(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);

    /// <summary>
    /// Whether a quantity is nothing at all, and its term can be dropped.
    ///
    /// <para>In ordinary arithmetic this is exactly a comparison against zero, and every member
    /// here is inlined away to one. It is a named predicate rather than <c>== 0.0</c> spelled out
    /// because it is NOT the same question under differentiation, and the sites that use it are
    /// the ones where the difference bites.</para>
    ///
    /// <para>A plane surface is the case to have in mind. Its curvature is zero, so the conic
    /// term of its sag is skipped - which is right, the term is zero. But if that curvature is a
    /// VARIABLE, being bent by the optimiser, the term has value nothing and derivative
    /// <c>r^2/2</c>, and skipping it leaves the sag correct while the gradient goes silently
    /// short. The dual-number version of this method therefore asks about the derivative too. See
    /// <c>AberrationCalculator.Core.Ad.DualMath.Vanishes</c>.</para>
    /// </summary>
    public static bool Vanishes(double x) => x == 0.0;

    /// <summary>The same, for quantities compared against a tolerance rather than exactly.</summary>
    public static bool Vanishes(double x, double tolerance) => Math.Abs(x) <= tolerance;
}
