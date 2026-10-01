using System;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Dispersion equations, numbered as the AGF format numbers them. The number is stored in
/// the catalog file, so these values are fixed by the format and not free to renumber.
/// </summary>
public enum DispersionFormula
{
    Unknown = 0,
    Schott = 1,
    Sellmeier1 = 2,
    Herzberger = 3,
    Sellmeier2 = 4,
    Conrady = 5,
    Sellmeier3 = 6,
    Handbook1 = 7,
    Handbook2 = 8,
    Sellmeier4 = 9,
    Extended = 10,
    Sellmeier5 = 11,
    Extended2 = 12,
    Extended3 = 13,
}

/// <summary>
/// One catalog glass: enough to evaluate its refractive index and to print it.
///
/// Indices are relative to air at standard temperature and pressure, which is the
/// convention the AGF format is written in — not absolute vacuum indices.
/// </summary>
public class GlassData
{
    public string Name { get; set; } = string.Empty;
    public string Catalog { get; set; } = string.Empty;

    public DispersionFormula Formula { get; set; } = DispersionFormula.Unknown;

    /// <summary>Dispersion coefficients, meaning set by <see cref="Formula"/>.</summary>
    public double[] Coefficients { get; set; } = Array.Empty<double>();

    /// <summary>Index at the d line (0.5876 µm) as the catalog states it.</summary>
    public double Nd { get; set; }

    /// <summary>Abbe number as the catalog states it.</summary>
    public double Vd { get; set; }

    /// <summary>Validity range in micrometres, from the catalog's LD line.</summary>
    public double LambdaMin { get; set; }
    public double LambdaMax { get; set; }

    /// <summary>Free-text status/comment from the catalog.</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Refractive index at <paramref name="lambdaUm"/> micrometres.
    ///
    /// Outside the catalog's stated validity range the formula is still evaluated rather
    /// than refused: extrapolating a little past the range is often exactly what a designer
    /// wants, and silently substituting some other value would be worse than a number the
    /// caller can sanity-check against <see cref="LambdaMin"/> and <see cref="LambdaMax"/>.
    /// Returns NaN when the formula is unknown or its argument is unphysical.
    /// </summary>
    public double IndexAt(double lambdaUm)
    {
        if (lambdaUm <= 0.0 || Coefficients.Length == 0) return double.NaN;

        double l2 = lambdaUm * lambdaUm;
        double[] c = Coefficients;

        switch (Formula)
        {
            // n² = c0 + c1·λ² + c2·λ⁻² + c3·λ⁻⁴ + c4·λ⁻⁶ + c5·λ⁻⁸
            case DispersionFormula.Schott:
            {
                if (c.Length < 6) return double.NaN;
                double n2 = c[0] + c[1] * l2
                          + c[2] / l2
                          + c[3] / (l2 * l2)
                          + c[4] / (l2 * l2 * l2)
                          + c[5] / (l2 * l2 * l2 * l2);
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // Extended: the Schott formula continued to λ⁻¹⁰ and λ⁻¹².
            case DispersionFormula.Extended:
            {
                if (c.Length < 8) return double.NaN;
                double n2 = c[0] + c[1] * l2;
                double p = l2;
                for (int k = 2; k < 8; k++) { n2 += c[k] / p; p *= l2; }
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // Extended 2: the Schott formula plus λ⁴ and λ⁶ terms,
            // n² = c0 + c1λ² + c2λ⁻² + c3λ⁻⁴ + c4λ⁻⁶ + c5λ⁻⁸ + c6λ⁴ + c7λ⁶.
            // (This was evaluated as Extended, which is a different formula.)
            case DispersionFormula.Extended2:
            {
                if (c.Length < 8) return double.NaN;
                double n2 = c[0] + c[1] * l2 + c[2] / l2 + c[3] / (l2 * l2) + c[4] / (l2 * l2 * l2)
                          + c[5] / (l2 * l2 * l2 * l2) + c[6] * l2 * l2 + c[7] * l2 * l2 * l2;
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // Extended 3: n² = c0 + c1λ² + c2λ⁴ + c3λ⁻² + c4λ⁻⁴ + c5λ⁻⁶ + c6λ⁻⁸ + c7λ⁻¹⁰ + c8λ⁻¹².
            case DispersionFormula.Extended3:
            {
                if (c.Length < 9) return double.NaN;
                double n2 = c[0] + c[1] * l2 + c[2] * l2 * l2;
                double p = l2;
                for (int k = 3; k < 9; k++) { n2 += c[k] / p; p *= l2; }
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // n² − 1 = Σ Kᵢλ²/(λ² − Lᵢ), three terms (Sellmeier 1) or four (Sellmeier 3/4/5).
            case DispersionFormula.Sellmeier1:
                return SellmeierPairs(c, l2, 3);
            case DispersionFormula.Sellmeier3:
                return SellmeierPairs(c, l2, 4);
            case DispersionFormula.Sellmeier5:
                return SellmeierPairs(c, l2, 5);

            // n² = 1 + A + B1λ²/(λ² − λ1²) + B2/(λ² − λ2²), coefficients A B1 λ1 B2 λ2.
            // (This read the coefficients as K1 L1 K2 L2 with no A and no squaring.)
            case DispersionFormula.Sellmeier2:
            {
                if (c.Length < 5) return double.NaN;
                double d1 = l2 - c[2] * c[2], d2 = l2 - c[4] * c[4];
                if (Math.Abs(d1) < 1e-30 || Math.Abs(d2) < 1e-30) return double.NaN;
                double n2 = 1.0 + c[0] + c[1] * l2 / d1 + c[3] / d2;
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // n² = A + Bλ²/(λ²−C) + Dλ²/(λ²−E)
            case DispersionFormula.Sellmeier4:
            {
                if (c.Length < 5) return double.NaN;
                double d1 = l2 - c[2], d2 = l2 - c[4];
                if (Math.Abs(d1) < 1e-30 || Math.Abs(d2) < 1e-30) return double.NaN;
                double n2 = c[0] + c[1] * l2 / d1 + c[3] * l2 / d2;
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // n = c0 + c1/λ + c2/λ^3.5
            case DispersionFormula.Conrady:
            {
                if (c.Length < 3) return double.NaN;
                return c[0] + c[1] / lambdaUm + c[2] / Math.Pow(lambdaUm, 3.5);
            }

            // n = A + B/(λ²−0.028) + C/(λ²−0.028)² + Dλ² + Eλ⁴ + Fλ⁶
            case DispersionFormula.Herzberger:
            {
                if (c.Length < 6) return double.NaN;
                double d = l2 - 0.028;
                if (Math.Abs(d) < 1e-30) return double.NaN;
                return c[0] + c[1] / d + c[2] / (d * d)
                     + c[3] * l2 + c[4] * l2 * l2 + c[5] * l2 * l2 * l2;
            }

            // n² = A + B/(λ²−C) − Dλ²
            case DispersionFormula.Handbook1:
            {
                if (c.Length < 4) return double.NaN;
                double d = l2 - c[2];
                if (Math.Abs(d) < 1e-30) return double.NaN;
                double n2 = c[0] + c[1] / d - c[3] * l2;
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            // n² = A + Bλ²/(λ²−C) − Dλ²
            case DispersionFormula.Handbook2:
            {
                if (c.Length < 4) return double.NaN;
                double d = l2 - c[2];
                if (Math.Abs(d) < 1e-30) return double.NaN;
                double n2 = c[0] + c[1] * l2 / d - c[3] * l2;
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            default:
                return double.NaN;
        }
    }

    /// <summary>Sellmeier of n terms: n² − 1 = Σ Kᵢλ²/(λ² − Lᵢ), coefficients in K,L pairs.</summary>
    private static double SellmeierPairs(double[] c, double l2, int terms)
    {
        double n2 = 1.0;
        for (int i = 0; i < terms; i++)
        {
            int k = 2 * i, l = 2 * i + 1;
            if (l >= c.Length) break;
            if (c[k] == 0.0 && c[l] == 0.0) continue;       // unused trailing pair
            double d = l2 - c[l];
            if (Math.Abs(d) < 1e-30) return double.NaN;     // at a resonance
            n2 += c[k] * l2 / d;
        }
        return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
    }
}
