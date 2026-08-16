using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using LessMouseWin.Localization;
using LessMouseWin.State;
using LessMouseWin.Suggestions;
using LessMouseWin.UI;

namespace LessMouseWin.UI.Pages;

internal sealed class MainPage : IPage
{
    private readonly AppState _state;
    private readonly Action<string> _openSuggestion;
    private readonly Action _openStats;
    private readonly Action _openSettings;
    private readonly Action _quit;

    private readonly TextBlock _headline;
    private readonly TextBlock _subline;
    private readonly Grid _dot;
    private readonly Ellipse _dotCore;
    private readonly Ellipse _dotHalo;
    private bool _pulsing;
    private bool _windowVisible;
    private readonly StackPanel _root;
    private readonly Border _celebrationModule;
    private readonly TextBlock _celebrationTitle;
    private readonly TextBlock _celebrationSub;
    private readonly Border _failureModule;
    private readonly TextBlock _failureText;
    private readonly TextBlock _eventsValue;
    private readonly TextBlock _patternsValue;
    private readonly TextBlock _combosValue;
    private readonly StackPanel _inboxStack;
    private readonly TextBlock _inboxEmpty;

    private string? _lastCelebration;
    private string _lastInboxSignature = "";

    public FrameworkElement Content => _root;

    public MainPage(AppState state, Action<string> openSuggestion, Action openStats,
        Action openSettings, Action quit)
    {
        _state = state;
        _openSuggestion = openSuggestion;
        _openStats = openStats;
        _openSettings = openSettings;
        _quit = quit;

        _root = new StackPanel { Margin = new Thickness(Ui.Gutter) };

        // Tracking header: dot + headline + today's summary, the original's
        // TrackingHeader. The dot is an 8pt core inside a 25% halo ring —
        // a flat ring, not a blur; this panel spends no shadows. It breathes
        // slowly while the hook is listening.
        _dotCore = new Ellipse
        {
            Width = 8, Height = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _dotHalo = new Ellipse
        {
            Width = 14, Height = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _dot = new Grid { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        _dot.Children.Add(_dotHalo);
        _dot.Children.Add(_dotCore);
        _headline = Ui.Text(Loc.T("tracking.active"), 15, FontWeights.SemiBold,
            margin: new Thickness(2, 0, 0, 0));
        var headerLine = new StackPanel { Orientation = Orientation.Horizontal };
        headerLine.Children.Add(_dot);
        headerLine.Children.Add(_headline);
        // The subline sits under the headline, indented past the dot so the
        // two lines of text share one left edge.
        _subline = Ui.Text("", 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            margin: new Thickness(16, 2, 0, 0));
        var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        headerStack.Children.Add(headerLine);
        headerStack.Children.Add(_subline);
        _root.Children.Add(headerStack);

        // Failure warning (only visible when the hook cannot start).
        _failureText = Ui.Text("", 11, FontWeights.Normal, Palette.DangerBrush, TextWrapping.Wrap);
        var retry = Ui.SecondaryButton(Loc.T("tracking.retry"), () => _state.StartMonitor());
        var failureStack = Ui.VStack(4);
        failureStack.Margin = new Thickness(Ui.RowPadding, 10, Ui.RowPadding, 10);
        failureStack.Children.Add(_failureText);
        failureStack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { retry },
        });
        _failureModule = Ui.Module(failureStack);
        _failureModule.Background = Palette.DangerSoftBrush;
        _failureModule.BorderBrush = Palette.DangerBrush;
        _failureModule.Visibility = Visibility.Collapsed;
        _root.Children.Add(_failureModule);

        // Celebration banner — a plain module row whose green check disc is
        // the whole celebration; no tinted card (the original spends the
        // accent on the glyph, not the background).
        _celebrationTitle = Ui.Text("", 13, FontWeights.Medium, Palette.TextBrush);
        _celebrationSub = Ui.Text("", 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            margin: new Thickness(0, 2, 0, 0));
        var celebrationDismiss = Ui.QuietButton(Loc.T("common.knowIt"), () => _state.DismissCelebration());
        var celebrationRow = new Grid { Margin = new Thickness(Ui.RowPadding, 8, Ui.RowPadding, 8) };
        celebrationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Ui.GlyphSize) });
        celebrationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        celebrationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var celebrationGlyph = Ui.Glyph("✓", true);
        Grid.SetColumn(celebrationGlyph, 0);
        celebrationRow.Children.Add(celebrationGlyph);
        var celebrationText = new StackPanel { Margin = new Thickness(8, 0, 6, 0) };
        celebrationText.Children.Add(_celebrationTitle);
        celebrationText.Children.Add(_celebrationSub);
        Grid.SetColumn(celebrationText, 1);
        celebrationRow.Children.Add(celebrationText);
        Grid.SetColumn(celebrationDismiss, 2);
        celebrationDismiss.VerticalAlignment = VerticalAlignment.Center;
        celebrationRow.Children.Add(celebrationDismiss);
        _celebrationModule = Ui.Module(celebrationRow);
        _celebrationModule.Visibility = Visibility.Collapsed;
        _root.Children.Add(_celebrationModule);

        // Today ledger: three rows, three numbers, all monospaced. Green is
        // spent on the tracking dot above — these glyphs stay off, counts
        // carry the screen.
        _eventsValue = Ui.Mono("0", 15, FontWeights.SemiBold);
        _patternsValue = Ui.Mono("0", 15, FontWeights.SemiBold);
        _combosValue = Ui.Mono("0", 15, FontWeights.SemiBold);
        var today = Ui.Module(
            Ui.Row(Loc.T("today.events"), Loc.T("today.events.hint"), "⌨", false, _eventsValue),
            Ui.Divider(),
            Ui.Row(Loc.T("today.patterns"), Loc.T("today.patterns.hint"), "⚡", false, _patternsValue),
            Ui.Divider(),
            Ui.Row(Loc.T("today.combos"), Loc.T("today.combos.hint"), "✦", false, _combosValue));
        _root.Children.Add(today);

        // Suggestion inbox.
        var inbox = new StackPanel();
        inbox.Children.Add(Ui.TitleRow(Loc.T("inbox.title"), Ui.Pill("", Palette.AccentInkBrush, Palette.AccentSoftBrush)));
        _inboxEmpty = Ui.Text(Loc.T("inbox.empty"), 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            TextWrapping.Wrap, margin: new Thickness(Ui.RowPadding, 0, Ui.RowPadding, 12));
        _inboxStack = new StackPanel();
        inbox.Children.Add(_inboxEmpty);
        inbox.Children.Add(_inboxStack);
        _root.Children.Add(Ui.Module(inbox));

        // Footer: stats and settings stay as a left group, quit is pushed to
        // the far right, with explicit gaps between all three actions.
        var footer = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statsButton = Ui.QuietButton(Loc.T("menu.stats"), _openStats);
        statsButton.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(statsButton, 0);
        footer.Children.Add(statsButton);

        var settingsButton = Ui.QuietButton(Loc.T("menu.settings"), _openSettings);
        settingsButton.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(settingsButton, 1);
        footer.Children.Add(settingsButton);

        var quitButton = Ui.QuietButton(Loc.T("menu.quit"), _quit);
        Grid.SetColumn(quitButton, 3);
        footer.Children.Add(quitButton);

        _root.Children.Add(footer);

        RefreshDynamic();
    }

    public void RefreshDynamic()
    {
        var phase = _state.Phase;
        var dotBrush = phase switch
        {
            TrackingPhase.Tracking => Palette.AccentBrush,
            TrackingPhase.Paused => Palette.TextTertiaryBrush,
            _ => Palette.WarningBrush,
        };
        _dotCore.Fill = dotBrush;
        _dotHalo.Fill = Ui.Faded(dotBrush, 0.25);
        UpdatePulse(phase);
        _headline.Text = _state.TrackingHeadline;

        if (phase == TrackingPhase.HookFailed)
        {
            _subline.Text = "";
            _failureText.Text = Loc.Format("tracking.failedHint", _state.HookError ?? "unknown error");
            _failureModule.Visibility = Visibility.Visible;
        }
        else
        {
            _subline.Text = Loc.Format("tracking.todaySummary", _state.Today.TotalEvents, _state.Today.Combos.Count);
            _failureModule.Visibility = Visibility.Collapsed;
        }

        _eventsValue.Text = _state.Today.TotalEvents.ToString();
        _patternsValue.Text = _state.Today.TotalPatterns.ToString();
        _combosValue.Text = _state.Today.Combos.Count.ToString();

        var celebration = _state.Celebration;
        if (celebration != _lastCelebration)
        {
            _lastCelebration = celebration;
            if (celebration is { } ruleId && RuleLibrary.Rule(ruleId) is { } rule)
            {
                _celebrationTitle.Text = Loc.Format("celebration.title", rule.PrimaryShortcutLabel);
                _celebrationSub.Text = Loc.T(rule.TitleKey);
                _celebrationModule.Visibility = Visibility.Visible;
            }
            else
            {
                _celebrationModule.Visibility = Visibility.Collapsed;
            }
        }

        var inboxSignature = string.Join("|", RuleLibrary.All.Select(rule =>
            _state.SuggestionStates.TryGetValue(rule.Id, out var s) ? s.Status.ToString() : "-"));
        if (inboxSignature != _lastInboxSignature)
        {
            _lastInboxSignature = inboxSignature;
            RebuildInbox();
        }
    }

    /// <summary>
    /// The status dot breathes while the hook is listening — a slow opacity
    /// pulse, stopped the moment tracking pauses or animations are off.
    /// </summary>
    public void OnWindowVisibilityChanged(bool visible)
    {
        _windowVisible = visible;
        UpdatePulse(_state.Phase);
    }

    private void UpdatePulse(TrackingPhase phase)
    {
        if (phase == TrackingPhase.Tracking && SystemParameters.ClientAreaAnimation && _windowVisible)
        {
            if (_pulsing) return;
            _pulsing = true;
            _dot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 1.0,
                To = 0.45,
                Duration = new Duration(TimeSpan.FromSeconds(1.6)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        }
        else if (_pulsing)
        {
            _pulsing = false;
            _dot.BeginAnimation(UIElement.OpacityProperty, null);
            _dot.Opacity = 1.0;
        }
    }

    private void RebuildInbox()
    {
        var order = new[] { SuggestionStatus.Unread, SuggestionStatus.Read, SuggestionStatus.Adopted };
        var rules = RuleLibrary.All
            .Where(rule => _state.SuggestionStates.TryGetValue(rule.Id, out var ruleState) &&
                           order.Contains(ruleState.Status))
            .OrderBy(rule => Array.IndexOf(order, _state.SuggestionStates[rule.Id].Status))
            .ToList();

        _inboxEmpty.Visibility = rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _inboxStack.Children.Clear();

        if (_inboxStack.Parent is StackPanel parent && parent.Children.Count > 0 &&
            parent.Children[0] is Grid titleGrid && titleGrid.Children.Count > 1)
        {
            var pill = titleGrid.Children[1] as Border;
            if (pill?.Child is TextBlock pillText)
                pillText.Text = Loc.Format("inbox.unread", _state.UnreadCount);
            if (pill is not null)
                pill.Visibility = _state.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        for (var i = 0; i < rules.Count; i++)
        {
            if (i > 0) _inboxStack.Children.Add(Ui.Divider());
            _inboxStack.Children.Add(InboxRow(rules[i]));
        }
    }

    private UIElement InboxRow(SuggestionRule rule)
    {
        var ruleState = _state.SuggestionStates[rule.Id];
        var status = ruleState.Status;
        var summary = Summary(rule);
        UIElement trailing = status == SuggestionStatus.Unread
            ? Ui.Pill(Loc.T("common.new"), Palette.AccentInkBrush, Palette.AccentSoftBrush)
            : status == SuggestionStatus.Adopted
                ? Ui.Pill(Loc.T("common.adopted"), Palette.PositiveBrush, Palette.PositiveSoftBrush)
                : new Grid();
        var row = Ui.Row(Loc.T(rule.TitleKey), summary, rule.Symbol, status == SuggestionStatus.Unread, trailing);
        var ruleId = rule.Id;
        return Ui.Hoverable(row, () => _openSuggestion(ruleId));
    }

    private string Summary(SuggestionRule rule)
    {
        switch (rule.Trigger.Kind)
        {
            case RuleTriggerKind.PatternBursts:
                return Loc.Format(rule.SummaryKey, _state.Today.Patterns.GetValueOrDefault(rule.Trigger.PatternId ?? ""));
            case RuleTriggerKind.ComboUsage:
            {
                var total = rule.Trigger.Signatures.Sum(signature => _state.Today.Combos.GetValueOrDefault(signature));
                return Loc.Format(rule.SummaryKey, total);
            }
            case RuleTriggerKind.UnusedWhileActive:
            {
                var days = rule.Trigger.Activity switch
                {
                    ActivityKind.BrowserUse => _state.Activity.Browser,
                    ActivityKind.MultiAppUse => _state.Activity.MultiApp,
                    _ => 0,
                };
                return Loc.Format(rule.SummaryKey, days);
            }
            case RuleTriggerKind.ActivityShare:
                return Loc.Format(rule.SummaryKey, _state.TodayAppSwitches);
            default:
                return "";
        }
    }
}
