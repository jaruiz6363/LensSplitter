using System;

using Python.Runtime;

namespace AberrationCalculator.Optiland;

/// <summary>
/// The Python.NET engine, started once per process and left running.
///
/// <para>Python cannot be restarted inside a process once shut down, so this never shuts it
/// down: the interpreter lives as long as the program does. The lifecycle - setting
/// <c>Runtime.PythonDLL</c> and <c>PythonEngine.PythonHome</c> before
/// <c>PythonEngine.Initialize</c>, then releasing the GIL so other threads can take it.</para>
/// </summary>
public static class PythonSession
{
    private static readonly object Gate = new();
    private static bool _started;

    public static bool IsStarted { get { lock (Gate) return _started; } }

    /// <summary>Starts the interpreter if it is not already running.</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (_started) return;
            if (!PythonEnvironment.IsReady) throw new InvalidOperationException(PythonEnvironment.SetupHint);

            Runtime.PythonDLL = PythonEnvironment.PythonDll;
            PythonEngine.PythonHome = PythonEnvironment.Home;
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            _started = true;
        }
    }

    /// <summary>Runs <paramref name="func"/> holding the GIL.</summary>
    public static T WithGil<T>(Func<T> func)
    {
        Start();
        using (Py.GIL()) return func();
    }

    /// <summary>Runs <paramref name="action"/> holding the GIL.</summary>
    public static void WithGil(Action action)
    {
        Start();
        using (Py.GIL()) action();
    }

    /// <summary>
    /// The installed optiland's version, from the package metadata rather than
    /// <c>optiland.__version__</c>, which in 0.6.2 still says 0.6.1.
    /// </summary>
    public static string OptilandVersion =>
        WithGil(() => Py.Import("importlib.metadata").InvokeMethod("version", new PyString("optiland"))
                        .ToString() ?? "unknown");
}
