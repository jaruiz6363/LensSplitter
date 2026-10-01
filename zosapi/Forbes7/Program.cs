using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using AberrationCalculator.Core.Report;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.RayTrace;
using ZOSAPI;

namespace AberrationCalculator.Forbes7
{
    /// <summary>
    /// FORBES7 - the twenty seventh-order aberration coefficients of a centred system,
    /// reported per surface and split into the part each surface generates on its own and the
    /// part it generates by acting on the aberration already present when light reaches it.
    ///
    /// Written by Javier Ruiz with Claude Code, September 2026.
    ///
    /// <para>Forbes, "Order doubling in the computation of aberration coefficients",
    /// J. Opt. Soc. Am. 73, 782 (1983). The system is traced on power series in the three
    /// rotational invariants rather than on numbers, so one trace yields every order the
    /// truncation keeps, for every ray at once.</para>
    ///
    /// <para><b>What this does that BUCH7.ZPL cannot.</b> Buchdahl's computing scheme is
    /// spherical-surface only - he gives the aspheric scheme in Sec. 85 of the monograph but
    /// never published the arranged table for it, and that arrangement is the one part of this
    /// subject with no printed answer to check against. Forbes has no such split: a sphere, a
    /// conic and an even asphere differ only in the coefficients of one power series and run
    /// through identical code. It handles either conjugate, as BUCH7 now does - the object plane
    /// is simply the plane the ray is launched from. It does not handle a mirror, and refuses one.</para>
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// STAThread because the file browser below is a Windows dialog, and a console program
        /// has to say so before it may open one.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            // Asked with no arguments, this asks back. Most of the ZOS-API examples OpticStudio
            // ships take none, so that is what its users expect; the switches are still there
            // for anyone scripting it.
            bool interactive = args.Length == 0;
            try
            {
                int code = Run(args, interactive);
                if (interactive) Hold();
                return code;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("FORBES7 stopped: " + ex.Message);
                if (interactive) Hold();
                return 1;
            }
        }

        private static int Run(string[] args, bool interactive)
        {
            bool help = args.Length == 1 &&
                        (args[0] == "-h" || args[0] == "--help" || args[0] == "/?");
            if (help) { Usage(); return 0; }

            int degree = 3;
            string? file = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--degree" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out degree)
                        || degree < 3 || degree > 8)
                        throw new ArgumentException("--degree takes a number from 3 to 8. Three is the "
                            + "seventh order; more costs time and must not change the answer.");
                }
                else file = args[i];
            }

            if (interactive)
            {
                Console.WriteLine("FORBES7 - seventh-order aberration coefficients, intrinsic and induced");
                Console.WriteLine("Forbes, J. Opt. Soc. Am. 73, 782 (1983). Spheres, conics and even aspheres.");
                Console.WriteLine();
                Console.WriteLine("Give it a file name to skip these questions; --help lists the switches.");
                Console.WriteLine();
                file = AskForFile();
                degree = AskForDegree();
                Console.WriteLine();
            }
            else if (file == null) { Usage(); return 1; }

            if (!System.IO.File.Exists(file))
                throw new ArgumentException("No such file: " + file);

            // The ZOS-API assemblies are NOT copied next to this program - they have to be the
            // ones belonging to the OpticStudio being driven - so nothing can load them until
            // ZOSAPI_Initializer has installed the resolver that finds them.
            //
            // Which is why the work is in another method, and why NOTHING above this line may
            // mention a ZOSAPI type. The JIT resolves every type a method refers to when it
            // COMPILES that method, which happens before the method's first statement runs. Put
            // the initialise and the first use in one method and the runtime tries to load
            // ZOSAPI before the handler that can find it exists, and says only
            //
            //     Could not load file or assembly 'ZOSAPI, Version=1.0.0.0'
            //
            // which points at everything except the cause.
            if (!ZOSAPI_NetHelper.ZOSAPI_Initializer.Initialize())
                throw new InvalidOperationException(
                    "OpticStudio was not found. ZOSAPI_NetHelper reads its location from the "
                    + "registry, so this usually means OpticStudio has never been run on this "
                    + "machine - installing it is not enough.");

            return Connect(file, degree);
        }

        /// <summary>
        /// The lens to work on. Typed, pasted, dragged onto the window - a dragged path arrives
        /// wrapped in quotes, so they come off - or chosen from a browser on an empty answer. It
        /// asks again rather than giving up: a mistyped path is not a reason to start over.
        /// </summary>
        private static string AskForFile()
        {
            while (true)
            {
                Console.Write("Lens file (or press Enter to browse): ");
                // A path dragged onto the window arrives wrapped in double quotes.
                string answer = (Console.ReadLine() ?? string.Empty).Trim().Trim('"');

                if (answer.Length == 0)
                {
                    string? picked = Browse();
                    if (picked == null) { Console.WriteLine("  Nothing chosen."); continue; }
                    Console.WriteLine("  " + picked);
                    return picked;
                }
                if (System.IO.File.Exists(answer)) return answer;
                Console.WriteLine("  There is no file there. Try again, or press Enter to browse.");
            }
        }

        private static string? Browse()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "The lens to compute the seventh order of";
                dialog.Filter = "OpticStudio lens files|*.zmx;*.zos|All files|*.*";
                dialog.CheckFileExists = true;
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                     ? dialog.FileName : null;
            }
        }

        /// <summary>
        /// The truncation. Three is the answer almost always. It is asked because raising it and
        /// getting the same numbers back is the one check that says the series has converged on
        /// THIS design, and nobody runs a check they have never heard of.
        /// </summary>
        private static int AskForDegree()
        {
            while (true)
            {
                Console.Write("Truncation degree [3 = seventh order, Enter to accept]: ");
                string answer = (Console.ReadLine() ?? string.Empty).Trim();
                if (answer.Length == 0) return 3;
                if (int.TryParse(answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d)
                    && d >= 3 && d <= 8) return d;
                Console.WriteLine("  A number from 3 to 8.");
            }
        }

        /// <summary>Keeps the window up when it was started by double-clicking it.</summary>
        private static void Hold()
        {
            Console.WriteLine();
            Console.Write("Press Enter to close. ");
            try { Console.ReadLine(); } catch (Exception) { }
        }

        /// <summary>
        /// Everything that touches the ZOS-API. Separate from the caller, and not inlined into
        /// it, so that it is compiled only after the resolver is in place - see the note above.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Connect(string file, int degree)
        {
            // STANDALONE only. This starts an OpticStudio of its own, does its work and closes
            // it again, so it never touches a session someone is using and cannot leave one in
            // a state it did not find it in. It does take a licence seat while it runs.
            var connection = new ZOSAPI_Connection();
            IZOSAPI_Application? app = null;
            try
            {
                app = connection.CreateNewApplication();
                if (app == null) throw new InvalidOperationException(
                    "OpticStudio would not start. That is usually a licence already in use.");
                if (!app.IsValidLicenseForAPI)
                    throw new InvalidOperationException("This OpticStudio licence does not permit API use.");
                if (!app.PrimarySystem.LoadFile(file, false))
                    throw new InvalidOperationException("Could not open " + file);

                Report(app.PrimarySystem, degree, file);
                return 0;
            }
            finally
            {
                if (app != null) app.CloseApplication();
            }
        }

        /// <summary>
        /// Reads the lens OpticStudio has open and prints the report.
        ///
        /// <para>The report itself is built by <c>ForbesReport</c> in the shared library, which
        /// is also what the command line and the MCP server call. There is one formatter for
        /// the three of them on purpose: three copies would agree on the day they were written
        /// and not for long, and the whole reason this program is C# rather than a second macro
        /// is to use the code the rest of the repository is tested against.</para>
        /// </summary>
        private static void Report(IOpticalSystem zos, int degree, string file)
        {
            var read = LensBridge.Build(zos);
            var paraxial = ParaxialTrace.Trace(read.System, read.Indices, read.MaxFieldDegrees);
            string? body = ForbesReport.Build(read.System, read.Indices, paraxial,
                                              read.MaxFieldDegrees, degree);
            if (body == null)
                throw new InvalidOperationException(
                    "The coefficients could not be separated. That happens when the system has no "
                    + "field, or when the series trace failed to close on this design.");

            var w = Console.Out;
            w.WriteLine("FORBES7 - seventh-order aberration coefficients, intrinsic and induced");
            w.WriteLine("Forbes, J. Opt. Soc. Am. 73, 782 (1983). Spheres, conics and even aspheres.");
            w.WriteLine();
            w.WriteLine(F("Lens                   {0}", file));
            w.WriteLine(F("Wavelength             {0:0.000000} um", read.Wavelength));
            w.Write(body);
            w.WriteLine();
            w.WriteLine("The third and fifth orders can be checked against Analyze > Aberrations >");
            w.WriteLine("Seidel Coefficients and against FIFTHORD, without trusting anything here.");
        }

        private static string F(string f, params object?[] a) =>
            string.Format(CultureInfo.InvariantCulture, f, a);

        private static void Usage()
        {
            Console.WriteLine("FORBES7 - seventh-order aberration coefficients, intrinsic and induced");
            Console.WriteLine();
            Console.WriteLine("  forbes7                     asks for the lens and the truncation.");
            Console.WriteLine("  forbes7 <file.zmx>          starts an OpticStudio of its own, opens the");
            Console.WriteLine("                              file, reports, and closes it again. It never");
            Console.WriteLine("                              touches a session you are working in.");
            Console.WriteLine("  forbes7 [...] --degree N    truncation, 3 to 8. Three is the seventh order.");
            Console.WriteLine("                              A higher value costs time and MUST NOT change");
            Console.WriteLine("                              the answer - which is worth checking once.");
        }
    }
}
