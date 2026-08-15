using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using LessMouseWin.Localization;
using LessMouseWin.Services;
using LessMouseWin.State;
using LessMouseWin.UI;

namespace LessMouseWin.UI.Pages;

internal sealed class SettingsPage : IPage
{
    private readonly AppState _state;
    private readonly Action _onBack;
    private readonly StackPanel _root;
    private readonly StackPanel _exclusionsStack;
    private readonly TextBlock _exclusionsEmpty;
    private readonly CheckBox _pauseToggle;
    private readonly CheckBox _launchToggle;
    private readonly Grid _languageStack;
    private readonly TextBlock _storagePath;
    private bool _confirmErase;
    private Button? _eraseButton;
    private TextBlock? _eraseSubtitle;

    public FrameworkElement Content => _root;

    public SettingsPage(AppState state, Action onBack)
    {
        _state = state;
        _onBack = onBack;
        _root = new StackPanel { Margin = new Thickness(Ui.Gutter) };
        _root.Children.Add(Ui.PageHeader(Loc.T("settings.title"), "⚙", onBack));

        // Tracking module.
        _pauseToggle = Ui.Toggle(_state.Settings.IsPaused, value => _state.Settings.IsPaused = value);
        var tracking = Ui.Module(
            Ui.TitleRow(Loc.T("settings.tracking")),
            Ui.Row(Loc.T("settings.pauseTracking"), Loc.T("settings.pauseTracking.hint"),
                _state.Settings.IsPaused ? "⏸" : "⌨", !_state.Settings.IsPaused, _pauseToggle,
                subtitleWrap: TextWrapping.Wrap));
        _root.Children.Add(tracking);

        // Startup module.
        _launchToggle = Ui.Toggle(LoginItemService.IsEnabled, value =>
        {
            LoginItemService.SetEnabled(value);
            _state.Settings.LaunchAtLogin = LoginItemService.IsEnabled;
        });
        var startup = Ui.Module(
            Ui.TitleRow(Loc.T("settings.startup")),
            Ui.Row(Loc.T("settings.launchAtLogin"), Loc.T("settings.launchAtLogin.hint"),
                "↗", LoginItemService.IsEnabled, _launchToggle, subtitleWrap: TextWrapping.Wrap));
        _root.Children.Add(startup);

        // Exclusions module.
        _exclusionsEmpty = Ui.Text(Loc.T("settings.exclusions.empty"), 11, FontWeights.Normal,
            Palette.TextTertiaryBrush, TextWrapping.Wrap,
            margin: new Thickness(Ui.RowPadding, 0, Ui.RowPadding, 12));
        _exclusionsStack = new StackPanel();
        var addButton = Ui.QuietButton("+", ShowAddAppMenu);
        var exclusions = new StackPanel();
        exclusions.Children.Add(Ui.TitleRow(Loc.T("settings.exclusions"), addButton));
        exclusions.Children.Add(_exclusionsEmpty);
        exclusions.Children.Add(_exclusionsStack);
        _root.Children.Add(Ui.Module(exclusions));

        // Language module.
        _languageStack = new Grid { Margin = new Thickness(Ui.RowPadding, 0, Ui.RowPadding, 12) };
        _languageStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _languageStack.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _languageStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var language = new StackPanel();
        language.Children.Add(Ui.TitleRow(Loc.T("settings.language")));
        language.Children.Add(_languageStack);
        _root.Children.Add(Ui.Module(language));
        RebuildLanguageChips();

        // Data module.
        _storagePath = Ui.Text(_state.Store.StoragePath, 11, FontWeights.Normal, Palette.TextTertiaryBrush,
            TextWrapping.Wrap);
        var showButton = Ui.QuietButton(Loc.T("settings.showData"), TrayIconService.OpenDataFolder);
        var data = new StackPanel();
        data.Children.Add(Ui.TitleRow(Loc.T("settings.data")));
        data.Children.Add(Ui.Row(Loc.T("settings.dataLocation"), null, "🗀", false, showButton, _storagePath));
        data.Children.Add(Ui.Divider());

        _eraseSubtitle = Ui.Text(Loc.T("settings.eraseAll.hint"), 11, FontWeights.Normal,
            Palette.TextTertiaryBrush, TextWrapping.Wrap);
        _eraseButton = Ui.QuietButton(Loc.T("settings.eraseAll"), () =>
        {
            if (!_confirmErase)
            {
                _confirmErase = true;
                RefreshEraseRow();
                return;
            }
            _confirmErase = false;
            _state.EraseAllData();
            RefreshEraseRow();
        });
        var eraseRow = Ui.Row(Loc.T("settings.eraseAll"), "", "🗑", false, _eraseButton, _eraseSubtitle,
            subtitleWrap: TextWrapping.Wrap);
        // Replace the subtitle text in the constructed row.
        if (eraseRow.Children[1] is StackPanel eraseText && eraseText.Children.Count > 1)
            eraseText.Children[1] = _eraseSubtitle;
        data.Children.Add(eraseRow);
        _root.Children.Add(Ui.Module(data));

        // Privacy note.
        _root.Children.Add(Ui.Text(Loc.T("settings.privacyNote"), 11, FontWeights.Normal,
            Palette.TextTertiaryBrush, TextWrapping.Wrap, margin: new Thickness(4, 6, 4, 0)));

        RefreshDynamic();
    }

    public void RefreshDynamic()
    {
        _pauseToggle.IsChecked = _state.Settings.IsPaused;
        _launchToggle.IsChecked = LoginItemService.IsEnabled;
        RebuildExclusions();
        RebuildLanguageChips();
        RefreshEraseRow();
    }

    private void RebuildExclusions()
    {
        _exclusionsStack.Children.Clear();
        var apps = _state.Settings.ExcludedApps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        _exclusionsEmpty.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < apps.Count; i++)
        {
            if (i > 0) _exclusionsStack.Children.Add(Ui.Divider());
            var app = apps[i];
            var remove = Ui.QuietButton("✕", () => _state.Settings.Include(app));
            _exclusionsStack.Children.Add(Ui.Row(AppDisplayName(app), app, "▣", false, remove,
                subtitleWrap: TextWrapping.Wrap));
        }
    }

    private void RebuildLanguageChips()
    {
        _languageStack.Children.Clear();

        var system = LanguageChip(Loc.T("settings.language.system"), null);
        system.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(system, 0);
        _languageStack.Children.Add(system);

        var codes = Loc.Available.ToList();
        for (var i = 0; i < codes.Count; i++)
        {
            var chip = LanguageChip(Loc.DisplayName(codes[i]), codes[i]);
            Grid.SetColumn(chip, i + 1);
            chip.HorizontalAlignment = i == 0 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            _languageStack.Children.Add(chip);
        }
    }

    private Button LanguageChip(string label, string? code)
    {
        var selected = Loc.Override == code;
        var text = (selected ? "✓ " : "") + label;
        var button = selected
            ? Ui.SecondaryButton(text, () => SetLanguage(code))
            : Ui.QuietButton(text, () => SetLanguage(code));
        button.Padding = new Thickness(12, 5, 12, 5);
        return button;
    }

    private void SetLanguage(string? code)
    {
        Loc.Override = code;
        _state.Settings.Language = code;
        RebuildLanguageChips();
    }

    private void RefreshEraseRow()
    {
        if (_eraseButton is null || _eraseSubtitle is null) return;
        _eraseButton.Content = Ui.Text(_confirmErase ? Loc.T("common.confirm") : Loc.T("settings.eraseAll"),
            13, FontWeights.Medium, _confirmErase ? Palette.DangerBrush : Palette.TextTertiaryBrush);
        _eraseSubtitle.Text = _confirmErase
            ? Loc.T("settings.eraseAll.confirm")
            : Loc.T("settings.eraseAll.hint");
        _eraseSubtitle.Foreground = _confirmErase ? Palette.DangerBrush : Palette.TextTertiaryBrush;
    }

    private void ShowAddAppMenu()
    {
        var menu = new ContextMenu
        {
            FontFamily = Ui.UiFont,
            FontSize = 12,
        };
        var processes = RunningApps();
        if (processes.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Loc.T("settings.exclusions.empty"), IsEnabled = false });
        }
        else
        {
            foreach (var process in processes)
            {
                var name = process.Key;
                var menuItem = new MenuItem { Header = $"{process.Value}  ({name})" };
                menuItem.Click += (_, _) => _state.Settings.Exclude(name);
                menu.Items.Add(menuItem);
            }
        }
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static List<KeyValuePair<string, string>> RunningApps()
    {
        var list = new List<KeyValuePair<string, string>>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    var name = process.ProcessName;
                    if (string.Equals(name, ProcessNameResolver.OwnProcessName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var title = string.IsNullOrWhiteSpace(process.MainWindowTitle) ? name : process.MainWindowTitle;
                    list.Add(new KeyValuePair<string, string>(name, title));
                }
                catch
                {
                    // Process exited while enumerating.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // Enumeration failure: the menu simply shows what it could gather.
        }
        return list
            .GroupBy(pair => pair.Key)
            .Select(group => group.First())
            .OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string AppDisplayName(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        return process.MainWindowTitle;
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
        }
        return processName;
    }
}
