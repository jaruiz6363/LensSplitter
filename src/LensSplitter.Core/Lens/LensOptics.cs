using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace LensSplitter.Core.Lens;

/// <summary>
/// A lens's first-order properties and aberrations, computed once, the way AberrationCalculator's
/// own report computes them: indices by its glass catalogs, a paraxial trace at the full field,
/// Seidel sums, Buchdahl's coefficients through seventh order with the tertiary terms attached,
/// and Robb's predicted RMS spot over every field and wavelength (PRMSA).
/// </summary>
public sealed class LensOptics
{
    public OpticalSystem System { get; }
    public GlassCatalog Catalog { get; }

    /// <summary>Index after each surface, per wavelength in the lens's order.</summary>
    public IReadOnlyList<double[]> Indices { get; }
    public double[] PrimaryIndices { get; }
    public int PrimaryWave { get; }

    /// <summary>Glasses the catalogs did not have, so traced as air. Empty for a usable lens.</summary>
    public IReadOnlyList<string> Unresolved { get; }

    public double MaxField { get; }
    public ParaxialResult Paraxial { get; }
    public SeidelResult Seidel { get; }
    public BuchdahlResult Buchdahl { get; }

    /// <summary>Robb's predicted RMS spot radius over every field and wavelength, in lens units.</summary>
    public double Prmsa { get; }

    public double Efl => Paraxial.Efl;

    public LensOptics(OpticalSystem system, GlassCatalog catalog)
    {
        System = system ?? throw new ArgumentNullException(nameof(system));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (system.Wavelengths.Count == 0) throw new InvalidOperationException("The lens has no wavelengths.");

        var unresolved = new List<string>();
        var indices = new List<double[]>();
        foreach (var w in system.Wavelengths)
            indices.Add(IndexResolver.Build(system, catalog, w.Value, unresolved));
        Indices = indices;
        Unresolved = unresolved.Distinct().ToList();
        PrimaryWave = Math.Max(0, system.PrimaryWavelengthIndex);
        PrimaryIndices = indices[PrimaryWave];

        MaxField = 0.0;
        foreach (var f in system.Fields) if (Math.Abs(f.Y) > Math.Abs(MaxField)) MaxField = f.Y;

        Paraxial = ParaxialTrace.Trace(system, PrimaryIndices, MaxField);
        var (shortN, longN) = SpectralExtremes();
        Seidel = SeidelCoefficients.Compute(system, PrimaryIndices, shortN, longN, Paraxial);
        Buchdahl = Coefficients(PrimaryIndices, Paraxial);

        var cases = new List<(BuchdahlTerms, double, double)>();
        for (int w = 0; w < system.Wavelengths.Count; w++)
        {
            var totals = w == PrimaryWave
                ? Buchdahl.Totals
                : Coefficients(indices[w], ParaxialTrace.Trace(system, indices[w], MaxField)).Totals;
            foreach (var (h, weight) in FieldPoints())
                cases.Add((totals, h, system.Wavelengths[w].Weight * weight));
        }
        Prmsa = Prms.Composite(cases);
    }

    /// <summary>The fields as (fraction of the full field, weight); one on axis if the lens lists none.</summary>
    public IEnumerable<(double H, double Weight)> FieldPoints()
    {
        if (System.Fields.Count == 0) { yield return (0.0, 1.0); yield break; }
        foreach (var f in System.Fields)
            yield return (Math.Abs(MaxField) > 1e-15 ? f.Y / MaxField : 0.0, f.Weight);
    }

    private BuchdahlResult Coefficients(double[] n, ParaxialResult p)
    {
        var b = BuchdahlCoefficients.Compute(System, p);
        TertiaryCoefficients.Attach(System, n, p, b, MaxField);
        return b;
    }

    private (double[] Short, double[] Long) SpectralExtremes()
    {
        if (System.Wavelengths.Count < 2) return (PrimaryIndices, PrimaryIndices);
        int lo = 0, hi = 0;
        for (int i = 1; i < System.Wavelengths.Count; i++)
        {
            if (System.Wavelengths[i].Value < System.Wavelengths[lo].Value) lo = i;
            if (System.Wavelengths[i].Value > System.Wavelengths[hi].Value) hi = i;
        }
        return (Indices[lo], Indices[hi]);
    }

    /// <summary>
    /// Each surface's share of the mean-square predicted spot at the primary wavelength,
    /// weighted over the fields, cross terms split evenly (AberrationCalculator's
    /// <see cref="ContributionAnalysis.BySurface"/>). The shares add up to the whole; a negative
    /// one is a surface correcting the others.
    /// </summary>
    public double[] SpotShareBySurface()
    {
        var share = new double[System.Surfaces.Count];
        double total = 0.0;
        foreach (var (h, w) in FieldPoints())
        {
            if (w <= 0) continue;
            total += w;
            foreach (var c in ContributionAnalysis.BySurface(Buchdahl, h))
                if (c.Surface >= 0 && c.Surface < share.Length) share[c.Surface] += w * c.Share;
        }
        if (total > 0) for (int i = 0; i < share.Length; i++) share[i] /= total;
        return share;
    }

    /// <summary>Abbe number of the medium after each surface (0 for air).</summary>
    public double[] AbbeNumbers()
    {
        var vd = new double[System.Surfaces.Count];
        for (int i = 0; i < vd.Length; i++)
        {
            var s = System.Surfaces[i];
            if (s.ModelIndexEnabled) vd[i] = s.ModelVd;
            else if (LensElements.GlassAfter(System, i))
                vd[i] = Catalog.Find(s.Material, System.GlassCatalogs.Count > 0 ? System.GlassCatalogs : null)?.Vd ?? 0.0;
        }
        return vd;
    }
}
