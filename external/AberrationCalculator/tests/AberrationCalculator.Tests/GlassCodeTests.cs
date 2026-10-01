using System;
using AberrationCalculator.Core.Glass;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Files routinely name their glasses by code rather than by catalog entry. Reading the
/// code is what lets those files be opened at all, so the parse has to be both correct and
/// unwilling to guess: a name mistaken for a code would put a made-up index on a surface
/// and report nothing wrong.
/// </summary>
public class GlassCodeTests
{
    [Theory]
    [InlineData("517642", 1.517, 64.2)]
    [InlineData("564.610", 1.564, 61.0)]
    [InlineData("620600", 1.620, 60.0)]
    [InlineData("1.5168/64.17", 1.5168, 64.17)]
    public void RecognisedForms_GiveTheIndexAndAbbeNumberTheyEncode(string text, double nd, double vd)
    {
        Assert.True(GlassCode.TryParse(text, out double gotNd, out double gotVd), $"'{text}' should parse");
        Assert.Equal(nd, gotNd, 6);
        Assert.Equal(vd, gotVd, 6);
    }

    /// <summary>
    /// The rejections matter more than the acceptances. A catalog name that happens to
    /// contain digits must not be read as a code, or a surface made of a real glass would
    /// silently take an invented index.
    /// </summary>
    [Theory]
    [InlineData("N-BK7")]
    [InlineData("SF11")]
    [InlineData("H-K9L")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("51764")]        // five digits: not the six-digit form
    [InlineData("5176421")]      // seven
    [InlineData("000000")]       // an index of 1.0 is not a glass
    [InlineData("AIR")]
    public void AnythingElse_IsRejectedRatherThanGuessed(string? text)
    {
        Assert.False(GlassCode.TryParse(text, out _, out _), $"'{text}' should not parse as a glass code");
    }

    /// <summary>
    /// A code carries nd and Vd, and those two numbers fix the index at d, F and C by
    /// definition: Vd = (nd-1)/(nF-nC). Checking the model reproduces its own defining
    /// relation is what says the dispersion formula behind it is right, without needing a
    /// catalog to compare against. The LensHH-LT model's constants are solved at the
    /// four-decimal lines (d 0.5876, F 0.4861, C 0.6563) and rounded to six decimals, so at the
    /// exact lines used here it gives nd back to 1e-5 and Vd to 0.05, not to the last digit.
    /// </summary>
    [Fact]
    public void ModelDispersion_ReproducesTheAbbeNumberItWasGiven()
    {
        const double lambdaD = 0.5875618, lambdaF = 0.4861327, lambdaC = 0.6562725;

        foreach (var (nd, vd) in new[] { (1.5168, 64.17), (1.7847, 25.68), (1.6200, 60.3) })
        {
            double nD = IndexResolver.ModelIndex(nd, vd, 0.0, lambdaD);
            double nF = IndexResolver.ModelIndex(nd, vd, 0.0, lambdaF);
            double nC = IndexResolver.ModelIndex(nd, vd, 0.0, lambdaC);

            Assert.Equal(nd, nD, 1e-5);
            Assert.Equal(vd, (nD - 1.0) / (nF - nC), 0.05);
            Assert.True(nF > nD && nD > nC, "index must fall with wavelength across the visible");
        }
    }

    /// <summary>
    /// How close the model gets to a real glass, stated rather than assumed: N-BK7 by its
    /// code against N-BK7 by its Sellmeier coefficients. The two agree to a few units in the
    /// fourth decimal across the visible, which is fine for first-order work and not fine
    /// for anything claiming catalog accuracy - hence the loose tolerance here and the
    /// unresolved-glass report in the program.
    /// </summary>
    [Fact]
    public void CodeIndexIsCloseToTheRealGlass_ButNotEqualToIt()
    {
        // N-BK7: nd = 1.51680, Vd = 64.17, so its code is 517642.
        Assert.True(GlassCode.TryParse("517642", out double nd, out double vd));

        // Sellmeier coefficients for N-BK7, from the published dispersion formula.
        double[] b = { 1.03961212, 0.231792344, 1.01046945 };
        double[] c = { 0.00600069867, 0.0200179144, 103.560653 };

        foreach (double lambda in new[] { 0.486, 0.546, 0.588, 0.656 })
        {
            double l2 = lambda * lambda;
            double n2 = 1.0;
            for (int i = 0; i < 3; i++) n2 += b[i] * l2 / (l2 - c[i]);
            double exact = Math.Sqrt(n2);

            double model = IndexResolver.ModelIndex(nd, vd, 0.0, lambda);

            Assert.True(Math.Abs(model - exact) < 1e-3,
                $"at {lambda} um the code gave {model:0.######} against {exact:0.######}");
        }
    }

    /// <summary>
    /// The decimal point separates the two halves of the code, so what follows it can be any
    /// length. One file writes "580.56"; another pads the same glass to
    /// "580.5600"; the classic six-digit form appears as "564.610". A parser that strips the
    /// point and demands six digits accepts only the middle one, and rejects a whole
    /// prescription over a trailing zero - which is how a three-element mobile lens came to
    /// read as a stack of air.
    /// </summary>
    [Theory]
    [InlineData("580.56", 1.580, 56.0)]
    [InlineData("580.5600", 1.580, 56.0)]
    [InlineData("804.24", 1.804, 24.0)]
    [InlineData("516.64", 1.516, 64.0)]
    [InlineData("469.61", 1.469, 61.0)]
    [InlineData("564.610", 1.564, 61.0)]
    [InlineData("517.642", 1.517, 64.2)]
    public void TheDecimalPointSeparatesRatherThanCounts(string code, double nd, double vd)
    {
        Assert.True(GlassCode.TryParse(code, out double gotNd, out double gotVd), code);
        Assert.Equal(nd, gotNd, 6);
        Assert.Equal(vd, gotVd, 6);
    }

    /// <summary>Things that are not codes must not become codes as the parser widens.</summary>
    [Theory]
    [InlineData("N-BK7")]
    [InlineData("")]
    [InlineData("58.56")]      // two digits before the point, not three
    [InlineData("5800.56")]    // four
    [InlineData("580.")]       // nothing after it
    [InlineData("580.56789")]  // more decimals than any convention writes
    [InlineData("580.5A")]
    public void WhatIsNotACodeIsRejected(string text)
        => Assert.False(GlassCode.TryParse(text, out _, out _), text);

    /// <summary>
    /// The indices the model produces for these codes, against an independent model-glass
    /// values for the same design (MOBILE_KYOCERA_3P_USP8558939), at the five wavelengths it
    /// defines.
    ///
    /// <para>nd is reproduced to eight decimals, which is what confirms the code is being READ
    /// correctly. The dispersion away from d is a model, and ours is not that one: the
    /// two agree to about 5e-5 on ordinary glasses and part company by 2.5e-3 at 0.436 um on
    /// the 1.804/24 one, the tolerance below being set to admit that. It does not matter for
    /// the work these codes were needed for, which is monochromatic at 0.5876 um where the
    /// worst disagreement is 7e-6, but it would matter for secondary colour.</para>
    /// </summary>
    [Theory]
    [InlineData("580.5600", 0.5876, 1.5800000143)]
    [InlineData("580.5600", 0.4861, 1.5872176190)]
    [InlineData("580.5600", 0.6563, 1.5768604563)]
    [InlineData("804.2400", 0.5876, 1.8040000374)]
    [InlineData("804.2400", 0.5500, 1.8110733615)]
    [InlineData("516.6400", 0.5876, 1.5160000116)]
    [InlineData("469.6100", 0.5876, 1.4690000109)]
    public void TheModelAgreesWithAnIndependentImplementation(string code, double lambda, double expected)
    {
        Assert.True(GlassCode.TryParse(code, out double nd, out double vd), code);
        double model = IndexResolver.ModelIndex(nd, vd, 0.0, lambda);
        Assert.True(Math.Abs(model - expected) < 6e-4,
            $"{code} at {lambda} um: {model:0.########} against the reference {expected:0.########}");
    }

    /// <summary>At d itself there is no model left to disagree about - the code IS nd.</summary>
    [Theory]
    [InlineData("580.5600", 1.5800000143)]
    [InlineData("804.2400", 1.8040000374)]
    [InlineData("516.6400", 1.5160000116)]
    [InlineData("469.6100", 1.4690000109)]
    public void AtTheDLineTheCodeIsTheIndex(string code, double expected)
    {
        Assert.True(GlassCode.TryParse(code, out double nd, out _), code);
        Assert.True(Math.Abs(nd - expected) < 1e-7,
            $"{code}: nd {nd:0.########} against the reference {expected:0.########}");
    }
}
