using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize;
using AberrationCalculator.Optimize.Algorithms;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The stochastic layer: Hooke-Jeeves on its own, and basin hopping over it.
///
/// <para>A search that throws random steps cannot be tested by asserting where it lands. What
/// can be asserted is that it does not go BACKWARDS, that it stays inside the limits it was
/// given, and that with one chain and a fixed seed it does the same thing twice - which is what
/// makes a disappointing run something that can be investigated rather than shrugged at.</para>
/// </summary>
public class BasinHoppingTests
{
    private static OpticalSystem Triplet(GlassCatalog catalog) =>
        LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

    private static VariableSet Vars()
    {
        var set = new VariableSet();
        for (int s = 1; s <= 6; s++)
            set.Add(new Variable { Kind = VariableKind.Curvature, Surface = s });
        return set;
    }

    private static Operand[] Operands() => new[]
    {
        new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
        new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 10.0 },
    };

    /// <summary>
    /// A design pushed off its optimum, to give the search something to find.
    /// </summary>
    private static OpticalSystem Degraded(GlassCatalog catalog)
    {
        var s = Triplet(catalog);
        s.Surfaces[1].Curvature *= 0.75;
        s.Surfaces[2].Curvature *= 1.30;
        s.Surfaces[4].Curvature *= 0.80;
        return s;
    }

    [Fact]
    public void BasinHoppingImprovesADegradedTriplet()
    {
        var catalog = CatalogLocator.LoadBundled();
        var result = new BasinHopping(Degraded(catalog), catalog, Vars(), Operands(),
            new BasinHoppingOptions
            {
                Chains = 2,
                MaxHops = 12,
                LmIterationsPerHop = 25,
                Seed = 4242,
            }).Run();

        Assert.NotNull(result.Best);
        Assert.True(result.Merit <= result.InitialMerit,
            $"merit went from {result.InitialMerit:G6} to {result.Merit:G6}");
        Assert.True(result.Hops > 0);
        Assert.Equal(2, result.Chains);
    }

    /// <summary>
    /// The run never returns something worse than it started with. It keeps the best design it
    /// saw, so a search that finds nothing hands back the design it was given rather than
    /// wherever the last hop happened to leave it.
    /// </summary>
    [Fact]
    public void NeverReturnsWorseThanItStarted()
    {
        var catalog = CatalogLocator.LoadBundled();
        var result = new BasinHopping(Triplet(catalog), catalog, Vars(), Operands(),
            new BasinHoppingOptions
            {
                Chains = 1,
                MaxHops = 6,
                LmIterationsPerHop = 15,
                HopSigma = 2.0,           // deliberately violent, so most hops are bad
                Seed = 7,
            }).Run();

        Assert.True(result.Merit <= result.InitialMerit + 1e-12);
    }

    /// <summary>One chain and one seed do the same thing twice.</summary>
    [Fact]
    public void SingleChainIsReproducible()
    {
        double Run()
        {
            var catalog = CatalogLocator.LoadBundled();
            return new BasinHopping(Degraded(catalog), catalog, Vars(), Operands(),
                new BasinHoppingOptions
                {
                    Chains = 1,
                    MaxHops = 8,
                    LmIterationsPerHop = 20,
                    Seed = 20260909,
                }).Run().Merit;
        }

        Assert.Equal(Run(), Run(), 12);
    }

    /// <summary>
    /// A bounded variable survives the hopping. This is the reflection bound handling being
    /// asked to do the job it was chosen for - a large random step lands outside the interval
    /// constantly, and has to come back inside every time.
    /// </summary>
    [Fact]
    public void BoundsHoldThroughTheHopping()
    {
        var catalog = CatalogLocator.LoadBundled();

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1,
                                Min = 0.018, Max = 0.024 });
        for (int s = 2; s <= 6; s++)
            vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = s });

        var result = new BasinHopping(Degraded(catalog), catalog, vars, Operands(),
            new BasinHoppingOptions
            {
                Chains = 2,
                MaxHops = 10,
                LmIterationsPerHop = 20,
                HopSigma = 3.0,
                RestartSigma = 6.0,
                Seed = 99,
            }).Run();

        Assert.NotNull(result.Best);
        Assert.InRange(result.Best!.Surfaces[1].Curvature, 0.018, 0.024);
        Assert.InRange(result.X[0], 0.018, 0.024);
    }

    /// <summary>Hooke-Jeeves on its own reduces the merit, without using a derivative at all.</summary>
    [Fact]
    public void HookeJeevesReducesTheMeritWithoutDerivatives()
    {
        var catalog = CatalogLocator.LoadBundled();
        var design = new Design(Degraded(catalog), catalog, Vars());
        var merit = new MeritFunction(design);
        merit.AddRange(Operands());

        var scale = Scaling.Build(merit);
        var result = new HookeJeeves(merit, scale,
            new HookeJeevesOptions { MaxIterations = 40 }).Run();

        Assert.True(result.Ok, result.Stop);
        Assert.True(result.Merit < result.InitialMerit,
            $"merit went from {result.InitialMerit:G6} to {result.Merit:G6}");
    }

    /// <summary>
    /// Every variable gets a step of its own size. A single step would be nonsense across
    /// parameters whose natural magnitudes differ by three orders and whose units differ
    /// altogether.
    /// </summary>
    [Fact]
    public void ScalingGivesEachVariableAStepOfItsOwnSize()
    {
        var catalog = CatalogLocator.LoadBundled();

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var design = new Design(Triplet(catalog), catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(Operands());

        var scale = Scaling.Build(merit);

        Assert.All(scale, s => Assert.True(s > 0.0 && !double.IsInfinity(s),
                                           $"scale {s} is not usable"));

        // A curvature near 0.02 per millimetre cannot take the step a ten-millimetre thickness
        // takes. That ordering is the whole point of the exercise.
        Assert.True(scale[0] < scale[1],
            $"curvature step {scale[0]:G4} is not smaller than the thickness step {scale[1]:G4}");
    }

    /// <summary>
    /// A bound is an INVARIANT, not a request: a bounded variable is inside its interval from the
    /// moment a <see cref="Design"/> exists, even when the lens handed in was already outside it.
    ///
    /// <para>Reflection is total - every real number folds to a value inside the interval - so
    /// there is no state in which a bounded variable is out of range, and this test says so at
    /// the only moment that could have created one. Testing it here rather than through a
    /// hopping run matters: the symptom that exposed it was a run returning an illegal lens AND
    /// reporting that it had found nothing, which is several inferences away from the cause.</para>
    /// </summary>
    [Fact]
    public void ABoundedVariableIsInsideItsRangeFromTheMomentTheDesignExists()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = Triplet(catalog);

        // Put the curvature well outside the bound that is about to be imposed on it.
        double outside = 0.0340698632072279;
        system.Surfaces[1].Curvature = outside;

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1,
                                Min = 0.018, Max = 0.024 });

        var design = new Design(system, catalog, vars);

        Assert.InRange(design.System.Surfaces[1].Curvature, 0.018, 0.024);
        Assert.InRange(design.Read()[0], 0.018, 0.024);
        Assert.NotEqual(outside, design.System.Surfaces[1].Curvature);
    }

    /// <summary>
    /// And it stays inside however far a step tries to throw it - reflection folds arbitrarily
    /// large excursions, so there is no step big enough to escape a bound.
    /// </summary>
    [Theory]
    [InlineData(1e3)]
    [InlineData(-1e3)]
    [InlineData(1e9)]
    public void NoStepIsLargeEnoughToLeaveABound(double wild)
    {
        var catalog = CatalogLocator.LoadBundled();
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1,
                                Min = 0.018, Max = 0.024 });

        var design = new Design(Triplet(catalog), catalog, vars);
        design.Apply(new[] { wild });

        Assert.InRange(design.System.Surfaces[1].Curvature, 0.018, 0.024);
        Assert.InRange(design.Read()[0], 0.018, 0.024);
    }
}

/// <summary>
/// The FIRST hop's kick, which is sized separately from the rest.
///
/// <para><b>These exist because the option was inert.</b> <c>InitialPerturbSigma</c> was declared
/// in the first optimizer commit, documented as "a small kick applied once before the first
/// minimisation ... to break exact symmetry", and read by nothing: hop 1 was kicked at
/// <c>HopSigma</c> like every other hop. Because the two default to the same 0.001 no run could
/// tell, and because nothing referenced it no compiler warning could either.</para>
///
/// <para>So the assertions are the two halves that were missing: that it now reaches the search
/// at all, and that leaving it alone reproduces the old behaviour exactly.</para>
/// </summary>
public class InitialSigmaTests
{
    private static OpticalSystem Triplet(GlassCatalog catalog) =>
        LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

    private static VariableSet Vars()
    {
        var set = new VariableSet();
        for (int s = 1; s <= 6; s++)
            set.Add(new Variable { Kind = VariableKind.Curvature, Surface = s });
        return set;
    }

    private static Operand[] Operands() => new[]
    {
        new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
        new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 10.0 },
    };

    private static double RunWith(double? initial)
    {
        var catalog = CatalogLocator.LoadBundled();
        var options = new BasinHoppingOptions
        {
            Chains = 1,
            MaxHops = 1,          // hop 1 IS the initial kick, so one hop isolates it
            LmIterationsPerHop = 3,
            HjStepsPerHop = 1,    // barely minimise, so where the kick landed still shows
            EnableMetropolis = false,
            Seed = 31337,
        };
        if (initial.HasValue) options.InitialPerturbSigma = initial.Value;
        return new BasinHopping(Triplet(catalog), catalog, Vars(), Operands(), options)
            .Run().Merit;
    }

    /// <summary>
    /// <b>It reaches the search.</b> A first kick four thousand times the default lands somewhere
    /// a near-unminimised hop cannot disguise, so the merit differs - which is all that was
    /// needed to notice the option was doing nothing, and was never asserted.
    /// </summary>
    [Fact]
    public void TheInitialSigmaChangesWhereTheFirstHopLands()
    {
        Assert.NotEqual(RunWith(4.0), RunWith(0.001), 9);
    }

    /// <summary>
    /// <b>And the default is the behaviour that was there before.</b> Connecting a dead option is
    /// only safe if leaving it alone is a no-op, and since it defaults to <c>HopSigma</c>'s own
    /// 0.001, setting it explicitly to that must give the untouched run bit for bit.
    /// </summary>
    [Fact]
    public void LeavingItAloneIsTheOldBehaviour()
    {
        Assert.Equal(RunWith(null), RunWith(0.001), 12);
    }

    /// <summary>Stated once, so that changing either default has to be deliberate.</summary>
    [Fact]
    public void ItDefaultsToTheOrdinaryHop()
    {
        var options = new BasinHoppingOptions();
        Assert.Equal(options.HopSigma, options.InitialPerturbSigma);
        Assert.Equal(0.001, new RunSettings().InitialPerturbSigma);
    }
}
