using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// The paragraph the PRMS table prints under itself, held to what <see cref="Prms"/> actually sums.
///
/// <para><b>It had gone stale, and it was the load-bearing sentence.</b> It said the seventh order
/// was "represented by spherical aberration (B7) alone - there is no seventh-order coma,
/// astigmatism or field curvature to carry", which was true when the fifth-order code was the only
/// source of seventh-order terms and false from the moment <c>TertiaryCoefficients.Attach</c>
/// started filling tau2 to tau20. A reader comparing this program's spot against another's would
/// have been told to expect a discrepancy that no longer existed.</para>
///
/// <para>So the claim is now a COUNT, and the count is checked against the term table rather than
/// against a remembered fact. Prose that states a number can be tested; prose that gestures cannot,
/// which is the argument <c>OperandTableTests</c> already makes about the operand table.</para>
/// </summary>
public class PrmsNoteTests
{
    private readonly ITestOutputHelper _out;
    public PrmsNoteTests(ITestOutputHelper o) { _out = o; }

    private static string Report()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        return new ReportWriter(LensFile.Read(path, catalog), catalog, path).BuildReport();
    }

    /// <summary>Every seventh-order coefficient the spot formula actually carries.</summary>
    private static HashSet<string> SeventhOrderTermsCarried()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (a, b, _, _) in Prms.Terms)
            foreach (var n in new[] { a, b })
                if (n == "B7" || Regex.IsMatch(n, @"^Tau\d+$"))
                    names.Add(n);
        return names;
    }

    /// <summary>
    /// The count in the note is the count in the table. Nineteen: B7 is tau1, tau2 to tau19 follow
    /// it, and tau20 is out because distortion moves the patch without resizing it.
    /// </summary>
    [Fact]
    public void TheNoteCountsTheTauTheFormulaActuallyCarries()
    {
        var carried = SeventhOrderTermsCarried();
        _out.WriteLine($"carried ({carried.Count}): " + string.Join(" ", carried.OrderBy(x => x)));

        Assert.Equal(19, carried.Count);
        Assert.Contains("B7", carried);
        Assert.DoesNotContain("Tau20", carried);
        for (int i = 2; i <= 19; i++) Assert.Contains($"Tau{i}", carried);

        string text = Report();
        Assert.Contains("nineteen of the twenty tau", text, StringComparison.Ordinal);
        Assert.Contains("tau20", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And the claim that went stale must not come back.</b> It is not enough to have corrected
    /// it; the sentence was plausible, it survived a rewrite of the thing it described, and the
    /// cheapest guard against its return is to name it.
    /// </summary>
    [Fact]
    public void TheOldClaimIsGone()
    {
        string text = Report();

        Assert.DoesNotContain("spherical aberration (B7)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("no seventh-order coma", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason the note now gives for accuracy falling off is the truncation, and that is a
    /// claim about the NINTH order - which this program can now compute, so the note is no longer
    /// pointing at something unreachable.
    /// </summary>
    [Fact]
    public void TheReasonGivenIsTheTruncation()
    {
        string text = Report();

        Assert.Contains("TRUNCATION", text, StringComparison.Ordinal);
        Assert.Contains("ninth order arrives", text, StringComparison.Ordinal);
        Assert.Contains("docs/spot-prediction.md", text, StringComparison.Ordinal);
    }
}
