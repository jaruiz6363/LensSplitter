using System;
using System.Linq;

using AberrationCalculator.Core.Aberrations;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Naming the coefficients, including the nineteen that have no name.
///
/// <para>A designer meets aberrations by name - Shafer's "5th-order field curvature", a
/// specification's "sagittal oblique spherical" - and the program answers to `Pi5` and `M2`. The
/// report annotates its own numbers so a reader need not carry the mapping; until now it stopped
/// at `B7`, so twenty tertiary coefficients were printed bare while everything above them was
/// labelled.</para>
/// </summary>
public class AberrationNameTests
{
    /// <summary>
    /// <b>The family rule reproduces the orders that DO have names.</b> This is what makes
    /// applying it at seventh order reading a pattern rather than inventing one: all eleven
    /// named coefficients come out of the same rule, so it was not fitted to the answer.
    ///
    /// <para>It is also the test that would have caught the error this rule replaced. Written
    /// out by hand in a documentation table, elliptical coma and astigmatism were put two rows
    /// too high at seventh order - elliptical coma is two powers of APERTURE, not two of field -
    /// and nothing said so, because nothing was checking the hand-written rows against the
    /// pattern the named ones follow.</para>
    /// </summary>
    [Theory]
    // Third order: rho^3, rho^2 H, rho H^2, H^3.
    [InlineData(3, 0, "spherical")]
    [InlineData(2, 1, "coma")]
    [InlineData(1, 2, "astigmatism / field curvature")]
    [InlineData(0, 3, "distortion")]
    // Fifth: rho^5 down to H^5.
    [InlineData(5, 0, "spherical")]
    [InlineData(4, 1, "coma")]
    [InlineData(3, 2, "oblique spherical")]
    [InlineData(2, 3, "elliptical coma")]
    [InlineData(1, 4, "astigmatism / field curvature")]
    [InlineData(0, 5, "distortion")]
    public void TheFamilyRuleReproducesTheNamedOrders(int aperture, int field, string expected)
    {
        Assert.Equal(expected, AberrationNames.FamilyOf(aperture, field));
    }

    /// <summary>
    /// And it declines to name the two seventh-order monomials that have no counterpart below
    /// them. Asserted rather than left implicit: an empty answer is the honest one, and a future
    /// edit that filled it in with a plausible word would be a regression that read like an
    /// improvement.
    /// </summary>
    [Theory]
    [InlineData(4, 3)]
    [InlineData(3, 4)]
    public void TheFamilyRuleNamesNothingItCannot(int aperture, int field)
    {
        Assert.Equal("", AberrationNames.FamilyOf(aperture, field));
    }

    /// <summary>
    /// <b>Every one of the thirty-seven coefficients is described.</b> Nineteen of them were not,
    /// so a report named B through B7 and then printed Tau2 to Tau20 bare - the coefficients a
    /// designer is least likely to recognise given the least help.
    /// </summary>
    [Fact]
    public void EveryCoefficientIsDescribed()
    {
        var bare = BuchdahlTerms.Names
                       .Where(n => string.IsNullOrWhiteSpace(AberrationNames.Describe(n)))
                       .ToArray();

        Assert.True(bare.Length == 0, "not described: " + string.Join(", ", bare));
    }

    /// <summary>And every one is given an order, since the report groups by it.</summary>
    [Fact]
    public void EveryCoefficientHasAnOrder()
    {
        foreach (string name in BuchdahlTerms.Names)
            Assert.True(AberrationNames.Order(name) is 3 or 5 or 7,
                        $"{name} has no order");
    }

    /// <summary>
    /// The tertiary descriptions say what the coefficient multiplies, which is the part that can
    /// be stated exactly when the name cannot.
    /// </summary>
    [Theory]
    [InlineData("Tau2", "7th coma, rho^6 H")]
    [InlineData("Tau4", "7th oblique spherical, rho^5 H^2")]
    [InlineData("Tau7", "7th rho^4 H^3")]                       // no classical counterpart
    [InlineData("Tau11", "7th rho^3 H^4")]                      // nor here
    [InlineData("Tau15", "7th elliptical coma, rho^2 H^5")]
    [InlineData("Tau18", "7th astigmatism / field curvature, rho H^6")]
    [InlineData("Tau20", "7th distortion, H^7")]
    public void ATertiaryCoefficientIsDescribedByItsMonomial(string name, string expected)
    {
        Assert.Equal(expected, AberrationNames.Describe(name));
    }

    /// <summary>
    /// B7 and Tau1 are the same quantity, and only B7 is spelled - but if anything ever asks for
    /// Tau1 it must not get a different answer from B7.
    /// </summary>
    [Fact]
    public void B7AndTau1AgreeAboutWhatTheyAre()
    {
        Assert.Contains("spherical", AberrationNames.Describe("B7"), StringComparison.Ordinal);
        Assert.Contains("spherical", AberrationNames.Describe("Tau1"), StringComparison.Ordinal);
        Assert.Equal(7, AberrationNames.Order("B7"));
        Assert.Equal(7, AberrationNames.Order("Tau1"));
    }

    /// <summary>Something that is not a coefficient gets nothing, rather than a guess.</summary>
    [Theory]
    [InlineData("Tau21")]
    [InlineData("Tau0")]
    [InlineData("Q9")]
    [InlineData("")]
    public void AnUnknownNameIsNotDescribed(string name)
    {
        Assert.Equal("", AberrationNames.Describe(name));
        Assert.Equal(0, AberrationNames.Order(name));
    }
}
