using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using LessMouseWin.Detection;
using LessMouseWin.Models;
using LessMouseWin.Services;
using LessMouseWin.Storage;
using LessMouseWin.Suggestions;

namespace LessMouseWin.State;

public enum TrackingPhase
{
    Tracking,
    Paused,
    HookFailed,
}

public sealed class ActivityDays
{
    public int Browser { get; set; }
    public int MultiApp { get; set; }
}

/// <summary>
/// Where the pipeline meets the UI. Keystrokes arrive from the hook thread,
/// are filtered, counted, pattern-matched and coached; everything the window
/// shows is published from here. The port intentionally keeps the macOS
/// AppState shape so behavior stays easy to compare with the original.
/// </summary>
public sealed class AppState : INotifyPropertyChanged, IDisposable
{
    public StatsStore Store { get; }
    public AppSettings Settings { get; }

    private readonly KeyboardMonitor _monitor;
    private readonly SecureInputTracker _secureInput = new();
    private readonly ForegroundTracker _foreground = new();
    private readonly PatternDetector _detector;
    private readonly SuggestionEngine _engine;
    private readonly DispatcherTimer _publishTimer;

    private TrackingPhase _phase = TrackingPhase.Paused;
    private bool _isTracking;
    private string? _hookError;
    private DaySnapshot _today = new("");
    private Dictionary<string, SuggestionState> _suggestionStates = [];
    private int _unreadCount;
    private string? _celebration;
    private int _todayAppSwitches;
    private readonly ActivityDays _activityDays = new();

    private bool _publishScheduled;
    private string _lastDayKey = "";
    private Dictionary<string, int> _todayComboCounts = [];
    private string? _lastEventApp;
    private bool _lastPaused;
    private string? _lastLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when page structure (not just counters) changes.</summary>
    public event Action? LayoutChanged;

    public AppState(StatsStore store, AppSettings settings, Dispatcher dispatcher, TimeSpan? publishDelay = null)
    {
        Store = store;
        Settings = settings;
        _detector = new PatternDetector(PatternLibrary.Defaults);
        _engine = new SuggestionEngine(RuleLibrary.All);
        _monitor = new KeyboardMonitor(dispatcher, raw =>
        {
            if (!dispatcher.HasShutdownStarted)
                dispatcher.BeginInvoke(DispatcherPriority.Background, () => IngestRaw(raw));
        });
        _publishTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = publishDelay ?? TimeSpan.FromSeconds(1),
        };
        _publishTimer.Tick += (_, _) =>
        {
            _publishTimer.Stop();
            _publishScheduled = false;
            RefreshToday();
        };

        _lastPaused = Settings.IsPaused;
        _lastLanguage = Settings.Language;
        Settings.Changed += OnSettingsChanged;
    }

    // MARK: lifecycle

    public void Start()
    {
        var snapshot = Store.TodaySnapshot();
        _lastDayKey = snapshot.DayKey;
        _todayComboCounts = new Dictionary<string, int>(snapshot.Combos);
        _suggestionStates = Store.LoadSuggestionStates();
        RefreshUnreadCount();
        RefreshToday();

        _secureInput.Start();
        _foreground.Start(NoteAppActivation);

        if (Settings.IsPaused)
        {
            _phase = TrackingPhase.Paused;
            _isTracking = false;
            NotifyChanged();
        }
        else
        {
            StartMonitor();
        }
    }

    public void StartMonitor()
    {
        var result = _monitor.Start();
        if (result.Status == HookStartStatus.Running)
        {
            _phase = TrackingPhase.Tracking;
            _isTracking = true;
            _hookError = null;
        }
        else
        {
            _phase = TrackingPhase.HookFailed;
            _isTracking = false;
            _hookError = result.Error ?? "unknown hook error";
        }
        NotifyChanged();
        NotifyLayout();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Settings.Language != _lastLanguage)
        {
            _lastLanguage = Settings.Language;
            Localization.Loc.Override = Settings.Language;
        }

        if (Settings.IsPaused != _lastPaused)
        {
            _lastPaused = Settings.IsPaused;
            if (Settings.IsPaused)
            {
                _monitor.Stop();
                _phase = TrackingPhase.Paused;
                _isTracking = false;
                _detector.ResetAll();
                Store.Flush();
            }
            else
            {
                StartMonitor();
            }
        }

        // Excluded-apps changes must not stitch bursts across the boundary.
        _detector.ResetAll();
        NotifyChanged();
        NotifyLayout();
    }

    // MARK: UI-facing state

    public TrackingPhase Phase => _phase;
    public bool IsTracking => _isTracking;
    public string? HookError => _hookError;
    public DaySnapshot Today => _today;
    public IReadOnlyDictionary<string, SuggestionState> SuggestionStates => _suggestionStates;
    public int UnreadCount => _unreadCount;
    public string? Celebration => _celebration;
    public int TodayAppSwitches => _todayAppSwitches;
    public ActivityDays Activity => _activityDays;

    public string TrackingHeadline => _phase switch
    {
        TrackingPhase.Tracking => Localization.Loc.T("tracking.active"),
        TrackingPhase.Paused => Localization.Loc.T("tracking.paused"),
        _ => Localization.Loc.T("tracking.failed"),
    };

    // MARK: behavior signals

    public void NoteAppActivation(string? processName)
    {
        if (processName is null) return;
        if (string.Equals(processName, ProcessNameResolver.OwnProcessName, StringComparison.OrdinalIgnoreCase)) return;
        Store.RecordAppActivation(processName);
        SchedulePublish();
    }

    private void ComputeActivityDays()
    {
        var browser = 0;
        var multiApp = 0;
        foreach (var day in Store.DaySummaries().Values)
        {
            var fronted = day.Apps
                .Where(pair => pair.Value.Activations > 0)
                .Select(pair => pair.Key)
                .ToList();
            if (fronted.Any(BrowserCatalog.IsBrowser)) browser++;
            if (fronted.Count(name => !string.Equals(name, ProcessNameResolver.OwnProcessName,
                    StringComparison.OrdinalIgnoreCase)) >= 2) multiApp++;
        }
        _activityDays.Browser = browser;
        _activityDays.MultiApp = multiApp;
    }

    // MARK: the pipeline

    public void IngestRaw(RawKeyEvent raw)
    {
        if (Settings.IsPaused || !_isTracking) return;

        // Secure-input guard: while a password field is focused, observe
        // nothing at all (macOS parity: IsSecureEventInputEnabled).
        if (_secureInput.ShouldBlock(raw.FocusWindow)) return;

        var app = ProcessNameResolver.Resolve(raw.ForegroundProcessId);
        if (Settings.IsExcluded(app)) return;

        IngestEvent(KeyEvent.FromRaw(raw, app), raw.FocusWindow);
    }

    /// <summary>Public test seam; production feeds IngestRaw.</summary>
    public void Ingest(KeyEvent keyEvent) => IngestEvent(keyEvent, IntPtr.Zero);

    private void IngestEvent(KeyEvent keyEvent, IntPtr focusWindow)
    {
        if (Settings.IsPaused || !_isTracking) return;
        if (focusWindow != IntPtr.Zero && _secureInput.ShouldBlock(focusWindow)) return;
        if (Settings.IsExcluded(keyEvent.Application)) return;

        var signature = KeySignatureFilter.Signature(keyEvent);
        if (signature is null) return;

        if (keyEvent.Application != _lastEventApp)
        {
            _detector.ResetAll();
            _lastEventApp = keyEvent.Application;
        }

        var storageKey = signature.StorageKey;
        Store.IncrementCombo(storageKey, keyEvent.Application);
        _todayComboCounts[storageKey] = _todayComboCounts.GetValueOrDefault(storageKey) + 1;

        // Adoption first — using the coached shortcut is worth reacting to
        // instantly.
        var adopted = _engine.OnComboObserved(storageKey, _todayComboCounts[storageKey], _suggestionStates);
        if (adopted is not null)
        {
            _celebration = adopted;
            RefreshUnreadCount();
            Store.SaveSuggestionStates(_suggestionStates);
            NotifyLayout();
        }

        // Then pattern detection on the same stroke.
        foreach (var hit in _detector.Feed(storageKey, keyEvent.Timestamp))
            Store.RecordPatternHit(hit.Id, keyEvent.Application);

        SchedulePublish();
    }


    // MARK: publishing

    private void SchedulePublish()
    {
        if (_publishScheduled) return;
        _publishScheduled = true;
        _publishTimer.Stop();
        _publishTimer.Start();
    }

    private void RefreshToday()
    {
        var snapshot = Store.TodaySnapshot();

        if (snapshot.DayKey != _lastDayKey)
        {
            _lastDayKey = snapshot.DayKey;
            Store.Prune();
            _todayComboCounts = new Dictionary<string, int>(snapshot.Combos);
            _detector.ResetAll();
        }

        _today = snapshot;
        var activationSummary = Store.TodayActivationSummary();
        _todayAppSwitches = activationSummary.Total;
        ComputeActivityDays();
        EvaluateSuggestions();
        Store.FlushIfDue();
        NotifyChanged();
    }

    private void EvaluateSuggestions()
    {
        var allTime = new Dictionary<string, int>();
        foreach (var rule in RuleLibrary.All)
        foreach (var signature in rule.WatchForAdoption)
            allTime[signature] = Store.ComboCount(signature, dayLimit: null);

        var context = new EngineContext
        {
            DayKey = _today.DayKey,
            PatternHitsToday = new Dictionary<string, int>(_today.Patterns),
            ComboCountsToday = new Dictionary<string, int>(_today.Combos),
            ComboCountsAllTime = allTime,
            AppSwitchesToday = _todayAppSwitches,
            BrowserActiveDays = _activityDays.Browser,
            MultiAppActiveDays = _activityDays.MultiApp,
        };

        var changes = _engine.Evaluate(context, _suggestionStates);
        if (changes.Count > 0)
        {
            RefreshUnreadCount();
            Store.SaveSuggestionStates(_suggestionStates);
            NotifyLayout();
        }
    }

    private void RefreshUnreadCount() =>
        _unreadCount = _suggestionStates.Values.Count(state => state.Status == SuggestionStatus.Unread);

    // MARK: user actions

    public void MarkRead(string ruleId)
    {
        _engine.MarkRead(ruleId, _suggestionStates);
        RefreshUnreadCount();
        Store.SaveSuggestionStates(_suggestionStates);
        NotifyChanged();
        NotifyLayout();
    }

    public void Dismiss(string ruleId)
    {
        _engine.Dismiss(ruleId, _suggestionStates);
        RefreshUnreadCount();
        Store.SaveSuggestionStates(_suggestionStates);
        NotifyChanged();
        NotifyLayout();
    }

    public void DismissCelebration()
    {
        if (_celebration is not null && _suggestionStates.TryGetValue(_celebration, out var state))
        {
            state.Celebrated = true;
            Store.SaveSuggestionStates(_suggestionStates);
        }
        _celebration = null;
        NotifyChanged();
        NotifyLayout();
    }

    public void EraseAllData()
    {
        Store.EraseAll();
        Store.Flush();
        _suggestionStates = [];
        _todayComboCounts = [];
        _detector.ResetAll();
        _celebration = null;
        RefreshToday();
        RefreshUnreadCount();
    }

    public void PopoverDidOpen() => RefreshToday();

    public void PopoverDidClose() => Store.Flush();

    public void Shutdown()
    {
        _publishTimer.Stop();
        _monitor.Stop();
        _foreground.Dispose();
        _secureInput.Dispose();
        Store.Flush();
    }


    public void Dispose() => Shutdown();

    private void NotifyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));

    private void NotifyLayout() => LayoutChanged?.Invoke();
}
