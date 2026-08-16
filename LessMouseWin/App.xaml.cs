using System.Windows;
using LessMouseWin.Services;
using LessMouseWin.State;
using LessMouseWin.UI;

namespace LessMouseWin;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayIconService? _tray;
    private MainWindow? _window;
    private AppState? _state;
    private bool _quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\LessMouseWinSingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        Localization.Loc.Initialize();

        var settingsStore = new SettingsStore(AppPaths.DataDirectory);
        Localization.Loc.Override = settingsStore.Settings.Language;

        var store = new Storage.StatsStore(AppPaths.DataDirectory);
        _state = new AppState(store, settingsStore.Settings, Dispatcher);

        _window = new MainWindow(_state, QuitApplication);
        _tray = new TrayIconService(_state, route => _window.ShowPopup(route), QuitApplication);
        _window.TrayIconRectProvider = () => _tray.GetIconRect();
        settingsStore.Settings.Changed += (_, _) => _tray.Refresh();

        Localization.Loc.LanguageChanged += () =>
        {
            _tray.Refresh();
            _window.RebuildPage();
        };
        _state.PropertyChanged += (_, _) =>
        {
            _window.RefreshPage();
            _tray.Refresh();
        };
        _state.LayoutChanged += () => _window.FitToPage();

        // Refresh the window when Windows flips light/dark mode.
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == Microsoft.Win32.UserPreferenceCategory.General)
                Dispatcher.BeginInvoke(() =>
                {
                    UI.Palette.Reload();
                    _window.RefreshPage();
                });
        };

        SessionEnding += (_, _) => QuitApplication();

        _state.Start();
        _tray.Refresh();

        // Welcome popup on a truly fresh install.
        if (store.DaysObserved() == 0 && !settingsStore.Settings.IsPaused)
            _window.ShowPopup();
    }

    /// <summary>
    /// The one and only quit path. It performs all cleanup synchronously and
    /// then calls Environment.Exit, so a cancelled/hidden popup can never
    /// leave the process running with a dead Window (the "Quit did nothing,
    /// then tray click throws" bug).
    /// </summary>
    private void QuitApplication()
    {
        if (_quitting) return;
        _quitting = true;

        try
        {
            _window?.AllowClose();
            _window?.Hide();
        }
        catch { }

        try { _state?.Shutdown(); } catch { }
        try { _tray?.Dispose(); _tray = null; } catch { }
        try { _singleInstance?.Dispose(); _singleInstance = null; } catch { }

        // Give the shell a beat to process NIM_DELETE so no ghost tray icon
        // lingers after the process is gone.
        try { Thread.Sleep(120); } catch { }

        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Fallback for shutdown paths that don't go through QuitApplication.
        if (!_quitting)
        {
            _state?.Shutdown();
            _tray?.Dispose();
            _singleInstance?.Dispose();
        }
        base.OnExit(e);
    }
}
