using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

// PerturbScale and Randomize are internal so that WHETHER a hop moves each KIND of variable, and
// by how much, can be asserted directly. The scale was wrong for the figuring kinds by a factor of
// three thousand and nothing said so, because the local optimisation after each hop hid it - the
// search wasted its hops rather than breaking. A defect that only costs work needs a test that
// reads the work.
[assembly: InternalsVisibleTo("AberrationCalculator.Tests")]

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>How a basin-hopping run is to be carried out.</summary>
public sealed class BasinHoppingOptions
{
    /// <summary>Hops per chain.</summary>
    public int MaxHops { get; set; } = 3000;

    /// <summary>Independent chains. Zero means one per physical core.</summary>
    public int Chains { get; set; }

    /// <summary>
    /// What minimises each basin. The three least-squares methods use the exact Jacobian;
    /// <see cref="StepMethod.Lm"/> is the safe one and PSD the deeper, and
    /// <see cref="UseHookeJeeves"/> minimises by pattern search instead.
    ///
    /// <para>A hop starts from a design that is already near a minimum and only needs
    /// re-settling, which LM does in tens of iterations and then stops. PSD goes deeper per
    /// iteration and was measured to reach a better basin on the designs here, which is why it
    /// is the default despite costing more inside a hop.</para>
    /// </summary>
    public StepMethod StepMethod { get; set; } = StepMethod.Psd3;

    /// <summary>Minimise each basin by Hooke-Jeeves rather than by least squares.</summary>
    public bool UseHookeJeeves { get; set; }

    public double HjInitialStep { get; set; } = 0.25;
    public double HjMinStep { get; set; } = 1e-4;
    public int HjStepsPerHop { get; set; } = 30;

    /// <summary>
    /// The per-hop least-squares budget. Four hundred rather than some thousands: a
    /// hop only has to re-settle a design the kick barely moved, and our per-iteration cost is a
    /// seventeen-pass analytic Jacobian through the full seventh order, so the cap here is a
    /// real budget rather than the insurance it can afford to be in a native engine.
    /// </summary>
    public int LmIterationsPerHop { get; set; } = 400;
    public double LmInitialDamping { get; set; } = 1e-3;
    public double LmTolerance { get; set; } = 1e-10;

    /// <summary>
    /// The kick on the FIRST hop, before the design has ever been minimised, in units of each
    /// variable's natural scale. Separate from <see cref="HopSigma"/> because the first kick has
    /// a different job: it breaks exact symmetry, since a design sitting on a stationary point
    /// has nowhere to go otherwise, whereas every later kick is asking to be moved somewhere new.
    ///
    /// <para>Default 0.001, the same as <see cref="HopSigma"/>, so leaving it alone is exactly
    /// the behaviour that was there before it was connected. Raise it to start the search from a
    /// design deliberately disturbed - useful off a skeleton, where the starting point is a guess
    /// rather than a design - without making every subsequent hop that violent.</para>
    ///
    /// <para>It obeys <see cref="HopFiguring"/> like any other kick: the first hop is still a hop.</para>
    /// </summary>
    public double InitialPerturbSigma { get; set; } = 0.001;

    /// <summary>
    /// The ordinary hop, in units of each variable's natural scale.
    ///
    /// <para><b>A whisper, not a shove</b> - a tenth of a per cent. This is deliberately the same
    /// 0.001 as <see cref="InitialPerturbSigma"/> - one small kick per hop. The instinct that a hop should be large enough to
    /// "leave the basin" is wrong and was measured to be wrong: a large kick lands the design
    /// somewhere unrelated, the per-hop minimisation cannot recover it, and the acceptance test
    /// then compares two unfinished designs. Escape is not the kick's job. It belongs to the
    /// Metropolis walk, to the long jump at <see cref="RestartSigma"/> after
    /// <see cref="RestartAfterStalledHops"/>, and to the elite restart.</para>
    /// </summary>
    public double HopSigma { get; set; } = 0.001;

    /// <summary>
    /// Whether a hop kicks the FIGURING variables - conic and the aspheric terms - or leaves them
    /// where the local optimiser put them. Default false.
    ///
    /// <para><b>They are still optimised.</b> This governs the random kick only; Hooke-Jeeves and
    /// the least-squares stage step them exactly as before. LensHH-LT draws the same line, and it
    /// is the right one: a figuring term is a nearly-linear correction that the local stage fits
    /// reliably from wherever it starts, so kicking it does not choose a different basin - it
    /// discards a fitted figure that is about to be fitted again. The kick's budget is better
    /// spent on the shape variables, which do choose the basin.</para>
    ///
    /// <para>Set it true to kick them anyway - defensible for a conic, which at -1 and at 0 is a
    /// genuinely different surface rather than a small correction. No design has yet been found
    /// where it changes the answer either way.</para>
    /// </summary>
    public bool HopFiguring { get; set; }

    /// <summary>Accept a worse design with probability exp(-dMerit/T), to walk between basins.</summary>
    public bool EnableMetropolis { get; set; } = true;

    /// <summary>The T above. Zero autotunes it from the merit the chain is working at.</summary>
    public double MetropolisTemperature { get; set; }

    /// <summary>Long-jump restart after this many hops with no new best. Zero switches it off.</summary>
    public int RestartAfterStalledHops { get; set; } = 20;

    /// <summary>Magnitude of that long jump, in units of the natural scale.</summary>
    public double RestartSigma { get; set; } = 0.5;

    /// <summary>
    /// Hops a chain must go without a best of its own before it may be handed another chain's
    /// design. Zero switches the rescue off.
    /// </summary>
    public int EliteRestartHops { get; set; } = 150;

    /// <summary>
    /// And how much worse than the global best it must be for that to happen. BOTH conditions
    /// are required; see <see cref="BasinHopping"/> for why either alone is wrong.
    /// </summary>
    public double EliteRestartMeritFactor { get; set; } = 10.0;

    /// <summary>Try swapping catalog glasses as part of the hop.</summary>
    public bool GlassSubstitution { get; set; }

    /// <summary>
    /// The substitution catalogue to choose glasses from - CoreSet28, say.
    ///
    /// <para>One named set, not every catalogue the program can read. A search free to pick from
    /// all of them settles on glasses nobody stocks, and the design that comes back cannot be
    /// built. See <see cref="AberrationCalculator.Core.Glass.SubstitutionCatalog"/>.</para>
    /// </summary>
    public string? SubstitutionCatalog { get; set; }

    public int Seed { get; set; } = 1234;

    public CancellationToken Cancellation { get; set; } = CancellationToken.None;

    /// <summary>Called as chains make progress, for a status line.</summary>
    public IProgress<BasinHoppingProgress>? Progress { get; set; }

    /// <summary>
    /// Called the moment a chain beats its own best, with the chain's index, a copy of the
    /// design, and its merit.
    ///
    /// <para><b>So that stopping never costs anything.</b> The default is three thousand hops
    /// and a designer stops the run when the best stops moving, so being interrupted is the
    /// normal ending - but a run that only writes when it finishes holds hours of work in memory
    /// with nothing on disk, and loses all of it to a crash, a reboot, or any stop that is not a
    /// console Ctrl+C. A chain's answer should exist outside the chain from the instant it is
    /// found.</para>
    ///
    /// <para>Each chain reports only its own index, so a handler that writes one file per chain
    /// needs no lock: no two chains ever name the same file.</para>
    /// </summary>
    public Action<int, OpticalSystem, double>? OnChainBest { get; set; }
}

/// <summary>A snapshot of a run in flight.</summary>
public sealed record BasinHoppingProgress(int Chain, int Hop, double ChainBest, double GlobalBest,
                                          int Accepted, int Rejected, int GlassSwaps);

/// <summary>What a basin-hopping run found.</summary>
public sealed class BasinHoppingResult
{
    public double[] X { get; init; } = Array.Empty<double>();
    public double Merit { get; init; }
    public double InitialMerit { get; init; }
    public int Hops { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int GlassSwaps { get; init; }
    public int Chains { get; init; }

    /// <summary>The best design found, ready to be written out.</summary>
    public OpticalSystem? Best { get; init; }

    /// <summary>
    /// The best design from EACH chain, best first, with its merit.
    ///
    /// <para>Chains search independently and land in different basins, so they come back with
    /// genuinely different lenses rather than with the same lens found several times. Which one
    /// is worth having is a judgement about manufacturability, glass availability and what the
    /// design is for - none of which the merit function knows - so all of them are handed back
    /// and the designer chooses.</para>
    /// </summary>
    public IReadOnlyList<(OpticalSystem Design, double Merit)> ChainBest { get; init; }
        = Array.Empty<(OpticalSystem, double)>();

    public bool Improved => Merit < InitialMerit;
}

/// <summary>
/// Basin hopping: local minimisation, a kick, local minimisation again, and a rule for whether
/// to stay where the kick landed.
///
/// <para><b>What it is for.</b> Every method in <see cref="LocalOptimizer"/> and
/// <see cref="HookeJeeves"/> finds the bottom of the valley it starts in and stops. A lens design
/// problem has a great many valleys, most of them poor, and which one a design falls into is
/// decided by the starting prescription rather than by anything about the optics. Basin hopping
/// turns that landscape into a search over the valley BOTTOMS: the kick decides which valley, the
/// local minimiser decides how deep, and the acceptance rule decides whether the walk stays.</para>
///
/// <para><b>Metropolis, and why not simply greedy.</b> Accepting only improvements makes the walk
/// a hill climb over basins and it gets stuck in the first decent one. Accepting a worse design
/// with probability exp(-dMerit/T) lets the chain cross a ridge and find what is on the other
/// side. The temperature is what sets how far uphill it will walk, and autotuning it from the
/// merit the chain is actually working at keeps that meaningful as the design improves by orders
/// of magnitude - a fixed T that was sensible at the start is either paralysing or meaningless by
/// the end.</para>
///
/// <para><b>Two rescues, and the difference between them.</b> A chain that has gone a while
/// without a new best gets a LONG JUMP - a much larger kick from its own best - on the theory
/// that its neighbourhood is exhausted. Separately, a chain that is both stalled AND far worse
/// than the best any chain has found may be handed that design to work from. Both conditions are
/// required for the second, and this matters: a Metropolis walk goes tens of hops between records
/// while working perfectly well, so a stall alone is not evidence of failure, and reseeding on it
/// collapses every chain onto the leader and throws away the independence that made running
/// several worth it.</para>
///
/// <para><b>Glass is the discrete variable.</b> Curvatures and thicknesses are continuous and
/// are moved by the gradient. A catalog glass is not: there is no
/// derivative from one glass to the next, only a list. So a glass change can only be a hop - it
/// is proposed here, the continuous variables are re-minimised around it, and the acceptance rule
/// judges the pair together.</para>
/// </summary>
public sealed class BasinHopping
{
    private readonly OpticalSystem _start;
    private readonly GlassCatalog _catalog;
    private readonly VariableSet _variables;
    private readonly IReadOnlyList<Operand> _operands;
    private readonly BasinHoppingOptions _options;

    private long _accepted, _rejected, _glassSwaps, _hops;
    private readonly object _bestLock = new();
    private double _globalBest = double.PositiveInfinity;
    private OpticalSystem? _globalBestSystem;

    public BasinHopping(OpticalSystem system, GlassCatalog catalog, VariableSet variables,
                        IReadOnlyList<Operand> operands, BasinHoppingOptions? options = null)
    {
        _start = system ?? throw new ArgumentNullException(nameof(system));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _operands = operands ?? throw new ArgumentNullException(nameof(operands));
        _options = options ?? new BasinHoppingOptions();
    }

    /// <summary>
    /// Whether the cores are already spoken for by the chains, in which case the Jacobian's own
    /// parallel loop must stand down. One chain leaves them free, and then it should not.
    /// </summary>
    private bool _chainsRunInParallel;

    public BasinHoppingResult Run()
    {
        int chains = _options.Chains > 0 ? _options.Chains : DefaultChains();
        chains = Math.Max(1, chains);
        _chainsRunInParallel = chains > 1;

        // Where the run began, so that a run which finds nothing can say so honestly rather than
        // handing back whatever it happened to end on.
        double initialMerit;
        {
            // Constructing the Design folds it inside its bounds, so the merit below - and the
            // global best seeded from it - describe a design that respects its own constraints.
            var design = NewDesign(DesignCopy.Deep(_start));
            var merit = NewMerit(design);
            var r = merit.Evaluate(false);
            initialMerit = r.Ok ? r.Merit : double.PositiveInfinity;
            _globalBest = initialMerit;
            _globalBestSystem = DesignCopy.Deep(design.System);   // the FOLDED design
        }

        // EVERY GLASS READ BEFORE ANY CHAIN STARTS. A file read inside a running optimisation is
        // a bug however rarely it happens - it puts the disk in the inner loop and makes the run
        // depend on what is on it. This is the last moment anything is loaded.
        if (_options.GlassSubstitution) GlassPool(_start);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = chains,
            CancellationToken = CancellationToken.None,
        };

        // Each chain's own answer, kept apart from the global best. Written by index, so the
        // chains never contend for it.
        var perChain = new (OpticalSystem? Design, double Merit)[chains];

        try
        {
            Parallel.For(0, chains, options, chain => RunChain(chain, perChain));
        }
        catch (OperationCanceledException)
        {
            // A cancelled run still has whatever the chains found before it stopped.
        }

        var ranked = new List<(OpticalSystem Design, double Merit)>();
        foreach (var (design, merit) in perChain)
            if (design != null && !double.IsInfinity(merit)) ranked.Add((design, merit));
        ranked.Sort((a, b) => a.Merit.CompareTo(b.Merit));

        var best = _globalBestSystem ?? DesignCopy.Deep(_start);
        var finalDesign = NewDesign(best);
        return new BasinHoppingResult
        {
            ChainBest = ranked,
            X = finalDesign.Read(),
            Merit = _globalBest,
            InitialMerit = initialMerit,
            Hops = (int)Interlocked.Read(ref _hops),
            Accepted = (int)Interlocked.Read(ref _accepted),
            Rejected = (int)Interlocked.Read(ref _rejected),
            GlassSwaps = (int)Interlocked.Read(ref _glassSwaps),
            Chains = chains,
            Best = best,
        };
    }

    /// <summary>
    /// One independent chain, on its own copy of the lens.
    /// </summary>
    /// <param name="perChain">
    /// Where this chain leaves its own answer. Written at its own index and read by nobody else
    /// until every chain has finished, so no lock is needed.
    /// </param>
    /// <summary>
    /// One chain: a Metropolis walk over basin bottoms.
    ///
    /// <para><b>Each hop is a pattern search and THEN a least-squares polish</b>, in that order,
    /// and the order is the point. On a lens merit surface the valley is long, shallow and
    /// curved, and a quadratic model fitted at your feet points at a wall a short distance ahead:
    /// measured on a double Gauss, Hooke-Jeeves reached a better spot in 47 steps than the exact
    /// Jacobian reached in 6000. The pattern search walks the length of the valley, and the LM
    /// then polishes a design that is already settled - which is why an iteration cap of six
    /// thousand is insurance rather than a budget that gets spent every hop.</para>
    ///
    /// <para><b>The walk centre is kept apart from the best.</b> The centre may step onto a WORSE
    /// design, accepted with probability exp(-dM/T), which is what lets it cross a ridge; the
    /// best is tracked separately so wandering never loses ground.</para>
    /// </summary>
    private void RunChain(int chain, (OpticalSystem? Design, double Merit)[] perChain)
    {
        var system = DesignCopy.Deep(_start);
        var design = NewDesign(system);
        var merit = NewMerit(design);
        var rng = new Random(_options.Seed + chain * 7919);

        double[] bestX = design.Read();          // in bounds already: see the Design constructor
        var bestGlass = CaptureGlasses(design);
        double bestMerit = Evaluate(merit);
        var bestSystem = DesignCopy.Deep(system);

        double[] centreX = (double[])bestX.Clone();
        var centreGlass = new Dictionary<int, Glass>(bestGlass);
        double centreMerit = bestMerit;

        double temperature = _options.MetropolisTemperature;
        var dmHistory = new List<double>();
        int hopsSinceCooling = 0;

        double sigma = _options.HopSigma;
        double hjStep = _options.HjInitialStep;

        // TWO counters, deliberately. hopsSinceBest is "how long since this chain improved on
        // itself" and is cleared only by an improvement or by adopting the elite. hopsSinceKick
        // is the same count but is ALSO cleared by each long jump, because the jump asks "has the
        // walk gone quiet lately" and a jump answers it. With one counter the 20-hop jump reset
        // it, the 150-hop elite trigger could never be reached, and the rescue was dead code.
        int hopsSinceBest = 0, hopsSinceKick = 0;

        Publish(bestMerit, system);

        // Every chain has an answer on disk from the outset, even one that never improves on
        // where it started - so the file set after a stop is the same file set as after a
        // finish, and a chain that found nothing says so by holding the design it began with.
        _options.OnChainBest?.Invoke(chain, DesignCopy.Deep(system), bestMerit);

        for (int hop = 1; hop <= _options.MaxHops; hop++)
        {
            // BREAK, not return: being stopped is the normal way a 3000-hop run ends, and what
            // this chain found still has to be recorded below.
            if (_options.Cancellation.IsCancellationRequested) break;
            Interlocked.Increment(ref _hops);

            bool restartHop = false;
            int swapsThisHop = 0;

            // ── The elite rescue ───────────────────────────────────────────────────────────
            // A stall is NOT "this chain has not set a record lately" - a Metropolis walk goes
            // tens of hops between records while working perfectly well, and reseeding on that
            // alone collapses every chain onto the leader. It is "not competitive AND has had
            // long enough to prove otherwise": both halves are required.
            if (_options.EliteRestartHops > 0 && hopsSinceBest >= _options.EliteRestartHops)
            {
                OpticalSystem? elite = null;
                lock (_bestLock)
                {
                    if (_globalBestSystem != null
                        && bestMerit > _globalBest * _options.EliteRestartMeritFactor)
                        elite = DesignCopy.Deep(_globalBestSystem);
                }

                if (elite != null)
                {
                    DesignCopy.CopyInto(elite, system);
                    design.RefreshIndices();
                    // The adopted design becomes this chain's own best AND its centre, or the
                    // next restore would drag it back to what it just abandoned.
                    bestX = design.Read();
                    bestGlass = CaptureGlasses(design);
                    bestMerit = Evaluate(merit);
                    bestSystem = DesignCopy.Deep(system);
                    centreX = (double[])bestX.Clone();
                    centreGlass = new Dictionary<int, Glass>(bestGlass);
                    centreMerit = bestMerit;

                    sigma = _options.HopSigma;
                    hopsSinceBest = 0;
                    hopsSinceKick = 0;
                    restartHop = true;
                }
            }

            // ── The local escape: this chain restarting itself from its own best ───────────
            if (!restartHop && _options.RestartAfterStalledHops > 0
                && hopsSinceKick >= _options.RestartAfterStalledHops)
            {
                design.Apply(bestX);
                RestoreGlasses(design, bestGlass);
                design.RefreshIndices();
                Randomize(design, _options.RestartSigma, rng, _options.HopFiguring);
                sigma = _options.HopSigma;
                // NOT hopsSinceBest: a jump is not an improvement, and clearing it here is
                // exactly what made the elite trigger unreachable.
                hopsSinceKick = 0;
                restartHop = true;
            }

            // ── Glass, roughly one swap per hop ───────────────────────────────────────────
            // Skipped on a restart hop, where the jump has already relocated the design.
            if (_options.GlassSubstitution && !restartHop)
                swapsThisHop = SwapGlasses(design, rng);

            // ── The kick ──────────────────────────────────────────────────────────────────
            // Hop 1 is the symmetry-breaker and takes its own sigma; see InitialPerturbSigma.
            // The two default to the same 0.001, so this is a knob rather than a change.
            if (!restartHop)
                Randomize(design, hop == 1 ? _options.InitialPerturbSigma : sigma, rng,
                          _options.HopFiguring);

            // ── Pattern search, then least squares ────────────────────────────────────────
            var hjClock = System.Diagnostics.Stopwatch.StartNew();
            if (_options.HjStepsPerHop > 0) HookeJeevesStage(design, merit, hjStep, rng);
            hjClock.Stop();

            var lmClock = System.Diagnostics.Stopwatch.StartNew();
            double trialMerit = Minimise(design, merit);
            lmClock.Stop();

            if (Environment.GetEnvironmentVariable("ABCALC_HOP_TIMING") == "1")
                Console.Error.WriteLine(
                    $"    hop {hop,3}  chain {chain}  HJ {hjClock.ElapsedMilliseconds,6} ms   "
                  + $"LM {lmClock.ElapsedMilliseconds,6} ms   iters {_lastIterations,5}  "
                  + $"stop {_lastStop}");

            bool newBest = trialMerit < bestMerit;
            if (newBest)
            {
                bestMerit = trialMerit;
                bestX = design.Read();
                bestGlass = CaptureGlasses(design);
                bestSystem = DesignCopy.Deep(system);
                Publish(bestMerit, system);

                // Hand it out NOW, not when the run ends. Everything after this point is
                // optional: the chain can be stopped, the machine can fall over, and what was
                // found is already somewhere else.
                _options.OnChainBest?.Invoke(chain, DesignCopy.Deep(system), bestMerit);
            }

            hopsSinceBest = newBest ? 0 : hopsSinceBest + 1;
            hopsSinceKick = newBest ? 0 : hopsSinceKick + 1;

            // ── Metropolis ────────────────────────────────────────────────────────────────
            double dm = trialMerit - centreMerit;
            bool acceptCentre;
            if (restartHop)
            {
                // A restart is an independent trial jumped off the best. Commit it as the new
                // centre ONLY if it beat the global best; otherwise it landed somewhere worse,
                // often somewhere that does not trace, and stranding the chain there means every
                // following hop perturbs off garbage.
                acceptCentre = newBest;
            }
            else if (dm <= 0.0)
            {
                acceptCentre = true;
            }
            else if (_options.EnableMetropolis)
            {
                if (temperature <= 0.0)
                {
                    // Autotune from the first few uphill samples; accept at even odds meanwhile.
                    dmHistory.Add(dm);
                    if (dmHistory.Count >= MetropolisAutotuneTrials)
                    {
                        double sum = 0.0;
                        foreach (double d in dmHistory) sum += d;
                        temperature = Math.Max(1e-30, sum / dmHistory.Count);
                    }
                    acceptCentre = rng.NextDouble() < 0.5;
                }
                else
                {
                    acceptCentre = rng.NextDouble() < Math.Exp(-dm / temperature);
                }
            }
            else
            {
                acceptCentre = false;
            }

            if (acceptCentre)
            {
                centreX = design.Read();
                centreGlass = CaptureGlasses(design);
                centreMerit = trialMerit;
                Interlocked.Increment(ref _accepted);
                Interlocked.Add(ref _glassSwaps, swapsThisHop);

                if (dm <= 0.0)
                {
                    // Downhill: a fertile region, so shrink the kick and fine-tune.
                    sigma = _options.HopSigma;
                    hjStep = Math.Max(_options.HjMinStep, hjStep * 0.9);
                }
                else
                {
                    // Uphill: still exploring, so let the kick grow until it wraps.
                    double grown = sigma * 1.5;
                    sigma = grown >= 2.0 ? _options.HopSigma : grown;
                }
            }
            else if (restartHop)
            {
                // A failed restart returns the walk to the BEST, not to the stale pre-restart
                // centre, so the chain is never stranded on a bad jump.
                design.Apply(bestX);
                RestoreGlasses(design, bestGlass);
                design.RefreshIndices();
                centreX = (double[])bestX.Clone();
                centreGlass = new Dictionary<int, Glass>(bestGlass);
                centreMerit = bestMerit;
                Interlocked.Increment(ref _rejected);
                sigma = _options.HopSigma;
            }
            else
            {
                design.Apply(centreX);
                RestoreGlasses(design, centreGlass);
                design.RefreshIndices();
                Interlocked.Increment(ref _rejected);
                double grown = sigma * 1.5;
                sigma = grown >= 2.0 ? _options.HopSigma : grown;
            }

            _options.Progress?.Report(new BasinHoppingProgress(
                chain, hop, bestMerit, _globalBest,
                (int)Interlocked.Read(ref _accepted), (int)Interlocked.Read(ref _rejected),
                (int)Interlocked.Read(ref _glassSwaps)));

            // Cool periodically so the walk settles toward the best basin.
            hopsSinceCooling++;
            if (temperature > 0.0 && hopsSinceCooling >= MetropolisCoolingInterval)
            {
                temperature *= MetropolisCoolingRate;
                hopsSinceCooling = 0;
            }
        }

        perChain[chain] = (bestSystem, bestMerit);
    }

    /// <summary>How many uphill samples the temperature is averaged from before it is trusted.</summary>
    private const int MetropolisAutotuneTrials = 8;

    private const int MetropolisCoolingInterval = 20;
    private const double MetropolisCoolingRate = 0.95;

    private static double Evaluate(MeritFunction merit)
    {
        var r = merit.Evaluate(false);
        return r.Ok ? r.Merit : double.PositiveInfinity;
    }

    /// <summary>
    /// The per-variable step, and why one number cannot serve every variable.
    ///
    /// <para>A bounded variable gets half its bound width, so sigma near one spans the range. An
    /// unbounded one gets the larger of its own magnitude and one, which gives a thickness near
    /// ninety a step proportional to itself while leaving a curvature near 0.04 at unit scale.
    /// A flat step in the variables' own units is either nothing to the thickness or enough to
    /// turn the lens inside out.</para>
    /// <summary>
    /// How far a kick of one sigma moves a variable, in its own physical units.
    ///
    /// <para><b>A bounded variable is kicked across its own interval</b>, which is the only scale
    /// it can have that means anything.</para>
    ///
    /// <para><b>An unbounded curvature or thickness is kicked against its own size, floored at
    /// one.</b> The floor is what lets a variable sitting at exactly zero move at all, and for
    /// these two it is harmless: a curvature near 0.01 is kicked by a tenth of a per cent of one,
    /// which is a tenth of the curvature - meaningful and survivable.</para>
    ///
    /// <para><b>For the FIGURING kinds that floor is catastrophic, and this is why they get their
    /// own rule.</b> An r^4 coefficient lives near 1E-6 and an r^8 one near 1E-12. Floored at one
    /// they are kicked by 1E-3 - a MILLION times the value for r^4 and a billion for r^8 - which
    /// puts a sag of ten lens units on the surface. It does not crash: the local optimisation
    /// after each hop hauls the design back and the Metropolis test rejects it. That is the
    /// damage. Every hop on a figuring variable is spent climbing out of somewhere absurd rather
    /// than exploring, which is exactly what this file says about large kicks - "a large kick
    /// lands the design somewhere unrelated, the per-hop minimisation cannot recover it, and the
    /// acceptance test then compares two unfinished designs".</para>
    ///
    /// <para>So figuring is kicked against the scale <see cref="Scaling.PhysicalCeilings"/>
    /// computes for it, which puts every kind on one footing - how far a step moves the glass at
    /// the edge of the aperture - and is the same scale the local optimiser steps by.</para>
    /// </summary>
    internal static double PerturbScale(Variable v, double value, double[] ceilings, int i)
    {
        if (v.IsBounded && !double.IsNegativeInfinity(v.Min) && !double.IsPositiveInfinity(v.Max))
            return 0.5 * (v.Max - v.Min);

        if (v.Figures && i < ceilings.Length && ceilings[i] > 0.0) return ceilings[i];

        return Math.Max(Math.Abs(value), 1.0);
    }
    /// <summary>
    /// A Gaussian kick in PHYSICAL units, reflected back inside the bounds rather than clamped.
    ///
    /// <para>Clamping piles variables onto the walls and they never come off; reflection keeps a
    /// constrained variable exploring the interior.</para>
    /// </summary>
    internal static void Randomize(Design design, double sigma, Random rng, bool hopFiguring)
    {
        var x = design.Read();
        var ceilings = Scaling.PhysicalCeilings(design);
        var items = design.Variables.Items;
        for (int i = 0; i < x.Length && i < items.Count; i++)
        {
            // The kick skips figuring by default; the LOCAL stages below still step it, so the
            // aspheric is optimised at every hop - it is simply not thrown first.
            if (!hopFiguring && items[i].Figures) continue;

            x[i] += sigma * PerturbScale(items[i], x[i], ceilings, i) * Gaussian(rng);
        }
        design.Apply(x);                       // Apply folds each variable inside its bounds
    }

    /// <summary>
    /// Hooke-Jeeves: probe each variable up and down, keep what helps, and when the whole sweep
    /// helped, try going the same way again.
    ///
    /// <para><b>That last part is why it beats an exact Jacobian here.</b> The pattern move
    /// extrapolates along the direction that just worked, which is exactly what a long curved
    /// valley rewards, while a quadratic model fitted at one point cannot see round the bend.
    /// When a sweep fails the step halves, and the search stops when the step is too small to
    /// matter.</para>
    /// </summary>
    private void HookeJeevesStage(Design design, MeritFunction merit, double step, Random rng)
    {
        var items = design.Variables.Items;
        int n = items.Count;
        if (n == 0) return;

        var baseX = design.Read();
        var scale = new double[n];
        var ceilings = Scaling.PhysicalCeilings(design);
        for (int i = 0; i < n; i++) scale[i] = PerturbScale(items[i], baseX[i], ceilings, i);

        double baseF = Evaluate(merit);

        for (int it = 0; it < _options.HjStepsPerHop && step > _options.HjMinStep; it++)
        {
            if (_options.Cancellation.IsCancellationRequested) break;

            var x = (double[])baseX.Clone();
            double f = baseF;

            for (int i = 0; i < n; i++)
            {
                double xi = x[i];
                double h = step * scale[i];

                // SKIP A PROBE THAT MOVED NOTHING. Apply folds each variable inside its bounds
                // and writes the folded value back, so a variable pinned at a bound comes back
                // where it started and the probe design is IDENTICAL to the base - its merit is
                // already known to be f, and evaluating it again is pure waste. Nine of the
                // seventeen variables here are bounded thicknesses and several sit on their
                // limits, so this is not a rare case: it was most of a thousand evaluations per
                // hop, through the full seventh-order chain, to learn nothing.
                x[i] = xi + h;
                design.Apply(x);                       // folds x[i] in place
                if (x[i] != xi)
                {
                    double up = Evaluate(merit);
                    if (up < f) { f = up; continue; }
                }

                x[i] = xi - h;
                design.Apply(x);
                if (x[i] != xi)
                {
                    double down = Evaluate(merit);
                    if (down < f) { f = down; continue; }
                }

                x[i] = xi;
            }

            if (f < baseF)
            {
                // The pattern move: as far again in the direction the sweep just went.
                var pattern = new double[n];
                for (int i = 0; i < n; i++) pattern[i] = x[i] + (x[i] - baseX[i]);
                design.Apply(pattern);                 // folds pattern in place
                double fPattern = Evaluate(merit);

                if (fPattern < f) { baseX = pattern; baseF = fPattern; }
                else { baseX = x; baseF = f; design.Apply(baseX); }
            }
            else
            {
                step *= 0.5;
                design.Apply(baseX);
            }
        }

        design.Apply(baseX);
    }

    /// <summary>
    /// A glass is a NAME AND THE CATALOGUE IT CAME FROM, and both have to travel together.
    ///
    /// <para>A substitution sets both; saving only the name meant that restoring after a rejected
    /// hop left the previous name beside the new catalogue. The pair then resolves to whatever
    /// that catalogue happens to call that name - a different glass, or none - and the design
    /// quietly stops being the one that was measured. The same name means different glass in
    /// different catalogues, which is exactly why <c>glass_dir</c> exists elsewhere in this
    /// program.</para>
    /// </summary>
    [ThreadStatic] private static int _lastIterations;
    [ThreadStatic] private static string? _lastStop;

    private readonly record struct Glass(string Material, string? Catalog, GlassData? Pointer);

    private static Dictionary<int, Glass> CaptureGlasses(Design design)
    {
        var system = design.System;
        var map = new Dictionary<int, Glass>();
        for (int i = 0; i < system.Surfaces.Count; i++)
        {
            string? m = system.Surfaces[i].Material;
            if (!string.IsNullOrWhiteSpace(m))
                map[i] = new Glass(m!, system.Surfaces[i].CatalogName, design.MaterialAt(i));
        }
        return map;
    }

    /// <summary>
    /// Puts the glasses back after a rejected hop - by moving pointers, not by resolving names.
    /// </summary>
    private static void RestoreGlasses(Design design, Dictionary<int, Glass> glasses)
    {
        foreach (var kv in glasses)
            design.RestoreMaterial(kv.Key, kv.Value.Pointer, kv.Value.Material, kv.Value.Catalog);
    }
    private static double Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>Least-squares polish of the basin the design has landed in.</summary>
    private double Minimise(Design design, MeritFunction merit)
    {
        if (design.Variables.Count == 0 || _options.LmIterationsPerHop <= 0)
            return Evaluate(merit);

        var r = new LocalOptimizer(merit, new OptimizerOptions
        {
            Method = _options.StepMethod,
            MaxIterations = _options.LmIterationsPerHop,
            InitialDamping = _options.LmInitialDamping,
            Tolerance = _options.LmTolerance,
            Cancellation = _options.Cancellation,
        }).Run();

        _lastIterations = r.Iterations;
        _lastStop = r.Stop;
        return r.Ok ? r.Merit : double.PositiveInfinity;
    }

    /// <summary>
    /// Offers every glass surface a new glass, at about one swap per hop overall.
    ///
    /// <para>Per SURFACE rather than one surface per hop, at <c>max(1/n, 0.1)</c> - so a design
    /// with few elements still sees its glasses move, and one with many does not have every
    /// element replaced at once. Glass is discrete: there is no gradient from N-BK7 to SF11, so a
    /// change can only be proposed and then judged by whether the polish that follows leaves the
    /// design better.</para>
    /// </summary>
    private int SwapGlasses(Design design, Random rng)
    {
        var pool = GlassPool(design.System);
        if (pool.Count == 0) return 0;

        var system = design.System;
        int last = system.LastOpticalSurface();

        var surfaces = new List<int>();
        for (int i = 1; i <= last; i++)
            if (Operand.IsGlassAfter(system, i)) surfaces.Add(i);
        if (surfaces.Count == 0) return 0;

        double probability = Math.Max(1.0 / surfaces.Count, 0.1);
        int swaps = 0;

        foreach (int surface in surfaces)
        {
            if (rng.NextDouble() >= probability) continue;
            // The swap IS the pointer move. The glass came out of a pool read once before any
            // chain started, so nothing is resolved by name and nothing touches a file.
            design.SetMaterial(surface, pool[rng.Next(pool.Count)]);
            swaps++;
        }

        if (swaps > 0) design.RefreshIndices();
        return swaps;
    }

    private List<GlassData>? _pool;
    private readonly object _poolLock = new();

    /// <summary>
    /// The glasses a substitution may draw on, built once and shared by every chain.
    ///
    /// <para>From the NAMED substitution catalogue and nothing else. The catalogues used to read
    /// a design are a different set for a different purpose - every vendor has to be there or an
    /// index resolves wrongly - and letting the search pick from all of them is how a run ends
    /// with a glass nobody stocks.</para>
    /// </summary>
    private List<GlassData> GlassPool(OpticalSystem system)
    {
        lock (_poolLock)
        {
            if (_pool != null) return _pool;

            var pool = new List<GlassData>();
            if (!string.IsNullOrWhiteSpace(_options.SubstitutionCatalog))
            {
                var source = SubstitutionCatalog.Load(_options.SubstitutionCatalog!);
                foreach (string c in source.LoadedCatalogs)
                    foreach (var g in source.InCatalog(c))
                        if (g.Nd > 1.0 && g.Vd > 0.0) pool.Add(g);
            }

            _pool = pool;
            return pool;
        }
    }

    private void Publish(double merit, OpticalSystem system)
    {
        lock (_bestLock)
        {
            if (merit < _globalBest)
            {
                _globalBest = merit;
                _globalBestSystem = DesignCopy.Deep(system);
            }
        }
    }

    /// <summary>
    /// How many chains to run when the caller does not say.
    ///
    /// <para>Not one per logical processor. <see cref="Environment.ProcessorCount"/> counts
    /// hyperthreads, and a chain is dense floating-point work that gets little from sharing a
    /// core's execution units with another chain; on the hybrid parts now common it also counts
    /// efficiency cores, which run the same chain markedly slower and hold the whole run up,
    /// since a hop is only finished when its chain finishes. Half the logical count is a
    /// reasonable stand-in for the physical cores on an SMT machine and errs toward leaving the
    /// machine usable, which matters for something that runs for minutes. Say
    /// <c>--chains</c> to override it in either direction.</para>
    /// </summary>
    public static int DefaultChains() => CpuInfo.PhysicalCoreCount();

    private Design NewDesign(OpticalSystem system) => new(system, _catalog, _variables);

    private MeritFunction NewMerit(Design design)
    {
        var m = new MeritFunction(design);
        m.AddRange(_operands);

        // This class is the outer parallel loop, so the inner one has to stand down - see
        // MeritFunction.Sequential. Left on, the two multiply and the run goes several times
        // slower than it would with the chains alone.
        m.Sequential = _chainsRunInParallel;
        return m;
    }
}
