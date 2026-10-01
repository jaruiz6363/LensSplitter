using System;

using AberrationCalculator.Core.Aberrations;
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
/// A figured flat facing collimated light - a Schmidt corrector plate - optimised.
///
/// <para>This was the last design the optimiser refused. The coefficients there are reached only
/// as the e^0 term of a Laurent series in the flat surface's curvature, and that route lived in
/// the plain-double build alone: in the differentiating build the call had no body, so the
/// optimiser would have had a right value beside a silently wrong derivative. It now runs in
/// <c>DualSeries</c>, a dual number whose value and derivative are each a Laurent series.</para>
/// </summary>
public class FlatCollimatedDerivativeTests
{
    private const string Fixture = "Ladder2_FlatFigured";

    private static (Design, OpticalSystem) Load(VariableSet vars)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(Fixture), catalog);
        return (new Design(system, catalog, vars), system);
    }

    private static VariableSet Vars(params Variable[] v)
    {
        var set = new VariableSet();
        set.AddRange(v);
        return set;
    }

    /// <summary>
    /// The fixture really is the singular case, so the rest of this file is testing what it
    /// claims to test rather than an ordinary aspheric design that happens to be nearly flat.
    /// </summary>
    [Fact]
    public void TheFixtureIsAFiguredFlatInCollimatedLight()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(Fixture), catalog);
        var design = new Design(system, catalog,
                                Vars(new Variable { Kind = VariableKind.Curvature, Surface = 3 }));

        var indices = design.Indices(design.PrimaryWave);
        var paraxial = ParaxialTrace.Trace(system, indices, design.MaxField);
        var flats = TertiaryCoefficients.SeriesOnlySurfaces(system, indices, paraxial);

        Assert.NotEmpty(flats);
    }

    /// <summary>
    /// <b>It is no longer refused.</b> The optimiser used to decline this design before the first
    /// evaluation, and the refusal was right while the derivative would have been wrong.
    /// </summary>
    [Fact]
    public void AFiguredFlatInCollimatedLightIsAccepted()
    {
        var (design, _) = Load(Vars(
            new Variable { Kind = VariableKind.Curvature, Surface = 3 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 }));

        Assert.Equal(2, design.Variables.Count);
    }

    /// <summary>
    /// <b>The differentiated route's VALUE equals the plain-double route's.</b>
    ///
    /// <para>This is the guard on the one duplication the fix carries. The formulas are shared -
    /// both builds compile the same Core sources - but the thirty lines of orchestration around
    /// them are written twice, once per arithmetic. If those ever drift, the derivative would
    /// belong to a different calculation from the value and nothing else here would notice: the
    /// Jacobian check below compares the derivative against differences of the SAME route, so it
    /// would pass happily while the value came from somewhere else.</para>
    /// </summary>
    [Fact]
    public void TheDifferentiatedSeriesRouteAgreesWithTheDoubleOneOnValue()
    {
        var (design, system) = Load(Vars(new Variable { Kind = VariableKind.Curvature, Surface = 3 }));

        var indices = design.Indices(design.PrimaryWave);
        var paraxial = ParaxialTrace.Trace(system, indices, design.MaxField);
        var reference = BuchdahlCoefficients.Compute(system, paraxial);
        TertiaryCoefficients.Attach(system, indices, paraxial, reference, design.MaxField);

        var merit = new MeritFunction(design);
        foreach (string name in BuchdahlTerms.Names)
            merit.Add(new Operand
            {
                Type = OperandType.ABER, Coefficient = name, Target = 0.0, Weight = 1.0,
            });

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        for (int i = 0; i < BuchdahlTerms.Names.Length; i++)
        {
            string name = BuchdahlTerms.Names[i];
            double want = reference.Totals[name];
            double got = r.Values[i];
            double tol = 1e-8 * Math.Max(1.0, Math.Abs(want));
            Assert.True(Math.Abs(want - got) <= tol,
                $"{name}: double route {want:G12}, series-of-dual route {got:G12}");
        }
    }

    /// <summary>
    /// <b>And the derivative is right.</b> Every one of the twenty tertiary coefficients, against
    /// central differences of the same quantity - so what is being checked is the derivative the
    /// dual half of the series carries, against the way the value actually moves.
    /// </summary>
    [Theory]
    [InlineData(VariableKind.Curvature, 3)]
    [InlineData(VariableKind.Thickness, 2)]
    [InlineData(VariableKind.Asphere4, 2)]
    public void TheTertiaryDerivativesMatchCentralDifferences(VariableKind kind, int surface)
    {
        var vars = Vars(new Variable { Kind = kind, Surface = surface });
        var (design, _) = Load(vars);

        var merit = new MeritFunction(design);
        for (int k = 2; k <= 20; k++)
            merit.Add(new Operand
            {
                Type = OperandType.ABER, Coefficient = "Tau" + k, Target = 0.0, Weight = 1.0,
            });

        var x0 = design.Read();
        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, analytic.Failure);

        int m = merit.Operands.Count;
        double largest = 0.0;
        for (int i = 0; i < m; i++) largest = Math.Max(largest, Math.Abs(analytic.Jacobian[i, 0]));
        Assert.True(largest > 0.0,
            "every tertiary derivative is zero, which is what the missing route used to produce");

        double magnitude = Math.Max(Math.Abs(x0[0]), 1.0);
        double h = Math.Max(Math.Min(1e-6 / largest, 1e-4 * magnitude), 1e-13 * magnitude);

        var plus = (double[])x0.Clone(); plus[0] += h;
        design.Apply(plus);
        var rp = merit.Evaluate(false);

        var minus = (double[])x0.Clone(); minus[0] -= h;
        design.Apply(minus);
        var rm = merit.Evaluate(false);

        design.Apply((double[])x0.Clone());
        Assert.True(rp.Ok && rm.Ok, "the design could not be evaluated beside the start point");

        for (int i = 0; i < m; i++)
        {
            double numeric = (rp.Residuals[i] - rm.Residuals[i]) / (2.0 * h);
            double exact = analytic.Jacobian[i, 0];
            double scale = Math.Max(Math.Abs(exact), Math.Abs(numeric));

            // Looser than the ordinary Jacobian check, and deliberately. The quantity being
            // differenced is itself the e^0 term of a truncated series, so the central difference
            // carries the series' own truncation error on top of its arithmetic one - it is the
            // less accurate of the two instruments here, not the more.
            double tolerance = 2e-3 * scale + 1e-6 * largest;

            Assert.True(Math.Abs(exact - numeric) <= tolerance,
                $"d({merit.Operands[i].Label})/d({vars[0].Name}): analytic {exact:G10}, " +
                $"central difference {numeric:G10}, differ by {Math.Abs(exact - numeric):G4} " +
                $"(tolerance {tolerance:G4})");
        }
    }

    /// <summary>
    /// The derivative is not merely present but DIFFERENT from what the broken path produced. The
    /// old behaviour was a zero column, so a test that only asked for agreement with differences
    /// could have been satisfied by a design where nothing moved.
    /// </summary>
    [Fact]
    public void TheTertiaryDerivativesAreNotAllZero()
    {
        var (design, _) = Load(Vars(new Variable { Kind = VariableKind.Asphere4, Surface = 2 }));

        var merit = new MeritFunction(design);
        for (int k = 2; k <= 20; k++)
            merit.Add(new Operand
            {
                Type = OperandType.ABER, Coefficient = "Tau" + k, Target = 0.0, Weight = 1.0,
            });

        var r = merit.Evaluate(true);
        Assert.True(r.Ok, r.Failure);

        int moving = 0;
        for (int i = 0; i < merit.Operands.Count; i++)
            if (Math.Abs(r.Jacobian[i, 0]) > 0.0) moving++;

        Assert.True(moving >= 10,
            $"only {moving} of the nineteen tertiary coefficients respond to the corrector's r^4 term");
    }

    /// <summary>
    /// <b>Figuring a DUMMY surface does nothing, and the derivative knows it.</b>
    ///
    /// <para>Surface 1 of this fixture is air on both sides, so n' - n is zero - and every
    /// figuring term in the scheme carries that factor. An r^4 term there deviates no ray and
    /// changes no coefficient, so the whole column is legitimately zero.</para>
    ///
    /// <para>It is worth an assertion rather than an omission, because a zero column is exactly
    /// what the BROKEN flat-collimated path produced, and the two have to be told apart. Here the
    /// central difference is zero as well: the value does not move, so the derivative that says
    /// it does not move is right. This test would fail if a future change made the derivative
    /// non-zero - a term picked up from a surface that cannot refract.</para>
    /// </summary>
    [Fact]
    public void FiguringASurfaceThatCannotRefractHasNoEffectEitherWay()
    {
        var vars = Vars(new Variable { Kind = VariableKind.Asphere4, Surface = 1 });
        var (design, _) = Load(vars);

        var merit = new MeritFunction(design);
        for (int k = 2; k <= 20; k++)
            merit.Add(new Operand
            {
                Type = OperandType.ABER, Coefficient = "Tau" + k, Target = 0.0, Weight = 1.0,
            });

        var x0 = design.Read();
        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, analytic.Failure);

        const double h = 1e-9;
        var plus = (double[])x0.Clone(); plus[0] += h;
        design.Apply(plus);
        var rp = merit.Evaluate(false);

        var minus = (double[])x0.Clone(); minus[0] -= h;
        design.Apply(minus);
        var rm = merit.Evaluate(false);
        design.Apply((double[])x0.Clone());

        for (int i = 0; i < merit.Operands.Count; i++)
        {
            Assert.Equal(0.0, analytic.Jacobian[i, 0]);
            Assert.Equal(rp.Residuals[i], rm.Residuals[i], 12);   // and the value does not move
        }
    }
}
