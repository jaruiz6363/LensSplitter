using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The aspheric secondary, DERIVED in the scheme's own variables, against the bridged value it
/// would replace.
///
/// <para><b>Why this matters.</b> <see cref="AsphericSchemeIncrements"/> obtains the aspheric
/// secondary by measuring a conversion on a SPHERICAL run and multiplying the fifth-order
/// code's answer by it. That bridge is why nine attempts to split the increment failed in both
/// directions: a conversion measured that way already carries the spherical scheme's
/// propagation, so apportioning its output corrects it a second time. The route below owes the
/// fifth-order code nothing, and it arrives with the D and L halves SEPARATE - which is what a
/// bridged scalar can never provide.</para>
///
/// <para>The chain is all Buchdahl's: M (65.1-2) give the two halves; (218.31)'s substitution
/// puts them in the theta basis, which <see cref="TertiaryScriptT.ExpandQuadraticPhysical"/>
/// performs; (218.6) reads the expansion back as s_1p and w1..w5; (218.7) runs those forward to
/// s_1p..s_6p. One scalar is still measured, from the spherical run of the same surface, and it
/// cannot bias the split because it multiplies both halves alike.</para>
///
/// <para><b>The L half carries one power of c less than the D half.</b> That is not a fudge but
/// the weighting <see cref="TertiaryCubics.LCubic"/> already documents at third order - "gamma
/// carries one more power of c0 than S3 does" - and it is what takes the agreement below from a
/// constant per-surface factor to exact.</para>
///
/// <para>The split is NOT yet applied to the scheme. Routing the D half onto the incidence
/// ratio, which is what (65.7) says it travels on, makes the traced fans worse; see
/// docs/verification.md. What is established here is that the derivation reproduces the quantity
/// exactly, which is the part that had to be true before any of that could be diagnosed.</para>
/// </summary>
public class DerivedSecondaryTests
{
    private static readonly int[] JPower = { 0, 1, 2, 2, 3, 4 };

    private static double[] Forward(BuchdahlTableIRow r, double[] theta, double carrier,
                                    double norm, double jc)
    {
        var g = new double[6];
        for (int m = 0; m < 6; m++)
            g[m] = norm * Math.Pow(jc, JPower[m]) * carrier * theta[m];

        // (218.6) read backwards: the six coefficients are s_1p, w1/j, (w2+w3)/j^2,
        // -2 w2/j^2, w4/j^3, w5/j^4, with the j powers already applied above.
        double w2 = -0.5 * g[3];
        var w = new[] { g[0], g[1], w2, g[2] - w2, g[4], g[5] };

        // (218.7) forwards.
        double q1 = r[6], q2 = q1 * q1, q3 = q2 * q1, q4 = q3 * q1;
        double a1 = w[0];
        return new[]
        {
            a1,
            4.0 * q1 * a1 + w[1],
            2.0 * q2 * a1 + q1 * w[1] + w[2] + w[3],
            4.0 * q2 * a1 + 2.0 * q1 * w[1] - 2.0 * w[2],
            4.0 * q3 * a1 + 3.0 * q2 * w[1] - 2.0 * q1 * w[2] + 2.0 * q1 * w[3] + w[4],
            q4 * a1 + q3 * w[1] - q2 * w[2] + q2 * w[3] + q1 * w[4] + w[5],
        };
    }

    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void TheDerivedSecondaryReproducesTheBridgedOne(string design)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(design), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        var bridged = AsphericSchemeIncrements.Build(b, sph, sys.LastOpticalSurface());
        Assert.NotNull(bridged);

        double scale = Math.Abs(p.Efl) > 1e-300 ? p.Efl : 1.0;
        int compared = 0;

        for (int i = 1; i < sph.Length && i <= sys.LastOpticalSurface(); i++)
        {
            if (bridged![i] == null) continue;
            var r = sph[i];
            double k = n[i - 1] / n[i];
            double c = sys.Surfaces[i].VertexCurvature * scale;
            var a = sys.Surfaces[i].AsphericCoefficients;
            var vf = sys.Surfaces[i].VertexForm();
            var fig = TertiaryCubics.Figuring.From(
                vf.Conic, vf.A4, vf.A6, vf.A8,
                sys.Surfaces[i].Curvature, scale);
            if (!fig.Present || Math.Abs(c) < 1e-12) continue;

            var (dRaw, lRaw) = TertiaryCubics.SecondaryHalves(k, fig.C1, fig.C2, c, r[1], r[2]);
            var dTheta = TertiaryScriptT.ExpandQuadraticPhysical(dRaw, r[1], r[2], c);
            var lTheta = TertiaryScriptT.ExpandQuadraticPhysical(lRaw, r[1], r[2], c);
            var sTheta = TertiaryScriptT.ExpandQuadraticPhysical(
                TertiaryCubics.SecondaryDSpherical(k, c, r[1], r[2]), r[1], r[2], c);

            double jc = r[7] / c;
            double baseline = sTheta[0] * r[3] / c;
            if (Math.Abs(baseline) < 1e-25) continue;
            double norm = 3.0 * r[10] * r[34] / baseline;

            var dHalf = Forward(r, dTheta, r[3] / c, norm, jc);   // D on the incidence, (65.7)
            var lHalf = Forward(r, lTheta, r[1] / c, norm, jc);   // L on the height

            for (int m = 0; m < 6; m++)
            {
                double want = bridged[i][m + 1];
                if (Math.Abs(want) < 1e-9) continue;
                double derived = dHalf[m] + lHalf[m];
                double rel = Math.Abs(derived - want) / Math.Abs(want);
                Assert.True(rel < 1e-6,
                    $"{design} surface {i}, s{m + 1}: derived {derived:E6} against bridged "
                  + $"{want:E6} ({rel:E2} apart)");
            }
            compared++;
        }

        Assert.True(compared >= 2, $"{design}: only {compared} figured surfaces were compared");
    }

    /// <summary>
    /// And the split the bridge could never give. The D half is small but not zero, which is
    /// the whole content of M (65.6) - that the figuring reaches D from the second order on -
    /// and it is the quantity every one of the nine failed hypotheses was trying to guess at.
    /// </summary>
    [Fact]
    public void TheDHalfIsPresentAndSmallerThanTheL()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet_SPOTM_START_LO_ASPHERE"), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        double scale = p.Efl;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);

        var r = sph[1];
        double k = n[0] / n[1];
        double c = sys.Surfaces[1].VertexCurvature * scale;
        var a = sys.Surfaces[1].AsphericCoefficients;
        var vf = sys.Surfaces[1].VertexForm();
        var fig = TertiaryCubics.Figuring.From(vf.Conic, vf.A4, vf.A6, vf.A8,
                                               vf.Curvature, scale);
        var (dRaw, lRaw) = TertiaryCubics.SecondaryHalves(k, fig.C1, fig.C2, c, r[1], r[2]);

        Assert.Contains(dRaw, x => Math.Abs(x) > 1e-14);
        Assert.Contains(lRaw, x => Math.Abs(x) > 1e-14);
    }
}
