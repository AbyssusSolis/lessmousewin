namespace LessMouseWin.Suggestions;

public enum RuleTriggerKind
{
    PatternBursts,
    ComboUsage,
    UnusedWhileActive,
    ActivityShare,
}

public enum ActivityKind
{
    BrowserUse,
    MultiAppUse,
    AppSwitching,
}

public sealed class RuleTrigger
{
    public RuleTriggerKind Kind { get; }
    public string? PatternId { get; }
    public int DailyMinimum { get; }
    public IReadOnlyList<string> Signatures { get; }
    public string? Signature { get; }
    public ActivityKind Activity { get; }
    public double MaxShare { get; }

    private RuleTrigger(RuleTriggerKind kind, string? patternId, int dailyMinimum,
        IReadOnlyList<string>? signatures, string? signature, ActivityKind activity, double maxShare)
    {
        Kind = kind;
        PatternId = patternId;
        DailyMinimum = dailyMinimum;
        Signatures = signatures ?? [];
        Signature = signature;
        Activity = activity;
        MaxShare = maxShare;
    }

    public static RuleTrigger PatternBursts(string id, int dailyMinimum) =>
        new(RuleTriggerKind.PatternBursts, id, dailyMinimum, null, null, default, 0);

    public static RuleTrigger ComboUsage(IEnumerable<string> signatures, int dailyMinimum) =>
        new(RuleTriggerKind.ComboUsage, null, dailyMinimum, signatures.ToArray(), null, default, 0);

    public static RuleTrigger UnusedWhileActive(string signature, ActivityKind activity, int minimumDays) =>
        new(RuleTriggerKind.UnusedWhileActive, null, minimumDays, null, signature, activity, 0);

    public static RuleTrigger ActivityShare(string signature, ActivityKind activity, int dailyMinimum, double maxShare) =>
        new(RuleTriggerKind.ActivityShare, null, dailyMinimum, null, signature, activity, maxShare);
}

public enum KeyCapKind { Key, Modifier }

public sealed record KeyCap(string Label, KeyCapKind Kind = KeyCapKind.Key)
{
    public static KeyCap Modifier(string label) => new(label, KeyCapKind.Modifier);
}

public sealed class SuggestionRule
{
    public string Id { get; }
    public RuleTrigger Trigger { get; }
    public IReadOnlyList<string> WatchForAdoption { get; }
    public string TitleKey { get; }
    public string BodyKey { get; }
    public string SummaryKey { get; }
    public IReadOnlyList<IReadOnlyList<KeyCap>> KeyCaps { get; }
    public string Symbol { get; }
    public int CooldownDays { get; }

    public SuggestionRule(string id, RuleTrigger trigger, string[] watchForAdoption,
        string titleKey, string bodyKey, string summaryKey,
        KeyCap[][] keyCaps, string symbol, int cooldownDays)
    {
        Id = id;
        Trigger = trigger;
        WatchForAdoption = watchForAdoption;
        TitleKey = titleKey;
        BodyKey = bodyKey;
        SummaryKey = summaryKey;
        KeyCaps = keyCaps;
        Symbol = symbol;
        CooldownDays = cooldownDays;
    }

    public string PrimaryShortcutLabel =>
        KeyCaps.Count > 0 ? string.Join("+", KeyCaps[0].Select(cap => cap.Label)) : "";
}

public enum SuggestionStatus
{
    Dormant,
    Unread,
    Read,
    Adopted,
    Dismissed,
}

public sealed class SuggestionState
{
    public string RuleId { get; set; } = "";
    public SuggestionStatus Status { get; set; } = SuggestionStatus.Dormant;
    public DateTime? GeneratedAt { get; set; }
    public Dictionary<string, int> AdoptionBaseline { get; set; } = new();
    public string? LastNotifiedDayKey { get; set; }
    public bool Celebrated { get; set; }

    public static SuggestionState Create(string ruleId) => new() { RuleId = ruleId };
}

public enum SuggestionChangeKind
{
    BecameUnread,
    PromotedAgain,
    Adopted,
}

public sealed record SuggestionChange(SuggestionChangeKind Kind, string RuleId);

public sealed class EngineContext
{
    public string DayKey { get; init; } = "";
    public Dictionary<string, int> PatternHitsToday { get; init; } = new();
    public Dictionary<string, int> ComboCountsToday { get; init; } = new();
    public Dictionary<string, int> ComboCountsAllTime { get; init; } = new();
    public int AppSwitchesToday { get; init; }
    public int BrowserActiveDays { get; init; }
    public int MultiAppActiveDays { get; init; }
}
