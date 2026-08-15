namespace LessMouseWin.Models;

/// <summary>
/// The normalized, privacy-filtered form of a keystroke — the only thing the
/// store ever persists. Storage keys stay human-readable ("ctrl+shift+right")
/// so stats.json can be audited by opening it.
/// </summary>
public sealed class KeySignature
{
    public ModifierSet Modifiers { get; }
    public SafeKey Key { get; }

    public KeySignature(ModifierSet modifiers, SafeKey key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public string StorageKey
    {
        get
        {
            var tokens = Modifiers.StorageTokens();
            return tokens.Length == 0 ? Key.Token : string.Join("+", tokens.Append(Key.Token));
        }
    }

    public string DisplayLabel =>
        string.Join("+", Modifiers.DisplayLabels().Append(Key.DisplaySymbol));
}

/// <summary>
/// The single choke point every keystroke passes through. Same three rules
/// as the macOS original:
/// 1. Autorepeat never becomes a signature.
/// 2. A non-navigation key without Ctrl/Alt/Win is text — dropped
///    (Shift+letter is just an uppercase letter).
/// 3. Everything else becomes a signature.
/// </summary>
public static class KeySignatureFilter
{
    public static KeySignature? Signature(KeyEvent keyEvent)
    {
        if (keyEvent.IsAutorepeat) return null;

        var key = KeyWhitelist.NamedKey(keyEvent.KeyCode) ?? SafeKey.Raw(keyEvent.KeyCode);

        if (!keyEvent.Modifiers.HasCommandModifier() && !key.IsNavigation) return null;

        return new KeySignature(keyEvent.Modifiers, key);
    }
}
