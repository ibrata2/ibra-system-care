using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IbraCare.Core;
using IbraCare.Core.Fonts;

namespace IbraCare.App.Pages;

public sealed class FontsPage : CarePage
{
    public override string Id => "fonts";
    private readonly StatusLine _status = new();
    private readonly Border _body = new();
    private readonly StackPanel _tabs;
    private readonly Button _scan;
    private int _tab;
    private FontScan? _scanResult;
    private PhotoshopReport? _ps;

    public FontsPage()
    {
        Rows(Auto, Auto, Auto, Auto, Star);
        _scan = Ui.Button(L.T("fonts.scan"), "", Theme.PrimaryButton, async () => await Scan());
        Children.Add(PageHeader("IBRA Font Doctor", L.T("fonts.subtitle"), _scan));
        Children.Add(_status.At(0, 1));
        _tabs = Common.Tabs(i => { _tab = i; Render(); }, L.T("fonts.tabProblems"), L.T("fonts.tabAll"), "Photoshop", L.T("fonts.tabTrusted"));
        Children.Add(_tabs.At(0, 2));
        Children.Add(_body.At(0, 4));
        _scanResult = AppState.LastFonts;
        _ps = AppState.LastPhotoshop;
        Render();
    }

    public override void OnShown()
    {
        if (_scanResult is null && AppState.LastFonts is { } f)
        {
            _scanResult = f;
            _ps = AppState.LastPhotoshop;
            Render();
        }
    }

    public async Task Scan()
    {
        _scan.IsEnabled = false;
        var ct = _status.Start(L.T("fonts.scanning"));
        var progress = new Progress<(int Done, int Total)>(p => _status.Report(p.Done, p.Total, $"{p.Done} / {p.Total}"));
        var scan = await Common.Run(() => FontInventory.Scan(null, progress, ct), _status);
        _scan.IsEnabled = true;
        if (scan is null) return;
        _scanResult = scan;
        _ps = await Common.Run(() => PhotoshopDoctor.Diagnose(scan));
        AppState.LastFonts = scan;
        AppState.LastPhotoshop = _ps;
        AppState.Notify();
        IbraCare.Core.Repair.Journal.Add("fonts", IbraCare.Core.Repair.EntryStatus.Info, "fonts.scanJournal",
            scan.Issues.Where(i => i.Severity != Severity.Info).Take(300).Select(i => $"{i.FontName} — {i.Message} — {i.Path}"),
            null, Fmt.Count(scan.Files.Count), Fmt.Count(scan.Issues.Count(i => i.Severity != Severity.Info)));
        _status.Finish(L.T("fonts.scanDone", Fmt.Count(scan.Files.Count), Fmt.Count(scan.Issues.Count(i => i.Severity == Severity.Error)), Fmt.Count(scan.Issues.Count(i => i.Severity == Severity.Warning))));
        Render();
    }

    private void Render()
    {
        if (_tab == 3) { _body.Child = new TrustedFontsView(this, _scanResult); return; }
        if (_scanResult is null)
        {
            _body.Child = Common.Empty("", L.T("fonts.empty"));
            return;
        }
        _body.Child = _tab switch
        {
            0 => ProblemsView(),
            1 => new AllFontsView(this, _scanResult),
            _ => PhotoshopView(),
        };
    }

    // ---------- problems ----------

    private readonly HashSet<FontIssue> _ticked = [];

    private UIElement ProblemsView()
    {
        var scan = _scanResult!;
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var issues = scan.Issues.OrderBy(i => i.Severity).ThenBy(i => i.FontName).ToList();
        _ticked.Clear();
        foreach (var i in issues.Where(i => Actionable(i.Fix))) _ticked.Add(i);

        var tools = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        tools.Children.Add(Ui.Text(L.T("fonts.problemsSummary", Fmt.Count(issues.Count(i => i.Severity == Severity.Error)), Fmt.Count(issues.Count(i => i.Severity == Severity.Warning)), Fmt.Count(issues.Count(i => i.Severity == Severity.Info))), 13, Theme.Ink, bold: true));
        var fix = Ui.Button(L.T("fonts.fixSelected"), "", Theme.PrimaryButton, async () => await FixTicked());
        var alts = Ui.Button(L.T("fonts.alternatives"), "", Theme.Button, ShowAlternatives);
        tools.Children.Add(Ui.Row(alts, fix).Spaced().At(1));
        tools.Margin = new Thickness(0, 0, 0, 8);
        g.Children.Add(tools);
        var legacy = issues.Where(i => i.Fix == FontFix.ConvertLegacy).ToList();
        if (legacy.Count > 0)
        {
            g.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
            var banner = Ui.Columns(GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto);
            banner.Children.Add(Ui.Glyph("\uE8D2", 22, Theme.Gold).Margin(0, 0, 12, 0));
            banner.Children.Add(new StackPanel
            {
                Children =
                {
                    Ui.Text(L.T("fonts.legacyTitle", Fmt.Count(legacy.Select(i => i.Path).Distinct().Count())), 14, Theme.Ink, bold: true),
                    Ui.Wrap(L.T("fonts.legacyText"), 12.5),
                },
            }.At(1));
            banner.Children.Add(Ui.Button(L.T("fonts.legacyConvertAll"), "\uE8AB", Theme.PrimaryButton, async () => await Fix(legacy)).Margin(12, 0, 0, 0).At(2));
            g.Children.Add(new Border { Child = banner, Background = Theme.GoldSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 10) }.At(0, 1));
        }
        var listRow = legacy.Count > 0 ? 2 : 1;
        if (issues.Count == 0)
        {
            g.Children.Add(Common.Empty("", L.T("fonts.noProblems")).At(0, 1));
            return g;
        }
        var list = Ui.List([
            ("", 36, o =>
            {
                var i = (FontIssue)o;
                if (!Actionable(i.Fix)) return new Border();
                var c = new CheckBox { Style = Theme.CheckBox, IsChecked = _ticked.Contains(i), HorizontalAlignment = HorizontalAlignment.Center };
                c.Checked += (_, _) => _ticked.Add(i);
                c.Unchecked += (_, _) => _ticked.Remove(i);
                return c;
            }),
            (L.T("fonts.colLevel"), 80, o => Ui.Badge(L.T("sev." + ((FontIssue)o).Severity), Theme.For(((FontIssue)o).Severity), Theme.SoftFor(((FontIssue)o).Severity))),
            (L.T("report.font"), 200, o => Ui.Name(((FontIssue)o).FontName ?? "")),
            (L.T("report.problem"), 380, o => Ui.Wrap(((FontIssue)o).Message, 12.5)),
            (L.T("fonts.colFix"), 150, o => Ui.Text(L.T("fix." + ((FontIssue)o).Fix), 12, Theme.Gold)),
            (L.T("report.path"), 300, o => Ui.PathText(((FontIssue)o).Path)),
        ]);
        list.ItemsSource = issues;
        list.MouseDoubleClick += (_, _) => { if (list.SelectedItem is FontIssue i) Ui.ShowInFolder(i.Path); };
        g.Children.Add(Ui.Card(list, new Thickness(4)).At(0, listRow));
        return g;
    }

    private static bool Actionable(FontFix f) => f is FontFix.RemoveFont or FontFix.RemoveRegistration or FontFix.RemoveDuplicate or FontFix.ConvertLegacy;

    private async Task FixTicked()
    {
        var chosen = _ticked.ToList();
        if (chosen.Count == 0) { Ui.Message(null, "IBRA Font Doctor", L.T("fonts.nothingTicked")); return; }
        await Fix(chosen);
    }

    /// <summary>Applies removal fixes after showing exactly what will change. Backups are always made first.</summary>
    public async Task Fix(IReadOnlyList<FontIssue> issues)
    {
        var scan = _scanResult;
        if (scan is null) return;
        var files = issues.Where(i => i.Fix is FontFix.RemoveFont or FontFix.RemoveDuplicate && i.Path is not null)
            .Select(i => (Issue: i, File: scan.Files.FirstOrDefault(f => string.Equals(f.Path, i.Path, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.File is not null).ToList();
        var regs = issues.Where(i => i.Fix == FontFix.RemoveRegistration && i.Registration is not null).Select(i => i.Registration!).Distinct().ToList();
        var convert = issues.Where(i => i.Fix == FontFix.ConvertLegacy && i.Path is not null)
            .Select(i => scan.Files.FirstOrDefault(f => string.Equals(f.Path, i.Path, StringComparison.OrdinalIgnoreCase)))
            .Where(f => f is not null).Select(f => f!).Distinct().ToList();
        var lines = files.Select(f => $"{L.T("fonts.willRemove")}: {f.File!.Path}").Concat(regs.Select(r => $"{L.T("fonts.willUnregister")}: {r.Name} → {r.Data}"))
            .Concat(convert.Select(f => $"{L.T("fonts.willConvert")}: {f.Path}")).ToList();
        if (lines.Count == 0) return;
        if ((files.Any(f => f.File!.Location == FontLocation.System) || convert.Any(f => f.Location == FontLocation.System)) && !Platform.IsAdmin)
        {
            if (Ui.Ask(null, L.T("fonts.adminNeededTitle"), L.T("fonts.adminNeeded"), L.T("ui.restartAdmin"), L.T("ui.cancel")) == 0) Program.RestartAsAdmin("fonts");
            return;
        }
        if (Ui.Ask(null, L.T("fonts.confirmTitle"), L.T("fonts.confirm"), lines, L.T("fonts.fixNow"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("fonts.fixing"), cancellable: false);
        var results = new List<RepairOutcome>();
        var corrupt = files.Where(f => f.Issue.Fix == FontFix.RemoveFont).Select(f => f.File!).Distinct().ToList();
        var dups = files.Where(f => f.Issue.Fix == FontFix.RemoveDuplicate).Select(f => f.File!).Distinct().Except(corrupt).ToList();
        if (corrupt.Count > 0 && await Common.Run(() => FontRepair.RemoveFonts(corrupt, "fix.corrupt"), _status) is { } a) results.Add(a);
        if (dups.Count > 0 && await Common.Run(() => FontRepair.RemoveFonts(dups, "fix.dup"), _status) is { } b) results.Add(b);
        if (regs.Count > 0 && await Common.Run(() => FontRepair.RemoveRegistrations(regs), _status) is { } c) results.Add(c);
        if (convert.Count > 0)
        {
            var progress = new Progress<(int Done, int Total, string Name)>(p => _status.Report(p.Done, p.Total, p.Name));
            if (await Common.Run(() => FontRepair.ConvertLegacy(convert, null, progress), _status) is { } d) results.Add(d);
        }
        var ok = results.All(r => r.Ok);
        var restart = results.Any(r => r.NeedsRestart);
        Ui.Ask(null, L.T(ok ? "fonts.fixedTitle" : "fonts.partlyFixedTitle"), L.T("fonts.fixedText") + (restart ? "\n" + L.T("fonts.restartNote") : "") + (convert.Count > 0 ? "\n" + L.T("fonts.legacyDone") : ""),
            results.SelectMany(r => r.Lines).ToList(), L.T("ui.ok"));
        await Scan();
    }

    private void ShowAlternatives()
    {
        if (_scanResult is null) return;
        var (installed, toInstall) = GoogleFonts.SuggestAlternatives(_scanResult);
        var lines = new List<string> { L.T("fonts.altInstalled") };
        lines.AddRange(installed.Select(f => "   ✓ " + f.Family + (f.ArabicExtended ? "  (گ چ پ ژ)" : "")));
        lines.Add("");
        lines.Add(L.T("fonts.altToInstall"));
        lines.AddRange(toInstall.Take(15).Select(c => $"   + {c.Family} — {c.Note}"));
        if (Ui.Ask(null, L.T("fonts.alternatives"), L.T("fonts.altText"), lines, L.T("fonts.openTrusted"), L.T("ui.close")) == 0)
            ((RadioButton)_tabs.Children[3]).IsChecked = true;
    }

    // ---------- Photoshop ----------

    private UIElement PhotoshopView()
    {
        var ps = _ps;
        var sp = new StackPanel();
        if (ps is null) return Common.Empty("", L.T("fonts.empty"));
        if (ps.Running)
            sp.Children.Add(new Border { Background = Theme.AmberSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10),
                Child = Ui.Row(Ui.Glyph("", 16, Theme.Amber), Ui.Wrap(L.T("ps.running"), 12.5)).Spaced(10) });

        // Installs
        var installs = new StackPanel();
        installs.Children.Add(Ui.Text(L.T("ps.installs"), 14, Theme.Ink, bold: true).Margin(0, 0, 0, 8));
        if (ps.Installs.Count == 0) installs.Children.Add(Ui.Wrap(L.T("ps.a.notFound"), 12.5));
        foreach (var i in ps.Installs)
        {
            var row = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
            var info = new StackPanel();
            info.Children.Add(Ui.Row(Ui.Text(i.Name, 13.5, Theme.Ink, bold: true), Ui.Ltr(i.Version ?? "", 12, Theme.Muted)).Spaced(10));
            info.Children.Add(Ui.Wrap(i.FontCacheDir is null ? L.T("ps.noCacheYet") : L.T("ps.cacheInfo", Fmt.Size(i.FontCacheSize)), 12));
            row.Children.Add(info);
            if (i.FontCacheDir is not null)
            {
                var install = i;
                row.Children.Add(Ui.Button(L.T("ps.clearCache"), "", Theme.Button, async () => await ClearCache(install)).At(1));
            }
            installs.Children.Add(Ui.Card(row, new Thickness(12)).Margin(0, 0, 0, 6));
        }
        sp.Children.Add(installs);

        // Findings
        sp.Children.Add(Ui.Text(L.T("ps.findings"), 14, Theme.Ink, bold: true).Margin(0, 14, 0, 8));
        var findings = ps.Findings.Where(f => f.Code != "ps.cache").ToList();
        if (findings.Count == 0) sp.Children.Add(Ui.Row(Ui.Glyph("", 15, Theme.Green), Ui.Text(L.T("ps.noFindings"), 13, Theme.Green)).Spaced());
        foreach (var group in findings.GroupBy(f => f.Code))
        {
            var first = group.First();
            var g = Ui.Columns(GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto);
            g.Children.Add(Ui.Badge(Fmt.Count(group.Count()), Theme.For(first.Severity), Theme.SoftFor(first.Severity)).Margin(0, 0, 10, 0));
            var text = new StackPanel();
            text.Children.Add(Ui.Text(first.Message, 13, Theme.Ink, bold: true, wrap: TextWrapping.Wrap));
            text.Children.Add(Ui.Wrap(string.Join(L.Sep, group.Select(f => f.FontName).Where(n => !string.IsNullOrEmpty(n)).Distinct().Take(12)) + (group.Count() > 12 ? "…" : ""), 12));
            g.Children.Add(text.At(1));
            var fixable = group.Where(f => Actionable(f.Fix)).ToList();
            if (fixable.Count > 0) g.Children.Add(Ui.Button(L.T(first.Code == "ps.legacy" ? "fonts.legacyConvertAll" : "ps.fixGroup"), "", Theme.Button, async () => await Fix(fixable)).Margin(10, 0, 0, 0).At(2));
            else if (group.Any(f => f.Fix == FontFix.InstallAlternative)) g.Children.Add(Ui.Button(L.T("fonts.alternatives"), "", Theme.Button, ShowAlternatives).Margin(10, 0, 0, 0).At(2));
            sp.Children.Add(Ui.Card(g, new Thickness(12)).Margin(0, 0, 0, 6));
        }

        // Advice
        sp.Children.Add(Ui.Text(L.T("ps.adviceTitle"), 14, Theme.Ink, bold: true).Margin(0, 14, 0, 8));
        var n = 1;
        foreach (var a in ps.Advice)
            sp.Children.Add(Ui.Row(Ui.Ltr($"{n++}.", 13, Theme.Gold, bold: true), Ui.Wrap(a, 12.5)).Spaced(8).Margin(0, 0, 0, 6));

        // Windows font cache
        var cacheCard = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        cacheCard.Children.Add(new StackPanel { Children = { Ui.Text(L.T("ps.winCacheTitle"), 13.5, Theme.Ink, bold: true), Ui.Wrap(L.T("ps.winCacheText"), 12) } });
        cacheCard.Children.Add(Ui.Button(L.T("ps.winCacheButton"), "", Theme.Button, async () => await RebuildCache()).At(1));
        sp.Children.Add(Ui.Card(cacheCard, new Thickness(12)).Margin(0, 14, 0, 0));
        return Common.Scroll(sp);
    }

    private async Task ClearCache(PhotoshopInstall ps)
    {
        if (Platform.IsProcessRunning("Photoshop")) { Ui.Message(null, "Photoshop", L.T("ps.closeFirst")); return; }
        if (Ui.Ask(null, L.T("ps.clearCache"), L.T("ps.clearConfirm", ps.Name), [ps.FontCacheDir!], L.T("ps.clearCache"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("fonts.fixing"), false);
        var r = await Common.Run(() => FontRepair.ClearPhotoshopCache(ps), _status);
        if (r is null) return;
        _status.Finish(L.T("ps.cleared"));
        _ps = await Common.Run(() => PhotoshopDoctor.Diagnose(_scanResult!));
        AppState.LastPhotoshop = _ps;
        Render();
    }

    private async Task RebuildCache()
    {
        if (!Platform.IsAdmin)
        {
            if (Ui.Ask(null, L.T("fonts.adminNeededTitle"), L.T("ps.winCacheAdmin"), L.T("ui.restartAdmin"), L.T("ui.cancel")) == 0) Program.RestartAsAdmin("fonts");
            return;
        }
        if (Ui.Ask(null, L.T("ps.winCacheTitle"), L.T("ps.winCacheConfirm"), L.T("ps.winCacheButton"), L.T("ui.cancel")) != 0) return;
        _status.Start(L.T("fonts.fixing"), false);
        var r = await Common.Run(FontRepair.RebuildWindowsFontCache, _status);
        if (r is null) return;
        _status.Finish(L.T("font.restartToFinish"), Theme.Amber);
        Ui.Ask(null, L.T("ps.winCacheTitle"), L.T("font.restartToFinish"), r.Lines, L.T("ui.ok"));
    }

    public void ShowStatus(string text, Brush? color = null) => _status.Finish(text, color);
    public StatusLine Status => _status;
}

/// <summary>Every font, with a live preview of Arabic joining and direction for the one selected.</summary>
public sealed class AllFontsView : Grid
{
    private readonly FontsPage _page;
    private readonly FontScan _scan;
    private readonly ListView _list;
    private readonly TextBox _filter = Ui.Input();
    private readonly Border _detail = new();
    private string _show = "all";

    private sealed record Row(FontFileInfo File, FaceReport? Face, string Name, string Status, Severity? Level);

    public AllFontsView(FontsPage page, FontScan scan)
    {
        _page = page;
        _scan = scan;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var tools = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        _filter.TextChanged += (_, _) => Fill();
        tools.Children.Add(_filter);
        var kinds = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
        foreach (var (k, label) in new[] { ("all", L.T("fonts.showAll")), ("arabic", L.T("fonts.showArabic")), ("problems", L.T("fonts.showProblems")) })
        {
            var rb = new RadioButton { Style = Theme.Tab, GroupName = "fontfilter", IsChecked = k == "all", Content = Ui.Text(label, 12.5) };
            rb.Checked += (_, _) => { _show = k; Fill(); };
            kinds.Children.Add(rb);
        }
        tools.Children.Add(kinds.At(1));
        tools.Margin = new Thickness(0, 0, 12, 8);
        Children.Add(tools);

        _list = Ui.List([
            (L.T("report.font"), 230, o => Ui.Name(((Row)o).Name)),
            (L.T("fonts.colArabic"), 150, o => Ui.Text(((Row)o).Face is { } f ? FontInventory.ArabicLabel(f.Arabic) : "—", 12, ((Row)o).Face?.Arabic == ArabicSupport.Full ? Theme.Green : Theme.Muted)),
            (L.T("fonts.colLatin"), 70, o => Ui.Text(((Row)o).Face is { Latin: true } ? "✓" : "—", 12, Theme.Muted)),
            (L.T("fonts.colWhere"), 110, o => Ui.Text(L.T("loc." + ((Row)o).File.Location), 12, Theme.Muted)),
            (L.T("fonts.colStatus"), 150, o => ((Row)o).Level is { } lv ? Ui.Badge(((Row)o).Status, Theme.For(lv), Theme.SoftFor(lv)) : Ui.Text(((Row)o).Status, 12, Theme.Green)),
        ]);
        _list.SelectionMode = SelectionMode.Single;
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is Row r) ShowDetail(r); };
        Children.Add(Ui.Card(_list, new Thickness(4)).Margin(0, 0, 12, 0).At(0, 1));
        Children.Add(Common.Scroll(_detail).At(1, 1));
        SetRowSpan(_detail, 1);
        _detail.Child = Common.Empty("", L.T("fonts.pickOne"));
        Fill();
    }

    private IEnumerable<Row> Rows()
    {
        foreach (var f in _scan.Files)
        {
            var issues = _scan.Issues.Where(i => string.Equals(i.Path, f.Path, StringComparison.OrdinalIgnoreCase)).ToList();
            var worst = issues.Count == 0 ? (Severity?)null : issues.Min(i => i.Severity);
            var status = worst is null ? L.T("fonts.ok") : L.T("sev." + worst);
            var faces = _scan.Faces.Where(x => ReferenceEquals(x.File, f)).ToList();
            if (faces.Count == 0) yield return new Row(f, null, f.DisplayName, f.IsCorrupt ? L.T("fonts.damaged") : status, f.IsCorrupt ? Severity.Error : worst);
            else foreach (var face in faces) yield return new Row(f, face, face.Name, status, worst);
        }
    }

    private void Fill()
    {
        var q = _filter.Text.Trim();
        _list.ItemsSource = Rows().Where(r =>
                (q.Length == 0 || r.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || r.File.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)) &&
                (_show != "arabic" || r.Face is { Arabic: not ArabicSupport.None }) &&
                (_show != "problems" || r.Level is Severity.Error or Severity.Warning))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private void ShowDetail(Row r)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Name(r.Name, 18));
        sp.Children.Add(Ui.PathText(r.File.Path).Margin(0, 4, 0, 10));

        void Fact(string label, string value, bool ltr = false)
        {
            var g = Ui.Columns(new GridLength(140), new GridLength(1, GridUnitType.Star));
            g.Children.Add(Ui.Text(label, 12, Theme.Muted));
            g.Children.Add((ltr ? Ui.Ltr(value, 12.5) : Ui.Text(value, 12.5, Theme.Ink, wrap: TextWrapping.Wrap)).At(1));
            g.Margin = new Thickness(0, 2, 0, 2);
            sp.Children.Add(g);
        }
        Fact(L.T("fonts.fFormat"), r.File.Format.ToString(), true);
        Fact(L.T("report.size"), Fmt.Size(r.File.Size), true);
        Fact(L.T("fonts.colWhere"), L.T("loc." + r.File.Location));
        if (r.Face is { } f)
        {
            Fact(L.T("fonts.fVersion"), f.Face.VersionText, true);
            Fact(L.T("fonts.fGlyphs"), Fmt.Count(f.Face.GlyphCount), true);
            Fact(L.T("fonts.colArabic"), FontInventory.ArabicLabel(f.Arabic) + $"  ({f.ArabicLetters}/36)");
            Fact(L.T("fonts.colExtra"), f.ArabicExtended ? "✓" : "—");
            Fact(L.T("fonts.fHarakat"), f.Harakat ? "✓" : "—");
            Fact(L.T("fonts.fDigits"), (f.LatinDigits ? "0-9 ✓  " : "") + (f.ArabicDigits ? "٠-٩ ✓" : ""));
            Fact(L.T("fonts.colLatin"), f.Latin ? "✓" : "—");
            Fact(L.T("fonts.fFeatures"), f.Face.ArabicFeatures.Count > 0 ? string.Join(" ", f.Face.ArabicFeatures.OrderBy(x => x)) : "—", true);
            Fact(L.T("fonts.fLicense"), EmbeddingText(f.Face.FsType));

            // Live test: the font draws a joined word, and the same letters alone; joined Arabic is narrower.
            sp.Children.Add(Ui.Text(L.T("fonts.testTitle"), 14, Theme.Ink, bold: true).Margin(0, 14, 0, 6));
            var family = PreviewFamily(r.File, f);
            var verdict = JoinProbe.Test(family);
            var verdictText = verdict switch
            {
                true => (L.T("fonts.joinYes"), Theme.Green),
                false => (L.T("fonts.joinNo"), Theme.Red),
                null => (L.T("fonts.joinUnknown"), Theme.Muted),
            };
            sp.Children.Add(Ui.Row(Ui.Glyph(verdict == true ? "" : "", 14, verdictText.Item2), Ui.Text(verdictText.Item1, 12.5, verdictText.Item2, wrap: TextWrapping.Wrap)).Spaced());
            var sample = Ui.Input(L.T("fonts.sample"));
            sample.FlowDirection = FlowDirection.RightToLeft;
            sample.Margin = new Thickness(0, 8, 0, 6);
            sp.Children.Add(sample);
            var preview = new TextBlock { FontFamily = family, FontSize = 30, Foreground = Theme.Ink, FlowDirection = FlowDirection.RightToLeft, TextWrapping = TextWrapping.Wrap, Text = sample.Text };
            var latin = new TextBlock { FontFamily = family, FontSize = 20, Foreground = Theme.Muted, FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap, Text = "Photoshop 2025 — The quick brown fox 0123456789" };
            var isolated = new TextBlock { FontFamily = family, FontSize = 20, Foreground = Theme.Muted, FlowDirection = FlowDirection.RightToLeft, Text = "س  ل  م  ع  ي  ك  گ  چ  پ  ژ" };
            sample.TextChanged += (_, _) => preview.Text = sample.Text;
            sp.Children.Add(Ui.Card(new StackPanel { Children = { preview, isolated.Margin(0, 8, 0, 0), latin.Margin(0, 8, 0, 0) } }, new Thickness(14)));
        }
        else if (r.File.ReadError is { } err) Fact(L.T("report.problem"), err);
        foreach (var i in _scan.Issues.Where(i => string.Equals(i.Path, r.File.Path, StringComparison.OrdinalIgnoreCase)))
            sp.Children.Add(Ui.Row(Ui.Badge(L.T("sev." + i.Severity), Theme.For(i.Severity), Theme.SoftFor(i.Severity)), Ui.Wrap(i.Message, 12.5)).Spaced().Margin(0, 8, 0, 0));

        var actions = Ui.Row(Ui.Button(L.T("clean.openFolder"), "", Theme.Button, () => Ui.ShowInFolder(r.File.Path)));
        var removable = _scan.Issues.Where(i => string.Equals(i.Path, r.File.Path, StringComparison.OrdinalIgnoreCase) && i.Fix is FontFix.RemoveFont or FontFix.RemoveDuplicate).ToList();
        var convertible = _scan.Issues.Where(i => string.Equals(i.Path, r.File.Path, StringComparison.OrdinalIgnoreCase) && i.Fix == FontFix.ConvertLegacy).Take(1).ToList();
        if (convertible.Count > 0) actions.Children.Add(Ui.Button(L.T("fix.ConvertLegacy"), "\uE8AB", Theme.PrimaryButton, async () => await _page.Fix(convertible)));
        if (removable.Count > 0) actions.Children.Add(Ui.Button(L.T("fonts.removeThis"), "", Theme.DangerButton, async () => await _page.Fix(removable)));
        actions.Spaced();
        actions.Margin = new Thickness(0, 14, 0, 0);
        sp.Children.Add(actions);
        _detail.Child = Ui.Card(sp, new Thickness(16));
    }

    private static string EmbeddingText(ushort fsType) =>
        (fsType & 0x000E) switch
        {
            0 => L.T("fonts.embInstallable"),
            _ when (fsType & 0x0002) != 0 => L.T("fonts.embRestricted"),
            _ when (fsType & 0x0008) != 0 => L.T("fonts.embEditable"),
            _ => L.T("fonts.embPreview"),
        };

    /// <summary>The font drawn from its own file (so a duplicate shows its own copy), falling back to the family name.</summary>
    public static FontFamily PreviewFamily(FontFileInfo file, FaceReport face)
    {
        try
        {
            var dir = Path.GetDirectoryName(file.Path)!.Replace('\\', '/');
            var name = face.Face.Family.Length > 0 ? face.Face.Family : face.Family;
            return new FontFamily(new Uri("file:///" + dir.TrimEnd('/') + "/"), "./" + Path.GetFileName(file.Path) + "#" + name);
        }
        catch (Exception) { return new FontFamily(face.Family); }
    }
}

/// <summary>Measures, with the font itself, whether Arabic letters join: in a joining font, "ســـس" drawn together is clearly
/// narrower than the same letters drawn one by one (each alone gets its long isolated form).</summary>
public static class JoinProbe
{
    public static bool? Test(FontFamily family)
    {
        try
        {
            var tf = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!tf.TryGetGlyphTypeface(out var gt) || !gt.CharacterToGlyphMap.ContainsKey('س')) return null;
            double W(string s) => new FormattedText(s, CultureInfo.GetCultureInfo("ar"), FlowDirection.RightToLeft, tf, 40, Brushes.Black, 1.0).WidthIncludingTrailingWhitespace;
            var joined = W("سسس");
            var alone = 3 * W("س");
            if (alone <= 0) return null;
            return joined < alone * 0.9;
        }
        catch (Exception) { return null; }
    }
}
