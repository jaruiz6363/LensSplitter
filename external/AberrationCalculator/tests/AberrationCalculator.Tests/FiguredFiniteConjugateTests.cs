using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A FIGURED system at a FINITE conjugate, against Forbes' series trace.
///
/// <para><b>This combination was in no fixture and had never been compared.</b> Every figured
/// design the Forbes tests name has its object at infinity, and every finite-conjugate one is a
/// system of spheres. The gap was found on 20 September 2026 while checking that the immersion
/// fix had reached the aspheric route: it had, and this turned up beside it, a disagreement of
/// about 5E-5 where spheres hold 5E-15.</para>
///
/// <para><b>The cause was the stop parameter the aspheric increments were differenced against.</b>
/// <c>TertiaryCoefficients.Attach</c> builds an all-spherical table for the increments to be
/// measured from, and built it at <c>scheme.P</c> - the value the scheme derives from the q/p
/// ray-height ratio - while the run those increments are fed back into uses the paraxial
/// entrance pupil position instead. The code's own comment says why the derived value cannot be
/// used at a finite conjugate: it rests on an identity that a non-zero <c>iota</c> breaks. So the
/// increments were taken against a reference built for a different pupil.</para>
///
/// <para><b>Why nothing saw it.</b> On a system of spheres the increments are null and that table
/// is never used. At an infinite conjugate the two stop parameters are the same expression. Only
/// figured AND finite reaches it, and nothing here was both.</para>
/// </summary>
public class FiguredFiniteConjugateTests
{
    private const double D = 0.5875618;

    /// <param name="stopSurface">Which surface carries the stop - 1 makes the stop parameter
    /// zero, 3 makes it large, and the defect scaled with it.</param>
    private static (OpticalSystem Sys, double[] N) Build(
        bool figured, bool infinite, int stopSurface, double objectDistance, double nObject)
    {
        var s = new OpticalSystem
        {
            Aperture = new Aperture(ApertureType.EPD, 16.0),
            FieldType = infinite ? FieldType.ObjectAngle : FieldType.ObjectHeight,
        };
        s.Wavelengths.Add(new Wavelength(D, 1.0, true));
        s.Fields.Add(new Field(0.0));
        s.Fields.Add(new Field(infinite ? 5.0 : 10.0));
        s.Surfaces.Add(new Surface
        {
            Index = 0,
            Thickness = infinite ? double.PositiveInfinity : objectDistance,
        });

        var front = new Surface
        {
            Index = 1, Curvature = 1.0 / 60.0, Thickness = 4.0, Material = "G",
            SemiDiameter = 15.0, IsStop = stopSurface == 1,
        };
        if (figured)
        {
            front.Type = SurfaceType.EvenAsphere;
            front.Conic = -0.6;
            front.AsphericCoefficients[1] = 2.0e-7;
            front.AsphericCoefficients[2] = 1.0e-11;
        }
        s.Surfaces.Add(front);

        s.Surfaces.Add(new Surface { Index = 2, Curvature = -1.0 / 60.0, Thickness = 8.0,
                                     SemiDiameter = 15.0, IsStop = stopSurface == 2 });
        s.Surfaces.Add(new Surface { Index = 3, Thickness = 60.0, SemiDiameter = 15.0,
                                     IsStop = stopSurface == 3 });
        s.Surfaces.Add(new Surface { Index = 4, Thickness = 0.0, SemiDiameter = 25.0 });
        return (s, new[] { nObject, 1.6, 1.0, 1.0, 1.0 });
    }

    /// <summary>Worst tau2..tau20 disagreement with Forbes, relative to the largest of them.</summary>
    private static double WorstAgainstForbes(OpticalSystem sys, double[] n, double field,
                                             out int at)
    {
        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var forbes = ForbesCoefficients.Invert(sys, n, p, field);
        Assert.NotNull(forbes);

        double largest = 0.0;
        var table = new double[21];
        for (int k = 1; k <= 20; k++)
        {
            table[k] = k == 1 ? b.Totals.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;
            largest = Math.Max(largest, Math.Abs(table[k]));
        }

        double worst = 0.0; at = 0;
        for (int k = 2; k <= 20; k++)
        {
            double d = Math.Abs(table[k] - forbes!.Tau[k]) / largest;
            if (d > worst) { worst = d; at = k; }
        }
        return worst;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (int stop in new[] { 1, 3 })
        {
            yield return new object[] { stop, 200.0, 1.0 };
            yield return new object[] { stop, 1000.0, 1.0 };
            yield return new object[] { stop, 10000.0, 1.0 };
            yield return new object[] { stop, 200.0, 1.30 };   // and immersed with it
        }
    }

    /// <summary>
    /// The case that was wrong. Both stop positions, three object distances, and one immersed
    /// object medium for good measure - Forbes shares no arithmetic with the scheme, so
    /// agreement at 1E-14 is agreement about the optics and not about a convention.
    ///
    /// <para>Before the fix these read 4.7E-05 and worse. The stop at surface 1 makes the stop
    /// parameter zero, where the two candidate values coincide and the defect vanishes; the stop
    /// at surface 3 is where it showed. Both are kept so that a regression cannot hide in the
    /// easy one.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AFiguredSystemAtAFiniteConjugateAgreesWithForbes(
        int stopSurface, double objectDistance, double nObject)
    {
        var (sys, n) = Build(true, false, stopSurface, objectDistance, nObject);
        double worst = WorstAgainstForbes(sys, n, 10.0, out int at);

        Assert.True(worst < 1e-13,
            $"stop at surface {stopSurface}, object {objectDistance} away, n = {nObject}: the "
          + $"scheme and Forbes differ by {worst:E3} at tau{at}. This read 4.7E-05 while the "
          + "aspheric increments were differenced against a table built at the scheme's derived "
          + "stop parameter rather than the paraxial one.");
    }

    /// <summary>
    /// The three neighbouring cases, which were always right and must stay so: spheres at either
    /// conjugate, and a figured system at infinity. They are here because the fix touches the
    /// table all four share, and because a fix that quietly moved one of these would be a worse
    /// bargain than the defect.
    /// </summary>
    [Theory]
    [InlineData(true, false, 1)]    // figured, infinite, stop at the lens
    [InlineData(true, false, 3)]    // figured, infinite, stop behind it
    [InlineData(false, true, 3)]    // spheres, finite
    [InlineData(false, false, 3)]   // spheres, infinite
    public void TheNeighbouringCasesAreUndisturbed(bool figured, bool finite, int stopSurface)
    {
        var (sys, n) = Build(figured, !finite, stopSurface, 200.0, 1.0);
        double worst = WorstAgainstForbes(sys, n, finite ? 10.0 : 5.0, out int at);

        Assert.True(worst < 1e-13,
            $"figured={figured} finite={finite} stop={stopSurface}: {worst:E3} at tau{at}.");
    }
}
