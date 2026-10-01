using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Models;

using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// The ninth-order spherical coefficient against the table Buchdahl printed for it.
///
/// <para>Paper IV computes the whole thing for the triplet <c>Sigma1</c> - the same system paper
/// III is checked against elsewhere in this suite - and prints not only the answer but the six
/// intermediate <c>r</c> rows. That is what makes this an unusually good acceptance test: a
/// wrong transcription fails at a NAMED ROW rather than at a total, so the diagnosis is the test
/// result rather than a bisection afterwards.</para>
///
/// <para><b>The tolerance is one part in a thousand, which is what the PRINTING supports, not
/// what the arithmetic achieves.</b> Buchdahl gives six significant figures at most and several
/// of these entries carry only three - r5 at surface 5 is printed "0.0_7 688" - so no test
/// against them can ask for more than the last digit he set. Where he prints six, agreement is
/// measured at a few parts in a MILLION: the system total is -172972 against his -172968, and the
/// per-surface totals at surfaces 1, 2, 3 and 6 all land inside 1E-5. A regression that left the
/// suite green would have to move a well-printed entry by a hundredfold more than the
/// transcription currently misses it by.</para>
///
/// <para><b>Buchdahl writes repeated zeros as a subscript, and it costs a decade if misread.</b>
/// "0.0_9 58496" is "0." followed by NINE zeros - 5.8496E-10, not E-9. The first run of this test
/// failed on exactly that, at the one surface where the coefficient is small enough for the
/// notation to be used.</para>
/// </summary>
public class QuaternarySphericalTests
{
    private readonly ITestOutputHelper _out;
    public QuaternarySphericalTests(ITestOutputHelper o) { _out = o; }

    /// <summary>
    /// Buchdahl's Sigma1, at unit focal length. The same prescription as
    /// <c>BuchdahlTableITests.Triplet</c>; kept here rather than shared because this file is the
    /// acceptance test for a DIFFERENT published table, and a fixture that two papers' tables
    /// depend on should not be able to drift under one of them silently.
    /// </summary>
    private static (List<Surface> Surfaces, double[] Indices) Sigma1()
    {
        double[] c = { 0, 4.82439, -0.753929, -1.64505, 5.11794, 0.310726, -1.46116, 0 };
        double[] dBefore = { 0, 0, 0.040278, 0.016851, 0.0096145, 0.138738, 0.0313246, 0.836 };
        double[] n = { 1.0, 1.6162, 1.0, 1.5725, 1.0, 1.6162, 1.0, 1.0 };

        var s = new List<Surface>();
        for (int i = 0; i < c.Length; i++) s.Add(new Surface { Curvature = c[i] });
        for (int i = 0; i + 1 < c.Length; i++) s[i].Thickness = dBefore[i + 1];

        return (s, n);
    }

    private static QuaternaryResult Run()
    {
        var (surfaces, indices) = Sigma1();
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl: 1.0, stopParameter: 0.113227);
        return QuaternarySpherical.Compute(rows, surfaces.Count - 2);
    }

    /// <summary>
    /// The six intermediate rows, surface by surface, exactly as paper IV Table I prints them.
    ///
    /// <para>Zeros are Buchdahl's own: at surface 1 nothing has been accumulated, so r1, r2 and
    /// r3 vanish identically rather than approximately.</para>
    /// </summary>
    public static IEnumerable<object[]> PublishedRows()
    {
        //                      surface  r1         r2         r3         r4          r5          r6
        yield return new object[] { 1,        0.0,       0.0,       0.0,   11.2023,   -185.446,   4440.73 };
        yield return new object[] { 2,   2779.35,  -261.407,   1740.40,   36.1363,    4.27033,  -205.131 };
        yield return new object[] { 3,   19229.7,   3208.50,   4745.57,   53.0520,    777.684,     174.4 };
        yield return new object[] { 4,   -2220.5,   -919.44,    1933.2,   5.04201,    163.874,   2748.03 };
        yield return new object[] { 5,   -4628.0,   -636.59,     32705, 4.2964E-7,   6.88E-8, -1.2810E-6 };
        yield return new object[] { 6,   -4627.8,   -637.75,   -1114.1,   0.455970,   -1.91905,  32.9673 };
    }

    [Theory]
    [MemberData(nameof(PublishedRows))]
    public void TheIntermediateRowsMatchPaperFour(
        int surface, double r1, double r2, double r3, double r4, double r5, double r6)
    {
        var q = Run().Rows[surface];

        Near($"r1 at {surface}", r1, q.R1);
        Near($"r2 at {surface}", r2, q.R2);
        Near($"r3 at {surface}", r3, q.T1Dagger);
        Near($"r4 at {surface}", r4, q.R4);
        Near($"r5 at {surface}", r5, q.R5);
        Near($"r6 at {surface}", r6, q.R6);
    }

    /// <summary>The intrinsic coefficient of Eq. (2.15), per surface.</summary>
    [Fact]
    public void TheIntrinsicCoefficientMatchesPaperFour()
    {
        var q = Run();
        double[] published = { 0, 47784.3, 17694.4, -2845.4, -5515.56, 5.8496E-10, 66.7755 };

        for (int i = 1; i <= 6; i++) Near($"intrinsic at {i}", published[i], q.Rows[i].Intrinsic);
    }

    /// <summary>
    /// The total contribution of Eq. (3.1), per surface, and the system coefficient.
    ///
    /// <para>Buchdahl's own remark on these numbers is worth keeping with them: the contributions
    /// "will be seen to be very large, as is the final balance ... to be expected of so poorly
    /// corrected a system", and in a system meant for f/2 one would aim at individual
    /// contributions of at most order 1000 at unit focal length.</para>
    /// </summary>
    [Fact]
    public void TheTotalContributionsAndTheSystemCoefficientMatchPaperFour()
    {
        var q = Run();
        double[] published = { 0, 47784.3, 466677, -605337, -75626, 18.593, -6484.9 };

        for (int i = 1; i <= 6; i++) Near($"total at {i}", published[i], q.Rows[i].Total);

        Near("system total", -172968, q.Total, 1e-3);
    }

    /// <summary>
    /// At surface 1 nothing has been accumulated, so the total must BE the intrinsic - which is
    /// what Buchdahl's two rows show, both 47784.3. It is the cheapest statement that the induced
    /// machinery is wired to the accumulations and not to something that is nonzero everywhere.
    /// </summary>
    [Fact]
    public void TheFirstSurfaceHasNothingInduced()
    {
        var q = Run().Rows[1];
        Assert.Equal(q.Intrinsic, q.Total, 9);
    }

    private void Near(string what, double published, double computed, double rel = 1e-3)
    {
        double scale = Math.Abs(published);
        if (scale < 1e-30)
        {
            Assert.True(Math.Abs(computed) < 1e-9, $"{what}: {computed:G6} should vanish");
            return;
        }

        double error = Math.Abs(computed - published) / scale;
        _out.WriteLine($"{what,-18} published {published,14:G6}  computed {computed,14:G6}  " +
                       $"rel {error:E2}");
        Assert.True(error < rel,
            $"{what}: published {published:G6}, computed {computed:G6}, relative {error:E3}");
    }
}
