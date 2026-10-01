using System;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The aspheric seventh-order spherical aberration against an exact closed form.
///
/// <para>A single conic surface with the object at infinity can be traced in closed form -
/// reflect or refract one ray analytically and carry it to the paraxial image plane - so its
/// transverse aberration is an exact function of the ray height. Expanding that as
/// <c>eps = a3 y^3 + a5 y^5 + a7 y^7 + ...</c> gives the third, fifth and seventh order
/// coefficients as NUMBERS, with no series, no table and no other program involved.</para>
///
/// <para>This is what <see cref="ParabolicMirrorTests"/> could not do. A parabola tests the
/// case where the seventh order cancels to zero, which a formula can pass by being zero for
/// the wrong reason; these test the MAGNITUDE, over a range of conics, including ones where
/// the fifth and seventh order terms have opposite signs.</para>
///
/// <para>Both signs of k are covered deliberately. A mirror has <c>k = N/N' = -1</c>, which
/// is a special value in Buchdahl's formulae - every one of them carries a factor
/// <c>(1-k)</c>, and at a mirror that is exactly 2. The refracting case runs at
/// <c>k = 1/N</c>, about 0.66, so agreement in both places is not agreement at one
/// convenient point.</para>
/// </summary>
public class ExactConicSurfaceTests
{
    // ---- Truncated power series in u = y^2, to u^4. ------------------------------------

    private const int N = 7;

    private static double[] Con(double v) { var a = new double[N]; a[0] = v; return a; }
    private static double[] U() { var a = new double[N]; a[1] = 1.0; return a; }
    private static double[] Add(double[] a, double[] b)
    { var c = new double[N]; for (int i = 0; i < N; i++) c[i] = a[i] + b[i]; return c; }
    private static double[] Sub(double[] a, double[] b)
    { var c = new double[N]; for (int i = 0; i < N; i++) c[i] = a[i] - b[i]; return c; }
    private static double[] Scale(double[] a, double v)
    { var c = new double[N]; for (int i = 0; i < N; i++) c[i] = a[i] * v; return c; }

    private static double[] Mul(double[] a, double[] b)
    {
        var c = new double[N];
        for (int i = 0; i < N; i++)
            for (int j = 0; i + j < N; j++) c[i + j] += a[i] * b[j];
        return c;
    }

    private static double[] Div(double[] a, double[] b)
    {
        var c = new double[N];
        for (int k = 0; k < N; k++)
        {
            double s = a[k];
            for (int i = 0; i < k; i++) s -= c[i] * b[k - i];
            c[k] = s / b[0];
        }
        return c;
    }

    private static double[] Sqrt(double[] a)
    {
        var s = new double[N];
        s[0] = Math.Sqrt(a[0]);
        for (int k = 1; k < N; k++)
        {
            double t = a[k];
            for (int i = 1; i < k; i++) t -= s[i] * s[k - i];
            s[k] = t / (2.0 * s[0]);
        }
        return s;
    }

    /// <summary>
    /// The conic <c>y^2 = 2Rz - (1+k)z^2</c>, and the quantities every ray needs from it:
    /// the sag, and <c>A = (1+k)z - R</c>, which is the axial part of the surface normal
    /// <c>(y, A)</c>.
    /// </summary>
    private static (double[] Z, double[] A) Geometry(double r, double k, double a4 = 0.0,
                                                     double a6 = 0.0, double a8 = 0.0)
    {
        double c = 1.0 / r, q = (1.0 + k) * c * c;
        var rad = Con(1.0); rad[1] = -q;
        var denom = Sqrt(rad); denom[0] += 1.0;
        var zn = new double[N]; zn[1] = c;
        var z = Div(zn, denom);
        z[2] += a4; z[3] += a6; z[4] += a8;

        // For ANY rotationally symmetric surface the normal is (y, A) with A = -1/(2 dS/du).
        // On a conic that is identically (1+k)z - R, so this generalises the conic case
        // rather than approximating it.
        var sp = new double[N];
        for (int i = 0; i + 1 < N; i++) sp[i] = (i + 1) * z[i + 1];
        return (z, Div(Con(-0.5), sp));
    }

    /// <summary>
    /// Mirror. Reflecting the incoming axial direction about the unit normal gives the
    /// direction <c>(-2Ay, y^2-A^2)/(y^2+A^2)</c>; carried to the paraxial focus
    /// <c>z = R/2</c> that leaves <c>eps = y[1 - 2A(R/2 - z)/(y^2 - A^2)]</c>.
    /// </summary>
    private static double[] MirrorSeries(double r, double k)
    {
        var (z, a) = Geometry(r, k);
        var half = Scale(z, -1.0); half[0] += r / 2.0;
        var num = Scale(Mul(a, half), 2.0);
        var den = Sub(U(), Mul(a, a));
        return Sub(Con(1.0), Div(num, den));
    }

    /// <summary>
    /// Refractor, index 1 into <paramref name="np"/>. Snell in vector form with unit normal
    /// <c>(y,A)/G</c>, <c>G = sqrt(y^2+A^2)</c> and <c>c1 = -A/G</c>, carried to the
    /// paraxial image plane <c>N'R/(N'-1)</c>.
    /// </summary>
    private static double[] RefractSeries(double r, double k, double np, double a4 = 0.0,
                                          double a6 = 0.0)
    {
        double mu = 1.0 / np, lp = np * r / (np - 1.0);
        var (z, a) = Geometry(r, k, a4, a6);
        var g = Sqrt(Add(U(), Mul(a, a)));
        var c1 = Div(Scale(a, -1.0), g);
        var cost = Sqrt(Sub(Con(1.0), Scale(Sub(Con(1.0), Mul(c1, c1)), mu * mu)));
        var w = Sub(Scale(c1, mu), cost);
        var dz = Add(Con(mu), Div(Mul(w, a), g));
        var lz = Scale(z, -1.0); lz[0] += lp;
        return Add(Con(1.0), Div(Mul(lz, w), Mul(g, dz)));
    }

    // ---- The program's answer. ---------------------------------------------------------

    private static BuchdahlResult Program(double conic, double radius, double epd,
                                          string? glass, double thickness)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, epd) };
        sys.GlassCatalogs.Add("SCHOTT");
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Fields.Add(new Field(0.5));

        void Add(double cv, double th, string? g = null, bool stop = false, double kk = 0.0)
            => sys.Surfaces.Add(new Surface
            {
                Index = sys.Surfaces.Count, Curvature = cv, Thickness = th,
                Material = g, IsStop = stop, Conic = kk,
            });

        Add(0.0, double.PositiveInfinity);
        Add(1.0 / radius, thickness, glass, stop: true, kk: conic);
        Add(0.0, 0.0);

        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        return BuchdahlCoefficients.Compute(sys, ParaxialTrace.Trace(sys, n, 0.5));
    }

    /// <summary>
    /// The same system, carried all the way through the tertiary pipeline to tau1. The stop
    /// sits on the surface and the semi-diameter is set explicitly, because the scheme needs
    /// one and a hand-built system has no ray trace to derive it from.
    /// </summary>
    private static double Tau1(double conic, double radius, double epd, string? glass,
                               double thickness, double[]? poly = null, bool useB7 = true)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, epd) };
        sys.GlassCatalogs.Add("SCHOTT");
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Fields.Add(new Field(0.5));

        void Add(double cv, double th, string? g = null, bool stop = false, double kk = 0.0,
                 double[]? a = null)
        {
            var s = new Surface
            {
                Index = sys.Surfaces.Count, Curvature = cv, Thickness = th,
                Material = g, IsStop = stop, Conic = kk, SemiDiameter = epd / 2.0,
            };
            if (a != null)
            {
                s.Type = SurfaceType.EvenAsphere;
                for (int j = 0; j < a.Length; j++) s.AsphericCoefficients[j + 1] = a[j];
            }
            sys.Surfaces.Add(s);
        }

        Add(0.0, double.PositiveInfinity);
        Add(1.0 / radius, thickness, glass, stop: true, kk: conic, a: poly);
        Add(0.0, 0.0);

        var n = IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55);
        var p = ParaxialTrace.Trace(sys, n, 0.5);
        var b = BuchdahlCoefficients.Compute(sys, p);
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
        double u = -1.0 / (2.0 * b.FNumber);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
        var increments = AsphericSchemeIncrements.Build(b, spherical, sys.LastOpticalSurface());
        var raw = TertiaryCoefficients.Compute(sys.Surfaces, n, p.Efl, scheme.P, increments);
        return TertiaryCoefficients.ToTransverse(
            raw, p.Efl, u, Math.Tan(0.5 * Math.PI / 180.0),
            useB7 ? b.Totals.B7 : (double?)null)[1];
    }

    private static double IndexOf(string glass)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 10.0) };
        sys.GlassCatalogs.Add("SCHOTT");
        sys.Wavelengths.Add(new Wavelength(0.55, 1.0, true));
        sys.Fields.Add(new Field(0.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Thickness = 1.0, Material = glass });
        sys.Surfaces.Add(new Surface { Index = 2 });
        return IndexResolver.Build(sys, CatalogLocator.LoadBundled(), 0.55)[1];
    }

    // ---- The tests. --------------------------------------------------------------------

    /// <summary>
    /// A conic mirror, R = -200 with the focus 100 in front of it, against the closed form
    /// at all three orders. The program reports a reflected system with the opposite sign
    /// throughout; that is a convention rather than an error, being the same factor -1 on
    /// every order and every conic, so it is asserted as such.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(-2.0)]
    [InlineData(0.5)]
    [InlineData(-6.0)]
    public void AConicMirrorMatchesTheClosedForm(double conic)
    {
        const double r = 200.0, ym = 20.0;
        Check("mirror", conic, MirrorSeries(r, conic),
              Program(conic, -r, 2.0 * ym, "MIRROR", -r / 2.0), ym, -1.0);
    }

    /// <summary>
    /// A conic refracting surface into N-BK7, which puts k at 1/N rather than the mirror's
    /// -1. Here the program's sign agrees outright.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(-1.0)]
    [InlineData(-2.0)]
    [InlineData(0.5)]
    [InlineData(-6.0)]
    public void AConicRefractingSurfaceMatchesTheClosedForm(double conic)
    {
        const double r = 100.0, ym = 10.0;
        double np = IndexOf("N-BK7");
        Check("refract", conic, RefractSeries(r, conic, np),
              Program(conic, r, 2.0 * ym, "N-BK7", np * r / (np - 1.0)), ym, 1.0);
    }

    private static void Check(string what, double conic, double[] e, BuchdahlResult p,
                              double ym, double sign)
    {
        var exact = new[] { e[1] * Math.Pow(ym, 3), e[2] * Math.Pow(ym, 5),
                            e[3] * Math.Pow(ym, 7) };
        var got = new[] { p.Totals.B, p.Totals.B5, p.Totals.B7 };
        var name = new[] { "third", "fifth", "seventh" };

        for (int i = 0; i < 3; i++)
        {
            // Skip a coefficient the chosen conic happens to annihilate; the parabola tests
            // cover cancellation, these cover magnitude.
            if (Math.Abs(exact[i]) < 1e-14) continue;

            double rel = Math.Abs(got[i] - sign * exact[i]) / Math.Abs(exact[i]);
            Assert.True(rel < 1e-9,
                $"{what} conic {conic}, {name[i]} order: closed form {sign * exact[i]:G10}, "
              + $"program {got[i]:G10}");
        }
    }

    /// <summary>
    /// tau1 - the tertiary module's OWN seventh-order spherical - against the same closed
    /// form, carried through the whole pipeline rather than read off the fifth-order code.
    ///
    /// <para>This is the test the fix exists to pass. The scheme's aspheric tertiary route
    /// gets this coefficient badly wrong: on these very surfaces it came out anywhere from
    /// half to eighty times the truth, its c1-linear part exactly doubled and its c1-squared
    /// part wrong in sign as well as size. <c>ToTransverse</c> now takes the value from B7
    /// instead, which the tests above prove exact.</para>
    ///
    /// <para>Polynomial figuring is covered here as well as conic, because the two excite
    /// c1, c2 and c3 in different proportions - a conic ties them together through kappa,
    /// while an A4 term moves c1 on its own.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-1.0, 0.0, 0.0)]
    [InlineData(-2.0, 0.0, 0.0)]
    [InlineData(0.0, 1e-7, 0.0)]
    [InlineData(0.0, 1e-6, 0.0)]
    [InlineData(0.0, -5e-7, 0.0)]
    [InlineData(0.0, 0.0, 1e-10)]
    [InlineData(-2.0, 5e-7, 1e-11)]
    public void TheTertiaryModulesOwnTau1MatchesTheClosedForm(double conic, double a4, double a6)
    {
        const double r = 100.0, ym = 10.0;
        double np = IndexOf("N-BK7");

        var e = RefractSeries(r, conic, np, a4, a6);
        double exact = e[3] * Math.Pow(ym, 7);
        double tau1 = Tau1(conic, r, 2.0 * ym, "N-BK7", np * r / (np - 1.0),
                           a4 == 0.0 && a6 == 0.0 ? null : new[] { a4, a6, 0.0 });

        Assert.True(Math.Abs(tau1 - exact) / Math.Abs(exact) < 1e-5,
            $"conic {conic}, A4 {a4:E1}, A6 {a6:E1}: closed form {exact:G10}, tau1 {tau1:G10}");
    }

    /// <summary>
    /// The CUBICS' own tau1, with no help from B7, against the closed form - now exact for
    /// every figuring tried rather than only for those with no c1.
    ///
    /// <para>This is the test the whole aspheric tertiary rests on, and what it took to pass
    /// was a convention. (56.5) defines <c>c1 = 4(theta2 - theta1^3)</c> with theta2 the r^4
    /// coefficient of the sag, which for a conic makes it <c>4 A4 + conic c0^3/2</c>. The
    /// fifth-order code uses twice that, and <c>Figuring.From</c> was handing the doubled
    /// value to (77.2), (77.4) and (77.5), which are written in Buchdahl's. Every term linear
    /// in c then came out twice too large and every c1-squared term four times, which is why
    /// no single factor ever fixed both and why every individual coefficient checked out
    /// against the page.</para>
    ///
    /// <para>The cases below span three decades of A4, conics from -6 to +1, sixth-order
    /// figuring at magnitudes from a 0.8 nm perturbation to a 33 um aberration, and mixtures
    /// of all three. Nothing here is fitted; the closed form is an exact ray trace.</para>
    ///
    /// <para>The one loose tolerance is the conic -1 with A4 = 1e-7, where the true value is
    /// -1.98e-8 because two much larger terms very nearly cancel. Relative error is amplified
    /// by the cancellation, not by any weakness in the coefficient.</para>
    /// </summary>
    [Theory]
    // Sixth-order figuring alone, which carries c2 and c3 but no c1 at all.
    [InlineData(0.0, 0.0, 1e-13, 1e-5)]
    [InlineData(0.0, 0.0, 1e-12, 1e-5)]
    [InlineData(0.0, 0.0, 1e-10, 1e-5)]
    // Conics, weak to strong.
    [InlineData(1e-4, 0.0, 0.0, 1e-5)]
    [InlineData(-1e-4, 0.0, 0.0, 1e-5)]
    [InlineData(1e-3, 0.0, 0.0, 1e-5)]
    [InlineData(-1.0, 0.0, 0.0, 1e-5)]
    [InlineData(-2.0, 0.0, 0.0, 1e-5)]
    [InlineData(1.0, 0.0, 0.0, 1e-5)]
    [InlineData(0.5, 0.0, 0.0, 1e-5)]
    [InlineData(-6.0, 0.0, 0.0, 1e-5)]
    // Fourth-order polynomial figuring, which is what drives c1 hardest.
    [InlineData(0.0, 1e-9, 0.0, 1e-5)]
    [InlineData(0.0, 1e-7, 0.0, 1e-5)]
    [InlineData(0.0, -5e-7, 0.0, 1e-5)]
    [InlineData(0.0, 1e-6, 0.0, 1e-5)]
    [InlineData(0.0, 2e-6, 0.0, 1e-5)]
    // Mixtures.
    [InlineData(-1.0, 1e-7, 0.0, 1e-3)]
    [InlineData(-2.0, 5e-7, 1e-11, 1e-5)]
    public void TheCubicsOwnTau1MatchesTheClosedForm(double conic, double a4, double a6,
                                                     double tolerance)
    {
        const double r = 100.0, ym = 10.0;
        double np = IndexOf("N-BK7");

        double exact = RefractSeries(r, conic, np, a4, a6)[3] * Math.Pow(ym, 7);
        double tau1 = Tau1(conic, r, 2.0 * ym, "N-BK7", np * r / (np - 1.0),
                           a4 == 0.0 && a6 == 0.0 ? null : new[] { a4, a6, 0.0 },
                           useB7: false);

        Assert.True(Math.Abs(tau1 - exact) / Math.Abs(exact) < tolerance,
            $"conic {conic}, A4 {a4:E1}, A6 {a6:E1}: closed form {exact:G10}, "
          + $"cubics {tau1:G10}, ratio {tau1 / exact:F6}");
    }

    /// <summary>
    /// The series machinery against the closed form it was expanded from - a guard on the
    /// expansion itself, independent of anything this program computes.
    ///
    /// <para>It cannot be asserted to agree exactly, because truncating at the seventh order
    /// leaves the ninth behind. So what is asserted is that the leftover IS the ninth order:
    /// double the ray height and it must grow by 2^9. That is a sharper statement than any
    /// tolerance would be - an algebra slip in the third, fifth or seventh order coefficient
    /// would leave a residual growing as the cube, the fifth or the seventh power instead,
    /// and would fail this however loose the threshold.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(0.5)]
    public void WhatTheExpansionLeavesBehindIsTheNinthOrder(double conic)
    {
        const double r = 200.0;
        var e = MirrorSeries(r, conic);

        double Residual(double y)
        {
            double c = 1.0 / r, q = (1.0 + conic) * c * c;
            double z = c * y * y / (1.0 + Math.Sqrt(1.0 - q * y * y));
            double a = (1.0 + conic) * z - r;
            double closed = y * (1.0 - 2.0 * a * (r / 2.0 - z) / (y * y - a * a));
            double series = e[1] * Math.Pow(y, 3) + e[2] * Math.Pow(y, 5)
                          + e[3] * Math.Pow(y, 7);
            return closed - series;
        }

        double growth = Residual(20.0) / Residual(10.0);
        Assert.True(Math.Abs(growth - 512.0) / 512.0 < 0.02,
            $"conic {conic}: residual grew by {growth:F1}, not 2^9 = 512");
    }
}
