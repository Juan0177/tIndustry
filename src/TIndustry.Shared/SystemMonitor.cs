using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TIndustry.Shared;

/// <summary>
/// Best-effort CPU / RAM / GPU sampling for the Impostazioni "risorse sistema" overlay.
/// Linux prefers /proc; GPU via nvidia-smi when present (otherwise omitted).
/// Shared so Godot and Raylib can both show session stats.
/// </summary>
public static class SystemMonitor
{
    private static readonly Process Current = Process.GetCurrentProcess();
    private static long lastIdle;
    private static long lastTotal;
    private static TimeSpan lastProcessorTime = TimeSpan.Zero;
    private static DateTime lastSampleUtc = DateTime.UtcNow;
    private static double cpuPercent;
    private static long ramUsedBytes;
    private static long ramTotalBytes;
    private static string? gpuLabel;
    private static float cpuRamAccum;
    private static float gpuAccum;
    private static bool gpuProbeDone;

    public static double CpuPercent => cpuPercent;
    public static long RamUsedBytes => ramUsedBytes;
    public static long RamTotalBytes => ramTotalBytes;
    public static string? GpuLabel => gpuLabel;

    public static void Update(float deltaSeconds)
    {
        cpuRamAccum += deltaSeconds;
        gpuAccum += deltaSeconds;
        if (cpuRamAccum >= 0.5f)
        {
            cpuRamAccum = 0f;
            SampleCpu();
            SampleRam();
        }

        if (gpuAccum >= 2f)
        {
            gpuAccum = 0f;
            SampleGpu();
        }
    }

    public static string FormatRam()
    {
        if (ramTotalBytes <= 0)
        {
            return $"{FormatBytes(ramUsedBytes)} (proc)";
        }

        return $"{FormatBytes(ramUsedBytes)} / {FormatBytes(ramTotalBytes)}";
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{bytes} B";
        }

        return value.ToString("0.#", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    private static void SampleCpu()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && TrySampleProcStat())
        {
            return;
        }

        try
        {
            Current.Refresh();
            var now = DateTime.UtcNow;
            var cpu = Current.TotalProcessorTime;
            var wall = now - lastSampleUtc;
            if (wall.TotalMilliseconds > 50 && lastProcessorTime != TimeSpan.Zero)
            {
                var cpuDelta = (cpu - lastProcessorTime).TotalMilliseconds;
                var raw = cpuDelta / (wall.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount)) * 100.0;
                cpuPercent = Math.Clamp(raw, 0, 100);
            }

            lastProcessorTime = cpu;
            lastSampleUtc = now;
        }
        catch
        {
            // keep last sample
        }
    }

    private static bool TrySampleProcStat()
    {
        try
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault();
            if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return false;
            }

            long user = ParseLong(parts[1]);
            long nice = ParseLong(parts[2]);
            long system = ParseLong(parts[3]);
            long idle = ParseLong(parts[4]);
            long iowait = parts.Length > 5 ? ParseLong(parts[5]) : 0;
            long irq = parts.Length > 6 ? ParseLong(parts[6]) : 0;
            long softirq = parts.Length > 7 ? ParseLong(parts[7]) : 0;
            var idleAll = idle + iowait;
            var total = user + nice + system + idleAll + irq + softirq;
            if (lastTotal > 0)
            {
                var totalDelta = total - lastTotal;
                var idleDelta = idleAll - lastIdle;
                if (totalDelta > 0)
                {
                    cpuPercent = Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0, 100);
                }
            }

            lastIdle = idleAll;
            lastTotal = total;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static void SampleRam()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && TryReadMemInfo(out var total, out var available))
            {
                ramTotalBytes = total;
                ramUsedBytes = Math.Max(0, total - available);
                return;
            }

            Current.Refresh();
            ramUsedBytes = Current.WorkingSet64;
            ramTotalBytes = 0;
        }
        catch
        {
            // keep last sample
        }
    }

    private static bool TryReadMemInfo(out long totalBytes, out long availableBytes)
    {
        totalBytes = 0;
        availableBytes = 0;
        try
        {
            long totalKb = 0;
            long availKb = 0;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    totalKb = ParseMemKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    availKb = ParseMemKb(line);
                }
            }

            if (totalKb <= 0)
            {
                return false;
            }

            totalBytes = totalKb * 1024;
            availableBytes = availKb * 1024;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseMemKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb)
            ? kb
            : 0;
    }

    private static void SampleGpu()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                gpuLabel = gpuProbeDone ? gpuLabel : null;
                gpuProbeDone = true;
                return;
            }

            if (!proc.WaitForExit(500))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                gpuLabel = null;
                gpuProbeDone = true;
                return;
            }

            var output = proc.StandardOutput.ReadToEnd().Trim();
            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                gpuLabel = null;
                gpuProbeDone = true;
                return;
            }

            var first = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
            var cols = first.Split(',', StringSplitOptions.TrimEntries);
            if (cols.Length < 3
                || !int.TryParse(cols[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var util))
            {
                gpuLabel = null;
                gpuProbeDone = true;
                return;
            }

            _ = int.TryParse(cols[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memUsed);
            _ = int.TryParse(cols[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memTotal);
            gpuLabel = memTotal > 0
                ? $"GPU {util}% · VRAM {memUsed}/{memTotal} MB"
                : $"GPU {util}%";
            gpuProbeDone = true;
        }
        catch
        {
            gpuLabel = null;
            gpuProbeDone = true;
        }
    }
}
