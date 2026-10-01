using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AberrationCalculator.Tests;

/// <summary>
/// Every optical design this repository keeps, discovered rather than listed.
///
/// <para>Two folders hold them: <c>fixtures/lenses</c>, which the aberration tests read, and
/// <c>fixtures/coefficient-reference</c>, which holds the .zmx ones including the E family -
/// curved end surfaces, an r-squared term, immersed object and image spaces, model glasses, a
/// figured design at a finite conjugate. The awkward cases live in the second folder on purpose.</para>
///
/// <para><b>One discovery, used by both sweeps.</b> A second copy of this list is a second thing
/// to keep in step, and a design missing from one copy is invisible precisely where it matters.
/// The sweeps differ in what they ask of a design, not in which designs they ask.</para>
/// </summary>
internal static class Designs
{
    public static string LensDir =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "lenses");

    public static string ZmxDir =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "coefficient-reference");

    /// <summary>Every design on disk, as (file name, folder), in a stable order.</summary>
    public static IEnumerable<object[]> All()
    {
        foreach (string f in Directory.GetFiles(LensDir, "*.lhlt").OrderBy(f => f))
            yield return new object[] { Path.GetFileName(f), "lenses" };
        foreach (string f in Directory.GetFiles(ZmxDir, "*.zmx").OrderBy(f => f))
            yield return new object[] { Path.GetFileName(f), "coefficient-reference" };
    }

    public static string PathOf(string name, string folder) =>
        Path.Combine(folder == "lenses" ? LensDir : ZmxDir, name);

    /// <summary>
    /// Designs whose own arithmetic is near-singular, and so cannot be held to the accuracy the
    /// rest are.
    ///
    /// <para>A face of radius 1E10, or a figured flat facing collimated light, sits where the
    /// ordinary chain divides by a vanishing incidence.
    /// <c>FlatSurfaceInCollimatedSpaceTests.TheCurvatureLimitOfAFlatFaceIsAccurate</c> measures
    /// the coefficients there at 0.067 per cent and allows half a per cent, and nothing computed
    /// from them - a derivative, a second route's version of the same number - can be better than
    /// that.</para>
    ///
    /// <para>Named by the shape of the name rather than listed one by one, so that the next
    /// near-flat rung added to the ladder is covered without anyone remembering to add it here.</para>
    /// </summary>
    public static bool IsNearSingular(string name) =>
        name.Contains("NearLimit") || name.Contains("NearFlat") || name.Contains("FlatFigured");
}
