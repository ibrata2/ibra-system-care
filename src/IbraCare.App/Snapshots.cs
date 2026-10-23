using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IbraCare.Core;
using IbraCare.Core.Cleaner;
using IbraCare.Core.Fonts;
using IbraCare.Core.Repair;

namespace IbraCare.App;

/// <summary>IbraSystemCare.exe --snapshot &lt;folder&gt; [--theme dark|light] [--lang ar|en]: renders every page to PNG after
/// real read-only scans, so each build can be looked at. Uses a temporary data folder; never deletes or repairs anything.</summary>
public static class Snapshots
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        try
        {
            AppState.LastClean = new Scanner { Specials = new WindowsSpecials() }.Scan(CleanTargets.All.Where(t => t.DefaultOn));
            AppState.LastFonts = FontInventory.Scan();
            AppState.LastPhotoshop = PhotoshopDoctor.Diagnose(AppState.LastFonts);
            Journal.Add("cleaner", EntryStatus.Success, "clean.journalTitle", ["sample"], null, "120", "1.2 GB");
            Journal.Add("fonts", EntryStatus.Partial, "fix.dup.journal", ["sample"], null, "2");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(dir, "scan-error.txt"), e.ToString()); }

        var w = new MainWindow("home") { Width = 1280, Height = 820 };
        foreach (var page in new[] { "home", "cleaner", "fonts", "repair", "settings", "about" })
        {
            try
            {
                w.Navigate(page);
                Save(w, Path.Combine(dir, $"{L.Language}-{page}.png"));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(dir, $"{L.Language}-{page}.error.txt"), e.ToString()); }
        }
        if (AppState.LastClean is { } report)
        {
            try { Save(new Pages.PreviewWindow(report) { Width = 1060, Height = 720 }, Path.Combine(dir, $"{L.Language}-cleaner-preview.png")); }
            catch (Exception e) { File.WriteAllText(Path.Combine(dir, $"{L.Language}-cleaner-preview.error.txt"), e.ToString()); }
        }
    }

    private static void Save(Window w, string file)
    {
        var content = (FrameworkElement)w.Content;
        w.Content = null;
        try
        {
            var size = new Size(w.Width, w.Height);
            var frame = new Border { Background = w.Background, Child = content, FlowDirection = w.FlowDirection, Width = size.Width, Height = size.Height };
            TextElement.SetFontFamily(frame, w.FontFamily);
            TextElement.SetFontSize(frame, w.FontSize);
            TextElement.SetForeground(frame, w.Foreground);
            frame.Language = w.Language;
            frame.Measure(size);
            frame.Arrange(new Rect(size));
            frame.UpdateLayout();
            // Let deferred work (Loaded handlers, bindings) run once before drawing.
            frame.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            frame.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(frame);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var f = File.Create(file);
            enc.Save(f);
            frame.Child = null;
            Console.WriteLine($"snapshot {file}");
        }
        finally { w.Content = content; }
    }
}

/// <summary>IbraSystemCare.exe --selftest &lt;file&gt;: checks the engine against the real Windows it runs on. Reads only, except a
/// test folder it creates in %TEMP% and removes again. Writes what it found; exit code 1 when a check fails.</summary>
public static class SelfTest
{
    public static int Run(string reportFile)
    {
        var lines = new List<string>();
        var failed = 0;
        void Check(bool ok, string what) { lines.Add($"{(ok ? "ok  " : "FAIL")} {what}"); if (!ok) failed++; }
        Paths.AppDataOverride = Path.Combine(Path.GetTempPath(), "IBRA System Care selftest " + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            lines.Add($"IBRA SYSTEM CARE {Program.Version} on {Environment.OSVersion} admin={Platform.IsAdmin}");

            // Cleaner: read-only scan of every place.
            var scan = new Scanner { Specials = new WindowsSpecials() }.Scan(CleanTargets.All);
            foreach (var t in scan.Targets)
                lines.Add($"     {t.Target.Id,-14} roots={t.Roots.Count} files={t.Count} size={Fmt.Size(t.Size)} admin={t.NeedsAdmin} notes={string.Join(" | ", t.Notes)}");
            Check(scan.Targets.First(t => t.Target.Id == "userTemp").Roots.Count >= 1, "the user Temp folder is found");
            Check(scan.Targets.All(t => t.Items.All(i => Safety.IsInside(i.Path, i.Root))), "every file found is inside its place");

            // Cleaner: a real quarantine and undo in a test folder inside %TEMP%.
            var root = Path.Combine(Path.GetTempPath(), "IBRA-selftest-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            foreach (var n in new[] { "a.tmp", "sub\\b.log" })
            {
                var p = Path.Combine(root, n);
                File.WriteAllText(p, new string('x', 1000));
                File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddDays(-5));
                File.SetCreationTimeUtc(p, DateTime.UtcNow.AddDays(-5));
            }
            using (File.Open(Path.Combine(root, "sub", "b.log"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var target = new CleanTarget { Id = "userTemp", Roots = [root] };
                var rep = new ScanReport();
                rep.Targets.Add(new Scanner { Settings = new Settings() }.ScanTarget(target));
                Check(rep.Targets[0].Items.Count == 2, "test files found");
                Check(rep.Targets[0].Items.Count(i => i.InUse) == 1, "a file held open by a program is detected as in use");
                foreach (var i in rep.Targets[0].Items) i.Selected = true;
                var res = CleanRunner.Run(rep, CleanMode.Quarantine);
                Check(res.Removed == 1 && res.Skipped.Count == 1, "quarantine moves the free file and skips the one in use");
                Check(!File.Exists(Path.Combine(root, "a.tmp")), "the free file left its place");
                var undo = Journal.Undo(res.Entry!, Registries.Default);
                Check(undo.Ok && File.Exists(Path.Combine(root, "a.tmp")), "undo puts it back");
            }
            Directory.Delete(root, true);

            // Fonts: the real Windows fonts and registry.
            var fonts = FontInventory.Scan();
            lines.Add($"     fonts={fonts.Files.Count} faces={fonts.Faces.Count} corrupt={fonts.CorruptCount} dupGroups={fonts.DuplicateGroups} broken={fonts.BrokenRegistrations.Count} arabicReady={fonts.ArabicReady.Count()}");
            Check(fonts.Files.Count > 50, "Windows fonts are listed");
            Check(fonts.Faces.Any(f => f.Face.Family == "Arial" && f.Latin), "Arial reads as a Latin font");
            var tahoma = fonts.Faces.FirstOrDefault(f => f.Face.Family == "Tahoma");
            Check(tahoma is { Arabic: ArabicSupport.Full }, $"Tahoma's Arabic joins ({tahoma?.Arabic})");
            Check(fonts.ArabicReady.Count() >= 3, "several Arabic fonts that join are found");
            foreach (var i in fonts.Issues.Take(40)) lines.Add($"     issue {i.Severity} {i.Code} {i.FontName} — {i.Path}");
            var ps = PhotoshopDoctor.Diagnose(fonts);
            lines.Add($"     photoshop installs={ps.Installs.Count} findings={ps.Findings.Count}");
            var file = Reports.Write(scan, fonts, ps, Path.GetTempPath());
            Check(File.Exists(file), "an HTML report is written");
            File.Delete(file);
        }
        catch (Exception e)
        {
            failed++;
            lines.Add("CRASH " + e);
        }
        finally
        {
            try { Directory.Delete(Paths.AppDataOverride!, true); } catch (Exception) { }
        }
        lines.Add(failed == 0 ? "ALL OK" : $"{failed} FAILED");
        File.WriteAllLines(reportFile, lines);
        return failed == 0 ? 0 : 1;
    }
}
