using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LessMouseWin.Interop;

internal static class NativeMethods
{
    public const int WhKeyboardLl = 13;
    public const int WmKeydown = 0x0100;
    public const int WmKeyup = 0x0101;
    public const int WmSyskeydown = 0x0104;
    public const int WmSyskeyup = 0x0105;
    public const int WmQuit = 0x0012;

    public const uint WineventOutOfContext = 0x0000;
    public const uint EventSystemForeground = 0x0003;

    public const int VkShift = 0x10;
    public const int VkControl = 0x11;
    public const int VkMenu = 0x12;
    public const int VkLwin = 0x5B;
    public const int VkRwin = 0x5C;

    public const int LlkhfExtended = 0x01;
    public const int LlkhfInjected = 0x10;

    public const int GwlStyle = -16;
    public const long EsPassword = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GuiThreadInfo
    {
        public int CbSize;
        public uint Flags;
        public IntPtr HwndActive;
        public IntPtr HwndFocus;
        public IntPtr HwndCapture;
        public IntPtr HwndMenuOwner;
        public IntPtr HwndMoveSize;
        public IntPtr HwndCaret;
        public Rect RcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassNameW(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostThreadMessageW(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime);

    public static bool IsDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

    public static string? WindowClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        var buffer = new char[256];
        var length = GetClassNameW(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : null;
    }

    public static bool IsLegacyPasswordControl(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var className = WindowClassName(hwnd);
        if (string.IsNullOrEmpty(className)) return false;

        // Classic EDIT/RichEdit controls expose ES_PASSWORD in their style.
        var isEdit = className.StartsWith("Edit", StringComparison.OrdinalIgnoreCase)
                     || className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
                     || className.StartsWith("TEdit", StringComparison.OrdinalIgnoreCase);
        if (!isEdit) return false;

        try
        {
            var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
            return (style & EsPassword) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static uint ForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public static IntPtr FocusWindow(uint foregroundThreadId)
    {
        var info = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(foregroundThreadId, ref info) ? info.HwndFocus : IntPtr.Zero;
    }

    public static string? Win32ErrorMessage()
    {
        return new Win32Exception(Marshal.GetLastWin32Error()).Message;
    }
}
