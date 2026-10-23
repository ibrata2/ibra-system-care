using System.Windows;
using System.Windows.Controls;
using IbraCare.Core;
using IbraCare.Core.Cleaner;
using IbraCare.Core.Fonts;
using IbraCare.Core.Repair;

namespace IbraCare.App.Pages;

public sealed class HomePage : CarePage
{
    public override string Id => "home";
    private readonly TextBlock _space, _fonts, _undo, _backups;
    private readonly StatusLine _status = new();
    private readonly Button _scan;

    public HomePage()
    {
        Rows(Auto, Auto, Auto, Auto, Star);
        _scan = Ui.Button(L.T("home.quickScan"), "", Theme.PrimaryButton, async () => await QuickScan());
        var report = Ui.Button(L.T("repair.makeReport"), "", Theme.Button, () => RepairPage.MakeReport());
        Children.Add(PageHeader(L.T("home.title"), L.T("home.subtitle"), report, _scan));
        Children.Add(_status.At(0, 1));

        var tiles = new UniformGrid4();
        tiles.Children.Add(Ui.Tile("—", L.T("home.tileSpace"), "", Theme.Gold, out _space));
        tiles.Children.Add(Ui.Tile("—", L.T("home.tileFonts"), "", Theme.Gold, out _fonts));
        tiles.Children.Add(Ui.Tile("—", L.T("home.tileUndo"), "", Theme.Gold, out _undo));
        tiles.Children.Add(Ui.Tile("—", L.T("home.tileBackups"), "", Theme.Gold, out _backups));
        Children.Add(tiles.At(0, 2));

        var modules = new UniformGrid3 { Margin = new Thickness(0, 6, 0, 0) };
        modules.Children.Add(Module("", "IBRA Deep Cleaner", L.T("home.cleanerText"), "cleaner"));
        modules.Children.Add(Module("", "IBRA Font Doctor", L.T("home.fontsText"), "fonts"));
        modules.Children.Add(Module("", "IBRA Repair Center", L.T("home.repairText"), "repair"));
        Children.Add(modules.At(0, 3));

        var safe = Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.Row(Ui.Glyph("", 16, Theme.Green), Ui.Text(L.T("home.safeTitle"), 13.5, Theme.Ink, bold: true)).Spaced(),
                Ui.Wrap(L.T("home.safeText"), 12.5).Margin(0, 6, 0, 0),
            },
        });
        safe.Margin = new Thickness(0, 12, 0, 0);
        safe.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(safe.At(0, 4));
        AppState.Changed += Refresh;
        Journal.Changed += () => Dispatcher.BeginInvoke(Refresh);
    }

    private Border Module(string glyph, string title, string text, string page)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Row(Ui.Glyph(glyph, 22, Theme.Gold), Ui.Text(title, 15, Theme.Ink, bold: true)).Spaced(10));
        sp.Children.Add(Ui.Wrap(text, 12.5).Margin(0, 8, 0, 12));
        var open = Ui.Button(L.T("home.open"), null, Theme.Button, () => CareApp.Instance.Main.Navigate(page));
        open.HorizontalAlignment = HorizontalAlignment.Left;
        sp.Children.Add(open);
        var c = Ui.Card(sp, new Thickness(16));
        c.Margin = new Thickness(0, 0, 10, 0);
        return c;
    }

    public override void OnShown() => Refresh();

    private void Refresh()
    {
        _space.Text = AppState.LastClean is { } c ? Fmt.Size(c.SelectedSize) : "—";
        _fonts.Text = AppState.LastFonts is { } f ? Fmt.Count(f.Issues.Count(i => i.Severity != Severity.Info)) : "—";
        _undo.Text = Fmt.Count(Journal.All.Count(e => e.CanUndo));
        _ = Task.Run(() => Fmt.Size(BackupStore.TotalSize())).ContinueWith(t => Dispatcher.BeginInvoke(() => _backups.Text = t.Result), TaskScheduler.Default);
    }

    /// <summary>Reads only: the chosen cleaner places and the fonts. Nothing is deleted from here.</summary>
    private async Task QuickScan()
    {
        _scan.IsEnabled = false;
        var ct = _status.Start(L.T("home.scanning"));
        var settings = Settings.Current;
        var progress = new Progress<string>(s => _status.Report(0, 0, s));
        var clean = await Common.Run(() => new Scanner { Specials = new WindowsSpecials() }.Scan(CleanTargets.All.Where(t => t.IsOn(settings)), progress, ct), _status);
        if (clean is not null)
        {
            AppState.LastClean = clean;
            _status.Say(L.T("fonts.scanning"));
            var fonts = await Common.Run(() => FontInventory.Scan(null, null, ct), _status);
            if (fonts is not null)
            {
                AppState.LastFonts = fonts;
                AppState.LastPhotoshop = await Common.Run(() => PhotoshopDoctor.Diagnose(fonts));
                Journal.Add("system", EntryStatus.Info, "home.scanJournal",
                    [L.T("home.scanLine1", Fmt.Size(clean.SelectedSize), Fmt.Count(clean.Targets.Sum(t => t.SelectedCount))),
                     L.T("report.fontsSummary", Fmt.Count(fonts.Files.Count), Fmt.Count(fonts.CorruptCount), Fmt.Count(fonts.DuplicateGroups), Fmt.Count(fonts.ArabicReady.Count()))]);
                _status.Finish(L.T("home.scanDone"));
            }
            AppState.Notify();
        }
        _scan.IsEnabled = true;
        Refresh();
    }
}

/// <summary>Equal columns that wrap nothing: simple fixed grids for tiles and cards.</summary>
public class UniformGrid4 : System.Windows.Controls.Primitives.UniformGrid
{
    public UniformGrid4() { Columns = 4; }
}

public class UniformGrid3 : System.Windows.Controls.Primitives.UniformGrid
{
    public UniformGrid3() { Columns = 3; }
}
