using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Carrying the aspheric primary into Buchdahl's computing scheme.
///
/// <para>The scheme forms its induced tertiary terms from running sums of its own primary
/// and secondary coefficients, and computes those from spherical formulae. On a real asphere
/// that is not a small error, so a tertiary increment injected into it means nothing until
/// the lower orders know about the figuring too.</para>
///
/// <para>These tests run on the aspheric Cooke triplets in <c>tests/fixtures/lenses</c>,
/// which travel with the repository.</para>
/// </summary>
public class AsphericSchemeIncrementsTests
{
    private static readonly string[] Designs =
    {
        Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE"),
        Fixtures.Lens("CookeTriplet_PRMSA_START_LO_ASPHERE"),
        Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8"),
    };

    private sealed record Case(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                               BuchdahlResult Macro, double Stop, int Last);

    private static Case? Load(string path)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());

        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        return new Case(sys, n, p, BuchdahlCoefficients.Compute(sys, p), scheme.P,
                        sys.LastOpticalSurface());
    }

    /// <summary>
    /// The conversion between the scheme's coefficients and the fifth-order code's is one
    /// constant per coefficient, and the same on every surface. That is what makes it a unit
    /// conversion rather than a fit, and it is asserted here rather than assumed.
    /// </summary>
    [Fact]
    public void TheConversionIsOneConstantPerCoefficientOnEverySurface()
    {
        foreach (string path in Designs)
        {
            var c = Load(path);
            if (c == null) return;

            var spherical = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices,
                                                   c.Paraxial.Efl, c.Stop);
            double? first = null;
            for (int i = 1; i <= c.Last; i++)
            {
                double macro = c.Macro.Intrinsic[i].B;
                if (Math.Abs(macro) < 1e-12) continue;

                double ratio = spherical[i][10] / macro;
                first ??= ratio;
                Assert.True(Math.Abs(ratio - first.Value) / Math.Abs(first.Value) < 1e-9,
                    $"surface {i}: conversion {ratio:G10} against {first.Value:G10}");
            }
            Assert.NotNull(first);
        }
    }

    /// <summary>
    /// With the increment applied, the scheme's figured primary must agree with the
    /// fifth-order code's — including the induced part, which the two compute by different
    /// formulae. This is the test the carry-across exists to pass.
    /// </summary>
    [Fact]
    public void TheFiguredPrimaryAgreesWithTheFifthOrderCode()
    {
        foreach (string path in Designs)
        {
            var c = Load(path);
            if (c == null) return;

            var spherical = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices,
                                                   c.Paraxial.Efl, c.Stop);
            var increments = AsphericSchemeIncrements.Build(c.Macro, spherical, c.Last);
            Assert.NotNull(increments);

            var figured = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices, c.Paraxial.Efl,
                                                 c.Stop, increments);

            double bridge = 0.0, best = 0.0;
            for (int i = 1; i <= c.Last; i++)
                if (Math.Abs(c.Macro.Intrinsic[i].B) > best)
                {
                    best = Math.Abs(c.Macro.Intrinsic[i].B);
                    bridge = spherical[i][10] / c.Macro.Intrinsic[i].B;
                }

            for (int i = 1; i <= c.Last; i++)
            {
                double predicted = bridge * c.Macro.PerSurface[i].B;
                if (Math.Abs(predicted) < 1e-10) continue;

                double rel = Math.Abs(figured[i][10] - predicted) / Math.Abs(predicted);
                Assert.True(rel < 1e-8,
                    $"{System.IO.Path.GetFileName(path)} surface {i}: scheme "
                  + $"{figured[i][10]:G10}, fifth-order code {predicted:G10}");
            }
        }
    }

    /// <summary>
    /// A spherical system must produce no increments at all, so nothing that follows can be
    /// disturbed by a lens that has no figuring.
    /// </summary>
    [Fact]
    public void ASphericalSystemProducesNoIncrements()
    {
        string path = Fixtures.Lens("CookeTriplet");
        var c = Load(path);
        if (c == null) return;

        var spherical = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices,
                                               c.Paraxial.Efl, c.Stop);
        Assert.Null(AsphericSchemeIncrements.Build(c.Macro, spherical, c.Last));
    }

    /// <summary>
    /// The whole intrinsic primary, not just a_p. The figuring runs down the chain on the
    /// ray-HEIGHT ratio where a sphere runs on the incidence ratio, so this is what checks
    /// that the two are being kept apart: the barred entry pairs with the fifth-order code's
    /// coma and the last with its distortion, each by one constant.
    ///
    /// <para>Running the figuring down the q chain instead gets these wrong by factors of
    /// twenty and, on one surface of the SPOTM design, by a sign.</para>
    /// </summary>
    [Fact]
    public void TheFiguredPrimaryChainAgreesEntryByEntry()
    {
        foreach (string path in Designs)
        {
            var c = Load(path);
            if (c == null) return;

            var spherical = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices,
                                                   c.Paraxial.Efl, c.Stop);
            var increments = AsphericSchemeIncrements.Build(c.Macro, spherical, c.Last);
            var figured = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices, c.Paraxial.Efl,
                                                 c.Stop, increments);

            // t10 pairs with B, t11 with F and t14 with E, each by one constant measured on
            // the spherical run.
            var entry = new[] { 10, 11, 14 };
            var macro = new Func<BuchdahlTerms, double>[]
                        { t => t.B, t => t.F, t => t.E };

            for (int e = 0; e < entry.Length; e++)
            {
                double bridge = 0.0, best = 0.0;
                for (int i = 1; i <= c.Last; i++)
                {
                    double m = macro[e](c.Macro.Intrinsic[i]);
                    if (Math.Abs(m) > best) { best = Math.Abs(m); bridge = spherical[i][entry[e]] / m; }
                }

                for (int i = 1; i <= c.Last; i++)
                {
                    var a = c.Macro.Aspheric[i];
                    double m = macro[e](c.Macro.Intrinsic[i]) + (a == null ? 0.0 : macro[e](a));
                    double predicted = bridge * m;
                    if (Math.Abs(predicted) < 1e-10) continue;

                    double rel = Math.Abs(figured[i][entry[e]] - predicted) / Math.Abs(predicted);
                    Assert.True(rel < 1e-8,
                        $"{System.IO.Path.GetFileName(path)} surface {i}, t{entry[e]}: scheme "
                      + $"{figured[i][entry[e]]:G10}, fifth-order code {predicted:G10}");
                }
            }
        }
    }

    /// <summary>
    /// The secondary, as far as it is carried. Every surface up to and including the FIRST
    /// figured one agrees exactly with the fifth-order code — all six coefficients, induced
    /// part included.
    ///
    /// <para>Past the first figured surface it does not, and the reason is the same D and L
    /// split one level further up. The scheme forms its running sums and the dagger family
    /// from them with the incidence ratio q, and once an earlier surface has contributed an L
    /// part those sums are mixed, so q is wrong for half of what they carry. Splitting them
    /// is the same shape of fix again and is not done, so this test states the boundary
    /// rather than pretending there is none.</para>
    /// </summary>
    [Fact]
    public void TheSecondaryAgreesUpToTheFirstFiguredSurface()
    {
        foreach (string path in Designs)
        {
            var c = Load(path);
            if (c == null) return;

            var spherical = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices,
                                                   c.Paraxial.Efl, c.Stop);
            var increments = AsphericSchemeIncrements.Build(c.Macro, spherical, c.Last);
            var figured = BuchdahlTableI.Compute(c.System.Surfaces, c.Indices, c.Paraxial.Efl,
                                                 c.Stop, increments);

            // t41..t67 are the totals; B5, F2, M2, M3, N3 and Pi5+C5 their counterparts.
            int[] entry = { 41, 46, 52, 56, 62, 67 };
            var macro = new Func<BuchdahlTerms, double>[]
            {
                t => t.B5, t => t.F2, t => t.M2, t => t.M3, t => t.N3, t => t.Pi5 + t.C5,
            };
            int[] intrinsic = { 38, 44, 50, 54, 59, 65 };
            double[] tolerance = { 1e-8, 1e-8, 1e-8, 1e-8, 1e-8, 1e-8 };

            for (int e = 0; e < entry.Length; e++)
            {
                double bridge = 0.0, best = 0.0;
                for (int i = 1; i <= c.Last; i++)
                {
                    double m = macro[e](c.Macro.Intrinsic[i]);
                    if (Math.Abs(m) > best)
                    {
                        best = Math.Abs(m);
                        bridge = spherical[i][intrinsic[e]] / m;
                    }
                }

                for (int i = 1; i <= c.Last; i++)
                {
                    double predicted = bridge * macro[e](c.Macro.PerSurface[i]);
                    if (Math.Abs(predicted) < 1e-10) continue;

                    double rel = Math.Abs(figured[i][entry[e]] - predicted) / Math.Abs(predicted);
                    Assert.True(rel < tolerance[e],
                        $"{System.IO.Path.GetFileName(path)} surface {i}, t{entry[e]}: scheme "
                      + $"{figured[i][entry[e]]:G10}, fifth-order code {predicted:G10}");
                }
            }
        }
    }
}
