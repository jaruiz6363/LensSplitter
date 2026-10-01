using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using AberrationCalculator.Optimize.Operands;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The operand table in <c>docs/optimizer.md</c> states what each operand takes, and this holds it
/// to what the parser actually takes.
///
/// <para><b>It was written by hand and it drifted.</b> When the coefficient operand gained a
/// surface its row still said <c>wave</c>, and when ASBLT's tilt input was named <c>tilt-deg</c>
/// the row still said <c>tilt</c>. A table that states its own arguments wrongly is worse than one
/// that says nothing, because a reader has no way to check it - which is the argument
/// <c>OperandInputs</c> already makes about the inputs being declared in exactly one place, and
/// that argument applies to the documentation of them too.</para>
///
/// <para>The check is on the SET of input signatures rather than row by row, because several rows
/// group operands that share a signature - six ray operands are one fact, not six - and a mapping
/// from rows back to types would be its own thing to get wrong.</para>
/// </summary>
public class OperandTableTests
{
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

    /// <summary>The table's third column, as a set, with the doc's "none" read as "no inputs".</summary>
    private static HashSet<string> SignaturesInTheTable(string doc)
    {
        int start = doc.IndexOf("| operand | | inputs |", StringComparison.Ordinal);
        Assert.True(start >= 0, "docs/optimizer.md has no operand table");

        int end = doc.IndexOf("\n\n", start, StringComparison.Ordinal);
        string table = end > start ? doc.Substring(start, end - start) : doc.Substring(start);

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in table.Split('\n'))
        {
            var cells = line.Split('|');
            if (cells.Length < 4) continue;

            string inputs = cells[3].Trim().Trim('`').Trim();
            if (inputs.Length == 0 || inputs.StartsWith("---", StringComparison.Ordinal)) continue;
            if (inputs == "inputs") continue;

            found.Add(inputs == "none" ? "no inputs" : inputs);
        }
        return found;
    }

    /// <summary>
    /// Every signature the parser accepts is in the table, and the table invents none.
    /// </summary>
    [Fact]
    public void TheOperandTableStatesTheSignaturesTheParserTakes()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));
        var inTable = SignaturesInTheTable(doc);

        var real = new HashSet<string>(
            OperandHelp.All.Select(OperandInputs.Describe), StringComparer.Ordinal);

        var missing = real.Except(inTable).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var invented = inTable.Except(real).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "the table does not state these signatures: " + string.Join(" | ", missing));
        Assert.True(invented.Length == 0,
            "the table states signatures no operand has: " + string.Join(" | ", invented));
    }

    /// <summary>
    /// And the coefficient row in particular says <c>surface</c>, since that is the one that went
    /// stale and the one a designer is most likely to be reading when they get it wrong.
    /// </summary>
    [Fact]
    public void TheCoefficientRowSaysWhatACoefficientTakes()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));
        var row = doc.Split('\n')
                     .FirstOrDefault(l => l.StartsWith("| a coefficient name |", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains(OperandInputs.Describe(OperandType.ABER), row!, StringComparison.Ordinal);
    }
}
