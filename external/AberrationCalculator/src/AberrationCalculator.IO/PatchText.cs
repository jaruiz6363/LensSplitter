using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// A text file held as lines, remembering how it was encoded and how its lines ended.
    ///
    /// <para>A .zmx is UTF-16 with CRLF. Reading it as UTF-8 and writing it back as UTF-8 would
    /// produce a file the format's own readers cannot open, and reading the lines with one
    /// convention and writing them with another would change every line in the diff. Both are
    /// remembered so that a patched file differs from the original only where it was
    /// patched.</para>
    ///
    /// <para>Shared by every patcher, because the rule is the same for all of them: the file
    /// belongs to whoever wrote it, and an optimiser is a guest in it.</para>
    /// </summary>
    internal sealed class PatchText
    {
        /// <summary>The lines. A null entry is a line that has been deleted.</summary>
        public List<string?> Lines { get; private set; } = new();

        private Encoding _encoding = new UTF8Encoding(false);
        private string _newline = Environment.NewLine;
        private bool _trailingNewline = true;

        public static PatchText Read(string path)
        {
            var file = new PatchText();
            bool hadByteOrderMark = HasByteOrderMark(path);

            using (var reader = new StreamReader(path, Encoding.UTF8,
                                                 detectEncodingFromByteOrderMarks: true))
            {
                string text = reader.ReadToEnd();
                file._encoding = reader.CurrentEncoding;
                file._trailingNewline = text.EndsWith("\n", StringComparison.Ordinal);

                // SPLIT ON EVERY LINE END, WHICHEVER IT IS. A file can carry both - one written
                // by one tool and edited by another - and this used to take the file's ending
                // from whether it held a single CRLF, then split on that alone: every LF-only
                // line stayed glued to its neighbours inside one "line", its keyword was never
                // seen, and a save edited nothing and still said it had written the design. The
                // readers split on either, so the lens read correctly and nothing looked wrong
                // (September 2026, a patent example built by hand: 12 CRLF, 62 LF). The file is
                // written back with the ending most of its lines had.
                var split = text.Split('\n');
                int count = split.Length;
                // Split leaves an empty tail for a file ending in a newline; that is the
                // newline, not a line, and re-adding it on write would grow the file.
                if (file._trailingNewline && count > 0 && split[count - 1].Length == 0) count--;
                int crlf = 0, lf = 0;
                for (int i = 0; i < count; i++)
                {
                    string line = split[i];
                    bool ended = i < split.Length - 1;          // the last piece had no line end
                    if (line.EndsWith("\r", StringComparison.Ordinal))
                    {
                        line = line.Substring(0, line.Length - 1);
                        if (ended) crlf++;
                    }
                    else if (ended) lf++;
                    file.Lines.Add(line);
                }
                file._newline = crlf >= lf && crlf > 0 ? "\r\n" : "\n";
            }

            // A BYTE ORDER MARK IS PART OF THE FILE, AND SO IS ITS ABSENCE. The reader is asked
            // to fall back to UTF-8 when there is no mark to detect, and hands back the very
            // Encoding.UTF8 it was given - which carries a preamble, and so would put a mark at
            // the front of a file that never had one. Three bytes, at the top of the first line,
            // in every diff.
            if (!hadByteOrderMark && file._encoding.CodePage == 65001)
                file._encoding = new UTF8Encoding(false);

            return file;
        }

        /// <summary>Whether the file actually begins with a byte order mark.</summary>
        private static bool HasByteOrderMark(string path)
        {
            using var stream = File.OpenRead(path);
            var head = new byte[3];
            int n = stream.Read(head, 0, head.Length);

            if (n >= 2 && ((head[0] == 0xFF && head[1] == 0xFE)
                        || (head[0] == 0xFE && head[1] == 0xFF))) return true;

            return n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        }

        /// <summary>Writes the lines back. A null entry is a line that has been deleted.</summary>
        public void Write(string path)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            bool first = true;
            foreach (string? line in Lines)
            {
                if (line == null) continue;
                if (!first) sb.Append(_newline);
                sb.Append(line);
                first = false;
            }
            if (_trailingNewline) sb.Append(_newline);

            File.WriteAllText(path, sb.ToString(), _encoding);
        }

        /// <summary>
        /// Applies a set of line insertions, in one pass, from the bottom of the file upwards.
        ///
        /// <para>Downwards would be wrong: the first insertion shifts every index after it, so
        /// each later one would land a line further from where it was meant to go. Going upwards
        /// the indices below an insertion never move, and none of the positions worked out during
        /// the scan need adjusting.</para>
        /// </summary>
        public void InsertAll(List<(int Index, string Text)> insertions)
        {
            if (insertions == null || insertions.Count == 0) return;
            insertions.Sort((a, b) => b.Index.CompareTo(a.Index));
            foreach (var (index, text) in insertions)
                Lines.Insert(Math.Max(0, Math.Min(index, Lines.Count)), text);
        }
    }

    /// <summary>
    /// Editing one keyword line without disturbing the rest of it.
    ///
    /// <para>Every format patched here is keyword-and-arguments, and the arguments this program
    /// understands are always the leading ones. Solve codes, index hints, aperture flags and
    /// labels sit after them, belong to the format rather than to the optimiser, and are copied
    /// through untouched - as is the indentation, so a patched file diffs against the original in
    /// the values that moved and nowhere else.</para>
    /// </summary>
    internal static class LineEdit
    {
        /// <summary>The keyword, upper-cased. Empty for a blank line.</summary>
        public static string Keyword(string line)
        {
            string t = line.TrimStart();
            int space = IndexOfSpace(t, 0);
            return (space < 0 ? t : t.Substring(0, space)).ToUpperInvariant();
        }

        /// <summary>The n-th argument after the keyword (0-based), or empty when absent.</summary>
        public static string Argument(string line, int n = 0)
        {
            foreach (var (start, end, index) in Fields(line))
                if (index == n + 1) return line.Substring(start, end - start);
            return string.Empty;
        }

        public static int ArgumentAsInt(string line, int fallback, int n = 0) =>
            int.TryParse(Argument(line, n), NumberStyles.Integer, CultureInfo.InvariantCulture,
                         out int v) ? v : fallback;

        public static bool ArgumentAsDouble(string line, out double value, int n = 0) =>
            double.TryParse(Argument(line, n), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out value);

        /// <summary>The leading whitespace of a line, so an inserted neighbour can match it.</summary>
        public static string Indent(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            return line.Substring(0, i);
        }

        /// <summary>
        /// Rewrites the n-th argument, keeping the indentation, the keyword and every other field
        /// exactly as they were. Appends when the line is too short to have that argument.
        /// </summary>
        public static string ReplaceArgument(string line, string value, int n = 0)
        {
            foreach (var (start, end, index) in Fields(line))
                if (index == n + 1)
                    return line.Substring(0, start) + value + line.Substring(end);

            return line + " " + value;
        }

        /// <summary>
        /// The same, but leaves the line untouched when the argument already says this number.
        ///
        /// <para>This is what keeps a patch small. An optimiser moves a handful of the values in a
        /// file; rewriting the rest in this program's spelling of the same number would change
        /// every line in the diff and say nothing.</para>
        /// </summary>
        public static string ReplaceNumberIfChanged(string line, double value, int n = 0)
        {
            if (ArgumentAsDouble(line, out double current, n))
            {
                double tolerance = 1e-12 * Math.Max(1.0, Math.Abs(current));
                if (Math.Abs(current - value) <= tolerance) return line;
            }
            return ReplaceArgument(line, Number(value), n);
        }

        /// <summary>Round-trippable: what is written back reads back as the same double.</summary>
        public static string Number(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        private static int IndexOfSpace(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
                if (s[i] == ' ' || s[i] == '\t') return i;
            return -1;
        }

        /// <summary>
        /// Every whitespace-delimited field of a line as (start, end, ordinal), with the keyword
        /// as ordinal 0. Positions are into the original string, so a caller can splice.
        /// </summary>
        private static IEnumerable<(int Start, int End, int Index)> Fields(string line)
        {
            int i = 0, ordinal = 0;
            while (i < line.Length)
            {
                while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
                if (i >= line.Length) yield break;

                int start = i;
                while (i < line.Length && line[i] != ' ' && line[i] != '\t') i++;
                yield return (start, i, ordinal);
                ordinal++;
            }
        }
    }
}
