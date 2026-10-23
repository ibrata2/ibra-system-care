using IbraCare.Core.Repair;

namespace IbraCare.Core.Cleaner;

public sealed class CleanResult
{
    public int Removed { get; set; }
    public long Freed { get; set; }
    public List<string> Skipped { get; } = [];
    public string? BackupId { get; set; }
    public JournalEntry? Entry { get; set; }
    public CleanMode Mode { get; set; }
}

/// <summary>Removes the files chosen in the preview: moved to the quarantine (can be undone) or deleted for good.
/// Every file is checked again right before it is touched.</summary>
public static class CleanRunner
{
    public static CleanResult Run(ScanReport report, CleanMode mode, ISpecialTargets? specials = null,
        IProgress<(int Done, int Total, string Path)>? progress = null, CancellationToken ct = default)
    {
        var s = Settings.Current;
        var result = new CleanResult { Mode = mode };
        var items = report.Targets.Where(t => t.Target.Special is null).SelectMany(t => t.Items).Where(i => i.Selected).ToList();
        var specialTargets = report.Targets.Where(t => t.Target.Special is not null && t.SpecialCount > 0).ToList();
        BackupSession? session = mode == CleanMode.Quarantine && items.Count > 0
            ? BackupStore.Begin("cleaner", "clean.backupTitle", s.QuarantineDays, Fmt.Count(items.Count))
            : null;
        var keep = new Safety.KeepRules(s.KeepList);
        var perTarget = new Dictionary<string, (int Files, long Bytes)>();
        var emptied = new List<(string Dir, string Root)>();
        var done = 0;
        try
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((++done, items.Count, item.Path));
                var why = Recheck(item, keep);
                if (why is not null) { result.Skipped.Add($"{item.Path} — {why}"); continue; }
                try
                {
                    if (session is not null) session.AddFileMove(item.Path, item.Size);
                    else
                    {
                        File.SetAttributes(item.Path, FileAttributes.Normal);
                        File.Delete(item.Path);
                    }
                    result.Removed++;
                    result.Freed += item.Size;
                    if (CleanTargets.ById(item.TargetId)?.RemoveEmptyFolders == true) emptied.Add((Path.GetDirectoryName(item.Path)!, item.Root));
                    var cur = perTarget.GetValueOrDefault(item.TargetId);
                    perTarget[item.TargetId] = (cur.Files + 1, cur.Bytes + item.Size);
                }
                catch (Exception e)
                {
                    result.Skipped.Add($"{item.Path} — {Errors.Friendly(e)}");
                }
            }
        }
        finally
        {
            if (session is not null)
            {
                var set = session.Commit();
                result.BackupId = set.Items.Count > 0 ? set.Id : null;
            }
        }

        // Folders this cleaning left empty (only those that held a cleaned file, and their parents), never the roots themselves.
        foreach (var (dir, root) in emptied.Distinct().OrderByDescending(x => x.Dir.Length)) RemoveIfEmpty(dir, root);

        foreach (var t in specialTargets)
        {
            try
            {
                specials?.Empty(t.Target.Special!);
                result.Removed += (int)Math.Min(int.MaxValue, t.SpecialCount);
                result.Freed += t.SpecialSize;
                perTarget[t.Target.Id] = ((int)t.SpecialCount, t.SpecialSize);
            }
            catch (Exception e) { result.Skipped.Add($"{t.Target.Name} — {Errors.Friendly(e)}"); }
        }

        var details = new List<string>
        {
            L.T(mode == CleanMode.Quarantine ? "clean.modeQuarantineLine" : "clean.modePermanentLine"),
        };
        foreach (var (id, v) in perTarget)
            details.Add($"{CleanTargets.ById(id)?.Name ?? id}: {Fmt.Count(v.Files)} — {Fmt.Size(v.Bytes)}");
        if (result.Skipped.Count > 0)
        {
            details.Add(L.T("clean.skippedHeader", Fmt.Count(result.Skipped.Count)));
            details.AddRange(result.Skipped.Take(500));
        }
        var status = result.Skipped.Count == 0 ? EntryStatus.Success : result.Removed > 0 ? EntryStatus.Partial : EntryStatus.Failed;
        if (result.Removed == 0 && result.Skipped.Count == 0) status = EntryStatus.Info;
        result.Entry = Journal.Add("cleaner", status, "clean.journalTitle", details, result.BackupId, Fmt.Count(result.Removed), Fmt.Size(result.Freed));
        Log.Write($"clean {mode}: removed {result.Removed}, freed {result.Freed}, skipped {result.Skipped.Count}");
        return result;
    }

    /// <summary>Null when the file may be removed now; otherwise why not.</summary>
    private static string? Recheck(ScanItem item, Safety.KeepRules keep)
    {
        try
        {
            if (!Safety.IsInside(item.Path, item.Root) || !Safety.IsAcceptableRoot(item.Root, out _)) return L.T("clean.outsideRoot");
            var f = new FileInfo(item.Path);
            if (!f.Exists) return L.T("clean.goneAlready");
            if (Safety.IsLink(f)) return L.T("clean.isLink");
            if (f.Attributes.HasFlag(FileAttributes.System)) return L.T("clean.isSystem");
            if (!keep.IsEmpty && keep.Matches(f.FullName)) return L.T("clean.isKept");
            // A parent folder that became a link after the scan would lead elsewhere: refuse.
            for (var d = f.Directory; d is not null && Safety.IsInside(d.FullName, item.Root) && !string.Equals(Safety.Normalize(d.FullName), Safety.Normalize(item.Root), Platform.PathComparison); d = d.Parent)
                if (Safety.IsLink(d)) return L.T("clean.isLink");
            return null;
        }
        catch (Exception e) { return Errors.Friendly(e); }
    }

    private static void RemoveIfEmpty(string dir, string root)
    {
        var rootN = Safety.Normalize(root);
        for (var d = new DirectoryInfo(dir); d is not null && Safety.IsInside(d.FullName, rootN) && !string.Equals(Safety.Normalize(d.FullName), rootN, Platform.PathComparison); d = d.Parent)
        {
            try
            {
                if (!d.Exists) continue;
                if (Safety.IsLink(d) || d.EnumerateFileSystemInfos().Any()) return;
                d.Delete();
            }
            catch (Exception) { return; }
        }
    }
}
