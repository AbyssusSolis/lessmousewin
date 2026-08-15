namespace LessMouseWin.Models;

/// <summary>
/// The four modifiers that change what a key means on Windows.
/// Ctrl is the macOS Command analogue; Alt is the Option analogue; Win is
/// the Windows-key modifier; Shift alone never turns text into a command.
/// </summary>
[Flags]
public enum ModifierSet : byte
{
    None = 0,
    Ctrl = 1 << 0,
    Alt = 1 << 1,
    Win = 1 << 2,
    Shift = 1 << 3,
}

public static class ModifierSetExtensions
{
    /// <summary>Any of Ctrl/Alt/Win turns a stroke from text into a command.</summary>
    public static bool HasCommandModifier(this ModifierSet modifiers) =>
        (modifiers & (ModifierSet.Ctrl | ModifierSet.Alt | ModifierSet.Win)) != 0;

    /// <summary>Stable storage order: ctrl, alt, win, shift, then key token.</summary>
    public static string[] StorageTokens(this ModifierSet modifiers)
    {
        var tokens = new List<string>(4);
        if (modifiers.HasFlag(ModifierSet.Ctrl)) tokens.Add("ctrl");
        if (modifiers.HasFlag(ModifierSet.Alt)) tokens.Add("alt");
        if (modifiers.HasFlag(ModifierSet.Win)) tokens.Add("win");
        if (modifiers.HasFlag(ModifierSet.Shift)) tokens.Add("shift");
        return tokens.ToArray();
    }

    /// <summary>Display order follows the Windows convention (Ctrl+Alt+Win+Shift).</summary>
    public static string[] DisplayLabels(this ModifierSet modifiers)
    {
        var tokens = new List<string>(4);
        if (modifiers.HasFlag(ModifierSet.Ctrl)) tokens.Add("Ctrl");
        if (modifiers.HasFlag(ModifierSet.Alt)) tokens.Add("Alt");
        if (modifiers.HasFlag(ModifierSet.Win)) tokens.Add("Win");
        if (modifiers.HasFlag(ModifierSet.Shift)) tokens.Add("Shift");
        return tokens.ToArray();
    }
}
