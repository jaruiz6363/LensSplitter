extern alias Ad;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AberrationCalculator.Optimize.Operands;

using Dual = Ad::AberrationCalculator.Core.Ad.Dual;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>What one evaluation of the merit function found.</summary>
public sealed class MeritResult
{
    /// <summary>The raw value of each operand, in its own units.</summary>
    public double[] Values { get; init; } = Array.Empty<double>();

    /// <summary>
    /// The weighted, scaled residuals whose sum of squares is minimised. A satisfied boundary
    /// operand contributes exactly zero.
    /// </summary>
    public double[] Residuals { get; init; } = Array.Empty<double>();

    /// <summary>
    /// The Jacobian of <see cref="Residuals"/> with respect to the variables, exact.
    /// Indexed [residual, variable]. Empty when the evaluation did not ask for it.
    /// </summary>
    public double[,] Jacobian { get; init; } = new double[0, 0];

    /// <summary>Sum of the squared residuals: the quantity the optimiser descends.</summary>
    public double SumSquares { get; init; }

    /// <summary>
    /// The merit as a designer reads it: the weighted RMS of the residuals. Minimising this and
    /// minimising <see cref="SumSquares"/> are the same thing; this one is comparable between
    /// runs whose weights differ.
    /// </summary>
    public double Merit { get; init; }

    /// <summary>
    /// False when the design could not be evaluated at all - a ray that does not get through,
    /// a trace that does not close. The search treats such a point as infinitely bad and steps
    /// back, rather than the run falling over.
    /// </summary>
    public bool Ok { get; init; } = true;

    /// <summary>Why the evaluation failed, when it did.</summary>
    public string? Failure { get; init; }

    /// <summary>A result standing for a design that cannot be evaluated.</summary>
    public static MeritResult Failed(string why) =>
        new() { Ok = false, Failure = why, SumSquares = double.PositiveInfinity,
                Merit = double.PositiveInfinity };
}

/// <summary>
/// The operands, and the two things an optimiser asks of them: what the design is worth, and
/// how that changes with every variable.
///
/// <para><b>The Jacobian is exact.</b> Each column is one pass of the aberration chain carried
/// out in dual arithmetic with that variable seeded, so the derivative comes out of the same
/// arithmetic as the value - no step size, no cancellation, no difference of two nearly equal
/// numbers. The columns are independent and are computed in parallel.</para>
///
/// <para><b>Residuals are relative where they can be.</b> A target operand contributes
/// <c>sqrt(w) (v - t) / |t|</c>, so a weight means the same thing whether the operand is a
/// 50 mm focal length or a 20 micron spot. Where the target is zero - which is what asking for
/// no spot and no distortion looks like - there is nothing to be relative to and the residual is
/// absolute.</para>
/// </summary>
public sealed class MeritFunction
{
    private readonly Design _design;
    private readonly OperandContext _context;
    private readonly List<Operand> _declared = new();
    private List<Operand> _expanded = new();

    public MeritFunction(Design design)
    {
        _design = design ?? throw new ArgumentNullException(nameof(design));
        _context = new OperandContext(design.System);
    }

    public Design Design => _design;

    public OperandContext Context => _context;

    /// <summary>The operands as the user wrote them, spans and sentinels intact.</summary>
    public IReadOnlyList<Operand> Declared => _declared;

    /// <summary>
    /// The operands as they are actually evaluated: spans written out one surface at a time,
    /// sentinels resolved. This is what the residual vector is aligned with.
    /// </summary>
    public IReadOnlyList<Operand> Operands => _expanded;

    public void Add(Operand op)
    {
        if (op == null) throw new ArgumentNullException(nameof(op));
        _declared.Add(op);
        Rebuild();
    }

    public void AddRange(IEnumerable<Operand> ops)
    {
        if (ops == null) throw new ArgumentNullException(nameof(ops));
        _declared.AddRange(ops);
        Rebuild();
    }

    public void Clear()
    {
        _declared.Clear();
        Rebuild();
    }

    /// <summary>Re-expands the operands. Needed after the surface list changes.</summary>
    public void Rebuild()
    {
        var list = new List<Operand>();
        foreach (var op in _declared)
            foreach (var one in op.Expand(_design.System))
                list.Add(one);
        _expanded = list;
    }

    /// <summary>
    /// Take the Jacobian's variable passes one at a time instead of in parallel.
    ///
    /// <para>Set by whatever is running a parallel loop OUTSIDE this one - basin hopping, which
    /// gives every core a chain. Two nested parallel loops do not share a machine, they multiply
    /// against it, and the result is slower than either loop alone. The caller that owns the
    /// outer loop is the one that knows, so it is the one that says.</para>
    /// </summary>
    public bool Sequential { get; set; }

    /// <summary>The merit alone, on a single pass with nothing seeded. This is the cheap call.</summary>
    public MeritResult Evaluate() => Evaluate(false);

    /// <summary>The merit and, when asked, the exact Jacobian.</summary>
    public MeritResult Evaluate(bool withJacobian)
    {
        int m = _expanded.Count;
        int n = _design.Variables.Count;

        if (m == 0)
            return new MeritResult { Jacobian = new double[0, n] };

        if (!withJacobian || n == 0)
        {
            var single = Pass(-1);
            if (single.Failure != null) return MeritResult.Failed(single.Failure);
            return Assemble(single.Values, single.Derivatives, null, n);
        }

        var values = new double[m];
        var jac = new double[m, n];
        string? failure = null;

        // One pass per variable, each an independent walk through the whole chain. They share
        // nothing but the design they read, so they parallelise exactly.
        //
        // UNLESS SOMETHING ABOVE IS ALREADY PARALLEL. Basin hopping runs a chain per core, and
        // each chain calls this; nesting the two multiplies rather than divides - sixteen chains
        // times seventeen variables asks for 272 concurrent passes on a machine with sixteen
        // logical processors. The thread pool then time-slices work that was meant to run
        // straight through, every chain's working set fights for the same cache, and the whole
        // run goes several times slower than the same chains would serially. Whoever owns the
        // outer loop sets Sequential and takes responsibility for keeping the cores busy.
        if (Sequential)
        {
            for (int v = 0; v < n; v++)
            {
                var pass = Pass(v);
                if (pass.Failure != null) { failure ??= pass.Failure; break; }
                for (int i = 0; i < m; i++) jac[i, v] = pass.Derivatives[i];
                if (v == 0) Array.Copy(pass.Values, values, m);
            }
        }
        else
        {
            Parallel.For(0, n, v =>
            {
                var pass = Pass(v);
                if (pass.Failure != null)
                {
                    System.Threading.Interlocked.CompareExchange(ref failure, pass.Failure, null);
                    return;
                }
                for (int i = 0; i < m; i++) jac[i, v] = pass.Derivatives[i];
                // Every pass computes the same values; taking them from the first keeps one
                // definition and costs nothing.
                if (v == 0) Array.Copy(pass.Values, values, m);
            });
        }

        if (failure != null) return MeritResult.Failed(failure);
        return Assemble(values, null, jac, n);
    }

    /// <summary>One dual-number pass: every operand's value, and its derivative on one seed.</summary>
    private (double[] Values, double[] Derivatives, string? Failure) Pass(int seed)
    {
        int m = _expanded.Count;
        var values = new double[m];
        var derivs = new double[m];
        var probe = _design.Probe(seed);

        for (int i = 0; i < m; i++)
        {
            Dual d;
            try
            {
                d = OperandEvaluator.Evaluate(_expanded[i], probe, _context);
            }
            catch (RayFailureException ex)
            {
                return (values, derivs, ex.Message);
            }

            if (double.IsNaN(d.Value) || double.IsInfinity(d.Value))
                return (values, derivs,
                        "operand " + _expanded[i].Label + " is not a finite number on this design.");

            values[i] = d.Value;
            derivs[i] = double.IsNaN(d.Deriv) ? 0.0 : d.Deriv;
        }
        return (values, derivs, null);
    }

    /// <summary>
    /// Turns operand values into residuals, and operand derivatives into residual derivatives.
    ///
    /// <para>The two are not the same. A residual carries the weight, the scaling and - for a
    /// boundary operand - the fact that it is zero while the bound is respected, and its
    /// derivative has to carry all three or the step will not agree with the merit it is
    /// supposed to reduce.</para>
    /// </summary>
    private MeritResult Assemble(double[] values, double[]? derivatives, double[,]? jacobian, int n)
    {
        int m = _expanded.Count;
        var residuals = new double[m];
        var outJac = jacobian != null ? new double[m, n] : new double[0, n];
        double sum = 0.0, weightSum = 0.0;

        for (int i = 0; i < m; i++)
        {
            var op = _expanded[i];
            double w = Math.Max(0.0, op.Weight);
            double rootW = Math.Sqrt(w);
            var (offset, slope, scale) = Shape(op, values[i]);

            residuals[i] = rootW * offset / scale;
            sum += residuals[i] * residuals[i];
            weightSum += w;

            if (jacobian != null)
                for (int v = 0; v < n; v++)
                    outJac[i, v] = rootW * slope * jacobian[i, v] / scale;
            else if (derivatives != null)
            {
                // Single-pass form: there is no Jacobian to fill, but the derivative is kept
                // consistent so that a caller asking for one variable gets the same answer.
                _ = derivatives;
            }
        }

        double merit = weightSum > 0.0 ? Math.Sqrt(sum / weightSum) : Math.Sqrt(sum);
        return new MeritResult
        {
            Values = values,
            Residuals = residuals,
            Jacobian = outJac,
            SumSquares = sum,
            Merit = merit,
        };
    }

    /// <summary>
    /// How far an operand is from where it should be, how that distance moves with the operand,
    /// and what it is measured against.
    ///
    /// <para><paramref name="slope"/> is zero for a boundary operand that is inside its limits,
    /// which is what makes a satisfied constraint free: it contributes nothing to the merit and
    /// nothing to the Jacobian, and stops pulling on the design entirely.</para>
    /// </summary>
    private static (double Offset, double Slope, double Scale) Shape(Operand op, double value)
    {
        if (op.IsBoundary)
        {
            if (op.Min.HasValue && value < op.Min.Value)
                return (value - op.Min.Value, 1.0, Scale(op.Min.Value));
            if (op.Max.HasValue && value > op.Max.Value)
                return (value - op.Max.Value, 1.0, Scale(op.Max.Value));
            return (0.0, 0.0, 1.0);
        }
        // A FOCAL LENGTH IS TARGETED THROUGH ITS POWER. The operand evaluates the power, so the
        // target - which the user wrote as a focal length - is compared against its reciprocal.
        // Near the target this is the same residual as before to first order; at zero power it
        // is exactly -1 instead of infinite, which is the difference between an optimiser that
        // can start from parallel plates and one that refuses to evaluate them.
        if (op.IsPower)
        {
            double wanted = ReciprocalOf(op.Target);
            return (value - wanted, 1.0, Scale(wanted));
        }

        return (value - op.Target, 1.0, Scale(op.Target));
    }

    /// <summary>
    /// The power a focal-length target asks for. A target of zero would mean an afocal system,
    /// which is a power of zero and not a division at all.
    /// </summary>
    private static double ReciprocalOf(double focalLength) =>
        Math.Abs(focalLength) > 1e-12 ? 1.0 / focalLength : 0.0;

    /// <summary>
    /// What a residual is measured against: the target's own magnitude, so that a weight means
    /// relative importance. A target of zero has no magnitude to offer and the residual stays
    /// in the operand's own units.
    /// </summary>
    private static double Scale(double target)
    {
        double a = Math.Abs(target);
        return a > 1e-12 ? a : 1.0;
    }
}
