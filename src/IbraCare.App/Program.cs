using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using IbraCare.Core;
using IbraCare.Core.Cleaner;
using IbraCare.Core.Fonts;
using IbraCare.Core.Repair;

namespace IbraCare.App;

public static class Program
{
    public static string Version { get; } =
        typeof(Program).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";

    public const string Repo = "ibrata2/ibra-system-care";

    [STAThread]
    public static int Main(string[] args)
    {
        string? Arg(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

        if (Arg("--lang") is { } lang && lang is "ar" or "en")
        {
            Settings.Current.Language = lang;
            // Started by the installer in its language: remember it (snapshots and self tests only use it for this run).
            if (!args.Contains("--snapshot") && !args.Contains("--selftest")) Settings.Current.Save();
        }
        L.Language = Settings.Current.Language;

        if (Arg("--selftest") is { } report)
        {
            // Used by the build on a real Windows machine: reads everything, changes nothing outside its own test folder.
            return SelfTest.Run(report);
        }
        if (Arg("--check-update") is { } updateOut)
        {
            try
            {
                var r = Updater.LatestAsync().GetAwaiter().GetResult();
                File.WriteAllText(updateOut, r is null ? "no release" : $"current={Version} latest={r.Version} newer={r.Version > Updater.Current} url={r.SetupUrl}");
            }
            catch (Exception e) { File.WriteAllText(updateOut, "error: " + e.Message); }
            return 0;
        }
        if (Arg("--snapshot") is { } snapDir)
        {
            Paths.AppDataOverride = Path.Combine(Path.GetTempPath(), "IBRA System Care snapshot " + Guid.NewGuid().ToString("N")[..6]);
            if (Arg("--theme") is { } theme) Settings.Current.Theme = theme;
            var snap = new CareApp { SnapshotDir = Path.GetFullPath(snapDir) };
            return snap.Run();
        }

        using var single = new Mutex(true, "Local\\IbraSystemCare.SingleInstance", out var first);
        if (!first && (args.Contains("--restart") || args.Contains("--elevated")))
        {
            try { first = single.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { first = true; }
        }
        if (!first)
        {
            Native.ShowOtherInstance();
            return 0;
        }
        var app = new CareApp { StartPage = Arg("--page") };
        return app.Run();
    }

    /// <summary>Starts again as administrator (Windows asks first) on the same page. Returns false if the person said no.</summary>
    public static bool RestartAsAdmin(string page)
    {
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException();
            Process.Start(new ProcessStartInfo(exe, $"--elevated --page {page}") { UseShellExecute = true, Verb = "runas" });
            CareApp.Instance.Quit();
            return true;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public static void Restart(string page)
    {
        try
        {
            if (Environment.ProcessPath is { } exe) Process.Start(exe, $"--restart --page {page}");
            CareApp.Instance.Quit();
        }
        catch (Exception e) { Log.Write($"restart: {e.Message}"); }
    }
}

/// <summary>The results of the last scans, shared by the pages (the home page shows them, reports use them).</summary>
public static class AppState
{
    public static ScanReport? LastClean { get; set; }
    public static FontScan? LastFonts { get; set; }
    public static PhotoshopReport? LastPhotoshop { get; set; }
    public static event Action? Changed;
    public static void Notify() => Application.Current?.Dispatcher.BeginInvoke(() => Changed?.Invoke());
}

public sealed class CareApp : Application
{
    public static CareApp Instance { get; private set; } = null!;
    public MainWindow Main { get; private set; } = null!;
    public string? SnapshotDir { get; init; }
    public string? StartPage { get; init; }

    public CareApp()
    {
        Instance = this;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            Log.Write($"unhandled: {e.Exception}");
            e.Handled = true;
            try { Ui.Error(e.Exception); } catch (Exception) { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"fatal: {e.ExceptionObject}");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(L.IsArabic ? "ar" : "en")));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Write($"IBRA SYSTEM CARE {Program.Version} started (admin: {Platform.IsAdmin})");
        if (SnapshotDir is not null)
        {
            Snapshots.Run(SnapshotDir);
            Shutdown();
            return;
        }
        Main = Attach(new MainWindow(StartPage ?? "home"));
        Main.Show();
        _ = Task.Run(() =>
        {
            try
            {
                var n = BackupStore.PurgeExpired();
                if (n > 0) Log.Write($"purged {n} expired backups");
            }
            catch (Exception ex) { Log.Write($"purge: {ex.Message}"); }
        });
        if (Settings.Current.CheckUpdatesOnStart) _ = Updater.CheckInBackground();
    }

    /// <summary>The language changed: build the window again in the new language and direction.</summary>
    public void Rebuild(string page)
    {
        L.Language = Settings.Current.Language;
        var old = Main;
        Main = Attach(new MainWindow(page) { WindowStartupLocation = WindowStartupLocation.Manual, Left = old.Left, Top = old.Top, Width = old.Width, Height = old.Height });
        Main.Show();
        Main.WindowState = old.WindowState;
        old.Replaced = true;
        old.Close();
    }

    private MainWindow Attach(MainWindow w)
    {
        w.Closed += (_, _) => { if (!w.Replaced) Quit(); };
        return w;
    }

    public void Quit() => Shutdown();
}

/// <summary>The Recycle Bin, through the Windows shell.</summary>
public sealed class WindowsSpecials : ISpecialTargets
{
    [StructLayout(LayoutKind.Sequential)] // 64-bit layout (shellapi.h packs to 1 byte only on 32-bit)
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    public (long Size, long Count) Query(string special)
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        var hr = SHQueryRecycleBin(null, ref info);
        if (hr != 0) Marshal.ThrowExceptionForHR(hr);
        return (info.i64Size, info.i64NumItems);
    }

    public void Empty(string special)
    {
        const uint NoConfirmation = 0x1, NoProgressUi = 0x2, NoSound = 0x4;
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
        if (hr != 0 && hr != unchecked((int)0x8000FFFF)) Marshal.ThrowExceptionForHR(hr); // E_UNEXPECTED = already empty
    }
}

internal static partial class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string title);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    /// <summary>Brings the copy that is already running to the front.</summary>
    public static void ShowOtherInstance()
    {
        try
        {
            var h = FindWindow(null, "IBRA SYSTEM CARE");
            if (h == IntPtr.Zero) return;
            ShowWindow(h, 9); // SW_RESTORE
            SetForegroundWindow(h);
        }
        catch (Exception) { }
    }
}
