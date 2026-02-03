using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Optimizes glass selection for split lens elements using the full iterative optimizing splitter.
/// For each glass combination, performs proper shape factor optimization with geometry enforcement.
/// </summary>
public class GlassOptimizer
{
    private readonly ParaxialRayTracer _tracer = new();
    private readonly ChromaticAberrationCalculator _chromaCalc = new();
    private readonly SeidelCalculator _seidelCalc = new();
    private readonly OptimizingSplitter _splitter = new();
    private Random _random = new();

    /// <summary>
    /// Settings for glass optimization.
    /// </summary>
    public class OptimizationSettings
    {
        /// <summary>
        /// Number of random glass combinations to try.
        /// </summary>
        public int NumberOfTrials { get; set; } = 1000;

        /// <summary>
        /// Number of top results to keep.
        /// </summary>
        public int TopResultsToKeep { get; set; } = 100;

        /// <summary>
        /// Aberration weights for merit function (same as split optimizer).
        /// </summary>
        public AberrationWeights AberrationWeights { get; set; } = AberrationWeights.Default;

        /// <summary>
        /// Field angle for lateral color evaluation (degrees).
        /// </summary>
        public double FieldAngleForLateralColor { get; set; } = 5.0;

        /// <summary>
        /// Minimum air gap between elements (mm).
        /// </summary>
        public double MinAirGap { get; set; } = 0.5;

        /// <summary>
        /// Maximum air gap between elements (mm).
        /// </summary>
        public double MaxAirGap { get; set; } = 3.0;

        /// <summary>
        /// Whether to try the achromatic power ratio for each glass pair.
        /// </summary>
        public bool UseAchromaticRatio { get; set; } = true;

        /// <summary>
        /// Whether to also search around the achromatic ratio.
        /// </summary>
        public bool SearchAroundAchromatic { get; set; } = true;

        /// <summary>
        /// Random seed for reproducibility (null for random).
        /// </summary>
        public int? RandomSeed { get; set; }

        /// <summary>
        /// Minimum edge clearance to prevent lens collision (mm).
        /// </summary>
        public double MinEdgeClearance { get; set; } = 0.5;

        /// <summary>
        /// Whether to enforce geometry constraints (may affect total track length).
        /// </summary>
        public bool EnforceGeometryConstraints { get; set; } = true;

        /// <summary>
        /// Whether to use full iterative optimization for each glass combination.
        /// When true, uses OptimizingSplitter for proper shape factor optimization.
        /// When false, uses simplified thin-lens calculations (faster but less accurate).
        /// </summary>
        public bool UseFullOptimization { get; set; } = true;

        /// <summary>
        /// Only include results that improve the merit function (MF improvement > 1.0).
        /// </summary>
        public bool OnlyShowImprovements { get; set; } = true;

        /// <summary>
        /// Maximum allowed EFL deviation from original (as fraction, e.g., 0.02 = 2%).
        /// Set to 0 to disable EFL correction entirely (fastest, best aberrations, but EFL may change).
        /// Set to a small value like 0.005 (0.5%) for strict EFL matching.
        /// Default is 0 (no EFL constraint) to prioritize aberration performance.
        /// </summary>
        public double MaxEflDeviation { get; set; } = 0;

        /// <summary>
        /// Whether to attempt to correct EFL back to original by scaling radii.
        /// This may degrade aberration performance. Only applies if MaxEflDeviation > 0.
        /// </summary>
        public bool CorrectEfl { get; set; } = false;
    }

    /// <summary>
    /// Optimizes glass selection for splitting an element.
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <param name="elementIndex">Index of element to split.</param>
    /// <param name="availableGlasses">List of glasses to choose from.</param>
    /// <param name="settings">Optimization settings.</param>
    /// <returns>Optimization results sorted by merit.</returns>
    public GlassOptimizationResult Optimize(
        OpticalSystem system,
        int elementIndex,
        IList<Glass> availableGlasses,
        OptimizationSettings? settings = null)
    {
        settings ??= new OptimizationSettings();

        if (settings.RandomSeed.HasValue)
        {
            _random = new Random(settings.RandomSeed.Value);
        }

        var elements = system.GetLensElements();
        if (elementIndex < 0 || elementIndex >= elements.Count)
        {
            throw new ArgumentException($"Element index {elementIndex} out of range.");
        }

        var originalElement = elements[elementIndex];
        var wavelength = system.PrimaryWavelength.ValueMicrons;
        double originalPower = originalElement.GetThickLensPower(wavelength);
        double entrancePupilRadius = system.EntrancePupilDiameter / 2.0;

        // Calculate original system aberrations for comparison
        var originalChromatic = _chromaCalc.Calculate(system, settings.FieldAngleForLateralColor);
        double originalS1 = CalculateElementS1(originalElement, entrancePupilRadius, wavelength);

        // Calculate full Seidel aberrations for original system
        SeidelResult? originalSeidel = null;
        double originalMeritFunction = double.MaxValue;
        try
        {
            originalSeidel = _seidelCalc.Calculate(system, wavelength, settings.FieldAngleForLateralColor);
            originalMeritFunction = SeidelCalculator.CalculateMeritFunction(originalSeidel, settings.AberrationWeights);
        }
        catch
        {
            // Fall back to thin-lens S1 if ray trace fails
        }

        // Calculate original system EFL
        double originalEfl = _tracer.CalculateEfl(system, wavelength);

        // Try splitting with the original glass to get baseline improvement from splitting alone
        GlassCombinationResult? originalGlassSplitResult = null;
        var originalGlass = originalElement.Glass;
        if (originalGlass != null)
        {
            originalGlassSplitResult = EvaluateWithFullOptimization(
                system, elementIndex, originalElement,
                originalGlass, originalGlass,
                wavelength, settings,
                originalEfl);  // Pass true original EFL

            if (originalGlassSplitResult != null)
            {
                originalGlassSplitResult.Rank = 0; // Special rank for original glass
                // Calculate MeritScore for the original glass split
                originalGlassSplitResult.MeritScore = CalculateMerit(
                    originalGlassSplitResult, originalS1, originalChromatic,
                    originalSeidel, originalMeritFunction, originalEfl, settings);
            }
        }

        var candidates = new List<GlassCombinationResult>();
        var triedCombinations = new HashSet<string>();

        // Use full optimization with OptimizingSplitter for each glass combination
        if (settings.UseFullOptimization)
        {
            // Try all unique glass pairs systematically
            for (int i = 0; i < availableGlasses.Count; i++)
            {
                for (int j = 0; j < availableGlasses.Count; j++)
                {
                    var glass1 = availableGlasses[i];
                    var glass2 = availableGlasses[j];

                    string key = $"{glass1.Name}|{glass2.Name}";
                    if (triedCombinations.Contains(key))
                    {
                        continue;
                    }
                    triedCombinations.Add(key);

                    try
                    {
                        var result = EvaluateWithFullOptimization(
                            system, elementIndex, originalElement,
                            glass1, glass2, wavelength, settings,
                            originalEfl);  // Pass true original EFL

                        if (result != null)
                        {
                            candidates.Add(result);
                        }
                    }
                    catch
                    {
                        // Skip invalid glass combinations
                    }
                }
            }
        }
        else
        {
            // Use simplified thin-lens calculations (faster but less accurate)
            // Generate and evaluate random combinations
            for (int trial = 0; trial < settings.NumberOfTrials; trial++)
            {
                // Select random glasses
                var glass1 = availableGlasses[_random.Next(availableGlasses.Count)];
                var glass2 = availableGlasses[_random.Next(availableGlasses.Count)];

                // Create combination key to avoid duplicates
                string key = $"{glass1.Name}|{glass2.Name}";
                if (triedCombinations.Contains(key))
                {
                    continue;
                }
                triedCombinations.Add(key);

                // Evaluate this glass combination
                var results = EvaluateGlassCombination(
                    system, elementIndex, originalElement, originalPower,
                    glass1, glass2, entrancePupilRadius, wavelength, settings);

                candidates.AddRange(results);
            }

            // Also try all unique pairs systematically if we have few glasses
            if (availableGlasses.Count <= 20)
            {
                for (int i = 0; i < availableGlasses.Count; i++)
                {
                    for (int j = 0; j < availableGlasses.Count; j++)
                    {
                        var glass1 = availableGlasses[i];
                        var glass2 = availableGlasses[j];

                        string key = $"{glass1.Name}|{glass2.Name}";
                        if (triedCombinations.Contains(key))
                        {
                            continue;
                        }
                        triedCombinations.Add(key);

                        var results = EvaluateGlassCombination(
                            system, elementIndex, originalElement, originalPower,
                            glass1, glass2, entrancePupilRadius, wavelength, settings);

                        candidates.AddRange(results);
                    }
                }
            }
        }

        // Calculate merit scores and sort
        foreach (var candidate in candidates)
        {
            candidate.MeritScore = CalculateMerit(candidate, originalS1, originalChromatic, originalSeidel, originalMeritFunction, originalEfl, settings);
        }

        // Filter out results that made things worse (if requested)
        var filteredCandidates = settings.OnlyShowImprovements
            ? candidates.Where(c => c.MeritScore > 1.0).ToList()
            : candidates;

        var sortedCandidates = filteredCandidates
            .OrderByDescending(c => c.MeritScore)
            .Take(settings.TopResultsToKeep)
            .ToList();

        // Assign ranks
        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            sortedCandidates[i].Rank = i + 1;
        }

        return new GlassOptimizationResult
        {
            OriginalSystem = system,
            ElementIndex = elementIndex,
            OriginalElement = originalElement,
            OriginalPower = originalPower,
            OriginalS1 = originalS1,
            OriginalChromaticAberration = originalChromatic,
            OriginalSeidel = originalSeidel,
            OriginalMeritFunction = originalMeritFunction,
            OriginalEfl = originalEfl,
            OriginalGlassSplitResult = originalGlassSplitResult,
            AvailableGlasses = availableGlasses.ToList(),
            TotalCombinationsTried = triedCombinations.Count,
            TopResults = sortedCandidates,
            Settings = settings
        };
    }

    /// <summary>
    /// Evaluates a glass combination using the full OptimizingSplitter.
    /// This provides proper shape factor optimization and geometry enforcement.
    /// The glasses are substituted BEFORE optimization so shape factors are correct for each glass.
    /// </summary>
    /// <param name="trueOriginalEfl">The EFL of the original system before any modifications.</param>
    private GlassCombinationResult? EvaluateWithFullOptimization(
        OpticalSystem system,
        int elementIndex,
        LensElement originalElement,
        Glass glass1,
        Glass glass2,
        double wavelength,
        OptimizationSettings settings,
        double trueOriginalEfl)
    {
        // Create a modified system with the original element's glass temporarily replaced
        // We use glass1 for the element so the splitter optimizes for that glass
        var modifiedSystem = system.Clone();
        var modifiedElements = modifiedSystem.GetLensElements();

        if (elementIndex >= modifiedElements.Count)
            return null;

        // Substitute the glass in the element to be split
        // The splitter will then use this glass for both elements initially
        var elemToModify = modifiedElements[elementIndex];
        elemToModify.FrontSurface.Glass = glass1;
        elemToModify.FrontSurface.GlassName = glass1.Name;

        // Set up splitter settings
        var splitterSettings = new OptimizingSplitter.OptimizationSettings
        {
            MinPowerRatio = 0.3,
            MaxPowerRatio = 0.7,
            OptimizeAirGap = true,
            UseFullSeidelOptimization = true,
            MinEdgeClearance = settings.MinEdgeClearance,
            EnforceGeometryConstraints = settings.EnforceGeometryConstraints,
            AberrationWeights = settings.AberrationWeights
        };

        // Perform the split with full optimization (uses glass1 for both elements)
        IterativeSplitResult splitResult;
        try
        {
            splitResult = _splitter.SplitWithOptimization(modifiedSystem, elementIndex, splitterSettings);
        }
        catch
        {
            return null;
        }

        // Use the true original EFL (before any glass substitution)
        double originalEfl = trueOriginalEfl;

        // Now substitute glass2 into the second element
        var splitSystem = splitResult.SplitSystem.Clone();
        var splitElements = splitSystem.GetLensElements();

        // Find the two new elements
        int newElement1Index = elementIndex;
        int newElement2Index = elementIndex + 1;

        if (newElement2Index < splitElements.Count)
        {
            var elem2 = splitElements[newElement2Index];
            elem2.FrontSurface.Glass = glass2;
            elem2.FrontSurface.GlassName = glass2.Name;

            // For different glasses, we need to recalculate the radii to maintain power
            // using the new refractive index
            double n2_new = glass2.GetRefractiveIndex(wavelength);
            double n2_old = glass1.GetRefractiveIndex(wavelength);

            // Adjust radii to maintain the same power with new glass
            // Power = (n-1) * (1/R1 - 1/R2) for thin lens
            // If we change n, we need to scale the curvatures
            if (Math.Abs(n2_new - n2_old) > 0.001)
            {
                double scaleFactor = (n2_old - 1.0) / (n2_new - 1.0);
                if (!double.IsInfinity(elem2.FrontSurface.Radius) && Math.Abs(elem2.FrontSurface.Radius) > 1e-6)
                {
                    elem2.FrontSurface.Radius *= scaleFactor;
                }
                if (!double.IsInfinity(elem2.RearSurface.Radius) && Math.Abs(elem2.RearSurface.Radius) > 1e-6)
                {
                    elem2.RearSurface.Radius *= scaleFactor;
                }
            }
        }

        // Calculate the system EFL after glass substitution
        double newEfl = _tracer.CalculateEfl(splitSystem, wavelength);
        double eflError = Math.Abs(newEfl - originalEfl) / Math.Abs(originalEfl);

        // Optionally correct EFL by scaling radii (may degrade aberrations)
        if (settings.CorrectEfl && settings.MaxEflDeviation > 0 && eflError > settings.MaxEflDeviation * 0.1)
        {
            const int maxIterations = 50;
            double convergenceTolerance = settings.MaxEflDeviation * 0.1; // 10% of max deviation

            for (int iter = 0; iter < maxIterations && eflError > convergenceTolerance; iter++)
            {
                double eflRatio = originalEfl / newEfl;
                double dampingFactor = eflError > 0.1 ? 0.5 : 0.8;
                double dampedRatio = 1.0 + dampingFactor * (eflRatio - 1.0);

                if (newElement1Index < splitElements.Count)
                {
                    var elem1 = splitElements[newElement1Index];
                    ScaleRadius(elem1.FrontSurface, dampedRatio);
                    ScaleRadius(elem1.RearSurface, dampedRatio);
                }
                if (newElement2Index < splitElements.Count)
                {
                    var elem2 = splitElements[newElement2Index];
                    ScaleRadius(elem2.FrontSurface, dampedRatio);
                    ScaleRadius(elem2.RearSurface, dampedRatio);
                }

                newEfl = _tracer.CalculateEfl(splitSystem, wavelength);
                eflError = Math.Abs(newEfl - originalEfl) / Math.Abs(originalEfl);
            }
        }

        // Reject if EFL deviation exceeds maximum allowed (only if constraint is enabled)
        if (settings.MaxEflDeviation > 0 && eflError > settings.MaxEflDeviation)
        {
            return null;
        }

        // Recalculate aberrations with the new glasses
        ChromaticAberrationResult? chromaticResult = null;
        SeidelResult? seidelResult = null;
        try
        {
            chromaticResult = _chromaCalc.Calculate(splitSystem, settings.FieldAngleForLateralColor);
            seidelResult = _seidelCalc.Calculate(splitSystem, wavelength, settings.FieldAngleForLateralColor);
        }
        catch
        {
            // Skip if calculation fails
            return null;
        }

        double n1 = glass1.GetRefractiveIndex(wavelength);
        double n2 = glass2.GetRefractiveIndex(wavelength);

        return new GlassCombinationResult
        {
            Glass1 = glass1,
            Glass2 = glass2,
            Glass1Name = glass1.Name,
            Glass2Name = glass2.Name,
            N1 = n1,
            N2 = n2,
            V1 = glass1.Vd,
            V2 = glass2.Vd,
            PowerRatio = splitResult.OptimalPowerRatio,
            AirGap = splitResult.ActualAirGap,
            Phi1 = splitResult.OptimizedResult.Phi1,
            Phi2 = splitResult.OptimizedResult.Phi2,
            TotalS1 = seidelResult?.S1 ?? splitResult.OptimizedResult.S1_Total,
            S1_Element1 = splitResult.OptimizedResult.S1_Element1,
            S1_Element2 = splitResult.OptimizedResult.S1_Element2,
            LongitudinalColor = chromaticResult?.LongitudinalColor ?? 0,
            LateralColor = chromaticResult?.LateralColor ?? 0,
            EstimatedLongitudinalColor = 0,
            ChromaticResult = chromaticResult,
            SeidelResult = seidelResult,
            SplitMeritFunction = seidelResult != null
                ? SeidelCalculator.CalculateMeritFunction(seidelResult, settings.AberrationWeights)
                : double.MaxValue,
            SplitSystem = splitSystem,
            EffectivePower = splitResult.OptimizedResult.Phi1 + splitResult.OptimizedResult.Phi2,
            Efl = newEfl,  // Use the verified EFL after glass substitution
            OriginalEfl = originalEfl
        };
    }

    private List<GlassCombinationResult> EvaluateGlassCombination(
        OpticalSystem system,
        int elementIndex,
        LensElement originalElement,
        double originalPower,
        Glass glass1,
        Glass glass2,
        double entrancePupilRadius,
        double wavelength,
        OptimizationSettings settings)
    {
        var results = new List<GlassCombinationResult>();

        double n1 = glass1.GetRefractiveIndex(wavelength);
        double n2 = glass2.GetRefractiveIndex(wavelength);
        double V1 = glass1.Vd;
        double V2 = glass2.Vd;

        // Determine power ratios to try
        var powerRatios = new List<double>();

        if (settings.UseAchromaticRatio && Math.Abs(V1 - V2) > 1)
        {
            double achromaticRatio = ChromaticAberrationCalculator.CalculateAchromaticPowerRatio(
                originalPower, V1, V2);

            if (achromaticRatio > 0.1 && achromaticRatio < 0.9)
            {
                powerRatios.Add(achromaticRatio);

                if (settings.SearchAroundAchromatic)
                {
                    powerRatios.Add(achromaticRatio - 0.1);
                    powerRatios.Add(achromaticRatio + 0.1);
                }
            }
        }

        // Also try some standard ratios
        powerRatios.Add(0.5);
        powerRatios.Add(0.4);
        powerRatios.Add(0.6);

        // Try different air gaps
        var airGaps = new[] { settings.MinAirGap, (settings.MinAirGap + settings.MaxAirGap) / 2, settings.MaxAirGap };

        foreach (double ratio in powerRatios.Distinct())
        {
            if (ratio < 0.1 || ratio > 0.9) continue;

            foreach (double airGap in airGaps)
            {
                try
                {
                    var result = EvaluateSingleConfiguration(
                        system, elementIndex, originalElement, originalPower,
                        glass1, glass2, n1, n2, V1, V2,
                        ratio, airGap, entrancePupilRadius, wavelength, settings);

                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch
                {
                    // Skip invalid configurations
                }
            }
        }

        return results;
    }

    private GlassCombinationResult? EvaluateSingleConfiguration(
        OpticalSystem system,
        int elementIndex,
        LensElement originalElement,
        double originalPower,
        Glass glass1,
        Glass glass2,
        double n1,
        double n2,
        double V1,
        double V2,
        double powerRatio,
        double airGap,
        double entrancePupilRadius,
        double wavelength,
        OptimizationSettings settings)
    {
        double phi1 = originalPower * powerRatio;
        double phi2 = originalPower * (1.0 - powerRatio);

        // Calculate S1 for this configuration
        double Y1 = -1.0; // Object at infinity
        double X1_opt = AberrationCalculator.CalculateOptimalShapeFactor(n1, Y1);
        double y1 = entrancePupilRadius;
        double S1_1 = AberrationCalculator.CalculateS1ThinLens(y1, phi1, n1, X1_opt, Y1);

        double y2 = AberrationCalculator.CalculateRayHeightAtSecondLens(y1, phi1, airGap);
        double Y2 = AberrationCalculator.CalculateSecondLensPositionFactor(phi1, phi2, airGap);
        double X2_opt = AberrationCalculator.CalculateOptimalShapeFactor(n2, Y2);
        double S1_2 = AberrationCalculator.CalculateS1ThinLens(y2, phi2, n2, X2_opt, Y2);

        double totalS1 = S1_1 + S1_2;

        // Estimate chromatic aberration
        double effectivePower = phi1 + phi2 - airGap * phi1 * phi2;
        double efl = Math.Abs(effectivePower) > 1e-10 ? 1.0 / effectivePower : 1000;
        double estimatedLongColor = ChromaticAberrationCalculator.EstimateLongitudinalColor(
            phi1, phi2, V1, V2, efl);

        // Build the split system for accurate chromatic calculation
        OpticalSystem? splitSystem = null;
        ChromaticAberrationResult? chromaticResult = null;
        SeidelResult? seidelResult = null;

        try
        {
            splitSystem = BuildSplitSystem(system, elementIndex, originalElement,
                glass1, glass2, phi1, phi2, n1, n2, X1_opt, X2_opt, airGap, wavelength);
        }
        catch
        {
            // BuildSplitSystem failed - create a minimal placeholder system
            splitSystem = system.Clone();
        }

        try
        {
            chromaticResult = _chromaCalc.Calculate(splitSystem, settings.FieldAngleForLateralColor);
            seidelResult = _seidelCalc.Calculate(splitSystem, wavelength, settings.FieldAngleForLateralColor);
        }
        catch
        {
            // Use estimated value if ray trace fails
        }

        return new GlassCombinationResult
        {
            Glass1 = glass1,
            Glass2 = glass2,
            Glass1Name = glass1.Name,
            Glass2Name = glass2.Name,
            N1 = n1,
            N2 = n2,
            V1 = V1,
            V2 = V2,
            PowerRatio = powerRatio,
            AirGap = airGap,
            Phi1 = phi1,
            Phi2 = phi2,
            TotalS1 = totalS1,
            S1_Element1 = S1_1,
            S1_Element2 = S1_2,
            LongitudinalColor = chromaticResult?.LongitudinalColor ?? estimatedLongColor,
            LateralColor = chromaticResult?.LateralColor ?? 0,
            EstimatedLongitudinalColor = estimatedLongColor,
            ChromaticResult = chromaticResult,
            SeidelResult = seidelResult,
            SplitSystem = splitSystem,
            EffectivePower = effectivePower,
            Efl = efl
        };
    }

    private OpticalSystem BuildSplitSystem(
        OpticalSystem originalSystem,
        int elementIndex,
        LensElement originalElement,
        Glass glass1,
        Glass glass2,
        double phi1,
        double phi2,
        double n1,
        double n2,
        double X1,
        double X2,
        double airGap,
        double wavelength)
    {
        // Solve for radii from power and shape
        var (r1_first, r2_first) = SolveRadiiForPowerAndShape(phi1, X1, n1);
        var (r1_second, r2_second) = SolveRadiiForPowerAndShape(phi2, X2, n2);

        var splitSystem = originalSystem.Clone();

        double originalThickness = originalElement.CenterThickness;
        double newThickness = Math.Max(0.5, (originalThickness - airGap) / 2.0);

        int frontSurfaceIndex = originalElement.FrontSurface.Index;
        int rearSurfaceIndex = originalElement.RearSurface.Index;

        var surface1 = new Surface
        {
            Index = frontSurfaceIndex,
            Radius = r1_first,
            Thickness = newThickness,
            Glass = glass1,
            GlassName = glass1.Name,
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface2 = new Surface
        {
            Index = frontSurfaceIndex + 1,
            Radius = r2_first,
            Thickness = airGap,
            Glass = Glass.Air,
            GlassName = "AIR",
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface3 = new Surface
        {
            Index = frontSurfaceIndex + 2,
            Radius = r1_second,
            Thickness = newThickness,
            Glass = glass2,
            GlassName = glass2.Name,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface4 = new Surface
        {
            Index = frontSurfaceIndex + 3,
            Radius = r2_second,
            Thickness = originalElement.RearSurface.Thickness,
            Glass = originalElement.RearSurface.Glass,
            GlassName = originalElement.RearSurface.GlassName,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard,
            IsStop = originalElement.RearSurface.IsStop
        };

        splitSystem.Surfaces.RemoveAt(rearSurfaceIndex);
        splitSystem.Surfaces.RemoveAt(frontSurfaceIndex);

        splitSystem.Surfaces.Insert(frontSurfaceIndex, surface1);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 1, surface2);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 2, surface3);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 3, surface4);

        splitSystem.ReindexSurfaces();

        return splitSystem;
    }

    private (double R1, double R2) SolveRadiiForPowerAndShape(double power, double shapeFactor, double n)
    {
        double factor = power / (2.0 * (n - 1.0));
        double c1 = (shapeFactor + 1.0) * factor;
        double c2 = (shapeFactor - 1.0) * factor;

        double r1 = Math.Abs(c1) > 1e-10 ? 1.0 / c1 : double.PositiveInfinity;
        double r2 = Math.Abs(c2) > 1e-10 ? 1.0 / c2 : double.PositiveInfinity;

        return (r1, r2);
    }

    private static void ScaleRadius(Surface surface, double scaleFactor)
    {
        if (!double.IsInfinity(surface.Radius) && Math.Abs(surface.Radius) > 1e-6)
        {
            surface.Radius *= scaleFactor;
        }
    }

    private double CalculateElementS1(LensElement element, double entrancePupilRadius, double wavelength)
    {
        double n = element.Glass.GetRefractiveIndex(wavelength);
        double power = element.GetThickLensPower(wavelength);
        double Y = -1.0;
        double X = AberrationCalculator.CalculateOptimalShapeFactor(n, Y);

        return AberrationCalculator.CalculateS1ThinLens(entrancePupilRadius, power, n, X, Y);
    }

    private double CalculateMerit(
        GlassCombinationResult candidate,
        double originalS1,
        ChromaticAberrationResult originalChromatic,
        SeidelResult? originalSeidel,
        double originalMeritFunction,
        double originalEfl,
        OptimizationSettings settings)
    {
        // Calculate EFL deviation penalty (applied to all merit calculations)
        double eflDeviation = Math.Abs(candidate.OriginalEfl) > 1e-6
            ? Math.Abs(candidate.Efl - candidate.OriginalEfl) / Math.Abs(candidate.OriginalEfl)
            : 0;

        // EFL penalty: 1.0 at 0% deviation, decreasing as deviation increases
        // At max tolerance, penalty is ~0.5, beyond that approaches 0
        double eflPenalty = 1.0;
        if (settings.MaxEflDeviation > 0 && eflDeviation > 0)
        {
            // Quadratic penalty: more aggressive for larger deviations
            double normalizedDeviation = eflDeviation / settings.MaxEflDeviation;
            eflPenalty = Math.Max(0, 1.0 - normalizedDeviation * normalizedDeviation);
        }

        // If we have full Seidel results for both, use weighted merit function
        if (candidate.SeidelResult != null && originalSeidel != null && originalMeritFunction < double.MaxValue)
        {
            double candidateMeritFunction = SeidelCalculator.CalculateMeritFunction(candidate.SeidelResult, settings.AberrationWeights);
            candidate.SplitMeritFunction = candidateMeritFunction;

            // Merit is how much better the split is (higher = better)
            double meritImprovement = originalMeritFunction > 1e-20
                ? originalMeritFunction / Math.Max(candidateMeritFunction, 1e-20)
                : 1.0;

            // Cap to avoid extreme values and protect against NaN
            meritImprovement = Math.Min(meritImprovement, 100);
            if (!double.IsFinite(meritImprovement) || double.IsNaN(meritImprovement))
            {
                meritImprovement = 1.0;
            }

            // Store individual improvements for display
            candidate.S1Improvement = Math.Abs(originalSeidel.S1) > 1e-20
                ? Math.Abs(originalSeidel.S1) / Math.Max(Math.Abs(candidate.SeidelResult.S1), 1e-20)
                : 1.0;
            candidate.LongColorImprovement = Math.Abs(originalChromatic.LongitudinalColor) > 1e-10
                ? Math.Abs(originalChromatic.LongitudinalColor) / Math.Max(Math.Abs(candidate.LongitudinalColor), 1e-10)
                : (Math.Abs(candidate.LongitudinalColor) < 0.1 ? 10.0 : 1.0);
            candidate.LatColorImprovement = Math.Abs(originalChromatic.LateralColor) > 1e-10
                ? Math.Abs(originalChromatic.LateralColor) / Math.Max(Math.Abs(candidate.LateralColor), 1e-10)
                : (Math.Abs(candidate.LateralColor) < 0.01 ? 10.0 : 1.0);

            // Apply EFL penalty to combined merit
            return meritImprovement * eflPenalty;
        }

        // Fall back to thin-lens S1 calculation if Seidel fails
        double s1Improvement = Math.Abs(originalS1) > 1e-20
            ? Math.Abs(originalS1) / Math.Max(Math.Abs(candidate.TotalS1), 1e-20)
            : 1.0;

        double longColorImprovement = Math.Abs(originalChromatic.LongitudinalColor) > 1e-10
            ? Math.Abs(originalChromatic.LongitudinalColor) / Math.Max(Math.Abs(candidate.LongitudinalColor), 1e-10)
            : (Math.Abs(candidate.LongitudinalColor) < 0.1 ? 10.0 : 1.0);

        double latColorImprovement = Math.Abs(originalChromatic.LateralColor) > 1e-10
            ? Math.Abs(originalChromatic.LateralColor) / Math.Max(Math.Abs(candidate.LateralColor), 1e-10)
            : (Math.Abs(candidate.LateralColor) < 0.01 ? 10.0 : 1.0);

        // Cap improvements to avoid extreme values
        s1Improvement = Math.Min(s1Improvement, 100);
        longColorImprovement = Math.Min(longColorImprovement, 100);
        latColorImprovement = Math.Min(latColorImprovement, 100);

        // Store individual improvements
        candidate.S1Improvement = s1Improvement;
        candidate.LongColorImprovement = longColorImprovement;
        candidate.LatColorImprovement = latColorImprovement;

        // Fallback merit: use W1 for spherical, and WCL/WCT for chromatic if enabled
        double merit = settings.AberrationWeights.W1 * s1Improvement;

        // Include chromatic aberrations in merit only if enabled
        if (settings.AberrationWeights.IncludeChromatic)
        {
            // For glass optimization, chromatic aberrations are important
            // Use moderate weights if not explicitly set
            double wCL = settings.AberrationWeights.WCL > 0 ? settings.AberrationWeights.WCL : 0.5;
            double wCT = settings.AberrationWeights.WCT > 0 ? settings.AberrationWeights.WCT : 0.3;
            merit += wCL * longColorImprovement + wCT * latColorImprovement;
        }

        // Ensure we don't return NaN or Infinity
        if (!double.IsFinite(merit) || double.IsNaN(merit))
        {
            merit = 1.0;
        }

        // Apply EFL penalty to combined merit
        return merit * eflPenalty;
    }
}

/// <summary>
/// Result from a single glass combination evaluation.
/// </summary>
public class GlassCombinationResult
{
    public int Rank { get; set; }
    public Glass Glass1 { get; set; } = null!;
    public Glass Glass2 { get; set; } = null!;
    public string Glass1Name { get; set; } = string.Empty;
    public string Glass2Name { get; set; } = string.Empty;
    public double N1 { get; set; }
    public double N2 { get; set; }
    public double V1 { get; set; }
    public double V2 { get; set; }
    public double PowerRatio { get; set; }
    public double AirGap { get; set; }
    public double Phi1 { get; set; }
    public double Phi2 { get; set; }
    public double TotalS1 { get; set; }
    public double S1_Element1 { get; set; }
    public double S1_Element2 { get; set; }
    public double LongitudinalColor { get; set; }
    public double LateralColor { get; set; }
    public double EstimatedLongitudinalColor { get; set; }
    public ChromaticAberrationResult? ChromaticResult { get; set; }
    public SeidelResult? SeidelResult { get; set; }
    public double SplitMeritFunction { get; set; }
    public OpticalSystem SplitSystem { get; set; } = null!;
    public double EffectivePower { get; set; }
    public double Efl { get; set; }
    public double OriginalEfl { get; set; }
    public double MeritScore { get; set; }
    public double S1Improvement { get; set; }
    public double LongColorImprovement { get; set; }
    public double LatColorImprovement { get; set; }

    public override string ToString()
    {
        return $"#{Rank}: {Glass1Name}/{Glass2Name} - " +
               $"S1={TotalS1:E3} ({S1Improvement:F1}x), " +
               $"LongColor={LongitudinalColor:F4}mm ({LongColorImprovement:F1}x), " +
               $"Merit={MeritScore:F2}";
    }
}

/// <summary>
/// Complete results from glass optimization.
/// </summary>
public class GlassOptimizationResult
{
    public OpticalSystem OriginalSystem { get; set; } = null!;
    public int ElementIndex { get; set; }
    public LensElement OriginalElement { get; set; } = null!;
    public double OriginalPower { get; set; }
    public double OriginalS1 { get; set; }
    public ChromaticAberrationResult OriginalChromaticAberration { get; set; } = null!;
    public SeidelResult? OriginalSeidel { get; set; }
    public double OriginalMeritFunction { get; set; }
    public double OriginalEfl { get; set; }

    /// <summary>
    /// Result from splitting with the original glass (same glass for both elements).
    /// This provides a baseline comparison showing improvement from splitting alone.
    /// </summary>
    public GlassCombinationResult? OriginalGlassSplitResult { get; set; }

    public List<Glass> AvailableGlasses { get; set; } = new();
    public int TotalCombinationsTried { get; set; }
    public List<GlassCombinationResult> TopResults { get; set; } = new();
    public GlassOptimizer.OptimizationSettings Settings { get; set; } = null!;

    public GlassCombinationResult? BestResult => TopResults.FirstOrDefault();

    /// <summary>
    /// Returns true if any result actually improved the merit function.
    /// </summary>
    public bool HasImprovements => TopResults.Any(r => r.MeritScore > 1.0);

    /// <summary>
    /// Number of glass combinations that improved the merit function.
    /// </summary>
    public int ImprovementCount => TopResults.Count(r => r.MeritScore > 1.0);

    public string Summary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Glass Optimization Results");
            sb.AppendLine($"==========================");
            sb.AppendLine($"Element {ElementIndex}: Original power = {OriginalPower:F6}");
            sb.AppendLine($"Original S1 = {OriginalS1:E4}");
            sb.AppendLine($"Original Longitudinal Color = {OriginalChromaticAberration.LongitudinalColor:F4}mm");
            sb.AppendLine($"Glasses available: {AvailableGlasses.Count}");
            sb.AppendLine($"Combinations tried: {TotalCombinationsTried}");
            sb.AppendLine();
            sb.AppendLine($"Top {Math.Min(10, TopResults.Count)} Results:");
            foreach (var result in TopResults.Take(10))
            {
                sb.AppendLine($"  {result}");
            }
            return sb.ToString();
        }
    }
}
