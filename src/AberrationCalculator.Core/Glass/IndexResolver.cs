using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Models;

using AberrationCalculator.Core.Enums;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Turns each surface's material into a refractive index at a wavelength.
///
/// The result is indexed by surface and holds the index of the medium AFTER that surface,
/// which is what a ray-trace recurrence wants: leaving surface i, the ray travels in
/// index[i] to reach surface i+1.
/// </summary>
public static class IndexResolver
{
    /// <summary>d, F and C lines in micrometres, for model-glass dispersion.</summary>
    public const double LambdaD = 0.5875618, LambdaF = 0.4861327, LambdaC = 0.6562725;

    /// <summary>
    /// Index after each surface at <paramref name="lambdaUm"/>. Air is 1. A mirror keeps the
    /// index of the medium it sits in — reflection is handled by the trace reversing
    /// direction, not by negating the index here, so that this array stays a description of
    /// the media rather than of the ray's history.
    /// </summary>
    /// <summary>
    /// The same, from materials ALREADY RESOLVED - a pointer per surface instead of a name.
    ///
    /// <para><b>A name is not a material.</b> Resolving one means a dictionary probe against
    /// every loaded catalogue, and doing it per surface per wavelength every time the indices are
    /// rebuilt is work that answers the same question over and over. An optimiser knows the
    /// answer already: the glasses are fixed for the whole run except when the search swaps one,
    /// and a swap is choosing a different glass that is ALSO already in hand. So the design holds
    /// the material itself, and a swap moves a pointer.</para>
    ///
    /// <para>A null entry means this surface has no glass to point at - air, a mirror, a model
    /// index, or a name nothing could resolve - and the fall-throughs below handle each, exactly
    /// as they do when the lookup is done by name.</para>
    /// </summary>
    public static double[] Build(OpticalSystem system, GlassData?[] materials, double lambdaUm,
                                 IList<string>? unresolved = null)
    {
        if (materials == null) throw new ArgumentNullException(nameof(materials));

        int n = system.Surfaces.Count;
        var idx = new double[n];

        for (int i = 0; i < n; i++)
        {
            var s = system.Surfaces[i];

            if (s.ModelIndexEnabled && s.ModelNd > 0.0)
            {
                idx[i] = ModelIndex(s.ModelNd, s.ModelVd, s.ModelDPgF, lambdaUm);
                continue;
            }

            if (string.IsNullOrEmpty(s.Material) || s.IsMirror)
            {
                idx[i] = s.IsMirror && i > 0 ? idx[i - 1] : 1.0;
                continue;
            }

            var pointed = i < materials.Length ? materials[i] : null;
            if (pointed == null)
            {
                // Nothing resolved this name when the design was opened. A glass code is still
                // worth reading - whole prescriptions are written that way, and treating one as
                // air would silently flatten the lens.
                if (GlassCode.TryParse(s.Material, out double codeNd, out double codeVd))
                {
                    idx[i] = ModelIndex(codeNd, codeVd, 0.0, lambdaUm);
                    continue;
                }

                unresolved?.Add(s.Material!);
                idx[i] = 1.0;
                continue;
            }

            double n0 = pointed.IndexAt(lambdaUm);
            if (double.IsNaN(n0) || n0 <= 0.0)
            {
                unresolved?.Add(s.Material!);
                idx[i] = pointed.Nd > 0.0 ? pointed.Nd : 1.0;
                continue;
            }

            idx[i] = n0;
        }

        return idx;
    }

    public static double[] Build(OpticalSystem system, GlassCatalog catalog, double lambdaUm,
                                 IList<string>? unresolved = null,
                                 IList<string>? ambiguous = null)
    {
        int n = system.Surfaces.Count;
        var idx = new double[n];

        for (int i = 0; i < n; i++)
        {
            var s = system.Surfaces[i];

            if (s.ModelIndexEnabled && s.ModelNd > 0.0)
            {
                idx[i] = ModelIndex(s.ModelNd, s.ModelVd, s.ModelDPgF, lambdaUm);
                continue;
            }

            if (string.IsNullOrEmpty(s.Material) || s.IsMirror)
            {
                // Air, or a mirror in whatever medium precedes it.
                idx[i] = s.IsMirror && i > 0 ? idx[i - 1] : 1.0;
                continue;
            }

            var g = catalog.Find(s.Material, system.GlassCatalogs);

            // AMBIGUOUS EVEN THOUGH RESOLVED. Find always answers, and its answer is a fine one to
            // an unanswerable question - several formats carry no catalog at all, and three of them
            // ship an F4 that differ in the third decimal of nd. Saying which glasses had more than
            // one owner costs a dictionary probe; not saying so cost 1.3 per cent of a focal length
            // with nothing anywhere to suggest a glass had been chosen rather than read.
            //
            // The preference list is no protection when it was DEDUCED rather than declared. A
            // reader for a format that names no catalog may work the catalog out from the glass
            // names and record what it worked out, and that guess then steers Find as firmly as a
            // declaration would - so the name still had more than one owner, and the choice is
            // still the program's rather than the file's.
            if (ambiguous != null
                && string.IsNullOrEmpty(s.CatalogName)
                && (system.GlassCatalogsAreInferred
                    || system.GlassCatalogs == null || system.GlassCatalogs.Count == 0))
            {
                var owners = catalog.CatalogsContaining(s.Material);
                if (owners.Count > 1)
                {
                    string note = g != null && !string.IsNullOrEmpty(g.Catalog)
                        ? s.Material! + " (" + string.Join(", ", owners) + "; used " + g.Catalog + ")"
                        : s.Material! + " (" + string.Join(", ", owners) + ")";
                    if (!ambiguous.Contains(note)) ambiguous.Add(note);
                }
            }

            if (g == null)
            {
                // Not a name any loaded catalog knows. Before giving up, read it as a glass
                // code: whole prescriptions are written that way, and treating one as air
                // would silently flatten the lens instead of reporting a problem.
                if (GlassCode.TryParse(s.Material, out double codeNd, out double codeVd))
                {
                    idx[i] = ModelIndex(codeNd, codeVd, 0.0, lambdaUm);
                    continue;
                }

                unresolved?.Add(s.Material!);
                idx[i] = 1.0;                       // unknown glass behaves as air, and is reported
                continue;
            }

            double v = g.IndexAt(lambdaUm);
            if (double.IsNaN(v) || v <= 0.0)
            {
                unresolved?.Add(s.Material!);
                idx[i] = g.Nd > 0.0 ? g.Nd : 1.0;   // fall back to the catalog's own nd
                continue;
            }

            idx[i] = v;
        }

        return idx;
    }

    /// <summary>
    /// Index of a model glass, given by nd, Vd and dPgF: the LensHH-LT model glass.
    ///
    /// <code>
    /// n(λ) = nd + (nd − 1)/Vd · [ A(λ) + P·B(λ) ],   P = 0.6438 − 0.001682·Vd + dPgF
    /// A(λ) = −7.128490 + 4.952840/λ − 0.202244/λ^3.5
    /// B(λ) =  8.849310 − 6.916770/λ + 0.454415/λ^3.5          (λ in µm)
    /// </code>
    ///
    /// <para>The form is Conrady's dispersion formula, n = n0 + a/λ + b/λ^3.5. Its three
    /// constants per glass come from the glass's nd, its Abbe number Vd = (nd − 1)/(nF − nC)
    /// and its g–F partial dispersion P = (ng − nF)/(nF − nC), at d 0.5876, F 0.4861,
    /// C 0.6563 and g 0.4358 µm. dPgF is P's deviation from the normal line of Schott's
    /// Technical Information TIE-29, P = 0.6438 − 0.001682·Vd. It is the model LensHH-LT uses,
    /// so a model glass gets the same index here as in LensHH-LT.</para>
    ///
    /// <para>(This replaced a two-point interpolation in 1/λ² with a quadratic dPgF term, which
    /// differed from LensHH-LT.)</para>
    /// </summary>
    public static double ModelIndex(double nd, double vd, double dPgF, double lambdaUm)
    {
        if (lambdaUm <= 0.0 || nd <= 0.0) return double.NaN;
        if (Math.Abs(vd) < 1e-12) return nd;             // no dispersion information

        double inv = 1.0 / lambdaUm, inv35 = Math.Pow(lambdaUm, -3.5);
        double p = ModelA0 + ModelA1 * vd + dPgF;
        double f = (-7.128490 + 4.952840 * inv - 0.202244 * inv35)
                 + p * (8.849310 - 6.916770 * inv + 0.454415 * inv35);
        return nd + (nd - 1.0) / vd * f;
    }

    // The TIE-29 normal line, P_gF = ModelA0 + ModelA1·Vd.
    private const double ModelA0 = 0.6438, ModelA1 = -0.001682;

    /// <summary>
    /// The model glass whose index is exactly the Conrady curve n = c0 + c1/λ + c2/λ^3.5 (λ in
    /// µm). The model glass is itself such a curve, and its three parameters map one-to-one onto
    /// the curve's constants. So a table of three indices, say, is a model glass exactly. Null
    /// when the mapping is singular. A curve with no dispersion (c1 = c2 = 0) is returned as Vd 0,
    /// a constant index.
    /// </summary>
    public static (double Nd, double Vd, double DPgF)? ModelFromConrady(double c0, double c1, double c2)
    {
        if (c1 == 0.0 && c2 == 0.0) return (c0, 0.0, 0.0);
        const double cA0 = -7.128490, cA1 = 4.952840, cA2 = -0.202244, cB0 = 8.849310, cB1 = -6.916770, cB2 = 0.454415;
        double den = c2 * cB1 - c1 * cB2;
        if (Math.Abs(den) < 1e-300) return null;
        double p = (c1 * cA2 - c2 * cA1) / den;
        double s1 = cA1 + p * cB1;
        if (Math.Abs(s1) < 1e-300) return null;
        double q = c1 / s1;
        if (Math.Abs(q) < 1e-300) return null;
        double nd = c0 - q * (cA0 + p * cB0);
        double vd = (nd - 1.0) / q;
        return (nd, vd, p - (ModelA0 + ModelA1 * vd));
    }
}
