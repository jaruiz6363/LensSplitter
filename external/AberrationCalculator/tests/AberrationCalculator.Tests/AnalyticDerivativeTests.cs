using System;
using System.Collections.Generic;
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
/// The claim this optimiser rests on: every derivative it uses is analytic and exact.
///
/// <para>The only way to check that from outside is to compare it against the thing it replaces.
/// A central difference is accurate to about the two-thirds power of machine epsilon - some
/// eleven digits at a well-chosen step - so agreement to five or six digits across every
/// operand and every variable says the analytic derivative is right and the difference quotient
/// is the one carrying the error.</para>
///
/// <para>This is the test that guards the whole dual-number retrofit. If someone edits
/// Buchdahl's scheme, or the series trace, or the paraxial recurrence, and gets the arithmetic
/// right but the differentiation wrong, this is what says so.</para>
/// </summary>
public class AnalyticDerivativeTests
{
    private static (Design Design, MeritFunction Merit) Build(string lens, VariableSet vars,
                                                              IEnumerable<Operand> operands,
                                                              Action<OpticalSystem>? prepare = null)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        prepare?.Invoke(system);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(operands);
        return (design, merit);
    }

    private static VariableSet TripletVariables() => Spread(new[]
    {
        new Variable { Kind = VariableKind.Curvature, Surface = 1 },
        new Variable { Kind = VariableKind.Curvature, Surface = 2 },
        new Variable { Kind = VariableKind.Curvature, Surface = 4 },
        new Variable { Kind = VariableKind.Thickness, Surface = 2 },
        new Variable { Kind = VariableKind.Thickness, Surface = 6 },
    });

    private static VariableSet Spread(IEnumerable<Variable> vs)
    {
        var set = new VariableSet();
        set.AddRange(vs);
        return set;
    }

    /// <summary>
    /// Every operand this optimiser offers, on a design that exercises all of them.
    /// </summary>
    private static Operand[] EveryOperand() => new[]
    {
        new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
        new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 1.0 },
        new Operand { Type = OperandType.TTL, Target = 60.0, Weight = 1.0 },

        new Operand { Type = OperandType.PY, Surface = 4, Py = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.PX, Surface = 3, Px = 0.5, Target = 0.0 },
        new Operand { Type = OperandType.PM, Surface = 5, Py = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.PN, Surface = 5, Py = 1.0, Target = 1.0 },
        new Operand { Type = OperandType.PL, Surface = 4, Px = 0.6, Target = 0.0 },

        new Operand { Type = OperandType.RY, Surface = 7, Py = 1.0, Hy = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.RX, Surface = 4, Px = 0.8, Hy = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.RZ, Surface = 3, Py = 0.9, Target = 0.0 },
        new Operand { Type = OperandType.RM, Surface = 6, Py = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.RN, Surface = 2, Py = 0.5, Target = 1.0 },
        new Operand { Type = OperandType.RL, Surface = 2, Px = 0.5, Target = 0.0 },

        new Operand { Type = OperandType.LCF, Hy = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.AXC, Target = 0.0 },
        new Operand { Type = OperandType.DISTF, Hy = 1.0, Target = 0.0 },

        // The as-built term. Its derivative runs through the paraxial recurrence and the
        // curvatures only - no coefficients and no rays - so it exercises a path none of the
        // others take, and Gu's tolerances from his own Cooke triplet run are what it is given.
        new Operand { Type = OperandType.ASBLT, Decentre = 0.0399, Tilt = 9.176 / 60.0, Target = 0.0 },

        // Boundary operands are deliberately given limits the design VIOLATES, so that the
        // residual is live and its derivative is not trivially zero. A satisfied boundary has
        // no gradient by design, and testing that would test nothing.
        new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = 50.0 },
        new Operand { Type = OperandType.EAT, Surface = 1, Surface2 = 6, Min = 50.0 },
        new Operand { Type = OperandType.DTRGT, Surface = 1, Surface2 = 6, Max = 0.001 },
    };

    [Fact]
    public void AnalyticJacobianMatchesCentralDifferences_SphericalTriplet()
    {
        // Spherical throughout, which is the only kind this optimiser accepts.
        var vars = TripletVariables();
        var (design, merit) = Build("CookeTriplet", vars, EveryOperand());
        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// A PLANE surface being bent keeps its full gradient.
    ///
    /// <para>This is the case that makes the zero-tests in the chain worth naming rather than
    /// spelling out as <c>== 0.0</c>. A plane has zero curvature, so the conic term of its sag is
    /// skipped - correctly, the term is zero. But when that curvature is a VARIABLE the term has
    /// value nothing and derivative <c>r^2/2</c>, and skipping it would leave the sag right while
    /// the gradient went short: the merit function stays correct and only the direction the
    /// search walks in goes wrong, which is a fault nothing announces.</para>
    ///
    /// <para>Surface 2 of the triplet is flattened here so that its curvature starts at exactly
    /// zero, and then every operand is checked against a central difference as usual. It is
    /// deliberately checked with the SAG-dependent operands present - edge thicknesses and real
    /// rays - because those are the ones that read a sag at all.</para>
    /// </summary>
    [Fact]
    public void ACurvatureVariableStartingAtExactlyZeroKeepsItsGradient()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Curvature, Surface = 2 },   // the plane
            new Variable { Kind = VariableKind.Curvature, Surface = 4 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand(),
                                    s => s.Surfaces[2].Curvature = 0.0);

        Assert.Equal(0.0, design.System.Surfaces[2].Curvature);
        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// <b>A figuring variable that starts at exactly zero keeps its gradient.</b> This is the
    /// case a designer actually meets: a spherical surface, a conic declared variable, and the
    /// optimiser asked to find out whether figuring it helps. The conic is 0 on the first
    /// evaluation.
    ///
    /// <para>It is the same hazard as
    /// <see cref="ACurvatureVariableStartingAtExactlyZeroKeepsItsGradient"/> and it bites harder.
    /// Whether the aspheric block runs at all is decided by asking whether the figuring is
    /// non-zero, and a magnitude test answers that from the VALUE alone - so a conic of 0 with
    /// derivative 1 reads as a sphere, the whole aspheric arrangement is skipped, and every
    /// coefficient comes back with a correct value and a zero derivative. The merit function
    /// would be right, the gradient would be silently short, and the optimiser would report that
    /// figuring the surface does not help because it could not see that it would.</para>
    ///
    /// <para>The fix is <c>SMath.Vanishes</c>, which is the same comparison in <c>double</c> and
    /// additionally asks about the derivative in the dual build. A structural zero has no
    /// derivative either, so the sparse skips that keep the chain fast are untouched.</para>
    /// </summary>
    [Fact]
    public void AFiguringVariableStartingAtExactlyZeroKeepsItsGradient()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Conic, Surface = 1 },
            new Variable { Kind = VariableKind.Asphere4, Surface = 2 },
            new Variable { Kind = VariableKind.Curvature, Surface = 4 },
        });

        // Every surface spherical, and left that way: the figuring variables start at zero.
        var (design, merit) = Build("CookeTriplet", vars, EveryOperand());

        Assert.Equal(0.0, design.System.Surfaces[1].Conic);
        Assert.Equal(0.0, design.System.Surfaces[2].AsphericCoefficients[1]);
        Assert.False(design.System.Surfaces[1].IsFigured);

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// The same for r^6 and r^8, which enter the arrangement through different terms than r^4
    /// does and are skipped by their own test.
    /// </summary>
    [Fact]
    public void HigherAsphericVariablesStartingAtZeroKeepTheirGradients()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Asphere6, Surface = 2 },
            new Variable { Kind = VariableKind.Asphere8, Surface = 2 },
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand());
        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// <b>A figured design is optimised, and every derivative through the aspheric arrangement
    /// is exact.</b> This is the test the figuring variables exist for.
    ///
    /// <para>It used to assert the opposite - that a figured design was refused - and the
    /// refusal was right while Buchdahl's aspheric seventh order was a reconstruction the rays
    /// rejected. That arrangement is now established (<c>docs/verification.md</c>), so the
    /// design runs, and what has to be checked is no longer that it is declined but that the
    /// whole chain differentiates: the aspheric increments, the dual run that supplies the sixth
    /// barred member, the trivariate figuring cubics, and the tertiary that comes out of them.
    /// Every one of those is on the path from a conic to PRMSA and none of them was ever
    /// exercised by a derivative before.</para>
    /// </summary>
    [Fact]
    public void AnalyticJacobianMatchesCentralDifferences_FiguredTriplet()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Curvature, Surface = 4 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand(),
                                    s => s.Surfaces[1].Conic = -0.42);

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// The figuring itself as the variable, which is the case the aspheric increments are
    /// differentiated for. A conic and an r^4 term on different surfaces, so that the induced
    /// terms carrying one into the other are live.
    /// </summary>
    [Fact]
    public void AnalyticJacobianMatchesCentralDifferences_FiguringIsTheVariable()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Conic, Surface = 1 },
            new Variable { Kind = VariableKind.Asphere4, Surface = 4 },
            new Variable { Kind = VariableKind.Curvature, Surface = 2 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand(), s =>
        {
            s.Surfaces[1].Conic = -0.42;
            s.Surfaces[4].AsphericCoefficients[1] = 1.5e-6;
        });

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// The r^6 and r^8 terms too, and on a surface that is otherwise a plain sphere - so the
    /// figuring being differentiated is the whole of what makes the surface aspheric.
    /// </summary>
    [Fact]
    public void AnalyticJacobianMatchesCentralDifferences_HigherAsphericTerms()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Asphere4, Surface = 2 },
            new Variable { Kind = VariableKind.Asphere6, Surface = 2 },
            new Variable { Kind = VariableKind.Asphere8, Surface = 2 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand(), s =>
        {
            s.Surfaces[2].AsphericCoefficients[1] = 2.0e-6;
            s.Surfaces[2].AsphericCoefficients[2] = -3.0e-9;
            s.Surfaces[2].AsphericCoefficients[3] = 5.0e-12;
        });

        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// A spherical design is unchanged by any of this. The routing sends spheres down
    /// Buchdahl's own published table exactly as before, so the one thing that must NOT have
    /// moved is the answer on a lens with no figuring in it.
    /// </summary>
    [Fact]
    public void ASphericalDesignStillTakesTheSphericalRoute()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

        var design = new Design(system, catalog, TripletVariables());
        var merit = new MeritFunction(design);
        merit.AddRange(EveryOperand());

        var r = merit.Evaluate(true);
        Assert.True(r.Ok, r.Failure);

        // No surface acquired figuring by being looked at.
        for (int i = 1; i <= system.LastOpticalSurface(); i++)
            Assert.False(system.Surfaces[i].IsFigured);
    }

    /// <summary>
    /// Compares the analytic Jacobian with a central difference of the same residuals. The body
    /// now lives in <see cref="JacobianCheck"/>, so that the sweep over every design on disk
    /// runs the identical check rather than a second copy of it.
    /// </summary>
    private static void CheckJacobian(Design design, MeritFunction merit, VariableSet vars)
        => JacobianCheck.Check(design, merit, vars);

    /// <summary>
    /// A satisfied boundary operand costs nothing and pulls on nothing.
    ///
    /// <para>This is what lets a design carry a dozen manufacturability limits without any of
    /// them bending the solution until one is actually threatened, so it is worth stating as a
    /// test rather than leaving as a remark in a comment.</para>
    /// </summary>
    [Fact]
    public void SatisfiedBoundaryOperandHasNoResidualAndNoGradient()
    {
        var vars = TripletVariables();
        var (design, merit) = Build("CookeTriplet", vars, new[]
        {
            new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = -1000.0 },
            new Operand { Type = OperandType.DTRGT, Surface = 1, Surface2 = 6, Max = 1e9 },
        });

        var r = merit.Evaluate(true);
        Assert.True(r.Ok, r.Failure);
        Assert.True(merit.Operands.Count > 0);

        Assert.All(r.Residuals, v => Assert.Equal(0.0, v));
        for (int i = 0; i < merit.Operands.Count; i++)
            for (int j = 0; j < vars.Count; j++)
                Assert.Equal(0.0, r.Jacobian[i, j]);
        Assert.Equal(0.0, r.SumSquares);
    }

    /// <summary>
    /// PRMSA as the optimiser computes it is PRMSA as the report prints it.
    ///
    /// <para>The optimiser reaches it through the dual-number build of the chain and the report
    /// through the ordinary one. They are the same source compiled twice, so the two ought to
    /// agree to the last bit - and if they ever do not, the optimiser is descending a quantity
    /// the program does not report.</para>
    /// </summary>
    [Fact]
    public void PrmsaAgreesWithTheReport()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        var system = LensFile.Read(path, catalog);

        var writer = new Core.Report.ReportWriter(system, catalog, path);
        string tsv = writer.BuildPrmsTsv();
        string last = tsv.TrimEnd('\n', '\r').Split('\n').Last();
        double reported = double.Parse(last.Split('\t').Last(),
                                       System.Globalization.CultureInfo.InvariantCulture);

        var design = new Design(system, catalog, new VariableSet());
        var merit = new MeritFunction(design);
        merit.Add(new Operand { Type = OperandType.PRMSA, Target = 0.0 });

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        // PRMSA is posed to the optimiser as one residual per field and wavelength, so the
        // aggregate the report prints is not any single value - it is what they come to
        // together. Reassembling it here is the whole claim: the quantity being minimised is
        // still, exactly, the number the report shows.
        //
        // residual = sqrt(w)*(v - 0), and the sub-weights were chosen so that
        // sum(residual^2) = W * PRMSA^2. So PRMSA = sqrt(sum(residual^2) / W).
        double squares = 0.0;
        foreach (double residual in r.Residuals) squares += residual * residual;

        double declared = 1.0;                     // the weight the operand was declared with
        Assert.Equal(reported, Math.Sqrt(squares / declared), 12);

        // And every sub-operand is one row of the report's own table.
        Assert.Equal(system.Wavelengths.Count * system.Fields.Count, r.Values.Length);
    }
}
