using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The asphere placement screen: which surface to figure, and what figuring it would buy.
///
/// <para><b>The screen is only worth having if its numbers are actionable</b>, so the test that
/// carries this file is not that the arithmetic matches a formula - it would, the formula is
/// three lines - but that the coefficient it PREDICTS will null a Seidel sum actually nulls it
/// when applied. That runs the prediction back through
/// <see cref="SeidelCoefficients"/>, which knows nothing about the screen, and is the difference
/// between a design aid and a restatement.</para>
/// </summary>
public class AspherePlacementTests
{
    private sealed record Setup(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                                SeidelResult Seidel, double Field);

    private static Setup Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var s = SeidelCoefficients.Compute(sys, n, n, n, p);
        return new Setup(sys, n, p, s, field);
    }

    /// <summary>Apply an r^4 coefficient to one surface and recompute from scratch.</summary>
    private static SeidelResult WithFiguring(string name, int surface, double a4)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var s = sys.Surfaces[surface];
        var a = new double[Math.Max(8, s.AsphericCoefficients.Length)];
        Array.Copy(s.AsphericCoefficients, a, s.AsphericCoefficients.Length);
        a[1] += a4;
        s.AsphericCoefficients = a;
        if (s.Type == SurfaceType.Standard) s.Type = SurfaceType.EvenAsphere;

        var p = ParaxialTrace.Trace(sys, n, field);
        return SeidelCoefficients.Compute(sys, n, n, n, p);
    }

    // ── The test that earns the screen ──────────────────────────────────────────────────

    /// <summary>
    /// The predicted coefficient nulls the sum it was predicted for. Checked on every surface of
    /// every design that has leverage, and against the program's own Seidel code rather than
    /// against the formula the prediction came from.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    [InlineData("Ladder1_Sphere")]
    public void ThePredictedCoefficientActuallyNullsTheSum(string name)
    {
        var s = Load(name);
        var rows = AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel);
        Assert.NotEmpty(rows);

        int checks = 0;
        foreach (var r in rows)
        {
            void Check(double a4, Func<SeidelResult, double> pick, double before, string what)
            {
                if (double.IsNaN(a4)) return;
                var after = WithFiguring(name, r.Surface, a4);
                double got = pick(after);
                // Against the size of what was removed, not an absolute floor: a sum of 1E-3
                // driven to 1E-18 and one of 1E+2 driven to 1E-13 are the same success.
                double tol = 1e-9 * Math.Max(Math.Abs(before), 1e-12);
                Assert.True(Math.Abs(got) <= tol,
                    $"{name} surface {r.Surface}: the screen said a4 = {a4:E6} would null {what}, "
                  + $"which stood at {before:E6}. Applying it left {got:E6}.");
                checks++;
            }

            Check(r.NullS1, x => x.TotalS1, s.Seidel.TotalS1, "S1");
            Check(r.NullS2, x => x.TotalS2, s.Seidel.TotalS2, "S2");
            Check(r.NullS3, x => x.TotalS3, s.Seidel.TotalS3, "S3");
            Check(r.NullS5, x => x.TotalS5, s.Seidel.TotalS5, "S5");
        }

        Assert.True(checks >= 4,
            $"{name}: only {checks} predictions were testable, so this proved almost nothing.");
    }

    /// <summary>
    /// Petzval cannot be reached by figuring, which is the one structural claim the screen makes
    /// and the reason S4 has no column. Schulz says the same in words; this says it in numbers.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void NoAmountOfFiguringMovesPetzval(string name)
    {
        var s = Load(name);
        foreach (var r in AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel))
        {
            var after = WithFiguring(name, r.Surface, 1e-5);
            Assert.True(after.TotalS4.Equals(s.Seidel.TotalS4),
                $"{name}: figuring surface {r.Surface} moved Petzval from {s.Seidel.TotalS4:E17} "
              + $"to {after.TotalS4:E17}. It depends on the curvatures and the index steps alone.");
        }
    }

    /// <summary>
    /// The four sensitivities stand in the ratios 1 : H/h : (H/h)^2 : (H/h)^3, which is the whole
    /// content of the Delano reading and the reason one number decides what a surface can reach.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void TheSensitivitiesFollowThePowersOfTheRatio(string name)
    {
        var s = Load(name);
        foreach (var r in AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel))
        {
            if (r.AtImage || Math.Abs(r.DS1) < 1e-30) continue;
            Assert.Equal(r.Ratio, r.DS2 / r.DS1, 10);
            Assert.Equal(r.Ratio * r.Ratio, r.DS3 / r.DS1, 10);
            Assert.Equal(r.Ratio * r.Ratio * r.Ratio, r.DS5 / r.DS1, 10);
        }
    }

    /// <summary>
    /// A conic of K is an r^4 coefficient of K c^3 / 8, so the conic column must predict the same
    /// physical surface. Applied as a conic, the prediction must null the same sum.
    /// </summary>
    [Fact]
    public void TheConicColumnAgreesWithTheCoefficientColumn()
    {
        var s = Load("CookeTriplet");
        foreach (var r in AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel))
        {
            if (double.IsNaN(r.NullS1) || Math.Abs(r.DS1Conic) < 1e-30) continue;

            double conic = -s.Seidel.TotalS1 / r.DS1Conic;
            double c = s.System.Surfaces[r.Surface].VertexCurvature;
            double asA4 = conic * c * c * c / 8.0;

            Assert.Equal(r.NullS1, asA4, 10);
        }
    }

    /// <summary>
    /// The impact table is exact. Whatever state it predicts after a figure is applied must be
    /// the state <see cref="SeidelCoefficients"/> computes when that figure is actually applied -
    /// all four sums, not just the one being nulled.
    ///
    /// <para>This is the test that earns the "what each choice buys" block. Without it the
    /// collateral columns would be a plausible-looking extrapolation, and the whole point of
    /// them is that a designer can act on them.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void ThePredictedStateAfterAFigureIsTheRealOne(string name)
    {
        var s = Load(name);
        int checks = 0;

        foreach (var r in AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel))
        {
            foreach (var (coef, ok) in new[]
                     {
                         (r.NullS1, r.CanReachS1), (r.NullS2, r.CanReachS2),
                         (r.NullS3, r.CanReachS3), (r.NullS5, r.CanReachS5),
                     })
            {
                if (!ok || double.IsNaN(coef)) continue;

                var predicted = AspherePlacement.After(r, coef, s.Seidel);
                var actual = WithFiguring(name, r.Surface, coef);

                void Same(double p, double a, string which)
                {
                    double scale = Math.Max(Math.Abs(a), Math.Abs(s.Seidel.TotalS1));
                    Assert.True(Math.Abs(p - a) <= 1e-9 * scale,
                        $"{name} surface {r.Surface}: the impact table predicted {which} = "
                      + $"{p:E8} after the figure; applying it gave {a:E8}.");
                }
                Same(predicted.S1, actual.TotalS1, "S1");
                Same(predicted.S2, actual.TotalS2, "S2");
                Same(predicted.S3, actual.TotalS3, "S3");
                Same(predicted.S5, actual.TotalS5, "S5");
                checks++;
            }
        }

        Assert.True(checks >= 4, $"{name}: only {checks} states were checked.");
    }

    /// <summary>
    /// A surface at the stop has no leverage on coma, astigmatism or distortion, and must be
    /// REFUSED for them rather than answered with a huge number.
    ///
    /// <para>This is the defect the sag column exists to close. The chief-ray height at the stop
    /// is zero only to within the rounding of the paraxial trace, so the leverage is tiny rather
    /// than exactly zero and the coefficient that would null those sums came out finite - 3.5E+09
    /// on the Cooke triplet, printed without comment in a column of microns. The guard is now on
    /// the sag, and a figure deeper than the surface is wide is refused.</para>
    /// </summary>
    [Fact]
    public void ASurfaceAtTheStopIsRefusedForTheFieldAberrations()
    {
        var s = Load("CookeTriplet");
        var rows = AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel);

        AsphereCandidate? atStop = null;
        foreach (var r in rows)
            if (atStop == null || Math.Abs(r.Ratio) < Math.Abs(atStop.Ratio)) atStop = r;

        Assert.NotNull(atStop);
        Assert.True(Math.Abs(atStop!.Ratio) < 1e-6,
            $"expected a surface with H/h at zero; the smallest was {atStop.Ratio:E3}");

        Assert.True(atStop.CanReachS1, "a surface at the stop still reaches spherical aberration");
        Assert.False(atStop.CanReachS2, "coma should be out of reach from the stop");
        Assert.False(atStop.CanReachS3, "astigmatism should be out of reach from the stop");
        Assert.False(atStop.CanReachS5, "distortion should be out of reach from the stop");
    }

    /// <summary>
    /// Everything the screen says is reachable must be a figure a surface could carry - the sag
    /// no deeper than the surface is wide - and the sag must be the coefficient times the fourth
    /// power of that radius, so the two tables describe one surface.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void ReachableMeansAFigureTheSurfaceCouldCarry(string name)
    {
        var s = Load(name);
        foreach (var r in AspherePlacement.Screen(s.System, s.Indices, s.Paraxial, s.Seidel))
        {
            Assert.True(r.ApertureRadius > 0.0, $"surface {r.Surface} has no aperture to judge at");
            double r4 = Math.Pow(r.ApertureRadius, 4);

            void Check(double coefficient, double sag, bool reach, string what)
            {
                if (!double.IsNaN(coefficient))
                    Assert.Equal(coefficient * r4, sag, 12);
                if (reach)
                    Assert.True(Math.Abs(sag) <= r.ApertureRadius,
                        $"{name} surface {r.Surface}: {what} is called reachable at {sag:E3} of "
                      + $"sag on a semi-aperture of {r.ApertureRadius:F4}.");
            }
            Check(r.NullS1, r.SagS1, r.CanReachS1, "S1");
            Check(r.NullS2, r.SagS2, r.CanReachS2, "S2");
            Check(r.NullS3, r.SagS3, r.CanReachS3, "S3");
            Check(r.NullS5, r.SagS5, r.CanReachS5, "S5");
        }
    }

    /// <summary>
    /// The screen renders, names Schulz, and says the two things a reader must not miss: that
    /// Petzval is out of reach, and that this is the third order only.
    /// </summary>
    [Fact]
    public void TheRenderedScreenSaysWhatItCannotDo()
    {
        var s = Load("CookeTriplet");
        string text = AspherePlacement.Render(s.System, s.Indices, s.Paraxial, s.Seidel);

        Assert.Contains("WHERE AN ASPHERE WOULD ACT", text, StringComparison.Ordinal);
        Assert.Contains("Schulz", text, StringComparison.Ordinal);
        Assert.Contains("PETZVAL IS ABSENT", text, StringComparison.Ordinal);
        Assert.Contains("SAG null S1", text, StringComparison.Ordinal);
        Assert.Contains("A4 null S1", text, StringComparison.Ordinal);
        Assert.Contains("worst side effect", text, StringComparison.Ordinal);
        Assert.Contains("OUT OF REACH", text, StringComparison.Ordinal);
        Assert.Contains("CHEAPEST SURFACE FOR EACH SUM", text, StringComparison.Ordinal);
        Assert.Contains("THE TERM APPLIED IS r^4 AND ONLY r^4", text, StringComparison.Ordinal);
        Assert.Contains("r^2 IS NOT TOUCHED", text, StringComparison.Ordinal);
        Assert.Contains("WHAT EACH CHOICE BUYS", text, StringComparison.Ordinal);
        Assert.Contains("THIRD ORDER", text, StringComparison.Ordinal);
    }
}
