using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LessMouseWin.Suggestions;

namespace LessMouseWin.UI;

internal static class Ui
{
    public const double PopupWidth = 380;
    public const double MaxPopupHeight = 720;
    public const double Gutter = 12;
    public const double ModuleGap = 12;
    public const double Radius = 10;
    public const double RowPadding = 12;
    public const double GlyphSize = 28;

    public static readonly FontFamily UiFont = new("Microsoft YaHei UI, Segoe UI");
    // The original macOS design used monospace for counted numbers. On
    // Windows, mixing Segoe UI and Consolas reads as two different fonts in a
    // 360 DIP panel, so the port uses one family everywhere and keeps the
    // visual rhythm through size and weight only.
    public static readonly FontFamily MonoFont = UiFont;

    public static TextBlock Text(string value, double size = 13, FontWeight? weight = null, Brush? brush = null,
        TextWrapping wrap = TextWrapping.NoWrap, TextTrimming trim = TextTrimming.CharacterEllipsis,
        Thickness? margin = null)
    {
        var text = new TextBlock
        {
            Text = value,
            FontFamily = UiFont,
            FontSize = size,
            Foreground = brush ?? Palette.TextBrush,
            TextWrapping = wrap,
            TextTrimming = trim,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (margin is not null) text.Margin = margin.Value;
        // The panel is 360 DIPs wide. Segoe UI at a single weight reads
        // cleanest; callers may still pass a weight for API compatibility,
        // but the rendered weight is intentionally uniform.
        text.FontWeight = FontWeights.Normal;
        return text;
    }

    public static TextBlock Mono(string value, double size = 11, FontWeight? weight = null, Brush? brush = null)
    {
        var text = Text(value, size, weight, brush);
        text.FontFamily = MonoFont;
        return text;
    }

    public static Border Module(params UIElement[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children)
            if (child is not null) stack.Children.Add(child);

        return new Border
        {
            Background = Palette.SurfaceBrush,
            BorderBrush = Palette.BorderStrongBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Radius),
            Margin = new Thickness(0, 0, 0, ModuleGap),
            Child = stack,
        };
    }

    public static Border Module(StackPanel content) => new()
    {
        Background = Palette.SurfaceBrush,
        BorderBrush = Palette.BorderStrongBrush,
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
            Fill = on ? Palette.AccentBrush : Palette.SurfaceAltBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var text = Text(label, 12, FontWeights.Medium, on ? Palette.OnAccentBrush : Palette.TextSecondaryBrush);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { Width = GlyphSize, Height = GlyphSize };
        grid.Children.Add(ellipse);
        grid.Children.Add(text);
        return grid;
    }

    public static Border Pill(string label, Brush? foreground = null, Brush? background = null, bool uppercase = false)
    {
        var border = new Border
        {
            Background = background ?? Palette.SurfaceAltBrush,
            BorderBrush = foreground ?? Palette.TextTertiaryBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Mono(uppercase ? label.ToUpperInvariant() : label, 13, FontWeights.Normal,
                foreground ?? Palette.TextTertiaryBrush),
        };
        return border;
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
        textStack.Children.Add(Text(title, 13, FontWeights.Medium, Palette.TextBrush));
        if (!string.IsNullOrEmpty(subtitle))
        {
            textStack.Children.Add(Text(subtitle, 11, FontWeights.Normal,
                subtitleBrush ?? Palette.TextTertiaryBrush, subtitleWrap));
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

    public static Rectangle Divider() => new()
    {
        Height = 1,
        Fill = Palette.BorderBrush,
        Margin = new Thickness(RowPadding + GlyphSize + 8, 0, 0, 0),
    };

    public static Grid TitleRow(string title, UIElement? trailing = null)
    {
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

    public static Button AccentButton(string label, Action onClick) =>
        StyledButton(label, Palette.OnInkBrush, Palette.InkBrush, Palette.InkHoverBrush, 0, 12, 5, onClick);

    public static Button SecondaryButton(string label, Action onClick) =>
        StyledButton(label, Palette.TextBrush, Palette.SurfaceBrush, Palette.SurfaceAltBrush, 1, 12, 5, onClick);

    public static Button QuietButton(string label, Action onClick) =>
        StyledButton(label, Palette.TextTertiaryBrush, Brushes.Transparent, Palette.SurfaceAltBrush, 0, 8, 4, onClick);

    private static Button StyledButton(string label, Brush foreground, Brush background, Brush hover,
        double borderThickness, double horizontalPadding, double verticalPadding, Action onClick)
    {
        var button = new Button
        {
            Content = Text(label, 13, FontWeights.Medium, foreground),
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
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
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

    public static CheckBox Toggle(bool isChecked, Action<bool> onChanged)
    {
        var check = new CheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
        };
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
        var back = QuietButton("←", onBack);
        back.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(back, 0);
        grid.Children.Add(back);
        var icon = Text(glyph, 13, FontWeights.Normal, Palette.AccentInkBrush);
        Grid.SetColumn(icon, 1);
        icon.Margin = new Thickness(4, 0, 6, 0);
        grid.Children.Add(icon);
        var titleText = Text(title, 13, FontWeights.Normal, Palette.TextBrush);
        Grid.SetColumn(titleText, 2);
        grid.Children.Add(titleText);
        return grid;
    }
}
