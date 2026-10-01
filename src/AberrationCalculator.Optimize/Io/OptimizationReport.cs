using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using AberrationCalculator.Optimize.Operands;

namespace AberrationCalculator.Optimize.Io;

/// <summary>
/// What the optimiser did, written to be read.
///
/// <para>A merit number on its own says whether a run went well and nothing about why. The three
/// things a designer actually needs are here instead: which route the seventh order came by and
/// what forced it, WHICH VARIABLE MOVED and by how much, and which operand is holding the
/// remaining error - because that last column is what says whether to add a variable, relax a
/// constraint, or accept the design.</para>
/// </summary>
public static class OptimizationReport
{
    public static string Build(RunOutcome outcome, string? lensName = null)
    {
        if (outcome == null) throw new ArgumentNullException(nameof(outcome));

        var sb = new StringBuilder();
        Header(sb, outcome, lensName);

        if (!outcome.Ok)
        {
            sb.AppendLine("  THE DESIGN COULD NOT BE EVALUATED");
            sb.AppendLine("  " + (outcome.Failure ?? "no reason given"));
            sb.AppendLine();
            return sb.ToString();
        }

        Changes(sb, outcome);
        Variables(sb, outcome);
        Operands(sb, outcome);
        Merit(sb, outcome);
        return sb.ToString();
    }

    /// <summary>
    /// What actually moved in the LENS, in the terms a prescription is written in.
    ///
    /// <para>The variables table below says a curvature went from 0.0454 to 0.0464, which is true
    /// and nearly unreadable. A designer works in RADII, and wants to know that surface 1 went
    /// from 22.014 to 21.553 and that the second element became a different glass. That is what
    /// gets typed into another program, quoted in a review, or handed to a shop, so it is the
    /// first thing in the report rather than something to be reconstructed from the variables.
    /// </para>
    ///
    /// <para>Only what changed is listed. A run that moved two surfaces should produce two lines,
    /// not a full prescription with two numbers different somewhere inside it.</para>
    /// </summary>
    private static void Changes(StringBuilder sb, RunOutcome o)
    {
        if (o.Start == null) return;

        var rows = new List<(int Surface, string Quantity, string Before, string After,
                             string Change)>();
        int count = Math.Min(o.Start.Surfaces.Count, o.Best.Surfaces.Count);

        for (int i = 0; i < count; i++)
        {
            var a = o.Start.Surfaces[i];
            var b = o.Best.Surfaces[i];

            if (Moved(a.Curvature, b.Curvature))
                rows.Add((i, "radius", Num(a.Radius), Num(b.Radius), Percent(a.Radius, b.Radius)));

            if (Moved(a.Thickness, b.Thickness))
                rows.Add((i, "thickness", Num(a.Thickness), Num(b.Thickness),
                          Percent(a.Thickness, b.Thickness)));

            string beforeGlass = a.Material ?? string.Empty;
            string afterGlass = b.Material ?? string.Empty;
            if (!string.Equals(beforeGlass, afterGlass, StringComparison.OrdinalIgnoreCase))
                rows.Add((i, "glass",
                          beforeGlass.Length == 0 ? "air" : beforeGlass,
                          afterGlass.Length == 0 ? "air" : afterGlass, "-"));

            // The figuring. Aspheric coefficients are tiny numbers (1e-7 and smaller), so the
            // absolute floor Moved() uses for lengths would hide every one of them; they are
            // compared relatively instead.
            if (FiguringMoved(a.Conic, b.Conic))
                rows.Add((i, "conic", Num(a.Conic), Num(b.Conic), FiguringPercent(a.Conic, b.Conic)));

            int terms = Math.Min(a.AsphericCoefficients.Length, b.AsphericCoefficients.Length);
            for (int k = 0; k < terms; k++)
                if (FiguringMoved(a.AsphericCoefficients[k], b.AsphericCoefficients[k]))
                    rows.Add((i, "A" + (2 * k + 2).ToString(CultureInfo.InvariantCulture),
                              Num(a.AsphericCoefficients[k]), Num(b.AsphericCoefficients[k]),
                              FiguringPercent(a.AsphericCoefficients[k], b.AsphericCoefficients[k])));
        }

        sb.AppendLine("WHAT CHANGED");
        sb.AppendLine();

        if (rows.Count == 0)
        {
            sb.AppendLine("  Nothing. The design that came out is the one that went in.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("  Lengths in millimetres. The saved file is written in its own units.");
        sb.AppendLine();
        sb.AppendLine("  surface  quantity           before             after           change");
        sb.AppendLine("  ---------------------------------------------------------------------");

        foreach (var r in rows)
        {
            sb.Append("  ").Append(r.Surface.ToString(CultureInfo.InvariantCulture).PadRight(9))
              .Append(r.Quantity.PadRight(10))
              .Append(r.Before.PadLeft(17))
              .Append(r.After.PadLeft(18))
              .AppendLine(r.Change.PadLeft(16));
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Whether two values differ by enough to be worth reporting.
    ///
    /// <para>An optimiser leaves rounding dust on parameters it did not really move - a variable
    /// that came back to where it started can be a few parts in 10^15 away - and listing those as
    /// changes would bury the two lines that matter.</para>
    /// </summary>
    private static bool Moved(double before, double after)
    {
        if (double.IsInfinity(before) && double.IsInfinity(after)) return false;
        if (double.IsInfinity(before) != double.IsInfinity(after)) return true;
        return Math.Abs(after - before) > 1e-10 * Math.Max(1.0, Math.Abs(before));
    }

    private static bool FiguringMoved(double before, double after) =>
        before != after && Math.Abs(after - before) > 1e-12 * Math.Max(Math.Abs(before), Math.Abs(after));

    // A coefficient of 3e-15 is not zero, so only an exact zero reads as "from zero".
    private static string FiguringPercent(double before, double after) =>
        before == 0.0 ? "from zero"
            : ((after - before) / Math.Abs(before) * 100.0)
              .ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";

    private static string Percent(double before, double after)
    {
        if (double.IsInfinity(before) || double.IsInfinity(after)) return "-";
        if (Math.Abs(before) < 1e-14) return "from zero";
        return ((after - before) / Math.Abs(before) * 100.0)
               .ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static void Header(StringBuilder sb, RunOutcome o, string? lensName)
    {
        sb.AppendLine();
        sb.AppendLine("OPTIMISATION");
        if (!string.IsNullOrWhiteSpace(lensName)) sb.AppendLine("  " + lensName);
        sb.AppendLine();

        sb.AppendLine("  Method     " + o.Method);
        sb.Append("  Problem    ").Append(o.Variables.Count)
          .Append(o.Variables.Count == 1 ? " variable, " : " variables, ")
          .Append(o.Operands.Count)
          .AppendLine(o.Operands.Count == 1 ? " operand" : " operands");

        if (o.Hops > 0)
        {
            sb.Append("  Hopping    ").Append(o.Hops).Append(" hops over ")
              .Append(o.Chains).Append(o.Chains == 1 ? " chain, " : " chains, ")
              .Append(o.Accepted).Append(" accepted, ").Append(o.Rejected).AppendLine(" rejected");
            if (o.GlassSwaps > 0)
                sb.AppendLine("             " + o.GlassSwaps + " glass substitutions accepted");
        }
        else
        {
            sb.AppendLine("  Stopped    after " + o.Iterations + " iterations: " + o.Stop);
        }

        sb.AppendLine();
        foreach (string line in Wrap(o.Route, 74)) sb.AppendLine("  " + line);
        sb.AppendLine();
    }

    /// <summary>
    /// What moved. A variable that did not move is worth seeing too: it usually means the
    /// merit function does not care about it, and a variable nothing depends on is a variable
    /// that should be spent somewhere else.
    /// </summary>
    private static void Variables(StringBuilder sb, RunOutcome o)
    {
        if (o.Variables.Count == 0) return;

        sb.AppendLine("VARIABLES");
        sb.AppendLine();
        sb.AppendLine("  name            start              end           change");
        sb.AppendLine("  ------------------------------------------------------------");

        for (int i = 0; i < o.Variables.Count; i++)
        {
            double a = i < o.StartX.Length ? o.StartX[i] : 0.0;
            double b = i < o.EndX.Length ? o.EndX[i] : 0.0;

            sb.Append("  ").Append(o.Variables[i].Name.PadRight(10))
              .Append(Num(a).PadLeft(15))
              .Append(Num(b).PadLeft(18));

            double change = b - a;
            string text = Math.Abs(a) > 1e-14
                ? (change / Math.Abs(a) * 100.0).ToString("+0.00;-0.00;0.00",
                                                          CultureInfo.InvariantCulture) + "%"
                : (Math.Abs(change) > 0.0 ? "from zero" : "-");
            sb.AppendLine(text.PadLeft(16));
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Where the design stands against what was asked of it, and which operand owns the error
    /// that is left.
    /// </summary>
    private static void Operands(StringBuilder sb, RunOutcome o)
    {
        if (o.Operands.Count == 0) return;

        double total = 0.0;
        foreach (double r in o.EndResiduals) total += r * r;

        sb.AppendLine("OPERANDS");
        sb.AppendLine();
        sb.AppendLine("  operand          asked            start              end     share");
        sb.AppendLine("  --------------------------------------------------------------------");

        for (int i = 0; i < o.Operands.Count; i++)
        {
            var op = o.Operands[i];

            // Reported in the units the operand was DECLARED in. A focal length is targeted
            // through the power, because a flat design has no focal length to speak of - but a
            // designer asked for a focal length and a table of reciprocals would be a poor
            // answer. See Operand.IsPower.
            double a = op.AsDeclared(i < o.StartValues.Length ? o.StartValues[i] : 0.0);
            double b = op.AsDeclared(i < o.EndValues.Length ? o.EndValues[i] : 0.0);
            double r = i < o.EndResiduals.Length ? o.EndResiduals[i] : 0.0;

            string asked = op.IsBoundary
                ? (op.Min.HasValue && op.Max.HasValue
                     ? Num(op.Min.Value) + ".." + Num(op.Max.Value)
                     : op.Min.HasValue ? ">= " + Num(op.Min.Value)
                                       : "<= " + Num(op.Max!.Value))
                : "-> " + Num(op.Target);

            // A boundary operand that is satisfied has no share of anything, and saying "0.0%"
            // would suggest it is contributing a little. It is not contributing at all.
            string share = op.IsBoundary && r == 0.0
                ? "met"
                : (total > 0.0 ? (r * r / total * 100.0).ToString("0.0",
                                                                  CultureInfo.InvariantCulture) + "%"
                               : "-");

            sb.Append("  ").Append(op.Label.PadRight(14))
              .Append(asked.PadLeft(12))
              .Append(Num(a).PadLeft(17))
              .Append(Num(b).PadLeft(17))
              .AppendLine(share.PadLeft(10));
        }
        sb.AppendLine();
    }

    private static void Merit(StringBuilder sb, RunOutcome o)
    {
        sb.Append("MERIT   ").Append(Num(o.InitialMerit))
          .Append("  ->  ").Append(Num(o.FinalMerit));

        if (o.InitialMerit > 0.0 && !double.IsInfinity(o.InitialMerit))
        {
            double change = (o.FinalMerit - o.InitialMerit) / o.InitialMerit * 100.0;
            sb.Append("   (").Append(change.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture))
              .Append("%)");
        }
        sb.AppendLine();

        if (!o.Improved)
            sb.AppendLine("        No improvement was found. The design returned is the one that "
                        + "went in.");
        sb.AppendLine();
    }

    private static string Num(double v)
    {
        if (double.IsPositiveInfinity(v)) return "inf";
        if (double.IsNegativeInfinity(v)) return "-inf";
        if (double.IsNaN(v)) return "nan";
        double a = Math.Abs(v);
        if (a != 0.0 && (a < 1e-4 || a >= 1e7))
            return v.ToString("0.000000E+00", CultureInfo.InvariantCulture);
        return v.ToString("0.#######", CultureInfo.InvariantCulture);
    }

    private static string[] Wrap(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

        var words = text.Split(' ');
        var lines = new System.Collections.Generic.List<string>();
        var line = new StringBuilder();

        foreach (string w in words)
        {
            if (line.Length > 0 && line.Length + 1 + w.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines.ToArray();
    }
}
