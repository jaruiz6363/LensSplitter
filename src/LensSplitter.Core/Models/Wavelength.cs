namespace LensSplitter.Core.Models;

/// <summary>
/// Represents a wavelength used in optical system analysis.
/// </summary>
public class Wavelength
{
    /// <summary>
    /// Gets or sets the wavelength value in micrometers.
    /// </summary>
    public double ValueMicrons { get; set; }

    /// <summary>
    /// Gets or sets the weight for this wavelength in polychromatic calculations.
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether this is the primary (reference) wavelength.
    /// </summary>
    public bool IsPrimary { get; set; }

    public Wavelength() { }

    public Wavelength(double valueMicrons, bool isPrimary = false, double weight = 1.0)
    {
        ValueMicrons = valueMicrons;
        IsPrimary = isPrimary;
        Weight = weight;
    }

    /// <summary>
    /// Standard d-line wavelength (587.56 nm).
    /// </summary>
    public static Wavelength DLine => new(0.58756, true);

    /// <summary>
    /// Standard C-line wavelength (656.27 nm).
    /// </summary>
    public static Wavelength CLine => new(0.65627);

    /// <summary>
    /// Standard F-line wavelength (486.13 nm).
    /// </summary>
    public static Wavelength FLine => new(0.48613);
}
