using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IbraCare.Core;
using IbraCare.Core.Fonts;

namespace IbraCare.App.Pages;

/// <summary>Fonts that look the same (same letter drawings under different names or files): pick the copy to keep,
/// then merge (remove the others) or delete single copies. Every removal is backed up and can be undone.</summary>
public sealed class LookAlikesView : Grid
{
    private readonly FontsPage _page;
    private readonly FontScan _scan;
    private readonly StackPanel _list = new();
    private List<LookAlikeGroup> _groups = [];

    public LookAlikesView(FontsPage page, FontScan scan)
    {
        _page = page;
        _scan = scan;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        head.Children.Add(Ui.Wrap(L.T("look.intro"), 12.5));
        var actions = Ui.Row(
            Ui.Button(L.T("look.search"), "", Theme.Button, async () => await Search()),
            Ui.Button(L.T("look.mergeAll"), "", Theme.PrimaryButton, async () => await MergeAll())).Spaced();
        head.Children.Add(actions.Margin(12, 0, 0, 0).At(1));
        head.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(head);
        Children.Add(Common.Scroll(_list).At(0, 1));
        Loaded += async (_, _) => await Search();
    }

    private async Task Search()
    {
        _list.Children.Clear();
        _list.Children.Add(new ProgressBar { Style = Theme.Progress, IsIndeterminate = true, Margin = new Thickness(0, 20, 0, 0) });
        var groups = await Common.Run(() => LookAlikes.Find(_scan));
        _list.Children.Clear();
        if (groups is null) return;
        _groups = groups;
        if (groups.Count == 0)
        {
            _list.Children.Add(Common.Empty("", L.T("look.none")));
            return;
        }
        _list.Children.Add(Ui.Text(L.T("look.summary", Fmt.Count(groups.Count), Fmt.Count(groups.Sum(g => g.Files.Count - 1)), Fmt.Size(groups.Sum(g => g.Wasted))), 13.5, Theme.Ink, bold: true).Margin(0, 0, 0, 10));
        foreach (var g in groups) _list.Children.Add(GroupCard(g));
    }

    private Border GroupCard(LookAlikeGroup g)
    {
        var sp = new StackPanel();
        var kind = g.Kind switch { "same" => L.T("look.kindSame"), "versions" => L.T("look.kindVersions"), _ => L.T("look.kindShapes", Math.Round(g.Similarity * 100)) };
        var head = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        head.Children.Add(Ui.Row(Ui.Badge(kind, Theme.Gold, Theme.GoldSoft).Margin(0, 0, 0, 0), Ui.Ltr(Fmt.Size(g.Wasted), 12, Theme.Muted).Margin(10, 0, 0, 0)));
        var merge = Ui.Button(L.T("look.merge"), "", Theme.Button, async () => await Remove(g.Files.Where(f => !ReferenceEquals(f, g.Keep)).ToList(), g));
        head.Children.Add(merge.At(1));
        sp.Children.Add(head);
        var group = "keep" + Guid.NewGuid().ToString("N")[..6];
        foreach (var f in g.Files)
        {
            var row = Ui.Columns(GridLength.Auto, new GridLength(1.1, GridUnitType.Star), new GridLength(1, GridUnitType.Star), GridLength.Auto);
            var keep = new RadioButton { GroupName = group, IsChecked = ReferenceEquals(f, g.Keep), ToolTip = L.T("look.keepThis"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            keep.Checked += (_, _) => g.Keep = f;
            row.Children.Add(keep);
            var info = new StackPanel();
            info.Children.Add(Ui.Name(f.DisplayName, 13));
            info.Children.Add(Ui.Ltr($"{f.FileName}  ·  {Fmt.Size(f.Size)}  ·  {L.T("loc." + f.Location)}" + (f.LegacyConvertible ? "  ·  " + L.T("look.old") : ""), 11.5, Theme.Muted));
            info.Children.Add(Ui.PathText(Path.GetDirectoryName(f.Path), 11));
            row.Children.Add(info.At(1));
            var sample = new TextBlock
            {
                Text = f.LegacyConvertible ? "AaBb 123" : L.T("look.sample"), FontSize = 24, Foreground = Theme.Ink, FlowDirection = FlowDirection.RightToLeft,
                FontFamily = AllFontsView.PreviewFamily(f, f.Faces.FirstOrDefault()?.Family ?? f.DisplayName), TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0),
            };
            row.Children.Add(sample.At(2));
            var buttons = Ui.Row(
                Ui.Button("", "", Theme.ToolButton, () => Ui.ShowInFolder(f.Path)),
                Ui.Button(L.T("look.delete"), "", Theme.DangerButton, async () => await Remove([f], g)));
            ((Button)buttons.Children[0]).ToolTip = L.T("clean.openFolder");
            buttons.Spaced(4);
            row.Children.Add(buttons.At(3));
            row.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(row);
        }
        var card = Ui.Card(sp, new Thickness(14));
        card.Margin = new Thickness(0, 0, 0, 10);
        return card;
    }

    private async Task Remove(IReadOnlyList<FontFileInfo> files, LookAlikeGroup g)
    {
        if (files.Count == 0) return;
        if (files.Count == g.Files.Count) { Ui.Message(null, L.T("fonts.tabLookAlikes"), L.T("look.keepOne")); return; }
        await RemoveFiles(files, [$"{L.T("look.keeping")}: {g.Keep.Path}"]);
    }

    /// <summary>For every group, keeps the recommended copy (or the one chosen) and removes the rest.</summary>
    private async Task MergeAll()
    {
        if (_groups.Count == 0) { Ui.Message(null, L.T("fonts.tabLookAlikes"), L.T("look.none")); return; }
        var remove = _groups.SelectMany(g => g.Files.Where(f => !ReferenceEquals(f, g.Keep))).Where(f => f.Faces.Count <= 1).ToList();
        await RemoveFiles(remove, _groups.Select(g => $"{L.T("look.keeping")}: {g.Keep.Path}").ToList());
    }

    private async Task RemoveFiles(IReadOnlyList<FontFileInfo> files, List<string> keeping)
    {
        if (files.Any(f => f.Location == FontLocation.System) && !Platform.IsAdmin)
        {
            if (Ui.Ask(null, L.T("fonts.adminNeededTitle"), L.T("fonts.adminNeeded"), L.T("ui.restartAdmin"), L.T("ui.cancel")) == 0) Program.RestartAsAdmin("fonts");
            return;
        }
        var lines = keeping.Concat(files.Select(f => $"{L.T("fonts.willRemove")}: {f.Path}")).ToList();
        if (Ui.Ask(null, L.T("look.confirmTitle"), L.T("look.confirm", Fmt.Count(files.Count), Fmt.Size(files.Sum(f => f.Size))), lines, L.T("look.removeNow"), L.T("ui.cancel")) != 0) return;
        _page.Status.Start(L.T("fonts.fixing"), false);
        var r = await Common.Run(() => FontRepair.RemoveFonts(files, "fix.dup"), _page.Status);
        if (r is null) return;
        Ui.Ask(null, L.T(r.Ok ? "fonts.fixedTitle" : "fonts.partlyFixedTitle"), L.T("fonts.fixedText") + (r.NeedsRestart ? "\n" + L.T("fonts.restartNote") : ""), r.Lines, L.T("ui.ok"));
        await _page.Scan();
    }
}

/// <summary>"Copy fonts for another computer": a zip with the fonts (old Arabic ones converted), an installer and a list.</summary>
public sealed class PackWindow : Window
{
    private readonly FontScan _scan;
    private readonly CheckBox _user, _other, _windows, _dedupe, _convert;
    private readonly TextBlock _count = Ui.Text("", 13, Theme.Ink, bold: true);
    private readonly StatusLine _status = new();
    private readonly Button _go;

    public static void Show(Window? owner, FontScan? scan)
    {
        if (scan is null) { Ui.Message(owner, L.T("pack.title"), L.T("pack.scanFirst")); return; }
        new PackWindow(scan) { Owner = owner }.ShowDialog();
    }

    private PackWindow(FontScan scan)
    {
        _scan = scan;
        Ui.Setup(this, L.T("pack.title"), 560, 520);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var o = new FontPack.Options();
        var sp = new StackPanel { Margin = new Thickness(22) };
        sp.Children.Add(Ui.Title(L.T("pack.title"), 18));
        sp.Children.Add(Ui.Wrap(L.T("pack.intro"), 12.5).Margin(0, 6, 0, 12));
        _user = Ui.Check(L.T("pack.user"), o.UserFonts);
        _other = Ui.Check(L.T("pack.other"), o.OtherSystemFonts);
        _windows = Ui.Check(L.T("pack.windows"), o.WindowsFonts);
        _dedupe = Ui.Check(L.T("pack.dedupe"), o.SkipLookAlikes);
        _convert = Ui.Check(L.T("pack.convert"), o.ConvertOldArabic);
        foreach (var c in new[] { _user, _other, _windows, _dedupe, _convert })
        {
            c.Checked += (_, _) => Count();
            c.Unchecked += (_, _) => Count();
            sp.Children.Add(c);
        }
        sp.Children.Add(_count.Margin(0, 12, 0, 6));
        sp.Children.Add(_status);
        _go = Ui.Button(L.T("pack.save"), "", Theme.PrimaryButton, async () => await Save());
        var close = Ui.Button(L.T("ui.close"), null, Theme.Button, Close);
        close.IsCancel = true;
        var row = Ui.Row(_go, close).Spaced();
        row.HorizontalAlignment = HorizontalAlignment.Right;
        row.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(row);
        Content = sp;
        Count();
    }

    private FontPack.Options Options() => new()
    {
        UserFonts = _user.IsChecked == true, OtherSystemFonts = _other.IsChecked == true, WindowsFonts = _windows.IsChecked == true,
        SkipLookAlikes = _dedupe.IsChecked == true, ConvertOldArabic = _convert.IsChecked == true,
    };

    private void Count()
    {
        var files = FontPack.Choose(_scan, Options());
        _count.Text = L.T("pack.count", Fmt.Count(files.Count), Fmt.Size(files.Sum(f => f.Size)), Fmt.Count(files.Count(f => f.LegacyConvertible)));
    }

    private async Task Save()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"IBRA-Fonts-{DateTime.Now:yyyy-MM-dd}.zip",
            Filter = "Zip (*.zip)|*.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dialog.ShowDialog(this) != true) return;
        _go.IsEnabled = false;
        var ct = _status.Start(L.T("pack.working"));
        var progress = new Progress<(int Done, int Total, string Name)>(p => _status.Report(p.Done, p.Total, p.Name));
        var options = Options();
        var r = await Common.Run(() => FontPack.Export(_scan, options, dialog.FileName, progress, ct), _status);
        _go.IsEnabled = true;
        if (r is null) return;
        _status.Finish(L.T("pack.done", Fmt.Count(r.Fonts), Fmt.Size(new FileInfo(r.ZipPath).Length), Fmt.Count(r.Converted)), r.Skipped.Count == 0 ? Theme.Green : Theme.Amber);
        if (Ui.Ask(this, L.T("pack.title"), L.T("pack.doneText"), r.Skipped, L.T("clean.openFolder"), L.T("ui.close")) == 0) Ui.ShowInFolder(r.ZipPath);
    }
}
