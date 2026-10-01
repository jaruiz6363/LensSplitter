using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AberrationCalculator.Tests")]

namespace AberrationCalculator.Cli;

/// <summary>
/// Command-line entry point.
///
/// Writes the report to stdout so it can be piped, and the same content to files so it can
/// be kept, diffed and re-read. The machine-readable TSVs are written alongside rather than
/// instead: a report you can read and a table you can parse serve different jobs, and
/// deriving both from one pass means they cannot drift apart.
/// </summary>
internal static class Program
{
    private const string Usage = @"
abcalc - read a lens design and report its prescription and first-order data

USAGE
  abcalc <lensfile> [options]

OPTIONS
  -o, --out <dir>     Write report and data files into <dir>.
                      Default: alongside the lens file.
      --no-files      Print to stdout only; write nothing.
      --stdout-only   Same as --no-files.
  -q, --quiet         Write the files but print nothing except errors.
      --glass <dir>   Use this folder of .agf catalogs instead of the bundled ones.
      --dir <dir>     Take bare file names as meaning this folder, for this run only.
                      Beats ABCALC_DIR, which beats the folder set with BASE, which
                      beats the current directory. An absolute path is never touched.
      --nat
                      Nodal aberration theory, third and fifth order: where each
                      surface's aberration field has been displaced to, where the
                      nodes of the system's field are, and what the fifth order does
                      to the third. Reads tilts, decentres and Zernike overlays from
                      the .align sidecar. Abbreviates to --nodal. See
                      docs/nodal-aberration-theory.md.

      --distortion-coefficients
                      What the aberration coefficients make of DISTORTION, against
                      the rays. Not a distortion report - tracing one chief ray gives
                      that exactly and no slower. This asks how far the coefficients
                      can be trusted: third, fifth and seventh order over a ladder of
                      field fractions, both mappings, and E, E5 and tau20 read back
                      out of the rays with an error bar. Write nothing else.
                      Abbreviates to --distortion.
      --quaternary-spherical
                      The coefficient of QUATERNARY - ninth-order - spherical
                      aberration, per surface, by Buchdahl paper IV (JOSA 48, 757).
                      Everything else this program reports stops at the seventh, so
                      an on-axis residual has had to be ATTRIBUTED to the ninth order
                      rather than measured; this makes it arithmetic. SPHERICAL
                      SURFACES ONLY - a figured design is refused, because Buchdahl
                      published no aspheric arrangement at this order and the scheme
                      would return a number that is neither one thing nor the other.
                      Writes nothing else. Abbreviates to --quaternary.
      --asphere-placement
                      WHERE an asphere would do the most good, and what it would
                      buy - the design question asked forwards, where every other
                      table here asks what is wrong with the design in hand. Per
                      surface: the marginal and chief ray heights, their ratio H/h,
                      which sum the surface moves most, and the r^4 coefficient
                      that would drive each Seidel sum to zero on its own. THIRD
                      ORDER ONLY, and Petzval is absent because no figuring can
                      reach it. Writes nothing else. Abbreviates to --asphere-where.
      --optimize [mf] Optimise the design and write the result.

                      SETTINGS. Without <mf>, the settings come from the sidecar
                      file beside the lens (<lensfile>.mf) and, for a .lhlt, from
                      the lens itself - a .lhlt states which surfaces have variable
                      curvatures, thicknesses, conics and aspheric terms, with what
                      bounds, and its pickups, and all of that is read and honoured.
                      Its own MERIT FUNCTION is NOT read: this tool optimises a
                      different one, and leaves the original untouched in the file.

                      SAVING. The optimised design goes back in the format it came
                      from, editing that file rather than regenerating it, so that
                      everything this program does not model - solves, coatings,
                      tolerances, somebody else's merit function - survives. Only
                      what the optimiser can move changes - curvatures, thicknesses,
                      glass names, and conics and aspheric terms - in the file's own
                      units. Conics and aspheric terms go back into .lhlt, .zmx and
                      Optiland .json; a CODE V, OSLO or Optalix file whose figuring
                      moved is NOT saved, and the values are in the report. For a
                      .lhlt the variable and pickup settings are written back too;
                      for every other format they go to the sidecar, along with the
                      merit function.

                      The derivatives are ANALYTIC throughout -
                      every operand, including the predicted spot, is differentiated
                      by carrying dual numbers through the same aberration chain that
                      computes it, so there is no step size anywhere and no
                      cancellation. Coefficients come from Buchdahl's closed-form
                      scheme. CONICS AND EVEN ASPHERES ARE CARRIED, as values
                      and as variables - CC, A4, A6, A8 - a figured surface
                      taking the aspheric arrangement of Sec. 85 and a
                      spherical one Buchdahl's own table. A figured flat
                      facing collimated light is carried too, in Laurent
                      series arithmetic; a design is refused only when that
                      route cannot vouch for its answer.
                      See docs/optimizer.md for the merit-function format.
      --optimize_basin_hopping
                      Search over BASINS rather than descending one: kick the design
                      out of its valley, re-minimise, keep or reject by a Metropolis
                      rule, on several chains at once. Chains land in different
                      valleys, so this produces one design PER CHAIN and needs
                      somewhere to put them - it is refused without --save <folder>.
      --glass_substitution <catalogue>
                      Let the hopping try glasses from that substitution catalogue,
                      e.g. CoreSet28. Glass is discrete - there is no gradient from
                      one glass to the next - so it can only be proposed and judged,
                      never followed downhill. Substitution catalogues live in
                      catalogs/Substitution and are deliberately NOT the catalogues
                      used to read a design: a search free to pick from every vendor
                      at once settles on glasses nobody stocks.
      --method <m>    lm, psd2, psd3 or hj. Default psd3: Dilworth's
                      pseudo-second-derivative, which estimates the curvature that
                      Gauss-Newton discards from successive exact Jacobians. hj is
                      Hooke-Jeeves pattern search, which uses no derivatives at all.
      --iterations <n>  Local iterations, or iterations per hop. Default 200 for a
                      local run, 6000 per hop when hopping - a CAP, not a count.
      --hops <n>      Hops per chain. Default 3000 when hopping. Watch the progress
                      and press Ctrl+C when the best stops moving: the run stops
                      and still writes what every chain found.
      --chains <n>    Independent hopping chains. Default 0 = one per PHYSICAL core.
                      Not per logical processor: two SMT threads on one core
                      contend for the same execution units, and on a hybrid part
                      the logical count includes efficiency cores, which run a
                      chain slower and hold up every hop they are given.
      --seed <n>      Random seed for the hopping. Default 1234.
      --hop-sigma <s> Size of a hop, in natural steps. Default 0.001 - a tenth of a
                      per cent. Escape is the job of the
                      Metropolis walk and the restarts, not of the kick.
      --hop-figuring  Kick the conic and aspheric terms on a hop as well. Off by
                      default: they are still OPTIMISED every hop, but throwing
                      them discards a figure the local stage is about to fit
                      again, and the kick is better spent on the shape.
      --initial-sigma <s>
                      Size of the FIRST hop only, in natural steps. Default 0.001,
                      the same as --hop-sigma. Its job is different: it breaks exact
                      symmetry, since a design on a stationary point has nowhere to
                      go. Raise it to start from a deliberately disturbed design -
                      useful off a skeleton - without making every later hop violent.
      --save          Overwrite the lens that was read with the optimised design.
      --save <folder> Under --optimize_basin_hopping, the folder to write the
                      designs into - one per chain, plus their settings.
      --saveas <path> Write the optimised design somewhere new. In the format read,
                      the file is edited; with another extension (.zmx .seq .len
                      .otx .json .lhlt) a whole lens is written in that format.
                      With none of these, the original is left alone and the result
                      is written beside it as <name>.optimised.<ext>.
      --screen [h]    Report whether this design would test the aspheric seventh-order
                      path, and write nothing else. Optional field fraction,
                      default 1.0 (the corner).
      --forbes [d]    Third, fifth and seventh order per surface, split into what each
                      surface generates on its own and what it generates by acting on
                      the aberration already reaching it. Write nothing else. The
                      seventh order is by the Forbes series trace, which handles conics
                      and even aspheres as well as spheres, at either conjugate.
                      Optional truncation degree 3 to 8, default 3 = seventh order.
                      Raising it must not change the answer; that it does not is a real
                      check that the series has converged for this design.
  -h, --help          This text. `abcalc HELP` is the commands and the merit
                      function operands, and `abcalc HELP <name>` is any one
                      of them on its own.

COMMANDS
  Building a merit function and a variable list, a line at a time. The command IS
  the file line: what you type is what lands in the settings file, so a transcript
  of commands is a valid file and a file is a script of commands. Quote the text.

  abcalc <lens> VAR ""TH 2 MIN 1.0 MAX 25.0""     declare a variable, or bound one
  abcalc <lens> VAR ""TH 2 FREE""                 drop its bounds
  abcalc <lens> VARLIST                         list them, numbered
  abcalc <lens> VARREMOVE 2                     remove number 2

  abcalc <lens> PICKUP ""TH 2 INDEX 1 SCALE 1 OFFSET -0.1""
  abcalc <lens> PICKUPLIST
  abcalc <lens> PICKUPREMOVE 1

  abcalc <lens> OP ""EFL, 100, TAR 50, 2""        add an operand
  abcalc <lens> OPLIST
  abcalc <lens> OPREMOVE 3                      remove number 3

  abcalc <lens> TILT ""2 X 0.115""                 a surface is tilted, in DEGREES
  abcalc <lens> DEC ""2 Y 0.05""                   and decentred, in lens units
  abcalc <lens> ZERN ""1 Z5 0.0001""              a Zernike figure error, surface sag
  abcalc <lens> ALIGNLIST                       list what is out of place
  abcalc <lens> ALIGNREMOVE 1                   remove number 1

  abcalc HELP                                   list every command and operand
  abcalc HELP VAR                               explain one of them
  abcalc HELP EFL                               what an operand takes

  A VAR line MERGES with what is already there, so setting a maximum does not
  discard a minimum set a moment earlier. Several commands may be given at once,
  and a run may follow them in the same invocation.

  WHERE THEY ARE KEPT. The merit function is always <lens>.mf. Variables and
  pickups are in the .lhlt itself for such a design - which is where its
  author put them - and in <lens>.var for every other format. A .lhlt's own merit
  function is not read and is left untouched.

  THE BASE FOLDER. One command is not about any particular lens, and is the one
  that can be given on its own - naming a folder is what stops you typing the path
  to a lens in the first place.

  abcalc BASE ""C:\lenses\project7""            bare names now mean this folder
  abcalc BASELIST                             show it, and where it came from
  abcalc BASEREMOVE                           forget it

  It is kept until it is changed, so it holds in the next shell too, and the MCP
  reads the same setting - which matters more there, since an MCP server's working
  directory is whatever the client started it in rather than anything you chose.
  Four things can set it, most specific first: --dir on the command line,
  ABCALC_DIR in the environment, the folder set with BASE, and failing all of
  those the current directory. An absolute path always means what it says.

FORMATS
  .zmx  ZEMAX      .seq  CODE V     .otx .opt  OPTALIX
  .len .osl OSLO   .json Optiland   .lhlt      LensHH-LT

OUTPUT
  <name>.report.txt          Everything, formatted to read.
  <name>.prescription.tsv    One row per surface.
  <name>.indices.tsv         One row per material, one column per wavelength.
  <name>.firstorder.tsv      Name/value pairs.
  <name>.paraxial.tsv        Marginal and chief ray at every surface.
  <name>.seidel.tsv          Third-order coefficients per surface, plus totals.
  <name>.buchdahl.tsv        Third, fifth and seventh order, per surface and total.
  <name>.prms.tsv            Predicted RMS spot per field and wavelength, and PRMSA.
  <name>.contributions.tsv   Per-aberration isolated RMS and share of the spot.
  <name>.surfaces.tsv        Per surface: intrinsic, aspheric, induced, and their total.
  <name>.surface-share.tsv   Per surface: share of the spot and induced fraction.
  <name>.nat.tsv             Full-field aberrations, third and fifth order, with --nat.

  Alignment - how one BUILT instance sits - is read from a sidecar named for the
  lens INCLUDING its extension, lens.zmx.align, as the .mf and .var files are. It
  is never written back into the lens: delete it to restore the nominal design.

  The .tsv files are tab-separated with full precision, so a script or a
  spreadsheet can use them without reparsing the report.

EXIT CODES
  0  read and analysed
  1  could not read the file, or no glass catalogs found
  2  read, but one or more materials could not be resolved
";

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// The command line proper, separated from <c>Main</c> so the tests can run it. What a
    /// flag actually does is not visible from anywhere else: the report builders are covered
    /// on their own, and a flag wired to the wrong one would pass every one of those tests.
    /// </summary>
    internal static int Run(string[] args)
    {
        string? lensPath = null, outDir = null, glassDir = null, baseDir = null;
        bool writeFiles = true, quiet = false, screen = false, forbes = false, distortion = false;
        bool nat = false, quaternary = false, asphereWhere = false;
        double screenH = 1.0;
        int forbesDegree = 3;

        string? meritPath = null, savePath = null;
        bool optimise = false, basinHopping = false, saveInPlace = false, iterationsGiven = false;
        string? substitutionCatalog = null;
        var commands = new List<(string Keyword, string? Argument)>();
        var optimize = new AberrationCalculator.Optimize.RunSettings();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-h": case "--help": Console.WriteLine(Usage.Trim()); return 0;
                case "--no-files": case "--stdout-only": writeFiles = false; break;
                case "-q": case "--quiet": quiet = true; break;
                case "-o": case "--out":
                    if (++i >= args.Length) throw new ArgumentException("--out needs a directory");
                    outDir = args[i]; break;
                case "--glass":
                    if (++i >= args.Length) throw new ArgumentException("--glass needs a directory");
                    glassDir = args[i]; break;
                // Overrides the stored base for this one run, without un-setting it.
                case "--dir":
                    if (++i >= args.Length) throw new ArgumentException("--dir needs a directory");
                    baseDir = args[i]; break;
                case "--forbes":
                    forbes = true;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer,
                                                            CultureInfo.InvariantCulture, out int fd))
                    {
                        if (fd < 3 || fd > 8)
                            throw new ArgumentException("--forbes takes a degree from 3 to 8");
                        forbesDegree = fd; i++;
                    }
                    break;
                case "--distortion-coefficients": case "--distortion": distortion = true; break;
                case "--quaternary-spherical": case "--quaternary": quaternary = true; break;
                case "--asphere-placement": case "--asphere-where": asphereWhere = true; break;
                case "--nat": case "--nodal": nat = true; break;

                case "--optimize": case "--optimise":
                    optimise = true;
                    // The settings file is optional: without one the sidecar beside the lens is
                    // used, and for a .lhlt its own variables and pickups as well. A bare token
                    // is only taken as that file once the lens itself is known, so
                    // `--optimize lens.lhlt` still reads as the lens.
                    if (lensPath != null && i + 1 < args.Length && !args[i + 1].StartsWith("-")
                        && !AberrationCalculator.Optimize.Io.SettingsCommands.IsCommand(args[i + 1]))
                        meritPath = args[++i];
                    break;
                // --save with no path overwrites the lens that was read; with one - which basin
                // hopping requires, since it produces a design per chain - it names a folder.
                case "--save":
                    saveInPlace = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-")
                        && !AberrationCalculator.Optimize.Io.SettingsCommands.IsCommand(args[i + 1]))
                    { savePath = args[++i]; saveInPlace = false; }
                    break;
                case "--saveas":
                    if (++i >= args.Length) throw new ArgumentException("--saveas needs a path");
                    savePath = args[i]; break;
                case "--optimize_basin_hopping": case "--optimise_basin_hopping":
                    optimise = true; basinHopping = true; break;
                case "--glass_substitution":
                    if (++i >= args.Length)
                        throw new ArgumentException(
                            "--glass_substitution needs a catalogue name, for example CoreSet28");
                    substitutionCatalog = args[i]; break;
                case "--method":
                    if (++i >= args.Length) throw new ArgumentException("--method needs lm, psd2, psd3 or hj");
                    switch (args[i].ToLowerInvariant())
                    {
                        case "lm": optimize.Method = Optimize.Algorithms.StepMethod.Lm; break;
                        case "psd2": optimize.Method = Optimize.Algorithms.StepMethod.Psd2; break;
                        case "psd3": optimize.Method = Optimize.Algorithms.StepMethod.Psd3; break;
                        case "hj": optimize.HookeJeeves = true; break;
                        default: throw new ArgumentException($"unknown method '{args[i]}'; use lm, psd2, psd3 or hj");
                    }
                    break;
                case "--iterations":
                    optimize.Iterations = Count(args, ref i, "--iterations");
                    iterationsGiven = true; break;
                case "--hops": optimize.Hops = Count(args, ref i, "--hops"); break;
                case "--chains": optimize.Chains = Count(args, ref i, "--chains"); break;
                case "--seed": optimize.Seed = Count(args, ref i, "--seed"); break;
                case "--hop-sigma":
                    if (++i >= args.Length) throw new ArgumentException("--hop-sigma needs a number");
                    optimize.HopSigma = double.Parse(args[i], CultureInfo.InvariantCulture); break;
                case "--hop-figuring": optimize.HopFiguring = true; break;
                case "--initial-sigma":
                    if (++i >= args.Length) throw new ArgumentException("--initial-sigma needs a number");
                    optimize.InitialPerturbSigma = double.Parse(args[i], CultureInfo.InvariantCulture); break;
                case "--glass-substitution": optimize.GlassSubstitution = true; break;
                case "--screen":
                    screen = true;
                    // Optional field fraction. The corner is the default but is also where
                    // the series is weakest, so being able to ask lower down matters.
                    if (i + 1 < args.Length && double.TryParse(args[i + 1],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double sh))
                    { screenH = sh; i++; }
                    break;
                default:
                    if (a.StartsWith("-")) throw new ArgumentException($"unknown option '{a}'");

                    // A settings command. Its argument is one quoted string that IS the file
                    // line - see SettingsCommands. Nearly all of them are about a particular
                    // lens and so are only recognised once one has been given; BASE is not, and
                    // must be sayable on its own, since its whole purpose is to stop you having
                    // to type the path to a lens.
                    if (AberrationCalculator.Optimize.Io.SettingsCommands.IsCommand(a)
                        && (lensPath != null
                            || !AberrationCalculator.Optimize.Io.SettingsCommands.NeedsLens(a)))
                    {
                        string? argument = null;
                        if (AberrationCalculator.Optimize.Io.SettingsCommands.TakesArgument(a))
                        {
                            // HELP is the one whose argument may be left off, and the topic it
                            // takes is often itself a command name - HELP VAR - so the next
                            // token is taken as the argument even though it is a keyword.
                            if (AberrationCalculator.Optimize.Io.SettingsCommands
                                    .ArgumentIsOptional(a))
                            {
                                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                                    argument = args[++i];
                            }
                            else
                            {
                                if (++i >= args.Length)
                                    throw new ArgumentException($"{a} needs an argument");
                                argument = args[i];
                            }
                        }
                        commands.Add((a, argument));
                        break;
                    }

                    // A lens command with no lens reads as a filename otherwise, and fails as
                    // "no such file: VARLIST", which says nothing about what went wrong.
                    if (AberrationCalculator.Optimize.Io.SettingsCommands.IsCommand(a))
                        throw new ArgumentException(
                            $"{a.ToUpperInvariant()} is about a particular lens; name the lens "
                          + "first, as in: abcalc lens.zmx " + a.ToUpperInvariant());

                    if (lensPath != null) throw new ArgumentException("give one lens file at a time");
                    lensPath = a; break;
            }
        }

        // The base folder comes first, so that a lens named in the same invocation resolves
        // against a base set in it.
        var globals = commands.FindAll(
            c => !AberrationCalculator.Optimize.Io.SettingsCommands.NeedsLens(c.Keyword));
        commands.RemoveAll(
            c => !AberrationCalculator.Optimize.Io.SettingsCommands.NeedsLens(c.Keyword));

        foreach (var (keyword, argument) in globals)
        {
            var result = AberrationCalculator.Optimize.Io.SettingsCommands.ExecuteGlobal(
                keyword, argument, baseDir);
            if (!quiet) Console.Write(result.Output);
        }

        if (lensPath == null)
        {
            // Saying BASE and nothing else is a complete instruction, not a mistake, so it does
            // not get the usage text thrown at it.
            if (globals.Count == 0) Console.WriteLine(Usage.Trim());
            return 0;
        }

        // Everything the user names is taken relative to the base folder, so a bare file name
        // means the design they are working on rather than whatever happens to be beside the
        // shell. An absolute path is left exactly as it was given.
        lensPath = AberrationCalculator.Core.IO.BasePath.Resolve(lensPath, baseDir);
        meritPath = AberrationCalculator.Core.IO.BasePath.ResolveIfGiven(meritPath, baseDir);
        savePath = AberrationCalculator.Core.IO.BasePath.ResolveIfGiven(savePath, baseDir);
        outDir = AberrationCalculator.Core.IO.BasePath.ResolveIfGiven(outDir, baseDir);
        glassDir = AberrationCalculator.Core.IO.BasePath.ResolveIfGiven(glassDir, baseDir);

        if (!File.Exists(lensPath)) { Console.Error.WriteLine($"error: no such file: {lensPath}"); return 1; }

        // Catalogs come from the bundle unless overridden. Failing loudly matters here:
        // without them every glass silently resolves as air and the whole report is wrong.
        GlassCatalog catalog;
        if (glassDir != null)
        {
            if (!Directory.Exists(glassDir)) { Console.Error.WriteLine($"error: no such folder: {glassDir}"); return 1; }
            catalog = new GlassCatalog();
            catalog.LoadFolder(glassDir);
        }
        else
        {
            catalog = CatalogLocator.LoadBundled();
        }

        var system = LensFile.Read(lensPath, catalog);

        // How this instance was BUILT, if anybody has said. Nothing but nodal aberration theory
        // reads it: the paraxial trace, the coefficients and the real ray trace are all
        // rotationally symmetric constructions and do not see these fields at all. It is applied
        // here anyway so that every mode gets the same system, and no writer knows about the
        // perturbation properties, so a --save can never leak a build error into a prescription.
        try
        {
            AberrationCalculator.Core.IO.AlignmentFile.ReadFor(lensPath).ApplyTo(system);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }

        // Settings commands come first: they edit what the optimiser will be told, and running
        // them alongside a run in one invocation means "set this up, then use it".
        if (commands.Count > 0)
        {
            int code = RunCommands(system, lensPath, commands, quiet);
            if (code != 0 || !optimise) return code;
        }

        // Optimisation is the one mode that CHANGES a design, so it says what it did and writes
        // nothing over the lens it was given unless asked with --save.
        if (optimise)
        {
            // Deliberately generous defaults: 3000 hops of 6000 iterations each. They
            // look enormous because they are CAPS, not counts - a designer watches the progress
            // and stops the run when the best stops moving, which is the normal way it ends.
            // Starting from different settings than the program this is meant to reproduce means
            // chasing settings instead of chasing designs.
            if (basinHopping)
            {
                if (optimize.Hops == 0) optimize.Hops = 3000;
                // The LM cap per hop. Not the Hooke-Jeeves budget, which is a different
                // quantity with its own default - see RunSettings.HjStepsPerHop.
                if (!iterationsGiven) optimize.Iterations = 400;
            }
            optimize.GlassSubstitution = substitutionCatalog != null;
            optimize.SubstitutionCatalog = substitutionCatalog;

            return basinHopping
                ? BasinHop(system, catalog, lensPath, meritPath, savePath, optimize, quiet)
                : Optimise(system, catalog, lensPath, meritPath, savePath, saveInPlace, outDir,
                           optimize, writeFiles, quiet);
        }

        var writer = new ReportWriter(system, catalog, Path.GetFullPath(lensPath));

        // The screen answers one question and writes nothing, so it short-circuits the rest.
        if (screen) { Console.Write(writer.BuildAsphericScreenText(screenH)); return writer.Unresolved.Count > 0 ? 2 : 0; }

        // And so does the distortion check, which is a measurement rather than a report and
        // is the only one of these that traces rays.
        if (distortion) { Console.Write(writer.BuildDistortionText()); return writer.Unresolved.Count > 0 ? 2 : 0; }

        // And the ninth order, which is its own mode for the same reason and is additionally
        // REFUSED on a figured design, so it would be an apology in the middle of a report.
        if (quaternary) { Console.Write(writer.BuildQuaternaryText()); return writer.Unresolved.Count > 0 ? 2 : 0; }
        if (asphereWhere) { Console.Write(writer.BuildAspherePlacementText()); return writer.Unresolved.Count > 0 ? 2 : 0; }

        // The nodal report answers a question about a MISALIGNED lens, which none of the others
        // can be asked at all, so it is its own mode rather than a section of the main report.
        if (nat)
        {
            Console.Write(writer.BuildNatText());
            if (writeFiles)
            {
                string dir = outDir ?? Path.GetDirectoryName(Path.GetFullPath(lensPath)) ?? ".";
                Directory.CreateDirectory(dir);
                string stem = Path.GetFileNameWithoutExtension(lensPath);
                string tsv = Write(dir, stem + ".nat.tsv", writer.BuildNatFullFieldTsv());
                if (!quiet) Console.WriteLine(Environment.NewLine + "  full-field display: " + tsv);
            }
            return writer.Unresolved.Count > 0 ? 2 : 0;
        }

        // So does the Forbes report, which is a different question about the same lens.
        if (forbes)
        {
            string? text = writer.BuildForbesText(forbesDegree);
            if (text == null)
            {
                Console.Error.WriteLine("error: the coefficients could not be separated. That happens "
                                      + "when the system has no field, when the series trace does "
                                      + "not close on this design, or when the design contains a mirror, which the series trace does not trace.");
                return 1;
            }
            Console.Write(text);
            return writer.Unresolved.Count > 0 ? 2 : 0;
        }
        string report = writer.BuildReport();

        if (!quiet) Console.Write(report);

        if (writeFiles)
        {
            string dir = outDir ?? Path.GetDirectoryName(Path.GetFullPath(lensPath)) ?? ".";
            Directory.CreateDirectory(dir);
            string stem = Path.GetFileNameWithoutExtension(lensPath);

            var written = new List<string>
            {
                Write(dir, stem + ".report.txt",       report),
                Write(dir, stem + ".prescription.tsv", writer.BuildPrescriptionTsv()),
                Write(dir, stem + ".indices.tsv",      writer.BuildIndicesTsv()),
                Write(dir, stem + ".firstorder.tsv",   writer.BuildFirstOrderTsv()),
                Write(dir, stem + ".paraxial.tsv",     writer.BuildParaxialRaysTsv()),
                Write(dir, stem + ".seidel.tsv",       writer.BuildSeidelTsv()),
                Write(dir, stem + ".buchdahl.tsv",     writer.BuildBuchdahlTsv()),
                Write(dir, stem + ".prms.tsv",         writer.BuildPrmsTsv()),
                Write(dir, stem + ".contributions.tsv", writer.BuildContributionTsv()),
                Write(dir, stem + ".surfaces.tsv",      writer.BuildSurfaceBreakdownTsv()),
                Write(dir, stem + ".surface-share.tsv", writer.BuildSurfaceShareTsv()),
            };

            if (!quiet)
            {
                Console.WriteLine("Written:");
                foreach (var f in written) Console.WriteLine("  " + f);
            }
        }

        if (writer.Unresolved.Count > 0)
        {
            Console.Error.WriteLine("warning: unresolved materials: " + string.Join(", ", writer.Unresolved));
            return 2;
        }
        return 0;
    }

    private static string Write(string dir, string name, string content)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static int Count(string[] args, ref int i, string flag)
    {
        if (++i >= args.Length) throw new ArgumentException(flag + " needs a number");
        if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            || v < 0)
            throw new ArgumentException($"{flag} needs a whole number, not '{args[i]}'");
        return v;
    }

    /// <summary>
    /// Optimises a design and writes the result out beside it.
    ///
    /// <para>The lens that was read is never modified. What comes back is a new file, so a run
    /// that made things worse costs nothing but the time - and the report says plainly when that
    /// is what happened rather than quietly handing back a design nobody asked for.</para>
    /// </summary>
    /// <summary>
    /// Runs the settings commands - VAR, OP, OPLIST and the rest - against this lens's settings,
    /// saving after any that changed something.
    ///
    /// <para>The settings file is the session: each command reads what is there, changes it and
    /// writes it back, so a command line that exits between every command still behaves like a
    /// program that remembers.</para>
    /// </summary>
    private static int RunCommands(AberrationCalculator.Core.Models.OpticalSystem system,
                                   string lensPath,
                                   List<(string Keyword, string? Argument)> commands, bool quiet)
    {
        var setup = AberrationCalculator.Optimize.Io.SettingsStore.Load(system, lensPath);
        var alignment = AberrationCalculator.Core.IO.AlignmentFile.ReadFor(lensPath);
        bool changed = false, alignmentChanged = false;

        foreach (var (keyword, argument) in commands)
        {
            AberrationCalculator.Optimize.Io.SettingsCommands.Result result;
            bool isAlignment =
                AberrationCalculator.Optimize.Io.SettingsCommands.IsAlignmentCommand(keyword);
            try
            {
                // Alignment is about how the lens was BUILT, not what may be optimised about
                // it, so it has its own container and its own sidecar. See AlignmentFile.
                result = isAlignment
                    ? AberrationCalculator.Optimize.Io.SettingsCommands.ExecuteAlignment(
                          keyword, argument, alignment, lensPath)
                    : AberrationCalculator.Optimize.Io.SettingsCommands.Execute(
                          keyword, argument, setup, lensPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 1;
            }

            if (isAlignment) alignmentChanged |= result.Changed;
            else changed |= result.Changed;
            if (!quiet) Console.Write(result.Output);
        }

        var written = new List<string>();

        if (alignmentChanged)
        {
            string path = AberrationCalculator.Core.IO.AlignmentFile.PathFor(lensPath);
            if (alignment.IsEmpty) { if (File.Exists(path)) File.Delete(path); }
            else
            {
                File.WriteAllText(path,
                    AberrationCalculator.Core.IO.AlignmentFile.Write(
                        alignment, Path.GetFileName(lensPath)));
                written.Add(path);
            }
        }

        if (!changed)
        {
            if (written.Count > 0 && !quiet)
            {
                Console.WriteLine("Written:");
                foreach (string path in written) Console.WriteLine("  " + path);
                Console.WriteLine();
            }
            return 0;
        }

        written.AddRange(AberrationCalculator.Optimize.Io.SettingsStore.Save(system, lensPath, setup));
        if (!quiet)
        {
            Console.WriteLine("Written:");
            foreach (string path in written) Console.WriteLine("  " + path);
            Console.WriteLine();
        }
        return 0;
    }

    /// <summary>
    /// Says what the chains are finding, while they are finding it.
    ///
    /// <para><b>Two kinds of line, and only two.</b> A new global best is the event worth
    /// interrupting for, so it always prints and is marked. Everything else is a heartbeat every
    /// few seconds. Sixteen chains reporting every hop is thousands of lines nobody reads, and a
    /// progress display that scrolls faster than the eye tells you no more than silence does -
    /// but the heartbeat still distinguishes working from hung, which silence cannot.</para>
    ///
    /// <para>Reports arrive from every chain thread at once, so the whole of a line - deciding,
    /// formatting and writing - happens under one lock. Without it two chains interleave
    /// mid-line and the output is unreadable exactly when something interesting is happening.</para>
    /// </summary>
    private sealed class HoppingProgress
        : IProgress<AberrationCalculator.Optimize.Algorithms.BasinHoppingProgress>
    {
        private readonly object _gate = new object();
        private readonly System.Diagnostics.Stopwatch _clock
            = System.Diagnostics.Stopwatch.StartNew();
        private readonly int _total;
        private readonly TimeSpan _heartbeat = TimeSpan.FromSeconds(10);

        private double _best = double.PositiveInfinity;
        private int _done;
        private TimeSpan _lastPrinted = TimeSpan.FromSeconds(-1000);
        private bool _headed;

        public HoppingProgress(int chains, int hops) => _total = chains * hops;

        public void Report(AberrationCalculator.Optimize.Algorithms.BasinHoppingProgress p)
        {
            lock (_gate)
            {
                _done++;

                // A new global best always prints. Anything else waits for the heartbeat.
                bool improved = p.GlobalBest < _best;
                if (improved) _best = p.GlobalBest;
                else if (_clock.Elapsed - _lastPrinted < _heartbeat) return;

                _lastPrinted = _clock.Elapsed;

                if (!_headed)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "        time        hops        global best   accepted  rejected   glass");
                    _headed = true;
                }

                var t = _clock.Elapsed;
                Console.WriteLine(
                    (improved ? "  * " : "    ")
                  + $"{(int)t.TotalMinutes,3}:{t.Seconds:00}"
                  + $"{_done,8}/{_total,-8}"
                  + $"{_best,14:F8}"
                  + $"{p.Accepted,10}{p.Rejected,10}{p.GlassSwaps,8}");
            }
        }

        /// <summary>Closes the display, so the report that follows does not run into it.</summary>
        public void Done()
        {
            lock (_gate) { if (_headed) Console.WriteLine(); }
        }
    }

    /// <summary>
    /// Basin hopping, which produces a design per chain and therefore needs somewhere to put them.
    ///
    /// <para>A folder is REQUIRED, and the run is refused without one. Several chains searching
    /// independently is the point of it - they find different designs, and which of them is
    /// interesting is a judgement only the designer can make. Keeping the single best and
    /// discarding the rest would throw away most of what was paid for.</para>
    /// </summary>
    private static int BasinHop(AberrationCalculator.Core.Models.OpticalSystem system,
                                GlassCatalog catalog, string lensPath, string? meritPath,
                                string? folder,
                                AberrationCalculator.Optimize.RunSettings settings, bool quiet)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            Console.Error.WriteLine(
                "error: --optimize_basin_hopping needs a folder to save into, because it "
              + "produces one design per chain rather than one design. Say --save <folder>.");
            return 1;
        }

        var setup = AberrationCalculator.Optimize.Io.SettingsStore.Load(system, lensPath);
        if (meritPath != null)
        {
            if (!File.Exists(meritPath))
            {
                Console.Error.WriteLine($"error: no such merit function file: {meritPath}");
                return 1;
            }
            setup.Operands.Clear();
            setup.Operands.AddRange(AberrationCalculator.Optimize.Io.MeritFile.Read(meritPath));
        }

        if (setup.Variables.Count == 0)
        {
            Console.Error.WriteLine("error: nothing is declared variable. " + setup.VariableSource);
            return 1;
        }
        if (setup.Operands.Count == 0)
        {
            Console.Error.WriteLine("error: no operands are declared. " + setup.MeritSource);
            return 1;
        }

        // A hopping run takes minutes and used to print nothing at all until it finished, which
        // leaves no way to tell working from hung, no sense of whether the chains are still
        // finding anything, and no basis for choosing --hops next time.
        // Ctrl+C stops the search and KEEPS what it found. The default is three thousand hops
        // and a designer watches the progress and stops when the best stops moving, so being
        // interrupted is the normal way a run ends rather than an error - and a run that threw
        // away an hour of chains because it was asked to stop would be unusable.
        using var stopping = new System.Threading.CancellationTokenSource();
        ConsoleCancelEventHandler? onBreak = (_, e) =>
        {
            e.Cancel = true;              // do not let the runtime kill us mid-write
            if (!stopping.IsCancellationRequested)
            {
                stopping.Cancel();
                Console.WriteLine();
                Console.WriteLine("  stopping - finishing the hop in flight, then writing what "
                                + "each chain found.");
            }
        };
        Console.CancelKeyPress += onBreak;
        settings.Cancellation = stopping.Token;

        // CHECKPOINT EVERY CHAIN THE MOMENT IT IMPROVES. Three thousand hops is hours, and a run
        // that writes only when it finishes loses all of it to a stop that is not a console
        // Ctrl+C - a crash, a reboot, a script, an MCP client. Each chain owns its own filename,
        // so ten chains writing at once need no lock between them.
        Directory.CreateDirectory(folder);
        string chainStem = Path.GetFileNameWithoutExtension(lensPath);
        string chainExtension = Path.GetExtension(lensPath);
        var chainFiles = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
        string? saveRefused = null;

        settings.OnChainBest = (chain, design, merit) =>
        {
            string path = Path.Combine(
                folder,
                chainStem + ".chain" + (chain + 1).ToString("00", CultureInfo.InvariantCulture)
                          + chainExtension);
            try
            {
                if (AberrationCalculator.Optimize.Io.Sidecar.KeepsVariablesInTheLensFile(path))
                {
                    AberrationCalculator.Optimize.Io.SurfaceVariables.Write(setup.Variables, design);
                    design.Pickups.Clear();
                    design.Pickups.AddRange(setup.Pickups);
                }

                AberrationCalculator.Core.IO.LensSave.Save(design, lensPath, path, catalog);
                if (chainFiles.TryAdd(chain, path))
                    AberrationCalculator.Optimize.Io.Sidecar.Save(path, setup);
            }
            catch (IOException)
            {
                // A checkpoint that cannot be written must not bring down a chain that is still
                // searching. The next improvement writes again, and the run's own final write
                // still happens.
            }
            catch (NotSupportedException ex)
            {
                // The format cannot carry what moved (a conic in a CODE V file, say). Every later
                // checkpoint would be refused the same way, so the chain searches on and the
                // reason is reported once, after the run, beside the report that holds the values.
                // A checkpoint this chain wrote before its figuring moved is no longer its best,
                // and a file on disk that looks like the answer and is not is worse than none.
                saveRefused ??= ex.Message;
                if (chainFiles.TryRemove(chain, out string? stale))
                {
                    try
                    {
                        File.Delete(stale);
                        File.Delete(AberrationCalculator.Optimize.Io.Sidecar.MeritPathFor(stale));
                        File.Delete(AberrationCalculator.Optimize.Io.Sidecar.VariablePathFor(stale));
                    }
                    catch (IOException) { }
                }
            }
        };

        HoppingProgress? progress = null;
        if (!quiet)
        {
            progress = new HoppingProgress(
                settings.Chains > 0
                    ? settings.Chains
                    : AberrationCalculator.Optimize.Algorithms.BasinHopping.DefaultChains(),
                settings.Hops);
            settings.Progress = progress;
        }

        var outcome = AberrationCalculator.Optimize.OptimizationRun.Execute(
            system, catalog, setup, settings);
        progress?.Done();
        Console.CancelKeyPress -= onBreak;

        if (!outcome.Ok)
        {
            Console.Error.WriteLine("error: the design could not be evaluated: " + outcome.Failure);
            return 1;
        }

        // The designs are ALREADY on disk - every chain wrote itself the moment it last improved,
        // and each is current. Nothing is written again here, which is what makes a stopped run
        // and a finished run produce the same files. The ranking lives in the report rather than
        // in the filenames, because a run that was stopped cannot know the ranking.
        string stem = Path.GetFileNameWithoutExtension(lensPath);
        var written = new List<string>(chainFiles.Values);
        written.Sort(StringComparer.OrdinalIgnoreCase);

        string report = AberrationCalculator.Optimize.Io.OptimizationReport.Build(
            outcome, Path.GetFileName(lensPath));
        // Keyed to the WHOLE file name, extension included, exactly as the .mf and .var sidecars
        // are. Two designs of the same name in different formats - a .lhlt and the .zmx it was
        // exported to - otherwise share one report path, and the second run overwrites the first
        // run's record of what it changed while both designs survive. Nothing announces that.
        string reportPath = Path.Combine(folder, Path.GetFileName(lensPath) + ".optimisation.txt");
        File.WriteAllText(reportPath, report);
        written.Add(reportPath);

        if (!quiet)
        {
            Console.Write(report);
            Console.WriteLine("Written:");
            foreach (string path in written) Console.WriteLine("  " + path);
        }
        if (saveRefused != null)
        {
            Console.Error.WriteLine("error: the designs were NOT saved. " + saveRefused);
            Console.Error.WriteLine("The optimised values are in " + reportPath);
            return 1;
        }
        return 0;
    }

    private static int Optimise(AberrationCalculator.Core.Models.OpticalSystem system,
                                GlassCatalog catalog, string lensPath, string? meritPath,
                                string? savePath, bool saveInPlace, string? outDir,
                                AberrationCalculator.Optimize.RunSettings settings,
                                bool writeFiles, bool quiet)
    {
        if (meritPath != null && !File.Exists(meritPath))
        {
            Console.Error.WriteLine($"error: no such merit function file: {meritPath}");
            return 1;
        }

        var setup = AberrationCalculator.Optimize.Io.Sidecar.Load(system, lensPath, meritPath);

        if (setup.Variables.Count == 0)
        {
            Console.Error.WriteLine(
                "error: nothing is declared variable, so there is nothing the optimiser is "
              + "allowed to change. " + setup.VariableSource);
            return 1;
        }
        if (setup.Operands.Count == 0)
        {
            Console.Error.WriteLine(
                "error: no operands are declared, so there is nothing to optimise towards. "
              + setup.MeritSource);
            return 1;
        }

        // Pickups read from a .var file belong to the design for the run. For a .lhlt they came
        // from the lens and are already there.
        if (setup.Pickups.Count > 0
            && !AberrationCalculator.Optimize.Io.Sidecar.KeepsVariablesInTheLensFile(lensPath))
        {
            system.Pickups.Clear();
            system.Pickups.AddRange(setup.Pickups);
        }

        var outcome = AberrationCalculator.Optimize.OptimizationRun.Execute(
            system, catalog, setup, settings);

        string report = AberrationCalculator.Optimize.Io.OptimizationReport.Build(
            outcome, Path.GetFileName(lensPath));

        if (!quiet) Console.Write(report);

        if (!outcome.Ok)
        {
            Console.Error.WriteLine("error: the design could not be evaluated: " + outcome.Failure);
            return 1;
        }

        if (!writeFiles) return 0;
        return Save(outcome, setup, lensPath, savePath, saveInPlace, outDir, report, quiet, catalog);
    }


    /// <summary>
    /// Writes the optimised design back in the format it arrived in, and the settings beside it.
    ///
    /// <para>The file that was READ is never touched. What comes out is a new one - the input
    /// name with <c>.optimised</c> before its extension - so a run that made the design worse
    /// costs nothing but the time.</para>
    /// </summary>
    private static int Save(AberrationCalculator.Optimize.RunOutcome outcome,
                            AberrationCalculator.Optimize.Io.OptimizationSetup setup,
                            string lensPath, string? savePath, bool saveInPlace, string? outDir,
                            string report, bool quiet, GlassCatalog catalog)
    {
        string dir = outDir ?? Path.GetDirectoryName(Path.GetFullPath(lensPath)) ?? ".";
        Directory.CreateDirectory(dir);

        string stem = Path.GetFileNameWithoutExtension(lensPath);
        string extension = Path.GetExtension(lensPath);

        // --save with no path overwrites the design that was read, which is what a designer
        // working on their own file wants. --saveas names somewhere else. Neither happening
        // leaves the original alone and writes beside it, so a run costs nothing by accident.
        string lensOut = saveInPlace ? lensPath
                       : savePath ?? Path.Combine(dir, stem + ".optimised" + extension);

        // The design's own statement of what may move travels with it, so a .lhlt goes back
        // carrying its variables, bounds and pickups as well as its new curvatures. Every other
        // format has nowhere to put them and gets a .var file instead.
        if (AberrationCalculator.Optimize.Io.Sidecar.KeepsVariablesInTheLensFile(lensOut))
        {
            AberrationCalculator.Optimize.Io.SurfaceVariables.Write(setup.Variables, outcome.Best);
            outcome.Best.Pickups.Clear();
            outcome.Best.Pickups.AddRange(setup.Pickups);
        }

        try
        {
            // Into its own format, the file is edited; into another, a whole lens is written, and
            // the note says what did not go across.
            foreach (string note in AberrationCalculator.Core.IO.LensSave.Save(outcome.Best, lensPath, lensOut, catalog))
                Console.WriteLine(note);
        }
        catch (NotSupportedException ex)
        {
            // The design could not be written back into its format - an optimised conic in a
            // CODE V file, say. The run is not lost with it: the report says what every value
            // became, so it is written regardless and named.
            string refusedReport = Write(dir, Path.GetFileName(lensPath) + ".optimisation.txt", report);
            Console.Error.WriteLine("error: the design was NOT saved. " + ex.Message);
            Console.Error.WriteLine("The optimised values are in " + refusedReport);
            return 1;
        }

        var written = AberrationCalculator.Optimize.Io.Sidecar.Save(lensOut, setup);
        // The whole file name, as the sidecars use - see BasinHop for why the stem alone is not
        // enough when two formats of one design sit in the same folder.
        string reportOut = Write(dir, Path.GetFileName(lensPath) + ".optimisation.txt", report);

        if (!quiet)
        {
            Console.WriteLine("Written:");
            Console.WriteLine("  " + lensOut);
            foreach (string path in written) Console.WriteLine("  " + path);
            Console.WriteLine("  " + reportOut);
        }
        return 0;
    }
}
