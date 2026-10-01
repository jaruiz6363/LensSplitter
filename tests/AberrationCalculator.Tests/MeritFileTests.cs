using System;
using System.Linq;

using AberrationCalculator.Core.Enums;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The two settings files, which a person edits by hand.
///
/// <para>Both have to survive a round trip, and both have to fail LOUDLY on a mistake rather than
/// silently optimising something other than what was asked for. A file that quietly drops a line
/// it did not understand is worse than one that refuses to start.</para>
/// </summary>
public class MeritFileTests
{
    private const string Merit = @"
# A comment, and a blank line above it.

EFL,   100, TAR 50,           2
EGT,    10, MIN 1,            2, 4
EAT,    10, MIN 0.1,          2, 4
DTRGT,  10, MIN 1.5, MAX 12,  2, 4
PRMSA,   1, TAR 0
TTL,     5, MAX 60
AXC,     2, TAR 0
LCF,     5, TAR 0,            1.0
DISTF,  10, MIN -2, MAX 2,    0.7
RY,      1, TAR 0,            7, 1, 1, 0, 1
";

    [Fact]
    public void ParsesEveryShapeTheGrammarOffers()
    {
        var ops = MeritFile.Parse(Merit.Split('\n'));
        Assert.Equal(10, ops.Count);

        var efl = ops[0];
        Assert.Equal(OperandType.EFL, efl.Type);
        Assert.Equal(100.0, efl.Weight);
        Assert.Equal(50.0, efl.Target);
        Assert.False(efl.IsBoundary);
        Assert.Equal(2, efl.Wave);                       // EFL's single input is the wavelength

        var egt = ops[1];
        Assert.True(egt.IsBoundary);
        Assert.Equal(1.0, egt.Min);
        Assert.Null(egt.Max);
        Assert.Equal(2, egt.Surface);                    // a span: two surface inputs
        Assert.Equal(4, egt.Surface2);

        var dtrgt = ops[3];
        Assert.Equal(1.5, dtrgt.Min);
        Assert.Equal(12.0, dtrgt.Max);

        // No inputs at all, and none invented.
        var prmsa = ops[4];
        Assert.Equal(OperandType.PRMSA, prmsa.Type);
        Assert.Equal(0.0, prmsa.Target);
        Assert.Empty(OperandInputs.For(OperandType.PRMSA));

        Assert.Equal(60.0, ops[5].Max);                  // TTL, no inputs
        Assert.Equal(OperandType.AXC, ops[6].Type);      // AXC, no inputs

        Assert.Equal(1.0, ops[7].Hy);                    // LCF takes only hy
        Assert.Equal(0.7, ops[8].Hy);                    // and so does DISTF

        // A ray operand: surface, wave, hy, px, py, positionally.
        var ry = ops[9];
        Assert.Equal(OperandType.RY, ry.Type);
        Assert.Equal(7, ry.Surface);
        Assert.Equal(1, ry.Wave);
        Assert.Equal(1.0, ry.Hy);
        Assert.Equal(0.0, ry.Px);
        Assert.Equal(1.0, ry.Py);
    }

    /// <summary>
    /// Wavelengths are numbered from one, and a zero is refused.
    ///
    /// <para>A merit function should never contain an index that is not an index. To mean the
    /// reference colour, the wavelength is left off - and the WRITER has to respect that too, or
    /// a file it produced would not read back.</para>
    /// </summary>
    [Theory]
    [InlineData("EFL, 1, TAR 50, 0")]
    [InlineData("RY, 1, TAR 0, 7, 0, 1, 0, 1")]
    [InlineData("EFL, 1, TAR 50, -1")]
    public void AWavelengthOfZeroIsRefused(string line)
    {
        var ex = Assert.Throws<FormatException>(() => MeritFile.Parse(new[] { line }));
        Assert.Contains("numbered from 1", ex.Message);
    }

    [Fact]
    public void TheWriterNeverEmitsAWavelengthOfZero()
    {
        // An operand that named no wavelength writes none, rather than padding with a zero the
        // parser would refuse.
        string written = MeritFile.Write(MeritFile.Parse(new[] { "EFL, 10, TAR 50" }));
        Assert.Contains("EFL, 10, TAR 50", written);
        Assert.DoesNotContain("TAR 50, 0", written);

        // And what it wrote reads back.
        var back = Assert.Single(MeritFile.Parse(written.Split('\n')));
        Assert.Equal(0, back.Wave);          // still unspecified, still the reference colour
        Assert.Equal(50.0, back.Target);
    }

    /// <summary>
    /// Trailing inputs may be left off. A ray operand given only a surface is that surface at the
    /// reference colour, the full field and the chief ray.
    /// </summary>
    [Fact]
    public void TrailingInputsMayBeOmitted()
    {
        var ry = Assert.Single(MeritFile.Parse(new[] { "RY, 1, TAR 0, 7" }));
        Assert.Equal(7, ry.Surface);
        Assert.Equal(0, ry.Wave);
        Assert.Equal(1.0, ry.Hy);
        Assert.Equal(0.0, ry.Px);
        Assert.Equal(0.0, ry.Py);

        // A span given one surface is that surface alone.
        var egt = Assert.Single(MeritFile.Parse(new[] { "EGT, 1, MIN 0.8, 3" }));
        Assert.Equal(3, egt.Surface);
        Assert.Equal(3, egt.Surface2);
    }

    [Fact]
    public void WhatIsWrittenParsesBackToWhatWentIn()
    {
        var first = MeritFile.Parse(Merit.Split('\n'));
        var second = MeritFile.Parse(MeritFile.Write(first).Split('\n'));

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            var a = first[i];
            var b = second[i];
            Assert.Equal(a.Type, b.Type);
            Assert.Equal(a.Surface, b.Surface);
            Assert.Equal(a.Surface2, b.Surface2);
            Assert.Equal(a.Wave, b.Wave);
            Assert.Equal(a.Hy, b.Hy, 12);
            Assert.Equal(a.Px, b.Px, 12);
            Assert.Equal(a.Py, b.Py, 12);
            Assert.Equal(a.Target, b.Target, 12);
            Assert.Equal(a.Weight, b.Weight, 12);
            Assert.Equal(a.Min, b.Min);
            Assert.Equal(a.Max, b.Max);
        }
    }

    [Theory]
    [InlineData("EFL", "a type and a weight")]
    [InlineData("NOSUCHTHING, 1, TAR 0", "is not an operand")]
    [InlineData("EFL, x, TAR 50", "is not a weight")]
    [InlineData("EFL, 1", "no TAR, MIN or MAX")]
    [InlineData("EFL, 1, TAR 50, MAX 60", "both a TAR and a limit")]
    [InlineData("EFL, 1, MIN 60, MAX 50", "is above MAX")]
    [InlineData("TTL, 1, MAX 60, 3", "takes no inputs")]
    [InlineData("EFL, 1, TAR 50, 1, 2", "takes 1 inputs")]
    [InlineData("EFL, 1, TAR x", "TAR needs a number")]
    [InlineData("EFL, 1, TAR 50, q", "is not a number")]
    public void RefusesAMalformedOperandAndSaysWhy(string line, string expected)
    {
        var ex = Assert.Throws<FormatException>(() => MeritFile.Parse(new[] { line }));
        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);

        // The line has to appear, or a forty-line merit function gives no clue which one is wrong.
        Assert.Contains(line.Split(',')[0], ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsTheLineNumberOfAFault()
    {
        var ex = Assert.Throws<FormatException>(() => MeritFile.Parse(
            new[] { "EFL, 1, TAR 50", "", "# a comment", "NOPE, 1, TAR 0" }, "test.mf"));
        Assert.Contains("line 4", ex.Message);
        Assert.Contains("test.mf", ex.Message);
    }

    // ── The variables file ───────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesVariablesAndPickups()
    {
        var spec = VarFile.Parse(new[]
        {
            "VAR TH 2 MIN 1.0 MAX 25.0",
            "VAR CV 4",
            "PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1",
        });

        Assert.Equal(2, spec.Variables.Count);
        Assert.Equal(VariableKind.Thickness, spec.Variables[0].Kind);
        Assert.Equal(2, spec.Variables[0].Surface);
        Assert.Equal(1.0, spec.Variables[0].Min);
        Assert.Equal(25.0, spec.Variables[0].Max);

        Assert.Equal(VariableKind.Curvature, spec.Variables[1].Kind);
        Assert.False(spec.Variables[1].IsBounded);

        var p = Assert.Single(spec.Pickups);
        Assert.Equal(2, p.TargetSurfaceIndex);
        Assert.Equal(1, p.SourceSurfaceIndex);
        Assert.Equal(PickupParameter.Thickness, p.Parameter);
        Assert.Equal(1.0, p.ScaleFactor);
        Assert.Equal(-0.1, p.Offset);
    }

    /// <summary>
    /// A VAR line MERGES into what is already known.
    ///
    /// <para>This is what makes setting a limit from a command safe. A command that named only
    /// the maximum would otherwise silently discard a minimum set a moment earlier - the surprise
    /// that <c>chmod u+x</c> exists to avoid. For a file this program wrote the rule never comes
    /// up, since it emits one line per variable.</para>
    /// </summary>
    [Fact]
    public void RepeatedVariableLinesMergeRatherThanReplace()
    {
        var spec = VarFile.Parse(new[]
        {
            "VAR TH 2 MIN 1.0",
            "VAR TH 2 MAX 25.0",
        });

        var v = Assert.Single(spec.Variables.Items);
        Assert.Equal(1.0, v.Min);
        Assert.Equal(25.0, v.Max);
    }

    /// <summary>Merging needs a way back out, or a bound could never be removed.</summary>
    [Fact]
    public void FreeDropsTheBounds()
    {
        var spec = VarFile.Parse(new[]
        {
            "VAR CV 3 MIN -0.05 MAX 0.05",
            "VAR CV 3 FREE",
        });

        var v = Assert.Single(spec.Variables.Items);
        Assert.False(v.IsBounded);
    }

    [Fact]
    public void VariablesRoundTripThroughTheFile()
    {
        var first = VarFile.Parse(new[]
        {
            "VAR TH 2 MIN 1.0 MAX 25.0",
            "VAR CV 4",
            "PICKUP CV 5 INDEX 4 SCALE -1 OFFSET 0.25",
        });
        var second = VarFile.Parse(VarFile.Write(first).Split('\n'));

        Assert.Equal(first.Variables.Count, second.Variables.Count);
        for (int i = 0; i < first.Variables.Count; i++)
        {
            Assert.Equal(first.Variables[i].Kind, second.Variables[i].Kind);
            Assert.Equal(first.Variables[i].Surface, second.Variables[i].Surface);
            Assert.Equal(first.Variables[i].Min, second.Variables[i].Min, 12);
            Assert.Equal(first.Variables[i].Max, second.Variables[i].Max, 12);
        }

        var a = Assert.Single(first.Pickups);
        var b = Assert.Single(second.Pickups);
        Assert.Equal(a.TargetSurfaceIndex, b.TargetSurfaceIndex);
        Assert.Equal(a.SourceSurfaceIndex, b.SourceSurfaceIndex);
        Assert.Equal(a.ScaleFactor, b.ScaleFactor);
        Assert.Equal(a.Offset, b.Offset);
    }

    /// <summary>
    /// The figuring kinds PARSE. They did not until the aspheric arrangement was established,
    /// and the refusal used to live here, in the parser; it now lives in
    /// <c>SphericalOnly.Require</c>, which is the layer that actually knows whether the
    /// evaluation loop can carry a figured surface. Moving it matters because a .var file is
    /// also read by tools that never optimise.
    /// </summary>
    [Theory]
    [InlineData("VAR CC 3", VariableKind.Conic, 3)]
    [InlineData("VAR CONIC 3", VariableKind.Conic, 3)]
    [InlineData("VAR A4 1", VariableKind.Asphere4, 1)]
    [InlineData("VAR A6 2", VariableKind.Asphere6, 2)]
    [InlineData("VAR A8 10", VariableKind.Asphere8, 10)]
    public void FiguringKindsParse(string line, VariableKind kind, int surface)
    {
        var spec = VarFile.Parse(new[] { line });

        var v = Assert.Single(spec.Variables.Items);
        Assert.Equal(kind, v.Kind);
        Assert.Equal(surface, v.Surface);
    }

    /// <summary>
    /// A figuring variable round-trips through the file it is written to. <c>A410</c> is the
    /// case worth pinning: every prefix is two characters, so the surface number needs no
    /// separator and cannot be misread.
    /// </summary>
    [Fact]
    public void FiguringVariablesRoundTripThroughTheFile()
    {
        var spec = VarFile.Parse(new[]
        {
            "VAR CC 3 MIN -2 MAX 0",
            "VAR A4 1",
            "VAR A6 2",
            "VAR A8 10",
        });

        var again = VarFile.Parse(VarFile.Write(spec).Split('\n'));

        Assert.Equal(4, again.Variables.Count);
        Assert.Equal("CC3", again.Variables[0].Name);
        Assert.Equal(-2.0, again.Variables[0].Min);
        Assert.Equal(0.0, again.Variables[0].Max);
        Assert.Equal("A41", again.Variables[1].Name);
        Assert.Equal("A62", again.Variables[2].Name);
        Assert.Equal("A810", again.Variables[3].Name);
        Assert.Equal(VariableKind.Asphere8, again.Variables[3].Kind);
        Assert.Equal(10, again.Variables[3].Surface);
    }

    /// <summary>
    /// r^10 and beyond are refused, and told why. They are not a typo - they are a perfectly
    /// reasonable surface description - but no coefficient this program computes can see them,
    /// so a variable driving one would have an identically zero column in the Jacobian and would
    /// look like it was working.
    /// </summary>
    [Fact]
    public void AsphericTermsBeyondR8AreRefusedWithTheReason()
    {
        var ex = Assert.Throws<FormatException>(() => VarFile.Parse(new[] { "VAR A10 2" }));

        Assert.Contains("beyond r^8", ex.Message, StringComparison.Ordinal);
        Assert.Contains("A4, A6 or A8", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VAR ZZ 1", "not a variable kind")]
    [InlineData("VAR CV", "needs a kind and a surface")]
    [InlineData("VAR CV x", "is not a surface")]
    [InlineData("VAR CV 1 MIN 5 MAX 1", "is above MAX")]
    [InlineData("VAR CV 1 WOBBLE", "unexpected")]
    [InlineData("PICKUP TH 2", "needs a parameter")]
    [InlineData("PICKUP ZZ 2 INDEX 1", "not a pickup parameter")]
    [InlineData("PICKUP TH 2 FROM 1", "expected INDEX")]
    [InlineData("SOMETHING ELSE", "expected VAR or PICKUP")]
    public void RefusesAMalformedVariableLineAndSaysWhy(string line, string expected)
    {
        var ex = Assert.Throws<FormatException>(() => VarFile.Parse(new[] { line }));
        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
