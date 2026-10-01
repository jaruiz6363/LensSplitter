extern alias Series;

using System;
using System.Collections.Generic;

using SAb = Series::AberrationCalculator.Core.Aberrations;
using SEnums = Series::AberrationCalculator.Core.Enums;
using SModels = Series::AberrationCalculator.Core.Models;
using SRay = Series::AberrationCalculator.Core.RayTrace;
using Laurent = Series::AberrationCalculator.Core.SeriesArithmetic.LaurentSeries;
using LaurentMath = Series::AberrationCalculator.Core.SeriesArithmetic.LaurentMath;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// <b>A figured flat facing collimated light</b>, by the same formulas in Laurent series arithmetic.
///
/// <para>At such a surface the marginal incidence is identically zero, so the incidence ratio q
/// is infinite, and every formula in the tertiary chain that divides by that incidence has no
/// finite form. The coefficients the chain reports are finite all the same - bend the surface a
/// little and the aspheric routine agrees with Forbes' series trace to 1E-12 - but the finite
/// value arrives only after large terms carrying different powers of q cancel, so neither the
/// flat branch nor a numerical limit recovers it (the first drops the figured tertiary
/// altogether, the second is good to a few parts in ten thousand).</para>
///
/// <para>Here the surface's curvature is seeded as the series variable e and the whole chain -
/// paraxial trace, fifth-order code, the bridge, Table I, the aspheric routine with its dual run,
/// the transverse conversion - runs unchanged in <c>AberrationCalculator.Core.Series</c>, the
/// Laurent build of these same source files. Each power of the vanishing incidence becomes an
/// explicit negative power of e, the cancellation happens coefficient by coefficient, and the
/// answer is the coefficient of e^0.</para>
///
/// <para><b>It vouches for itself or it is not used.</b> The route runs at two truncations and
/// the answer is taken only if the two agree, nothing fell below the lowest carried order, no
/// division stepped past a leading coefficient larger than rounding, and the negative orders of
/// the result cancelled.</para>
/// </summary>
public static partial class TertiaryCoefficients
{
    /// <summary>What the series route produced, and whether it can be trusted.</summary>
    /// <param name="Tau">Transverse tau, indexed 1..20; tau1 is not the fifth-order B7 here.</param>
    /// <param name="Converged">True when every self-check passed.</param>
    /// <param name="Underflows">Terms that fell below the lowest carried order.</param>
    /// <param name="WorstDroppedLeading">Largest leading coefficient a division stepped past.</param>
    /// <param name="NegativeOrderResidue">Largest negative-order coefficient of any tau, relative to the largest tau.</param>
    /// <param name="TruncationDisagreement">Largest change between the two truncations, relative to the largest tau.</param>
    public sealed record SeriesTauResult(double[] Tau, bool Converged, int Underflows,
                                         double WorstDroppedLeading, double NegativeOrderResidue,
                                         double TruncationDisagreement)
    {
        /// <summary>
        /// Each surface's share of <see cref="Tau"/>, transverse, indexed [surface][1..20]; null
        /// when some surface's share is not finite - carries negative orders that cancel only in
        /// the sum - or the two truncations disagree on it. The shares sum to <see cref="Tau"/>.
        /// </summary>
        public double[]?[]? PerSurface { get; init; }

        /// <summary>DIAGNOSTIC: per stage of the chain, the lowest order present and where it first appeared.</summary>
        public string Trace { get; init; } = "";
    }

    [ThreadStatic] private static System.Text.StringBuilder? _trace;

    private static void Stage(string name, IEnumerable<(string Where, Laurent Value)> values)
    {
        if (_trace == null) return;
        int lowest = 0;
        string first = "";
        foreach (var (where, v) in values)
            if (v.LowestOrder < lowest) { lowest = v.LowestOrder; if (first.Length == 0) first = where; }
        _trace.Append(name).Append(": lowest order ").Append(lowest)
              .Append(first.Length > 0 ? " first at " + first : "")
              .Append(", underflows so far ").Append(Laurent.Underflows).Append('\n');
    }

    private static IEnumerable<(string, Laurent)> RowsOf(SAb.BuchdahlTableIRow[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            for (int k = 1; k < rows[i].T.Length; k++) yield return ($"row {i} t{k}", rows[i].T[k]);
            for (int k = 1; k < rows[i].Y.Length; k++) yield return ($"row {i} Y{k}", rows[i].Y[k]);
        }
    }

    static partial void FlatCollimatedTau(Models.OpticalSystem system, double[] indices,
                                          double maxField, List<int> flatSurfaces,
                                          ref double[]? transverse, ref double[]?[]? perSurface)
    {
        var result = SeriesTau(system, indices, maxField, flatSurfaces);
        if (result.Converged)
        {
            transverse = result.Tau;
            perSurface = result.PerSurface;
        }
    }

    /// <summary>
    /// Transverse tau with the curvatures of <paramref name="seeded"/> surfaces carried as
    /// <c>c + e</c>, read at e^0. On surfaces that are not singular this reproduces the double
    /// route, which is how the series arithmetic itself is gated.
    /// </summary>
    public static SeriesTauResult SeriesTau(Models.OpticalSystem system, double[] indices,
                                            double maxField, IReadOnlyCollection<int> seeded)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (seeded == null) throw new ArgumentNullException(nameof(seeded));

        int savedMin = Laurent.MinOrder, savedMax = Laurent.MaxOrder;
        _trace = new System.Text.StringBuilder();
        try
        {
            var wide = Run(system, indices, maxField, seeded, 32,
                           out int underflows, out double dropped, out double negative,
                           out var widePer, out double widePerNegative);
            string trace = _trace.ToString();
            _trace = null;
            var narrow = Run(system, indices, maxField, seeded, 24, out _, out _, out _,
                             out var narrowPer, out _);

            if (wide == null || narrow == null)
                return new SeriesTauResult(new double[21], false, underflows, dropped, negative,
                                           double.NaN) { Trace = trace };

            double largest = 0.0;
            for (int k = 2; k <= 20; k++) largest = Math.Max(largest, Math.Abs(wide[k]));
            double disagreement = 0.0;
            bool finite = true;
            for (int k = 2; k <= 20; k++)
            {
                if (double.IsNaN(wide[k]) || double.IsInfinity(wide[k])) finite = false;
                if (largest > 0.0)
                    disagreement = Math.Max(disagreement, Math.Abs(wide[k] - narrow[k]) / largest);
            }

            bool converged = finite && largest > 0.0 && underflows == 0 && dropped < 1e-10
                             && disagreement < 1e-9 && negative < 1e-8;

            // The split is held to the same standard as the total, surface by surface: no
            // negative orders left in any share, and the two truncations agreeing on each.
            bool splits = converged && widePer != null && narrowPer != null
                          && widePerNegative < 1e-8;
            if (splits)
                for (int i = 0; i < widePer!.Length && splits; i++)
                    if (widePer[i] is { } w)
                        for (int k = 2; k <= 20; k++)
                        {
                            double n = narrowPer![i]?[k] ?? double.NaN;
                            if (double.IsNaN(w[k]) || double.IsInfinity(w[k])
                                || !(Math.Abs(w[k] - n) / largest < 1e-9)) { splits = false; break; }
                        }

            return new SeriesTauResult(wide, converged, underflows, dropped, negative, disagreement)
                { Trace = trace, PerSurface = splits ? widePer : null };
        }
        finally
        {
            _trace = null;
            Laurent.MinOrder = savedMin;
            Laurent.MaxOrder = savedMax;
        }
    }

    private static double[]? Run(Models.OpticalSystem core, double[] indices, double maxField,
                                 IReadOnlyCollection<int> seeded, int window,
                                 out int underflows, out double dropped, out double negative,
                                 out double[]?[]? perSurface, out double perSurfaceNegative)
    {
        Laurent.MinOrder = -window;
        Laurent.MaxOrder = window;
        Laurent.ResetDiagnostics();
        underflows = 0;
        dropped = 0.0;
        negative = double.NaN;
        perSurface = null;
        perSurfaceNegative = double.NaN;

        var sys = ToSeries(core, seeded);
        var n = new Laurent[indices.Length];
        for (int k = 0; k < indices.Length; k++) n[k] = indices[k];

        int stop = sys.StopSurfaceIndex;
        if (stop < 0 || stop >= sys.Surfaces.Count) return null;

        var p = SRay.ParaxialTrace.Trace(sys, n, maxField);
        Stage("paraxial", ParaxialValues(p));
        var b = SAb.BuchdahlCoefficients.Compute(sys, p);
        Stage("fifth-order code", TermsOf(b));

        Laurent objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = Laurent.IsInfinity(objectDistance);
        Laurent iota = infinite ? (Laurent)0.0 : -p.Efl / objectDistance;

        var scheme = SAb.BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                                sys.Surfaces[stop].SemiDiameter, iota);
        Stage("scheme P", new[] { ("P", scheme.P), ("PRayFinalAngle", scheme.PRayFinalAngle) });

        // The stop parameter comes FIRST, because the all-spherical table the increments are
        // differenced against has to be built at the same pupil as the run they are fed back
        // into. At a finite conjugate that is not scheme.P - the derived value rests on an
        // identity a non-zero iota breaks - and building the reference at one and the run at
        // the other leaves the difference between the two in the answer. See the same note in
        // TertiaryCoefficients.Attach, where it cost 4.7E-5 against Forbes.
        Laurent stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;

        var spherical = SAb.BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter,
                                                   iota: iota);
        Stage("Table I, spherical", RowsOf(spherical));
        int last = sys.LastOpticalSurface();
        var increments = SAb.AsphericSchemeIncrements.Build(b, spherical, last);
        if (increments == null) return null;
        Stage("increments", IncrementsOf(increments));

        Laurent g = 1.0 - stopParameter * iota;
        Laurent lengthFactor = p.Efl / (p.N[last] * scheme.PRayFinalAngle);
        Laurent u = -(0.5 * p.Epd / p.Efl) / g;

        // The object index, for the same reason as in the ordinary route: the q ray is started
        // reduced so the pair's invariant is one, and the physical chief ray is N_0 times it.
        Laurent nObject = LaurentMath.Abs(p.N[0]);
        if (LaurentMath.Vanishes(nObject, 1e-12)) nObject = 1.0;

        Laurent hmax = nObject * (infinite
            ? LaurentMath.Tan(maxField * Math.PI / 180.0)
            : -(p.ParaxialImageHeight / p.Magnification) / objectDistance);

        var dual = SAb.AsphericSchemeIncrements.BuildDual(sys, p, n, stopParameter, iota);
        if (dual != null) Stage("dual increments", IncrementsOf(dual));
        if (_trace != null)
        {
            var figured = SAb.BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter,
                                                     increments, iota: iota);
            Stage("Table I, figured", RowsOf(figured));
        }
        var surfaceT = new Laurent[sys.Surfaces.Count][];
        var surfaceTbar = new Laurent[sys.Surfaces.Count][];
        var raw = SAb.BuchdahlAsphericScheme.Tau(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                                 iota, SAb.BuchdahlAsphericScheme.Options.Default,
                                                 dual,
                                                 (i, hat, hatBar, check, checkBar) =>
                                                 {
                                                     var T = new Laurent[11];
                                                     var Tb = new Laurent[11];
                                                     for (int m = 1; m <= 10; m++)
                                                     {
                                                         T[m] = hat[m] + check[m];
                                                         Tb[m] = hatBar[m] + checkBar[m];
                                                     }
                                                     surfaceT[i] = T;
                                                     surfaceTbar[i] = Tb;
                                                 });
        var rawList = new List<(string, Laurent)>();
        for (int k = 1; k <= 20; k++) rawList.Add(($"raw tau{k}", raw[k]));
        Stage("aspheric routine, raw tau", rawList);
        var tau = SAb.TertiaryCoefficients.ToTransverse(raw, lengthFactor, u, hmax);

        var constant = new double[21];
        double largest = 0.0, worstNegative = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            if (Laurent.IsNaN(tau[k])) return null;
            constant[k] = tau[k].Constant;
            largest = Math.Max(largest, Math.Abs(constant[k]));
        }
        for (int k = 1; k <= 20; k++)
            for (int order = tau[k].LowestOrder; order < 0; order++)
                worstNegative = Math.Max(worstNegative, Math.Abs(tau[k].Coefficient(order)));

        // Each surface's share, read at e^0 like the total. A share may carry negative orders
        // that cancel only against another surface's; then it has no finite value of its own,
        // and the worst such residue says so.
        var shares = new double[]?[sys.Surfaces.Count];
        double worstShareNegative = 0.0;
        for (int i = 0; i < shares.Length; i++)
        {
            if (surfaceT[i] == null) continue;
            var st = SAb.TertiaryCoefficients.ToTransverse(
                SAb.TertiaryCoefficients.AssembleTau(surfaceT[i], surfaceTbar[i]), lengthFactor, u, hmax);
            var c = new double[21];
            for (int k = 2; k <= 20; k++)
            {
                if (Laurent.IsNaN(st[k])) { c[k] = double.NaN; continue; }
                c[k] = st[k].Constant;
                for (int order = st[k].LowestOrder; order < 0; order++)
                    worstShareNegative = Math.Max(worstShareNegative, Math.Abs(st[k].Coefficient(order)));
            }
            shares[i] = c;
        }

        underflows = Laurent.Underflows;
        dropped = Laurent.WorstDroppedLeading;
        negative = largest > 0.0 ? worstNegative / largest : double.NaN;
        perSurface = shares;
        perSurfaceNegative = largest > 0.0 ? worstShareNegative / largest : double.NaN;
        return constant;
    }

    private static IEnumerable<(string, Laurent)> ParaxialValues(SRay.ParaxialResult p)
    {
        yield return ("Efl", p.Efl);
        yield return ("Epd", p.Epd);
        yield return ("EntrancePupilPosition", p.EntrancePupilPosition);
        for (int i = 0; i < p.Y.Length; i++)
        {
            yield return ($"Y[{i}]", p.Y[i]);
            yield return ($"U[{i}]", p.U[i]);
            yield return ($"Ybar[{i}]", p.Ybar[i]);
            yield return ($"Ubar[{i}]", p.Ubar[i]);
        }
    }

    private static IEnumerable<(string, Laurent)> TermsOf(SAb.BuchdahlResult b)
    {
        var fields = typeof(SAb.BuchdahlTerms).GetFields();
        for (int i = 0; i < b.Intrinsic.Length; i++)
            foreach (var f in fields)
                if (f.FieldType == typeof(Laurent))
                    yield return ($"intrinsic[{i}].{f.Name}", (Laurent)f.GetValue(b.Intrinsic[i])!);
        for (int i = 0; i < b.Aspheric.Length; i++)
            if (b.Aspheric[i] is { } a)
                foreach (var f in fields)
                    if (f.FieldType == typeof(Laurent))
                        yield return ($"aspheric[{i}].{f.Name}", (Laurent)f.GetValue(a)!);
    }

    private static IEnumerable<(string, Laurent)> IncrementsOf(Laurent[][] increments)
    {
        for (int i = 0; i < increments.Length; i++)
            if (increments[i] != null)
                for (int k = 0; k < increments[i].Length; k++)
                    yield return ($"surface {i} [{k}]", increments[i][k]);
    }

    /// <summary>
    /// The design in series arithmetic: a field-for-field copy, as the dual-number bridge makes
    /// one, with the curvature of each seeded surface carried as <c>c + e</c>.
    /// </summary>
    private static SModels.OpticalSystem ToSeries(Models.OpticalSystem core,
                                                  IReadOnlyCollection<int> seeded)
    {
        var sys = new SModels.OpticalSystem
        {
            Title = core.Title,
            Designer = core.Designer,
            Notes = core.Notes,
            FieldType = (SEnums.FieldType)(int)core.FieldType,
            IsAfocal = core.IsAfocal,
            TelecentricObjectSpace = core.TelecentricObjectSpace,
            RayAiming = (SEnums.RayAimingMode)(int)core.RayAiming,
            SemiDiameterSolve = (SEnums.SemiDiameterSolve)(int)core.SemiDiameterSolve,
            Aperture = new SModels.Aperture((SEnums.ApertureType)(int)core.Aperture.Type,
                                            core.Aperture.Value),
        };

        foreach (var w in core.Wavelengths)
            sys.Wavelengths.Add(new SModels.Wavelength(w.Value, w.Weight, w.IsPrimary));
        foreach (var f in core.Fields)
            sys.Fields.Add(new SModels.Field(f.Y, f.Weight) { X = f.X });

        var set = new HashSet<int>(seeded);
        for (int i = 0; i < core.Surfaces.Count; i++)
        {
            var s = core.Surfaces[i];
            var d = new SModels.Surface
            {
                Index = s.Index,
                Type = (SEnums.SurfaceType)(int)s.Type,
                Curvature = set.Contains(i) ? Laurent.Linear(s.Curvature, 1.0) : s.Curvature,
                Thickness = s.Thickness,
                Conic = s.Conic,
                Material = s.Material,
                CatalogName = s.CatalogName,
                IsStop = s.IsStop,
                SemiDiameter = s.SemiDiameter,
                SemiDiameterMode = (SEnums.SemiDiameterMode)(int)s.SemiDiameterMode,
                ClearAperturePercent = s.ClearAperturePercent,
                ObscurationRadius = s.ObscurationRadius,
                Comment = s.Comment,
                ModelIndexEnabled = s.ModelIndexEnabled,
                ModelNd = s.ModelNd,
                ModelVd = s.ModelVd,
                ModelDPgF = s.ModelDPgF,
                FocalLength = s.FocalLength,
                FloatingApertureRadius = s.FloatingApertureRadius,
                ClapOuterRadius = s.ClapOuterRadius,
                InnerRadius = s.InnerRadius,
                HasMarginalRaySolve = s.HasMarginalRaySolve,
            };

            for (int k = 0; k < s.AsphericCoefficients.Length && k < d.AsphericCoefficients.Length; k++)
                d.AsphericCoefficients[k] = s.AsphericCoefficients[k];
            for (int k = 0; k < s.Parameters.Length; k++) d.SetParameter(k, s.Parameters[k]);
            for (int k = 0; k < s.Settings.Length; k++) d.SetSetting(k, s.Settings[k]);

            sys.Surfaces.Add(d);
        }
        return sys;
    }
}
