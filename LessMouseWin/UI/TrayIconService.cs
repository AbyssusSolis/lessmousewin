using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using LessMouseWin.Localization;
using LessMouseWin.Services;
using LessMouseWin.State;

namespace LessMouseWin.UI;

/// <summary>
/// The Windows tray icon — the macOS MenuBarExtra analogue. Left-click opens
/// the popup window; right-click gives the usual tray menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly AppState _state;
    private readonly Action<PopupRoute> _open;
    private readonly Action _quit;
    private readonly NotifyIcon _icon;
    private readonly Stream _iconStream;
    private readonly Stream _unreadIconStream;
    private readonly Icon _defaultIcon;
    private readonly Icon _unreadIcon;
    private bool _alertShown;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _statsItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _dataItem;
    private readonly ToolStripMenuItem _quitItem;
    private readonly ContextMenuStrip _menu;

    public TrayIconService(AppState state, Action<PopupRoute> open, Action quit)
    {
        _state = state;
        _open = open;
        _quit = quit;

        _iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("LessMouseWin.Assets.LessMouse.ico")
            ?? throw new InvalidOperationException("embedded tray icon is missing");
        _unreadIconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("LessMouseWin.Assets.LessMouseUnread.ico")
            ?? throw new InvalidOperationException("embedded unread tray icon is missing");
        _defaultIcon = new Icon(_iconStream);
        _unreadIcon = new Icon(_unreadIconStream);
        _icon = new NotifyIcon
        {
            Icon = _defaultIcon,
            Text = Loc.T("app.name"),
            Visible = true,
        };

        _pauseItem = new ToolStripMenuItem(Loc.T("menu.pause"));
        _openItem = new ToolStripMenuItem(Loc.T("menu.open"), null, (_, _) => _open(PopupRoute.Main));
        _statsItem = new ToolStripMenuItem(Loc.T("menu.stats"), null, (_, _) => _open(PopupRoute.Stats));
        _settingsItem = new ToolStripMenuItem(Loc.T("menu.settings"), null, (_, _) => _open(PopupRoute.Settings));
        _dataItem = new ToolStripMenuItem(Loc.T("menu.showData"), null, (_, _) => OpenDataFolder());
        _quitItem = new ToolStripMenuItem(Loc.T("menu.quit"), null, (_, _) => _quit());
        _menu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9F),
        };
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_pauseItem);
        _pauseItem.Click += (_, _) => state.Settings.IsPaused = !state.Settings.IsPaused;
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_statsItem);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_dataItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_quitItem);

        _icon.ContextMenuStrip = _menu;
        _icon.MouseClick += OnMouseClick;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        try
        {
            if (e.Button == MouseButtons.Left)
                _open(PopupRoute.Main);
        }
        catch
        {
            // The window can never be reused after a real close; swallowing
            // a late tray click is safer than showing an error dialog.
        }
    }

    public void Refresh()
    {
        var status = _state.Settings.IsPaused
            ? Loc.T("tracking.paused")
            : _state.IsTracking
                ? Loc.T("tracking.active")
                : Loc.T("tracking.failed");
        var unread = _state.UnreadCount > 0 ? $" · {Loc.Format("inbox.unread", _state.UnreadCount)}" : "";
        _icon.Text = $"{Loc.T("app.name")} · {status}{unread}";

        var alert = _state.UnreadCount > 0 || _state.Celebration is not null;
        if (alert != _alertShown)
        {
            _alertShown = alert;
            _icon.Icon = alert ? _unreadIcon : _defaultIcon;
        }
        _pauseItem.Text = _state.Settings.IsPaused ? Loc.T("menu.resume") : Loc.T("menu.pause");
        _openItem.Text = Loc.T("menu.open");
        _statsItem.Text = Loc.T("menu.stats");
        _settingsItem.Text = Loc.T("menu.settings");
        _dataItem.Text = Loc.T("menu.showData");
        _quitItem.Text = Loc.T("menu.quit");
    }

    private const uint NimDelete = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint UID;
        public uint UFlags;
        public uint UCallbackMessage;
        public IntPtr HIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SzTip;
        public uint DwState;
        public uint DwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string SzInfo;
        public uint UTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string SzInfoTitle;
        public uint DwInfoFlags;
        public Guid GuidItem;
        public IntPtr HBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NotifyIconData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint UID;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out Win32Rect iconLocation);

    public static void OpenDataFolder()
    {
        var path = AppPaths.DataDirectory;
        Directory.CreateDirectory(path);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Folder opening is best-effort.
        }
    }

    /// <summary>
    /// Returns the screen rectangle of the tray icon. This is the anchor the
    /// popup should align to (the Windows equivalent of a macOS menu bar
    /// item), not the current mouse position.
    /// </summary>
    public Rectangle? GetIconRect()
    {
        if (!TryGetNativeIcon(out var window, out var id)) return null;
        try
        {
            var identifier = new NotifyIconIdentifier
            {
                CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                HWnd = window.Handle,
                UID = id,
                GuidItem = Guid.Empty,
            };
            if (Shell_NotifyIconGetRect(ref identifier, out var rect) == 0)
                return new Rectangle(rect.Left, rect.Top,
                    Math.Max(0, rect.Right - rect.Left),
                    Math.Max(0, rect.Bottom - rect.Top));
        }
        catch
        {
        }
        return null;
    }

    private bool TryGetNativeIcon(out NativeWindow window, out uint id)
    {
        window = null!;
        id = 0;
        try
        {
            var windowField = typeof(NotifyIcon).GetField("_window",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var idField = typeof(NotifyIcon).GetField("_id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (windowField?.GetValue(_icon) is NativeWindow nativeWindow &&
                idField?.GetValue(_icon) is uint nativeId)
            {
                window = nativeWindow;
                id = nativeId;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    /// <summary>
    /// Sends Shell_NotifyIcon(NIM_DELETE) directly in addition to the WinForms
    /// Visible=false path. Windows occasionally leaves a ghost icon in the
    /// notification area when the process exits immediately after disposal;
    /// an explicit delete avoids that.
    /// </summary>
    private void DeleteTrayIconExplicitly()
    {
        try
        {
            if (TryGetNativeIcon(out var window, out var id))
            {
                var data = new NotifyIconData
                {
                    CbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                    HWnd = window.Handle,
                    UID = id,
                };
                Shell_NotifyIconW(NimDelete, ref data);
            }
        }
        catch
        {
            // Best effort; Visible=false below is the regular removal path.
        }
    }

    public void Dispose()
    {
        DeleteTrayIconExplicitly();
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _iconStream.Dispose();
        _unreadIconStream.Dispose();
        _defaultIcon.Dispose();
        _unreadIcon.Dispose();
    }
}
