using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// What each coefficient is called in the literature and in other design programs.
///
/// The coefficients this program reports carry Rimmer's names - B, F, C, Pi, B5, M1 and
/// so on - which say nothing about what the aberration IS. A designer looking at a table
/// of eighteen two-character labels cannot see that M1, M2 and M3 are oblique spherical
/// aberration, which is the term Buchdahl singled out as the hardest to control and the
/// usual reason a design predicts badly in the outer field.
///
/// The names come from R. B. Johnson, "Polynomial Ray Aberrations Computed in Various
/// Lens Design Programs," <i>Appl. Opt.</i> <b>12</b>, 2079-2082 (1973), Table I, which
/// tabulates the definition each of six programs used. Robb cites it as the standard
/// nomenclature. Johnson writes in Buchdahl's sigma/mu/tau; the bridge to the Rimmer
/// names used here is the one set out in <see cref="Prms"/>.
///
/// <para><b>Several named aberrations are combinations, not single coefficients.</b>
/// Tangential oblique spherical is mu4 + mu6, which is M1 + M2 + M3; fifth-order linear
/// coma is mu2 + mu3, which is F1 + F2. Where that is so, the name given here identifies
/// the family the coefficient belongs to rather than claiming the coefficient alone is
/// the named quantity. Johnson's own finding is the reason to be careful about this: he
/// compared six programs and found "significant variances in term definitions", so a
/// named aberration is only meaningful alongside its definition.</para>
/// </summary>
public static class AberrationNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        // Third order (Buchdahl's sigma1..sigma5).
        ["B"]   = "spherical",
        ["F"]   = "linear coma",
        ["C"]   = "astigmatism",
        ["Pi"]  = "Petzval field curvature",
        ["E"]   = "distortion",

        // Fifth order (mu1..mu12).
        ["B5"]  = "spherical",
        ["F1"]  = "linear coma (with F2)",
        ["F2"]  = "linear coma (with F1)",
        ["M1"]  = "oblique spherical, tangential (with M2, M3)",
        ["M2"]  = "oblique spherical, sagittal",
        ["M3"]  = "oblique spherical, tangential (with M1, M2)",
        ["N1"]  = "elliptical coma, tangential (with N2)",
        ["N2"]  = "elliptical coma, tangential (with N1)",
        ["N3"]  = "elliptical coma, oblique",
        ["C5"]  = "astigmatism (with Pi5)",
        ["Pi5"] = "Petzval / astigmatism (with C5)",
        ["E5"]  = "distortion",

        // Seventh order (tau1). The rest of the tertiary set is not computed here.
        ["B7"]  = "spherical",
    };

    private static readonly Dictionary<string, int> Orders = new()
    {
        ["B"] = 3, ["F"] = 3, ["C"] = 3, ["Pi"] = 3, ["E"] = 3,
        ["B5"] = 5, ["F1"] = 5, ["F2"] = 5, ["M1"] = 5, ["M2"] = 5, ["M3"] = 5,
        ["N1"] = 5, ["N2"] = 5, ["N3"] = 5, ["C5"] = 5, ["Pi5"] = 5, ["E5"] = 5,
        ["B7"] = 7,
    };

    /// <summary>
    /// The aberration family a monomial rho^a H^b belongs to, or empty where the classical
    /// vocabulary has no word for it.
    ///
    /// <para><b>This is a rule, not a table, and the third and fifth orders are its proof.</b>
    /// Every one of the eleven named coefficients above comes out of it correctly - spherical has
    /// no field, coma one power of it, oblique spherical two; distortion has no aperture,
    /// astigmatism and field curvature one, elliptical coma two - so applying the same rule at
    /// seventh order is reading the pattern the names already follow rather than inventing names
    /// for new terms. <c>TheFamilyRuleReproducesTheNamedOrders</c> is that anchor, and it fails
    /// if this is ever edited into something the known names do not satisfy.</para>
    ///
    /// <para><b>Two seventh-order monomials get nothing, and that is the honest answer.</b>
    /// rho^4 H^3 and rho^3 H^4 have no third- or fifth-order counterpart to be named after: the
    /// classical vocabulary was built for a set that stops at the fifth, and at the seventh there
    /// are eight monomials where the fifth has six. They are described by what they multiply,
    /// which is exact, instead of by a word invented to fill the column.</para>
    /// </summary>
    private static string Family(int aperture, int field)
    {
        if (field == 0) return "spherical";
        if (field == 1) return "coma";
        if (aperture == 0) return "distortion";
        if (aperture == 1) return "astigmatism / field curvature";
        if (field == 2) return "oblique spherical";
        if (aperture == 2) return "elliptical coma";
        return "";
    }

    /// <summary>The monomial a coefficient multiplies, as <c>rho^a H^b</c>.</summary>
    private static string Monomial(int aperture, int field)
    {
        string r = aperture == 0 ? "" : aperture == 1 ? "rho" : "rho^" + aperture;
        string h = field == 0 ? "" : field == 1 ? "H" : "H^" + field;
        return r.Length > 0 && h.Length > 0 ? r + " " + h : r + h;
    }

    /// <summary>The aberration this coefficient belongs to, with its order. Empty if unknown.</summary>
    public static string Describe(string coefficient)
    {
        // The tertiary set, worked out from its monomial. Buchdahl's tau2..tau20 have no
        // classical names to look up - Johnson's table stops at the fifth order, because the
        // vocabulary does - so what is given is the family the monomial puts them in and the
        // monomial itself. That is less than a name and more than nothing, and it is the part
        // that can be stated exactly.
        if (coefficient.StartsWith("Tau", StringComparison.Ordinal)
            && int.TryParse(coefficient.Substring(3), out int n))
        {
            int a = TertiaryCoefficients.AperturePowerOf(n);
            if (n < 1 || n > 20) return "";

            int b = 7 - a;
            string family = Family(a, b);
            return family.Length > 0
                 ? "7th " + family + ", " + Monomial(a, b)
                 : "7th " + Monomial(a, b);
        }

        if (!Names.TryGetValue(coefficient, out var name)) return "";
        string ordinal = Orders.TryGetValue(coefficient, out int o)
            ? o switch { 3 => "3rd", 5 => "5th", 7 => "7th", _ => o + "th" }
            : "";
        return ordinal.Length > 0 ? ordinal + " " + name : name;
    }

    /// <summary>
    /// Buchdahl's order for this coefficient: 3, 5 or 7. Zero if unknown.
    /// </summary>
    public static int Order(string coefficient)
    {
        if (coefficient.StartsWith("Tau", StringComparison.Ordinal)
            && int.TryParse(coefficient.Substring(3), out int n))
            return n >= 1 && n <= 20 ? 7 : 0;
        return Orders.TryGetValue(coefficient, out int o) ? o : 0;
    }

    /// <summary>
    /// The monomial and family rule, for the test that anchors it against the named orders.
    /// </summary>
    public static string FamilyOf(int aperture, int field) => Family(aperture, field);
}
