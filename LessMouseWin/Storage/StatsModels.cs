namespace LessMouseWin.Storage;

/// <summary>
/// stats.json, schema v1 — same human-readable shape as the macOS original,
/// with Windows process names instead of macOS bundle ids.
/// </summary>
public sealed class StatsRoot
{
    public int Version { get; set; } = 1;
    public Dictionary<string, DayStats> Days { get; set; } = new();
}

public sealed class DayStats
{
    public Dictionary<string, AppStats> Apps { get; set; } = new();
}

public sealed class AppStats
{
    public Dictionary<string, int> Combos { get; set; } = new();
    public Dictionary<string, int> Patterns { get; set; } = new();
    public int Activations { get; set; }
}

public sealed class DaySnapshot
{
    public string DayKey { get; }
    public Dictionary<string, int> Combos { get; }
    public Dictionary<string, int> Patterns { get; }

    public DaySnapshot(string dayKey, Dictionary<string, int>? combos = null, Dictionary<string, int>? patterns = null)
    {
        DayKey = dayKey;
        Combos = combos ?? new Dictionary<string, int>();
        Patterns = patterns ?? new Dictionary<string, int>();
    }

    public int TotalEvents => Combos.Values.Sum();
    public int TotalPatterns => Patterns.Values.Sum();
}
