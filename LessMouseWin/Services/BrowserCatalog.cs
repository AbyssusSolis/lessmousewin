namespace LessMouseWin.Services;

/// <summary>
/// Windows browsers, keyed by process name (the Windows analogue of bundle
/// ids). Curated; PRs adding missing browsers are welcome.
/// </summary>
public static class BrowserCatalog
{
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc",
        "iexplore", "waterfox", "palemoon", "tor", "librewolf", "chromium",
    };

    public static bool IsBrowser(string? processName) =>
        processName is not null && Browsers.Contains(processName);
}
