using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Optimize;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;
using LensSplitter.Core.Lens;

namespace LensSplitter.Core.Splitting;

/// <summary>How a split is searched for.</summary>
public sealed class SplitOptions
{
    public SplitLimits Limits { get; set; } = new();

    /// <summary>First half's share of the element's power, tried as starting points.</summary>
    public double[] Ratios { get; set; } = { 0.35, 0.5, 0.65 };

    /// <summary>Shape factors X = (c1 + c2)/(c1 - c2) tried for each half.</summary>
    public double[] Shapes { get; set; } = { -1.0, 0.0, 1.0, 2.0 };

    /// <summary>Centre air gaps tried; each is raised to what the edges need.</summary>
    public double[] Gaps { get; set; } = { 0.1, 1.0 };

    /// <summary>
    /// Length the split borrows from the air after the element, as fractions of the element's
    /// thickness: the two halves get thicker, and every later surface stays where it was. A thin
    /// element split with none leaves halves too thin to bend (measured on the Cooke triplet).
    /// </summary>
    public double[] Extras { get; set; } = { 0.0, 0.5, 1.0 };

    /// <summary>How many starting points are optimised: the best for each gap and length, then the best of the rest.</summary>
    public int Refine { get; set; } = 6;

    /// <summary>Least-squares iterations for each, and again for the polish.</summary>
    public int Iterations { get; set; } = 300;

    /// <summary>The merit function; null for <see cref="SplitMerit.DefaultFor"/>.</summary>
    public IReadOnlyList<Operand>? Merit { get; set; }

    /// <summary>
    /// Weight of the focal length and track held while the starting points are optimised.
    ///
    /// <para>Deliberately light. Both are made exact afterwards, and a polish follows with
    /// <see cref="PolishWeight"/>; held hard from the start they make the problem stiff, and with
    /// the thicknesses free the optimiser stalled where it began (measured on the Cooke triplet:
    /// predicted spot 0.0697 at weight 1E6 and 1E4, 0.0131 at 100).</para>
    /// </summary>
    public double EflWeight { get; set; } = 100.0;

    /// <summary>The same weight for the polish, which starts with both already exact.</summary>
    public double PolishWeight { get; set; } = 1e4;

    /// <summary>Weight of the edge limits, which cost nothing while they are met.</summary>
    public double EdgeWeight { get; set; } = 100.0;

    /// <summary>
    /// Let the optimiser change the two halves' centre thicknesses, the gap and the air after the
    /// split as well as the curvatures - their sum held, so every later surface stays where it was.
    ///
    /// <para>Off by default: measured on the Cooke triplet, it converged to a better split at one
    /// setting of the weights and stalled far from any at five others, where the curvatures alone
    /// converged every time. The thicknesses are searched instead, by <see cref="Extras"/> and
    /// <see cref="Gaps"/>.</para>
    /// </summary>
    public bool VaryThickness { get; set; }

    /// <summary>A glass for each half; null keeps the element's.</summary>
    public (string A, string B)? Glasses { get; set; }

    /// <summary>How each optimisation steps: AberrationCalculator's settings for a run.</summary>
    public Func<RunSettings> Run { get; set; } = () => new RunSettings();

    public IProgress<string>? Progress { get; set; }
}

/// <summary>What a split did.</summary>
public sealed class SplitResult
{
    public LensElement Element { get; init; } = null!;
    public LensOptics Original { get; init; } = null!;
    public LensOptics Split { get; init; } = null!;

    /// <summary>The starting point the result was optimised from.</summary>
    public SplitShape Start { get; init; } = null!;

    public int Front { get; init; }
    public double T1 { get; init; }
    public double Gap { get; init; }
    public double T2 { get; init; }

    public int StartingPoints { get; init; }
    public int Optimised { get; init; }
    public double StartMerit { get; init; }
    public double FinalMerit { get; init; }
    public string Method { get; init; } = "";
    public string Stop { get; init; } = "";

    /// <summary>Iterations of the run that produced the result, and of the polish after it (0 when the polish changed nothing).</summary>
    public int Iterations { get; init; }
    public int PolishIterations { get; init; }

    public IReadOnlyList<Operand> Operands { get; init; } = Array.Empty<Operand>();

    /// <summary>The merit function the split was optimised against, as its file would hold it.</summary>
    public IReadOnlyList<Operand> Merit { get; init; } = Array.Empty<Operand>();

    /// <summary>The merit function alone - without the focal length and edges LensSplitter adds - on the original lens and on the split.</summary>
    public double OriginalMerit { get; init; }
    public double SplitMerit { get; init; }

    public List<string> Notes { get; } = new();

    /// <summary>Whether the split made the predicted spot smaller.</summary>
    public bool Improved => Split.Prmsa < Original.Prmsa;
}

/// <summary>
/// Splits one element into two and optimises the pair.
///
/// <list type="number">
/// <item><b>Starting points.</b> For every combination of power ratio, the two halves' shape
/// factors and a gap, <see cref="SplitGeometry"/> builds the four faces, and the two halves' power is
/// scaled until the lens's focal length is exactly the original's. Each is scored by the merit
/// function.</item>
/// <item><b>Optimisation.</b> The best few are handed to AberrationCalculator's optimiser, with the
/// four split faces' curvatures free (bounded so no face is steeper than a hemisphere over the
/// clear aperture), the merit function, the focal length held, and edge limits on the two halves
/// and the air around them. Derivatives are exact, through the predicted spot.</item>
/// <item><b>Finishing.</b> The best result's focal length is made exact by scaling its four
/// faces together, and the image plane moved with the paraxial focus, keeping whatever defocus the
/// original had.</item>
/// </list>
///
/// <para>The conic and aspheric terms of the element's outer faces are kept as they are, and are not
/// variables: the figuring the designer put there stays.</para>
/// </summary>
public static class Splitter
{
    public static SplitResult Split(OpticalSystem system, GlassCatalog catalog, int elementNumber, SplitOptions? options = null)
    {
        options ??= new SplitOptions();
        var original = new LensOptics(system, catalog);
        if (original.Unresolved.Count > 0)
            throw new InvalidOperationException($"The glass catalogs do not have {string.Join(", ", original.Unresolved)}.");

        var elements = LensElements.Find(system, original.PrimaryIndices, original.AbbeNumbers());
        if (elementNumber < 0 || elementNumber >= elements.Count)
            throw new ArgumentOutOfRangeException(nameof(elementNumber), $"The lens has {elements.Count} elements; there is no element {elementNumber + 1}.");
        var e = elements[elementNumber];
        if (e.Cemented) throw new InvalidOperationException($"Element {elementNumber + 1} is part of a {e.Group}, which is corrected as a unit.");

        var n = original.PrimaryIndices;
        double power = LensElements.Power(system, e, n);
        double efl = original.Efl;
        if (!double.IsFinite(efl)) throw new InvalidOperationException("The lens is afocal; there is no focal length to hold.");
        double clear = ClearSemiDiameter(system, original.Paraxial, e);

        SplitGlasses? glasses = null;
        if (options.Glasses is { } g)
        {
            double IndexOf(string name) => (catalog.Find(name) ?? throw new InvalidOperationException($"The glass catalogs do not have {name}."))
                .IndexAt(system.Wavelengths[original.PrimaryWave].Value);
            glasses = new SplitGlasses(g.A, IndexOf(g.A), g.B, IndexOf(g.B));
        }

        var merit = (options.Merit ?? SplitMerit.DefaultFor(original)).ToList();
        // Thicknesses vary only when the air after the element is finite, to take up what they give.
        bool varyThickness = options.VaryThickness && double.IsFinite(system.Surfaces[e.Rear].Thickness);
        double? track = varyThickness ? Track(system) : null;
        var operands = SplitMerit.ForSplit(merit, efl, e.Front, options.Limits, options.EflWeight, options.EdgeWeight, track);
        var variables = Variables(system, e, clear, options.Limits, varyThickness);

        // 1. Starting points, each with the focal length exact, scored by the merit.
        var starts = new List<(SplitLens Lens, double Merit)>();
        int built = 0;
        double elementT = system.Surfaces[e.Front].Thickness;
        foreach (double extra in options.Extras)
            foreach (double ratio in options.Ratios)
                foreach (double x1 in options.Shapes)
                    foreach (double x2 in options.Shapes)
                        foreach (double gap in options.Gaps)
                        {
                            var s = SplitGeometry.HoldFocalLength(system, e, n, power, clear,
                                new SplitShape(ratio, x1, x2, gap, extra * elementT), options.Limits, efl, glasses);
                            if (s == null) continue;
                            built++;
                            double m = Evaluate(s.System, catalog, variables, operands);
                            if (double.IsFinite(m)) starts.Add((s, m));
                        }
        if (starts.Count == 0)
            throw new InvalidOperationException(
                $"No split of element {elementNumber + 1} fits: at the clear aperture ({clear:0.###}) the two halves, "
                + "the gap and the air after them cannot all keep their edges.");
        options.Progress?.Report($"{built} starting points, best merit {starts.Min(x => x.Merit):G4}");

        // 2. The best few, optimised.
        RunOutcome? best = null;
        SplitLens? bestStart = null;
        int tried = 0;
        // The best for each gap and length first - a starting point's merit says little about
        // where its thickness can take it - then the best of the rest.
        var chosen = starts.GroupBy(x => (x.Lens.Shape.Gap, x.Lens.Shape.Extra))
            .Select(g => g.OrderBy(x => x.Merit).First())
            .OrderBy(x => x.Merit)
            .Concat(starts.OrderBy(x => x.Merit))
            .Distinct()
            .Take(Math.Max(1, options.Refine));
        foreach (var (lens, m) in chosen)
        {
            var setup = new OptimizationSetup();
            setup.Variables.AddRange(variables.Items);
            setup.Operands.AddRange(operands);
            var outcome = OptimizationRun.Execute(lens.System, catalog, setup, RunWith(options));
            tried++;
            options.Progress?.Report($"start {tried}: merit {m:G4} -> {outcome.FinalMerit:G4} ({outcome.Stop})");
            if (!outcome.Ok) continue;
            if (best == null || outcome.FinalMerit < best.FinalMerit) { best = outcome; bestStart = lens; }
        }
        if (best == null || bestStart == null)
            throw new InvalidOperationException("The optimiser could not improve any starting point it was given.");

        // 3. Track and focal length made exact, then a polish holding them hard, then exact again;
        //    and the image plane moved with the focus.
        var notes = new List<string>();
        var final = DesignCopy.Deep(best.Best);
        Exact(final, catalog, original, e.Front, efl, track, null);
        var polishOps = SplitMerit.ForSplit(merit, efl, e.Front, options.Limits, options.PolishWeight, options.EdgeWeight, track);
        var polishSetup = new OptimizationSetup();
        polishSetup.Variables.AddRange(variables.Items);
        polishSetup.Operands.AddRange(polishOps);
        var polish = OptimizationRun.Execute(final, catalog, polishSetup, RunWith(options));
        options.Progress?.Report($"polish: merit {polish.InitialMerit:G4} -> {polish.FinalMerit:G4} ({polish.Stop})");
        int polishIterations = 0;
        if (polish.Ok && polish.FinalMerit < polish.InitialMerit)
        {
            final = DesignCopy.Deep(polish.Best);
            polishIterations = polish.Iterations;
        }
        Exact(final, catalog, original, e.Front, efl, track, notes);
        KeepDefocus(system, original, final, catalog, notes);

        var result = new SplitResult
        {
            Element = e,
            Original = original,
            Split = new LensOptics(final, catalog),
            Start = bestStart.Shape,
            Front = e.Front,
            T1 = final.Surfaces[e.Front].Thickness,
            Gap = final.Surfaces[e.Front + 1].Thickness,
            T2 = final.Surfaces[e.Front + 2].Thickness,
            StartingPoints = built,
            Optimised = tried,
            StartMerit = starts.Min(x => x.Merit),
            FinalMerit = best.FinalMerit,
            Method = best.Method,
            Stop = best.Stop,
            Iterations = best.Iterations,
            PolishIterations = polishIterations,
            Operands = best.Operands,
            Merit = merit,
            OriginalMerit = GlassSearch.Evaluate(system, catalog, new VariableSet(), merit),
            SplitMerit = GlassSearch.Evaluate(final, catalog, new VariableSet(), merit),
        };
        result.Notes.AddRange(notes);
        return result;
    }

    /// <summary>
    /// The clear semi-diameter the split faces must pass: the larger of the element's declared
    /// apertures and its paraxial beam, |y| + |ybar| at the full field, with a twentieth to spare.
    /// </summary>
    public static double ClearSemiDiameter(OpticalSystem system, ParaxialResult p, LensElement e)
    {
        double beam = 0.0;
        foreach (int i in new[] { e.Front, e.Rear })
            beam = Math.Max(beam, Math.Abs(p.Y[i]) + Math.Abs(p.Ybar[i]));
        double declared = Math.Max(system.Surfaces[e.Front].SemiDiameter, system.Surfaces[e.Rear].SemiDiameter);
        return Math.Max(declared, 1.05 * beam);
    }

    // The track exact by the air after the split, so every later surface is where it was; then the
    // focal length exact by the four faces scaled together.
    private static void Exact(OpticalSystem lens, GlassCatalog catalog, LensOptics original, int front, double efl,
                              double? track, List<string>? notes)
    {
        if (track is double ttl)
            lens.Surfaces[front + 3].Thickness -= Track(lens) - ttl;
        MakeFocalLengthExact(lens, catalog, original, front, efl, notes ?? new List<string>());
    }

    private static RunSettings RunWith(SplitOptions options)
    {
        var s = options.Run();
        s.Iterations = options.Iterations;
        return s;
    }

    /// <summary>First surface to image, as AberrationCalculator's TTL operand measures it.</summary>
    public static double Track(OpticalSystem lens)
    {
        double sum = 0.0;
        for (int i = 1; i <= lens.LastOpticalSurface(); i++) sum += lens.Surfaces[i].Thickness;
        return sum;
    }

    // The four split faces' curvatures, none steeper than a hemisphere over the clear aperture;
    // and, when asked, the two centre thicknesses, the gap and the air after the split - the glass
    // at least its least centre thickness, the gap and the air their least gap, and none more than
    // the whole space the element and the air after it had.
    private static VariableSet Variables(OpticalSystem original, LensElement e, double clear, SplitLimits limits, bool thickness)
    {
        var set = new VariableSet();
        double cMax = 1.0 / clear;
        int front = e.Front;
        for (int i = front; i <= front + 3; i++)
            set.Add(new Variable { Kind = VariableKind.Curvature, Surface = i, Min = -cMax, Max = cMax });
        if (thickness)
        {
            double room = original.Surfaces[e.Front].Thickness + original.Surfaces[e.Rear].Thickness;
            set.Add(new Variable { Kind = VariableKind.Thickness, Surface = front, Min = limits.MinCentreThickness, Max = room });
            set.Add(new Variable { Kind = VariableKind.Thickness, Surface = front + 1, Min = limits.MinAirGap, Max = room });
            set.Add(new Variable { Kind = VariableKind.Thickness, Surface = front + 2, Min = limits.MinCentreThickness, Max = room });
            set.Add(new Variable { Kind = VariableKind.Thickness, Surface = front + 3, Min = limits.MinAirGap, Max = room });
        }
        return set;
    }

    private static double Evaluate(OpticalSystem lens, GlassCatalog catalog, VariableSet variables, IEnumerable<Operand> operands)
    {
        try
        {
            var merit = new MeritFunction(new Design(DesignCopy.Deep(lens), catalog, variables));
            merit.AddRange(operands);
            var r = merit.Evaluate(false);
            return r.Ok ? r.Merit : double.PositiveInfinity;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException || ex is ArgumentException)
        {
            return double.PositiveInfinity;
        }
    }

    /// <summary>
    /// Scales the four split faces' vertex curvatures together until the focal length is exactly
    /// the original's. The optimiser holds it only as closely as its weight asks.
    /// </summary>
    private static void MakeFocalLengthExact(OpticalSystem lens, GlassCatalog catalog, LensOptics original, int front,
                                             double target, List<string> notes)
    {
        double wave = lens.Wavelengths[original.PrimaryWave].Value;
        var n = IndexResolver.Build(lens, catalog, wave);
        var vertex = new double[4];
        for (int k = 0; k < 4; k++) vertex[k] = lens.Surfaces[front + k].VertexCurvature;

        double Efl(double s)
        {
            for (int k = 0; k < 4; k++)
            {
                var surf = lens.Surfaces[front + k];
                double r2 = surf.AsphericCoefficients.Length > 0 ? surf.AsphericCoefficients[0] : 0.0;
                surf.Curvature = s * vertex[k] - 2.0 * r2;
            }
            return ParaxialTrace.Trace(lens, n, 0.0).Efl - target;
        }

        double before = Efl(1.0) + target;
        double x0 = 1.0, f0 = before - target, x1 = 1.0 + 1e-6, f1 = Efl(x1);
        for (int i = 0; i < 30 && Math.Abs(f1) > 1e-13 * Math.Abs(target) && Math.Abs(f1 - f0) > 1e-300; i++)
        {
            double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
            (x0, f0, x1, f1) = (x1, f1, x2, Efl(x2));
        }
        if (Math.Abs(f1) > 1e-9 * Math.Abs(target))
        {
            Efl(1.0);
            notes.Add(FormattableString.Invariant($"The focal length could not be made exact: {before:0.######} against {target:0.######}."));
        }
        else if (Math.Abs(before - target) > 1e-9 * Math.Abs(target))
            notes.Add(FormattableString.Invariant(
                $"The optimiser left the focal length at {before:0.########}; the four split faces were scaled by {x1:0.########} to make it {target:0.########}."));
    }

    /// <summary>
    /// Moves the image plane with the paraxial focus, keeping the defocus the original lens had -
    /// zero for a lens imaged at its paraxial focus.
    /// </summary>
    private static void KeepDefocus(OpticalSystem original, LensOptics originalOptics, OpticalSystem split, GlassCatalog catalog,
                                    List<string> notes)
    {
        int lastO = original.LastOpticalSurface(), lastS = split.LastOpticalSurface();
        double focusO = originalOptics.Paraxial.ParaxialFocusDistance;
        double tO = original.Surfaces[lastO].Thickness;
        if (!double.IsFinite(focusO) || !double.IsFinite(tO)) return;
        var n = IndexResolver.Build(split, catalog, split.Wavelengths[originalOptics.PrimaryWave].Value);
        double focusS = ParaxialTrace.Trace(split, n, 0.0).ParaxialFocusDistance;
        if (!double.IsFinite(focusS)) return;
        double t = focusS + (tO - focusO);
        if (Math.Abs(t - split.Surfaces[lastS].Thickness) > 1e-9)
            notes.Add(FormattableString.Invariant(
                $"The image plane moved {t - split.Surfaces[lastS].Thickness:+0.######;-0.######} with the paraxial focus."));
        split.Surfaces[lastS].Thickness = t;
    }
}
