using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl's computing scheme, checked against numbers Buchdahl himself published.
///
/// He works the same triplet through twice: in TABLE II of Chapter VI of the monograph
/// (<i>Optical Aberration Coefficients</i>, Oxford, 1954; Dover 1968, pp. 61-65), where he
/// prints every numbered entry t1..t108 for all six surfaces, and again in <i>J. Opt. Soc.
/// Am.</i> <b>48</b>, 747 (1958), which states its constitution on p.753. So these are
/// tests against the source rather than against another implementation.
///
/// That distinction is not academic here. The scheme is a long chain of numbered entries
/// with no individual physical meaning, so an error in the middle produces plausible
/// output that nothing else would catch - and six of the twelve secondary coefficients
/// were in fact wrong for exactly that reason, agreeing perfectly with the C++ they were
/// ported from and with nothing else.
/// </summary>
public class BuchdahlSchemeTests
{
    /// <summary>
    /// Buchdahl's triplet, in millimetres at focal length 50 - the form the prescription
    /// was supplied in, and the one that reproduces his published entrance pupil.
    ///
    /// <para>The plano surface at index 5 is not a dummy in any dispensable sense: it IS
    /// Buchdahl's stop, which lies between two refracting surfaces. Marking it as the stop
    /// and taking the ray-height ratio there gives his published p = 0.113227 directly.
    ///
    /// An earlier version of this fixture flagged surface 4 and still produced the right
    /// number, because the formula it was read with propagated both rays forward to the
    /// next surface before taking the ratio - so the stop was flagged one surface early and
    /// measured one surface late, and the two cancelled. That coincidence held only for
    /// this file.</para>
    /// </summary>
    private static (List<Surface> Surfaces, double[] Indices) Triplet()
    {
        double[] c = { 0, 0.09648784, -0.01507852, -0.03290101, 0.10235886, 0.0, 0.00621452, -0.02922327, 0 };
        double[] d = { 0, 2.013900, 0.842550, 0.480725, 2.067650, 4.869250, 1.566230, 41.800027, 0 };
        double[] n = { 1.0, 1.6162, 1.0, 1.5725, 1.0, 1.0, 1.6162, 1.0, 1.0 };

        var surfaces = new List<Surface>();
        for (int i = 0; i < c.Length; i++)
            surfaces.Add(new Surface { Curvature = c[i], Thickness = d[i] });
        // The stop is the PLANO SURFACE AT INDEX 5, not the refracting surface before it.
        // Buchdahl's stop lies between two refracting surfaces, and this file encodes that
        // plane as a dummy. Flagging surface 4 instead only appeared to work because the
        // formula it was read with propagated one surface forward.
        surfaces[5].IsStop = true;
        surfaces[5].SemiDiameter = 3.91358;

        return (surfaces, n);
    }

    /// <summary>
    /// The prescription is Buchdahl's. He gives curvatures in units of the focal length,
    /// and c1 = 4.82439; scaling this file's first curvature by 50 must reproduce it. If
    /// this fails, the fixture is not his lens and every other test here is meaningless.
    /// </summary>
    [Fact]
    public void TheFixtureIsBuchdahlsOwnTriplet()
    {
        var (surfaces, _) = Triplet();

        Assert.Equal(4.82439, surfaces[1].Curvature * 50.0, 4);
        Assert.Equal(-0.753929, surfaces[2].Curvature * 50.0, 5);
        Assert.Equal(5.11794, surfaces[4].Curvature * 50.0, 4);

        // His k = n/n' per surface: 1/1.6162 = 0.618735, 1/1.5725 = 0.635930.
        Assert.Equal(0.618735, 1.0 / 1.6162, 6);
        Assert.Equal(0.635930, 1.0 / 1.5725, 6);

        // And his separations, with the dummy collapsed: d5 = 0.138738.
        Assert.Equal(0.138738, (surfaces[4].Thickness + surfaces[5].Thickness) / 50.0, 6);
    }

    /// <summary>
    /// "Position of entrance pupil, p = 0.113227" - p.753, stated outright. This is the
    /// first published quantity the scheme produces, so it catches an error in the p/q ray
    /// trace before anything downstream.
    /// </summary>
    [Fact]
    public void TheStopParameterMatchesBuchdahlsPublishedValue()
    {
        var (surfaces, n) = Triplet();
        var r = BuchdahlScheme.Compute(surfaces, n, efl: 50.0, stopRadius: 3.91358);

        Assert.True(r.Evaluated, "no stop was found");

        // He prints six figures; we compute 0.1132276. Agreement to the last printed
        // digit is all the published value can support.
        Assert.Equal(0.113227, r.P, 5);
    }

    /// <summary>The primary coefficient a_p, against the sum column of his Table I.</summary>
    [Fact]
    public void ThePrimaryCoefficientMatchesTableI()
    {
        var (surfaces, n) = Triplet();
        var r = BuchdahlScheme.Compute(surfaces, n, efl: 50.0);

        Assert.Equal(1.35914, r.Ap, 4);
    }

    /// <summary>
    /// The secondary coefficient S1p, also from Table I's sum column. With the test above
    /// this spans the primary and secondary halves, which different parts of the scheme
    /// compute.
    /// </summary>
    [Fact]
    public void TheSecondaryCoefficientMatchesTableI()
    {
        var (surfaces, n) = Triplet();
        var r = BuchdahlScheme.Compute(surfaces, n, efl: 50.0);

        // Five significant figures printed, so compare relatively rather than absolutely.
        Assert.Equal(-90.923, r.S1p, 1);
        Assert.True(System.Math.Abs((r.S1p + 90.923) / 90.923) < 5e-5,
            $"S1p = {r.S1p}, published -90.923");
    }

    /// <summary>
    /// Every coefficient the scheme produces, against the sum column Buchdahl prints for
    /// this triplet in TABLE II of Chapter VI of the monograph (pp. 61-65).
    ///
    /// <para>This test used to compare against the C++ this was ported from, on the
    /// understanding that the C++ reproduced Buchdahl. Six of the twelve secondary
    /// coefficients did not: s2p, s3p, s4p, s5p, s6p and s5p-bar were all wrong, because
    /// t100..t108 were wrong, and the port carried the error over faithfully. Comparing
    /// against another implementation cannot find that; comparing against the source can,
    /// which is why the values below are Buchdahl's and not ours.</para>
    ///
    /// <para>The tolerance is one part in a thousand, set by his printing rather than by
    /// us. Several of these sums cancel hard - s3p is 18.3 + 31.5 - 34.5 - 16.6 + 0.19 +
    /// 0.46, six numbers of order ten collapsing to -0.66 - so five significant figures per
    /// column supports only three or so in the total. That is still far tighter than the
    /// errors this test exists to catch: the six wrong coefficients were out by factors of
    /// three to thirty, not by tenths of a percent.</para>
    /// </summary>
    [Theory]
    // Primary, from t46..t51 - a_p, b_p, c_p, a_q, b_q, c_q.
    [InlineData("Ap", 1.3592)] [InlineData("Bp", -0.33713)] [InlineData("Cp", 0.17495)]
    [InlineData("Aq", -0.66858)] [InlineData("Bq", -0.909237)] [InlineData("Cq", 0.042272)]
    // Secondary, unbarred.
    [InlineData("S1p", -90.921)] [InlineData("S2p", -50.960)] [InlineData("S3p", -0.6578)]
    [InlineData("S4p", 3.0157)] [InlineData("S5p", 3.4514)] [InlineData("S6p", -0.70824)]
    // Secondary, barred.
    [InlineData("S1pBar", -12.8773)] [InlineData("S2pBar", 4.3946)] [InlineData("S3pBar", 1.53762)]
    [InlineData("S4pBar", 1.70641)] [InlineData("S5pBar", -0.83821)] [InlineData("S6pBar", 0.02713)]
    public void EveryCoefficientMatchesTheMonographsPrintedSum(string name, double published)
    {
        var (surfaces, n) = Triplet();
        var r = BuchdahlScheme.Compute(surfaces, n, efl: 50.0, stopRadius: 3.91358);

        double computed = typeof(BuchdahlSchemeResult).GetField(name)!.GetValue(r) switch
        {
            double d => d,
            _ => throw new System.InvalidOperationException($"{name} is not a double"),
        };

        double error = System.Math.Abs((computed - published) / published);
        Assert.True(error < 1e-3, $"{name}: computed {computed:G8}, published {published}, "
                                + $"relative error {error:E2}");
    }

    /// <summary>The entrance pupil the p and q rays place, at the published stop radius.</summary>
    [Fact]
    public void TheEntrancePupilFollowsFromTheRayPair()
    {
        var (surfaces, n) = Triplet();
        var r = BuchdahlScheme.Compute(surfaces, n, efl: 50.0, stopRadius: 3.91358);

        Assert.Equal(0.113227601, r.P, 9);
        Assert.Equal(4.608563998, r.EntrancePupilRadius, 8);
    }

    /// <summary>
    /// Curvatures scale with the focal length and separations against it, so the same lens
    /// expressed in different units must give the same coefficients. This is the property
    /// that let the supplied file be recognised as Buchdahl's own.
    /// </summary>
    [Fact]
    public void TheSchemeIsInvariantToTheFocalLengthScaling()
    {
        var (mm, n) = Triplet();
        var unit = new List<Surface>();
        foreach (var s in mm)
            unit.Add(new Surface
            {
                Curvature = s.Curvature * 50.0,
                Thickness = s.Thickness / 50.0,
                IsStop = s.IsStop,
                SemiDiameter = s.SemiDiameter / 50.0,
            });

        var a = BuchdahlScheme.Compute(mm, n, efl: 50.0);
        var b = BuchdahlScheme.Compute(unit, n, efl: 1.0);

        Assert.Equal(a.P, b.P, 9);
        Assert.Equal(a.Ap, b.Ap, 9);
        Assert.Equal(a.S1p, b.S1p, 6);
    }

    /// <summary>A system with too few surfaces returns an unevaluated result, not a crash.</summary>
    [Fact]
    public void ADegenerateSystemIsHandled()
    {
        var r = BuchdahlScheme.Compute(new List<Surface> { new(), new() }, new[] { 1.0, 1.0 }, 1.0);
        Assert.False(r.Evaluated);
    }
}
