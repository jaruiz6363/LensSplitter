using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Implements power-preserving splitting for lens elements to reduce spherical aberration.
/// </summary>
public class ElementSplitter
{
    private readonly ParaxialRayTracer _tracer = new();

    /// <summary>
    /// Splits a lens element using power-preserving splitting.
    /// The element is split into two elements of equal power, which theoretically
    /// reduces spherical aberration by a factor of 4.
    /// </summary>
    /// <param name="system">The optical system containing the element to split.</param>
    /// <param name="elementIndex">The index of the element to split (0-based).</param>
    /// <param name="airGap">The air gap between the split elements in mm.</param>
    /// <returns>The split result containing original and modified systems.</returns>
    public SplitResult Split(OpticalSystem system, int elementIndex, double airGap = 0.1)
    {
        var elements = system.GetLensElements();

        if (elementIndex < 0 || elementIndex >= elements.Count)
        {
            throw new ArgumentException($"Element index {elementIndex} is out of range. System has {elements.Count} elements.");
        }

        var originalElement = elements[elementIndex];
        var wavelength = system.PrimaryWavelength.ValueMicrons;

        // Calculate original element power
        double originalPower = originalElement.GetThickLensPower(wavelength);

        // Check for negative power lenses
        if (originalPower < 0)
        {
            throw new InvalidOperationException(
                $"Element {elementIndex} has negative power ({originalPower:F6}). " +
                $"Power-preserving splitting is designed for positive lenses. " +
                $"Negative lenses contribute overcorrected spherical aberration that typically " +
                $"corrects the undercorrected SA from positive lenses. Splitting a negative lens " +
                $"may reduce its beneficial correction and make overall system aberration worse.");
        }

        double targetPower = originalPower / 2.0; // Each split element gets half the power

        // Get glass properties
        var glass = originalElement.Glass;
        double n = glass.GetRefractiveIndex(wavelength);

        // Calculate optimal shape factor for minimum spherical aberration
        double optimalShape = originalElement.GetOptimalShapeFactor(wavelength);

        // Solve for radii of the split elements
        // Each element should have power = targetPower
        // Using thin lens approximation: P = (n-1) * (c1 - c2) = (n-1) * (1/R1 - 1/R2)
        // Shape factor: X = (R2 + R1) / (R2 - R1)
        // We can solve for R1 and R2 given P and X

        var (r1_first, r2_first) = SolveRadiiForPowerAndShape(targetPower, optimalShape, n);
        var (r1_second, r2_second) = SolveRadiiForPowerAndShape(targetPower, optimalShape, n);

        // Create the split system
        var splitSystem = system.Clone();

        // Find the surface indices for this element
        int frontSurfaceIndex = originalElement.FrontSurface.Index;
        int rearSurfaceIndex = originalElement.RearSurface.Index;

        // Calculate thicknesses while preserving total track length
        double originalElementThickness = originalElement.CenterThickness;
        double originalSpaceAfter = originalElement.RearSurface.Thickness;

        // Minimum glass thickness for each split element
        const double minGlassThickness = 1.0; // mm

        // Constrain air gap to fit within the available space
        double maxAirGap = Math.Max(0.1, originalElementThickness - 2 * minGlassThickness);
        double actualAirGap = Math.Min(airGap, maxAirGap);

        // Distribute remaining glass thickness equally between the two elements
        double totalGlassThickness = originalElementThickness - actualAirGap;
        double newGlassThickness = Math.Max(minGlassThickness, totalGlassThickness / 2.0);

        // Calculate the new total track for the split elements
        double newSplitTrack = newGlassThickness + actualAirGap + newGlassThickness;

        // Adjust the spacing after the split to preserve total track length
        double trackDifference = newSplitTrack - originalElementThickness;
        double adjustedSpaceAfter = Math.Max(0.1, originalSpaceAfter - trackDifference);

        // Create new surfaces for the split elements
        var surface1 = new Surface
        {
            Index = frontSurfaceIndex,
            Radius = r1_first,
            Thickness = newGlassThickness,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            Conic = 0,
            SurfaceType = SurfaceType.Standard
        };

        var surface2 = new Surface
        {
            Index = frontSurfaceIndex + 1,
            Radius = r2_first,
            Thickness = actualAirGap,
            Glass = null, // Air after this surface
            GlassName = null,
            SemiDiameter = originalElement.FrontSurface.SemiDiameter,
            Conic = 0,
            SurfaceType = SurfaceType.Standard
        };

        var surface3 = new Surface
        {
            Index = frontSurfaceIndex + 2,
            Radius = r1_second,
            Thickness = newGlassThickness,
            Glass = glass,
            GlassName = glass.Name,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            Conic = 0,
            SurfaceType = SurfaceType.Standard
        };

        var surface4 = new Surface
        {
            Index = frontSurfaceIndex + 3,
            Radius = r2_second,
            Thickness = adjustedSpaceAfter, // Adjusted to preserve track length
            Glass = null, // Air after the split element
            GlassName = null,
            SemiDiameter = originalElement.RearSurface.SemiDiameter,
            Conic = 0,
            SurfaceType = SurfaceType.Standard,
            IsStop = originalElement.RearSurface.IsStop,
            // Preserve marginal ray height solve from original rear surface
            HasMarginalRayHeightSolve = originalElement.RearSurface.HasMarginalRayHeightSolve,
            MarginalRayHeightSolveParams = originalElement.RearSurface.MarginalRayHeightSolveParams
        };

        // Replace the original two surfaces with four new surfaces
        splitSystem.Surfaces.RemoveAt(rearSurfaceIndex);
        splitSystem.Surfaces.RemoveAt(frontSurfaceIndex);

        splitSystem.Surfaces.Insert(frontSurfaceIndex, surface1);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 1, surface2);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 2, surface3);
        splitSystem.Surfaces.Insert(frontSurfaceIndex + 3, surface4);

        // Re-index surfaces
        splitSystem.ReindexSurfaces();

        // Create element objects for the result
        var firstElement = new LensElement
        {
            Index = elementIndex,
            FrontSurface = surface1,
            RearSurface = surface2,
            Glass = glass
        };

        var secondElement = new LensElement
        {
            Index = elementIndex + 1,
            FrontSurface = surface3,
            RearSurface = surface4,
            Glass = glass
        };

        // Calculate powers
        double firstPower = firstElement.GetThickLensPower(wavelength);
        double secondPower = secondElement.GetThickLensPower(wavelength);
        double totalPower = firstPower + secondPower - airGap * firstPower * secondPower;

        // Trace rays through both systems
        var originalRays = _tracer.TraceRayPair(system, 0, wavelength);
        var splitRays = _tracer.TraceRayPair(splitSystem, 0, wavelength);

        // Calculate EFLs
        double originalEfl = _tracer.CalculateEfl(system, wavelength);
        double splitEfl = _tracer.CalculateEfl(splitSystem, wavelength);

        return new SplitResult
        {
            OriginalSystem = system,
            SplitSystem = splitSystem,
            SplitElementIndex = elementIndex,
            OriginalElement = originalElement,
            FirstSplitElement = firstElement,
            SecondSplitElement = secondElement,
            AirGap = actualAirGap, // Use the actual constrained air gap
            OriginalPower = originalPower,
            FirstElementPower = firstPower,
            SecondElementPower = secondPower,
            TotalSplitPower = totalPower,
            OriginalMarginalRay = originalRays.MarginalRay,
            OriginalChiefRay = originalRays.ChiefRay,
            SplitMarginalRay = splitRays.MarginalRay,
            SplitChiefRay = splitRays.ChiefRay,
            OriginalEfl = originalEfl,
            SplitEfl = splitEfl
        };
    }

    /// <summary>
    /// Solves for the radii of curvature given a target power and shape factor.
    /// </summary>
    /// <param name="power">Target thin lens power.</param>
    /// <param name="shapeFactor">Target shape factor X = (R2 + R1) / (R2 - R1).</param>
    /// <param name="n">Refractive index.</param>
    /// <returns>Tuple of (R1, R2).</returns>
    private (double R1, double R2) SolveRadiiForPowerAndShape(double power, double shapeFactor, double n)
    {
        // From thin lens equation: P = (n-1) * (1/R1 - 1/R2)
        // From shape factor: X = (R2 + R1) / (R2 - R1)
        //
        // Let c1 = 1/R1 and c2 = 1/R2
        // P = (n-1) * (c1 - c2)
        // X = (1/c2 + 1/c1) / (1/c2 - 1/c1) = (c1 + c2) / (c1 - c2) * (c1*c2)/(c1*c2)
        //   = (c1 + c2) / (c1 - c2)
        //
        // From P: c1 - c2 = P / (n-1)
        // From X: c1 + c2 = X * (c1 - c2) = X * P / (n-1)
        //
        // Solving:
        // 2*c1 = (X + 1) * P / (n-1)
        // 2*c2 = (X - 1) * P / (n-1)
        //
        // c1 = (X + 1) * P / (2 * (n-1))
        // c2 = (X - 1) * P / (2 * (n-1))

        double factor = power / (2.0 * (n - 1.0));
        double c1 = (shapeFactor + 1.0) * factor;
        double c2 = (shapeFactor - 1.0) * factor;

        // Handle near-zero curvatures
        double r1 = Math.Abs(c1) > 1e-10 ? 1.0 / c1 : double.PositiveInfinity;
        double r2 = Math.Abs(c2) > 1e-10 ? 1.0 / c2 : double.PositiveInfinity;

        return (r1, r2);
    }

    /// <summary>
    /// Estimates the spherical aberration reduction factor from splitting.
    /// Theoretical reduction is factor of 4 for equal power split.
    /// </summary>
    /// <param name="originalPower">Original element power.</param>
    /// <param name="power1">First split element power.</param>
    /// <param name="power2">Second split element power.</param>
    /// <returns>Estimated SA reduction factor.</returns>
    public static double EstimateSaReductionFactor(double originalPower, double power1, double power2)
    {
        // SA ∝ P³ for a thin lens
        // Splitting into two equal power elements: SA_new = 2 * (P/2)³ = P³/4
        // Reduction factor = SA_old / SA_new = 4

        if (Math.Abs(originalPower) < 1e-12) return 1.0;

        double originalSaFactor = Math.Pow(Math.Abs(originalPower), 3);
        double splitSaFactor = Math.Pow(Math.Abs(power1), 3) + Math.Pow(Math.Abs(power2), 3);

        return splitSaFactor > 0 ? originalSaFactor / splitSaFactor : 1.0;
    }
}
