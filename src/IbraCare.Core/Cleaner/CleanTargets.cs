namespace IbraCare.Core.Cleaner;

public enum TargetGroup { System, Browsers, Apps, Graphics }
public enum Risk { Safe, Careful }

/// <summary>
/// One known place that holds files Windows or an app recreates by itself (temporary files, caches, logs, crash reports).
/// Roots use %VARIABLES%; a "*" folder name matches every profile (e.g. all Chrome profiles).
/// </summary>
public sealed class CleanTarget
{
    public required string Id { get; init; }
    public TargetGroup Group { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public required string[] Roots { get; init; }
    /// <summary>File name patterns (*.log). Empty = every file.</summary>
    public string[] Patterns { get; init; } = [];
    public bool Recursive { get; init; } = true;
    /// <summary>-1 = the age from the settings; 0 = any age; otherwise hours.</summary>
    public int MinAgeHours { get; init; } = -1;
    public bool NeedsAdmin { get; init; }
    public bool DefaultOn { get; init; } = true;
    /// <summary>Programs that hold these files while open (shown as "close X first").</summary>
    public string[] Apps { get; init; } = [];
    /// <summary>Handled by the platform (the Recycle Bin), not by walking folders.</summary>
    public string? Special { get; init; }
    /// <summary>Removes folders left empty after cleaning (temporary folders). Never the root itself.</summary>
    public bool RemoveEmptyFolders { get; init; }
    /// <summary>The files can only be deleted, not quarantined (the Recycle Bin).</summary>
    public bool PermanentOnly { get; init; }

    public string Name => L.T("target." + Id);
    public string Description => L.T("target." + Id + ".d");

    public bool IsOn(Settings s) => s.TargetChoices.TryGetValue(Id, out var on) ? on : DefaultOn;
}

public static class CleanTargets
{
    private static string[] ChromiumCaches(string userData) =>
    [
        userData + "\\*\\Cache\\Cache_Data", userData + "\\*\\Code Cache", userData + "\\*\\GPUCache",
        userData + "\\*\\DawnGraphiteCache", userData + "\\*\\DawnWebGPUCache",
        userData + "\\ShaderCache", userData + "\\GrShaderCache",
    ];

    public static readonly IReadOnlyList<CleanTarget> All =
    [
        // ---------- Windows ----------
        new() { Id = "userTemp", Group = TargetGroup.System, Roots = ["%TEMP%", "%LOCALAPPDATA%\\Temp"], RemoveEmptyFolders = true },
        new() { Id = "winTemp", Group = TargetGroup.System, Roots = ["%WINDIR%\\Temp"], NeedsAdmin = true, RemoveEmptyFolders = true },
        new() { Id = "wer", Group = TargetGroup.System, MinAgeHours = 0, RemoveEmptyFolders = true,
            Roots = ["%LOCALAPPDATA%\\Microsoft\\Windows\\WER", "%PROGRAMDATA%\\Microsoft\\Windows\\WER\\ReportArchive",
                     "%PROGRAMDATA%\\Microsoft\\Windows\\WER\\ReportQueue", "%PROGRAMDATA%\\Microsoft\\Windows\\WER\\Temp"] },
        new() { Id = "crashDumps", Group = TargetGroup.System, MinAgeHours = 0, DefaultOn = false,
            Roots = ["%LOCALAPPDATA%\\CrashDumps", "%WINDIR%\\Minidump"], Patterns = ["*.dmp", "*.mdmp", "*.hdmp"] },
        new() { Id = "thumbCache", Group = TargetGroup.System, MinAgeHours = 0, Recursive = false, Apps = ["explorer"],
            Roots = ["%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer"], Patterns = ["thumbcache_*.db", "iconcache_*.db"] },
        new() { Id = "inetCache", Group = TargetGroup.System, MinAgeHours = 0, RemoveEmptyFolders = true,
            Roots = ["%LOCALAPPDATA%\\Microsoft\\Windows\\INetCache\\IE", "%LOCALAPPDATA%\\Microsoft\\Windows\\INetCache\\Low\\IE"] },
        new() { Id = "updateCache", Group = TargetGroup.System, Risk = Risk.Careful, NeedsAdmin = true, DefaultOn = false, MinAgeHours = 72,
            Roots = ["%WINDIR%\\SoftwareDistribution\\Download"], RemoveEmptyFolders = true },
        new() { Id = "deliveryOpt", Group = TargetGroup.System, NeedsAdmin = true, DefaultOn = false, MinAgeHours = 0,
            Roots = ["%WINDIR%\\ServiceProfiles\\NetworkService\\AppData\\Local\\Microsoft\\Windows\\DeliveryOptimization\\Cache"] },
        new() { Id = "winLogs", Group = TargetGroup.System, Risk = Risk.Careful, NeedsAdmin = true, DefaultOn = false, MinAgeHours = 24 * 14,
            Roots = ["%WINDIR%\\Logs\\CBS", "%WINDIR%\\Logs\\DISM", "%WINDIR%\\Logs\\WindowsUpdate", "%WINDIR%\\Panther"],
            Patterns = ["*.log", "*.cab", "*.etl", "*.old"] },
        new() { Id = "recycleBin", Group = TargetGroup.System, Roots = [], Special = "recyclebin", DefaultOn = false, PermanentOnly = true },

        // ---------- Browsers ----------
        new() { Id = "chrome", Group = TargetGroup.Browsers, MinAgeHours = 0, Apps = ["chrome"], Roots = ChromiumCaches("%LOCALAPPDATA%\\Google\\Chrome\\User Data") },
        new() { Id = "edge", Group = TargetGroup.Browsers, MinAgeHours = 0, Apps = ["msedge"], Roots = ChromiumCaches("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data") },
        new() { Id = "brave", Group = TargetGroup.Browsers, MinAgeHours = 0, Apps = ["brave"], Roots = ChromiumCaches("%LOCALAPPDATA%\\BraveSoftware\\Brave-Browser\\User Data") },
        new() { Id = "opera", Group = TargetGroup.Browsers, MinAgeHours = 0, Apps = ["opera"],
            Roots = ["%LOCALAPPDATA%\\Opera Software\\Opera Stable\\Cache\\Cache_Data", "%APPDATA%\\Opera Software\\Opera Stable\\Code Cache", "%APPDATA%\\Opera Software\\Opera Stable\\GPUCache"] },
        new() { Id = "firefox", Group = TargetGroup.Browsers, MinAgeHours = 0, Apps = ["firefox"],
            Roots = ["%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2", "%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\startupCache"] },

        // ---------- Apps ----------
        new() { Id = "adobeMedia", Group = TargetGroup.Apps, MinAgeHours = 0, DefaultOn = false, Apps = ["Photoshop", "Adobe Premiere Pro", "AfterFX"],
            Roots = ["%APPDATA%\\Adobe\\Common\\Media Cache Files", "%APPDATA%\\Adobe\\Common\\Media Cache", "%LOCALAPPDATA%\\Adobe\\Common\\Media Cache Files"] },
        new() { Id = "discord", Group = TargetGroup.Apps, MinAgeHours = 0, Apps = ["Discord"],
            Roots = ["%APPDATA%\\discord\\Cache\\Cache_Data", "%APPDATA%\\discord\\Code Cache", "%APPDATA%\\discord\\GPUCache"] },
        new() { Id = "teams", Group = TargetGroup.Apps, MinAgeHours = 0, Apps = ["Teams", "ms-teams"],
            Roots = ["%APPDATA%\\Microsoft\\Teams\\Cache", "%APPDATA%\\Microsoft\\Teams\\Code Cache", "%APPDATA%\\Microsoft\\Teams\\GPUCache"] },
        new() { Id = "devCaches", Group = TargetGroup.Apps, MinAgeHours = 0, DefaultOn = false,
            Roots = ["%LOCALAPPDATA%\\npm-cache\\_cacache", "%LOCALAPPDATA%\\pip\\Cache", "%LOCALAPPDATA%\\Yarn\\Cache"] },

        // ---------- Graphics ----------
        new() { Id = "shaderCache", Group = TargetGroup.Graphics, MinAgeHours = 0, DefaultOn = false,
            Roots = ["%LOCALAPPDATA%\\D3DSCache", "%LOCALAPPDATA%\\NVIDIA\\DXCache", "%LOCALAPPDATA%\\NVIDIA\\GLCache", "%LOCALAPPDATA%\\AMD\\DxCache", "%LOCALAPPDATA%\\AMD\\GLCache"] },
    ];

    public static CleanTarget? ById(string id) => All.FirstOrDefault(t => t.Id == id);
}
