using IbraCare.Core.Repair;

namespace IbraCare.Core.Fonts;

/// <summary>Fonts that look the same: the same letter drawings saved under different names or files
/// (a renamed copy, "Arial (2).ttf", one font sold under three names…). Compared by the outlines themselves, not by names.</summary>
public sealed class LookAlikeGroup
{
    public List<FontFileInfo> Files { get; } = [];
    /// <summary>Share of identical letter drawings between the members (0–1).</summary>
    public double Similarity { get; set; }
    public FontFileInfo Keep { get; set; } = null!;
    /// <summary>"Same file" (byte for byte), "same shapes" (renamed) or "same name, different versions".</summary>
    public string Kind { get; set; } = "";
    public long Wasted => Files.Where(f => !ReferenceEquals(f, Keep)).Sum(f => f.Size);
}

public static class LookAlikes
{
    /// <summary>Glyphs smaller than this (space, dots, simple bars) are too common to tell fonts apart.</summary>
    private const int MinGlyphBytes = 24;
    private const double Threshold = 0.85;
    private const int MinShared = 20;

    /// <summary>One hash per letter drawing of the font (TrueType outlines), or one for the whole outline table (CFF).</summary>
    public static HashSet<ulong> Fingerprint(byte[] data)
    {
        var set = new HashSet<ulong>();
        try
        {
            var f = Sfnt.Read(data);
            if (f.Has("glyf") && f.Has("loca") && f.Has("head"))
            {
                var longLoca = OpenType.U16(f.Table("head"), 50) == 1;
                var loca = f.Table("loca");
                var glyf = f.Table("glyf");
                var n = (longLoca ? loca.Length / 4 : loca.Length / 2) - 1;
                for (var i = 0; i < n; i++)
                {
                    int a, b;
                    if (longLoca) { a = (int)OpenType.U32(loca, i * 4); b = (int)OpenType.U32(loca, i * 4 + 4); }
                    else { a = OpenType.U16(loca, i * 2) * 2; b = OpenType.U16(loca, i * 2 + 2) * 2; }
                    if (b - a < MinGlyphBytes || b > glyf.Length || a < 0) continue;
                    // Skip the 8-byte bounding box at the start, which shifts with tiny edits; hash the contours and points.
                    set.Add(Fnv(glyf.AsSpan(a + 10, b - a - 10)));
                }
            }
            else if (f.Has("CFF ")) set.Add(Fnv(f.Table("CFF ")));
        }
        catch (Exception) { }
        return set;
    }

    private static ulong Fnv(ReadOnlySpan<byte> s)
    {
        // FNV-1a 64
        var h = 14695981039346656037UL;
        foreach (var b in s) { h ^= b; h *= 1099511628211UL; }
        return h;
    }

    public static List<LookAlikeGroup> Find(FontScan scan)
    {
        var files = scan.Files.Where(f => f.Shapes is { Count: >= MinShared } && !f.IsCorrupt).ToList();
        // Index: drawing → fonts that contain it. Count shared drawings per pair.
        var index = new Dictionary<ulong, List<int>>();
        for (var i = 0; i < files.Count; i++)
            foreach (var h in files[i].Shapes!)
            {
                if (!index.TryGetValue(h, out var list)) index[h] = list = [];
                list.Add(i);
            }
        var shared = new Dictionary<(int, int), int>();
        foreach (var list in index.Values)
        {
            if (list.Count < 2 || list.Count > 40) continue; // a drawing found in very many fonts says nothing
            for (var a = 0; a < list.Count; a++)
                for (var b = a + 1; b < list.Count; b++)
                {
                    var key = (list[a], list[b]);
                    shared[key] = shared.GetValueOrDefault(key) + 1;
                }
        }
        // Union of pairs that share most of their drawings.
        var parent = Enumerable.Range(0, files.Count).ToArray();
        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        var best = new Dictionary<int, double>();
        foreach (var ((a, b), n) in shared)
        {
            var sim = (double)n / Math.Min(files[a].Shapes!.Count, files[b].Shapes!.Count);
            var size = (double)Math.Min(files[a].Shapes!.Count, files[b].Shapes!.Count) / Math.Max(files[a].Shapes!.Count, files[b].Shapes!.Count);
            if (n < MinShared || sim < Threshold || size < 0.7) continue;
            parent[Find(a)] = Find(b);
            best[a] = Math.Max(best.GetValueOrDefault(a), sim);
            best[b] = Math.Max(best.GetValueOrDefault(b), sim);
        }
        var groups = new List<LookAlikeGroup>();
        foreach (var g in Enumerable.Range(0, files.Count).GroupBy(Find).Where(g => g.Count() > 1))
        {
            var group = new LookAlikeGroup { Similarity = g.Min(i => best.GetValueOrDefault(i, 1)) };
            group.Files.AddRange(g.Select(i => files[i]));
            foreach (var f in group.Files) f.Sha256 ??= SafeHash(f.Path);
            group.Kind = group.Files.Select(f => f.Sha256).Distinct().Count() == 1 ? "same" : "shapes";
            group.Keep = Recommend(scan, group.Files);
            groups.Add(group);
        }
        // Fonts with the same name but different drawings (two versions) are look-alikes too, if not already grouped.
        var grouped = groups.SelectMany(g => g.Files).ToHashSet();
        foreach (var dup in scan.Issues.Where(i => i.Code == "dup" && i.KeepPath is not null))
        {
            var a = scan.Files.FirstOrDefault(f => f.Path == dup.Path);
            var b = scan.Files.FirstOrDefault(f => f.Path == dup.KeepPath);
            if (a is null || b is null || grouped.Contains(a) || grouped.Contains(b)) continue;
            var existing = groups.FirstOrDefault(g => g.Kind == "versions" && g.Files.Contains(b));
            if (existing is not null) { existing.Files.Add(a); grouped.Add(a); continue; }
            var group = new LookAlikeGroup { Kind = "versions", Similarity = 1, Keep = b };
            group.Files.Add(b);
            group.Files.Add(a);
            grouped.Add(a); grouped.Add(b);
            groups.Add(group);
        }
        return groups.OrderByDescending(g => g.Wasted).ToList();
    }

    /// <summary>The copy to keep: Arabic that joins, then not an old-encoding font, more letters, newer version, the copy for all users.</summary>
    public static FontFileInfo Recommend(FontScan scan, IEnumerable<FontFileInfo> files) =>
        files.OrderBy(f => Rank(scan, f))
            .ThenByDescending(f => f.Faces.FirstOrDefault()?.Coverage.Count ?? 0)
            .ThenByDescending(f => f.Faces.FirstOrDefault()?.VersionNumber ?? 0)
            .ThenBy(f => f.Location == FontLocation.System ? 0 : 1)
            .ThenBy(f => f.FileName.Length)
            .First();

    private static int Rank(FontScan scan, FontFileInfo f)
    {
        if (f.LegacyConvertible) return 3;
        var face = scan.Faces.FirstOrDefault(x => ReferenceEquals(x.File, f));
        return face?.Arabic switch { ArabicSupport.Full => 0, ArabicSupport.None or null => 1, _ => 2 };
    }

    private static string? SafeHash(string path)
    {
        try { return IbraCare.Core.Hash.Sha256(path); } catch (Exception) { return null; }
    }
}

/// <summary>Everything needed to install the fonts on another computer: the font files, an installer (one double-click,
/// no administrator rights) and a list of what is inside.</summary>
public static class FontPack
{
    public sealed class Options
    {
        public bool UserFonts { get; set; } = true;
        /// <summary>Fonts in Windows\Fonts that did not come with Windows.</summary>
        public bool OtherSystemFonts { get; set; } = true;
        /// <summary>Fonts that came with Windows (Arial, Tahoma…): the other computer already has them.</summary>
        public bool WindowsFonts { get; set; }
        /// <summary>One copy per look-alike group (the recommended one).</summary>
        public bool SkipLookAlikes { get; set; } = true;
        /// <summary>Old Arabic fonts that were not converted yet are converted inside the pack (the originals stay as they are).</summary>
        public bool ConvertOldArabic { get; set; } = true;
    }

    public sealed class Result
    {
        public required string ZipPath { get; init; }
        public int Fonts { get; set; }
        public int Converted { get; set; }
        public long Bytes { get; set; }
        public List<string> Skipped { get; } = [];
    }

    private static readonly string[] WindowsVendors = ["MS", "MSFT", "MONO", "MOJO"];

    /// <summary>True for a font Windows installs itself (made by Microsoft or licensed to it, in Windows\Fonts).</summary>
    public static bool CameWithWindows(FontFileInfo f)
    {
        if (f.Location != FontLocation.System) return false;
        var face = f.Faces.FirstOrDefault();
        if (face is null) return f.Format is FontFormat.Bitmap; // old .fon files are Windows' own
        return WindowsVendors.Contains(face.Vendor.Trim(), StringComparer.OrdinalIgnoreCase)
            || face.Copyright.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
            || face.Family.StartsWith("Segoe", StringComparison.OrdinalIgnoreCase);
    }

    public static List<FontFileInfo> Choose(FontScan scan, Options o)
    {
        var lookAlikes = o.SkipLookAlikes ? LookAlikes.Find(scan) : [];
        var drop = lookAlikes.SelectMany(g => g.Files.Where(f => !ReferenceEquals(f, g.Keep))).ToHashSet();
        return scan.Files.Where(f =>
                !f.IsCorrupt && f.Format is not (FontFormat.Bitmap or FontFormat.Woff or FontFormat.Unknown) && !drop.Contains(f) &&
                (f.Location == FontLocation.User ? o.UserFonts
                    : CameWithWindows(f) ? o.WindowsFonts
                    : o.OtherSystemFonts))
            .ToList();
    }

    public static Result Export(FontScan scan, Options o, string zipPath, IProgress<(int Done, int Total, string Name)>? progress = null, CancellationToken ct = default)
    {
        var files = Choose(scan, o);
        var result = new Result { ZipPath = zipPath };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        var tmp = zipPath + ".part";
        var entries = new List<(string File, string Name)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var stream = File.Create(tmp))
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            var done = 0;
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((++done, files.Count, f.DisplayName));
                try
                {
                    var data = File.ReadAllBytes(f.Path);
                    var name = f.FileName;
                    if (o.ConvertOldArabic && f.LegacyConvertible)
                    {
                        data = LegacyArabic.Convert(data).Data;
                        name = Path.GetFileNameWithoutExtension(name) + "-IBRA.ttf";
                        result.Converted++;
                    }
                    // Type 1 fonts are pairs: keep .pfb with .pfm.
                    var stem = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name);
                    for (var k = 2; !usedNames.Add(name); k++) name = $"{stem} ({k}){ext}";
                    var entry = zip.CreateEntry("Fonts/" + name, System.IO.Compression.CompressionLevel.Optimal);
                    using (var es = entry.Open()) es.Write(data);
                    var display = f.Faces.FirstOrDefault(x => x.FullName.Length > 0)?.FullName ?? stem;
                    entries.Add((name, display + (f.Format is FontFormat.OpenTypeCff ? " (OpenType)" : " (TrueType)")));
                    if (f.Format == FontFormat.Type1 && Path.GetExtension(f.Path).Equals(".pfm", StringComparison.OrdinalIgnoreCase))
                    {
                        var pfb = Path.ChangeExtension(f.Path, ".pfb");
                        if (File.Exists(pfb) && usedNames.Add(Path.GetFileName(pfb)))
                            using (var es = zip.CreateEntry("Fonts/" + Path.GetFileName(pfb)).Open()) es.Write(File.ReadAllBytes(pfb));
                    }
                    result.Fonts++;
                    result.Bytes += data.Length;
                }
                catch (Exception e) { result.Skipped.Add($"{f.Path} — {Errors.Friendly(e)}"); }
            }
            Write(zip, "fonts.json", System.Text.Json.JsonSerializer.Serialize(entries.Select(e => new { file = e.File, name = e.Name }), Json.Options), bom: false);
            Write(zip, "Install-Fonts.ps1", InstallScript, bom: true);
            Write(zip, "تثبيت الخطوط - Install Fonts.cmd", InstallCmd, bom: false);
            Write(zip, "README.txt", Readme(result), bom: true);
        }
        File.Move(tmp, zipPath, true);
        Journal.Add("fonts", result.Skipped.Count == 0 ? EntryStatus.Success : EntryStatus.Partial, "pack.journal",
            [zipPath, .. result.Skipped], null, Fmt.Count(result.Fonts));
        return result;
    }

    private static void Write(System.IO.Compression.ZipArchive zip, string name, string text, bool bom)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open(), new System.Text.UTF8Encoding(bom));
        w.Write(text.ReplaceLineEndings("\r\n"));
    }

    private static string Readme(Result r) => $"""
        IBRA SYSTEM CARE — حزمة خطوط / Font pack
        {DateTime.Now:yyyy-MM-dd HH:mm} — {r.Fonts} خط / fonts

        للتثبيت على حاسبة ثانية:
          1. فك الضغط عن الملف (Extract All).
          2. شغّل «تثبيت الخطوط - Install Fonts.cmd» بالضغط المزدوج.
          الخطوط تُثبّت لحسابك بدون صلاحيات المسؤول، والخط المثبّت مسبقاً يُتجاوز.
          أغلق Photoshop وافتحه بعد التثبيت.

        To install on another computer:
          1. Extract the zip (Extract All).
          2. Double-click "تثبيت الخطوط - Install Fonts.cmd".
          Fonts are installed for your account, no administrator rights needed; fonts already installed are skipped.

        {L.T("app.credit")} — https://t.me/ibraprogram — https://www.tiktok.com/@ibraprogram
        """;

    private const string InstallCmd = """
        @echo off
        chcp 65001 >nul
        cd /d "%~dp0"
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Fonts.ps1"
        echo.
        pause
        """;

    /// <summary>Installs every font of the pack for the current user, like Windows' own "Install" (no administrator rights).
    /// -FontsDir and -NoRegistry are for testing.</summary>
    public const string InstallScript = """
        param([string]$FontsDir = "$env:LOCALAPPDATA\Microsoft\Windows\Fonts", [switch]$NoRegistry)
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        $here = Split-Path -Parent $MyInvocation.MyCommand.Path
        $list = Get-Content -Raw -Encoding UTF8 (Join-Path $here 'fonts.json') | ConvertFrom-Json
        New-Item -ItemType Directory -Force $FontsDir | Out-Null
        $key = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Fonts'
        if (-not $NoRegistry -and -not (Test-Path $key)) { New-Item -Force $key | Out-Null }
        Add-Type -Namespace IbraCare -Name Gdi -MemberDefinition @'
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResource(string file);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, int m, IntPtr w, IntPtr l, int f, int t, out IntPtr r);
        '@
        $installed = 0; $skipped = 0; $failed = 0
        Write-Host "IBRA SYSTEM CARE — تثبيت الخطوط / Installing fonts" -ForegroundColor Yellow
        foreach ($f in $list) {
          $src = Join-Path $here (Join-Path 'Fonts' $f.file)
          $dst = Join-Path $FontsDir $f.file
          try {
            if (Test-Path $dst) { $skipped++; continue }
            Copy-Item -LiteralPath $src -Destination $dst
            if (-not $NoRegistry) {
              New-ItemProperty -Path $key -Name $f.name -Value $dst -PropertyType String -Force | Out-Null
              [IbraCare.Gdi]::AddFontResource($dst) | Out-Null
            }
            $installed++
            Write-Host ("  + " + $f.name)
          } catch { $failed++; Write-Host ("  x " + $f.name + " — " + $_.Exception.Message) -ForegroundColor Red }
        }
        if (-not $NoRegistry) { $r = [IntPtr]::Zero; [IbraCare.Gdi]::SendMessageTimeout([IntPtr]0xFFFF, 0x1D, [IntPtr]::Zero, [IntPtr]::Zero, 2, 1000, [ref]$r) | Out-Null }
        Write-Host ""
        Write-Host "تم تثبيت $installed خط، موجود مسبقاً $skipped، فشل $failed" -ForegroundColor Green
        Write-Host "Installed $installed, already there $skipped, failed $failed" -ForegroundColor Green
        exit $failed
        """;
}
