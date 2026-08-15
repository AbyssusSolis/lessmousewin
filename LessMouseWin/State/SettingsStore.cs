using System.Text.Json;

namespace LessMouseWin.State;

/// <summary>User preferences, persisted in %APPDATA%\LessMouse\settings.json.</summary>
public sealed class AppSettings
{
    private bool _isPaused;
    private bool _launchAtLogin;
    private string? _language;
    private HashSet<string> _excludedApps = new(StringComparer.OrdinalIgnoreCase);

    public bool IsPaused
    {
        get => _isPaused;
        set { if (_isPaused == value) return; _isPaused = value; Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set { if (_launchAtLogin == value) return; _launchAtLogin = value; Changed?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Explicit language code ("en" / "zh-Hans"), or null to follow Windows.</summary>
    public string? Language
    {
        get => _language;
        set { if (_language == value) return; _language = value; Changed?.Invoke(this, EventArgs.Empty); }
    }

    public IReadOnlyCollection<string> ExcludedApps => _excludedApps;

    public event EventHandler? Changed;

    public bool IsExcluded(string? app) =>
        app is not null && _excludedApps.Contains(app);

    public void Exclude(string app)
    {
        if (_excludedApps.Add(app)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Include(string app)
    {
        if (_excludedApps.Remove(app)) Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void ReplaceExcluded(IEnumerable<string> apps)
    {
        var next = new HashSet<string>(apps, StringComparer.OrdinalIgnoreCase);
        if (next.SetEquals(_excludedApps)) return;
        _excludedApps = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public AppSettings Settings { get; }

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
        Settings = Load();
        Settings.Changed += (_, _) => Save();
    }

    private AppSettings Load()
    {
        var settings = new AppSettings();
        if (!File.Exists(_path)) return settings;
        try
        {
            var data = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_path), JsonOptions);
            if (data is null) return settings;
            settings.IsPaused = data.IsPaused;
            settings.LaunchAtLogin = data.LaunchAtLogin;
            settings.Language = data.Language;
            settings.ReplaceExcluded(data.ExcludedApps ?? []);
        }
        catch
        {
            try { File.Move(_path, _path + ".corrupt", overwrite: true); } catch { }
        }
        return settings;
    }

    public void Save()
    {
        try
        {
            var data = new SettingsFile
            {
                IsPaused = Settings.IsPaused,
                LaunchAtLogin = Settings.LaunchAtLogin,
                Language = Settings.Language,
                ExcludedApps = Settings.ExcludedApps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true);
        }
        catch
        {
            // Preferences are recoverable; never crash on a write failure.
        }
    }

    private sealed class SettingsFile
    {
        public bool IsPaused { get; set; }
        public bool LaunchAtLogin { get; set; }
        public string? Language { get; set; }
        public List<string> ExcludedApps { get; set; } = [];
    }
}
