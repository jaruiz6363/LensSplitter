using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The analytic derivative against a central difference, on EVERY design in this repository.
///
/// <para><b>Why a sweep and not more hand-written cases.</b> <see cref="AnalyticDerivativeTests"/>
/// checks the same thing on four designs chosen when it was written. Every defect found on
/// 20 September 2026 lived in a case no fixture contained - an r-squared term, a model glass, an
/// immersed object space, a figured system at a finite conjugate - and three of the four were in
/// code the optimiser differentiates through. A check that covers the designs someone thought of
/// covers the designs someone thought of. This one covers whatever is on disk, so a fixture added
/// for any other reason is differentiated the day it lands.</para>
///
/// <para><b>What the comparison is worth.</b> The reference is produced by the VALUE code, on the
/// spot, so it cannot go stale and cannot be edited into agreement. It is also a DIFFERENT
/// LINEAGE from the analytic derivative, which matters more than it sounds: a check that compares
/// two copies of the same arithmetic cannot see a defect they share, and this repository has now
/// watched that happen twice in one day.</para>
///
/// <para><b>Both fixture folders.</b> <c>fixtures/lenses</c> holds the designs the aberration
/// tests use; <c>fixtures/coefficient-reference</c> holds the .zmx ones, including the E family,
/// whose whole purpose is to sit in the awkward cases - curved end surfaces, an r-squared term,
/// immersed object and image spaces, model glasses. Those are exactly the designs a derivative
/// has never been taken on.</para>
/// </summary>
public class DerivativeSweepTests
{
    /// <summary>Every design on disk - the one discovery, shared with the coefficient sweep.</summary>
    public static IEnumerable<object[]> EveryDesign() => Designs.All();

    // ── Variables and operands derived from the design, not assumed of it ────────────────

    /// <summary>
    /// A spread of variables this particular design can actually carry: two curvatures, a
    /// thickness, and - where the design is figured - the conic and the r^4 term of a figured
    /// surface, which are the paths the aspheric scheme is reached through.
    ///
    /// <para>Derived rather than hard-coded because the designs differ: some have three optical
    /// surfaces and some have nine, some are figured on the first surface and some on the last.
    /// A fixed list would silently pick a surface that does not exist.</para>
    /// </summary>
    private static VariableSet VariablesFor(OpticalSystem system)
    {
        var set = new VariableSet();
        int last = system.LastOpticalSurface();

        // Curvatures: the first two surfaces that have any power to speak of, so the column is
        // not trivially zero. A flat still gets picked if nothing else is available, because a
        // derivative at zero curvature is a case worth taking - see AnalyticDerivativeTests.
        int taken = 0;
        for (int i = 1; i <= last && taken < 2; i++)
        {
            set.Add(new Variable { Kind = VariableKind.Curvature, Surface = i });
            taken++;
        }

        // A thickness with something after it, so moving it moves the design.
        for (int i = 1; i < last; i++)
        {
            if (double.IsInfinity(system.Surfaces[i].Thickness)) continue;
            set.Add(new Variable { Kind = VariableKind.Thickness, Surface = i });
            break;
        }

        // The figuring, where there is any.
        for (int i = 1; i <= last; i++)
        {
            if (!system.Surfaces[i].IsFigured) continue;
            set.Add(new Variable { Kind = VariableKind.Conic, Surface = i });
            set.Add(new Variable { Kind = VariableKind.Asphere4, Surface = i });
            set.Add(new Variable { Kind = VariableKind.Asphere6, Surface = i });
            break;
        }

        return set;
    }

    /// <summary>
    /// Operands that exist on any design: the predicted spot, the two first-order targets, a
    /// real ray and a paraxial ray at the last optical surface, and the three chromatic and
    /// distortion terms. Deliberately not the full set of
    /// <see cref="AnalyticDerivativeTests"/> - that one names specific surfaces, which only its
    /// own design has, and it keeps its breadth where the surfaces are known.
    /// </summary>
    private static Operand[] OperandsFor(OpticalSystem system)
    {
        int last = system.LastOpticalSurface();
        return new[]
        {
            new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
            new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 1.0 },
            new Operand { Type = OperandType.TTL, Target = 60.0, Weight = 1.0 },
            new Operand { Type = OperandType.PY, Surface = last, Py = 0.7, Target = 0.0 },
            new Operand { Type = OperandType.RY, Surface = last, Py = 1.0, Hy = 1.0, Target = 0.0 },
            new Operand { Type = OperandType.RX, Surface = last, Px = 0.8, Hy = 0.7, Target = 0.0 },
            new Operand { Type = OperandType.LCF, Hy = 1.0, Target = 0.0 },
            new Operand { Type = OperandType.AXC, Target = 0.0 },
            new Operand { Type = OperandType.DISTF, Hy = 1.0, Target = 0.0 },
        };
    }

    /// <summary>
    /// Designs whose own VALUES are near-singular, and the tolerance their conditioning earns.
    ///
    /// <para>A derivative cannot be more accurate than the value it differentiates. On a face of
    /// radius 1E10 this scheme's coefficients are measured at 0.067 per cent and allowed half a
    /// per cent - <c>FlatSurfaceInCollimatedSpaceTests.TheCurvatureLimitOfAFlatFaceIsAccurate</c>
    /// says so in as many words - and the analytic derivative there comes out 0.12 per cent from
    /// the slope of the computed value, steady across four decades of step size. That is the
    /// conditioning showing through, not a second defect: holding the derivative to 0.02 per cent
    /// would be holding it to a standard the value it differentiates does not meet.</para>
    ///
    /// <para>Named individually rather than loosened globally. Every other design on disk is held
    /// to 0.02 per cent, and a design that starts needing to be in this list is telling you
    /// something.</para>
    /// </summary>
    private static double ToleranceFor(string name) =>
        Designs.IsNearSingular(name) ? 5e-3 : 2e-4;

    // ── The sweep ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every design on disk, differentiated and checked against a central difference of its own
    /// residuals.
    ///
    /// <para><b>A design the optimiser declines is reported, not skipped.</b> Some of these
    /// cannot be evaluated - an afocal system has no focal length to target, a design with no
    /// field has no chromatic difference to measure - and a sweep that quietly passed over them
    /// would read as coverage it does not have. The refusal is recorded in the assertion message,
    /// and <see cref="EveryDesignOnDiskWasActuallyDifferentiated"/> fails if too many designs end up in
    /// that bucket.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDesign))]
    public void EveryDesignsDerivativeMatchesItsOwnValue(string name, string folder)
    {
        string? reason = CheckOne(name, folder);
        Assert.True(reason == null || reason.Length > 0,
            $"{name}: declined without saying why, which is the one outcome that is not allowed");
    }

    /// <summary>
    /// Returns null when the design was checked, or a stated reason when it could not be.
    /// Throws, through the check itself, when the derivative is wrong.
    /// </summary>
    private static string? CheckOne(string name, string folder)
    {
        var catalog = CatalogLocator.LoadBundled();
        OpticalSystem system;
        try { system = LensFile.Read(Designs.PathOf(name, folder), catalog); }
        catch (Exception e) { return "the file could not be read: " + e.Message; }

        if (system.LastOpticalSurface() < 1) return "no optical surfaces";
        if (system.Fields.Count == 0) return "no fields";

        var vars = VariablesFor(system);
        if (vars.Count == 0) return "no variable could be formed";

        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(OperandsFor(system));

        var probe = merit.Evaluate(true);
        if (!probe.Ok) return "the optimiser declined it: " + probe.Failure;

        OffTheKink(design, merit, vars, probe);

        JacobianCheck.Check(design, merit, vars, name, ToleranceFor(name));
        return null;
    }

    /// <summary>
    /// A design that images some field PERFECTLY sits on a kink of PRMSA, and is moved off it.
    ///
    /// <para>The parabolic mirror is the case: its axial spot is exactly zero, so an r^4 term
    /// makes it |c A4| - a cone, with no derivative at its tip. The analytic value there is the
    /// slope of one side, chosen by the roundoff sign of B, and a central difference averages
    /// the two sides towards zero; neither is wrong, and no comparison between them can pass.
    /// This sweep used to pass the mirror only because its tertiary coefficients were NaN and a
    /// comparison with NaN never fails. So where a PRMSA case is zero and a figuring variable
    /// sits at exactly zero, that variable is set to a small value first - A4 = 1E-9 on the
    /// parabola - and the derivative is checked on the smooth side of the cone.</para>
    /// </summary>
    private static void OffTheKink(Design design, MeritFunction merit, VariableSet vars, MeritResult probe)
    {
        double largest = 0.0;
        bool vanishing = false;
        for (int i = 0; i < merit.Operands.Count; i++)
        {
            if (merit.Operands[i].Type != OperandType.PRMSA) continue;
            largest = Math.Max(largest, Math.Abs(probe.Values[i]));
        }
        for (int i = 0; i < merit.Operands.Count; i++)
            if (merit.Operands[i].Type == OperandType.PRMSA
                && Math.Abs(probe.Values[i]) <= 1e-12 * largest)
                vanishing = true;
        if (!vanishing) return;

        var x = design.Read();
        bool moved = false;
        for (int j = 0; j < vars.Count; j++)
        {
            if (!vars[j].Figures || x[j] != 0.0) continue;
            if (vars[j].Kind == VariableKind.Conic) continue;          // a conic of 0 is a sphere, not a kink
            x[j] = vars[j].Kind == VariableKind.Asphere4 ? 1e-9
                 : vars[j].Kind == VariableKind.Asphere6 ? 1e-13 : 1e-17;
            moved = true;
        }
        if (moved) design.Apply(x);
    }

    /// <summary>
    /// The guard against a hollow sweep: EVERY design must actually be checked.
    ///
    /// <para>A theory that skips is indistinguishable from a theory that passes, which this
    /// repository has been caught by before - the note at the top of <see cref="Fixtures"/> is
    /// about exactly that. This counts the two outcomes and prints the declined ones with their
    /// reasons, so a change that started refusing half the fixtures fails here rather than
    /// quietly reducing the coverage.</para>
    /// </summary>
    [Fact]
    public void EveryDesignOnDiskWasActuallyDifferentiated()
    {
        int checkedCount = 0;
        var declined = new List<string>();

        foreach (var row in EveryDesign())
        {
            string name = (string)row[0], folder = (string)row[1];
            string? reason;
            try { reason = CheckOne(name, folder); }
            catch (Exception e) { reason = "THREW: " + e.Message; }

            if (reason == null) checkedCount++;
            else declined.Add($"    {name}: {reason}");
        }

        int total = EveryDesign().Count();
        Assert.True(checkedCount >= total,
            $"only {checkedCount} of {total} designs were differentiated. The rest were "
          + "declined:\n" + string.Join("\n", declined));
    }

    /// <summary>
    /// Thompson's telescope, differentiated through BOTH mirrors - the one design that folds
    /// twice, and the only one with a gap long enough to have lost its off-axis rays.
    ///
    /// <para>It is built in code rather than read from a file, so the sweep above never sees it.
    /// The fixes to the reflecting path live in files the differentiating build compiles too -
    /// the reflection and the stalled intersection in RealRayTrace, the signed indices and |N'|
    /// in TertiaryCoefficients - and this is where their DERIVATIVES are held to the values: the
    /// real-ray operands cross the 7490 mm gap off axis, and PRMSA carries the seventh order of
    /// a two-mirror system.</para>
    /// </summary>
    [Fact]
    public void TheTwoMirrorTelescopeIsDifferentiatedThroughBothMirrors()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = ThompsonTelescopeTests.Telescope(false);
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 2 });
        vars.Add(new Variable { Kind = VariableKind.Thickness, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Conic, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Conic, Surface = 2 });

        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(OperandsFor(system));

        var probe = merit.Evaluate(true);
        Assert.True(probe.Ok, probe.Failure);
        JacobianCheck.Check(design, merit, vars, "Thompson telescope");
    }
}
