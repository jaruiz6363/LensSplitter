using System;
using System.IO;

namespace AberrationCalculator.Tests;

/// <summary>
/// Lens files the tests run against, which live in the repository rather than on the
/// machine that happened to write them.
///
/// <para>These were previously absolute paths into a working directory outside the repo, so
/// a clone could not run the suite - the tests that needed them silently skipped, and a
/// reader had no way to tell a passing run from a hollow one. They are now copied to the
/// build output, so they are found the same way on any machine.</para>
///
/// <para>Everything the suite reads is here. It used to reach outside for the coefficient
/// reference fixtures, into a separate private repository, which meant a fresh clone ran
/// seven fewer tests and said nothing about it - the count at the end still read as a pass.
/// Those fixtures are now in <c>fixtures/coefficient-reference</c> and the tests that read
/// them throw if they are absent rather than quietly yielding no cases.</para>
/// </summary>
public static class Fixtures
{
    /// <summary>The directory holding the lens files, next to the test assembly.</summary>
    public static string LensDir => Path.Combine(AppContext.BaseDirectory, "fixtures", "lenses");

    /// <summary>
    /// Full path to a fixture lens, by bare name. Throws rather than returning a missing
    /// path: a fixture that has gone astray should fail loudly, not turn into a skipped test.
    /// </summary>
    public static string Lens(string name)
    {
        string path = Path.Combine(LensDir, name.EndsWith(".lhlt", StringComparison.Ordinal)
                                             ? name : name + ".lhlt");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"fixture '{name}' is missing from {LensDir}. It should have been copied "
              + "from tests/fixtures by the build.", path);
        return path;
    }
}
