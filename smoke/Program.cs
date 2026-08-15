using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using LessMouseWin.Localization;
using LessMouseWin.Models;
using LessMouseWin.Services;
using LessMouseWin.State;
using LessMouseWin.UI.Pages;
using LessMouseWin.UI;
using System.Windows;
using LessMouseWin.Suggestions;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Loc.Initialize();
        Loc.Override = "en";
        Check("english localization", Loc.T("today.events") == "Key events");
        Loc.Override = "zh-Hans";
        Check("chinese localization", Loc.T("today.events") == "按键事件");
        Loc.Override = null;
        var dir = Path.Combine(Path.GetTempPath(), "lm-smoke-" + Guid.NewGuid().ToString("N"));
        var dispatcher = Dispatcher.CurrentDispatcher;
        var settings = new SettingsStore(dir).Settings;
        var store = new LessMouseWin.Storage.StatsStore(dir);
        using var state = new AppState(store, settings, dispatcher, TimeSpan.FromMilliseconds(50));
        state.StartMonitor();

        for (var burst = 0; burst < 3; burst++)
        for (var j = 0; j < 5; j++)
            state.Ingest(new KeyEvent
            {
                Timestamp = burst * 10 + j * 0.2,
                KeyCode = 0x08,
                Modifiers = ModifierSet.None,
                Application = "notepad",
            });

        state.Ingest(new KeyEvent { Timestamp = 50, KeyCode = 0x43, Modifiers = ModifierSet.Ctrl, Application = "notepad" });
        state.Ingest(new KeyEvent { Timestamp = 51, KeyCode = 0x45, Modifiers = ModifierSet.None, Application = "notepad" });
        state.Ingest(new KeyEvent { Timestamp = 52, KeyCode = 0x45, Modifiers = ModifierSet.Shift, Application = "notepad" });

        // Pump the dispatcher long enough for the publish timer.
        var frame = new DispatcherFrame();
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(900), DispatcherPriority.Background,
            (_, _) => { timer?.Stop(); frame.Continue = false; }, dispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);

        state.Shutdown();
        store.Flush();

        var today = store.TodaySnapshot();
        var ok = true;
        void Check(string name, bool condition)
        {
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
            if (!condition) ok = false;
        }

        Check("backspaces counted", today.Combos.GetValueOrDefault("backspace") == 15);
        Check("bursts detected", today.Patterns.GetValueOrDefault("backspace-burst") >= 3);
        Check("bare and shifted letters dropped", !today.Combos.ContainsKey("e"));
        Check("ctrl+c counted", today.Combos.GetValueOrDefault("ctrl+c") == 1);
        Check("unread card created", state.UnreadCount == 1 &&
            state.SuggestionStates.TryGetValue("delete-by-word", out var s) && s.Status == SuggestionStatus.Unread);
        var suggestionsJson = File.ReadAllText(Path.Combine(dir, "suggestions.json"));
        Console.WriteLine("SUGGESTIONS JSON: " + suggestionsJson.Replace(Environment.NewLine, " "));
        Check("suggestion state serializes as text", suggestionsJson.Contains("unread"));

        // Page constructors must all render a measurable tree.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(state);
        Check("main window builds", window.Width > 0);
        window.AllowClose();
        window.Close();

        var main = new MainPage(state, _ => { }, () => { }, () => { }, () => { });
        var stats = new StatsPage(state, () => { });
        var settingsPage = new SettingsPage(state, () => { });
        var detail = new DetailPage(state, "delete-by-word", () => { });
        Check("main page renders", Measure(main.Content).Height > 100);
        Check("stats page renders", Measure(stats.Content).Height > 100);
        Check("settings page renders", Measure(settingsPage.Content).Height > 100);
        Check("detail page renders", Measure(detail.Content).Height > 100);

        Directory.Delete(dir, recursive: true);
        return ok ? 0 : 1;
    }

    private static Size Measure(FrameworkElement element)
    {
        element.Measure(new Size(354, double.PositiveInfinity));
        return element.DesiredSize;
    }
}
