using IbraCare.Core.Repair;

namespace IbraCare.Core.Fonts;

public sealed class RepairOutcome
{
    public bool Ok { get; set; }
    public List<string> Lines { get; } = [];
    public string? BackupId { get; set; }
    public JournalEntry? Entry { get; set; }
    public bool NeedsRestart { get; set; }
}

/// <summary>Font repairs. Each one first saves a backup (files and registry lines) and writes a history entry that can be undone.</summary>
public static class FontRepair
{
    private static int Keep => Settings.Current.BackupDays;

    private static void RequireRights(string path)
    {
        var sys = FontInventory.SystemFontsDir;
        if (sys is not null && Cleaner.Safety.IsInside(path, sys) && !Platform.IsAdmin)
            throw new UnauthorizedAccessException(L.T("font.needAdmin"));
    }

    /// <summary>Removes font files (damaged ones, or duplicate copies) together with their registry lines.</summary>
    public static RepairOutcome RemoveFonts(IReadOnlyList<FontFileInfo> files, string reasonKey, IRegistryAccess? registry = null)
    {
        registry ??= Registries.Default;
        var outcome = new RepairOutcome();
        var session = BackupStore.Begin("fonts", reasonKey + ".backup", Keep, Fmt.Count(files.Count));
        var removed = 0;
        foreach (var f in files)
        {
            try
            {
                RequireRights(f.Path);
                if (Platform.IsWindows && f.Location == FontLocation.System && Platform.IsProcessRunning("Photoshop"))
                    outcome.Lines.Add(L.T("font.photoshopOpen"));
                session.AddFileCopy(f.Path);
                foreach (var reg in f.Registrations)
                {
                    session.AddRegistry(registry, reg.Hive, FontInventory.FontsKey, reg.Name);
                    registry.Delete(reg.Hive, FontInventory.FontsKey, reg.Name);
                }
                FontSystem.Unload(f.Path);
                try
                {
                    File.SetAttributes(f.Path, FileAttributes.Normal);
                    File.Delete(f.Path);
                }
                catch (IOException e) when (Errors.IsInUse(e))
                {
                    // Windows keeps fonts loaded; the line is gone from the registry, so the file is not loaded after a restart.
                    outcome.NeedsRestart = true;
                    outcome.Lines.Add($"{f.Path} — {L.T("font.removeAfterRestart")}");
                    ScheduleDelete(f.Path);
                }
                removed++;
                outcome.Lines.Add($"✓ {f.DisplayName} — {f.Path}");
            }
            catch (Exception e)
            {
                outcome.Lines.Add($"✗ {f.DisplayName} — {Errors.Friendly(e)}");
            }
        }
        var set = session.Commit();
        outcome.BackupId = set.Items.Count > 0 ? set.Id : null;
        outcome.Ok = removed == files.Count;
        FontSystem.Broadcast();
        outcome.Entry = Journal.Add("fonts", outcome.Ok ? EntryStatus.Success : removed > 0 ? EntryStatus.Partial : EntryStatus.Failed,
            reasonKey + ".journal", outcome.Lines, outcome.BackupId, Fmt.Count(removed));
        return outcome;
    }

    /// <summary>Converts old symbol-encoded Arabic fonts (FS_ and the like) into Unicode fonts whose letters join.
    /// For each font: the original is backed up, the converted copy is written next to it as "&lt;name&gt;-IBRA.ttf",
    /// Windows' registry line is pointed at the new file, and the old file is removed (after a restart if Windows holds it).
    /// The family name stays the same, so designs that already use the font keep working. Undo puts everything back.</summary>
    public static RepairOutcome ConvertLegacy(IReadOnlyList<FontFileInfo> files, IRegistryAccess? registry = null, IProgress<(int Done, int Total, string Name)>? progress = null)
    {
        registry ??= Registries.Default;
        var outcome = new RepairOutcome();
        var session = BackupStore.Begin("fonts", "fix.legacy.backup", Keep, Fmt.Count(files.Count));
        var done = 0;
        var n = 0;
        foreach (var f in files)
        {
            progress?.Report((++n, files.Count, f.DisplayName));
            try
            {
                RequireRights(f.Path);
                var data = File.ReadAllBytes(f.Path);
                var result = LegacyArabic.Convert(data);
                var dir = Path.GetDirectoryName(f.Path)!;
                var dest = Path.Combine(dir, Path.GetFileNameWithoutExtension(f.Path) + "-IBRA.ttf");
                var system = FontInventory.SystemFontsDir is { } sys && Cleaner.Safety.IsInside(f.Path, sys);

                session.AddFileCopy(f.Path);
                if (File.Exists(dest)) session.AddFileCopy(dest); else session.AddCreatedFile(dest);
                File.WriteAllBytes(dest, result.Data);

                // Point Windows at the converted file (same registry line names), or register it if it never was.
                var regs = f.Registrations.ToList();
                if (regs.Count == 0)
                    regs.Add(new FontRegistration(system ? Hive.LocalMachine : Hive.CurrentUser, $"{(result.Family.Length > 0 ? result.Family : f.DisplayName)} (TrueType)", "", null));
                foreach (var reg in regs)
                {
                    session.AddRegistry(registry, reg.Hive, FontInventory.FontsKey, reg.Name);
                    var value = reg.Hive == Hive.LocalMachine && system ? Path.GetFileName(dest) : dest;
                    registry.Set(reg.Hive, FontInventory.FontsKey, new RegValue(reg.Name, "String", value));
                }
                FontSystem.Unload(f.Path);
                try
                {
                    File.SetAttributes(f.Path, FileAttributes.Normal);
                    File.Delete(f.Path);
                }
                catch (IOException e) when (Errors.IsInUse(e))
                {
                    outcome.NeedsRestart = true;
                    ScheduleDelete(f.Path);
                }
                FontSystem.Load(dest);
                done++;
                outcome.Lines.Add($"✓ {f.DisplayName} — {L.T("fix.legacy.line", result.Letters, result.Joined, result.LamAlefs)} — {dest}");
            }
            catch (Exception e)
            {
                outcome.Lines.Add($"✗ {f.DisplayName} — {Errors.Friendly(e)}");
            }
        }
        var set = session.Commit();
        outcome.BackupId = set.Items.Count > 0 ? set.Id : null;
        outcome.Ok = done == files.Count;
        FontSystem.Broadcast();
        if (Platform.IsProcessRunning("Photoshop")) outcome.Lines.Insert(0, L.T("font.photoshopOpen"));
        outcome.Entry = Journal.Add("fonts", outcome.Ok ? EntryStatus.Success : done > 0 ? EntryStatus.Partial : EntryStatus.Failed,
            "fix.legacy.journal", outcome.Lines, outcome.BackupId, Fmt.Count(done));
        return outcome;
    }

    /// <summary>Removes registry lines that point to fonts that no longer exist.</summary>
    public static RepairOutcome RemoveRegistrations(IReadOnlyList<FontRegistration> regs, IRegistryAccess? registry = null)
    {
        registry ??= Registries.Default;
        var outcome = new RepairOutcome();
        var session = BackupStore.Begin("fonts", "fix.broken.backup", Keep, Fmt.Count(regs.Count));
        var done = 0;
        foreach (var r in regs)
        {
            try
            {
                if (r.Hive == Hive.LocalMachine && !Platform.IsAdmin) throw new UnauthorizedAccessException(L.T("font.needAdmin"));
                session.AddRegistry(registry, r.Hive, FontInventory.FontsKey, r.Name);
                registry.Delete(r.Hive, FontInventory.FontsKey, r.Name);
                done++;
                outcome.Lines.Add($"✓ {r.Name} → {r.Data}");
            }
            catch (Exception e) { outcome.Lines.Add($"✗ {r.Name} — {Errors.Friendly(e)}"); }
        }
        var set = session.Commit();
        outcome.BackupId = set.Items.Count > 0 ? set.Id : null;
        outcome.Ok = done == regs.Count;
        outcome.Entry = Journal.Add("fonts", outcome.Ok ? EntryStatus.Success : done > 0 ? EntryStatus.Partial : EntryStatus.Failed,
            "fix.broken.journal", outcome.Lines, outcome.BackupId, Fmt.Count(done));
        return outcome;
    }

    /// <summary>Installs verified font files for the current user (no administrator rights needed; modern Photoshop sees them).
    /// A file with the same name already installed for the user is backed up and replaced (an update).</summary>
    public static RepairOutcome InstallForUser(DownloadedFont font, IRegistryAccess? registry = null)
    {
        registry ??= Registries.Default;
        var outcome = new RepairOutcome();
        var dir = FontInventory.UserFontsDir ?? throw new DirectoryNotFoundException(L.T("err.notfound"));
        Directory.CreateDirectory(dir);
        var session = BackupStore.Begin("fonts", "fix.install.backup", Keep, font.Family);
        var done = 0;
        foreach (var (fileName, local, face) in font.Files)
        {
            try
            {
                var dest = Path.Combine(dir, fileName);
                var valueName = $"{face.FullName} ({(face.IsCff ? "OpenType" : "TrueType")})";
                session.AddRegistry(registry, Hive.CurrentUser, FontInventory.FontsKey, valueName);
                if (File.Exists(dest))
                {
                    session.AddFileCopy(dest);
                    FontSystem.Unload(dest);
                    outcome.Lines.Add($"↻ {fileName} — {L.T("font.updated")}");
                }
                else
                {
                    // Restoring this backup must remove the new file: remember that it did not exist.
                    session.AddCreatedFile(dest);
                }
                File.Copy(local, dest, true);
                registry.Set(Hive.CurrentUser, FontInventory.FontsKey, new RegValue(valueName, "String", dest));
                FontSystem.Load(dest);
                done++;
                outcome.Lines.Add($"✓ {face.FullName} — {dest}");
            }
            catch (Exception e) { outcome.Lines.Add($"✗ {fileName} — {Errors.Friendly(e)}"); }
        }
        outcome.Lines.Add(L.T("font.licenseLine", font.License, font.SourceUrl));
        var set = session.Commit();
        outcome.BackupId = set.Id;
        outcome.Ok = done == font.Files.Count;
        FontSystem.Broadcast();
        outcome.Entry = Journal.Add("fonts", outcome.Ok ? EntryStatus.Success : done > 0 ? EntryStatus.Partial : EntryStatus.Failed,
            "fix.install.journal", outcome.Lines, outcome.BackupId, font.Family);
        return outcome;
    }

    /// <summary>Clears Photoshop's "CT Font Cache" (Photoshop rebuilds it on the next start). Photoshop must be closed.</summary>
    public static RepairOutcome ClearPhotoshopCache(PhotoshopInstall ps)
    {
        var outcome = new RepairOutcome();
        if (ps.FontCacheDir is null || !Directory.Exists(ps.FontCacheDir)) throw new DirectoryNotFoundException(L.T("ps.noCache"));
        if (Platform.IsProcessRunning("Photoshop")) throw new InvalidOperationException(L.T("ps.closeFirst"));
        var session = BackupStore.Begin("photoshop", "fix.pscache.backup", Keep, ps.Name);
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(ps.FontCacheDir, "*", SearchOption.AllDirectories).ToList())
        {
            try
            {
                session.AddFileMove(f, new FileInfo(f).Length);
                n++;
            }
            catch (Exception e) { outcome.Lines.Add($"✗ {f} — {Errors.Friendly(e)}"); }
        }
        outcome.Lines.Insert(0, L.T("fix.pscache.line", Fmt.Count(n), ps.FontCacheDir));
        var set = session.Commit();
        outcome.BackupId = set.Items.Count > 0 ? set.Id : null;
        outcome.Ok = outcome.Lines.All(l => !l.StartsWith('✗'));
        outcome.Entry = Journal.Add("photoshop", outcome.Ok ? EntryStatus.Success : EntryStatus.Partial, "fix.pscache.journal", outcome.Lines, outcome.BackupId, ps.Name);
        return outcome;
    }

    /// <summary>Rebuilds the Windows font cache: stops the Font Cache service, moves its cache files to a backup, starts it again.
    /// Needs administrator rights; a restart completes it.</summary>
    public static RepairOutcome RebuildWindowsFontCache()
    {
        if (!Platform.IsWindows) throw new PlatformNotSupportedException();
        if (!Platform.IsAdmin) throw new UnauthorizedAccessException(L.T("font.needAdmin"));
        var outcome = new RepairOutcome { NeedsRestart = true };
        var session = BackupStore.Begin("fonts", "fix.fontcache.backup", Keep);
        Run("sc.exe", "stop FontCache");
        Thread.Sleep(1500);
        var candidates = new List<string>();
        if (Env.Expand(@"%WINDIR%\ServiceProfiles\LocalService\AppData\Local\FontCache") is { } dir && Directory.Exists(dir))
            candidates.AddRange(Directory.EnumerateFiles(dir, "*FontCache*", SearchOption.AllDirectories));
        if (Env.Expand(@"%WINDIR%\System32\FNTCACHE.DAT") is { } fnt && File.Exists(fnt)) candidates.Add(fnt);
        foreach (var f in candidates)
        {
            try
            {
                session.AddFileMove(f, new FileInfo(f).Length);
                outcome.Lines.Add($"✓ {f}");
            }
            catch (Exception e) { outcome.Lines.Add($"✗ {f} — {Errors.Friendly(e)}"); }
        }
        Run("sc.exe", "start FontCache");
        var set = session.Commit();
        outcome.BackupId = set.Items.Count > 0 ? set.Id : null;
        outcome.Ok = true;
        outcome.Lines.Add(L.T("font.restartToFinish"));
        outcome.Entry = Journal.Add("fonts", EntryStatus.Success, "fix.fontcache.journal", outcome.Lines, outcome.BackupId);
        return outcome;
    }

    private static void Run(string exe, string args)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(15000);
        }
        catch (Exception e) { Log.Write($"{exe} {args}: {e.Message}"); }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existing, string? newName, int flags);

    /// <summary>Asks Windows to delete the file at the next restart (MOVEFILE_DELAY_UNTIL_REBOOT). Works for the user's own fonts;
    /// system fonts need administrator rights, which a removal of a system font already has.</summary>
    private static void ScheduleDelete(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { MoveFileEx(path, null, 0x4); } catch (Exception e) { Log.Write($"MoveFileEx: {e.Message}"); }
    }
}
