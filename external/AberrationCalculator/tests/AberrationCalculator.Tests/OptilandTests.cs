using System;
using System.Collections.Generic;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Optiland;
using Xunit;
using Xunit.Abstractions;
using static AberrationCalculator.Tests.LowerOrderInversionTests;

namespace AberrationCalculator.Tests;

/// <summary>
/// The Optiland cross-check: every design built inside Optiland from this program's parsed
/// prescription, and asked for what Optiland has.
///
/// <para><b>What Optiland has.</b> Seidel sums (<c>optic.aberrations.seidels()</c>) and a real-ray
/// trace. It has no fifth-order coefficients - optiland 0.6.2, the current release, stops at third
/// order - so the fifth order is checked the way this repository checks everything it cannot get
/// from elsewhere: Optiland's rays are put through <see cref="CoefficientInversion"/>, and what
/// comes back is compared with Buchdahl's closed form. The rays are Optiland's; the reading of
/// them is this program's, and is gated on its own rays in <c>LowerOrderInversionTests</c>.</para>
///
/// <para>These tests need the embedded Python that <c>tools/setup-python.ps1</c> installs. A
/// fresh clone does not have it, so they are marked skipped, with the reason, rather than failing
/// or passing - so the summary cannot quietly count a check that never ran.</para>
/// </summary>
public class OptilandTests
{
    private readonly ITestOutputHelper _out;
    public OptilandTests(ITestOutputHelper output) { _out = output; }

    private static OptilandOptic Build(Loaded l) =>
        OptilandOptic.Build(l.System, l.Indices, l.Paraxial, l.Field, l.WavelengthUm);

    private static Loaded Load(string name) =>
        LowerOrderInversionTests.Load(name, name.EndsWith(".zmx") ? "coefficient-reference" : "lenses",
                                      CatalogLocator.LoadBundled());

    private static double[] Totals(SeidelResult s) =>
        new[] { s.TotalS1, s.TotalS2, s.TotalS3, s.TotalS4, s.TotalS5 };

    private static double[] Split(SeidelResult s, int term) => term switch
    {
        1 => s.S1, 2 => s.S2, 3 => s.S3, 4 => s.S4, _ => s.S5,
    };

    private static SeidelResult Seidel(Loaded l) =>
        SeidelCoefficients.Compute(l.System, l.Indices, l.Indices, l.Indices, l.Paraxial);

    // ── Seidel ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// On every design Optiland's Seidel analysis can be asked about fairly - object at
    /// infinity, and no even-asphere terms, which it leaves out (see below) - the five sums
    /// agree SURFACE BY SURFACE, once Optiland's sign is turned: it reports the negative of
    /// Welford's S1..S5, which is what this program and OpticStudio's Seidel analysis report.
    /// Conics are included, and so is the parabolic mirror.
    ///
    /// <para>And so is the flat face in collimated light, <c>Ladder2_FlatPlain</c>, which this
    /// sweep is what first caught: this program set that face's distortion to zero where A = 0,
    /// and Optiland did not. See <c>LowerOrderInversionTests</c> for why this program, and not
    /// Optiland, was the one that was wrong.</para>
    /// </summary>
    [OptilandFact]
    public void SeidelSumsAgreeSurfaceBySurfaceWithTheSignTurned()
    {
        var catalog = CatalogLocator.LoadBundled();
        int count = 0;
        foreach (var d in Designs.All())
        {
            var l = LowerOrderInversionTests.Load((string)d[0], (string)d[1], catalog);
            if (!l.Paraxial.InfiniteConjugate || OptilandOptic.Unsupported(l.System) != null) continue;
            if (l.System.Surfaces.Any(s => s.AsphericCoefficients.Any(a => a != 0.0))) continue;

            var mine = Seidel(l);
            var theirs = Build(l).Seidels();
            double largest = Totals(mine).Max(Math.Abs);

            double worst = 0.0;
            for (int t = 1; t <= 5; t++)
            {
                // The split is read from Optiland's private per-surface terms, so it must first
                // add up to the totals its public API returns.
                Assert.True(Math.Abs(theirs[t].Sum() - theirs.Totals[t - 1]) < 1e-12 * largest,
                    $"{l.Name}: Optiland's S{t} split does not add up to its own total");

                worst = Math.Max(worst, Math.Abs(theirs.Totals[t - 1] + Totals(mine)[t - 1]) / largest);
                for (int j = 0; j < l.System.Surfaces.Count; j++)
                    worst = Math.Max(worst, Math.Abs(theirs[t][j] + Split(mine, t)[j]) / largest);
            }
            _out.WriteLine($"{l.Name,-40} worst {worst:E1} of the largest sum");
            Assert.True(worst < 1e-10, $"{l.Name}: Seidel sums differ by {worst:E2} of the largest");
            count++;
        }
        Assert.True(count >= 12, $"only {count} designs were compared");
    }

    /// <summary>
    /// The sign, pinned on the plainest case there is: a positive singlet, whose spherical
    /// aberration is undercorrected. Welford's S1 is positive there, and so is OpticStudio's
    /// SPHA; Optiland's is negative.
    /// </summary>
    [OptilandFact]
    public void OptilandsSeidelSignIsTheOppositeOfWelfords()
    {
        var l = Load("Ladder1_Sphere.lhlt");
        Assert.True(l.Paraxial.Efl > 0);
        Assert.True(Seidel(l).TotalS1 > 0, "this program: S1 of a positive singlet is positive");
        Assert.True(Build(l).Seidels().Totals[0] < 0, "Optiland: the same S1 comes back negative");
    }

    /// <summary>
    /// Optiland's Seidel sums take the figuring from the conic alone. Its third-order surface
    /// term is <c>(n'-n) k c^3 y^a ybar^b</c>; the r^4 coefficient, which contributes to third
    /// order exactly as the conic does, never enters. So F3 - F1's conic singlet with r^4, r^6
    /// and r^8 added - gets F1's sums from Optiland to the last digit, where this program, whose
    /// third order on F3 agrees with the recorded FIFTHORD reference, finds S1 changed by more
    /// than half.
    /// </summary>
    [OptilandFact]
    public void OptilandsSeidelSumsLeaveOutTheEvenAsphereTerms()
    {
        var f1 = Load("F1_conic_singlet.zmx");
        var f3 = Load("F3_conic_a4_a6_a8.zmx");

        var theirs1 = Build(f1).Seidels().Totals;
        var theirs3 = Build(f3).Seidels().Totals;
        for (int t = 0; t < 5; t++)
            Assert.True(Math.Abs(theirs3[t] - theirs1[t]) < 1e-12 * Math.Abs(theirs1[0]),
                $"S{t + 1}: Optiland gives F3 {theirs3[t]:E6} and F1 {theirs1[t]:E6}");

        double mine1 = Seidel(f1).TotalS1, mine3 = Seidel(f3).TotalS1;
        _out.WriteLine($"S1: F1 {mine1:E6}, F3 {mine3:E6}; Optiland {-theirs1[0]:E6} for both (sign turned)");
        Assert.True(Math.Abs(mine3 - mine1) > 0.5 * Math.Abs(mine1));
    }

    /// <summary>
    /// Optiland's PARAXIAL trace takes a surface's power from its radius alone, so an r^2
    /// coefficient - which is a curvature change, c + 2 A2 - is lost there although its real-ray
    /// trace carries it. F8 is F3 with <c>PARM 1 = 1E-4</c>: OpticStudio, this program and
    /// BUCH7_ASPH all put its focal length at 77.419426, and Optiland at F3's 78.037505. F9, the
    /// same surface written as a shifted sphere, it gets right. This is the defect FIFTHORD has,
    /// and LensHH-LT had until 1.0.156.
    /// </summary>
    [OptilandFact]
    public void OptilandsParaxialTraceLeavesOutTheR2Term()
    {
        var f3 = Load("F3_conic_a4_a6_a8.zmx");
        var f8 = Load("F8_r2_conic_a4_a6_a8.zmx");
        var f9 = Load("F9_r2_as_shifted_sphere.zmx");

        Assert.Equal(77.419426, f8.Paraxial.Efl, 6);
        Assert.Equal(77.419426, Build(f9).Describe().Efl, 6);
        Assert.Equal(f3.Paraxial.Efl, Build(f8).Describe().Efl, 9);
        Assert.Equal(78.037505, Build(f8).Describe().Efl, 6);
    }

    /// <summary>
    /// At a finite conjugate Optiland's paraxial chief ray is wrong, and every Seidel sum that
    /// uses it with it. To find the chief ray it traces a unit ray backward from the stop and
    /// scales it to the object height; the backward trace stops at surface 1 and never makes the
    /// last transfer to the object, so the ray is scaled as if the object sat on the first
    /// surface. Its object-space slope comes out as height over EPL where it should be height
    /// over (object distance + EPL): on G0, 12 / 33.79 instead of 12 / 433.79. With the stop on
    /// the first surface EPL is zero, the scale divides by zero and every sum is NaN.
    /// </summary>
    [OptilandFact]
    public void OptilandsChiefRayAtAFiniteConjugateMissesTheObjectDistance()
    {
        var g0 = Load("G0_finite_no_r2.zmx");
        double h = g0.Field;
        double epl = g0.Paraxial.EntrancePupilPosition;
        double distance = g0.System.Surfaces[0].Thickness;

        Assert.Equal(h / (distance + epl), Math.Abs(g0.Paraxial.Ubar[0]), 12);
        Assert.Equal(h / epl, Math.Abs(Build(g0).Describe().ChiefSlope), 9);

        var e0 = Load("E0_finite_flat.zmx");
        Assert.True(e0.System.Surfaces[1].IsStop);
        Assert.All(Build(e0).Seidels().Totals, s => Assert.True(double.IsNaN(s)));
    }

    // ── Real rays, and the fifth order ─────────────────────────────────────────────────

    /// <summary>
    /// Optiland's rays land where this program's do, on every design both can trace: spheres,
    /// conics, r^2 to r^8 terms, a figured flat in collimated light, immersed image space.
    /// A grid over three fields, three pupil radii and three azimuths, launched from this
    /// program's paraxial pupil and caught at its paraxial focus. The worst, 1.5E-8 mm, is on a
    /// Cooke triplet with aspheres, where Optiland iterates to its surface; on spheres it is 1E-14.
    /// </summary>
    [OptilandFact]
    public void OptilandsRaysLandWhereOursDo()
    {
        int count = 0;
        foreach (var l in Invertible(CatalogLocator.LoadBundled()))
        {
            if (OptilandOptic.Unsupported(l.System) != null) continue;
            var requests = new List<CoefficientInversion.RayRequest>();
            foreach (double f in new[] { 0.0, 0.5 * l.Field, l.Field })
                foreach (double r in new[] { 0.0, 0.5, 1.0 })
                    foreach (double th in new[] { 0.0, 1.0, 2.0 })
                        requests.Add(new(f, r * Math.Cos(th), r * Math.Sin(th)));

            var theirs = Build(l).Trace(requests);
            double worst = 0.0;
            for (int k = 0; k < requests.Count; k++)
            {
                var mine = RealRayTrace.Trace(l.System, l.Indices, l.Paraxial,
                                              requests[k].FieldDeg, requests[k].Py, requests[k].Pz);
                Assert.True(mine.Ok && theirs[k].Ok, $"{l.Name}: ray {k} failed");
                worst = Math.Max(worst, Math.Max(Math.Abs(mine.Y - theirs[k].Y), Math.Abs(mine.Z - theirs[k].Z)));
            }
            _out.WriteLine($"{l.Name,-45} {worst:E1} mm");
            Assert.True(worst < 1e-7, $"{l.Name}: rays differ by {worst:E2} mm");
            count++;
        }
        Assert.True(count >= 30, $"only {count} designs were traced");
    }

    /// <summary>
    /// The third and fifth orders from Optiland's rays, against Buchdahl's closed form: all five
    /// third-order and all twelve fifth-order coefficients, on every design the inversion
    /// applies to - aspheric ones included, where Optiland's own Seidel analysis cannot be used.
    /// The bounds are those the inversion meets on this program's own rays; Optiland's rays
    /// meet them too, and on each design land within a few parts in a billion of the same place.
    /// </summary>
    [OptilandFact]
    public void ThirdAndFifthOrderFromOptilandsRaysMatchBuchdahl()
    {
        int count = 0;
        foreach (var l in Invertible(CatalogLocator.LoadBundled()))
        {
            if (OptilandOptic.Unsupported(l.System) != null) continue;
            var optic = Build(l);
            var b = BuchdahlCoefficients.Compute(l.System, l.Paraxial).Totals;
            var r = CoefficientInversion.InvertThirdAndFifth(l.System, l.Indices, l.Paraxial, l.Field,
                                                             rays: optic.Trace);
            Assert.NotNull(r);

            var (w3, n3) = Worst(CoefficientInversion.ThirdOrderNames, b, r!.Terms);
            var (w5, n5) = Worst(CoefficientInversion.FifthOrderNames, b, r.Terms);
            _out.WriteLine($"{l.Name,-45} third {w3:E1} ({n3})  fifth {w5:E1} ({n5})");
            Assert.True(w3 < 1e-7, $"{l.Name}: third order off by {w3:E2} of the largest, at {n3}");
            Assert.True(w5 < 3e-5, $"{l.Name}: fifth order off by {w5:E2} of the largest, at {n5}");
            count++;
        }
        Assert.True(count >= 30, $"only {count} designs were inverted");
    }

    /// <summary>The seventeen coefficients side by side on one design, for the record.</summary>
    [OptilandTheory]
    [InlineData("CookeTriplet.lhlt")]
    [InlineData("F3_conic_a4_a6_a8.zmx")]
    public void CoefficientTable(string name)
    {
        var l = Load(name);
        var optic = Build(l);
        var b = BuchdahlCoefficients.Compute(l.System, l.Paraxial).Totals;
        var r = CoefficientInversion.InvertThirdAndFifth(l.System, l.Indices, l.Paraxial, l.Field,
                                                         rays: optic.Trace)!;
        _out.WriteLine($"{name}, optiland {PythonSession.OptilandVersion}");
        _out.WriteLine($"{"",-5} {"Buchdahl",15} {"Optiland rays",15} {"diff",10}");
        foreach (var n in CoefficientInversion.ThirdOrderNames.Concat(CoefficientInversion.FifthOrderNames))
            _out.WriteLine($"{n,-5} {b[n],15:E8} {r.Terms[n],15:E8} {r.Terms[n] - b[n],10:E1}");
        Assert.True(r.ResidualFifth < 1e-3);
    }

    /// <summary>
    /// What the bridge will not build is refused rather than built as something else: figuring
    /// on the object or image surface, a tilt. Figured surfaces between them are built.
    /// </summary>
    [Fact]
    public void WhatTheBridgeCannotBuildIsRefused()
    {
        Assert.Null(OptilandOptic.Unsupported(Load("F3_conic_a4_a6_a8.zmx").System));
        Assert.Null(OptilandOptic.Unsupported(Load("F8_r2_conic_a4_a6_a8.zmx").System));
        Assert.NotNull(OptilandOptic.Unsupported(Load("Ec_image_curved_a4a6.zmx").System));
        Assert.NotNull(OptilandOptic.Unsupported(Load("Eg_object_curved_a4a6.zmx").System));

        var tilted = Load("CookeTriplet.lhlt").System;
        tilted.Surfaces[2].TiltX = 0.1;
        Assert.NotNull(OptilandOptic.Unsupported(tilted));
    }

    // ── Save-back ───────────────────────────────────────────────────────────────────────

    private static double Sag(AberrationCalculator.Core.Models.Surface s, double r)
    {
        double c = s.Curvature, r2 = r * r;
        double z = c * r2 / (1.0 + Math.Sqrt(1.0 - (1.0 + s.Conic) * c * c * r2));
        double p = r2;
        foreach (double a in s.AsphericCoefficients) { z += a * p; p *= r2; }
        return z;
    }

    /// <summary>
    /// An optimised design saved into Optiland .json is a file OPTILAND loads, with the figuring
    /// that was written. Reading it back with this program's own reader proves only that the
    /// writer and reader agree - and they agreed for years on coefficients one power too high.
    /// So Optiland reads it, and its sag at 6 mm is compared with the sag the design should
    /// have. Both routes: a sphere made an asphere (the double Gauss, as Optiland wrote it) and
    /// an even asphere edited (a singlet Optiland wrote).
    /// </summary>
    [OptilandTheory]
    [InlineData("KingslakeDG.json", 3)]
    [InlineData("AsphericSinglet.optiland.json", 1)]
    public void OptilandLoadsTheFiguringThisProgramSaved(string name, int surface)
    {

        var catalog = CatalogLocator.LoadBundled();
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "abcalc-opt-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            string lens = System.IO.Path.Combine(Fixtures.LensDir, name);
            var system = AberrationCalculator.Core.IO.LensFile.Read(lens, catalog);
            var s = system.Surfaces[surface];
            s.Conic = -0.8;
            s.AsphericCoefficients[1] = 2.0e-6;
            s.AsphericCoefficients[2] = -3.0e-9;

            string output = System.IO.Path.Combine(dir, "saved.json");
            AberrationCalculator.Core.IO.LensPatcher.Save(system, lens, output, catalog);

            var theirs = OptilandOptic.SagsFromFile(output, 6.0);
            _out.WriteLine($"{name} surface {surface}: Optiland reads {theirs[surface].Type}, "
                         + $"sag {theirs[surface].Sag:G15}; wanted {Sag(s, 6.0):G15}");
            Assert.Equal("EvenAsphere", theirs[surface].Type);
            Assert.True(Math.Abs(theirs[surface].Sag - Sag(s, 6.0)) < 1e-12 * Math.Abs(Sag(s, 6.0)) + 1e-14,
                "Optiland's sag of the saved surface is not the design's");

            // And the surfaces nothing touched are still what they were.
            for (int i = 1; i < system.Surfaces.Count - 1; i++)
                if (i != surface)
                    Assert.True(Math.Abs(theirs[i].Sag - Sag(system.Surfaces[i], 6.0)) < 1e-9,
                        $"surface {i} changed in Optiland's reading");
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { }
        }
    }
}

/// <summary>
/// A test that needs the embedded Python: skipped, with the setup hint as the reason, when it is
/// not there. xunit 2 decides skipping when it discovers the test, not while it runs.
/// </summary>
public sealed class OptilandFactAttribute : FactAttribute
{
    public OptilandFactAttribute()
    {
        if (!PythonEnvironment.IsReady) Skip = PythonEnvironment.SetupHint;
    }
}

/// <summary>The same, for a test run once per data row.</summary>
public sealed class OptilandTheoryAttribute : TheoryAttribute
{
    public OptilandTheoryAttribute()
    {
        if (!PythonEnvironment.IsReady) Skip = PythonEnvironment.SetupHint;
    }
}
