namespace LensSplitter.Core.Models;

/// <summary>
/// Represents a glass material with dispersion properties.
/// </summary>
public class Glass
{
    /// <summary>
    /// Gets or sets the glass name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the catalog name this glass belongs to.
    /// </summary>
    public string Catalog { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dispersion model used to calculate refractive index.
    /// </summary>
    public DispersionModel DispersionModel { get; set; } = DispersionModel.Sellmeier1;

    /// <summary>
    /// Gets or sets the dispersion coefficients.
    /// </summary>
    public double[] Coefficients { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the refractive index at the d-line (587.56 nm).
    /// </summary>
    public double Nd { get; set; }

    /// <summary>
    /// Gets or sets the Abbe number (dispersion).
    /// </summary>
    public double Vd { get; set; }

    /// <summary>
    /// Calculates the refractive index at the specified wavelength.
    /// </summary>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The refractive index.</returns>
    public double GetRefractiveIndex(double wavelengthMicrons)
    {
        if (Coefficients.Length == 0)
        {
            // Fall back to Nd if no coefficients available
            return Nd > 0 ? Nd : 1.0;
        }

        return DispersionCalculator.CalculateRefractiveIndex(DispersionModel, Coefficients, wavelengthMicrons);
    }

    /// <summary>
    /// Creates an air material (n=1 for all wavelengths).
    /// </summary>
    public static Glass Air => new()
    {
        Name = "AIR",
        Catalog = "IDEAL",
        DispersionModel = DispersionModel.Constant,
        Coefficients = new[] { 1.0 },
        Nd = 1.0,
        Vd = 0.0
    };

    /// <summary>
    /// Creates an ideal material with constant refractive index.
    /// </summary>
    /// <param name="n">The refractive index.</param>
    /// <returns>A glass with constant refractive index.</returns>
    public static Glass Ideal(double n) => new()
    {
        Name = $"IDEAL_{n:F4}",
        Catalog = "IDEAL",
        DispersionModel = DispersionModel.Constant,
        Coefficients = new[] { n },
        Nd = n,
        Vd = 0.0
    };

    public override string ToString() => string.IsNullOrEmpty(Catalog) ? Name : $"{Catalog}:{Name}";
}
