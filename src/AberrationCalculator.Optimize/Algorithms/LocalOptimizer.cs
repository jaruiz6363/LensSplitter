using System;
using System.Threading;

using AberrationCalculator.Optimize.Evaluation;

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>Which diagonal is added to the Gauss-Newton matrix to form the step.</summary>
public enum StepMethod
{
    /// <summary>
    /// Marquardt damping: a multiple of the Gauss-Newton diagonal itself. Robust, and the
    /// benchmark the others have to beat.
    /// </summary>
    Lm = 0,

    /// <summary>
    /// Dilworth's pseudo-second derivative, clipped at zero. The neglected curvature is
    /// estimated per variable and added where it is positive.
    /// </summary>
    Psd2 = 1,

    /// <summary>
    /// The same estimate, and negative curvature answered with damping rather than merely
    /// discarded. See <see cref="LocalOptimizer"/>.
    /// </summary>
    Psd3 = 2,
}

/// <summary>How a local optimisation is to be run.</summary>
public sealed class OptimizerOptions
{
    public StepMethod Method { get; set; } = StepMethod.Psd3;

    public int MaxIterations { get; set; } = 6000;

    /// <summary>Stop when the merit stops improving by this fraction of itself.</summary>
    public double Tolerance { get; set; } = 1e-10;

    public double InitialDamping { get; set; } = 1e-3;

    /// <summary>
    /// The multiplier on Dilworth's curvature term, before it starts adapting.
    ///
    /// <para>Unity, as he specifies. He then scales it "according to the same rule" as a damping
    /// factor and reports that it "usually remained near unity" - see
    /// <see cref="LocalOptimizer"/> for what that rule is here.</para>
    /// </summary>
    public double PsdInitialScale { get; set; } = 1.0;

    /// <summary>Whether that scale adapts, or stays where <see cref="PsdInitialScale"/> put it.</summary>
    public bool PsdAdaptiveScale { get; set; } = false;

    /// <summary>Damping beyond this means the step has nowhere left to go.</summary>
    public double MaxDamping { get; set; } = 1e10;

    /// <summary>Attempts to re-damp a rejected step before giving up on the iteration.</summary>
    public int MaxDampingRetries { get; set; } = 12;

    public CancellationToken Cancellation { get; set; } = CancellationToken.None;
}

/// <summary>What a local optimisation did.</summary>
public sealed class OptimizeResult
{
    public double[] X { get; init; } = Array.Empty<double>();
    public double Merit { get; init; }
    public double InitialMerit { get; init; }
    public int Iterations { get; init; }
    public int Evaluations { get; init; }
    public string Stop { get; init; } = string.Empty;
    public bool Ok { get; init; } = true;
}

/// <summary>
/// Damped least squares, with the damping chosen by Dilworth's pseudo-second-derivative method.
///
/// <para><b>The problem PSD solves.</b> The merit function is a sum of squares,
/// <c>phi = sum r_i^2</c>, whose exact second derivative is</para>
///
/// <code>
///     d2phi/dxj dxk = 2 [ (J'J)_jk + sum_i r_i d2r_i/dxj dxk ]
/// </code>
///
/// <para>Gauss-Newton, and Levenberg-Marquardt after it, drop the second term entirely and damp
/// with an arbitrary multiple of the first. That is safe and it is why LM is hard to break, but
/// the discarded term is not small on a lens: it is exactly the part that knows an aberration
/// coefficient is a strongly curved function of a curvature, and throwing it away is what makes
/// a least-squares run crawl once the residuals stop being small.</para>
///
/// <para><b>What Dilworth does about it.</b> The neglected term cannot be computed without
/// second derivatives of every residual, which nobody wants to form. But its DIAGONAL can be
/// estimated for nothing from something the optimiser already has: the gradient at this
/// iteration and the gradient at the last one. A secant in each variable separately,</para>
///
/// <code>
///     d2phi/dxj^2  ~  ( g_j(k) - g_j(k-1) ) / ( x_j(k) - x_j(k-1) )
/// </code>
///
/// <para>gives the full curvature along that variable, and subtracting the Gauss-Newton
/// diagonal from it leaves an estimate of precisely the part that was dropped. That estimate,
/// rather than a blind multiple of the identity, is what is added to the normal equations.
/// Dilworth, <i>Applied Optics</i> <b>17</b>, 3372 (1978).</para>
///
/// <para><b>Why the Jacobians must be fresh.</b> The secant is a difference of two gradients,
/// and a gradient built from a Broyden-updated Jacobian is not an independent measurement of
/// anything - differencing two of them measures the update rule rather than the design. That is
/// why this optimiser holds an exact analytic Jacobian at every iteration and never updates one
/// approximately: the estimate is only worth having if both ends of the secant are real. Here
/// they are better than real, being analytic rather than differenced, so the estimate carries no
/// truncation error of its own.</para>
///
/// <para><b>What is Dilworth's and what is not.</b> The per-variable secant curvature above, and
/// its use as the diagonal of the damping matrix, are his. The safeguards around it - clipping a
/// negative estimate, smoothing it between iterations, and answering a rejected step by
/// increasing the damping - are ordinary practice rather than anything specific to his paper,
/// and <see cref="StepMethod.Psd2"/> and <see cref="StepMethod.Psd3"/> here differ only in how
/// hard they lean on them. PSD2 clips a negative estimate to zero and lets Marquardt damping
/// carry the direction; PSD3 treats a negative estimate as evidence that the quadratic model is
/// wrong along that variable and damps it in proportion, which is the more aggressive and the
/// better on a design with many variables.</para>
/// </summary>
public sealed class LocalOptimizer
{
    /// <summary>
    /// How far the curvature scale moves per iteration, and how far it may wander.
    ///
    /// <para>Half and double rather than lambda's tenth and tenfold: this multiplies a MEASURED
    /// quantity, not a trust region, and Dilworth reports it stays near unity. The floor and
    /// ceiling are a safeguard against a pathological run rather than a working constraint - if
    /// the scale spends its time pinned at either, the curvature estimate is wrong and no
    /// multiplier is going to rescue it.</para>
    /// </summary>
    /// <para><b>Asymmetric, because lambda is already doing half the job.</b> A rejected step
    /// ramps lambda tenfold; ramping the curvature scale hard on the same event compounds the
    /// two and the diagonal runs away - measured, with a matched pair of factors, as a run that
    /// hit the damping ceiling after 249 iterations and reported itself ill-conditioned while
    /// still descending. The scale relaxes briskly when steps are being taken and tightens only
    /// gently when they are not, which is what keeps it near unity where Dilworth found it.</para>
    private const double PsdScaleDown = 0.5;
    private const double PsdScaleUp = 1.2;
    private const double PsdScaleFloor = 1.0 / 1024.0;
    private const double PsdScaleCeiling = 16.0;

    private readonly MeritFunction _merit;
    private readonly OptimizerOptions _options;

    public LocalOptimizer(MeritFunction merit, OptimizerOptions? options = null)
    {
        _merit = merit ?? throw new ArgumentNullException(nameof(merit));
        _options = options ?? new OptimizerOptions();
    }

    /// <summary>Runs from the design's present state and leaves it at the best point found.</summary>
    public OptimizeResult Run()
    {
        var design = _merit.Design;
        int n = design.Variables.Count;

        var x = design.Read();
        var start = _merit.Evaluate(false);
        if (!start.Ok)
            return new OptimizeResult { X = x, Ok = false, Stop = start.Failure ?? "unevaluable" };
        if (n == 0)
            return new OptimizeResult { X = x, Merit = start.Merit, InitialMerit = start.Merit,
                                        Stop = "no variables" };

        double initialMerit = start.Merit;
        double currentMerit = initialMerit;
        double lambda = _options.InitialDamping;

        // Dilworth's scale on the curvature term: "initially unity, increased or decreased each
        // iteration according to the same rule" as a damping factor - down when a step is taken,
        // up when one is refused - and in his experience it "usually remained near unity".
        //
        // The curvature sits exactly where the damping would, so it IS damping, only measured
        // rather than guessed; and a measured quantity still needs a trust factor, because the
        // secant estimate is approximate and its error is not. Left frozen at one, as it was
        // here, the method carries a term it cannot moderate: too much damping
        // on a variable whose curvature it has overestimated, and no way to relax it.
        double psdScale = _options.PsdInitialScale;
        int evaluations = 1, iteration = 0;
        string stop = "iteration limit";

        // PSD state. The curvature estimate differences two Jacobians, so it needs the previous
        // one and the x it was taken at. Both ends are analytic here, so the secant carries no
        // truncation error of its own.
        bool psdActive = _options.Method != StepMethod.Lm;
        double[,]? jPrevious = null;
        double[]? xAtPrevious = null;
        double[]? curvature = null;
        double[]? curvaturePrevious = null;

        var jtj = new double[n, n];
        var jtr = new double[n];
        var delta = new double[n];

        for (iteration = 1; iteration <= _options.MaxIterations; iteration++)
        {
            if (_options.Cancellation.IsCancellationRequested) { stop = "cancelled"; break; }

            design.Apply(x);
            var here = _merit.Evaluate(true);
            evaluations++;
            if (!here.Ok) { stop = here.Failure ?? "unevaluable"; break; }

            int m = here.Residuals.Length;
            if (m == 0) { stop = "no active residuals"; break; }
            var j = here.Jacobian;

            // Dilworth's diagonal curvature, from this Jacobian and the last.
            if (psdActive && jPrevious != null && xAtPrevious != null)
            {
                var dx = new double[n];
                for (int a = 0; a < n; a++) dx[a] = x[a] - xAtPrevious[a];

                var curv = new double[n];
                for (int a = 0; a < n; a++)
                {
                    // Denominator: this variable's own step, plus the norm of every OTHER
                    // variable's step. That second term is what makes this PSD II rather than
                    // the original PSD I, which used a fixed 1e-4: a variable that barely moved
                    // would otherwise produce an enormous spurious curvature and be damped so
                    // hard it could never move again. PSD III weights the others by the ratio of
                    // the previous iteration's curvatures.
                    double others = 0.0;
                    for (int k = 0; k < n; k++)
                    {
                        if (k == a) continue;
                        double w = 1.0;
                        if (_options.Method == StepMethod.Psd3 && curvaturePrevious != null)
                        {
                            double sj = Math.Abs(curvaturePrevious[a]);
                            double sk = Math.Abs(curvaturePrevious[k]);
                            // A zero sec_j divides by nothing; fall back to PSD II for that pair
                            // rather than manufacture a ratio out of noise.
                            if (sj > 1e-300 && sk > 0.0) w = sk / sj;
                        }
                        others += dx[k] * dx[k] * w;
                    }

                    double denom = Math.Abs(dx[a]) + Math.Sqrt(Math.Max(0.0, others));
                    if (!(denom > 1e-300)) { curv[a] = 0.0; continue; }

                    double acc = 0.0;
                    for (int i = 0; i < m; i++)
                        acc += here.Residuals[i] * ((j[i, a] - jPrevious[i, a]) / denom);

                    // Dilworth: "The logical procedure is to take the absolute value in all
                    // cases; if the negative numbers are correct, this rule will steer the
                    // design away from the stationary point." A negative curvature added to the
                    // diagonal would shrink J'J and could make the normal equations indefinite.
                    curv[a] = Math.Abs(acc);
                    if (double.IsNaN(curv[a]) || double.IsInfinity(curv[a])) curv[a] = 0.0;
                }

                curvature = curv;
                curvaturePrevious = curv;
            }

            jPrevious = (double[,])j.Clone();
            xAtPrevious = (double[])x.Clone();

            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                {
                    double s = 0.0;
                    for (int i = 0; i < m; i++) s += j[i, a] * j[i, b];
                    jtj[a, b] = s;
                }

                double g = 0.0;
                for (int i = 0; i < m; i++) g += j[i, a] * here.Residuals[i];
                jtr[a] = -g;
            }

            // The curvature goes exactly where Dilworth says the damping belongs, and lambda is
            // retained UNDERNEATH it as step control only. Retaining it is a deliberate
            // deviation from the paper: a variable whose estimated curvature is zero would
            // otherwise get no damping at all and take an unbounded step, and the accept/reject
            // loop needs something it can ramp on a rejected step, which the curvature - a
            // property of the function, not of the trust region - cannot provide. Where the
            // curvature is well estimated it dominates and lambda is inert.
            for (int a = 0; a < n; a++)
            {
                if (psdActive && curvature != null) jtj[a, a] += psdScale * curvature[a];
                jtj[a, a] *= 1.0 + lambda;
                if (jtj[a, a] < 1e-20) jtj[a, a] = 1e-20;
            }

            if (!Cholesky.Solve(jtj, jtr, delta)) { stop = "singular matrix"; break; }

            // The real convergence quantities. A finished run has both small; a stuck one has a
            // vanishing step with a gradient that has not vanished.
            double gradientNorm = 0.0, stepNorm = 0.0;
            for (int a = 0; a < n; a++)
            {
                gradientNorm += jtr[a] * jtr[a];
                stepNorm += delta[a] * delta[a];
            }
            gradientNorm = Math.Sqrt(gradientNorm);
            stepNorm = Math.Sqrt(stepNorm);
            if (gradientNorm <= 1e-16) { stop = "gradient is zero"; break; }

            var trial = new double[n];
            for (int a = 0; a < n; a++) trial[a] = x[a] + delta[a];

            design.Apply(trial);                       // folds the trial inside its bounds
            var probe = _merit.Evaluate(false);
            evaluations++;

            if (probe.Ok && probe.Merit < currentMerit)
            {
                double improvement = currentMerit - probe.Merit;
                Array.Copy(trial, x, n);
                currentMerit = probe.Merit;
                lambda *= 0.1;
                if (lambda < 1e-15) lambda = 1e-15;

                // The step was taken, so trust the curvature a little further and let it damp
                // less. Gentler than lambda's factor of ten on purpose: lambda is a trust region
                // and may swing over orders of magnitude, while this multiplies a quantity that
                // was MEASURED and should stay, as Dilworth found it did, near unity.
                if (_options.PsdAdaptiveScale)
                    psdScale = Math.Max(PsdScaleFloor, psdScale * PsdScaleDown);

                // EITHER relative OR absolute improvement below tolerance. Testing only the
                // relative one keeps a run grinding on gains that are real but worthless.
                //
                // The absolute test is GUARDED BY THE DAMPING, which the relative one does not
                // need. A tiny improvement means one of two quite different things: the design
                // is at the bottom, or the STEP was small because lambda was large. Only the
                // first is convergence. Unguarded, the second ends a run mid-descent - measured
                // on the double Gauss as a "converged" at 137 iterations that stopped 15% short,
                // after which a restart from that very point found 5244 more iterations of real
                // improvement, and a third pass then converged in 22. Near the floor lambda is
                // inert and the step is essentially undamped Gauss-Newton, so a small gain there
                // does mean the bottom.
                bool relative = improvement < _options.Tolerance * currentMerit;
                bool absolute = improvement < _options.Tolerance
                             && lambda <= _options.InitialDamping;
                if (relative || absolute)
                {
                    stop = "converged";
                    break;
                }
            }
            else
            {
                // THE REJECT PATH, which is where a finished design actually ends up: at a
                // minimum every direction goes uphill, so no step is ever accepted again and the
                // accept-path test above never fires. Without a test here the run cannot
                // terminate at all - it damps its way to the ceiling and reports the iteration
                // limit on a design that converged long before.
                //
                // Trusted only when the damping has genuinely ramped (the step tried to escape
                // and could not), the step has vanished, and the miss is small.
                double miss = probe.Ok ? probe.Merit - currentMerit : double.PositiveInfinity;
                double rejectTolerance = _options.Tolerance * 0.01;
                bool ramped = lambda >= _options.InitialDamping * 100.0;

                // "The step has vanished" has to be asked RELATIVE to the variables, not against
                // a fixed 1e-6. An optimiser that steps in sigmoid-transformed space has every
                // variable at O(1), and an absolute threshold means the same for all of them;
                // this one steps in PHYSICAL units under reflection, where a curvature is 0.04
                // and a thickness is 90. A fixed 1e-6 there is nothing to a curvature and
                // unreachable for a thickness, so the test never fired and the run could not
                // stop. The constant does not port; the question it asks does.
                double xNorm = 0.0;
                for (int a = 0; a < n; a++) xNorm += x[a] * x[a];
                xNorm = Math.Sqrt(xNorm);
                bool tiny = stepNorm <= 1e-9 * Math.Max(xNorm, 1.0);
                bool relative = miss < rejectTolerance * Math.Max(currentMerit, 1e-12);
                bool absolute = miss < rejectTolerance;

                if (ramped && tiny && (relative || absolute))
                {
                    stop = "converged";
                    break;
                }

                // The step was refused, so the curvature was too small to hold it back: damp
                // harder on the measured term as well as on lambda.
                if (_options.PsdAdaptiveScale)
                    psdScale = Math.Min(PsdScaleCeiling, psdScale * PsdScaleUp);

                lambda *= 10.0;
                if (lambda > _options.MaxDamping)
                {
                    // Reaching the ceiling means every step from Gauss-Newton scale down to a
                    // vanishing one went UPHILL. With the step vanished that is a local minimum,
                    // not a failure, and calling it "damping too large" makes a finished run
                    // look broken. With the step still LARGE it is a different animal -
                    // conditioning, or a Jacobian inconsistent with its residuals - and keeps
                    // the failure wording. The signal that says "minimum" is the vanished step,
                    // not the miss.
                    if (tiny) stop = "converged";
                    else stop = "no descent at any damping; ill-conditioned rather than minimal";
                    break;
                }

                design.Apply(x);                       // put the design back
            }
        }

        design.Apply(x);
        var final = _merit.Evaluate(false);
        return new OptimizeResult
        {
            X = x,
            Merit = final.Ok ? final.Merit : double.PositiveInfinity,
            InitialMerit = initialMerit,
            Iterations = iteration,
            Evaluations = evaluations,
            Stop = stop,
            Ok = final.Ok,
        };
    }
}
