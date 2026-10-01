using System;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Optimize;
using AberrationCalculator.Optimize.Algorithms;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// How far a basin hop moves each KIND of variable.
///
/// <para>The hop scale is the one place in the search where a variable's units matter, and it was
/// wrong for the figuring kinds by three orders of magnitude. An unbounded variable was kicked
/// against its own size floored at one - harmless for a curvature near 0.01, and absurd for an
/// r^4 coefficient near 1E-6, which was kicked by 1E-3 and landed with ten lens units of sag on
/// the surface.</para>
///
/// <para><b>It did not break anything, which is why it survived.</b> The local optimisation after
/// each hop hauled the design back and the Metropolis test rejected it, so the search still
/// converged - it simply spent every figuring hop climbing out of somewhere absurd instead of
/// exploring. A defect that costs only work needs a test that reads the work.</para>
/// </summary>
public class HoppingScaleTests
{
    private static Design Triplet(params Variable[] vars)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var set = new VariableSet();
        set.AddRange(vars);
        return new Design(system, catalog, set);
    }

    /// <summary>
    /// <b>A figuring variable is kicked against its own physical scale, not against one.</b> The
    /// bound is generous - four orders below the old floor - because the point is the order of
    /// magnitude, not a tuned constant.
    /// </summary>
    [Theory]
    [InlineData(VariableKind.Conic)]
    [InlineData(VariableKind.Asphere4)]
    [InlineData(VariableKind.Asphere6)]
    [InlineData(VariableKind.Asphere8)]
    public void AFiguringVariableIsKickedAgainstItsOwnScale(VariableKind kind)
    {
        var v = new Variable { Kind = kind, Surface = 2 };
        var design = Triplet(v);
        var ceilings = Scaling.PhysicalCeilings(design);

        double scale = BasinHopping.PerturbScale(v, 0.0, ceilings, 0);

        Assert.Equal(ceilings[0], scale);
        Assert.True(scale > 0.0, $"{kind} would never move");

        // The conic is dimensionless and its ceiling is capped at 2; the three aspheric terms
        // carry 1/length^3 and smaller, so anything near one would be the old floor back again.
        if (kind != VariableKind.Conic)
            Assert.True(scale < 1e-3,
                $"{kind} is kicked by {scale:E3}, which is the floor-of-one bug returning");
    }

    /// <summary>
    /// The r^8 term is kicked far more gently than the r^4 one, because it multiplies a far higher
    /// power of the aperture. One scale for all three would be wrong for two of them.
    /// </summary>
    [Fact]
    public void TheHigherAsphericTermsAreKickedMoreGently()
    {
        var a4 = new Variable { Kind = VariableKind.Asphere4, Surface = 2 };
        var a6 = new Variable { Kind = VariableKind.Asphere6, Surface = 2 };
        var a8 = new Variable { Kind = VariableKind.Asphere8, Surface = 2 };

        var design = Triplet(a4, a6, a8);
        var c = Scaling.PhysicalCeilings(design);

        double s4 = BasinHopping.PerturbScale(a4, 0.0, c, 0);
        double s6 = BasinHopping.PerturbScale(a6, 0.0, c, 1);
        double s8 = BasinHopping.PerturbScale(a8, 0.0, c, 2);

        Assert.True(s6 < s4, $"r^6 scale {s6:E3} is not below r^4's {s4:E3}");
        Assert.True(s8 < s6, $"r^8 scale {s8:E3} is not below r^6's {s6:E3}");
    }

    /// <summary>
    /// <b>Curvature and thickness keep the rule they had.</b> It is tuned, the default hop size was
    /// measured against it, and the figuring fix is not an excuse to disturb it: a curvature near
    /// 0.01 is kicked against a floor of one, which is a tenth of the curvature and survivable.
    /// </summary>
    [Fact]
    public void CurvatureAndThicknessAreUnchanged()
    {
        var cv = new Variable { Kind = VariableKind.Curvature, Surface = 1 };
        var th = new Variable { Kind = VariableKind.Thickness, Surface = 2 };

        var design = Triplet(cv, th);
        var c = Scaling.PhysicalCeilings(design);
        var x = design.Read();

        Assert.Equal(Math.Max(Math.Abs(x[0]), 1.0), BasinHopping.PerturbScale(cv, x[0], c, 0));
        Assert.Equal(Math.Max(Math.Abs(x[1]), 1.0), BasinHopping.PerturbScale(th, x[1], c, 1));
    }

    /// <summary>
    /// A bounded variable is still kicked across its own interval, whatever kind it is - the
    /// interval is the only scale a bounded variable can have that means anything, and it beats
    /// the physical ceiling because the user stated it.
    /// </summary>
    [Fact]
    public void ABoundedFiguringVariableIsKickedAcrossItsInterval()
    {
        var v = new Variable { Kind = VariableKind.Conic, Surface = 2, Min = -2.0, Max = 0.0 };
        var design = Triplet(v);
        var c = Scaling.PhysicalCeilings(design);

        Assert.Equal(1.0, BasinHopping.PerturbScale(v, -0.5, c, 0));
    }

    /// <summary>
    /// And the guard for a design whose ceilings cannot be formed: fall back to the old rule
    /// rather than to a scale of zero, which would freeze the variable for the whole run.
    /// </summary>
    [Fact]
    public void AMissingCeilingFallsBackRatherThanFreezing()
    {
        var v = new Variable { Kind = VariableKind.Asphere4, Surface = 2 };

        Assert.True(BasinHopping.PerturbScale(v, 0.0, Array.Empty<double>(), 0) > 0.0);
        Assert.True(BasinHopping.PerturbScale(v, 0.0, new[] { 0.0 }, 0) > 0.0);
    }
}

/// <summary>
/// WHETHER a hop moves each kind, which is a separate question from how far.
///
/// <para>LensHH-LT does not randomize aspherics, and that is the better default. A figuring term is
/// a nearly-linear correction the local stage fits reliably from wherever it starts, so throwing it
/// does not choose a different basin - it discards a figure that is about to be fitted again. The
/// shape variables are what choose the basin, and the kick belongs to them.</para>
///
/// <para><b>The distinction these tests exist to protect is between the KICK and the SEARCH.</b>
/// Excluding figuring from the kick must not exclude it from the optimisation: Hooke-Jeeves and the
/// least-squares stage still step it at every hop, so the aspheric is fitted as hard as before. An
/// implementation that skipped it in both places would still pass a merit-improves test while
/// quietly freezing the figure, so the two are asserted apart.</para>
/// </summary>
public class HopFiguringTests
{
    private static Design Triplet(params Variable[] vars)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        var set = new VariableSet();
        set.AddRange(vars);
        return new Design(system, catalog, set);
    }

    private static (double[] before, double[] after) Hop(bool hopFiguring, params Variable[] vars)
    {
        var design = Triplet(vars);
        var before = design.Read();
        BasinHopping.Randomize(design, 0.5, new Random(4242), hopFiguring);
        return (before, design.Read());
    }

    /// <summary>
    /// By default the kick passes the figuring variables over and moves the shape ones.
    /// </summary>
    [Fact]
    public void AHopLeavesTheFiguringAloneAndMovesTheShape()
    {
        var cv = new Variable { Kind = VariableKind.Curvature, Surface = 1 };
        var cc = new Variable { Kind = VariableKind.Conic, Surface = 2 };
        var a4 = new Variable { Kind = VariableKind.Asphere4, Surface = 2 };
        var a6 = new Variable { Kind = VariableKind.Asphere6, Surface = 2 };
        var a8 = new Variable { Kind = VariableKind.Asphere8, Surface = 2 };

        var (before, after) = Hop(hopFiguring: false, cv, cc, a4, a6, a8);

        Assert.NotEqual(before[0], after[0]);                 // the curvature was kicked
        for (int i = 1; i < before.Length; i++)
            Assert.Equal(before[i], after[i]);                // and the four figuring kinds were not
    }

    /// <summary>And asking for it kicks them, so the switch is a switch and not a comment.</summary>
    [Fact]
    public void AskingForItKicksThemInstead()
    {
        var cc = new Variable { Kind = VariableKind.Conic, Surface = 2 };
        var a4 = new Variable { Kind = VariableKind.Asphere4, Surface = 2 };

        var (before, after) = Hop(hopFiguring: true, cc, a4);

        Assert.NotEqual(before[0], after[0]);
        Assert.NotEqual(before[1], after[1]);
    }

    /// <summary>
    /// <b>The one that matters.</b> Not kicking a figuring variable must not stop the local search
    /// stepping it - the Hooke-Jeeves scale is read from <c>PerturbScale</c>, which knows nothing
    /// about the switch, and a nonzero scale there is what keeps the aspheric being optimised.
    /// </summary>
    [Fact]
    public void TheLocalSearchStillStepsWhatTheHopSkips()
    {
        var a4 = new Variable { Kind = VariableKind.Asphere4, Surface = 2 };
        var design = Triplet(a4);
        var ceilings = Scaling.PhysicalCeilings(design);

        Assert.True(BasinHopping.PerturbScale(a4, 0.0, ceilings, 0) > 0.0,
            "the aspheric would be frozen in the local search, not merely unkicked");
    }

    /// <summary>The default is off, stated once so a change to it has to be deliberate.</summary>
    [Fact]
    public void TheDefaultIsNotToKickThem()
    {
        Assert.False(new BasinHoppingOptions().HopFiguring);
        Assert.False(new RunSettings().HopFiguring);
    }
}
