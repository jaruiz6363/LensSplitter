namespace LensSplitter.Core.Lens;

/// <summary>What one element contributes, and whether it can be split.</summary>
public sealed class ElementReport
{
    public LensElement Element { get; init; } = null!;
    public double Power { get; init; }

    /// <summary>The element's two surfaces' share of the mean-square predicted spot.</summary>
    public double SpotShare { get; init; }

    /// <summary><see cref="SpotShare"/> as a percentage of the whole spot; the elements' add to 100 with the rest.</summary>
    public double SpotPercent { get; init; }

    public double S1 { get; init; }
    public double S2 { get; init; }
    public double S3 { get; init; }

    public bool Figured { get; init; }

    /// <summary>Why it cannot be split, or null when it can.</summary>
    public string? NotSplittable { get; init; }
    public bool Splittable => NotSplittable == null;
}

/// <summary>
/// Which element to split. An element is worth splitting when it makes the spot larger: its two
/// surfaces' share of the mean-square predicted spot (Robb's, through seventh order, cross terms
/// split evenly) is positive and large. A negative share is an element correcting the others,
/// which splitting would weaken.
///
/// <para>Not split, whatever their share: a negative element (splitting halves the overcorrection
/// it contributes), an element cemented to another, and one of a closely air-spaced achromatic
/// pair - both are corrected as a unit. An element with a conic or aspheric face can be split: the
/// figuring stays on its outer face.</para>
/// </summary>
public static class ElementAnalysis
{
    public static List<ElementReport> Analyse(LensOptics optics)
    {
        var system = optics.System;
        var n = optics.PrimaryIndices;
        var elements = LensElements.Find(system, n, optics.AbbeNumbers());
        var share = optics.SpotShareBySurface();
        double total = share.Sum();
        var seidel = optics.Seidel;
        double At(double[] a, int i) => i < a.Length ? a[i] : 0.0;

        var reports = new List<ElementReport>();
        foreach (var e in elements)
        {
            double power = LensElements.Power(system, e, n);
            double s = share[e.Front] + share[e.Rear];
            string? why =
                e.Cemented ? $"part of a {e.Group}"
                : e.AirSpacedPair ? $"part of an {e.Group}"
                : power <= 0 ? "negative power"
                : system.Surfaces[e.Front].IsMirror || system.Surfaces[e.Rear].IsMirror ? "a mirror"
                : system.Surfaces[e.Front].IsPerturbed || system.Surfaces[e.Rear].IsPerturbed ? "tilted or decentred"
                : null;
            reports.Add(new ElementReport
            {
                Element = e,
                Power = power,
                SpotShare = s,
                SpotPercent = Math.Abs(total) > 1e-300 ? 100.0 * s / total : 0.0,
                S1 = At(seidel.S1, e.Front) + At(seidel.S1, e.Rear),
                S2 = At(seidel.S2, e.Front) + At(seidel.S2, e.Rear),
                S3 = At(seidel.S3, e.Front) + At(seidel.S3, e.Rear),
                Figured = Figured(system.Surfaces[e.Front]) || Figured(system.Surfaces[e.Rear]),
                NotSplittable = why,
            });
        }
        return reports;
    }

    private static bool Figured(AberrationCalculator.Core.Models.Surface s) =>
        s.Conic != 0.0 || s.AsphericCoefficients.Any(c => c != 0.0);

    /// <summary>The splittable element with the largest share of the spot, or null when none is splittable.</summary>
    public static ElementReport? Recommend(IEnumerable<ElementReport> reports) =>
        reports.Where(r => r.Splittable && r.SpotShare > 0).OrderByDescending(r => r.SpotShare).FirstOrDefault();
}
