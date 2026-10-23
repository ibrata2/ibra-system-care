namespace IbraCare.Core.Cleaner;

public sealed class ScanItem
{
    public required string Path { get; init; }
    public required string Root { get; init; }
    public required string TargetId { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    /// <summary>Chosen for cleaning (the person can untick it in the preview).</summary>
    public bool Selected { get; set; } = true;
    /// <summary>Another program had the file open when it was scanned.</summary>
    public bool InUse { get; set; }
}

public enum SkipReason { TooNew, SystemFile, Link, Kept, InUse, NoAccess, OwnData }

public sealed class TargetScan
{
    public required CleanTarget Target { get; init; }
    public List<string> Roots { get; } = [];
    public List<ScanItem> Items { get; } = [];
    public Dictionary<SkipReason, int> Skipped { get; } = new();
    public List<string> Notes { get; } = [];
    /// <summary>Not scanned because it needs administrator rights.</summary>
    public bool NeedsAdmin { get; set; }
    /// <summary>For the Recycle Bin: what the platform reported.</summary>
    public long SpecialSize { get; set; }
    public long SpecialCount { get; set; }

    public long Size => Target.Special is null ? Items.Sum(i => i.Size) : SpecialSize;
    public long SelectedSize => Target.Special is null ? Items.Where(i => i.Selected).Sum(i => i.Size) : SpecialSize;
    public long Count => Target.Special is null ? Items.Count : SpecialCount;
    public int SelectedCount => Items.Count(i => i.Selected);
    public void Skip(SkipReason r) => Skipped[r] = Skipped.GetValueOrDefault(r) + 1;
}

public sealed class ScanReport
{
    public DateTime Time { get; } = DateTime.Now;
    public List<TargetScan> Targets { get; } = [];
    public long TotalSize => Targets.Sum(t => t.Size);
    public long SelectedSize => Targets.Sum(t => t.SelectedSize);
    public long TotalCount => Targets.Sum(t => t.Count);
}

/// <summary>What the platform does for "special" targets (the Recycle Bin on Windows).</summary>
public interface ISpecialTargets
{
    (long Size, long Count) Query(string special);
    void Empty(string special);
}

public sealed class Scanner
{
    public bool IsAdmin { get; init; } = Platform.IsAdmin;
    public ISpecialTargets? Specials { get; init; }
    /// <summary>Open each file without sharing to find files another program is using (Windows only).</summary>
    public bool ProbeInUse { get; init; } = Platform.IsWindows;
    public Settings Settings { get; init; } = Settings.Current;
    private List<string> _protected = [];

    public ScanReport Scan(IEnumerable<CleanTarget> targets, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var report = new ScanReport();
        foreach (var t in targets)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(t.Name);
            report.Targets.Add(ScanTarget(t, ct));
        }
        return report;
    }

    public TargetScan ScanTarget(CleanTarget t, CancellationToken ct = default)
    {
        var scan = new TargetScan { Target = t };
        if (t.Special is not null)
        {
            if (Specials is null) { scan.Notes.Add(L.T("clean.notAvailable")); return scan; }
            try { (scan.SpecialSize, scan.SpecialCount) = Specials.Query(t.Special); }
            catch (Exception e) { scan.Notes.Add(Errors.Friendly(e)); }
            return scan;
        }
        if (t.NeedsAdmin && !IsAdmin)
        {
            scan.NeedsAdmin = true;
        }
        _protected = Safety.ProtectedTrees().ToList();
        var keep = new Safety.KeepRules(Settings.KeepList);
        var minAge = t.MinAgeHours < 0 ? Settings.MinAgeHours : t.MinAgeHours;
        var cutoff = DateTime.UtcNow.AddHours(-minAge);
        var patterns = t.Patterns.Select(Safety.Wildcard).ToList();
        var seen = new HashSet<string>(Platform.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var root in ResolveRoots(t))
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;
            if (!Safety.IsAcceptableRoot(root, out var why))
            {
                scan.Notes.Add($"{root} — {why}");
                continue;
            }
            if (!seen.Add(Safety.Normalize(root))) continue;
            scan.Roots.Add(root);
            Walk(new DirectoryInfo(root), root, t, scan, keep, patterns, cutoff, ct);
        }
        return scan;
    }

    /// <summary>Expands variables and "*" folder names; unknown variables drop the root.</summary>
    public static List<string> ResolveRoots(CleanTarget t)
    {
        var list = new List<string>();
        foreach (var r in t.Roots)
        {
            var expanded = Env.Expand(r);
            if (expanded is null) continue;
            var parts = expanded.Split(Path.DirectorySeparatorChar);
            var current = new List<string> { parts[0].Length == 0 ? Path.DirectorySeparatorChar.ToString() : parts[0] + (parts[0].EndsWith(':') ? Path.DirectorySeparatorChar : "") };
            for (var i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var next = new List<string>();
                foreach (var c in current)
                {
                    if (parts[i].Contains('*'))
                    {
                        try
                        {
                            var rx = Safety.Wildcard(parts[i]);
                            foreach (var d in new DirectoryInfo(c).EnumerateDirectories())
                                if (rx.IsMatch(d.Name) && !Safety.IsLink(d)) next.Add(d.FullName);
                        }
                        catch (Exception) { }
                    }
                    else next.Add(Path.Combine(c, parts[i]));
                }
                current = next;
            }
            list.AddRange(current);
        }
        return list;
    }

    private void Walk(DirectoryInfo dir, string root, CleanTarget t, TargetScan scan, Safety.KeepRules keep, List<System.Text.RegularExpressions.Regex> patterns, DateTime cutoff, CancellationToken ct)
    {
        IEnumerable<FileSystemInfo> entries;
        try { entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false }).ToList(); }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            scan.Skip(SkipReason.NoAccess);
            if (t.NeedsAdmin && !IsAdmin) scan.NeedsAdmin = true;
            return;
        }
        foreach (var e in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (_protected.Any(p => Safety.IsInside(e.FullName, p))) { scan.Skip(SkipReason.OwnData); continue; }
            if (Safety.IsLink(e)) { scan.Skip(SkipReason.Link); continue; }
            if (e is DirectoryInfo sub)
            {
                if (t.Recursive) Walk(sub, root, t, scan, keep, patterns, cutoff, ct);
                continue;
            }
            if (e is not FileInfo f) continue;
            if (patterns.Count > 0 && !patterns.Any(p => p.IsMatch(f.Name))) continue;
            try
            {
                if (f.Attributes.HasFlag(FileAttributes.System)) { scan.Skip(SkipReason.SystemFile); continue; }
                var changed = f.LastWriteTimeUtc > f.CreationTimeUtc ? f.LastWriteTimeUtc : f.CreationTimeUtc;
                if (changed > cutoff) { scan.Skip(SkipReason.TooNew); continue; }
                if (!keep.IsEmpty && keep.Matches(f.FullName)) { scan.Skip(SkipReason.Kept); continue; }
                var item = new ScanItem { Path = f.FullName, Root = root, TargetId = t.Id, Size = f.Length, Modified = f.LastWriteTime };
                if (ProbeInUse && IsInUse(f.FullName))
                {
                    item.InUse = true;
                    item.Selected = false;
                }
                scan.Items.Add(item);
            }
            catch (Exception) { scan.Skip(SkipReason.NoAccess); }
        }
    }

    /// <summary>True when another program holds the file open so it cannot be removed now.</summary>
    public static bool IsInUse(string path)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            return false;
        }
        catch (IOException e) when (Errors.IsInUse(e)) { return true; }
        catch (Exception) { return false; }
    }
}
