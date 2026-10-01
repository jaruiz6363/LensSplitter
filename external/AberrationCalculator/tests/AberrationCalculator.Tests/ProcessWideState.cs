using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The collection for tests that touch state belonging to the whole PROCESS rather than to
/// themselves: the console, and environment variables.
///
/// <para>xUnit runs test classes in parallel but never two tests of the same collection at once,
/// so naming this collection is how such a class says "one at a time, please". Everything here
/// either redirects <c>Console.Out</c> to read what a command printed, or sets a variable like
/// <c>ABCALC_DIR</c> - and both are single slots shared by the whole test run. Two classes doing
/// it at once means one of them reads the other's output, which shows up as a failure that
/// cannot be reproduced by running either class on its own.</para>
///
/// <para>The cost is that these classes no longer overlap each other. They are a small and fast
/// fraction of the suite, and a test that passes alone and fails in company is worth more than
/// the second it saves.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProcessWideState
{
    public const string Name = "process-wide state";
}
