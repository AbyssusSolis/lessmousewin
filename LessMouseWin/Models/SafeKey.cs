namespace LessMouseWin.Models;

/// <summary>
/// A key LessMouseWin is willing to name. Navigation keys may be counted
/// bare; named keys (letters/digits/punctuation) only inside Ctrl/Alt/Win
/// combinations. Unknown key codes are opaque ("k91") and can only be
/// recorded inside a combination.
/// </summary>
public sealed record SafeKey(string Token, bool IsNavigation, string DisplaySymbol)
{
    public static SafeKey Raw(ushort keyCode) =>
        new($"k{keyCode}", false, $"k{keyCode}");
}
