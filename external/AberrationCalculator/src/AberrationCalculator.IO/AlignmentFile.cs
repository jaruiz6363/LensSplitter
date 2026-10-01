using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO;

/// <summary>How one surface sits relative to the mechanical axis.</summary>
public sealed class Perturbation
{
    /// <summary>Surface number, as the prescription numbers them.</summary>
    public int Surface { get; set; }

    /// <summary>Decentre along x, in the design's length units.</summary>
    public double DecenterX { get; set; }

    /// <summary>Decentre along y.</summary>
    public double DecenterY { get; set; }

    /// <summary>Tilt about the x axis, in DEGREES - see <see cref="AlignmentFile"/>.</summary>
    public double TiltX { get; set; }

    /// <summary>Tilt about the y axis, in degrees.</summary>
    public double TiltY { get; set; }

    /// <summary>
    /// Fringe Zernike departure of the surface, indexed by term number: <c>[5]</c> and
    /// <c>[6]</c> astigmatism, <c>[7]</c> and <c>[8]</c> coma. Surface SAG in lens units, not
    /// wavefront - see <see cref="AlignmentFile"/>.
    /// </summary>
    public double[] Zernike { get; } = new double[19];

    /// <summary>True when any Zernike term has been stated.</summary>
    public bool HasZernike
    {
        get { foreach (var z in Zernike) if (z != 0.0) return true; return false; }
    }

    public bool IsZero =>
        DecenterX == 0.0 && DecenterY == 0.0 && TiltX == 0.0 && TiltY == 0.0 && !HasZernike;
}

/// <summary>Every surface that has been said to be out of place.</summary>
public sealed class AlignmentSpecification
{
    public List<Perturbation> Perturbations { get; } = new();

    public bool IsEmpty => Perturbations.Count == 0;

    /// <summary>The entry for a surface, created if it is not there yet.</summary>
    public Perturbation For(int surface)
    {
        var found = Perturbations.Find(p => p.Surface == surface);
        if (found != null) return found;
        var made = new Perturbation { Surface = surface };
        Perturbations.Add(made);
        return made;
    }

    /// <summary>
    /// Writes the perturbations onto a system, converting the file's DEGREES into the radians
    /// the model carries.
    ///
    /// <para>Surfaces the file does not mention are left alone rather than zeroed, so applying
    /// two specifications in turn accumulates rather than replacing - which is what a caller
    /// building up a hypothesis expects.</para>
    /// </summary>
    public void ApplyTo(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        const double toRadians = Math.PI / 180.0;

        foreach (var p in Perturbations)
        {
            if (p.Surface < 0 || p.Surface >= system.Surfaces.Count)
                throw new FormatException(
                    $"surface {p.Surface} is not in this design, which has "
                  + $"{system.Surfaces.Count - 1}");

            var s = system.Surfaces[p.Surface];
            s.DecenterX = p.DecenterX;
            s.DecenterY = p.DecenterY;
            s.TiltX = p.TiltX * toRadians;
            s.TiltY = p.TiltY * toRadians;

            if (p.HasZernike)
            {
                if (s.FringeZernike == null || s.FringeZernike.Length < p.Zernike.Length)
                    s.FringeZernike = new double[p.Zernike.Length];
                Array.Copy(p.Zernike, s.FringeZernike, p.Zernike.Length);
            }
        }
    }
}

/// <summary>
/// The alignment file (<c>.align</c>): which surfaces are out of place, and by how much.
///
/// <code>
///     TILT 2 X 0.115
///     DEC  2 Y 0.05
/// </code>
///
/// <para><b>This is not part of the design, and that is why it is a file of its own.</b> A
/// perturbation is a statement about one BUILT INSTANCE - "this surface ended up fifty microns
/// off" - not about the lens. Writing it into the prescription would corrupt the design record
/// with a build error, and for a <c>.lhlt</c>, which keeps its own variables inside the lens
/// file, that is exactly where the variables rule would have put it. So alignment gets its own
/// sidecar, uniformly for all six formats, and deleting that file restores the nominal design
/// exactly.</para>
///
/// <para>It is also not a <c>.var</c>. Variables say what the OPTIMISER may move; these say what
/// the WORKSHOP got wrong. Neither is a merit function, which is the only one of the three that
/// can be carried from one design to another at all.</para>
///
/// <para><b>Tilts are in DEGREES</b>, which is how every lens format states one. Degrees are the
/// only angular unit this program takes from a user anywhere - the <c>ASBLT</c> merit operand
/// states its tilt TOLERANCE in them too - so a number copied from one place to the other means
/// what it said. Radians appear once, in the conversion below, and nowhere else.</para>
///
/// <para><b>A line MERGES</b>, exactly as <c>VAR</c> does: <c>TILT 2 X 0.1</c> followed by
/// <c>TILT 2 Y 0.05</c> leaves surface 2 tilted about both axes. <c>FREE</c> is the way back
/// out, and <c>TILT 2 FREE</c> drops only the tilt, leaving any decentre alone.</para>
///
/// <para><b>What this is for, and what it is not.</b> The paraxial trace here is a rotationally
/// symmetric construction and does not read these at all; nodal aberration theory treats them as
/// small departures from the symmetric system, which is right for alignment errors and wrong for
/// a design with large deliberate tilts. Such a design needs a real-ray treatment on the optical
/// axis ray, which this program does not yet have.</para>
/// </summary>
public static class AlignmentFile
{
    /// <summary>The extension a lens's alignment sidecar carries.</summary>
    public const string Extension = ".align";

    /// <summary>
    /// The alignment file that belongs beside a lens, named for it INCLUDING its extension:
    /// <c>triplet.zmx</c> reads <c>triplet.zmx.align</c>.
    ///
    /// <para>That is the same rule the merit function and variable sidecars follow, and for the
    /// same reason: a folder holding <c>triplet.zmx</c> and <c>triplet.seq</c> must keep their
    /// settings apart. This once dropped the extension, so those two lenses shared one alignment
    /// file - two different designs, one record of how the workshop had built them.</para>
    /// </summary>
    public static string PathFor(string lensPath) =>
        (lensPath ?? throw new ArgumentNullException(nameof(lensPath))) + Extension;

    /// <summary>Reads the file, or an empty specification when there is none.</summary>
    public static AlignmentSpecification ReadFor(string lensPath)
    {
        string path = PathFor(lensPath);
        return File.Exists(path) ? Read(path) : new AlignmentSpecification();
    }

    public static AlignmentSpecification Read(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllLines(path), path);
    }

    public static AlignmentSpecification Parse(IEnumerable<string> lines, string? origin = null)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));

        var spec = new AlignmentSpecification();
        int number = 0;

        foreach (string raw in lines)
        {
            number++;
            string line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            var token = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string keyword = token[0].ToUpperInvariant();

            try
            {
                if (keyword == "TILT") Merge(spec, token, tilt: true);
                else if (keyword == "DEC" || keyword == "DECENTER" || keyword == "DECENTRE")
                    Merge(spec, token, tilt: false);
                else if (keyword == "ZERN" || keyword == "ZERNIKE") MergeZernike(spec, token);
                else throw new FormatException($"expected TILT, DEC or ZERN, found '{token[0]}'");
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new FormatException(
                    $"{origin ?? "alignment"}, line {number}: {ex.Message}\n  {raw.Trim()}");
            }
        }

        // A surface mentioned only to be freed carries nothing and is not worth keeping.
        spec.Perturbations.RemoveAll(p => p.IsZero);
        return spec;
    }

    /// <summary>One line of a command or a file: <c>TILT|DEC n [X v] [Y v] [FREE]</c>.</summary>
    public static void MergeLine(AlignmentSpecification spec, string line)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (line == null) throw new ArgumentNullException(nameof(line));

        var token = StripComment(line).Trim()
                        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (token.Length == 0) throw new FormatException("an empty alignment line says nothing");

        string keyword = token[0].ToUpperInvariant();
        if (keyword == "TILT") Merge(spec, token, tilt: true);
        else if (keyword == "DEC" || keyword == "DECENTER" || keyword == "DECENTRE")
            Merge(spec, token, tilt: false);
        else if (keyword == "ZERN" || keyword == "ZERNIKE") MergeZernike(spec, token);
        else throw new FormatException($"expected TILT, DEC or ZERN, found '{token[0]}'");

        spec.Perturbations.RemoveAll(p => p.IsZero);
    }

    private static void Merge(AlignmentSpecification spec, string[] token, bool tilt)
    {
        string what = tilt ? "TILT" : "DEC";
        if (token.Length < 2) throw new FormatException(what + " needs a surface");

        int surface = Whole(token[1], "surface");
        if (surface < 1) throw new FormatException(
            $"surface {surface} cannot be perturbed - surfaces are numbered from 1");

        var p = spec.For(surface);

        // Nothing but a surface means "show me", which the caller handles; here it is a no-op
        // rather than an error, so that TILT 2 on its own does not wipe surface 2.
        for (int i = 2; i < token.Length; i++)
        {
            switch (token[i].ToUpperInvariant())
            {
                case "X":
                    if (tilt) p.TiltX = Number(token, ++i, "X");
                    else p.DecenterX = Number(token, ++i, "X");
                    break;

                case "Y":
                    if (tilt) p.TiltY = Number(token, ++i, "Y");
                    else p.DecenterY = Number(token, ++i, "Y");
                    break;

                // Merging means a value cannot be removed by leaving it off, so there has to be
                // a word that says to drop it. FREE drops only this KIND, so that clearing a
                // tilt does not silently clear the decentre with it.
                case "FREE":
                    if (tilt) { p.TiltX = 0.0; p.TiltY = 0.0; }
                    else { p.DecenterX = 0.0; p.DecenterY = 0.0; }
                    break;

                default:
                    throw new FormatException($"unexpected '{token[i]}' on a {what} line");
            }
        }
    }

    /// <summary>
    /// <c>ZERN n [Z5 v] [Z6 v] [Z7 v] [Z8 v] ... [FREE]</c> - a Zernike departure on a surface.
    ///
    /// <para>Terms are named by their Fringe number, so the line reads the way an interferogram
    /// report is written. Any term from 1 to 18 is accepted and carried; only 5 to 8 are acted
    /// on today, and a stated term that is not yet used is kept rather than refused so that a
    /// file survives a round trip.</para>
    /// </summary>
    private static void MergeZernike(AlignmentSpecification spec, string[] token)
    {
        if (token.Length < 2) throw new FormatException("ZERN needs a surface");

        int surface = Whole(token[1], "surface");
        if (surface < 1) throw new FormatException(
            $"surface {surface} cannot carry a Zernike term - surfaces are numbered from 1");

        var p = spec.For(surface);

        for (int i = 2; i < token.Length; i++)
        {
            string word = token[i].ToUpperInvariant();

            if (word == "FREE")
            {
                Array.Clear(p.Zernike, 0, p.Zernike.Length);
                continue;
            }

            if (word.Length < 2 || word[0] != 'Z'
                || !int.TryParse(word.Substring(1), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out int term))
                throw new FormatException(
                    $"expected a Fringe term like Z5, found '{token[i]}' on a ZERN line");

            if (term < 1 || term >= p.Zernike.Length)
                throw new FormatException(
                    $"Z{term} is outside the Fringe terms this carries, 1 to {p.Zernike.Length - 1}");

            p.Zernike[term] = Number(token, ++i, word);
        }
    }

    /// <summary>The file, as text. One line per kind per surface, so it reads back identical.</summary>
    public static string Write(AlignmentSpecification spec, string? header = null)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var sb = new StringBuilder();
        if (header != null) sb.AppendLine("# " + header);
        sb.AppendLine("# Alignment: how the surfaces of one BUILT instance sit relative to the axis.");
        sb.AppendLine("# Tilts in degrees, decentres in the design's length units.");
        sb.AppendLine("# This is not part of the design - delete this file to restore the nominal one.");
        sb.AppendLine();

        var ordered = new List<Perturbation>(spec.Perturbations);
        ordered.Sort((a, b) => a.Surface.CompareTo(b.Surface));

        foreach (var p in ordered)
        {
            if (p.TiltX != 0.0 || p.TiltY != 0.0)
                sb.AppendLine(Line("TILT", p.Surface, p.TiltX, p.TiltY));
            if (p.DecenterX != 0.0 || p.DecenterY != 0.0)
                sb.AppendLine(Line("DEC ", p.Surface, p.DecenterX, p.DecenterY));
            if (p.HasZernike)
            {
                var z = new StringBuilder();
                z.Append("ZERN ").Append(p.Surface.ToString(CultureInfo.InvariantCulture));
                for (int t = 1; t < p.Zernike.Length; t++)
                    if (p.Zernike[t] != 0.0)
                        z.Append(" Z").Append(t.ToString(CultureInfo.InvariantCulture))
                         .Append(' ').Append(p.Zernike[t].ToString("R", CultureInfo.InvariantCulture));
                sb.AppendLine(z.ToString());
            }
        }
        return sb.ToString();
    }

    private static string Line(string keyword, int surface, double x, double y)
    {
        var sb = new StringBuilder();
        sb.Append(keyword).Append(' ').Append(surface.ToString(CultureInfo.InvariantCulture));
        if (x != 0.0) sb.Append(" X ").Append(x.ToString("R", CultureInfo.InvariantCulture));
        if (y != 0.0) sb.Append(" Y ").Append(y.ToString("R", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string StripComment(string line)
    {
        int at = line.IndexOf('#');
        return at < 0 ? line : line.Substring(0, at);
    }

    private static int Whole(string text, string what)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            throw new FormatException($"'{text}' is not a {what}");
        return v;
    }

    private static double Number(string[] token, int at, string what)
    {
        if (at >= token.Length) throw new FormatException(what + " needs a value");
        if (!double.TryParse(token[at], NumberStyles.Float, CultureInfo.InvariantCulture,
                             out double v))
            throw new FormatException($"'{token[at]}' is not a number, for {what}");
        return v;
    }
}
