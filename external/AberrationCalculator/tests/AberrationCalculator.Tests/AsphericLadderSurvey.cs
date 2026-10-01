using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// TEMPORARY diagnostic. Where the aspheric tertiary stands today, rung by rung, against both
/// oracles at once. Delete when the aspheric arrangement is settled.
/// </summary>
public class AsphericLadderSurvey
{
    private readonly ITestOutputHelper _out;
    public AsphericLadderSurvey(ITestOutputHelper output) => _out = output;

    private static readonly string[] Designs =
    {
        "Ladder1_Sphere", "Ladder1_A4", "Ladder1_Conic", "Ladder1_FiguredSphere",
        "Ladder2_Sphere", "Ladder2_Sphere_FlatRear",
        "Ladder2_A4_First", "Ladder2_A4_Second", "Ladder2_A4_Both",
        "Ladder2_A4_First_FlatRear", "Ladder2_A4_Then_FiguredSphere",
        "Ladder2_FiguredSphere_First", "Ladder2_FiguredSphere_Second",
        "Ladder2_FiguredSphere_Both", "Ladder2_FiguredSphere_Then_A4",
        "Ladder2_FlatFigured", "Ladder2_FiguredFlatRear", "Ladder2_FiguredNearFlatRear",
        "Ladder2_FlatPlain", "Ladder2_FlatPlain_NearLimit", "Ladder2_Sphere_NearFlatRear",
        "Ladder3_Sphere", "Ladder3_A4_First", "Ladder3_A4_Middle",
        "Ladder3_FiguredSphere_Middle",
        "CookeTriplet", "CookeTriplet_PRMSA_START_LO_ASPHERE",
        "CookeTriplet_SPOTM_START_LO_ASPHERE", "CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8",
        "TertiaryTestbed_Triplet24",
    };

    [Fact]
    public void Survey()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tworst_vs_rays_pct\tworst_k_rays\tworst_vs_forbes_pct"
                    + "\tworst_k_forbes\tforbes_vs_rays_pct\tresidual\tfiguring_work_pct");

        foreach (string name in Designs)
        {
            string row;
            try { row = Row(name); }
            catch (Exception ex) { row = name + "\tFAILED: " + ex.Message; }
            sb.AppendLine(row);
            _out.WriteLine(row);
        }

        // Per-coefficient detail on the rungs that matter.
        foreach (string name in new[] { "Ladder2_A4_First", "Ladder2_A4_Second" })
        {
            sb.AppendLine();
            sb.AppendLine(name + "  k\tbuchdahl\tforbes\trays\tB-F share\tF-R share");
            var d = Detail(name);
            foreach (string line in d) sb.AppendLine(line);
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-survey.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine("written: " + path);
    }

    /// <summary>
    /// Every reading of Sec. 85 the new routine offers, measured against the rays on every
    /// rung. This is the instrument the aspheric arrangement is settled with: a reading either
    /// moves the broken rungs toward the oracle or it does not.
    /// </summary>
    /// <summary>
    /// The best arrangement so far: members one to five of the barred q accumulation from the
    /// identities, the sixth by (85.1) with the split primary, and the figuring's D half out of the
    /// check pass in both the secondaries and the M entries.
    /// </summary>
    private static readonly BuchdahlAsphericScheme.Options Best = new()
    {
        BarredQAccumulationFromIdentities = true,
        FiguredSecondarySplitByDandL = true,
        FiguredMSplitByDandL = true,
        SharedQBarWithSplitPrimary = true,
        YBarredFromSharedAccumulations = true,
    };

    [Fact]
    public void Readings()
    {
        var readings = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("intrinsic-chain-on-pass-ratio",
                new BuchdahlAsphericScheme.Options { IntrinsicChainOnPassRatio = true }),
            ("accumulated-figuring-on-height-ratio",
                new BuchdahlAsphericScheme.Options { AccumulatedFiguringOnHeightRatio = true }),
            ("full-figured-barred-secondary-in-dagger",
                new BuchdahlAsphericScheme.Options { FullFiguredBarredSecondaryInDagger = true }),
            ("eq-68.8-bracket",
                new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true }),
            ("dagger-increment-figured-half",
                new BuchdahlAsphericScheme.Options
                    { DaggerIncrementFiguredHalfOnHeightRatio = true }),
            ("no-figured-correction-in-dagger",
                new BuchdahlAsphericScheme.Options { NoFiguredCorrectionInDagger = true }),
            ("q-side-products-spherical",
                new BuchdahlAsphericScheme.Options { QSideProductsOnSphericalHalves = true }),
            ("split-primary-only",
                new BuchdahlAsphericScheme.Options { SharedQBarWithSplitPrimary = true }),
            ("spherical-accumulations-in-direct-uses",
                new BuchdahlAsphericScheme.Options { SphericalAccumulationsInDirectUses = true }),
            ("dagger-on-increment-alone",
                new BuchdahlAsphericScheme.Options { DaggerCorrectionOnIncrementAlone = true }),
            ("increment-alone + D-half",
                new BuchdahlAsphericScheme.Options
                {
                    DaggerCorrectionOnIncrementAlone = true,
                    FiguredSecondarySplitByDandL = true,
                }),
            ("lift-split-by-D-and-L",
                new BuchdahlAsphericScheme.Options { LiftSplitByDandL = true }),
            ("D-half-into-the-hat",
                new BuchdahlAsphericScheme.Options { FiguredSecondarySplitByDandL = true }),
            ("D-half-plus-85.1",
                new BuchdahlAsphericScheme.Options
                {
                    FiguredSecondarySplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("Y-barred-shared-accumulations",
                new BuchdahlAsphericScheme.Options { YBarredFromSharedAccumulations = true }),
            ("barred-q-from-identities",
                new BuchdahlAsphericScheme.Options { BarredQAccumulationFromIdentities = true }),
            ("identities + D-half",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                }),
            ("identities + D-half + 6th",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                    SixthBarredMemberByEquation851 = true,
                }),
            ("D-half + M + 85.1",
                new BuchdahlAsphericScheme.Options
                {
                    FiguredSecondarySplitByDandL = true,
                    FiguredMSplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("identities + D-half + M",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                    FiguredMSplitByDandL = true,
                }),
            ("identities + D-half + 6th + M",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                    SixthBarredMemberByEquation851 = true,
                    FiguredMSplitByDandL = true,
                }),
            // The identities overwrite members one to five, so the shared-accumulation readings
            // act on the sixth alone: (85.1) on it, with and without the split primary.
            ("ident + D + M + 6th-split",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                    FiguredMSplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("ident + D + M + 6th-dual",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                    FiguredMSplitByDandL = true,
                    SixthBarredMemberFromDuality = true,
                }),
            ("best + 6th quadF +", Best with { SixthMemberExtraPart = 1 }),
            ("best + 6th quadF -", Best with { SixthMemberExtraPart = 1, SixthMemberExtraSign = -1.0 }),
            ("best + 6th cross +", Best with { SixthMemberExtraPart = 2 }),
            ("best + 6th cross -", Best with { SixthMemberExtraPart = 2, SixthMemberExtraSign = -1.0 }),
            ("best + 6th lin +", Best with { SixthMemberExtraPart = 3 }),
            ("best + 6th lin -", Best with { SixthMemberExtraPart = 3, SixthMemberExtraSign = -1.0 }),
            ("best + 6th bracket +", Best with { SixthMemberExtraPart = 4 }),
            ("best + 6th bracket -", Best with { SixthMemberExtraPart = 4, SixthMemberExtraSign = -1.0 }),
            ("best + 6th own x sph", Best with { SixthMemberExtraPart = 5 }),
            ("best + 6th own x all", Best with { SixthMemberExtraPart = 6 }),
            ("both-halves-together",
                new BuchdahlAsphericScheme.Options
                {
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
        };

        var sb = new StringBuilder();
        sb.Append("design\tfiguring_work_pct");
        foreach (var r in readings) sb.Append('\t').Append(r.Name);
        sb.AppendLine();

        foreach (string name in Designs)
        {
            var d = Load(name);
            var bare = LoadStripped(name);
            double work = 0.0;
            for (int k = 1; k <= 20; k++)
                work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

            sb.Append(name).Append('\t')
              .Append((100 * work).ToString("F1", CultureInfo.InvariantCulture));

            foreach (var r in readings)
            {
                double[] tau = NewRoute(name, r.O);

                // EACH COEFFICIENT AGAINST ITSELF, and against Forbes rather than the ray
                // inversion. Both of those were wrong before and both mattered.
                //
                // Dividing by the largest coefficient in the set hid the failures this
                // arrangement actually has. verification.md records tau15 wrong by a factor of
                // nearly five including its sign and tau20 by half, while the large ones agree
                // to one per cent; a small coefficient wrong by five times is nothing when it is
                // divided by the largest, so the instrument reported the design that does that
                // as 1.7 per cent and the readings were being ranked on coefficients that were
                // already right.
                //
                // The old normalisation existed because relative error is meaningless at the ray
                // INVERSION's noise floor. Against Forbes that objection lapses: its series is
                // an expansion rather than a least-squares recovery, it tracks the rays to
                // between 0.001 and 0.13 per cent on every design here, and where the two
                // disagree beyond the recovery's error bar the rays land on Forbes.
                double worst = 0.0;
                int bad = 0, worstK = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double f = d.Forbes[k];
                    if (Math.Abs(f) < 1e-9 * d.Largest) continue;
                    double rel = Math.Abs(tau[k] - f) / Math.Abs(f);
                    if (rel > worst) { worst = rel; worstK = k; }
                    if (rel > 0.01) bad++;
                }
                sb.Append('\t')
                  .Append((100 * worst).ToString("F1", CultureInfo.InvariantCulture))
                  .Append("(t").Append(worstK).Append(")/").Append(bad);
            }
            sb.AppendLine();
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-readings.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Can ONE number at the accumulated-figuring site bring all twenty tau to the rays at once?
    /// If the twenty agree about where the zero is, the structure is right; if they scatter, no
    /// reading of that site can fix it and the search moves elsewhere.
    /// </summary>
    [Fact]
    public void ShiftScan()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder2_A4_First", "Ladder2_A4_Both",
                                        "Ladder2_A4_First_FlatRear", "Ladder2_FlatFigured",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            sb.AppendLine();
            sb.AppendLine(name + "\tx\tworst_pct\tworst_k");
            foreach (double x in new[] { -2.0, -1.0, -0.5, 0.0, 0.25, 0.5, 0.75, 1.0, 2.0 })
            {
                var tau = NewRoute(name, new BuchdahlAsphericScheme.Options
                {
                    AccumulatedFiguringOnHeightRatio = true,
                    HeightRatioShiftFraction = x,
                });
                double worst = 0.0; int kw = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double e = Math.Abs(tau[k] - d.Rays[k]) / d.Largest;
                    if (e > worst) { worst = e; kw = k; }
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\t{0,5}\t{1:F3}\t{2}", x, 100 * worst, kw));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-shift-scan.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// What (68.8)'s dropped bracket actually evaluates to, surface by surface. A reading that
    /// changes nothing might be right and inert, or might be reaching a quantity that is zero
    /// for a reason that makes the whole rung unable to test it - and those are not the same
    /// thing to learn.
    /// </summary>
    [Fact]
    public void BracketSizes()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder3_A4_Middle", "Ladder3_A4_First",
                                        "Ladder2_A4_First", "Ladder2_A4_Second" })
        {
            var rows = RowsFor(name, out int count);
            sb.AppendLine();
            sb.AppendLine(name + "  surf\talpha\trho\tq\tA_p\tAbar_p\tA_q\tbracket");
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i]; var t = r.T;
                double bracket = r.ApFigured
                               * ((t[16] - 2.0 * t[20]) + (2.0 * r.Rho - t[6]) * t[15]);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\t{0}\t{1:E3}\t{2:E3}\t{3:E3}\t{4:E3}\t{5:E3}\t{6:E3}\t{7:E3}",
                    i, r.ApFigured, r.Rho, t[6], t[15], t[16], t[20], bracket));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-brackets.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>Does the reading reach the totals at all, before asking whether it helps?</summary>
    [Fact]
    public void BracketReachesTheTotals()
    {
        foreach (string name in new[] { "CookeTriplet_SPOTM_START_LO_ASPHERE",
                                        "Ladder2_A4_Second" })
        {
            var rows = RowsFor(name, out int count);
            var plain = BuchdahlAsphericScheme.Totals(
                rows, count, BuchdahlAsphericScheme.Options.AsBuilt);

            var rows2 = RowsFor(name, out int count2);
            var moved = BuchdahlAsphericScheme.Totals(
                rows2, count2,
                new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true });

            double worst = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                worst = Math.Max(worst, Math.Abs(plain.T[k] - moved.T[k]));
                worst = Math.Max(worst, Math.Abs(plain.Tbar[k] - moved.Tbar[k]));
            }
            _out.WriteLine($"{name}: largest move in the ten totals = {worst:E4}");
        }
    }

    /// <summary>
    /// For the FIRST secondary we now hold two things that claim to be the same quantity: the
    /// bracket M (68.8) prints, and the difference the scheme can form between the figured
    /// barred secondary and its lift half. Reading 3 uses the difference and helps a great deal
    /// where the figuring is in the middle while hurting where it is first; (68.8) helps less
    /// and hurts nowhere. Both cannot be right, and comparing them at m = 0 says what the
    /// difference carries that the printed bracket does not.
    /// </summary>
    [Fact]
    public void BracketAgainstTheDifference()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\teq68.8_bracket\tsecBarFig-Lift\tdifference\tratio");
        foreach (string name in new[] { "Ladder3_A4_Middle", "Ladder3_A4_First",
                                        "Ladder2_A4_First", "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i]; var t = r.T;
                double printed = r.ApFigured
                               * ((t[16] - 2.0 * t[20]) + (2.0 * r.Rho - t[6]) * t[15]);
                double formed = r.SecBarFig[0] - r.SecBarFigLift[0];
                if (Math.Abs(printed) < 1e-14 && Math.Abs(formed) < 1e-14) continue;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E4}\t{3:E4}\t{4:E4}\t{5:F4}",
                    name, i, printed, formed, formed - printed,
                    Math.Abs(printed) > 1e-20 ? formed / printed : double.NaN));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-bracket-vs-diff.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Every coefficient, against Forbes, as its OWN relative error.
    ///
    /// <para><b>Why not share-of-the-largest.</b> That normalisation was chosen because relative
    /// error is meaningless on a coefficient three orders below the rest, which is where the ray
    /// inversion's noise floor sits. But it hides the failures this arrangement actually has:
    /// <c>docs/verification.md</c> records tau15 wrong "by a factor of nearly five including its
    /// sign" and tau20 by half, while the large ones agree to under one per cent. Divided by the
    /// largest coefficient in the set, a small coefficient wrong by five times reports as
    /// nothing, and a reading can be ranked on the coefficients that were already right.</para>
    ///
    /// <para>Against FORBES the objection to relative error does not apply. Forbes' series is an
    /// expansion, not a least-squares recovery from traced landings, so it has no noise floor of
    /// its own - it tracks the rays to between 0.001 and 0.13 per cent on every design here, and
    /// where the two routes disagree beyond the recovery's error bar the rays land on Forbes.
    /// So each coefficient can be asked about on its own terms.</para>
    /// </summary>
    [Fact]
    public void PerCoefficient()
    {
        var readings = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("eq-68.8", new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true }),
            ("full-fig-dagger",
                new BuchdahlAsphericScheme.Options { FullFiguredBarredSecondaryInDagger = true }),
        };

        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder3_Sphere", "Ladder3_A4_Middle",
                                        "Ladder3_A4_First", "Ladder2_A4_First",
                                        "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var tau = new double[readings.Length][];
            for (int r = 0; r < readings.Length; r++) tau[r] = NewRoute(name, readings[r].O);

            sb.AppendLine();
            sb.Append(name).Append("\tk\tforbes\trays");
            foreach (var r in readings) sb.Append('\t').Append(r.Name).Append("_rel%");
            sb.AppendLine("\tsize_vs_largest");

            for (int k = 1; k <= 20; k++)
            {
                double f = d.Forbes[k];
                sb.Append('\t').Append(k)
                  .Append('\t').Append(f.ToString("E4", CultureInfo.InvariantCulture))
                  .Append('\t').Append(d.Rays[k].ToString("E4", CultureInfo.InvariantCulture));

                foreach (var t in tau)
                    sb.Append('\t').Append(Relative(t[k], f));

                sb.Append('\t')
                  .Append((100 * Math.Abs(f) / d.Largest)
                              .ToString("F2", CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            // The headline: worst relative error over the twenty, and how many are off by more
            // than one per cent - which is the figure verification.md quotes.
            sb.Append("\tWORST/over1pct");
            foreach (var t in tau)
            {
                double worst = 0.0; int bad = 0, k0 = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double f = d.Forbes[k];
                    if (Math.Abs(f) < 1e-9 * d.Largest) continue;   // no signal to divide by
                    double rel = Math.Abs(t[k] - f) / Math.Abs(f);
                    if (rel > worst) { worst = rel; k0 = k; }
                    if (rel > 0.01) bad++;
                }
                sb.Append('\t')
                  .Append((100 * worst).ToString("F1", CultureInfo.InvariantCulture))
                  .Append(" (tau").Append(k0).Append(") / ").Append(bad);
            }
            sb.AppendLine();
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-per-coefficient.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// The ten tertiary coefficients and their barred partners, which is the level Sec. 85
    /// actually computes at, against what Forbes implies for them.
    ///
    /// <para>Paper III's Table II is a linear map from (T1..T10, T-1..T-10) to tau1..tau20 and it
    /// INVERTS, so Forbes' twenty tau give Forbes' twenty totals. Comparing there rather than at
    /// tau is sharper for localising: tau2 and tau3 both draw on T2, so a fault in T2 shows up
    /// twice at tau and once here, and a coefficient that the arrangement builds in one place can
    /// be named.</para>
    /// </summary>
    private static (double[] T, double[] Tbar) TotalsFromTau(double[] t)
    {
        var T = new double[11];
        var B = new double[11];

        T[1] = t[1];
        T[2] = 2.0 * t[3];            B[1] = t[2] - t[3];
        T[3] = t[5];                  B[2] = t[4] - t[5];
        T[4] = t[6];
        T[7] = 8.0 * t[10];
        T[5] = 2.0 * t[9] - 4.0 * t[10];
        B[4] = 2.0 * t[8] - 2.0 * t[9] - 4.0 * t[10];
        B[3] = t[7] - t[8] + t[10];
        T[6] = t[13];                 B[5] = t[11] - t[13];
        T[8] = t[14];                 B[7] = t[12] - t[14];
        T[9] = 2.0 * t[17];
        B[8] = 2.0 * t[16] - 2.0 * t[17];
        B[6] = t[15] - t[16];
        T[10] = t[19];                B[9] = t[18] - t[19];
        B[10] = t[20];

        return (T, B);
    }

    /// <summary>
    /// Which of the twenty totals the confirmed (85.1) reading breaks on the two designs it
    /// makes worse - both of which figure the LAST powered surface.
    ///
    /// <para>The reading is correct by the source: (85.1) gives the barred (Y) member as
    /// <c>q~ 'G-_p - 'G-_q</c> on the same accumulations as its (I) partner, and rebuilding
    /// <c>'S-_1q</c> as an explicit accumulation is algebraically identical to applying the
    /// reading as a delta, so no reformulation can change these numbers. Something else is
    /// wrong, and the totals say where: each T-bar entry is built from named daggers, so the
    /// one that moves names the entry.</para>
    /// </summary>
    [Fact]
    public void WhichTotalsTheConfirmedReadingBreaks()
    {
        var reading = new BuchdahlAsphericScheme.Options
        {
            SharedQBarWithSplitPrimary = true,
            YBarredFromSharedAccumulations = true,
        };

        var sb = new StringBuilder();
        sb.AppendLine("design\tentry\tforbes\tas-built rel%\treading rel%\tverdict");

        foreach (string name in new[] { "Ladder2_A4_Second", "Ladder2_FiguredSphere_Then_A4",
                                        "Ladder3_A4_Middle", "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);
            var (aT, aB) = TotalsFromTau(NewRoute(name, null));
            var (rT, rB) = TotalsFromTau(NewRoute(name, reading));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var f = pass == 0 ? fT : fB;
                var a = pass == 0 ? aT : aB;
                var r = pass == 0 ? rT : rB;
                string tag = pass == 0 ? "T" : "Tbar";

                for (int k = 1; k <= 10; k++)
                {
                    if (Math.Abs(f[k]) < 1e-12 * big) continue;
                    double before = 100 * Math.Abs(a[k] - f[k]) / Math.Abs(f[k]);
                    double after = 100 * Math.Abs(r[k] - f[k]) / Math.Abs(f[k]);
                    if (before < 0.5 && after < 0.5) continue;

                    string verdict = after < before * 0.9 ? "better"
                                   : after > before * 1.1 ? "WORSE" : "same";
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}{2}\t{3:E3}\t{4:F2}\t{5:F2}\t{6}",
                        name, tag, k, f[k], before, after, verdict));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-reading-totals.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Which of the two families of term in the barred rule carries defect 2.
    ///
    /// <para>On <c>Ladder2_A4_Second</c> the figured surface is the last powered one and the one
    /// before it is a sphere, so every contribution to T-bar except that surface's CHECK half is
    /// spherical and known right - the whole 79 per cent error sits in one place. The barred rule
    /// there is <c>q~ T_1 + t115 alpha + Y31 t40</c>, two families of term, and suppressing each
    /// in turn says which carries it. Suppression is a probe, not a candidate arrangement.</para>
    /// </summary>
    [Fact]
    public void WhichTermOfTheBarredRuleCarriesDefectTwo()
    {
        var probes = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("drop own-primary x tertiary-family",
                new BuchdahlAsphericScheme.Options
                    { DropOwnPrimaryTimesTertiaryFamilyInCheckBarred = true }),
            ("drop dagger x own-secondary",
                new BuchdahlAsphericScheme.Options
                    { DropDaggerTimesOwnSecondaryInCheckBarred = true }),
            ("drop both",
                new BuchdahlAsphericScheme.Options
                {
                    DropOwnPrimaryTimesTertiaryFamilyInCheckBarred = true,
                    DropDaggerTimesOwnSecondaryInCheckBarred = true,
                }),
            ("t38 for t40 in check barred",
                new BuchdahlAsphericScheme.Options { IntrinsicSecondaryInCheckBarred = true }),
            ("D half into the hat",
                new BuchdahlAsphericScheme.Options { FiguredSecondarySplitByDandL = true }),
            ("D half + the (85.1) reading",
                new BuchdahlAsphericScheme.Options
                {
                    FiguredSecondarySplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("t38 + the (85.1) reading",
                new BuchdahlAsphericScheme.Options
                {
                    IntrinsicSecondaryInCheckBarred = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
        };

        var sb = new StringBuilder();

        foreach (string name in new[] { "Ladder2_A4_Second", "Ladder2_FiguredSphere_Then_A4",
                                        "Ladder3_A4_Middle" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);

            sb.AppendLine();
            sb.Append(name).Append("\tentry\tforbes");
            foreach (var p in probes) sb.Append('\t').Append(p.Name);
            sb.AppendLine();

            var got = new (double[] T, double[] B)[probes.Length];
            for (int i = 0; i < probes.Length; i++) got[i] = TotalsFromTau(NewRoute(name, probes[i].O));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int k = 1; k <= 10; k++)
            {
                if (Math.Abs(fB[k]) < 1e-12 * big) continue;
                sb.Append("\tTbar").Append(k).Append('\t')
                  .Append(fB[k].ToString("E3", CultureInfo.InvariantCulture));
                foreach (var g in got)
                    sb.Append('\t')
                      .Append((100 * Math.Abs(g.B[k] - fB[k]) / Math.Abs(fB[k]))
                                  .ToString("F2", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-defect2.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Defect 2 again, now over the barred q accumulation the identities supply rather than one
    /// known to be wrong. Every earlier localisation of it was made with the recursion's
    /// accumulation in force, which the identities show was compensating part of it - so which
    /// totals are wrong, and which family of the check half's barred rule carries them, has to be
    /// asked over.
    /// </summary>
    [Fact]
    public void DefectTwoOverTheExactAccumulation()
    {
        var baseline = new BuchdahlAsphericScheme.Options
        {
            BarredQAccumulationFromIdentities = true,
            FiguredSecondarySplitByDandL = true,
        };
        var probes = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("ident+D", baseline),
            ("ident+D+6th", baseline with { SixthBarredMemberByEquation851 = true }),
            ("ident+D+6th+M", baseline with
                { SixthBarredMemberByEquation851 = true, FiguredMSplitByDandL = true }),
            ("ident+D+M+6th-split", baseline with
                {
                    FiguredMSplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("ident+D+M+6th-dual", baseline with
                {
                    FiguredMSplitByDandL = true,
                    SixthBarredMemberFromDuality = true,
                }),
            ("drop own-prim x tert", baseline with
                { DropOwnPrimaryTimesTertiaryFamilyInCheckBarred = true }),
            ("drop dagger x own-sec", baseline with
                { DropDaggerTimesOwnSecondaryInCheckBarred = true }),
            ("t38 for t40", baseline with { IntrinsicSecondaryInCheckBarred = true }),
            ("chain on pass ratio", baseline with { IntrinsicChainOnPassRatio = true }),
        };

        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder2_A4_Second", "Ladder2_FiguredSphere_Then_A4",
                                        "Ladder2_A4_Both", "Ladder3_A4_Middle",
                                        "Ladder2_A4_First", "Ladder3_A4_First",
                                        "CookeTriplet_PRMSA_START_LO_ASPHERE",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);

            // The dual arrangement against Forbes at full precision, tau by tau, so that "0.0"
            // in the readings table is a number and not a rounding.
            var dualTau = NewRoute(name, baseline with
                { FiguredMSplitByDandL = true, SixthBarredMemberFromDuality = true });
            var asBuiltTau = NewRoute(name, null);
            double worstDual = 0.0, worstAsBuilt = 0.0;
            int kDual = 0;
            for (int k = 1; k <= 20; k++)
            {
                if (Math.Abs(d.Forbes[k]) < 1e-9 * d.Largest) continue;
                double rd = Math.Abs(dualTau[k] - d.Forbes[k]) / Math.Abs(d.Forbes[k]);
                double ra = Math.Abs(asBuiltTau[k] - d.Forbes[k]) / Math.Abs(d.Forbes[k]);
                if (rd > worstDual) { worstDual = rd; kDual = k; }
                worstAsBuilt = Math.Max(worstAsBuilt, ra);
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "#tau {0}: dual worst {1:E3} (tau{2}), as-built worst {3:E3}, forbes vs rays {4:E3}",
                name, worstDual, kDual, worstAsBuilt, RaysAgreement(d)));

            var got = new (double[] T, double[] B)[probes.Length];
            for (int i = 0; i < probes.Length; i++)
                got[i] = TotalsFromTau(NewRoute(name, probes[i].O));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
                big = Math.Max(big, Math.Max(Math.Abs(fT[k]), Math.Abs(fB[k])));

            sb.AppendLine();
            sb.Append(name).Append("\tentry\tforbes");
            foreach (var p in probes) sb.Append('\t').Append(p.Name);
            sb.AppendLine();

            for (int pass = 0; pass < 2; pass++)
            {
                var f = pass == 0 ? fT : fB;
                for (int k = 1; k <= 10; k++)
                {
                    if (Math.Abs(f[k]) < 1e-12 * big) continue;
                    bool any = false;
                    var line = new StringBuilder();
                    line.Append('\t').Append(pass == 0 ? "T" : "Tbar").Append(k).Append('\t')
                        .Append(f[k].ToString("E3", CultureInfo.InvariantCulture));
                    foreach (var g in got)
                    {
                        double v = pass == 0 ? g.T[k] : g.B[k];
                        double rel = 100 * Math.Abs(v - f[k]) / Math.Abs(f[k]);
                        if (rel >= 0.5) any = true;
                        line.Append('\t').Append(rel.ToString("F2", CultureInfo.InvariantCulture));
                    }
                    if (any) sb.AppendLine(line.ToString());
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-defect2-exact.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Which SINGLE entry, wrong by a single factor, explains the totals that remain wrong over
    /// the exact accumulation.
    ///
    /// <para>For each entry of each pass: scale it by <c>1 + eps</c>, read the change in all
    /// twenty totals as its contribution <c>c</c>, and fit the one multiplier <c>x</c> that best
    /// removes the error <c>e</c> (each total weighed by its own size, as the tables report
    /// them). The explained share <c>1 - |e - x c|^2 / |e|^2</c> ranks the entries. A fault in
    /// one entry shows as a share near one with a plain multiplier; a share that stays low for
    /// every entry says the fault is not a single entry's value at all.</para>
    /// </summary>
    [Fact]
    public void WhichSingleEntryExplainsTheRemainingTotals()
    {
        // The best arrangement so far: members one to five from the identities, the sixth by
        // (85.1) with the split primary, and the D half out of the check pass in both the
        // secondaries and the M entries. What is left is on the triplets alone.
        var baseline = new BuchdahlAsphericScheme.Options
        {
            BarredQAccumulationFromIdentities = true,
            FiguredSecondarySplitByDandL = true,
            FiguredMSplitByDandL = true,
            SharedQBarWithSplitPrimary = true,
            YBarredFromSharedAccumulations = true,
        };

        var indices = new List<int> { 10, 13 };
        for (int m = 15; m <= 30; m++) indices.Add(m);
        indices.AddRange(new[] { 38, 44, 45, 50, 51, 54, 55, 59, 61, 65, 66 });
        for (int m = 69; m <= 80; m++) indices.Add(m);
        for (int m = 101; m <= 114; m++) indices.Add(m);
        for (int m = 121; m <= 130; m++) indices.Add(m);

        const double eps = 1e-4;
        var sb = new StringBuilder();

        foreach (string name in new[] { "CookeTriplet_SPOTM_START_LO_ASPHERE",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8",
                                        "CookeTriplet_PRMSA_START_LO_ASPHERE",
                                        "Ladder2_A4_Both" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);
            var (bT, bB) = TotalsFromTau(NewRoute(name, baseline));

            var e = new double[20];
            var w = new double[20];
            double big = 0.0;
            for (int k = 1; k <= 10; k++) big = Math.Max(big, Math.Max(Math.Abs(fT[k]), Math.Abs(fB[k])));
            for (int k = 1; k <= 10; k++)
            {
                w[k - 1] = Math.Abs(fT[k]) < 1e-12 * big ? 0.0 : 1.0 / Math.Abs(fT[k]);
                w[k + 9] = Math.Abs(fB[k]) < 1e-12 * big ? 0.0 : 1.0 / Math.Abs(fB[k]);
                e[k - 1] = w[k - 1] * (fT[k] - bT[k]);
                e[k + 9] = w[k + 9] * (fB[k] - bB[k]);
            }
            double ee = 0.0;
            foreach (double v in e) ee += v * v;

            var ranked = new List<(double Share, string Label, double X, string Top)>();
            foreach (bool check in new[] { true, false })
                foreach (int idx in indices)
                {
                    var (pT, pB) = TotalsFromTau(NewRoute(name, baseline with
                        { ScaleOneEntry = (idx, 1.0 + eps, check) }));
                    var c = new double[20];
                    double cc = 0.0, ec = 0.0;
                    for (int k = 1; k <= 10; k++)
                    {
                        c[k - 1] = w[k - 1] * (pT[k] - bT[k]) / eps;
                        c[k + 9] = w[k + 9] * (pB[k] - bB[k]) / eps;
                    }
                    for (int k = 0; k < 20; k++) { cc += c[k] * c[k]; ec += e[k] * c[k]; }
                    if (cc < 1e-30) continue;
                    double x = ec / cc;
                    double share = ee < 1e-30 ? 0.0 : 1.0 - (ee - x * ec) / ee;

                    // The totals this entry reaches most, so a high share can be read.
                    var reach = new List<(double, string)>();
                    for (int k = 0; k < 20; k++)
                        if (Math.Abs(c[k]) > 1e-12)
                            reach.Add((Math.Abs(c[k]), (k < 10 ? "T" : "Tb") + (k < 10 ? k + 1 : k - 9)));
                    reach.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                    string top = string.Join(",", reach.GetRange(0, Math.Min(4, reach.Count))
                                                       .ConvertAll(r => r.Item2));

                    ranked.Add((share, (check ? "check t" : "hat t") + idx, x, top));
                }

            ranked.Sort((a, b) => b.Share.CompareTo(a.Share));
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}  (rms relative error over the totals {1:F2} %)",
                name, 100 * Math.Sqrt(ee / 20)));
            sb.AppendLine("\tentry\texplained\tmultiplier-1\treaches");
            foreach (var r in ranked.GetRange(0, Math.Min(12, ranked.Count)))
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\t{0}\t{1:F3}\t{2:E3}\t{3}", r.Label, r.Share, r.X, r.Top));
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-single-entry.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Where the arrangement goes wrong at the level it computes: which of the twenty totals,
    /// and by how much of itself.
    /// </summary>
    [Fact]
    public void PerTotal()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tentry\tforbes\tbuchdahl\trel%\tsize_vs_largest%");

        foreach (string name in new[] { "Ladder3_Sphere", "Ladder3_A4_Middle",
                                        "Ladder3_A4_First", "Ladder2_A4_First",
                                        "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);
            var (bT, bB) = TotalsFromTau(NewRoute(name, null));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var f = pass == 0 ? fT : fB;
                var b = pass == 0 ? bT : bB;
                string tag = pass == 0 ? "T" : "Tbar";
                for (int k = 1; k <= 10; k++)
                {
                    if (Math.Abs(f[k]) < 1e-12 * big) continue;
                    double rel = 100 * Math.Abs(b[k] - f[k]) / Math.Abs(f[k]);
                    if (rel < 0.5) continue;              // only what is actually wrong
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}{2}\t{3:E3}\t{4:E3}\t{5:F2}\t{6:F2}",
                        name, tag, k, f[k], b[k], rel, 100 * Math.Abs(f[k]) / big));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-per-total.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Worst disagreement between Forbes and the ray inversion, over the coefficients the ray
    /// inversion states well, as a share of the largest - the oracle's own floor on a design.
    /// </summary>
    private static double RaysAgreement(Loaded d)
    {
        double worst = 0.0;
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(d.Forbes[k] - d.Rays[k]) / d.Largest);
        return worst;
    }

    /// <summary>One coefficient's relative error, or a dash where there is nothing to divide by.</summary>
    private static string Relative(double mine, double reference)
    {
        if (Math.Abs(reference) < 1e-300) return "-";
        return (100 * Math.Abs(mine - reference) / Math.Abs(reference))
            .ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Pins how M (68.8)'s accumulations map onto the scheme's entries, using the SPHERICAL case
    /// as the identity - it is verified against Buchdahl's own printed numbers, so whatever the
    /// symbols mean, the two must agree there.
    ///
    /// <para>(68.8) has <c>s-_1p = q s_1p - ['A_q - q('A-_p + 'A_q) + q^2 'A_p] a_p + ...</c>,
    /// and the scheme forms the same barred entry as <c>a_p t31 + q s_1p</c>. So <c>t31</c> must
    /// BE the negative of that bracket. If it is, reading the bracket off t15, t16 and t20 is
    /// right and the (68.8) reading was transcribed correctly; if it is not, that reading was
    /// measuring the wrong quantity and its result means nothing.</para>
    /// </summary>
    [Fact]
    public void WhatTheAccumulationsInEq688Are()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tt31\t-[A_q-q(Abar_p+A_q)+q^2 A_p]\tdiff\tusing t21 instead");

        foreach (string name in new[] { "Ladder3_Sphere", "CookeTriplet",
                                        "Ladder3_A4_Middle", "KingslakeDG" })
        {
            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var t = rows[i].T;
                double q = t[6];
                double withT20 = -(t[20] - q * (t[16] + t[20]) + q * q * t[15]);
                double withT21 = -(t[21] - q * (t[16] + t[21]) + q * q * t[15]);
                if (Math.Abs(t[31]) < 1e-14 && Math.Abs(withT20) < 1e-14) continue;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E5}\t{3:E5}\t{4:E2}\t{5:E5}",
                    name, i, t[31], withT20, t[31] - withT20, withT21));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-t31.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// How much of the aspheric machinery a FIGURED SPHERE actually exercises.
    ///
    /// <para>The control rests on it: those rungs come out exact, the r^4 rungs do not, and the
    /// difference between them is what says the defect lives in the figuring's PRIMARY content.
    /// That inference is only worth as much as the control is - if a figured sphere barely
    /// perturbs the scheme at all, "exact" there is not evidence of anything.</para>
    ///
    /// <para>So this prints, per figured surface, the three things the scheme carries: the
    /// figured primary alpha, the figured secondary, and the figured tertiary. A figured sphere
    /// should show alpha at zero and the other two alive; if all three are near zero it is a null
    /// test wearing a control's clothes.</para>
    /// </summary>
    [Fact]
    public void WhatAFiguredSphereExercises()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\talpha\t|sec_fig|\t|z_check|\tfiguring_work_pct");

        foreach (string name in new[] { "Ladder1_FiguredSphere",
                                        "Ladder2_FiguredSphere_First",
                                        "Ladder2_FiguredSphere_Both",
                                        "Ladder3_FiguredSphere_Middle",
                                        "Ladder1_A4", "Ladder2_A4_First",
                                        "Ladder3_A4_Middle" })
        {
            var d = Load(name);
            var bare = LoadStripped(name);
            double work = 0.0;
            for (int k = 1; k <= 20; k++)
                work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];

                double sec = 0.0, z = 0.0;
                for (int m = 1; m <= 6; m++) sec = Math.Max(sec, Math.Abs(r.SecFig[m]));
                for (int m = 1; m <= 10; m++) z = Math.Max(z, Math.Abs(r.ZCheck[m]));
                if (Math.Abs(r.ApFigured) < 1e-15 && sec < 1e-15 && z < 1e-15) continue;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E3}\t{3:E3}\t{4:E3}\t{5:F2}",
                    name, i, r.ApFigured, sec, z, 100 * work));
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-control.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>Is t86 the accumulated first q-side secondary on a FIGURED system?</b>
    ///
    /// <para>Six dagger corrections have failed, and every one assumed that <c>t86|i -
    /// t86|i-1</c> is surface i-1's q-side secondary. Asking whether the increment is "a
    /// per-surface quantity" settles nothing - any closed form of accumulations has increments
    /// that depend only on what lies at and before the surface. The question with content is
    /// whether the closed form is <c>'S1_q</c> at all once the system is figured.</para>
    ///
    /// <para><b>An independent value.</b> M (21.6) gives <c>omega1 = 'S-1_p - 'S1_q</c> and
    /// (22.41) <c>omega7 = 4 'S-1_p - 'S2_p</c>; together they give
    /// <c>omega7 - 2 omega1 = 3[AB] + paraxial terms</c>, and (22.12) accumulates [AB] surface
    /// by surface from the primaries. So</para>
    /// <code>
    ///   'S1_q = t70 - (omega7 - 3[AB] + N1 vp^2 dvp - (1/4) N1 dvp (vp^2 + 3 vp1^2)) / 2
    /// </code>
    /// <para>from nothing but the p-side secondary sums and the primaries - all verified on
    /// figured systems, by the primary and secondary identities and against Forbes.
    /// <c>TheBracketReproducesTheDeterminedAB</c> closes this at 1E-9 on Buchdahl's spherical
    /// triplet. The identities come from the characteristic function, not from the surfaces
    /// being spheres, so they hold for figuring as well.</para>
    ///
    /// <para>Each residual is printed against the figured content of t86 itself - the
    /// difference from a spherical twin with the same stop - so it reads as a share of what
    /// the figuring put there.</para>
    /// </summary>
    [Fact]
    public void IsT86TheAccumulatedFirstQSecondary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tt86\tidentity\trel\tt86_figured\tresid/figured"
                    + "\td_t86\td_identity\td_resid/d_figured");

        foreach (string name in Designs)
        {
            var rows = RowsFor(name, out int count, out var twin, out double n1);
            double vp1 = rows[1][2];
            double ab = 0.0;
            double prevT86 = 0.0, prevId = 0.0, prevFig = 0.0;

            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];
                double vp = r[2];
                double omega7 = 4.0 * r[70] - r[71];
                double dvp = vp * vp - vp1 * vp1;
                double omega1 = 0.5 * (omega7 - 3.0 * ab + n1 * vp * vp * dvp
                                       - 0.25 * n1 * dvp * (vp * vp + 3.0 * vp1 * vp1));
                double identity = r[70] - omega1;
                double t86 = r[86];
                double figured = t86 - twin[i][86];

                double scale = Math.Max(Math.Abs(t86), Math.Abs(identity));
                double rel = scale < 1e-14 ? 0.0 : Math.Abs(t86 - identity) / scale;
                double ofFig = Math.Abs(figured) < 1e-14 ? double.NaN
                             : Math.Abs(t86 - identity) / Math.Abs(figured);

                double dT = t86 - prevT86, dI = identity - prevId, dF = figured - prevFig;
                double dOfFig = Math.Abs(dF) < 1e-14 ? double.NaN : Math.Abs(dT - dI) / Math.Abs(dF);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E6}\t{3:E6}\t{4:E2}\t{5:E3}\t{6:E2}\t{7:E4}\t{8:E4}\t{9:E2}",
                    name, i, t86, identity, rel, figured, ofFig, dT, dI, dOfFig));

                prevT86 = t86; prevId = identity; prevFig = figured;

                // (22.12): Delta[AB] = (1/N1){(A|b) - (B|a) + (a|b)}, the pairing skew over p, q.
                double Ap = r[15], Aq = r[20], Bp = 2.0 * r[16], Bq = r[22];
                double ap = r[10], aq = r[99], bp = 2.0 * r[11], bq = r[100];
                ab += (Ap * bq - Aq * bp - (Bp * aq - Bq * ap) + (ap * bq - aq * bp)) / n1;
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-t86-identity.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>What the barred q-side accumulation the dagger family carries SHOULD be</b>, from the
    /// identities, against what the scheme builds.
    ///
    /// <para>The (I) barred members are <c>t102 = q t70 - 'S-1_q</c> and partners, so the
    /// scheme's <c>'S-_q</c> is read back as <c>q 'S-_p - t102</c>. <see cref="BuchdahlSecondaryQ"/>
    /// recovers the same accumulations from M Sec. 22 without the dagger recursion at all -
    /// <c>Sbar_1q</c> from omega9 and t73, <c>Sbar_3q</c> from omega15 and t79, neither touching
    /// a q-side closed form. So the value the recursion must reach is known per surface, and
    /// every reading of the correction can be judged against it rather than against the totals
    /// several stages downstream.</para>
    ///
    /// <para>Printed four ways: the (I) member as built, the same with the lift correction taken
    /// back out, and the (Y) member likewise on the height ratio - so it says whether the
    /// correction should exist, and on which family.</para>
    /// </summary>
    [Fact]
    public void WhatTheBarredQAccumulationShouldBe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tm\tidentity\tI_built\tI_nocorr\tY_built"
                    + "\terr_I_built\terr_I_nocorr\terr_Y_built\tlift_corr");

        // (m, barred p sum, (I) barred member): Sbar_1q .. Sbar_5q.
        var sites = new[] { (1, 70, 102), (2, 72, 104), (3, 74, 107), (4, 76, 109), (5, 78, 112) };

        foreach (string name in Designs)
        {
            var rows = RowsFor(name, out int count, out _, out double n1);
            var corr = new double[6];

            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];
                var t = r.T;

                if (i > 1)
                {
                    var prv = rows[i - 1];
                    double dr = prv.Rho - prv.T[6];
                    for (int m = 0; m < 5; m++) corr[m] += dr * prv.SecBarFigLift[m];
                }

                if (r.FlatInCollimatedSpace || Math.Abs(t[6]) > 1e6) continue;
                var id = BuchdahlSecondaryQ.At(rows, i, n1);

                // The recovery's one internal check, (22.42) against (22.53) for Sbar_2q, on the
                // figured rows as well - it passes through t92 and so vouches for the closed forms
                // Sbar_2q, Sbar_4q and Sbar_5q lean on.
                double alt = BuchdahlSecondaryQ.SecondBarredQAlternative(rows, i, n1);
                double altScale = Math.Max(Math.Abs(alt), 1e-14);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "#alt\t{0}\t{1}\t(22.42) {2:E6}\t(22.53) {3:E6}\trel {4:E2}",
                    name, i, id[2], alt, Math.Abs(id[2] - alt) / altScale));

                foreach (var (m, sp, member) in sites)
                {
                    double target = id[m];
                    double iBuilt = t[6] * t[sp] - t[member];
                    double iNo = t[6] * t[sp] - (t[member] + corr[m - 1]);
                    // The (Y) recursion carries no lift correction, so it has no second column.
                    double yBuilt = r.Rho * t[sp] - r.Y[member];

                    double scale = Math.Max(Math.Abs(target), 1e-14);
                    string E(double v) =>
                        (Math.Abs(v - target) / scale).ToString("E2", CultureInfo.InvariantCulture);

                    if (Math.Abs(target) < 1e-14 && Math.Abs(iBuilt) < 1e-14) continue;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3:E5}\t{4:E5}\t{5:E5}\t{6:E5}\t{7}\t{8}\t{9}\t{10:E3}",
                        name, i, m, target, iBuilt, iNo, yBuilt,
                        E(iBuilt), E(iNo), E(yBuilt), corr[m - 1]));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-qbar-identity.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>What per-surface correction the barred q recursion needs</b>, measured on the five
    /// members the identities supply, so that it can be carried to the sixth, which they cannot.
    ///
    /// <para>Each recursion adds, per surface j, <c>q_j dS_mq</c> plus its dagger x a_q, b_q
    /// terms to <c>'S-_mq</c>, and then the lift correction. Without the lift, that sum reaches
    /// the identities exactly on the spherical twin, which is the gate printed first. On a figured
    /// system the shortfall against the identities is what a correct aspheric form must supply. It
    /// is fitted per member over every surface of every design on a few structural candidates: the
    /// right set leaves a residual at rounding, with the same coefficients for all five.</para>
    /// </summary>
    [Fact]
    public void WhatTheBarredQRecursionIsMissing()
    {
        int[] Q = { 86, 89, 92, 94, 97, 98 };
        int[] P = { 70, 72, 74, 76, 78, 80 };
        // The figured half of each increment in its parts: the linear S-bar_p term, paraxial x
        // figured primary, figured x figured, spherical x figured; then the q-side D half and the
        // split-primary dagger term. The hypothesis carried over from the verified p side is
        // (1, -1, 1, 1, 1, 1), the cross term's coefficient being the open question.
        string[] names = { "Sfig", "qD", "lin", "quadF", "cross", "split" };
        double decompWorst = 0.0;
        var hyp = new double[5];
        var hypDen = new double[5];
        var hyp2 = new double[5];
        var hyp3 = new double[5];
        var hyp3d = new double[5];
        var hyp5 = new double[5];
        var hyp6 = new double[5];
        var hyp0 = new double[5];
        var detail5 = new StringBuilder();
        detail5.AppendLine("design\tj\tm\tshortfall\tlift+split_rel\tH5_own_all_rel\tH6_own_sph_rel");
        var detail3 = new StringBuilder();
        detail3.AppendLine("design\tj\tm\tshortfall\tH3\tH3_keepD\tH3_rel\tH3_keepD_rel");
        var detail2 = new StringBuilder();
        detail2.AppendLine("design\tj\tm\tq\trho\tshortfall\tqtilde_rule+split\tD_fix\tH2_resid\tH2_rel");
        var ys = new List<double>[5];
        var xs = new List<double[]>[5];
        for (int m = 0; m < 5; m++) { ys[m] = new List<double>(); xs[m] = new List<double[]>(); }

        var detail = new StringBuilder();
        detail.AppendLine("design\tj\tm\tdS_identity\tshortfall\tSfig\tqD\tlin\tquadF\tcross\tsplit"
                        + "\thyp_resid");
        double twinWorst = 0.0;

        static double[] Recursion(double[] t, double[] p)
        {
            double qj = p[6];
            var d = new double[6];
            for (int m = 0; m < 6; m++) d[m] = qj * (t[new[] { 86, 89, 92, 94, 97, 98 }[m]]
                                                     - p[new[] { 86, 89, 92, 94, 97, 98 }[m]]);
            d[0] += p[31] * p[99];
            d[1] += p[31] * p[100] + p[32] * p[99];
            d[2] += 0.5 * qj * p[100] * p[31] + p[33] * p[99];
            d[3] += p[32] * p[100];
            d[4] += (0.5 * qj * p[32] + p[33]) * p[100];
            d[5] += 0.5 * qj * p[33] * p[100];
            return d;
        }

        foreach (string name in Designs)
        {
            var rows = RowsFor(name, out int count, out var twin, out double n1);

            for (int i = 2; i < count - 1; i++)
            {
                var r = rows[i];
                var pr = rows[i - 1];
                if (r.FlatInCollimatedSpace || pr.FlatInCollimatedSpace
                    || Math.Abs(r.T[6]) > 1e6 || Math.Abs(pr.T[6]) > 1e6) continue;

                var idI = BuchdahlSecondaryQ.At(rows, i, n1);
                var idJ = BuchdahlSecondaryQ.At(rows, i - 1, n1);
                var rec = Recursion(r.T, pr.T);

                // The gate: the spherical twin's recursion against its own identities.
                var twI = BuchdahlSecondaryQ.At(twin, i, n1);
                var twJ = BuchdahlSecondaryQ.At(twin, i - 1, n1);
                var twRec = Recursion(twin[i].T, twin[i - 1].T);
                for (int m = 0; m < 5; m++)
                {
                    double dTw = twI[m + 1] - twJ[m + 1];
                    if (Math.Abs(dTw) > 1e-14)
                        twinWorst = Math.Max(twinWorst, Math.Abs(dTw - twRec[m]) / Math.Abs(dTw));
                }

                var t = r.T;
                var p = pr.T;
                double qj = p[6];
                double dr = pr.Rho - qj;
                double e31 = pr.Y[31] - p[31], e32 = pr.Y[32] - p[32], e33 = pr.Y[33] - p[33];
                double f99 = p[99] - twin[i - 1].T[99], f100 = p[100] - twin[i - 1].T[100];
                var split = new[]
                {
                    e31 * f99,
                    e31 * f100 + e32 * f99,
                    0.5 * qj * e31 * f100 + e33 * f99,
                    e32 * f100,
                    (0.5 * qj * e32 + e33) * f100,
                };

                var partsI = QParts(r.T, twin[i].T, P);
                var partsJ = QParts(pr.T, twin[i - 1].T, P);

                // H3: (85.2) barred on the q side - s-bar = q s + t31 a^ + Y31 a^v + (q~ - q)(check
                // part of s). The check part is what surface j's OWN figured quantities put into
                // the increment: its figured primaries, and the non-D half of its figured barred
                // secondary, times whatever they multiply. Evaluated as the closed forms at i with
                // and without them, on i's paraxial quantities.
                var accI = new double[10];
                var accNoOwn = new double[10];
                for (int k = 0; k < 10; k++)
                {
                    accI[k] = r.T[15 + k];
                    double ownFig = (r.T[15 + k] - twin[i].T[15 + k])
                                  - (pr.T[15 + k] - twin[i - 1].T[15 + k]);
                    accNoOwn[k] = accI[k] - ownFig;
                }
                var sI = new double[6];
                var sNoOwn = new double[6];
                var sNoOwnKeepD = new double[6];
                for (int k = 0; k < 6; k++)
                {
                    sI[k] = r.T[P[k]];
                    double ownFig = (r.T[P[k]] - twin[i].T[P[k]])
                                  - (pr.T[P[k]] - twin[i - 1].T[P[k]]);
                    sNoOwn[k] = sI[k] - (ownFig - pr.T[6] * pr.SecDFigured[k]);
                    sNoOwnKeepD[k] = sI[k] - ownFig;
                }
                var qWith = QClosed(r.T[9], r.T[81], r.T[82], accI, sI);
                var checkPart = QClosed(r.T[9], r.T[81], r.T[82], accNoOwn, sNoOwn);
                var checkPartD = QClosed(r.T[9], r.T[81], r.T[82], accNoOwn, sNoOwnKeepD);
                for (int k = 0; k < 6; k++)
                {
                    checkPart[k] = qWith[k] - checkPart[k];
                    checkPartD[k] = qWith[k] - checkPartD[k];
                }

                // H5 / H6: the lift and split as built, plus (q~ - q) on the PRODUCTS carrying
                // surface j's own figured primaries - all of them (H5), or only those paired with
                // spherical accumulations (H6). Paraxial x own-figured terms get nothing.
                var z6 = new double[6];
                var aSph = new double[10];
                var own = new double[10];
                var sphPlusOwn = new double[10];
                for (int k = 0; k < 10; k++)
                {
                    aSph[k] = twin[i].T[15 + k];
                    own[k] = accI[k] - accNoOwn[k];
                    sphPlusOwn[k] = aSph[k] + own[k];
                }
                var prodWith = QClosed(0.0, 0.0, 0.0, accI, z6);
                var prodWithout = QClosed(0.0, 0.0, 0.0, accNoOwn, z6);
                var cs1 = QClosed(0.0, 0.0, 0.0, sphPlusOwn, z6);
                var cs2 = QClosed(0.0, 0.0, 0.0, aSph, z6);
                var cs3 = QClosed(0.0, 0.0, 0.0, own, z6);

                for (int m = 0; m < 5; m++)
                {
                    double dId = idI[m + 1] - idJ[m + 1];
                    double y = dId - rec[m];
                    double dQfig = (t[Q[m]] - twin[i].T[Q[m]]) - (p[Q[m]] - twin[i - 1].T[Q[m]]);

                    double dS = partsI.S[m] - partsJ.S[m];
                    double dLin = partsI.Lin[m] - partsJ.Lin[m];
                    double dQuad = partsI.QuadF[m] - partsJ.QuadF[m];
                    double dCross = partsI.Cross[m] - partsJ.Cross[m];
                    double sum = dS + dLin + dQuad + dCross;
                    double gate = Math.Max(Math.Abs(dQfig), 1e-14);
                    if (Math.Abs(dQfig) > 1e-12)
                        decompWorst = Math.Max(decompWorst, Math.Abs(sum - dQfig) / gate);

                    var x = new[]
                    {
                        dr * dS,
                        dr * qj * pr.SecDFigured[m],
                        dr * dLin,
                        dr * dQuad,
                        dr * dCross,
                        split[m],
                    };
                    if (Math.Abs(dId) < 1e-14 && Math.Abs(y) < 1e-14) continue;

                    double h = y - (x[0] - x[1] + x[2] + x[3] + x[4] + x[5]);
                    hyp[m] += h * h / (dId * dId);
                    hypDen[m] += y * y / (dId * dId);

                    // H2: the D content the IDENTITIES carry for each member - through the linear
                    // p-side term each one's recovery uses, unbarred S_p riding no ratio and the
                    // barred S-bar_p inside a q-side closed form riding q - against the q q~ D_m
                    // the q~ rule gives the recursion's q dQ_m.
                    var D = pr.SecDFigured;
                    double truthD = m switch
                    {
                        0 => 0.5 * D[2],           // (om9 + 2 S3p) / 4
                        1 => 2.0 * qj * D[2],      // om8 + 2 S3q,  S3q carries S-bar3p
                        2 => 2.0 * D[5],           // (om15 + 4 S6p) / 2
                        3 => qj * D[4],            // (om11 + 2 S5q) / 2, S5q carries S-bar5p
                        _ => 4.0 * qj * D[5],      // om14 + 4 S6q,  S6q carries S-bar6p
                    };
                    double h2 = y - (dr * dQfig + split[m] + truthD - qj * pr.Rho * D[m]);
                    hyp2[m] += h2 * h2 / (dId * dId);
                    double liftM = dr * pr.SecBarFigLift[m];
                    double h5 = y - (liftM + split[m] + dr * (prodWith[m] - prodWithout[m]));
                    double h6 = y - (liftM + split[m] + dr * (cs1[m] - cs2[m] - cs3[m]));
                    double h0 = y - (liftM + split[m]);
                    hyp5[m] += h5 * h5 / (dId * dId);
                    hyp6[m] += h6 * h6 / (dId * dId);
                    hyp0[m] += h0 * h0 / (dId * dId);
                    if (Math.Abs(y) > 1e-9 * Math.Abs(dId))
                        detail5.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3:E4}\t{4:E2}\t{5:E2}\t{6:E2}",
                            name, i - 1, m + 1, y, Math.Abs(h0 / dId), Math.Abs(h5 / dId),
                            Math.Abs(h6 / dId)));
                    double h3 = y - (dr * checkPart[m] + split[m]);
                    double h3d = y - (dr * checkPartD[m] + split[m]);
                    hyp3[m] += h3 * h3 / (dId * dId);
                    hyp3d[m] += h3d * h3d / (dId * dId);
                    if (Math.Abs(y) > 1e-9 * Math.Abs(dId))
                        detail3.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3:E4}\t{4:E4}\t{5:E4}\t{6:E2}\t{7:E2}",
                            name, i - 1, m + 1, y, dr * checkPart[m] + split[m],
                            dr * checkPartD[m] + split[m], Math.Abs(h3 / dId), Math.Abs(h3d / dId)));
                    if (Math.Abs(y) > 1e-9 * Math.Abs(dId))
                        detail2.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3:F5}\t{4:F5}\t{5:E4}\t{6:E4}\t{7:E4}\t{8:E4}\t{9:E2}",
                            name, i - 1, m + 1, qj, pr.Rho, y, dr * dQfig + split[m],
                            truthD - qj * pr.Rho * D[m], h2, Math.Abs(h2 / dId)));

                    double w = 1.0 / Math.Max(Math.Abs(dId), 1e-14);
                    ys[m].Add(w * y);
                    var wx = new double[x.Length];
                    for (int k = 0; k < x.Length; k++) wx[k] = w * x[k];
                    xs[m].Add(wx);

                    if (Math.Abs(y) > 1e-9 * Math.Abs(dId))
                        detail.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3:E4}\t{4:E4}\t{5:E4}\t{6:E4}\t{7:E4}\t{8:E4}\t{9:E4}"
                            + "\t{10:E4}\t{11:E4}",
                            name, i - 1, m + 1, dId, y, x[0], x[1], x[2], x[3], x[4], x[5], h));
                }
            }
        }

        var sets = new[]
        {
            new[] { 0, 1, 5 }, new[] { 0, 1, 2, 3, 5 }, new[] { 0, 2, 3, 4, 5 },
            new[] { 0, 1, 2, 3, 4, 5 },
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "#gate: spherical twin, recursion without lift vs identities, worst rel {0:E2}",
            twinWorst));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "#gate: the four parts sum to the figured half of the increment, worst rel {0:E2}",
            decompWorst));
        for (int m = 0; m < 5; m++)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "#hypothesis (1,-1,1,1,1,1): S{0} residual share {1:E3}",
                m + 1, hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp[m] / hypDen[m])));
        for (int m = 0; m < 5; m++)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "#H2 identity D content: S{0} residual share {1:E3}",
                m + 1, hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp2[m] / hypDen[m])));
        for (int m = 0; m < 5; m++)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "#H3 own check part: S{0} residual share {1:E3}   keeping D in it {2:E3}",
                m + 1, hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp3[m] / hypDen[m]),
                hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp3d[m] / hypDen[m])));
        for (int m = 0; m < 5; m++)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "#own products: S{0}  lift+split {1:E3}   H5 own x all {2:E3}   H6 own x sph {3:E3}",
                m + 1,
                hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp0[m] / hypDen[m]),
                hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp5[m] / hypDen[m]),
                hypDen[m] < 1e-300 ? 0.0 : Math.Sqrt(hyp6[m] / hypDen[m])));
        sb.AppendLine();
        sb.Append(detail5);
        sb.AppendLine();
        sb.Append(detail3);
        sb.AppendLine();
        sb.Append(detail2);
        sb.AppendLine();
        sb.AppendLine("set\tmember\tn\tcoefficients\tresidual_share\tworst_row_rel");
        foreach (var set in sets)
        {
            string label = string.Join("+", Array.ConvertAll(set, k => names[k]));
            for (int m = 0; m < 5; m++)
            {
                int n = ys[m].Count, k = set.Length;
                var ata = new double[k, k];
                var aty = new double[k];
                double yy = 0.0;
                for (int row = 0; row < n; row++)
                {
                    yy += ys[m][row] * ys[m][row];
                    for (int a = 0; a < k; a++)
                    {
                        aty[a] += xs[m][row][set[a]] * ys[m][row];
                        for (int b = 0; b < k; b++)
                            ata[a, b] += xs[m][row][set[a]] * xs[m][row][set[b]];
                    }
                }
                var c = Solve(ata, aty);
                if (c == null)
                {
                    sb.AppendLine($"{label}\tS{m + 1}\t{n}\tsingular");
                    continue;
                }
                double rr = 0.0, worst = 0.0;
                for (int row = 0; row < n; row++)
                {
                    double fit = 0.0;
                    for (int a = 0; a < k; a++) fit += c[a] * xs[m][row][set[a]];
                    double res = ys[m][row] - fit;
                    rr += res * res;
                    worst = Math.Max(worst, Math.Abs(res));   // rows are already relative to dS
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\tS{1}\t{2}\t{3}\t{4:E2}\t{5:E2}",
                    label, m + 1, n,
                    string.Join(",", Array.ConvertAll(c, v => v.ToString("F6", CultureInfo.InvariantCulture))),
                    yy < 1e-300 ? 0.0 : Math.Sqrt(rr / yy), worst));
            }
        }

        sb.AppendLine();
        sb.Append(detail);
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-qbar-missing.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>Paper XII's Principle of Duality, gated on spheres.</b> The scheme run on the
    /// interchanged ray data (6.9) with the refractive indices negated should give every
    /// coefficient's dual with its sign reversed: <c>k_(mu nu)p# = kbar_(n-nu,n-mu)q</c> (6.2).
    /// For the primaries (a, b, c) = (00, 10, 11) and the secondaries s1..s6 = (00, 10, 11, 20,
    /// 21, 22), so the dual partners run 6, 5, 3, 4, 2, 1 - the dual run's entry s_1p is
    /// sbar_6q, which is the one coefficient the identities cannot supply.
    ///
    /// <para>On a spherical system the recursion's sbar_6q is exact, so this is the gate the
    /// figured case is built on. Printed as the worst relative residual of <c>orig + dual</c>
    /// per comparison, with the ratio at the first surface that has anything accumulated.</para>
    /// </summary>
    [Fact]
    public void DualityOnSpheres()
    {
        int[] sp = { 69, 71, 73, 75, 77, 79 };
        int[] sbp = { 70, 72, 74, 76, 78, 80 };
        int[] sq = { 86, 89, 92, 94, 97, 98 };
        int[] member = { 102, 104, 107, 109, 112, 114 };
        int[] partner = { 5, 4, 2, 3, 1, 0 };

        // (label, dual entry, original entry) for the primaries.
        var primaries = new (string, int, int)[]
        {
            ("A_p~-Cbar_q", 15, 24), ("Abar_p~-C_q", 16, 23), ("Bbar_p~-B_q", 17, 22),
            ("C_p~-Abar_q", 18, 21), ("Cbar_p~-A_q", 19, 20),
        };

        var sb = new StringBuilder();
        sb.AppendLine("design\tcomparison\tworst_rel\tratio_orig/dual_first");

        foreach (string name in Designs)
        {
            var (orig, dual, count) = DualPair(name);

            void Compare(string label, Func<BuchdahlTableIRow, double> o, Func<BuchdahlTableIRow, double> dd)
            {
                double worst = 0.0;
                double ratio = double.NaN;
                for (int i = 1; i < count - 1; i++)
                {
                    if (orig[i].FlatInCollimatedSpace || dual[i].FlatInCollimatedSpace
                        || Math.Abs(orig[i].T[6]) > 1e6 || Math.Abs(dual[i].T[6]) > 1e6) continue;
                    double a = o(orig[i]), b = dd(dual[i]);
                    double scale = Math.Max(Math.Abs(a), Math.Abs(b));
                    if (scale < 1e-12) continue;
                    worst = Math.Max(worst, Math.Abs(a + b) / scale);
                    if (double.IsNaN(ratio) && Math.Abs(b) > 1e-12) ratio = a / b;
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E2}\t{3:F6}", name, label, worst, ratio));
            }

            foreach (var (label, d, o) in primaries)
                Compare(label, r => r.T[o], r => r.T[d]);
            for (int m = 0; m < 6; m++)
            {
                int mm = m, mp = partner[m];
                Compare($"S{mm + 1}p~-Sbar{mp + 1}q",
                    r => r.T[6] * r.T[sbp[mp]] - r.T[member[mp]], r => r.T[sp[mm]]);
                Compare($"Sbar{mm + 1}p~-S{mp + 1}q", r => r.T[sq[mp]], r => r.T[sbp[mm]]);
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-duality-spheres.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>The Principle of Duality on FIGURED systems</b>, which XII Sec. 6(iii) says holds
    /// "whether the surfaces of the optical system are spherical or not".
    ///
    /// <para>The dual run needs dual figured inputs, so the fifth-order code is run on the
    /// interchanged paraxial rays with the indices negated, and its increments are bridged into
    /// the dual scheme exactly as the direct ones are. Gates, in order: the bridge constants must
    /// again be the same on every surface; the dual primaries must be the negated q-side
    /// accumulations; the dual S-bar_p must be the negated q-side closed forms; and the dual S_p
    /// must be the negated barred q accumulations the IDENTITIES recover, five of the six. The
    /// sixth, dual S_1p, is then S-bar_6q, printed against the recursion's value.</para>
    /// </summary>
    [Fact]
    public void DualityOnFigured()
    {
        int[] sp = { 69, 71, 73, 75, 77, 79 };
        int[] sbp = { 70, 72, 74, 76, 78, 80 };
        int[] sq = { 86, 89, 92, 94, 97, 98 };
        int[] partner = { 5, 4, 2, 3, 1, 0 };
        int[] schemeSeven = { 10, 38, 44, 50, 54, 59, 65 };

        var sb = new StringBuilder();
        var sixth = new StringBuilder();
        sixth.AppendLine("design\tsurf\tSbar6q_dual\tSbar6q_recursion\trel_diff");
        sb.AppendLine("design\tcomparison\tworst_rel");

        foreach (string name in Designs)
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = LensFile.Read(Fixtures.Lens(name), catalog);
            var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
            double field = 0.0;
            foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
            var p = ParaxialTrace.Trace(sys, n, field);
            double objectDistance = sys.Surfaces[0].Thickness;
            bool infinite = double.IsInfinity(objectDistance);
            double iota = infinite ? 0.0 : -p.Efl / objectDistance;
            int stop = sys.StopSurfaceIndex;
            var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                                sys.Surfaces[stop].SemiDiameter, iota);
            double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
            int last = sys.LastOpticalSurface();
            int count = sys.Surfaces.Count;
            double n1 = n[0];

            var macro = BuchdahlCoefficients.Compute(sys, p);
            var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
            var inc = AsphericSchemeIncrements.Build(macro, sph, last);
            var orig = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, inc,
                                              iota: iota);

            var negN = new double[p.N.Length];
            for (int k = 0; k < negN.Length; k++) negN[k] = -p.N[k];
            var pd = new ParaxialResult
            {
                Y = p.Ybar, U = p.Ubar, Ybar = p.Y, Ubar = p.U, N = negN,
                Efl = p.Efl, Power = p.Power, Bfl = p.Bfl, Epd = p.Epd,
                EntrancePupilPosition = p.EntrancePupilPosition,
                LagrangeInvariant = p.LagrangeInvariant, InfiniteConjugate = p.InfiniteConjugate,
            };
            var negated = new double[n.Length];
            for (int k = 0; k < n.Length; k++) negated[k] = -n[k];

            var macroD = BuchdahlCoefficients.Compute(sys, pd);
            var sphD = BuchdahlTableI.Compute(sys.Surfaces, negated, p.Efl, scheme.P,
                                              iota: iota, dual: true);
            var incD = AsphericSchemeIncrements.Build(macroD, sphD, last);
            var dual = BuchdahlTableI.Compute(sys.Surfaces, negated, p.Efl, stopParameter, incD,
                                              iota: iota, dual: true);

            // Gate 0: the bridge constants, dual run, must not vary from surface to surface.
            double bridgeSpread = 0.0;
            for (int q = 0; q < 7; q++)
            {
                double r0 = double.NaN;
                for (int i = 1; i <= last && i < count - 1; i++)
                {
                    var t = macroD.Intrinsic[i];
                    double m = q switch
                    {
                        0 => t.B, 1 => t.B5, 2 => t.F2, 3 => t.M2, 4 => t.M3, 5 => t.N3,
                        _ => t.Pi5 + t.C5,
                    };
                    double s = sphD[i].T[schemeSeven[q]];
                    if (Math.Abs(m) < 1e-12 || Math.Abs(s) < 1e-12) continue;
                    double ratio = s / m;
                    if (double.IsNaN(r0)) r0 = ratio;
                    else bridgeSpread = Math.Max(bridgeSpread, Math.Abs(ratio / r0 - 1.0));
                }
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}\tbridge constants spread (dual)\t{1:E2}", name, bridgeSpread));

            var worst = new Dictionary<string, double>();
            void Note(string label, double a, double b)
            {
                double scale = Math.Max(Math.Abs(a), Math.Abs(b));
                if (scale < 1e-12) return;
                double rel = Math.Abs(a + b) / scale;
                worst[label] = Math.Max(worst.TryGetValue(label, out var w) ? w : 0.0, rel);
            }

            for (int i = 2; i < count - 1; i++)
            {
                var o = orig[i];
                var d = dual[i];
                if (o.FlatInCollimatedSpace || d.FlatInCollimatedSpace
                    || Math.Abs(o.T[6]) > 1e6 || Math.Abs(d.T[6]) > 1e6) continue;

                int[] qPrim = { 24, 23, 22, 21, 20 };
                for (int k = 0; k < 5; k++) Note("primaries", o.T[qPrim[k]], d.T[15 + k]);
                for (int m = 0; m < 6; m++) Note("Sbar_p vs S_q", o.T[sq[partner[m]]], d.T[sbp[m]]);

                var id = BuchdahlSecondaryQ.At(orig, i, n1);
                for (int m = 1; m < 6; m++)
                    Note("S_p vs identity Sbar_q", id[partner[m] + 1], d.T[sp[m]]);

                double recursion = o.T[6] * o.T[80] - o.T[114];
                double viaDual = -d.T[69];
                double scale6 = Math.Max(Math.Abs(recursion), Math.Abs(viaDual));
                if (scale6 > 1e-12)
                    sixth.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2:E6}\t{3:E6}\t{4:E2}",
                        name, i, viaDual, recursion, Math.Abs(viaDual - recursion) / scale6));
            }

            foreach (var kv in worst)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E2}", name, kv.Key, kv.Value));
        }

        sb.AppendLine();
        sb.Append(sixth);
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-duality-figured.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>The figured flat in collimated light, approached through its curvature.</b>
    /// <c>Ladder2_FlatFigured</c>'s surface 2 is an exactly flat r^4 asphere with the marginal ray
    /// arriving parallel, so i_p = 0 and q is infinite; Table I stores q as zero there and takes
    /// hand-derived limits at three sites. Bending that surface through R = 1e4 .. 1e10 and
    /// comparing each against Forbes says whether the arrangement converges as the surface
    /// flattens - in which case the exactly-flat branches are what fail - or does not.
    /// </summary>
    [Fact]
    public void FlatFiguredCurvatureLadder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("curvature\tq_surf2\ti_p_surf2\tflat_flag\tas-built worst\tdefault worst\tforbes largest");

        foreach (double c in new[] { 0.0, 1e-10, 1e-9, 1e-8, 1e-7, 1e-6, 1e-5, 1e-4,
                                     3e-4, 1e-3, 3e-3, 1e-2 })
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = LensFile.Read(Fixtures.Lens("Ladder2_FlatFigured"), catalog);
            sys.Surfaces[2].Curvature = c;

            var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
            double field = 0.0;
            foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
            var p = ParaxialTrace.Trace(sys, n, field);
            var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                                sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
            var rows = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
            var forbes = ForbesCoefficients.Invert(sys, n, p, field)?.Tau ?? new double[21];

            double largest = 0.0;
            for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(forbes[k]));

            string Worst(double[] tau)
            {
                double w = 0.0;
                int wk = 0, bad = 0;
                for (int k = 1; k <= 20; k++)
                {
                    if (Math.Abs(forbes[k]) < 1e-9 * largest) continue;
                    double rel = Math.Abs(tau[k] - forbes[k]) / Math.Abs(forbes[k]);
                    if (rel > w) { w = rel; wk = k; }
                    if (rel > 0.01) bad++;
                }
                return string.Format(CultureInfo.InvariantCulture, "{0:E3} (t{1})/{2}", w, wk, bad);
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0:E1}\t{1:E3}\t{2:E3}\t{3}\t{4}\t{5}\t{6:E3}",
                c, rows[2].T[6], rows[2].T[3], rows[2].FlatInCollimatedSpace,
                Worst(NewRoute(sys, BuchdahlAsphericScheme.Options.AsBuilt)),
                Worst(NewRoute(sys, BuchdahlAsphericScheme.Options.Default)),
                largest));
        }

        // The limit taken numerically: the default arrangement at c = +-h and +-2h on surface 2,
        // Richardson-extrapolated to c = 0 as (4 S(h) - S(2h)) / 3 with S the symmetric mean,
        // against Forbes on the exactly flat lens.
        {
            var catalog = CatalogLocator.LoadBundled();
            var flat = LensFile.Read(Fixtures.Lens("Ladder2_FlatFigured"), catalog);
            var n = IndexResolver.Build(flat, catalog, 0.55, new List<string>());
            double field = 0.0;
            foreach (var f in flat.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
            var p = ParaxialTrace.Trace(flat, n, field);
            var forbes = ForbesCoefficients.Invert(flat, n, p, field)?.Tau ?? new double[21];
            double largest = 0.0;
            for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(forbes[k]));

            double[] TauAt(double c)
            {
                var s = LensFile.Read(Fixtures.Lens("Ladder2_FlatFigured"), catalog);
                s.Surfaces[2].Curvature = c;
                return NewRoute(s, BuchdahlAsphericScheme.Options.Default);
            }

            sb.AppendLine();
            sb.AppendLine("h\tRichardson worst vs Forbes at c = 0\tsymmetric mean at h only");
            foreach (double h in new[] { 5e-4, 1e-3, 2e-3, 4e-3 })
            {
                var a = TauAt(h);
                var b = TauAt(-h);
                var a2 = TauAt(2 * h);
                var b2 = TauAt(-2 * h);
                double worstR = 0.0, worstS = 0.0;
                int kR = 0;
                for (int k = 1; k <= 20; k++)
                {
                    if (Math.Abs(forbes[k]) < 1e-9 * largest) continue;
                    double s1 = 0.5 * (a[k] + b[k]);
                    double s2 = 0.5 * (a2[k] + b2[k]);
                    double ext = (4.0 * s1 - s2) / 3.0;
                    double rel = Math.Abs(ext - forbes[k]) / Math.Abs(forbes[k]);
                    if (rel > worstR) { worstR = rel; kR = k; }
                    worstS = Math.Max(worstS, Math.Abs(s1 - forbes[k]) / Math.Abs(forbes[k]));
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:E1}\t{1:E3} (t{2})\t{3:E3}", h, worstR, kR, worstS));
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-flat-collimated.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// The Laurent-series route's numbers: on the figured flat through the shipping path against
    /// Forbes, and on regular surfaces against the double route, with its self-checks.
    /// </summary>
    [Fact]
    public void FlatCollimatedSeriesNumbers()
    {
        var sb = new StringBuilder();
        sb.AppendLine("case\tworst rel\ttau\tunderflows\tdropped\tnegative orders\ttruncation gap\tconverged");

        foreach (var (name, surface, againstForbes) in new[]
                 {
                     ("Ladder2_FlatFigured", 2, true),
                     ("Ladder2_A4_Both", 2, false),
                     ("Ladder3_A4_Middle", 3, false),
                     ("CookeTriplet_SPOTM_START_LO_ASPHERE", 1, false),
                     ("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8", 5, false),
                 })
        {
            var catalog = CatalogLocator.LoadBundled();
            var sys = LensFile.Read(Fixtures.Lens(name), catalog);
            var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
            double field = 0.0;
            foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
            var p = ParaxialTrace.Trace(sys, n, field);
            var b = BuchdahlCoefficients.Compute(sys, p);
            TertiaryCoefficients.Attach(sys, n, p, b, field);
            var shipped = new double[21];
            for (int k = 2; k <= 20; k++)
                shipped[k] = (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;

            var series = TertiaryCoefficients.SeriesTau(sys, n, field, new[] { surface });
            var reference = againstForbes
                ? ForbesCoefficients.Invert(sys, n, p, field)?.Tau ?? new double[21]
                : shipped;
            var compared = againstForbes ? shipped : series.Tau;

            double largest = 0.0;
            for (int k = 2; k <= 20; k++) largest = Math.Max(largest, Math.Abs(reference[k]));
            double worst = 0.0;
            int wk = 0;
            for (int k = 2; k <= 20; k++)
            {
                if (Math.Abs(reference[k]) < 1e-9 * largest) continue;
                double rel = Math.Abs(compared[k] - reference[k]) / Math.Abs(reference[k]);
                if (rel > worst) { worst = rel; wk = k; }
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0} ({1})\t{2:E2}\t{3}\t{4}\t{5:E1}\t{6:E1}\t{7:E1}\t{8}",
                name, againstForbes ? "shipped vs Forbes" : $"series surf {surface} vs double",
                worst, wk, series.Underflows, series.WorstDroppedLeading,
                series.NegativeOrderResidue, series.TruncationDisagreement, series.Converged));
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-flat-series.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// The figured flat's own inputs to the two passes - its primary, secondary and tertiary
    /// halves as the row carries them - on the exactly flat lens against the mean of c = +-h.
    /// Where the flat row holds zero and the mean does not, the flat branch has dropped that input.
    /// </summary>
    [Fact]
    public void FlatFiguredRowInputs()
    {
        const double h = 5e-4;
        var catalog = CatalogLocator.LoadBundled();

        BuchdahlTableIRow Row(double c)
        {
            var sys = LensFile.Read(Fixtures.Lens("Ladder2_FlatFigured"), catalog);
            sys.Surfaces[2].Curvature = c;
            var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
            double field = 0.0;
            foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
            var p = ParaxialTrace.Trace(sys, n, field);
            var macro = BuchdahlCoefficients.Compute(sys, p);
            var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                                sys.Surfaces[sys.StopSurfaceIndex].SemiDiameter);
            var sph = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P);
            var inc = AsphericSchemeIncrements.Build(macro, sph, sys.LastOpticalSurface());
            return BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, inc)[2];
        }

        var flat = Row(0.0);
        var up = Row(h);
        var down = Row(-h);
        var up2 = Row(2 * h);
        var down2 = Row(-2 * h);
        var up4 = Row(4 * h);
        var down4 = Row(-4 * h);

        // Richardson limits at step h and at step 2h: where the two agree the quantity has a
        // limit at c = 0 and it is that; where they do not it diverges as the surface flattens
        // and only what it feeds can be compared.
        var sb = new StringBuilder();
        sb.AppendLine("quantity\tflat\tlimit(h)\tlimit(2h)\tflat vs limit(h)");
        void Put(string label, Func<BuchdahlTableIRow, double> get)
        {
            double s1 = 0.5 * (get(up) + get(down));
            double s2 = 0.5 * (get(up2) + get(down2));
            double s4 = 0.5 * (get(up4) + get(down4));
            double limH = (4.0 * s1 - s2) / 3.0;
            double lim2H = (4.0 * s2 - s4) / 3.0;
            double f = get(flat);
            double scale = Math.Max(Math.Abs(f), Math.Abs(limH));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}\t{1:E4}\t{2:E4}\t{3:E4}\t{4}",
                label, f, limH, lim2H,
                scale < 1e-12 ? "-" : (Math.Abs(f - limH) / scale).ToString("E1", CultureInfo.InvariantCulture)));
        }

        Put("q (t6)", r => r.T[6]);
        Put("i_p (t3)", r => r.T[3]);
        Put("rho", r => r.Rho);
        Put("ApSpherical", r => r.ApSpherical);
        Put("ApFigured", r => r.ApFigured);
        Put("C13Spherical", r => r.C13Spherical);
        Put("C13Figured", r => r.C13Figured);
        for (int m = 1; m <= 6; m++) { int mm = m; Put($"SecSph[{mm}]", r => r.SecSph[mm]); }
        for (int m = 1; m <= 6; m++) { int mm = m; Put($"SecFig[{mm}]", r => r.SecFig[mm]); }
        for (int m = 0; m < 6; m++) { int mm = m; Put($"SecDFigured[{mm}]", r => r.SecDFigured[mm]); }
        for (int m = 0; m < 6; m++) { int mm = m; Put($"SecBarFig[{mm}]", r => r.SecBarFig[mm]); }
        // The barred D term (q - rho) D: D is odd in c at a collimated flat and q goes as 1/c, so
        // this has a finite limit the flat row, carrying q = 0, cannot form.
        for (int m = 0; m < 6; m++)
        {
            int mm = m;
            Put($"(q-rho)D[{mm}]", r => (r.T[6] - r.Rho) * r.SecDFigured[mm]);
        }
        for (int m = 0; m < 6; m++)
        {
            int mm = m;
            Put($"SecBarFig-(q-rho)D[{mm}]",
                r => r.SecBarFig[mm] - (r.T[6] - r.Rho) * r.SecDFigured[mm]);
        }
        for (int m = 1; m <= 5; m++) { int mm = m; Put($"MSph[{mm}]", r => r.MSph[mm]); }
        for (int m = 1; m <= 5; m++) { int mm = m; Put($"MFig[{mm}]", r => r.MFig[mm]); }
        for (int m = 1; m <= 10; m++) { int mm = m; Put($"ZHat[{mm}]", r => r.ZHat[mm]); }
        for (int m = 1; m <= 10; m++) { int mm = m; Put($"ZCheck[{mm}]", r => r.ZCheck[mm]); }
        Put("QT152", r => r.QT152);
        for (int m = 1; m <= 10; m++) { int mm = m; Put($"TertiaryTotal[{mm}]", r => r.TertiaryTotal[mm]); }
        for (int m = 1; m <= 10; m++) { int mm = m; Put($"TertiaryTotalBar[{mm}]", r => r.TertiaryTotalBar[mm]); }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-flat-row.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>Which surface and which pass the exactly flat figured surface gets wrong.</b> Each
    /// surface's hat, hat-barred, check and check-barred totals on the flat lens, against the
    /// limit of the same quantity from c = +-h, +-2h on surface 2 (Richardson, as in
    /// <see cref="FlatFiguredCurvatureLadder"/>, good to about 1E-4 there). The sums hat+check are
    /// printed too, since the halves can be large and cancel as the surface flattens.
    /// </summary>
    [Fact]
    public void FlatFiguredPerSurface()
    {
        const double h = 5e-4;
        var catalog = CatalogLocator.LoadBundled();

        Dictionary<int, double[][]> Capture(double c)
        {
            var s = LensFile.Read(Fixtures.Lens("Ladder2_FlatFigured"), catalog);
            s.Surfaces[2].Curvature = c;
            var got = new Dictionary<int, double[][]>();
            NewRoute(s, BuchdahlAsphericScheme.Options.Default,
                     (i, a, b, cc, d) =>
                     {
                         var sum = new double[11];
                         var sumBar = new double[11];
                         for (int k = 1; k <= 10; k++) { sum[k] = a[k] + cc[k]; sumBar[k] = b[k] + d[k]; }
                         got[i] = new[] { a, b, cc, d, sum, sumBar };
                     });
            return got;
        }

        var flat = Capture(0.0);
        var p1 = Capture(h);
        var m1 = Capture(-h);
        var p2 = Capture(2 * h);
        var m2 = Capture(-2 * h);

        string[] part = { "hat", "hatBar", "check", "checkBar", "T sum", "Tbar sum" };
        var sb = new StringBuilder();
        sb.AppendLine("surf\tpart\tk\tflat\tlimit\trel\t(mean at h, for scale)");
        foreach (int i in flat.Keys)
            for (int q = 0; q < 6; q++)
                for (int k = 1; k <= 10; k++)
                {
                    double f = flat[i][q][k];
                    double s1 = 0.5 * (p1[i][q][k] + m1[i][q][k]);
                    double s2 = 0.5 * (p2[i][q][k] + m2[i][q][k]);
                    double lim = (4.0 * s1 - s2) / 3.0;
                    double scale = Math.Max(Math.Abs(f), Math.Abs(lim));
                    if (scale < 1e-14) continue;
                    double rel = Math.Abs(f - lim) / scale;
                    if (rel < 1e-3) continue;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3:E4}\t{4:E4}\t{5:E2}\t{6:E4}", i, part[q], k, f, lim, rel, s1));
                }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-flat-per-surface.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// The spherical scheme on a design as it stands, and again on the interchanged ray data with
    /// the refractive indices negated, per XII Sec. 6(iii).
    /// </summary>
    private static (BuchdahlTableIRow[] Orig, BuchdahlTableIRow[] Dual, int Count) DualPair(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;
        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;

        var negated = new double[n.Length];
        for (int k = 0; k < n.Length; k++) negated[k] = -n[k];

        var orig = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, iota: iota);
        var dual = BuchdahlTableI.Compute(sys.Surfaces, negated, p.Efl, stopParameter,
                                          iota: iota, dual: true);
        return (orig, dual, sys.Surfaces.Count);
    }

    private sealed record QSplit(double[] S, double[] Lin, double[] QuadF, double[] Cross);

    /// <summary>
    /// The scheme's six q-side closed forms t86, t89, t92, t94, t97, t98, transcribed so that
    /// they can be evaluated on parts of the accumulations. <paramref name="a"/> is t15..t24 from
    /// zero, <paramref name="s"/> the barred p sums t70..t80.
    /// </summary>
    private static double[] QClosed(double t9, double t81, double t82, double[] a, double[] s)
    {
        double t15 = a[0], t16 = a[1], t17 = a[2], t18 = a[3], t19 = a[4];
        double t20 = a[5], t21 = a[6], t22 = a[7], t23 = a[8], t24 = a[9];
        double t83 = t16 - t20, t84 = t17 - t22, t85 = t19 - t23;

        double q86 = -1.5 * t83 * t83 + t9 * t16 - t16 * t20 + t15 * t21 - t15 * t81 + s[0];
        double t87 = (2.0 * t21 - t22 - t81) * t16 + t9 * t21 - t20 * t81;
        double t88 = (2.0 * t23 - t82) * t15 - t17 * t20 + t9 * t17;
        double q89 = -3.0 * t83 * t84 + t87 + t88 + s[1];
        double t90 = -0.5 * t81 * t84 + t9 * t19 - t20 * t82;
        double t91 = t18 * t21 + t15 * t24 - t16 * t23 - t19 * t20;
        double q92 = -3.0 * t83 * t85 + s[2] + t90 + t91;
        double t93 = 2.0 * (2.0 * t16 * t23 - t16 * t82 + t9 * t23) - t17 * t22;
        double q94 = -1.5 * t84 * t84 + t81 * t84 + s[3] + t93;
        double t95 = (2.0 * t18 - t17 + t81) * t23 + t19 * t81 - t22 * t82;
        double t96 = (2.0 * t16 + t9) * t24 - t19 * t22 - t18 * t82;
        double q97 = -3.0 * t84 * t85 + t95 + t96 + s[4];
        double q98 = -1.5 * t85 * t85 + t24 * t81 + t18 * t24 - t23 * t82 - t19 * t23 + s[5];
        return new[] { q86, q89, q92, q94, q97, q98 };
    }

    /// <summary>
    /// The figured half of the q-side closed forms at one surface, in its parts: the linear
    /// barred p sum, the paraxial x figured-primary terms, figured x figured, and the spherical x
    /// figured cross terms. The spherical half is the twin's accumulations.
    /// </summary>
    private static QSplit QParts(double[] row, double[] twin, int[] pIdx)
    {
        var aS = new double[10];
        var aF = new double[10];
        var aFull = new double[10];
        for (int k = 0; k < 10; k++)
        {
            aS[k] = twin[15 + k];
            aFull[k] = row[15 + k];
            aF[k] = aFull[k] - aS[k];
        }
        var sS = new double[6];
        var sF = new double[6];
        var sFull = new double[6];
        for (int k = 0; k < 6; k++)
        {
            sS[k] = twin[pIdx[k]];
            sFull[k] = row[pIdx[k]];
            sF[k] = sFull[k] - sS[k];
        }

        double t9 = row[9], t81 = row[81], t82 = row[82];
        var zero = new double[6];
        var full = QClosed(t9, t81, t82, aFull, sFull);
        var sph = QClosed(twin[9], twin[81], twin[82], aS, sS);
        var linQuad = QClosed(t9, t81, t82, aF, zero);
        var quad = QClosed(0.0, 0.0, 0.0, aF, zero);

        var lin = new double[6];
        var cross = new double[6];
        for (int k = 0; k < 6; k++)
        {
            lin[k] = linQuad[k] - quad[k];
            cross[k] = full[k] - sph[k] - sF[k] - linQuad[k];
        }
        return new QSplit(sF, lin, quad, cross);
    }

    /// <summary>Gaussian elimination with partial pivoting; null when the system is singular.</summary>
    private static double[]? Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var v = (double[])b.Clone();
        double scale = 0.0;
        for (int i = 0; i < n; i++) scale = Math.Max(scale, Math.Abs(m[i, i]));
        for (int col = 0; col < n; col++)
        {
            int piv = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(m[row, col]) > Math.Abs(m[piv, col])) piv = row;
            if (Math.Abs(m[piv, col]) <= 1e-12 * Math.Max(scale, 1e-300)) return null;
            if (piv != col)
            {
                for (int k = 0; k < n; k++) (m[col, k], m[piv, k]) = (m[piv, k], m[col, k]);
                (v[col], v[piv]) = (v[piv], v[col]);
            }
            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double f = m[row, col] / m[col, col];
                for (int k = col; k < n; k++) m[row, k] -= f * m[col, k];
                v[row] -= f * v[col];
            }
        }
        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = v[i] / m[i, i];
        return x;
    }

    private static BuchdahlTableIRow[] RowsFor(string name, out int count,
                                               out BuchdahlTableIRow[] twin, out double n1)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, p);
        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;
        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());
        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
        count = sys.Surfaces.Count;
        n1 = n[0];
        twin = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, iota: iota);
        return BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                      iota: iota);
    }

    private static BuchdahlTableIRow[] RowsFor(string name, out int count)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, p);
        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;
        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());
        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
        count = sys.Surfaces.Count;
        return BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                      iota: iota);
    }

    /// <summary>The new routine's tau, in the transverse convention the oracles use.</summary>
    private static double[] NewRoute(string name, BuchdahlAsphericScheme.Options? options)
        => NewRoute(LensFile.Read(Fixtures.Lens(name), CatalogLocator.LoadBundled()), options);

    /// <summary>The same, on a system already in hand - so a fixture can be modified first.</summary>
    private static double[] NewRoute(Core.Models.OpticalSystem sys,
                                     BuchdahlAsphericScheme.Options? options,
                                     Action<int, double[], double[], double[], double[]>? perSurface = null)
    {
        var catalog = CatalogLocator.LoadBundled();
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, p);

        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;

        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());

        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
        double g = 1.0 - stopParameter * iota;
        double lengthFactor = p.Efl / (p.N[sys.LastOpticalSurface()] * scheme.PRayFinalAngle);
        double u = -(0.5 * p.Epd / p.Efl) / g;
        double hmax = infinite
            ? Math.Tan(field * Math.PI / 180.0)
            : -(p.ParaxialImageHeight / p.Magnification) / objectDistance;

        // The dual figured increments, XII Sec. 6(iii): the fifth-order code on the interchanged
        // paraxial rays with the indices negated, bridged into the dual scheme as the direct ones.
        // In this survey a null reading means the arrangement as built, not the routine's default.
        options ??= BuchdahlAsphericScheme.Options.AsBuilt;
        var dualIncrements = options.SixthBarredMemberFromDuality
            ? AsphericSchemeIncrements.BuildDual(sys, p, n, scheme.P, iota)
            : null;

        var raw = BuchdahlAsphericScheme.Tau(sys.Surfaces, n, p.Efl, stopParameter,
                                             increments, iota, options, dualIncrements, perSurface);
        return TertiaryCoefficients.ToTransverse(raw, lengthFactor, u, hmax,
                                                 coefficients.Totals.B7);
    }

    private sealed record Loaded(double[] Scheme, double[] Forbes, double[] Rays,
                                 double Largest, double Residual);

    private static Loaded Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var t = b.Totals;

        var scheme = new double[21];
        for (int k = 1; k <= 20; k++)
            scheme[k] = k == 1 ? t.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        var inv = CoefficientInversion.Invert(sys, n, p, field);
        var forbes = ForbesCoefficients.Invert(sys, n, p, field);

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(scheme[k]));

        return new Loaded(scheme, forbes?.Tau ?? new double[21], inv?.Tau ?? new double[21],
                          big, inv?.Residual ?? double.NaN);
    }

    /// <summary>
    /// The same lens with every figuring removed, by Forbes - so that "the figuring does real
    /// work on this rung" is a measurement rather than an assumption. Without it, a rung that
    /// agrees might only be one where the figuring does nothing.
    /// </summary>
    private static double[] LoadStripped(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        foreach (var s in sys.Surfaces)
        {
            s.Conic = 0.0;
            for (int k = 0; k < s.AsphericCoefficients.Length; k++)
                s.AsphericCoefficients[k] = 0.0;
        }
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        return ForbesCoefficients.Invert(sys, n, p, field)?.Tau ?? new double[21];
    }

    private static string Row(string name)
    {
        var d = Load(name);
        var bare = LoadStripped(name);
        double work = 0.0;
        for (int k = 1; k <= 20; k++)
            work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

        double wr = 0, wf = 0, fr = 0;
        int kr = 0, kf = 0;
        for (int k = 1; k <= 20; k++)
        {
            double a = Math.Abs(d.Scheme[k] - d.Rays[k]) / d.Largest;
            double b = Math.Abs(d.Scheme[k] - d.Forbes[k]) / d.Largest;
            double c = Math.Abs(d.Forbes[k] - d.Rays[k]) / d.Largest;
            if (a > wr) { wr = a; kr = k; }
            if (b > wf) { wf = b; kf = k; }
            if (c > fr) fr = c;
        }
        return string.Format(CultureInfo.InvariantCulture,
            "{0}\t{1:F3}\t{2}\t{3:F3}\t{4}\t{5:F3}\t{6:E1}\t{7:F3}",
            name, 100 * wr, kr, 100 * wf, kf, 100 * fr, d.Residual, 100 * work);
    }

    private static List<string> Detail(string name)
    {
        var d = Load(name);
        var lines = new List<string>();
        for (int k = 1; k <= 20; k++)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "  tau{0}\t{1:E6}\t{2:E6}\t{3:E6}\t{4:F3}\t{5:F3}",
                k, d.Scheme[k], d.Forbes[k], d.Rays[k],
                100 * Math.Abs(d.Scheme[k] - d.Forbes[k]) / d.Largest,
                100 * Math.Abs(d.Forbes[k] - d.Rays[k]) / d.Largest));
        return lines;
    }
}
