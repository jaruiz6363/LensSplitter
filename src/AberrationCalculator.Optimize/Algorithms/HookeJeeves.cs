using System;
using System.Threading;

using AberrationCalculator.Optimize.Evaluation;

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>How a Hooke-Jeeves pattern search is to be run.</summary>
public sealed class HookeJeevesOptions
{
    /// <summary>Starting step, as a multiple of each variable's natural scale.</summary>
    public double InitialStep { get; set; } = 0.25;

    /// <summary>Stop once the step has been cut below this multiple of that scale.</summary>
    public double MinStep { get; set; } = 1e-4;

    /// <summary>Exploration-plus-pattern cycles to run.</summary>
    public int MaxIterations { get; set; } = 30;

    public CancellationToken Cancellation { get; set; } = CancellationToken.None;
}

/// <summary>
/// Hooke and Jeeves' pattern search: a direct search that uses no derivatives at all.
///
/// <para><b>Why a derivative-free method sits inside an optimiser built on exact derivatives.</b>
/// The two are good at different things. The least-squares step is a local move built on a
/// quadratic model, and it is unbeatable while that model holds; it is helpless where it does
/// not - at a boundary operand's kink, where a residual switches on and its derivative jumps, or
/// in the flat-bottomed valleys a corrected lens sits in, where the model says the design is
/// finished and a step twice as far says otherwise. Pattern search asks a cruder question - is
/// the merit lower over there? - and that question still has an answer in all those places.</para>
///
/// <para><b>The method.</b> From a base point, try each variable in turn, one step up and one
/// step down, keeping any move that helps: that is the EXPLORATION, and it costs two evaluations
/// per variable. If it found anything, the direction from the old base to the new one is
/// evidently worth following, so take the same move again and explore from there: that is the
/// PATTERN move, and it is what lets the search accelerate down a valley instead of creeping
/// across it. If exploration found nothing, the step was too big; halve it and try again. The
/// search ends when the step has been cut past all usefulness.</para>
///
/// <para>Steps are in units of each variable's natural scale - see <see cref="Scaling"/> - not in
/// the variables. own units: a curvature lives near 0.02 and a thickness near ten, so a step
/// that suits one is meaningless for the other.</para>
/// </summary>
public sealed class HookeJeeves
{
    private readonly MeritFunction _merit;
    private readonly HookeJeevesOptions _options;
    private readonly double[] _scale;
    private int _evaluations;

    public HookeJeeves(MeritFunction merit, double[] scale, HookeJeevesOptions? options = null)
    {
        _merit = merit ?? throw new ArgumentNullException(nameof(merit));
        _scale = scale ?? throw new ArgumentNullException(nameof(scale));
        _options = options ?? new HookeJeevesOptions();
    }

    /// <summary>Runs from the design's present state and leaves it at the best point found.</summary>
    public OptimizeResult Run()
    {
        var design = _merit.Design;
        int n = design.Variables.Count;

        var basePoint = design.Read();
        var start = _merit.Evaluate(false);
        double baseValue = start.Ok ? start.SumSquares : double.PositiveInfinity;
        double initialMerit = start.Ok ? start.Merit : double.PositiveInfinity;
        _evaluations = 1;

        if (n == 0 || double.IsInfinity(baseValue))
            return Finish(design, basePoint, initialMerit, 0, "nothing to search");

        double step = _options.InitialStep;
        int iteration = 0;
        string stop = "iteration limit";

        for (iteration = 1; iteration <= _options.MaxIterations; iteration++)
        {
            if (_options.Cancellation.IsCancellationRequested) { stop = "cancelled"; break; }

            var explored = (double[])basePoint.Clone();
            double exploredValue = Explore(explored, baseValue, step);

            if (exploredValue < baseValue)
            {
                // The pattern move: having found that going that way helped, go the same way
                // again from the new point, and explore around where that lands.
                var pattern = new double[n];
                for (int j = 0; j < n; j++) pattern[j] = 2.0 * explored[j] - basePoint[j];
                design.Variables.Fold(pattern);

                double patternValue = Value(pattern);
                _evaluations++;
                patternValue = Explore(pattern, patternValue, step);

                if (patternValue < exploredValue)
                {
                    basePoint = pattern;
                    baseValue = patternValue;
                }
                else
                {
                    basePoint = explored;
                    baseValue = exploredValue;
                }
            }
            else
            {
                step *= 0.5;
                if (step < _options.MinStep) { stop = "step exhausted"; break; }
            }
        }

        return Finish(design, basePoint, initialMerit, iteration, stop);
    }

    /// <summary>
    /// One exploration: each variable tried up and down, every improvement kept. Returns the
    /// value at the point the array is left holding.
    /// </summary>
    private double Explore(double[] point, double value, double step)
    {
        var design = _merit.Design;
        int n = point.Length;

        for (int j = 0; j < n; j++)
        {
            if (_options.Cancellation.IsCancellationRequested) return value;

            double delta = step * _scale[j];
            double original = point[j];

            foreach (double direction in Directions)
            {
                point[j] = original + direction * delta;
                design.Variables.Fold(point);
                double trial = Value(point);
                _evaluations++;

                if (trial < value)
                {
                    value = trial;
                    original = point[j];        // Fold may have moved it; keep where it landed
                    break;                      // downhill found, no need to try the other way
                }
                point[j] = original;
            }
        }
        return value;
    }

    private static readonly double[] Directions = { 1.0, -1.0 };

    /// <summary>
    /// The merit at a point. A design that cannot be evaluated is infinitely bad rather than an
    /// error, so the search steps back from it like any other uphill move.
    /// </summary>
    private double Value(double[] x)
    {
        _merit.Design.Apply(x);
        var r = _merit.Evaluate(false);
        return r.Ok ? r.SumSquares : double.PositiveInfinity;
    }

    private OptimizeResult Finish(Design design, double[] x, double initialMerit,
                                  int iterations, string stop)
    {
        design.Apply(x);
        var final = _merit.Evaluate(false);
        return new OptimizeResult
        {
            X = x,
            Merit = final.Ok ? final.Merit : double.PositiveInfinity,
            InitialMerit = initialMerit,
            Iterations = iterations,
            Evaluations = _evaluations,
            Stop = stop,
            Ok = final.Ok,
        };
    }
}
