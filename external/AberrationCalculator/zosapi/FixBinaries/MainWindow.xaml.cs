using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FixBinaries
{
    /// <summary>
    /// Finds OpticStudio and writes the one file the Forbes7 project needs and the repository
    /// does not have: <c>ZemaxPaths.props</c>, holding the install directory.
    ///
    /// <para>It is a separate program rather than a line in the build because the answer is
    /// different on every machine and is not anybody else's business. Committing a path would
    /// publish both where OpticStudio is installed and which release it is; a default in the
    /// project file would do the same, and would be wrong everywhere but one desk. So the file
    /// is generated, and it is in .gitignore.</para>
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Where OpticStudio has been installed under the names it has used.</summary>
        private static readonly string[] SearchPatterns =
        {
            @"Ansys Zemax OpticStudio*",
            @"Zemax OpticStudio*",
            @"OpticStudio*",
        };

        private static readonly string[] Required =
        {
            "ZOSAPI.dll", "ZOSAPI_Interfaces.dll", "ZOSAPI_NetHelper.dll",
        };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => Scan();
        }

        private void Scan()
        {
            lstInstallations.Items.Clear();
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            };

            foreach (string root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
                foreach (string pattern in SearchPatterns)
                {
                    string[] dirs;
                    try { dirs = Directory.GetDirectories(root, pattern); }
                    catch (Exception) { continue; }

                    // Newest first, so the usual answer is the one already selected.
                    foreach (string dir in dirs.OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                        if (Complete(dir) && !lstInstallations.Items.Contains(dir))
                            lstInstallations.Items.Add(dir);
                }

            if (lstInstallations.Items.Count > 0) lstInstallations.SelectedIndex = 0;
            else txtStatus.Text = "No OpticStudio found in Program Files. Browse to it instead - "
                                + "it is the folder holding OpticStudio.exe.";
            Refresh();
        }

        private static bool Complete(string dir) =>
            !string.IsNullOrWhiteSpace(dir) && Required.All(d => File.Exists(Path.Combine(dir, d)));

        private void Installations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstInstallations.SelectedItem is string path) txtPath.Text = path;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "The OpticStudio folder - the one holding OpticStudio.exe";
                if (Directory.Exists(txtPath.Text)) dialog.SelectedPath = txtPath.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    txtPath.Text = dialog.SelectedPath;
            }
        }

        private void Path_Changed(object sender, TextChangedEventArgs e) => Refresh();

        /// <summary>Says which of the three assemblies is there, one line each, so that a wrong
        /// folder is obvious rather than showing up later as a build error about a reference.</summary>
        private void Refresh()
        {
            if (txtFound == null) return;
            string path = txtPath.Text;
            var sb = new StringBuilder();
            bool all = true;
            foreach (string dll in Required)
            {
                bool ok = !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, dll));
                all &= ok;
                sb.AppendLine((ok ? "  found     " : "  MISSING   ") + dll);
            }
            txtFound.Text = sb.ToString();
            txtFound.Foreground = all ? Brushes.DarkGreen : Brushes.Firebrick;
            btnGenerate.IsEnabled = all;
            if (!all && !string.IsNullOrWhiteSpace(path))
                txtStatus.Text = "That folder does not hold the ZOS-API assemblies.";
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string root = txtPath.Text.TrimEnd('\\') + "\\";
                string target = Target();
                File.WriteAllText(target, Props(root), new UTF8Encoding(false));
                txtStatus.Text = "Written: " + target + Environment.NewLine
                               + "Forbes7 will build now. This file is not in the repository, "
                               + "so it stays on this machine.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Could not write the file: " + ex.Message;
            }
        }

        /// <summary>Beside Forbes7.csproj, which is where its conditional Import looks.</summary>
        private static string Target()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Forbes7")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException(
                    "The Forbes7 folder was not found above this program. Run it from where it "
                    + "was built, inside the solution.");
            return Path.Combine(dir.FullName, "Forbes7", "ZemaxPaths.props");
        }

        private static string Props(string root) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
            "<!-- Written by FixBinaries. Machine-specific; not in the repository. -->" + Environment.NewLine +
            "<Project>" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            "    <ZEMAX_ROOT>" + root + "</ZEMAX_ROOT>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }
}
