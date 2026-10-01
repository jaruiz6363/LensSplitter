using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The aspheric tertiary routine, and the gate it is built against.
///
/// <para><see cref="BuchdahlAsphericScheme"/> exists so that M Sec. 85 - the aspherical form of
/// the condensed iteration, which Buchdahl never published an arranged table for - can be worked
/// on without putting the spherical seventh order at risk. The spherical half is established
/// against Buchdahl's own printed numbers, an independent implementation and Forbes' series
/// trace; it is not in question and it is not touched.</para>
///
/// <para>So the first thing asserted here is not that the new routine is better. It is that
/// with the arrangement as the working scheme has it, the new routine gives the working
/// scheme's answer <b>exactly</b> - the same quantities, arranged the same way, to the last
/// bit. Anything the routine later moves can then be attributed to the one reading that
/// moved it, rather than to the rewrite.</para>
/// </summary>
public class BuchdahlAsphericSchemeTests
{
    private sealed record Case(IReadOnlyList<Surface> Surfaces, double[] Indices, double Efl,
                               double StopParameter, IReadOnlyList<double[]>? Increments,
                               double Iota);

    /// <summary>
    /// The scheme's inputs for one fixture, assembled exactly as
    /// <see cref="TertiaryCoefficients.Attach"/> assembles them - including the aspheric
    /// increments, which are what makes a figured fixture figured as far as the scheme is
    /// concerned.
    /// </summary>
    private static Case Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var paraxial = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, paraxial);

        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -paraxial.Efl / objectDistance;

        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, paraxial.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);

        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, paraxial.Efl, scheme.P,
                                               iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());

        double stopParameter = infinite
            ? scheme.P
            : paraxial.EntrancePupilPosition / paraxial.Efl;

        return new Case(sys.Surfaces, n, paraxial.Efl, stopParameter, increments, iota);
    }

    private static double[] Working(Case c) =>
        TertiaryCoefficients.Compute(c.Surfaces, c.Indices, c.Efl, c.StopParameter,
                                     c.Increments, c.Iota);

    private static double[] New(Case c, BuchdahlAsphericScheme.Options? o = null) =>
        BuchdahlAsphericScheme.Tau(c.Surfaces, c.Indices, c.Efl, c.StopParameter,
                                   c.Increments, c.Iota, o ?? BuchdahlAsphericScheme.Options.AsBuilt);

    /// <summary>
    /// <b>The aspheric routine as the program uses it.</b> Through <see cref="TertiaryCoefficients.Attach"/>,
    /// which sends a figured system to <see cref="BuchdahlAsphericScheme"/> with its default
    /// arrangement, all twenty coefficients must agree with Forbes' series trace - an independent
    /// expansion of the same ray function - essentially to rounding. The arrangement as built was
    /// out by 17 to 467 per cent on these designs.
    /// </summary>
    /// <para>The R = 1e10 near-flat twin is held to 1E-7 rather than 1E-8: a curvature that small
    /// costs digits to cancellation in both routes, and the spherical near-limit flat shows the
    /// same 2E-8 between Table I and Forbes (<c>docs/verification.md</c>).</para>
    [Theory]
    [InlineData("Ladder2_A4_First", 1e-8)]
    [InlineData("Ladder2_A4_Second", 1e-8)]
    [InlineData("Ladder2_A4_Both", 1e-8)]
    [InlineData("Ladder2_FiguredSphere_Then_A4", 1e-8)]
    [InlineData("Ladder3_A4_First", 1e-8)]
    [InlineData("Ladder3_A4_Middle", 1e-8)]
    [InlineData("Ladder2_FiguredFlatRear", 1e-8)]
    [InlineData("Ladder2_FiguredNearFlatRear", 1e-7)]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE", 1e-8)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE", 1e-8)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8", 1e-8)]
    public void TheAsphericRoutineAgreesWithForbes(string fixtureName, double tolerance)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var paraxial = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, paraxial);
        TertiaryCoefficients.Attach(sys, n, paraxial, b, field);
        var forbes = Core.Forbes.ForbesCoefficients.Invert(sys, n, paraxial, field);
        Assert.NotNull(forbes);

        var mine = new double[21];
        for (int k = 1; k <= 20; k++)
            mine[k] = k == 1 ? b.Totals.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(b.Totals)!;

        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(forbes!.Tau[k]));

        for (int k = 1; k <= 20; k++)
        {
            double reference = forbes!.Tau[k];
            if (Math.Abs(reference) < 1e-9 * largest) continue;
            double rel = Math.Abs(mine[k] - reference) / Math.Abs(reference);
            Assert.True(rel < tolerance,
                $"tau{k}: the aspheric routine gives {mine[k]:E12}, Forbes {reference:E12}, "
              + $"relative {rel:E2}.");
        }
    }

    /// <summary>
    /// On a system of SPHERES the default aspheric arrangement must reproduce the spherical
    /// routine. Not bit for bit - members one to five of the barred q accumulation come from the
    /// identities rather than the recursion, and the sixth from the dual run - but to rounding.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_Sphere")]
    [InlineData("Ladder3_Sphere")]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void TheDefaultArrangementReproducesTheSphericalRoutineOnSpheres(string fixtureName)
    {
        var c = Load(fixtureName);
        var working = Working(c);
        var mine = BuchdahlAsphericScheme.Tau(c.Surfaces, c.Indices, c.Efl, c.StopParameter,
                                              c.Increments, c.Iota,
                                              BuchdahlAsphericScheme.Options.Default);
        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(working[k]));
        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(working[k] - mine[k]) <= 1e-10 * largest,
                $"tau{k}: spherical routine {working[k]:E17}, default aspheric {mine[k]:E17}.");
    }

    /// <summary>
    /// <b>The gate.</b> With the arrangement as built, the new routine reproduces the working
    /// scheme to the last bit, on spheres and on figured designs alike. Not "to a tolerance":
    /// it is the same arithmetic in the same order, and anything less than exact equality means
    /// the transcription moved something.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("Ladder2_Sphere_FlatRear")]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    [InlineData("Ladder1_A4")]
    [InlineData("Ladder1_Conic")]
    [InlineData("Ladder2_A4_First")]
    [InlineData("Ladder2_A4_Second")]
    [InlineData("Ladder2_FiguredSphere_Both")]
    [InlineData("Ladder2_FlatFigured")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    public void AsBuiltItReproducesTheWorkingSchemeExactly(string fixtureName)
    {
        var c = Load(fixtureName);
        var working = Working(c);
        var mine = New(c);

        for (int k = 1; k <= 20; k++)
            Assert.True(working[k].Equals(mine[k]),
                $"tau{k}: the working scheme gives {working[k]:E17}, the aspheric routine "
              + $"{mine[k]:E17}. As built the two are the same arrangement over the same "
              + "quantities and must agree bit for bit.");
    }

    /// <summary>
    /// On a system of SPHERES the check half is identically zero - (60.3) has no L term for a
    /// sphere - so every reading of Sec. 85 must give the same answer. A reading that moves a
    /// spherical result has split something that does not split.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("CookeTriplet")]
    [InlineData("KingslakeDG")]
    public void NoReadingOfSection85MovesASphericalSystem(string fixtureName)
    {
        var c = Load(fixtureName);
        var asBuilt = New(c);

        foreach (var option in Readings())
        {
            var moved = New(c, option);
            for (int k = 1; k <= 20; k++)
                Assert.True(asBuilt[k].Equals(moved[k]),
                    $"tau{k} moved from {asBuilt[k]:E17} to {moved[k]:E17} on a system of "
                  + $"spheres under {option}. A sphere has no check half for a reading to "
                  + "change.");
        }
    }

    /// <summary>Every reading the routine offers, so the sphere gate covers all of them.</summary>
    public static IEnumerable<BuchdahlAsphericScheme.Options> Readings()
    {
        yield return new BuchdahlAsphericScheme.Options { IntrinsicChainOnPassRatio = true };
        yield return new BuchdahlAsphericScheme.Options
            { AccumulatedFiguringOnHeightRatio = true };
    }

    /// <summary>
    /// A reading must not move a design with ONE powered surface, whatever its figuring.
    ///
    /// <para>There is nothing ahead of a single powered surface to accumulate, so every induced
    /// term vanishes identically and what is left is the intrinsic aspheric tertiary - which the
    /// ladder shows is already right, at the ray oracle's own noise floor. A reading of Sec. 85
    /// that moves these rungs has split something in the intrinsic chain, which is not where the
    /// defect is, and it is refuted before its effect on the broken rungs is even looked at.
    /// This is what disposed of the first reading tried: running the intrinsic chain on the
    /// pass's own ratio took <c>Ladder1_A4</c> from 0.03 per cent to 115.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder1_A4")]
    [InlineData("Ladder1_Conic")]
    [InlineData("Ladder1_FiguredSphere")]
    public void NoReadingMovesASingleSurfaceWhereThereAreNoInducedTerms(string fixtureName)
    {
        var c = Load(fixtureName);
        var asBuilt = New(c);

        foreach (var option in Readings())
        {
            if (option.IntrinsicChainOnPassRatio) continue;   // refuted, kept for the record

            var moved = New(c, option);
            for (int k = 1; k <= 20; k++)
                Assert.True(Math.Abs(asBuilt[k] - moved[k])
                            <= 1e-12 * Math.Abs(asBuilt[k]) + 1e-30,
                    $"tau{k} moved from {asBuilt[k]:E6} to {moved[k]:E6} on a single powered "
                  + $"surface under {option}. There is nothing accumulated ahead of it for a "
                  + "reading of the induced stage to change.");
        }
    }
}
