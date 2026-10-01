using System;

using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Real rays at a FINITE conjugate.
///
/// <para>The trace itself never asked where the object was - it is handed a ray at surface one's
/// vertex plane and propagates it - so the restriction to an object at infinity was only ever in
/// the AIMING, which built the ray direction from the field angle alone. That is true of a
/// collimated beam and of nothing else: light from a finite object leaves at a direction that
/// depends on which pupil point it is going through.</para>
///
/// <para>The check that matters is not that a finite conjugate now traces. It is that what it
/// traces is the RIGHT ray, and the way to know is that a real ray must approach the paraxial one
/// as it shrinks toward the axis. If the launch geometry were wrong - object on the wrong side,
/// height from the wrong convention, pupil at the wrong distance - the two would disagree at
/// first order and no amount of shrinking would bring them together.</para>
/// </summary>
public class FiniteConjugateRayTests
{
    private const double ObjectDistance = 250.0;

    private static (OpticalSystem System, double[] Indices) Triplet(double objectDistance,
                                                                    FieldType fieldType)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        system.Surfaces[0].Thickness = objectDistance;
        system.FieldType = fieldType;

        double lambda = system.Wavelengths[system.PrimaryWavelengthIndex].Value;
        return (system, IndexResolver.Build(system, catalog, lambda));
    }

    /// <summary>
    /// A real ray shrinking toward the axis becomes the paraxial ray, at a finite conjugate.
    ///
    /// <para>Both the field and the pupil coordinate are scaled by the same factor, so every
    /// aberration falls as the cube of it while the paraxial part falls linearly. The ratio of
    /// the two therefore has to converge, and converge to one - and it does so from a
    /// third-order discrepancy, which is the signature of a launch that is geometrically right
    /// rather than merely close.</para>
    /// </summary>
    [Theory]
    [InlineData(FieldType.ObjectHeight, -20.0)]
    [InlineData(FieldType.ObjectAngle, -3.0)]
    public void ARealRayConvergesOnTheParaxialRayAsItShrinks(FieldType fieldType, double field)
    {
        var (system, n) = Triplet(ObjectDistance, fieldType);

        double previous = double.MaxValue;
        foreach (double s in new[] { 1e-1, 1e-2, 1e-3 })
        {
            double scaledField = field * s;
            var p = ParaxialTrace.Trace(system, n, scaledField);

            // The paraxial ray of this field and pupil fraction, at the image surface.
            int image = system.Surfaces.Count - 1;
            double py = 0.6 * s;
            double paraxial = py * p.Y[image] + p.Ybar[image];

            var landing = RealRayTrace.Trace(system, n, p, scaledField, py, 0.0,
                                             atParaxialFocus: false);
            Assert.True(landing.Ok, "the finite-conjugate ray did not get through");

            double error = Math.Abs(landing.Y - paraxial) / Math.Abs(paraxial);
            Assert.True(error < previous,
                $"scale {s}: relative departure {error:G4} did not fall (was {previous:G4})");
            previous = error;
        }

        // At a thousandth of the field and aperture the two must agree to better than a part in
        // a million; a launch wrong at first order could not.
        Assert.True(previous < 1e-6, $"still {previous:G4} adrift at the smallest ray");
    }

    /// <summary>
    /// The real chief ray lands where the paraxial one says, to within the distortion.
    ///
    /// <para>A blunter check than the one above and a more direct one: it says the object point
    /// is where the launch thinks it is, in the units the field type names.</para>
    /// </summary>
    [Fact]
    public void TheRealChiefRayAgreesWithTheParaxialImageHeight()
    {
        var (system, n) = Triplet(ObjectDistance, FieldType.ObjectHeight);

        const double smallObject = -0.2;              // small enough that distortion is nothing
        var p = ParaxialTrace.Trace(system, n, smallObject);
        var landing = RealRayTrace.Trace(system, n, p, smallObject, 0.0, 0.0,
                                         atParaxialFocus: false);

        Assert.True(landing.Ok);
        Assert.Equal(p.ImageHeight, landing.Y, 6);
    }

    /// <summary>
    /// At infinity the launch still builds exactly the collimated ray it always did.
    ///
    /// <para>Checked against the ray constructed by hand rather than against a number written
    /// down here, because a remembered constant only says the answer has not changed since
    /// somebody typed it. This says what the answer IS: a beam that is parallel in object space,
    /// crossing the entrance pupil at the fractional coordinate asked for. The traced values the
    /// rest of the suite guards were all measured through that branch.</para>
    /// </summary>
    [Fact]
    public void TheInfiniteConjugateStillLaunchesACollimatedRay()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        double lambda = system.Wavelengths[system.PrimaryWavelengthIndex].Value;
        var n = IndexResolver.Build(system, catalog, lambda);

        const double fieldDeg = 20.0, py = 1.0, px = 0.35;
        var p = ParaxialTrace.Trace(system, n, fieldDeg);

        // The same ray written out longhand: parallel in object space at the field angle,
        // crossing the pupil plane at (px, py) of the pupil radius, walked back to surface one.
        double alpha = fieldDeg * Math.PI / 180.0;
        double epr = 0.5 * p.Epd, ep = p.EntrancePupilPosition;
        double dy = Math.Sin(alpha), dz = Math.Cos(alpha);
        double back = -ep / dz;
        double y1 = py * epr + back * dy;
        double x1 = px * epr;

        var byLaunch = RealRayTrace.Trace(system, n, p, fieldDeg, py, px);
        var byHand = RealRayTrace.TraceFrom(system, n, p, x1, y1, 0.0, dy, dz);

        Assert.True(byLaunch.Ok);
        Assert.True(byHand.Ok);
        Assert.Equal(byHand.Y, byLaunch.Y, 12);
        Assert.Equal(byHand.Z, byLaunch.Z, 12);
    }

    // ── Through the optimiser ────────────────────────────────────────────────────────────

    /// <summary>
    /// Every operand, including the real-ray ones, evaluates at a finite conjugate - and its
    /// analytic derivative is right there too.
    /// </summary>
    [Fact]
    public void EveryOperandWorksAtAFiniteConjugateAndKeepsItsExactDerivative()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        system.Surfaces[0].Thickness = ObjectDistance;
        system.FieldType = FieldType.ObjectHeight;
        system.Fields.Clear();
        system.Fields.Add(new Field(0.0));
        system.Fields.Add(new Field(-14.0));
        system.Fields.Add(new Field(-20.0));

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 4 });
        vars.Add(new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(new[]
        {
            new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
            new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 1.0 },
            new Operand { Type = OperandType.TTL, Target = 60.0, Weight = 1.0 },
            new Operand { Type = OperandType.RY, Surface = 7, Py = 1.0, Hy = 1.0 },
            new Operand { Type = OperandType.RX, Surface = 4, Px = 0.8, Hy = 0.7 },
            new Operand { Type = OperandType.RZ, Surface = 3, Py = 0.9 },
            new Operand { Type = OperandType.RM, Surface = 6, Py = 0.7 },
            new Operand { Type = OperandType.PY, Surface = 4, Py = 0.7 },
            new Operand { Type = OperandType.LCF, Hy = 1.0 },
            new Operand { Type = OperandType.AXC },
            new Operand { Type = OperandType.DISTF, Hy = 1.0 },
            new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = 50.0 },
        });

        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, analytic.Failure);

        // Nothing may be silently zero: an operand that returned nothing because it could not
        // trace would pass a derivative check trivially.
        Assert.Contains(analytic.Values, v => Math.Abs(v) > 0.0);

        var x0 = design.Read();
        int m = merit.Operands.Count, nv = vars.Count;

        double largest = 0.0;
        for (int i = 0; i < m; i++)
            for (int j = 0; j < nv; j++)
                largest = Math.Max(largest, Math.Abs(analytic.Jacobian[i, j]));
        Assert.True(largest > 0.0);

        for (int j = 0; j < nv; j++)
        {
            double column = 0.0;
            for (int i = 0; i < m; i++)
                column = Math.Max(column, Math.Abs(analytic.Jacobian[i, j]));

            double magnitude = Math.Max(Math.Abs(x0[j]), 1.0);
            double h = column > 0.0 ? 1e-6 / column : 1e-6;
            h = Math.Min(h, 1e-4 * magnitude);
            h = Math.Max(h, 1e-13 * magnitude);

            var plus = (double[])x0.Clone(); plus[j] += h;
            design.Apply(plus);
            var rp = merit.Evaluate(false);

            var minus = (double[])x0.Clone(); minus[j] -= h;
            design.Apply(minus);
            var rm = merit.Evaluate(false);

            design.Apply((double[])x0.Clone());
            Assert.True(rp.Ok && rm.Ok);

            for (int i = 0; i < m; i++)
            {
                double numeric = (rp.Residuals[i] - rm.Residuals[i]) / (2.0 * h);
                double exact = analytic.Jacobian[i, j];
                double scale = Math.Max(Math.Abs(exact), Math.Abs(numeric));
                double tolerance = 2e-4 * scale + 1e-7 * largest;

                Assert.True(Math.Abs(exact - numeric) <= tolerance,
                    $"d({merit.Operands[i].Label})/d({vars[j].Name}): analytic {exact:G10}, " +
                    $"central difference {numeric:G10}");
            }
        }
    }
}
