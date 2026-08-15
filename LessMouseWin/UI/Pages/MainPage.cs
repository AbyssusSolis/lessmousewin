using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly Ellipse _dot;
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

        // Tracking header.
        _dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        _headline = Ui.Text(Loc.T("tracking.active"), 13, FontWeights.Normal);
        var headerLine = new StackPanel { Orientation = Orientation.Horizontal };
        headerLine.Children.Add(_dot);
        var headlineWrap = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        headlineWrap.Children.Add(_headline);
        _subline = Ui.Text("", 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            margin: new Thickness(0, 1, 0, 0));
        headlineWrap.Children.Add(_subline);
        headerLine.Children.Add(headlineWrap);
        _root.Children.Add(new StackPanel { Children = { headerLine }, Margin = new Thickness(0, 6, 0, 8) });

        // Failure warning (only visible when the hook cannot start).
        _failureText = Ui.Text("", 11, FontWeights.Normal, Palette.DangerBrush, TextWrapping.Wrap);
        var retry = Ui.SecondaryButton(Loc.T("tracking.retry"), () => _state.StartMonitor());
        var failureStack = Ui.VStack(4);
        failureStack.Children.Add(_failureText);
        failureStack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { retry } });
        _failureModule = Ui.Module(failureStack);
        _failureModule.Visibility = Visibility.Collapsed;
        _root.Children.Add(_failureModule);

        // Celebration banner.
        _celebrationTitle = Ui.Text("", 13, FontWeights.Normal, Palette.TextBrush);
        _celebrationSub = Ui.Text("", 11, FontWeights.Normal, Palette.TextSecondaryBrush);
        var celebrationDismiss = Ui.QuietButton(Loc.T("common.knowIt"), () => _state.DismissCelebration());
        var celebrationStack = Ui.VStack(2);
        celebrationStack.Children.Add(_celebrationTitle);
        celebrationStack.Children.Add(_celebrationSub);
        celebrationStack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { celebrationDismiss } });
        _celebrationModule = Ui.Module(celebrationStack);
        _celebrationModule.Visibility = Visibility.Collapsed;
        _root.Children.Add(_celebrationModule);

        // Today ledger.
        _eventsValue = Ui.Mono("0", 13, FontWeights.Normal);
        _patternsValue = Ui.Mono("0", 13, FontWeights.Normal);
        _combosValue = Ui.Mono("0", 13, FontWeights.Normal);
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
        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
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
        _dot.Fill = phase switch
        {
            TrackingPhase.Tracking => Palette.AccentBrush,
            TrackingPhase.Paused => Palette.TextTertiaryBrush,
            _ => Palette.WarningBrush,
        };
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

        if (_state.Celebration is { } ruleId && RuleLibrary.Rule(ruleId) is { } rule)
        {
            _celebrationTitle.Text = Loc.Format("celebration.title", rule.PrimaryShortcutLabel);
            _celebrationSub.Text = Loc.T(rule.TitleKey);
            _celebrationModule.Visibility = Visibility.Visible;
        }
        else
        {
            _celebrationModule.Visibility = Visibility.Collapsed;
        }

        RebuildInbox();
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

        var button = new Button
        {
            Content = row,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        var template = new ControlTemplate(typeof(Button));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        template.VisualTree = content;
        button.Template = template;
        var ruleId = rule.Id;
        button.Click += (_, _) => _openSuggestion(ruleId);
        return button;
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
