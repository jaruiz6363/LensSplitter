namespace LensSplitter.Core.Splitting;

/// <summary>
/// Provides a default list of commonly used optical glasses for optimization.
/// Based on Schott's preferred glass selection (S1_GLASS catalog).
/// </summary>
public static class DefaultGlasses
{
    /// <summary>
    /// Default list of glass names from the Schott S1_GLASS (preferred) catalog.
    /// These represent commonly available, well-characterized glasses suitable
    /// for most optical designs.
    /// </summary>
    public static readonly string[] S1GlassNames = new[]
    {
        // Flint glasses (higher dispersion, lower Abbe number)
        "F2",       // Classic flint, Nd=1.620, Vd=36.4
        "F5",       // Nd=1.603, Vd=38.0
        "SF1",      // Dense flint, Nd=1.717, Vd=29.5
        "SF2",      // Nd=1.648, Vd=33.8
        "SF4",      // Nd=1.755, Vd=27.5
        "SF5",      // Nd=1.673, Vd=32.2
        "LF5",      // Light flint, Nd=1.581, Vd=40.9

        // Crown glasses (lower dispersion, higher Abbe number)
        "K7",       // Classic crown, Nd=1.511, Vd=60.4
        "N-BK7",    // Borosilicate crown, Nd=1.517, Vd=64.2 - most common
        "N-K5",     // Crown, Nd=1.522, Vd=59.5
        "N-SK2",    // Dense crown, Nd=1.607, Vd=56.7
        "N-SK5",    // Nd=1.589, Vd=61.3
        "N-SK16",   // Nd=1.620, Vd=60.3
        "N-SSK5",   // Extra dense crown, Nd=1.658, Vd=50.9

        // Barium glasses
        "N-BAF51",  // Barium flint, Nd=1.652, Vd=45.0
        "N-BAF52",  // Nd=1.609, Vd=47.0
        "N-BALF4",  // Barium light flint, Nd=1.580, Vd=53.9
        "N-BASF2",  // Barium dense flint, Nd=1.664, Vd=36.0

        // Lanthanum glasses (high index)
        "N-LAF2",   // Lanthanum flint, Nd=1.744, Vd=44.7
        "LAFN7",    // Nd=1.750, Vd=35.0
        "LASF35",   // Lanthanum dense flint, Nd=1.903, Vd=31.3
        "N-LAK9",   // Lanthanum crown, Nd=1.691, Vd=54.7
        "N-LAK10",  // Nd=1.720, Vd=50.4

        // Special glasses
        "N-FK58",   // Fluorine crown (low dispersion), Nd=1.456, Vd=90.9
        "N-PK51",   // Phosphate crown, Nd=1.529, Vd=77.0
        "N-PSK53A", // Nd=1.620, Vd=63.5
        "N-KZFS4",  // Short flint, Nd=1.613, Vd=44.3
        "N-SF57",   // Dense flint (high index), Nd=1.847, Vd=23.8
    };

    /// <summary>
    /// A smaller subset of glasses for quick optimization.
    /// Covers the main crown-flint combinations needed for achromatic designs.
    /// </summary>
    public static readonly string[] QuickOptimizationGlasses = new[]
    {
        "N-BK7",    // Standard crown
        "N-SK16",   // Dense crown
        "N-LAK9",   // Lanthanum crown
        "F2",       // Standard flint
        "SF2",      // Dense flint
        "N-SF57",   // Extra dense flint
        "N-FK58",   // Low dispersion
    };

    /// <summary>
    /// Gets the default glass names as a comma-separated string.
    /// </summary>
    public static string DefaultGlassString => string.Join(",", S1GlassNames);

    /// <summary>
    /// Gets the quick optimization glass names as a comma-separated string.
    /// </summary>
    public static string QuickGlassString => string.Join(",", QuickOptimizationGlasses);
}
