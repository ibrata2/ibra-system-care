using IbraCare.Core.Repair;

namespace IbraCare.Core.Fonts;

public sealed class PhotoshopInstall
{
    public required string Name { get; init; }        // "Adobe Photoshop 2025"
    public string? ExePath { get; init; }
    public string? Version { get; init; }
    /// <summary>The per-user font cache Photoshop builds ("CT Font Cache"); deleting it makes Photoshop rebuild its font list.</summary>
    public string? FontCacheDir { get; init; }
    public long FontCacheSize { get; init; }
    public int? Year { get; init; }
}

public sealed class PhotoshopReport
{
    public List<PhotoshopInstall> Installs { get; } = [];
    public bool Running { get; set; }
    public List<FontIssue> Findings { get; } = [];
    /// <summary>Steps inside Photoshop the program cannot do for you (shown as a checklist).</summary>
    public List<string> Advice { get; } = [];
}

/// <summary>Why fonts misbehave in modern Photoshop on Windows 11, from what can really be checked on this computer.</summary>
public static class PhotoshopDoctor
{
    public static PhotoshopReport Diagnose(FontScan fonts, IRegistryAccess? registry = null)
    {
        registry ??= Registries.Default;
        var r = new PhotoshopReport { Running = Platform.IsProcessRunning("Photoshop") };
        r.Installs.AddRange(FindInstalls(registry));

        // 1. Damaged fonts: Photoshop reads every font when it builds its menu; one bad file can freeze or crash it.
        foreach (var f in fonts.Files.Where(f => f.IsCorrupt))
            r.Findings.Add(new FontIssue { Severity = Severity.Error, Code = "ps.corrupt", Path = f.Path, FontName = f.DisplayName, Fix = FontFix.RemoveFont, Message = L.T("ps.f.corrupt") });

        // 2. Type 1 (PostScript) fonts are no longer supported by current Photoshop versions.
        foreach (var f in fonts.Files.Where(f => f.Format == FontFormat.Type1 && f.Path.EndsWith(".pfm", StringComparison.OrdinalIgnoreCase)))
            r.Findings.Add(new FontIssue { Severity = Severity.Warning, Code = "ps.type1", Path = f.Path, FontName = f.DisplayName, Message = L.T("ps.f.type1") });

        // 3. The same font installed twice in different versions: Photoshop may pick the wrong one.
        foreach (var d in fonts.Issues.Where(i => i.Code == "dup"))
            r.Findings.Add(new FontIssue { Severity = Severity.Warning, Code = "ps.dup", Path = d.Path, FontName = d.FontName, KeepPath = d.KeepPath, Fix = d.Fix, Message = L.T("ps.f.dup") });

        // 4. Arabic fonts that will appear disconnected or reversed.
        foreach (var f in fonts.Faces.Where(f => f.Arabic is ArabicSupport.LegacySymbol or ArabicSupport.PresentationFormsOnly or ArabicSupport.NoShaping))
            r.Findings.Add(f.File.LegacyConvertible
                ? new FontIssue { Severity = Severity.Warning, Code = "ps.legacy", Path = f.File.Path, FontName = f.Name, Fix = FontFix.ConvertLegacy, Message = L.T("ps.f.convertible") }
                : new FontIssue { Severity = Severity.Warning, Code = "ps.arabic", Path = f.File.Path, FontName = f.Name, Fix = FontFix.InstallAlternative,
                    Message = L.T(f.Arabic == ArabicSupport.LegacySymbol ? "ps.f.legacy" : "ps.f.disconnected") });

        // 5. Fonts without Windows names do not show (or show under another name) in Photoshop's menu.
        foreach (var f in fonts.Faces.Where(f => !f.Face.HasWindowsNames))
            r.Findings.Add(new FontIssue { Severity = Severity.Info, Code = "ps.names", Path = f.File.Path, FontName = f.Name, Message = L.T("ps.f.names") });

        // 6. Registry lines for fonts that are gone.
        foreach (var b in fonts.BrokenRegistrations)
            r.Findings.Add(new FontIssue { Severity = Severity.Info, Code = "ps.broken", Path = b.ResolvedPath ?? b.Data, FontName = b.Name, Registration = b, Fix = FontFix.RemoveRegistration, Message = L.T("ps.f.broken") });

        // 7. Very many fonts make the font menu slow.
        var faceCount = fonts.Files.Sum(f => Math.Max(1, f.Faces.Count));
        if (faceCount > 2500)
            r.Findings.Add(new FontIssue { Severity = Severity.Info, Code = "ps.many", Message = L.T("ps.f.many", Fmt.Count(faceCount)) });

        // 8. Photoshop's own font cache.
        foreach (var i in r.Installs.Where(i => i.FontCacheDir is not null))
            r.Findings.Add(new FontIssue { Severity = Severity.Info, Code = "ps.cache", Path = i.FontCacheDir, FontName = i.Name, Fix = FontFix.ClearPhotoshopCache,
                Message = L.T("ps.f.cache", Fmt.Size(i.FontCacheSize)) });

        if (r.Installs.Count == 0) r.Advice.Add(L.T("ps.a.notFound"));
        r.Advice.Add(L.T("ps.a.worldReady"));
        r.Advice.Add(L.T("ps.a.composer"));
        r.Advice.Add(L.T("ps.a.direction"));
        r.Advice.Add(L.T("ps.a.restart"));
        r.Advice.Add(L.T("ps.a.prefs"));
        return r;
    }

    public static List<PhotoshopInstall> FindInstalls(IRegistryAccess registry)
    {
        var list = new List<PhotoshopInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Program folders
        foreach (var pf in new[] { Env.Expand("%PROGRAMFILES%"), Env.Expand("%PROGRAMFILES(X86)%") })
        {
            var adobe = pf is null ? null : Path.Combine(pf, "Adobe");
            if (adobe is null || !Directory.Exists(adobe)) continue;
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(adobe, "Adobe Photoshop*").ToList(); } catch (Exception) { continue; }
            foreach (var dir in dirs)
            {
                var exe = Path.Combine(dir, "Photoshop.exe");
                var name = Path.GetFileName(dir);
                if (!seen.Add(name)) continue;
                list.Add(Make(name, File.Exists(exe) ? exe : null, null));
            }
        }
        // Registry: HKLM\SOFTWARE\Adobe\Photoshop\<version>  ApplicationPath
        try
        {
            foreach (var ver in registry.SubKeys(Hive.LocalMachine, @"SOFTWARE\Adobe\Photoshop"))
            {
                var path = registry.Get(Hive.LocalMachine, $@"SOFTWARE\Adobe\Photoshop\{ver}", "ApplicationPath")?.Data;
                if (path is null) continue;
                var name = Path.GetFileName(path.TrimEnd('\\', '/'));
                if (name.Length == 0 || !seen.Add(name)) continue;
                var exe = Path.Combine(path, "Photoshop.exe");
                list.Add(Make(name, File.Exists(exe) ? exe : null, ver));
            }
        }
        catch (Exception e) { Log.Write($"photoshop registry: {e.Message}"); }

        // Caches of versions whose program folder was not found (e.g. installed elsewhere) are still useful.
        var appData = Env.Expand("%APPDATA%");
        var adobeData = appData is null ? null : Path.Combine(appData, "Adobe");
        if (adobeData is not null && Directory.Exists(adobeData))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(adobeData, "Adobe Photoshop*"))
                {
                    var name = Path.GetFileName(dir);
                    if (seen.Add(name)) list.Add(Make(name, null, null));
                }
            }
            catch (Exception) { }
        }
        return list.OrderByDescending(i => i.Year ?? 0).ToList();
    }

    private static PhotoshopInstall Make(string name, string? exe, string? ver)
    {
        var year = System.Text.RegularExpressions.Regex.Match(name, @"(20\d\d)") is { Success: true } m ? int.Parse(m.Value) : (int?)null;
        string? cache = null;
        long size = 0;
        var appData = Env.Expand("%APPDATA%");
        if (appData is not null)
        {
            var c = Path.Combine(appData, "Adobe", name, "CT Font Cache");
            if (Directory.Exists(c))
            {
                cache = c;
                try { size = new DirectoryInfo(c).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch (Exception) { }
            }
        }
        string? fileVersion = ver;
        if (exe is not null && OperatingSystem.IsWindows())
        {
            try { fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? ver; } catch (Exception) { }
        }
        return new PhotoshopInstall { Name = name, ExePath = exe, Version = fileVersion, FontCacheDir = cache, FontCacheSize = size, Year = year };
    }
}
