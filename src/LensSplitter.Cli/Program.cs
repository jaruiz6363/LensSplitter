using System.CommandLine;
using LensSplitter.Core.Aberrations;
using LensSplitter.Core.Paraxial;
using LensSplitter.Core.Splitting;
using LensSplitter.Parsing;
using LensSplitter.Parsing.Export;
using LensSplitter.Parsing.Glass;

namespace LensSplitter.Cli;

class Program
{
    // Persisted settings for interactive mode
    private static string? _lastInputFile;
    private static string? _lastOutputDir;
    private static string? _lastCatalogPath;
    private static AberrationWeights _currentWeights = AberrationWeights.Default;

    static async Task<int> Main(string[] args)
    {
        // If no arguments provided, run interactive mode
        if (args.Length == 0)
        {
            return await RunInteractiveMode();
        }

        var rootCommand = new RootCommand("LensSplitter - Optical lens element splitting using power-preserving splitting");

        // Split command
        var splitCommand = CreateSplitCommand();
        rootCommand.AddCommand(splitCommand);

        // Analyze command
        var analyzeCommand = CreateAnalyzeCommand();
        rootCommand.AddCommand(analyzeCommand);

        // Glass optimization command
        var optimizeGlassCommand = CreateOptimizeGlassCommand();
        rootCommand.AddCommand(optimizeGlassCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> RunInteractiveMode()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                         LENSSPLITTER v1.2                            ║");
        Console.WriteLine("║            Power-Preserving Optical Element Splitting                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            // Show current settings
            Console.WriteLine("┌─────────────────────────────────────────────────────────────────────┐");
            if (!string.IsNullOrEmpty(_lastInputFile))
                Console.WriteLine($"│ Input:  {TruncatePath(_lastInputFile, 60),-60} │");
            else
                Console.WriteLine("│ Input:  (not set)                                                  │");
            if (!string.IsNullOrEmpty(_lastOutputDir))
                Console.WriteLine($"│ Output: {TruncatePath(_lastOutputDir, 60),-60} │");
            else
                Console.WriteLine("│ Output: (not set)                                                  │");
            if (!string.IsNullOrEmpty(_lastCatalogPath))
                Console.WriteLine($"│ Catalog: {TruncatePath(_lastCatalogPath, 59),-59} │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("What would you like to do?");
            Console.WriteLine();
            Console.WriteLine("  1. Split a lens element (same glass for both elements)");
            Console.WriteLine("  2. Analyze an optical system");
            Console.WriteLine("  3. Split and optimize glass selection (try glass combinations)");
            Console.WriteLine("  4. Show command-line help");
            Console.WriteLine("  S. Set input/output/catalog paths");
            Console.WriteLine("  W. Configure aberration weights");
            Console.WriteLine("  Q. Quit");
            Console.WriteLine();
            Console.Write("Enter choice (1-4, S, W, or Q): ");

            var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

            switch (choice)
            {
                case "1":
                    await RunInteractiveSplit();
                    break;
                case "2":
                    await RunInteractiveAnalyze();
                    break;
                case "3":
                    await RunInteractiveOptimizeGlass();
                    break;
                case "4":
                    ShowCommandLineHelp();
                    break;
                case "S":
                    ConfigurePaths();
                    break;
                case "W":
                    ConfigureWeights();
                    break;
                case "Q":
                case "QUIT":
                case "EXIT":
                    Console.WriteLine("Goodbye!");
                    return 0;
                default:
                    Console.WriteLine("Invalid choice. Please enter 1-4, S, W, or Q.\n");
                    break;
            }
        }
    }

    static string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        return "..." + path.Substring(path.Length - maxLength + 3);
    }

    static void ConfigurePaths()
    {
        Console.WriteLine("\n── Configure Paths ──\n");

        Console.Write($"Input file [{_lastInputFile ?? "not set"}]: ");
        var input = Console.ReadLine()?.Trim().Trim('"');
        if (!string.IsNullOrEmpty(input))
        {
            if (File.Exists(input))
                _lastInputFile = input;
            else
                Console.WriteLine($"  Warning: File not found: {input}");
        }

        Console.Write($"Output directory [{_lastOutputDir ?? "not set"}]: ");
        var output = Console.ReadLine()?.Trim().Trim('"');
        if (!string.IsNullOrEmpty(output))
            _lastOutputDir = output;

        Console.Write($"Glass catalog [{_lastCatalogPath ?? "not set"}]: ");
        var catalog = Console.ReadLine()?.Trim().Trim('"');
        if (!string.IsNullOrEmpty(catalog))
        {
            if (Directory.Exists(catalog) || File.Exists(catalog))
                _lastCatalogPath = catalog;
            else
                Console.WriteLine($"  Warning: Path not found: {catalog}");
        }

        Console.WriteLine("\nPaths updated.\n");
    }

    static void ConfigureWeights()
    {
        Console.WriteLine("\n── Configure Aberration Weights ──\n");

        Console.WriteLine("Current Seidel (3rd-order) weights:");
        Console.WriteLine($"  W1  (Spherical):    {_currentWeights.W1}");
        Console.WriteLine($"  W2  (Coma):         {_currentWeights.W2}");
        Console.WriteLine($"  W3  (Astigmatism):  {_currentWeights.W3}");
        Console.WriteLine($"  W4  (Petzval):      {_currentWeights.W4}");
        Console.WriteLine($"  W5  (Distortion):   {_currentWeights.W5}");
        Console.WriteLine($"  WCL (Axial Color):  {_currentWeights.WCL}");
        Console.WriteLine($"  WCT (Lat. Color):   {_currentWeights.WCT}");
        Console.WriteLine($"  IncludeChromatic:   {_currentWeights.IncludeChromatic}");
        Console.WriteLine();
        Console.WriteLine("Current Buchdahl (5th-order) weights:");
        Console.WriteLine($"  WBSph (Spherical, Ap):        {_currentWeights.WBSph}");
        Console.WriteLine($"  WBCma (Coma, Aq):             {_currentWeights.WBCma}");
        Console.WriteLine($"  WBObl (Oblique Sph, Bp):      {_currentWeights.WBObl}");
        Console.WriteLine($"  WBEll (Elliptical Coma, Bq):  {_currentWeights.WBEll}");
        Console.WriteLine($"  WBAst (Astigmatism, Cp):      {_currentWeights.WBAst}");
        Console.WriteLine($"  WBDst (Distortion, Cq):       {_currentWeights.WBDst}");
        Console.WriteLine($"  IncludeBuchdahl:               {_currentWeights.IncludeBuchdahl}");
        Console.WriteLine();

        if (PromptForYesNo("Edit Seidel weights?", false))
        {
            _currentWeights.W1 = PromptForDouble("  W1  (Spherical)", _currentWeights.W1);
            _currentWeights.W2 = PromptForDouble("  W2  (Coma)", _currentWeights.W2);
            _currentWeights.W3 = PromptForDouble("  W3  (Astigmatism)", _currentWeights.W3);
            _currentWeights.W4 = PromptForDouble("  W4  (Petzval)", _currentWeights.W4);
            _currentWeights.W5 = PromptForDouble("  W5  (Distortion)", _currentWeights.W5);
            _currentWeights.WCL = PromptForDouble("  WCL (Axial Color)", _currentWeights.WCL);
            _currentWeights.WCT = PromptForDouble("  WCT (Lat. Color)", _currentWeights.WCT);
            _currentWeights.IncludeChromatic = PromptForYesNo("  Include chromatic?", _currentWeights.IncludeChromatic);
        }

        if (PromptForYesNo("Edit Buchdahl weights?", false))
        {
            _currentWeights.WBSph = PromptForDouble("  WBSph (Spherical, Ap)", _currentWeights.WBSph);
            _currentWeights.WBCma = PromptForDouble("  WBCma (Coma, Aq)", _currentWeights.WBCma);
            _currentWeights.WBObl = PromptForDouble("  WBObl (Oblique Sph, Bp)", _currentWeights.WBObl);
            _currentWeights.WBEll = PromptForDouble("  WBEll (Elliptical Coma, Bq)", _currentWeights.WBEll);
            _currentWeights.WBAst = PromptForDouble("  WBAst (Astigmatism, Cp)", _currentWeights.WBAst);
            _currentWeights.WBDst = PromptForDouble("  WBDst (Distortion, Cq)", _currentWeights.WBDst);
            _currentWeights.IncludeBuchdahl = _currentWeights.HasNonZeroBuchdahlWeights;
            Console.WriteLine($"  IncludeBuchdahl auto-set to: {_currentWeights.IncludeBuchdahl}");
        }

        Console.WriteLine($"\nUpdated weights: {_currentWeights}");
        Console.WriteLine();
    }

    static string PromptForFile(string prompt, string? defaultValue = null, bool mustExist = true)
    {
        while (true)
        {
            if (!string.IsNullOrEmpty(defaultValue))
                Console.Write($"{prompt} [{Path.GetFileName(defaultValue)}]: ");
            else
                Console.Write(prompt);

            var path = Console.ReadLine()?.Trim().Trim('"');

            if (string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(defaultValue))
                    return defaultValue;
                Console.WriteLine("Path cannot be empty.\n");
                continue;
            }

            if (mustExist && !File.Exists(path))
            {
                Console.WriteLine($"File not found: {path}\n");
                continue;
            }

            return path;
        }
    }

    static string PromptForDirectory(string prompt, string? defaultValue = null)
    {
        if (!string.IsNullOrEmpty(defaultValue))
            Console.Write($"{prompt} [{defaultValue}]: ");
        else
            Console.Write($"{prompt} [.]: ");

        var path = Console.ReadLine()?.Trim().Trim('"');

        if (string.IsNullOrEmpty(path))
        {
            return defaultValue ?? ".";
        }

        return path;
    }

    static int? PromptForOptionalInt(string prompt, int? defaultValue = null)
    {
        var defaultStr = defaultValue.HasValue ? $" [{defaultValue}]" : " [auto]";
        Console.Write($"{prompt}{defaultStr}: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return defaultValue;
        }

        if (int.TryParse(input, out int result))
        {
            return result;
        }

        return defaultValue;
    }

    static double PromptForDouble(string prompt, double defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return defaultValue;
        }

        if (double.TryParse(input, out double result))
        {
            return result;
        }

        return defaultValue;
    }

    static bool PromptForYesNo(string prompt, bool defaultValue = true)
    {
        var defaultStr = defaultValue ? "[Y/n]" : "[y/N]";
        Console.Write($"{prompt} {defaultStr}: ");
        var input = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(input))
        {
            return defaultValue;
        }

        return input.StartsWith("Y");
    }

    static async Task RunInteractiveSplit()
    {
        Console.WriteLine("\n── Split Lens Element ──\n");

        var inputPath = PromptForFile("Input lens file (ZMX or JSON): ", _lastInputFile);
        _lastInputFile = inputPath;

        var outputDir = PromptForDirectory("Output directory", _lastOutputDir);
        _lastOutputDir = outputDir;

        // Glass catalog option
        FileInfo? catalogFile = null;
        if (!string.IsNullOrEmpty(_lastCatalogPath))
            Console.Write($"Glass catalog [{Path.GetFileName(_lastCatalogPath)}]: ");
        else
            Console.Write("Glass catalog (Enter to skip): ");
        var catalogPath = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrEmpty(catalogPath) && !string.IsNullOrEmpty(_lastCatalogPath))
            catalogPath = _lastCatalogPath;
        if (!string.IsNullOrEmpty(catalogPath) && (File.Exists(catalogPath) || Directory.Exists(catalogPath)))
        {
            _lastCatalogPath = catalogPath;
            catalogFile = new FileInfo(catalogPath);
        }

        var elementIndex = PromptForOptionalInt("Element number to split (1-N)", null);
        var optimize = PromptForYesNo("Use iterative optimization?", true);

        double gap = 0.1;
        double minRatio = 0.3;
        double maxRatio = 0.7;

        bool enforceGeometry = true;
        if (optimize)
        {
            minRatio = PromptForDouble("Min power ratio", 0.3);
            maxRatio = PromptForDouble("Max power ratio", 0.7);
            enforceGeometry = PromptForYesNo("Enforce geometry constraints?", true);
        }
        else
        {
            gap = PromptForDouble("Air gap (mm)", 0.1);
        }

        Console.WriteLine();

        await HandleSplit(
            new FileInfo(inputPath),
            new DirectoryInfo(outputDir),
            elementIndex,
            gap,
            catalogFile,
            optimize,
            minRatio,
            maxRatio,
            enforceGeometry: enforceGeometry);

        Console.WriteLine("\nPress Enter to continue...");
        Console.ReadLine();
        Console.WriteLine();
    }

    static async Task RunInteractiveAnalyze()
    {
        Console.WriteLine("\n── Analyze Optical System ──\n");

        var inputPath = PromptForFile("Input lens file (ZMX or JSON): ", _lastInputFile);
        _lastInputFile = inputPath;

        // Glass catalog option for resolving glass names
        FileInfo? catalogFile = null;
        if (!string.IsNullOrEmpty(_lastCatalogPath) && (File.Exists(_lastCatalogPath) || Directory.Exists(_lastCatalogPath)))
        {
            catalogFile = new FileInfo(_lastCatalogPath);
        }

        Console.WriteLine();

        await HandleAnalyze(new FileInfo(inputPath), catalogFile);

        Console.WriteLine("\nPress Enter to continue...");
        Console.ReadLine();
        Console.WriteLine();
    }

    static async Task RunInteractiveOptimizeGlass()
    {
        Console.WriteLine("\n── Split and Optimize Glass Selection ──\n");
        Console.WriteLine("This will split an element and try different glass combinations");
        Console.WriteLine("for the resulting doublet to minimize aberrations.\n");

        var inputPath = PromptForFile("Input lens file (ZMX or JSON): ", _lastInputFile);
        _lastInputFile = inputPath;

        var outputDir = PromptForDirectory("Output directory", _lastOutputDir);
        _lastOutputDir = outputDir;

        // Glass catalog option
        FileInfo? catalogFile = null;
        if (!string.IsNullOrEmpty(_lastCatalogPath))
            Console.Write($"Glass catalog [{Path.GetFileName(_lastCatalogPath)}]: ");
        else
            Console.Write("Glass catalog (Enter to skip): ");
        var catalogPath = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrEmpty(catalogPath) && !string.IsNullOrEmpty(_lastCatalogPath))
            catalogPath = _lastCatalogPath;
        if (!string.IsNullOrEmpty(catalogPath) && (File.Exists(catalogPath) || Directory.Exists(catalogPath)))
        {
            _lastCatalogPath = catalogPath;
            catalogFile = new FileInfo(catalogPath);
        }

        Console.Write($"Glass names (Enter for default {DefaultGlasses.S1GlassNames.Length} glasses): ");
        var glassInput = Console.ReadLine()?.Trim();
        var glasses = string.IsNullOrEmpty(glassInput) ? DefaultGlasses.DefaultGlassString : glassInput;

        var elementIndex = PromptForOptionalInt("Element number to split (1-N)", null);
        var trials = (int)PromptForDouble("Number of trials", 1000);
        var top = (int)PromptForDouble("Top results to keep", 100);
        var eflTolerance = PromptForDouble("Max EFL deviation % (0 to disable)", 3.0);

        Console.WriteLine();

        await HandleOptimizeGlass(
            new FileInfo(inputPath),
            new DirectoryInfo(outputDir),
            glasses,
            elementIndex,
            trials,
            top,
            catalogFile,
            eflTolerance);

        Console.WriteLine("\nPress Enter to continue...");
        Console.ReadLine();
        Console.WriteLine();
    }

    static void ShowCommandLineHelp()
    {
        Console.WriteLine("\n── Command-Line Usage ──\n");
        Console.WriteLine("LensSplitter can also be run from the command line:\n");
        Console.WriteLine("  lenssplitter split -i <input> -o <output-dir> [options]");
        Console.WriteLine("  lenssplitter analyze -i <input>");
        Console.WriteLine("  lenssplitter optimize-glass -i <input> -o <output-dir> -g <glasses>\n");
        Console.WriteLine("Examples:");
        Console.WriteLine("  lenssplitter split -i lens.zmx -o ./output");
        Console.WriteLine("  lenssplitter split -i lens.zmx -o ./output -e 0 --no-optimize -g 0.5");
        Console.WriteLine("  lenssplitter analyze -i lens.zmx");
        Console.WriteLine("  lenssplitter optimize-glass -i lens.zmx -o ./results -g \"N-BK7,N-SK16,N-SF6\" -t 1000\n");
        Console.WriteLine("For full help on any command, use: lenssplitter <command> --help\n");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
        Console.WriteLine();
    }

    static Command CreateOptimizeGlassCommand()
    {
        var inputOption = new Option<FileInfo>(
            aliases: new[] { "--input", "-i" },
            description: "Input lens file (ZMX or Optiland JSON)")
        { IsRequired = true };

        var outputDirOption = new Option<DirectoryInfo>(
            aliases: new[] { "--output-dir", "-o" },
            description: "Output directory for results")
        { IsRequired = true };

        var glassesOption = new Option<string>(
            aliases: new[] { "--glasses", "-g" },
            description: "Comma-separated list of glass names to try. Uses Schott S1_GLASS catalog (28 glasses) by default.",
            getDefaultValue: () => DefaultGlasses.DefaultGlassString);

        var elementOption = new Option<int?>(
            aliases: new[] { "--element", "-e" },
            description: "Element number to split (1-based, auto-selects if not specified)");

        var trialsOption = new Option<int>(
            aliases: new[] { "--trials", "-t" },
            description: "Number of random combinations to try",
            getDefaultValue: () => 1000);

        var topOption = new Option<int>(
            aliases: new[] { "--top", "-n" },
            description: "Number of top results to keep",
            getDefaultValue: () => 100);

        var catalogOption = new Option<FileInfo?>(
            aliases: new[] { "--catalog", "-c" },
            description: "Additional AGF glass catalog file");

        var eflToleranceOption = new Option<double>(
            aliases: new[] { "--efl-tolerance", "--efl" },
            description: "Maximum EFL deviation allowed (as percentage, e.g., 3 for 3%). Set to 0 to disable EFL constraint.",
            getDefaultValue: () => 3.0);

        var noChromaticOption = new Option<bool>(
            aliases: new[] { "--no-chromatic" },
            description: "Disable chromatic aberration in merit function. Default: enabled (auto-disabled if only 1 wavelength).",
            getDefaultValue: () => false);

        var command = new Command("optimize-glass", "Optimize glass selection for split elements")
        {
            inputOption,
            outputDirOption,
            glassesOption,
            elementOption,
            trialsOption,
            topOption,
            catalogOption,
            eflToleranceOption,
            noChromaticOption
        };

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption)!;
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption)!;
            var glasses = context.ParseResult.GetValueForOption(glassesOption)!;
            var element = context.ParseResult.GetValueForOption(elementOption);
            var trials = context.ParseResult.GetValueForOption(trialsOption);
            var top = context.ParseResult.GetValueForOption(topOption);
            var catalog = context.ParseResult.GetValueForOption(catalogOption);
            var eflTolerance = context.ParseResult.GetValueForOption(eflToleranceOption);
            var noChromatic = context.ParseResult.GetValueForOption(noChromaticOption);

            await HandleOptimizeGlass(input, outputDir, glasses, element, trials, top, catalog, eflTolerance, noChromatic);
        });

        return command;
    }

    static Command CreateSplitCommand()
    {
        var inputOption = new Option<FileInfo>(
            aliases: new[] { "--input", "-i" },
            description: "Input lens file (ZMX or Optiland JSON)")
        { IsRequired = true };

        var outputDirOption = new Option<DirectoryInfo>(
            aliases: new[] { "--output-dir", "-o" },
            description: "Output directory for generated files")
        { IsRequired = true };

        var elementOption = new Option<int?>(
            aliases: new[] { "--element", "-e" },
            description: "Element number to split (1-based). If not specified, auto-selects the best element.");

        var gapOption = new Option<double>(
            aliases: new[] { "--gap", "-g" },
            description: "Air gap between split elements in mm (used when --optimize is false)",
            getDefaultValue: () => 0.1);

        var catalogOption = new Option<FileInfo?>(
            aliases: new[] { "--catalog", "-c" },
            description: "Additional AGF glass catalog file");

        var optimizeOption = new Option<bool>(
            aliases: new[] { "--optimize", "-O" },
            description: "Use iterative optimization to minimize spherical aberration",
            getDefaultValue: () => true);

        var minRatioOption = new Option<double>(
            aliases: new[] { "--min-ratio" },
            description: "Minimum power ratio for optimization search",
            getDefaultValue: () => 0.3);

        var maxRatioOption = new Option<double>(
            aliases: new[] { "--max-ratio" },
            description: "Maximum power ratio for optimization search",
            getDefaultValue: () => 0.7);

        var edgeClearanceOption = new Option<double>(
            aliases: new[] { "--edge-clearance" },
            description: "Minimum edge clearance to prevent lens collision (mm)",
            getDefaultValue: () => 0.5);

        var enforceGeometryOption = new Option<bool>(
            aliases: new[] { "--enforce-geometry" },
            description: "Enforce geometry constraints even if it changes total track length",
            getDefaultValue: () => true);

        var command = new Command("split", "Split a lens element using power-preserving splitting")
        {
            inputOption,
            outputDirOption,
            elementOption,
            gapOption,
            catalogOption,
            optimizeOption,
            minRatioOption,
            maxRatioOption,
            edgeClearanceOption,
            enforceGeometryOption
        };

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption)!;
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption)!;
            var element = context.ParseResult.GetValueForOption(elementOption);
            var gap = context.ParseResult.GetValueForOption(gapOption);
            var catalog = context.ParseResult.GetValueForOption(catalogOption);
            var optimize = context.ParseResult.GetValueForOption(optimizeOption);
            var minRatio = context.ParseResult.GetValueForOption(minRatioOption);
            var maxRatio = context.ParseResult.GetValueForOption(maxRatioOption);
            var edgeClearance = context.ParseResult.GetValueForOption(edgeClearanceOption);
            var enforceGeometry = context.ParseResult.GetValueForOption(enforceGeometryOption);

            await HandleSplit(input, outputDir, element, gap, catalog, optimize, minRatio, maxRatio,
                edgeClearance, enforceGeometry);
        });

        return command;
    }

    static Command CreateAnalyzeCommand()
    {
        var inputOption = new Option<FileInfo>(
            aliases: new[] { "--input", "-i" },
            description: "Input lens file (ZMX or Optiland JSON)")
        { IsRequired = true };

        var catalogOption = new Option<FileInfo?>(
            aliases: new[] { "--catalog", "-c" },
            description: "Additional AGF glass catalog file");

        var command = new Command("analyze", "Analyze an optical system")
        {
            inputOption,
            catalogOption
        };

        command.SetHandler(async (FileInfo input, FileInfo? catalog) =>
        {
            await HandleAnalyze(input, catalog);
        }, inputOption, catalogOption);

        return command;
    }

    static async Task HandleSplit(FileInfo input, DirectoryInfo outputDir, int? element, double gap,
        FileInfo? catalog, bool optimize, double minRatio, double maxRatio,
        double edgeClearance = 0.5, bool enforceGeometry = true)
    {
        try
        {
            // Setup glass catalog
            var glassCatalog = new GlassCatalogManager();
            glassCatalog.LoadDefaultCatalogs();

            if (glassCatalog.LoadedCatalogs.Count > 0)
            {
                Console.WriteLine($"Loaded glass catalogs: {string.Join(", ", glassCatalog.LoadedCatalogs)} ({glassCatalog.GlassCount} glasses)");
            }
            else
            {
                Console.WriteLine($"WARNING: No default glass catalogs found in: {GlassCatalogManager.DefaultCatalogFolder}");
            }

            if (catalog != null && catalog.Exists)
            {
                var count = glassCatalog.LoadCatalog(catalog.FullName);
                Console.WriteLine($"Loaded additional {count} glasses from {catalog.Name}");
            }

            Console.WriteLine($"\nLoading system from: {input.FullName}");

            // Load optical system
            var loader = new OpticalSystemLoader(glassCatalog, autoLoadCatalogs: false);
            var system = loader.Load(input.FullName);

            // Validate field types
            if (!ValidateFieldTypes(system))
            {
                return;
            }

            Console.WriteLine($"System: {system.Name}");
            Console.WriteLine($"  Surfaces: {system.OpticalSurfaceCount}");
            Console.WriteLine($"  Elements: {system.GetLensElements().Count}");

            // Ensure output directory exists
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            // Auto-select element if not specified
            // Note: user input is 1-based, convert to 0-based index
            int elementIndex;
            if (element.HasValue)
            {
                elementIndex = element.Value - 1;  // Convert 1-based to 0-based
                Console.WriteLine($"\nUsing user-specified element: {element.Value}");
            }
            else
            {
                var splitter = new OptimizingSplitter();
                var analysis = splitter.AnalyzeElements(system);

                Console.WriteLine("\nElement Analysis:");
                foreach (var elem in analysis.SortedByPriority)
                {
                    Console.WriteLine($"  {elem}");
                }

                if (!analysis.HasSplittableElements)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nERROR: No splittable elements found in this system.");
                    Console.WriteLine("All elements are either:");
                    Console.WriteLine("  - Negative power (splitting not beneficial)");
                    Console.WriteLine("  - Part of cemented/air-spaced groups");
                    Console.WriteLine("  - Have aspheric (conic) surfaces");
                    Console.ResetColor();
                    Console.WriteLine("\nUse --element to manually specify an element if you want to override.");
                    return;
                }

                elementIndex = analysis.RecommendedElementIndex;
                Console.WriteLine($"\nAuto-selected element {elementIndex + 1} (highest splitting priority)");  // Show 1-based to user
            }

            // Check if the selected element has negative power
            var elements = system.GetLensElements();
            if (elementIndex < 0 || elementIndex >= elements.Count)
            {
                throw new ArgumentException($"Element index {elementIndex + 1} out of range. System has {elements.Count} elements.");
            }

            var selectedElement = elements[elementIndex];
            double elementPower = selectedElement.GetThickLensPower(system.PrimaryWavelength.ValueMicrons);

            if (elementPower < 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nWARNING: Element {elementIndex + 1} is a NEGATIVE lens (power = {elementPower:F6})");
                Console.WriteLine("Power-preserving splitting is designed for positive lenses.");
                Console.WriteLine("Negative lenses contribute overcorrected spherical aberration, which");
                Console.WriteLine("typically corrects the undercorrected SA from positive lenses.");
                Console.WriteLine("Splitting a negative lens may:");
                Console.WriteLine("  - Reduce its beneficial correction");
                Console.WriteLine("  - Make overall system aberration worse");
                Console.WriteLine("  - Produce physically unrealizable lens shapes");
                Console.ResetColor();

                Console.Write("\nDo you want to continue anyway? (y/N): ");
                var response = Console.ReadLine()?.Trim().ToUpperInvariant();
                if (response != "Y" && response != "YES")
                {
                    Console.WriteLine("Split cancelled.");
                    return;
                }
                Console.WriteLine();
            }

            if (optimize)
            {
                await HandleOptimizedSplit(system, outputDir, elementIndex, minRatio, maxRatio,
                    edgeClearance, enforceGeometry);
            }
            else
            {
                await HandleBasicSplit(system, outputDir, elementIndex, gap);
            }

            Console.WriteLine("\nSplit completed successfully!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static async Task HandleOptimizedSplit(Core.Models.OpticalSystem system, DirectoryInfo outputDir,
        int element, double minRatio, double maxRatio,
        double edgeClearance = 0.5, bool enforceGeometry = true)
    {
        var splitter = new OptimizingSplitter();
        var settings = new OptimizingSplitter.OptimizationSettings
        {
            MinPowerRatio = minRatio,
            MaxPowerRatio = maxRatio,
            OptimizeAirGap = true,
            MinEdgeClearance = edgeClearance,
            EnforceGeometryConstraints = enforceGeometry,
            AberrationWeights = _currentWeights
        };

        Console.WriteLine($"\nPerforming iterative optimization...");
        Console.WriteLine($"  Power ratio range: {minRatio:F2} - {maxRatio:F2}");
        Console.WriteLine($"  Aberration weights: W1={settings.AberrationWeights.W1}, W2={settings.AberrationWeights.W2}, " +
                          $"W3={settings.AberrationWeights.W3}, W4={settings.AberrationWeights.W4}, W5={settings.AberrationWeights.W5}");
        if (settings.AberrationWeights.IncludeBuchdahl)
        {
            Console.WriteLine($"  Buchdahl weights: WBSph={settings.AberrationWeights.WBSph}, WBCma={settings.AberrationWeights.WBCma}, " +
                              $"WBObl={settings.AberrationWeights.WBObl}, WBEll={settings.AberrationWeights.WBEll}, " +
                              $"WBAst={settings.AberrationWeights.WBAst}, WBDst={settings.AberrationWeights.WBDst}");
        }
        if (enforceGeometry)
        {
            Console.WriteLine($"  Enforce geometry: enabled (total track may change)");
        }

        var result = splitter.SplitWithOptimization(system, element, settings);

        Console.WriteLine($"\nOptimized split of element {element + 1}:");
        Console.WriteLine($"  Original power: {result.OriginalPower:F6}");
        Console.WriteLine($"  Optimal power ratio: {result.OptimalPowerRatio:F4}");
        Console.WriteLine($"  Min air gap (geometry): {result.GeometryMinAirGap:F3} mm");
        Console.WriteLine($"  Requested air gap: {result.OptimalAirGap:F3} mm");
        Console.WriteLine($"  Actual air gap: {result.ActualAirGap:F3} mm");
        if (result.HasGeometryWarning)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  WARNING: {result.GeometryWarning}");
            Console.ResetColor();
        }
        Console.WriteLine($"  First element power: {result.OptimizedResult.Phi1:F6}");
        Console.WriteLine($"  Second element power: {result.OptimizedResult.Phi2:F6}");
        Console.WriteLine($"  Original EFL: {result.OriginalEfl:F2} mm");
        Console.WriteLine($"  Split EFL: {result.SplitEfl:F2} mm");
        // Calculate total system aberration before and after
        var originalAnalysis = splitter.AnalyzeElements(result.OriginalSystem);
        var splitAnalysis = splitter.AnalyzeElements(result.SplitSystem);

        Console.WriteLine($"\nAberration analysis (element being split):");
        Console.WriteLine($"  Single lens S1: {result.SingleLensS1:E4}");
        Console.WriteLine($"  Split elements S1: {result.OptimizedResult.S1_Total:E4}");
        Console.WriteLine($"  Element S1 reduction: {result.S1ReductionFactor:F2}x");

        // Display full aberration comparison (Seidel + chromatic in one table)
        if (result.OriginalSeidel != null && result.SplitSeidel != null)
        {
            var orig = result.OriginalSeidel;
            var split = result.SplitSeidel;

            // Calculate chromatic aberrations if multiple wavelengths
            ChromaticAberrationResult? origChroma = null;
            ChromaticAberrationResult? splitChroma = null;
            double maxField = 0;
            string fieldUnit = "°";
            if (result.OriginalSystem.Wavelengths.Count > 1)
            {
                try
                {
                    var chromaCalc = new ChromaticAberrationCalculator();
                    if (result.OriginalSystem.Fields.Count > 0)
                    {
                        var maxFieldObj = result.OriginalSystem.Fields.OrderByDescending(f => Math.Abs(f.Y)).First();
                        maxField = Math.Abs(maxFieldObj.Y);
                        fieldUnit = maxFieldObj.FieldType == Core.Models.FieldType.Angle ? "°" : "mm";
                        origChroma = chromaCalc.Calculate(result.OriginalSystem, maxField, maxFieldObj.FieldType);
                        splitChroma = chromaCalc.Calculate(result.SplitSystem, maxField, maxFieldObj.FieldType);
                    }
                    else
                    {
                        maxField = 10.0;
                        origChroma = chromaCalc.Calculate(result.OriginalSystem, maxField);
                        splitChroma = chromaCalc.Calculate(result.SplitSystem, maxField);
                    }
                }
                catch { }
            }

            Console.WriteLine($"\nAberrations (ray-traced):");
            Console.WriteLine("  Aberration            Original        Split           Change");
            Console.WriteLine("  ─────────────────────────────────────────────────────────────");
            PrintAberrationRow("S1 (Spherical)", orig.S1, split.S1);
            PrintAberrationRow("S2 (Coma)", orig.S2, split.S2);
            PrintAberrationRow("S3 (Astigmatism)", orig.S3, split.S3);
            PrintAberrationRow("S4 (Petzval)", orig.S4, split.S4);
            PrintAberrationRow("S5 (Distortion)", orig.S5, split.S5);
            if (origChroma != null && splitChroma != null)
            {
                PrintAberrationRowMm("CL (Axial Color)", origChroma.LongitudinalColor, splitChroma.LongitudinalColor);
                PrintAberrationRowMm($"CT (Lat {maxField:F0}{fieldUnit})", origChroma.LateralColor, splitChroma.LateralColor);
            }
            Console.WriteLine("  ─────────────────────────────────────────────────────────────");

            // Buchdahl 5th-order comparison
            try
            {
                var buchdahlCalc = new BuchdahlCalculator();
                var origB = buchdahlCalc.Calculate(result.OriginalSystem, result.OriginalSystem.PrimaryWavelength.ValueMicrons);
                var splitB = buchdahlCalc.Calculate(result.SplitSystem, result.SplitSystem.PrimaryWavelength.ValueMicrons);
                double origBEfl = Math.Abs(origB.Efl) > 1e-6 ? origB.Efl : 1.0;
                double splitBEfl = Math.Abs(splitB.Efl) > 1e-6 ? splitB.Efl : 1.0;

                Console.WriteLine($"\n  Buchdahl 5th-order (/EFL):");
                Console.WriteLine("  Aberration            Original        Split           Change");
                Console.WriteLine("  ─────────────────────────────────────────────────────────────");
                PrintAberrationRow("Ap (Spherical)", origB.ApEffective / origBEfl, splitB.ApEffective / splitBEfl);
                PrintAberrationRow("Aq (Coma)", origB.AqEffective / origBEfl, splitB.AqEffective / splitBEfl);
                PrintAberrationRow("Bp (Oblique Sph)", origB.BpEffective / origBEfl, splitB.BpEffective / splitBEfl);
                PrintAberrationRow("Bq (Ellip. Coma)", origB.BqEffective / origBEfl, splitB.BqEffective / splitBEfl);
                PrintAberrationRow("Cp (Astigmatism)", origB.CpEffective / origBEfl, splitB.CpEffective / splitBEfl);
                PrintAberrationRow("Cq (Distortion)", origB.CqEffective / origBEfl, splitB.CqEffective / splitBEfl);
                PrintAberrationRow("Total magnitude", origB.TotalMagnitude / origBEfl, splitB.TotalMagnitude / splitBEfl);
                Console.WriteLine("  ─────────────────────────────────────────────────────────────");
            }
            catch { }

            Console.WriteLine($"\n  Merit Function (weighted sum):");
            Console.WriteLine($"    Original: {result.OriginalMeritFunction:E4}");
            Console.WriteLine($"    Split:    {result.SplitMeritFunction:E4}");
            Console.WriteLine($"    Improvement: {result.MeritFunctionImprovement:F2}x");

            // Show warning if split made things worse
            if (!result.SplitImprovedMeritFunction)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  ⚠ WARNING: Splitting did not improve overall aberrations.");
                Console.WriteLine($"    The optimizer tried {result.IterationHistory.Count} configurations but");
                Console.WriteLine($"    could not find one better than the original system.");
                Console.WriteLine($"    Consider: using different glass, or not splitting this element.");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine($"\nTotal system aberration (thin-lens S1 only):");
            Console.WriteLine($"  Original system S1: {originalAnalysis.TotalS1:E4}");
            Console.WriteLine($"  Split system S1: {splitAnalysis.TotalS1:E4}");
            double systemReduction = Math.Abs(originalAnalysis.TotalS1) > 1e-20
                ? Math.Abs(originalAnalysis.TotalS1 / splitAnalysis.TotalS1)
                : 1.0;
            string systemChange = splitAnalysis.TotalS1 < originalAnalysis.TotalS1 ? "improved" : "changed";
            Console.WriteLine($"  System S1 change: {systemReduction:F2}x ({systemChange})");
        }
        Console.WriteLine($"\n  Iterations evaluated: {result.IterationHistory.Count}");

        // Export ZMX
        var zmxExporter = new ZmxExporter();
        var zmxPath = Path.Combine(outputDir.FullName, "split.zmx");
        zmxExporter.Export(result.SplitSystem, zmxPath);
        Console.WriteLine($"ZMX saved to: {zmxPath}");

        // Export Optiland JSON
        var jsonExporter = new OptilandJsonExporter();
        var jsonPath = Path.Combine(outputDir.FullName, "split.json");
        jsonExporter.Export(result.SplitSystem, jsonPath);
        Console.WriteLine($"JSON saved to: {jsonPath}");
    }

    static async Task HandleBasicSplit(Core.Models.OpticalSystem system, DirectoryInfo outputDir,
        int element, double gap)
    {
        var splitter = new ElementSplitter();
        var result = splitter.Split(system, element, gap);

        Console.WriteLine($"\nSplit element {element}:");
        Console.WriteLine($"  Original power: {result.OriginalPower:F6}");
        Console.WriteLine($"  First element power: {result.FirstElementPower:F6}");
        Console.WriteLine($"  Second element power: {result.SecondElementPower:F6}");
        Console.WriteLine($"  Total split power: {result.TotalSplitPower:F6}");
        Console.WriteLine($"  Power error: {result.RelativePowerError:F3}%");
        Console.WriteLine($"  Original EFL: {result.OriginalEfl:F2} mm");
        Console.WriteLine($"  Split EFL: {result.SplitEfl:F2} mm");

        // Export ZMX
        var zmxExporter = new ZmxExporter();
        var zmxPath = Path.Combine(outputDir.FullName, "split.zmx");
        zmxExporter.Export(result.SplitSystem, zmxPath);
        Console.WriteLine($"ZMX saved to: {zmxPath}");

        // Export Optiland JSON
        var jsonExporter = new OptilandJsonExporter();
        var jsonPath = Path.Combine(outputDir.FullName, "split.json");
        jsonExporter.Export(result.SplitSystem, jsonPath);
        Console.WriteLine($"JSON saved to: {jsonPath}");
    }

    static async Task HandleAnalyze(FileInfo input, FileInfo? catalog)
    {
        try
        {
            // Setup glass catalog
            var glassCatalog = new GlassCatalogManager();
            glassCatalog.LoadDefaultCatalogs();
            if (catalog != null && catalog.Exists)
            {
                glassCatalog.LoadCatalog(catalog.FullName);
            }

            // Load optical system
            var loader = new OpticalSystemLoader(glassCatalog);
            var system = loader.Load(input.FullName);

            // Validate field types
            if (!ValidateFieldTypes(system))
            {
                return;
            }

            Console.WriteLine($"System: {system.Name}");
            Console.WriteLine($"Surfaces: {system.Surfaces.Count} ({system.OpticalSurfaceCount} optical)");
            Console.WriteLine($"Wavelengths: {system.Wavelengths.Count}");
            Console.WriteLine($"Fields: {system.Fields.Count}");
            Console.WriteLine($"Aperture: {system.ApertureType} = {system.ApertureValue}");
            Console.WriteLine($"Stop surface: {system.StopIndex}");

            // Calculate EFL
            var tracer = new ParaxialRayTracer();
            var wavelength = system.PrimaryWavelength.ValueMicrons;
            var efl = tracer.CalculateEfl(system, wavelength);
            var bfl = tracer.CalculateBfl(system, wavelength);

            Console.WriteLine($"\nParaxial properties at {wavelength * 1000:F1} nm:");
            Console.WriteLine($"  EFL: {efl:F4} mm");
            Console.WriteLine($"  BFL: {bfl:F4} mm");

            // List elements (display 1-based for users)
            var elements = system.GetLensElements();
            Console.WriteLine($"\nLens Elements ({elements.Count}):");
            foreach (var elem in elements)
            {
                var power = elem.GetThickLensPower(wavelength);
                var fl = Math.Abs(power) > 1e-10 ? 1.0 / power : double.PositiveInfinity;
                Console.WriteLine($"  {elem.Index + 1}: R1={elem.R1:F3}, R2={elem.R2:F3}, d={elem.CenterThickness:F3}, Glass={elem.Glass.Name}, P={power:F6}, f={fl:F2}");
            }

            // Aberration analysis
            if (elements.Count > 0)
            {
                var splitter = new OptimizingSplitter();
                var analysis = splitter.AnalyzeElements(system);

                Console.WriteLine($"\nAberration Analysis:");
                Console.WriteLine($"  Total system S1: {analysis.TotalS1:E4} ({(analysis.TotalS1 < 0 ? "undercorrected" : "overcorrected")})");
                Console.WriteLine($"\nElement S1 Contributions (by splitting priority):");
                foreach (var elem in analysis.SortedByPriority)
                {
                    // Use ToString() which includes 1-based numbering and status tags
                    Console.WriteLine($"  {elem}");
                }

                if (analysis.HasSplittableElements)
                {
                    Console.WriteLine($"\nRecommended element to split: {analysis.RecommendedElementIndex + 1}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\nNo splittable elements found. All elements are negative, in groups, or have aspheric surfaces.");
                    Console.ResetColor();
                }
            }

            // Seidel per-surface analysis
            try
            {
                var seidelCalc = new SeidelCalculator();
                var seidel = seidelCalc.Calculate(system, wavelength);

                Console.WriteLine($"\nSeidel 3rd-Order Aberrations:");
                Console.WriteLine($"  {"Surf",4}  {"S1",12}  {"S2",12}  {"S3",12}  {"S4",12}  {"S5",12}  {"CL",12}  {"CT",12}");
                Console.WriteLine($"  {"────",4}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}");
                foreach (var sc in seidel.SurfaceCoefficients)
                {
                    Console.WriteLine($"  {sc.SurfaceIndex,4}  {sc.S1,12:E3}  {sc.S2,12:E3}  {sc.S3,12:E3}  {sc.S4,12:E3}  {sc.S5,12:E3}  {sc.CL,12:E3}  {sc.CT,12:E3}");
                }
                Console.WriteLine($"  {"Sum",4}  {seidel.S1,12:E3}  {seidel.S2,12:E3}  {seidel.S3,12:E3}  {seidel.S4,12:E3}  {seidel.S5,12:E3}  {seidel.CL,12:E3}  {seidel.CT,12:E3}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nSeidel 3rd-order: calculation failed ({ex.Message})");
            }

            // Buchdahl 5th-order analysis
            try
            {
                var buchdahlCalc = new BuchdahlCalculator();
                var buchdahl = buchdahlCalc.Calculate(system, wavelength);

                double bEfl = Math.Abs(buchdahl.Efl) > 1e-6 ? buchdahl.Efl : 1.0;
                Console.WriteLine($"\nBuchdahl 5th-Order Aberrations (EFL={bEfl:F2} mm):");
                Console.WriteLine($"  Effective coefficients (P={buchdahl.P:F4}):");
                Console.WriteLine($"  {"Coeff",-28} {"Raw",12} {"/ EFL",12}");
                Console.WriteLine($"  {"─────────────────────────────",28} {"────────────",12} {"────────────",12}");
                Console.WriteLine($"  {"Ap_eff (Spherical)",-28} {buchdahl.ApEffective,12:E3} {buchdahl.ApEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Aq_eff (Coma)",-28} {buchdahl.AqEffective,12:E3} {buchdahl.AqEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Bp_eff (Oblique Sph)",-28} {buchdahl.BpEffective,12:E3} {buchdahl.BpEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Bq_eff (Elliptical Coma)",-28} {buchdahl.BqEffective,12:E3} {buchdahl.BqEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Cp_eff (Astigmatism)",-28} {buchdahl.CpEffective,12:E3} {buchdahl.CpEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Cq_eff (Distortion)",-28} {buchdahl.CqEffective,12:E3} {buchdahl.CqEffective / bEfl,12:E3}");
                Console.WriteLine($"  {"Total magnitude",-28} {buchdahl.TotalMagnitude,12:E3} {buchdahl.TotalMagnitude / bEfl,12:E3}");

                // Total 5th-order = Primary + Secondary
                double P = buchdahl.P;
                double apTotal = buchdahl.Ap + buchdahl.S1p + P * (buchdahl.ApBar + buchdahl.S1pBar);
                double bpTotal = buchdahl.Bp + buchdahl.S2p + P * (buchdahl.BpBar + buchdahl.S2pBar);
                double cpTotal = buchdahl.Cp + buchdahl.S3p + P * (buchdahl.CpBar + buchdahl.S3pBar);
                double aqTotal = buchdahl.Aq + buchdahl.S4p + P * (buchdahl.AqBar + buchdahl.S4pBar);
                double bqTotal = buchdahl.Bq + buchdahl.S5p + P * (buchdahl.BqBar + buchdahl.S5pBar);
                double cqTotal = buchdahl.Cq + buchdahl.S6p + P * (buchdahl.CqBar + buchdahl.S6pBar);
                double totalMag = Math.Sqrt(apTotal * apTotal + bpTotal * bpTotal + cpTotal * cpTotal +
                                            aqTotal * aqTotal + bqTotal * bqTotal + cqTotal * cqTotal);
                Console.WriteLine($"\n  Total (Primary + Secondary):");
                Console.WriteLine($"  {"Ap_total (Spherical)",-28} {apTotal,12:E3} {apTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Aq_total (Coma)",-28} {aqTotal,12:E3} {aqTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Bp_total (Oblique Sph)",-28} {bpTotal,12:E3} {bpTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Bq_total (Elliptical Coma)",-28} {bqTotal,12:E3} {bqTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Cp_total (Astigmatism)",-28} {cpTotal,12:E3} {cpTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Cq_total (Distortion)",-28} {cqTotal,12:E3} {cqTotal / bEfl,12:E3}");
                Console.WriteLine($"  {"Total magnitude",-28} {totalMag,12:E3} {totalMag / bEfl,12:E3}");

                if (buchdahl.SurfaceContributions.Count > 0)
                {
                    Console.WriteLine($"\n  Per-surface primary contributions:");
                    Console.WriteLine($"  {"Surf",4}  {"Ap",12}  {"Bp",12}  {"Cp",12}  {"Aq",12}  {"Bq",12}  {"Cq",12}");
                    Console.WriteLine($"  {"────",4}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}");
                    foreach (var sc in buchdahl.SurfaceContributions)
                    {
                        Console.WriteLine($"  {sc.SurfaceIndex,4}  {sc.Ap,12:E3}  {sc.Bp,12:E3}  {sc.Cp,12:E3}  {sc.Aq,12:E3}  {sc.Bq,12:E3}  {sc.Cq,12:E3}");
                    }
                    Console.WriteLine($"  {"Sum",4}  {buchdahl.Ap,12:E3}  {buchdahl.Bp,12:E3}  {buchdahl.Cp,12:E3}  {buchdahl.Aq,12:E3}  {buchdahl.Bq,12:E3}  {buchdahl.Cq,12:E3}");

                    Console.WriteLine($"\n  Per-surface secondary intrinsic (effective):");
                    Console.WriteLine($"  {"Surf",4}  {"Ap",12}  {"Bp",12}  {"Cp",12}  {"Aq",12}  {"Bq",12}  {"Cq",12}");
                    Console.WriteLine($"  {"────",4}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}");
                    double sumIA = 0, sumIB = 0, sumIC = 0, sumIAq = 0, sumIBq = 0, sumICq = 0;
                    foreach (var sc in buchdahl.SurfaceContributions)
                    {
                        Console.WriteLine($"  {sc.SurfaceIndex,4}  {sc.IntrinsicAp,12:E3}  {sc.IntrinsicBp,12:E3}  {sc.IntrinsicCp,12:E3}  {sc.IntrinsicAq,12:E3}  {sc.IntrinsicBq,12:E3}  {sc.IntrinsicCq,12:E3}");
                        sumIA += sc.IntrinsicAp; sumIB += sc.IntrinsicBp; sumIC += sc.IntrinsicCp;
                        sumIAq += sc.IntrinsicAq; sumIBq += sc.IntrinsicBq; sumICq += sc.IntrinsicCq;
                    }
                    Console.WriteLine($"  {"Sum",4}  {sumIA,12:E3}  {sumIB,12:E3}  {sumIC,12:E3}  {sumIAq,12:E3}  {sumIBq,12:E3}  {sumICq,12:E3}");

                    Console.WriteLine($"\n  Per-surface secondary induced (effective):");
                    Console.WriteLine($"  {"Surf",4}  {"Ap",12}  {"Bp",12}  {"Cp",12}  {"Aq",12}  {"Bq",12}  {"Cq",12}");
                    Console.WriteLine($"  {"────",4}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}  {"────────────",12}");
                    double sumDA = 0, sumDB = 0, sumDC = 0, sumDAq = 0, sumDBq = 0, sumDCq = 0;
                    foreach (var sc in buchdahl.SurfaceContributions)
                    {
                        Console.WriteLine($"  {sc.SurfaceIndex,4}  {sc.InducedAp,12:E3}  {sc.InducedBp,12:E3}  {sc.InducedCp,12:E3}  {sc.InducedAq,12:E3}  {sc.InducedBq,12:E3}  {sc.InducedCq,12:E3}");
                        sumDA += sc.InducedAp; sumDB += sc.InducedBp; sumDC += sc.InducedCp;
                        sumDAq += sc.InducedAq; sumDBq += sc.InducedBq; sumDCq += sc.InducedCq;
                    }
                    Console.WriteLine($"  {"Sum",4}  {sumDA,12:E3}  {sumDB,12:E3}  {sumDC,12:E3}  {sumDAq,12:E3}  {sumDBq,12:E3}  {sumDCq,12:E3}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nBuchdahl 5th-order: calculation failed ({ex.Message})");
            }

            // List surfaces
            Console.WriteLine($"\nSurfaces:");
            foreach (var surf in system.Surfaces)
            {
                var radiusStr = surf.IsFlat ? "Infinity" : $"{surf.Radius:F4}";
                var glassStr = surf.GlassName ?? "AIR";
                var stopStr = surf.IsStop ? " [STOP]" : "";
                Console.WriteLine($"  {surf.Index}: R={radiusStr}, T={surf.Thickness:F4}, Glass={glassStr}{stopStr}");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void PrintAberrationRow(string name, double original, double split)
    {
        double ratio = Math.Abs(original) > 1e-20 ? Math.Abs(original / split) : 1.0;
        string change = Math.Abs(split) < Math.Abs(original) ? "better" : "worse";
        if (Math.Abs(Math.Abs(split) - Math.Abs(original)) < 1e-10 * Math.Max(Math.Abs(original), Math.Abs(split)))
        {
            change = "same";
            ratio = 1.0;
        }
        Console.WriteLine($"  {name,-18} {original,13:E3}  {split,13:E3}  {ratio,5:F2}x ({change})");
    }

    static void PrintAberrationRowMm(string name, double original, double split)
    {
        double ratio = Math.Abs(original) > 1e-10 ? Math.Abs(original / split) : 1.0;
        string change = Math.Abs(split) < Math.Abs(original) ? "better" : "worse";
        if (Math.Abs(Math.Abs(split) - Math.Abs(original)) < 1e-6)
        {
            change = "same";
            ratio = 1.0;
        }
        Console.WriteLine($"  {name,-18} {original,10:F4}mm  {split,10:F4}mm  {ratio,5:F2}x ({change})");
    }

    static async Task HandleOptimizeGlass(FileInfo input, DirectoryInfo outputDir, string glassNames,
        int? element, int trials, int top, FileInfo? catalog, double eflTolerancePercent = 3.0, bool noChromatic = false)
    {
        try
        {
            Console.WriteLine($"Loading system from: {input.FullName}");

            // Setup glass catalog
            var glassCatalog = new GlassCatalogManager();
            glassCatalog.LoadDefaultCatalogs();
            if (catalog != null && catalog.Exists)
            {
                var count = glassCatalog.LoadCatalog(catalog.FullName);
                Console.WriteLine($"Loaded {count} glasses from {catalog.Name}");
            }

            // Load optical system
            var loader = new OpticalSystemLoader(glassCatalog);
            var system = loader.Load(input.FullName);

            // Validate field types
            if (!ValidateFieldTypes(system))
            {
                return;
            }

            Console.WriteLine($"System: {system.Name}");
            Console.WriteLine($"  Elements: {system.GetLensElements().Count}");

            // Parse glass list
            var glassNameList = glassNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var availableGlasses = new List<Core.Models.Glass>();

            Console.WriteLine($"\nResolving glasses ({glassNameList.Length} requested):");
            foreach (var name in glassNameList)
            {
                var glass = glassCatalog.GetGlass(name);
                if (glass != null)
                {
                    availableGlasses.Add(glass);
                }
                else
                {
                    Console.WriteLine($"  {name}: NOT FOUND (skipping)");
                }
            }
            Console.WriteLine($"  Found {availableGlasses.Count} valid glasses");

            if (availableGlasses.Count < 2)
            {
                Console.Error.WriteLine("Error: Need at least 2 valid glasses for optimization.");
                Environment.Exit(1);
                return;
            }

            // Determine element to split
            // Note: user input is 1-based, convert to 0-based index
            int elementIndex;
            if (element.HasValue)
            {
                elementIndex = element.Value - 1;  // Convert 1-based to 0-based
            }
            else
            {
                var splitter = new OptimizingSplitter();
                var analysis = splitter.AnalyzeElements(system);

                Console.WriteLine("\nElement Analysis:");
                foreach (var elem in analysis.SortedByPriority)
                {
                    Console.WriteLine($"  {elem}");
                }

                if (!analysis.HasSplittableElements)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nERROR: No splittable elements found in this system.");
                    Console.WriteLine("All elements are either:");
                    Console.WriteLine("  - Negative power (splitting not beneficial)");
                    Console.WriteLine("  - Part of cemented/air-spaced groups");
                    Console.WriteLine("  - Have aspheric (conic) surfaces");
                    Console.ResetColor();
                    Console.WriteLine("\nUse --element to manually specify an element if you want to override.");
                    return;
                }

                elementIndex = analysis.RecommendedElementIndex;
                Console.WriteLine($"\nAuto-selected element {elementIndex + 1} for splitting");  // Show 1-based to user
            }

            // Check if manually selected element has negative power
            var elements = system.GetLensElements();
            if (elementIndex < 0 || elementIndex >= elements.Count)
            {
                throw new ArgumentException($"Element index {elementIndex + 1} out of range. System has {elements.Count} elements.");
            }

            var selectedElement = elements[elementIndex];
            double elementPower = selectedElement.GetThickLensPower(system.PrimaryWavelength.ValueMicrons);

            if (elementPower < 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nWARNING: Element {elementIndex + 1} is a NEGATIVE lens (power = {elementPower:F6})");
                Console.WriteLine("Power-preserving splitting is designed for positive lenses.");
                Console.ResetColor();

                Console.Write("\nDo you want to continue anyway? (y/N): ");
                var response = Console.ReadLine()?.Trim().ToUpperInvariant();
                if (response != "Y" && response != "YES")
                {
                    Console.WriteLine("Optimization cancelled.");
                    return;
                }
                Console.WriteLine();
            }

            // Ensure output directory exists
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            // Determine if chromatic aberrations should be included
            bool includeChromatic = !noChromatic;
            if (includeChromatic && system.Wavelengths.Count <= 1)
            {
                includeChromatic = false;
                Console.WriteLine($"\nNote: Only {system.Wavelengths.Count} wavelength defined - chromatic aberrations auto-disabled.");
            }

            // Use current weights, adjusting chromatic setting
            var weights = _currentWeights;
            if (!includeChromatic)
            {
                weights = weights.WithoutChromatic();
            }

            // Run optimization
            Console.WriteLine($"\nRunning glass optimization...");
            Console.WriteLine($"  Trials: {trials}");
            Console.WriteLine($"  Top results to keep: {top}");
            Console.WriteLine($"  Aberration weights: W1={weights.W1}, W2={weights.W2}, W3={weights.W3}, W4={weights.W4}, W5={weights.W5}");
            if (includeChromatic)
            {
                Console.WriteLine($"  Chromatic weights: WCL={weights.WCL}, WCT={weights.WCT} (included in merit function)");
            }
            else
            {
                Console.WriteLine($"  Chromatic aberrations: disabled");
            }
            if (weights.IncludeBuchdahl)
            {
                Console.WriteLine($"  Buchdahl weights: WBSph={weights.WBSph}, WBCma={weights.WBCma}, " +
                                  $"WBObl={weights.WBObl}, WBEll={weights.WBEll}, " +
                                  $"WBAst={weights.WBAst}, WBDst={weights.WBDst}");
            }
            if (eflTolerancePercent > 0)
                Console.WriteLine($"  EFL tolerance: {eflTolerancePercent:F1}%");
            else
                Console.WriteLine($"  EFL tolerance: disabled (EFL may vary)");

            // Get max field angle from system (use actual field, not hardcoded)
            double maxFieldAngle = system.Fields.Count > 0
                ? system.Fields.Max(f => Math.Max(Math.Abs(f.X), Math.Abs(f.Y)))
                : 5.0;  // Default to 5° if no fields defined

            var optimizer = new GlassOptimizer();
            var settings = new GlassOptimizer.OptimizationSettings
            {
                NumberOfTrials = trials,
                TopResultsToKeep = top,
                AberrationWeights = weights,
                FieldAngleForLateralColor = maxFieldAngle,
                MaxEflDeviation = eflTolerancePercent / 100.0,  // Convert percentage to fraction
                CorrectEfl = false  // Don't correct EFL (preserves aberration performance), just filter
            };

            var result = optimizer.Optimize(system, elementIndex, availableGlasses, settings);

            Console.WriteLine($"\nOptimization complete!");
            Console.WriteLine($"  Glass combinations evaluated: {result.TotalCombinationsTried}");
            Console.WriteLine($"  Original merit function: {result.OriginalMeritFunction:E4}");
            if (result.OriginalSeidel != null)
            {
                Console.WriteLine($"    S1={result.OriginalSeidel.S1:E3}, S2={result.OriginalSeidel.S2:E3}, " +
                                  $"S3={result.OriginalSeidel.S3:E3}, S4={result.OriginalSeidel.S4:E3}, S5={result.OriginalSeidel.S5:E3}");
            }
            Console.WriteLine($"  Original longitudinal color: {result.OriginalChromaticAberration.LongitudinalColor:F4} mm");
            Console.WriteLine($"  Original lateral color: {result.OriginalChromaticAberration.LateralColor:F4} mm (at {result.Settings.FieldAngleForLateralColor}° field)");

            // Check if any improvements were found
            if (!result.HasImprovements || result.TopResults.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⚠ WARNING: No glass combinations improved the merit function.");
                Console.WriteLine($"  All {result.TotalCombinationsTried} combinations made aberrations worse.");
                Console.WriteLine($"  Consider: this element may not be suitable for splitting,");
                Console.WriteLine($"  or try a different set of glasses.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Found {result.ImprovementCount} glass combinations that improve the merit function.");
                Console.ResetColor();
            }

            // Display results table
            Console.WriteLine($"\nResults (Original EFL: {result.OriginalEfl:F2} mm):");
            Console.WriteLine("Rank  Glass1          Glass2          MF Improv  LongColor     LatColor      EFL (mm)");
            Console.WriteLine("────  ──────────────  ──────────────  ─────────  ────────────  ────────────  ────────");

            // Row 1: Original (unsplit) - baseline
            var origGlassName = result.OriginalElement?.Glass?.Name ?? "UNKNOWN";
            Console.WriteLine($"{"Orig",4}  {origGlassName,-14}  {"(unsplit)",-14}  {"1.00x",9}  {result.OriginalChromaticAberration.LongitudinalColor,11:F4}mm  {result.OriginalChromaticAberration.LateralColor,11:F4}mm  {result.OriginalEfl,8:F2}");

            // Row 2: Split with original glass (shows improvement from splitting alone)
            if (result.OriginalGlassSplitResult != null)
            {
                var og = result.OriginalGlassSplitResult;
                string splitImprovStr = og.MeritScore >= 1.0 ? $"{og.MeritScore,8:F2}x" : $"{og.MeritScore,7:F2}x*";
                Console.WriteLine($"{"Splt",4}  {og.Glass1Name,-14}  {og.Glass2Name,-14}  {splitImprovStr}  {og.LongitudinalColor,11:F4}mm  {og.LateralColor,11:F4}mm  {og.Efl,8:F2}");
            }

            Console.WriteLine("────  ──────────────  ──────────────  ─────────  ────────────  ────────────  ────────");

            // Top 10 ranked glass combinations
            if (result.TopResults.Count > 0)
            {
                foreach (var r in result.TopResults.Take(10))
                {
                    string improvStr = r.MeritScore >= 1.0 ? $"{r.MeritScore,8:F2}x" : $"{r.MeritScore,7:F2}x*";
                    Console.WriteLine($"{r.Rank,4}  {r.Glass1Name,-14}  {r.Glass2Name,-14}  {improvStr}  {r.LongitudinalColor,11:F4}mm  {r.LateralColor,11:F4}mm  {r.Efl,8:F2}");
                }
                if (result.TopResults.Any(r => r.MeritScore < 1.0))
                {
                    Console.WriteLine("  (* = worse than original)");
                }
            }

            // Export results
            var exporter = new GlassOptimizationExporter();

            var csvPath = Path.Combine(outputDir.FullName, "glass_optimization.csv");
            exporter.ExportToCsv(result, csvPath);
            Console.WriteLine($"\nCSV saved to: {csvPath}");

            var jsonPath = Path.Combine(outputDir.FullName, "glass_optimization.json");
            exporter.ExportToJson(result, jsonPath);
            Console.WriteLine($"JSON saved to: {jsonPath}");

            var textPath = Path.Combine(outputDir.FullName, "glass_optimization_report.txt");
            exporter.ExportToText(result, textPath);
            Console.WriteLine($"Report saved to: {textPath}");

            // Export all top results that improved the system
            var improvedResults = result.TopResults.Where(r => r.MeritScore >= 1.0).ToList();

            var zmxExporter = new ZmxExporter();
            var jsonExporter = new OptilandJsonExporter();

            // Create subdirectory for all splits
            var splitsDir = Path.Combine(outputDir.FullName, "splits");
            Directory.CreateDirectory(splitsDir);

            // Export split with original glass first
            if (result.OriginalGlassSplitResult?.SplitSystem != null)
            {
                var og = result.OriginalGlassSplitResult;
                string baseName = $"00_{og.Glass1Name}_{og.Glass2Name}_SameGlass";

                var zmxPath = Path.Combine(splitsDir, $"{baseName}.zmx");
                zmxExporter.Export(og.SplitSystem, zmxPath);

                var optilandPath = Path.Combine(splitsDir, $"{baseName}.json");
                jsonExporter.Export(og.SplitSystem, optilandPath);

                Console.WriteLine($"\nExported split with original glass:");
                Console.WriteLine($"  Splt: {og.Glass1Name} + {og.Glass2Name} ({og.MeritScore:F2}x) -> {baseName}.zmx");
            }

            if (improvedResults.Count > 0)
            {
                Console.WriteLine($"\nExporting top {Math.Min(10, improvedResults.Count)} improved configurations...");

                foreach (var r in improvedResults.Take(10))
                {
                    if (r.SplitSystem == null) continue;

                    // Create filename from rank and glass names
                    string baseName = $"{r.Rank:D2}_{r.Glass1Name}_{r.Glass2Name}";

                    var zmxPath = Path.Combine(splitsDir, $"{baseName}.zmx");
                    zmxExporter.Export(r.SplitSystem, zmxPath);

                    var optilandPath = Path.Combine(splitsDir, $"{baseName}.json");
                    jsonExporter.Export(r.SplitSystem, optilandPath);

                    Console.WriteLine($"  #{r.Rank}: {r.Glass1Name} + {r.Glass2Name} ({r.MeritScore:F2}x) -> {baseName}.zmx");
                }

                Console.WriteLine($"\nAll split systems saved to: {splitsDir}");

                // Also export the best one to the main directory for convenience
                var best = improvedResults.First();
                Console.WriteLine($"\nBest configuration: {best.Glass1Name} + {best.Glass2Name}");
                Console.WriteLine($"  Power ratio: {best.PowerRatio:F4}");
                Console.WriteLine($"  Air gap: {best.AirGap:F2} mm");
                Console.WriteLine($"  Merit function improvement: {best.MeritScore:F2}x");
                if (best.SeidelResult != null)
                {
                    Console.WriteLine($"  Split aberrations:");
                    Console.WriteLine($"    S1={best.SeidelResult.S1:E3}, S2={best.SeidelResult.S2:E3}, " +
                                      $"S3={best.SeidelResult.S3:E3}, S4={best.SeidelResult.S4:E3}, S5={best.SeidelResult.S5:E3}");
                }
                Console.WriteLine($"  Longitudinal color: {best.LongitudinalColor:F4} mm ({best.LongColorImprovement:F2}x improvement)");
                Console.WriteLine($"  Lateral color: {best.LateralColor:F4} mm ({best.LatColorImprovement:F2}x improvement)");

                var bestZmxPath = Path.Combine(outputDir.FullName, "best_split.zmx");
                zmxExporter.Export(best.SplitSystem, bestZmxPath);
                Console.WriteLine($"Best system also saved to: {bestZmxPath}");
            }
            else if (result.BestResult != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nBest result ({result.BestResult.Glass1Name} + {result.BestResult.Glass2Name}) " +
                                  $"still has {result.BestResult.MeritScore:F2}x merit (worse than original).");
                Console.WriteLine($"No improved split systems were exported.");
                Console.ResetColor();
            }

            Console.WriteLine("\nGlass optimization completed successfully!");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Validates that the optical system uses supported field types.
    /// Currently only Angle and ObjectHeight are supported.
    /// </summary>
    /// <returns>True if valid, false if unsupported field types are present.</returns>
    static bool ValidateFieldTypes(Core.Models.OpticalSystem system)
    {
        var unsupportedFields = system.Fields
            .Where(f => f.FieldType != Core.Models.FieldType.Angle &&
                        f.FieldType != Core.Models.FieldType.ObjectHeight)
            .ToList();

        if (unsupportedFields.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nERROR: Unsupported field type(s) detected.");
            Console.ResetColor();

            foreach (var field in unsupportedFields)
            {
                string typeName = field.FieldType switch
                {
                    Core.Models.FieldType.ParaxialImageHeight => "Paraxial Image Height",
                    Core.Models.FieldType.ImageHeight => "Real Image Height",
                    _ => field.FieldType.ToString()
                };
                Console.WriteLine($"  - Field ({field.X}, {field.Y}) uses '{typeName}'");
            }

            Console.WriteLine();
            Console.WriteLine("LensSplitter currently supports only these field types:");
            Console.WriteLine("  - Angle (field angles in degrees)");
            Console.WriteLine("  - Object Height (object heights in mm)");
            Console.WriteLine();
            Console.WriteLine("Paraxial Image Height and Real Image Height require inverse ray tracing");
            Console.WriteLine("which is not yet implemented. Please convert your system to use Angle");
            Console.WriteLine("or Object Height fields in your optical design software.");

            return false;
        }

        return true;
    }
}
