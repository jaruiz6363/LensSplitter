extern alias SeriesAd;

using System;
using System.Collections.Generic;

using SAb = SeriesAd::AberrationCalculator.Core.Aberrations;
using SEnums = SeriesAd::AberrationCalculator.Core.Enums;
using SModels = SeriesAd::AberrationCalculator.Core.Models;
using SRay = SeriesAd::AberrationCalculator.Core.RayTrace;
using Lau = SeriesAd::AberrationCalculator.Core.SeriesArithmetic.LaurentSeries;
using DS = SeriesAd::AberrationCalculator.Core.SeriesAd.DualSeries;
using DSMath = SeriesAd::AberrationCalculator.Core.SeriesAd.DualSeriesMath;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// <b>A figured flat facing collimated light, differentiated.</b>
///
/// <para>The plain-double build reaches this design's coefficients by running the whole chain in
/// Laurent series arithmetic with the flat surface's curvature as the series variable, and taking
/// the e^0 term. For a long time that was all it could do, so the OPTIMISER refused such a design
/// outright: in this build the partial method had no body, the call compiled away, and the result
/// would have been a right value beside a silently wrong derivative. That refusal was the last
/// design this optimiser turned away.</para>
///
/// <para>This is the same route in <c>DualSeries</c> - a dual number whose value and derivative
/// are each a Laurent series - so value and derivative come out of one run. Extracting the e^0
/// coefficient is linear, so the e^0 term of the derivative series IS the derivative of the e^0
/// term; nothing has to be re-derived to justify reading them off independently.</para>
///
/// <para><b>Why this file exists beside the double one instead of being shared with it.</b> The
/// formulas are not duplicated - every one of them is in the linked Core sources, compiled here
/// against <c>DualSeries</c>. What is repeated is about thirty lines of ORCHESTRATION: trace,
/// coefficients, scheme, Table I, increments, tau, convert. The double file carries a large
/// diagnostic apparatus this build has no use for, and threading a fifth arithmetic through it
/// with conditional compilation would have put a dozen <c>#if</c>s inside the one routine anyone
/// debugging this will read. The risk that the two orchestrations drift is real and is answered
/// by a test rather than by a comment: <c>FlatCollimatedDerivativeTests</c> requires this route's
/// VALUE to equal the double route's on the same design, so a divergence fails the build rather
/// than quietly making the derivative belong to a different calculation from the value.</para>
/// </summary>
public static partial class TertiaryCoefficients
{
    static partial void FlatCollimatedTau(Models.OpticalSystem system, Scalar[] indices,
                                          Scalar maxField, List<int> flatSurfaces,
                                          ref Scalar[]? transverse, ref Scalar[]?[]? perSurface)
    {
        int savedMin = Lau.MinOrder, savedMax = Lau.MaxOrder;
        try
        {
            // The same two truncations, and the same rule: the answer is used only if the two
            // agree, nothing fell below the lowest carried order, no leading term was stepped
            // past in a division, and nothing is left at a negative order. A route that cannot
            // vouch for itself returns nothing and the caller keeps what it had.
            var wide = Run(system, indices, maxField, flatSurfaces, 32,
                           out int underflows, out double dropped, out double negative,
                           out var widePer, out double widePerNegative);
            var narrow = Run(system, indices, maxField, flatSurfaces, 24, out _, out _, out _,
                             out var narrowPer, out _);
            if (wide == null || narrow == null) return;

            double largest = 0.0;
            for (int k = 2; k <= 20; k++) largest = Math.Max(largest, Math.Abs(wide[k].Value));

            double disagreement = 0.0;
            bool finite = true;
            for (int k = 2; k <= 20; k++)
            {
                if (double.IsNaN(wide[k].Value) || double.IsInfinity(wide[k].Value)) finite = false;
                if (double.IsNaN(wide[k].Deriv) || double.IsInfinity(wide[k].Deriv)) finite = false;
                if (largest > 0.0)
                    disagreement = Math.Max(disagreement,
                                            Math.Abs(wide[k].Value - narrow[k].Value) / largest);
            }

            bool converged = finite && largest > 0.0 && underflows == 0 && dropped < 1e-10
                             && disagreement < 1e-9 && negative < 1e-8;
            if (!converged) return;
            transverse = wide;

            // The split by surface, held to the same rule as the double route so that value and
            // derivative always come from the same attribution: no negative orders left in any
            // share, and the two truncations agreeing on each.
            bool splits = widePer != null && narrowPer != null && widePerNegative < 1e-8;
            if (splits)
                for (int i = 0; i < widePer!.Length && splits; i++)
                    if (widePer[i] is { } w)
                        for (int k = 2; k <= 20; k++)
                        {
                            double n = narrowPer![i] is { } nw ? nw[k].Value : double.NaN;
                            if (double.IsNaN(w[k].Value) || double.IsInfinity(w[k].Value)
                                || double.IsNaN(w[k].Deriv) || double.IsInfinity(w[k].Deriv)
                                || !(Math.Abs(w[k].Value - n) / largest < 1e-9)) { splits = false; break; }
                        }
            if (splits) perSurface = widePer;
        }
        finally
        {
            Lau.MinOrder = savedMin;
            Lau.MaxOrder = savedMax;
        }
    }

    /// <summary>One pass at one truncation width. Null when the chain cannot form an answer.</summary>
    private static Scalar[]? Run(Models.OpticalSystem core, Scalar[] indices, Scalar maxField,
                                 IReadOnlyCollection<int> seeded, int window,
                                 out int underflows, out double dropped, out double negative,
                                 out Scalar[]?[]? perSurface, out double perSurfaceNegative)
    {
        Lau.MinOrder = -window;
        Lau.MaxOrder = window;
        Lau.ResetDiagnostics();
        underflows = 0;
        dropped = 0.0;
        negative = double.NaN;
        perSurface = null;
        perSurfaceNegative = double.NaN;

        var sys = ToSeries(core, seeded);
        var n = new DS[indices.Length];
        for (int k = 0; k < indices.Length; k++) n[k] = Lift(indices[k]);

        int stop = sys.StopSurfaceIndex;
        if (stop < 0 || stop >= sys.Surfaces.Count) return null;

        var p = SRay.ParaxialTrace.Trace(sys, n, Lift(maxField));
        var b = SAb.BuchdahlCoefficients.Compute(sys, p);

        DS objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = DS.IsInfinity(objectDistance);
        DS iota = infinite ? (DS)0.0 : -p.Efl / objectDistance;

        var scheme = SAb.BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                                sys.Surfaces[stop].SemiDiameter, iota);
        // The stop parameter comes FIRST, because the all-spherical table the increments are
        // differenced against has to be built at the same pupil as the run they are fed back
        // into. At a finite conjugate that is not scheme.P. See the note in
        // TertiaryCoefficients.Attach, where the mismatch cost 4.7E-5 against Forbes.
        DS stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;

        var spherical = SAb.BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter,
                                                   iota: iota);
        int last = sys.LastOpticalSurface();
        var increments = SAb.AsphericSchemeIncrements.Build(b, spherical, last);
        if (increments == null) return null;
        DS g = 1.0 - stopParameter * iota;
        DS lengthFactor = p.Efl / (p.N[last] * scheme.PRayFinalAngle);
        DS u = -(0.5 * p.Epd / p.Efl) / g;

        // The object index, for the same reason as in the ordinary route: the q ray is started
        // reduced so the pair's Lagrange invariant is one, and the physical chief ray is N_0
        // times it. See the note at the ray start in BuchdahlTableI.
        DS nObject = DSMath.Abs(p.N[0]);

        DS hmax = nObject * (infinite
            ? DSMath.Tan(Lift(maxField) * Math.PI / 180.0)
            : -(p.ParaxialImageHeight / p.Magnification) / objectDistance);

        var dual = SAb.AsphericSchemeIncrements.BuildDual(sys, p, n, stopParameter, iota);
        var surfaceT = new DS[sys.Surfaces.Count][];
        var surfaceTbar = new DS[sys.Surfaces.Count][];
        var raw = SAb.BuchdahlAsphericScheme.Tau(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                                 iota, SAb.BuchdahlAsphericScheme.Options.Default,
                                                 dual,
                                                 (i, hat, hatBar, check, checkBar) =>
                                                 {
                                                     var T = new DS[11];
                                                     var Tb = new DS[11];
                                                     for (int m = 1; m <= 10; m++)
                                                     {
                                                         T[m] = hat[m] + check[m];
                                                         Tb[m] = hatBar[m] + checkBar[m];
                                                     }
                                                     surfaceT[i] = T;
                                                     surfaceTbar[i] = Tb;
                                                 });
        var tau = SAb.TertiaryCoefficients.ToTransverse(raw, lengthFactor, u, hmax);

        var result = new Scalar[21];
        double largest = 0.0, worstNegative = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            if (DS.IsNaN(tau[k])) return null;
            result[k] = Lower(tau[k]);
            largest = Math.Max(largest, Math.Abs(result[k].Value));
        }

        // A term left at a negative order is a pole that did not cancel, which means the limit was
        // never taken. Judged on the value, as every diagnostic here is: a derivative series with
        // a surviving pole would be caught by its value having one too.
        for (int k = 1; k <= 20; k++)
            for (int order = tau[k].Value.LowestOrder; order < 0; order++)
                worstNegative = Math.Max(worstNegative, Math.Abs(tau[k].Value.Coefficient(order)));

        // Each surface's share, read at e^0 like the total; see the double route.
        var shares = new Scalar[]?[sys.Surfaces.Count];
        double worstShareNegative = 0.0;
        for (int i = 0; i < shares.Length; i++)
        {
            if (surfaceT[i] == null) continue;
            var st = SAb.TertiaryCoefficients.ToTransverse(
                SAb.TertiaryCoefficients.AssembleTau(surfaceT[i], surfaceTbar[i]), lengthFactor, u, hmax);
            var c = new Scalar[21];
            for (int k = 2; k <= 20; k++)
            {
                if (DS.IsNaN(st[k])) { c[k] = double.NaN; continue; }
                c[k] = Lower(st[k]);
                for (int order = st[k].Value.LowestOrder; order < 0; order++)
                    worstShareNegative = Math.Max(worstShareNegative, Math.Abs(st[k].Value.Coefficient(order)));
            }
            shares[i] = c;
        }

        underflows = Lau.Underflows;
        dropped = Lau.WorstDroppedLeading;
        negative = largest > 0.0 ? worstNegative / largest : double.NaN;
        perSurface = shares;
        perSurfaceNegative = largest > 0.0 ? worstShareNegative / largest : double.NaN;
        return result;
    }

    /// <summary>A dual carried into the series arithmetic as a dual of constant series.</summary>
    private static DS Lift(Scalar x) => new DS(x.Value, x.Deriv);

    /// <summary>
    /// The finite part, back out: the e^0 coefficient of the value and of the derivative. Both are
    /// read from the same run, and the extraction is linear, which is why they can be read
    /// independently.
    /// </summary>
    private static Scalar Lower(DS s) => new Scalar(s.Value.Constant, s.Deriv.Constant);

    /// <summary>
    /// The design in series-of-dual arithmetic, with the flat surfaces' curvatures carried as
    /// <c>c + e</c>.
    ///
    /// <para>The seed goes on the VALUE half only. The series variable is a perturbation of the
    /// curvature used to take a limit; it is not the quantity being differentiated with respect
    /// to, and putting it on the derivative half would mix the two.</para>
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
                                            Lift(core.Aperture.Value)),
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
                Curvature = set.Contains(i)
                          ? new DS(Lau.Linear(s.Curvature.Value, 1.0), s.Curvature.Deriv)
                          : Lift(s.Curvature),
                Thickness = Lift(s.Thickness),
                Conic = Lift(s.Conic),
                Material = s.Material,
                CatalogName = s.CatalogName,
                IsStop = s.IsStop,
                SemiDiameter = Lift(s.SemiDiameter),
                SemiDiameterMode = (SEnums.SemiDiameterMode)(int)s.SemiDiameterMode,
                ClearAperturePercent = Lift(s.ClearAperturePercent),
                ObscurationRadius = Lift(s.ObscurationRadius),
                Comment = s.Comment,
                ModelIndexEnabled = s.ModelIndexEnabled,
                ModelNd = Lift(s.ModelNd),
                ModelVd = Lift(s.ModelVd),
                ModelDPgF = Lift(s.ModelDPgF),
                FocalLength = Lift(s.FocalLength),
                FloatingApertureRadius = Lift(s.FloatingApertureRadius),
                ClapOuterRadius = Lift(s.ClapOuterRadius),
                InnerRadius = Lift(s.InnerRadius),
                HasMarginalRaySolve = s.HasMarginalRaySolve,
            };

            for (int k = 0; k < s.AsphericCoefficients.Length
                            && k < d.AsphericCoefficients.Length; k++)
                d.AsphericCoefficients[k] = Lift(s.AsphericCoefficients[k]);

            for (int k = 0; k < s.Parameters.Length; k++) d.SetParameter(k, Lift(s.Parameters[k]));
            for (int k = 0; k < s.Settings.Length; k++) d.SetSetting(k, s.Settings[k]);

            sys.Surfaces.Add(d);
        }

        return sys;
    }
}
