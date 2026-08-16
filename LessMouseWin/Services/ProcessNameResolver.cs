using System.Collections.Concurrent;
using System.Diagnostics;

namespace LessMouseWin.Services;

/// <summary>
/// PID → process name cache. Resolving a PID on every hook callback would be
/// far too slow, but Windows reuses PIDs, so the cache must never serve a
/// stale name forever. The foreground tracker refreshes the cache on every
/// foreground event (even when the PID number hasn't changed); key events
/// then take the cheap cached path.
/// </summary>
public static class ProcessNameResolver
{
    private sealed record CacheEntry(string? Name, DateTime StartTime);

    private static readonly ConcurrentDictionary<uint, CacheEntry> Cache = new();

    public static string OwnProcessName { get; } = Process.GetCurrentProcess().ProcessName;

    public static string? Resolve(uint processId)
    {
        if (processId == 0) return null;
        if (Cache.TryGetValue(processId, out var cached) && cached.Name is not null)
            return cached.Name;
        return Refresh(processId);
    }

    /// <summary>
    /// Re-reads the process name and start time. Called on foreground
    /// changes — a frequency where a Process lookup is harmless — so the
    /// hot key-event path never has to pay for it.
    /// </summary>
    public static string? Refresh(uint processId)
    {
        CacheEntry? entry;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var startTime = process.StartTime;
            entry = new CacheEntry(process.ProcessName, startTime);
        }
        catch
        {
            entry = new CacheEntry(null, default);
        }
        Cache[processId] = entry;
        return entry.Name;
    }

    public static bool IsOwnProcess(uint processId) =>
        processId != 0 && Resolve(processId) is { } name &&
        string.Equals(name, OwnProcessName, StringComparison.OrdinalIgnoreCase);
}
