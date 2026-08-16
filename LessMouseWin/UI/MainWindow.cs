using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LessMouseWin.Interop;
using LessMouseWin.State;
using LessMouseWin.UI.Pages;

namespace LessMouseWin.UI;

/// <summary>
/// The popup window — the Windows analogue of the macOS MenuBarExtra panel.
/// It lives in the tray, anchors to the tray icon's rectangle on left-click
/// and hides when it loses focus, just like a menu bar popover.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly AppState _state;
    private readonly ScrollViewer _scroller;
    private readonly Border _surface;
    private readonly Border _scrollFade;
    // The window is 380 DIPs wide with a 12 DIP shadow margin and a 1 DIP
    // border on each side, leaving exactly 354 DIPs for the ScrollViewer.
    private const double PageViewportWidth = Ui.PopupWidth - 26;
    private const double ChromeHeight = 26;

    private readonly Action? _quitAction;
    private enum TaskbarAnchor
    {
        Top,
        Bottom,
    }

    private readonly record struct MonitorLayout(Rect WorkDip, double Scale);

    private PopupRoute _route = PopupRoute.Main;
    private string? _detailRuleId;
    private IPage? _page;
    private bool _allowClose;
    private bool _repositionAfterFit;

    /// <summary>
    /// Provides the tray icon's screen rectangle (pixels). Set by App after
    /// the tray service exists; the popup anchors to the tray icon instead
    /// of following the mouse cursor.
    /// </summary>
    public Func<System.Drawing.Rectangle?>? TrayIconRectProvider { get; set; }

    public MainWindow(AppState state, Action? quitAction = null)
    {
        _state = state;
        _quitAction = quitAction;

        Width = Ui.PopupWidth;
        FontFamily = Ui.UiFont;
        FontSize = 13;
        FontWeight = FontWeights.Normal;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowActivated = true;

        var shadow = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 24,
            ShadowDepth = 2,
            Opacity = 0.35,
        };
        _surface = new Border
        {
            Background = Palette.BackgroundBrush,
            // Hairline edge only — the shadow does the separation work.
            BorderBrush = Palette.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Effect = shadow,
            Margin = new Thickness(12),
        };
        _scroller = new ScrollViewer
        {
            // Hidden (not Disabled): the page still follows the mouse wheel,
            // but no scrollbar is ever drawn on the right edge.
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };

        // With the scrollbar hidden, a soft bottom fade is the only signal
        // that more content exists. It disappears at the bottom.
        _scrollFade = new Border
        {
            Height = 22,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        var surfaceContent = new Grid();
        surfaceContent.Children.Add(_scroller);
        surfaceContent.Children.Add(_scrollFade);
        _surface.Child = surfaceContent;
        Content = _surface;
        _scroller.ScrollChanged += (_, _) => UpdateScrollFade();
        _scroller.SizeChanged += (_, _) => UpdateScrollFade();

        Deactivated += (_, _) =>
        {
            Hide();
            _state.PopoverDidClose();
        };
        Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true;
            Hide();
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Escape) Hide();
        };

        BuildPage();
    }

    public void ShowPopup(PopupRoute route = PopupRoute.Main, string? ruleId = null)
    {
        // Once shutdown has started, this window must never be shown again.
        if (_allowClose) return;
        _route = route;
        _detailRuleId = ruleId;
        BuildPage();
        if (!IsVisible) Show();
        Activate();
        _repositionAfterFit = true;
        PositionNearTrayIcon();
        _state.PopoverDidOpen();
    }

    public void RefreshPage()
    {
        if (!IsVisible) return;
        _page?.RefreshDynamic();
    }

    public void RebuildPage()
    {
        _surface.Background = Palette.BackgroundBrush;
        _surface.BorderBrush = Palette.BorderBrush;
        BuildPage();
        UpdateScrollFade();
    }

    private void BuildPage()
    {
        _page = _route switch
        {
            PopupRoute.Stats => new StatsPage(_state, () => ShowPopup(PopupRoute.Main)),
            PopupRoute.Settings => new SettingsPage(_state, () => ShowPopup(PopupRoute.Main)),
            _ => new MainPage(_state,
                ruleId => ShowPopup(PopupRoute.Main, ruleId),
                () => ShowPopup(PopupRoute.Stats),
                () => ShowPopup(PopupRoute.Settings),
                RequestQuit),
        };

        if (_route == PopupRoute.Main && _detailRuleId is not null)
            _page = new DetailPage(_state, _detailRuleId, () => ShowPopup(PopupRoute.Main));

        _scroller.Content = _page.Content;
        FitToPage();
    }

    /// <summary>
    /// Sizes the window so the page is always fully visible without a
    /// scrollbar when it fits, and when it genuinely needs to scroll the
    /// window grows by the scrollbar width so the content is never clipped
    /// horizontally ("有滚轮就显示不全" fixed by keeping the viewport width
    /// constant in both cases).
    /// </summary>
    public void FitToPage()
    {
        if (_page is null) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_page is null) return;
            _page.Content.Measure(new Size(PageViewportWidth, double.PositiveInfinity));
            var desiredHeight = _page.Content.DesiredSize.Height;

            // No visible scrollbar means the viewport width never changes;
            // keep the window at the designed 380 DIPs in every state.
            Width = Ui.PopupWidth;
            var previousHeight = Height;
            var previousTop = Top;
            var maxByWorkArea = Math.Max(240, GetWorkArea().Height - 12);
            var maxHeight = Math.Min(Ui.MaxPopupHeight, maxByWorkArea);
            var newHeight = Math.Clamp(desiredHeight + ChromeHeight, 240, maxHeight);

            if (_repositionAfterFit && IsVisible)
            {
                // First appearance: the tray anchor owns the position.
                Height = newHeight;
                _repositionAfterFit = false;
                PositionNearTrayIcon();
            }
            else
            {
                Height = newHeight;

                // Subsequent size changes (for example a language switch)
                // grow away from the taskbar instead of always downward:
                // taskbar at the bottom -> keep the bottom edge fixed and
                // expand upward; taskbar at the top -> keep the top edge
                // fixed and expand downward.
                if (IsVisible && !double.IsNaN(previousHeight) &&
                    Math.Abs(Height - previousHeight) > 0.5)
                {
                    if (GetTaskbarAnchor() == TaskbarAnchor.Bottom)
                    {
                        Top = previousTop + previousHeight - Height;
                    }
                    ClampToWorkArea();
                }
            }
        });
    }

    /// <summary>
    /// Detects which screen edge the taskbar occupies, using the tray icon
    /// rectangle first (it lies on the taskbar itself) and the primary work
    /// area as fallback.
    /// </summary>
    private TaskbarAnchor GetTaskbarAnchor()
    {
        var work = GetWorkArea();
        var scale = GetMonitorLayout()?.Scale ?? CurrentScale();

        if (TrayIconRectProvider?.Invoke() is { } trayRect)
        {
            var top = trayRect.Top / scale;
            var bottom = trayRect.Bottom / scale;
            if (top >= work.Bottom - 4) return TaskbarAnchor.Bottom;
            if (bottom <= work.Top + 4) return TaskbarAnchor.Top;

            // Side taskbar (or auto-hidden taskbar): anchor vertically away
            // from the half of the screen the tray icon sits in.
            var centerY = (top + bottom) / 2.0;
            return centerY >= work.Top + work.Height / 2.0
                ? TaskbarAnchor.Bottom
                : TaskbarAnchor.Top;
        }

        if (work.Bottom < SystemParameters.PrimaryScreenHeight - 1)
            return TaskbarAnchor.Bottom;
        if (work.Top > 0)
            return TaskbarAnchor.Top;
        return TaskbarAnchor.Bottom;
    }

    private void UpdateScrollFade()
    {
        var scrollable = _scroller.ScrollableHeight > 0.5;
        var atBottom = _scroller.VerticalOffset >= _scroller.ScrollableHeight - 1.0;
        if (!scrollable || atBottom)
        {
            _scrollFade.Visibility = Visibility.Collapsed;
            return;
        }

        var color = Palette.BackgroundBrush.ToColor();
        _scrollFade.Background = new LinearGradientBrush(
            Color.FromArgb(0, color.R, color.G, color.B),
            Color.FromArgb(230, color.R, color.G, color.B),
            new Point(0, 0), new Point(0, 1));
        _scrollFade.Visibility = Visibility.Visible;
    }

    private void ClampToWorkArea()
    {
        var work = GetWorkArea();
        var maxLeft = Math.Max(work.Left + 8, work.Right - Width - 8);
        var maxTop = Math.Max(work.Top + 8, work.Bottom - Height - 8);
        Left = Math.Clamp(Left, work.Left + 8, maxLeft);
        Top = Math.Clamp(Top, work.Top + 8, maxTop);
    }

    private Rect GetWorkArea() => GetMonitorLayout()?.WorkDip ?? SystemParameters.WorkArea;

    private double CurrentScale()
    {
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        return scale > 0 ? scale : 1.0;
    }

    /// <summary>
    /// Resolves the tray icon's monitor and returns that monitor's work area
    /// and DPI scale. This keeps positioning correct on mixed-DPI multi-monitor
    /// setups instead of assuming the primary screen's metrics.
    /// </summary>
    private MonitorLayout? GetMonitorLayout()
    {
        if (TrayIconRectProvider?.Invoke() is not { } trayRect) return null;

        var rc = new NativeMethods.Rect
        {
            Left = trayRect.Left,
            Top = trayRect.Top,
            Right = trayRect.Right,
            Bottom = trayRect.Bottom,
        };
        var monitor = NativeMethods.MonitorFromRect(ref rc, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return null;

        var info = new NativeMethods.MonitorInfo { CbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfoW(monitor, ref info)) return null;

        var scale = CurrentScale();
        if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdttEffectiveDpi, out var dpiX, out _) == 0)
            scale = dpiX / 96.0;
        if (scale <= 0) scale = 1.0;

        var work = new Rect(
            info.RcWork.Left / scale,
            info.RcWork.Top / scale,
            (info.RcWork.Right - info.RcWork.Left) / scale,
            (info.RcWork.Bottom - info.RcWork.Top) / scale);
        return new MonitorLayout(work, scale);
    }

    /// <summary>Called before Application.Shutdown so the popup's cancel-on-close doesn't block exit.</summary>
    public void AllowClose()
    {
        _allowClose = true;
    }

    private void RequestQuit()
    {
        // The application owns the quit sequence (flush, dispose the tray,
        // stop the hook, then Environment.Exit). Closing this popup during
        // shutdown is handled there via AllowClose(), so the window can
        // never reach the "closed window shown again" state.
        if (_quitAction is not null)
        {
            _quitAction();
            return;
        }

        _allowClose = true;
        Application.Current.Shutdown();
    }

    private void PositionNearTrayIcon()
    {
        var layout = GetMonitorLayout();
        var work = layout?.WorkDip ?? SystemParameters.WorkArea;
        var scale = layout?.Scale ?? CurrentScale();

        var rect = TrayIconRectProvider?.Invoke();
        double left;
        double top;

        if (rect is { } trayRect)
        {
            // Shell_NotifyIconGetRect returns physical pixels; Window Left/Top
            // are DIPs, so convert with the actual monitor scale factor.
            var anchorX = (trayRect.Left + trayRect.Width / 2.0) / scale;
            var anchorTop = trayRect.Top / scale;
            var anchorBottom = trayRect.Bottom / scale;

            left = Math.Clamp(anchorX - Width / 2.0,
                work.Left + 8, Math.Max(work.Left + 8, work.Right - Width - 8));
            if (anchorBottom + 8 + Height <= work.Bottom)
                top = anchorBottom + 8;
            else
                top = anchorTop - Height - 8;
            top = Math.Clamp(top, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - Height - 8));
        }
        else
        {
            // Fallback: bottom-right corner above the taskbar.
            left = work.Right - Width - 12;
            top = work.Bottom - Height - 12;
        }

        Left = left;
        Top = top;
    }
}
