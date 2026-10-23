namespace IbraCare.Core.Repair;

public enum BackupState { Active, Restored, Purged }
public enum BackupItemKind { File, Registry }

public sealed class BackupItem
{
    public BackupItemKind Kind { get; set; }
    /// <summary>File: its original full path.</summary>
    public string Original { get; set; } = "";
    /// <summary>File: path inside the backup folder.</summary>
    public string Stored { get; set; } = "";
    public long Size { get; set; }
    /// <summary>True when the file was moved into the backup (quarantine) rather than copied.</summary>
    public bool Moved { get; set; }
    public Hive Hive { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Registry: null when the value did not exist before the change (restoring removes it again).</summary>
    public RegValue? Value { get; set; }
    public bool Restored { get; set; }
    /// <summary>The change created this file (it did not exist before): restoring removes it.</summary>
    public bool CreatedByChange { get; set; }
}

/// <summary>One backup (or quarantine) made before a change. Everything needed to undo the change is in its folder.</summary>
public sealed class BackupSet
{
    public string Id { get; set; } = "";
    public DateTime Created { get; set; }
    public DateTime Expires { get; set; }
    public string Module { get; set; } = "";
    /// <summary>Text key and values, so the title shows in the language chosen now.</summary>
    public string TitleKey { get; set; } = "";
    public List<string> TitleArgs { get; set; } = [];
    public BackupState State { get; set; }
    public List<BackupItem> Items { get; set; } = [];

    public string Title => L.T(TitleKey, TitleArgs.Cast<object?>().ToArray());
    public long Size => Items.Where(i => i.Kind == BackupItemKind.File && !i.Restored).Sum(i => i.Size);
    public int Count => Items.Count;
    public bool IsQuarantine => Items.Any(i => i.Moved);
}

public sealed class RestoreResult
{
    public int Restored { get; set; }
    public List<string> Skipped { get; } = [];
    public List<string> Failed { get; } = [];
    public bool Ok => Failed.Count == 0;
}

public static class BackupStore
{
    private static readonly object Gate = new();

    private static string Folder(string id) => Path.Combine(Paths.Backups, id);
    private static string Manifest(string id) => Path.Combine(Folder(id), "manifest.json");

    public static BackupSession Begin(string module, string titleKey, int keepDays, params string[] titleArgs)
    {
        var now = DateTime.Now;
        var set = new BackupSet
        {
            Id = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
            Created = now,
            Expires = now.AddDays(Math.Max(1, keepDays)),
            Module = module,
            TitleKey = titleKey,
            TitleArgs = titleArgs.ToList(),
        };
        Directory.CreateDirectory(Path.Combine(Folder(set.Id), "files"));
        return new BackupSession(set);
    }

    public static void Save(BackupSet set)
    {
        lock (Gate) Json.SaveAtomic(Manifest(set.Id), set);
    }

    public static BackupSet? Get(string id) => Json.Load<BackupSet>(Manifest(id));

    public static List<BackupSet> All()
    {
        var list = new List<BackupSet>();
        foreach (var dir in Directory.EnumerateDirectories(Paths.Backups))
        {
            var s = Json.Load<BackupSet>(Path.Combine(dir, "manifest.json"));
            if (s is not null) list.Add(s);
        }
        return list.OrderByDescending(s => s.Created).ToList();
    }

    public static string FullStoredPath(BackupSet set, BackupItem item) => Path.Combine(Folder(set.Id), item.Stored);

    /// <summary>Puts every file and registry value back. Files that exist again at their place are overwritten only when
    /// <paramref name="overwrite"/> is true (font repairs), otherwise kept and reported (quarantine).</summary>
    public static RestoreResult Restore(string id, IRegistryAccess registry, bool? overwrite = null)
    {
        var result = new RestoreResult();
        var set = Get(id) ?? throw new FileNotFoundException(L.T("repair.backupMissing"));
        if (set.State == BackupState.Purged) throw new InvalidOperationException(L.T("repair.backupPurged"));
        var replace = overwrite ?? !set.IsQuarantine;
        // Registry values first (in reverse), then files.
        foreach (var item in Enumerable.Reverse(set.Items).Where(i => !i.Restored))
        {
            try
            {
                if (item.Kind == BackupItemKind.Registry)
                {
                    if (item.Value is null) registry.Delete(item.Hive, item.Key, item.Name);
                    else registry.Set(item.Hive, item.Key, item.Value);
                    item.Restored = true;
                    result.Restored++;
                    continue;
                }
                if (item.CreatedByChange)
                {
                    if (File.Exists(item.Original))
                    {
                        if (IsFontFile(item.Original)) Fonts.FontSystem.Unload(item.Original);
                        File.SetAttributes(item.Original, FileAttributes.Normal);
                        File.Delete(item.Original);
                    }
                    item.Restored = true;
                    result.Restored++;
                    continue;
                }
                var stored = FullStoredPath(set, item);
                if (!File.Exists(stored)) { result.Failed.Add($"{item.Original} — {L.T("repair.storedMissing")}"); continue; }
                if (File.Exists(item.Original) && !replace) { result.Skipped.Add($"{item.Original} — {L.T("repair.existsAgain")}"); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(item.Original)!);
                if (item.Moved) File.Move(stored, item.Original, replace);
                else
                {
                    if (replace && File.Exists(item.Original) && IsFontFile(item.Original)) Fonts.FontSystem.Unload(item.Original);
                    File.Copy(stored, item.Original, replace);
                }
                if (IsFontFile(item.Original)) Fonts.FontSystem.Load(item.Original);
                item.Restored = true;
                result.Restored++;
            }
            catch (Exception e)
            {
                result.Failed.Add($"{(item.Kind == BackupItemKind.File ? item.Original : item.Key + "\\" + item.Name)} — {Errors.Friendly(e)}");
            }
        }
        if (set.Items.All(i => i.Restored)) set.State = BackupState.Restored;
        Save(set);
        if (set.Module is "fonts") Fonts.FontSystem.Broadcast();
        return result;
    }

    private static bool IsFontFile(string path) => Fonts.FontInventory.FontExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Deletes the backup's files for good (frees the space).</summary>
    public static void Purge(string id)
    {
        var set = Get(id);
        var files = Path.Combine(Folder(id), "files");
        try { if (Directory.Exists(files)) Directory.Delete(files, true); }
        catch (Exception e) { Log.Write($"purge {id}: {e.Message}"); throw; }
        if (set is not null)
        {
            set.State = BackupState.Purged;
            Save(set);
        }
    }

    /// <summary>Removes expired backups. Returns how many were purged.</summary>
    public static int PurgeExpired()
    {
        var n = 0;
        foreach (var s in All().Where(s => s.State != BackupState.Purged && s.Expires < DateTime.Now))
        {
            try { Purge(s.Id); n++; }
            catch (Exception) { }
        }
        return n;
    }

    public static long TotalSize() => All().Where(s => s.State != BackupState.Purged).Sum(s => s.Size);
}

/// <summary>Collects items while a change is being made; the manifest is written as it goes, so a crash keeps what was saved.</summary>
public sealed class BackupSession
{
    public BackupSet Set { get; }
    private int _n;
    private int _unsaved;

    internal BackupSession(BackupSet set)
    {
        Set = set;
        BackupStore.Save(set);
    }

    public string Id => Set.Id;

    private string NewStoredName(string original)
    {
        var name = Path.GetFileName(original);
        if (name.Length > 80) name = name[..40] + "…" + name[^30..];
        return Path.Combine("files", $"{++_n:D6}_{name}");
    }

    /// <summary>Copies a file into the backup (the original stays).</summary>
    public BackupItem AddFileCopy(string path)
    {
        var item = new BackupItem { Kind = BackupItemKind.File, Original = Path.GetFullPath(path), Stored = NewStoredName(path) };
        var dest = BackupStore.FullStoredPath(Set, item);
        File.Copy(path, dest, true);
        item.Size = new FileInfo(dest).Length;
        Add(item);
        return item;
    }

    /// <summary>Moves a file into the backup (quarantine). Throws if it cannot be moved (e.g. it is in use).</summary>
    public BackupItem AddFileMove(string path, long size)
    {
        var item = new BackupItem { Kind = BackupItemKind.File, Original = Path.GetFullPath(path), Stored = NewStoredName(path), Moved = true, Size = size };
        File.Move(path, BackupStore.FullStoredPath(Set, item));
        Add(item);
        return item;
    }

    /// <summary>Records that the change is about to create this file, so undoing removes it.</summary>
    public BackupItem AddCreatedFile(string path)
    {
        var item = new BackupItem { Kind = BackupItemKind.File, Original = Path.GetFullPath(path), CreatedByChange = true };
        Add(item);
        return item;
    }

    /// <summary>Remembers a registry value as it is now (or that it does not exist).</summary>
    public BackupItem AddRegistry(IRegistryAccess reg, Hive hive, string key, string name)
    {
        var item = new BackupItem { Kind = BackupItemKind.Registry, Hive = hive, Key = key, Name = name, Value = reg.Get(hive, key, name) };
        Add(item);
        return item;
    }

    private void Add(BackupItem item)
    {
        lock (Set) Set.Items.Add(item);
        if (++_unsaved >= 200) Flush();
    }

    public void Flush()
    {
        _unsaved = 0;
        lock (Set) BackupStore.Save(Set);
    }

    /// <summary>Saves the manifest; an empty backup is removed again.</summary>
    public BackupSet Commit()
    {
        if (Set.Items.Count == 0)
        {
            try { Directory.Delete(Path.Combine(Paths.Backups, Set.Id), true); } catch (Exception) { }
            return Set;
        }
        Flush();
        return Set;
    }
}
