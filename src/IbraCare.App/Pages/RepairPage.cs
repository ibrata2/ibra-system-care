using System.Windows;
using System.Windows.Controls;
using IbraCare.Core;
using IbraCare.Core.Repair;

namespace IbraCare.App.Pages;

public sealed class RepairPage : CarePage
{
    public override string Id => "repair";
    private readonly Border _body = new();
    private readonly StatusLine _status = new();
    private int _tab;

    public RepairPage()
    {
        Rows(Auto, Auto, Auto, Star);
        var report = Ui.Button(L.T("repair.makeReport"), "", Theme.Button, () => MakeReport());
        var restore = Ui.Button(L.T("repair.restorePoint"), "", Theme.Button, async () => await RestorePoint());
        Children.Add(PageHeader("IBRA Repair Center", L.T("repair.subtitle"), restore, report));
        Children.Add(_status.At(0, 1));
        Children.Add(Common.Tabs(i => { _tab = i; Render(); }, L.T("repair.tabHistory"), L.T("repair.tabBackups"), L.T("repair.tabReports")).At(0, 2));
        Children.Add(_body.At(0, 3));
        Journal.Changed += () => Dispatcher.BeginInvoke(() => { if (IsVisible && _tab == 0) Render(); });
    }

    public override void OnShown() => Render();

    private void Render() => _body.Child = _tab switch { 0 => HistoryView(), 1 => BackupsView(), _ => ReportsView() };

    // ---------- history ----------

    private UIElement HistoryView()
    {
        var entries = Journal.All.ToList();
        if (entries.Count == 0) return Common.Empty("", L.T("repair.noHistory"));
        var g = Ui.Columns(new GridLength(1.3, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
        var detail = new Border();
        var list = Ui.List([
            (L.T("report.time"), 130, o => Ui.Ltr(Fmt.Date(((JournalEntry)o).Time), 12, Theme.Muted)),
            (L.T("repair.colModule"), 110, o => Ui.Text(L.T("module." + ((JournalEntry)o).Module), 12, Theme.Gold)),
            (L.T("report.action"), 300, o =>
            {
                var e = (JournalEntry)o;
                var t = Ui.Text(e.Title, 13, e.Undone ? Theme.Muted : Theme.Ink, bold: true);
                if (e.Undone) t.TextDecorations = TextDecorations.Strikethrough;
                return t;
            }),
            (L.T("report.result"), 110, o => StatusBadge((JournalEntry)o)),
        ]);
        list.SelectionMode = SelectionMode.Single;
        list.ItemsSource = entries;
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is JournalEntry e) detail.Child = EntryDetail(e); };
        g.Children.Add(Ui.Card(list, new Thickness(4)).Margin(0, 0, 12, 0));
        detail.Child = Common.Empty("", L.T("repair.pickEntry"));
        g.Children.Add(Common.Scroll(detail).At(1));
        return g;
    }

    private static Border StatusBadge(JournalEntry e)
    {
        if (e.Undone) return Ui.Badge(L.T("repair.undone"), Theme.Muted, Theme.Hover);
        return e.Status switch
        {
            EntryStatus.Success => Ui.Badge(L.T("status.Success"), Theme.Green, Theme.GreenSoft),
            EntryStatus.Partial => Ui.Badge(L.T("status.Partial"), Theme.Amber, Theme.AmberSoft),
            EntryStatus.Failed => Ui.Badge(L.T("status.Failed"), Theme.Red, Theme.RedSoft),
            _ => Ui.Badge(L.T("status.Info"), Theme.Blue, Theme.BlueSoft),
        };
    }

    private UIElement EntryDetail(JournalEntry e)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(e.Title, 16, Theme.Ink, bold: true, wrap: TextWrapping.Wrap));
        sp.Children.Add(Ui.Row(Ui.Ltr(Fmt.Date(e.Time), 12, Theme.Muted), StatusBadge(e)).Spaced().Margin(0, 4, 0, 10));
        var box = new TextBox
        {
            Style = Theme.Input, IsReadOnly = true, FontFamily = Theme.Mono, FontSize = 12, MinHeight = 160, MaxHeight = 380,
            Text = e.Details.Count == 0 ? L.T("repair.noDetails") : string.Join(Environment.NewLine, e.Details),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top, FlowDirection = FlowDirection.LeftToRight,
        };
        sp.Children.Add(box);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        if (e.CanUndo)
        {
            var set = BackupStore.Get(e.BackupId!);
            var usable = set is not null && set.State != BackupState.Purged;
            var undo = Ui.Button(L.T("repair.undo"), "", Theme.PrimaryButton, async () => await Undo(e));
            undo.IsEnabled = usable;
            actions.Children.Add(undo);
            if (!usable) sp.Children.Add(Ui.Wrap(L.T("repair.backupPurged"), 12).Margin(0, 8, 0, 0));
        }
        actions.Children.Add(Ui.Button(L.T("repair.copy"), "", Theme.Button, () =>
        {
            try { Clipboard.SetText($"{e.Title}\n{Fmt.Date(e.Time)}\n{string.Join("\n", e.Details)}"); } catch (Exception) { }
        }));
        actions.Spaced();
        sp.Children.Add(actions);
        return Ui.Card(sp, new Thickness(16));
    }

    private async Task Undo(JournalEntry e)
    {
        var set = BackupStore.Get(e.BackupId!);
        if (set is null) return;
        var lines = set.Items.Where(i => !i.Restored).Select(i => i.Kind == BackupItemKind.File
            ? (i.CreatedByChange ? $"{L.T("repair.willDelete")}: {i.Original}" : $"{L.T("repair.willRestore")}: {i.Original}")
            : $"{L.T("repair.willRestoreReg")}: {i.Key}\\{i.Name}").ToList();
        if (set.Module == "fonts" && set.Items.Any(i => i.Hive == Hive.LocalMachine || (i.Kind == BackupItemKind.File && FontLocationIsSystem(i.Original))) && !Platform.IsAdmin)
        {
            if (Ui.Ask(null, L.T("fonts.adminNeededTitle"), L.T("repair.undoAdmin"), L.T("ui.restartAdmin"), L.T("ui.cancel")) == 0) Program.RestartAsAdmin("repair");
            return;
        }
        if (Ui.Ask(null, L.T("repair.undoTitleAsk"), L.T("repair.undoAsk", e.Title), lines, L.T("repair.undo"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("repair.undoing"), false);
        var r = await Common.Run(() => Journal.Undo(e, Registries.Default), _status);
        if (r is null) return;
        _status.Finish(L.T("repair.undoCounts", r.Restored, r.Skipped.Count, r.Failed.Count), r.Ok ? Theme.Green : Theme.Amber);
        if (r.Skipped.Count + r.Failed.Count > 0) Ui.Ask(null, L.T("repair.undoTitleAsk"), L.T("repair.undoPartial"), r.Skipped.Concat(r.Failed).ToList(), L.T("ui.ok"));
        Render();
    }

    private static bool FontLocationIsSystem(string path) =>
        IbraCare.Core.Fonts.FontInventory.SystemFontsDir is { } sys && IbraCare.Core.Cleaner.Safety.IsInside(path, sys);

    // ---------- backups ----------

    private UIElement BackupsView()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var sets = BackupStore.All();
        var head = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        head.Children.Add(Ui.Wrap(L.T("repair.backupsIntro", Fmt.Size(sets.Where(s => s.State != BackupState.Purged).Sum(s => s.Size)), Settings.Current.QuarantineDays, Settings.Current.BackupDays), 12.5));
        head.Children.Add(Ui.Row(
            Ui.Button(L.T("repair.openFolder"), "", Theme.Button, () => Ui.ShowInFolder(Paths.Backups)),
            Ui.Button(L.T("repair.purgeExpired"), "", Theme.Button, () =>
            {
                var n = BackupStore.PurgeExpired();
                _status.Finish(L.T("repair.purgedN", n));
                Render();
            })).Spaced().Margin(12, 0, 0, 0).At(1));
        head.Margin = new Thickness(0, 0, 0, 10);
        g.Children.Add(head);
        if (sets.Count == 0)
        {
            g.Children.Add(Common.Empty("", L.T("repair.noBackups")).At(0, 1));
            return g;
        }
        var list = Ui.List([
            (L.T("report.time"), 130, o => Ui.Ltr(Fmt.Date(((BackupSet)o).Created), 12, Theme.Muted)),
            (L.T("repair.colContent"), 330, o => Ui.Text(((BackupSet)o).Title, 13, Theme.Ink, bold: true)),
            (L.T("report.files"), 70, o => Ui.Ltr(Fmt.Count(((BackupSet)o).Count), 12)),
            (L.T("report.size"), 90, o => Ui.Ltr(Fmt.Size(((BackupSet)o).Size), 12)),
            (L.T("repair.colExpires"), 110, o => Ui.Ltr(((BackupSet)o).Expires.ToString("yyyy-MM-dd"), 12, Theme.Muted)),
            (L.T("repair.colState"), 110, o => Ui.Text(L.T("bstate." + ((BackupSet)o).State), 12, ((BackupSet)o).State == BackupState.Active ? Theme.Green : Theme.Muted)),
            ("", 230, o =>
            {
                var s = (BackupSet)o;
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                if (s.State == BackupState.Active)
                {
                    row.Children.Add(Ui.Button(L.T("repair.restore"), "", Theme.Button, async () => await RestoreSet(s)));
                    row.Children.Add(Ui.Button(L.T("repair.purge"), "", Theme.DangerButton, () =>
                    {
                        if (Ui.Ask(null, L.T("repair.purge"), L.T("repair.purgeAsk", s.Title, Fmt.Size(s.Size)), L.T("repair.purge"), L.T("ui.cancel")) != 0) return;
                        try { BackupStore.Purge(s.Id); Render(); } catch (Exception e) { Ui.Error(e); }
                    }).Margin(6, 0, 0, 0));
                }
                return row;
            }),
        ]);
        list.ItemsSource = sets;
        g.Children.Add(Ui.Card(list, new Thickness(4)).At(0, 1));
        return g;
    }

    private async Task RestoreSet(BackupSet s)
    {
        var entry = Journal.All.FirstOrDefault(e => e.BackupId == s.Id && !e.Undone);
        if (entry is not null) { await Undo(entry); return; }
        if (Ui.Ask(null, L.T("repair.restore"), L.T("repair.undoAsk", s.Title), s.Items.Select(i => i.Kind == BackupItemKind.File ? i.Original : $"{i.Key}\\{i.Name}").ToList(), L.T("repair.restore"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("repair.undoing"), false);
        var r = await Common.Run(() => BackupStore.Restore(s.Id, Registries.Default), _status);
        if (r is null) return;
        Journal.Add("repair", r.Ok ? EntryStatus.Success : EntryStatus.Partial, "repair.undoTitle", r.Skipped.Concat(r.Failed), null, s.Title);
        _status.Finish(L.T("repair.undoCounts", r.Restored, r.Skipped.Count, r.Failed.Count), r.Ok ? Theme.Green : Theme.Amber);
        Render();
    }

    // ---------- reports ----------

    private UIElement ReportsView()
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Wrap(L.T("repair.reportsIntro"), 12.5).Margin(0, 0, 0, 10));
        sp.Children.Add(Ui.Row(
            Ui.Button(L.T("repair.makeReport"), "", Theme.PrimaryButton, () => { MakeReport(); Render(); }),
            Ui.Button(L.T("repair.openReports"), "", Theme.Button, () => Ui.ShowInFolder(Paths.Reports))).Spaced().Margin(0, 0, 0, 14));
        try
        {
            var files = new DirectoryInfo(Paths.Reports).EnumerateFiles("*.html").OrderByDescending(f => f.LastWriteTime).Take(50).ToList();
            if (files.Count == 0) sp.Children.Add(Common.Empty("", L.T("repair.noReports")));
            foreach (var f in files)
            {
                var row = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
                row.Children.Add(new StackPanel { Children = { Ui.Ltr(f.Name, 13, Theme.Ink), Ui.Ltr(Fmt.Date(f.LastWriteTime) + "  ·  " + Fmt.Size(f.Length), 11.5, Theme.Muted) } });
                var file = f.FullName;
                row.Children.Add(Ui.Button(L.T("repair.open"), "", Theme.Button, () => Ui.Open(file)).At(1));
                sp.Children.Add(Ui.Card(row, new Thickness(12, 8, 12, 8)).Margin(0, 0, 0, 6));
            }
        }
        catch (Exception e) { sp.Children.Add(Ui.Wrap(Errors.Friendly(e))); }
        return Common.Scroll(sp);
    }

    public static void MakeReport()
    {
        try
        {
            var file = Reports.Write(AppState.LastClean, AppState.LastFonts, AppState.LastPhotoshop);
            Ui.Open(file);
        }
        catch (Exception e) { Ui.Error(e); }
    }

    private async Task RestorePoint()
    {
        if (Ui.Ask(null, L.T("repair.restorePoint"), L.T("repair.restorePointText"), L.T("repair.restorePointCreate"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("repair.restorePointWorking"), false);
        var ok = await Task.Run(() => SystemRestore.CreatePoint());
        _status.Finish(L.T(ok ? "repair.restorePointDone" : "repair.restorePointFailed"), ok ? Theme.Green : Theme.Amber);
        if (_tab == 0) Render();
    }
}
