using System.Globalization;
using LensSplitter.Core.Models;

namespace LensSplitter.Parsing.Glass;

/// <summary>
/// Parses AGF (ZEMAX glass catalog) files.
/// </summary>
public class AgfParser
{
    /// <summary>
    /// Parses an AGF file and returns a dictionary of glass materials.
    /// </summary>
    /// <param name="path">Path to the AGF file.</param>
    /// <returns>Dictionary mapping glass names to Glass objects.</returns>
    public Dictionary<string, Core.Models.Glass> Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("AGF file not found.", path);
        }

        var glasses = new Dictionary<string, Core.Models.Glass>(StringComparer.OrdinalIgnoreCase);
        var catalogName = Path.GetFileNameWithoutExtension(path);

        var currentLines = new List<string>();
        bool foundFirstMaterial = false;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("NM ", StringComparison.OrdinalIgnoreCase))
            {
                if (foundFirstMaterial && currentLines.Count > 0)
                {
                    var glass = ParseGlassEntry(currentLines, catalogName);
                    if (glass != null && !string.IsNullOrEmpty(glass.Name))
                    {
                        glasses[glass.Name] = glass;
                    }
                    currentLines.Clear();
                }
                foundFirstMaterial = true;
                currentLines.Add(trimmed);
            }
            else if (foundFirstMaterial)
            {
                currentLines.Add(trimmed);
            }
        }

        // Process the last material
        if (currentLines.Count > 0)
        {
            var glass = ParseGlassEntry(currentLines, catalogName);
            if (glass != null && !string.IsNullOrEmpty(glass.Name))
            {
                glasses[glass.Name] = glass;
            }
        }

        return glasses;
    }

    private Core.Models.Glass? ParseGlassEntry(List<string> lines, string catalogName)
    {
        var glass = new Core.Models.Glass
        {
            Catalog = catalogName
        };

        foreach (var line in lines)
        {
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;

            switch (tokens[0].ToUpperInvariant())
            {
                case "NM":
                    ParseNmLine(tokens, glass);
                    break;
                case "CD":
                    ParseCdLine(tokens, glass);
                    break;
            }
        }

        return glass;
    }

    private void ParseNmLine(string[] tokens, Core.Models.Glass glass)
    {
        // NM name dispersion_model glass_avail nd vd [param1 param2 param3]
        if (tokens.Length > 1)
        {
            glass.Name = tokens[1];
        }

        if (tokens.Length > 2 && int.TryParse(tokens[2], out int dispModel))
        {
            glass.DispersionModel = dispModel switch
            {
                1 => DispersionModel.Schott,
                2 => DispersionModel.Sellmeier1,
                _ => DispersionModel.Constant
            };
        }

        if (tokens.Length > 4 && double.TryParse(tokens[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double nd))
        {
            glass.Nd = nd;
        }

        if (tokens.Length > 5 && double.TryParse(tokens[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double vd))
        {
            glass.Vd = vd;
        }
    }

    private void ParseCdLine(string[] tokens, Core.Models.Glass glass)
    {
        // CD coeff1 coeff2 coeff3 ... (up to 10 coefficients)
        var coefficients = new List<double>();

        for (int i = 1; i < tokens.Length && i <= 10; i++)
        {
            if (double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
            {
                coefficients.Add(coeff);
            }
        }

        glass.Coefficients = coefficients.ToArray();
    }
}
