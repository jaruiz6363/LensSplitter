using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>
/// How many PHYSICAL cores this machine has, as distinct from logical processors.
///
/// <para><b>Why the distinction decides the chain count.</b> A hopping chain is dense
/// floating-point work, and two SMT threads sharing one core contend for the same execution
/// units - so a second thread on a core buys a fraction of a core, not another one. On the
/// hybrid parts now common it is worse than that: the logical count includes efficiency cores,
/// which run the same chain markedly slower and hold up every hop they are given, because a hop
/// is only finished when its chain finishes.</para>
///
/// <para>The distinction is not academic: an i5-13400F has ten physical cores and sixteen
/// logical processors, so a guess of "half the logical count" would have said eight.</para>
///
/// <para>Detection always degrades to <see cref="Environment.ProcessorCount"/> rather than
/// failing - a wrong chain count is a performance question, and refusing to run over one would
/// be a far worse answer than running with a few too many.</para>
/// </summary>
public static class CpuInfo
{
    private static int _cached;

    /// <summary>Physical cores, or the logical count if the topology cannot be read. Cached.</summary>
    public static int PhysicalCoreCount()
    {
        if (_cached > 0) return _cached;

        int n;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) n = WindowsPhysicalCores();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) n = LinuxPhysicalCores();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) n = MacPhysicalCores();
            else n = 0;
        }
        catch { n = 0; }

        if (n <= 0) n = Environment.ProcessorCount;
        n = Math.Max(1, Math.Min(n, Environment.ProcessorCount));
        _cached = n;
        return n;
    }

    // ── Windows: count the RelationProcessorCore records. ────────────────────────────────
    private const int RelationProcessorCore = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemLogicalProcessorInformation
    {
        public UIntPtr ProcessorMask;
        public int Relationship;
        // A sixteen-byte union - ProcessorCore, NumaNode or Cache - that nothing here reads.
        public long Union0;
        public long Union1;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnLength);

    private static int WindowsPhysicalCores()
    {
        uint length = 0;
        GetLogicalProcessorInformation(IntPtr.Zero, ref length);   // ask how much room it needs
        if (length == 0) return 0;

        IntPtr buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformation(buffer, ref length)) return 0;

            int size = Marshal.SizeOf<SystemLogicalProcessorInformation>();
            int records = (int)(length / size);
            int cores = 0;

            for (int i = 0; i < records; i++)
            {
                var record = Marshal.PtrToStructure<SystemLogicalProcessorInformation>(
                    buffer + i * size);
                if (record.Relationship == RelationProcessorCore) cores++;
            }
            return cores;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    // ── Linux: distinct (physical id, core id) pairs in /proc/cpuinfo. ───────────────────
    private static int LinuxPhysicalCores()
    {
        var seen = new HashSet<string>();
        string physical = string.Empty, core = string.Empty;

        foreach (string line in File.ReadAllLines("/proc/cpuinfo"))
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                if (line.Trim().Length == 0 && (physical.Length > 0 || core.Length > 0))
                {
                    seen.Add(physical + "/" + core);
                    physical = core = string.Empty;
                }
                continue;
            }

            string key = line.Substring(0, colon).Trim();
            string value = line.Substring(colon + 1).Trim();
            if (key == "physical id") physical = value;
            else if (key == "core id") core = value;
        }

        if (physical.Length > 0 || core.Length > 0) seen.Add(physical + "/" + core);
        return seen.Count;                     // zero if the fields were absent; caller falls back
    }

    // ── macOS: sysctl hw.physicalcpu. ────────────────────────────────────────────────────
    private static int MacPhysicalCores()
    {
        var start = new System.Diagnostics.ProcessStartInfo("/usr/sbin/sysctl", "-n hw.physicalcpu")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = System.Diagnostics.Process.Start(start);
        if (process == null) return 0;

        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(2000);
        return int.TryParse(output, out int n) ? n : 0;
    }
}
