using System.Collections.Generic;

namespace AberrationCalculator.Core.Models;

/// <summary>
/// Tidies the field list a reader produced.
///
/// Several formats store a fixed-size field table and zero the unused slots, so a lens
/// with one on-axis field arrives carrying a run of repeated (0, 0) entries. Left alone
/// those become extra field points, and every per-field result would be printed several
/// times over. Readers call this once, at the end of a load.
/// </summary>
public static class FieldValidation
{
    /// <summary>
    /// Drops repeated field points, keeping the first occurrence and the file's order,
    /// and guarantees at least one field so a system is always evaluable on axis.
    /// </summary>
    public static void FilterImportedFields(OpticalSystem system)
    {
        const double tol = 1e-9;

        var kept = new List<Field>();
        foreach (var f in system.Fields)
        {
            bool duplicate = false;
            foreach (var k in kept)
            {
                if (System.Math.Abs(k.Y - f.Y) <= tol && System.Math.Abs(k.X - f.X) <= tol)
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate) kept.Add(f);
        }

        if (kept.Count == 0) kept.Add(new Field(0.0));

        system.Fields.Clear();
        system.Fields.AddRange(kept);
    }
}
