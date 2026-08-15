namespace LessMouseWin.Detection;

/// <summary>
/// Detects burst patterns over a sliding window of recent keystrokes.
/// Semantics match the macOS original, including the inclusive window edge
/// and draining the window when a burst fires.
/// </summary>
public sealed class PatternDetector
{
    private readonly object _lock = new();
    private readonly Dictionary<string, SlidingWindowCounter> _counters;
    private readonly Dictionary<string, PatternSpec> _specsById;
    private readonly Dictionary<string, List<string>> _specIdsBySignature;

    public PatternDetector(IEnumerable<PatternSpec> specs)
    {
        _counters = new Dictionary<string, SlidingWindowCounter>();
        _specsById = new Dictionary<string, PatternSpec>();
        _specIdsBySignature = new Dictionary<string, List<string>>();

        foreach (var spec in specs)
        {
            _counters[spec.Id] = new SlidingWindowCounter(spec.Count, spec.Window);
            _specsById[spec.Id] = spec;
            foreach (var signature in spec.Signatures)
            {
                if (!_specIdsBySignature.TryGetValue(signature, out var list))
                {
                    list = [];
                    _specIdsBySignature[signature] = list;
                }
                list.Add(spec.Id);
            }
        }
    }

    public List<PatternSpec> Feed(string signature, double timestamp)
    {
        var fired = new List<PatternSpec>();
        lock (_lock)
        {
            if (!_specIdsBySignature.TryGetValue(signature, out var specIds)) return fired;
            foreach (var specId in specIds)
            {
                if (!_counters.TryGetValue(specId, out var counter)) continue;
                if (counter.Record(timestamp))
                    fired.Add(_specsById[specId]);
            }
        }
        return fired;
    }

    public void ResetAll()
    {
        lock (_lock)
        {
            foreach (var counter in _counters.Values)
                counter.Reset();
        }
    }
}

internal sealed class SlidingWindowCounter
{
    private readonly List<double> _timestamps = [];
    private readonly int _count;
    private readonly double _window;

    public SlidingWindowCounter(int count, double window)
    {
        _count = count;
        _window = window;
    }

    public bool Record(double t)
    {
        _timestamps.Add(t);
        while (_timestamps.Count > 0 && t - _timestamps[0] > _window)
            _timestamps.RemoveAt(0);

        if (_timestamps.Count >= _count)
        {
            _timestamps.Clear();
            return true;
        }
        return false;
    }

    public void Reset() => _timestamps.Clear();
}
