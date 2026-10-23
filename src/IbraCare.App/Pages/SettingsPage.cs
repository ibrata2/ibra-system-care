using System.Windows;
using System.Windows.Controls;
using IbraCare.Core;

namespace IbraCare.App.Pages;

public sealed class SettingsPage : CarePage
{
    public override string Id => "settings";

    public SettingsPage()
    {
        Rows(Auto, Star);
        var s = Settings.Current;
        var save = Ui.Button(L.T("set.save"), "", Theme.PrimaryButton, null);
        Children.Add(PageHeader(L.T("nav.settings"), L.T("set.subtitle"), save));

        var col = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };

        // Language
        col.Children.Add(Section(L.T("set.language")));
        var ar = Ui.Choice("العربية", "lang", s.Language == "ar", "من اليمين إلى اليسار");
        var en = Ui.Choice("English", "lang", s.Language == "en", "Left to right");
        col.Children.Add(Pair(ar, en));

        // Theme
        col.Children.Add(Section(L.T("set.theme")));
        var auto = Ui.Choice(L.T("set.themeAuto"), "theme", s.Theme == "auto");
        var dark = Ui.Choice(L.T("set.themeDark"), "theme", s.Theme == "dark");
        var light = Ui.Choice(L.T("set.themeLight"), "theme", s.Theme == "light");
        var themes = new UniformGrid3();
        foreach (var r in new[] { auto, dark, light }) { r.Margin = new Thickness(0, 0, 8, 0); themes.Children.Add(r); }
        col.Children.Add(themes);

        // Cleaner
        col.Children.Add(Section("IBRA Deep Cleaner"));
        var quarantine = Ui.Choice(L.T("prev.quarantine"), "cmode", s.DefaultCleanMode == CleanMode.Quarantine, L.T("set.quarantineSub"));
        var permanent = Ui.Choice(L.T("prev.permanent"), "cmode", s.DefaultCleanMode == CleanMode.Permanent, L.T("prev.permanentSub"));
        col.Children.Add(Pair(quarantine, permanent));
        var qDays = Number(s.QuarantineDays);
        var minAge = Number(s.MinAgeHours);
        col.Children.Add(Field(L.T("set.quarantineDays"), qDays));
        col.Children.Add(Field(L.T("set.minAge"), minAge));
        col.Children.Add(Ui.Label(L.T("set.keepList")));
        var keep = Ui.Input(string.Join(Environment.NewLine, s.KeepList), multiline: true);
        keep.Height = 130;
        keep.FlowDirection = FlowDirection.LeftToRight;
        keep.FontFamily = Theme.Mono;
        col.Children.Add(keep);
        col.Children.Add(Ui.Wrap(L.T("set.keepHelp"), 12).Margin(0, 4, 0, 0));

        // Fonts / backups
        col.Children.Add(Section(L.T("set.backups")));
        var bDays = Number(s.BackupDays);
        col.Children.Add(Field(L.T("set.backupDays"), bDays));
        col.Children.Add(Ui.Wrap(L.T("set.backupAlways"), 12).Margin(0, 4, 0, 0));

        // App
        col.Children.Add(Section(L.T("set.app")));
        var updates = Ui.Check(L.T("set.checkUpdates"), s.CheckUpdatesOnStart);
        col.Children.Add(updates);
        col.Children.Add(Ui.Row(
            Ui.Button(L.T("set.openData"), "", Theme.Button, () => Ui.ShowInFolder(Paths.AppData)),
            Ui.Button(L.T("set.openLog"), "", Theme.Button, () => Ui.Open(Path.Combine(Paths.AppData, "log.txt")))).Spaced().Margin(0, 8, 0, 0));

        Children.Add(Common.Scroll(col).At(0, 1));

        save.Click += (_, _) =>
        {
            var lang = en.IsChecked == true ? "en" : "ar";
            var theme = dark.IsChecked == true ? "dark" : light.IsChecked == true ? "light" : "auto";
            if (!int.TryParse(qDays.Text, out var q) || q is < 1 or > 365 || !int.TryParse(minAge.Text, out var age) || age is < 0 or > 2160 ||
                !int.TryParse(bDays.Text, out var b) || b is < 7 or > 3650)
            {
                Ui.Message(null, L.T("nav.settings"), L.T("set.badNumber"));
                return;
            }
            var langChanged = lang != s.Language;
            var themeChanged = theme != s.Theme;
            s.Language = lang;
            s.Theme = theme;
            s.DefaultCleanMode = permanent.IsChecked == true ? CleanMode.Permanent : CleanMode.Quarantine;
            s.QuarantineDays = q;
            s.MinAgeHours = age;
            s.BackupDays = b;
            s.KeepList = keep.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
            s.CheckUpdatesOnStart = updates.IsChecked == true;
            s.Save();
            if (themeChanged)
            {
                if (Ui.Ask(null, L.T("nav.settings"), L.T("set.themeRestart"), L.T("set.restartNow"), L.T("set.later")) == 0) Program.Restart("settings");
            }
            else if (langChanged) CareApp.Instance.Rebuild("settings");
            else Ui.Message(null, L.T("nav.settings"), L.T("set.saved"));
        };
    }

    private static UIElement Section(string title) => Ui.Text(title, 15, Theme.Gold, bold: true).Margin(0, 18, 0, 8);

    private static UIElement Pair(UIElement a, UIElement b)
    {
        var g = new UniformGrid3 { Columns = 2 };
        if (a is FrameworkElement fa) fa.Margin = new Thickness(0, 0, 8, 0);
        g.Children.Add(a);
        g.Children.Add(b);
        return g;
    }

    private static TextBox Number(int v)
    {
        var t = Ui.Input(v.ToString());
        t.Width = 90;
        t.FlowDirection = FlowDirection.LeftToRight;
        t.HorizontalAlignment = HorizontalAlignment.Left;
        return t;
    }

    private static UIElement Field(string label, UIElement input)
    {
        var g = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        g.Children.Add(Ui.Text(label, 13, Theme.Ink, wrap: TextWrapping.Wrap));
        g.Children.Add(input.At(1));
        g.Margin = new Thickness(0, 6, 0, 0);
        return g;
    }
}

public sealed class AboutPage : CarePage
{
    public override string Id => "about";

    public AboutPage()
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 620 };
        var logo = Ui.Logo(110);
        logo.Margin = new Thickness(0, 0, 0, 12);
        sp.Children.Add(logo);
        var name = Ui.Text("IBRA SYSTEM CARE", 26, Theme.Gold, bold: true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.FlowDirection = FlowDirection.LeftToRight;
        sp.Children.Add(name);
        var tag = Ui.Text(L.T("app.tagline"), 14, Theme.Ink);
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(tag);
        var ver = Ui.Ltr(L.IsArabic ? $"الإصدار {Program.Version}" : $"Version {Program.Version}", 12, Theme.Muted);
        ver.HorizontalAlignment = HorizontalAlignment.Center;
        ver.FlowDirection = Ui.Dir;
        sp.Children.Add(ver.Margin(0, 4, 0, 16));
        var credit = Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.Text(L.T("app.credit"), 15, Theme.Ink, bold: true, wrap: TextWrapping.Wrap),
                Ui.Text(L.T("app.developer"), 13, Theme.Muted).Margin(0, 4, 0, 0),
                Ui.Wrap(L.T("about.text"), 12.5).Margin(0, 10, 0, 0),
            },
        }, new Thickness(18));
        sp.Children.Add(credit);
        var row = Ui.Row(
            Ui.Button(L.T("about.updates"), "", Theme.PrimaryButton, () => Updater.ShowWindow(Window.GetWindow(this))),
            Ui.Button("GitHub", "", Theme.Button, () => Ui.Open("https://github.com/" + Program.Repo)),
            Ui.Button("Telegram", "", Theme.Button, () => Ui.Open("https://t.me/ibraprogram"))).Spaced();
        row.HorizontalAlignment = HorizontalAlignment.Center;
        row.Margin = new Thickness(0, 16, 0, 16);
        sp.Children.Add(row);
        sp.Children.Add(Ui.Wrap(L.T("about.licenses"), 11.5));
        Children.Add(Common.Scroll(sp));
    }
}
