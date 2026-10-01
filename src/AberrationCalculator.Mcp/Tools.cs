using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

[assembly: InternalsVisibleTo("AberrationCalculator.Tests")]

namespace AberrationCalculator.Mcp;

/// <summary>
/// What the server offers, and what each one does.
///
/// <para>Every tool takes a lens file and returns text, because that is what this program
/// produces: a report meant to be read, and tab-separated tables meant to be parsed. The
/// division is deliberate and is kept here rather than collapsed - a caller that wants to
/// reason about one number should ask for the table, not scrape the report.</para>
///
/// <para>The tools are a thin layer over <see cref="ReportWriter"/>, which the command line
/// also uses, so the two cannot drift apart.</para>
/// <summary>
/// One reporting tool: hand it a lens, it hands back a reading.
///
/// <para>Most take nothing beyond the lens and the glass folder, which is why those two live in
/// one shared schema rather than being restated fourteen times. <see cref="Extra"/> is for the
/// handful that genuinely take something more - so far only the Forbes degree - and it exists
/// because the alternative was leaving the CLI able to ask a question the MCP could not. A
/// caller should not have to know which door they came in through.</para>
/// </summary>
internal sealed record Tool(
    string Name,
    string Description,
    Func<ReportWriter, JsonNode?, string> Run,
    IReadOnlyList<ArgumentSpec>? Extra = null);

internal static class Tools
{
    /// <summary>
    /// Reads the lens and prepares the report. The catalogs are loaded from the bundle unless
    /// the caller names a folder; failing to find them at all is worth an exception rather
    /// than a silent fallback, because without a catalog every glass resolves as air and the
    /// whole answer is quietly wrong.
    /// </summary>
    public static ReportWriter Open(string lensPath, string? glassDir) =>
        Open(lensPath, glassDir, null);

    /// <summary>
    /// As above, with the alignment stated inline rather than read from the sidecar.
    ///
    /// <para>Passing null reads <c>&lt;lens&gt;.align</c> if it is there, which is what the
    /// command line does for every analysis. It matters that both do the same thing: a lens with
    /// a perturbation beside it must not read one way through the CLI and another through this
    /// server.</para>
    /// </summary>
    public static ReportWriter Open(string lensPath, string? glassDir,
                                    AlignmentSpecification? alignment)
    {
        if (string.IsNullOrWhiteSpace(lensPath))
            throw new ArgumentException("a lens file path is required");

        // Bare names are taken against the base folder. This matters more here than on the
        // command line: an MCP server's working directory is whatever the client started it in,
        // not anything the user chose, so without a base every path has to be absolute.
        lensPath = BasePath.Resolve(lensPath);
        glassDir = BasePath.ResolveIfGiven(glassDir);

        if (!File.Exists(lensPath))
            throw new FileNotFoundException($"no such file: {lensPath}", lensPath);

        GlassCatalog catalog;
        if (!string.IsNullOrWhiteSpace(glassDir))
        {
            if (!Directory.Exists(glassDir))
                throw new DirectoryNotFoundException($"no such folder: {glassDir}");
            catalog = new GlassCatalog();
            catalog.LoadFolder(glassDir);
        }
        else
        {
            catalog = CatalogLocator.LoadBundled();
        }

        var system = LensFile.Read(lensPath, catalog);
        (alignment ?? AlignmentFile.ReadFor(lensPath)).ApplyTo(system);
        return new ReportWriter(system, catalog, Path.GetFullPath(lensPath));
    }
    /// <summary>
    /// The Forbes degree an argument node asks for, defaulting to 3 and refused outside 3 to 8.
    ///
    /// <para>Refused rather than clamped. A caller asking for degree 12 has a reason - probably a
    /// wrong idea of what the number means - and silently giving them 8 would confirm it.</para>
    /// </summary>
    private static int ForbesDegree(JsonNode? a)
    {
        var node = a?["degree"];
        if (node == null) return 3;

        int degree = node.GetValue<int>();
        if (degree < 3 || degree > 8)
            throw new ArgumentException(
                $"degree must be between 3 and 8; {degree} was asked for. Three is the seventh "
              + "order, which is what the twenty tau are, and higher degrees carry the orders "
              + "ABOVE it rather than improving those twenty.");
        return degree;
    }


    public static readonly IReadOnlyList<Tool> All = new[]
    {
        new Tool("analyse_lens",
            "The whole analysis of one lens as formatted text: prescription, first-order "
          + "data, paraxial rays, Seidel, Buchdahl third/fifth/seventh order, predicted RMS "
          + "spot and the per-aberration and per-surface breakdowns. Start here; ask for one "
          + "of the table tools when a specific number is wanted.",
            (w, _) => w.BuildReport()),

        new Tool("prescription",
            "One row per surface: radius, thickness, material, semi-diameter, conic and "
          + "aspheric terms. Tab-separated.",
            (w, _) => w.BuildPrescriptionTsv()),

        new Tool("first_order",
            "Focal length, back and front focal length, F-number, numerical aperture, "
          + "entrance and exit pupils, image height, magnification and total track. "
          + "Name/value pairs, tab-separated.",
            (w, _) => w.BuildFirstOrderTsv()),

        new Tool("paraxial_rays",
            "The marginal and chief ray height and angle at every surface. Tab-separated.",
            (w, _) => w.BuildParaxialRaysTsv()),

        new Tool("indices",
            "Refractive index of each material at each wavelength. Tab-separated.",
            (w, _) => w.BuildIndicesTsv()),

        new Tool("seidel",
            "The third-order coefficients - spherical, coma, astigmatism, Petzval, "
          + "distortion and the two chromatic ones - per surface and totalled. "
          + "Tab-separated.",
            (w, _) => w.BuildSeidelTsv()),

        new Tool("buchdahl",
            "Third, fifth and SEVENTH order aberration coefficients, per surface and "
          + "totalled. The seventh-order set is the part this program exists for; see "
          + "`docs/verification.md` for what is and is not verified about it. Tab-separated.",
            (w, _) => w.BuildBuchdahlTsv()),

        new Tool("rms_spot",
            "Predicted RMS spot radius at each field and wavelength, and the composite "
          + "PRMSA, from the coefficients rather than from traced rays. Tab-separated.",
            (w, _) => w.BuildPrmsTsv()),

        new Tool("contributions",
            "Per aberration: the RMS spot it would produce on its own, and its share of the "
          + "whole. This is what says WHICH aberration is costing the design its performance. "
          + "Tab-separated.",
            (w, _) => w.BuildContributionTsv()),

        new Tool("surface_breakdown",
            "Per surface: intrinsic, aspheric and induced contributions and their total. The "
          + "induced column is the part a designer cannot see any other way - a surface can "
          + "be blameless on its own and still spoil the system through what it induces "
          + "downstream. Tab-separated.",
            (w, _) => w.BuildSurfaceBreakdownTsv()),

        new Tool("aspheric_screen",
            "Whether this design would exercise the aspheric SEVENTH-order path hard enough "
          + "to test it: how much of the predicted spot the three suspect coefficients carry, "
          + "how much of those comes from the figuring rather than from the underlying "
          + "spheres, whether the series still converges at this aperture and field, and "
          + "whether the seventh order is visible at all. Use it to sort candidate test "
          + "designs before tracing any of them. Readable text.",
            (w, _) => w.BuildAsphericScreenText()),

        new Tool("seventh_order",
            "Third, FIFTH and SEVENTH order aberration coefficients per surface, each split "
          + "into what the surface generates on its own, what its figuring adds, and what it "
          + "generates by acting on the aberration already reaching it. The seventh order is "
          + "the twenty tau, by the Forbes series trace (J. Opt. Soc. Am. 73, 782), which "
          + "handles spheres, conics and even aspheres alike and needs no ray tracer and no "
          + "other program. Either conjugate; a design with a mirror is declined. Ends with a cross-check: seventh-order spherical "
          + "aberration reached by two routes sharing no code, which must agree. Use this when "
          + "asked why a design will not correct, or which surface to change - a table of "
          + "totals cannot say, and the induced column can. Readable text.",
            (w, a) => w.BuildForbesText(ForbesDegree(a)) ?? "The coefficients could not be "
               + "separated. That happens when the system has no field, when the series trace "
               + "does not close on this design, or when the design contains a mirror, which the series trace does not trace.",
            new[]
            {
                new ArgumentSpec("degree", "integer",
                    "How far to carry the series, 3 to 8. Three is the seventh order and the "
                  + "default. HIGHER IS NOT MORE ACCURATE FOR THE SEVENTH ORDER - the twenty tau "
                  + "are what degree 3 already gives - it carries the NEXT orders, which is how "
                  + "to find out whether a design's residual is seventh order at all. On the "
                  + "Cooke triplet the predicted corner distortion walks back towards the traced "
                  + "ray as the degree rises, 1.219E-02 at 3, 1.153E-02 at 4, 1.011E-02 at 5, "
                  + "against 8.836E-03 traced: that is how docs/distortion-prediction.md "
                  + "establishes the overshoot is the ORDER running out and not this program "
                  + "being wrong. It costs more the higher it goes."),
            }),

        new Tool("distortion_from_coefficients",
            "How far the ABERRATION COEFFICIENTS can be trusted for distortion, measured "
          + "against rays. NOT the way to obtain a distortion figure - tracing one chief ray "
          + "gives that exactly, at the same speed, with no error at the corner - so do not "
          + "quote the predicted columns when asked what a lens's distortion is; the traced "
          + "column beside them is the answer. What this gives that a trace cannot is WHICH "
          + "ORDER the distortion is: third order is stop position and symmetry, the higher "
          + "orders are not, and they respond to different changes. Third, fifth and seventh "
          + "order across the field in both mappings, F-tan(theta) and F-theta; the paraxial "
          + "image plane the coefficients live at reconciled with the image surface the file "
          + "defines, where a design program quotes; and E, E5 and tau20 read back out of the "
          + "rays with an error bar, which is a check the predicted RMS spot cannot make. On a "
          + "FIGURED design the seventh-order term comes from the Forbes series trace, a choice "
          + "made while the scheme's aspheric arrangement was still one the rays rejected and "
          + "kept now that the two agree, because the report names the route it used - except "
          + "where Forbes declines, as it does any mirror, when the scheme's value is used and "
          + "named. Readable text.",
            (w, _) => w.BuildDistortionText()),

        new Tool("quaternary_spherical",
            "The coefficient of QUATERNARY - ninth-order - spherical aberration, per surface, "
          + "with the intermediate rows Buchdahl prints beside it. Everything else here stops "
          + "at the SEVENTH order, so where a prediction and a traced ray part company on axis "
          + "the residual has had to be attributed to the ninth order rather than measured; "
          + "this makes that arithmetic. Buchdahl, Optical Aberration Coefficients IV, J. Opt. "
          + "Soc. Am. 48, 757 (1958). SPHERICAL SURFACES ONLY - a figured design is refused "
          + "with its reason rather than answered, because he published no aspheric "
          + "arrangement at this order and the scheme would return a number that is neither "
          + "the spherical coefficient nor the aspheric one. Readable text.",
            (w, _) => w.BuildQuaternaryText()),

        new Tool("surface_share",
            "Per surface: its share of the spot and the fraction of that which is induced "
          + "rather than its own. Tab-separated.",
            (w, _) => w.BuildSurfaceShareTsv()),
    };
}
