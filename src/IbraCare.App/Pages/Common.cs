using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IbraCare.Core;

namespace IbraCare.App.Pages;

/// <summary>A progress line under a page's actions: text, bar, cancel.</summary>
public sealed class StatusLine : Border
{
    private readonly TextBlock _text = Ui.Text("", 12.5, Theme.Muted);
    private readonly TextBlock _detail = Ui.PathText("", 11.5);
    private readonly ProgressBar _bar = new() { Style = Theme.Progress, Maximum = 100, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _cancel;
    private CancellationTokenSource? _cts;

    public StatusLine()
    {
        var g = Ui.Columns(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        var sp = new StackPanel();
        sp.Children.Add(_text);
        sp.Children.Add(_detail);
        sp.Children.Add(_bar);
        g.Children.Add(sp);
        _cancel = Ui.Button(L.T("ui.cancel"), "", Theme.Button, () => _cts?.Cancel());
        _cancel.Margin = new Thickness(12, 0, 0, 0);
        _cancel.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(_cancel.At(1));
        Child = g;
        Padding = new Thickness(0, 6, 0, 6);
        Visibility = Visibility.Collapsed;
    }

    public CancellationToken Start(string text, bool cancellable = true)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _text.Text = text;
        _text.Foreground = Theme.Muted;
        _detail.Text = "";
        _detail.Visibility = Visibility.Collapsed;
        _bar.IsIndeterminate = true;
        _bar.Visibility = Visibility.Visible;
        _cancel.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Visibility.Visible;
        return _cts.Token;
    }

    public void Report(int done, int total, string? detail = null)
    {
        _bar.IsIndeterminate = total <= 0;
        if (total > 0) _bar.Value = done * 100.0 / total;
        if (detail is not null)
        {
            _detail.Text = detail;
            _detail.Visibility = Visibility.Visible;
        }
    }

    public void Say(string text) => _text.Text = text;

    public void Finish(string text, Brush? color = null)
    {
        _text.Text = text;
        _text.Foreground = color ?? Theme.Green;
        _bar.Visibility = Visibility.Collapsed;
        _detail.Visibility = Visibility.Collapsed;
        _cancel.Visibility = Visibility.Collapsed;
        Visibility = Visibility.Visible;
    }

    public void Hide() => Visibility = Visibility.Collapsed;
}

public static class Common
{
    /// <summary>"This part needs administrator rights" with a button that restarts the app elevated on this page.</summary>
    public static Border AdminBanner(string page, string text)
    {
        var g = Ui.Columns(GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto);
        g.Children.Add(Ui.Glyph("", 18, Theme.Amber).Margin(0, 0, 12, 0));
        g.Children.Add(Ui.Wrap(text, 12.5).At(1));
        var b = Ui.Button(L.T("ui.restartAdmin"), "", Theme.Button, () =>
        {
            if (!Program.RestartAsAdmin(page)) Ui.Message(null, L.T("ui.adminDeclinedTitle"), L.T("ui.adminDeclined"));
        });
        b.Margin = new Thickness(12, 0, 0, 0);
        g.Children.Add(b.At(2));
        return new Border
        {
            Child = g, Background = Theme.AmberSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12), Visibility = Platform.IsAdmin ? Visibility.Collapsed : Visibility.Visible,
        };
    }

    /// <summary>Runs work off the window thread and comes back with the result; errors become a friendly message.</summary>
    public static async Task<T?> Run<T>(Func<T> work, StatusLine? status = null) where T : class
    {
        try { return await Task.Run(work); }
        catch (OperationCanceledException)
        {
            status?.Finish(L.T("err.cancelled"), Theme.Muted);
            return null;
        }
        catch (Exception e)
        {
            Log.Write($"task: {e}");
            status?.Finish(Errors.Friendly(e), Theme.Red);
            Ui.Error(e);
            return null;
        }
    }

    public static Border Empty(string glyph, string text) => new()
    {
        Padding = new Thickness(20, 40, 20, 40),
        Child = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { Ui.Glyph(glyph, 34, Theme.Muted), Ui.Wrap(text, 13).Margin(0, 12, 0, 0) },
        },
    };

    public static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    /// <summary>A row of tabs (gold underline); calls <paramref name="show"/> with the index chosen.</summary>
    public static StackPanel Tabs(Action<int> show, params string[] titles)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var group = "tabs" + Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < titles.Length; i++)
        {
            var index = i;
            var rb = new RadioButton { Style = Theme.Tab, GroupName = group, Content = Ui.Text(titles[i], 13.5, Theme.Ink, bold: true), IsChecked = i == 0 };
            rb.Checked += (_, _) => show(index);
            sp.Children.Add(rb);
        }
        return sp;
    }
}
