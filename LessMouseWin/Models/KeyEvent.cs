namespace LessMouseWin.Models;

/// <summary>One raw keyboard event captured by the low-level hook.</summary>
public sealed class RawKeyEvent
{
    public double Timestamp { get; init; }
    public ushort VkCode { get; init; }
    public ModifierSet Modifiers { get; init; }
    public bool IsAutorepeat { get; init; }
    public uint ForegroundProcessId { get; init; }
    public IntPtr FocusWindow { get; init; }
}

/// <summary>
/// One keystroke after the platform layer has normalized it. The pipeline
/// works with this type; the Win32 hook layer is never seen downstream.
/// </summary>
public sealed class KeyEvent
{
    public double Timestamp { get; init; }
    public ushort KeyCode { get; init; }
    public ModifierSet Modifiers { get; init; }
    public bool IsAutorepeat { get; init; }
    public string? Application { get; init; }
    public IntPtr FocusWindow { get; init; }

    public static KeyEvent FromRaw(RawKeyEvent raw, string? application) =>
        new()
        {
            Timestamp = raw.Timestamp,
            KeyCode = raw.VkCode,
            Modifiers = raw.Modifiers,
            IsAutorepeat = raw.IsAutorepeat,
            Application = application,
            FocusWindow = raw.FocusWindow,
        };
}
