using System.Globalization;

namespace LessMouseWin.Suggestions;

/// <summary>
/// Turns an EngineContext plus persisted states into card transitions.
/// Dismissed is forever; adopted is forever; read cards respect a cooldown.
/// </summary>
public sealed class SuggestionEngine
{
    private readonly IReadOnlyList<SuggestionRule> _rules;
    private readonly Func<DateTime> _now;

    public SuggestionEngine(IEnumerable<SuggestionRule> rules, Func<DateTime>? now = null)
    {
        _rules = rules.ToArray();
        _now = now ?? (() => DateTime.Now);
    }

    public List<SuggestionChange> Evaluate(EngineContext context, Dictionary<string, SuggestionState> states)
    {
        var changes = new List<SuggestionChange>();
        foreach (var rule in _rules)
        {
            if (!IsTriggered(rule, context)) continue;

            var status = states.TryGetValue(rule.Id, out var existing) ? existing.Status : SuggestionStatus.Dormant;
            switch (status)
            {
                case SuggestionStatus.Dismissed:
                case SuggestionStatus.Adopted:
                    continue;

                case SuggestionStatus.Dormant:
                    states[rule.Id] = new SuggestionState
                    {
                        RuleId = rule.Id,
                        Status = SuggestionStatus.Unread,
                        GeneratedAt = _now(),
                        AdoptionBaseline = Baseline(rule, context),
                        LastNotifiedDayKey = context.DayKey,
                    };
                    changes.Add(new SuggestionChange(SuggestionChangeKind.BecameUnread, rule.Id));
                    break;

                case SuggestionStatus.Unread:
                    if (states.TryGetValue(rule.Id, out var unread))
                    {
                        unread.LastNotifiedDayKey = context.DayKey;
                    }
                    break;

                case SuggestionStatus.Read:
                    if (states.TryGetValue(rule.Id, out var read) &&
                        ShouldRenag(read, context.DayKey, rule.CooldownDays))
                    {
                        read.Status = SuggestionStatus.Unread;
                        read.LastNotifiedDayKey = context.DayKey;
                        changes.Add(new SuggestionChange(SuggestionChangeKind.PromotedAgain, rule.Id));
                    }
                    break;
            }
        }
        return changes;
    }

    private bool IsTriggered(SuggestionRule rule, EngineContext context)
    {
        switch (rule.Trigger.Kind)
        {
            case RuleTriggerKind.PatternBursts:
                return (context.PatternHitsToday.GetValueOrDefault(rule.Trigger.PatternId ?? "")) >= rule.Trigger.DailyMinimum;

            case RuleTriggerKind.ComboUsage:
            {
                var total = rule.Trigger.Signatures.Sum(signature => context.ComboCountsToday.GetValueOrDefault(signature));
                return total >= rule.Trigger.DailyMinimum;
            }

            case RuleTriggerKind.UnusedWhileActive:
            {
                var signature = rule.Trigger.Signature ?? "";
                if (context.ComboCountsAllTime.GetValueOrDefault(signature) != 0) return false;
                return rule.Trigger.Activity switch
                {
                    ActivityKind.BrowserUse => context.BrowserActiveDays >= rule.Trigger.DailyMinimum,
                    ActivityKind.MultiAppUse => context.MultiAppActiveDays >= rule.Trigger.DailyMinimum,
                    _ => false,
                };
            }

            case RuleTriggerKind.ActivityShare:
            {
                if (rule.Trigger.Activity != ActivityKind.AppSwitching) return false;
                var volume = context.AppSwitchesToday;
                if (volume < rule.Trigger.DailyMinimum) return false;
                var viaShortcut = context.ComboCountsToday.GetValueOrDefault(rule.Trigger.Signature ?? "");
                return viaShortcut < rule.Trigger.MaxShare * volume;
            }

            default:
                return false;
        }
    }

    private static Dictionary<string, int> Baseline(SuggestionRule rule, EngineContext context)
    {
        var baseline = new Dictionary<string, int>();
        foreach (var signature in rule.WatchForAdoption)
            baseline[signature] = context.ComboCountsToday.GetValueOrDefault(signature);
        return baseline;
    }

    private static bool ShouldRenag(SuggestionState state, string dayKey, int cooldownDays)
    {
        if (state.LastNotifiedDayKey is null) return true;
        return DaysBetween(state.LastNotifiedDayKey, dayKey) >= cooldownDays;
    }

    private static int DaysBetween(string from, string to)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var toDate))
            return int.MaxValue;
        return toDate.DayNumber - fromDate.DayNumber;
    }

    /// <summary>
    /// Event path: the pipeline just counted <paramref name="signature"/>.
    /// If that crosses a live card's baseline, the card is adopted.
    /// </summary>
    public string? OnComboObserved(string signature, int todayCount, Dictionary<string, SuggestionState> states)
    {
        foreach (var rule in _rules)
        {
            if (!rule.WatchForAdoption.Contains(signature)) continue;
            if (!states.TryGetValue(rule.Id, out var state)) continue;
            if (state.Status is not (SuggestionStatus.Unread or SuggestionStatus.Read)) continue;

            var baseline = state.AdoptionBaseline.GetValueOrDefault(signature);
            if (todayCount > baseline)
            {
                state.Status = SuggestionStatus.Adopted;
                state.Celebrated = false;
                return rule.Id;
            }
        }
        return null;
    }

    public void MarkRead(string ruleId, Dictionary<string, SuggestionState> states)
    {
        if (!states.TryGetValue(ruleId, out var state) || state.Status != SuggestionStatus.Unread) return;
        state.Status = SuggestionStatus.Read;
    }

    public void Dismiss(string ruleId, Dictionary<string, SuggestionState> states)
    {
        if (!states.TryGetValue(ruleId, out var state))
            states[ruleId] = state = SuggestionState.Create(ruleId);
        state.Status = SuggestionStatus.Dismissed;
    }
}
