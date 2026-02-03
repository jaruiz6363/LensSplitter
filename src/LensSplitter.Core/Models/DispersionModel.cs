namespace LensSplitter.Core.Models;

/// <summary>
/// Specifies the dispersion formula used to calculate refractive index.
/// </summary>
public enum DispersionModel
{
    /// <summary>
    /// Schott formula (model 1 in AGF files).
    /// n² = A₀ + A₁λ² + A₂λ⁻² + A₃λ⁻⁴ + A₄λ⁻⁶ + A₅λ⁻⁸
    /// </summary>
    Schott = 1,

    /// <summary>
    /// Sellmeier 1 formula (model 2 in AGF files).
    /// n² - 1 = K₁λ²/(λ² - L₁) + K₂λ²/(λ² - L₂) + K₃λ²/(λ² - L₃)
    /// </summary>
    Sellmeier1 = 2,

    /// <summary>
    /// Constant refractive index (no dispersion).
    /// </summary>
    Constant = 0
}

/// <summary>
/// Provides dispersion calculation methods for different models.
/// </summary>
public static class DispersionCalculator
{
    /// <summary>
    /// Calculates the refractive index for a given wavelength using the specified dispersion model.
    /// </summary>
    /// <param name="model">The dispersion model to use.</param>
    /// <param name="coefficients">The dispersion coefficients.</param>
    /// <param name="wavelengthMicrons">The wavelength in micrometers.</param>
    /// <returns>The refractive index, or 0 if calculation fails.</returns>
    public static double CalculateRefractiveIndex(DispersionModel model, double[] coefficients, double wavelengthMicrons)
    {
        return model switch
        {
            DispersionModel.Schott => CalculateSchott(coefficients, wavelengthMicrons),
            DispersionModel.Sellmeier1 => CalculateSellmeier1(coefficients, wavelengthMicrons),
            DispersionModel.Constant => coefficients.Length > 0 ? coefficients[0] : 1.0,
            _ => 0.0
        };
    }

    private static double CalculateSchott(double[] c, double lambda)
    {
        if (c.Length < 6) return 0.0;

        double lambda2 = lambda * lambda;
        double lambdaN2 = 1.0 / lambda2;
        double lambdaN4 = lambdaN2 * lambdaN2;
        double lambdaN6 = lambdaN4 * lambdaN2;
        double lambdaN8 = lambdaN6 * lambdaN2;

        double n2 = c[0] + (lambda2 * c[1]) + (lambdaN2 * c[2])
                  + (lambdaN4 * c[3]) + (lambdaN6 * c[4]);

        // Some AGF files have more coefficients
        if (c.Length > 5)
        {
            n2 += lambdaN8 * c[5];
        }

        return n2 >= 0 ? Math.Sqrt(n2) : 0.0;
    }

    private static double CalculateSellmeier1(double[] c, double lambda)
    {
        if (c.Length < 6) return 0.0;

        double lambda2 = lambda * lambda;

        double term1 = c[0] * lambda2 / (lambda2 - c[1]);
        double term2 = c[2] * lambda2 / (lambda2 - c[3]);
        double term3 = c[4] * lambda2 / (lambda2 - c[5]);

        double n2 = 1.0 + term1 + term2 + term3;

        return n2 > 0 ? Math.Sqrt(n2) : 0.0;
    }
}
