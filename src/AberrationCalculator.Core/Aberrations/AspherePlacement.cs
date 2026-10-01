using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>What figuring one surface would do, at third order.</summary>
public sealed class AsphereCandidate
{
    /// <summary>Surface index, as the prescription numbers it.</summary>
    public int Surface;

    /// <summary>Marginal ray height there. Schulz's <c>h</c>.</summary>
    public Scalar MarginalHeight;

    /// <summary>Chief ray height there. Schulz's <c>H</c>.</summary>
    public Scalar ChiefHeight;

    /// <summary>
    /// <c>H/h</c>, the Delano ratio, and the whole of what decides WHICH aberrations an
    /// asphere here can reach. NaN where the marginal ray height vanishes.
    /// </summary>
    public Scalar Ratio;

    /// <summary>True where <c>h</c> vanishes, so the ratio is not formed.</summary>
    public bool AtImage;

    /// <summary>
    /// What one unit of r^4 coefficient adds to each Seidel sum. These are EXACT derivatives,
    /// not differences: the aspheric contribution is linear in the coefficient.
    /// Petzval is absent because figuring cannot reach it.
    /// </summary>
    public Scalar DS1, DS2, DS3, DS5;

    /// <summary>The same for one unit of CONIC constant, which is how most designs state it.</summary>
    public Scalar DS1Conic, DS2Conic, DS3Conic, DS5Conic;

    /// <summary>
    /// The r^4 coefficient that would drive each sum to zero on its own, or NaN where this
    /// surface has no leverage on that sum at all.
    /// </summary>
    public Scalar NullS1, NullS2, NullS3, NullS5;

    /// <summary>
    /// The semi-aperture the figuring is judged at: the surface's own semi-diameter where the
    /// file states one, and the sum of the two ray heights otherwise, which is what the beam
    /// needs at full field.
    /// </summary>
    public Scalar ApertureRadius;

    /// <summary>
    /// What each <c>Null</c> coefficient amounts to as SAG at that semi-aperture, in lens units.
    ///
    /// <para><b>This, not the coefficient, is the number to judge.</b> Coefficients of different
    /// surfaces are not comparable - each multiplies the fourth power of a different radius - and
    /// a coefficient alone gives a reader no way to see that an answer is absurd. The sag does:
    /// a figure of two microns is a polish, and one of a kilometre is a surface that does not
    /// exist.</para>
    /// </summary>
    public Scalar SagS1, SagS2, SagS3, SagS5;

    /// <summary>
    /// Whether each sum can actually be reached from this surface, judged physically rather than
    /// numerically: the figure needed must not be deeper than the surface is wide.
    ///
    /// <para>A surface at the stop has a chief-ray height of zero, so its leverage on coma,
    /// astigmatism and distortion is zero too - but it is zero to within the rounding of the
    /// paraxial trace rather than exactly, so dividing by it yields an enormous finite number
    /// instead of an error. The first version of this class printed those, and 3.5E+09 sitting
    /// in a column of microns is the kind of confident wrong answer this program refuses
    /// elsewhere. The test is now that the sag exceeds the semi-aperture, which is a statement
    /// about lenses and not about floating point.</para>
    /// </summary>
    public bool CanReachS1, CanReachS2, CanReachS3, CanReachS5;
}

/// <summary>
/// Which surface to figure, and what figuring it would buy - the design question this program
/// could always answer backwards and never forwards.
///
/// <para>Everything else here takes a design and says what is wrong with it, per surface. This
/// asks the other way round: given that an asphere is expensive and you can afford one, where
/// does it do the most good? The two questions share their arithmetic and have opposite
/// directions.</para>
///
/// <para><b>The whole of the answer is H/h</b>, the ratio of the chief-ray to the marginal-ray
/// height at the surface. <see cref="SeidelCoefficients"/> already forms it - as a local named
/// <c>ratio</c> - because the aspheric contribution to the five sums is</para>
///
/// <code>
///     dS1 = 8 (n' - n) a4 h^4,   dS2 = dS1 (H/h),   dS3 = dS1 (H/h)^2,   dS5 = dS1 (H/h)^3
/// </code>
///
/// <para>so one number fixes the whole pattern. Small <c>|H/h|</c> and the surface reaches
/// spherical aberration and little else; large and it reaches distortion; in between it moves
/// all four by comparable amounts. That is G. Schulz's reading of the Delano diagram, *Progress
/// in Optics* XXV (1988) Sec. 3.3, and this class is that diagram evaluated rather than drawn.
/// </para>
///
/// <para><b>Petzval is untouched and that is not an omission.</b> `S4` depends on the surface
/// curvature and the index step alone; no deformation of the surface can move it. Schulz says the
/// same in Sec. 3.3 - the condition "cannot be influenced by asphericities" - and this program's
/// own aspheric block leaves the column blank for the same reason. It is the one thing an asphere
/// cannot buy, and it is why a design can need a glass change rather than a figure.</para>
///
/// <para><b>The sensitivities are exact.</b> The contribution above is LINEAR in <c>a4</c>, so
/// the derivative is the contribution at unit coefficient and there is nothing to difference and
/// no step size to choose. That is a property of the third order only; at fifth and seventh the
/// figuring enters nonlinearly and this would have to be differentiated properly.</para>
/// </summary>
public static class AspherePlacement
{
    /// <summary>
    /// Evaluate every optical surface as a candidate. <paramref name="indices"/> is the index
    /// in each region, as <see cref="SeidelCoefficients"/> takes it.
    /// </summary>
    public static List<AsphereCandidate> Screen(OpticalSystem system, IReadOnlyList<Scalar> indices,
                                                ParaxialResult paraxial, SeidelResult seidel)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));

        var rows = new List<AsphereCandidate>();
        int last = system.LastOpticalSurface();

        for (int j = 1; j <= last; j++)
        {
            var surf = system.Surfaces[j];
            Scalar y = j < paraxial.Y.Length ? paraxial.Y[j] : 0.0;
            Scalar ybar = j < paraxial.Ybar.Length ? paraxial.Ybar[j] : 0.0;

            Scalar nBefore = j - 1 < indices.Count ? indices[j - 1] : 1.0;
            Scalar nAfter = j < indices.Count ? indices[j] : 1.0;

            bool atImage = Math.Abs(y) <= 1e-12;
            Scalar ratio = atImage ? double.NaN : ybar / y;

            // The contribution at unit r^4 coefficient, which IS the derivative.
            Scalar d1 = 8.0 * (nAfter - nBefore) * y * y * y * y;
            Scalar d2 = atImage ? 0.0 : d1 * ratio;
            Scalar d3 = atImage ? 0.0 : d2 * ratio;
            Scalar d5 = atImage ? 0.0 : d3 * ratio;

            // A conic K is an r^4 coefficient of K c^3 / 8, so its sensitivity is the same
            // numbers scaled. Reported because most designs state a conic, not a polynomial.
            Scalar c = surf.VertexCurvature;
            Scalar toConic = c * c * c / 8.0;

            // The radius the figuring is judged at. A stated semi-diameter is authoritative;
            // otherwise the beam needs the marginal ray plus the chief ray at full field.
            Scalar aperture = surf.SemiDiameter > 0.0
                            ? surf.SemiDiameter
                            : Math.Abs(y) + Math.Abs(ybar);

            var row = new AsphereCandidate
            {
                Surface = j,
                MarginalHeight = y,
                ChiefHeight = ybar,
                Ratio = ratio,
                AtImage = atImage,
                ApertureRadius = aperture,
                DS1 = d1, DS2 = d2, DS3 = d3, DS5 = d5,
                DS1Conic = d1 * toConic, DS2Conic = d2 * toConic,
                DS3Conic = d3 * toConic, DS5Conic = d5 * toConic,
                NullS1 = Null(seidel.TotalS1, d1),
                NullS2 = Null(seidel.TotalS2, d2),
                NullS3 = Null(seidel.TotalS3, d3),
                NullS5 = Null(seidel.TotalS5, d5),
            };

            Scalar r4 = aperture * aperture * aperture * aperture;
            row.SagS1 = row.NullS1 * r4;
            row.SagS2 = row.NullS2 * r4;
            row.SagS3 = row.NullS3 * r4;
            row.SagS5 = row.NullS5 * r4;

            row.CanReachS1 = Reachable(row.SagS1, aperture);
            row.CanReachS2 = Reachable(row.SagS2, aperture);
            row.CanReachS3 = Reachable(row.SagS3, aperture);
            row.CanReachS5 = Reachable(row.SagS5, aperture);

            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// The coefficient that drives a sum to zero, or NaN where the surface has no leverage on it
    /// - which is a real answer and not a failure, so it is not reported as a very large number.
    /// </summary>
    private static Scalar Null(Scalar total, Scalar derivative)
        => Math.Abs(derivative) <= 1e-30 ? double.NaN : -total / derivative;

    /// <summary>
    /// Whether a figure of this sag is a figure at all. The bound is physical rather than
    /// numerical: a deformation deeper than the surface is wide is not a correction to that
    /// surface, it is a different surface. Everything the screen refuses fails this by many
    /// orders of magnitude, so the exact bound is not doing delicate work.
    /// </summary>
    private static bool Reachable(Scalar sag, Scalar aperture)
        => !double.IsNaN(sag) && !double.IsInfinity(sag)
           && aperture > 0.0 && Math.Abs(sag) <= aperture;

    /// <summary>
    /// How this surface's leverage divides across the four sums, normalised to the largest -
    /// the profile <c>1 : |H/h| : (H/h)^2 : |H/h|^3</c> rescaled.
    ///
    /// <para><b>Normalised rather than ranked against the design's current aberrations</b>, and
    /// the first version of this did the latter. Dividing each sensitivity by the size of the sum
    /// it moves looks like the useful comparison and is a trap: an aberration the design has
    /// already corrected has a total near zero, so it scores near infinity and wins the ranking
    /// precisely because there is nothing left to correct. The four Seidel sums are in one
    /// measure and are directly comparable, so the honest statement is the bare profile, and what
    /// to do with it is the reader's judgement and not this program's.</para>
    ///
    /// <para>Returns an empty array where the surface has no leverage at all - no index step, or
    /// the marginal ray at zero height.</para>
    /// </summary>
    public static Scalar[] ReachProfile(AsphereCandidate r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        var raw = new[] { Math.Abs(r.DS1), Math.Abs(r.DS2), Math.Abs(r.DS3), Math.Abs(r.DS5) };
        Scalar largest = 0.0;
        foreach (var v in raw) if (v > largest) largest = v;
        if (largest <= 1e-30) return Array.Empty<Scalar>();

        for (int i = 0; i < raw.Length; i++) raw[i] /= largest;
        return raw;
    }

    /// <summary>
    /// The third-order state after applying <paramref name="a4"/> to this surface - exactly,
    /// because the contribution is linear in the coefficient.
    ///
    /// <para><b>This is what the screen was missing.</b> The tables say what a figure COSTS and
    /// never what it BUYS, so a reader cannot see that nulling a large coma removes a great deal
    /// and nulling a small astigmatism removes almost nothing. Worse, they cannot see the
    /// collateral: the figure that nulls one sum moves the other three, sometimes cancelling a
    /// second one for nothing and sometimes wrecking it.</para>
    /// </summary>
    public static (Scalar S1, Scalar S2, Scalar S3, Scalar S5) After(
        AsphereCandidate r, Scalar a4, SeidelResult seidel)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        return (seidel.TotalS1 + a4 * r.DS1, seidel.TotalS2 + a4 * r.DS2,
                seidel.TotalS3 + a4 * r.DS3, seidel.TotalS5 + a4 * r.DS5);
    }

    /// <summary>
    /// A crude equal-weighted size for a third-order state: the root sum of squares of the four
    /// sums figuring can reach.
    ///
    /// <para>Petzval is left out because no figure can move it, so carrying it would add the same
    /// constant to every row and make the differences harder to read rather than easier. This is
    /// not a spot size and does not pretend to be one - the four are weighted equally, which no
    /// image quality metric does. It is here to rank rows that would otherwise have to be
    /// compared four numbers at a time.</para>
    /// </summary>
    public static Scalar Magnitude(Scalar s1, Scalar s2, Scalar s3, Scalar s5)
        => Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s5 * s5);

    /// <summary>The screen as a readable block.</summary>
    public static string Render(OpticalSystem system, IReadOnlyList<Scalar> indices,
                                ParaxialResult paraxial, SeidelResult seidel)
    {
        var rows = Screen(system, indices, paraxial, seidel);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("WHERE AN ASPHERE WOULD ACT");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Every other table here says what is wrong with the design. This one asks the");
        sb.AppendLine("question the other way round: an asphere costs money, so if you can afford one,");
        sb.AppendLine("where does it do the most good? Third order only.");
        sb.AppendLine();
        sb.AppendLine("THE TERM APPLIED IS r^4 AND ONLY r^4 - a single coefficient, A4. That is forced");
        sb.AppendLine("rather than chosen. A deformation r^n first reaches transverse order n-1, so r^6");
        sb.AppendLine("reaches the fifth order and r^8 the seventh, and NEITHER CAN MOVE A SEIDEL SUM AT");
        sb.AppendLine("ALL. r^4 is the only figuring the third order can see.");
        sb.AppendLine();
        sb.AppendLine("r^2 IS NOT TOUCHED. It is not figuring: it is a curvature change, and it moves the");
        sb.AppendLine("focal length. Where a design already carries one it is folded into the vertex");
        sb.AppendLine("curvature before anything here is computed, so the ray heights below already");
        sb.AppendLine("account for it - but nothing here proposes changing it.");
        sb.AppendLine();
        sb.AppendLine("A CONIC WOULD DO THE SAME JOB AT THIS ORDER. A conic K on a surface of vertex");
        sb.AppendLine("curvature c is an r^4 coefficient of K c^3 / 8, so either states the same third-");
        sb.AppendLine("order correction. They are NOT the same surface: a conic drags r^6 and r^8 along");
        sb.AppendLine("with it, growing as K^2 and K^3, so the two choices agree here and part company at");
        sb.AppendLine("the fifth and seventh orders, which this screen cannot see. A conic also cannot");
        sb.AppendLine("figure a flat at all, since c^3 is then zero.");
        sb.AppendLine();
        sb.AppendLine("H/h - the chief-ray height over the marginal-ray height - decides the whole");
        sb.AppendLine("pattern, because figuring contributes to the four sums as 1, H/h, (H/h)^2 and");
        sb.AppendLine("(H/h)^3 of one another. Small: the surface reaches spherical aberration and little");
        sb.AppendLine("else. Large: distortion. In between: all four together. G. Schulz, Progress in");
        sb.AppendLine("Optics XXV (1988), Sec. 3.3, reading the Delano diagram.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-5} {1,10} {2,10} {3,10}   {4}",
            "Surf", "h", "H", "H/h", "reaches"));
        foreach (var r in rows)
        {
            var profile = ReachProfile(r);
            string reach;
            if (profile.Length == 0) reach = "nothing - no index step, or h = 0";
            else
            {
                // Named rather than tabulated. A normalised profile was printed here and was
                // useless: with |H/h| < 1 the first entry is 1.000 on every surface of every
                // design, because S1 is always the largest, and the remaining three are just
                // the powers of a ratio the column to the left already gives.
                Scalar a = Math.Abs(r.Ratio);
                reach = a < 0.15 ? "spherical, essentially alone"
                      : a < 0.5 ? "spherical mainly, some coma"
                      : a < 2.0 ? "all four, comparably"
                      : "distortion mainly";
            }
            sb.AppendLine(string.Format(inv, "{0,-5} {1,10:0.####} {2,10:0.####} {3,10}   {4}",
                r.Surface, r.MarginalHeight, r.ChiefHeight,
                r.AtImage ? "-" : r.Ratio.ToString("0.####", inv), reach));
        }
        sb.AppendLine();
        sb.AppendLine("The four sensitivities stand as 1 : H/h : (H/h)^2 : (H/h)^3, so H/h alone fixes");
        sb.AppendLine("what a surface can reach and the bands above are a reading of it, not a second");
        sb.AppendLine("measurement. They say how a surface DIVIDES its effect, never how much it has,");
        sb.AppendLine("and they are not ranked against what this design suffers from.");

        sb.AppendLine();
        sb.AppendLine("The figuring that would drive each sum to zero on its own, AS SAG at the surface's");
        sb.AppendLine("semi-aperture, in lens units. A row is FOUR ALTERNATIVES, not four things at once:");
        sb.AppendLine("one surface, one figure, and you choose which sum to kill.");
        sb.AppendLine();
        sb.AppendLine("What the choice costs elsewhere is the RATIO of two entries in the row, not the");
        sb.AppendLine("entries themselves. Applying the figure that nulls Si leaves Sj at");
        sb.AppendLine("Sj * (1 - null_i/null_j) - so where two entries are equal the second sum is exactly");
        sb.AppendLine("cancelled too, and where null_i is several times null_j the second is overshot and");
        sb.AppendLine("changes sign.");
        sb.AppendLine();
        sb.AppendLine("Where a file states a semi-diameter that is used; otherwise the semi-aperture is");
        sb.AppendLine("|h| + |H|, the paraxial bound on the extreme ray. THAT IS NOT THE SAME AS THE");
        sb.AppendLine("SOLVED APERTURE A DESIGN PROGRAM COMPUTES, which knows the real bundle and is");
        sb.AppendLine("usually a little smaller - on the Kingslake double Gauss, 9.08 here against");
        sb.AppendLine("OpticStudio's 8.71. So the SAG column can differ by a few per cent between this");
        sb.AppendLine("and ASPHWHERE.ZPL. The A4 column cannot: it has no aperture in it, and the two");
        sb.AppendLine("agree to every printed digit. Nor can the ranking below, which is by outcome.");
        sb.AppendLine();
        sb.AppendLine("Sag rather than the coefficient, because coefficients of different surfaces are");
        sb.AppendLine("not comparable - each multiplies the fourth power of a different radius - and a");
        sb.AppendLine("coefficient gives no way to see that an answer is absurd. A figure of two microns");
        sb.AppendLine("is a polish; one of a kilometre is a surface that does not exist.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-5} {1,10} {2,14} {3,14} {4,14} {5,14}",
            "Surf", "semi-ap", "SAG null S1", "SAG null S2", "SAG null S3", "SAG null S5"));
        foreach (var r in rows)
            sb.AppendLine(string.Format(inv, "{0,-5} {1,10:0.####} {2,14} {3,14} {4,14} {5,14}",
                r.Surface, r.ApertureRadius,
                Sag(r.SagS1, r.CanReachS1), Sag(r.SagS2, r.CanReachS2),
                Sag(r.SagS3, r.CanReachS3), Sag(r.SagS5, r.CanReachS5)));

        sb.AppendLine();
        sb.AppendLine("OUT OF REACH means the figure needed would be deeper than the surface is wide -");
        sb.AppendLine("not a correction to that surface but a different one. It is what a surface at the");
        sb.AppendLine("stop returns for coma, astigmatism and distortion: its chief-ray height is zero,");
        sb.AppendLine("so its leverage on those three is zero, and the coefficient that would null them");
        sb.AppendLine("is unbounded. The test is on the lens and not on the arithmetic, which is why it");
        sb.AppendLine("is stated in sag.");
        sb.AppendLine();
        sb.AppendLine("THE SAME FIGURES AS r^4 COEFFICIENTS - this is the number to type into a lens");
        sb.AppendLine("file. The table above is the same figuring expressed as depth of glass; this one");
        sb.AppendLine("is what A4 must be set to. Nothing above is an A4 and nothing here is a sag.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-5} {1,16} {2,16} {3,16} {4,16}",
            "Surf", "A4 null S1", "A4 null S2", "A4 null S3", "A4 null S5"));
        foreach (var r in rows)
            sb.AppendLine(string.Format(inv, "{0,-5} {1,16} {2,16} {3,16} {4,16}",
                r.Surface,
                Coef(r.NullS1, r.CanReachS1), Coef(r.NullS2, r.CanReachS2),
                Coef(r.NullS3, r.CanReachS3), Coef(r.NullS5, r.CanReachS5)));

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("CHEAPEST SURFACE FOR EACH SUM - the least figuring that does the job, which is the");
        sb.AppendLine("nearest thing here to an answer rather than a table. It is a third-order answer and");
        sb.AppendLine("it ignores everything else about the surface: whether it is already figured, what");
        sb.AppendLine("the figure does to the other three sums, and whether the shop can make it.");
        sb.AppendLine();
        sb.AppendLine("Cheapest by DEPTH OF GLASS TO REMOVE, which is a manufacturing cost and not an");
        sb.AppendLine("optical one. A surface far from the stop has a wide footprint, so the whole of it");
        sb.AppendLine("must be figured to the quoted depth even though only the marginal-ray zone does the");
        sb.AppendLine("work on spherical aberration. That mildly favours surfaces near the stop here for a");
        sb.AppendLine("geometric reason rather than an optical one, and it is worth knowing which is");
        sb.AppendLine("which before spending money on the answer.");
        sb.AppendLine();
        foreach (var (name, pick) in new (string, Func<AsphereCandidate, (Scalar Sag, bool Ok)>)[]
                 {
                     ("spherical   S1", c => (c.SagS1, c.CanReachS1)),
                     ("coma        S2", c => (c.SagS2, c.CanReachS2)),
                     ("astigmatism S3", c => (c.SagS3, c.CanReachS3)),
                     ("distortion  S5", c => (c.SagS5, c.CanReachS5)),
                 })
        {
            AsphereCandidate? best = null;
            Scalar least = double.MaxValue;
            foreach (var r in rows)
            {
                var (sag, ok) = pick(r);
                if (!ok) continue;
                if (Math.Abs(sag) < least) { least = Math.Abs(sag); best = r; }
            }
            sb.AppendLine(best == null
                ? string.Format(inv, "    {0,-15} no surface can reach it", name)
                : string.Format(inv, "    {0,-15} surface {1,-3} at {2} of sag",
                    name, best.Surface, least.ToString("0.0000E+00", inv)));
        }

        sb.AppendLine();
        sb.AppendLine("WHAT EACH CHOICE BUYS. Everything above is the COST of a figure; this is the");
        sb.AppendLine("benefit. Each row nulls its target from whichever surface leaves the design in");
        sb.AppendLine("the best state overall - NOT the cheapest one - and shows that state, so a large");
        sb.AppendLine("aberration knocked to zero is visible as such, and so is the collateral: the other");
        sb.AppendLine("three move too, and a figure aimed at one sum can cancel a second for nothing or");
        sb.AppendLine("wreck it.");
        sb.AppendLine();
        sb.AppendLine("The state is exact, not linearised: the contribution is linear in the coefficient,");
        sb.AppendLine("so applying it and predicting it are the same arithmetic.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-12} {1,5} {2,12} {3,12} {4,12} {5,12}   {6,10}   {7}",
            "choice", "surf", "S1", "S2", "S3", "S5", "size", "worst side effect"));

        Scalar before = Magnitude(seidel.TotalS1, seidel.TotalS2, seidel.TotalS3, seidel.TotalS5);
        sb.AppendLine(string.Format(inv, "{0,-12} {1,5} {2,12:0.0000E+00} {3,12:0.0000E+00} {4,12:0.0000E+00} {5,12:0.0000E+00}   {6,10:0.0000E+00}   {7}",
            "as it is", "-", seidel.TotalS1, seidel.TotalS2, seidel.TotalS3, seidel.TotalS5, before, "-"));

        // The worst of the three sums that were NOT targeted, as a multiple of what it was.
        // Without this the collateral is there in the columns and has to be worked out by
        // dividing, which is exactly the arithmetic a reader should not have to do to see that
        // a figure has wrecked something.
        string Damage(Scalar[] after, int targeted)
        {
            var wasArr = new[] { seidel.TotalS1, seidel.TotalS2, seidel.TotalS3, seidel.TotalS5 };
            var names = new[] { "S1", "S2", "S3", "S5" };
            string worst = "none"; Scalar factor = 1.0;
            for (int k = 0; k < 4; k++)
            {
                if (k == targeted) continue;
                Scalar was = Math.Abs(wasArr[k]);
                if (was <= 1e-15) continue;
                Scalar now = Math.Abs(after[k]) / was;
                if (now > factor) { factor = now; worst = names[k]; }
            }
            return worst == "none" ? "none - all improve"
                 : string.Format(inv, "{0} x{1:0.0}{2}", worst, factor,
                     factor >= 3.0 ? "  <- WRECKED" : "");
        }

        var targets = new (string Name, Func<AsphereCandidate, (Scalar Sag, Scalar Coef, bool Ok)> Pick)[]
        {
            ("null S1", c => (c.SagS1, c.NullS1, c.CanReachS1)),
            ("null S2", c => (c.SagS2, c.NullS2, c.CanReachS2)),
            ("null S3", c => (c.SagS3, c.NullS3, c.CanReachS3)),
            ("null S5", c => (c.SagS5, c.NullS5, c.CanReachS5)),
        };

        string bestChoice = "none"; int bestSurface = 0; Scalar bestSize = before;
        var alsoCheaper = new List<string>();

        foreach (var (name, pick) in targets)
        {
            // BY OUTCOME, NOT BY COST, and the first version of this got it wrong. It showed
            // the surface needing the least figuring, which is the wrong question for a row
            // whose subject is what the figure BUYS. On the Kingslake double Gauss the coma
            // sags are within three per cent across the whole lens while the outcomes differ
            // by a factor of two, so the cheapest surface was being chosen on a near-tie and
            // could be the worst available. It also made the recommendation depend on how the
            // semi-aperture is defined; ranking by outcome does not.
            AsphereCandidate? best = null;
            Scalar bestOf = double.MaxValue;
            AsphereCandidate? cheapest = null;
            Scalar least = double.MaxValue;

            foreach (var r in rows)
            {
                var (sag, coefficient, ok) = pick(r);
                if (!ok) continue;

                var (t1, t2, t3, t5) = After(r, coefficient, seidel);
                Scalar size = Magnitude(t1, t2, t3, t5);
                if (size < bestOf) { bestOf = size; best = r; }
                if (Math.Abs(sag) < least) { least = Math.Abs(sag); cheapest = r; }
            }

            if (best == null)
            {
                sb.AppendLine(string.Format(inv, "{0,-12} {1,5}   out of reach on every surface", name, "-"));
                continue;
            }

            var (_, coef, _) = pick(best);
            var (a1, a2, a3, a5) = After(best, coef, seidel);
            Scalar chosen = Magnitude(a1, a2, a3, a5);
            int targeted = name == "null S1" ? 0 : name == "null S2" ? 1 : name == "null S3" ? 2 : 3;
            sb.AppendLine(string.Format(inv, "{0,-12} {1,5} {2,12:0.0000E+00} {3,12:0.0000E+00} {4,12:0.0000E+00} {5,12:0.0000E+00}   {6,10:0.0000E+00}   {7}",
                name, best.Surface, a1, a2, a3, a5, chosen,
                Damage(new[] { a1, a2, a3, a5 }, targeted)));

            if (cheapest != null && cheapest.Surface != best.Surface)
            {
                var (_, cheapCoef, _) = pick(cheapest);
                var (c1, c2, c3, c5) = After(cheapest, cheapCoef, seidel);
                alsoCheaper.Add(string.Format(inv,
                    "    {0}: surface {1} needs less glass ({2:0.0000E+00} against {3:0.0000E+00} of sag)"
                  + " and leaves {4:0.0000E+00} against {5:0.0000E+00}.",
                    name, cheapest.Surface, least, Math.Abs(pick(best).Sag),
                    Magnitude(c1, c2, c3, c5), chosen));
            }

            if (chosen < bestSize) { bestSize = chosen; bestChoice = name; bestSurface = best.Surface; }
        }

        if (alsoCheaper.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("A CHEAPER SURFACE EXISTS FOR THESE, and is not the one above, because the rows");
            sb.AppendLine("are ranked by what the figure BUYS and not by what it costs. Where the two");
            sb.AppendLine("differ it is worth knowing both - a slightly deeper figure can be worth a much");
            sb.AppendLine("better lens, and sometimes it is not.");
            foreach (var line in alsoCheaper) sb.AppendLine(line);
        }

        sb.AppendLine();
        if (bestChoice == "none")
        {
            sb.AppendLine("NO SINGLE FIGURE IMPROVES THE THIRD ORDER OVERALL on this design. Every null");
            sb.AppendLine("costs more elsewhere than it gains, which is what a design already balanced at");
            sb.AppendLine("third order looks like from here.");
        }
        else
        {
            sb.AppendLine(string.Format(inv,
                "BEST SINGLE FIGURE by that measure: {0} on surface {1}, taking the four from",
                bestChoice, bestSurface));
            sb.AppendLine(string.Format(inv,
                "{0:0.0000E+00} to {1:0.0000E+00}, a factor of {2:0.00}.",
                before, bestSize, before / Math.Max(bestSize, 1e-300)));
        }
        sb.AppendLine();
        sb.AppendLine("'size' weights the four equally and is not a spot size - it is there to rank rows");
        sb.AppendLine("that would otherwise be compared four numbers at a time. Petzval is left out of it");
        sb.AppendLine("because no figure moves it, so it would add the same constant to every row.");

        sb.AppendLine();
        sb.AppendLine("PETZVAL IS ABSENT AND THAT IS NOT AN OMISSION. S4 depends on the surface");
        sb.AppendLine("curvatures and the index steps alone, and no deformation of a surface can move");
        sb.AppendLine("it. It is the one thing figuring cannot buy, and it is why a flat field can need");
        sb.AppendLine("a glass change or another element rather than an asphere.");
        sb.AppendLine();
        sb.AppendLine("AND THIS IS THE THIRD ORDER. The contribution above is exactly linear in the");
        sb.AppendLine("coefficient, so these are exact derivatives with no step size to choose. The");
        sb.AppendLine("fifth and seventh orders are not linear in it, will move when the figure is");
        sb.AppendLine("applied, and are not screened here. Treat this as where to START looking.");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Sag(Scalar v, bool reachable)
        => reachable ? v.ToString("0.0000E+00", CultureInfo.InvariantCulture) : "out of reach";

    private static string Coef(Scalar v, bool reachable)
        => reachable ? v.ToString("0.000000000E+00", CultureInfo.InvariantCulture) : "-";
}
