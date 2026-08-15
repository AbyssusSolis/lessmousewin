using System.Runtime.InteropServices;
using LessMouseWin.Interop;

namespace LessMouseWin.Services;

/// <summary>
/// Tracks the foreground process via EVENT_SYSTEM_FOREGROUND — the Windows
/// analogue of NSWorkspace.didActivateApplicationNotification. The WinEvent
/// hook is installed on the WPF main thread, so its callback arrives on that
/// thread's message loop.
/// </summary>
public sealed class ForegroundTracker : IDisposable
{
    private IntPtr _hook;
    private NativeMethods.WinEventProc? _callback;
    private Action<string?>? _onForeground;
    private uint _lastForegroundPid;

    public void Start(Action<string?> onForeground)
    {
        _onForeground = onForeground;
        _callback = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WineventOutOfContext);

        // Prime the cache with the current foreground process without
        // counting it as an activation (macOS parity: the initial
        // frontmost-app snapshot is a baseline, not an event).
        var initial = NativeMethods.ForegroundProcessId();
        _lastForegroundPid = initial;
        ProcessNameResolver.Resolve(initial);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            if (idObject != 0 || idChild != 0) return;
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            Notify(pid);
        }
        catch
        {
            // A foreground event must never take the app down.
        }
    }

    private void Notify(uint processId)
    {
        if (processId == 0 || processId == _lastForegroundPid) return;
        _lastForegroundPid = processId;

        if (ProcessNameResolver.IsOwnProcess(processId)) return;
        var name = ProcessNameResolver.Resolve(processId);
        _onForeground?.Invoke(name);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        _callback = null;
    }
}
