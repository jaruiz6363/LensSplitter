using System;
using System.Collections.Generic;
using System.Linq;

using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The analytic Jacobian against a central difference of the same residuals.
///
/// <para><b>Why the reference is a difference quotient and not a stored table.</b> The numbers
/// this compares against are produced by the VALUE code, on the spot. Change an aberration
/// formula and the expectation moves with it; there is nothing to update and nothing that can go
/// stale. What the comparison pins is the one thing that does not follow from the value being
/// right - that the derivative belongs to it.</para>
///
/// <para>A central difference is accurate to about the two-thirds power of machine epsilon, some
/// eleven digits at a well-chosen step, so agreement to five or six digits says the analytic
/// derivative is right and the difference quotient is the one carrying the error.</para>
///
/// <para>Lifted out of <see cref="AnalyticDerivativeTests"/>, which had it private, so that
/// <see cref="DerivativeSweepTests"/> can run the same check over every design on disk. It is
/// deliberately not duplicated: two copies of a check are two things to keep in step, and a
/// defect they SHARE is exactly what a comparison between copies cannot see.</para>
/// </summary>
internal static class JacobianCheck
{
    /// <summary>
    /// Compares the analytic Jacobian with a central difference of the same residuals, column by
    /// column, naming the operand and the variable that disagreed rather than only reporting that
    /// something did.
    /// </summary>
    /// <param name="what">
    /// How to describe this design in a failure - the sweep passes the fixture's name, so a
    /// failure says which of thirty-odd designs it was.
    /// </param>
    /// <param name="relativeTolerance">
    /// How closely the two must agree, relative to the larger of them. The default suits a
    /// well-conditioned design. A few designs here are deliberately near-singular - a face at
    /// radius 1e10, a figured flat - and their VALUES are only good to a fraction of a per cent
    /// by this repository's own measurement (see FlatSurfaceInCollimatedSpaceTests, which allows
    /// half a per cent on the coefficients there). A derivative inherits its value's conditioning,
    /// so on those designs the comparison is held to the same standard the values are.
    /// </param>
    public static void Check(Design design, MeritFunction merit, VariableSet vars,
                             string what = "", double relativeTolerance = 2e-4)
    {
        string where = what.Length > 0 ? what + ": " : "";

        var x0 = design.Read();
        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, where + analytic.Failure);

        int m = merit.Operands.Count;
        int n = vars.Count;
        Assert.True(m > 0, where + "no operands");
        Assert.True(n > 0, where + "no variables");

        // Something has to be moving, or the comparison is between two zeros.
        double largest = 0.0;
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                largest = Math.Max(largest, Math.Abs(analytic.Jacobian[i, j]));
        Assert.True(largest > 0.0, where + "the analytic Jacobian is entirely zero");

        for (int j = 0; j < n; j++)
        {
            // The step has to leave the difference quotient inside its linear regime, and what
            // counts as small depends entirely on the variable. A curvature lives near 0.01, a
            // thickness near 10 and an r^4 aspheric coefficient near 1e-8; one step size cannot
            // serve all three, and stepping A4 by 1e-7 moves the merit by four per cent, which
            // measures a secant and not a derivative.
            //
            // Sizing the step so that the RESIDUALS move by about a part in a million puts every
            // variable in the same regime whatever its units. Taking that size from the analytic
            // column does not bias the comparison: it sets how far to probe, not what to expect
            // there, and an analytic derivative wrong by any factor would still be caught.
            double column = 0.0;
            for (int i = 0; i < m; i++)
                column = Math.Max(column, Math.Abs(analytic.Jacobian[i, j]));

            double magnitude = Math.Max(Math.Abs(x0[j]), 1.0);
            double h = column > 0.0 ? 1e-6 / column : 1e-6;
            h = Math.Min(h, 1e-4 * magnitude);
            h = Math.Max(h, 1e-13 * magnitude);

            var first = CentralDifference(design, merit, x0, j, h, where);

            for (int i = 0; i < m; i++)
            {
                double exact = analytic.Jacobian[i, j];

                // A central difference of a quantity of size s carries an error of order
                // s * eps^(2/3); the tolerance has to be relative to the column, not to the
                // entry, or an entry that is legitimately near zero is held to an absolute
                // standard nothing could meet.
                double scale = Math.Max(Math.Abs(exact), Math.Abs(first[i]));
                double tolerance = relativeTolerance * scale + 1e-7 * largest;
                if (Math.Abs(exact - first[i]) <= tolerance) continue;

                // THE STEP IS A GUESS AND CAN BE A BAD ONE, so a disagreement is not yet a
                // finding. One size is chosen for a whole COLUMN, from the largest derivative in
                // it, which can leave it far too small for the other operands - and a step too
                // small is not merely imprecise. On a figured FLAT the value code switches
                // routes within 1E-13 of zero curvature, where the ordinary chain divides by a
                // vanishing incidence, so a difference quotient taken across that neighbourhood
                // measures the switch rather than a slope. That is how this check first read a
                // correct derivative of -0.9695 as 2.3E+14.
                //
                // So a failing entry is re-measured over a LADDER of steps spanning six decades
                // and accepted if ANY rung agrees. A genuinely wrong derivative agrees with no
                // rung, and the failure prints the whole ladder so the reader can see whether
                // the quotient was converging on something else or simply thrashing.
                double best = first[i];
                double bestGap = Math.Abs(exact - first[i]);
                var ladder = new List<(double H, double Value)> { (h, first[i]) };

                foreach (double factor in new[] { 10.0, 100.0, 1000.0, 1e4, 1e5, 1e6 })
                {
                    double hh = h * factor;
                    if (hh > 1e-3 * magnitude) break;
                    var rung = CentralDifference(design, merit, x0, j, hh, where);
                    ladder.Add((hh, rung[i]));

                    double gap = Math.Abs(exact - rung[i]);
                    if (gap < bestGap) { bestGap = gap; best = rung[i]; }
                    if (gap <= relativeTolerance * Math.Max(Math.Abs(exact), Math.Abs(rung[i])) + 1e-7 * largest)
                        break;
                }

                double finalTolerance = relativeTolerance * Math.Max(Math.Abs(exact), Math.Abs(best))
                                      + 1e-7 * largest;
                Assert.True(bestGap <= finalTolerance,
                    $"{where}d({merit.Operands[i].Label})/d({vars[j].Name}): analytic " +
                    $"{exact:G10}, and no step reproduces it. The ladder: " +
                    string.Join(", ", ladder.Select(r => $"h={r.H:E1} -> {r.Value:G10}")));
            }
        }
    }
    /// <summary>
    /// One central difference of every residual, at one step size. The design is always put
    /// back where it started, so the caller can take as many of these as it likes.
    /// </summary>
    private static double[] CentralDifference(Design design, MeritFunction merit, double[] x0,
                                              int j, double h, string where)
    {
        var plus = (double[])x0.Clone(); plus[j] += h;
        design.Apply(plus);
        var rp = merit.Evaluate(false);

        var minus = (double[])x0.Clone(); minus[j] -= h;
        design.Apply(minus);
        var rm = merit.Evaluate(false);

        design.Apply((double[])x0.Clone());

        Assert.True(rp.Ok && rm.Ok,
            where + "the design could not be evaluated beside the start point");

        var d = new double[merit.Operands.Count];
        for (int i = 0; i < d.Length; i++)
            d[i] = (rp.Residuals[i] - rm.Residuals[i]) / (2.0 * h);
        return d;
    }
}
