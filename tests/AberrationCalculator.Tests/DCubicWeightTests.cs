using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The c0 weight bookkeeping in <see cref="TertiaryCubics.DCubic"/>, against the scheme's own
/// spherical z.
///
/// <para><b>Why this needs its own test.</b> The spherical z - t121 to t130 - is built by a
/// separate closed form, and `DCubic` is never used for it: the cubics are reached only through
/// `DCubicIncrement` inside the figured branch. So the published Table I, which validates the
/// spherical tertiary, says nothing whatever about `DCubic`. Nor does the figured-plate test of
/// (73.7): its surfaces are plano, both terms of the D cubic carry c0, and the D side vanishes
/// identically there. Until this file, `DCubic` had no passing case at all.</para>
///
/// <para>The conversion it performs is the delicate part and is a local derivation rather than a
/// printed equation: S3 and X3 do not carry the same power of c0, so the S3 term is formed with
/// Y = c0 y while the X3 term carries an explicit extra c0. This pins that arithmetic against a
/// reference the published table does validate.</para>
///
/// <para><b>What it does NOT reach.</b> Only the spherical case, c1 = c2 = 0 - because that is
/// the only case where an independent reference exists. The figuring terms of the cubic remain
/// unverified, and that is precisely where the aspheric tertiary is known to be wrong. Do not
/// read a pass here as a clean bill for `DCubic`.</para>
/// </summary>
public class DCubicWeightTests
{
    [Fact]
    public void TheSphericalDCubicReproducesTheSchemesOwnZ()
    {
        string path = Fixtures.Lens("CookeTriplet");
        if (!System.IO.File.Exists(path)) return;

        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(path, catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var rows = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        double scale = p.Efl;

        int checkedSurfaces = 0;
        for (int i = 1; i <= sys.LastOpticalSurface(); i++)
        {
            double c = sys.Surfaces[i].VertexCurvature * scale;
            if (Math.Abs(c) < 1e-12) continue;                 // plano: the D side vanishes

            double nPrev = i - 1 < n.Length ? n[i - 1] : 1.0;
            double nCurr = i < n.Length ? n[i] : 1.0;
            double k = nPrev / nCurr;

            var t = rows[i].T;
            double y = t[1], v = t[2], incidence = t[3], j = t[7];
            double norm = nPrev * (1.0 - k) / 16.0;

            var d = TertiaryScriptT.ExpandCubicPhysical(
                TertiaryCubics.DCubic(k, 0.0, 0.0, c, y, v), y, v, c);

            var z = new double[11];
            for (int m = 1; m <= 10; m++)
                z[m] = norm * Math.Pow(j / c, TertiaryScriptT.JPower[m]) * (incidence * d[m] / c);

            // z3 and z4 are packed in the scheme: script-T_3 is z3 + z4 and script-T_4 is 8 z4.
            double z4 = z[4] / 8.0;
            z[3] -= z4;
            z[4] = z4;

            for (int m = 1; m <= 10; m++)
            {
                double expected = t[120 + m];
                double sc = Math.Max(Math.Abs(expected), Math.Abs(z[m]));
                if (sc < 1e-12) continue;
                Assert.True(Math.Abs(expected - z[m]) / sc < 1e-9,
                    $"surface {i}, z{m}: the scheme has {expected:E10}, DCubic assembles " +
                    $"{z[m]:E10}. The c0 weighting in DCubic no longer agrees with the closed " +
                    "form the published table validates.");
            }
            checkedSurfaces++;
        }

        Assert.True(checkedSurfaces >= 4,
            $"only {checkedSurfaces} curved surfaces were checked; the fixture changed");
    }
}
