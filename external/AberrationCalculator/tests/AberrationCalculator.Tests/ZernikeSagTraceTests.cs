using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The real ray trace through a surface's Fringe Zernike departure (<see cref="Surface.ZernikeSag"/>).
///
/// <para><b>The polynomials</b> are checked against what every Zernike set satisfies - each cosine
/// and rotationally symmetric term is 1 at the rim along x, each sine term 0 there - and their
/// slopes against central differences, for all 37 terms.</para>
///
/// <para><b>The trace</b> is checked exactly, against shapes the trace already handled. Z9 − Z1 is
/// 6ρ⁴ − 6ρ², an even asphere's r² and r⁴ terms, so a surface given it must trace as that asphere
/// does, to rounding. Z5 = (x² − y²)/a² is +r²/a² along the x meridian and −r²/a² along y, and
/// its slope across each is zero, so a ray in either stays there and must trace as the matching
/// asphere does; Z6 = 2xy/a² the same along the diagonal x = y.</para>
/// </summary>
public class ZernikeSagTraceTests
{
    private const double A = 6.0;              // the departure's unit radius, mm
    private const double B = 2e-3;            // its coefficient: a few waves of sag, enough to move every ray
    private const int Powered = 2;            // the singlet's front surface; surface 1 is its remote stop, in air

    private static Surface Figured(params (int Term, double Value)[] terms)
    {
        var s = new Surface { ZernikeRadius = A, FringeZernike = new double[40] };
        foreach (var (term, value) in terms) s.FringeZernike[term] = value;
        return s;
    }

    /// <summary>Fringe Z_j alone at (u, v) on the unit disk, with its slopes there.</summary>
    private static (double Z, double Du, double Dv) Term(int j, double u, double v)
    {
        var s = Figured((j, 1.0));
        double z = s.ZernikeSag(u * A, v * A, out double dx, out double dy);
        return (z, dx * A, dy * A);
    }

    [Fact]
    public void EveryTermIsOneOrZeroAtTheRimAlongX()
    {
        // Within each s = (n + m)/2, m falls from s to 0, cosine then sine: the sine terms are
        // the second of each pair with m > 0.
        var sine = new HashSet<int> { 3, 6, 8, 11, 13, 15, 18, 20, 22, 24, 27, 29, 31, 33, 35 };
        for (int j = 1; j <= Surface.MaxTracedZernike; j++)
            Assert.Equal(sine.Contains(j) ? 0.0 : 1.0, Term(j, 1.0, 0.0).Z, 12);
    }

    [Theory]
    [InlineData(4, 0.0, 0.0, -1.0)]                       // 2ρ² − 1
    [InlineData(9, 0.70710678118654752, 0.0, -0.5)]      // 6ρ⁴ − 6ρ² + 1 at ρ² = 1/2
    [InlineData(5, 0.0, 1.0, -1.0)]                       // ρ² cos 2θ along y
    [InlineData(6, 0.70710678118654752, 0.70710678118654752, 1.0)]   // ρ² sin 2θ at 45°
    [InlineData(7, 0.5, 0.0, -0.625)]                     // (3ρ² − 2) ρ cos θ
    [InlineData(10, 0.0, 1.0, 0.0)]                       // ρ³ cos 3θ at 90°
    [InlineData(11, 0.0, 1.0, -1.0)]                      // ρ³ sin 3θ at 90°
    [InlineData(16, 0.0, 0.0, -1.0)]                      // 20ρ⁶ − 30ρ⁴ + 12ρ² − 1
    [InlineData(37, 0.0, 0.0, 1.0)]                       // R_12^0(0)
    public void TermsHaveTheirKnownValues(int j, double u, double v, double expected) =>
        Assert.Equal(expected, Term(j, u, v).Z, 12);

    [Fact]
    public void EverySlopeIsTheDerivative()
    {
        const double h = 1e-6;
        foreach (var (u, v) in new[] { (0.31, -0.47), (0.0, 0.0), (-0.66, 0.52), (0.9, 0.05) })
            for (int j = 1; j <= Surface.MaxTracedZernike; j++)
            {
                var (_, du, dv) = Term(j, u, v);
                Assert.Equal((Term(j, u + h, v).Z - Term(j, u - h, v).Z) / (2 * h), du, 1e-6 * Math.Max(1.0, Math.Abs(du)));
                Assert.Equal((Term(j, u, v + h).Z - Term(j, u, v - h).Z) / (2 * h), dv, 1e-6 * Math.Max(1.0, Math.Abs(dv)));
            }
    }

    [Fact]
    public void WithoutARadiusOrAboveZ37TheSagIsUndefined()
    {
        var noRadius = new Surface { FringeZernike = new double[] { 0, 0, 0, 0, 0, 1e-4 } };
        Assert.True(double.IsNaN(noRadius.ZernikeSag(1.0, 1.0, out _, out _)));

        var withSemiDiameter = new Surface { SemiDiameter = 4.0, FringeZernike = new double[] { 0, 0, 0, 0, 0, 1e-4 } };
        Assert.Equal(1e-4 * (9.0 - 1.0) / 16.0, withSemiDiameter.ZernikeSag(3.0, 1.0, out _, out _), 15);

        var high = Figured((38, 1e-4));
        Assert.True(double.IsNaN(high.ZernikeSag(1.0, 1.0, out _, out _)));
        Assert.Equal(0.0, new Surface().ZernikeSag(1.0, 1.0, out double sx, out double sy));
        Assert.Equal((0.0, 0.0), (sx, sy));
    }

    // --- The trace -----------------------------------------------------------------------

    private static (OpticalSystem System, double[] Indices) Singlet()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("Ladder1_Sphere.lhlt"), catalog);
        return (sys, IndexResolver.Build(sys, catalog, 0.5876, new List<string>()));
    }

    /// <summary>Each ray's landing at the image and its optical path there.</summary>
    private static List<double[]> Trace(OpticalSystem sys, double[] n, IEnumerable<(double Py, double Pz)> rays)
    {
        var paraxial = ParaxialTrace.Trace(sys, n, 0.0);
        var landings = new List<double[]>();
        foreach (var (py, pz) in rays)
        {
            var path = new double[sys.Surfaces.Count];
            var hits = RealRayTrace.TraceRecord(sys, n, paraxial, 0.0, py, pz, false, null, null, path);
            var last = hits[hits.Length - 1];
            Assert.True(last.Ok);
            landings.Add(new[] { last.X, last.Y, last.L, last.M, last.N, path[path.Length - 1] });
        }
        return landings;
    }

    /// <summary>
    /// The figured surface lands every ray as the asphere does, to 1e-10, where the departure moves
    /// them by far more than that from the plain lens: otherwise the check would prove nothing.
    /// </summary>
    private static void SameLandings(OpticalSystem asphere, OpticalSystem figured, double[] n, IReadOnlyList<(double, double)> rays)
    {
        var expected = Trace(asphere, n, rays);
        var actual = Trace(figured, n, rays);
        var plain = Trace(Singlet().System, n, rays);
        double moved = 0.0;
        for (int k = 0; k < rays.Count; k++)
            moved = Math.Max(moved, Math.Max(Math.Abs(expected[k][0] - plain[k][0]), Math.Abs(expected[k][1] - plain[k][1])));
        Assert.True(moved > 1e-4, $"the departure moved the rays only {moved:E2} mm");
        for (int k = 0; k < rays.Count; k++)
            for (int c = 0; c < expected[k].Length; c++)
                Assert.Equal(expected[k][c], actual[k][c], 10);
    }

    private static readonly (double, double)[] Meridian = { (0.2, 0.0), (0.5, 0.0), (-0.7, 0.0), (1.0, 0.0) };
    private static readonly (double, double)[] Sagittal = { (0.0, 0.3), (0.0, -0.6), (0.0, 0.95) };
    private static readonly (double, double)[] Diagonal = { (0.3, 0.3), (-0.5, -0.5), (0.65, 0.65) };

    [Fact]
    public void ARotationallySymmetricDepartureTracesAsItsAsphere()
    {
        var (asphere, n) = Singlet();
        asphere.Surfaces[Powered].AsphericCoefficients[0] += -6.0 * B / (A * A);
        asphere.Surfaces[Powered].AsphericCoefficients[1] += 6.0 * B / (A * A * A * A);
        var (figured, _) = Singlet();
        figured.Surfaces[Powered].ZernikeRadius = A;
        figured.Surfaces[Powered].FringeZernike = new double[] { 0, -B, 0, 0, 0, 0, 0, 0, 0, B };

        var rays = new List<(double, double)>(Meridian);
        rays.AddRange(Diagonal);
        SameLandings(asphere, figured, n, rays);
    }

    [Fact]
    public void AstigmatismTracesAsItsAsphereInEachPrincipalMeridian()
    {
        var (figured, n) = Singlet();
        figured.Surfaces[Powered].ZernikeRadius = A;
        figured.Surfaces[Powered].FringeZernike = new double[] { 0, 0, 0, 0, 0, B };

        var (alongX, _) = Singlet();
        alongX.Surfaces[Powered].AsphericCoefficients[0] += B / (A * A);
        SameLandings(alongX, figured, n, Sagittal);

        var (alongY, _) = Singlet();
        alongY.Surfaces[Powered].AsphericCoefficients[0] -= B / (A * A);
        SameLandings(alongY, figured, n, Meridian);
    }

    [Fact]
    public void ObliqueAstigmatismTracesAsItsAsphereAlongTheDiagonal()
    {
        var (figured, n) = Singlet();
        figured.Surfaces[Powered].ZernikeRadius = A;
        figured.Surfaces[Powered].FringeZernike = new double[] { 0, 0, 0, 0, 0, 0, B };

        var (diagonal, _) = Singlet();
        diagonal.Surfaces[Powered].AsphericCoefficients[0] += B / (A * A);
        SameLandings(diagonal, figured, n, Diagonal);
    }

    [Fact]
    public void AstigmatismBendsASagittalRayWithinItsPlane()
    {
        // Z5 bends a sagittal ray as an r² term does, and leaves it in the x-z plane.
        var (figured, n) = Singlet();
        figured.Surfaces[Powered].ZernikeRadius = A;
        figured.Surfaces[Powered].FringeZernike = new double[] { 0, 0, 0, 0, 0, B };
        var (plain, _) = Singlet();
        var bent = Trace(figured, n, Sagittal);
        var straight = Trace(plain, n, Sagittal);
        for (int k = 0; k < bent.Count; k++)
        {
            Assert.Equal(0.0, bent[k][1], 12);                          // still y = 0
            Assert.True(Math.Abs(bent[k][0] - straight[k][0]) > 1e-4);  // but bent in x
        }
    }

    [Fact]
    public void AZeroDepartureTracesBitForBitAsNone()
    {
        var (zeros, n) = Singlet();
        zeros.Surfaces[Powered].ZernikeRadius = A;
        zeros.Surfaces[Powered].FringeZernike = new double[38];
        var (plain, _) = Singlet();
        var rays = new List<(double, double)>(Meridian);
        rays.AddRange(Sagittal);
        rays.AddRange(Diagonal);
        var a = Trace(plain, n, rays);
        var b = Trace(zeros, n, rays);
        for (int k = 0; k < a.Count; k++) Assert.Equal(a[k], b[k]);
    }
}
