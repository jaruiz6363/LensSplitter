using LensSplitter.Core.Models;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// Contains the results of a lens element splitting operation.
/// </summary>
public class SplitResult
{
    /// <summary>
    /// Gets or sets the original optical system (before splitting).
    /// </summary>
    public OpticalSystem OriginalSystem { get; set; } = null!;

    /// <summary>
    /// Gets or sets the modified optical system (after splitting).
    /// </summary>
    public OpticalSystem SplitSystem { get; set; } = null!;

    /// <summary>
    /// Gets or sets the index of the element that was split.
    /// </summary>
    public int SplitElementIndex { get; set; }

    /// <summary>
    /// Gets or sets the original element that was split.
    /// </summary>
    public LensElement OriginalElement { get; set; } = null!;

    /// <summary>
    /// Gets or sets the first element after splitting.
    /// </summary>
    public LensElement FirstSplitElement { get; set; } = null!;

    /// <summary>
    /// Gets or sets the second element after splitting.
    /// </summary>
    public LensElement SecondSplitElement { get; set; } = null!;

    /// <summary>
    /// Gets or sets the air gap between the split elements.
    /// </summary>
    public double AirGap { get; set; }

    /// <summary>
    /// Gets or sets the power of the original element.
    /// </summary>
    public double OriginalPower { get; set; }

    /// <summary>
    /// Gets or sets the power of the first split element.
    /// </summary>
    public double FirstElementPower { get; set; }

    /// <summary>
    /// Gets or sets the power of the second split element.
    /// </summary>
    public double SecondElementPower { get; set; }

    /// <summary>
    /// Gets or sets the total power of both split elements.
    /// </summary>
    public double TotalSplitPower { get; set; }

    /// <summary>
    /// Gets or sets the marginal ray for the original system.
    /// </summary>
    public ParaxialRay? OriginalMarginalRay { get; set; }

    /// <summary>
    /// Gets or sets the chief ray for the original system.
    /// </summary>
    public ParaxialRay? OriginalChiefRay { get; set; }

    /// <summary>
    /// Gets or sets the marginal ray for the split system.
    /// </summary>
    public ParaxialRay? SplitMarginalRay { get; set; }

    /// <summary>
    /// Gets or sets the chief ray for the split system.
    /// </summary>
    public ParaxialRay? SplitChiefRay { get; set; }

    /// <summary>
    /// Gets or sets the EFL of the original system.
    /// </summary>
    public double OriginalEfl { get; set; }

    /// <summary>
    /// Gets or sets the EFL of the split system.
    /// </summary>
    public double SplitEfl { get; set; }

    /// <summary>
    /// Gets the power error (difference between original and split total power).
    /// </summary>
    public double PowerError => Math.Abs(OriginalPower - TotalSplitPower);

    /// <summary>
    /// Gets the relative power error as a percentage.
    /// </summary>
    public double RelativePowerError => OriginalPower != 0 ? PowerError / Math.Abs(OriginalPower) * 100.0 : 0.0;

    /// <summary>
    /// Gets the EFL error.
    /// </summary>
    public double EflError => Math.Abs(OriginalEfl - SplitEfl);

    /// <summary>
    /// Gets whether the split was successful (power preserved within tolerance).
    /// </summary>
    public bool IsSuccessful => RelativePowerError < 1.0; // Less than 1% error

    /// <summary>
    /// Gets a summary of the split operation.
    /// </summary>
    public string Summary =>
        $"Split element {SplitElementIndex}: P_orig={OriginalPower:F6}, P1={FirstElementPower:F6}, P2={SecondElementPower:F6}, " +
        $"P_total={TotalSplitPower:F6}, Error={RelativePowerError:F2}%";
}
