using System;
using System.Collections.Generic;
using System.Linq;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The RMS spot radius predicted from the Buchdahl/Rimmer coefficients, with no rays
/// traced - Robb's analytic merit function.
///
/// Robb, P. N., "Analytic merit function based on Buchdahl's aberration coefficients,"
/// <i>JOSA</i> <b>66</b>, 1037 (1976). His Eq. (2) writes the ray's intersection with the
/// Gaussian image plane as polynomials in the pupil radius rho, the pupil azimuth theta and
/// the field H; Eq. (4) is the variance of that intersection over the pupil.
///
/// Every term of eps_y and eps_z has the form  coefficient * rho^a * H^b * f(theta), so the
/// pupil average of a product of two terms separates completely:
///
///     &lt;eps_i eps_j&gt; = c_i c_j * &lt;f_i f_j&gt;_theta * H^(b_i + b_j) * 2/(a_i + a_j + 2)
///
/// The radial factor is the average of rho^(a_i+a_j) over the unit disc with weight
/// 2 rho drho. The theta factor is exact by trapezoid: these are trigonometric polynomials
/// of degree at most six, and the trapezoid rule is spectrally exact for periodic
/// integrands once the sample count exceeds the degree.
///
/// So the whole thing collapses to a quadratic form in the coefficients, evaluated once
/// here rather than carried as a table of magic constants. There are no rings and no arms:
/// the pupil average is analytic, and the only approximation left is the truncation of the
/// series at seventh order.
///
/// <para><b>Referenced to the centroid</b>, as Robb specifies - the variance about the mean
/// intersection, not about the chief ray. That is why the mean of eps_y is subtracted;
/// the mean of eps_z is zero because every one of its terms is odd in theta.</para>
///
/// <para><b>Distortion is deliberately absent.</b> E and E5 displace the whole patch
/// without changing its size, so they cannot appear in a spot radius. Robb's
/// corresponding terms vanish identically for the same reason.</para>
/// </summary>
public static class Prms
{
    /// <summary>One term of the intersection polynomial.</summary>
    private sealed class Term
    {
        public Dictionary<string, Scalar> C = new();   // coefficient name -> multiplier
        public int A;                                  // power of rho
        public int B;                                  // power of H
        public string F = "one";                       // theta function
    }

    // Robb Eq. (2), written in the Rimmer coefficient names this program reports, with
    // seventh order in his own tau. The notational bridge is: sigma1 = B, sigma2 = F,
    // sigma3 = C, sigma4 = Pi, sigma5 = E; mu1 = B5, mu2 = F1, mu3 = F2, mu4 = M1+M2,
    // mu5 = M2, mu6 = M3, mu7 = N1+N2/2, mu8 = N2/2, mu9 = N3, mu10 = 5*C5+Pi5,
    // mu11 = C5+Pi5, mu12 = E5; tau1 = B7. Verified term by term against Eq. (2), and the
    // seventh-order lines are identical to Buchdahl's own in J. Opt. Soc. Am. 48, 747
    // (1958), p.753 - Robb took them from there.
    private static readonly Term[] Ey =
    {
        new() { C = new() { ["B"] = 1 },                 A = 3, B = 0, F = "cos"   },
        new() { C = new() { ["F"] = 2 },                 A = 2, B = 1, F = "one"   },
        new() { C = new() { ["F"] = 1 },                 A = 2, B = 1, F = "cos2"  },
        new() { C = new() { ["C"] = 3, ["Pi"] = 1 },     A = 1, B = 2, F = "cos"   },
        new() { C = new() { ["B5"] = 1 },                A = 5, B = 0, F = "cos"   },
        new() { C = new() { ["F1"] = 1 },                A = 4, B = 1, F = "one"   },
        new() { C = new() { ["F2"] = 1 },                A = 4, B = 1, F = "cos2"  },
        new() { C = new() { ["M1"] = 1, ["M2"] = 1 },    A = 3, B = 2, F = "cos"   },
        new() { C = new() { ["M3"] = 1 },                A = 3, B = 2, F = "cos3p" },
        new() { C = new() { ["N1"] = 1, ["N2"] = 0.5 },  A = 2, B = 3, F = "one"   },
        new() { C = new() { ["N2"] = 0.5 },              A = 2, B = 3, F = "cos2"  },
        new() { C = new() { ["C5"] = 5, ["Pi5"] = 1 },   A = 1, B = 4, F = "cos"   },
        new() { C = new() { ["B7"] = 1 },                A = 7, B = 0, F = "cos"   },

        // The rest of seventh order. Robb writes it in tau; tau1 is B7 above, and the
        // eighteen here are the ones that affect spot size. tau20 is distortion and is
        // absent for the same reason E and E5 are.
        new() { C = new() { ["Tau2"] = 1 },              A = 6, B = 1, F = "one"   },
        new() { C = new() { ["Tau3"] = 1 },              A = 6, B = 1, F = "cos2"  },
        new() { C = new() { ["Tau4"] = 1 },              A = 5, B = 2, F = "cos"   },
        new() { C = new() { ["Tau6"] = 1 },              A = 5, B = 2, F = "cos3p" },
        new() { C = new() { ["Tau7"] = 1 },              A = 4, B = 3, F = "one"   },
        new() { C = new() { ["Tau8"] = 1 },              A = 4, B = 3, F = "cos2"  },
        new() { C = new() { ["Tau10"] = 1 },             A = 4, B = 3, F = "cos4"  },
        new() { C = new() { ["Tau11"] = 1 },             A = 3, B = 4, F = "cos"   },
        new() { C = new() { ["Tau12"] = 1 },             A = 3, B = 4, F = "cos3p" },
        new() { C = new() { ["Tau15"] = 1 },             A = 2, B = 5, F = "one"   },
        new() { C = new() { ["Tau16"] = 1 },             A = 2, B = 5, F = "cos2"  },
        new() { C = new() { ["Tau18"] = 1 },             A = 1, B = 6, F = "cos"   },
    };

    private static readonly Term[] Ez =
    {
        new() { C = new() { ["B"] = 1 },                 A = 3, B = 0, F = "sin"   },
        new() { C = new() { ["F"] = 1 },                 A = 2, B = 1, F = "sin2"  },
        new() { C = new() { ["C"] = 1, ["Pi"] = 1 },     A = 1, B = 2, F = "sin"   },
        new() { C = new() { ["B5"] = 1 },                A = 5, B = 0, F = "sin"   },
        new() { C = new() { ["F2"] = 1 },                A = 4, B = 1, F = "sin2"  },
        new() { C = new() { ["M2"] = 1 },                A = 3, B = 2, F = "sin"   },
        new() { C = new() { ["M3"] = 1 },                A = 3, B = 2, F = "sin3p" },
        new() { C = new() { ["N3"] = 1 },                A = 2, B = 3, F = "sin2"  },
        new() { C = new() { ["C5"] = 1, ["Pi5"] = 1 },   A = 1, B = 4, F = "sin"   },
        new() { C = new() { ["B7"] = 1 },                A = 7, B = 0, F = "sin"   },

        new() { C = new() { ["Tau3"] = 1 },              A = 6, B = 1, F = "sin2"  },
        new() { C = new() { ["Tau5"] = 1 },              A = 5, B = 2, F = "sin"   },
        new() { C = new() { ["Tau6"] = 1 },              A = 5, B = 2, F = "sin3p" },
        new() { C = new() { ["Tau9"] = 1 },              A = 4, B = 3, F = "sin2"  },
        new() { C = new() { ["Tau10"] = 1 },             A = 4, B = 3, F = "sin4"  },
        new() { C = new() { ["Tau13"] = 1 },             A = 3, B = 4, F = "sin"   },
        new() { C = new() { ["Tau14"] = 1 },             A = 3, B = 4, F = "sin3p" },
        new() { C = new() { ["Tau17"] = 1 },             A = 2, B = 5, F = "sin2"  },
        new() { C = new() { ["Tau19"] = 1 },             A = 1, B = 6, F = "sin"   },
    };

    private static Scalar Theta(string name, Scalar t) => name switch
    {
        "one" => 1.0,
        "cos" => SMath.Cos(t),
        "cos2" => SMath.Cos(2 * t),
        "cos3p" => SMath.Cos(t) * SMath.Cos(t) * SMath.Cos(t),
        "sin" => SMath.Sin(t),
        "sin2" => SMath.Sin(2 * t),
        "sin3p" => SMath.Cos(t) * SMath.Cos(t) * SMath.Sin(t),
        "cos4" => SMath.Cos(4 * t),
        "sin4" => SMath.Sin(4 * t),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown theta function"),
    };

    /// <summary>
    /// Average of f1*f2 over a full turn. Trapezoid is exact here: the integrand is a
    /// trigonometric polynomial of degree at most six and the sample count is far above it.
    /// The result is snapped to a nearby simple rational to clear floating-point dust.
    /// </summary>
    private static Scalar ThetaAverage(string f1, string f2)
    {
        const int n = 4096;
        Scalar s = 0.0;
        for (int k = 0; k < n; k++)
        {
            Scalar t = 2 * SMath.PI * k / n;
            s += Theta(f1, t) * Theta(f2, t);
        }
        Scalar v = s / n;
        Scalar snapped = SMath.Round(v * 65536.0) / 65536.0;
        return SMath.Abs(v - snapped) < 1e-9 ? snapped : v;
    }

    /// <summary>One entry of the assembled quadratic form.</summary>
    private readonly struct Pair
    {
        public readonly string A, B;
        public readonly int HPower;
        public readonly Scalar Factor;
        public Pair(string a, string b, int h, Scalar f) { A = a; B = b; HPower = h; Factor = f; }
    }

    private static readonly Pair[] Form = Build();

    private static Pair[] Build()
    {
        var acc = new Dictionary<(string, string, int), Scalar>();

        void Add(string a, string b, int h, Scalar v)
        {
            // Order the names so a*b and b*a land in the same bucket.
            var key = string.CompareOrdinal(a, b) <= 0 ? (a, b, h) : (b, a, h);
            acc[key] = acc.TryGetValue(key, out var cur) ? cur + v : v;
        }

        void Accumulate(Term[] list)
        {
            foreach (var x in list)
                foreach (var y in list)
                {
                    Scalar th = ThetaAverage(x.F, y.F);
                    if (th == 0.0) continue;
                    Scalar rad = 2.0 / (x.A + y.A + 2);
                    foreach (var (ca, va) in x.C)
                        foreach (var (cb, vb) in y.C)
                            Add(ca, cb, x.B + y.B, va * vb * th * rad);
                }
        }

        Accumulate(Ey);
        Accumulate(Ez);

        // Centroid reference: subtract the square of the mean of eps_y. Only terms whose
        // theta average is non-zero survive, and <eps_z> is zero by symmetry.
        var mean = new List<(string C, Scalar V, int H)>();
        foreach (var t in Ey)
        {
            Scalar m = ThetaAverage(t.F, "one");
            if (SMath.Abs(m) < 1e-12) continue;
            foreach (var (c, v) in t.C)
                mean.Add((c, v * m * 2.0 / (t.A + 2), t.B));
        }
        foreach (var mi in mean)
            foreach (var mj in mean)
                Add(mi.C, mj.C, mi.H + mj.H, -mi.V * mj.V);

        return acc.Where(kv => SMath.Abs(kv.Value) > 1e-12)
                  .Select(kv => new Pair(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value))
                  .OrderBy(p => p.HPower).ThenBy(p => p.A).ThenBy(p => p.B)
                  .ToArray();
    }

    /// <summary>Number of terms in the assembled quadratic form - a handle for tests.</summary>
    public static int TermCount => Form.Length;

    /// <summary>The assembled form, as (coefficient, coefficient, H power, factor).</summary>
    public static IEnumerable<(string A, string B, int HPower, Scalar Factor)> Terms =>
        Form.Select(p => (p.A, p.B, p.HPower, p.Factor));

    /// <summary>
    /// Robb's Eq. (2) itself - where one ray lands on the Gaussian image plane, rather than
    /// the spread of all of them. <paramref name="rho"/> is the normalised pupil radius,
    /// <paramref name="theta"/> its azimuth measured from the meridian, and
    /// <paramref name="h"/> the field as a fraction of tan(theta_max). The result is in the
    /// same length units as the coefficients, measured from the Gaussian image point.
    ///
    /// <para>The RMS spot below is the pupil average of this squared, so the two cannot
    /// disagree. It is exposed separately because a ray fan says something an RMS radius
    /// cannot: the SHAPE of the residual against a traced fan shows which order is missing,
    /// where a single number only says how much is.</para>
    ///
    /// <para><paramref name="order"/> truncates the series - 3, 5 or 7 - so that the orders
    /// can be compared against each other. Terms are selected by total degree a+b.</para>
    /// </summary>
    public static (Scalar Y, Scalar Z) Transverse(BuchdahlTerms totals, Scalar rho,
                                                  Scalar theta, Scalar h, int order = 7)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        Scalar Sum(Term[] list)
        {
            Scalar s = 0.0;
            foreach (var t in list)
            {
                if (t.A + t.B > order) continue;
                Scalar v = 0.0;
                foreach (var (name, mult) in t.C) v += mult * totals[name];
                s += v * SMath.Pow(rho, t.A) * (t.B == 0 ? 1.0 : SMath.Pow(h, t.B))
                   * Theta(t.F, theta);
            }
            return s;
        }
        return (Sum(Ey), Sum(Ez));
    }

    /// <summary>
    /// The pupil average of <c>eps . rho_vec</c>, where <c>rho_vec</c> is the normalised pupil
    /// vector <c>(rho cos theta, rho sin theta)</c>. This is the ONE quantity Robb's polynomial
    /// does not itself expose and a best-focus plane needs.
    ///
    /// <para><b>Why this is all it takes.</b> Moving the image plane by <c>dZ</c> displaces a ray
    /// at normalised pupil height <c>rho</c> by <c>dZ u rho</c> along the pupil vector, with
    /// <c>u</c> the paraxial marginal slope in image space - Sands's Eq. (9), the paraxial
    /// approximation for the image-space direction, which he finds justified except near
    /// 45-degree ray angles or very large distortion. Writing <c>d = dZ u</c>, the mean square
    /// radius at the shifted plane is</para>
    ///
    /// <code>    S2(d) = MeanSquare + 2 d L + d^2/2</code>
    ///
    /// <para>with <c>L</c> this average and <c>1/2</c> the average of <c>rho^2</c> over the unit
    /// disc. It is a quadratic in <c>d</c>, so best focus is <c>d = -2L</c> exactly, with no
    /// search and nothing traced.</para>
    ///
    /// <para><b>No centroid correction appears</b>, and that is not an omission. The variance is
    /// about the centroid, so the defocus term should enter as a covariance - but the mean of
    /// <c>rho_vec</c> over the pupil is zero, so the covariance and the raw average coincide and
    /// the defocus term cannot move the centroid. What it does move is the magnification, by
    /// carrying the reference point along the chief ray; that is Sands's <c>m*</c> and it is a
    /// different quantity from this one.</para>
    ///
    /// <para><b>Distortion is absent here too.</b> E, E5 and Tau20 are missing from the term
    /// table, as Robb intended, and they belong missing: a term with no pupil dependence has
    /// zero average against <c>rho_vec</c> and could not contribute to a best-focus plane even
    /// if it were carried.</para>
    /// </summary>
    public static Scalar DefocusCoupling(BuchdahlTerms totals, Scalar h)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        Scalar sum = 0.0;
        foreach (var t in CrossForm)
        {
            Scalar hp = t.HPower == 0 ? 1.0 : SMath.Pow(h, t.HPower);
            sum += t.Factor * totals[t.Name] * hp;
        }
        return sum;
    }

    /// <summary>
    /// Mean square spot radius at a plane <paramref name="deltaZ"/> from the paraxial one, with
    /// <paramref name="u"/> the paraxial marginal slope in image space. At
    /// <c>deltaZ = 0</c> this is <see cref="MeanSquare"/> exactly.
    /// </summary>
    public static Scalar MeanSquareDefocused(BuchdahlTerms totals, Scalar h, Scalar deltaZ,
                                             Scalar u)
    {
        Scalar d = deltaZ * u;
        return MeanSquare(totals, h) + 2.0 * d * DefocusCoupling(totals, h) + 0.5 * d * d;
    }

    /// <summary>The linear form of <see cref="DefocusCoupling"/>, assembled once.</summary>
    private static readonly (string Name, int HPower, Scalar Factor)[] CrossForm = BuildCross();

    private static (string Name, int HPower, Scalar Factor)[] BuildCross()
    {
        var acc = new Dictionary<(string, int), Scalar>();

        void Add(string c, int h, Scalar v)
            => acc[(c, h)] = acc.TryGetValue((c, h), out var cur) ? cur + v : v;

        // Each term contributes <c rho^A H^B f(theta) * rho g(theta)>, which separates into the
        // radial average of rho^(A+1) over the unit disc - 2/(A+3) with weight 2 rho drho - and
        // the azimuthal average of f against g. g is cos for the meridional component and sin
        // for the sagittal one, which is what makes this the projection onto the pupil vector.
        void Accumulate(Term[] list, string against)
        {
            foreach (var t in list)
            {
                Scalar th = ThetaAverage(t.F, against);
                if (th == 0.0) continue;
                Scalar rad = 2.0 / (t.A + 3);
                foreach (var (name, mult) in t.C) Add(name, t.B, mult * th * rad);
            }
        }

        Accumulate(Ey, "cos");
        Accumulate(Ez, "sin");

        return acc.Where(kv => SMath.Abs(kv.Value) > 1e-12)
                  .Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value))
                  .OrderBy(t => t.Item2).ThenBy(t => t.Item1)
                  .ToArray();
    }

    /// <summary>The assembled linear form, as (coefficient, H power, factor).</summary>
    public static IEnumerable<(string Name, int HPower, Scalar Factor)> CrossTerms => CrossForm;

    /// <summary>
    /// Mean square spot radius at fractional field height <paramref name="h"/>, from the
    /// system's transverse coefficients. Zero on axis for a design with no spherical
    /// aberration; never negative.
    /// </summary>
    public static Scalar MeanSquare(BuchdahlTerms totals, Scalar h)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        Scalar sum = 0.0;
        foreach (var p in Form)
        {
            Scalar hp = p.HPower == 0 ? 1.0 : SMath.Pow(h, p.HPower);
            sum += p.Factor * totals[p.A] * totals[p.B] * hp;
        }
        return sum;
    }

    /// <summary>
    /// RMS spot radius at fractional field height <paramref name="h"/>.
    ///
    /// Clamped at zero: the series is a truncation, and on a well-corrected design the
    /// surviving terms can cancel to a very small negative number that is numerical dust
    /// rather than a real quantity.
    /// </summary>
    public static Scalar Value(BuchdahlTerms totals, Scalar h)
    {
        Scalar ms = MeanSquare(totals, h);
        return ms > 0.0 ? SMath.Sqrt(ms) : 0.0;
    }

    /// <summary>
    /// The composite over every field and wavelength - PRMSA.
    ///
    /// Robb's spectrally weighted average: the weighted mean of the mean-square radii,
    /// square-rooted once at the end. Averaging the RMS values directly instead would
    /// weight a design's worst field differently and is not the same quantity.
    /// </summary>
    /// <param name="cases">
    /// One entry per (wavelength, field) combination: the coefficients at that wavelength,
    /// the fractional field height, and the product of the field and wavelength weights.
    /// </param>
    public static Scalar Composite(IEnumerable<(BuchdahlTerms Totals, Scalar H, Scalar Weight)> cases)
    {
        Scalar num = 0.0, den = 0.0;
        foreach (var (totals, h, w) in cases)
        {
            if (w <= 0.0) continue;
            Scalar ms = MeanSquare(totals, h);
            num += w * (ms > 0.0 ? ms : 0.0);
            den += w;
        }
        if (den <= 0.0) return 0.0;
        Scalar mean = num / den;
        return mean > 0.0 ? SMath.Sqrt(mean) : 0.0;
    }
}
