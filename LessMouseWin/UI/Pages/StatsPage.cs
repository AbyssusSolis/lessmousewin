using System.Windows;
using System.Windows.Controls;
using LessMouseWin.Localization;
using LessMouseWin.Models;
using LessMouseWin.State;
using LessMouseWin.Suggestions;
using LessMouseWin.UI;

namespace LessMouseWin.UI.Pages;

internal sealed class StatsPage : IPage
{
    private readonly AppState _state;
    private readonly StackPanel _root;
    private readonly StackPanel _combosStack;
    private readonly TextBlock _emptyText;
    private readonly TextBlock _adoptedTitle;
    private readonly TextBlock _adoptedValue;
    private readonly Border _adoptedMeter;
    private readonly TextBlock _daysTitle;
    private string _lastSignature = "";

    public FrameworkElement Content => _root;

    public StatsPage(AppState state, Action onBack)
    {
        _state = state;
        _root = new StackPanel { Margin = new Thickness(Ui.Gutter) };
        _root.Children.Add(Ui.PageHeader(Loc.T("stats.title"), "▤", onBack));

        _emptyText = Ui.Text(Loc.T("stats.empty"), 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            margin: new Thickness(Ui.RowPadding, 0, Ui.RowPadding, 12));
        _combosStack = new StackPanel();
        var combos = new StackPanel();
        combos.Children.Add(Ui.TitleRow(Loc.T("stats.topCombos"),
            Ui.Text(Loc.T("stats.topCombos.range"), 11, FontWeights.Normal, Palette.TextTertiaryBrush)));
        combos.Children.Add(_emptyText);
        combos.Children.Add(_combosStack);
        _root.Children.Add(Ui.Module(combos));

        _adoptedValue = Ui.Mono("0/0", 13, FontWeights.Normal);
        _adoptedMeter = Ui.Meter(0);
        var adoptedRow = Ui.Row("", Loc.T("stats.adopted.hint"), "✓", false, _adoptedValue, _adoptedMeter);
        var daysRow = Ui.Row("", Loc.T("stats.daysObserved.hint"), "◷", false, null);

        var progress = new StackPanel();
        progress.Children.Add(Ui.TitleRow(Loc.T("stats.progress")));
        progress.Children.Add(adoptedRow);
        progress.Children.Add(Ui.Divider());
        progress.Children.Add(daysRow);
        _root.Children.Add(Ui.Module(progress));

        _adoptedTitle = (TextBlock)((StackPanel)((Grid)adoptedRow).Children[1]).Children[0];
        _daysTitle = (TextBlock)((StackPanel)((Grid)daysRow).Children[1]).Children[0];

        _root.Children.Add(Ui.QuietButton(Loc.T("stats.revealData"), TrayIconService.OpenDataFolder));

        Rebuild(_state.Store.TopCombos(dayLimit: 7, limit: 8),
            _state.SuggestionStates.Values.Count(s => s.Status == SuggestionStatus.Adopted),
            _state.Store.DaysObserved());
    }

    public void RefreshDynamic()
    {
        var top = _state.Store.TopCombos(dayLimit: 7, limit: 8);
        var adopted = _state.SuggestionStates.Values.Count(s => s.Status == SuggestionStatus.Adopted);
        var days = _state.Store.DaysObserved();
        var signature = string.Join(";", top.Select(pair => $"{pair.Key}:{pair.Value}"))
                        + $"|{adopted}|{days}";
        if (signature == _lastSignature) return;
        _lastSignature = signature;
        Rebuild(top, adopted, days);
    }

    private void Rebuild(IReadOnlyList<KeyValuePair<string, int>> top, int adopted, int days)
    {
        _combosStack.Children.Clear();
        _emptyText.Visibility = top.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var peak = top.Count > 0 ? top[0].Value : 1;
        for (var i = 0; i < top.Count; i++)
        {
            if (i > 0) _combosStack.Children.Add(Ui.Divider());
            var entry = top[i];
            var meter = Ui.Meter((double)entry.Value / Math.Max(peak, 1));
            // Title in the UI face, count in 11pt medium mono (Typo.numeric):
            // every number here is one the user might audit against
            // stats.json, so every number is monospaced.
            _combosStack.Children.Add(Ui.Row(
                KeyWhitelist.FormatSignatureDisplay(entry.Key),
                null,
                "⌨", false,
                Ui.Mono(entry.Value.ToString(), 11, FontWeights.Medium),
                meter));
        }

        var total = RuleLibrary.All.Count;
        _adoptedTitle.Text = Loc.Format("stats.adopted", adopted, total);
        _adoptedValue.Text = $"{adopted}/{total}";
        Ui.UpdateMeter(_adoptedMeter, (double)adopted / Math.Max(total, 1));

        _daysTitle.Text = Loc.Format("stats.daysObserved", days);
    }
}
