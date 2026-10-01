using System;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Nat;
using AberrationCalculator.Core.RayTrace;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Nodal aberration theory, stages one and two: the vector algebra, the two coordinate frames,
/// the Seidel-to-Wklm bridge, and Gu's tolerance sensitivity.
///
/// <para>Most of what can go wrong here goes wrong QUIETLY - a conjugate that negates the wrong
/// component, or an orientation rule applied to the Zernike pair it does not belong to, produces
/// perfectly plausible numbers ninety degrees from where they belong. So these tests check the
/// structure rather than spot values wherever they can: that orientations ADD under
/// multiplication, that the published identities hold, and that the three frame conversions are
/// genuinely three different rules.</para>
/// </summary>
public class NatTests
{
    private const double Tol = 1e-12;

    // ── Vec2 ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The convention this whole subject turns on: phi runs CLOCKWISE FROM Y, so y is the real
    /// axis and the conjugate negates x. Fuerschbach 2014 Eq. (12).
    /// </summary>
    [Fact]
    public void TheConjugateNegatesXAndNotY()
    {
        var v = new Vec2(3.0, 4.0);
        Assert.Equal(-3.0, v.Conjugate.X, 12);
        Assert.Equal(4.0, v.Conjugate.Y, 12);
    }

    /// <summary>A vector on the y axis is at orientation zero, not ninety degrees.</summary>
    [Fact]
    public void OrientationIsMeasuredFromTheYAxis()
    {
        Assert.Equal(0.0, new Vec2(0.0, 1.0).Orientation, 12);
        Assert.Equal(Math.PI / 2.0, new Vec2(1.0, 0.0).Orientation, 12);
    }

    /// <summary>
    /// Multiplication adds orientations and multiplies magnitudes. This is what makes the
    /// algebra the right one; if the product were the naive complex form with x real, this would
    /// fail and every node in the theory would land in the wrong quadrant.
    /// </summary>
    [Theory]
    [InlineData(0.3, 1.7, 0.9, -0.4)]
    [InlineData(2.0, 0.1, 0.5, 2.9)]
    [InlineData(1.0, -2.5, 3.0, 0.75)]
    public void MultiplicationAddsOrientations(double m1, double p1, double m2, double p2)
    {
        var a = Vec2.FromPolar(m1, p1);
        var b = Vec2.FromPolar(m2, p2);
        var c = a * b;

        Assert.Equal(m1 * m2, c.Magnitude, 12);

        // Orientations add modulo a full turn.
        double expected = Math.Atan2(Math.Sin(p1 + p2), Math.Cos(p1 + p2));
        Assert.Equal(expected, c.Orientation, 12);
    }

    /// <summary>
    /// Thompson's first identity, quoted as Eq. (11) of Fuerschbach 2014:
    /// <c>A . (B C) = A B* . C</c>.
    /// </summary>
    [Fact]
    public void TheConjugateIdentityHolds()
    {
        var a = new Vec2(0.7, -1.3);
        var b = new Vec2(-2.1, 0.4);
        var c = new Vec2(1.1, 1.9);

        Assert.Equal(Vec2.Dot(a, b * c), Vec2.Dot(a * b.Conjugate, c), 12);
    }

    /// <summary>
    /// Thompson's second identity, Eq. (23) of Fuerschbach 2014:
    /// <c>2 (A.B)(A.C) = (A.A)(B.C) + A^2 . (B C)</c>. This is the one the coma-overlay
    /// expansion runs through.
    /// </summary>
    [Fact]
    public void TheExpansionIdentityHolds()
    {
        var a = new Vec2(0.7, -1.3);
        var b = new Vec2(-2.1, 0.4);
        var c = new Vec2(1.1, 1.9);

        double lhs = 2.0 * Vec2.Dot(a, b) * Vec2.Dot(a, c);
        double rhs = Vec2.Dot(a, a) * Vec2.Dot(b, c) + Vec2.Dot(a.Squared, b * c);

        Assert.Equal(lhs, rhs, 12);
    }

    /// <summary>
    /// The component forms printed in Thompson's Appendix A, checked term for term. This is the
    /// primary source for this algebra, so these are not a restatement of the implementation -
    /// they are the definition it has to meet.
    ///
    /// <para>Eq. (A1)/(A9), vector multiplication:
    /// <c>(AB)x = ay bx + ax by</c>, <c>(AB)y = ay by - ax bx</c>.</para>
    /// <para>Eq. (A6), the conjugate: <c>A* = -ax i + ay j</c>.</para>
    /// <para>Eq. (A7), the product with a conjugate:
    /// <c>(AB*)x = ax by - ay bx</c>, <c>(AB*)y = ay by + ax bx</c>.</para>
    /// <para>Eq. (A10), the squared vector:
    /// <c>(A^2)x = 2 ax ay</c>, <c>(A^2)y = ay^2 - ax^2</c>.</para>
    /// </summary>
    [Fact]
    public void TheComponentFormsMatchThompsonAppendixA()
    {
        var a = new Vec2(0.7, -1.3);
        var b = new Vec2(-2.1, 0.4);

        var ab = a * b;
        Assert.Equal(a.Y * b.X + a.X * b.Y, ab.X, 12);          // (A1), (A9)
        Assert.Equal(a.Y * b.Y - a.X * b.X, ab.Y, 12);

        Assert.Equal(-a.X, a.Conjugate.X, 12);                  // (A6)
        Assert.Equal(a.Y, a.Conjugate.Y, 12);

        var abStar = a * b.Conjugate;
        Assert.Equal(a.X * b.Y - a.Y * b.X, abStar.X, 12);      // (A7)
        Assert.Equal(a.Y * b.Y + a.X * b.X, abStar.Y, 12);

        var sq = a.Squared;
        Assert.Equal(2.0 * a.X * a.Y, sq.X, 12);                // (A10)
        Assert.Equal(a.Y * a.Y - a.X * a.X, sq.Y, 12);
    }

    /// <summary>
    /// Thompson's third identity, Eq. (A13):
    /// <c>2 (A.B)(AB . C^2) = (A.A)(B^2 . C^2) + (B.B)(A^2 . C^2)</c>.
    /// </summary>
    [Fact]
    public void TheThirdIdentityHolds()
    {
        var a = new Vec2(0.7, -1.3);
        var b = new Vec2(-2.1, 0.4);
        var c = new Vec2(1.1, 1.9);

        double lhs = 2.0 * Vec2.Dot(a, b) * Vec2.Dot(a * b, c.Squared);
        double rhs = Vec2.Dot(a, a) * Vec2.Dot(b.Squared, c.Squared)
                   + Vec2.Dot(b, b) * Vec2.Dot(a.Squared, c.Squared);

        Assert.Equal(lhs, rhs, 12);
    }

    /// <summary>Both square roots square back, which is what the binodal solution needs.</summary>
    [Fact]
    public void BothSquareRootsSquareBack()
    {
        var v = new Vec2(-0.8, 2.2);
        var r = v.Sqrt();

        Assert.Equal(v.X, r.Squared.X, 12);
        Assert.Equal(v.Y, r.Squared.Y, 12);

        var minus = -r;
        Assert.Equal(v.X, minus.Squared.X, 12);
        Assert.Equal(v.Y, minus.Squared.Y, 12);
    }

    // ── Conventions ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The three Zernike pairs do NOT share a conversion rule, and this is the test that says
    /// so. Astigmatism and coma are a reflection about the quarter turn; trefoil EXCHANGES its
    /// arguments. A reader who assumes one rule gets two of them wrong, and nothing else here
    /// would catch it.
    /// </summary>
    [Fact]
    public void TheThreeFrameConversionsAreThreeDifferentRules()
    {
        const double a = 0.6, b = 0.25;

        Assert.Equal(0.5 * (Math.PI / 2.0 - Math.Atan2(b, a)),
                     Conventions.NatAstigmatism(a, b), 12);

        Assert.Equal(Math.PI / 2.0 - Math.Atan2(b, a), Conventions.NatComa(a, b), 12);

        // The trefoil rule reads its arguments the other way round - Fuerschbach 2012 Eq. (8)
        // against Eq. (6). Applying the astigmatism-shaped rule here would give something else
        // entirely, which this pins.
        Assert.Equal((1.0 / 3.0) * Math.Atan2(a, b), Conventions.NatTrefoil(a, b), 12);
        Assert.NotEqual(Conventions.NatTrefoil(a, b), Conventions.TestTrefoil(a, b), 6);
    }

    /// <summary>
    /// A surface AT the stop has no beam walk, so an overlay there stays field constant. This is
    /// the distinction the whole freeform stage is built on.
    /// </summary>
    [Fact]
    public void BeamDisplacementVanishesWhereTheMarginalRayDoes()
    {
        Assert.Equal(0.0, Conventions.BeamDisplacement(0.0, 3.0), 12);
        Assert.Equal(0.5, Conventions.BeamDisplacement(4.0, 2.0), 12);
    }

    /// <summary>A mirror's index step is -2, so the overlay vectors change sign through it.</summary>
    [Fact]
    public void AnOverlayVectorScalesWithTheIndexStep()
    {
        var refracting = Conventions.ComaOverlay(1.0, 0.0, 1.0, 1.5);
        var mirror = Conventions.ComaOverlay(1.0, 0.0, 1.0, -1.0);

        Assert.Equal(3.0 * 0.5, refracting.Magnitude, 12);
        Assert.Equal(3.0 * 2.0, mirror.Magnitude, 12);
    }

    // ── WaveCoefficients ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Seidel-to-Wklm bridge, on a real design. The ratios are fixed and exact, and the
    /// medial field curvature is NOT the Petzval one - mistaking them puts the field-curvature
    /// node in the wrong place.
    /// </summary>
    [Fact]
    public void TheWaveCoefficientBridgeIsExact()
    {
        var (sys, n, p) = Load("CookeTriplet");
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var w = WaveCoefficients.OfSystem(seidel);

        Assert.Equal(seidel.TotalS1 / 8.0, w.W040, 15);
        Assert.Equal(seidel.TotalS2 / 2.0, w.W131, 15);
        Assert.Equal(seidel.TotalS3 / 2.0, w.W222, 15);
        Assert.Equal(seidel.TotalS4 / 4.0, w.W220P, 15);
        Assert.Equal(seidel.TotalS5 / 2.0, w.W311, 15);

        // The four field-curvature surfaces, a half-astigmatism apart in a chain. They are
        // asserted against the SEIDEL SUMS rather than against each other, because asserting
        // W220M against W220P + W222/2 is what let the medial read the sagittal for as long as
        // it did: the test restated the formula instead of checking it.
        Assert.Equal((seidel.TotalS3 + seidel.TotalS4) / 4.0, w.W220S, 15);       // sagittal
        Assert.Equal((2 * seidel.TotalS3 + seidel.TotalS4) / 4.0, w.W220M, 15);   // medial
        Assert.Equal((3 * seidel.TotalS3 + seidel.TotalS4) / 4.0, w.W220T, 15);   // tangential

        // and the medial really is the average of the other two
        Assert.Equal(0.5 * (w.W220S + w.W220T), w.W220M, 15);
        Assert.NotEqual(w.W220P, w.W220M, 6);
        Assert.NotEqual(w.W220S, w.W220M, 6);
    }

    /// <summary>Per-surface coefficients sum to the system's, because third order is additive.</summary>
    [Fact]
    public void PerSurfaceWaveCoefficientsSumToTheSystem()
    {
        var (sys, n, p) = Load("CookeTriplet");
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);

        double w131 = 0.0, w222 = 0.0;
        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
        {
            var s = WaveCoefficients.OfSurface(seidel, j);
            w131 += s.W131;
            w222 += s.W222;
        }

        var total = WaveCoefficients.OfSystem(seidel);
        Assert.Equal(total.W131, w131, 12);
        Assert.Equal(total.W222, w222, 12);
    }

    // ── Sensitivity ─────────────────────────────────────────────────────────────────────

    /// <summary>A design with no tolerance to meet has no as-built penalty.</summary>
    [Fact]
    public void ZeroToleranceCostsNothing()
    {
        var (sys, n, p) = Load("CookeTriplet");
        Assert.Equal(0.0, Sensitivity.AsBuilt(sys, n, p, 0.0, 0.0, 0.5), 15);
    }

    /// <summary>
    /// <b>The singularity that is not there.</b> Gu's sigma vector carries <c>1/ibar</c>, which
    /// blows up wherever the chief ray strikes a surface at normal incidence. The kernels here
    /// cancel that factor analytically, so the answer must stay finite even when a surface is
    /// arranged to sit exactly at that incidence.
    ///
    /// <para>Surface 3 of the triplet is the stop, where the chief ray height is zero; bending
    /// it changes nothing about ibar there, so the case is forced directly instead - the test
    /// sweeps a curvature through the value that zeroes the chief-ray incidence and requires the
    /// result to stay finite and smooth across it.</para>
    /// </summary>
    [Fact]
    public void TheResultStaysFiniteWhereTheChiefRayIncidenceVanishes()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new System.Collections.Generic.List<string>());

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        // Find the curvature at which surface 1's chief-ray incidence passes through zero:
        // ibar = ybar c + ubar = 0, so c = -ubar / ybar.
        var p0 = ParaxialTrace.Trace(sys, n, field);
        double ybar = p0.Ybar[1], ubar = p0.Ubar[0];
        if (Math.Abs(ybar) < 1e-12) return;           // nothing to force on this design
        double cZero = -ubar / ybar;

        double previous = double.NaN;
        for (int k = -2; k <= 2; k++)
        {
            sys.Surfaces[1].Curvature = cZero + k * 1e-9;
            var p = ParaxialTrace.Trace(sys, n, field);
            double v = Sensitivity.AsBuilt(sys, n, p, 0.0399, 0.00267, 0.5);

            Assert.True(double.IsFinite(v), $"as-built went non-finite at c = {sys.Surfaces[1].Curvature}");
            if (!double.IsNaN(previous))
                Assert.True(Math.Abs(v - previous) < 0.05 * Math.Max(Math.Abs(v), 1e-9) + 1e-9,
                            "as-built jumped across the vanishing chief-ray incidence: "
                          + previous + " then " + v);
            previous = v;
        }
    }

    /// <summary>
    /// How the terms scale. The coma and the field-linear astigmatism are linear in the
    /// equivalent tilt; the field-constant astigmatism is quadratic. Doubling a tolerance that
    /// only drove a linear term would double the answer, so the departure from exactly two is
    /// the quadratic term making itself felt - and it must be a departure UPWARDS.
    /// </summary>
    [Fact]
    public void TheAnswerGrowsFasterThanLinearlyWithTheTolerance()
    {
        var (sys, n, p) = Load("CookeTriplet");

        double one = Sensitivity.AsBuilt(sys, n, p, 0.04, 0.00267, 0.5);
        double two = Sensitivity.AsBuilt(sys, n, p, 0.08, 0.00534, 0.5);

        Assert.True(one > 0.0, "a real design with a real tolerance must cost something");
        Assert.True(two > 2.0 * one,
                    $"expected super-linear growth, got {two} against {2.0 * one}");
    }

    /// <summary>
    /// Surfaces ahead of a perturbation contribute nothing to it - light has not reached them.
    /// Gu Eq. (8). The kernel of the LAST surface therefore has only its own term in it, and is
    /// the simplest one to check independently.
    /// </summary>
    [Fact]
    public void TheKernelOfTheLastSurfaceHasOnlyItsOwnTerm()
    {
        var (sys, n, p) = Load("CookeTriplet");
        var kernels = Sensitivity.PerSurface(sys, n, p);
        int last = sys.LastOpticalSurface();

        // Rebuilt here from the paraxial data alone: i G / (4 sqrt2), with xi = 1.
        double nBefore = p.N[last - 1], nAfter = p.N[last];
        double c = sys.Surfaces[last].VertexCurvature;
        double y = p.Y[last], u = p.U[last - 1];
        double i = y * c + u;
        double g = nBefore * nBefore * y * (p.U[last] / nAfter - u / nBefore);

        Assert.Equal(i * g / (4.0 * Math.Sqrt(2.0)), kernels[last].Coma, 12);
    }

    // ── Stage 3: sigma, the aberration field, and the nodes ─────────────────────────────

    /// <summary>
    /// <b>The gate on the whole stage.</b> An aligned system has every sigma zero, so the sums
    /// collapse and every node sits at the centre of the field. If this fails nothing below it
    /// means anything, and it is exact rather than approximate - the perturbation terms are
    /// multiplied by zero, not by something small.
    /// </summary>
    [Fact]
    public void AnAlignedSystemPutsEveryNodeAtTheFieldCentre()
    {
        var f = Field("CookeTriplet");

        Assert.True(f.IsAligned);
        foreach (var s in f.Sigmas.Sigma) Assert.Equal(0.0, s.Magnitude, 15);

        Assert.Equal(0.0, f.A131.Magnitude, 15);
        Assert.Equal(0.0, f.A222.Magnitude, 15);
        Assert.Equal(0.0, f.B222Squared.Magnitude, 15);

        Assert.Equal(0.0, f.ComaNode.Magnitude, 15);
        Assert.Equal(0.0, f.AstigmatismNode1.Magnitude, 15);
        Assert.Equal(0.0, f.AstigmatismNode2.Magnitude, 15);
    }

    /// <summary>
    /// <b>The sign test, and it is decisive.</b> Gu's Eqs. (21) and (27) disagree about the sign
    /// of sigma, and Thompson (2005) gives no formula in tilt and decentre at all - he defines
    /// sigma geometrically, as the projection of the line joining the pupil centre to the
    /// surface's CENTRE OF CURVATURE.
    ///
    /// <para>That definition is testable. A sphere is symmetric about every axis through its
    /// centre of curvature, so a perturbation that leaves the centre of curvature where it was
    /// cannot displace anything: sigma must be exactly zero. Tilting by <c>T</c> about the vertex
    /// swings the centre of curvature, at distance <c>R = 1/c</c>, sideways by <c>R T</c>;
    /// decentring by <c>D = -R T</c> puts it back.</para>
    ///
    /// <para>So the combination that vanishes is <c>T + c D</c> - which is Gu's equivalent tilt
    /// with a PLUS, and settles the relative sign of the two terms against Thompson's geometry
    /// rather than against either of Gu's equations.</para>
    /// </summary>
    [Fact]
    public void ATiltCancelledByItsOwnDecentreDisplacesNothing()
    {
        var (sys, n, _) = Load("CookeTriplet");

        const double tilt = 0.004;                       // radians
        double c = sys.Surfaces[2].VertexCurvature;
        Assert.True(Math.Abs(c) > 1e-6, "this test needs a curved surface");

        sys.Surfaces[2].TiltX = tilt;
        sys.Surfaces[2].DecenterY = -tilt / c;           // D = -R T, the centre of curvature stays

        var f = FieldOf(sys, n);

        Assert.False(f.IsAligned, "the surface IS perturbed; it is the EFFECT that cancels");
        foreach (var s in f.Sigmas.Reduced)
            Assert.True(s.Magnitude < 1e-18, "a stationary centre of curvature displaced something");

        Assert.Equal(0.0, f.ComaNode.Magnitude, 12);
        Assert.Equal(0.0, f.AstigmatismNode1.Magnitude, 12);
    }

    /// <summary>
    /// Sigma is linear in the perturbation and reverses with it - the property that makes the
    /// whole treatment a first-order one, and the cheapest check that nothing squares a sign
    /// away.
    /// </summary>
    [Fact]
    public void SigmaIsLinearInThePerturbationAndReversesWithIt()
    {
        var (sys, n, _) = Load("CookeTriplet");

        sys.Surfaces[2].TiltX = 0.001;
        var one = FieldOf(sys, n).Sigmas.Reduced[4];

        sys.Surfaces[2].TiltX = 0.002;
        var two = FieldOf(sys, n).Sigmas.Reduced[4];

        sys.Surfaces[2].TiltX = -0.001;
        var back = FieldOf(sys, n).Sigmas.Reduced[4];

        Assert.True(one.Magnitude > 0.0, "a real tilt on a real surface must displace something");
        Assert.Equal(2.0 * one.X, two.X, 12);
        Assert.Equal(2.0 * one.Y, two.Y, 12);
        Assert.Equal(-one.X, back.X, 12);
        Assert.Equal(-one.Y, back.Y, 12);
    }

    /// <summary>
    /// Light has not reached a surface ahead of the perturbation, so it carries no displacement.
    /// Gu Eq. (8), and the reason the inner sum starts at <c>k</c>.
    /// </summary>
    [Fact]
    public void SurfacesAheadOfThePerturbationAreUndisplaced()
    {
        var (sys, n, _) = Load("CookeTriplet");
        sys.Surfaces[4].TiltX = 0.003;

        var f = FieldOf(sys, n);

        for (int j = 1; j < 4; j++)
            Assert.Equal(0.0, f.Sigmas.Reduced[j].Magnitude, 15);

        Assert.True(f.Sigmas.Reduced[4].Magnitude > 0.0);
    }

    /// <summary>
    /// The nodes are where the aberration is zero - which is what the word means, and is worth
    /// asserting because the node formulae and the field formulae are written down separately
    /// and could drift apart.
    /// </summary>
    [Fact]
    public void TheNodesAreTheZerosOfTheField()
    {
        var (sys, n, _) = Load("CookeTriplet");
        sys.Surfaces[2].TiltX = 0.002;
        sys.Surfaces[4].DecenterY = 0.05;

        var f = FieldOf(sys, n);
        Assert.True(f.ComaNodeExists && f.AstigmatismNodesExist);

        // Scale: how big the aberration is at the edge of the field, for a relative tolerance.
        double scale = f.AstigmatismAt(new Vec2(0.0, 1.0)).Magnitude;
        Assert.True(scale > 0.0);

        Assert.True(f.ComaAt(f.ComaNode).Magnitude < 1e-12 * Math.Abs(f.Totals.W131) + 1e-18);
        Assert.True(f.AstigmatismAt(f.AstigmatismNode1).Magnitude < 1e-10 * scale);
        Assert.True(f.AstigmatismAt(f.AstigmatismNode2).Magnitude < 1e-10 * scale);
    }

    /// <summary>
    /// The two astigmatic nodes sit symmetrically about their midpoint <c>a222</c>, at ninety
    /// degrees to the node-splitting vector - Thompson Eq. (4.25). This is the geometry of
    /// Shack's discovery, and it is what lets figure error be told from misalignment: a figure
    /// error at the stop contributes to <c>B222^2</c> but not to <c>a222</c>, so ITS nodes stay
    /// symmetric about the field centre while a misalignment's do not.
    /// </summary>
    [Fact]
    public void TheTwoAstigmaticNodesAreSymmetricAboutTheirMidpoint()
    {
        var (sys, n, _) = Load("CookeTriplet");
        sys.Surfaces[2].TiltX = 0.002;

        var f = FieldOf(sys, n);
        var mid = 0.5 * (f.AstigmatismNode1 + f.AstigmatismNode2);

        Assert.Equal(f.A222Normalised.X, mid.X, 12);
        Assert.Equal(f.A222Normalised.Y, mid.Y, 12);
    }

    /// <summary>
    /// The report reduces correctly on an aligned design - it says so in as many words rather
    /// than printing a table of zeros - and says something different once a surface moves.
    /// </summary>
    [Fact]
    public void TheReportDistinguishesAnAlignedDesignFromAMisalignedOne()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var aligned = new Core.Report.ReportWriter(LensFile.Read(path, catalog), catalog, path);
        string a = aligned.BuildNatText();
        Assert.Contains("ALIGNED", a);
        Assert.DoesNotContain("Nodes", a);

        var sys = LensFile.Read(path, catalog);
        sys.Surfaces[2].TiltX = 0.002;
        var moved = new Core.Report.ReportWriter(sys, catalog, path);
        string m = moved.BuildNatText();

        Assert.DoesNotContain("ALIGNED", m);
        Assert.Contains("Nodes", m);
        Assert.Contains("coma", m);
        Assert.Contains("astigmatism", m);
    }

    /// <summary>
    /// The full-field display is a square grid, and the astigmatism it reports at a node is the
    /// zero the node claims to be. This is the report and the field equations agreeing, which
    /// they are written separately enough to be worth checking.
    /// </summary>
    [Fact]
    public void TheFullFieldDisplayIsAGridAndAgreesWithTheNodes()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        var sys = LensFile.Read(path, catalog);
        sys.Surfaces[2].TiltX = 0.002;

        string tsv = new Core.Report.ReportWriter(sys, catalog, path).BuildNatFullFieldTsv(5);
        var lines = tsv.TrimEnd().Split('\n');

        Assert.Equal(1 + 25, lines.Length);                      // header plus 5 x 5
        Assert.StartsWith("hx\thy\tcoma", lines[0]);

        // The first six columns keep their names and their meaning; the fifth-order ones are
        // appended after them, so a reader written against the old file still works.
        int columns = lines[0].Trim().Split('\t').Length;
        Assert.True(columns == 6 || columns == 20, $"unexpected column count {columns}");

        // Every row parses, and the field really does vary over the grid.
        double biggest = 0.0;
        for (int i = 1; i < lines.Length; i++)
        {
            var cell = lines[i].Trim().Split('\t');
            Assert.Equal(columns, cell.Length);
            biggest = Math.Max(biggest, double.Parse(cell[4], System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.True(biggest > 0.0);
    }

    /// <summary>
    /// <b>On an aligned system the fifth-order correction to the third order is nothing, and the
    /// corrected columns must equal the uncorrected ones to the last digit.</b>
    ///
    /// <para>That is not free. The two come by different routes - <c>coma</c> from the Seidel
    /// chain, <c>coma_E</c> from Buchdahl's W coordinates through Eqs. (B13-14) - and the routes
    /// use different normalisations, differing by a factor <c>A^l F^k</c>. The columns are made
    /// comparable by scaling with the ratio <c>W131(Seidel)/W131(Buchdahl)</c>, which needs no
    /// scale factor to be derived because the induced ratio <c>W131E/W131</c> is itself free of
    /// the normalisation. If that scaling were wrong, or if the induced terms failed to vanish
    /// when every sigma is zero, this separates immediately.</para>
    /// </summary>
    [Fact]
    public void TheCorrectedColumnsMatchTheUncorrectedOnesWhenAligned()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        var sys = LensFile.Read(path, catalog);

        string tsv = new Core.Report.ReportWriter(sys, catalog, path).BuildNatFullFieldTsv(5);
        var lines = tsv.TrimEnd().Split('\n');
        if (lines[0].Trim().Split('\t').Length < 20) return;     // no wave front chain here

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double spread = 0.0;
        for (int i = 1; i < lines.Length; i++)
        {
            var c = lines[i].Trim().Split('\t');
            double coma = double.Parse(c[2], inv), comaE = double.Parse(c[6], inv);
            double ast = double.Parse(c[4], inv), astE = double.Parse(c[8], inv);

            Assert.Equal(coma, comaE, 12);
            Assert.Equal(ast, astE, 12);
            spread = Math.Max(spread, Math.Abs(coma));
        }
        Assert.True(spread > 0.0, "the aligned coma is zero everywhere, so nothing was compared");
    }

    /// <summary>
    /// And on a perturbed system the correction is NOT nothing - the fifth order really does
    /// change the third-order coma it sits with, which is the reason the column exists.
    /// </summary>
    [Fact]
    public void TheCorrectedColumnsDifferWhenASurfaceIsTilted()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        var sys = LensFile.Read(path, catalog);
        sys.Surfaces[2].TiltY = 0.15;

        string tsv = new Core.Report.ReportWriter(sys, catalog, path).BuildNatFullFieldTsv(5);
        var lines = tsv.TrimEnd().Split('\n');
        if (lines[0].Trim().Split('\t').Length < 20) return;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double worst = 0.0;
        for (int i = 1; i < lines.Length; i++)
        {
            var c = lines[i].Trim().Split('\t');
            double coma = double.Parse(c[2], inv), comaE = double.Parse(c[6], inv);
            if (Math.Abs(coma) > 1e-12) worst = Math.Max(worst, Math.Abs(comaE - coma) / Math.Abs(coma));
        }
        Assert.True(worst > 1e-3, $"the fifth order changed the third-order coma by only {worst:0.000e+00}");
    }

    /// <summary>
    /// <b>The assembled nodal form against the raw sum it was assembled from.</b>
    ///
    /// <para>Thompson Eq. (4.14) is the aberration as a sum over surfaces of displaced fields,
    /// <c>sum_j W222_j (H - sigma_j)^2</c>. Eq. (4.19) is the same thing collected into
    /// <c>W222[(H - a222)^2 + b222^2]</c>. They are algebraically identical and computed by
    /// completely different routes - one walks the surfaces, the other goes through the
    /// normalised displacement vectors and the vector square - so agreement between them
    /// exercises the whole of the <c>a</c>/<c>b</c> algebra against arithmetic that never uses
    /// it. No oracle, no second program, nothing published: an identity that must hold.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.3, -0.7)]
    [InlineData(-1.0, 0.4)]
    [InlineData(0.9, 0.9)]
    public void TheNodalFormAgreesWithTheRawSumOverSurfaces(double hx, double hy)
    {
        var (sys, n, _) = Load("CookeTriplet");
        sys.Surfaces[2].TiltX = 0.002;
        sys.Surfaces[4].DecenterY = 0.05;

        double field = 0.0;
        foreach (var fl in sys.Fields) if (Math.Abs(fl.Y) > Math.Abs(field)) field = fl.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var f = NatField.Compute(sys, n, p, seidel);

        var h = new Vec2(hx, hy);
        Vec2 rawAst = Vec2.Zero, rawComa = Vec2.Zero;

        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
        {
            var w = WaveCoefficients.OfSurface(seidel, j);
            var ha = h - f.Sigmas.Sigma[j];
            rawAst += (0.5 * w.W222) * ha.Squared;
            rawComa += w.W131 * ha;
        }

        var ast = f.AstigmatismAt(h);
        var coma = f.ComaAt(h);
        double scale = Math.Abs(f.Totals.W222) + Math.Abs(f.Totals.W131);

        Assert.Equal(rawAst.X, ast.X, 10);
        Assert.Equal(rawAst.Y, ast.Y, 10);
        Assert.True((rawComa - coma).Magnitude < 1e-12 * scale);
    }

    private static NatField Field(string name)
    {
        var (sys, n, _) = Load(name);
        return FieldOf(sys, n);
    }

    private static NatField FieldOf(Core.Models.OpticalSystem sys, double[] n)
    {
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        return NatField.Compute(sys, n, p, seidel);
    }

    private static (Core.Models.OpticalSystem Sys, double[] N, ParaxialResult P) Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new System.Collections.Generic.List<string>());

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        return (sys, n, ParaxialTrace.Trace(sys, n, field));
    }
}
