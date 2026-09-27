using System.Collections.Concurrent;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Variables;
using LensSplitter.Core.Lens;

namespace LensSplitter.Core.Splitting;

/// <summary>How a glass search is run.</summary>
public sealed class GlassSearchOptions
{
    /// <summary>
    /// The glasses to choose from. Null for AberrationCalculator's CoreSet28 working set - a
    /// search free to pick from every vendor's whole catalogue settles on glasses nobody stocks.
    /// </summary>
    public IReadOnlyList<string>? Glasses { get; set; }

    /// <summary>How many of the best-screened pairs are split and optimised in full.</summary>
    public int Refine { get; set; } = 8;

    /// <summary>The split's own settings, used for the pairs refined.</summary>
    public SplitOptions Split { get; set; } = new();

    public IProgress<string>? Progress { get; set; }
}

/// <summary>One pair of glasses and what it gave.</summary>
public sealed class GlassPair
{
    public string A { get; init; } = "";
    public string B { get; init; } = "";

    /// <summary>The merit function at its best unoptimised starting point, which chose the pairs to refine.</summary>
    public double Screen { get; init; }

    /// <summary>The split in full, for a refined pair; null for one only screened.</summary>
    public SplitResult? Result { get; set; }

    /// <summary>The refined split's merit function, without the constraints LensSplitter adds - comparable between pairs.</summary>
    public double Merit { get; set; } = double.PositiveInfinity;
}

/// <summary>
/// Which glasses the two halves of a split should be.
///
/// <list type="number">
/// <item><b>Screening.</b> Every ordered pair of the candidate glasses, the element's own
/// included, is given a small set of starting points - power ratios and bendings, focal length
/// exact - and scored by its best, unoptimised. Pairs are independent, so this runs in parallel.</item>
/// <item><b>Refinement.</b> The best-screened pairs are split and optimised in full, as
/// <see cref="Splitter"/> does, and ranked by the merit function.</item>
/// </list>
///
/// <para>The merit function is the split's: by default the predicted spot and, for a lens with
/// more than one wavelength, its axial and lateral colour - which is what a choice of glass mostly
/// moves, and which the spot alone cannot see.</para>
/// </summary>
public static class GlassSearch
{
    /// <summary>The CoreSet28 glasses, as the vendor catalogs name them.</summary>
    public static IReadOnlyList<string> DefaultGlasses(GlassCatalog catalog)
    {
        var set = SubstitutionCatalog.Load("CoreSet28");
        var names = new List<string>();
        foreach (var c in set.LoadedCatalogs)
            foreach (var g in set.InCatalog(c))
                if (catalog.Find(g.Name) != null && !names.Contains(g.Name, StringComparer.OrdinalIgnoreCase))
                    names.Add(g.Name);
        return names;
    }

    public static List<GlassPair> Search(OpticalSystem system, GlassCatalog catalog, int elementNumber, GlassSearchOptions? options = null)
    {
        options ??= new GlassSearchOptions();
        var original = new LensOptics(system, catalog);
        var elements = LensElements.Find(system, original.PrimaryIndices, original.AbbeNumbers());
        if (elementNumber < 0 || elementNumber >= elements.Count)
            throw new ArgumentOutOfRangeException(nameof(elementNumber), $"The lens has {elements.Count} elements; there is no element {elementNumber + 1}.");
        var e = elements[elementNumber];

        var glasses = (options.Glasses ?? DefaultGlasses(catalog)).ToList();
        if (!string.IsNullOrEmpty(system.Surfaces[e.Front].Material) && !system.Surfaces[e.Front].ModelIndexEnabled
            && !glasses.Contains(system.Surfaces[e.Front].Material!, StringComparer.OrdinalIgnoreCase))
            glasses.Add(system.Surfaces[e.Front].Material!);
        foreach (var g in glasses)
            if (catalog.Find(g) == null) throw new InvalidOperationException($"The glass catalogs do not have {g}.");

        var n = original.PrimaryIndices;
        double power = LensElements.Power(system, e, n);
        double clear = Splitter.ClearSemiDiameter(system, original.Paraxial, e);
        double wave = system.Wavelengths[original.PrimaryWave].Value;
        // Screened, and ranked, by the merit function alone: the focal length is exact at every
        // starting point already, and the constraints' weights would only blur the comparison.
        var merit = (options.Split.Merit ?? SplitMerit.DefaultFor(original)).ToList();
        var noVariables = new VariableSet();

        // 1. Screening, every ordered pair, in parallel.
        var screened = new ConcurrentBag<GlassPair>();
        var pairs = glasses.SelectMany(a => glasses.Select(b => (a, b))).ToList();
        options.Progress?.Report($"screening {pairs.Count} pairs of {glasses.Count} glasses");
        Parallel.ForEach(pairs, pair =>
        {
            var split = new SplitGlasses(pair.a, catalog.Find(pair.a)!.IndexAt(wave), pair.b, catalog.Find(pair.b)!.IndexAt(wave));
            double best = double.PositiveInfinity;
            foreach (double ratio in new[] { 0.35, 0.5, 0.65 })
                foreach (double x1 in new[] { -1.0, 0.0, 1.0, 2.0 })
                    foreach (double x2 in new[] { -1.0, 0.0, 1.0, 2.0 })
                    {
                        var s = SplitGeometry.HoldFocalLength(system, e, n, power, clear,
                            new SplitShape(ratio, x1, x2, 0.5, 0.5 * system.Surfaces[e.Front].Thickness),
                            options.Split.Limits, original.Efl, split);
                        if (s == null) continue;
                        best = Math.Min(best, Evaluate(s.System, catalog, noVariables, merit));
                    }
            if (double.IsFinite(best)) screened.Add(new GlassPair { A = pair.a, B = pair.b, Screen = best });
        });
        var ranked = screened.OrderBy(p => p.Screen).ToList();
        if (ranked.Count == 0) throw new InvalidOperationException("No pair of glasses gives a split that fits.");

        // 2. The best, split and optimised in full.
        foreach (var p in ranked.Take(Math.Max(1, options.Refine)))
        {
            try
            {
                var split = options.Split;
                var opts = new SplitOptions
                {
                    Limits = split.Limits, Ratios = split.Ratios, Shapes = split.Shapes, Gaps = split.Gaps,
                    Extras = split.Extras, Refine = split.Refine, Iterations = split.Iterations, Merit = split.Merit,
                    EflWeight = split.EflWeight, PolishWeight = split.PolishWeight, EdgeWeight = split.EdgeWeight,
                    VaryThickness = split.VaryThickness, Run = split.Run, Glasses = (p.A, p.B),
                };
                p.Result = Splitter.Split(system, catalog, elementNumber, opts);
                p.Merit = Evaluate(p.Result.Split.System, catalog, noVariables, merit);
                options.Progress?.Report($"{p.A} + {p.B}: merit {p.Merit:G4}, predicted spot {p.Result.Split.Prmsa:G4}");
            }
            catch (InvalidOperationException ex)
            {
                options.Progress?.Report($"{p.A} + {p.B}: {ex.Message}");
            }
        }
        return ranked.OrderBy(p => p.Merit).ThenBy(p => p.Screen).ToList();
    }

    /// <summary>A lens scored by a merit function, with nothing variable; infinity when it cannot be.</summary>
    public static double Evaluate(OpticalSystem lens, GlassCatalog catalog, VariableSet variables,
                                  IEnumerable<AberrationCalculator.Optimize.Operands.Operand> operands)
    {
        try
        {
            var m = new MeritFunction(new Design(DesignCopy.Deep(lens), catalog, variables));
            m.AddRange(operands);
            var r = m.Evaluate(false);
            return r.Ok ? r.Merit : double.PositiveInfinity;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException || ex is ArgumentException)
        {
            return double.PositiveInfinity;
        }
    }
}
