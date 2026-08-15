using System.Windows.Automation;
using LessMouseWin.Interop;

namespace LessMouseWin.Services;

/// <summary>
/// Best-effort Windows analogue of macOS IsSecureEventInputEnabled().
///
/// Windows has no global "secure input" flag, so two layers are used:
/// 1. A UI Automation focus-changed listener that asks the focused control
///    whether it is a password field (covers WPF PasswordBox, Win32 password
///    edits, UWP PasswordBox and browser password fields that expose UIA).
/// 2. A legacy ES_PASSWORD style check on the focused HWND captured by the
///    keyboard hook (covers classic EDIT controls even when UIA is blocked).
///
/// As on macOS, typed text is never recorded anyway — this guard adds the
/// same "while a password field is focused, observe nothing at all" behavior
/// for combination shortcuts entered into password boxes.
/// </summary>
public sealed class SecureInputTracker : IDisposable
{
    private AutomationFocusChangedEventHandler? _handler;

    public volatile bool IsSecure;

    public void Start()
    {
        if (_handler is not null) return;
        _handler = OnFocusChanged;
        try
        {
            Automation.AddAutomationFocusChangedEventHandler(_handler);
            Refresh(AutomationElement.FocusedElement);
        }
        catch
        {
            // UIA can be disabled by policy or a broken screen reader shim.
            // The legacy style check still runs; tracking must not fail.
        }
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            Refresh(element);
        }
        catch
        {
            // Nothing to do — the next focus change tries again.
        }
    }

    private void Refresh(AutomationElement? element)
    {
        if (element is null)
        {
            IsSecure = false;
            return;
        }

        try
        {
            IsSecure = element.Current.IsPassword;
        }
        catch
        {
            IsSecure = false;
        }
    }

    public bool ShouldBlock(IntPtr focusWindow) =>
        IsSecure || NativeMethods.IsLegacyPasswordControl(focusWindow);

    public void Dispose()
    {
        // Removing a UIA focus-changed handler can block indefinitely when
        // shutdown is triggered from inside a UI Automation call (or simply
        // during teardown). The tracker lives for the whole process, and the
        // process is about to exit; dropping the delegate reference is
        // sufficient — UIA's own COM registration dies with the process.
        _handler = null;
        IsSecure = false;
    }
}
