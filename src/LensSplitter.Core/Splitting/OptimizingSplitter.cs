using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Implements iterative power-preserving splitting with aberration optimization.
/// </summary>
public class OptimizingSplitter
{
    private readonly ParaxialRayTracer _tracer = new();
    private readonly AberrationCalculator _aberrationCalc = new();
    private readonly SeidelCalculator _seidelCalc = new();
    private OptimizationSettings? _currentSettings;

    /// <summary>
    /// Optimization settings for the iterative splitter.
    /// </summary>
    public class OptimizationSettings
    {
        /// <summary>
        /// Minimum power ratio to consider (φ₁/φ_total).
        /// </summary>
        public double MinPowerRatio { get; set; } = 0.3;

        /// <summary>
        /// Maximum power ratio to consider.
        /// </summary>
        public double MaxPowerRatio { get; set; } = 0.7;

        /// <summary>
        /// Power ratio search step size.
        /// </summary>
        public double PowerRatioStep { get; set; } = 0.02;

        /// <summary>
        /// Minimum air gap to consider (mm).
        /// </summary>
        public double MinAirGap { get; set; } = 0.1;

        /// <summary>
        /// Maximum air gap to consider (mm).
        /// </summary>
        public double MaxAirGap { get; set; } = 5.0;

        /// <summary>
        /// Air gap search step size (mm).
        /// </summary>
        public double AirGapStep { get; set; } = 0.2;

        /// <summary>
        /// Maximum iterations for refinement.
        /// </summary>
        public int MaxIterations { get; set; } = 50;

        /// <summary>
        /// Convergence tolerance for merit function.
        /// </summary>
        public double Tolerance { get; set; } = 1e-10;

        /// <summary>
        /// Whether to optimize air gap (if false, uses MinAirGap).
        /// </summary>
        public bool OptimizeAirGap { get; set; } = true;

        /// <summary>
        /// Minimum edge clearance to prevent lens collision (mm).
        /// </summary>
        public double MinEdgeClearance { get; set; } = 0.5;

        /// <summary>
        /// Whether to enforce geometry constraints even if it means changing total track length.
        /// If false, geometry constraints are reported as warnings but not enforced.
        /// </summary>
        public bool EnforceGeometryConstraints { get; set; } = true;

        /// <summary>
        /// Weights for each Seidel aberration in the merit function.
        /// Default: W2 (coma) = 1.5, W5 (distortion) = 0.2.
        /// </summary>
        public AberrationWeights AberrationWeights { get; set; } = AberrationWeights.Default;

        /// <summary>
        /// Whether to use full Seidel aberration optimization (S1-S5).
        /// If false, optimizes only S1 (legacy mode).
        /// </summary>
        public bool UseFullSeidelOptimization { get; set; } = true;

        /// <summary>
        /// Maximum allowed EFL deviation from original (as fraction, e.g., 0.03 = 3%).
        /// The merit function includes a quadratic penalty for EFL deviation.
        /// Default is 0.03 (3%).
        /// </summary>
        public double MaxEflDeviation { get; set; } = 0.03;
    }

    /// <summary>
    /// Performs iterative power-preserving splitting with aberration optimization.
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <param name="elementIndex">Index of the element to split.</param>
    /// <param name="settings">Optimization settings (null for defaults).</param>
    /// <returns>The optimized split result.</returns>
    public IterativeSplitResult SplitWithOptimization(
        OpticalSystem system,
        int elementIndex,
        OptimizationSettings? settings = null)
    {
        settings ??= new OptimizationSettings();
        _currentSettings = settings;

        var elements = system.GetLensElements();
        if (elementIndex < 0 || elementIndex >= elements.Count)
        {
            throw new ArgumentException($"Element index {elementIndex} out of range.");
        }

        // Check if element is part of a cemented or air-spaced group
        var groupInfo = DetectElementGroups(elements);
        if (groupInfo.TryGetValue(elementIndex, out var group))
        {
            string groupType = group.isCemented ? "cemented" : "air-spaced";
            throw new InvalidOperationException(
                $"Element {elementIndex} is part of a {group.description ?? groupType + " group"} and cannot be split. " +
                $"Splitting elements in {groupType} groups would destroy the aberration correction they provide. " +
                $"Use the 'analyze' command to find splittable elements.");
        }

        var originalElement = elements[elementIndex];
        var wavelength = system.PrimaryWavelength.ValueMicrons;

        // Check for negative power lenses
        double elementPower = originalElement.GetThickLensPower(wavelength);
        if (elementPower < 0)
        {
            throw new InvalidOperationException(
                $"Element {elementIndex} has negative power ({elementPower:F6}). " +
                $"Power-preserving splitting is designed for positive lenses. " +
                $"Negative lenses contribute overcorrected spherical aberration that typically " +
                $"corrects the undercorrected SA from positive lenses. Splitting a negative lens " +
                $"may reduce its beneficial correction and make overall system aberration worse. " +
                $"Consider splitting a positive lens instead, or use the 'analyze' command to find recommended elements.");
        }

        // Calculate geometry-based minimum air gap to prevent lens collision
        double geometryMinAirGap = CalculateMinimumAirGap(system, elementIndex, 0.5, settings.MinEdgeClearance);

        // Constrain maximum air gap to what's physically realizable within the element
        // Need at least 1mm of glass on each side
        const double minGlassThickness = 1.0;
        double maxRealizableGap = Math.Max(0.1, originalElement.CenterThickness - 2 * minGlassThickness);

        // Use the larger of requested minimum or geometry-based minimum
        double effectiveMinAirGap = Math.Max(settings.MinAirGap, geometryMinAirGap);

        if (settings.MaxAirGap > maxRealizableGap || effectiveMinAirGap > settings.MinAirGap)
        {
            settings = new OptimizationSettings
            {
                MinPowerRatio = settings.MinPowerRatio,
                MaxPowerRatio = settings.MaxPowerRatio,
                PowerRatioStep = settings.PowerRatioStep,
                MinAirGap = Math.Min(effectiveMinAirGap, maxRealizableGap),
                MaxAirGap = Math.Min(settings.MaxAirGap, maxRealizableGap),
                AirGapStep = settings.AirGapStep,
                MaxIterations = settings.MaxIterations,
                Tolerance = settings.Tolerance,
                OptimizeAirGap = settings.OptimizeAirGap
            };
        }

        // Get original element properties
        double originalPower = originalElement.GetThickLensPower(wavelength);
        double n = originalElement.Glass.GetRefractiveIndex(wavelength);
        double entrancePupilRadius = system.EntrancePupilDiameter / 2.0;

        // Calculate single lens S₁ for comparison
        double singleLensY = -1.0; // Object at infinity
        double singleLensX = AberrationCalculator.CalculateOptimalShapeFactor(n, singleLensY);
        double singleLensS1 = AberrationCalculator.CalculateS1ThinLens(
            entrancePupilRadius, originalPower, n, singleLensX, singleLensY);

        // Calculate original EFL for full system optimization
        double originalEfl = _tracer.CalculateEfl(system, wavelength);

        // Set optimization context for full system merit function evaluation
        _optimizationSystem = system;
        _optimizationElementIndex = elementIndex;
        _optimizationWavelength = wavelength;
        _targetEfl = originalEfl;

        // Calculate original system's merit function as baseline
        double fieldAngle = system.Fields.Count > 0
            ? system.Fields.Max(f => Math.Abs(f.Y))
            : 1.0;
        if (fieldAngle < 0.001) fieldAngle = 1.0;
        var originalSeidel = _seidelCalc.Calculate(system, wavelength, fieldAngle);
        double originalMeritFunction = SeidelCalculator.CalculateMeritFunction(originalSeidel, settings.AberrationWeights);
        _originalSystemMeritFunction = originalMeritFunction;

        // Phase 1: Grid search to find approximate optimum
        var gridResult = GridSearch(originalPower, n, entrancePupilRadius, settings);

        // Phase 2: Local refinement around best grid point
        var refinedResult = LocalRefinement(
            originalPower, n, entrancePupilRadius,
            gridResult.PowerRatio, gridResult.AirGap, settings);

        // Clear optimization context
        _optimizationSystem = null;

        // Build the split optical system with EFL preservation
        var splitSystem = BuildSplitSystemWithEflPreservation(
            system, elementIndex, refinedResult, wavelength, originalEfl);

        // Trace rays through both systems
        var originalRays = _tracer.TraceRayPair(system, 0, wavelength);
        var splitRays = _tracer.TraceRayPair(splitSystem, 0, wavelength);

        // Calculate final EFL (should be very close to original now)
        double splitEfl = _tracer.CalculateEfl(splitSystem, wavelength);

        // Calculate S₁ reduction factor
        double s1Reduction = Math.Abs(singleLensS1) > 1e-20
            ? Math.Abs(singleLensS1 / refinedResult.S1_Total)
            : 1.0;

        // Check if geometry constraint was satisfied
        // Get actual air gap from the split system
        var splitElements = splitSystem.GetLensElements();
        var splitElement1Front = originalElement.FrontSurface.Index;
        double actualAirGapUsed = splitSystem.Surfaces.Count > splitElement1Front + 1
            ? splitSystem.Surfaces[splitElement1Front + 1].Thickness
            : refinedResult.AirGap;

        bool hasGeometryWarning = actualAirGapUsed < geometryMinAirGap;
        string? geometryWarning = null;
        if (hasGeometryWarning)
        {
            geometryWarning = $"Geometry requires {geometryMinAirGap:F2}mm air gap to prevent collision, " +
                             $"but only {actualAirGapUsed:F2}mm is available. " +
                             $"Consider using --enforce-geometry or increasing original element thickness.";
        }

        // Calculate full Seidel aberrations for the split system (reuse originalSeidel calculated earlier)
        var splitSeidel = _seidelCalc.Calculate(splitSystem, wavelength, fieldAngle);

        // Calculate merit functions using settings weights
        var weights = settings.AberrationWeights;
        double originalMF = SeidelCalculator.CalculateMeritFunction(originalSeidel, weights);
        double splitMF = SeidelCalculator.CalculateMeritFunction(splitSeidel, weights);

        return new IterativeSplitResult
        {
            OriginalSystem = system,
            SplitSystem = splitSystem,
            SplitElementIndex = elementIndex,
            OriginalElement = originalElement,
            OriginalPower = originalPower,
            SingleLensS1 = singleLensS1,
            OptimizedResult = refinedResult,
            S1ReductionFactor = s1Reduction,
            OriginalMarginalRay = originalRays.MarginalRay,
            OriginalChiefRay = originalRays.ChiefRay,
            SplitMarginalRay = splitRays.MarginalRay,
            SplitChiefRay = splitRays.ChiefRay,
            OriginalEfl = originalEfl,
            SplitEfl = splitEfl,
            OriginalSeidel = originalSeidel,
            SplitSeidel = splitSeidel,
            OriginalMeritFunction = originalMF,
            SplitMeritFunction = splitMF,
            IterationHistory = _iterationHistory.ToList(),
            GeometryMinAirGap = geometryMinAirGap,
            HasGeometryWarning = hasGeometryWarning,
            GeometryWarning = geometryWarning
        };
    }

    private List<SplitAberrationResult> _iterationHistory = new();

    // Store context for full-system optimization
    private OpticalSystem? _optimizationSystem;
    private int _optimizationElementIndex;
    private double _optimizationWavelength;
    private double _targetEfl;
    private double _originalSystemMeritFunction;

    private SplitAberrationResult GridSearch(
        double totalPower,
        double refractiveIndex,
        double entrancePupilRadius,
        OptimizationSettings settings)
    {
        _iterationHistory.Clear();

        SplitAberrationResult? bestResult = null;
        double bestMeritFunction = double.MaxValue;

        // Calculate S1-optimal shape factors for reference
        double X1_s1opt = AberrationCalculator.CalculateOptimalShapeFactor(refractiveIndex, -1.0);

        // Shape factor offsets to try (relative to S1-optimal)
        // For full Seidel optimization, we need to explore shapes that may reduce coma/astigmatism
        // Include extreme shapes that might create aplanatic-like corrections
        double[] shapeOffsets = settings.UseFullSeidelOptimization
            ? new[] { -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5 }  // Much wider exploration
            : new[] { 0.0 };  // Only S1-optimal for S1-only mode

        // Grid search over power ratio, air gap, and shape factors
        for (double ratio = settings.MinPowerRatio;
             ratio <= settings.MaxPowerRatio;
             ratio += settings.PowerRatioStep)
        {
            double gapStart = settings.OptimizeAirGap ? settings.MinAirGap : settings.MinAirGap;
            double gapEnd = settings.OptimizeAirGap ? settings.MaxAirGap : settings.MinAirGap;

            for (double gap = gapStart; gap <= gapEnd; gap += settings.AirGapStep)
            {
                // For each (ratio, gap) combination, try different shape factor combinations
                foreach (double dX1 in shapeOffsets)
                {
                    // For X2, use a coarser but wider grid
                    double[] x2Offsets = settings.UseFullSeidelOptimization
                        ? new[] { -2.0, -1.0, 0.0, 1.0, 2.0 }
                        : new[] { 0.0 };

                    foreach (double dX2 in x2Offsets)
                    {
                        // Calculate actual shape factors
                        // X2's S1-optimal depends on the ray geometry which varies with ratio/gap
                        var baseResult = _aberrationCalc.EvaluateSplitConfiguration(
                            totalPower, ratio, gap, refractiveIndex, entrancePupilRadius);
                        double X2_s1opt = baseResult.X2_Optimal;

                        double X1 = X1_s1opt + dX1;
                        double X2 = X2_s1opt + dX2;

                        var result = _aberrationCalc.EvaluateSplitConfiguration(
                            totalPower, ratio, gap, refractiveIndex, entrancePupilRadius,
                            customX1: X1, customX2: X2);

                        // Calculate merit function using full system ray tracing
                        double meritFunction = EvaluateFullSystemMeritFunction(result, settings);
                        result.MeritFunction = meritFunction;

                        _iterationHistory.Add(result);

                        if (meritFunction < bestMeritFunction)
                        {
                            bestMeritFunction = meritFunction;
                            bestResult = result;
                        }
                    }
                }
            }
        }

        return bestResult ?? _aberrationCalc.EvaluateSplitConfiguration(
            totalPower, 0.5, settings.MinAirGap, refractiveIndex, entrancePupilRadius);
    }

    /// <summary>
    /// Evaluates the full system merit function by building a trial split system
    /// and calculating total Seidel aberrations across all surfaces.
    /// </summary>
    private double EvaluateFullSystemMeritFunction(SplitAberrationResult result, OptimizationSettings settings)
    {
        if (_optimizationSystem == null || !settings.UseFullSeidelOptimization)
        {
            // Fall back to thin-lens approximation
            return Math.Abs(result.S1_Total);
        }

        try
        {
            // Build a trial split system with this configuration
            var trialSystem = BuildTrialSplitSystem(result);
            if (trialSystem == null)
            {
                return double.MaxValue;
            }

            // Calculate full Seidel aberrations for the trial system
            double fieldAngle = _optimizationSystem.Fields.Count > 0
                ? _optimizationSystem.Fields.Max(f => Math.Abs(f.Y))
                : 1.0;
            if (fieldAngle < 0.001) fieldAngle = 1.0;

            var seidel = _seidelCalc.Calculate(trialSystem, _optimizationWavelength, fieldAngle);

            // Store the full Seidel results in the result for later reference
            result.S1_Total = seidel.S1;
            result.S2_Total = seidel.S2;
            result.S3_Total = seidel.S3;
            result.S4_Total = seidel.S4;
            result.S5_Total = seidel.S5;
            result.CL_Total = seidel.CL;
            result.CT_Total = seidel.CT;

            // Calculate weighted merit function
            double meritFunction = SeidelCalculator.CalculateMeritFunction(seidel, settings.AberrationWeights);

            // Add EFL penalty: quadratic penalty for deviation from target EFL
            if (settings.MaxEflDeviation > 0 && _targetEfl > 0)
            {
                double trialEfl = _tracer.CalculateEfl(trialSystem, _optimizationWavelength);
                double eflDeviation = Math.Abs(trialEfl - _targetEfl) / _targetEfl;

                // Quadratic penalty: 1.0 at 0% deviation, 0.0 at max deviation
                double normalizedDeviation = eflDeviation / settings.MaxEflDeviation;
                double eflPenalty = Math.Max(0, 1.0 - normalizedDeviation * normalizedDeviation);

                // Apply penalty by dividing merit (lower is better, so divide by penalty < 1 makes it worse)
                if (eflPenalty > 0.01)
                {
                    meritFunction /= eflPenalty;
                }
                else
                {
                    // Severe penalty for large deviations
                    meritFunction *= 100.0;
                }
            }

            // Add penalty if configuration is worse than original system
            // This prevents the optimizer from selecting splits that degrade performance
            if (meritFunction > _originalSystemMeritFunction && _originalSystemMeritFunction > 0)
            {
                // Apply exponential penalty for exceeding original - the more we exceed, the worse
                double degradationRatio = meritFunction / _originalSystemMeritFunction;
                meritFunction *= (1.0 + 10.0 * (degradationRatio - 1.0));
            }

            return meritFunction;
        }
        catch
        {
            // If building the trial system fails, return a large value
            return double.MaxValue;
        }
    }

    /// <summary>
    /// Builds a trial split system for merit function evaluation.
    /// This is a lightweight version that doesn't do full EFL preservation iteration.
    /// </summary>
    private OpticalSystem? BuildTrialSplitSystem(SplitAberrationResult aberrationResult)
    {
        if (_optimizationSystem == null) return null;

        var elements = _optimizationSystem.GetLensElements();
        if (_optimizationElementIndex >= elements.Count) return null;

        var originalElement = elements[_optimizationElementIndex];
        var glass = originalElement.Glass;
        double n = glass.GetRefractiveIndex(_optimizationWavelength);

        // Solve for radii from optimal shapes and powers
        var (r1_first, r2_first) = SolveRadiiForPowerAndShape(
            aberrationResult.Phi1, aberrationResult.X1_Optimal, n);
        var (r1_second, r2_second) = SolveRadiiForPowerAndShape(
            aberrationResult.Phi2, aberrationResult.X2_Optimal, n);

        // Create new system
        var trialSystem = _optimizationSystem.Clone();

        // Calculate thicknesses
        double originalElementThickness = originalElement.CenterThickness;
        double totalGlassThickness = originalElementThickness - aberrationResult.AirGap;
        double newGlassThickness1 = Math.Max(1.0, totalGlassThickness / 2.0);
        double newGlassThickness2 = Math.Max(1.0, totalGlassThickness - newGlassThickness1);

        int frontSurfaceIndex = originalElement.FrontSurface.Index;
        int rearSurfaceIndex = originalElement.RearSurface.Index;

        double originalSpaceAfter = originalElement.RearSurface.Thickness;
        double newSplitTrack = newGlassThickness1 + aberrationResult.AirGap + newGlassThickness2;
        double trackDifference = newSplitTrack - originalElementThickness;
        double adjustedSpaceAfter = Math.Max(0.1, originalSpaceAfter - trackDifference);

        // Create four new surfaces
        var surface1 = new Surface
        {
            Index = frontSurfaceIndex,
            Radius = r1_first,
            Thickness = newGlassThickness1,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface2 = new Surface
        {
            Index = frontSurfaceIndex + 1,
            Radius = r2_first,
            Thickness = aberrationResult.AirGap,
            Glass = null,
            GlassName = null,
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface3 = new Surface
        {
            Index = frontSurfaceIndex + 2,
            Radius = r1_second,
            Thickness = newGlassThickness2,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard
        };

        var surface4 = new Surface
        {
            Index = frontSurfaceIndex + 3,
            Radius = r2_second,
            Thickness = adjustedSpaceAfter,
            Glass = null,
            GlassName = null,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            SurfaceType = SurfaceType.Standard,
            IsStop = originalElement.RearSurface.IsStop
        };

        // Replace original surfaces
        trialSystem.Surfaces.RemoveAt(rearSurfaceIndex);
        trialSystem.Surfaces.RemoveAt(frontSurfaceIndex);

        trialSystem.Surfaces.Insert(frontSurfaceIndex, surface1);
        trialSystem.Surfaces.Insert(frontSurfaceIndex + 1, surface2);
        trialSystem.Surfaces.Insert(frontSurfaceIndex + 2, surface3);
        trialSystem.Surfaces.Insert(frontSurfaceIndex + 3, surface4);

        trialSystem.ReindexSurfaces();

        return trialSystem;
    }

    private SplitAberrationResult LocalRefinement(
        double totalPower,
        double refractiveIndex,
        double entrancePupilRadius,
        double initialRatio,
        double initialGap,
        OptimizationSettings settings)
    {
        double ratio = initialRatio;
        double gap = initialGap;
        double stepRatio = settings.PowerRatioStep / 2.0;  // Start with larger steps
        double stepGap = settings.AirGapStep / 2.0;

        // Initialize shape factors from grid search result
        var initialResult = _aberrationCalc.EvaluateSplitConfiguration(
            totalPower, ratio, gap, refractiveIndex, entrancePupilRadius);
        double X1 = initialResult.X1_Optimal;
        double X2 = initialResult.X2_Optimal;
        double stepX = 0.5; // Larger initial shape factor step

        var currentResult = initialResult;

        // Calculate merit function using full system ray tracing
        double currentMF = EvaluateFullSystemMeritFunction(currentResult, settings);
        currentResult.MeritFunction = currentMF;

        // Store the best result found (may be from grid search)
        var bestResult = currentResult;
        double bestMF = currentMF;

        for (int iter = 0; iter < settings.MaxIterations; iter++)
        {
            bool improved = false;

            // Try all 4 variables in each iteration for better convergence
            // Use gradient-like approach: try all directions, then move in best direction

            var candidates = new List<(double r, double g, double x1, double x2, double mf, SplitAberrationResult result)>();

            // Try adjusting power ratio
            foreach (double dRatio in new[] { -stepRatio, stepRatio })
            {
                double newRatio = ratio + dRatio;
                if (newRatio < settings.MinPowerRatio || newRatio > settings.MaxPowerRatio)
                    continue;

                var testResult = _aberrationCalc.EvaluateSplitConfiguration(
                    totalPower, newRatio, gap, refractiveIndex, entrancePupilRadius,
                    customX1: X1, customX2: X2);

                double testMF = EvaluateFullSystemMeritFunction(testResult, settings);
                testResult.MeritFunction = testMF;
                candidates.Add((newRatio, gap, X1, X2, testMF, testResult));
            }

            // Try adjusting air gap
            if (settings.OptimizeAirGap)
            {
                foreach (double dGap in new[] { -stepGap, stepGap })
                {
                    double newGap = gap + dGap;
                    if (newGap < settings.MinAirGap || newGap > settings.MaxAirGap)
                        continue;

                    var testResult = _aberrationCalc.EvaluateSplitConfiguration(
                        totalPower, ratio, newGap, refractiveIndex, entrancePupilRadius,
                        customX1: X1, customX2: X2);

                    double testMF = EvaluateFullSystemMeritFunction(testResult, settings);
                    testResult.MeritFunction = testMF;
                    candidates.Add((ratio, newGap, X1, X2, testMF, testResult));
                }
            }

            // Try adjusting shape factors (for full Seidel optimization)
            if (settings.UseFullSeidelOptimization)
            {
                // Optimize X1 with larger range
                foreach (double dX in new[] { -stepX, stepX, -stepX * 2, stepX * 2 })
                {
                    double newX1 = X1 + dX;
                    if (newX1 < -4.0 || newX1 > 4.0) continue;

                    var testResult = _aberrationCalc.EvaluateSplitConfiguration(
                        totalPower, ratio, gap, refractiveIndex, entrancePupilRadius,
                        customX1: newX1, customX2: X2);

                    double testMF = EvaluateFullSystemMeritFunction(testResult, settings);
                    testResult.MeritFunction = testMF;
                    candidates.Add((ratio, gap, newX1, X2, testMF, testResult));
                }

                // Optimize X2 with larger range
                foreach (double dX in new[] { -stepX, stepX, -stepX * 2, stepX * 2 })
                {
                    double newX2 = X2 + dX;
                    if (newX2 < -4.0 || newX2 > 4.0) continue;

                    var testResult = _aberrationCalc.EvaluateSplitConfiguration(
                        totalPower, ratio, gap, refractiveIndex, entrancePupilRadius,
                        customX1: X1, customX2: newX2);

                    double testMF = EvaluateFullSystemMeritFunction(testResult, settings);
                    testResult.MeritFunction = testMF;
                    candidates.Add((ratio, gap, X1, newX2, testMF, testResult));
                }

                // Try diagonal moves (X1 and X2 together) - often more effective
                foreach (double dX1 in new[] { -stepX, stepX })
                {
                    foreach (double dX2 in new[] { -stepX, stepX })
                    {
                        double newX1 = X1 + dX1;
                        double newX2 = X2 + dX2;
                        if (newX1 < -4.0 || newX1 > 4.0 || newX2 < -4.0 || newX2 > 4.0) continue;

                        var testResult = _aberrationCalc.EvaluateSplitConfiguration(
                            totalPower, ratio, gap, refractiveIndex, entrancePupilRadius,
                            customX1: newX1, customX2: newX2);

                        double testMF = EvaluateFullSystemMeritFunction(testResult, settings);
                        testResult.MeritFunction = testMF;
                        candidates.Add((ratio, gap, newX1, newX2, testMF, testResult));
                    }
                }
            }

            // Find best candidate
            var bestCandidate = candidates.OrderBy(c => c.mf).FirstOrDefault();
            if (bestCandidate.result != null && bestCandidate.mf < currentMF - settings.Tolerance)
            {
                ratio = bestCandidate.r;
                gap = bestCandidate.g;
                X1 = bestCandidate.x1;
                X2 = bestCandidate.x2;
                currentMF = bestCandidate.mf;
                currentResult = bestCandidate.result;
                improved = true;
                _iterationHistory.Add(bestCandidate.result);

                // Track global best
                if (currentMF < bestMF)
                {
                    bestMF = currentMF;
                    bestResult = currentResult;
                }
            }

            if (!improved)
            {
                // Reduce step size
                stepRatio /= 2.0;
                stepGap /= 2.0;
                stepX /= 2.0;

                if (stepRatio < settings.PowerRatioStep / 100.0 && stepX < 0.005)
                    break; // Converged
            }
        }

        return bestResult;
    }

    private OpticalSystem BuildSplitSystem(
        OpticalSystem originalSystem,
        int elementIndex,
        SplitAberrationResult aberrationResult,
        double wavelength)
    {
        var elements = originalSystem.GetLensElements();
        var originalElement = elements[elementIndex];
        var glass = originalElement.Glass;
        double n = glass.GetRefractiveIndex(wavelength);

        // Solve for radii from optimal shapes and powers
        var (r1_first, r2_first) = SolveRadiiForPowerAndShape(
            aberrationResult.Phi1, aberrationResult.X1_Optimal, n);
        var (r1_second, r2_second) = SolveRadiiForPowerAndShape(
            aberrationResult.Phi2, aberrationResult.X2_Optimal, n);

        // Create new system
        var splitSystem = originalSystem.Clone();

        // Calculate thicknesses while preserving total track length
        // Original: |---glass (t_orig)---|---air (t_after)---|
        // Split:    |--g1--|--air gap--|--g2--|---air (t_after_adjusted)---|
        double originalElementThickness = originalElement.CenterThickness;
        double originalSpaceAfter = originalElement.RearSurface.Thickness;
        double originalTotalTrack = originalElementThickness + originalSpaceAfter;

        // Calculate the REQUIRED clear aperture by tracing rays through the original system
        // This ensures we size the geometry to pass all rays
        double requiredClearAperture = CalculateRequiredClearAperture(originalSystem, elementIndex, wavelength);

        // Calculate minimum separations based on surface geometry (sag) for the REQUIRED aperture
        double minEdgeClearance = _currentSettings?.MinEdgeClearance ?? 0.5;
        var (minAirGapFromGeometry, minGlass1, minGlass2) = CalculateMinimumSeparationsForAperture(
            r1_first, r2_first, r1_second, r2_second,
            requiredClearAperture, minEdgeClearance);

        // Constrain air gap to fit within the available space
        double maxAirGap = Math.Max(0.1, originalElementThickness - minGlass1 - minGlass2);
        bool enforceGeometry = _currentSettings?.EnforceGeometryConstraints ?? false;

        // Use the larger of the requested air gap or the geometry-based minimum
        // But warn if geometry minimum exceeds what's realizable
        double actualAirGap;
        if (minAirGapFromGeometry > maxAirGap)
        {
            if (enforceGeometry)
            {
                // Enforce geometry constraints - use geometry minimum even if it exceeds available space
                // This will change the total track length
                actualAirGap = minAirGapFromGeometry;
            }
            else
            {
                // Geometry requires more space than available - use max and thinner glass
                actualAirGap = maxAirGap;
                // Adjust glass thicknesses to be as thin as possible while making room for air gap
                double availableForGlass = originalElementThickness - actualAirGap;
                minGlass1 = Math.Max(0.5, availableForGlass / 2.0);
                minGlass2 = Math.Max(0.5, availableForGlass / 2.0);
            }
        }
        else
        {
            // Use geometry minimum or requested gap, whichever is larger
            actualAirGap = Math.Max(aberrationResult.AirGap, minAirGapFromGeometry);
            actualAirGap = Math.Min(actualAirGap, maxAirGap);
        }

        // Distribute remaining glass thickness between the two elements
        double totalGlassThickness = originalElementThickness - actualAirGap;

        // When enforcing geometry and it exceeds available space, we need minimum glass thicknesses
        // and the air gap will cause the total track to increase
        double newGlassThickness1, newGlassThickness2;
        if (enforceGeometry && minAirGapFromGeometry > maxAirGap)
        {
            // Use minimum glass thicknesses - total track will increase
            newGlassThickness1 = Math.Max(0.5, minGlass1);
            newGlassThickness2 = Math.Max(0.5, minGlass2);
        }
        else
        {
            newGlassThickness1 = Math.Max(minGlass1, totalGlassThickness / 2.0);
            newGlassThickness2 = Math.Max(minGlass2, totalGlassThickness - newGlassThickness1);

            // Ensure we have enough total glass thickness
            if (newGlassThickness1 + newGlassThickness2 > totalGlassThickness)
            {
                // Not enough space - reduce air gap further
                actualAirGap = Math.Max(0.1, originalElementThickness - newGlassThickness1 - newGlassThickness2);
            }
        }

        // Calculate the new total track for the split elements
        double newSplitTrack = newGlassThickness1 + actualAirGap + newGlassThickness2;

        // Adjust the spacing after the split to preserve total track length
        double trackDifference = newSplitTrack - originalElementThickness;
        double adjustedSpaceAfter = Math.Max(0.1, originalSpaceAfter - trackDifference);

        int frontSurfaceIndex = originalElement.FrontSurface.Index;
        int rearSurfaceIndex = originalElement.RearSurface.Index;

        // Create four new surfaces with the REQUIRED clear aperture
        var surface1 = new Surface
        {
            Index = frontSurfaceIndex,
            Radius = r1_first,
            Thickness = newGlassThickness1,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = requiredClearAperture,
            SurfaceType = SurfaceType.Standard,
            Conic = 0 // Reset conic for new surfaces
        };

        var surface2 = new Surface
        {
            Index = frontSurfaceIndex + 1,
            Radius = r2_first,
            Thickness = actualAirGap,
            Glass = null, // Air after this surface
            GlassName = null,
            SemiDiameter = requiredClearAperture,
            SurfaceType = SurfaceType.Standard
        };

        var surface3 = new Surface
        {
            Index = frontSurfaceIndex + 2,
            Radius = r1_second,
            Thickness = newGlassThickness2,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = requiredClearAperture,
            SurfaceType = SurfaceType.Standard
        };

        var surface4 = new Surface
        {
            Index = frontSurfaceIndex + 3,
            Radius = r2_second,
            Thickness = adjustedSpaceAfter, // Adjusted to preserve track length
            Glass = null, // Air after the split element
            GlassName = null,
            SemiDiameter = requiredClearAperture,
            SurfaceType = SurfaceType.Standard,
            IsStop = originalElement.RearSurface.IsStop,
            // Preserve marginal ray height solve from original rear surface
            HasMarginalRayHeightSolve = originalElement.RearSurface.HasMarginalRayHeightSolve,
            MarginalRayHeightSolveParams = originalElement.RearSurface.MarginalRayHeightSolveParams
        };

        // Replace original surfaces
        splitSystem.Surfaces.RemoveAt(rearSurfaceIndex);
        splitSystem.Surfaces.RemoveAt(frontSurfaceIndex);

        splitSystem.Surfaces.Insert(frontSurfaceIndex, surface1);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 1, surface2);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 2, surface3);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 3, surface4);

        splitSystem.ReindexSurfaces();

        return splitSystem;
    }

    /// <summary>
    /// Builds the split system and iteratively adjusts to preserve the original EFL.
    /// Adjusts the power of the split elements to maintain the original system focal length.
    /// </summary>
    private OpticalSystem BuildSplitSystemWithEflPreservation(
        OpticalSystem originalSystem,
        int elementIndex,
        SplitAberrationResult aberrationResult,
        double wavelength,
        double targetEfl)
    {
        const int maxIterations = 200;
        const double eflTolerance = 0.03; // 3% tolerance

        // Start with the initial split system
        var splitSystem = BuildSplitSystem(originalSystem, elementIndex, aberrationResult, wavelength);

        // Get element info
        var elements = originalSystem.GetLensElements();
        var originalElement = elements[elementIndex];
        var glass = originalElement.Glass;
        double n = glass.GetRefractiveIndex(wavelength);
        int frontSurfaceIndex = originalElement.FrontSurface.Index;

        // Calculate the required clear aperture from the ORIGINAL system
        // This must be done before any modifications to ensure correct ray heights
        double requiredClearAperture = CalculateRequiredClearAperture(originalSystem, elementIndex, wavelength);

        double currentEfl = _tracer.CalculateEfl(splitSystem, wavelength);
        double eflError = Math.Abs((currentEfl - targetEfl) / targetEfl);

        // If EFL is already close enough, diameters are already set correctly by BuildSplitSystem
        if (eflError < eflTolerance)
        {
            return splitSystem;
        }

        // The base powers from the aberration optimization
        double basePhi1 = aberrationResult.Phi1;
        double basePhi2 = aberrationResult.Phi2;
        double baseTotalPower = basePhi1 + basePhi2;

        // Get the air gap in the split system
        double airGap = splitSystem.Surfaces[frontSurfaceIndex + 1].Thickness;

        // Calculate what the combined power should be for the target EFL
        // For a thick lens doublet: φ_combined = φ₁ + φ₂ - d*φ₁*φ₂
        // We need φ_combined ≈ 1/targetEfl for the system to have the right EFL
        double targetPower = 1.0 / targetEfl;

        // Calculate initial scale factor estimate
        // If we scale both powers by s: φ_combined = s*φ₁ + s*φ₂ - d*s²*φ₁*φ₂
        // For small air gaps, this approximates to s*(φ₁ + φ₂), so s ≈ targetPower/baseTotalPower
        double powerScaleFactor = 1.0;
        if (Math.Abs(baseTotalPower) > 1e-12)
        {
            powerScaleFactor = targetPower / baseTotalPower;
            powerScaleFactor = Math.Max(0.2, Math.Min(5.0, powerScaleFactor));
        }

        // Store the best result found
        double bestScaleFactor = 1.0;
        double bestEflError = eflError;
        (double r1, double r2, double r3, double r4) bestRadii = (0, 0, 0, 0);

        // First, apply initial estimate and store as best if better
        {
            double adjustedPhi1 = basePhi1 * powerScaleFactor;
            double adjustedPhi2 = basePhi2 * powerScaleFactor;

            var (r1, r2) = SolveRadiiForPowerAndShape(adjustedPhi1, aberrationResult.X1_Optimal, n);
            var (r3, r4) = SolveRadiiForPowerAndShape(adjustedPhi2, aberrationResult.X2_Optimal, n);

            splitSystem.Surfaces[frontSurfaceIndex].Radius = r1;
            splitSystem.Surfaces[frontSurfaceIndex + 1].Radius = r2;
            splitSystem.Surfaces[frontSurfaceIndex + 2].Radius = r3;
            splitSystem.Surfaces[frontSurfaceIndex + 3].Radius = r4;

            currentEfl = _tracer.CalculateEfl(splitSystem, wavelength);
            eflError = Math.Abs((currentEfl - targetEfl) / targetEfl);

            if (eflError < bestEflError)
            {
                bestEflError = eflError;
                bestScaleFactor = powerScaleFactor;
                bestRadii = (r1, r2, r3, r4);
            }
        }

        // Iteratively refine
        for (int iter = 0; iter < maxIterations && eflError > eflTolerance; iter++)
        {
            // Calculate numerical derivative dEFL/d(scaleFactor)
            const double delta = 0.005;
            double testScaleFactor = powerScaleFactor * (1.0 + delta);

            double testPhi1 = basePhi1 * testScaleFactor;
            double testPhi2 = basePhi2 * testScaleFactor;

            var (r1_test, r2_test) = SolveRadiiForPowerAndShape(testPhi1, aberrationResult.X1_Optimal, n);
            var (r3_test, r4_test) = SolveRadiiForPowerAndShape(testPhi2, aberrationResult.X2_Optimal, n);

            splitSystem.Surfaces[frontSurfaceIndex].Radius = r1_test;
            splitSystem.Surfaces[frontSurfaceIndex + 1].Radius = r2_test;
            splitSystem.Surfaces[frontSurfaceIndex + 2].Radius = r3_test;
            splitSystem.Surfaces[frontSurfaceIndex + 3].Radius = r4_test;

            double eflAtTestPlus = _tracer.CalculateEfl(splitSystem, wavelength);
            double dEfl_dScale = (eflAtTestPlus - currentEfl) / (delta * powerScaleFactor);

            if (Math.Abs(dEfl_dScale) < 1e-10)
            {
                // Derivative too small - use bisection-like approach
                if (currentEfl > targetEfl)
                    powerScaleFactor *= 1.05;
                else
                    powerScaleFactor *= 0.95;
            }
            else
            {
                // Newton-Raphson step
                double eflErrorValue = currentEfl - targetEfl;
                double scaleAdjustment = -eflErrorValue / dEfl_dScale;

                // Limit adjustment
                double maxAdjust = powerScaleFactor * 0.3;
                scaleAdjustment = Math.Max(-maxAdjust, Math.Min(maxAdjust, scaleAdjustment));
                powerScaleFactor += scaleAdjustment;
            }

            // Keep in bounds
            powerScaleFactor = Math.Max(0.1, Math.Min(10.0, powerScaleFactor));

            // Apply and evaluate
            double adjustedPhi1 = basePhi1 * powerScaleFactor;
            double adjustedPhi2 = basePhi2 * powerScaleFactor;

            var (r1_first, r2_first) = SolveRadiiForPowerAndShape(adjustedPhi1, aberrationResult.X1_Optimal, n);
            var (r1_second, r2_second) = SolveRadiiForPowerAndShape(adjustedPhi2, aberrationResult.X2_Optimal, n);

            splitSystem.Surfaces[frontSurfaceIndex].Radius = r1_first;
            splitSystem.Surfaces[frontSurfaceIndex + 1].Radius = r2_first;
            splitSystem.Surfaces[frontSurfaceIndex + 2].Radius = r1_second;
            splitSystem.Surfaces[frontSurfaceIndex + 3].Radius = r2_second;

            currentEfl = _tracer.CalculateEfl(splitSystem, wavelength);
            eflError = Math.Abs((currentEfl - targetEfl) / targetEfl);

            if (eflError < bestEflError)
            {
                bestEflError = eflError;
                bestScaleFactor = powerScaleFactor;
                bestRadii = (r1_first, r2_first, r1_second, r2_second);
            }
        }

        // Apply the best radii found
        splitSystem.Surfaces[frontSurfaceIndex].Radius = bestRadii.r1;
        splitSystem.Surfaces[frontSurfaceIndex + 1].Radius = bestRadii.r2;
        splitSystem.Surfaces[frontSurfaceIndex + 2].Radius = bestRadii.r3;
        splitSystem.Surfaces[frontSurfaceIndex + 3].Radius = bestRadii.r4;

        // Set diameters to the required aperture calculated from the ORIGINAL system
        // This ensures rays pass through regardless of how the split system geometry changed
        splitSystem.Surfaces[frontSurfaceIndex].SemiDiameter = requiredClearAperture;
        splitSystem.Surfaces[frontSurfaceIndex + 1].SemiDiameter = requiredClearAperture;
        splitSystem.Surfaces[frontSurfaceIndex + 2].SemiDiameter = requiredClearAperture;
        splitSystem.Surfaces[frontSurfaceIndex + 3].SemiDiameter = requiredClearAperture;

        return splitSystem;
    }

    /// <summary>
    /// Updates the semi-diameters of the split surfaces based on ray heights and collision avoidance.
    /// Calculates the minimum diameter needed for rays, then limits by maximum diameter to prevent collision.
    /// </summary>
    private void UpdateSplitSurfaceDiameters(OpticalSystem splitSystem, int frontSurfaceIndex, double wavelength,
        double originalFrontDiameter, double originalRearDiameter)
    {
        // Trace rays to get minimum required diameters
        var marginalRay = _tracer.TraceMarginalRay(splitSystem, wavelength);

        double maxFieldAngle = 0;
        if (splitSystem.Fields.Count > 0)
        {
            maxFieldAngle = splitSystem.Fields.Max(f => Math.Abs(f.Y));
        }
        if (maxFieldAngle < 0.001)
        {
            maxFieldAngle = 1.0; // Default 1 degree if no field
        }
        var chiefRay = _tracer.TraceChiefRay(splitSystem, maxFieldAngle, wavelength);

        const double rayMargin = 1.1; // 10% margin for ray clearance
        const double minEdgeClearance = 0.5; // Minimum edge clearance in mm

        // Get surface data
        var surf1 = splitSystem.Surfaces[frontSurfaceIndex];
        var surf2 = splitSystem.Surfaces[frontSurfaceIndex + 1];
        var surf3 = splitSystem.Surfaces[frontSurfaceIndex + 2];
        var surf4 = splitSystem.Surfaces[frontSurfaceIndex + 3];

        // Get thicknesses
        double t1 = surf1.Thickness; // First element center thickness
        double airGap = surf2.Thickness; // Air gap between elements
        double t2 = surf3.Thickness; // Second element center thickness

        // Calculate minimum diameters based on ray heights for each surface
        double[] minDiameters = new double[4];
        for (int i = 0; i < 4; i++)
        {
            int surfaceIndex = frontSurfaceIndex + i;
            double marginalHeight = 0;
            double chiefHeight = 0;

            if (surfaceIndex < marginalRay.States.Count)
            {
                marginalHeight = Math.Abs(marginalRay.States[surfaceIndex].Height);
            }
            if (surfaceIndex < chiefRay.States.Count)
            {
                chiefHeight = Math.Abs(chiefRay.States[surfaceIndex].Height);
            }

            // Full field ray bundle height
            minDiameters[i] = (marginalHeight + chiefHeight) * rayMargin;
            minDiameters[i] = Math.Max(1.0, minDiameters[i]); // Absolute minimum
        }

        // Calculate maximum diameters to prevent collision
        // For each pair of adjacent surfaces, find max diameter that maintains edge clearance

        // Surface 1-2 (first element): edge_clearance = t1 - sag1 + sag2 >= minEdgeClearance
        double maxDiam12 = CalculateMaxDiameterForClearance(surf1.Radius, surf2.Radius, t1, minEdgeClearance);

        // Surface 2-3 (air gap): edge_clearance = airGap - sag2 + sag3 >= minEdgeClearance
        double maxDiam23 = CalculateMaxDiameterForClearance(surf2.Radius, surf3.Radius, airGap, minEdgeClearance);

        // Surface 3-4 (second element): edge_clearance = t2 - sag3 + sag4 >= minEdgeClearance
        double maxDiam34 = CalculateMaxDiameterForClearance(surf3.Radius, surf4.Radius, t2, minEdgeClearance);

        // Apply constraints
        // Surface 1: limited by element 1 geometry
        double diam1 = Math.Min(Math.Max(minDiameters[0], 1.0), maxDiam12);

        // Surface 2: limited by element 1 and air gap geometry
        double diam2 = Math.Min(Math.Max(minDiameters[1], 1.0), Math.Min(maxDiam12, maxDiam23));

        // Surface 3: limited by air gap and element 2 geometry
        double diam3 = Math.Min(Math.Max(minDiameters[2], 1.0), Math.Min(maxDiam23, maxDiam34));

        // Surface 4: limited by element 2 geometry
        double diam4 = Math.Min(Math.Max(minDiameters[3], 1.0), maxDiam34);

        // Apply the calculated diameters
        surf1.SemiDiameter = diam1;
        surf2.SemiDiameter = diam2;
        surf3.SemiDiameter = diam3;
        surf4.SemiDiameter = diam4;
    }

    /// <summary>
    /// Calculates the maximum semi-diameter that maintains edge clearance between two surfaces.
    /// </summary>
    /// <param name="r1">Radius of first surface (can be infinite).</param>
    /// <param name="r2">Radius of second surface (can be infinite).</param>
    /// <param name="centerSeparation">Center-to-center separation.</param>
    /// <param name="minEdgeClearance">Minimum required edge clearance.</param>
    /// <returns>Maximum semi-diameter.</returns>
    private double CalculateMaxDiameterForClearance(double r1, double r2, double centerSeparation, double minEdgeClearance)
    {
        // Edge clearance = centerSeparation - sag1 + sag2
        // where sag = r - sign(r)*sqrt(r² - y²) for radius r and semi-diameter y
        // For small y: sag ≈ y²/(2r)

        // We need: centerSeparation - sag1 + sag2 >= minEdgeClearance
        // Using small angle approximation: centerSeparation - y²/(2r1) + y²/(2r2) >= minEdgeClearance
        // y² * (1/(2r2) - 1/(2r1)) >= minEdgeClearance - centerSeparation

        // If both radii are infinite (flat surfaces), no sag constraint
        if (double.IsInfinity(r1) && double.IsInfinity(r2))
        {
            return 100.0; // Large practical limit
        }

        // Available edge clearance at center
        double availableClearance = centerSeparation - minEdgeClearance;
        if (availableClearance <= 0)
        {
            return 1.0; // Minimum practical diameter
        }

        // Calculate the sag coefficient: how much edge clearance changes per y²
        // sag1 = y²/(2r1), sag2 = y²/(2r2) (positive r = center of curvature to the right)
        // For a surface curving "into" the gap, sag reduces edge clearance
        // For a surface curving "away" from the gap, sag increases edge clearance

        double c1 = double.IsInfinity(r1) ? 0 : 1.0 / (2.0 * r1);
        double c2 = double.IsInfinity(r2) ? 0 : 1.0 / (2.0 * r2);

        // Net sag effect: positive means clearance decreases with larger diameter
        // sag_net = sag1 - sag2 = y² * (c1 - c2)
        double sagCoeff = c1 - c2;

        if (sagCoeff <= 0)
        {
            // Sag increases clearance or has no effect - no upper limit from geometry
            return 100.0; // Large practical limit
        }

        // Solve: y² * sagCoeff <= availableClearance
        // y <= sqrt(availableClearance / sagCoeff)
        double maxY = Math.Sqrt(availableClearance / sagCoeff);

        // Apply practical limits
        maxY = Math.Max(1.0, Math.Min(100.0, maxY));

        return maxY;
    }

    private (double R1, double R2) SolveRadiiForPowerAndShape(double power, double shapeFactor, double n)
    {
        // From thin lens: P = (n-1)(c1 - c2)
        // From shape: X = (c1 + c2)/(c1 - c2)
        // Solving: c1 = (X+1)P / (2(n-1)), c2 = (X-1)P / (2(n-1))

        double factor = power / (2.0 * (n - 1.0));
        double c1 = (shapeFactor + 1.0) * factor;
        double c2 = (shapeFactor - 1.0) * factor;

        double r1 = Math.Abs(c1) > 1e-10 ? 1.0 / c1 : double.PositiveInfinity;
        double r2 = Math.Abs(c2) > 1e-10 ? 1.0 / c2 : double.PositiveInfinity;

        return (r1, r2);
    }

    /// <summary>
    /// Calculates the minimum air gap between split elements to prevent collision.
    /// Takes into account the sag of all four surfaces and neighboring elements.
    /// </summary>
    /// <param name="system">The original optical system.</param>
    /// <param name="elementIndex">Index of the element being split.</param>
    /// <param name="r1_first">Front radius of first split element.</param>
    /// <param name="r2_first">Rear radius of first split element.</param>
    /// <param name="r1_second">Front radius of second split element.</param>
    /// <param name="r2_second">Rear radius of second split element.</param>
    /// <param name="glassThickness">Center thickness of each split element.</param>
    /// <param name="minEdgeClearance">Minimum edge clearance required (mm).</param>
    /// <returns>Tuple of (minAirGap, minGlassThickness1, minGlassThickness2).</returns>
    private (double minAirGap, double minGlassThickness1, double minGlassThickness2) CalculateMinimumSeparations(
        OpticalSystem system,
        int elementIndex,
        double r1_first,
        double r2_first,
        double r1_second,
        double r2_second,
        double glassThickness,
        double minEdgeClearance = 0.5)
    {
        var elements = system.GetLensElements();
        var originalElement = elements[elementIndex];

        // Use the clear aperture of the original element
        double clearAperture = Math.Max(
            originalElement.FrontSurface.SemiDiameter,
            originalElement.RearSurface.SemiDiameter);

        if (clearAperture <= 0)
        {
            clearAperture = system.EntrancePupilDiameter / 2.0;
        }

        // Create temporary surfaces to calculate sag
        var surf1 = new Surface { Radius = r1_first, SemiDiameter = clearAperture };
        var surf2 = new Surface { Radius = r2_first, SemiDiameter = clearAperture };
        var surf3 = new Surface { Radius = r1_second, SemiDiameter = clearAperture };
        var surf4 = new Surface { Radius = r2_second, SemiDiameter = clearAperture };

        // Calculate minimum air gap between split elements (surface2 to surface3)
        // Edge clearance = centerGap - sag2 + sag3 >= minEdgeClearance
        // centerGap >= minEdgeClearance + sag2 - sag3
        double sag2 = surf2.GetSag(clearAperture);
        double sag3 = surf3.GetSag(clearAperture);
        double minAirGapBetweenSplits = minEdgeClearance + sag2 - sag3;

        // Calculate minimum glass thickness for first element (surface1 to surface2)
        double sag1 = surf1.GetSag(clearAperture);
        double minGlassThickness1 = minEdgeClearance + sag1 - sag2;

        // Calculate minimum glass thickness for second element (surface3 to surface4)
        double sag4 = surf4.GetSag(clearAperture);
        double minGlassThickness2 = minEdgeClearance + sag3 - sag4;

        // Check collision with preceding element (if any)
        double minGapFromPreceding = 0;
        if (elementIndex > 0)
        {
            var precedingElement = elements[elementIndex - 1];
            double sagPrecedingRear = precedingElement.RearSurface.GetSag(clearAperture);
            // The gap before our element is precedingElement.RearSurface.Thickness
            // We need: precedingRearThickness - sagPrecedingRear + sag1 >= minEdgeClearance
            // This affects whether we can place our first surface where planned
            double requiredGapFromPreceding = minEdgeClearance + sagPrecedingRear - sag1;
            minGapFromPreceding = Math.Max(0, requiredGapFromPreceding - precedingElement.RearSurface.Thickness);
        }

        // Check collision with following element (if any)
        double minGapToFollowing = 0;
        if (elementIndex < elements.Count - 1)
        {
            var followingElement = elements[elementIndex + 1];
            double sagFollowingFront = followingElement.FrontSurface.GetSag(clearAperture);
            // We need space after surf4 to not collide with following element
            // adjustedSpaceAfter - sag4 + sagFollowingFront >= minEdgeClearance
            double requiredGapToFollowing = minEdgeClearance + sag4 - sagFollowingFront;
            minGapToFollowing = Math.Max(0, requiredGapToFollowing);
        }

        // The overall minimum air gap must satisfy all constraints
        double finalMinAirGap = Math.Max(0.1, minAirGapBetweenSplits);

        // Ensure glass thicknesses are at least the minimum required
        double finalMinGlass1 = Math.Max(1.0, minGlassThickness1);
        double finalMinGlass2 = Math.Max(1.0, minGlassThickness2);

        return (finalMinAirGap, finalMinGlass1, finalMinGlass2);
    }

    /// <summary>
    /// Calculates the required clear aperture (semi-diameter) at an element by tracing rays
    /// through the original system. This ensures the split elements can pass all rays.
    /// </summary>
    /// <param name="system">The original optical system.</param>
    /// <param name="elementIndex">Index of the element being split.</param>
    /// <param name="wavelength">Wavelength for ray tracing.</param>
    /// <returns>Required semi-diameter to pass all rays with margin.</returns>
    private double CalculateRequiredClearAperture(OpticalSystem system, int elementIndex, double wavelength)
    {
        const double rayMargin = 1.15; // 15% margin for ray clearance

        var elements = system.GetLensElements();
        var element = elements[elementIndex];
        int frontSurfaceIndex = element.FrontSurface.Index;
        int rearSurfaceIndex = element.RearSurface.Index;

        // Trace marginal ray
        var marginalRay = _tracer.TraceMarginalRay(system, wavelength);

        // Get maximum field for chief ray
        double maxFieldValue = 0;
        if (system.Fields.Count > 0)
        {
            maxFieldValue = system.Fields.Max(f => Math.Abs(f.Y));
        }
        if (maxFieldValue < 0.001)
        {
            maxFieldValue = 1.0; // Default if no field defined
        }

        // Trace chief ray at maximum field
        var chiefRay = _tracer.TraceChiefRay(system, maxFieldValue, wavelength);

        // Find maximum ray bundle height at the element surfaces
        double maxHeight = 0;

        // Check front and rear surfaces of the element
        for (int surfIdx = frontSurfaceIndex; surfIdx <= rearSurfaceIndex; surfIdx++)
        {
            double marginalHeight = 0;
            double chiefHeight = 0;

            if (surfIdx < marginalRay.States.Count)
            {
                marginalHeight = Math.Abs(marginalRay.States[surfIdx].Height);
            }
            if (surfIdx < chiefRay.States.Count)
            {
                chiefHeight = Math.Abs(chiefRay.States[surfIdx].Height);
            }

            // Full ray bundle = marginal + chief (for off-axis field point)
            double bundleHeight = marginalHeight + chiefHeight;
            maxHeight = Math.Max(maxHeight, bundleHeight);
        }

        // Apply margin and ensure minimum
        double requiredAperture = maxHeight * rayMargin;
        requiredAperture = Math.Max(requiredAperture, 1.0); // Absolute minimum 1mm

        // Also consider the original element diameter as a reference
        double originalDiameter = Math.Max(
            element.FrontSurface.SemiDiameter,
            element.RearSurface.SemiDiameter);

        // Use the larger of ray-based or original diameter
        return Math.Max(requiredAperture, originalDiameter);
    }

    /// <summary>
    /// Calculates minimum separations (air gap, glass thicknesses) for a given clear aperture.
    /// This ensures the split geometry can accommodate the required ray bundle.
    /// </summary>
    private (double minAirGap, double minGlassThickness1, double minGlassThickness2) CalculateMinimumSeparationsForAperture(
        double r1_first, double r2_first, double r1_second, double r2_second,
        double clearAperture, double minEdgeClearance)
    {
        // Create temporary surfaces to calculate sag at the required aperture
        var surf1 = new Surface { Radius = r1_first, SemiDiameter = clearAperture };
        var surf2 = new Surface { Radius = r2_first, SemiDiameter = clearAperture };
        var surf3 = new Surface { Radius = r1_second, SemiDiameter = clearAperture };
        var surf4 = new Surface { Radius = r2_second, SemiDiameter = clearAperture };

        // Calculate sag at the clear aperture for each surface
        double sag1 = surf1.GetSag(clearAperture);
        double sag2 = surf2.GetSag(clearAperture);
        double sag3 = surf3.GetSag(clearAperture);
        double sag4 = surf4.GetSag(clearAperture);

        // Minimum glass thickness for first element: edge_thickness >= minEdgeClearance
        // edge_thickness = center_thickness - sag1 + sag2
        // center_thickness >= minEdgeClearance + sag1 - sag2
        double minGlassThickness1 = minEdgeClearance + sag1 - sag2;

        // Minimum glass thickness for second element
        double minGlassThickness2 = minEdgeClearance + sag3 - sag4;

        // Minimum air gap between elements: edge_clearance >= minEdgeClearance
        // edge_clearance = air_gap - sag2 + sag3
        // air_gap >= minEdgeClearance + sag2 - sag3
        double minAirGap = minEdgeClearance + sag2 - sag3;

        // Ensure minimum values
        double finalMinAirGap = Math.Max(0.1, minAirGap);
        double finalMinGlass1 = Math.Max(1.0, minGlassThickness1);
        double finalMinGlass2 = Math.Max(1.0, minGlassThickness2);

        return (finalMinAirGap, finalMinGlass1, finalMinGlass2);
    }

    /// <summary>
    /// Calculates and reports the minimum air gap needed for given split configuration.
    /// </summary>
    public double CalculateMinimumAirGap(
        OpticalSystem system,
        int elementIndex,
        double powerRatio,
        double minEdgeClearance = 0.5)
    {
        var elements = system.GetLensElements();
        var originalElement = elements[elementIndex];
        var wavelength = system.PrimaryWavelength.ValueMicrons;
        var glass = originalElement.Glass;
        double n = glass.GetRefractiveIndex(wavelength);
        double originalPower = originalElement.GetThickLensPower(wavelength);

        // Calculate split element powers
        double phi1 = originalPower * powerRatio;
        double phi2 = originalPower * (1 - powerRatio);

        // Calculate optimal shapes
        double X1 = AberrationCalculator.CalculateOptimalShapeFactor(n, -1.0);
        double X2 = AberrationCalculator.CalculateOptimalShapeFactor(n, -1.0);

        // Solve for radii
        var (r1_first, r2_first) = SolveRadiiForPowerAndShape(phi1, X1, n);
        var (r1_second, r2_second) = SolveRadiiForPowerAndShape(phi2, X2, n);

        // Calculate minimum separations
        var (minAirGap, _, _) = CalculateMinimumSeparations(
            system, elementIndex,
            r1_first, r2_first, r1_second, r2_second,
            1.0, minEdgeClearance);

        return minAirGap;
    }

    /// <summary>
    /// Analyzes how S₁ varies with power ratio for visualization/understanding.
    /// </summary>
    public List<SplitAberrationResult> AnalyzePowerRatioSweep(
        double totalPower,
        double refractiveIndex,
        double entrancePupilRadius,
        double airGap,
        int numPoints = 50)
    {
        var results = new List<SplitAberrationResult>();

        for (int i = 0; i <= numPoints; i++)
        {
            double ratio = 0.1 + 0.8 * i / numPoints; // 0.1 to 0.9
            var result = _aberrationCalc.EvaluateSplitConfiguration(
                totalPower, ratio, airGap, refractiveIndex, entrancePupilRadius);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Analyzes how S₁ varies with air gap for visualization/understanding.
    /// </summary>
    public List<SplitAberrationResult> AnalyzeAirGapSweep(
        double totalPower,
        double refractiveIndex,
        double entrancePupilRadius,
        double powerRatio,
        double maxGap = 10.0,
        int numPoints = 50)
    {
        var results = new List<SplitAberrationResult>();

        for (int i = 0; i <= numPoints; i++)
        {
            double gap = 0.1 + (maxGap - 0.1) * i / numPoints;
            var result = _aberrationCalc.EvaluateSplitConfiguration(
                totalPower, powerRatio, gap, refractiveIndex, entrancePupilRadius);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Maximum air gap (mm) to consider elements as part of an air-spaced group.
    /// </summary>
    private const double MaxAirSpacedGroupGap = 2.0;

    /// <summary>
    /// Maximum thickness to consider a cemented interface (mm).
    /// </summary>
    private const double MaxCementedGap = 0.01;

    /// <summary>
    /// Analyzes all elements in the system to determine their S₁ contributions.
    /// Automatically detects finite vs infinite conjugate.
    /// </summary>
    /// <param name="system">The optical system to analyze.</param>
    /// <returns>Analysis results for each element, sorted by splitting priority.</returns>
    public ElementAnalysisResult AnalyzeElements(OpticalSystem system)
    {
        var elements = system.GetLensElements();
        var wavelength = system.PrimaryWavelength.ValueMicrons;
        var marginalRay = _tracer.TraceMarginalRay(system, wavelength);

        // Detect conjugate condition
        bool isFiniteConjugate = AberrationCalculator.IsFiniteConjugate(system);
        double objectDistance = AberrationCalculator.GetObjectDistance(system);

        // Detect element groups (doublets, triplets)
        var groupInfo = DetectElementGroups(elements);

        // Use full Seidel ray-traced calculation to get per-surface aberration coefficients.
        // This gives accurate S1, S2, S3 per surface, which we sum per element.
        // This replaces the thin-lens S1-only approximation for element scoring.
        SeidelResult? seidelResult = null;
        Dictionary<int, SeidelSurfaceCoefficients>? surfaceCoeffs = null;
        try
        {
            seidelResult = _seidelCalc.Calculate(system, wavelength);
            if (seidelResult.SurfaceCoefficients.Count > 0)
            {
                surfaceCoeffs = seidelResult.SurfaceCoefficients
                    .ToDictionary(c => c.SurfaceIndex);
            }
        }
        catch
        {
            // Fall back to thin-lens approximation if Seidel calculation fails
        }

        var analyses = new List<ElementAberrationAnalysis>();
        double totalS1 = 0;
        double totalS2 = 0;
        double totalS3 = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            double n = element.Glass.GetRefractiveIndex(wavelength);
            double power = element.GetThickLensPower(wavelength);

            // Get ray height at this element (approximate using front surface)
            double rayHeight = system.EntrancePupilDiameter / 2.0;
            if (marginalRay.States.Count > element.FrontSurface.Index)
            {
                rayHeight = Math.Abs(marginalRay.States[element.FrontSurface.Index].Height);
            }

            // Check for conic surfaces
            double conic1 = element.FrontSurface.Conic;
            double conic2 = element.RearSurface.Conic;
            bool hasConic = Math.Abs(conic1) > 1e-10 || Math.Abs(conic2) > 1e-10;

            double S1, S2, S3;

            // Try to get per-element aberrations from full Seidel ray trace
            // by summing the contributions from both surfaces of this element
            if (surfaceCoeffs != null)
            {
                S1 = 0; S2 = 0; S3 = 0;
                int frontIdx = element.FrontSurface.Index;
                int rearIdx = element.RearSurface.Index;
                if (surfaceCoeffs.TryGetValue(frontIdx, out var frontCoeffs))
                {
                    S1 += frontCoeffs.S1;
                    S2 += frontCoeffs.S2;
                    S3 += frontCoeffs.S3;
                }
                if (surfaceCoeffs.TryGetValue(rearIdx, out var rearCoeffs))
                {
                    S1 += rearCoeffs.S1;
                    S2 += rearCoeffs.S2;
                    S3 += rearCoeffs.S3;
                }
            }
            else
            {
                // Fallback: thin-lens S1 approximation (no S2/S3 available)
                double Y;
                if (i == 0 && isFiniteConjugate)
                {
                    double imageDistance = 1.0 / (power - 1.0 / objectDistance);
                    Y = AberrationCalculator.CalculatePositionFactor(objectDistance, imageDistance);
                }
                else if (i == 0)
                {
                    Y = -1.0;
                }
                else
                {
                    Y = -1.0 + 0.1 * i;
                }

                double X_actual;
                double r1 = element.R1;
                double r2 = element.R2;
                if (Math.Abs(r2 - r1) > 1e-10)
                    X_actual = (r2 + r1) / (r2 - r1);
                else
                    X_actual = 0;

                if (hasConic)
                {
                    S1 = AberrationCalculator.CalculateS1WithConic(
                        rayHeight, power, n, X_actual, Y, r1, r2, conic1, conic2);
                }
                else
                {
                    S1 = AberrationCalculator.CalculateS1ThinLens(rayHeight, power, n, X_actual, Y);
                }
                // Negate to match Seidel convention (S1 > 0 = undercorrected).
                // The thin-lens formula uses opposite sign convention (S1 < 0 = undercorrected).
                S1 = -S1;
                S2 = 0;
                S3 = 0;
            }

            // Get group information for this element
            var (isPartOfCemented, isPartOfAirSpaced, groupDesc) = groupInfo.ContainsKey(i)
                ? groupInfo[i]
                : (false, false, (string?)null);

            analyses.Add(new ElementAberrationAnalysis
            {
                ElementIndex = i,
                Element = element,
                Power = power,
                RayHeight = rayHeight,
                RefractiveIndex = n,
                S1Contribution = S1,
                S2Contribution = S2,
                S3Contribution = S3,
                IsPositive = power > 0,
                HasConicSurfaces = hasConic,
                FrontConic = conic1,
                RearConic = conic2,
                IsPartOfCementedGroup = isPartOfCemented,
                IsPartOfAirSpacedGroup = isPartOfAirSpaced,
                GroupDescription = groupDesc
            });

            if (double.IsFinite(S1)) totalS1 += S1;
            if (double.IsFinite(S2)) totalS2 += S2;
            if (double.IsFinite(S3)) totalS3 += S3;
        }

        // Get aberration weights for priority scoring - use same weights as merit function
        var weights = _currentSettings?.AberrationWeights ?? AberrationWeights.Default;

        // Calculate percentages and determine splitting priority
        foreach (var analysis in analyses)
        {
            analysis.S1Percentage = Math.Abs(totalS1) > 1e-20
                ? (analysis.S1Contribution / totalS1) * 100.0
                : 0;
            analysis.S2Percentage = Math.Abs(totalS2) > 1e-20
                ? (analysis.S2Contribution / totalS2) * 100.0
                : 0;
            analysis.S3Percentage = Math.Abs(totalS3) > 1e-20
                ? (analysis.S3Contribution / totalS3) * 100.0
                : 0;

            // Never split elements with conic surfaces - they're already optimized
            if (analysis.HasConicSurfaces)
            {
                analysis.SplittingPriority = double.NegativeInfinity;
                continue;
            }

            // Never split elements that are part of cemented or air-spaced groups
            // These are designed as corrected pairs/triplets
            if (analysis.IsPartOfGroup)
            {
                analysis.SplittingPriority = double.NegativeInfinity;
                continue;
            }

            // Never split negative power lenses - power-preserving splitting is designed
            // for positive lenses. Negative lenses contribute overcorrected SA that
            // typically corrects the undercorrected SA from positive lenses.
            if (!analysis.IsPositive)
            {
                analysis.SplittingPriority = double.NegativeInfinity;
                continue;
            }

            // Compute priority using weighted combination of S1, S2, S3 contributions.
            // This mirrors the optimization merit function weights, ensuring the element
            // selected for splitting is the one whose splitting most improves the
            // overall system performance - not just spherical aberration alone.
            double basePriority = weights.W1 * Math.Abs(analysis.S1Contribution) +
                                  weights.W2 * Math.Abs(analysis.S2Contribution) +
                                  weights.W3 * Math.Abs(analysis.S3Contribution);

            // Boost priority if S1 sign matches total (main contributor to dominant aberration)
            bool sameSignAsTotal = (analysis.S1Contribution > 0) == (totalS1 > 0);
            if (sameSignAsTotal)
            {
                analysis.SplittingPriority = basePriority * 1.2;
            }
            else
            {
                analysis.SplittingPriority = basePriority;
            }
        }

        // Sort by splitting priority (highest first)
        var sortedAnalyses = analyses.OrderByDescending(a => a.SplittingPriority).ToList();

        // Determine recommended element - must be splittable (positive priority)
        var splittableElements = sortedAnalyses.Where(a => a.SplittingPriority > 0).ToList();
        int recommendedIndex = splittableElements.Count > 0
            ? splittableElements[0].ElementIndex
            : -1; // -1 indicates no splittable elements

        return new ElementAnalysisResult
        {
            Elements = analyses,
            SortedByPriority = sortedAnalyses,
            TotalS1 = totalS1,
            TotalS2 = totalS2,
            TotalS3 = totalS3,
            RecommendedElementIndex = recommendedIndex,
            RecommendedElement = recommendedIndex >= 0 && elements.Count > recommendedIndex
                ? elements[recommendedIndex]
                : null,
            IsFiniteConjugate = isFiniteConjugate,
            ObjectDistance = objectDistance,
            HasSplittableElements = splittableElements.Count > 0
        };
    }

    /// <summary>
    /// Detects cemented and air-spaced element groups (doublets, triplets).
    /// </summary>
    /// <param name="elements">The list of lens elements.</param>
    /// <returns>Dictionary mapping element index to group info (isCemented, isAirSpaced, description).</returns>
    private Dictionary<int, (bool isCemented, bool isAirSpaced, string? description)> DetectElementGroups(
        List<LensElement> elements)
    {
        var result = new Dictionary<int, (bool, bool, string?)>();
        if (elements.Count < 2) return result;

        // Track which elements are part of groups
        var cementedGroups = new List<List<int>>();
        var airSpacedGroups = new List<List<int>>();

        // First pass: detect cemented interfaces
        for (int i = 0; i < elements.Count - 1; i++)
        {
            var current = elements[i];
            var next = elements[i + 1];

            // Check for cemented interface using multiple criteria:
            // 1. The air gap between elements is essentially zero, OR
            // 2. The rear surface of current element has glass (not air) - this means
            //    it's a cement interface where the next glass starts immediately
            double gapBetween = current.RearSurface.Thickness;
            bool rearSurfaceHasGlass = current.RearSurface.Glass != null &&
                                       !string.IsNullOrEmpty(current.RearSurface.GlassName) &&
                                       current.RearSurface.GlassName != "AIR";

            // Also check if rear surface of current shares radius with front surface of next
            // (common cement interface)
            bool sharedRadius = Math.Abs(current.RearSurface.Radius - next.FrontSurface.Radius) < 0.001 ||
                               (double.IsInfinity(current.RearSurface.Radius) && double.IsInfinity(next.FrontSurface.Radius));

            bool isCemented = Math.Abs(gapBetween) < MaxCementedGap ||
                             rearSurfaceHasGlass ||
                             (sharedRadius && gapBetween < 0.5);

            if (isCemented)
            {
                // This is a cemented interface
                // Find or create a cemented group
                var existingGroup = cementedGroups.FirstOrDefault(g => g.Contains(i));
                if (existingGroup != null)
                {
                    if (!existingGroup.Contains(i + 1))
                        existingGroup.Add(i + 1);
                }
                else
                {
                    cementedGroups.Add(new List<int> { i, i + 1 });
                }
            }
        }

        // Second pass: detect air-spaced groups (elements with small air gaps that likely form pairs)
        // Only check elements not already in cemented groups
        var cementedElements = new HashSet<int>(cementedGroups.SelectMany(g => g));

        for (int i = 0; i < elements.Count - 1; i++)
        {
            if (cementedElements.Contains(i) || cementedElements.Contains(i + 1))
                continue;

            var current = elements[i];
            var next = elements[i + 1];

            double gapBetween = current.RearSurface.Thickness;

            // Check if this is a small air gap suggesting a designed achromatic pair
            // Must have BOTH opposite sign powers AND significantly different dispersion
            // to be considered an air-spaced achromat. This is more conservative to avoid
            // incorrectly grouping triplet designs like Cooke triplet.
            if (gapBetween > MaxCementedGap && gapBetween < MaxAirSpacedGroupGap)
            {
                double power1 = current.GetThinLensPower(0.5876);
                double power2 = next.GetThinLensPower(0.5876);

                // Must have opposite sign powers (one positive, one negative)
                bool oppositeSign = (power1 > 0) != (power2 > 0);

                // Must have significantly different Abbe numbers (chromatic correction intent)
                double v1 = current.Glass?.Vd ?? 50;
                double v2 = next.Glass?.Vd ?? 50;
                bool differentDispersion = Math.Abs(v1 - v2) > 15; // Increased threshold

                // Only consider it an air-spaced achromat if BOTH conditions are met
                // This prevents incorrectly grouping Cooke triplets and similar designs
                if (oppositeSign && differentDispersion)
                {
                    var existingGroup = airSpacedGroups.FirstOrDefault(g => g.Contains(i));
                    if (existingGroup != null)
                    {
                        if (!existingGroup.Contains(i + 1))
                            existingGroup.Add(i + 1);
                    }
                    else
                    {
                        airSpacedGroups.Add(new List<int> { i, i + 1 });
                    }
                }
            }
        }

        // Build result dictionary
        foreach (var group in cementedGroups)
        {
            string desc = group.Count == 2 ? "Cemented doublet" :
                         group.Count == 3 ? "Cemented triplet" :
                         $"Cemented group ({group.Count} elements)";

            foreach (var idx in group)
            {
                result[idx] = (true, false, desc);
            }
        }

        foreach (var group in airSpacedGroups)
        {
            string desc = group.Count == 2 ? "Air-spaced doublet" :
                         group.Count == 3 ? "Air-spaced triplet" :
                         $"Air-spaced group ({group.Count} elements)";

            foreach (var idx in group)
            {
                // Don't overwrite cemented designation
                if (!result.ContainsKey(idx))
                {
                    result[idx] = (false, true, desc);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the best element to split using weighted Seidel aberration scoring (S₁, S₂, S₃).
    /// Scoring uses the same aberration weights as the optimization merit function to ensure
    /// the selected element is the one whose splitting most improves overall system performance.
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <returns>Index of the recommended element to split.</returns>
    public int FindBestElementToSplit(OpticalSystem system)
    {
        return AnalyzeElements(system).RecommendedElementIndex;
    }

    /// <summary>
    /// Performs iterative splitting on the best element (auto-selected).
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <param name="settings">Optimization settings.</param>
    /// <returns>The optimized split result.</returns>
    public IterativeSplitResult SplitBestElement(OpticalSystem system, OptimizationSettings? settings = null)
    {
        int bestElement = FindBestElementToSplit(system);
        return SplitWithOptimization(system, bestElement, settings);
    }
}

/// <summary>
/// Analysis of a single element's aberration contribution.
/// </summary>
public class ElementAberrationAnalysis
{
    public int ElementIndex { get; set; }
    public LensElement Element { get; set; } = null!;
    public double Power { get; set; }
    public double RayHeight { get; set; }
    public double RefractiveIndex { get; set; }
    public double S1Contribution { get; set; }
    public double S2Contribution { get; set; }
    public double S3Contribution { get; set; }
    public double S1Percentage { get; set; }
    public double S2Percentage { get; set; }
    public double S3Percentage { get; set; }
    public bool IsPositive { get; set; }
    public double SplittingPriority { get; set; }

    /// <summary>
    /// Whether this element has conic (aspheric) surfaces.
    /// </summary>
    public bool HasConicSurfaces { get; set; }

    /// <summary>
    /// Front surface conic constant (K=0 sphere, K=-1 parabola).
    /// </summary>
    public double FrontConic { get; set; }

    /// <summary>
    /// Rear surface conic constant.
    /// </summary>
    public double RearConic { get; set; }

    /// <summary>
    /// Whether this element is part of a cemented group (doublet, triplet).
    /// </summary>
    public bool IsPartOfCementedGroup { get; set; }

    /// <summary>
    /// Whether this element is part of an air-spaced doublet/triplet.
    /// </summary>
    public bool IsPartOfAirSpacedGroup { get; set; }

    /// <summary>
    /// Group type description (e.g., "Cemented doublet", "Air-spaced triplet").
    /// </summary>
    public string? GroupDescription { get; set; }

    /// <summary>
    /// Whether this element is part of any multi-element group.
    /// </summary>
    public bool IsPartOfGroup => IsPartOfCementedGroup || IsPartOfAirSpacedGroup;

    /// <summary>
    /// Whether this element contributes undercorrected spherical aberration (S₁ &gt; 0).
    /// Uses Seidel sign convention where positive S₁ = undercorrected (marginal ray
    /// focuses behind the paraxial focus), matching ZEMAX convention.
    /// </summary>
    public bool IsUndercorrected => S1Contribution > 0;

    /// <summary>
    /// Whether splitting this element is recommended.
    /// Elements with conic surfaces, part of groups, or negative power are never recommended for splitting.
    /// </summary>
    public bool RecommendedForSplitting => IsPositive && !HasConicSurfaces && !IsPartOfGroup && SplittingPriority > 0;

    public override string ToString()
    {
        string sign = IsPositive ? "+" : "-";
        string correction = IsUndercorrected ? "undercorrected" : "overcorrected";  // Seidel convention: S1>0 = undercorrected
        string recommend = "";
        if (HasConicSurfaces)
        {
            recommend = " [ASPHERIC - do not split]";
        }
        else if (IsPartOfGroup)
        {
            recommend = $" [{GroupDescription?.ToUpper()} - do not split]";
        }
        else if (!IsPositive)
        {
            recommend = " [NEGATIVE - do not split]";
        }
        else if (RecommendedForSplitting)
        {
            recommend = " [Recommended]";
        }
        string conicInfo = HasConicSurfaces ? $" K1={FrontConic:F3}, K2={RearConic:F3}," : "";
        return $"Element {ElementIndex + 1}: P={Power:F6} ({sign}), y={RayHeight:F2}mm,{conicInfo} " +
               $"S1={S1Contribution:E3} ({correction}, {Math.Abs(S1Percentage):F1}%), " +
               $"S2={S2Contribution:E3} ({Math.Abs(S2Percentage):F1}%){recommend}";
    }
}

/// <summary>
/// Result of analyzing all elements in a system.
/// </summary>
public class ElementAnalysisResult
{
    public List<ElementAberrationAnalysis> Elements { get; set; } = new();
    public List<ElementAberrationAnalysis> SortedByPriority { get; set; } = new();
    public double TotalS1 { get; set; }
    public double TotalS2 { get; set; }
    public double TotalS3 { get; set; }

    /// <summary>
    /// Index of the recommended element to split (-1 if no splittable elements).
    /// </summary>
    public int RecommendedElementIndex { get; set; }
    public LensElement? RecommendedElement { get; set; }

    /// <summary>
    /// Whether there are any elements that can be split (positive power, not in groups, no conics).
    /// </summary>
    public bool HasSplittableElements { get; set; }

    /// <summary>
    /// Whether the system has finite conjugates (object at finite distance).
    /// </summary>
    public bool IsFiniteConjugate { get; set; }

    /// <summary>
    /// Object distance (negative for real object). Negative infinity for infinite conjugate.
    /// </summary>
    public double ObjectDistance { get; set; }

    /// <summary>
    /// Conjugate description for display.
    /// </summary>
    public string ConjugateDescription => IsFiniteConjugate
        ? $"Finite conjugate (object at {-ObjectDistance:F1}mm)"
        : "Infinite conjugate";

    public string Summary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Conjugate: {ConjugateDescription}");
            sb.AppendLine($"Total system S₁: {TotalS1:E4} ({(TotalS1 > 0 ? "undercorrected" : "overcorrected")})");
            sb.AppendLine($"Total system S₂: {TotalS2:E4} (coma)");
            sb.AppendLine($"Total system S₃: {TotalS3:E4} (astigmatism)");
            sb.AppendLine();
            sb.AppendLine("Element Analysis (by splitting priority - weighted S₁+S₂+S₃):");
            foreach (var elem in SortedByPriority)
            {
                sb.AppendLine($"  {elem}");
            }
            sb.AppendLine();
            sb.AppendLine($"Recommended element to split: {RecommendedElementIndex}");
            return sb.ToString();
        }
    }
}

/// <summary>
/// Extended result from iterative power-preserving splitting.
/// </summary>
public class IterativeSplitResult
{
    public OpticalSystem OriginalSystem { get; set; } = null!;
    public OpticalSystem SplitSystem { get; set; } = null!;
    public int SplitElementIndex { get; set; }
    public LensElement OriginalElement { get; set; } = null!;
    public double OriginalPower { get; set; }

    /// <summary>
    /// S₁ for single lens at optimal shape (baseline for comparison).
    /// </summary>
    public double SingleLensS1 { get; set; }

    /// <summary>
    /// Optimized split configuration.
    /// </summary>
    public SplitAberrationResult OptimizedResult { get; set; } = null!;

    /// <summary>
    /// S₁ reduction factor: |S1_single| / |S1_split|.
    /// Values > 1 indicate improvement.
    /// </summary>
    public double S1ReductionFactor { get; set; }

    public ParaxialRay? OriginalMarginalRay { get; set; }
    public ParaxialRay? OriginalChiefRay { get; set; }
    public ParaxialRay? SplitMarginalRay { get; set; }
    public ParaxialRay? SplitChiefRay { get; set; }
    public double OriginalEfl { get; set; }
    public double SplitEfl { get; set; }

    /// <summary>
    /// Full Seidel aberrations of the original system (from ray tracing).
    /// </summary>
    public SeidelResult? OriginalSeidel { get; set; }

    /// <summary>
    /// Full Seidel aberrations of the split system (from ray tracing).
    /// </summary>
    public SeidelResult? SplitSeidel { get; set; }

    /// <summary>
    /// Merit function value for original system.
    /// </summary>
    public double OriginalMeritFunction { get; set; }

    /// <summary>
    /// Merit function value for split system.
    /// </summary>
    public double SplitMeritFunction { get; set; }

    /// <summary>
    /// Merit function improvement factor: Original / Split.
    /// Values > 1 indicate improvement.
    /// </summary>
    public double MeritFunctionImprovement =>
        Math.Abs(SplitMeritFunction) > 1e-20 ? OriginalMeritFunction / SplitMeritFunction : 1.0;

    /// <summary>
    /// Whether the split improved the overall merit function.
    /// </summary>
    public bool SplitImprovedMeritFunction => MeritFunctionImprovement > 1.0;

    /// <summary>
    /// Warning message if split made things worse.
    /// </summary>
    public string? MeritFunctionWarning => !SplitImprovedMeritFunction
        ? $"WARNING: Splitting made overall aberrations worse ({MeritFunctionImprovement:F2}x). " +
          $"The best configuration found still degrades the merit function from {OriginalMeritFunction:E3} to {SplitMeritFunction:E3}. " +
          "Consider not splitting this element, or try different glass types."
        : null;

    /// <summary>
    /// History of configurations evaluated during optimization.
    /// </summary>
    public List<SplitAberrationResult> IterationHistory { get; set; } = new();

    /// <summary>
    /// Minimum air gap required based on surface geometry (sag) to prevent lens collision.
    /// </summary>
    public double GeometryMinAirGap { get; set; }

    /// <summary>
    /// Whether the geometry constraint could not be fully satisfied (potential collision).
    /// </summary>
    public bool HasGeometryWarning { get; set; }

    /// <summary>
    /// Warning message if geometry constraint could not be satisfied.
    /// </summary>
    public string? GeometryWarning { get; set; }

    /// <summary>
    /// Gets the optimal power ratio (φ₁/φ_total).
    /// </summary>
    public double OptimalPowerRatio => OptimizedResult.PowerRatio;

    /// <summary>
    /// Gets the optimal air gap (from optimization, may differ from actual).
    /// </summary>
    public double OptimalAirGap => OptimizedResult.AirGap;

    /// <summary>
    /// Gets the actual air gap used in the split system (from surface thickness).
    /// </summary>
    public double ActualAirGap
    {
        get
        {
            // The air gap is the thickness of the rear surface of the first split element
            // which is the surface at index (original front index + 1)
            var elements = OriginalSystem.GetLensElements();
            if (SplitElementIndex >= elements.Count) return OptimalAirGap;

            var originalElement = elements[SplitElementIndex];
            int frontSurfaceIndex = originalElement.FrontSurface.Index;

            // In split system, surface at frontSurfaceIndex+1 has the air gap thickness
            if (SplitSystem.Surfaces.Count > frontSurfaceIndex + 1)
            {
                return SplitSystem.Surfaces[frontSurfaceIndex + 1].Thickness;
            }
            return OptimalAirGap;
        }
    }

    public string Summary =>
        $"Split element {SplitElementIndex}: " +
        $"Ratio={OptimalPowerRatio:F3}, Gap={OptimalAirGap:F2}mm, " +
        $"S1_single={SingleLensS1:E3}, S1_split={OptimizedResult.S1_Total:E3}, " +
        $"Reduction={S1ReductionFactor:F2}x";

    /// <summary>
    /// Gets a detailed summary including all Seidel aberrations.
    /// </summary>
    public string DetailedSummary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Split element {SplitElementIndex}:");
            sb.AppendLine($"  Power ratio: {OptimalPowerRatio:F3}");
            sb.AppendLine($"  Air gap: {ActualAirGap:F2}mm (requested: {OptimalAirGap:F2}mm)");
            sb.AppendLine($"  EFL: {OriginalEfl:F2}mm → {SplitEfl:F2}mm");
            sb.AppendLine();

            if (OriginalSeidel != null && SplitSeidel != null)
            {
                sb.AppendLine("  Seidel Aberrations (Original → Split):");
                sb.AppendLine($"    S1 (Spherical):  {OriginalSeidel.S1:E3} → {SplitSeidel.S1:E3}");
                sb.AppendLine($"    S2 (Coma):       {OriginalSeidel.S2:E3} → {SplitSeidel.S2:E3}");
                sb.AppendLine($"    S3 (Astigmatism):{OriginalSeidel.S3:E3} → {SplitSeidel.S3:E3}");
                sb.AppendLine($"    S4 (Petzval):    {OriginalSeidel.S4:E3} → {SplitSeidel.S4:E3}");
                sb.AppendLine($"    S5 (Distortion): {OriginalSeidel.S5:E3} → {SplitSeidel.S5:E3}");
                sb.AppendLine();
                sb.AppendLine($"  Merit Function: {OriginalMeritFunction:E3} → {SplitMeritFunction:E3}");
                sb.AppendLine($"  Improvement: {MeritFunctionImprovement:F2}x");
            }
            else
            {
                sb.AppendLine($"  S1 reduction: {S1ReductionFactor:F2}x");
            }

            return sb.ToString();
        }
    }
}
