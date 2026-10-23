namespace IbraCare.Core.Repair;

public enum EntryStatus { Info, Success, Partial, Failed }

/// <summary>One line in the history of scans and repairs.</summary>
public sealed class JournalEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public DateTime Time { get; set; } = DateTime.Now;
    /// <summary>"cleaner", "fonts", "photoshop", "system", "repair".</summary>
    public string Module { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public List<string> TitleArgs { get; set; } = [];
    public EntryStatus Status { get; set; }
    /// <summary>Problems found, files changed, errors — already in the language of the moment they were written,
    /// except for file paths and numbers which read the same in both.</summary>
    public List<string> Details { get; set; } = [];
    public string? BackupId { get; set; }
    public bool Undone { get; set; }
    public DateTime? UndoneAt { get; set; }

    public string Title => L.T(TitleKey, TitleArgs.Cast<object?>().ToArray());
    public bool CanUndo => BackupId is not null && !Undone;
}

/// <summary>The history file (history.json), newest first, at most 2000 entries.</summary>
public static class Journal
{
    private static readonly object Gate = new();
    private const int Max = 2000;
    private static List<JournalEntry>? _items;
    public static event Action? Changed;

    private static string FilePath => Path.Combine(Paths.AppData, "history.json");

    public static IReadOnlyList<JournalEntry> All
    {
        get { lock (Gate) return Items.ToList(); }
    }

    private static List<JournalEntry> Items => _items ??= Json.Load<List<JournalEntry>>(FilePath) ?? [];

    public static JournalEntry Add(string module, EntryStatus status, string titleKey, IEnumerable<string>? details = null, string? backupId = null, params string[] titleArgs)
    {
        var e = new JournalEntry
        {
            Module = module, Status = status, TitleKey = titleKey, TitleArgs = titleArgs.ToList(),
            Details = details?.Take(5000).ToList() ?? [], BackupId = backupId,
        };
        lock (Gate)
        {
            Items.Insert(0, e);
            if (Items.Count > Max) Items.RemoveRange(Max, Items.Count - Max);
            Persist();
        }
        Changed?.Invoke();
        return e;
    }

    public static void MarkUndone(string id)
    {
        lock (Gate)
        {
            var e = Items.FirstOrDefault(x => x.Id == id);
            if (e is null) return;
            e.Undone = true;
            e.UndoneAt = DateTime.Now;
            Persist();
        }
        Changed?.Invoke();
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Items.Clear();
            Persist();
        }
        Changed?.Invoke();
    }

    /// <summary>Undoes an entry through its backup and records the undo.</summary>
    public static RestoreResult Undo(JournalEntry entry, IRegistryAccess registry)
    {
        if (entry.BackupId is null) throw new InvalidOperationException(L.T("repair.notUndoable"));
        var r = BackupStore.Restore(entry.BackupId, registry);
        if (r.Restored > 0 || r.Ok) MarkUndone(entry.Id);
        var details = new List<string> { L.T("repair.undoCounts", r.Restored, r.Skipped.Count, r.Failed.Count) };
        details.AddRange(r.Skipped);
        details.AddRange(r.Failed);
        Add("repair", r.Ok ? (r.Skipped.Count == 0 ? EntryStatus.Success : EntryStatus.Partial) : EntryStatus.Failed,
            "repair.undoTitle", details, null, entry.Title);
        return r;
    }

    private static void Persist()
    {
        try { Json.SaveAtomic(FilePath, Items); }
        catch (Exception e) { Log.Write($"history save: {e.Message}"); }
    }

    /// <summary>For tests.</summary>
    public static void Reload() { lock (Gate) _items = null; }
}
