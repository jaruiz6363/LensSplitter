using AberrationCalculator.Core.Models;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Splitting;
using Xunit;

namespace LensSplitter.Tests;

public class SplitGeometryTests
{
    private static SplitLens Split(OpticalSystem lens, int element, SplitShape shape)
    {
        var o = new LensOptics(lens, Lenses.Catalog);
        var e = LensElements.Find(lens)[element];
        double clear = Splitter.ClearSemiDiameter(lens, o.Paraxial, e);
        return SplitGeometry.HoldFocalLength(lens, e, o.PrimaryIndices, LensElements.Power(lens, e, o.PrimaryIndices),
                                             clear, shape, new SplitLimits(), o.Efl)!;
    }

    [Fact]
    public void TheSplitAddsTwoSurfacesKeepsTheFocalLengthAndMovesNothingAfterIt()
    {
        var lens = Lenses.Cooke();
        var s = Split(lens, 2, new SplitShape(0.5, 0.5, 0.5, 0.5, 1.0));
        Assert.Equal(lens.Surfaces.Count + 2, s.System.Surfaces.Count);
        Assert.Equal(new LensOptics(lens, Lenses.Catalog).Efl, new LensOptics(s.System, Lenses.Catalog).Efl, 9);

        // Everything after the split - here the image - is where it was.
        Assert.Equal(Lenses.Positions(lens)[^1], Lenses.Positions(s.System)[^1], 9);
        // The glass is the element's in both halves, with air between.
        Assert.Equal("SK16", s.System.Surfaces[5].Material);
        Assert.Null(s.System.Surfaces[6].Material);
        Assert.Equal("SK16", s.System.Surfaces[7].Material);
    }

    [Fact]
    public void TheOuterFacesKeepTheirFiguring()
    {
        var lens = Lenses.FiguredSinglet();
        var s = Split(lens, 0, new SplitShape(0.5, 0.5, 0.5, 0.5, 1.0));
        var front = s.System.Surfaces[1];
        Assert.Equal(-0.6, front.Conic, 12);
        Assert.Equal(lens.Surfaces[1].AsphericCoefficients, front.AsphericCoefficients);
        Assert.True(front.IsStop);
        // The new faces are spheres.
        Assert.Equal(0.0, s.System.Surfaces[2].Conic);
        Assert.All(s.System.Surfaces[2].AsphericCoefficients, c => Assert.Equal(0.0, c));
    }

    /// <summary>An r^2 term is a curvature change: the face's VERTEX curvature is the one the bending asks for.</summary>
    [Fact]
    public void AnR2TermCountsInTheVertexCurvature()
    {
        var lens = Lenses.R2Singlet();
        var s = Split(lens, 0, new SplitShape(0.5, 0.5, 0.5, 0.5, 1.0));
        var front = s.System.Surfaces[1];
        Assert.Equal(1e-4, front.AsphericCoefficients[0]);
        Assert.Equal(front.Curvature + 2e-4, front.VertexCurvature, 12);
        Assert.Equal(new LensOptics(lens, Lenses.Catalog).Efl, new LensOptics(s.System, Lenses.Catalog).Efl, 9);
    }

    [Fact]
    public void ASplitThatDoesNotFitIsRefused()
    {
        var lens = Lenses.Cooke();
        var o = new LensOptics(lens, Lenses.Catalog);
        var e = LensElements.Find(lens)[0];
        // Borrowing far more length than the air after the element has.
        var s = SplitGeometry.Build(lens, e, o.PrimaryIndices, LensElements.Power(lens, e, o.PrimaryIndices), 8.0,
                                    new SplitShape(0.5, 0.5, 0.5, 0.5, 100.0), new SplitLimits());
        Assert.Null(s);
    }
}
