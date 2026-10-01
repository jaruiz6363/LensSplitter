using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Core.Nat;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Systems whose OBJECT SPACE or IMAGE SPACE is not air.
///
/// <para>Almost every design in this repository, and every fixture, has <c>n = 1</c> at both
/// ends. That makes a whole class of error invisible: a dropped <c>n'</c> in a conversion from a
/// wave coefficient to a transverse one, or in an F/number, or in the invariant, changes nothing
/// at all until something is immersed. The r-squared bug lived in exactly that kind of blind spot
/// for as long as the file existed - see <see cref="SeidelR2TermTests"/> - so this file asks the
/// same question about the indices.</para>
///
/// <para><b>1.01 is asked for because it is the realistic case</b> (a cover glass, a coupling
/// fluid, a slightly wrong ambient) and 1.30 because it is the diagnostic one: a formula missing
/// a factor of <c>n'</c> is out by 1% at 1.01, which could be a tolerance, and by 30% at 1.30,
/// which cannot be anything else.</para>
///
/// <para>Nothing here compares against another program. Each test is a statement that must hold
/// of any correct implementation: a third-order coefficient must predict a real traced ray as
/// the aperture shrinks, two independently computed coefficient sets must agree, the invariant
/// must be conserved, and the F/number must be the one the numerical aperture implies.</para>
/// </summary>
public class ImmersedSpaceTests
{
    private const double D = 0.5875618;

    /// <summary>
    /// Infinite conjugate singlet whose IMAGE SPACE carries <paramref name="nImage"/>. The
    /// image surface is left at the file's distance; every measurement below is taken at the
    /// paraxial focus, which the trace locates for itself.
    /// </summary>
    private static (OpticalSystem Sys, double[] N) ImmersedImage(double nImage)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(3.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "GLASS", IsStop = true, SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 50.0,
                                     Material = nImage == 1.0 ? "" : "IMMERSION",
                                     SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 12.0 });
        return (s, new[] { 1.0, 1.6, nImage, nImage });
    }

    /// <summary>
    /// Finite conjugate - as the object-side cases must be - whose OBJECT SPACE carries
    /// <paramref name="nObject"/>. The object sits 200 mm away and the field is an object height.
    /// </summary>
    private static (OpticalSystem Sys, double[] N) ImmersedObject(double nObject)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0),
                                    FieldType = FieldType.ObjectHeight };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(10.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = 200.0,
                                     Material = nObject == 1.0 ? "" : "IMMERSION" });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "GLASS", IsStop = true, SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 190.0,
                                     SemiDiameter = 12.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 12.0 });
        return (s, new[] { nObject, 1.6, 1.0, 1.0 });
    }

    // ── The decisive check: a coefficient against a traced ray ──────────────────────────

    /// <summary>
    /// Third-order spherical aberration, predicted from <c>S1</c> and measured on a traced ray,
    /// as the pupil shrinks.
    ///
    /// <para>On axis the transverse aberration at the paraxial focus is
    /// <c>eps = S1 rho^3 / (2 n' u')</c>. Everything about the immersion lives in that
    /// <c>n'</c>: if it were missing here, or missing from the sum, the ratio of traced to
    /// predicted would settle on <c>n'</c> or <c>1/n'</c> instead of on one. It settles on one at
    /// 1.00, at 1.01 and at 1.30, so the factor is present and is present once.</para>
    ///
    /// <para>The residual must also SHRINK LIKE rho^2, because what is left over is the fifth
    /// order. A constant residual would mean a wrong coefficient rather than a truncated series,
    /// and halving the pupil would not help.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void SeidelSphericalPredictsATracedRayWithAnImmersedImageSpace(double nImage)
    {
        var (sys, n) = ImmersedImage(nImage);
        SphericalConverges(sys, n, $"image space n' = {nImage}");
    }

    /// <summary>
    /// The same on the object side, at finite conjugate, where the immersion changes the
    /// object-space ray angles and the invariant rather than the image-space cone.
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void SeidelSphericalPredictsATracedRayWithAnImmersedObjectSpace(double nObject)
    {
        var (sys, n) = ImmersedObject(nObject);
        SphericalConverges(sys, n, $"object space n = {nObject}");
    }

    private static void SphericalConverges(OpticalSystem sys, double[] n, string what)
    {
        int last = sys.LastOpticalSurface();
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        var sd = SeidelCoefficients.Compute(sys, n, n, n, p);
        double nPrime = p.N[last], uPrime = p.U[last];

        Assert.True(Math.Abs(sd.TotalS1) > 1e-3, $"{what}: the design has no spherical to measure");

        double previous = double.NaN;
        foreach (double rho in new[] { 0.25, 0.125, 0.0625 })
        {
            var landing = RealRayTrace.TraceFrom(sys, n, p, 0.0, rho * p.Y[1], 0.0,
                                                 rho * p.U[0], 1.0);
            Assert.True(landing.Ok, $"{what}: the ray at rho = {rho} did not arrive");

            double predicted = sd.TotalS1 * rho * rho * rho / (2.0 * nPrime * uPrime);
            double error = landing.Y / predicted - 1.0;

            if (rho == 0.0625)
                Assert.True(Math.Abs(error) < 2e-3,
                    $"{what}: at rho = {rho} the traced ray is {landing.Y:E8} and S1 predicts "
                  + $"{predicted:E8}, out by {100.0 * error:F3}%. At this aperture the fifth "
                  + "order is worth about 0.04%, so a residual this size is a missing factor - "
                  + $"n' is {nPrime}, and 1/n' and n' would show as {100.0 * (1.0 / nPrime - 1.0):F1}% "
                  + $"and {100.0 * (nPrime - 1.0):F1}%.");

            // Halving the pupil must quarter what is left over: the residual is the fifth order.
            if (!double.IsNaN(previous))
                Assert.True(Math.Abs(error) < 0.35 * Math.Abs(previous),
                    $"{what}: the residual went from {previous:E3} to {error:E3} when the pupil "
                  + "was halved. The fifth order would have fallen by four; this has not, so "
                  + "what is left is not truncation.");
            previous = error;
        }
    }

    // ── The two coefficient sets against each other ─────────────────────────────────────

    /// <summary>
    /// Seidel and Buchdahl compute third-order spherical by different routes, in different
    /// measures: <c>S1</c> is a wave-aberration sum and <c>B</c> is already transverse. The
    /// conversion between them is the one factor an immersed image space changes,
    /// <c>B = S1 / (2 n' u')</c>, and it holds exactly in both spaces at every index.
    ///
    /// <para>Both sets are read from the same paraxial trace, so this does not prove the trace
    /// right - <see cref="SeidelSphericalPredictsATracedRayWithAnImmersedImageSpace"/> does
    /// that. What it proves is that the two routes carry the immersion the same way, which is
    /// what a report mixing coefficients from both needs.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void SeidelAndBuchdahlCarryTheImmersionTheSameWay(double index)
    {
        foreach (var (sys, n, field, what) in new[]
        {
            (ImmersedImage(index).Sys,  ImmersedImage(index).N,  3.0,  $"image n' = {index}"),
            (ImmersedObject(index).Sys, ImmersedObject(index).N, 10.0, $"object n = {index}"),
        })
        {
            int last = sys.LastOpticalSurface();
            var p = ParaxialTrace.Trace(sys, n, field);
            var sd = SeidelCoefficients.Compute(sys, n, n, n, p);
            var b = BuchdahlCoefficients.Compute(sys, p);

            double converted = sd.TotalS1 / (2.0 * p.N[last] * p.U[last]);
            double scale = Math.Max(Math.Abs(converted), Math.Abs(b.Totals.B));
            Assert.True(Math.Abs(converted - b.Totals.B) / scale < 1e-10,
                $"{what}: S1/(2 n' u') is {converted:E12} and Buchdahl's B is {b.Totals.B:E12}. "
              + "One of the two routes is carrying the index differently from the other.");
        }
    }

    // ── First-order quantities that the index appears in ────────────────────────────────

    /// <summary>
    /// The Lagrange invariant carries the object-space index outright, and is then conserved
    /// through the system.
    ///
    /// <para>In object space the two rays are fixed by the pupil and the field, not by the
    /// medium, so immersing the object multiplies <c>H = n (ubar y - u ybar)</c> by exactly that
    /// index and nothing else. It must then hold that value at every surface: the drift is the
    /// trace's own self-check and immersion must not disturb it.</para>
    /// </summary>
    [Theory]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheInvariantScalesWithTheObjectIndexAndIsStillConserved(double nObject)
    {
        var (air, nAir) = ImmersedObject(1.0);
        var (wet, nWet) = ImmersedObject(nObject);

        var pAir = ParaxialTrace.Trace(air, nAir, 10.0);
        var pWet = ParaxialTrace.Trace(wet, nWet, 10.0);

        double ratio = pWet.LagrangeInvariant / pAir.LagrangeInvariant;
        Assert.True(Math.Abs(ratio - nObject) < 1e-12,
            $"immersing the object in n = {nObject} multiplied the invariant by {ratio:E12}; "
          + "the two rays in object space are the same rays, so it must be the index exactly.");

        Assert.True(pWet.InvariantDrift < 1e-13,
            $"the invariant drifts by {pWet.InvariantDrift:E3} through an immersed system, so "
          + "the trace is not carrying the index consistently from surface to surface.");
    }

    /// <summary>
    /// The F/number the coefficients are normalised to is the one the NUMERICAL APERTURE
    /// implies, <c>1 / (2 n' sin u')</c> paraxially, and not the geometric cone alone.
    ///
    /// <para>This is the factor that would silently rescale every Buchdahl total on an immersed
    /// design, since they are all normalised to it. Immersing image space at a fixed ray angle
    /// raises the numerical aperture by <c>n'</c> and so lowers the F/number by <c>n'</c>, and
    /// the two forms agree to the bit.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheFNumberCarriesTheImageSpaceIndex(double nImage)
    {
        var (sys, n) = ImmersedImage(nImage);
        int last = sys.LastOpticalSurface();
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        var b = BuchdahlCoefficients.Compute(sys, p);

        double na = Math.Abs(p.N[last] * p.U[last]);
        double fromNa = 1.0 / (2.0 * na);
        Assert.True(Math.Abs(b.FNumber - fromNa) / fromNa < 1e-12,
            $"n' = {nImage}: the coefficients are normalised to F/{b.FNumber:F6} while the "
          + $"numerical aperture {na:F6} says F/{fromNa:F6}. Every total is scaled by this.");
    }

    /// <summary>
    /// The focal length this program reports is <c>n_object / phi</c> - the convention its own
    /// comment states - and not <c>-y/u'</c>, which the two part company over as soon as image
    /// space stops being air.
    ///
    /// <para>Pinned because it is a CONVENTION and not a theorem: the two differ by a factor of
    /// <c>n'/n</c>, they agree on every design in this repository, and a reader comparing an
    /// immersed design against a program that prints the other one will find a discrepancy that
    /// is not an error in either.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheFocalLengthIsTheObjectSpaceOne(double nImage)
    {
        var (sys, n) = ImmersedImage(nImage);
        var p = ParaxialTrace.Trace(sys, n, 0.0);

        Assert.True(Math.Abs(p.Efl * p.Power - p.N[0]) < 1e-12,
            $"efl * power is {p.Efl * p.Power:E12} where the stated convention makes it the "
          + $"object index, {p.N[0]}.");

        // The image-space form, for the record: the same lens is f' = n' / phi.
        double imageSideEfl = p.N[sys.LastOpticalSurface()] / p.Power;
        Assert.True(Math.Abs(imageSideEfl / p.Efl - nImage) < 1e-12,
            "the image-side focal length must exceed the object-side one by exactly n'.");
    }

    // ── The whole axial series against traced rays, at every order at once ──────────────

    /// <summary>
    /// <c>B</c>, <c>B5</c> and <c>B7</c> - the spherical aberration of the third, fifth and
    /// seventh orders - fitted straight off real traced rays, with the object medium immersed.
    ///
    /// <para>On axis the transverse aberration is
    /// <c>eps(rho) = B rho^3 + B5 rho^5 + B7 rho^7 + ...</c>, so tracing a spread of pupil
    /// fractions and fitting that polynomial asks the LENS what the coefficients are. Nothing is
    /// normalised, no convention is assumed and no other implementation is consulted - which is
    /// what makes it worth having, because the fifth order has no independent reference here at
    /// all and this reaches its spherical part.</para>
    ///
    /// <para><b>The ninth-order term is carried in the fit and thrown away.</b> Without it the
    /// ninth order lands on <c>a7</c> and the comparison reads 1.26 even in air, where B7 is
    /// known good - the fit's own truncation, mistaken for a finding. With it the residuals fall
    /// to 1E-4 on the fifth order and under a per cent on the seventh.</para>
    ///
    /// <para><b>What is actually asserted is that the residual does not depend on the object
    /// index.</b> A fit residual is a property of the fit; an index error is not. If <c>B5</c> or
    /// <c>B7</c> carried a wrong power of <c>n</c>, the ratio at 1.30 would be tens of per cent
    /// from the ratio in air. It is within a per cent of it, and moves the wrong way.</para>
    /// </summary>
    [Theory]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheAxialSeriesMatchesTracedRaysWithTheObjectImmersed(double nObject)
    {
        var (air, nAir) = AxialSystem(1.0);
        var (wet, nWet) = AxialSystem(nObject);

        var (bAir, b5Air, b7Air) = AxialRatios(air, nAir);
        var (bWet, b5Wet, b7Wet) = AxialRatios(wet, nWet);

        // Each coefficient agrees with the rays to the fit's own accuracy...
        Assert.True(Math.Abs(bWet - 1.0) < 1e-5, $"n = {nObject}: B is out by {bWet - 1.0:E3}");
        Assert.True(Math.Abs(b5Wet - 1.0) < 2e-3, $"n = {nObject}: B5 is out by {b5Wet - 1.0:E3}");
        Assert.True(Math.Abs(b7Wet - 1.0) < 2e-2, $"n = {nObject}: B7 is out by {b7Wet - 1.0:E3}");

        // ...and, the sharper statement, by the SAME amount as in air.
        Assert.True(Math.Abs(b5Wet - b5Air) < 1e-3,
            $"n = {nObject}: the fifth order sits {b5Wet:F6} of the traced value against "
          + $"{b5Air:F6} in air. A fit residual does not depend on the object medium; a wrong "
          + "power of n does.");
        Assert.True(Math.Abs(b7Wet - b7Air) < 1e-2,
            $"n = {nObject}: the seventh order sits {b7Wet:F6} of the traced value against "
          + $"{b7Air:F6} in air.");
    }

    private static (OpticalSystem Sys, double[] N) AxialSystem(double n0)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(3.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "G", IsStop = true, SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 60.0,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 25.0 });
        return (s, new[] { n0, 1.6, 1.0, 1.0 });
    }

    /// <summary>Fitted-over-stated ratio for B, B5 and B7 on one system.</summary>
    private static (double B, double B5, double B7) AxialRatios(OpticalSystem sys, double[] n)
    {
        var p = ParaxialTrace.Trace(sys, n, 3.0);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, 3.0);

        double[] rhos = { 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70 };
        var fit = LeastSquares(sys, n, p, rhos, new[] { 3, 5, 7, 9 });
        return (fit[0] / b.Totals.B, fit[1] / b.Totals.B5, fit[2] / b.Totals.B7);
    }

    /// <summary>Odd-polynomial least squares on traced axial rays, by normal equations.</summary>
    private static double[] LeastSquares(OpticalSystem sys, double[] n, ParaxialResult p,
                                         double[] rhos, int[] powers)
    {
        int m = rhos.Length, k = powers.Length;
        var A = new double[m, k];
        var y = new double[m];
        for (int i = 0; i < m; i++)
        {
            var land = RealRayTrace.TraceFrom(sys, n, p, 0.0, rhos[i] * p.Y[1], 0.0,
                                              rhos[i] * p.U[0], 1.0);
            Assert.True(land.Ok, $"the ray at rho = {rhos[i]} did not arrive");
            y[i] = land.Y;
            for (int j = 0; j < k; j++) A[i, j] = Math.Pow(rhos[i], powers[j]);
        }

        var M = new double[k, k];
        var rhs = new double[k];
        for (int a = 0; a < k; a++)
        {
            for (int c = 0; c < k; c++)
                for (int i = 0; i < m; i++) M[a, c] += A[i, a] * A[i, c];
            for (int i = 0; i < m; i++) rhs[a] += A[i, a] * y[i];
        }
        for (int c = 0; c < k; c++)
        {
            int piv = c;
            for (int r = c + 1; r < k; r++) if (Math.Abs(M[r, c]) > Math.Abs(M[piv, c])) piv = r;
            if (piv != c)
            {
                for (int j = 0; j < k; j++) (M[c, j], M[piv, j]) = (M[piv, j], M[c, j]);
                (rhs[c], rhs[piv]) = (rhs[piv], rhs[c]);
            }
            for (int r = c + 1; r < k; r++)
            {
                double f = M[r, c] / M[c, c];
                for (int j = c; j < k; j++) M[r, j] -= f * M[c, j];
                rhs[r] -= f * rhs[c];
            }
        }
        var x = new double[k];
        for (int r = k - 1; r >= 0; r--)
        {
            double sum = rhs[r];
            for (int j = r + 1; j < k; j++) sum -= M[r, j] * x[j];
            x[r] = sum / M[r, r];
        }
        return x;
    }

    // ── The seventh order under immersion, which used to be wrong on the object side ────

    /// <summary>
    /// The twenty tertiary coefficients, Buchdahl's route against Forbes' series trace and
    /// against real traced rays, with each end medium immersed in turn.
    ///
    /// <para><b>These held a defect for a day and now hold its fix.</b> Object-space immersion
    /// used to break the seventh order - 0.24% out at <c>n = 1.01</c> and 23% at
    /// <c>n = 1.30</c>, worst at tau2, with Forbes and real rays agreeing with each other
    /// against the table. The cause was that Buchdahl's scheme requires its p and q rays to
    /// carry a Lagrange invariant of ONE, which holds only when object space is air: the pair's
    /// invariant is exactly <c>N_0</c>. The q ray is now started in the reduced coordinates the
    /// scheme's own comment names, and the field normalisation carries the matching <c>N_0</c>.
    /// See the note at the ray start in <c>BuchdahlTableI</c>.</para>
    ///
    /// <para><b>It was not the focal length convention</b>, which was the first suspect, since
    /// <c>Efl</c> is the length scale the whole chain is normalised to and it carries the OBJECT
    /// index. Substituting <c>1/phi</c> or <c>n'/phi</c> for it changes the answer by not one
    /// bit - that normalisation cancels.</para>
    ///
    /// <para>The fifth order has still not been checked under immersion, for want of an
    /// independent fifth-order reference that works at a finite conjugate.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00, 2e-12)]
    [InlineData(1.01, 2e-12)]
    [InlineData(1.30, 2e-12)]
    public void TheSeventhOrderSurvivesIMAGESpaceImmersion(double nImage, double tolerance)
    {
        var (sys, n) = ImmersedImage(nImage);
        var (table, forbes, _, largest) = Tertiary(sys, n, 3.0);

        double worst = Worst(table, forbes!, largest, out int at);
        Assert.True(worst < tolerance,
            $"n' = {nImage}: Buchdahl's tau{at} is {table[at]:E8} and Forbes' is {forbes![at]:E8}, "
          + $"a relative {worst:E3}. The two routes share no arithmetic, so image-space "
          + "immersion has stopped being carried by one of them.");
    }

    /// <summary>
    /// The object-side counterpart, which is the one that was wrong. See the note above.
    ///
    /// <para>The tolerance is the same at every index, which is the whole content of the fix:
    /// immersing the object must not cost accuracy, and it no longer does.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheSeventhOrderSurvivesOBJECTSpaceImmersion(double nObject)
    {
        var (sys, n) = ImmersedObject(nObject);
        var (table, forbes, _, largest) = Tertiary(sys, n, 10.0);

        double worst = Worst(table, forbes!, largest, out int at);
        Assert.True(worst < 2e-12,
            $"n = {nObject}: Buchdahl's tau{at} is {table[at]:E8} and Forbes' is {forbes![at]:E8}, "
          + $"a relative {worst:E3}. Before the ray pair was given unit Lagrange invariant this "
          + "read 2.4E-3 at n = 1.01 and 8.7E-2 at n = 1.30, so a number of that size here means "
          + "the reduction at the ray start has been lost again.");
    }

    /// <summary>
    /// All three routes at once, on an object immersed in 1.30: Buchdahl's table, Forbes' series
    /// trace and real traced rays.
    ///
    /// <para>This is the test that decided the question. Before the fix, Forbes and the rays
    /// agreed with each other to better than 1E-5 and the table disagreed with both by 23% - two
    /// independent witnesses against one implementation, which is what said the table was the
    /// one at fault rather than merely different. All three now agree.</para>
    ///
    /// <para>The conjugate is INFINITE because that is the only place the ray inversion works,
    /// and an object at infinity immersed in a medium is contrived on purpose: it puts real rays
    /// onto the case that failed.</para>
    /// </summary>
    [Fact]
    public void TheTableForbesAndRealRaysAllAgreeWithObjectSpaceImmersed()
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(5.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                     Material = "IMMERSED", IsStop = true, SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 60.0,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0, SemiDiameter = 25.0 });
        var n = new[] { 1.30, 1.6, 1.0, 1.0 };

        var (table, forbes, rays, largest) = Tertiary(s, n, 5.0);
        Assert.NotNull(rays);

        double betweenReferences = Worst(forbes!, rays!, largest, out int k1);
        double tableAgainstForbes = Worst(table, forbes!, largest, out int k2);
        double tableAgainstRays = Worst(table, rays!, largest, out int k3);

        Assert.True(betweenReferences < 1e-5,
            $"Forbes and the rays differ by {betweenReferences:E3} at tau{k1}; they must agree "
          + "with each other for either to be worth comparing against.");
        Assert.True(tableAgainstForbes < 2e-12,
            $"the table differs from Forbes by {tableAgainstForbes:E3} at tau{k2} with the "
          + "object immersed in 1.30. That was 2.3E-1 before the ray pair was given unit "
          + "invariant.");
        Assert.True(tableAgainstRays < 1e-5,
            $"the table differs from the traced rays by {tableAgainstRays:E3} at tau{k3}, "
          + "against a ray-fit noise floor of about 6E-7.");
    }

    /// <summary>
    /// The same immersion on a FIGURED system, which is the one combination the fix could still
    /// have got wrong.
    ///
    /// <para>A figured design takes the Sec. 85 arrangement rather than Buchdahl's own, and that
    /// route runs the scheme four times - direct and dual, each unfigured then figured - with the
    /// DUAL run interchanging the p and q rays AFTER the reduction that this fix applies. If the
    /// reduction belonged on the other side of that swap, spheres would still be right and
    /// figured systems would not. They are right: 3E-15 at every index, against a route that
    /// shares no arithmetic with the scheme.</para>
    ///
    /// <para>Infinite conjugate deliberately. A figured system at a FINITE conjugate disagrees
    /// with Forbes by about 5E-5 - in air as much as immersed, so it is nothing to do with this
    /// fix - and that combination appears in no fixture here. It is recorded under *What is not
    /// established* in docs/verification.md rather than hidden inside a tolerance here.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheSeventhOrderSurvivesImmersionOnAFiguredSystem(double nObject)
    {
        var s = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 16.0) };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(5.0));
        s.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        s.Surfaces.Add(new Surface
        {
            Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0, Material = "IMMERSED",
            SemiDiameter = 15.0, Type = SurfaceType.EvenAsphere, Conic = -0.6,
        });
        s.Surfaces[1].AsphericCoefficients[1] = 2.0e-7;
        s.Surfaces[1].AsphericCoefficients[2] = 1.0e-11;
        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 8.0,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 60.0, IsStop = true,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0, SemiDiameter = 25.0 });
        var n = new[] { nObject, 1.6, 1.0, 1.0, 1.0 };

        var (table, forbes, _, largest) = Tertiary(s, n, 5.0);
        double worst = Worst(table, forbes!, largest, out int at);

        Assert.True(worst < 1e-12,
            $"figured, object index {nObject}: the aspheric arrangement and Forbes differ by "
          + $"{worst:E3} at tau{at}. The dual run swaps the two rays after the reduction, so if "
          + "that reduction is on the wrong side of the swap this is where it shows.");
    }

    /// <summary>The tertiary set three ways: the table, Forbes, and real rays where possible.</summary>
    private static (double[] Table, double[]? Forbes, double[]? Rays, double Largest)
        Tertiary(OpticalSystem sys, double[] n, double field)
    {
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);

        var table = new double[21];
        double largest = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            table[k] = k == 1 ? b.Totals.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;
            largest = Math.Max(largest, Math.Abs(table[k]));
        }

        var forbes = AberrationCalculator.Core.Forbes.ForbesCoefficients.Invert(sys, n, p, field);
        Assert.NotNull(forbes);

        double[]? rays = null;
        if (p.InfiniteConjugate)
        {
            try { rays = CoefficientInversion.Invert(sys, n, p, field)?.Tau; }
            catch (NotSupportedException) { }
        }
        return (table, forbes!.Tau, rays, largest);
    }

    private static double Worst(double[] a, double[] b, double largest, out int at)
    {
        double worst = 0.0; at = 0;
        for (int k = 1; k <= 20; k++)
        {
            double d = Math.Abs(a[k] - b[k]) / largest;
            if (d > worst) { worst = d; at = k; }
        }
        return worst;
    }

    // ── End to end, through the coefficients and back to rays ───────────────────────────

    /// <summary>
    /// Best focus, predicted from the coefficients and found by tracing, with image space
    /// immersed.
    ///
    /// <para>This is the whole chain at once: paraxial trace, Buchdahl totals, the defocus
    /// coupling in <see cref="Prms"/>, and the real ray trace. The predicted plane is a
    /// LONGITUDINAL distance in the immersion medium and the coefficients that produce it are
    /// normalised through <c>n'</c>, so any index left out anywhere in that chain moves the
    /// answer away from the traced minimum. The same test at <c>n' = 1</c> is
    /// <c>BestFocusTests.TheAxialPlaneAgreesWithTracedRays</c>; this is it with the medium
    /// changed underneath.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void BestFocusAgreesWithTracedRaysThroughAnImmersedImageSpace(double nImage)
    {
        var (sys, n) = ImmersedImage(nImage);
        int last = sys.LastOpticalSurface();
        // Traced at the full field and the coefficients normalised to it, as the report does;
        // the plane below is then asked for on axis. Attaching the tertiary set matters because
        // the defocus coupling reads seventh-order names as well as third.
        var p = ParaxialTrace.Trace(sys, n, 3.0);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, 3.0);

        double predicted = BestFocus.DeltaZ(b.Totals, 0.0, p.U[last]);
        Assert.True(Math.Abs(predicted) > 1e-4 && !double.IsNaN(predicted),
            $"n' = {nImage}: the predicted plane came back {predicted:G6}, so there is nothing "
          + "to compare against the traced one.");

        var hits = new List<(double X, double Y, double L, double M, double N)>();
        for (int r = 1; r <= 8; r++)
            for (int a = 0; a < 16; a++)
            {
                double rho = Math.Sqrt(r / 8.0);                  // equal-area rings
                double th = 2.0 * Math.PI * a / 16.0;
                var e = RealRayTrace.TraceRecord(sys, n, p, 0.0,
                                                 rho * Math.Cos(th), rho * Math.Sin(th))[sys.Surfaces.Count - 1];
                if (e.Ok) hits.Add((e.X, e.Y, e.L, e.M, e.N));
            }
        Assert.True(hits.Count > 100, "too few rays survived to measure anything");

        double span = Math.Abs(predicted) * 3.0, best = double.MaxValue, bestAt = 0.0;
        for (int i = 0; i <= 600; i++)
        {
            double z = -span + 2.0 * span * i / 600.0;
            double sx = 0.0, sy = 0.0, s2 = 0.0;
            foreach (var q in hits)
            {
                double x = q.X + z * q.L / q.N, y = q.Y + z * q.M / q.N;
                sx += x; sy += y; s2 += x * x + y * y;
            }
            double ms = s2 / hits.Count - (sx / hits.Count) * (sx / hits.Count)
                                        - (sy / hits.Count) * (sy / hits.Count);
            if (ms < best) { best = ms; bestAt = z; }
        }

        double tol = 0.1 * Math.Abs(predicted) + 1e-6;
        Assert.True(Math.Abs(bestAt - predicted) <= tol,
            $"n' = {nImage}: the rays put best focus at dZ = {bestAt:G6} and the coefficients "
          + $"predict {predicted:G6}. An index dropped anywhere between the sums and this plane "
          + "shows up here.");
    }

    // ── The NAT route, which consumes the same scheme ───────────────────────────────────

    /// <summary>
    /// The nodal-aberration-theory route reconciles with the Seidel sums at every end medium,
    /// at either conjugate, figured or not.
    ///
    /// <para><b>Why this is here rather than with the other NAT tests.</b> The immersion fix
    /// rescaled the scheme's q ray by <c>1/N_0</c>, and the tertiary route compensates for that
    /// in its field normalisation. <c>WaveFront</c> takes the scheme's rows and converts nothing,
    /// so the rescaling reaches its W coefficients uncompensated - and at <c>N_0 = 1.30</c> they
    /// genuinely are different numbers from before.</para>
    ///
    /// <para><b>That is harmless, and the reason is worth stating.</b>
    /// <see cref="NormalisationBridge"/> FITS the scale between the two routes from the
    /// third order rather than assuming it, so an overall rescaling of the field variable is
    /// absorbed into the fitted <c>F</c>. And the fit checks itself: four coefficients give four
    /// ratios against two unknowns, so two are free. Those two are what this test reads back.</para>
    ///
    /// <para>Twelve combinations, and the bridge residual is at machine precision in every one.
    /// A route that ASSUMED the normalisation would have failed the immersed cases the day the
    /// q ray was rescaled.</para>
    /// </summary>
    [Theory]
    [InlineData(1.00)]
    [InlineData(1.01)]
    [InlineData(1.30)]
    public void TheNatRouteReconcilesWithSeidelWhateverTheObjectMedium(double nObject)
    {
        foreach (bool infinite in new[] { true, false })
        foreach (bool figured in new[] { false, true })
        {
            var (sys, n) = NatSystem(nObject, figured, infinite);
            double field = infinite ? 5.0 : 10.0;
            var p = ParaxialTrace.Trace(sys, n, field);

            var wf = WaveFront.FromSystem(sys, n, p);
            Assert.NotNull(wf);

            var s = SeidelCoefficients.Compute(sys, n, n, n, p);
            var bridge = NormalisationBridge.Fit(
                new[] { s.TotalS1 / 8.0, s.TotalS2 / 2.0, s.TotalS3 / 2.0, s.TotalS5 / 2.0 },
                new[] { wf!.System.W040, wf.System.W131, wf.System.W222, wf.System.W311 });

            string what = $"n = {nObject}, {(infinite ? "infinite" : "finite")}, "
                        + $"{(figured ? "figured" : "spherical")}";

            Assert.True(bridge.IsUsable, $"{what}: the scale between the routes could not be fitted");
            Assert.True(bridge.Residual < 1e-12,
                $"{what}: the bridge's free checks disagree by {bridge.Residual:E3}. Two of the "
              + "four coefficients are not used to fit, so this is a real check on the W route "
              + "and not a tautology.");

            // The two free ones, read back into the design's own units.
            double s3 = bridge.WToSeidel(2, 2) * wf.System.W222 * 2.0;
            double s5 = bridge.WToSeidel(3, 1) * wf.System.W311 * 2.0;
            Assert.True(Math.Abs(s3 / s.TotalS3 - 1.0) < 1e-12, $"{what}: S3 from W is out");
            Assert.True(Math.Abs(s5 / s.TotalS5 - 1.0) < 1e-12, $"{what}: S5 from W is out");
        }
    }

    private static (OpticalSystem Sys, double[] N) NatSystem(double n0, bool figured, bool infinite)
    {
        var s = new OpticalSystem
        {
            Aperture = new Aperture(ApertureType.EPD, 16.0),
            FieldType = infinite ? FieldType.ObjectAngle : FieldType.ObjectHeight,
        };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(infinite ? 5.0 : 10.0));
        s.Surfaces.Add(new Surface { Index = 0,
            Thickness = infinite ? double.PositiveInfinity : 200.0 });

        var front = new Surface { Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0,
                                  Material = "G", SemiDiameter = 15.0 };
        if (figured)
        {
            front.Type = SurfaceType.EvenAsphere;
            front.Conic = -0.6;
            front.AsphericCoefficients[1] = 2.0e-7;
        }
        s.Surfaces.Add(front);

        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 8.0,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 60.0, IsStop = true,
                                     SemiDiameter = 15.0 });
        s.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0, SemiDiameter = 25.0 });
        return (s, new[] { n0, 1.6, 1.0, 1.0, 1.0 });
    }
}
