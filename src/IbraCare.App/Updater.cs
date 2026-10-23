using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using IbraCare.Core;

namespace IbraCare.App;

/// <summary>Updates from GitHub Releases (same way as IBDM Pro): finds the newest installer, downloads it and installs quietly.</summary>
public static class Updater
{
    private static string Api => $"https://api.github.com/repos/{Program.Repo}/releases/latest";
    public static string Page => $"https://github.com/{Program.Repo}/releases/latest";

    public sealed record Release(Version Version, string Tag, string SetupUrl, long Size, string Notes);

    public static Version Current => Parse(Program.Version);

    private static Version Parse(string s)
    {
        s = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(s, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);
    }

    public static async Task<Release?> LatestAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Api);
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var resp = await Http.Client.SendAsync(req, timeout.Token);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException(L.T("gf.http", (int)resp.StatusCode));
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(timeout.Token));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        string? url = null;
        long size = -1;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            {
                url = a.GetProperty("browser_download_url").GetString();
                size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : -1;
            }
        }
        if (url is null) return null;
        return new Release(Parse(tag), tag, url, size, root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
    }

    public static async Task CheckInBackground()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            var r = await LatestAsync();
            if (r is not null && r.Version > Current)
                CareApp.Instance.Dispatcher.Invoke(() => CareApp.Instance.Main.ShowUpdateBadge(r.Version.ToString()));
        }
        catch (Exception e) { Log.Write($"update check: {e.Message}"); }
    }

    public static void ShowWindow(Window? owner) => new UpdateWindow { Owner = owner is { IsVisible: true } ? owner : null }.ShowDialog();

    public static async Task<string> DownloadAsync(Release r, Action<long, long> progress, CancellationToken ct)
    {
        var file = Path.Combine(Path.GetTempPath(), $"IBRA-System-Care-Setup-{r.Version}.exe");
        using var resp = await Http.Client.GetAsync(r.SetupUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? r.Size;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(file + ".part"))
        {
            var buf = new byte[128 * 1024];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                progress(done, total);
            }
            if (total > 0 && done != total) throw new IOException(L.T("upd.cut"));
        }
        File.Move(file + ".part", file, true);
        return file;
    }

    public static void Install(string setup)
    {
        Process.Start(new ProcessStartInfo(setup, "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
        CareApp.Instance.Quit();
    }
}

public sealed class UpdateWindow : Window
{
    private readonly StackPanel _body = new();
    private readonly CancellationTokenSource _cts = new();

    public UpdateWindow()
    {
        Ui.Setup(this, L.T("upd.title"), 520, 320);
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(24) };
        var head = Ui.Row(Ui.Logo(44), new StackPanel
        {
            Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { Ui.Text(L.T("upd.title"), 17, Theme.Ink, bold: true), Ui.Ltr(L.T("upd.current", Program.Version), 12, Theme.Muted) },
        });
        head.Margin = new Thickness(0, 0, 0, 14);
        root.Children.Add(head);
        root.Children.Add(_body);
        Content = root;
        Closed += (_, _) => _cts.Cancel();
        Loaded += async (_, _) => await Check();
    }

    private void Busy(string text)
    {
        _body.Children.Clear();
        _body.Children.Add(Ui.Text(text, 13));
        _body.Children.Add(new ProgressBar { Style = Theme.Progress, IsIndeterminate = true, Margin = new Thickness(0, 12, 0, 6) });
    }

    private async Task Check()
    {
        Busy(L.T("upd.checking"));
        Updater.Release? r;
        try { r = await Updater.LatestAsync(_cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Result(L.T("upd.failed"), Errors.Friendly(e), Theme.Red, true);
            return;
        }
        if (r is null || r.Version <= Updater.Current)
        {
            Result(L.T("upd.latest"), L.T("upd.latestText", Program.Version), Theme.Green, false);
            return;
        }
        _body.Children.Clear();
        _body.Children.Add(Ui.Text(L.T("upd.available", r.Version), 15, Theme.Gold, bold: true));
        _body.Children.Add(Ui.Wrap(L.T("upd.availableText"), 12.5).Margin(0, 6, 0, 16));
        var go = Ui.Button(L.T("upd.install"), "", Theme.PrimaryButton, async () => await Install(r));
        go.IsDefault = true;
        var later = Ui.Button(L.T("set.later"), null, Theme.Button, Close);
        later.IsCancel = true;
        _body.Children.Add(Ui.Row(go, Ui.Button(L.T("upd.page"), null, Theme.Button, () => Ui.Open(Updater.Page)), later).Spaced());
    }

    private void Result(string title, string text, System.Windows.Media.Brush color, bool retry)
    {
        _body.Children.Clear();
        _body.Children.Add(Ui.Text(title, 15, color, bold: true));
        _body.Children.Add(Ui.Wrap(text, 12.5).Margin(0, 6, 0, 16));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (retry) row.Children.Add(Ui.Button(L.T("upd.retry"), null, Theme.Button, async () => await Check()));
        var close = Ui.Button(L.T("ui.close"), null, Theme.Button, Close);
        close.IsCancel = true;
        row.Children.Add(close);
        _body.Children.Add(row.Spaced());
    }

    private async Task Install(Updater.Release r)
    {
        _body.Children.Clear();
        var label = Ui.Text(L.T("upd.downloading", r.Version), 13);
        var bar = new ProgressBar { Style = Theme.Progress, Maximum = 100, Margin = new Thickness(0, 12, 0, 6) };
        var stats = Ui.Ltr("", 12, Theme.Muted);
        _body.Children.Add(label);
        _body.Children.Add(bar);
        _body.Children.Add(stats);
        try
        {
            var setup = await Updater.DownloadAsync(r, (done, total) => Dispatcher.BeginInvoke(() =>
            {
                if (total > 0) bar.Value = done * 100.0 / total;
                stats.Text = $"{Fmt.Size(done)} / {Fmt.Size(total)}";
            }), _cts.Token);
            label.Text = L.T("upd.installing");
            bar.IsIndeterminate = true;
            await Task.Delay(500);
            Updater.Install(setup);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Result(L.T("upd.failed"), Errors.Friendly(e), Theme.Red, true); }
    }
}
