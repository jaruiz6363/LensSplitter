using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace LensSplitter.Core.Lens;

/// <summary>
/// One lens element: a glass between two surfaces, <see cref="Front"/> and <see cref="Front"/> + 1.
/// Numbered from 0 in the order the lens has them; shown to people from 1.
/// </summary>
public sealed class LensElement
{
    public int Number { get; init; }

    /// <summary>Surface index of the front face; the rear face is the next surface.</summary>
    public int Front { get; init; }
    public int Rear => Front + 1;

    /// <summary>The glass, as the lens names it, or a description of its model glass.</summary>
    public string Glass { get; init; } = "";

    /// <summary>Part of a cemented group: glass on both sides of one of its faces.</summary>
    public bool Cemented { get; set; }

    /// <summary>Part of a closely air-spaced achromat, whose correction depends on the pair.</summary>
    public bool AirSpacedPair { get; set; }

    /// <summary>What group it belongs to, for a message; null when it stands alone.</summary>
    public string? Group { get; set; }

    public override string ToString() => $"Element {Number + 1} (surfaces {Front}-{Rear}, {Glass})";
}

/// <summary>Finds the elements of a lens and the groups among them.</summary>
public static class LensElements
{
    /// <summary>Air spaces up to this long, between an opposite-power pair of different glasses, make an air-spaced achromat.</summary>
    public const double MaxAirSpacedPairGap = 2.0;

    /// <summary>Whether the medium after surface <paramref name="i"/> is glass (a mirror is not).</summary>
    public static bool GlassAfter(OpticalSystem system, int i)
    {
        var s = system.Surfaces[i];
        if (s.IsMirror) return false;
        if (s.ModelIndexEnabled) return true;
        return !string.IsNullOrEmpty(s.Material) && !s.Material!.Equals("AIR", StringComparison.OrdinalIgnoreCase);
    }

    public static string GlassName(Surface s) =>
        s.ModelIndexEnabled
            ? FormattableString.Invariant($"model {s.ModelNd:0.######}/{s.ModelVd:0.##}")
            : s.Material ?? "";

    /// <summary>
    /// Every element: each glass between two refracting surfaces. A cemented group gives one
    /// element per glass, marked <see cref="LensElement.Cemented"/>.
    /// </summary>
    /// <param name="n">Index after each surface at the primary wavelength, for the powers the
    /// air-spaced-pair test compares; null skips that test.</param>
    /// <param name="vd">Abbe number of the glass after each surface, for the same test.</param>
    public static List<LensElement> Find(OpticalSystem system, double[]? n = null, double[]? vd = null)
    {
        var list = new List<LensElement>();
        int last = system.LastOpticalSurface();
        // A glass after surface i, with i + 1 its rear face - a refracting surface, not the image.
        for (int i = 1; i + 1 <= last; i++)
        {
            if (!GlassAfter(system, i)) continue;
            if (system.Surfaces[i].Type == SurfaceType.Paraxial || system.Surfaces[i + 1].Type == SurfaceType.Paraxial) continue;
            var e = new LensElement { Number = list.Count, Front = i, Glass = GlassName(system.Surfaces[i]) };
            // Glass on the far side of either face: cemented to a neighbour. (Surface 0's medium
            // is object space, which an immersed object makes glass without cementing anything.)
            e.Cemented = (i > 1 && GlassAfter(system, i - 1)) || GlassAfter(system, i + 1);
            list.Add(e);
        }

        foreach (var e in list)
            if (e.Cemented)
            {
                int size = 1;
                for (int j = e.Front - 1; j >= 1 && GlassAfter(system, j); j--) size++;
                for (int j = e.Rear; j <= last && GlassAfter(system, j); j++) size++;
                e.Group = size == 2 ? "cemented doublet" : size == 3 ? "cemented triplet" : $"cemented group of {size}";
            }

        if (n != null && vd != null)
            for (int k = 0; k + 1 < list.Count; k++)
            {
                var a = list[k];
                var b = list[k + 1];
                if (a.Cemented || b.Cemented || b.Front != a.Rear + 1) continue;
                double gap = system.Surfaces[a.Rear].Thickness;
                if (gap <= 0 || gap >= MaxAirSpacedPairGap) continue;
                bool opposite = Power(system, a, n) * Power(system, b, n) < 0;
                bool dispersions = Math.Abs(vd[a.Front] - vd[b.Front]) > 15.0;
                if (opposite && dispersions)
                {
                    a.AirSpacedPair = b.AirSpacedPair = true;
                    a.Group ??= "air-spaced doublet";
                    b.Group ??= "air-spaced doublet";
                }
            }
        return list;
    }

    /// <summary>
    /// The thick-lens power of an element in the media around it, from its vertex curvatures
    /// (an r^2 aspheric term is a curvature change, and counted).
    /// </summary>
    public static double Power(OpticalSystem system, LensElement e, double[] n)
    {
        double n0 = Math.Abs(n[e.Front - 1]), ng = Math.Abs(n[e.Front]), n2 = Math.Abs(n[e.Rear]);
        double p1 = (ng - n0) * system.Surfaces[e.Front].VertexCurvature;
        double p2 = (n2 - ng) * system.Surfaces[e.Rear].VertexCurvature;
        return p1 + p2 - system.Surfaces[e.Front].Thickness / ng * p1 * p2;
    }
}
