using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LessMouseWin.UI;

/// <summary>
/// Design tokens from the macOS original (ink, paper, one green accent),
/// reimplemented as WPF brushes with Windows light/dark-mode detection.
/// </summary>
public static class Palette
{
    private static Color _background;
    private static Color _surface;
    private static Color _surfaceAlt;
    private static Color _surfaceHover;
    private static Color _border;
    private static Color _borderStrong;
    private static Color _text;
    private static Color _textSecondary;
    private static Color _textTertiary;
    private static Color _accent;
    private static Color _accentInk;
    private static Color _accentSoft;
    private static Color _onAccent;
    private static Color _ink;
    private static Color _inkHover;
    private static Color _onInk;
    private static Color _positive;
    private static Color _positiveSoft;
    private static Color _warning;
    private static Color _warningSoft;
    private static Color _danger;
    private static Color _dangerSoft;
    private static Color _moduleFill;
    private static Color _moduleBorder;

    public static bool IsDark { get; private set; }

    public static Brush BackgroundBrush => Frozen(_background);
    public static Brush SurfaceBrush => Frozen(_surface);
    public static Brush SurfaceAltBrush => Frozen(_surfaceAlt);
    public static Brush SurfaceHoverBrush => Frozen(_surfaceHover);
    public static Brush BorderBrush => Frozen(_border);
    public static Brush BorderStrongBrush => Frozen(_borderStrong);
    public static Brush TextBrush => Frozen(_text);
    public static Brush TextSecondaryBrush => Frozen(_textSecondary);
    public static Brush TextTertiaryBrush => Frozen(_textTertiary);
    public static Brush AccentBrush => Frozen(_accent);
    public static Brush AccentInkBrush => Frozen(_accentInk);
    public static Brush AccentSoftBrush => Frozen(_accentSoft);
    public static Brush OnAccentBrush => Frozen(_onAccent);
    public static Brush InkBrush => Frozen(_ink);
    public static Brush InkHoverBrush => Frozen(_inkHover);
    public static Brush OnInkBrush => Frozen(_onInk);
    public static Brush PositiveBrush => Frozen(_positive);
    public static Brush PositiveSoftBrush => Frozen(_positiveSoft);
    public static Brush WarningBrush => Frozen(_warning);
    public static Brush WarningSoftBrush => Frozen(_warningSoft);
    public static Brush DangerBrush => Frozen(_danger);
    public static Brush DangerSoftBrush => Frozen(_dangerSoft);
    public static Brush ModuleFillBrush => Frozen(_moduleFill);
    public static Brush ModuleBorderBrush => Frozen(_moduleBorder);

    static Palette()
    {
        Reload();
    }

    public static void Reload()
    {
        IsDark = IsSystemDark();
        // Values synced 1:1 with the macOS original's Theme.swift ("ink,
        // signal green, and clean paper"). _surfaceHover is the only
        // Windows-only addition — macOS rows give feedback on press, Windows
        // users expect it on hover.
        if (IsDark)
        {
            _background = Hex(0x0B0D0B);
            _surface = Hex(0x141714);
            _surfaceAlt = Hex(0x1C201C);
            _surfaceHover = Hex(0x212621);
            _border = Hex(0x272C28);
            _borderStrong = Hex(0x3B423C);
            _text = Hex(0xF2F4F2);
            _textSecondary = Hex(0xC2C9C3);
            _textTertiary = Hex(0x838D85);
            _accent = Hex(0x2CDB5C);
            _accentInk = Hex(0x4FE47A);
            _accentSoft = Hex(0x11361D);
            _onAccent = Hex(0x07230F);
            _ink = Hex(0xF2F4F2);
            _inkHover = Hex(0xD9DDD9);
            _onInk = Hex(0x0B0D0B);
            _positive = Hex(0x4FE47A);
            _positiveSoft = Hex(0x4FE47A, 0.25);
            _warning = Hex(0xEAB308);
            _warningSoft = Hex(0xEAB308, 0.25);
            _danger = Hex(0xF87171);
            _dangerSoft = Hex(0xF87171, 0.25);
            _moduleFill = Hex(0xFFFFFF, 0.07);
            _moduleBorder = Hex(0xFFFFFF, 0.10);
        }
        else
        {
            _background = Hex(0xF7F8F7);
            _surface = Hex(0xFFFFFF);
            _surfaceAlt = Hex(0xEFF1EF);
            _surfaceHover = Hex(0xF1F3F1);
            _border = Hex(0xE3E6E3);
            _borderStrong = Hex(0xC6CCC7);
            _text = Hex(0x0B0F0C);
            _textSecondary = Hex(0x3E463F);
            _textTertiary = Hex(0x6D766E);
            _accent = Hex(0x2CDB5C);
            _accentInk = Hex(0x128A38);
            _accentSoft = Hex(0xDFF8E7);
            _onAccent = Hex(0x07230F);
            _ink = Hex(0x111511);
            _inkHover = Hex(0x2A2F2A);
            _onInk = Hex(0xFFFFFF);
            _positive = Hex(0x128A38);
            _positiveSoft = Hex(0x128A38, 0.15);
            _warning = Hex(0xA16207);
            _warningSoft = Hex(0xA16207, 0.15);
            _danger = Hex(0xD92D20);
            _dangerSoft = Hex(0xD92D20, 0.15);
            _moduleFill = Hex(0xFFFFFF, 0.55);
            _moduleBorder = Hex(0xFFFFFF, 0.65);
        }
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Color Hex(uint value, double alpha = 1) =>
        Color.FromArgb(
            (byte)(alpha * 255),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static Color ToColor(this System.Windows.Media.Brush brush) =>
        brush is SolidColorBrush solid ? solid.Color : Colors.Transparent;
}
