using System;
using System.Collections.Generic;
using System.Text;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// Designs this optimiser can evaluate, checked once before anything runs.
///
/// <para><b>It used to refuse two things and now refuses neither by class.</b> First every figured
/// design, while Buchdahl's aspheric seventh order was a reconstruction the rays rejected; that
/// arrangement is now established and figuring is carried. Then the figured flat facing collimated
/// light, because the coefficients there are reached only as the e^0 term of a Laurent series in
/// that surface's curvature, and that route existed in the plain-double build alone - so the
/// optimiser would have had a right value beside a silently wrong derivative. It now exists here
/// too, in <c>DualSeries</c>, and that design optimises like any other.</para>
///
/// <para><b>What remains is not a class of design but a failure to converge.</b> The series route
/// vouches for itself or it is not used: two truncations must agree, nothing may fall below the
/// lowest carried order, no leading term may have been stepped past in a division, and nothing may
/// be left at a negative order. On a design where it cannot say that, the tertiary would silently
/// keep the ordinary chain's values - which at such a surface are not finite in any useful sense -
/// so the run is refused. That is a measurement on the design in front of it rather than a rule
/// about a shape, which is the difference worth keeping.</para>
/// </summary>
public static class SupportedDesign
{
    /// <summary>The line the report prints about where the coefficients came from.</summary>
    public const string Explanation =
        "Seventh order by BUCHDAHL's computing scheme, which is closed-form sums over the "
      + "paraxial ray data and the fastest route there is. A figured surface takes the aspheric "
      + "arrangement of his Sec. 85, which agrees with Forbes' series trace on all twenty "
      + "tertiary coefficients to 2E-10 or better on a refracting design, and with reflected "
      + "real rays on a mirror, which Forbes declines; a spherical design takes his own published "
      + "table, bit for bit as it always did; and a figured flat facing collimated light takes "
      + "the same chain in Laurent series arithmetic, differentiated, with the limit read at "
      + "e^0.";

    /// <summary>
    /// Throws unless the design is one the evaluation loop can carry.
    ///
    /// <para>Called once, before the first evaluation.</para>
    /// </summary>
    public static void Require(Design design)
    {
        if (design == null) throw new ArgumentNullException(nameof(design));

        var series = SeriesOnly(design);
        if (series.Count == 0) return;

        // There IS a figured flat in collimated light. That is no longer a refusal on its own -
        // what matters is whether the series route can vouch for its answer on this design.
        if (SeriesRouteConverges(design)) return;

        var sb = new StringBuilder();
        sb.Append("Surface ");
        sb.Append(series.Count == 1 ? series[0].ToString() : Join(series));
        sb.Append(series.Count == 1 ? " is a FIGURED FLAT facing collimated light"
                                    : " are FIGURED FLATS facing collimated light");
        sb.Append(", and the series route cannot reach its coefficients on this design.\n\n");
        sb.Append("There the marginal incidence is identically zero, the incidence ratio is "
                + "infinite, and the finite coefficients arrive only after terms carrying "
                + "different powers of it cancel. They are reached by running the whole chain in "
                + "Laurent series arithmetic with that surface's curvature as the variable and "
                + "reading the limit at e^0 - but that route is used only when it can vouch for "
                + "itself, and on this design it cannot: two truncations disagreed, or a term "
                + "fell below the carried order, or a pole failed to cancel.\n\n");
        sb.Append("Bend the surface and the singularity is gone - at R = 100 the two routes agree "
                + "to 1.6E-12. Or analyse it without optimising: `abcalc <lens>` and `--forbes` "
                + "report what they can and say which route they used.");
        throw new NotSupportedException(sb.ToString());
    }

    /// <summary>
    /// Whether the series route reaches an answer it will stand behind on this design.
    ///
    /// <para>Asked of the plain-double route, which shares its convergence rule with the
    /// differentiated one: the same two truncations, the same underflow, dropped-leading and
    /// negative-order tests. If the value converges the derivative does, because they are
    /// coefficients of the same two series produced by the same run.</para>
    /// </summary>
    private static bool SeriesRouteConverges(Design design)
    {
        try
        {
            var system = design.System;
            var indices = design.Indices(design.PrimaryWave);
            var paraxial = Core.RayTrace.ParaxialTrace.Trace(system, indices, design.MaxField);
            var flats = Core.Aberrations.TertiaryCoefficients.SeriesOnlySurfaces(system, indices,
                                                                                paraxial);
            if (flats.Count == 0) return true;

            var result = Core.Aberrations.TertiaryCoefficients.SeriesTau(
                system, indices, design.MaxField, flats);
            return result.Converged;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The figured flats in collimated light, or an empty list when the design cannot be traced
    /// at all.
    ///
    /// <para>A design that will not trace is not refused HERE. It has a fault the optimiser's own
    /// first evaluation will report far better than this check could, and swallowing it into a
    /// message about aspheric flats would be actively misleading.</para>
    /// </summary>
    private static List<int> SeriesOnly(Design design)
    {
        try
        {
            var system = design.System;
            var indices = design.Indices(design.PrimaryWave);
            var paraxial = Core.RayTrace.ParaxialTrace.Trace(system, indices, design.MaxField);
            return Core.Aberrations.TertiaryCoefficients.SeriesOnlySurfaces(system, indices,
                                                                           paraxial);
        }
        catch (Exception)
        {
            return new List<int>();
        }
    }

    private static string Join(List<int> values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(i == values.Count - 1 ? " and " : ", ");
            sb.Append(values[i]);
        }
        return sb.ToString();
    }
}
