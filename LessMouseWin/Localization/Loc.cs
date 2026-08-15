using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace LessMouseWin.Localization;

/// <summary>
/// Localization façade. Strings live in embedded JSON files
/// (Localization/en.json, Localization/zh-Hans.json). Language follows the
/// Windows display language by default and can be overridden in Settings.
/// </summary>
public static class Loc
{
    private const string OverrideKey = "language";
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = [];
    private static string _current = "en";
    private static string? _override;

    public static IReadOnlyList<string> Available { get; } = ["en", "zh-Hans"];

    public static event Action? LanguageChanged;

    public static string Current => _current;

    public static string? Override
    {
        get => _override;
        set
        {
            if (_override == value) return;
            _override = value;
            Resolve();
            LanguageChanged?.Invoke();
        }
    }

    public static void Initialize(string? overrideCode = null)
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var code in Available)
        {
            using var stream = assembly.GetManifestResourceStream($"LessMouseWin.Localization.{code}.json");
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var table = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
                        ?? new Dictionary<string, string>();
            Tables[code] = table;
        }

        _override = overrideCode;
        Resolve();
    }

    private static void Resolve()
    {
        if (_override is not null && Tables.ContainsKey(_override))
        {
            _current = _override;
            return;
        }

        var ui = CultureInfo.CurrentUICulture.Name;
        _current = ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && Tables.ContainsKey("zh-Hans")
            ? "zh-Hans"
            : "en";
    }

    public static string DisplayName(string code) =>
        code switch
        {
            "zh-Hans" => "中文",
            _ => "English",
        };

    public static string T(string key)
    {
        if (Tables.TryGetValue(_current, out var table) && table.TryGetValue(key, out var value))
            return value;
        if (Tables.TryGetValue("en", out var english) && english.TryGetValue(key, out value))
            return value;
        return key;
    }

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);
}
