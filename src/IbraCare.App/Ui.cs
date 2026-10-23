using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IbraCare.Core;
using IbraCare.Core.Fonts;

namespace IbraCare.App;

/// <summary>The IBRA look (black and gold, from IBDM Pro): dark or light following Windows' app mode, unless chosen in the settings.</summary>
public static class Theme
{
    public static readonly bool IsDark = ReadDark();
    private static string P(string dark, string light) => IsDark ? dark : light;

    public static readonly Brush Bg = Br(P("#0F0F11", "#F3F3F5"));
    public static readonly Brush Panel = Br(P("#16161A", "#FAFAFB"));
    public static readonly Brush Card = Br(P("#1E1E23", "#FFFFFF"));
    public static readonly Brush Hover = Br(P("#2A2A31", "#ECECF0"));
    public static readonly Brush Pressed = Br(P("#33333B", "#E1E1E7"));
    public static readonly Brush Line = Br(P("#2C2C33", "#DCDCE2"));
    public static readonly Brush Gold = Br(P("#D4AF37", "#A87F0A"));
    public static readonly Brush GoldHover = Br(P("#E2C25A", "#B98E17"));
    public static readonly Brush GoldPressed = Br(P("#B8962A", "#8F6C08"));
    public static readonly Brush GoldSoft = Br(P("#33D4AF37", "#26C49A1C"));
    public static readonly Brush OnGold = Br(P("#111111", "#FFFFFF"));
    public static readonly Brush Ink = Br(P("#F2F2F4", "#1B1B1F"));
    public static readonly Brush Muted = Br(P("#9C9CA6", "#6B6B75"));
    public static readonly Brush Red = Br(P("#E5533D", "#C93B26"));
    public static readonly Brush RedSoft = Br(P("#33E5533D", "#22C93B26"));
    public static readonly Brush Green = Br(P("#4CC26B", "#23914A"));
    public static readonly Brush GreenSoft = Br(P("#334CC26B", "#2223914A"));
    public static readonly Brush Amber = Br(P("#F0B429", "#B7791F"));
    public static readonly Brush AmberSoft = Br(P("#33F0B429", "#22B7791F"));
    public static readonly Brush Blue = Br(P("#3B9BF0", "#1F7BD0"));
    public static readonly Brush BlueSoft = Br(P("#333B9BF0", "#221F7BD0"));
    public static readonly Brush Track = Br(P("#2A2A30", "#E3E3E9"));
    public static readonly Brush BoxLine = Br(P("#55555F", "#9A9AA5"));

    private static bool ReadDark()
    {
        var mode = Settings.Current.Theme;
        if (mode == "dark") return true;
        if (mode == "light") return false;
        return WindowsIsDark();
    }

    public static bool WindowsIsDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
        }
        catch (Exception) { return true; }
    }

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static Brush Br(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static Brush For(Severity s) => s switch { Severity.Error => Red, Severity.Warning => Amber, _ => Blue };
    public static Brush SoftFor(Severity s) => s switch { Severity.Error => RedSoft, Severity.Warning => AmberSoft, _ => BlueSoft };

    // ---------- styles built in code (no XAML) ----------

    public static readonly Style Button = MakeButton(Card, Hover, Pressed, Ink, Line);
    public static readonly Style PrimaryButton = MakeButton(Gold, GoldHover, GoldPressed, OnGold, Gold);
    public static readonly Style DangerButton = MakeButton(RedSoft, Hover, Pressed, Red, RedSoft);
    public static readonly Style ToolButton = MakeButton(Brushes.Transparent, Hover, Pressed, Ink, Brushes.Transparent);
    public static readonly Style Choice = MakeChoice();
    public static readonly Style Nav = MakeNav();
    public static readonly Style Tab = MakeTab();
    public static readonly Style Progress = MakeProgress();
    public static readonly Style Row = MakeRow();
    public static readonly Style Header = MakeHeader();
    public static readonly Style Input = MakeInput();
    public static readonly Style CheckBox = MakeCheck();

    private static Style MakeCheck()
    {
        var root = new FrameworkElementFactory(typeof(DockPanel));
        root.SetValue(System.Windows.Controls.Panel.BackgroundProperty, Brushes.Transparent);
        var box = new FrameworkElementFactory(typeof(Border), "Box");
        box.SetValue(FrameworkElement.WidthProperty, 18.0);
        box.SetValue(FrameworkElement.HeightProperty, 18.0);
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
        box.SetValue(Border.BorderBrushProperty, BoxLine);
        box.SetValue(Border.BackgroundProperty, Card);
        box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        box.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 0));
        box.SetValue(DockPanel.DockProperty, Dock.Left);
        var mark = new FrameworkElementFactory(typeof(TextBlock), "Mark");
        mark.SetValue(TextBlock.TextProperty, "");
        mark.SetValue(TextBlock.FontFamilyProperty, Icons);
        mark.SetValue(TextBlock.FontSizeProperty, 12.0);
        mark.SetValue(TextBlock.ForegroundProperty, OnGold);
        mark.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        mark.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        mark.SetValue(FrameworkElement.FlowDirectionProperty, FlowDirection.LeftToRight);
        mark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        box.AppendChild(mark);
        root.AppendChild(box);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 0, 0));
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(cp);
        var t = new ControlTemplate(typeof(CheckBox)) { VisualTree = root };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BorderBrushProperty, Gold, "Box") } });
        t.Triggers.Add(new Trigger
        {
            Property = ToggleButton.IsCheckedProperty, Value = true,
            Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Mark"), new Setter(Border.BackgroundProperty, Gold, "Box"), new Setter(Border.BorderBrushProperty, Gold, "Box") },
        });
        t.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, 0.45) } });
        var s = new Style(typeof(CheckBox));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return s;
    }

    private static Style MakeButton(Brush bg, Brush hover, Brush pressed, Brush fg, Brush border)
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        bd.SetBinding(Border.BackgroundProperty, TP("Background"));
        bd.SetBinding(Border.BorderBrushProperty, TP("BorderBrush"));
        bd.SetBinding(Border.BorderThicknessProperty, TP("BorderThickness"));
        bd.SetBinding(Border.PaddingProperty, TP("Padding"));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        var t = new ControlTemplate(typeof(ButtonBase)) { VisualTree = bd };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, hover, "Bd") } });
        t.Triggers.Add(new Trigger { Property = ButtonBase.IsPressedProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, pressed, "Bd") } });
        t.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, 0.4) } });
        var s = new Style(typeof(ButtonBase));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.BackgroundProperty, bg));
        s.Setters.Add(new Setter(Control.ForegroundProperty, fg));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, border));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 7, 16, 7)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        s.Setters.Add(new Setter(UIElement.FocusableProperty, true));
        return s;
    }

    /// <summary>A RadioButton drawn as a selectable card.</summary>
    private static Style MakeChoice()
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        bd.SetBinding(Border.BackgroundProperty, TP("Background"));
        bd.SetBinding(Border.BorderBrushProperty, TP("BorderBrush"));
        bd.SetBinding(Border.PaddingProperty, TP("Padding"));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        var t = new ControlTemplate(typeof(RadioButton)) { VisualTree = bd };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, Hover, "Bd") } });
        t.Triggers.Add(new Trigger
        {
            Property = ToggleButton.IsCheckedProperty, Value = true,
            Setters = { new Setter(Border.BorderBrushProperty, Gold, "Bd"), new Setter(Border.BackgroundProperty, GoldSoft, "Bd") },
        });
        var s = new Style(typeof(RadioButton));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.BackgroundProperty, Card));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6)));
        return s;
    }

    /// <summary>Side navigation entry: a gold bar marks the open page.</summary>
    private static Style MakeNav()
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bd.SetValue(Border.PaddingProperty, new Thickness(12, 9, 12, 9));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(3, 0, 0, 0));
        bd.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        bd.AppendChild(cp);
        var t = new ControlTemplate(typeof(RadioButton)) { VisualTree = bd };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, Hover, "Bd") } });
        t.Triggers.Add(new Trigger
        {
            Property = ToggleButton.IsCheckedProperty, Value = true,
            Setters = { new Setter(Border.BackgroundProperty, GoldSoft, "Bd"), new Setter(Border.BorderBrushProperty, Gold, "Bd") },
        });
        var s = new Style(typeof(RadioButton));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 1)));
        return s;
    }

    /// <summary>A tab at the top of a page: underlined in gold when selected.</summary>
    private static Style MakeTab()
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bd.SetValue(Border.PaddingProperty, new Thickness(14, 8, 14, 8));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        bd.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        bd.AppendChild(cp);
        var t = new ControlTemplate(typeof(RadioButton)) { VisualTree = bd };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, Hover, "Bd") } });
        t.Triggers.Add(new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true, Setters = { new Setter(Border.BorderBrushProperty, Gold, "Bd") } });
        var s = new Style(typeof(RadioButton));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return s;
    }

    private static Style MakeProgress()
    {
        var grid = new FrameworkElementFactory(typeof(Grid));
        var track = new FrameworkElementFactory(typeof(Border), "PART_Track");
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        track.SetValue(Border.BackgroundProperty, Track);
        var ind = new FrameworkElementFactory(typeof(Border), "PART_Indicator");
        ind.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        ind.SetBinding(Border.BackgroundProperty, TP("Foreground"));
        ind.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        grid.AppendChild(track);
        grid.AppendChild(ind);
        var s = new Style(typeof(ProgressBar));
        s.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ProgressBar)) { VisualTree = grid }));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Gold));
        s.Setters.Add(new Setter(FrameworkElement.HeightProperty, 6.0));
        return s;
    }

    private static Style MakeRow()
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bd.SetValue(Border.BorderBrushProperty, Line);
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        bd.SetValue(Border.PaddingProperty, new Thickness(0, 5, 0, 5));
        var rp = new FrameworkElementFactory(typeof(GridViewRowPresenter));
        rp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(rp);
        var t = new ControlTemplate(typeof(ListViewItem)) { VisualTree = bd };
        t.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, Card, "Bd") } });
        t.Triggers.Add(new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, GoldSoft, "Bd") } });
        var s = new Style(typeof(ListViewItem));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        return s;
    }

    private static Style MakeHeader()
    {
        var bd = new FrameworkElementFactory(typeof(Border), "Bd");
        bd.SetValue(Border.BackgroundProperty, Panel);
        bd.SetValue(Border.BorderBrushProperty, Line);
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        bd.SetValue(Border.PaddingProperty, new Thickness(8, 8, 8, 8));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        bd.AppendChild(cp);
        var t = new ControlTemplate(typeof(GridViewColumnHeader)) { VisualTree = bd };
        var s = new Style(typeof(GridViewColumnHeader));
        s.Setters.Add(new Setter(Control.TemplateProperty, t));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Muted));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        return s;
    }

    private static Style MakeInput()
    {
        var s = new Style(typeof(TextBox));
        s.Setters.Add(new Setter(Control.BackgroundProperty, Card));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        s.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, Gold));
        s.Setters.Add(new Setter(TextBoxBase.SelectionBrushProperty, Gold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        s.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return s;
    }

    private static Binding TP(string path) => new(path) { RelativeSource = RelativeSource.TemplatedParent };
}

/// <summary>Small builders so pages read like a description of their layout.</summary>
public static class Ui
{
    public static FlowDirection Dir => L.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static TextBlock Text(string s, double size = 13, Brush? fg = null, bool bold = false, TextWrapping wrap = TextWrapping.NoWrap) => new()
    {
        Text = s,
        FontSize = size,
        Foreground = fg ?? Theme.Ink,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = wrap,
        TextTrimming = wrap == TextWrapping.NoWrap ? TextTrimming.CharacterEllipsis : TextTrimming.None,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Wrap(string s, double size = 13, Brush? fg = null) => Text(s, size, fg ?? Theme.Muted, wrap: TextWrapping.Wrap);

    /// <summary>Numbers and sizes ("94.8 MB") always read left to right, even inside Arabic.</summary>
    public static TextBlock Ltr(string s, double size = 13, Brush? fg = null, bool bold = false)
    {
        var t = Text(s, size, fg, bold);
        t.FlowDirection = FlowDirection.LeftToRight;
        // In a right-to-left parent "Left" is mirrored to the right edge, so this always sits at the start of the line.
        t.HorizontalAlignment = HorizontalAlignment.Left;
        return t;
    }

    /// <summary>A file path: left to right, monospaced, trimmed with the full path as a tooltip.</summary>
    public static TextBlock PathText(string? path, double size = 12)
    {
        var t = Ltr(path ?? "", size, Theme.Muted);
        t.FontFamily = Theme.Mono;
        t.ToolTip = path;
        return t;
    }

    /// <summary>Right to left for Arabic text, left to right for text that starts with a Latin letter (an English font name).</summary>
    public static FlowDirection DirectionOf(string? s)
    {
        foreach (var c in s ?? "")
        {
            if (c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= 'ﻼ') return FlowDirection.RightToLeft;
            if (char.IsLetter(c)) return FlowDirection.LeftToRight;
        }
        return Dir;
    }

    /// <summary>A name that may be Arabic or English (a font name): drawn in its own direction, aligned to the start of the line.</summary>
    public static TextBlock Name(string s, double size = 13, Brush? fg = null, bool bold = true)
    {
        var t = Text(s, size, fg ?? Theme.Ink, bold);
        t.FlowDirection = DirectionOf(s);
        t.HorizontalAlignment = HorizontalAlignment.Left;
        return t;
    }

    public static TextBlock Glyph(string g, double size = 16, Brush? fg = null) => new()
    {
        Text = g,
        FontFamily = Theme.Icons,
        FontSize = size,
        Foreground = fg ?? Theme.Ink,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        FlowDirection = FlowDirection.LeftToRight,
    };

    public static Button Button(string text, string? glyph = null, Style? style = null, Action? click = null)
    {
        var b = new Button { Style = style ?? Theme.Button };
        if (glyph is null) b.Content = text;
        else
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var g = Glyph(glyph, 14);
            g.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = b });
            sp.Children.Add(g);
            var t = Text(text);
            t.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = b });
            t.Margin = new Thickness(8, 0, 0, 0);
            sp.Children.Add(t);
            b.Content = sp;
        }
        if (click is not null) b.Click += (_, _) => click();
        return b;
    }

    public static TextBox Input(string text = "", bool multiline = false)
    {
        var t = new TextBox { Style = Theme.Input, Text = text };
        if (multiline)
        {
            t.AcceptsReturn = true;
            t.TextWrapping = TextWrapping.NoWrap;
            t.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            t.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            t.VerticalContentAlignment = VerticalAlignment.Top;
        }
        return t;
    }

    public static CheckBox Check(string text, bool value) => new()
    {
        Style = Theme.CheckBox,
        Content = Text(text, 13, Theme.Ink, wrap: TextWrapping.Wrap),
        IsChecked = value,
        Foreground = Theme.Ink,
        Margin = new Thickness(0, 4, 0, 4),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    public static RadioButton Choice(string text, string group, bool on, string? sub = null)
    {
        UIElement content = Text(text, 13, Theme.Ink, bold: true);
        if (sub is not null)
        {
            var sp = new StackPanel();
            sp.Children.Add(content);
            var s = Wrap(sub, 12);
            s.Margin = new Thickness(0, 2, 0, 0);
            sp.Children.Add(s);
            content = sp;
        }
        return new RadioButton { Style = Theme.Choice, GroupName = group, IsChecked = on, Content = content };
    }

    public static Border Card(UIElement child, Thickness? padding = null) => new()
    {
        Child = child,
        Background = Theme.Card,
        BorderBrush = Theme.Line,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = padding ?? new Thickness(14),
    };

    public static Border Badge(string text, Brush fg, Brush bg) => new()
    {
        Child = Text(text, 11, fg, bold: true),
        Background = bg,
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 1, 8, 2),
        Margin = new Thickness(6, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Title(string text, double size = 20) => Text(text, size, Theme.Ink, bold: true);

    public static UIElement Label(string text) => new TextBlock
    {
        Text = text, FontSize = 12, Foreground = Theme.Muted, Margin = new Thickness(0, 12, 0, 4),
    };

    /// <summary>A stat tile: big number, caption.</summary>
    public static Border Tile(string value, string caption, string glyph, Brush color, out TextBlock valueText)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(10), Background = Theme.GoldSoft, Child = Glyph(glyph, 20, color), Margin = new Thickness(0, 0, 12, 0) };
        g.Children.Add(icon);
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        valueText = Ltr(value, 20, Theme.Ink, bold: true);
        sp.Children.Add(valueText);
        sp.Children.Add(Text(caption, 12, Theme.Muted));
        Grid.SetColumn(sp, 1);
        g.Children.Add(sp);
        var card = Card(g);
        card.Margin = new Thickness(0, 0, 10, 10);
        return card;
    }

    public static ImageSource AppIcon(int size = 256)
    {
        try
        {
            var uri = new Uri(size > 48 ? "pack://application:,,,/Assets/ibracare-256.png" : "pack://application:,,,/Assets/ibracare.ico");
            return BitmapFrame.Create(uri);
        }
        catch (Exception) { return new DrawingImage(); }
    }

    public static Image Logo(double size) => new() { Source = AppIcon(), Width = size, Height = size };

    /// <summary>Every window: themed, in the app's language and direction, dark title bar when dark.</summary>
    public static void Setup(Window w, string title, double width, double height)
    {
        w.Title = title;
        w.Width = width;
        w.Height = height;
        w.Background = Theme.Bg;
        w.Foreground = Theme.Ink;
        w.FontFamily = Theme.Font;
        w.FontSize = 13;
        w.FlowDirection = Dir;
        w.Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.IsArabic ? "ar" : "en");
        w.Icon = AppIcon(32);
        w.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
        w.SourceInitialized += (_, _) => DarkTitleBar(w);
    }

    public static void DarkTitleBar(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = Theme.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, 4);
            var bg = ((SolidColorBrush)Theme.Bg).Color;
            int caption = bg.R | bg.G << 8 | bg.B << 16;
            DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
        }
        catch (Exception) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) { Message(null, L.T("ui.cannotOpen"), Errors.Friendly(e)); }
    }

    public static void ShowInFolder(string? file)
    {
        if (string.IsNullOrEmpty(file)) return;
        try
        {
            if (File.Exists(file)) Process.Start("explorer.exe", $"/select,\"{file}\"");
            else if (Directory.Exists(file)) Process.Start("explorer.exe", $"\"{file}\"");
            else if (Directory.Exists(Path.GetDirectoryName(file))) Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(file)}\"");
            else Message(null, L.T("ui.folderMissing"), file);
        }
        catch (Exception e) { Message(null, L.T("ui.cannotOpen"), Errors.Friendly(e)); }
    }

    public static Window? ActiveWindow => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

    /// <summary>A themed question box. Returns the index of the button pressed, or -1 if closed. The first button is the main one.</summary>
    public static int Ask(Window? owner, string title, string message, params string[] buttons) => Ask(owner, title, message, null, buttons);

    /// <summary>Same, with an optional list (e.g. the files about to change) shown in a scrollable box.</summary>
    public static int Ask(Window? owner, string title, string message, IReadOnlyList<string>? list, params string[] buttons)
    {
        owner ??= ActiveWindow;
        var w = new Window { ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.Height, ShowInTaskbar = owner is null };
        Setup(w, title, list is { Count: > 0 } ? 620 : 460, 200);
        w.WindowStartupLocation = owner is { IsVisible: true } ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
        if (owner is { IsVisible: true } && !ReferenceEquals(owner, w)) w.Owner = owner;
        var result = -1;
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(Text(title, 16, Theme.Ink, bold: true));
        var msg = Wrap(message, 13);
        msg.Margin = new Thickness(0, 10, 0, 14);
        root.Children.Add(msg);
        if (list is { Count: > 0 })
        {
            var box = new TextBox
            {
                Style = Theme.Input, IsReadOnly = true, Height = 180, FontFamily = Theme.Mono, FontSize = 12,
                Text = string.Join(Environment.NewLine, list.Take(2000)) + (list.Count > 2000 ? Environment.NewLine + "…" : ""),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 0, 0, 16), VerticalContentAlignment = VerticalAlignment.Top,
            };
            root.Children.Add(box);
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var b = Button(buttons[i], style: i == 0 ? Theme.PrimaryButton : Theme.Button, click: () => { result = index; w.Close(); });
            b.Margin = new Thickness(0, 0, 8, 0);
            if (i == 0) b.IsDefault = true;
            if (i == buttons.Length - 1) b.IsCancel = true;
            row.Children.Add(b);
        }
        root.Children.Add(row);
        w.Content = root;
        w.ShowDialog();
        return result;
    }

    public static void Message(Window? owner, string title, string message) => Ask(owner, title, message, L.T("ui.ok"));

    public static void Error(Exception e, string? title = null) => Message(null, title ?? L.T("ui.errorTitle"), Errors.Friendly(e));

    /// <summary>A two-column grid where the first column holds labels.</summary>
    public static Grid Columns(params GridLength[] widths)
    {
        var g = new Grid();
        foreach (var w in widths) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        return g;
    }

    public static T At<T>(this T e, int column, int row = 0) where T : UIElement
    {
        Grid.SetColumn(e, column);
        Grid.SetRow(e, row);
        return e;
    }

    public static T Margin<T>(this T e, double l, double t, double r, double b) where T : FrameworkElement
    {
        e.Margin = new Thickness(l, t, r, b);
        return e;
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    /// <summary>Gives every child but the last a gap after it.</summary>
    public static StackPanel Spaced(this StackPanel sp, double gap = 8)
    {
        for (var i = 0; i < sp.Children.Count - 1; i++)
            if (sp.Children[i] is FrameworkElement fe) fe.Margin = sp.Orientation == Orientation.Horizontal ? new Thickness(0, 0, gap, 0) : new Thickness(0, 0, 0, gap);
        return sp;
    }

    public static ListView List(IEnumerable<(string Header, double Width, Func<object, FrameworkElement> Cell)> columns)
    {
        var view = new GridView { ColumnHeaderContainerStyle = Theme.Header };
        foreach (var (header, width, cell) in columns)
        {
            var factory = new FrameworkElementFactory(typeof(ContentControl));
            factory.SetBinding(ContentControl.ContentProperty, new Binding(".") { Converter = new CellConverter(cell) });
            view.Columns.Add(new GridViewColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = factory } });
        }
        var list = new ListView
        {
            View = view, ItemContainerStyle = Theme.Row, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = Theme.Ink, SelectionMode = SelectionMode.Extended,
        };
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list, true);
        return list;
    }

    private sealed class CellConverter(Func<object, FrameworkElement> make) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is null ? null : make(value);
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
