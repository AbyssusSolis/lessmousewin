using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using LessMouseWin.Interop;
using LessMouseWin.Models;

namespace LessMouseWin.Services;

public enum HookStartStatus
{
    Running,
    Failed,
}

public sealed record HookStartResult(HookStartStatus Status, string? Error = null)
{
    public static readonly HookStartResult Running = new(HookStartStatus.Running);
}

/// <summary>
/// Low-level keyboard hook (WH_KEYBOARD_LL) wrapped in its own thread.
///
/// Windows differences handled here:
/// - No Input Monitoring permission exists; a low-level hook either installs
///   or reports the Win32 error directly.
/// - Windows key repeat is a stream of WM_KEYDOWN messages with no autorepeat
///   bit, so repeats are detected by tracking which keys are already down.
/// - The callback captures the foreground PID and focused HWND immediately,
///   then hands a tiny immutable object to the WPF dispatcher. All real work
///   happens on the UI thread, never in the hook callback.
/// </summary>
public sealed class KeyboardMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<RawKeyEvent> _onEvent;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private static readonly double TickFrequency = Stopwatch.Frequency;

    private readonly object _stateLock = new();
    private Thread? _thread;
    private uint _osThreadId;
    private IntPtr _hook;
    private NativeMethods.LowLevelKeyboardProc? _callback;
    private readonly HashSet<ushort> _downKeys = [];

    private static KeyboardMonitor? _current;

    public KeyboardMonitor(Dispatcher dispatcher, Action<RawKeyEvent> onEvent)
    {
        _dispatcher = dispatcher;
        _onEvent = onEvent;
    }

    public HookStartResult Start()
    {
        Stop();

        using var boot = new ManualResetEventSlim(false);
        HookStartResult result = new(HookStartStatus.Failed, "hook thread did not report");
        var thread = new Thread(() => HookThread(boot, box => result = box))
        {
            Name = "lm.keyboardhook",
            IsBackground = true,
        };
        _thread = thread;
        thread.Start();
        boot.Wait();
        return result;
    }

    private void HookThread(ManualResetEventSlim boot, Action<HookStartResult> report)
    {
        try
        {
            _current = this;
            _osThreadId = NativeMethods.GetCurrentThreadId();
            _callback = HookCallback;
            var module = NativeMethods.GetModuleHandleW(null);
            _hook = NativeMethods.SetWindowsHookExW(
                NativeMethods.WhKeyboardLl, _callback, module, 0);

            if (_hook == IntPtr.Zero)
            {
                report(new HookStartResult(HookStartStatus.Failed,
                    $"SetWindowsHookEx failed: {NativeMethods.Win32ErrorMessage()}"));
                boot.Set();
                _current = null;
                return;
            }

            report(HookStartResult.Running);
            boot.Set();

            while (true)
            {
                var ret = GetMessageW(out var msg, IntPtr.Zero, 0, 0);
                if (ret <= 0) break;
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }
        catch (Exception ex)
        {
            report(new HookStartResult(HookStartStatus.Failed, ex.Message));
            boot.Set();
        }
        finally
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            _downKeys.Clear();
            _current = null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookStruct>(lParam);
                var vk = (ushort)data.VkCode;

                if (message is NativeMethods.WmKeydown or NativeMethods.WmSyskeydown)
                {
                    var autorepeat = !_downKeys.Add(vk);
                    if (!autorepeat && !IsModifierKey(vk))
                    {
                        var foreground = NativeMethods.GetForegroundWindow();
                        if (foreground != IntPtr.Zero)
                        {
                            var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out var pid);
                            if (pid != 0 && pid != _ownProcessId)
                            {
                                var focus = NativeMethods.FocusWindow(foregroundThread);
                                var raw = new RawKeyEvent
                                {
                                    Timestamp = Stopwatch.GetTimestamp() / TickFrequency,
                                    VkCode = vk,
                                    Modifiers = ReadModifiers(),
                                    IsAutorepeat = false,
                                    ForegroundProcessId = pid,
                                    FocusWindow = focus,
                                };
                                Schedule(raw);
                            }
                        }
                    }
                }
                else if (message is NativeMethods.WmKeyup or NativeMethods.WmSyskeyup)
                {
                    _downKeys.Remove(vk);
                }
            }
        }
        catch
        {
            // A hook callback must never throw. If anything above fails, the
            // event simply isn't recorded; the next one will try again.
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Schedule(RawKeyEvent raw)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () => _onEvent(raw));
    }

    private static bool IsModifierKey(ushort vk) =>
        vk == NativeMethods.VkShift ||
        vk == NativeMethods.VkControl ||
        vk == NativeMethods.VkMenu ||
        vk == NativeMethods.VkLwin ||
        vk == NativeMethods.VkRwin;

    private static ModifierSet ReadModifiers()
    {
        var modifiers = ModifierSet.None;
        if (NativeMethods.IsDown(NativeMethods.VkControl)) modifiers |= ModifierSet.Ctrl;
        if (NativeMethods.IsDown(NativeMethods.VkMenu)) modifiers |= ModifierSet.Alt;
        if (NativeMethods.IsDown(NativeMethods.VkLwin) || NativeMethods.IsDown(NativeMethods.VkRwin)) modifiers |= ModifierSet.Win;
        if (NativeMethods.IsDown(NativeMethods.VkShift)) modifiers |= ModifierSet.Shift;
        return modifiers;
    }

    public void Stop()
    {
        Thread? thread;
        uint threadId;
        lock (_stateLock)
        {
            thread = _thread;
            threadId = _osThreadId;
            _thread = null;
            _osThreadId = 0;
        }

        if (thread is null || !thread.IsAlive) return;
        if (threadId != 0)
            NativeMethods.PostThreadMessageW(threadId, NativeMethods.WmQuit, UIntPtr.Zero, IntPtr.Zero);

        thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Win32Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Win32Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref Win32Msg lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Win32Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }
}
