namespace LessMouseWin.Detection;

/// <summary>One detectable habit: "N presses of this signature family within T seconds".</summary>
public sealed class PatternSpec
{
    public string Id { get; }
    public IReadOnlySet<string> Signatures { get; }
    public int Count { get; }
    public double Window { get; }

    public PatternSpec(string id, IEnumerable<string> signatures, int count, double window)
    {
        Id = id;
        Signatures = new HashSet<string>(signatures);
        Count = count;
        Window = window;
    }
}

/// <summary>The v1 rule book, thresholds identical to the macOS original.</summary>
public static class PatternLibrary
{
    public static readonly PatternSpec[] Defaults =
    [
        new("backspace-burst", ["backspace"], 5, 2),
        new("harrow-burst", ["left", "right"], 4, 2),
        new("shift-arrow-burst", ["shift+left", "shift+right"], 4, 2),
        new("varrow-burst", ["up", "down"], 12, 3),
    ];
}
