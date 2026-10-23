using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IbraCare.Core;
using IbraCare.Core.Cleaner;

namespace IbraCare.App.Pages;

public sealed class CleanerPage : CarePage
{
    public override string Id => "cleaner";
    private readonly StatusLine _status = new();
    private readonly StackPanel _list = new();
    private readonly Dictionary<string, (CheckBox Box, TextBlock Size, StackPanel Badges)> _rows = new();
    private readonly Button _scan, _preview;
    private readonly TextBlock _summary = Ui.Text("", 13, Theme.Ink, bold: true);
    private readonly Border _admin;
    private ScanReport? _report;

    public CleanerPage()
    {
        Rows(Auto, Auto, Auto, Auto, Star);
        _scan = Ui.Button(L.T("clean.scan"), "", Theme.Button, async () => await Scan());
        _preview = Ui.Button(L.T("clean.previewAndClean"), "", Theme.PrimaryButton, Preview);
        _preview.IsEnabled = false;
        Children.Add(PageHeader("IBRA Deep Cleaner", L.T("clean.subtitle"), _scan, _preview));
        _admin = Common.AdminBanner("cleaner", L.T("clean.adminText"));
        Children.Add(_admin.At(0, 1));
        Children.Add(_status.At(0, 2));

        var bar = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        bar.Children.Add(_summary);
        var all = Ui.Button(L.T("ui.selectAll"), null, Theme.ToolButton, () => SetAll(true));
        var none = Ui.Button(L.T("ui.selectNone"), null, Theme.ToolButton, () => SetAll(false));
        var defaults = Ui.Button(L.T("clean.defaults"), null, Theme.ToolButton, () =>
        {
            Settings.Current.TargetChoices.Clear();
            Settings.Current.Save();
            foreach (var t in CleanTargets.All) _rows[t.Id].Box.IsChecked = t.DefaultOn;
        });
        bar.Children.Add(Ui.Row(all, none, defaults).At(1));
        bar.Margin = new Thickness(0, 0, 0, 8);
        Children.Add(bar.At(0, 3));

        foreach (var group in CleanTargets.All.GroupBy(t => t.Group))
        {
            _list.Children.Add(Ui.Text(L.T("group." + group.Key), 13, Theme.Gold, bold: true).Margin(2, 10, 0, 6));
            foreach (var t in group) _list.Children.Add(TargetRow(t));
        }
        Children.Add(Common.Scroll(_list).At(0, 4));
        UpdateSummary();
    }

    private Border TargetRow(CleanTarget t)
    {
        var s = Settings.Current;
        var g = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto);
        var text = new StackPanel();
        var name = Ui.Row(Ui.Text(t.Name, 13.5, Theme.Ink, bold: true));
        var badges = new StackPanel { Orientation = Orientation.Horizontal };
        if (t.NeedsAdmin) badges.Children.Add(Ui.Badge(L.T("clean.badgeAdmin"), Theme.Amber, Theme.AmberSoft));
        if (t.Risk == Risk.Careful) badges.Children.Add(Ui.Badge(L.T("clean.badgeCareful"), Theme.Amber, Theme.AmberSoft));
        if (t.PermanentOnly) badges.Children.Add(Ui.Badge(L.T("clean.badgePermanent"), Theme.Red, Theme.RedSoft));
        name.Children.Add(badges);
        text.Children.Add(name);
        text.Children.Add(Ui.Wrap(t.Description, 12).Margin(0, 2, 0, 0));
        var box = new CheckBox { Style = Theme.CheckBox, Content = text, IsChecked = t.IsOn(s), VerticalContentAlignment = VerticalAlignment.Top };
        box.Checked += (_, _) => Choose(t, true);
        box.Unchecked += (_, _) => Choose(t, false);
        g.Children.Add(box);
        var size = Ui.Ltr("", 13, Theme.Muted, bold: true);
        size.Margin = new Thickness(16, 0, 8, 0);
        g.Children.Add(size.At(1));
        var open = Ui.Button("", "", Theme.ToolButton, () =>
        {
            var root = Scanner.ResolveRoots(t).FirstOrDefault(Directory.Exists);
            if (root is null) Ui.Message(null, t.Name, L.T("clean.noFolder"));
            else Ui.ShowInFolder(root);
        });
        open.ToolTip = L.T("clean.openFolder");
        open.Padding = new Thickness(8, 6, 8, 6);
        if (t.Special is not null) open.Visibility = Visibility.Hidden;
        g.Children.Add(open.At(2));
        _rows[t.Id] = (box, size, badges);
        var card = Ui.Card(g, new Thickness(12, 10, 12, 10));
        card.Margin = new Thickness(0, 0, 0, 6);
        return card;
    }

    private void Choose(CleanTarget t, bool on)
    {
        var s = Settings.Current;
        s.TargetChoices[t.Id] = on;
        s.Save();
        UpdateSummary();
    }

    private void SetAll(bool on)
    {
        foreach (var t in CleanTargets.All) _rows[t.Id].Box.IsChecked = on;
    }

    private List<CleanTarget> Chosen() => CleanTargets.All.Where(t => _rows[t.Id].Box.IsChecked == true).ToList();

    private void UpdateSummary()
    {
        var chosen = Chosen();
        _admin.Visibility = !Platform.IsAdmin && chosen.Any(t => t.NeedsAdmin) ? Visibility.Visible : Visibility.Collapsed;
        if (_report is null)
        {
            _summary.Text = L.T("clean.chosenCount", chosen.Count, CleanTargets.All.Count);
            return;
        }
        var shown = _report.Targets.Where(r => chosen.Any(c => c.Id == r.Target.Id)).ToList();
        _summary.Text = L.T("clean.found", Fmt.Count(shown.Sum(r => r.SelectedCount + (r.Target.Special is null ? 0 : (int)r.SpecialCount))), Fmt.Size(shown.Sum(r => r.SelectedSize)));
    }

    private async Task Scan()
    {
        var chosen = Chosen();
        if (chosen.Count == 0) { Ui.Message(null, "IBRA Deep Cleaner", L.T("clean.nothingChosen")); return; }
        _scan.IsEnabled = _preview.IsEnabled = false;
        foreach (var r in _rows.Values) r.Size.Text = "";
        var ct = _status.Start(L.T("clean.scanning"));
        var progress = new Progress<string>(name => _status.Report(0, 0, name));
        var report = await Common.Run(() => new Scanner { Specials = new WindowsSpecials() }.Scan(chosen, progress, ct), _status);
        _scan.IsEnabled = true;
        if (report is null) return;
        _report = report;
        AppState.LastClean = report;
        AppState.Notify();
        ShowResults();
        _status.Finish(L.T("clean.scanDone", Fmt.Size(report.SelectedSize)));
        _preview.IsEnabled = report.Targets.Any(t => t.SelectedCount > 0 || t.SpecialCount > 0);
    }

    private void ShowResults()
    {
        if (_report is null) return;
        foreach (var r in _report.Targets)
        {
            var row = _rows[r.Target.Id];
            row.Size.Text = r.Count == 0 ? "0" : $"{Fmt.Size(r.SelectedSize)}  ·  {Fmt.Count(r.Target.Special is null ? r.SelectedCount : r.SpecialCount)}";
            row.Size.Foreground = r.Size > 0 ? Theme.Ink : Theme.Muted;
            // Remove old result badges (keep the fixed ones from the start).
            foreach (var b in row.Badges.Children.OfType<Border>().Where(b => b.Tag is "result").ToList()) row.Badges.Children.Remove(b);
            void Add(Border b) { b.Tag = "result"; row.Badges.Children.Add(b); }
            if (r.NeedsAdmin && !Platform.IsAdmin) Add(Ui.Badge(L.T("clean.needsAdmin"), Theme.Amber, Theme.AmberSoft));
            var inUse = r.Items.Count(i => i.InUse);
            if (inUse > 0) Add(Ui.Badge(L.T("clean.inUseCount", inUse), Theme.Blue, Theme.BlueSoft));
            var running = r.Target.Apps.Where(a => Platform.IsProcessRunning(a)).ToList();
            if (running.Count > 0 && r.Target.Apps.Length > 0 && r.Target.Id != "thumbCache")
                Add(Ui.Badge(L.T("clean.closeApp", string.Join(L.Sep, running)), Theme.Amber, Theme.AmberSoft));
            if (r.Notes.Count > 0) Add(new Border { Tag = "result", Child = Ui.Glyph("", 13, Theme.Muted), ToolTip = string.Join("\n", r.Notes), Margin = new Thickness(6, 0, 0, 0) });
        }
        UpdateSummary();
    }

    private void Preview()
    {
        if (_report is null) return;
        var chosenIds = Chosen().Select(c => c.Id).ToHashSet();
        var report = new ScanReport();
        report.Targets.AddRange(_report.Targets.Where(t => chosenIds.Contains(t.Target.Id)));
        var w = new PreviewWindow(report) { Owner = Window.GetWindow(this) };
        if (w.ShowDialog() == true)
        {
            _report = null;
            foreach (var r in _rows.Values) r.Size.Text = "";
            _preview.IsEnabled = false;
            if (w.Result is { } res)
                _status.Finish(L.T(res.Mode == CleanMode.Quarantine ? "clean.doneQuarantine" : "clean.donePermanent", Fmt.Count(res.Removed), Fmt.Size(res.Freed)) +
                               (res.Skipped.Count > 0 ? "  " + L.T("clean.doneSkipped", Fmt.Count(res.Skipped.Count)) : ""),
                    res.Skipped.Count > 0 ? Theme.Amber : Theme.Green);
            UpdateSummary();
        }
        else ShowResults();
    }
}

/// <summary>Every file found, with a tick to keep or remove it, before anything happens.</summary>
public sealed class PreviewWindow : Window
{
    private readonly ScanReport _report;
    private readonly List<ScanItem> _all;
    private readonly ListView _list;
    private readonly TextBox _filter = Ui.Input();
    private readonly TextBlock _count = Ui.Text("", 13, Theme.Ink, bold: true);
    private readonly RadioButton _quarantine, _permanent;
    private readonly StatusLine _status = new();
    private readonly Button _go;
    public CleanResult? Result { get; private set; }

    public PreviewWindow(ScanReport report)
    {
        _report = report;
        _all = report.Targets.SelectMany(t => t.Items).OrderByDescending(i => i.Size).ToList();
        Ui.Setup(this, L.T("prev.title"), 1060, 720);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(20) };
        foreach (var h in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto })
            root.RowDefinitions.Add(new RowDefinition { Height = h });

        var head = new StackPanel();
        head.Children.Add(Ui.Title(L.T("prev.title"), 19));
        head.Children.Add(Ui.Wrap(L.T("prev.help"), 12.5).Margin(0, 4, 0, 12));
        root.Children.Add(head);

        var tools = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        _filter.ToolTip = L.T("prev.filter");
        _filter.TextChanged += (_, _) => ApplyFilter();
        var filterRow = Ui.Columns(GridLength.Auto, new GridLength(1, GridUnitType.Star));
        filterRow.Children.Add(Ui.Glyph("", 14, Theme.Muted).Margin(0, 0, 8, 0));
        filterRow.Children.Add(_filter.At(1));
        tools.Children.Add(filterRow);
        tools.Children.Add(Ui.Row(
            Ui.Button(L.T("ui.selectAll"), null, Theme.ToolButton, () => SetShown(true)),
            Ui.Button(L.T("ui.selectNone"), null, Theme.ToolButton, () => SetShown(false)),
            Ui.Button(L.T("prev.keepSelected"), "", Theme.Button, KeepSelected)).Spaced(6).Margin(12, 0, 0, 0).At(1));
        tools.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(tools.At(0, 1));

        _list = Ui.List([
            ("", 36, o => Tick((ScanItem)o)),
            (L.T("prev.colFile"), 470, o => FileCell((ScanItem)o)),
            (L.T("report.size"), 90, o => Ui.Ltr(Fmt.Size(((ScanItem)o).Size), 12.5)),
            (L.T("prev.colDate"), 130, o => Ui.Ltr(Fmt.Date(((ScanItem)o).Modified), 12, Theme.Muted)),
            (L.T("prev.colPlace"), 220, o => Ui.Text(CleanTargets.ById(((ScanItem)o).TargetId)?.Name ?? "", 12, Theme.Muted)),
        ]);
        _list.ItemsSource = _all;
        var listCard = Ui.Card(_list, new Thickness(4));
        root.Children.Add(listCard.At(0, 2));

        var specials = report.Targets.Where(t => t.Target.Special is not null && t.SpecialCount > 0).ToList();
        var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var sp in specials)
            bottom.Children.Add(Ui.Row(Ui.Glyph("", 14, Theme.Red), Ui.Text(L.T("prev.special", sp.Target.Name, Fmt.Count(sp.SpecialCount), Fmt.Size(sp.SpecialSize)), 12.5, Theme.Red)).Spaced());
        bottom.Children.Add(_count.Margin(0, 6, 0, 8));
        var modes = new UniformGrid3 { Columns = 2 };
        var def = Settings.Current.DefaultCleanMode;
        _quarantine = Ui.Choice(L.T("prev.quarantine"), "mode", def == CleanMode.Quarantine, L.T("prev.quarantineSub", Settings.Current.QuarantineDays));
        _permanent = Ui.Choice(L.T("prev.permanent"), "mode", def == CleanMode.Permanent, L.T("prev.permanentSub"));
        _quarantine.Margin = new Thickness(0, 0, 8, 0);
        modes.Children.Add(_quarantine);
        modes.Children.Add(_permanent);
        bottom.Children.Add(modes);
        root.Children.Add(bottom.At(0, 3));
        root.Children.Add(_status.At(0, 4));

        _go = Ui.Button(L.T("prev.clean"), "", Theme.PrimaryButton, async () => await Clean());
        var cancel = Ui.Button(L.T("ui.cancel"), null, Theme.Button, Close);
        cancel.IsCancel = true;
        var actions = Ui.Row(_go, cancel).Spaced();
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        actions.Margin = new Thickness(0, 12, 0, 0);
        root.Children.Add(actions.At(0, 5));
        Content = root;
        UpdateCount();
    }

    private FrameworkElement Tick(ScanItem item)
    {
        var c = new CheckBox { Style = Theme.CheckBox, IsChecked = item.Selected, Content = null, HorizontalAlignment = HorizontalAlignment.Center };
        c.Checked += (_, _) => { item.Selected = true; UpdateCount(); };
        c.Unchecked += (_, _) => { item.Selected = false; UpdateCount(); };
        return c;
    }

    private static FrameworkElement FileCell(ScanItem item)
    {
        var sp = new StackPanel();
        var name = Ui.Ltr(Path.GetFileName(item.Path), 12.5, Theme.Ink);
        if (item.InUse) name.Text += "   (" + L.T("prev.inUse") + ")";
        sp.Children.Add(name);
        sp.Children.Add(Ui.PathText(Path.GetDirectoryName(item.Path), 11));
        return sp;
    }

    private IEnumerable<ScanItem> Shown() => _list.ItemsSource as IEnumerable<ScanItem> ?? _all;

    private void ApplyFilter()
    {
        var f = _filter.Text.Trim();
        _list.ItemsSource = f.Length == 0 ? _all : _all.Where(i => i.Path.Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void SetShown(bool on)
    {
        foreach (var i in Shown()) i.Selected = on;
        _list.Items.Refresh();
        UpdateCount();
    }

    /// <summary>Adds the highlighted rows to the keep list (never cleaned again) and unticks them.</summary>
    private void KeepSelected()
    {
        var rows = _list.SelectedItems.OfType<ScanItem>().ToList();
        if (rows.Count == 0) { Ui.Message(this, L.T("prev.keepSelected"), L.T("prev.keepHint")); return; }
        var s = Settings.Current;
        foreach (var r in rows)
        {
            r.Selected = false;
            if (!s.KeepList.Contains(r.Path, StringComparer.OrdinalIgnoreCase)) s.KeepList.Add(r.Path);
        }
        s.Save();
        _list.Items.Refresh();
        UpdateCount();
        Ui.Message(this, L.T("prev.keepSelected"), L.T("prev.keptDone", rows.Count));
    }

    private void UpdateCount()
    {
        var chosen = _all.Where(i => i.Selected).ToList();
        _count.Text = L.T("prev.count", Fmt.Count(chosen.Count), Fmt.Count(_all.Count), Fmt.Size(chosen.Sum(i => i.Size)));
    }

    private async Task Clean()
    {
        var chosen = _all.Count(i => i.Selected);
        var specials = _report.Targets.Where(t => t.Target.Special is not null && t.SpecialCount > 0).ToList();
        if (chosen == 0 && specials.Count == 0) { Ui.Message(this, L.T("prev.title"), L.T("prev.nothing")); return; }
        var mode = _permanent.IsChecked == true ? CleanMode.Permanent : CleanMode.Quarantine;
        var msg = L.T(mode == CleanMode.Quarantine ? "prev.confirmQuarantine" : "prev.confirmPermanent", Fmt.Count(chosen), Fmt.Size(_all.Where(i => i.Selected).Sum(i => i.Size)));
        if (specials.Count > 0) msg += "\n" + L.T("prev.confirmSpecial", string.Join(L.Sep, specials.Select(s => s.Target.Name)));
        if (Ui.Ask(this, L.T("prev.confirmTitle"), msg, L.T("prev.clean"), L.T("ui.cancel")) != 0) return;

        _go.IsEnabled = false;
        var ct = _status.Start(L.T("prev.cleaning"));
        var progress = new Progress<(int Done, int Total, string Path)>(p => _status.Report(p.Done, p.Total, p.Path));
        var result = await Common.Run(() => CleanRunner.Run(_report, mode, new WindowsSpecials(), progress, ct), _status);
        _go.IsEnabled = true;
        if (result is null) return;
        Result = result;
        if (result.Skipped.Count > 0)
            Ui.Ask(this, L.T("prev.doneTitle"), L.T("prev.doneWithSkips", Fmt.Count(result.Removed), Fmt.Size(result.Freed), Fmt.Count(result.Skipped.Count)), result.Skipped, L.T("ui.ok"));
        DialogResult = true;
    }
}
