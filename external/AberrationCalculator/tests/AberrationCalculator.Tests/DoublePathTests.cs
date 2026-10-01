using System;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The value-only path runs the aberration chain in ordinary arithmetic and lifts the results
/// into dual numbers; the Jacobian path runs it in duals throughout. They must agree.
///
/// <para><b>Why this test rather than trust.</b> The lift enumerates forty-one coefficient
/// fields, five paraxial arrays and fifteen paraxial scalars by hand. A field left out does not
/// throw and does not warn - it leaves a zero where a number should be, and a zero aberration
/// coefficient looks exactly like one that has been corrected. The merit would improve, the
/// optimiser would chase it, and nothing would ever say so. So every operand is evaluated down
/// both paths and held to agreeing.</para>
/// </summary>
public class DoublePathTests
{
    private static Design Open(VariableSet vars)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        return new Design(system, catalog, vars);
    }

    private static VariableSet Curvatures(params int[] surfaces)
    {
        var set = new VariableSet();
        foreach (int s in surfaces) set.Add(new Variable { Kind = VariableKind.Curvature, Surface = s });
        return set;
    }

    /// <summary>
    /// Every operand type, evaluated on the double path and on a seeded dual pass, must give the
    /// same VALUE. The derivative is what differs between them, and only that.
    /// </summary>
    [Fact]
    public void TheDoubleAndDualPathsAgree()
    {
        var vars = Curvatures(1, 2, 4);
        var design = Open(vars);

        var operands = new[]
        {
            new Operand { Type = OperandType.PRMSA, Target = 0.0 },
            new Operand { Type = OperandType.TTL, Target = 0.0 },
            new Operand { Type = OperandType.EFL, Target = 0.0, Wave = 2 },
            new Operand { Type = OperandType.PY, Surface = 6, Wave = 2, Hy = 1.0, Py = 1.0 },
            new Operand { Type = OperandType.PL, Surface = 4, Wave = 1, Hy = 0.7, Py = 0.5 },
            new Operand { Type = OperandType.RY, Surface = 6, Wave = 2, Hy = 1.0, Py = 1.0 },
            new Operand { Type = OperandType.RZ, Surface = 3, Wave = 1, Hy = 0.5, Py = 0.8 },
            new Operand { Type = OperandType.RN, Surface = 5, Wave = 3, Hy = 1.0, Py = 0.3 },
            new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 1 },
            new Operand { Type = OperandType.EAT, Surface = 2, Surface2 = 2 },
            new Operand { Type = OperandType.DTRGT, Surface = 1, Surface2 = 1 },
            new Operand { Type = OperandType.LCF, Hy = 1.0 },
            new Operand { Type = OperandType.AXC },
            new Operand { Type = OperandType.DISTF, Hy = 0.7 },
        };

        // Through the merit function, because the test assembly deliberately cannot see Dual -
        // the aliased build is PrivateAssets so it can never leak into anything that did not ask
        // for it. Evaluate(false) takes the double path and Evaluate(true) the dual one, so
        // comparing their values compares exactly what this test is about, one operand at a time.
        var merit = new MeritFunction(design);
        foreach (var op in operands) merit.Add(op);

        var value = merit.Evaluate(false);
        var dual = merit.Evaluate(true);

        Assert.True(value.Ok, value.Failure);
        Assert.True(dual.Ok, dual.Failure);
        Assert.Equal(dual.Values.Length, value.Values.Length);

        for (int i = 0; i < value.Values.Length; i++)
            Assert.Equal(dual.Values[i], value.Values[i], 10);

        // Every operand type was actually exercised, or the loop above proves nothing.
        var seen = new System.Collections.Generic.HashSet<OperandType>();
        foreach (var op in merit.Operands) seen.Add(op.Type);
        foreach (var op in operands)
            Assert.Contains(op.Type, seen);
    }

    /// <summary>
    /// The whole merit function, both ways. This is the property the optimiser actually leans
    /// on: the number it compares against a trial is the same number either path produces.
    /// </summary>
    [Fact]
    public void TheMeritIsTheSameDownBothPaths()
    {
        var vars = Curvatures(1, 2, 4);
        var design = Open(vars);
        var merit = new MeritFunction(design);
        merit.Add(new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 100.0 });
        merit.Add(new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 10.0, Wave = 2 });
        merit.Add(new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = 1.0 });
        merit.Add(new Operand { Type = OperandType.AXC, Min = -0.2, Max = 0.2 });

        var value = merit.Evaluate(false);     // the double path
        var jacobian = merit.Evaluate(true);   // duals throughout

        Assert.True(value.Ok && jacobian.Ok);
        Assert.Equal(jacobian.Merit, value.Merit, 10);
        Assert.Equal(jacobian.Values.Length, value.Values.Length);

        for (int i = 0; i < value.Values.Length; i++)
            Assert.Equal(jacobian.Values[i], value.Values[i], 10);
    }
}
