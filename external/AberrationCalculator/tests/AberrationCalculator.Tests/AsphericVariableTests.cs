using System;
using System.IO;
using System.Linq;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The conic and even-asphere variables: that they read, write, file, copy and round-trip.
///
/// <para><b>The DERIVATIVE of a figuring variable is not tested here</b>, and that is placement
/// rather than omission. <c>AnalyticDerivativeTests</c> compares every analytic derivative
/// against a central difference, operand by operand and variable by variable, and its figured
/// cases go through the same harness as the spherical ones - which is where a derivative
/// belongs, beside the others it has to agree with. This file is about the plumbing: that a
/// conic reads, writes, folds inside a bound, survives a save and a reopen, and is not shared
/// between two copies of a design.</para>
/// </summary>
public class AsphericVariableTests
{
    private sealed class Scratch : IDisposable
    {
        public string Dir { get; }
        public Scratch()
        {
            Dir = Path.Combine(Path.GetTempPath(), "abcalc-asph-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
        }
        public string Copy(string source, string name)
        {
            string path = Path.Combine(Dir, name);
            File.Copy(source, path);
            return path;
        }
        public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
    }

    private static OpticalSystem Triplet()
    {
        var catalog = CatalogLocator.LoadBundled();
        return LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
    }

    /// <summary>
    /// A conic and the three even-asphere terms go into a design and come back out. The r^4,
    /// r^6 and r^8 kinds have to land in slots 1, 2 and 3 of the coefficient array - slot 0 is
    /// the r^2 term, which is folded into the vertex curvature before any coefficient is
    /// computed, so a variable landing there would silently duplicate the curvature variable.
    /// </summary>
    [Fact]
    public void FiguringVariablesReadAndWriteThroughADesign()
    {
        var system = Triplet();
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Conic, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Asphere4, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Asphere6, Surface = 2 });
        vars.Add(new Variable { Kind = VariableKind.Asphere8, Surface = 3 });

        var x = new[] { -0.75, 1.5e-6, -2.5e-9, 3.5e-12 };
        vars.Write(system, x);

        Assert.Equal(-0.75, system.Surfaces[1].Conic);
        Assert.Equal(1.5e-6, system.Surfaces[1].AsphericCoefficients[1]);
        Assert.Equal(-2.5e-9, system.Surfaces[2].AsphericCoefficients[2]);
        Assert.Equal(3.5e-12, system.Surfaces[3].AsphericCoefficients[3]);

        // The r^2 slot is untouched, which is the whole point of starting at index 1.
        Assert.Equal(0.0, system.Surfaces[1].AsphericCoefficients[0]);

        Assert.Equal(x, vars.Read(system));
    }

    /// <summary>
    /// Writing a figuring variable makes the surface figured, which is what sends the tertiary
    /// down the aspheric arrangement. Stated as a test because it is the whole reason these
    /// variables needed the arrangement to be established first.
    /// </summary>
    [Fact]
    public void AConicVariableTurnsASphericalSurfaceIntoAFiguredOne()
    {
        var system = Triplet();
        Assert.False(system.Surfaces[1].IsFigured);

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Conic, Surface = 1 });
        vars.Write(system, new[] { -1.0 });

        Assert.True(system.Surfaces[1].IsFigured);
    }

    /// <summary>
    /// A bound is enforced on a figuring variable exactly as on a curvature: by reflection, and
    /// written back into the caller's vector so that the design and the optimiser cannot
    /// disagree about where it is standing.
    /// </summary>
    [Fact]
    public void AConicVariableIsFoldedInsideItsBounds()
    {
        var system = Triplet();
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Conic, Surface = 1, Min = -1.0, Max = 0.0 });

        var x = new[] { -1.25 };          // a quarter past the lower limit
        vars.Write(system, x);

        Assert.Equal(-0.75, x[0], 12);                       // folded back inside
        Assert.Equal(-0.75, system.Surfaces[1].Conic, 12);   // and the design agrees
    }

    /// <summary>
    /// A .lhlt already carried ConicVariable and AsphericVariable - it deserialized them and
    /// wrote them back so a file would not lose them - but nothing ever surfaced them as
    /// variables. This is that path, end to end: declare them, save, reopen, and find them.
    /// </summary>
    [Fact]
    public void FiguringVariablesSurviveALhltRoundTrip()
    {
        using var scratch = new Scratch();
        string path = scratch.Copy(Fixtures.Lens("CookeTriplet"), "figured.lhlt");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(path, catalog);

        var declared = new VariableSet();
        declared.Add(new Variable { Kind = VariableKind.Conic, Surface = 1 });
        declared.Add(new Variable { Kind = VariableKind.Asphere4, Surface = 2 });
        declared.Add(new Variable { Kind = VariableKind.Asphere8, Surface = 5 });
        SurfaceVariables.Write(declared, system);

        LhltPatcher.Patch(system, path, path);

        var reopened = LensFile.Read(path, catalog);
        var back = SurfaceVariables.Read(reopened);

        Assert.Equal(3, back.Count);
        Assert.Contains(back.Items, v => v.Kind == VariableKind.Conic && v.Surface == 1);
        Assert.Contains(back.Items, v => v.Kind == VariableKind.Asphere4 && v.Surface == 2);
        Assert.Contains(back.Items, v => v.Kind == VariableKind.Asphere8 && v.Surface == 5);
    }

    /// <summary>
    /// <b>An optimised aspheric coefficient has to reach the file.</b> The .lhlt patcher wrote
    /// Radius, Thickness, Conic and Material back but never AsphericCoefficients, which was
    /// harmless for exactly as long as nothing could change one. With r^4 a variable, a design
    /// would otherwise have been written back carrying the figuring it started with - no error
    /// anywhere, and the result of the run quietly discarded.
    /// </summary>
    [Fact]
    public void AnOptimisedAsphericCoefficientIsWrittenBackToTheFile()
    {
        using var scratch = new Scratch();
        string path = scratch.Copy(Fixtures.Lens("CookeTriplet"), "moved.lhlt");

        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(path, catalog);

        system.Surfaces[2].AsphericCoefficients[1] = 4.25e-7;
        system.Surfaces[2].Conic = -0.6;
        LhltPatcher.Patch(system, path, path);

        var reopened = LensFile.Read(path, catalog);
        Assert.Equal(4.25e-7, reopened.Surfaces[2].AsphericCoefficients[1], 15);
        Assert.Equal(-0.6, reopened.Surfaces[2].Conic, 12);
    }

    /// <summary>
    /// A deep copy shares no figuring with the design it came from. The basin hopping runs
    /// several chains at once and every optimiser here can hand back the design it started
    /// from, so a shared flag array would let one chain's conic appear in another's lens - the
    /// kind of fault that shows up only as a parallel run disagreeing with a serial one.
    /// </summary>
    [Fact]
    public void ADeepCopySharesNoFiguringWithItsOriginal()
    {
        var system = Triplet();
        system.Surfaces[1].ConicVariable = true;
        system.Surfaces[1].AsphericVariable[1] = true;

        var copy = DesignCopy.Deep(system);

        Assert.True(copy.Surfaces[1].ConicVariable);
        Assert.True(copy.Surfaces[1].AsphericVariable[1]);

        copy.Surfaces[1].AsphericVariable[1] = false;
        copy.Surfaces[1].AsphericCoefficients[1] = 9.9e-7;
        copy.Surfaces[1].Conic = -3.0;

        Assert.True(system.Surfaces[1].AsphericVariable[1]);
        Assert.Equal(0.0, system.Surfaces[1].AsphericCoefficients[1]);
        Assert.NotEqual(-3.0, system.Surfaces[1].Conic);
    }
    /// <summary>

    /// A figuring variable is ACCEPTED. It was refused until the evaluation loop was routed to
    /// the aspheric tertiary, and this is the assertion that the routing happened - the test
    /// that the derivative through it is exact lives in
    /// <c>AnalyticDerivativeTests.AnalyticJacobianMatchesCentralDifferences_FiguringIsTheVariable</c>.
    /// </summary>
    [Theory]
    [InlineData(VariableKind.Conic)]
    [InlineData(VariableKind.Asphere4)]
    [InlineData(VariableKind.Asphere6)]
    [InlineData(VariableKind.Asphere8)]
    public void AFiguringVariableIsAccepted(VariableKind kind)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = Triplet();

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        vars.Add(new Variable { Kind = kind, Surface = 2 });

        var design = new Design(system, catalog, vars);

        Assert.Equal(2, design.Variables.Count);
        Assert.Equal(kind, design.Variables[1].Kind);
    }

    /// <summary>Curvature and thickness are untouched by any of this.</summary>
    [Fact]
    public void TheOrdinaryVariablesStillWork()
    {
        var catalog = CatalogLocator.LoadBundled();
        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });
        vars.Add(new Variable { Kind = VariableKind.Thickness, Surface = 2 });

        var design = new Design(Triplet(), catalog, vars);

        Assert.Equal(2, design.Variables.Count);
        Assert.Equal("CV1", design.Variables[0].Name);
        Assert.Equal("TH2", design.Variables[1].Name);
    }

    /// <summary>
    /// A figured flat facing collimated light used to be refused here, and is not any more: the
    /// series route that reaches its coefficients now exists in the differentiating build too.
    /// That design's acceptance, the value it produces and the derivatives through it are tested
    /// in <see cref="FlatCollimatedDerivativeTests"/>, beside the arithmetic that made it
    /// possible. What remains in this file is the ordinary aspheric case.
    /// </summary>
    [Fact]
    public void ANearlyFlatFiguredSurfaceIsAccepted()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("Ladder2_FiguredNearFlatRear"), catalog);

        var vars = new VariableSet();
        vars.Add(new Variable { Kind = VariableKind.Curvature, Surface = 1 });

        var design = new Design(system, catalog, vars);
        Assert.Single(design.Variables.Items);
    }
}
