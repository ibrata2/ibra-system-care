using IbraCare.Core.Repair;

namespace IbraCare.Core.Fonts;

public enum FontLocation { System, User, Other }
public enum ArabicSupport { None, Full, Partial, PresentationFormsOnly, NoShaping, LegacySymbol }
public enum Severity { Error, Warning, Info }

/// <summary>A registry line that tells Windows a font exists: "Arial (TrueType)" = "arial.ttf".</summary>
public sealed record FontRegistration(Hive Hive, string Name, string Data, string? ResolvedPath);

public sealed class FontFileInfo
{
    public required string Path { get; init; }
    public FontLocation Location { get; init; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public FontFormat Format { get; set; }
    public List<FontFace> Faces { get; } = [];
    public List<FontRegistration> Registrations { get; } = [];
    public string? Sha256 { get; set; }
    public string? ReadError { get; set; }
    /// <summary>An old symbol-encoded Arabic font (like FS_) that <see cref="LegacyArabic"/> can convert to Unicode.</summary>
    public bool LegacyConvertible { get; set; }
    /// <summary>Hashes of the letter drawings, to find fonts that look the same under different names.</summary>
    public HashSet<ulong>? Shapes { get; set; }

    public string FileName => System.IO.Path.GetFileName(Path);
    public bool IsCorrupt => ReadError is not null || Faces.Any(f => f.IsCorrupt);
    public string DisplayName => Faces.FirstOrDefault(f => f.FullName.Length > 0)?.FullName ?? Registrations.FirstOrDefault()?.Name ?? FileName;
}

/// <summary>What the doctor concluded about one face.</summary>
public sealed class FaceReport
{
    public required FontFileInfo File { get; init; }
    public required FontFace Face { get; init; }
    public bool Latin { get; set; }
    public bool LatinDigits { get; set; }
    public int ArabicLetters { get; set; }
    public bool ArabicExtended { get; set; }
    public bool Harakat { get; set; }
    public bool ArabicDigits { get; set; }
    public bool MarkPositioning { get; set; }
    public ArabicSupport Arabic { get; set; }
    public string Name => Face.FullName.Length > 0 ? Face.FullName : File.FileName;
    public string Family => Face.TypographicFamily.Length > 0 ? Face.TypographicFamily : Face.Family;
}

public sealed class FontIssue
{
    public Severity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Path { get; init; }
    public string? FontName { get; init; }
    public FontFix Fix { get; init; } = FontFix.None;
    public FontRegistration? Registration { get; init; }
    /// <summary>For duplicates: the copy recommended to keep.</summary>
    public string? KeepPath { get; init; }
}

public enum FontFix { None, RemoveFont, RemoveRegistration, RemoveDuplicate, ClearPhotoshopCache, RebuildFontCache, InstallAlternative, ConvertLegacy }

public sealed class FontScan
{
    public DateTime Time { get; } = DateTime.Now;
    public List<FontFileInfo> Files { get; } = [];
    public List<FaceReport> Faces { get; } = [];
    public List<FontIssue> Issues { get; } = [];
    public List<FontRegistration> BrokenRegistrations { get; } = [];

    public int CorruptCount => Files.Count(f => f.IsCorrupt);
    public int DuplicateGroups => Issues.Count(i => i.Code == "dup");
    public IEnumerable<FaceReport> ArabicReady => Faces.Where(f => f.Arabic == ArabicSupport.Full && !f.File.IsCorrupt);
}

public static class FontInventory
{
    public const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    public static readonly string[] FontExtensions = [".ttf", ".otf", ".ttc", ".otc", ".fon", ".fnt", ".pfb", ".pfm"];

    public static string? SystemFontsDir => Env.Expand("%WINDIR%\\Fonts");
    public static string? UserFontsDir => Env.Expand("%LOCALAPPDATA%\\Microsoft\\Windows\\Fonts");

    // Arabic letters every Arabic font must have: ء to ي without tatweel.
    private static readonly int[] CoreArabic = [.. Enumerable.Range(0x0621, 0x063A - 0x0621 + 1), .. Enumerable.Range(0x0641, 0x064A - 0x0641 + 1)];
    private static readonly int[] ExtendedArabic = [0x067E, 0x0686, 0x0698, 0x06AF, 0x06A4, 0x06A9, 0x06CC]; // پ چ ژ گ ڤ ک ی
    private static readonly string[] LegacyArabicHints = ["arab", "naskh", "kufi", "thuluth", "diwani", "ruqaa", "ruqa", "farsi", "nastaliq", "af_", "ae_", "sultan", "hacen", "mudir", "traditional ar", "kofi", "diwan", "fs_"];

    public static FontScan Scan(IRegistryAccess? registry = null, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default,
        IEnumerable<(string Dir, FontLocation Loc)>? extraFolders = null)
    {
        registry ??= Registries.Default;
        var scan = new FontScan();
        var files = new Dictionary<string, FontFileInfo>(Platform.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var folders = new List<(string Dir, FontLocation Loc)>();
        if (SystemFontsDir is { } sys) folders.Add((sys, FontLocation.System));
        if (UserFontsDir is { } usr) folders.Add((usr, FontLocation.User));
        if (extraFolders is not null) folders.AddRange(extraFolders);

        foreach (var (dir, loc) in folders)
        {
            if (!Directory.Exists(dir)) continue;
            IEnumerable<string> list;
            try { list = Directory.EnumerateFiles(dir).ToList(); }
            catch (Exception e) { Log.Write($"fonts list {dir}: {e.Message}"); continue; }
            foreach (var path in list)
            {
                if (!FontExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())) continue;
                files[path] = new FontFileInfo { Path = path, Location = loc };
            }
        }

        // The registry: what Windows will load at sign-in. A line pointing to a missing file is a broken registration.
        foreach (var hive in new[] { Hive.LocalMachine, Hive.CurrentUser })
        {
            IReadOnlyList<RegValue> values;
            try { values = registry.List(hive, FontsKey); }
            catch (Exception e) { Log.Write($"fonts registry {hive}: {e.Message}"); continue; }
            foreach (var v in values)
            {
                var resolved = ResolveRegistered(v.Data);
                var reg = new FontRegistration(hive, v.Name, v.Data, resolved);
                if (resolved is null || !System.IO.File.Exists(resolved))
                {
                    scan.BrokenRegistrations.Add(reg);
                    continue;
                }
                if (!files.TryGetValue(resolved, out var fi))
                    files[resolved] = fi = new FontFileInfo { Path = resolved, Location = FontLocation.Other };
                fi.Registrations.Add(reg);
            }
        }

        var all = files.Values.ToList();
        var done = 0;
        Parallel.ForEach(all, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2), CancellationToken = ct }, fi =>
        {
            Read(fi);
            progress?.Report((Interlocked.Increment(ref done), all.Count));
        });
        scan.Files.AddRange(all.OrderBy(f => f.DisplayName, StringComparer.CurrentCultureIgnoreCase));
        foreach (var fi in scan.Files)
            foreach (var face in fi.Faces.Where(f => !f.IsCorrupt))
                scan.Faces.Add(Analyze(fi, face));
        FindIssues(scan);
        return scan;
    }

    /// <summary>Registry data is a file name inside Windows\Fonts, or a full path (fonts installed for one user).</summary>
    public static string? ResolveRegistered(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        var d = Env.Expand(data.Trim()) ?? data.Trim();
        if (System.IO.Path.DirectorySeparatorChar == '/') d = d.Replace('\\', '/');
        if (System.IO.Path.IsPathRooted(d)) return d;
        return SystemFontsDir is { } sys ? System.IO.Path.Combine(sys, d) : null;
    }

    public static void Read(FontFileInfo fi)
    {
        try
        {
            var info = new FileInfo(fi.Path);
            fi.Size = info.Length;
            fi.Modified = info.LastWriteTime;
            if (info.Length > 150_000_000) { fi.ReadError = L.T("font.p.tooBig"); return; }
            fi.Format = OpenType.Detect(fi.Path);
            if (fi.Format is FontFormat.Type1 or FontFormat.Bitmap) return;
            if (fi.Format is FontFormat.Woff) { fi.ReadError = L.T("font.p.woff"); return; }
            var data = System.IO.File.ReadAllBytes(fi.Path);
            if (data.Length == 0) { fi.ReadError = L.T("font.p.empty"); return; }
            fi.Faces.AddRange(OpenType.Parse(data));
            if (fi.Faces.Count == 1) fi.Shapes = LookAlikes.Fingerprint(data);
            // Old Arabic fonts: no real Arabic letters, shapes at the private codes U+F2xx (symbol cmap, or a "Unicode" one like MCS).
            if (fi.Faces.Count == 1 && !fi.Faces[0].Coverage.Contains(0x0627) && fi.Faces[0].Coverage.Count(c => c is >= 0xF200 and <= 0xF2FF) >= 40)
                fi.LegacyConvertible = LegacyArabic.CanConvert(data);
            if (fi.Format == FontFormat.Unknown && fi.Faces.All(f => f.IsCorrupt)) fi.ReadError ??= null;
        }
        catch (Exception e)
        {
            fi.ReadError = Errors.Friendly(e);
        }
    }

    public static FaceReport Analyze(FontFileInfo fi, FontFace face)
    {
        var c = face.Coverage;
        var r = new FaceReport { File = fi, Face = face };
        r.Latin = Enumerable.Range('A', 26).All(c.Contains) && Enumerable.Range('a', 26).All(c.Contains);
        r.LatinDigits = Enumerable.Range('0', 10).All(c.Contains);
        r.ArabicLetters = CoreArabic.Count(c.Contains);
        r.ArabicExtended = ExtendedArabic.All(c.Contains);
        r.Harakat = Enumerable.Range(0x064B, 8).All(c.Contains);
        r.ArabicDigits = Enumerable.Range(0x0660, 10).All(c.Contains);
        r.MarkPositioning = face.GposFeatures.Contains("mark");
        var presentation = Enumerable.Range(0xFE80, 0xFEFC - 0xFE80 + 1).Count(c.Contains);
        var a = face.ArabicFeatures;
        var hasShaping = a.Contains("init") && a.Contains("medi") && a.Contains("fina");
        var someShaping = a.Contains("init") || a.Contains("medi") || a.Contains("fina") || a.Contains("isol");

        if (fi.LegacyConvertible) r.Arabic = ArabicSupport.LegacySymbol;
        else if (face.IsSymbolEncoded)
        {
            var name = (face.FullName + " " + face.Family + " " + fi.FileName).ToLowerInvariant();
            r.Arabic = fi.LegacyConvertible || LegacyArabicHints.Any(name.Contains) || name.Any(ch => ch is >= '؀' and <= 'ۿ') ? ArabicSupport.LegacySymbol : ArabicSupport.None;
        }
        else if (r.ArabicLetters < 20) r.Arabic = ArabicSupport.None;
        else if (hasShaping) r.Arabic = r.ArabicLetters >= 34 ? ArabicSupport.Full : ArabicSupport.Partial;
        else if (someShaping) r.Arabic = ArabicSupport.Partial;
        else if (presentation >= 100) r.Arabic = ArabicSupport.PresentationFormsOnly;
        else r.Arabic = ArabicSupport.NoShaping;
        return r;
    }

    private static void FindIssues(FontScan scan)
    {
        foreach (var fi in scan.Files)
        {
            if (fi.ReadError is not null)
            {
                scan.Issues.Add(new FontIssue { Severity = Severity.Error, Code = "unreadable", Message = L.T("font.i.unreadable", fi.ReadError), Path = fi.Path, FontName = fi.DisplayName, Fix = FontFix.RemoveFont });
                continue;
            }
            foreach (var face in fi.Faces)
            {
                if (face.IsCorrupt)
                    scan.Issues.Add(new FontIssue { Severity = Severity.Error, Code = "corrupt", Path = fi.Path, FontName = fi.DisplayName, Fix = FontFix.RemoveFont,
                        Message = L.T("font.i.corrupt", string.Join(L.Sep, face.Problems.Distinct().Select(ProblemText))) });
                else
                {
                    var important = face.Warnings.Where(w => w is "font.w.noWindowsNames" or "font.w.symbolEncoded" or "font.w.badLayout").ToList();
                    if (important.Count > 0)
                        scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "warn", Path = fi.Path, FontName = fi.DisplayName,
                            Message = string.Join(L.Sep, important.Select(k => L.T(k))) });
                }
            }
            if (fi.Format == FontFormat.Type1)
                scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "type1", Path = fi.Path, FontName = fi.DisplayName, Message = L.T("font.i.type1") });
            if (fi.Location == FontLocation.User && fi.Registrations.Count == 0 && fi.Format is not (FontFormat.Bitmap or FontFormat.Type1))
                scan.Issues.Add(new FontIssue { Severity = Severity.Info, Code = "unregistered", Path = fi.Path, FontName = fi.DisplayName, Message = L.T("font.i.unregistered") });
        }
        foreach (var b in scan.BrokenRegistrations)
            scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "broken", Path = b.ResolvedPath ?? b.Data, FontName = b.Name, Fix = FontFix.RemoveRegistration, Registration = b,
                Message = L.T("font.i.broken", b.Hive == Hive.LocalMachine ? L.T("font.allUsers") : L.T("font.thisUser")) });

        foreach (var f in scan.Faces)
        {
            if (f.Arabic == ArabicSupport.LegacySymbol)
                scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "legacyArabic", Path = f.File.Path, FontName = f.Name,
                    Fix = f.File.LegacyConvertible ? FontFix.ConvertLegacy : FontFix.InstallAlternative,
                    Message = L.T(f.File.LegacyConvertible ? "font.i.legacyConvertible" : "font.i.legacyArabic") });
            else if (f.Arabic == ArabicSupport.PresentationFormsOnly)
                scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "presentationOnly", Path = f.File.Path, FontName = f.Name, Fix = FontFix.InstallAlternative, Message = L.T("font.i.presentationOnly") });
            else if (f.Arabic == ArabicSupport.NoShaping)
                scan.Issues.Add(new FontIssue { Severity = Severity.Warning, Code = "noShaping", Path = f.File.Path, FontName = f.Name, Fix = FontFix.InstallAlternative, Message = L.T("font.i.noShaping") });
            else if (f.Arabic == ArabicSupport.Partial)
                scan.Issues.Add(new FontIssue { Severity = Severity.Info, Code = "partialArabic", Path = f.File.Path, FontName = f.Name, Message = L.T("font.i.partialArabic", f.ArabicLetters) });
        }

        // Duplicates: the same font (PostScript name, else full name) in more than one file.
        var groups = scan.Files.Where(f => !f.IsCorrupt && f.Format is not (FontFormat.Bitmap or FontFormat.Type1))
            .SelectMany(f => f.Faces.Select(face => (File: f, Face: face)))
            .GroupBy(x => (x.Face.PostScriptName.Length > 0 ? x.Face.PostScriptName : x.Face.FullName).ToLowerInvariant())
            .Where(g => g.Key.Length > 0 && g.Select(x => x.File.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
        foreach (var g in groups)
        {
            var members = g.GroupBy(x => x.File.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            foreach (var m in members) m.File.Sha256 ??= SafeHash(m.File.Path);
            // Keep the copy that works best: Arabic that joins, then fewer warnings, then the newest version, then the system copy.
            var keep = members.OrderBy(m => QualityRank(Analyze(m.File, m.Face).Arabic)).ThenBy(m => m.Face.Warnings.Count)
                .ThenByDescending(m => m.Face.VersionNumber).ThenBy(m => m.File.Location == FontLocation.System ? 0 : 1)
                .ThenByDescending(m => m.File.Modified).First();
            var identical = members.Select(m => m.File.Sha256).Distinct().Count() == 1;
            foreach (var m in members.Where(m => !ReferenceEquals(m.File, keep.File)))
            {
                // Removing a file that holds several faces (a .ttc) would also remove faces that are not duplicated.
                var safeToRemove = m.File.Faces.Count == 1;
                scan.Issues.Add(new FontIssue
                {
                    Severity = Severity.Warning, Code = "dup", Path = m.File.Path, FontName = m.Face.FullName, KeepPath = keep.File.Path,
                    Fix = safeToRemove ? FontFix.RemoveDuplicate : FontFix.None,
                    Message = L.T(identical ? "font.i.dupIdentical" : "font.i.dupVersion", m.Face.VersionText.Length > 0 ? m.Face.VersionText : "?", keep.Face.VersionText.Length > 0 ? keep.Face.VersionText : "?"),
                });
            }
        }
    }

    private static int QualityRank(ArabicSupport a) => a switch
    {
        ArabicSupport.Full => 0, ArabicSupport.None => 1, ArabicSupport.Partial => 2, ArabicSupport.PresentationFormsOnly => 3,
        ArabicSupport.NoShaping => 4, _ => 5,
    };

    public static string ProblemText(string problem)
    {
        if (Strings.Table.ContainsKey(problem)) return L.T(problem);
        if (problem.StartsWith("font.p.missing.")) return L.T("font.p.missingTable", problem["font.p.missing.".Length..]);
        if (problem.StartsWith("font.p.short.")) return L.T("font.p.shortTable", problem["font.p.short.".Length..]);
        return L.T("font.p.damaged");
    }

    private static string? SafeHash(string path)
    {
        try { return Hash.Sha256(path); } catch (Exception) { return null; }
    }

    public static string ArabicLabel(ArabicSupport a) => L.T("arabic." + a);
}
