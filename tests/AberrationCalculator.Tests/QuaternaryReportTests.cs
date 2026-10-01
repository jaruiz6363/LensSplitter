using System;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The ninth-order coefficient as the report, the CLI and the MCP hand it out.
///
/// <para><b>The refusal is the part worth testing.</b> Getting a number out of a spherical design
/// is visible the moment anyone looks; a figured design quietly starting to ANSWER is not, because
/// the answer would be a plausible six-figure number rather than an obvious fault. So the refusal
/// has a test of its own, and it asserts the reason and not merely the absence of a table.</para>
/// </summary>
public class QuaternaryReportTests
{
    private static ReportWriter Writer(string fixture)
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens(fixture);
        return new ReportWriter(LensFile.Read(path, catalog), catalog, path);
    }

    /// <summary>A spherical design gets a table, a system figure and the units it is in.</summary>
    [Fact]
    public void ASphericalDesignGetsTheCoefficient()
    {
        string text = Writer("CookeTriplet").BuildQuaternaryText();

        Assert.Contains("QUATERNARY (NINTH-ORDER) SPHERICAL ABERRATION", text, StringComparison.Ordinal);
        Assert.Contains("intrinsic", text, StringComparison.Ordinal);
        Assert.Contains("T1-dagger", text, StringComparison.Ordinal);
        Assert.Contains("TOTAL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Not computed", text, StringComparison.Ordinal);

        // The units are not obvious and are not the rest of the report's, so they are stated.
        Assert.Contains("unit", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A figured design is refused, and the refusal says which surface and why - not just that
    /// something went wrong. A reader who is told "surface 1 is figured" can act on it.
    /// </summary>
    [Fact]
    public void AFiguredDesignIsRefusedWithItsReason()
    {
        string text = Writer("CookeTriplet_SPOTM_START_LO_ASPHERE").BuildQuaternaryText();

        Assert.Contains("Not computed", text, StringComparison.Ordinal);
        Assert.Contains("figured", text, StringComparison.Ordinal);
        Assert.Contains("spherical surfaces only", text, StringComparison.OrdinalIgnoreCase);

        // And it must not print a table anyway.
        Assert.DoesNotContain("T1-dagger", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="QuaternarySpherical.FromSystem"/> refuses in step with
    /// <see cref="QuaternarySpherical.Unsupported"/>, rather than the two drifting apart so that
    /// one says no while the other quietly computes.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet", false)]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE", true)]
    public void TheRefusalAndTheComputationAgree(string fixture, bool refused)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(fixture), catalog);
        var indices = IndexResolver.Build(system, catalog, 0.55, new System.Collections.Generic.List<string>());
        var paraxial = Core.RayTrace.ParaxialTrace.Trace(system, indices, 0.0);

        string? why = QuaternarySpherical.Unsupported(system);
        var result = QuaternarySpherical.FromSystem(system, indices, paraxial);

        Assert.Equal(refused, why != null);
        Assert.Equal(refused, result == null);
    }
}
