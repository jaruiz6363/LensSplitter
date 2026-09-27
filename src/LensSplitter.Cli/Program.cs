using System.CommandLine;
using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using LensSplitter.Core.Lens;
using LensSplitter.Core.Reporting;
using LensSplitter.Core.Splitting;

namespace LensSplitter.Cli;

public static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Interactive-mode settings, kept for the session.
    private static string? _input;
    private static string? _output;
    private static string? _merit;
    private static string _formats = "";

    public static async Task<int> Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = Inv;
        CultureInfo.CurrentCulture = Inv;
        if (args.Length == 0) return Interactive();

        var root = new RootCommand(
            "LensSplitter - splits a lens element into two, sharing its power, and optimises the pair.\n" +
            "Reads and writes ZEMAX .zmx, CODE V .seq, OSLO .len, OPTALIX .otx, Optiland .json and LensHH-LT .lhlt.");
        root.AddCommand(AnalyzeCommand());
        root.AddCommand(SplitCommand());
        root.AddCommand(GlassCommand());
        root.AddCommand(MeritCommand());
        return await root.InvokeAsync(args);
    }

    // ── Options shared by the commands ────────────────────────────────────────────────────────

    private static Option<FileInfo> InputOption() =>
        new(new[] { "--input", "-i" }, "The lens: .zmx, .seq, .len, .otx, .json (Optiland) or .lhlt") { IsRequired = true };
    private static Option<DirectoryInfo> OutputOption() =>
        new(new[] { "--output", "-o" }, "Folder for the results") { IsRequired = true };
    private static Option<int?> ElementOption() =>
        new(new[] { "--element", "-e" }, "The element to split, from 1; the recommended one if left off");
    private static Option<DirectoryInfo?> CatalogsOption() =>
        new("--catalogs", "A folder of extra .agf glass catalogs, besides the bundled ones");
    private static Option<FileInfo?> MeritOption() =>
        new("--merit", "A merit function file (.mf); the lens's default if left off (see the merit command)");
    private static Option<string> FormatOption() =>
        new("--format", () => "", "Formats to write, comma-separated (zmx,seq,len,otx,json,lhlt) or 'all'; the input's if left off");

    // ── analyze ──────────────────────────────────────────────────────────────────────────────

    private static Command AnalyzeCommand()
    {
        var input = InputOption();
        var catalogs = CatalogsOption();
        var cmd = new Command("analyze", "The lens's first-order numbers, predicted spot and elements, and which to split");
        cmd.AddOption(input);
        cmd.AddOption(catalogs);
        cmd.SetHandler((FileInfo i, DirectoryInfo? c) => Run(() => Analyze(i.FullName, c?.FullName)), input, catalogs);
        return cmd;
    }

    private static void Analyze(string input, string? catalogs)
    {
        var (lens, catalog) = Load(input, catalogs);
        var optics = new LensOptics(lens, catalog);
        Unresolved(optics);
        Console.WriteLine(Report.Lens(optics, input));
        Console.WriteLine(Report.Elements(ElementAnalysis.Analyse(optics)));
    }

    // ── split ────────────────────────────────────────────────────────────────────────────────

    private static Command SplitCommand()
    {
        var input = InputOption();
        var output = OutputOption();
        var element = ElementOption();
        var catalogs = CatalogsOption();
        var merit = MeritOption();
        var format = FormatOption();
        var minEdge = new Option<double>("--min-edge", () => 0.5, "Least edge thickness of the halves and of the air around them");
        var minCentre = new Option<double>("--min-centre", () => 1.0, "Least centre thickness of either half");
        var minGap = new Option<double>("--min-gap", () => 0.1, "Least centre air gap between the halves");
        var iterations = new Option<int>("--iterations", () => 300, "Optimiser iterations per starting point");
        var varyThickness = new Option<bool>("--vary-thickness", "Let the optimiser change the thicknesses too (their sum held)");
        var cmd = new Command("split", "Split an element into two, sharing its power, and optimise the pair");
        foreach (var o in new Option[] { input, output, element, catalogs, merit, format, minEdge, minCentre, minGap, iterations, varyThickness })
            cmd.AddOption(o);
        cmd.SetHandler(ctx =>
        {
            var p = ctx.ParseResult;
            Run(() => Split(p.GetValueForOption(input)!.FullName, p.GetValueForOption(output)!.FullName,
                p.GetValueForOption(element), p.GetValueForOption(catalogs)?.FullName, p.GetValueForOption(merit)?.FullName,
                p.GetValueForOption(format) ?? "",
                new SplitOptions
                {
                    Limits = new SplitLimits
                    {
                        MinEdgeThickness = p.GetValueForOption(minEdge),
                        MinCentreThickness = p.GetValueForOption(minCentre),
                        MinAirGap = p.GetValueForOption(minGap),
                    },
                    Iterations = p.GetValueForOption(iterations),
                    VaryThickness = p.GetValueForOption(varyThickness),
                }));
        });
        return cmd;
    }

    private static void Split(string input, string output, int? element, string? catalogs, string? meritFile, string formats, SplitOptions options)
    {
        var (lens, catalog) = Load(input, catalogs);
        var optics = new LensOptics(lens, catalog);
        Unresolved(optics);
        Console.WriteLine(Report.Lens(optics, input));
        var reports = ElementAnalysis.Analyse(optics);
        Console.WriteLine(Report.Elements(reports));

        int number = Choose(reports, element);
        if (meritFile != null) options.Merit = SplitMerit.Read(meritFile);
        options.Progress = new Progress<string>(m => Console.WriteLine("  " + m));
        Console.WriteLine($"Splitting element {number + 1}...");
        var result = Splitter.Split(lens, catalog, number, options);
        Console.WriteLine();
        string report = Report.Split(result);
        Console.WriteLine(report);

        Directory.CreateDirectory(output);
        string stem = Path.Combine(output, $"{Path.GetFileNameWithoutExtension(input)}_split{number + 1}");
        result.Split.System.Title = Title(lens, $"element {number + 1} split");
        foreach (var path in Write(result.Split.System, stem, Formats(input, formats), catalog))
            Console.WriteLine($"Written: {path}");
        File.WriteAllText(stem + ".txt", Report.Lens(optics, input) + Environment.NewLine + Report.Elements(reports)
                                         + Environment.NewLine + report);
        File.WriteAllText(stem + ".svg", LensDrawing.Svg(new[]
        {
            ("Original: " + Title(lens, ""), optics),
            ($"Element {number + 1} split", result.Split),
        }, result.Front, result.Front + 3));
        Console.WriteLine($"Written: {stem}.txt, {stem}.svg");
    }

    // ── glass ────────────────────────────────────────────────────────────────────────────────

    private static Command GlassCommand()
    {
        var input = InputOption();
        var output = OutputOption();
        var element = ElementOption();
        var catalogs = CatalogsOption();
        var merit = MeritOption();
        var format = FormatOption();
        var glasses = new Option<string>(new[] { "--glasses", "-g" }, () => "",
            "Glasses to choose from, comma-separated; AberrationCalculator's CoreSet28 set if left off");
        var refine = new Option<int>("--refine", () => 8, "How many of the best-screened pairs to split and optimise in full");
        var top = new Option<int>(new[] { "--top", "-n" }, () => 3, "How many of the best splits to write as lenses");
        var cmd = new Command("glass", "Choose the glasses for the two halves of a split");
        foreach (var o in new Option[] { input, output, element, catalogs, merit, format, glasses, refine, top })
            cmd.AddOption(o);
        cmd.SetHandler(ctx =>
        {
            var p = ctx.ParseResult;
            Run(() => Glass(p.GetValueForOption(input)!.FullName, p.GetValueForOption(output)!.FullName,
                p.GetValueForOption(element), p.GetValueForOption(catalogs)?.FullName, p.GetValueForOption(merit)?.FullName,
                p.GetValueForOption(format) ?? "", p.GetValueForOption(glasses) ?? "",
                p.GetValueForOption(refine), p.GetValueForOption(top)));
        });
        return cmd;
    }

    private static void Glass(string input, string output, int? element, string? catalogs, string? meritFile,
                              string formats, string glassList, int refine, int top)
    {
        var (lens, catalog) = Load(input, catalogs);
        var optics = new LensOptics(lens, catalog);
        Unresolved(optics);
        Console.WriteLine(Report.Lens(optics, input));
        var reports = ElementAnalysis.Analyse(optics);
        Console.WriteLine(Report.Elements(reports));
        int number = Choose(reports, element);

        var options = new GlassSearchOptions
        {
            Refine = refine,
            Glasses = string.IsNullOrWhiteSpace(glassList) ? null
                : glassList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Progress = new Progress<string>(m => Console.WriteLine("  " + m)),
        };
        if (meritFile != null) options.Split.Merit = SplitMerit.Read(meritFile);
        Console.WriteLine($"Choosing glasses for element {number + 1}...");
        var pairs = GlassSearch.Search(lens, catalog, number, options);
        Console.WriteLine();
        string ranking = Report.Glasses(pairs, Math.Max(refine, 20));
        Console.WriteLine(ranking);

        Directory.CreateDirectory(output);
        string stem = Path.Combine(output, $"{Path.GetFileNameWithoutExtension(input)}_glass{number + 1}");
        var csv = new StringBuilder("rank,glass_a,glass_b,merit,predicted_spot,screened\n");
        int rank = 0;
        foreach (var p in pairs)
            csv.AppendLine(string.Join(",", ++rank, p.A, p.B,
                p.Result == null ? "" : p.Merit.ToString("R", Inv),
                p.Result == null ? "" : p.Result.Split.Prmsa.ToString("R", Inv), p.Screen.ToString("R", Inv)));
        File.WriteAllText(stem + ".csv", csv.ToString());
        var text = new StringBuilder(Report.Lens(optics, input) + Environment.NewLine + ranking);
        rank = 0;
        foreach (var p in pairs.Where(x => x.Result != null).Take(Math.Max(0, top)))
        {
            rank++;
            string name = $"{stem}_{rank}_{Safe(p.A)}+{Safe(p.B)}";
            p.Result!.Split.System.Title = Title(lens, $"element {number + 1} split, {p.A} + {p.B}");
            foreach (var path in Write(p.Result.Split.System, name, Formats(input, formats), catalog))
                Console.WriteLine($"Written: {path}");
            text.AppendLine().AppendLine($"#{rank}: {p.A} + {p.B}").AppendLine(Report.Split(p.Result));
        }
        File.WriteAllText(stem + ".txt", text.ToString());
        Console.WriteLine($"Written: {stem}.csv, {stem}.txt");
    }

    // ── merit ────────────────────────────────────────────────────────────────────────────────

    private static Command MeritCommand()
    {
        var input = InputOption();
        var output = new Option<FileInfo>(new[] { "--output", "-o" }, "The merit function file to write (.mf)") { IsRequired = true };
        var catalogs = CatalogsOption();
        var cmd = new Command("merit", "Write the lens's default merit function to a file, to edit and pass back with --merit");
        cmd.AddOption(input);
        cmd.AddOption(output);
        cmd.AddOption(catalogs);
        cmd.SetHandler((FileInfo i, FileInfo o, DirectoryInfo? c) => Run(() =>
        {
            var (lens, catalog) = Load(i.FullName, c?.FullName);
            File.WriteAllText(o.FullName, SplitMerit.DefaultTextFor(new LensOptics(lens, catalog)));
            Console.WriteLine($"Written: {o.FullName}");
        }), input, output, catalogs);
        return cmd;
    }

    // ── Shared ───────────────────────────────────────────────────────────────────────────────

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is NotSupportedException
                                   || ex is ArgumentException || ex is FormatException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: " + ex.Message);
            Console.ResetColor();
            Environment.ExitCode = 1;
        }
    }

    private static (OpticalSystem Lens, GlassCatalog Catalog) Load(string input, string? catalogs)
    {
        var catalog = CatalogLocator.LoadBundled();
        if (catalogs != null) catalog.LoadFolder(catalogs);
        return (LensFile.Read(input, catalog), catalog);
    }

    private static void Unresolved(LensOptics optics)
    {
        if (optics.Unresolved.Count > 0)
            throw new InvalidOperationException(
                $"The glass catalogs do not have {string.Join(", ", optics.Unresolved)}. Add a folder of .agf files with --catalogs.");
    }

    private static int Choose(IReadOnlyList<ElementReport> reports, int? element)
    {
        if (element is int e)
        {
            if (e < 1 || e > reports.Count)
                throw new ArgumentException($"The lens has {reports.Count} elements; there is no element {e}.");
            var r = reports[e - 1];
            if (r.NotSplittable != null)
                Console.WriteLine($"Element {e} would not be recommended ({r.NotSplittable}); splitting it as asked.");
            return e - 1;
        }
        var rec = ElementAnalysis.Recommend(reports)
            ?? throw new InvalidOperationException(
                "No element is recommended for splitting: none that can be split is adding to the spot. Name one with --element.");
        return rec.Element.Number;
    }

    // The formats to write: a list, 'all', or the input's own.
    private static IReadOnlyList<string> Formats(string input, string formats)
    {
        if (formats.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)) return LensFile.WritableExtensions;
        if (string.IsNullOrWhiteSpace(formats))
        {
            string ext = Path.GetExtension(input).ToLowerInvariant();
            ext = ext == ".opt" ? ".otx" : ext == ".osl" ? ".len" : ext;
            return new[] { ext };
        }
        var list = new List<string>();
        foreach (var f in formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string ext = "." + f.TrimStart('.').ToLowerInvariant();
            if (!LensFile.WritableExtensions.Contains(ext))
                throw new ArgumentException($"'{f}' is not a format LensSplitter writes: {string.Join(", ", LensFile.WritableExtensions)}.");
            list.Add(ext);
        }
        return list;
    }

    // Each format that can carry the lens; one that cannot says why, and the rest are still written.
    private static List<string> Write(OpticalSystem lens, string stem, IEnumerable<string> formats, GlassCatalog catalog)
    {
        var written = new List<string>();
        foreach (var ext in formats)
        {
            string path = stem + ext;
            try
            {
                foreach (var note in LensFile.Write(lens, path, catalog)) Console.WriteLine("  " + note);
                written.Add(path);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Not written as {ext}: {ex.Message}");
            }
        }
        return written;
    }

    private static string Title(OpticalSystem lens, string what) =>
        string.IsNullOrWhiteSpace(what) ? lens.Title
        : string.IsNullOrWhiteSpace(lens.Title) ? what : $"{lens.Title} - {what}";

    private static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
        return sb.ToString();
    }

    // ── Interactive mode ─────────────────────────────────────────────────────────────────────

    private static int Interactive()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("LensSplitter 2 - splits a lens element into two and optimises the pair");
            Console.WriteLine($"  Lens:     {_input ?? "(not set)"}");
            Console.WriteLine($"  Output:   {_output ?? "(not set)"}");
            Console.WriteLine($"  Merit:    {_merit ?? "the lens's default (predicted spot, and colour)"}");
            Console.WriteLine($"  Formats:  {(string.IsNullOrWhiteSpace(_formats) ? "the lens's own" : _formats)}");
            Console.WriteLine();
            Console.WriteLine("  [1] Analyze   [2] Split   [3] Glass   [4] Write the default merit function");
            Console.WriteLine("  [L] Lens      [O] Output  [M] Merit file  [F] Formats   [Q] Quit");
            Console.Write("> ");
            string choice = (Console.ReadLine() ?? "Q").Trim().ToUpperInvariant();
            switch (choice)
            {
                case "1": if (NeedLens()) Run(() => Analyze(_input!, null)); break;
                case "2":
                    if (NeedLens() && NeedOutput())
                        Run(() => Split(_input!, _output!, AskElement(), null, _merit, _formats, new SplitOptions()));
                    break;
                case "3":
                    if (NeedLens() && NeedOutput())
                        Run(() => Glass(_input!, _output!, AskElement(), null, _merit, _formats, Ask("Glasses (comma-separated, blank for CoreSet28)"), 8, 3));
                    break;
                case "4":
                    if (NeedLens())
                    {
                        string path = Ask("Write the merit function to", Path.ChangeExtension(_input!, ".mf"));
                        Run(() =>
                        {
                            var (lens, catalog) = Load(_input!, null);
                            File.WriteAllText(path, SplitMerit.DefaultTextFor(new LensOptics(lens, catalog)));
                            Console.WriteLine($"Written: {path} - edit it, then choose it with [M].");
                        });
                    }
                    break;
                case "L": _input = AskFile("Lens file"); break;
                case "O": _output = Ask("Output folder", _output); break;
                case "M":
                    string m = Ask("Merit function file (blank for the default)");
                    _merit = string.IsNullOrWhiteSpace(m) ? null : m;
                    break;
                case "F": _formats = Ask("Formats (zmx,seq,len,otx,json,lhlt, 'all', or blank for the lens's own)"); break;
                case "Q": case "QUIT": case "EXIT": return 0;
            }
        }
    }

    private static bool NeedLens()
    {
        if (_input == null) _input = AskFile("Lens file");
        return _input != null;
    }

    private static bool NeedOutput()
    {
        if (_output == null) _output = Ask("Output folder", Path.GetDirectoryName(Path.GetFullPath(_input!)));
        return !string.IsNullOrWhiteSpace(_output);
    }

    private static int? AskElement()
    {
        string s = Ask("Element to split (blank for the recommended one)");
        return int.TryParse(s, NumberStyles.Integer, Inv, out int e) ? e : null;
    }

    private static string? AskFile(string prompt)
    {
        string s = Ask(prompt).Trim('"');
        if (File.Exists(s)) return s;
        Console.WriteLine($"No such file: {s}");
        return null;
    }

    private static string Ask(string prompt, string? current = null)
    {
        Console.Write(current == null ? $"{prompt}: " : $"{prompt} [{current}]: ");
        string s = (Console.ReadLine() ?? "").Trim();
        return s.Length == 0 && current != null ? current : s;
    }
}
