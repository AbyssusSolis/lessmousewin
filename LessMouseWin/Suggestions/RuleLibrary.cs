using LessMouseWin.Models;

namespace LessMouseWin.Suggestions;

/// <summary>
/// The Windows coaching book. Same product philosophy as the macOS original,
/// with every shortcut translated to the Windows keyboard:
/// Mac ⌘ → Windows Ctrl, Mac ⌥ → Windows Alt, and macOS-only cards
/// (Home/End "the Mac way", universal Emacs keys) replaced with their
/// Windows-native equivalents.
/// </summary>
public static class RuleLibrary
{
    public static readonly IReadOnlyList<SuggestionRule> All =
    [
        new SuggestionRule(
            id: "delete-by-word",
            trigger: RuleTrigger.PatternBursts("backspace-burst", 3),
            watchForAdoption: ["ctrl+backspace", "ctrl+delete"],
            titleKey: "rule.deleteByWord.title",
            bodyKey: "rule.deleteByWord.body",
            summaryKey: "rule.deleteByWord.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Ctrl"), new KeyCap("⌫")],
                [KeyCap.Modifier("Ctrl"), new KeyCap("⌦")],
            ],
            symbol: "⌫",
            cooldownDays: 3),

        new SuggestionRule(
            id: "hop-by-word",
            trigger: RuleTrigger.PatternBursts("harrow-burst", 3),
            watchForAdoption: ["ctrl+left", "ctrl+right"],
            titleKey: "rule.hopByWord.title",
            bodyKey: "rule.hopByWord.body",
            summaryKey: "rule.hopByWord.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Ctrl"), new KeyCap("←")],
                [KeyCap.Modifier("Ctrl"), new KeyCap("→")],
            ],
            symbol: "↔",
            cooldownDays: 3),

        new SuggestionRule(
            id: "select-by-word",
            trigger: RuleTrigger.PatternBursts("shift-arrow-burst", 2),
            watchForAdoption: ["ctrl+shift+left", "ctrl+shift+right", "shift+end"],
            titleKey: "rule.selectByWord.title",
            bodyKey: "rule.selectByWord.body",
            summaryKey: "rule.selectByWord.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Ctrl"), KeyCap.Modifier("Shift"), new KeyCap("←")],
                [KeyCap.Modifier("Ctrl"), KeyCap.Modifier("Shift"), new KeyCap("→")],
                [KeyCap.Modifier("Shift"), new KeyCap("End")],
            ],
            symbol: "▤",
            cooldownDays: 4),

        new SuggestionRule(
            id: "doc-start-end",
            trigger: RuleTrigger.PatternBursts("varrow-burst", 2),
            watchForAdoption: ["ctrl+home", "ctrl+end"],
            titleKey: "rule.docStartEnd.title",
            bodyKey: "rule.docStartEnd.body",
            summaryKey: "rule.docStartEnd.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Ctrl"), new KeyCap("Home")],
                [KeyCap.Modifier("Ctrl"), new KeyCap("End")],
            ],
            symbol: "↕",
            cooldownDays: 5),

        new SuggestionRule(
            id: "line-start-end",
            trigger: RuleTrigger.ComboUsage(["left", "right"], 30),
            watchForAdoption: ["home", "end"],
            titleKey: "rule.lineStartEnd.title",
            bodyKey: "rule.lineStartEnd.body",
            summaryKey: "rule.lineStartEnd.summary",
            keyCaps:
            [
                [new KeyCap("Home")],
                [new KeyCap("End")],
            ],
            symbol: "⇤",
            cooldownDays: 5),

        new SuggestionRule(
            id: "virtual-desktops",
            trigger: RuleTrigger.UnusedWhileActive("ctrl+win+left", ActivityKind.MultiAppUse, 3),
            watchForAdoption: ["ctrl+win+left", "ctrl+win+right"],
            titleKey: "rule.virtualDesktops.title",
            bodyKey: "rule.virtualDesktops.body",
            summaryKey: "rule.virtualDesktops.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Win"), KeyCap.Modifier("Ctrl"), new KeyCap("←")],
                [KeyCap.Modifier("Win"), KeyCap.Modifier("Ctrl"), new KeyCap("→")],
            ],
            symbol: "▢",
            cooldownDays: 30),

        new SuggestionRule(
            id: "app-switching",
            trigger: RuleTrigger.ActivityShare("alt+tab", ActivityKind.AppSwitching, 15, 0.2),
            watchForAdoption: ["alt+tab", "alt+shift+tab"],
            titleKey: "rule.appSwitching.title",
            bodyKey: "rule.appSwitching.body",
            summaryKey: "rule.appSwitching.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Alt"), new KeyCap("Tab")],
                [KeyCap.Modifier("Alt"), KeyCap.Modifier("Shift"), new KeyCap("Tab")],
            ],
            symbol: "⇥",
            cooldownDays: 30),

        new SuggestionRule(
            id: "tab-switching",
            trigger: RuleTrigger.UnusedWhileActive("ctrl+tab", ActivityKind.BrowserUse, 3),
            watchForAdoption: ["ctrl+tab", "ctrl+shift+tab"],
            titleKey: "rule.tabSwitching.title",
            bodyKey: "rule.tabSwitching.body",
            summaryKey: "rule.tabSwitching.summary",
            keyCaps:
            [
                [KeyCap.Modifier("Ctrl"), new KeyCap("Tab")],
                [KeyCap.Modifier("Ctrl"), KeyCap.Modifier("Shift"), new KeyCap("Tab")],
            ],
            symbol: "⇄",
            cooldownDays: 30),
    ];

    public static SuggestionRule? Rule(string id) => All.FirstOrDefault(rule => rule.Id == id);
}
