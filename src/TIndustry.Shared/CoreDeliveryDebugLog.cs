namespace TIndustry.Shared;

/// <summary>
/// TEMP debug instrumentation for Core belt delivery bug (v0.3.4).
/// Writes NDJSON to /tmp/cursor-debug-core-delivery/debug.log — remove after fix confirmed.
/// </summary>
public static class CoreDeliveryDebugLog
{
    private const string Dir = "/tmp/cursor-debug-core-delivery";
    private const string Path = Dir + "/debug.log";
    private static long _tick;
    private static long _lastSummaryTick = -1;
    private static int _edgeLeaveLogs;
    private static int _autoDropLogs;

    public static long Tick => _tick;

    public static long NextTick() => System.Threading.Interlocked.Increment(ref _tick);

    public static void Write(string hypothesisId, string location, string message, object data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = $"log_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}"[..32],
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tick = _tick,
                hypothesisId,
                location,
                message,
                data
            });
            File.AppendAllText(Path, payload + "\n");
        }
        catch
        {
            // Never break sim for debug I/O.
        }
    }

    public static bool ShouldLogSummary()
    {
        if (_tick - _lastSummaryTick < 30)
        {
            return false;
        }

        _lastSummaryTick = _tick;
        return true;
    }

    public static bool ShouldLogEdgeLeave()
    {
        if (_edgeLeaveLogs >= 80)
        {
            return false;
        }

        _edgeLeaveLogs++;
        return true;
    }

    public static bool ShouldLogAutoDrop()
    {
        if (_autoDropLogs >= 40)
        {
            return false;
        }

        _autoDropLogs++;
        return true;
    }
}
