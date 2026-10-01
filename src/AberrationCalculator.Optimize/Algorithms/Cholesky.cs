using System;

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>
/// Solves the normal equations by Cholesky factorisation.
///
/// <para>The matrix a least-squares step has to invert is <c>J'J + D</c> with <c>D</c> a
/// positive diagonal, which is symmetric and positive definite by construction - so half the
/// arithmetic of a general solve is wasted work, and the factorisation itself is the test of
/// whether the damping is sufficient. When it fails, the matrix is not positive definite after
/// all, which is precisely the signal to add more damping and try again rather than to press on
/// with a step in a direction that is not downhill.</para>
/// </summary>
public static class Cholesky
{
    /// <summary>
    /// Solves <c>a x = b</c> for a symmetric positive definite <paramref name="a"/>.
    /// Returns false, having changed nothing, when the matrix is not positive definite.
    /// </summary>
    /// <param name="a">
    /// The matrix, which is overwritten with its factor. Only the lower triangle is read.
    /// </param>
    public static bool Solve(double[,] a, double[] b, double[] x)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (x == null) throw new ArgumentNullException(nameof(x));

        int n = b.Length;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = a[i, j];
                for (int k = 0; k < j; k++) sum -= a[i, k] * a[j, k];

                if (i == j)
                {
                    if (sum <= 0.0 || double.IsNaN(sum)) return false;
                    a[i, i] = Math.Sqrt(sum);
                }
                else
                {
                    a[i, j] = sum / a[j, j];
                }
            }
        }

        // Forward substitution, then back.
        for (int i = 0; i < n; i++)
        {
            double sum = b[i];
            for (int k = 0; k < i; k++) sum -= a[i, k] * x[k];
            x[i] = sum / a[i, i];
        }
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = x[i];
            for (int k = i + 1; k < n; k++) sum -= a[k, i] * x[k];
            x[i] = sum / a[i, i];
        }
        return true;
    }
}
