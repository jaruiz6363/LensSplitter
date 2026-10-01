using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;

using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl's own check on the quaternary intrinsic coefficient, paper IV Eq. (2.12).
///
/// <para>He offers it in the text - "a fairly reliable check upon the correctness of (2.11) is
/// obtained by setting <c>i = -v</c>, which corresponds to a plane refracting surface" - and then
/// uses it himself. At such a surface Eq. M (18.6) requires</para>
/// <code>
///     q1p = (35/128) N (1 - k^2)^4 y v^9
/// </code>
///
/// <para><b>This is a better test than the Sigma1 table, and worth having beside it.</b> The table
/// is six significant figures computed on a desk machine, so it can only be asked for a few parts
/// in a million. This is a closed form: it holds to machine precision, it needs no published
/// number, no fixture lens and no tolerance argument, and it exercises the intrinsic coefficient
/// on its own rather than buried in a total that induced terms dominate.</para>
///
/// <para><b>What makes it bite.</b> At <c>c = 0</c> the scheme gives <c>t3 = -t2</c>, which is the
/// <c>i = -v</c> Buchdahl names, and then <c>t7</c> vanishes identically - so r5 and r6 drop out
/// and the intrinsic row collapses to <c>35 w^3 a_p</c>. The identity therefore pins the two
/// surviving factors, the <c>280/8</c> and the cube of <c>w</c>, against an independent
/// expression of them. A wrong coefficient on either would pass the Sigma1 table only by
/// coincidence, but this says so exactly.</para>
/// </summary>
public class QuaternaryPlaneSurfaceTests
{
    private readonly ITestOutputHelper _out;
    public QuaternaryPlaneSurfaceTests(ITestOutputHelper o) { _out = o; }

    /// <summary>
    /// A curved surface to give the ray an angle, then the PLANE refracting surface the check is
    /// about. The plane cannot be first: with the object at infinity the axial ray arrives
    /// parallel, <c>v = 0</c> there, and every term in the identity vanishes for a reason that
    /// has nothing to do with the arithmetic being right.
    /// </summary>
    private static (List<Surface> Surfaces, double[] Indices) PlaneAtSurfaceTwo(double curvature,
                                                                               double glass)
    {
        double[] c = { 0, curvature, 0.0, 0 };       // surface 2 is the plane
        double[] dBefore = { 0, 0, 0.06, 0.9 };
        double[] n = { 1.0, glass, 1.0, 1.0 };

        var s = new List<Surface>();
        for (int i = 0; i < c.Length; i++) s.Add(new Surface { Curvature = c[i] });
        for (int i = 0; i + 1 < c.Length; i++) s[i].Thickness = dBefore[i + 1];

        return (s, n);
    }

    /// <summary>
    /// The identity itself, over a range of glasses and powers so that a coincidence at one
    /// point cannot pass for agreement.
    /// </summary>
    [Theory]
    [InlineData(0.4, 1.5)]
    [InlineData(0.4, 1.9)]
    [InlineData(1.1, 1.5)]
    [InlineData(1.1, 1.75)]
    [InlineData(-0.7, 1.6162)]
    public void TheIntrinsicCoefficientObeysEquation212(double curvature, double glass)
    {
        var (surfaces, indices) = PlaneAtSurfaceTwo(curvature, glass);
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.1);
        var q = QuaternarySpherical.Compute(rows, surfaces.Count - 2);

        var t = rows[2].T;

        // The scheme's own quantities at the plane, so the test cannot drift from the code by
        // re-deriving them: y and v are t1 and t2, N is the index BEFORE the surface and k is
        // the ratio across it.
        double y = t[1], v = t[2];
        double n = indices[1], k = indices[1] / indices[2];

        // The condition Buchdahl names, checked rather than assumed. t3 IS c*y - v, so this is
        // a statement that the fixture really does put a plane there.
        Assert.Equal(-v, t[3], 12);

        double expected = (35.0 / 128.0) * n * Math.Pow(1.0 - k * k, 4) * y * Math.Pow(v, 9);
        double actual = q.Rows[2].Intrinsic;

        _out.WriteLine($"c={curvature} n={glass}:  Eq.(2.12) {expected:E10}  scheme {actual:E10}");

        Assert.Equal(expected, actual, 12);
        Assert.True(Math.Abs(expected) > 1e-18, "the identity was satisfied only by vanishing");
    }

    /// <summary>
    /// And the mechanism, asserted separately from the result. <c>t7</c> vanishing at a plane is
    /// what removes r5 and r6; if some future change made it merely SMALL rather than zero, the
    /// identity above would start to fail by an amount that looked like a tolerance problem, and
    /// this says which of the two it is.
    /// </summary>
    [Fact]
    public void AtAPlaneTheTwoBracketTermsVanish()
    {
        var (surfaces, indices) = PlaneAtSurfaceTwo(0.4, 1.5);
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.1);
        var q = QuaternarySpherical.Compute(rows, surfaces.Count - 2);

        Assert.Equal(0.0, rows[2].T[7], 14);
        Assert.Equal(0.0, q.Rows[2].R5, 14);
        Assert.Equal(0.0, q.Rows[2].R6, 14);

        // r4 does NOT vanish - it has no factor of t7 - so this is not a test that everything
        // is zero at a plane, which would pass whatever the rows said.
        Assert.True(Math.Abs(q.Rows[2].R4) > 1e-12, "r4 should survive at a plane surface");
    }

    /// <summary>
    /// The curved surface in the same system is NOT subject to the identity, which is the control:
    /// without it, an implementation that returned Eq. (2.12) unconditionally would pass.
    /// </summary>
    [Fact]
    public void TheCurvedSurfaceIsNotSubjectToTheIdentity()
    {
        var (surfaces, indices) = PlaneAtSurfaceTwo(0.4, 1.5);
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.1);
        var q = QuaternarySpherical.Compute(rows, surfaces.Count - 2);

        var t = rows[1].T;
        double k = indices[0] / indices[1];
        double formula = (35.0 / 128.0) * indices[0] * Math.Pow(1.0 - k * k, 4)
                       * t[1] * Math.Pow(t[2], 9);

        Assert.NotEqual(formula, q.Rows[1].Intrinsic, 12);
    }
}
