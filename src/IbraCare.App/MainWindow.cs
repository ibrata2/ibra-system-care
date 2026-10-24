using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IbraCare.App.Pages;
using IbraCare.Core;

namespace IbraCare.App;

/// <summary>A screen of the app, built once and kept while the window lives.</summary>
public abstract class CarePage : Grid
{
    public abstract string Id { get; }
    public virtual void OnShown() { }

    protected CarePage()
    {
        Margin = new Thickness(26, 20, 26, 18);
    }

    /// <summary>Title, description and an optional row of actions on the far side.</summary>
    protected UIElement PageHeader(string title, string description, params UIElement[] actions)
    {
        var g = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        var sp = new StackPanel();
        sp.Children.Add(Ui.Title(title, 22));
        var d = Ui.Wrap(description, 13);
        d.Margin = new Thickness(0, 4, 0, 0);
        sp.Children.Add(d);
        g.Children.Add(sp);
        var row = Ui.Row(actions).Spaced();
        row.VerticalAlignment = VerticalAlignment.Top;
        g.Children.Add(row.At(1));
        g.Margin = new Thickness(0, 0, 0, 16);
        return g;
    }

    /// <summary>Rows for the page's layout: header (auto), then the given rows.</summary>
    protected void Rows(params GridLength[] rows)
    {
        RowDefinitions.Clear();
        foreach (var r in rows) RowDefinitions.Add(new RowDefinition { Height = r });
    }

    protected static GridLength Auto => GridLength.Auto;
    protected static GridLength Star => new(1, GridUnitType.Star);
}

public sealed class MainWindow : Window
{
    private readonly Dictionary<string, CarePage> _pages = new();
    private readonly Dictionary<string, RadioButton> _nav = new();
    private readonly Border _host = new();
    private readonly TextBlock _updateBadge = Ui.Text("", 11, Theme.OnGold, bold: true);
    private readonly Border _updateBox;
    public bool Replaced { get; set; }
    public string CurrentPage { get; private set; } = "home";

    public MainWindow(string page)
    {
        Ui.Setup(this, "IBRA SYSTEM CARE", 1180, 760);
        MinWidth = 980;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = Ui.Columns(new GridLength(250), new GridLength(1, GridUnitType.Star));

        // ---------- side bar ----------
        var side = new DockPanel { Background = Theme.Panel };
        var brand = new StackPanel { Margin = new Thickness(18, 20, 18, 18) };
        var logoRow = Ui.Row(Ui.Logo(44), new StackPanel
        {
            Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { Ui.Text("IBRA", 18, Theme.Gold, bold: true), Ui.Text("SYSTEM CARE", 12, Theme.Ink, bold: true) },
        });
        foreach (var c in logoRow.Children.OfType<StackPanel>()) foreach (var t in c.Children.OfType<TextBlock>()) t.FlowDirection = FlowDirection.LeftToRight;
        brand.Children.Add(logoRow);
        DockPanel.SetDock(brand, Dock.Top);
        side.Children.Add(brand);

        var footer = new StackPanel { Margin = new Thickness(18, 8, 18, 16) };
        _updateBox = new Border { Background = Theme.Gold, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Child = _updateBadge, Visibility = Visibility.Collapsed, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 0, 10) };
        _updateBox.MouseLeftButtonUp += (_, _) => Updater.ShowWindow(this);
        footer.Children.Add(_updateBox);
        if (Platform.IsAdmin) footer.Children.Add(Ui.Row(Ui.Glyph("", 13, Theme.Gold), Ui.Text(L.T("ui.runningAdmin"), 11.5, Theme.Gold)).Spaced(6).Margin(0, 0, 0, 8));
        footer.Children.Add(Ui.Wrap(L.T("app.credit"), 11));
        var social = Ui.Row(
            Ui.Button("Telegram", null, Theme.ToolButton, () => Ui.Open(Links.Telegram)),
            Ui.Button("TikTok", null, Theme.ToolButton, () => Ui.Open(Links.TikTok)));
        foreach (var b in social.Children.OfType<Button>()) { b.Padding = new Thickness(6, 3, 6, 3); b.FontSize = 11.5; b.Foreground = Theme.Gold; }
        social.Margin = new Thickness(-6, 4, 0, 2);
        footer.Children.Add(social);
        var ver = Ui.Ltr("v" + Program.Version, 11, Theme.Muted);
        footer.Children.Add(ver);
        DockPanel.SetDock(footer, Dock.Bottom);
        side.Children.Add(footer);

        var nav = new StackPanel { Margin = new Thickness(10, 4, 10, 0) };
        void Nav(string id, string glyph, string text, string? sub = null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(Ui.Glyph(glyph, 17, Theme.Gold));
            var labels = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            var t = Ui.Text(text, 13.5, Theme.Ink, bold: true);
            labels.Children.Add(t);
            if (sub is not null) labels.Children.Add(Ui.Text(sub, 11, Theme.Muted));
            sp.Children.Add(labels);
            var rb = new RadioButton { Style = Theme.Nav, GroupName = "nav", Content = sp };
            rb.Checked += (_, _) => Show(id);
            _nav[id] = rb;
            nav.Children.Add(rb);
        }
        Nav("home", "", L.T("nav.home"));
        Nav("cleaner", "", "Deep Cleaner", L.T("nav.cleanerSub"));
        Nav("fonts", "", "Font Doctor", L.T("nav.fontsSub"));
        Nav("repair", "", "Repair Center", L.T("nav.repairSub"));
        nav.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(8, 10, 8, 10) });
        Nav("settings", "", L.T("nav.settings"));
        Nav("about", "", L.T("nav.about"));
        side.Children.Add(new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        // Layout is mirrored in right-to-left windows (the bar moves to the right and this border to its left side),
        // so one value puts the line between the bar and the content in both languages.
        root.Children.Add(new Border { Child = side, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0) });

        _host.Background = Theme.Bg;
        root.Children.Add(_host.At(1));
        Content = root;

        Loaded += (_, _) => Navigate(page);
    }

    public void Navigate(string id)
    {
        if (!_nav.ContainsKey(id)) id = "home";
        if (_nav[id].IsChecked == true) Show(id);
        else _nav[id].IsChecked = true;
    }

    private void Show(string id)
    {
        if (!_pages.TryGetValue(id, out var page))
        {
            page = Create(id);
            _pages[id] = page;
        }
        CurrentPage = id;
        _host.Child = page;
        page.OnShown();
    }

    public static CarePage Create(string id) => id switch
    {
        "cleaner" => new CleanerPage(),
        "fonts" => new FontsPage(),
        "repair" => new RepairPage(),
        "settings" => new SettingsPage(),
        "about" => new AboutPage(),
        _ => new HomePage(),
    };

    public void ShowUpdateBadge(string version)
    {
        _updateBadge.Text = L.T("ui.updateBadge", version);
        _updateBox.Visibility = Visibility.Visible;
    }
}
