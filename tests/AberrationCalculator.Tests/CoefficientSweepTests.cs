using System;
using System.Collections.Generic;
using System.Linq;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The seventh-order coefficients against a route of DIFFERENT LINEAGE, on every design on disk.
///
/// <para><b>Why lineage is the word that matters.</b> Buchdahl's computing scheme and Forbes'
/// series trace share no arithmetic: one accumulates a table of per-surface quantities through an
/// arranged recursion, the other propagates a ray through sag polynomials and reads coefficients
/// off the resulting power series in the invariants. Agreement between them is evidence about the
/// OPTICS. Agreement between two implementations of the same method is evidence about
/// transcription, and this repository has twice watched that kind of agreement hold while both
/// sides were wrong - the two macros against the C# on an immersed object space, and the two
/// copies of the flat-collimated orchestration against each other on a finite conjugate.</para>
///
/// <para><b>Why a sweep.</b> <see cref="ForbesCoefficientsTests"/> makes this comparison on a
/// fixed list of named designs, all of them at an infinite conjugate. That is how a figured design at a
/// FINITE conjugate went 5E-5 wrong without anything noticing: the pairing that could have failed
/// was never formed. This runs the comparison on whatever is on disk, so a design added for any
/// other reason is cross-checked the day it lands.</para>
///
/// <para>Where the conjugate is infinite, REAL TRACED RAYS are a third lineage and are compared
/// too - <see cref="CoefficientInversion"/> recovers the same twenty coefficients from the
/// landings of actual rays, which knows nothing of either scheme.</para>
/// </summary>
public class CoefficientSweepTests
{
    /// <summary>Every design on disk - the same discovery the derivative sweep uses.</summary>
    public static IEnumerable<object[]> EveryDesign() => Designs.All();

    /// <summary>
    /// How closely the two routes must agree, relative to the largest of the twenty.
    ///
    /// <para>A thousandth of a part per million is the level the scheme and the series trace meet
    /// at on an ordinary design - the standing result is 2E-13 - and 1E-9 leaves room for the
    /// figured arrangement, which is reconstructed rather than published and is measured at 2E-10.
    /// The near-singular designs get the allowance their own values earn; see
    /// <see cref="Designs.IsNearSingular"/>.</para>
    /// </summary>
    private static double ToleranceFor(string name) => Designs.IsNearSingular(name) ? 5e-3 : 1e-9;

    /// <summary>
    /// Buchdahl's tertiary set against Forbes' series trace, and against real rays where the
    /// conjugate allows it.
    ///
    /// <para>A design either route declines is reported rather than skipped, and
    /// <see cref="EveryDesignWasActuallyCrossChecked"/> fails if too few are actually compared.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDesign))]
    public void TheTertiarySetAgreesWithARouteOfDifferentLineage(string name, string folder)
    {
        string? reason = CheckOne(name, folder);
        Assert.True(reason == null || reason.Length > 0,
            $"{name}: declined without saying why, which is the one outcome that is not allowed");
    }

    /// <summary>Null when the design was compared; a stated reason when it could not be.</summary>
    private static string? CheckOne(string name, string folder)
    {
        var catalog = CatalogLocator.LoadBundled();
        OpticalSystem system;
        try { system = LensFile.Read(Designs.PathOf(name, folder), catalog); }
        catch (Exception e) { return "the file could not be read: " + e.Message; }

        if (system.LastOpticalSurface() < 1) return "no optical surfaces";
        if (system.Wavelengths.Count == 0) return "no wavelengths";

        double field = 0.0;
        foreach (var f in system.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        if (Math.Abs(field) < 1e-12) return "no off-axis field, so the field terms are all zero";

        int primary = system.PrimaryWavelengthIndex < 0 ? 0 : system.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(system, catalog, system.Wavelengths[primary].Value,
                                    new List<string>());

        var p = ParaxialTrace.Trace(system, n, field);
        var b = BuchdahlCoefficients.Compute(system, p);
        TertiaryCoefficients.Attach(system, n, p, b, field);

        var table = new double[21];
        double largest = 0.0;
        for (int k = 1; k <= 20; k++)
        {
            table[k] = k == 1 ? b.Totals.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;
            largest = Math.Max(largest, Math.Abs(table[k]));
        }
        if (largest <= 0.0) return "the scheme produced no tertiary coefficients";

        double tolerance = ToleranceFor(name);

        // Forbes' series trace where it applies - it declines a mirror - and real rays where they
        // can be inverted. A design must be held to at least one of the two.
        bool compared = false;
        var forbes = ForbesCoefficients.Invert(system, n, p, field);
        if (forbes != null)
        {
            Compare(name, "Forbes' series trace", table, forbes.Tau, largest, tolerance);
            compared = true;
        }

        // Real rays, where they can be inverted at all. A third lineage, and the only one that
        // knows nothing about either series.
        if (p.InfiniteConjugate)
        {
            CoefficientInversion.Result? rays = null;
            try { rays = CoefficientInversion.Invert(system, n, p, field); }
            catch (NotSupportedException) { }

            // THE FIT SAYS HOW WELL IT FIT, and that is the bar it is held to. A least squares
            // over traced landings carries its own noise, which depends on the design: measured
            // across these fixtures the disagreement runs between two and forty times the fit's
            // own reported residual, so a hundred times it is a bar every sound design clears
            // while a coefficient that is actually WRONG - which would be out by per cent, not
            // by parts in ten thousand - still fails.
            //
            // A fit that did not close is not a reference at all. The parabolic mirror used to
            // leave a residual of 0.75 here - because RealRayTrace did not reflect, so the rays
            // were not the mirror's. Now they are, it closes, and they are the mirror's only
            // second lineage, since Forbes declines it.
            if (rays != null && !double.IsNaN(rays.Residual) && rays.Residual < 1e-2)
            {
                Compare(name, "real traced rays", table, rays.Tau, largest,
                        Math.Max(5e-4, 100.0 * rays.Residual));
                compared = true;
            }
        }

        return compared ? null : "neither Forbes' series trace nor real rays could be compared";
    }

    private static void Compare(string name, string other, double[] table, double[] reference,
                                double largest, double tolerance)
    {
        double worst = 0.0;
        int at = 0;
        for (int k = 1; k <= 20; k++)
        {
            double d = Math.Abs(table[k] - reference[k]) / largest;
            if (d > worst) { worst = d; at = k; }
        }

        Assert.True(worst < tolerance,
            $"{name}: the scheme's tau{at} is {table[at]:E10} and {other} gives "
          + $"{reference[at]:E10}, a relative {worst:E3} of the largest coefficient against a "
          + $"tolerance of {tolerance:E1}. The two share no arithmetic, so this is a statement "
          + "about the optics rather than about a transcription.");
    }

    /// <summary>
    /// The guard against a hollow sweep: EVERY design must actually be compared.
    ///
    /// <para>It demands all of them because all of them can be: every design is held to at least
    /// one lineage. Forbes declines the mirrors, and reflected real rays stand in for it there.
    /// If a design is added that neither route can take - an on-axis one has no
    /// field variable to fit against - this fails and prints the reason, which is the moment to
    /// decide whether that design should be excluded or the route extended. A theory that quietly
    /// skipped it would look exactly like a theory that passed.</para>
    /// </summary>
    [Fact]
    public void EveryDesignWasActuallyCrossChecked()
    {
        int compared = 0;
        var declined = new List<string>();

        foreach (var row in EveryDesign())
        {
            string name = (string)row[0], folder = (string)row[1];
            string? reason;
            try { reason = CheckOne(name, folder); }
            catch (Exception e) { reason = "THREW: " + e.Message; }

            if (reason == null) compared++;
            else declined.Add($"    {name}: {reason}");
        }

        int total = EveryDesign().Count();
        Assert.True(compared >= total,
            $"only {compared} of {total} designs were cross-checked against a second lineage. "
          + "The rest:\n" + string.Join("\n", declined));
    }
}
