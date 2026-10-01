using System;
using System.Collections.Generic;
using System.Reflection;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Figuring beyond r^8 takes no part in the third, fifth or seventh order, and the claim this
/// repository makes about it is a strong one: not that the terms are dropped, but that they
/// CANNOT appear. A deformation <c>A_n r^n</c> first contributes at wave order <c>n</c>, which
/// is transverse order <c>n-1</c> - so r^4 reaches the third, r^6 the fifth, r^8 the seventh,
/// and r^10 reaches the NINTH and nothing below it.
///
/// <para><b>Both halves have to be tested or neither means anything.</b> That the coefficients
/// do not move is half; on its own it would pass just as well against code that threw the
/// coefficient away before it reached the sag, which is a real bug and a silent one - the same
/// bug the r^2 term actually had once, recorded in <see cref="AsphericR2TermTests"/>. So the
/// other half is that the surface really is different: real rays must land elsewhere, and the
/// displacement must fall off at NINTH order in the scale, which is what "it cannot reach the
/// seventh" means when it is stated as something measurable rather than as an assertion.</para>
///
/// <para>Both ZPL macros say this to the user - `BUCH7_ASPH` prints a note per surface and
/// per parameter when it meets one, `FORBES` likewise. Nothing was checking that the statement
/// they make is true of the code they stand beside.</para>
/// </summary>
public class AsphericBeyondR8Tests
{
    /// <summary>
    /// Put figuring beyond r^8 on the first figured surface, sized so that its sag at the edge
    /// of that surface is <paramref name="edgeSag"/> in lens units - which is how an aspheric
    /// term has to be sized to mean anything, since the raw coefficients of different powers
    /// are not comparable with one another.
    /// </summary>
    /// <param name="onlyAlreadyFigured">
    /// True picks a surface that ALREADY carries figuring, so that <c>Surface.IsFigured</c> is
    /// unchanged and the tertiary computation stays on the route it was already taking. That
    /// distinction decides whether bit-identity is available - see the two tests below.
    /// </param>
    private static int AddBeyondR8(OpticalSystem sys, double edgeSag, bool onlyAlreadyFigured,
                                   params int[] slots)
    {
        for (int i = 1; i <= sys.LastOpticalSurface(); i++)
        {
            var s = sys.Surfaces[i];
            if (onlyAlreadyFigured && !s.IsFigured) continue;

            // Semi-diameters are not set in every fixture format, and the exact radius does not
            // matter here - only that the sag it implies is large enough to be worth noticing.
            double r = s.SemiDiameter > 0.0 ? s.SemiDiameter
                     : sys.Aperture.Type == ApertureType.EPD ? 0.5 * sys.Aperture.Value
                     : 5.0;

            var a = new double[Math.Max(8, s.AsphericCoefficients.Length)];
            Array.Copy(s.AsphericCoefficients, a, s.AsphericCoefficients.Length);
            foreach (int k in slots) a[k] = edgeSag / Math.Pow(r, 2 * k + 2);
            s.AsphericCoefficients = a;
            if (s.Type == SurfaceType.Standard) s.Type = SurfaceType.EvenAsphere;
            return i;
        }
        throw new InvalidOperationException(
            onlyAlreadyFigured ? "no already-figured surface in this design" : "no surface");
    }

    private sealed record Run(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                              BuchdahlTerms Totals, double Field);

    private static Run Analyse(string name, double edgeSag, bool onlyAlreadyFigured,
                               params int[] slots)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        if (edgeSag != 0.0) AddBeyondR8(sys, edgeSag, onlyAlreadyFigured, slots);

        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value, new List<string>());
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return new Run(sys, n, p, b.Totals, field);
    }

    // ── Half one: nothing moves ─────────────────────────────────────────────────────────

    /// <summary>
    /// On a surface that is figured ALREADY, adding r^10, r^12, r^14 and r^16 must leave every
    /// coefficient BIT-IDENTICAL. Nothing about the route changes, so nothing about the
    /// arithmetic changes, and there is no tolerance to argue about.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    [InlineData("Ladder1_Conic")]
    [InlineData("Ladder1_A4")]
    [InlineData("TertiaryTestbed_Triplet24")]
    public void OnAFiguredSurfaceNothingBeyondR8MovesABit(string name)
    {
        var plain = Analyse(name, 0.0, true);
        var figured = Analyse(name, 1e-3, true, 4, 5, 6, 7);     // r^10, r^12, r^14, r^16

        Assert.Equal(plain.Paraxial.Efl, figured.Paraxial.Efl, 15);

        foreach (var f in typeof(BuchdahlTerms).GetFields(BindingFlags.Public
                                                          | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(double)) continue;
            double a = (double)f.GetValue(plain.Totals)!;
            double c = (double)f.GetValue(figured.Totals)!;
            Assert.True(a.Equals(c),
                $"{name}: {f.Name} moved from {a:E17} to {c:E17} when figuring beyond r^8 was "
              + "added to a surface that was figured already. Those terms are ninth order and "
              + "cannot reach any coefficient this program computes, and nothing about the "
              + "route changed, so the number should be bit-identical.");
        }
    }

    /// <summary>
    /// On a surface that was SPHERICAL, the same addition agrees to roundoff rather than to the
    /// bit, and the reason is worth recording because it looks like a defect and is not.
    ///
    /// <para><c>Surface.IsFigured</c> is true for any aspheric term, r^10 included - correctly,
    /// since the surface really is aspheric - and that flag decides which route computes the
    /// tertiary coefficients. So adding r^10 to a sphere moves the whole design from the
    /// spherical scheme onto the aspheric one. The two are mathematically the same where the
    /// figuring cannot reach the order, and they differ in the last bit or two because they
    /// reach it by different arithmetic. Measured here at one or two parts in 1E16.</para>
    ///
    /// <para>So the term changes no value and does change which code path produces it. That is
    /// the honest statement, and a test demanding bit-identity here would be asserting something
    /// untrue about the implementation rather than something true about the optics.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Sphere")]
    public void OnASphericalSurfaceTheyMoveNothingButTheRoute(string name)
    {
        var plain = Analyse(name, 0.0, false);
        var figured = Analyse(name, 1e-3, false, 4, 5, 6, 7);

        Assert.Equal(plain.Paraxial.Efl, figured.Paraxial.Efl, 15);

        foreach (var f in typeof(BuchdahlTerms).GetFields(BindingFlags.Public
                                                          | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(double)) continue;
            double a = (double)f.GetValue(plain.Totals)!;
            double c = (double)f.GetValue(figured.Totals)!;
            double scale = Math.Max(Math.Abs(a), Math.Abs(c));
            if (scale < 1e-14) continue;
            Assert.True(Math.Abs(a - c) / scale < 1e-12,
                $"{name}: {f.Name} moved from {a:E17} to {c:E17} when figuring beyond r^8 was "
              + "added. A change at roundoff is the two routes differing; a change above 1E-12 "
              + "means the term is reaching the seventh order, which it cannot.");
        }
    }

    /// <summary>
    /// And the Forbes route ignores them for its own reasons rather than by sharing the same
    /// code: its figure is a power series in <c>p = r^2</c> truncated one degree past the
    /// trace, so at the seventh order it carries p to p^4 - r^2 to r^8 - and r^10 falls outside
    /// the truncation rather than being filtered out of a list.
    ///
    /// <para><b>Bit-identity is demanded here even on a surface that was spherical</b>, unlike
    /// the Buchdahl route above. There is no flag to flip: the series trace has no spherical
    /// special case to leave, so adding r^10 changes nothing about how the answer is reached and
    /// the last bit has no excuse to move.</para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Conic")]
    public void TheForbesRouteIgnoresThemToo(string name)
    {
        var plain = Analyse(name, 0.0, false);
        var figured = Analyse(name, 1e-3, false, 4, 5, 6, 7);

        var a = ForbesCoefficients.Invert(plain.System, plain.Indices, plain.Paraxial,
                                          plain.Field);
        var b = ForbesCoefficients.Invert(figured.System, figured.Indices, figured.Paraxial,
                                          figured.Field);
        Assert.NotNull(a);
        Assert.NotNull(b);

        for (int k = 1; k <= 20; k++)
            Assert.True(a!.Tau[k].Equals(b!.Tau[k]),
                $"{name}: tau{k} moved from {a.Tau[k]:E17} to {b.Tau[k]:E17} in the series "
              + "trace when figuring beyond r^8 was added.");
    }

    // ── Half two: but the surface is genuinely different ────────────────────────────────

    /// <summary>
    /// The guard on everything above. If r^10 were being discarded before it reached the sag,
    /// every assertion so far would still pass and the tests would be certifying a bug. So a
    /// real ray must land somewhere else.
    /// </summary>
    [Fact]
    public void FiguringBeyondR8IsNotSilentlyDiscarded()
    {
        var plain = Analyse("CookeTriplet", 0.0, false);
        var figured = Analyse("CookeTriplet", 1e-3, false, 4);

        var a = RealRayTrace.Trace(plain.System, plain.Indices, plain.Paraxial, 0.0, 1.0, 0.0);
        var b = RealRayTrace.Trace(figured.System, figured.Indices, figured.Paraxial,
                                   0.0, 1.0, 0.0);
        Assert.True(a.Ok && b.Ok);

        Assert.True(Math.Abs(a.Y - b.Y) > 1e-9,
            $"the marginal ray landed at {a.Y:E12} both with and without r^10 figuring, so the "
          + "term is not reaching the surface at all and the tests above prove nothing.");
    }

    /// <summary>
    /// And the displacement it does cause is NINTH order, which is the whole reason the
    /// coefficients are entitled to ignore it.
    ///
    /// <para>Shrinking the pupil by a factor of two must divide the displacement by 2^9 = 512.
    /// An r^8 term would give 2^7 = 128 and an r^12 term 2^11 = 2048, so the measurement
    /// distinguishes which order arrived rather than merely confirming that something falls
    /// off. Measured on axis, where the pupil radius is the only variable in play.</para>
    /// </summary>
    [Fact]
    public void TheDisplacementFromRTenthIsNinthOrderInThePupil()
    {
        var plain = Analyse("CookeTriplet", 0.0, false);
        var figured = Analyse("CookeTriplet", 1e-2, false, 4);

        double Displacement(double rho)
        {
            var a = RealRayTrace.Trace(plain.System, plain.Indices, plain.Paraxial, 0.0, rho, 0.0);
            var b = RealRayTrace.Trace(figured.System, figured.Indices, figured.Paraxial,
                                       0.0, rho, 0.0);
            Assert.True(a.Ok && b.Ok, $"trace failed at rho = {rho}");
            return Math.Abs(a.Y - b.Y);
        }

        // Away from full aperture, where the eleventh order the term also carries is smaller
        // than the floating-point floor of the difference.
        foreach (double rho in new[] { 0.8, 0.4 })
        {
            double ratio = Displacement(rho) / Displacement(rho / 2.0);
            Assert.True(ratio > 0.85 * 512.0 && ratio < 1.15 * 512.0,
                $"halving the pupil from rho = {rho} divided the r^10 displacement by "
              + $"{ratio:F1}, not by about 512. That is the order the term arrives at, and it "
              + "is what entitles the seventh-order coefficients to ignore it.");
        }
    }

    // ── And where it DOES start to matter ───────────────────────────────────────────────

    /// <summary>
    /// The other side of the claim, and the one that says the rule is a rule rather than a
    /// convenient exclusion: r^10 is absent at the seventh order because it cannot reach it, and
    /// PRESENT at the ninth because that is exactly the order it reaches.
    ///
    /// <para><c>ForbesTrace.Figure</c> builds the sag to <c>degree + 1</c>, so a trace truncated
    /// at degree 3 - the seventh order - carries p to p^4, which is r^2 to r^8, and one at degree
    /// 4 carries p^5, which is r^10. That is not a coincidence of the implementation: the highest
    /// deformation that can reach transverse order 2m+1 is r^(2m+2), which is exactly the term
    /// <c>degree + 1</c> admits. The same line of code gives the right answer at every order.</para>
    ///
    /// <para>So this test requires both things of one pair of traces: at degree 3 every monomial
    /// of the series is untouched by the term, and at degree 4 the degree-four part moves while
    /// everything below it still does not.</para>
    /// </summary>
    [Fact]
    public void RTenthIsDeadAtTheSeventhOrderAndLiveAtTheNinth()
    {
        var plain = Analyse("CookeTriplet", 0.0, false);
        var figured = Analyse("CookeTriplet", 1e-2, false, 4);

        ForbesTrace Trace(Run r, int degree)
            => ForbesCoefficients.Trace(r.System, r.Indices, r.Paraxial, degree);

        // Degree 3 - the seventh order. Nothing anywhere may move.
        var a3 = Trace(plain, 3);
        var b3 = Trace(figured, 3);
        for (int x = 0; x <= 3; x++)
            for (int y = 0; x + y <= 3; y++)
                for (int z = 0; x + y + z <= 3; z++)
                {
                    Assert.True(a3.S[x, y, z].Equals(b3.S[x, y, z]),
                        $"S[{x},{y},{z}] moved at degree 3 when r^10 was added: it cannot reach "
                      + "the seventh order.");
                    Assert.True(a3.T[x, y, z].Equals(b3.T[x, y, z]),
                        $"T[{x},{y},{z}] moved at degree 3 when r^10 was added.");
                }

        // Degree 4 - the ninth order. Everything below degree four still may not move, and
        // something AT degree four must.
        var a4 = Trace(plain, 4);
        var b4 = Trace(figured, 4);
        bool moved = false;
        for (int x = 0; x <= 4; x++)
            for (int y = 0; x + y <= 4; y++)
                for (int z = 0; x + y + z <= 4; z++)
                {
                    bool same = a4.S[x, y, z].Equals(b4.S[x, y, z])
                             && a4.T[x, y, z].Equals(b4.T[x, y, z]);
                    if (x + y + z < 4)
                        Assert.True(same,
                            $"[{x},{y},{z}] is of degree {x + y + z} and moved when r^10 was "
                          + "added. The term reaches the ninth order and nothing below it.");
                    else if (!same) moved = true;
                }

        Assert.True(moved,
            "the degree-four part of the trace did not move when r^10 was added, so the ninth "
          + "order is not seeing a term that is of exactly that order - and a ninth-order "
          + "extraction built on this trace would be wrong.");
    }
}
