namespace IbraCare.Core;

public enum CleanMode { Quarantine, Permanent }

public sealed class Settings
{
    /// <summary>"ar" (default) or "en".</summary>
    public string Language { get; set; } = "ar";

    /// <summary>"auto" = light or dark like Windows, "dark", or "light".</summary>
    public string Theme { get; set; } = "auto";

    // Deep Cleaner
    public CleanMode DefaultCleanMode { get; set; } = CleanMode.Quarantine;
    /// <summary>Files in the quarantine (and backups made by the cleaner) are removed for good after this many days.</summary>
    public int QuarantineDays { get; set; } = 7;
    /// <summary>Temporary files changed more recently than this are left alone (they may still be in use).</summary>
    public int MinAgeHours { get; set; } = 24;
    /// <summary>Targets the person turned on or off (by id). Missing = the target's own default.</summary>
    public Dictionary<string, bool> TargetChoices { get; set; } = new();
    /// <summary>Paths or file patterns (* and ?) that are never cleaned — "what I want to keep".</summary>
    public List<string> KeepList { get; set; } = [];

    // Font Doctor
    public bool BackupBeforeFontRepair { get; set; } = true; // always true in practice; kept visible in settings
    public int BackupDays { get; set; } = 60;

    // App
    public bool CheckUpdatesOnStart { get; set; } = true;
    public bool HasSeenWelcome { get; set; }

    // ---------- loading and saving ----------

    private static Settings? _current;
    public static Settings Current => _current ??= Load();
    public static event Action? Changed;

    private static string FilePath => Path.Combine(Paths.AppData, "settings.json");

    public static Settings Load()
    {
        var s = Json.Load<Settings>(FilePath) ?? new Settings();
        s.QuarantineDays = Math.Clamp(s.QuarantineDays, 1, 365);
        s.BackupDays = Math.Clamp(s.BackupDays, 7, 3650);
        s.MinAgeHours = Math.Clamp(s.MinAgeHours, 0, 24 * 90);
        s.KeepList ??= [];
        s.TargetChoices ??= new();
        if (s.Language is not ("ar" or "en")) s.Language = "ar";
        return s;
    }

    public void Save()
    {
        try { Json.SaveAtomic(FilePath, this); }
        catch (Exception e) { Log.Write($"settings save: {e.Message}"); }
        _current = this;
        Changed?.Invoke();
    }

    /// <summary>For tests.</summary>
    public static void Reset(Settings? s = null) => _current = s;
}
