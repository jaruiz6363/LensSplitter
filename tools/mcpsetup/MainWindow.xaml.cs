using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace AberrationCalculator.McpSetup
{
    /// <summary>
    /// Registers the abcalc MCP server with the Claude clients on this machine.
    ///
    /// <para><b>It edits a file the user did not write and may care about a great deal.</b>
    /// Claude's configuration usually holds other servers, and losing them to a tool meant to
    /// help would be a poor trade. So: it takes a timestamped backup before every write, it
    /// merges rather than replaces, it leaves every key it does not own alone, and it prints
    /// what else is registered so the user can see for themselves that nothing went missing.
    /// </para>
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ServerExe = "abcalc-mcp.exe";
        private static readonly string DesktopConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json");

        private string? _claudeCli;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => Scan();
        }

        // ---- discovery ------------------------------------------------------------------

        private void Scan()
        {
            txtLog.Clear();
            txtServer.Text = FindServer() ?? string.Empty;

            bool desktop = File.Exists(DesktopConfig);
            chkDesktop.IsEnabled = desktop;
            chkDesktop.IsChecked = desktop;
            txtDesktop.Text = desktop
                ? "Claude Desktop  —  " + DesktopConfig
                : "Claude Desktop  —  not installed, or it has never been run (no config file yet)";

            _claudeCli = FindClaudeCli();
            chkCode.IsEnabled = _claudeCli != null;
            chkCode.IsChecked = _claudeCli != null;
            txtCode.Text = _claudeCli != null
                ? "Claude Code  —  " + _claudeCli + "   (registered by running: claude mcp add)"
                : "Claude Code  —  the 'claude' command was not found on PATH";

            if (desktop) Report(DesktopConfig);
            ReportCodeScopes();
            RefreshServerState();
            DescribeScope();
            pnlRestart.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// The built server, preferring Release. Walks up from this program to the repository
        /// root rather than assuming a working directory, because a GUI is usually started by
        /// double-clicking it and then the working directory is anybody's guess.
        /// </summary>
        private static string? FindServer()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AberrationCalculator.sln")))
                dir = dir.Parent;
            if (dir == null) return null;

            foreach (string config in new[] { "Release", "Debug" })
            {
                string candidate = Path.Combine(dir.FullName, "src", "AberrationCalculator.Mcp",
                                                "bin", config, "net8.0", ServerExe);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static string? FindClaudeCli()
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            foreach (string dir in (pathVar ?? string.Empty).Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (string name in new[] { "claude.exe", "claude.cmd", "claude.bat" })
                {
                    string candidate;
                    try { candidate = Path.Combine(dir.Trim(), name); }
                    catch (Exception) { continue; }
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        // ---- the config file ------------------------------------------------------------

        /// <summary>Says what is already registered, so nothing appears to vanish.</summary>
        private void Report(string configPath)
        {
            try
            {
                var names = ClaudeConfig.ServerNames(ClaudeConfig.Load(configPath));
                if (names.Count == 0) { Log("Claude Desktop has no MCP servers registered yet."); return; }
                Log("Claude Desktop currently has: " + string.Join(", ", names));
            }
            catch (Exception ex) { Log("Could not read " + configPath + ": " + ex.Message); }
        }

        // ---- actions ---------------------------------------------------------------------

        /// <summary>
        /// What each scope means, in the terms someone deciding between them needs. The default
        /// is "user" and not the CLI's "local", because a person registering a lens tool wants
        /// it on lens files wherever they are, not only inside this repository - which is the
        /// mistake local quietly makes for them.
        /// </summary>
        private void Scope_Changed(object sender, SelectionChangedEventArgs e) => DescribeScope();

        private string Scope => (cboScope?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "user";

        private void DescribeScope()
        {
            if (txtScope == null) return;
            txtScope.Text = Scope switch
            {
                "user"    => "everywhere, for you",
                "local"   => "this folder only",
                "project" => "this folder, and committed to the repository as .mcp.json",
                _         => string.Empty,
            };
        }

        /// <summary>Says where Claude Code has it already, so a second scope is a choice.</summary>
        private void ReportCodeScopes()
        {
            if (_claudeCli == null) return;
            string output = RunClaude("mcp list", out _);
            foreach (string line in output.Split('\n'))
                if (line.Contains(txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                    Log("Claude Code already lists: " + line.Trim());
        }

        private void Register_Click(object sender, RoutedEventArgs e) => Apply(remove: false);
        private void Remove_Click(object sender, RoutedEventArgs e) => Apply(remove: true);
        private void Rescan_Click(object sender, RoutedEventArgs e) => Scan();

        private void Apply(bool remove)
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0) { Log("Give the server a name."); return; }

            string server = txtServer.Text.Trim().Trim('"');
            if (!remove && !File.Exists(server))
            {
                Log("The server was not found at that path. Build it, or browse to " + ServerExe + ".");
                return;
            }

            if (chkDesktop.IsChecked == true) ApplyDesktop(name, server, remove);
            if (chkCode.IsChecked == true) ApplyCode(name, server, remove);
            if (chkDesktop.IsChecked != true && chkCode.IsChecked != true)
            {
                Log("Nothing selected, so nothing was changed.");
                return;
            }

            // The restart is not a footnote. A client reads its servers once, at startup, so
            // until it is restarted the registration has no visible effect at all - and the
            // obvious conclusion to draw from that is that this program did not work.
            var who = new List<string>();
            if (chkDesktop.IsChecked == true) who.Add("Claude Desktop");
            if (chkCode.IsChecked == true) who.Add("Claude Code");
            txtRestart.Text = "Now restart " + string.Join(" and ", who)
                            + ". A client reads its MCP servers once, when it starts, so until "
                            + "you do this it will not list " + name + " - and nothing is wrong.";
            pnlRestart.Visibility = Visibility.Visible;
        }

        private void ApplyDesktop(string name, string server, bool remove)
        {
            try
            {
                JsonObject root = ClaudeConfig.Load(DesktopConfig);
                var before = ClaudeConfig.ServerNames(root);

                if (remove)
                {
                    if (!ClaudeConfig.Remove(root, name))
                    { Log("Claude Desktop: '" + name + "' was not registered."); return; }
                }
                else ClaudeConfig.Register(root, name, server);

                string backup = ClaudeConfig.Save(DesktopConfig, root);
                var after = ClaudeConfig.ServerNames(root);

                Log((remove ? "Claude Desktop: removed '" : "Claude Desktop: registered '") + name + "'.");
                if (backup.Length > 0) Log("  the previous file is kept at " + Path.GetFileName(backup));
                Log("  before: " + string.Join(", ", before));
                Log("  after:  " + string.Join(", ", after));
                foreach (string s in before.Where(s => s != name && !after.Contains(s)))
                    Log("  WARNING: '" + s + "' is no longer there. Restore the backup.");
            }
            catch (Exception ex) { Log("Claude Desktop: " + ex.Message); }
        }

        /// <summary>
        /// Claude Code keeps its own registry and has a command for editing it, so that command
        /// is used rather than this program guessing at the file. If the CLI is not on PATH the
        /// line to run is printed instead, which is more use than an error.
        /// </summary>
        private void ApplyCode(string name, string server, bool remove)
        {
            string scope = Scope;
            string arguments = remove
                ? "mcp remove " + name + " --scope " + scope
                : "mcp add " + name + " --scope " + scope + " -- \"" + server + "\"";
            if (_claudeCli == null)
            {
                Log("Claude Code: the CLI was not found. Run this yourself:");
                Log("  claude " + arguments);
                return;
            }

            string output = RunClaude(arguments, out int exit);
            Log("Claude Code: claude " + arguments);
            Log("  scope '" + scope + "' - " + (scope == "user" ? "available everywhere, for you"
                : scope == "local" ? "available in this folder only"
                : "available in this folder, and written into the repository as .mcp.json"));
            foreach (string line in output.Split('\n').Where(l => l.Trim().Length > 0))
                Log("  " + line.TrimEnd());
            if (exit != 0) Log("  the command reported failure, exit code " + exit);
        }

        private string RunClaude(string arguments, out int exit)
        {
            exit = -1;
            if (_claudeCli == null) return string.Empty;
            try
            {
                var psi = new ProcessStartInfo(_claudeCli, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi)!;
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                exit = p.ExitCode;
                return output;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AberrationCalculator.sln")))
                dir = dir.Parent;
            if (dir == null) { Log("The repository was not found above this program."); return; }

            btnBuild.IsEnabled = false;
            Log("Building the server, which takes a few seconds...");
            try
            {
                var psi = new ProcessStartInfo("dotnet",
                    "build \"" + Path.Combine(dir.FullName, "src", "AberrationCalculator.Mcp") + "\" -c Release")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi)!;
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(180000);
                if (p.ExitCode == 0)
                {
                    txtServer.Text = FindServer() ?? txtServer.Text;
                    Log("Built. " + txtServer.Text);
                }
                else
                {
                    Log("The build failed:");
                    foreach (string line in output.Split('\n').Where(l => l.Contains("error")).Take(6))
                        Log("  " + line.TrimEnd());
                }
            }
            catch (Exception ex) { Log("Could not run dotnet: " + ex.Message); }
            finally { btnBuild.IsEnabled = true; RefreshServerState(); }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "The built MCP server",
                Filter = ServerExe + "|" + ServerExe + "|Programs|*.exe|All files|*.*",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog() == true) txtServer.Text = dialog.FileName;
        }

        private void Server_Changed(object sender, TextChangedEventArgs e) => RefreshServerState();

        private void RefreshServerState()
        {
            if (txtServerState == null) return;
            string path = txtServer.Text.Trim().Trim('"');
            bool ok = path.Length > 0 && File.Exists(path);
            txtServerState.Text = ok
                ? "  found"
                : path.Length == 0
                    ? "  not built yet - press Build it, or browse to " + ServerExe
                    : "  not there";
            txtServerState.Foreground = ok ? System.Windows.Media.Brushes.DarkGreen
                                           : System.Windows.Media.Brushes.Firebrick;
            btnRegister.IsEnabled = ok;
        }

        private void Log(string line) => txtLog.AppendText(line + Environment.NewLine);
    }
}
