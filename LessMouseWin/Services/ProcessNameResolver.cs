using System.Collections.Concurrent;
using System.Diagnostics;

namespace LessMouseWin.Services;

/// <summary>
/// PID → process name cache. Resolving a PID on every hook callback would be
/// far too slow; process names change only at process exit/creation.
/// </summary>
public static class ProcessNameResolver
{
    private static readonly ConcurrentDictionary<uint, string?> Cache = new();

    public static string OwnProcessName { get; } = Process.GetCurrentProcess().ProcessName;

    public static string? Resolve(uint processId)
    {
        if (processId == 0) return null;
        if (Cache.TryGetValue(processId, out var cached)) return cached;

        string? name = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            name = process.ProcessName;
        }
        catch
        {
            name = null;
        }
        Cache[processId] = name;
        return name;
    }

    public static bool IsOwnProcess(uint processId) =>
        processId != 0 && Resolve(processId) is { } name &&
        string.Equals(name, OwnProcessName, StringComparison.OrdinalIgnoreCase);
}
