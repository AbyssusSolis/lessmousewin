namespace LessMouseWin.Models;

/// <summary>
/// Windows virtual-key code → SafeKey. Virtual key codes are keyboard
/// positions, layout-independent, so a combination is stored as
/// "ctrl+c" no matter which keyboard layout is active. The table mirrors
/// the macOS Carbon-keycode table adapted to VK_* constants.
/// </summary>
public static class KeyWhitelist
{
    private static readonly Dictionary<ushort, SafeKey> Table = BuildTable();
    public static readonly IReadOnlyDictionary<string, string> DisplayByToken = BuildTokenDisplay();

    private static Dictionary<ushort, SafeKey> BuildTable()
    {
        var map = new Dictionary<ushort, SafeKey>();

        void Add(ushort vk, string token, bool navigation = false, string? display = null) =>
            map[vk] = new SafeKey(token, navigation, display ?? token.ToUpperInvariant());

        // Navigation keys — safe to count bare, because they carry no
        // character content.
        Add(0x08, "backspace", true, "⌫");             // VK_BACK
        Add(0x09, "tab", true, "Tab");                 // VK_TAB
        Add(0x1B, "esc", true, "Esc");                 // VK_ESCAPE
        Add(0x21, "pageup", true, "PgUp");             // VK_PRIOR
        Add(0x22, "pagedown", true, "PgDn");           // VK_NEXT
        Add(0x23, "end", true, "End");                 // VK_END
        Add(0x24, "home", true, "Home");               // VK_HOME
        Add(0x25, "left", true, "←");                  // VK_LEFT
        Add(0x26, "up", true, "↑");                    // VK_UP
        Add(0x27, "right", true, "→");                 // VK_RIGHT
        Add(0x28, "down", true, "↓");                  // VK_DOWN
        Add(0x2E, "delete", true, "⌦");                 // VK_DELETE

        for (var i = 0; i < 12; i++) Add((ushort)(0x70 + i), $"f{i + 1}", true, $"F{i + 1}");

        // Enter and Space are text-neighbors: dropped bare, but recorded when
        // held with Ctrl/Alt/Win (Ctrl+Enter, Ctrl+Space are shortcuts).
        Add(0x0D, "enter", false, "Enter");            // VK_RETURN
        Add(0x20, "space", false, "Space");            // VK_SPACE

        // Letters (virtual-key positions A..Z).
        for (var vk = 0x41; vk <= 0x5A; vk++)
        {
            var c = (char)('a' + (vk - 0x41));
            Add((ushort)vk, c.ToString(), false, c.ToString().ToUpperInvariant());
        }

        // Digits.
        for (var vk = 0x30; vk <= 0x39; vk++)
            Add((ushort)vk, ((char)vk).ToString(), false, ((char)vk).ToString());

        // Punctuation: token + display glyph.
        Add(0xBA, "semicolon", false, ";");            // VK_OEM_1
        Add(0xBF, "slash", false, "/");                // VK_OEM_2
        Add(0xC0, "grave", false, "`");                // VK_OEM_3
        Add(0xDB, "bracketLeft", false, "[");          // VK_OEM_4
        Add(0xDC, "backslash", false, "\\");           // VK_OEM_5
        Add(0xDD, "bracketRight", false, "]");         // VK_OEM_6
        Add(0xDE, "quote", false, "'");                // VK_OEM_7
        Add(0xBC, "comma", false, ",");                // VK_OEM_COMMA
        Add(0xBE, "period", false, ".");               // VK_OEM_PERIOD
        Add(0xBD, "minus", false, "-");                // VK_OEM_MINUS
        Add(0xBB, "equal", false, "=");                // VK_OEM_PLUS

        return map;
    }

    private static IReadOnlyDictionary<string, string> BuildTokenDisplay()
    {
        var map = new Dictionary<string, string>();
        foreach (var key in Table.Values)
            map.TryAdd(key.Token, key.DisplaySymbol);
        return map;
    }

    public static SafeKey? NamedKey(ushort keyCode) =>
        Table.TryGetValue(keyCode, out var key) ? key : null;

    /// <summary>"ctrl+shift+right" → "Ctrl+Shift+→", with stable modifier order.</summary>
    public static string FormatSignatureDisplay(string storageKey)
    {
        var tokens = storageKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var labels = new List<string>();
        string? keySymbol = null;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "ctrl": labels.Add("Ctrl"); break;
                case "alt": labels.Add("Alt"); break;
                case "win": labels.Add("Win"); break;
                case "shift": labels.Add("Shift"); break;
                default:
                    keySymbol = DisplayByToken.TryGetValue(token, out var display)
                        ? display
                        : token.ToUpperInvariant();
                    break;
            }
        }

        var ordered = new[] { "Ctrl", "Alt", "Win", "Shift" }.Where(labels.Contains);
        return string.Join("+", ordered.Append(keySymbol ?? string.Empty));
    }
}
