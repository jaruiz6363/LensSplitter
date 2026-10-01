using System;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The alignment sidecar: how one BUILT instance sits relative to the axis.
///
/// <para>The property that matters most here is a negative one - that none of this ever reaches
/// the prescription. A perturbation is a statement about a workshop, not about a design, and a
/// build error written into a lens file is a corrupted design record.</para>
/// </summary>
public class AlignmentFileTests
{
    [Fact]
    public void ATiltAndADecentreOnOneSurfaceMerge()
    {
        var spec = AlignmentFile.Parse(new[] { "TILT 2 X 0.115", "DEC 2 Y 0.05" });

        Assert.Single(spec.Perturbations);
        var p = spec.Perturbations[0];
        Assert.Equal(2, p.Surface);
        Assert.Equal(0.115, p.TiltX, 12);
        Assert.Equal(0.05, p.DecenterY, 12);
        Assert.Equal(0.0, p.TiltY, 12);
        Assert.Equal(0.0, p.DecenterX, 12);
    }

    /// <summary>
    /// <c>FREE</c> drops the kind it is written on and leaves the other alone. Clearing a tilt
    /// and silently losing the decentre with it would be the worst kind of quiet wrong.
    /// </summary>
    [Fact]
    public void FreeDropsOnlyItsOwnKind()
    {
        var spec = AlignmentFile.Parse(new[]
        {
            "TILT 2 X 0.115",
            "DEC 2 Y 0.05",
            "TILT 2 FREE",
        });

        var p = Assert.Single(spec.Perturbations);
        Assert.Equal(0.0, p.TiltX, 12);
        Assert.Equal(0.05, p.DecenterY, 12);
    }

    /// <summary>A surface freed of everything is not worth keeping, and is not kept.</summary>
    [Fact]
    public void ASurfaceFreedOfEverythingDisappears()
    {
        var spec = AlignmentFile.Parse(new[] { "TILT 2 X 0.1", "TILT 2 FREE" });
        Assert.True(spec.IsEmpty);
    }

    /// <summary>What is written reads back as the same thing - the sidecar round-trips.</summary>
    [Fact]
    public void TheFileRoundTrips()
    {
        var written = AlignmentFile.Parse(new[]
        {
            "TILT 2 X 0.115 Y -0.02",
            "DEC 4 X 0.01 Y 0.05",
        });

        var read = AlignmentFile.Parse(AlignmentFile.Write(written).Split('\n'));

        Assert.Equal(2, read.Perturbations.Count);
        foreach (var a in written.Perturbations)
        {
            var b = read.Perturbations.Find(x => x.Surface == a.Surface);
            Assert.NotNull(b);
            Assert.Equal(a.TiltX, b!.TiltX, 15);
            Assert.Equal(a.TiltY, b.TiltY, 15);
            Assert.Equal(a.DecenterX, b.DecenterX, 15);
            Assert.Equal(a.DecenterY, b.DecenterY, 15);
        }
    }

    /// <summary>
    /// The file speaks DEGREES and the model carries RADIANS, because every lens format states a
    /// tilt in degrees and the theory wants radians. The conversion happens once, here.
    /// </summary>
    [Fact]
    public void DegreesInTheFileBecomeRadiansOnTheModel()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

        AlignmentFile.Parse(new[] { "TILT 2 X 0.115" }).ApplyTo(sys);

        Assert.Equal(0.115 * Math.PI / 180.0, sys.Surfaces[2].TiltX, 15);
        Assert.True(sys.Surfaces[2].IsPerturbed);
    }

    /// <summary>A surface the design does not have is an error, not a silent no-op.</summary>
    [Fact]
    public void ASurfaceTheDesignDoesNotHaveIsRefused()
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);

        var ex = Assert.Throws<FormatException>(
            () => AlignmentFile.Parse(new[] { "TILT 99 X 0.1" }).ApplyTo(sys));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void TheObjectSurfaceCannotBePerturbed()
    {
        Assert.Throws<FormatException>(() => AlignmentFile.Parse(new[] { "TILT 0 X 0.1" }));
    }

    [Fact]
    public void AnUnknownKeywordSaysWhatItExpected()
    {
        var ex = Assert.Throws<FormatException>(() => AlignmentFile.Parse(new[] { "WOBBLE 2 X 1" }));
        Assert.Contains("TILT", ex.Message);
    }

    /// <summary>
    /// <b>The negative property, and the reason this file exists at all.</b> Applying an
    /// alignment must not touch anything the lens file records - not a curvature, not a
    /// thickness, not a glass. If it did, a <c>--save</c> would write a build error into the
    /// prescription and the design record would be wrong from then on.
    /// </summary>
    [Fact]
    public void ApplyingAnAlignmentChangesNothingThePrescriptionRecords()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var nominal = LensFile.Read(path, catalog);
        var built = LensFile.Read(path, catalog);

        AlignmentFile.Parse(new[] { "TILT 2 X 0.115", "DEC 4 Y 0.05" }).ApplyTo(built);

        for (int i = 0; i < nominal.Surfaces.Count; i++)
        {
            Assert.Equal(nominal.Surfaces[i].Curvature, built.Surfaces[i].Curvature, 15);
            Assert.Equal(nominal.Surfaces[i].Thickness, built.Surfaces[i].Thickness, 15);
            Assert.Equal(nominal.Surfaces[i].Conic, built.Surfaces[i].Conic, 15);
            Assert.Equal(nominal.Surfaces[i].SemiDiameter, built.Surfaces[i].SemiDiameter, 15);
            Assert.Equal(nominal.Surfaces[i].Material, built.Surfaces[i].Material);
        }
    }

    /// <summary>
    /// And the corollary: the rotationally symmetric analysis does not see them either. The
    /// paraxial trace of a perturbed system is the paraxial trace of the design, to the bit,
    /// because nodal aberration theory treats the perturbation as a departure FROM that trace
    /// rather than as something the trace should carry.
    /// </summary>
    [Fact]
    public void TheParaxialTraceIsUnchangedByAnAlignment()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");

        var nominal = LensFile.Read(path, catalog);
        var built = LensFile.Read(path, catalog);
        AlignmentFile.Parse(new[] { "TILT 2 X 0.115", "DEC 4 Y 0.05" }).ApplyTo(built);

        var n = IndexResolver.Build(nominal, catalog, 0.55, new System.Collections.Generic.List<string>());

        double field = 0.0;
        foreach (var f in nominal.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var a = Core.RayTrace.ParaxialTrace.Trace(nominal, n, field);
        var b = Core.RayTrace.ParaxialTrace.Trace(built, n, field);

        Assert.Equal(a.Efl, b.Efl);
        Assert.Equal(a.LagrangeInvariant, b.LagrangeInvariant);
        for (int i = 0; i < a.Y.Length; i++)
        {
            Assert.Equal(a.Y[i], b.Y[i]);
            Assert.Equal(a.Ybar[i], b.Ybar[i]);
        }
    }

    /// <summary>
    /// <b>The sidecar is named for the lens INCLUDING its extension</b>, which is the rule the
    /// merit function and variable files follow.
    ///
    /// <para>It once dropped the extension, so <c>triplet.zmx</c> and <c>triplet.seq</c> in one
    /// folder shared a single <c>triplet.align</c> - two different designs, one record of how
    /// the workshop had built them. <c>Sidecar</c> states that reason for the other two; it
    /// applies here for exactly the same reason.</para>
    /// </summary>
    [Theory]
    [InlineData("triplet.zmx", "triplet.zmx.align")]
    [InlineData("triplet.seq", "triplet.seq.align")]
    [InlineData("triplet.lhlt", "triplet.lhlt.align")]
    [InlineData("no-extension", "no-extension.align")]
    public void TheSidecarKeepsTheLensExtension(string lens, string expected)
    {
        Assert.Equal(expected, System.IO.Path.GetFileName(AlignmentFile.PathFor(lens)));
    }

    /// <summary>
    /// And two lenses differing only in extension get two files, which is the whole point.
    /// </summary>
    [Fact]
    public void TwoLensesInOneFolderDoNotShareAnAlignmentFile()
    {
        Assert.NotEqual(AlignmentFile.PathFor("triplet.zmx"), AlignmentFile.PathFor("triplet.seq"));
    }
}
