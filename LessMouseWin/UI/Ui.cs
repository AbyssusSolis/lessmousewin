using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LessMouseWin.Suggestions;

namespace LessMouseWin.UI;

internal static class Ui
{
    // Geometry follows the macOS original's Metrics: 10pt module radius
    // (16 for the window — a big outer curve wrapping small inner ones),
    // 8pt module gap (tight, so modules tile into one panel rather than
    // drifting into separate cards), rows on a 12/8 grid.
    public const double PopupWidth = 380;
    public const double MaxPopupHeight = 720;
    public const double Gutter = 12;
    public const double ModuleGap = 8;
    public const double Radius = 10;
    public const double RadiusSmall = 6;
    public const double RowPadding = 12;
    public const double GlyphSize = 28;

    public static readonly FontFamily UiFont = new("Microsoft YaHei UI, Segoe UI");
    // Monospace is reserved for the instrument-like elements of the design —
    // keycaps and counted numbers — where fixed-width figures make values
    // scannable and keys look like keys. Body text stays in the UI family.
    public static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");
    // Glyphs come from the symbol face so they render as monochrome line
    // icons instead of color emoji, matching the ink/paper design language.
    public static readonly FontFamily SymbolFont = new("Segoe UI Symbol");

    public static TextBlock Text(string value, double size = 13, FontWeight? weight = null, Brush? brush = null,
        TextWrapping wrap = TextWrapping.NoWrap, TextTrimming trim = TextTrimming.CharacterEllipsis,
        Thickness? margin = null)
    {
        var text = new TextBlock
        {
            Text = value,
            FontFamily = UiFont,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = brush ?? Palette.TextBrush,
            TextWrapping = wrap,
            TextTrimming = trim,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (margin is not null) text.Margin = margin.Value;
        return text;
    }

    public static TextBlock Mono(string value, double size = 11, FontWeight? weight = null, Brush? brush = null)
    {
        var text = Text(value, size, weight, brush);
        text.FontFamily = MonoFont;
        return text;
    }

    public static TextBlock Symbol(string glyph, double size = 13, Brush? brush = null)
    {
        var text = Text(glyph, size, FontWeights.Normal, brush);
        text.FontFamily = SymbolFont;
        return text;
    }

    public static Border Module(params UIElement[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children)
            if (child is not null) stack.Children.Add(child);

        return Module(stack);
    }

    public static Border Module(StackPanel content) => new()
    {
        // The original's Module: a tinted fill and a hairline, never a
        // shadow — the window carries the only shadow in the picture, and
        // modules separate by fill contrast alone.
        Background = Palette.ModuleFillBrush,
        BorderBrush = Palette.ModuleBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(Radius),
        Margin = new Thickness(0, 0, 0, ModuleGap),
        Child = content,
    };

    public static StackPanel VStack(double spacing = 6) => new() { Orientation = Orientation.Vertical };

    public static Grid Glyph(string label, bool on = false)
    {
        var ellipse = new Ellipse
        {
            Width = GlyphSize,
            Height = GlyphSize,
            // The Control Center grammar, exactly two cases: a solid accent
            // disc means "on", a flat gray one "off". The glyph on green is
            // ink, not white — white on #2CDB5C fails contrast.
            Fill = on ? Palette.AccentBrush : Palette.SurfaceAltBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var text = Symbol(label, 12, on ? Palette.OnAccentBrush : Palette.TextSecondaryBrush);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { Width = GlyphSize, Height = GlyphSize };
        grid.Children.Add(ellipse);
        grid.Children.Add(text);
        return grid;
    }

    public static Border Pill(string label, Brush? foreground = null, Brush? background = null, bool uppercase = false)
    {
        var fg = foreground ?? Palette.TextTertiaryBrush;
        // Tinted fill plus a stroke at 35% of the foreground — the original's
        // pill recipe. A neutral pill strokes with the plain border instead.
        var stroke = foreground is null ? Palette.BorderBrush : Faded(fg, 0.35);
        var border = new Border
        {
            Background = background ?? Palette.SurfaceAltBrush,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1),
            // WPF clamps an oversized CornerRadius per-axis (rx→width/2,
            // ry→height/2) and draws an ellipse, not a capsule. The radius
            // must literally be half the height for semicircle ends and a
            // straight middle — the macOS original's Capsule() shape.
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(uppercase ? label.ToUpperInvariant() : label, 11, FontWeights.Medium, fg),
        };
        return border;
    }

    public static Brush Faded(Brush brush, double opacity)
    {
        var faded = new SolidColorBrush(brush.ToColor()) { Opacity = opacity };
        faded.Freeze();
        return faded;
    }

    public static Grid Row(string title, string? subtitle, string glyph, bool glyphOn, UIElement? trailing,
        UIElement? extra = null, Brush? subtitleBrush = null, TextWrapping subtitleWrap = TextWrapping.NoWrap)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GlyphSize) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = Glyph(glyph, glyphOn);
        Grid.SetColumn(badge, 0);
        badge.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(badge);

        var textStack = new StackPanel { Margin = new Thickness(8, 0, 6, 0) };
        // 13 medium — the original's rowTitle, macOS Headline size.
        textStack.Children.Add(Text(title, 13, FontWeights.Medium, Palette.TextBrush));
        if (!string.IsNullOrEmpty(subtitle))
        {
            textStack.Children.Add(Text(subtitle, 11, FontWeights.Normal,
                subtitleBrush ?? Palette.TextTertiaryBrush, subtitleWrap,
                margin: new Thickness(0, 2, 0, 0)));
        }
        if (extra is not null) textStack.Children.Add(extra);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            if (trailing is FrameworkElement element)
                element.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(trailing);
        }

        grid.Margin = new Thickness(RowPadding, 8, RowPadding, 8);
        return grid;
    }

    public static Rectangle Divider(double indent = -1) => new()
    {
        Height = 1,
        Fill = Palette.BorderBrush,
        Margin = new Thickness(indent >= 0 ? indent : RowPadding + GlyphSize + 8, 0, 0, 0),
    };

    public static Grid TitleRow(string title, UIElement? trailing = null)
    {
        // ModuleTitleRow: sentence case at row-title size in full ink — a
        // heading the user reads, not a tracked uppercase eyebrow.
        var grid = new Grid { Margin = new Thickness(RowPadding, 12, RowPadding, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Text(title, 13, FontWeights.Medium, Palette.TextBrush);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 1);
            grid.Children.Add(trailing);
        }
        return grid;
    }

    // Primary action: ink fill — the green accent is for states, not chrome.
    public static Button AccentButton(string label, Action onClick) =>
        StyledButton(label, Palette.OnInkBrush, Palette.InkBrush, Palette.InkHoverBrush, 0, 12, 5, onClick,
            pressNudge: true);

    // Secondary: surface fill and a strong border — a page gets exactly one
    // ink-filled button, anything else must not compete for the eye.
    public static Button SecondaryButton(string label, Action onClick) =>
        StyledButton(label, Palette.TextBrush, Palette.SurfaceBrush, Palette.SurfaceAltBrush, 1, 12, 5, onClick);

    // Ghost action: no border, muted ink, a wash on hover (Windows) and
    // surfaceAlt on press (the original's RowButtonStyle grammar).
    public static Button QuietButton(string label, Action onClick) =>
        StyledButton(label, Palette.TextTertiaryBrush, Brushes.Transparent, Palette.SurfaceHoverBrush, 0, 8, 4, onClick,
            radius: RadiusSmall, size: 11, weight: FontWeights.Medium);

    /// <summary>
    /// A stadium-shaped toggle chip — semicircle ends, straight middle.
    /// Used for mutually exclusive picks like the language selector.
    /// MinHeight and CornerRadius are kept numerically tied so WPF always
    /// draws a true capsule instead of a rounded rectangle.
    /// </summary>
    public static Button Chip(string label, bool selected, Action onClick)
    {
        const double chipHeight = 28;
        const double chipRadius = chipHeight / 2; // 14

        var foreground = selected ? Palette.AccentInkBrush : Palette.TextSecondaryBrush;
        var background = selected ? Palette.AccentSoftBrush : Palette.SurfaceAltBrush;
        var hoverBackground = selected ? Palette.AccentSoftBrush : Palette.SurfaceHoverBrush;
        var border = selected ? Faded(Palette.AccentInkBrush, 0.35) : Brushes.Transparent;

        var button = new Button
        {
            Content = Text(label, 13, selected ? FontWeights.SemiBold : FontWeights.Normal, foreground),
            MinHeight = chipHeight,
            Padding = new Thickness(14, 0, 14, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = border,
            Background = background,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var template = new ControlTemplate(typeof(Button));
        var capsule = new FrameworkElementFactory(typeof(Border));
        capsule.Name = "bg";
        capsule.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        capsule.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        capsule.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        capsule.SetValue(Border.CornerRadiusProperty, new CornerRadius(chipRadius));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        capsule.AppendChild(presenter);
        template.VisualTree = capsule;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
        template.Triggers.Add(pressed);

        button.Template = template;
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// The circular back button used by page headers: a hand-drawn vector
    /// chevron (round-cap stroke, no font dependence) inside a disc that
    /// washes in on hover.
    /// </summary>
    public static Button BackButton(Action onClick)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(10.5, 4.5), false, false);
            context.LineTo(new Point(4.5, 10.5), true, false);
            context.LineTo(new Point(10.5, 16.5), true, false);
        }
        geometry.Freeze();
        var chevron = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = Palette.TextSecondaryBrush,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.None,
            Width = 15,
            Height = 21,
        };

        var button = new Button
        {
            Width = 28,
            Height = 28,
            Content = chevron,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = Localization.Loc.T("common.back"),
        };
        System.Windows.Automation.AutomationProperties.SetName(button, Localization.Loc.T("common.back"));

        var template = new ControlTemplate(typeof(Button));
        var background = new FrameworkElementFactory(typeof(Border));
        background.Name = "bg";
        background.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        background.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        background.AppendChild(presenter);
        template.VisualTree = background;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Palette.SurfaceHoverBrush, "bg"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, Palette.SurfaceAltBrush, "bg"));
        template.Triggers.Add(pressed);
        button.Template = template;
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A transparent button whose background washes in on hover — for clickable list rows.</summary>
    public static Button Hoverable(UIElement content, Action onClick)
    {
        var button = new Button
        {
            Content = content,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        var template = new ControlTemplate(typeof(Button));
        var background = new FrameworkElementFactory(typeof(Border));
        background.Name = "bg";
        background.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        background.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        background.AppendChild(presenter);
        template.VisualTree = background;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Palette.SurfaceHoverBrush, "bg"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, Palette.SurfaceAltBrush, "bg"));
        template.Triggers.Add(pressed);
        button.Template = template;
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button StyledButton(string label, Brush foreground, Brush background, Brush hover,
        double borderThickness, double horizontalPadding, double verticalPadding, Action onClick,
        double radius = Radius, double size = 13, FontWeight? weight = null, bool pressNudge = false)
    {
        var button = new Button
        {
            Content = Text(label, size, weight ?? FontWeights.Medium, foreground),
            Background = background,
            Foreground = foreground,
            BorderBrush = Palette.BorderStrongBrush,
            BorderThickness = new Thickness(borderThickness),
            Padding = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, background));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, borderThickness > 0 ? Palette.BorderStrongBrush : Brushes.Transparent));
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hover));
        style.Triggers.Add(hoverTrigger);
        var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hover));
        style.Triggers.Add(pressedTrigger);
        if (pressNudge)
        {
            // The original's AccentButtonStyle sinks 1pt while pressed.
            var nudge = new Trigger { Property = Button.IsPressedProperty, Value = true };
            nudge.Setters.Add(new Setter(UIElement.RenderTransformProperty, new TranslateTransform(0, 1), "bd"));
            template.Triggers.Add(nudge);
        }
        button.Style = style;
        button.Template = template;
        button.Click += (_, _) => onClick();
        return button;
    }

    private static TextBlock CreateCapText(KeyCap cap)
    {
        var text = Mono(cap.Label, 11, FontWeights.Normal, Palette.TextBrush);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        return text;
    }

    public static StackPanel KeyCapRow(IEnumerable<KeyCap> caps, Thickness? margin = null)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        if (margin is not null) stack.Margin = margin.Value;
        foreach (var cap in caps)
        {
            // The house language applied to hardware: surface fill, one
            // strong hairline, no shadow, mono type — a flat key, not a
            // glossy 3-D key render (this panel has zero shadows to spend).
            var border = new Border
            {
                Background = Palette.SurfaceBrush,
                BorderBrush = Palette.BorderStrongBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                MinWidth = cap.Kind == KeyCapKind.Modifier ? 34 : 22,
                MinHeight = 22,
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 3, 0),
                SnapsToDevicePixels = true,
                Child = CreateCapText(cap),
            };
            stack.Children.Add(border);
        }
        return stack;
    }

    public static Border Meter(double fraction, Brush? fill = null)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var outer = new Border
        {
            Height = 4,
            Background = Palette.SurfaceAltBrush,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(fraction, 0.0001), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - fraction, 0.0001), GridUnitType.Star) });
        outer.Child = grid;
        var bar = new Border
        {
            Background = fill ?? Palette.AccentBrush,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 3,
        };
        Grid.SetColumn(bar, 0);
        grid.Children.Add(bar);
        return outer;
    }

    public static void UpdateMeter(Border meter, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (meter.Child is not Grid grid || grid.ColumnDefinitions.Count != 2) return;
        grid.ColumnDefinitions[0].Width = new GridLength(Math.Max(fraction, 0.0001), GridUnitType.Star);
        grid.ColumnDefinitions[1].Width = new GridLength(Math.Max(1 - fraction, 0.0001), GridUnitType.Star);
    }

    /// <summary>
    /// A switch, not a stock checkbox: a capsule track with a sliding knob.
    /// The stock WPF check square clashes with every other control here.
    /// </summary>
    public static CheckBox Toggle(bool isChecked, Action<bool> onChanged)
    {
        var check = new CheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        var template = new ControlTemplate(typeof(CheckBox));
        var track = new FrameworkElementFactory(typeof(Border));
        track.Name = "track";
        track.SetValue(Border.WidthProperty, 36.0);
        track.SetValue(Border.HeightProperty, 20.0);
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        track.SetValue(Border.BackgroundProperty, Palette.SurfaceAltBrush);
        track.SetValue(Border.BorderBrushProperty, Palette.BorderStrongBrush);
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var knob = new FrameworkElementFactory(typeof(Ellipse));
        knob.Name = "knob";
        knob.SetValue(FrameworkElement.WidthProperty, 14.0);
        knob.SetValue(FrameworkElement.HeightProperty, 14.0);
        knob.SetValue(Shape.FillProperty, Brushes.White);
        knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        knob.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        knob.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0));
        track.AppendChild(knob);
        template.VisualTree = track;

        var on = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty, Palette.AccentBrush, "track"));
        on.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.AccentBrush, "track"));
        on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "knob"));
        on.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 2, 0), "knob"));
        template.Triggers.Add(on);

        check.Template = template;
        check.Checked += (_, _) => onChanged(true);
        check.Unchecked += (_, _) => onChanged(false);
        return check;
    }

    public static Grid PageHeader(string title, string glyph, Action onBack)
    {
        var grid = new Grid { Margin = new Thickness(4, 8, 4, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var back = BackButton(onBack);
        back.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(back, 0);
        grid.Children.Add(back);
        var icon = Symbol(glyph, 13, Palette.AccentBrush);
        Grid.SetColumn(icon, 1);
        icon.Margin = new Thickness(8, 0, 6, 0);
        grid.Children.Add(icon);
        // Typo.title: 15 semibold.
        var titleText = Text(title, 15, FontWeights.SemiBold, Palette.TextBrush);
        Grid.SetColumn(titleText, 2);
        grid.Children.Add(titleText);
        return grid;
    }
}
