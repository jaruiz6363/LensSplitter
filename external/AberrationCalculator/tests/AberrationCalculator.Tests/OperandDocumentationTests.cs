using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Every operand is documented, and the check is mechanical because the alternative did not work.
///
/// <para><c>ASBLT</c> was added, given inline help, wired into the evaluator and checked against a
/// central difference - and left out of the operand table in <c>docs/optimizer.md</c>, which the
/// README describes as listing every operand. Nothing failed, because nothing was looking. The
/// usage text has had <c>TheUsageTextMentionsEveryCommand</c> guarding it for exactly this
/// reason; the documentation had no equivalent.</para>
/// </summary>
public class OperandDocumentationTests
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly until the docs folder
    /// appears. Returns null when the tests run somewhere the sources are not, in which case
    /// there is nothing to check rather than something to fail.
    /// </summary>
    private static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "optimizer.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void EveryOperandAppearsInTheOptimizerDocument()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));

        // Whole word rather than backtick-delimited: the ray operands are documented as one
        // grouped row, `PX PY PZ PL PM PN`, so requiring backticks around each name would flag
        // six that are there.
        var missing = OperandHelp.All
                          .Select(t => t.ToString())
                          .Where(name => !Regex.IsMatch(doc, @"\b" + Regex.Escape(name) + @"\b"))
                          .ToArray();

        Assert.True(missing.Length == 0,
            "docs/optimizer.md does not mention: " + string.Join(", ", missing));
    }

    /// <summary>
    /// And every operand answers <c>HELP</c>, which is the other place one could be forgotten:
    /// the inline help is hand written per operand, not generated.
    /// </summary>
    [Fact]
    public void EveryOperandHasInlineHelpAndAnExample()
    {
        foreach (var type in OperandHelp.All)
        {
            string what = OperandHelp.Summary(type);
            Assert.False(string.IsNullOrWhiteSpace(what), $"{type} has no description");
            Assert.DoesNotContain("TODO", what, StringComparison.OrdinalIgnoreCase);

            // The example must PARSE, and parse to this operand. That used to be a check that
            // the text began with the operand's name, which is weaker in both directions: it
            // passed an example whose inputs were wrong, and it failed ABER, whose name in a
            // merit file is deliberately not its name in the enum - a coefficient operand is
            // written as its coefficient, `Tau15, 1, TAR 0`, because that is how the report
            // spells it. Parsing the example says the thing actually worth saying, which is that
            // a user who copies it gets what the help promised.
            string example = OperandHelp.Example(type);
            var parsed = MeritFile.Parse(new[] { example });

            Assert.True(parsed.Count == 1, $"{type}'s example does not parse to one operand: {example}");
            Assert.Equal(type, parsed[0].Type);
        }
    }

    /// <summary>
    /// <b>Every targetable coefficient appears in the lookup table in docs/optimizer.md.</b>
    ///
    /// <para>A designer meets aberrations by NAME - Shafer's "5th-order field curvature", a
    /// specification's "sagittal oblique spherical" - and has to type a symbol. The mapping
    /// existed in <c>AberrationNames</c> and was printed beside report values, but until the
    /// coefficients became targetable it never needed to be somewhere a user could look it up
    /// before writing anything. Mechanical for the reason every check in this file is: a table
    /// maintained by hand loses the row that was added last.</para>
    /// </summary>
    [Fact]
    public void EveryCoefficientIsInTheLookupTable()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));
        int start = doc.IndexOf("#### Which coefficient is which aberration", StringComparison.Ordinal);
        Assert.True(start >= 0, "docs/optimizer.md has no coefficient lookup table");

        // The table ends where the next heading begins.
        int end = doc.IndexOf("\n### ", start, StringComparison.Ordinal);
        string table = end > start ? doc.Substring(start, end - start) : doc.Substring(start);

        var missing = BuchdahlTerms.Names
                          .Where(n => !table.Contains("`" + n + "`", StringComparison.Ordinal))
                          .ToArray();

        Assert.True(missing.Length == 0,
            "the lookup table does not name: " + string.Join(", ", missing));
    }

    /// <summary>
    /// And every coefficient the table names is one the merit-function parser accepts, so the
    /// table cannot send a reader to a symbol that does not work.
    /// </summary>
    [Fact]
    public void TheLookupTableNamesNothingThatCannotBeTargeted()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));
        int start = doc.IndexOf("#### Which coefficient is which aberration", StringComparison.Ordinal);
        if (start < 0) return;
        int end = doc.IndexOf("\n### ", start, StringComparison.Ordinal);
        string table = end > start ? doc.Substring(start, end - start) : doc.Substring(start);

        foreach (Match m in Regex.Matches(table, @"`(Tau\d+|B7|B5|Pi5|C5|E5|[BFCE]|Pi|[FMN]\d)`"))
        {
            string name = m.Groups[1].Value;
            var parsed = MeritFile.Parse(new[] { $"{name}, 1, TAR 0" });
            Assert.Equal(OperandType.ABER, parsed[0].Type);
        }
    }
}
