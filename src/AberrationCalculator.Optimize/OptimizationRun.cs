using System;
using System.Collections.Generic;
using System.Threading;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Algorithms;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;

namespace AberrationCalculator.Optimize;

/// <summary>How a run is to be carried out.</summary>
public sealed class RunSettings
{
    /// <summary>Which diagonal the least-squares step uses. Ignored under Hooke-Jeeves.</summary>
    public StepMethod Method { get; set; } = StepMethod.Psd3;

    /// <summary>Minimise by pattern search instead of least squares.</summary>
    public bool HookeJeeves { get; set; }

    /// <summary>
    /// The LEAST-SQUARES iteration cap: for a local run, the whole run; under hopping, per hop.
    /// </summary>
    public int Iterations { get; set; } = 200;

    /// <summary>
    /// Hooke-Jeeves steps per hop, which is NOT the same quantity as <see cref="Iterations"/>
    /// and must never be set from it.
    ///
    /// <para>They were, and the cost was absurd: a step of the pattern search probes every
    /// variable twice, so thirty steps on seventeen variables is about a thousand evaluations,
    /// while six thousand steps is two hundred thousand - thirty-three seconds of a hop that
    /// should take a fraction of a second. One number driving two budgets that differ by a
    /// factor of twenty is a bug waiting for someone to change the one they know about.</para>
    /// </summary>
    public int HjStepsPerHop { get; set; } = 30;

    /// <summary>Basin-hopping hops per chain. Zero runs a single local optimisation.</summary>
    public int Hops { get; set; }

    /// <summary>Chains. Zero means one per physical core.</summary>
    public int Chains { get; set; }

    public int Seed { get; set; } = 1234;

    public bool GlassSubstitution { get; set; }

    /// <summary>
    /// Which substitution catalogue the hopping may choose glasses from - CoreSet28, say.
    ///
    /// <para>Named rather than "all of them" on purpose: a search free to pick from every vendor
    /// catalogue at once settles on glasses nobody stocks. See
    /// <see cref="Core.Glass.SubstitutionCatalog"/>.</para>
    /// </summary>
    public string? SubstitutionCatalog { get; set; }

    /// <summary>
    /// The per-hop kick, in units of each variable's natural scale. See
    /// <see cref="BasinHoppingOptions.HopSigma"/> for why it is this small.
    /// </summary>
    public double HopSigma { get; set; } = 0.001;

    /// <summary>
    /// Whether the hop kicks the conic and aspheric terms too. Default false - they are still
    /// optimised, just not thrown. See <see cref="BasinHoppingOptions.HopFiguring"/>.
    /// </summary>
    public bool HopFiguring { get; set; }

    /// <summary>
    /// The kick on the first hop, before the design has ever been minimised. Default 0.001, the
    /// same as <see cref="HopSigma"/>. See <see cref="BasinHoppingOptions.InitialPerturbSigma"/>.
    /// </summary>
    public double InitialPerturbSigma { get; set; } = 0.001;

    public IProgress<BasinHoppingProgress>? Progress { get; set; }

    /// <summary>
    /// Called when a chain beats its own best, so the answer is written the moment it exists
    /// rather than when the run ends. See BasinHoppingOptions.OnChainBest.
    /// </summary>
    public Action<int, OpticalSystem, double>? OnChainBest { get; set; }

    public CancellationToken Cancellation { get; set; } = CancellationToken.None;
}

/// <summary>What a run did, in enough detail to report it.</summary>
public sealed class RunOutcome
{
    public OpticalSystem Best { get; init; } = null!;

    /// <summary>
    /// The design as it arrived, kept so the report can say what actually MOVED.
    ///
    /// <para>A variable vector says a curvature went from 0.0454 to 0.0464. A designer wants to
    /// know the radius went from 22.014 to 21.553, and which glass became which. Neither is
    /// recoverable from the optimised design alone - it takes both ends.</para>
    /// </summary>
    public OpticalSystem? Start { get; init; }

    /// <summary>
    /// The best design each hopping chain found, best first.
    ///
    /// <para>Several chains searching independently is the point of basin hopping: they find
    /// DIFFERENT designs, and which of them is interesting is a judgement only a designer can
    /// make. Keeping the single lowest merit and discarding the rest throws away most of what the
    /// run paid for. Empty for a local optimisation, which has only one answer.</para>
    /// </summary>
    public IReadOnlyList<OpticalSystem> ChainBest { get; init; } = Array.Empty<OpticalSystem>();

    public double InitialMerit { get; init; }
    public double FinalMerit { get; init; }

    public double[] StartX { get; init; } = Array.Empty<double>();
    public double[] EndX { get; init; } = Array.Empty<double>();
    public double[] StartValues { get; init; } = Array.Empty<double>();
    public double[] EndValues { get; init; } = Array.Empty<double>();
    public double[] EndResiduals { get; init; } = Array.Empty<double>();

    public IReadOnlyList<Operand> Operands { get; init; } = Array.Empty<Operand>();
    public Variables.VariableSet Variables { get; init; } = new();

    public string Route { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Stop { get; init; } = string.Empty;

    public int Hops { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int GlassSwaps { get; init; }
    public int Chains { get; init; }
    public int Iterations { get; init; }

    public bool Ok { get; init; } = true;
    public string? Failure { get; init; }

    public bool Improved => FinalMerit < InitialMerit;
}

/// <summary>
/// Runs an optimisation and reports what it did.
///
/// <para>The design handed in is never touched. Everything happens on a copy, and the copy comes
/// back in <see cref="RunOutcome.Best"/> for the caller to keep or discard - because an optimiser
/// that has quietly overwritten a working design before telling you the result got worse is not
/// a tool anybody trusts twice.</para>
/// </summary>
public static class OptimizationRun
{
    public static RunOutcome Execute(OpticalSystem system, GlassCatalog catalog,
                                     OptimizationSetup setup, RunSettings? settings = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        settings ??= new RunSettings();

        var working = DesignCopy.Deep(system);
        // A second copy, never touched, so the report can say what actually moved.
        var before = DesignCopy.Deep(system);
        var design = new Design(working, catalog, setup.Variables);
        var merit = new MeritFunction(design);
        merit.AddRange(setup.Operands);

        var startX = design.Read();
        var start = merit.Evaluate(false);
        if (!start.Ok)
            return new RunOutcome
            {
                Best = working,
                Start = before,
                Ok = false,
                Failure = start.Failure,
                Route = design.RouteExplanation,
                Variables = setup.Variables,
                Operands = merit.Operands,
            };

        string method = settings.HookeJeeves
            ? "Hooke-Jeeves pattern search"
            : settings.Method switch
            {
                StepMethod.Lm => "Levenberg-Marquardt",
                StepMethod.Psd2 => "Dilworth PSD II",
                _ => "Dilworth PSD III",
            };

        if (settings.Hops > 0)
        {
            var hop = new BasinHopping(working, catalog, setup.Variables, setup.Operands,
                new BasinHoppingOptions
                {
                    MaxHops = settings.Hops,
                    Chains = settings.Chains,
                    StepMethod = settings.Method,
                    UseHookeJeeves = settings.HookeJeeves,
                    LmIterationsPerHop = settings.Iterations,
                    HjStepsPerHop = settings.HjStepsPerHop,
                    HopSigma = settings.HopSigma,
                    InitialPerturbSigma = settings.InitialPerturbSigma,
                    HopFiguring = settings.HopFiguring,
                    GlassSubstitution = settings.GlassSubstitution,
                    SubstitutionCatalog = settings.SubstitutionCatalog,
                    Seed = settings.Seed,
                    Progress = settings.Progress,
                    OnChainBest = settings.OnChainBest,
                    Cancellation = settings.Cancellation,
                }).Run();

            var best = hop.Best ?? working;
            var endDesign = new Design(best, catalog, setup.Variables);
            var endMerit = new MeritFunction(endDesign);
            endMerit.AddRange(setup.Operands);
            var end = endMerit.Evaluate(false);

            var chainBest = new List<OpticalSystem>();
            foreach (var found in hop.ChainBest) chainBest.Add(found.Design);

            return new RunOutcome
            {
                Best = best,
                Start = before,
                ChainBest = chainBest,
                InitialMerit = start.Merit,
                FinalMerit = hop.Merit,
                StartX = startX,
                EndX = endDesign.Read(),
                StartValues = start.Values,
                EndValues = end.Values,
                EndResiduals = end.Residuals,
                Operands = endMerit.Operands,
                Variables = setup.Variables,
                Route = design.RouteExplanation,
                Method = method + ", inside basin hopping",
                Stop = "hops exhausted",
                Hops = hop.Hops,
                Accepted = hop.Accepted,
                Rejected = hop.Rejected,
                GlassSwaps = hop.GlassSwaps,
                Chains = hop.Chains,
                Iterations = settings.Iterations,
                Ok = end.Ok,
                Failure = end.Failure,
            };
        }

        OptimizeResult local;
        if (settings.HookeJeeves)
        {
            var scale = Scaling.Build(merit);
            local = new HookeJeeves(merit, scale, new HookeJeevesOptions
            {
                MaxIterations = settings.Iterations,
                Cancellation = settings.Cancellation,
            }).Run();
        }
        else
        {
            local = new LocalOptimizer(merit, new OptimizerOptions
            {
                Method = settings.Method,
                MaxIterations = settings.Iterations,
                Cancellation = settings.Cancellation,
            }).Run();
        }

        var after = merit.Evaluate(false);
        return new RunOutcome
        {
            Best = working,
            Start = before,
            InitialMerit = start.Merit,
            FinalMerit = local.Merit,
            StartX = startX,
            EndX = local.X,
            StartValues = start.Values,
            EndValues = after.Values,
            EndResiduals = after.Residuals,
            Operands = merit.Operands,
            Variables = setup.Variables,
            Route = design.RouteExplanation,
            Method = method,
            Stop = local.Stop,
            Iterations = local.Iterations,
            Chains = 1,
            Ok = local.Ok,
            Failure = local.Ok ? null : local.Stop,
        };
    }
}
