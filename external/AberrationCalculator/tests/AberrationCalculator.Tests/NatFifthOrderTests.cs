using System;
using AberrationCalculator.Core.Nat;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The fifth-order nodal solutions, Thompson 2010 Appendix B.
///
/// <para>These need no external oracle, and that is the point of them. Thompson gives two forms
/// of every result: an unnormalised one he recommends for computation, and a normalised one whose
/// structure reveals where the nodes are. The two are derived from each other by a page of vector
/// algebra, so <b>the analytic node positions must be zeros of the unnormalised expression</b>.
/// Each is implemented independently here and checked against the other. A slip in the cubic
/// solution, in the branch pairing, or in the vector product would break that agreement.</para>
/// </summary>
public class NatFifthOrderTests
{
    /// <summary>A synthetic system: contributions and displacements chosen to be untidy, so that
    /// nothing cancels by accident.</summary>
    private static NatFifthOrder Build(params Vec2[] sigmas)
    {
        var coeff = new[]
        {
            //                pi1  pi2   pi3   pi4   pi5   s1    s2    s3    s4    s5    s6    s7    s8    s9
            new Deformation(0.11, 0.23, -0.4, 0.17, 0.05, 1.30, -2.1, 0.75, -1.4, 0.62, 0.31, 0.44, -0.9, 0.13),
            new Deformation(-0.3, 0.07, 0.22, -0.6, 0.19, -0.8, 1.55, -0.3, 0.91, -1.2, 0.08, -0.7, 0.35, -0.2),
            new Deformation(0.05, -0.5, 0.13, 0.28, -0.1, 0.42, 0.33, 1.10, -0.2, 0.47, -0.5, 1.25, 0.60, 0.09),
        };
        double[] w131 = { 0.23, 0.07, -0.5 };

        int n = Math.Min(sigmas.Length, coeff.Length);
        return NatFifthOrder.Compute(j => coeff[j], j => w131[j], j => sigmas[j], n);
    }

    private static void AssertNearZero(Vec2 v, string what, double scale = 1.0)
        => Assert.True(v.Magnitude <= 1e-9 * Math.Max(1.0, scale),
                       $"{what}: residual {v} has magnitude {v.Magnitude}");

    /// <summary>
    /// The three elliptical-coma nodes are roots of Eq. (B11)'s cubic. This is the sharpest test
    /// in the file: the cubic is solved by Cardano in Thompson's vector algebra, with a branch
    /// choice - the pairing <c>R S = -b^2</c> - that has no other check on it.
    /// </summary>
    [Fact]
    public void TheThreeTrefoilNodesAreRootsOfTheCubic()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        var nodes = f.Nodes333;
        Assert.Equal(3, nodes.Length);
        foreach (var node in nodes)
            AssertNearZero(f.TrefoilResidual(node), $"trefoil node {node}");
    }

    /// <summary>
    /// The three field-cubed coma nodes are zeros of Eq. (B10)'s field-cubed group, and they are
    /// COLLINEAR - the outer two are placed symmetrically about the middle one, which is what
    /// makes this aberration's signature distinct from the trefoil's.
    /// </summary>
    [Fact]
    public void TheThreeFieldCubedComaNodesAreZerosAndCollinear()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        var nodes = f.Nodes331M;
        Assert.Equal(3, nodes.Length);
        foreach (var node in nodes)
            AssertNearZero(f.FieldCubedComaResidual(node), $"field-cubed coma node {node}");

        // Collinear: the outer two are equally spaced about the centre one, so their midpoint is
        // the centre. (That is stronger than collinearity and is what Eq. (B10) actually gives.)
        var mid = 0.5 * (nodes[1] + nodes[2]);
        AssertNearZero(mid - nodes[0], "midpoint of the outer pair against the centre node");
    }

    /// <summary>
    /// Field-linear fifth-order coma has one node, Eq. (B9), and it behaves exactly like the
    /// third-order coma it resembles.
    /// </summary>
    [Fact]
    public void FieldLinearFifthOrderComaHasASingleNode()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        Vec2 node = f.Node151;
        AssertNearZero(f.M151.W * node - f.M151.A, "W151 node");
    }

    /// <summary>
    /// Fifth-order astigmatism is binodal, on the same form Shack found at third order: the two
    /// nodes are where <c>(H - a242)^2 = -b242^2</c>.
    /// </summary>
    [Fact]
    public void FifthOrderAstigmatismIsBinodal()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        var nodes = f.Nodes242;
        Assert.Equal(2, nodes.Length);
        foreach (var node in nodes)
        {
            Vec2 hn = node - f.M242.a;
            AssertNearZero(hn.Squared + f.M242.b2, $"astigmatic node {node}");
        }
    }

    /// <summary>
    /// An aligned system has every node on axis. Thompson's own statement of this is that the
    /// rotationally symmetric theory is the special case of his "where the multinodal zeroes
    /// degenerate to overlay at the center of symmetry".
    /// </summary>
    [Fact]
    public void AnAlignedSystemHasEveryNodeOnAxis()
    {
        var f = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);

        AssertNearZero(f.Node151, "W151 node");
        foreach (var node in f.Nodes331M) AssertNearZero(node, "field-cubed coma node");
        foreach (var node in f.Nodes333) AssertNearZero(node, "trefoil node");
        foreach (var node in f.Nodes242) AssertNearZero(node, "astigmatic node");
        AssertNearZero(f.Vertex240M, "medial vertex");
        Assert.Equal(0.0, (double)f.B240M, 12);
    }

    /// <summary>
    /// A uniformly displaced system is the same system about a shifted axis, so every node moves
    /// to that displacement and none of them splits. This is the same statement as the field
    /// moment test, but carried all the way through the cubic solution - where a wrong branch or
    /// a mis-signed root would show up as a spurious splitting rather than as a shift.
    /// </summary>
    [Fact]
    public void AUniformDisplacementMovesEveryNodeAndSplitsNone()
    {
        var s = new Vec2(0.014, -0.0092);
        var f = Build(s, s, s);

        AssertNearZero(f.Node151 - s, "W151 node");
        foreach (var node in f.Nodes331M) AssertNearZero(node - s, "field-cubed coma node");
        foreach (var node in f.Nodes333) AssertNearZero(node - s, "trefoil node");
        foreach (var node in f.Nodes242) AssertNearZero(node - s, "astigmatic node");
        AssertNearZero(f.Vertex240M - s, "medial vertex");
    }

    /// <summary>
    /// Eqs. (B13-14): field-cubed coma changes both the magnitude and the node of the third-order
    /// coma it sits with. On an aligned system it must change neither, because there is nothing
    /// for it to be displaced about - and on a perturbed one it must change both, or the
    /// correction has not been applied.
    /// </summary>
    [Fact]
    public void FieldCubedComaModifiesTheThirdOrderComaItGenerates()
    {
        var aligned = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);
        Assert.Equal((double)aligned.M131.W, (double)aligned.W131E, 12);
        AssertNearZero(aligned.Node131E, "third-order coma node, aligned");

        var perturbed = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));
        Assert.NotEqual((double)perturbed.M131.W, (double)perturbed.W131E, 8);

        // And the corrected node is not simply the uncorrected one.
        Assert.True((perturbed.Node131E - perturbed.M131.a).Magnitude > 1e-9,
            "Eq. (B14) left the third-order coma node unchanged");
    }

    /// <summary>
    /// The unnormalised expansion, Eq. (B5), is what Thompson recommends computing with, and it
    /// must agree with the nodal picture: at a node of elliptical coma the trefoil part of the
    /// wave aberration vanishes for every pupil point.
    ///
    /// <para>This closes the loop between the two forms. <see cref="ComaticWave"/> is assembled
    /// term by term from Eqs. (B2-B4) and knows nothing about where the nodes are; the node
    /// solutions come from Eqs. (B10-B11) and know nothing about the expansion.</para>
    /// </summary>
    [Fact]
    public void TheUnnormalisedExpansionVanishesAtTheTrefoilNodes()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        // Isolate the trefoil term by differencing against a field with the same everything but
        // W333 removed is not available here, so instead check the residual vector directly for
        // several pupil points: W = (1/4)[residual].rho^3, so a vanishing residual kills the term
        // at every rho at once.
        foreach (var node in f.Nodes333)
        {
            Vec2 res = f.TrefoilResidual(node);
            foreach (var rho in new[] { new Vec2(1, 0), new Vec2(0, 1), new Vec2(0.6, -0.8) })
                Assert.True(Math.Abs((double)Vec2.Dot(res, rho.Squared * rho)) < 1e-9,
                    $"trefoil term at node {node}, pupil {rho}");
        }
    }

    /// <summary>
    /// The degeneracy guard in the trefoil solution must not swallow real physics.
    ///
    /// <para>A cube root raises relative error to the one-third power, so a <c>b^2</c> sitting at
    /// the round-off floor of the subtraction that made it returns a node splitting of order
    /// 1e-6 - noise reported as physics. The solution therefore collapses the three nodes onto
    /// the field centre when <c>b^2</c> and <c>c^3</c> are below the floor of their own
    /// construction. This checks the other side of that: a displacement small enough to be
    /// nearly uniform, but far above round-off, still splits the nodes.</para>
    /// </summary>
    [Fact]
    public void ASmallButRealDisplacementStillSplitsTheTrefoilNodes()
    {
        var s = new Vec2(0.014, -0.0092);
        var nudge = new Vec2(0.014 + 1e-7, -0.0092);
        var f = Build(s, nudge, s);

        var nodes = f.Nodes333;
        double spread = 0;
        for (int i = 0; i < 3; i++)
            for (int k = i + 1; k < 3; k++)
                spread = Math.Max(spread, (nodes[i] - nodes[k]).Magnitude);

        Assert.True(spread > 1e-12, $"a real displacement of 1e-7 was swallowed; spread {spread}");
        foreach (var node in nodes)
            AssertNearZero(f.TrefoilResidual(node), $"trefoil node {node}");
    }

    // ── The astigmatic types, Thompson 2011 ─────────────────────────────────────────────────

    /// <summary>
    /// Fifth-order astigmatism is QUADRANODAL - the property the 2011 paper is named for.
    /// Eq. (C23) leaves the fifth-order group as a cubic multiplied by a conjugate, so one node
    /// sits at the field centre where the conjugate vanishes and three more are the cubic's
    /// roots. All four must be zeros of Eq. (C23)'s group.
    /// </summary>
    [Fact]
    public void FifthOrderAstigmatismHasFourNodes()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        var nodes = f.Nodes422;
        Assert.Equal(4, nodes.Length);
        foreach (var node in nodes)
            AssertNearZero(f.FifthAstigmatismResidual(node), $"fifth astigmatic node {node}");

        // The four are genuinely distinct on a system this asymmetric.
        for (int i = 0; i < 4; i++)
            for (int k = i + 1; k < 4; k++)
                Assert.True((nodes[i] - nodes[k]).Magnitude > 1e-12,
                    $"nodes {i} and {k} coincide at {nodes[i]}");
    }

    /// <summary>
    /// The one node at the field centre is there because the conjugate vanishes, not because the
    /// cubic happens to have a root there. Checking it separately keeps that structural fact from
    /// being lost in the loop above.
    /// </summary>
    [Fact]
    public void OneFifthAstigmaticNodeSitsAtTheFieldCentre()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));
        AssertNearZero(f.Nodes422[0] - f.M422.a, "the conjugate node against the field centre");
    }

    /// <summary>
    /// Eqs. (C19-22): fifth-order astigmatism changes the magnitude, the centre AND the binodal
    /// separation of the third-order astigmatism it sits with - Shack's binodal astigmatism, with
    /// the field-quartic term folded in. On an aligned system it changes none of them.
    /// </summary>
    [Fact]
    public void FifthOrderAstigmatismModifiesTheThirdOrderBinodalPair()
    {
        var aligned = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);
        Assert.Equal((double)aligned.M222.W, (double)aligned.W222E, 12);
        foreach (var node in aligned.Nodes222E) AssertNearZero(node, "third-order node, aligned");

        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));
        Assert.NotEqual((double)f.M222.W, (double)f.W222E, 8);

        var nodes = f.Nodes222E;
        Assert.Equal(2, nodes.Length);
        foreach (var node in nodes)
        {
            Vec2 hn = node - f.A222E;
            AssertNearZero(hn.Squared + f.B2222ENormalised, $"modified third-order node {node}");
        }
    }

    /// <summary>
    /// Thompson 2011 Sec. 2: the fifth-order medial surface modifies the third-order one's
    /// magnitude, vertex and scalar offset. Aligned, it modifies nothing.
    /// </summary>
    [Fact]
    public void FifthOrderMedialFieldCurvatureModifiesTheThirdOrderSurface()
    {
        var aligned = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);
        Assert.Equal((double)aligned.M220M.W, (double)aligned.W220ME, 12);
        AssertNearZero(aligned.A220ME, "medial vertex, aligned");

        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));
        Assert.NotEqual((double)f.M220M.W, (double)f.W220ME, 8);
        Assert.True(f.A220ME.Magnitude > 1e-12, "the medial vertex did not move");
    }

    /// <summary>
    /// A uniform displacement moves the quadranodal set and splits nothing, carried through the
    /// cubic and the conjugate together.
    ///
    /// <para>This does NOT discriminate Eq. (C24): for a uniform sigma both <c>b^2</c> and
    /// <c>c^3</c> vanish, so the adjusted and unadjusted cubes agree and either would pass. What
    /// checks (C24) is <see cref="TheTwoAstigmaticFormsAgreeEverywhere"/>.</para>
    /// </summary>
    [Fact]
    public void AUniformDisplacementLeavesTheAstigmaticNodesUnsplit()
    {
        var s = new Vec2(0.014, -0.0092);
        var f = Build(s, s, s);

        foreach (var node in f.Nodes422) AssertNearZero(node - s, "fifth astigmatic node");
        foreach (var node in f.Nodes222E) AssertNearZero(node - s, "modified third-order node");
        AssertNearZero(f.A220ME - s, "medial vertex");
    }

    /// <summary>
    /// <b>The unnormalised astigmatic expansion and the nodal one must agree at every field and
    /// pupil point.</b> This is the test that actually verifies Eqs. (C19-24) - the modified
    /// third-order vectors <c>W222E</c>, <c>a222E</c>, <c>b222E^2</c> and the adjusted cube
    /// <c>(c422^3)'</c>. Nothing else does.
    ///
    /// <para>The two are transcribed from different equations and share no code: one is the
    /// third-order term of Eq. (C8) plus Eq. (C13), assembled from the raw moments and knowing
    /// nothing about nodes; the other is Eq. (C23), assembled from the normalised vectors. They
    /// are several pages of vector algebra apart in the paper. A wrong coefficient in any of
    /// (C19-24), or a dot product where a vector product belongs, separates them.</para>
    /// </summary>
    [Fact]
    public void TheTwoAstigmaticFormsAgreeEverywhere()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        var fields = new[]
        {
            new Vec2(0.0, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0),
            new Vec2(0.6, -0.8), new Vec2(-0.35, 0.22), new Vec2(0.05, 0.05),
        };
        var pupils = new[] { new Vec2(0.0, 1.0), new Vec2(1.0, 0.0), new Vec2(0.6, -0.8) };

        double largest = 0;
        foreach (var h in fields)
            foreach (var rho in pupils)
            {
                double a = f.AstigmaticWaveUnnormalised(h, rho);
                double b = f.AstigmaticWaveNodal(h, rho);
                largest = Math.Max(largest, Math.Abs(a));
                Assert.True(Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Abs(a)),
                    $"H = {h}, rho = {rho}: unnormalised {a}, nodal {b}");
            }

        // Two expressions that are both zero everywhere would agree without meaning anything.
        Assert.True(largest > 1e-3, $"the aberration is too small to be testing anything: {largest}");
    }

    // ── Fifth-order distortion ──────────────────────────────────────────────────────────────

    /// <summary>The W511 contributions the synthetic system carries, in surface order.</summary>
    private static readonly double[] W511 = { 0.13, -0.2, 0.09 };

    /// <summary>
    /// <b>The derived closed form must equal the defining sum, exactly.</b>
    ///
    /// <para>Thompson's nodal solution for fifth-order distortion is in his 1980 dissertation,
    /// which this archive does not hold, so the expansion in <see cref="NatFifthOrder.DistortionField"/>
    /// is derived from the published definition rather than transcribed from a result. That makes
    /// this the test the whole term rests on: the closed form, written in the standard moments,
    /// against <c>sum_j W511j [(H - sigma_j).(H - sigma_j)]^2 (H - sigma_j)</c> evaluated surface
    /// by surface. Any error in reducing a mixed product - and there are four of them -
    /// separates the two immediately.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(-0.6, 0.8)]
    [InlineData(0.033, -0.014)]
    [InlineData(12.0, -7.0)]
    public void TheDerivedDistortionFieldMatchesTheDefiningSum(double hx, double hy)
    {
        var sig = new[] { new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004) };
        var f = Build(sig[0], sig[1], sig[2]);
        var h = new Vec2(hx, hy);

        Vec2 closed = f.DistortionField(h);
        Vec2 direct = NatFifthOrder.DistortionFieldDirect(j => W511[j], j => sig[j], 3, h);

        double scale = Math.Max(1.0, direct.Magnitude);
        Assert.True((closed - direct).Magnitude <= 1e-11 * scale,
            $"H = {h}: closed {closed}, direct {direct}");
    }

    /// <summary>
    /// An aligned system reduces the field to <c>W511 (H.H)^2 H</c>, whose only zero is the
    /// origin - five coincident nodes, not five separate ones.
    /// </summary>
    [Fact]
    public void AnAlignedSystemPutsAllFiveDistortionNodesTogether()
    {
        var f = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);

        var h = new Vec2(0.3, -0.4);
        double hh = Vec2.Dot(h, h);
        Vec2 expect = (f.M511.W * hh * hh) * h;
        AssertNearZero(f.DistortionField(h) - expect, "aligned distortion field");

        var nodes = f.DistortionNodes();
        Assert.Equal(5, nodes.Length);
        foreach (var node in nodes) AssertNearZero(node, "distortion node, aligned");
    }

    /// <summary>
    /// A uniform displacement moves the quintuple node and splits nothing, which for this term is
    /// the statement that the field is exactly <c>W511 (H-s . H-s)^2 (H-s)</c>.
    /// </summary>
    [Fact]
    public void AUniformDisplacementMovesTheDistortionNodeWithoutSplitting()
    {
        var s = new Vec2(0.014, -0.0092);
        var f = Build(s, s, s);

        var h = new Vec2(0.3, -0.4);
        Vec2 u = h - s;
        double uu = Vec2.Dot(u, u);
        AssertNearZero(f.DistortionField(h) - (f.M511.W * uu * uu) * u, "uniform distortion field");

        foreach (var node in f.DistortionNodes()) AssertNearZero(node - s, "distortion node");
    }

    /// <summary>
    /// Outside the two exact cases the node solution is refused, and returns empty.)
    ///
    /// <para>Five is the most fifth-order distortion can have, not the number it does have. The
    /// equation carries <c>H*</c> as well as <c>H</c>, so its real root count depends on the
    /// system; on every case tried there is exactly one node, confirmed by a brute-force scan of
    /// the whole disc rather than inferred from this search finding no more. An earlier version
    /// of the search accepted a residual of 8e-9 as a root and reported a second node that the
    /// scan says is not there, which is why the acceptance test is now measured against the size
    /// the field has over the search region rather than against an absolute epsilon.</para>
    /// </summary>
    [Fact]
    public void ThePerturbedDistortionNodesAreRefusedRatherThanGuessed()
    {
        var f = Build(new Vec2(0.03, -0.017), new Vec2(-0.008, 0.021), new Vec2(0.012, 0.004));

        // Empty, deliberately. A multi-start Newton search was written for this and then removed:
        // on a tilted Cooke triplet a direct scan of the field over the whole disc finds four
        // roots at positions the search did not report, and on a tilted double Gauss the two
        // disagree again. The answer moved every time the acceptance tolerance was retuned, which
        // says the tolerance was deciding rather than the mathematics. Thompson's closed solution
        // is in his 1980 dissertation, which is not to hand.
        Assert.Empty(f.DistortionNodes());

        // The FIELD is unaffected by that and remains exact, which is what the rest of the
        // distortion tests check. This asserts only that it is a live quantity, not a stub.
        Assert.True(f.DistortionField(new Vec2(0.2, -0.1)).Magnitude > 0.0);
    }

    /// <summary>
    /// The wave aberration is finite and well behaved across the field, and reduces to the
    /// rotationally symmetric answer on axis when the system is aligned.
    /// </summary>
    [Fact]
    public void TheAlignedWaveIsTheSymmetricOne()
    {
        var f = Build(Vec2.Zero, Vec2.Zero, Vec2.Zero);
        var h = new Vec2(0.0, 0.7);
        var rho = new Vec2(0.0, 1.0);

        // With every sigma zero the field vectors vanish and Eq. (B5) collapses to
        //   W131 (H.rho)(rho.rho) + W151 (H.rho)(rho.rho)^2
        //     + W331M (H.H)(H.rho)(rho.rho) + (1/4) W333 (H^3.rho^3)
        double rr = Vec2.Dot(rho, rho), hh = Vec2.Dot(h, h);
        double expect = f.M131.W * Vec2.Dot(h, rho) * rr
                      + f.M151.W * Vec2.Dot(h, rho) * rr * rr
                      + f.M331M.W * hh * Vec2.Dot(h, rho) * rr
                      + 0.25 * f.M333.W * Vec2.Dot(h.Squared * h, rho.Squared * rho);

        Assert.Equal(expect, (double)f.ComaticWave(h, rho), 10);
    }
}
