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
        var ok = true;
        void Check(string name, bool condition)
        {
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
            if (!condition) ok = false;
        }

        Loc.Initialize();
        Loc.Override = "en";
        Check("english localization", Loc.T("today.events") == "Key events");
        Loc.Override = "zh-Hans";
        Check("chinese localization", Loc.T("today.events") == "按键事件");
        Loc.Override = null;

        Check("privacy sweep: bare/shift keys are navigation-only",
            RunPrivacySweep(BareModifiers, requireNavigationOnly: true));
        Check("privacy sweep: combos always have readable tokens",
            RunPrivacySweep(ComboModifiers, requireNavigationOnly: false));
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
        var mainSize = Measure(main.Content);
        var statsSize = Measure(stats.Content);
        var settingsSize = Measure(settingsPage.Content);
        var detailSize = Measure(detail.Content);
        Check("main page renders", mainSize.Height > 100 && mainSize.Width <= 354);
        Check("stats page renders", statsSize.Height > 100 && statsSize.Width <= 354);
        Check("settings page renders", settingsSize.Height > 100 && settingsSize.Width <= 354);
        Check("detail page renders", detailSize.Height > 100 && detailSize.Width <= 354);

        // Language/layout regression guards: both languages must fit the
        // popup viewport without scrolling, and even the shortest chip must
        // keep a straight middle (min width 68 with a 28px capsule).
        Loc.Override = "zh-Hans";
        var zhHeight = Measure(new SettingsPage(state, () => { }).Content).Height;
        Loc.Override = "en";
        var enHeight = Measure(new SettingsPage(state, () => { }).Content).Height;
        Loc.Override = null;
        var viewport = Ui.MaxPopupHeight - 26;
        Check("zh settings fit viewport", zhHeight <= viewport);
        Check("en settings fit viewport", enHeight <= viewport);
        var shortChip = Ui.Chip("中文", false, () => { });
        var chipSize = Measure(shortChip);
        Check("short language chip keeps a straight middle", chipSize.Width >= 68 && chipSize.Height >= 28);

        Directory.Delete(dir, recursive: true);
        return ok ? 0 : 1;
    }

    private static readonly ModifierSet[] BareModifiers = [ModifierSet.None, ModifierSet.Shift];
    private static readonly ModifierSet[] ComboModifiers =
    [
        ModifierSet.Ctrl,
        ModifierSet.Alt,
        ModifierSet.Win,
        ModifierSet.Ctrl | ModifierSet.Shift,
        ModifierSet.Alt | ModifierSet.Shift,
        ModifierSet.Ctrl | ModifierSet.Alt | ModifierSet.Shift,
    ];

    /// <summary>
    /// The privacy invariant from the macOS original, stated over the whole
    /// Windows VK input space: every signature reachable with no Ctrl/Alt/Win
    /// must name a navigation key, and every combination signature must have
    /// a non-empty, whitespace-free token so stats.json stays auditable.
    /// </summary>
    private static bool RunPrivacySweep(IEnumerable<ModifierSet> modifiers, bool requireNavigationOnly)
    {
        var navigationTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "backspace", "tab", "esc", "pageup", "pagedown", "end", "home",
            "left", "up", "right", "down", "delete",
            "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12",
        };

        foreach (var modifier in modifiers)
        for (var keyCode = 0; keyCode <= 0xFF; keyCode++)
        {
            var signature = KeySignatureFilter.Signature(new KeyEvent
            {
                Timestamp = 1,
                KeyCode = (ushort)keyCode,
                Modifiers = modifier,
                Application = "privacy-sweep",
            });

            if (signature is null) continue;

            if (requireNavigationOnly && !navigationTokens.Contains(signature.Key.Token))
                return false;

            if (string.IsNullOrWhiteSpace(signature.Key.Token))
                return false;
        }

        return true;
    }

    private static Size Measure(FrameworkElement element)
    {
        element.Measure(new Size(354, double.PositiveInfinity));
        return element.DesiredSize;
    }
}
