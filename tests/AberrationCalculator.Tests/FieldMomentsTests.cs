using System;
using AberrationCalculator.Core.Nat;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Thompson's aberration field vectors, 2010 Appendix A.
///
/// <para>These are the moments of the per-surface sigma vectors weighted by each surface's
/// contribution, and they are what turns a set of rotationally symmetric surface contributions
/// into a nodal field. The tests below are exact algebra rather than published numbers, because
/// the definitions are exact algebra - but they are not vacuous: each one asserts a physical
/// statement that a wrong sign or a confused product would break.</para>
/// </summary>
public class FieldMomentsTests
{
    /// <summary>
    /// <b>A uniformly displaced system has no nodal splitting.</b> If every surface carries the
    /// same sigma, the system is just the same system about a shifted axis: the whole field moves
    /// and nothing splits. So the field centre must come out equal to that sigma and every
    /// normalised moment about it must vanish identically.
    ///
    /// <para>This is the sharpest available check on the normalisations. Each of b, b2, c and c3
    /// subtracts a different power of the field centre, and getting any one of those subtractions
    /// wrong - the dot product where the vector square belongs, or the wrong power - leaves a
    /// residue here while leaving the unnormalised moments untouched.</para>
    /// </summary>
    [Theory]
    [InlineData(0.003, -0.0017)]
    [InlineData(-0.02, 0.0)]
    [InlineData(0.0, 0.011)]
    [InlineData(1.5, -2.25)]
    public void AUniformSigmaShiftsTheFieldAndSplitsNothing(double sx, double sy)
    {
        var s = new Vec2(sx, sy);
        double[] w = { 3.1, -0.7, 12.0, 0.02, -5.5 };

        var m = FieldMoments.Accumulate(j => w[j], _ => s, w.Length);

        Assert.Equal(sx, (double)m.a.X, 12);
        Assert.Equal(sy, (double)m.a.Y, 12);

        Assert.Equal(0.0, (double)m.b, 12);
        Assert.Equal(0.0, (double)m.b2.X, 12);
        Assert.Equal(0.0, (double)m.b2.Y, 12);
        Assert.Equal(0.0, (double)m.c.X, 12);
        Assert.Equal(0.0, (double)m.c.Y, 12);
        Assert.Equal(0.0, (double)m.c3.X, 12);
        Assert.Equal(0.0, (double)m.c3.Y, 12);
    }

    /// <summary>
    /// An aligned system has every sigma zero, so every moment is zero and the coefficient is
    /// left as the rotationally symmetric theory has it. The nodes degenerate to the axis, which
    /// is Thompson's own statement that the symmetric theory is the special case of his.
    /// </summary>
    [Fact]
    public void AnAlignedSystemHasNoFieldVectorsAtAll()
    {
        double[] w = { 3.1, -0.7, 12.0 };
        var m = FieldMoments.Accumulate(j => w[j], _ => Vec2.Zero, w.Length);

        Assert.Equal(3.1 - 0.7 + 12.0, (double)m.W, 12);
        Assert.Equal(0.0, (double)m.A.X, 12);
        Assert.Equal(0.0, (double)m.A.Y, 12);
        Assert.Equal(0.0, (double)m.B, 12);
        Assert.Equal(0.0, (double)m.B2.Y, 12);
        Assert.Equal(0.0, (double)m.C.Y, 12);
        Assert.Equal(0.0, (double)m.C3.Y, 12);
    }

    /// <summary>
    /// Two equal surfaces displaced oppositely. The field centre is back on axis, and the
    /// aberration is nonetheless not symmetric: the SCALAR second moment survives and so does the
    /// VECTOR one, while both third moments cancel.
    ///
    /// <para>This is the case that separates <c>B</c> from <c>B^2</c>. With sigma = (0, +s) and
    /// (0, -s) the dot products add to <c>2 s^2</c> while the vector squares also add - both
    /// survive - but the vector CUBES cancel against each other where the mixed third moment
    /// cancels too. A version that had used the dot product where the vector square belongs would
    /// still put the centre on axis and would still show a surviving second moment, and would
    /// differ only here.</para>
    /// </summary>
    [Fact]
    public void OppositeDisplacementsLeaveTheSecondMomentsAndCancelTheThird()
    {
        const double s = 0.25;
        var sig = new[] { new Vec2(0.0, s), new Vec2(0.0, -s) };
        double[] w = { 1.0, 1.0 };

        var m = FieldMoments.Accumulate(j => w[j], j => sig[j], 2);

        // The centre is on axis: the two displacements are equal and opposite.
        Assert.Equal(0.0, (double)m.a.X, 12);
        Assert.Equal(0.0, (double)m.a.Y, 12);

        // The scalar second moment is the mean square displacement, and it does not cancel.
        Assert.Equal(s * s, (double)m.b, 12);

        // Neither does the vector one: squaring (0, +s) and (0, -s) gives (0, s^2) both times.
        Assert.Equal(0.0, (double)m.b2.X, 12);
        Assert.Equal(s * s, (double)m.b2.Y, 12);

        // Both third moments cancel, being odd in sigma.
        Assert.Equal(0.0, (double)m.c.X, 12);
        Assert.Equal(0.0, (double)m.c.Y, 12);
        Assert.Equal(0.0, (double)m.c3.X, 12);
        Assert.Equal(0.0, (double)m.c3.Y, 12);
    }

    /// <summary>
    /// One surface carrying all the weight puts the field centre exactly at its own sigma, with
    /// nothing split - a single tilted surface in an otherwise perfect system has a displaced
    /// but unsplit field for that aberration type.
    /// </summary>
    [Fact]
    public void ASingleContributingSurfacePutsTheCentreAtItsOwnSigma()
    {
        var sig = new[] { new Vec2(0.01, -0.02), new Vec2(0.5, 0.5), new Vec2(-3.0, 1.0) };
        double[] w = { 2.5, 0.0, 0.0 };

        var m = FieldMoments.Accumulate(j => w[j], j => sig[j], 3);

        Assert.Equal(0.01, (double)m.a.X, 12);
        Assert.Equal(-0.02, (double)m.a.Y, 12);
        Assert.Equal(0.0, (double)m.b, 12);
        Assert.Equal(0.0, (double)m.c3.X, 12);
        Assert.Equal(0.0, (double)m.c3.Y, 12);
    }

    /// <summary>
    /// A coefficient of zero has no node, and asking for one must not produce a number. Dividing
    /// by it would send the centre to infinity, which is the right physics and the wrong return
    /// value.
    /// </summary>
    [Fact]
    public void AVanishingCoefficientHasNoFieldCentre()
    {
        var sig = new[] { new Vec2(0.1, 0.2), new Vec2(0.3, -0.1) };
        double[] w = { 1.0, -1.0 };

        var m = FieldMoments.Accumulate(j => w[j], j => sig[j], 2);

        Assert.False(m.HasField);
        Assert.Equal(0.0, (double)m.a.X, 12);
        Assert.Equal(0.0, (double)m.a.Y, 12);

        // The unnormalised moments are still perfectly well defined, and still carry the
        // information: it is only the division that is refused.
        Assert.NotEqual(0.0, (double)m.A.X, 12);
    }

    /// <summary>The two accumulation overloads agree.</summary>
    [Fact]
    public void TheListOverloadMatchesTheCallbackOverload()
    {
        var sig = new[] { new Vec2(0.1, 0.2), new Vec2(-0.3, 0.05), new Vec2(0.0, -0.4) };
        var w = new double[] { 1.5, -2.0, 0.25 };

        var a = FieldMoments.Accumulate(j => w[j], j => sig[j], 3);
        var b = FieldMoments.Accumulate(w, sig);

        Assert.Equal((double)a.W, (double)b.W, 12);
        Assert.Equal((double)a.C3.X, (double)b.C3.X, 12);
        Assert.Equal((double)a.C3.Y, (double)b.C3.Y, 12);
    }
}
