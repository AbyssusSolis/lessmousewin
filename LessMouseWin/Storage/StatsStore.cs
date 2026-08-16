using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LessMouseWin.Suggestions;

namespace LessMouseWin.Storage;

/// <summary>
/// Local aggregate-count store. One JSON file, written atomically, readable by
/// opening it — the Windows mirror of the macOS StatsStore. All calls are
/// thread-safe via a single lock; writes are O(1) dictionary bumps and
/// flushing is debounced to once per 30 seconds.
/// </summary>
public sealed class StatsStore
{
    private readonly object _lock = new();
    private readonly string _directory;
    private readonly Func<DateTime> _now;

    private StatsRoot _root = new();
    private Dictionary<string, SuggestionState> _suggestionStates = new();
    private bool _dirty;
    private DateTime? _lastFlush;

    private const int RetentionDays = 60;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    public StatsStore(string directory, Func<DateTime>? now = null)
    {
        _directory = directory;
        _now = now ?? (() => DateTime.Now);
        Directory.CreateDirectory(_directory);
        Load();
    }

    public string StoragePath => Path.Combine(_directory, "stats.json");
    public string SuggestionsPath => Path.Combine(_directory, "suggestions.json");

    // MARK: loading / recovery

    private void Load()
    {
        if (File.Exists(StoragePath))
        {
            try
            {
                var json = File.ReadAllText(StoragePath);
                _root = JsonSerializer.Deserialize<StatsRoot>(json, JsonOptions) ?? new StatsRoot();
                _root.Days ??= new Dictionary<string, DayStats>();
                foreach (var day in _root.Days.Values)
                {
                    day.Apps ??= new Dictionary<string, AppStats>();
                    foreach (var app in day.Apps.Values)
                    {
                        app.Combos ??= new Dictionary<string, int>();
                        app.Patterns ??= new Dictionary<string, int>();
                    }
                }
            }
            catch
            {
                Archive(StoragePath, "stats.corrupt");
                _root = new StatsRoot();
            }
        }

        if (File.Exists(SuggestionsPath))
        {
            try
            {
                var json = File.ReadAllText(SuggestionsPath);
                _suggestionStates = JsonSerializer.Deserialize<Dictionary<string, SuggestionState>>(json, JsonOptions)
                                    ?? new Dictionary<string, SuggestionState>();
                foreach (var state in _suggestionStates.Values)
                    state.AdoptionBaseline ??= new Dictionary<string, int>();
            }
            catch
            {
                Archive(SuggestionsPath, "suggestions.corrupt");
                _suggestionStates = new Dictionary<string, SuggestionState>();
            }
        }
    }

    private bool Archive(string path, string prefix)
    {
        try
        {
            if (!File.Exists(path)) return true;
            var stamp = new DateTimeOffset(_now()).ToUnixTimeSeconds();
            var target = Path.Combine(_directory, $"{prefix}-{stamp}.json");
            if (File.Exists(target))
                target = Path.Combine(_directory, $"{prefix}-{stamp}-{Guid.NewGuid():N}.json");
            // Copy-then-delete keeps the original until an archive actually
            // exists. If either step fails, callers can abort destructive
            // operations instead of silently overwriting the only copy.
            File.Copy(path, target, overwrite: false);
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // MARK: writes

    public void IncrementCombo(string signature, string? app)
    {
        var key = app ?? "";
        var dayKey = DayKey(_now());
        lock (_lock)
        {
            var appStats = EnsureApp(EnsureDay(dayKey), key);
            appStats.Combos[signature] = appStats.Combos.GetValueOrDefault(signature) + 1;
            _dirty = true;
        }
    }

    public void RecordPatternHit(string patternId, string? app)
    {
        var key = app ?? "";
        var dayKey = DayKey(_now());
        lock (_lock)
        {
            var appStats = EnsureApp(EnsureDay(dayKey), key);
            appStats.Patterns[patternId] = appStats.Patterns.GetValueOrDefault(patternId) + 1;
            _dirty = true;
        }
    }

    public void RecordAppActivation(string? app)
    {
        var key = app ?? "";
        var dayKey = DayKey(_now());
        lock (_lock)
        {
            EnsureApp(EnsureDay(dayKey), key).Activations += 1;
            _dirty = true;
        }
    }

    private DayStats EnsureDay(string dayKey)
    {
        if (!_root.Days.TryGetValue(dayKey, out var day))
        {
            day = new DayStats();
            _root.Days[dayKey] = day;
        }
        return day;
    }

    private static AppStats EnsureApp(DayStats day, string key)
    {
        if (!day.Apps.TryGetValue(key, out var app))
        {
            app = new AppStats();
            day.Apps[key] = app;
        }
        return app;
    }

    // MARK: reads

    public DaySnapshot TodaySnapshot() => Snapshot(DayKey(_now()));

    public DaySnapshot Snapshot(string dayKey)
    {
        lock (_lock)
        {
            if (!_root.Days.TryGetValue(dayKey, out var day))
                return new DaySnapshot(dayKey);

            var combos = new Dictionary<string, int>();
            var patterns = new Dictionary<string, int>();
            foreach (var app in day.Apps.Values)
            {
                foreach (var pair in app.Combos)
                    combos[pair.Key] = combos.GetValueOrDefault(pair.Key) + pair.Value;
                foreach (var pair in app.Patterns)
                    patterns[pair.Key] = patterns.GetValueOrDefault(pair.Key) + pair.Value;
            }
            return new DaySnapshot(dayKey, combos, patterns);
        }
    }

    public List<KeyValuePair<string, int>> TopCombos(int? dayLimit, int limit)
    {
        var totals = ComboTotals(dayLimit);
        return totals
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public int ComboCount(string signature, int? dayLimit = null) =>
        ComboTotals(dayLimit).GetValueOrDefault(signature);

    private Dictionary<string, int> ComboTotals(int? dayLimit)
    {
        lock (_lock)
        {
            string? cutoff = null;
            if (dayLimit is not null)
            {
                var cutoffDate = _now().Date.AddDays(-(dayLimit.Value - 1));
                cutoff = DayKey(cutoffDate);
            }

            var totals = new Dictionary<string, int>();
            foreach (var pair in _root.Days)
            {
                if (cutoff is not null && string.CompareOrdinal(pair.Key, cutoff) < 0) continue;
                foreach (var app in pair.Value.Apps.Values)
                foreach (var combo in app.Combos)
                    totals[combo.Key] = totals.GetValueOrDefault(combo.Key) + combo.Value;
            }
            return totals;
        }
    }

    public int DaysObserved()
    {
        lock (_lock)
        {
            return _root.Days.Values.Count(day =>
                day.Apps.Values.Any(app => app.Combos.Count > 0 || app.Patterns.Count > 0));
        }
    }

    public IReadOnlyDictionary<string, DayStats> DaySummaries()
    {
        lock (_lock)
        {
            return new Dictionary<string, DayStats>(_root.Days);
        }
    }

    public (int Total, int DistinctApps) TodayActivationSummary()
    {
        var dayKey = DayKey(_now());
        lock (_lock)
        {
            if (!_root.Days.TryGetValue(dayKey, out var day))
                return (0, 0);
            var total = day.Apps.Values.Sum(app => app.Activations);
            var distinct = day.Apps.Values.Count(app => app.Activations > 0);
            return (total, distinct);
        }
    }

    // MARK: housekeeping

    public void Prune()
    {
        lock (_lock)
        {
            var cutoff = DayKey(_now().Date.AddDays(-RetentionDays));
            var before = _root.Days.Count;
            _root.Days = _root.Days
                .Where(pair => string.CompareOrdinal(pair.Key, cutoff) >= 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            if (_root.Days.Count != before) _dirty = true;
        }
    }

    public void FlushIfDue()
    {
        bool due;
        lock (_lock)
        {
            if (!_dirty)
            {
                due = false;
            }
            else
            {
                due = _lastFlush is null || _now() - _lastFlush.Value >= FlushInterval;
            }
        }
        if (due) Flush();
    }

    /// <summary>Unconditional, atomic write; a crash mid-write cannot leave a half file.</summary>
    public void Flush()
    {
        string? json;
        lock (_lock)
        {
            _lastFlush = _now();
            if (!_dirty) return;
            json = JsonSerializer.Serialize(_root, JsonOptions);
        }

        if (json is null) return;
        try
        {
            AtomicWrite(StoragePath, json);
            lock (_lock) { _dirty = false; }
        }
        catch
        {
            // Losing today's counts to a full disk is bad; losing the app is
            // worse. The dirty flag stays set and the next tick tries again.
        }
    }

    public bool EraseAll()
    {
        lock (_lock)
        {
            var statsArchived = Archive(StoragePath, "stats.erased");
            var suggestionsArchived = Archive(SuggestionsPath, "suggestions.erased");
            if (!statsArchived || !suggestionsArchived) return false;

            _root = new StatsRoot();
            _suggestionStates = new Dictionary<string, SuggestionState>();
            _dirty = false;
            _lastFlush = null;
            return true;
        }
    }

    // MARK: suggestion states

    public Dictionary<string, SuggestionState> LoadSuggestionStates()
    {
        lock (_lock)
        {
            return new Dictionary<string, SuggestionState>(_suggestionStates);
        }
    }

    public void SaveSuggestionStates(IReadOnlyDictionary<string, SuggestionState> states)
    {
        string json;
        lock (_lock)
        {
            _suggestionStates = new Dictionary<string, SuggestionState>(states);
            json = JsonSerializer.Serialize(_suggestionStates, JsonOptions);
        }

        try
        {
            AtomicWrite(SuggestionsPath, json);
        }
        catch
        {
            // Suggestion states are small and written eagerly; a transient
            // write failure is retried on the next state change.
        }
    }

    private static void AtomicWrite(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }

    public static string DayKey(DateTime date) =>
        $"{date.Year:0000}-{date.Month:00}-{date.Day:00}";
}
