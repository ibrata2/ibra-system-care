using System.Windows;
using System.Windows.Controls;
using IbraCare.Core;
using IbraCare.Core.Fonts;

namespace IbraCare.App.Pages;

/// <summary>Arabic fonts from the official Google Fonts repository: install or update, with the license checked first.</summary>
public sealed class TrustedFontsView : Grid
{
    private readonly FontsPage _page;
    private readonly FontScan? _scan;
    private readonly WrapPanel _cards = new();
    private bool _busy;

    public TrustedFontsView(FontsPage page, FontScan? scan)
    {
        _page = page;
        _scan = scan;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        head.Children.Add(Ui.Wrap(L.T("gf.intro"), 12.5));
        var updates = Ui.Button(L.T("gf.checkUpdates"), "", Theme.Button, async () => await CheckUpdates());
        updates.IsEnabled = scan is not null;
        head.Children.Add(updates.Margin(12, 0, 0, 0).At(1));
        head.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(head);
        foreach (var c in GoogleFonts.Arabic) _cards.Children.Add(Card(c));
        Children.Add(Common.Scroll(_cards).At(0, 1));
    }

    private FaceReport? Installed(CatalogFont c) =>
        _scan?.Faces.Where(f => string.Equals(f.Family, c.Family, StringComparison.OrdinalIgnoreCase) || f.Face.Family.StartsWith(c.Family, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Face.VersionNumber).FirstOrDefault();

    private Border Card(CatalogFont c)
    {
        var sp = new StackPanel { Width = 250 };
        sp.Children.Add(Ui.Name(c.Family, 14));
        sp.Children.Add(Ui.Row(Ui.Badge(L.T("kind." + c.Kind), Theme.Gold, Theme.GoldSoft).Margin(0, 0, 0, 0), Ui.Badge("OFL", Theme.Green, Theme.GreenSoft)).Margin(0, 4, 0, 4));
        sp.Children.Add(Ui.Wrap(c.Note, 12));
        var have = Installed(c);
        if (have is not null) sp.Children.Add(Ui.Text(L.T("gf.installedVersion", have.Face.VersionText), 11.5, Theme.Green).Margin(0, 6, 0, 0));
        var row = Ui.Row(
            Ui.Button(have is null ? L.T("gf.install") : L.T("gf.reinstall"), "", have is null ? Theme.PrimaryButton : Theme.Button, async () => await Install(c)),
            Ui.Button("", "", Theme.ToolButton, () => Ui.Open(c.PageUrl)));
        ((Button)row.Children[1]).ToolTip = L.T("gf.page");
        row.Spaced(6);
        row.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(row);
        var card = Ui.Card(sp, new Thickness(14));
        card.Margin = new Thickness(0, 0, 10, 10);
        return card;
    }

    private async Task Install(CatalogFont c)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var ct = _page.Status.Start(L.T("gf.downloadingFamily", c.Family));
            var progress = new Progress<string>(s => _page.Status.Report(0, 0, s));
            var dl = await Common.Run(() => GoogleFonts.DownloadAsync(c, progress, ct).GetAwaiter().GetResult(), _page.Status);
            if (dl is null) return;
            var lines = new List<string>
            {
                L.T("gf.source", dl.SourceUrl),
                L.T("gf.licenseOk", dl.License),
            };
            lines.AddRange(dl.Files.Select(f => $"✓ {f.FileName} — {f.Face.FullName} — {f.Face.VersionText}"));
            var have = Installed(c);
            if (have is not null) lines.Add(L.T("gf.willReplace", have.Face.VersionText));
            _page.Status.Hide();
            if (Ui.Ask(null, L.T("gf.verifiedTitle", c.Family), L.T("gf.verifiedText"), lines, L.T("gf.installNow"), L.T("ui.cancel")) != 0) return;
            var r = await Common.Run(() => FontRepair.InstallForUser(dl), _page.Status);
            if (r is null) return;
            _page.ShowStatus(L.T(r.Ok ? "gf.installed" : "gf.partlyInstalled", c.Family), r.Ok ? Theme.Green : Theme.Amber);
            if (!r.Ok) Ui.Ask(null, c.Family, L.T("gf.partlyInstalled", c.Family), r.Lines, L.T("ui.ok"));
        }
        finally { _busy = false; }
    }

    /// <summary>Downloads the families that are installed and compares versions; offers the newer ones.</summary>
    private async Task CheckUpdates()
    {
        if (_scan is null || _busy) return;
        var installed = GoogleFonts.Arabic.Select(c => (Font: c, Have: Installed(c))).Where(x => x.Have is not null).ToList();
        if (installed.Count == 0) { Ui.Message(null, L.T("gf.checkUpdates"), L.T("gf.noneInstalled")); return; }
        _busy = true;
        var newer = new List<(CatalogFont Font, string Old, string New)>();
        try
        {
            var ct = _page.Status.Start(L.T("gf.checking"));
            var i = 0;
            foreach (var (font, have) in installed)
            {
                _page.Status.Report(++i, installed.Count, font.Family);
                var dl = await Common.Run(() => GoogleFonts.DownloadAsync(font, null, ct).GetAwaiter().GetResult(), _page.Status);
                if (dl is null) return;
                var best = dl.Files.Select(f => f.Face).OrderByDescending(f => f.VersionNumber).First();
                if (best.VersionNumber > have!.Face.VersionNumber + 0.0005) newer.Add((font, have.Face.VersionText, best.VersionText));
            }
        }
        finally { _busy = false; }
        if (newer.Count == 0)
        {
            _page.ShowStatus(L.T("gf.allCurrent"));
            return;
        }
        _page.Status.Hide();
        if (Ui.Ask(null, L.T("gf.updatesTitle"), L.T("gf.updatesText"), newer.Select(n => $"{n.Font.Family}: {n.Old} → {n.New}").ToList(), L.T("gf.updateAll"), L.T("ui.cancel")) != 0) return;
        foreach (var n in newer)
        {
            var dl = await Common.Run(() => GoogleFonts.DownloadAsync(n.Font).GetAwaiter().GetResult(), _page.Status);
            if (dl is not null) await Common.Run(() => FontRepair.InstallForUser(dl), _page.Status);
        }
        _page.ShowStatus(L.T("gf.updated", newer.Count));
    }
}
