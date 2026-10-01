using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Forbes' series trace against real rays.
///
/// <para><b>How this is checked, and why not by a tolerance.</b> The trace is a truncated
/// expansion, so it does not agree with a real ray exactly and no fixed tolerance would say
/// anything about whether it is right. What it must do is converge at the correct ORDER. Keeping
/// the invariants to degree three keeps the ray coordinates to order seven, and the first term
/// omitted is order nine — symmetry removes the even orders — so scaling a ray down by <c>s</c>
/// must shrink the disagreement like <c>s^9</c>. Halving <c>s</c> must divide the error by about
/// 512. A trace with an error at fifth or seventh order would still look accurate at small
/// <c>s</c> but would converge at the wrong rate, and that is what these tests would catch.</para>
///
/// <para>The comparison is against <see cref="RealRayTrace"/> rather than against a scalar copy
/// of the same equations: a second implementation of my own algebra would share any error in it.
/// That tracer is exact, is what <c>CoefficientInversion</c> is built on, and is independent of
/// everything in the Forbes namespace.</para>
/// </summary>
public class ForbesTraceTests
{
    private const int D = 3;

    private sealed record Setup(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                                List<double[]> Figures, List<double> Regions);

    private static Setup Load(string name, int degree = D)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var paraxial = ParaxialTrace.Trace(sys, n, field);

        int last = sys.LastOpticalSurface();

        // The input base plane is surface 1's own vertex plane, which is where RealRayTrace
        // starts its rays, so the two are launched from the same place by construction.
        var figures = new List<double[]> { new double[] { 0.0 } };
        double z = 0.0;
        for (int i = 1; i <= last; i++)
        {
            var s = sys.Surfaces[i];
            figures.Add(ForbesTrace.Figure(z, s.Curvature, s.Conic, s.AsphericCoefficients, degree));
            z += s.Thickness;
        }
        figures.Add(new double[] { z });          // the output base plane, caught not refracted

        var regions = new List<double>();
        for (int i = 0; i <= last; i++) regions.Add(i < n.Length ? n[i] : 1.0);

        return new Setup(sys, n, paraxial, figures, regions);
    }

    /// <summary>The ray RealRayTrace launches, expressed at surface 1's vertex plane.</summary>
    private static (double Ym, double Ys, double Bm, double Bs) LaunchedRay(
        Setup s, double fieldDeg, double py, double pz)
    {
        double epr = 0.5 * s.Paraxial.Epd;
        double a = fieldDeg * Math.PI / 180.0;
        double bm = Math.Sin(a), bs = 0.0, dz = Math.Cos(a);
        double back = -s.Paraxial.EntrancePupilPosition / dz;
        double ym = py * epr + back * bm;
        double ys = pz * epr + back * bs;
        return (ym, ys, bm, bs);
    }

    private static double Disagreement(Setup s, ForbesTrace trace,
                                       double fieldDeg, double py, double pz)
    {
        var (ym, ys, bm, bs) = LaunchedRay(s, fieldDeg, py, pz);
        var predicted = trace.Predict(ym, ys, bm, bs);
        var landing = RealRayTrace.Trace(s.System, s.Indices, s.Paraxial, fieldDeg, py, pz,
                                         atParaxialFocus: false);
        Assert.True(landing.Ok, "the real ray failed to reach the image plane");
        return Math.Sqrt((predicted.Meridional - landing.Y) * (predicted.Meridional - landing.Y)
                       + (predicted.Sagittal - landing.Z) * (predicted.Sagittal - landing.Z));
    }

    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("TertiaryTestbed_Triplet24")]
    public void ConvergesAtNinthOrderOnSphericalSystems(string fixtureName)
    {
        var s = Load(fixtureName);
        var trace = ForbesTrace.Run(s.Figures, s.Regions, D);

        // A skew ray, so that all three invariants are live rather than only p and u.
        const double fieldDeg = 3.0, py = 0.8, pz = 0.5;

        double previous = double.NaN;
        var report = new StringBuilder();
        foreach (double scale in new[] { 1.0, 0.5, 0.25 })
        {
            double e = Disagreement(s, trace, fieldDeg * scale, py * scale, pz * scale);
            report.AppendLine($"    scale {scale,5}  disagreement {e:E4}");
            if (!double.IsNaN(previous))
            {
                double ratio = previous / e;
                report.AppendLine($"        fell by {ratio:F1} against the 512 of ninth order");
                Assert.True(ratio > 200.0 && ratio < 1400.0,
                    $"{fixtureName}: halving the ray divided the disagreement by {ratio:F1}, not " +
                    $"by about 512. The series is not accurate to seventh order.\n{report}");
            }
            previous = e;
        }
    }

    /// <summary>
    /// The control the order test needs: at degree two the trace keeps only to fifth order, so
    /// the disagreement must fall by about 128 rather than 512. Without this, a trace that had
    /// simply stopped changing could pass the test above.
    /// </summary>
    [Fact]
    public void ConvergesAtSeventhOrderWhenTruncatedOneDegreeLower()
    {
        var s = Load("CookeTriplet", degree: 2);
        var trace = ForbesTrace.Run(s.Figures, s.Regions, 2);

        double e1 = Disagreement(s, trace, 3.0, 0.8, 0.5);
        double e2 = Disagreement(s, trace, 1.5, 0.4, 0.25);
        double ratio = e1 / e2;

        Assert.True(ratio > 60.0 && ratio < 300.0,
            $"at degree two the disagreement should fall by about 128 when the ray is halved, " +
            $"and it fell by {ratio:F1}. Either the truncation is not doing what it claims or " +
            "the trace does not depend on it.");
    }

    /// <summary>On axis with no field the trace must reproduce the paraxial image height exactly
    /// in its constant term: a ray through the vertex goes to the axis.</summary>
    [Fact]
    public void TheConstantTermIsTheParaxialTransfer()
    {
        var s = Load("CookeTriplet");
        var trace = ForbesTrace.Run(s.Figures, s.Regions, D);

        // With p = k = u = 0 the ray is the axial one; S and T reduce to the paraxial ray
        // transfer between the two base planes, so a ray leaving the axis at zero height and
        // zero angle stays on the axis.
        var predicted = trace.Predict(0.0, 0.0, 0.0, 0.0);
        Assert.Equal(0.0, predicted.Meridional, 15);
        Assert.Equal(0.0, predicted.Sagittal, 15);

        // And the constant terms are finite and not degenerate.
        Assert.True(Math.Abs(trace.T[0, 0, 0]) > 1e-9,
            "the constant term of T is zero, so the trace has no paraxial transfer at all");
    }

    /// <summary>
    /// The degree the trace is run at must not change the answer at lower degrees. This is the
    /// check that found the one real defect in the trace: every coefficient below the top agreed
    /// between runs, and only the top degree of each run was wrong, which said that some
    /// intermediate was one degree short rather than that the physics was wrong. It was df/dp,
    /// which enters the refracted direction with nothing of degree one or more in front of it.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder2_Sphere")]
    public void RunningAtAHigherDegreeDoesNotChangeTheLowerCoefficients(string fixtureName)
    {
        var lo = Load(fixtureName, degree: 3);
        var hi = Load(fixtureName, degree: 4);
        var a = ForbesTrace.Run(lo.Figures, lo.Regions, 3);
        var b = ForbesTrace.Run(hi.Figures, hi.Regions, 4);

        for (int total = 0; total <= 3; total++)
            for (int i = 0; i <= total; i++)
                for (int j = 0; i + j <= total; j++)
                {
                    int k = total - i - j;
                    if (k < 0) continue;
                    foreach (var (name, x, y) in new[]
                             { ("S", a.S[i, j, k], b.S[i, j, k]), ("T", a.T[i, j, k], b.T[i, j, k]),
                               ("V", a.V[i, j, k], b.V[i, j, k]), ("W", a.W[i, j, k], b.W[i, j, k]) })
                        Assert.True(Math.Abs(x - y) < 1e-11 * (1 + Math.Abs(y)),
                            $"{fixtureName}: {name} coefficient of p^{i} k^{j} u^{k} is {x:E10} at " +
                            $"degree 3 and {y:E10} at degree 4. A truncation that changes the " +
                            "coefficients below it is losing a term, not merely dropping one.");
                }
    }

    [Fact]
    public void FigureReproducesTheSurfaceSagItIsBuiltFrom()
    {
        // A conic with an r^4 term, which is the case the aspheric work turns on.
        var surface = new Surface { Curvature = 0.02, Conic = -0.6, Thickness = 4 };
        surface.AsphericCoefficients[1] = 1e-6;

        var f = ForbesTrace.Figure(0.0, surface.Curvature, surface.Conic,
                                   surface.AsphericCoefficients, 6);

        // Evaluate the series in p against Surface.Sag(r) at a real aperture.
        foreach (double r in new[] { 1.0, 3.0, 6.0, 10.0 })
        {
            double p = r * r, series = 0.0, power = 1.0;
            for (int j = 0; j <= 6; j++) { series += f[j] * power; power *= p; }
            double exact = surface.Sag(r);
            Assert.True(Math.Abs(series - exact) < 1e-9 * (1 + Math.Abs(exact)),
                $"at r = {r} the figure series gives {series:E12} against Sag's {exact:E12}");
        }
    }
}
