using System.Windows;
using System.Windows.Controls;
using LessMouseWin.Localization;
using LessMouseWin.State;
using LessMouseWin.Suggestions;
using LessMouseWin.UI;

namespace LessMouseWin.UI.Pages;

internal sealed class DetailPage : IPage
{
    private readonly AppState _state;
    private readonly string _ruleId;
    private readonly Action _onBack;
    private readonly StackPanel _root;
    private readonly TextBlock _summaryText;
    private readonly StackPanel _bodyStack;

    public FrameworkElement Content => _root;

    public DetailPage(AppState state, string ruleId, Action onBack)
    {
        _state = state;
        _ruleId = ruleId;
        _onBack = onBack;

        var rule = RuleLibrary.Rule(ruleId);
        _root = new StackPanel { Margin = new Thickness(Ui.Gutter) };

        if (rule is null)
        {
            _root.Children.Add(Ui.PageHeader(Loc.T("suggestion.title"), "💡", onBack));
            _root.Children.Add(Ui.Text(Loc.T("suggestion.missing"), 13, FontWeights.Normal,
                Palette.TextTertiaryBrush, TextWrapping.Wrap));
            _summaryText = Ui.Text("");
            _bodyStack = new StackPanel();
            return;
        }

        _root.Children.Add(Ui.PageHeader(Loc.T(rule.TitleKey), rule.Symbol, onBack));

        // Evidence module.
        _summaryText = Ui.Text("", 11, FontWeights.Normal, Palette.TextTertiaryBrush, TextWrapping.Wrap);
        var evidence = new StackPanel { Margin = new Thickness(Ui.RowPadding) };
        var evidenceHead = new StackPanel { Orientation = Orientation.Horizontal };
        evidenceHead.Children.Add(Ui.Glyph(rule.Symbol, true));
        var evidenceText = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        evidenceText.Children.Add(_summaryText);
        evidenceText.Children.Add(Ui.KeyCapRow(rule.KeyCaps[0], margin: new Thickness(0, 6, 0, 0)));
        evidenceHead.Children.Add(evidenceText);
        evidence.Children.Add(evidenceHead);
        _root.Children.Add(Ui.Module(evidence));

        // Body module.
        _bodyStack = new StackPanel { Margin = new Thickness(Ui.RowPadding) };
        _bodyStack.Children.Add(Ui.Text(Loc.T(rule.BodyKey), 13, FontWeights.Normal,
            Palette.TextSecondaryBrush, TextWrapping.Wrap));
        _root.Children.Add(Ui.Module(_bodyStack));

        // Alternatives.
        if (rule.KeyCaps.Count > 1)
        {
            var alt = new StackPanel();
            alt.Children.Add(Ui.TitleRow(Loc.T("suggestion.alternatives")));
            for (var i = 1; i < rule.KeyCaps.Count; i++)
            {
                alt.Children.Add(Ui.KeyCapRow(rule.KeyCaps[i],
                    margin: new Thickness(Ui.RowPadding, 0, Ui.RowPadding, 10)));
            }
            _root.Children.Add(Ui.Module(alt));
        }

        // Actions.
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(Ui.AccentButton(Loc.T("common.knowIt"), () =>
        {
            _state.MarkRead(rule.Id);
            _onBack();
        }));
        actions.Children.Add(Ui.SecondaryButton(Loc.T("common.neverAgain"), () =>
        {
            _state.Dismiss(rule.Id);
            _onBack();
        }));
        _root.Children.Add(actions);

        RefreshDynamic();
    }

    public void RefreshDynamic()
    {
        var rule = RuleLibrary.Rule(_ruleId);
        if (rule is null || _summaryText.Text.Length > 0) return;

        var count = TriggerCount(rule);
        _summaryText.Text = Loc.Format(rule.SummaryKey, count);
    }

    private int TriggerCount(SuggestionRule rule)
    {
        switch (rule.Trigger.Kind)
        {
            case RuleTriggerKind.PatternBursts:
                return _state.Today.Patterns.GetValueOrDefault(rule.Trigger.PatternId ?? "");
            case RuleTriggerKind.ComboUsage:
                return rule.Trigger.Signatures.Sum(signature => _state.Today.Combos.GetValueOrDefault(signature));
            case RuleTriggerKind.UnusedWhileActive:
                return rule.Trigger.Activity switch
                {
                    ActivityKind.BrowserUse => _state.Activity.Browser,
                    ActivityKind.MultiAppUse => _state.Activity.MultiApp,
                    _ => 0,
                };
            case RuleTriggerKind.ActivityShare:
                return _state.TodayAppSwitches;
            default:
                return 0;
        }
    }
}
