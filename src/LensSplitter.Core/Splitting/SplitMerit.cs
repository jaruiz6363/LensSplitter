using System.Globalization;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using LensSplitter.Core.Lens;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// The merit function a split is optimised against.
///
/// <para>It is AberrationCalculator's, in its own text format (docs/optimizer.md there), so it can
/// be written out, edited, kept beside a lens and argued over: one operand per line - Robb's
/// predicted spot (<c>PRMSA</c>), any of the thirty-seven aberration coefficients by name, real
/// axial and lateral colour, distortion, rays. The default is the predicted spot, which weighs
/// every aberration through seventh order by what it does to the image, over every field and
/// wavelength of the lens - and, for a lens with more than one wavelength, its colour.</para>
///
/// <para>To it LensSplitter adds what a split must keep, whatever the merit function says: the
/// lens's focal length, and edges on the two halves and the air around them.</para>
/// </summary>
public static class SplitMerit
{
    /// <summary>The merit function for a lens of one wavelength, as a file would hold it.</summary>
    public const string DefaultText =
        "# LensSplitter merit function, in AberrationCalculator's format (its docs/optimizer.md).\n" +
        "# TYPE, WEIGHT, TAR x | MIN x | MAX x, INPUTS\n" +
        "#\n" +
        "# Robb's predicted RMS spot over every field and wavelength of the lens - every aberration\n" +
        "# through seventh order, weighed by what it does to the image.\n" +
        "PRMSA, 1, TAR 0\n" +
        "#\n" +
        "# Examples, left off. Any coefficient can be named, and a distortion or ray term added:\n" +
        "#   B7,    1, TAR 0                 # seventh-order spherical\n" +
        "#   DISTF, 1, MIN -1, MAX 1, 1.0    # real distortion, per cent, at the full field\n" +
        "#\n" +
        "# LensSplitter adds, whatever is here: the focal length held (EFL), and edge thickness on\n" +
        "# the two halves and the air around them (EGT, EAT).\n";

    public static List<Operand> Default() => MeritFile.Parse(DefaultText.Split('\n'), "default");

    /// <summary>
    /// The default for one lens: <see cref="DefaultText"/>, and with more than one wavelength its
    /// colour too.
    ///
    /// <para>The predicted spot measures each wavelength at its own focus, so it cannot see a focus
    /// that moves with colour, nor an image that grows with it - and a glass is chosen largely for
    /// exactly those. Axial colour (AXC) is a focus shift along the axis, which blurs the image by
    /// about the shift times the image-space marginal ray angle u', so it is weighted by u'^2 to
    /// count as a spot radius does. Lateral colour (LCF) is already a transverse spread in lens
    /// units, and is weighted 1.</para>
    /// </summary>
    public static string DefaultTextFor(LensOptics lens)
    {
        if (lens.System.Wavelengths.Count < 2) return DefaultText;
        int last = lens.System.LastOpticalSurface();
        double u = Math.Abs(lens.Paraxial.U[last]);
        string w = (u * u).ToString("G4", CultureInfo.InvariantCulture);
        string text = DefaultText +
            "#\n" +
            "# The lens has more than one wavelength, so its colour, which the predicted spot cannot see:\n" +
            $"AXC, {w}, TAR 0         # real axial colour, weighted by u'^2 = {w} to count as a blur\n";
        if (Math.Abs(lens.MaxField) > 0)
            text += "LCF, 1, TAR 0, 1.0      # real lateral colour at the full field\n";
        return text;
    }

    public static List<Operand> DefaultFor(LensOptics lens) => MeritFile.Parse(DefaultTextFor(lens).Split('\n'), "default");

    public static List<Operand> Read(string path) => MeritFile.Read(path);

    /// <summary>
    /// The operands for one split: the merit function, then the focal length and total track held,
    /// and the edges.
    /// </summary>
    /// <param name="front">Surface index of the split's first face; its four faces follow.</param>
    /// <param name="track">The total track to hold, so that with the split's thicknesses free the
    /// surfaces after it stay where they were; null when the thicknesses are fixed.</param>
    public static List<Operand> ForSplit(IEnumerable<Operand> merit, double targetEfl, int front,
                                         SplitLimits limits, double eflWeight, double edgeWeight, double? track = null)
    {
        var ops = merit.ToList();
        if (!ops.Any(o => o.Type == OperandType.EFL))
            ops.Add(new Operand { Type = OperandType.EFL, Weight = eflWeight, Target = targetEfl, Comment = "the focal length held" });
        if (track is double ttl && !ops.Any(o => o.Type == OperandType.TTL))
            ops.Add(new Operand { Type = OperandType.TTL, Weight = eflWeight, Target = ttl, Comment = "the track held" });
        // The two halves' glass edges, and every air space from the one before the split to the
        // one after it - including the new gap.
        ops.Add(new Operand { Type = OperandType.EGT, Weight = edgeWeight, Min = limits.MinEdgeThickness,
                              Surface = front, Surface2 = front + 3, Comment = "split glass edges" });
        ops.Add(new Operand { Type = OperandType.EAT, Weight = edgeWeight, Min = limits.MinEdgeThickness,
                              Surface = Math.Max(1, front - 1), Surface2 = front + 3, Comment = "air edges around the split" });
        return ops;
    }
}
