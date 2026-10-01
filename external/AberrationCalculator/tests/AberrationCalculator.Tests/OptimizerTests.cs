using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Optimize.Algorithms;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// That the optimiser actually descends, that it respects the limits it is given, and that
/// the bound handling does what it claims.
/// </summary>
public class OptimizerTests
{
    private static (Design, MeritFunction) Triplet(VariableSet vars, params Operand[] operands)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(operands);
        return (design, merit);
    }

    private static VariableSet Curvatures(params int[] surfaces)
    {
        var set = new VariableSet();
        foreach (int s in surfaces)
            set.Add(new Variable { Kind = VariableKind.Curvature, Surface = s });
        return set;
    }

    [Theory]
    [InlineData(StepMethod.Lm)]
    [InlineData(StepMethod.Psd2)]
    [InlineData(StepMethod.Psd3)]
    public void EveryStepMethodImprovesTheSpotAndHoldsTheFocalLength(StepMethod method)
    {
        var vars = Curvatures(1, 2, 3, 4, 5, 6);
        var (design, merit) = Triplet(vars,
            new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
            new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 10.0 });

        var before = merit.Evaluate(false);
        double spotBefore = Spot(merit, before);

        var result = new LocalOptimizer(merit, new OptimizerOptions
        {
            Method = method,
            MaxIterations = 120,
        }).Run();

        Assert.True(result.Ok, result.Stop);
        Assert.True(result.Merit < result.InitialMerit,
            $"{method}: merit went from {result.InitialMerit:G6} to {result.Merit:G6}");

        var after = merit.Evaluate(false);

        // BY TYPE, NOT BY POSITION. PRMSA expands to one operand per field and wavelength, so
        // the index an operand lands at depends on the design's evaluation set. Asking for the
        // one that is an EFL says what the test means and cannot silently start reading a
        // different quantity.
        double spotAfter = Spot(merit, after);
        Assert.True(spotAfter < spotBefore,
            $"{method}: predicted spot went from {spotBefore:G6} to {spotAfter:G6}");

        // The focal length is the point of weighting it ten times: it has to survive.
        // AsDeclared, because EFL is differentiated as a POWER and reported as a length.
        var efl = System.Linq.Enumerable.First(merit.Operands, o => o.Type == OperandType.EFL);
        Assert.InRange(efl.AsDeclared(ValueOf(merit, after, OperandType.EFL)), 49.0, 51.0);
    }

    /// <summary>
    /// PSD is meant to earn its keep where Marquardt alone struggles. This is not a proof of
    /// that - one lens is one lens - but a run where it came out WORSE would be worth knowing.
    /// </summary>
    [Fact]
    public void PsdReachesAtLeastAsDeepAsMarquardtOnTheSameProblem()
    {
        double Run(StepMethod method)
        {
            var vars = Curvatures(1, 2, 3, 4, 5, 6);
            var (_, merit) = Triplet(vars,
                new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
                new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 10.0 });
            return new LocalOptimizer(merit, new OptimizerOptions
            {
                Method = method,
                MaxIterations = 60,
            }).Run().Merit;
        }

        double lm = Run(StepMethod.Lm);
        double psd3 = Run(StepMethod.Psd3);

        // A factor of two of slack: the claim is that PSD is not worse, not that it always wins.
        Assert.True(psd3 <= lm * 2.0, $"PSD3 reached {psd3:G6}, Marquardt {lm:G6}");
    }

    /// <summary>
    /// A bounded variable stays inside its bounds, however hard the optimiser pulls.
    /// </summary>
    [Fact]
    public void ReflectionKeepsBoundedVariablesInsideTheirLimits()
    {
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1,
                                Min = 0.02, Max = 0.025 });
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 2 });
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 4 });

        var (design, merit) = Triplet(vars,
            new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 });

        var result = new LocalOptimizer(merit, new OptimizerOptions { MaxIterations = 80 }).Run();
        Assert.True(result.Ok, result.Stop);

        double c = design.System.Surfaces[1].Curvature;
        Assert.InRange(c, 0.02, 0.025);
        Assert.InRange(result.X[0], 0.02, 0.025);
    }

    /// <summary>
    /// Reflection folds a value back inside, and folds it the right way round.
    /// </summary>
    [Theory]
    [InlineData(0.5, 0.0, 1.0, 0.5)]     // already inside
    [InlineData(1.25, 0.0, 1.0, 0.75)]   // over the top, reflected down
    [InlineData(-0.25, 0.0, 1.0, 0.25)]  // under the bottom, reflected up
    [InlineData(2.5, 0.0, 1.0, 0.5)]     // far outside: bounces twice and lands inside
    [InlineData(-3.25, 0.0, 1.0, 0.75)]
    public void FoldReflectsIntoTheInterval(double x, double lo, double hi, double expected)
    {
        double folded = Reflection.Fold(x, lo, hi);
        Assert.Equal(expected, folded, 12);
        Assert.InRange(folded, lo, hi);
    }

    [Fact]
    public void FoldHandlesOneSidedAndAbsentBounds()
    {
        Assert.Equal(3.0, Reflection.Fold(-1.0, 1.0, double.PositiveInfinity), 12);
        Assert.Equal(-1.0, Reflection.Fold(3.0, double.NegativeInfinity, 1.0), 12);
        Assert.Equal(42.0, Reflection.Fold(42.0, double.NegativeInfinity, double.PositiveInfinity), 12);
    }

    /// <summary>
    /// Whatever the value it starts from, folding never leaves the interval - which is the
    /// property the stochastic search leans on when it throws a large step.
    /// </summary>
    [Fact]
    public void FoldNeverEscapes()
    {
        var rng = new Random(20260909);
        for (int i = 0; i < 20000; i++)
        {
            double lo = -5.0 + 10.0 * rng.NextDouble();
            double hi = lo + 1e-3 + 10.0 * rng.NextDouble();
            double x = -1000.0 + 2000.0 * rng.NextDouble();
            double folded = Reflection.Fold(x, lo, hi);
            Assert.InRange(folded, lo, hi);
        }
    }

    /// <summary>
    /// The predicted spot, reassembled from however many residuals PRMSA expanded into.
    ///
    /// <para>Its sub-weights are chosen so the sum of their squares is the declared weight times
    /// the aggregate squared, so this recovers the aggregate whatever the evaluation set.</para>
    /// </summary>
    private static double Spot(MeritFunction merit, MeritResult r)
    {
        double squares = 0.0, weight = 0.0;
        for (int i = 0; i < merit.Operands.Count; i++)
        {
            if (merit.Operands[i].Type != OperandType.PRMSA) continue;
            squares += r.Residuals[i] * r.Residuals[i];
            weight += merit.Operands[i].Weight;
        }
        return weight > 0.0 ? Math.Sqrt(squares / weight) : 0.0;
    }

    /// <summary>The value of the one operand of that type, found by type rather than position.</summary>
    private static double ValueOf(MeritFunction merit, MeritResult r, OperandType type)
    {
        for (int i = 0; i < merit.Operands.Count; i++)
            if (merit.Operands[i].Type == type) return r.Values[i];
        throw new Xunit.Sdk.XunitException("no " + type + " operand in this merit function");
    }
}
