// Real tests of the IBRA SYSTEM CARE engine: real files, real fonts, a fake Windows folder tree, an in-memory registry
// and (unless --offline) real downloads from Google Fonts. Run: dotnet run -c Release --project tests/IbraCare.Tests -- <tests/data>
using System.Text.RegularExpressions;
using IbraCare.Core;
using IbraCare.Core.Cleaner;
using IbraCare.Core.Fonts;
using IbraCare.Core.Repair;

var dataDir = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? "tests/data");
var offline = args.Contains("--offline");
var srcDir = Path.GetFullPath(Path.Combine(dataDir, "..", "..", "src"));
var work = Path.Combine(Path.GetTempPath(), "ibracare-tests-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(work);
var failures = 0;
var passed = 0;

void Check(bool ok, string what)
{
    if (ok) { passed++; Console.WriteLine($"  ok   {what}"); }
    else { failures++; Console.WriteLine($"  FAIL {what}"); }
}

void Section(string name) => Console.WriteLine($"\n== {name}");

string Fake(params string[] parts) => Path.Combine([work, "win", .. parts]);

void Touch(string path, int bytes, double hoursAgo)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, new byte[bytes]);
    var t = DateTime.UtcNow.AddHours(-hoursAgo);
    File.SetCreationTimeUtc(path, t);
    File.SetLastWriteTimeUtc(path, t);
}

// A fake Windows: every %VARIABLE% points inside the work folder.
Paths.AppDataOverride = Path.Combine(work, "appdata");
Env.Override = new()
{
    ["TEMP"] = Fake("Users", "me", "AppData", "Local", "Temp"),
    ["LOCALAPPDATA"] = Fake("Users", "me", "AppData", "Local"),
    ["APPDATA"] = Fake("Users", "me", "AppData", "Roaming"),
    ["WINDIR"] = Fake("Windows"),
    ["PROGRAMDATA"] = Fake("ProgramData"),
    ["USERPROFILE"] = Fake("Users", "me"),
    ["PROGRAMFILES"] = Fake("Program Files"),
    ["PROGRAMFILES(X86)"] = Fake("Program Files (x86)"),
};
var settings = new Settings();
Settings.Reset(settings);
var reg = new MemoryRegistry();
Registries.Default = reg;

try
{
    // ------------------------------------------------------------------
    Section("Translations");
    var used = new HashSet<string>();
    foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\bL\.T\(""([A-Za-z0-9_.]+)"""))
            if (!m.Groups[1].Value.EndsWith('.')) used.Add(m.Groups[1].Value);
    var missing = used.Where(k => !Strings.Table.ContainsKey(k)).ToList();
    Check(missing.Count == 0, $"every text used in the code exists ({used.Count} keys) {string.Join(", ", missing)}");
    var empty = Strings.Table.Where(kv => string.IsNullOrWhiteSpace(kv.Value.Ar) || string.IsNullOrWhiteSpace(kv.Value.En)).Select(kv => kv.Key).ToList();
    Check(empty.Count == 0, $"every text has Arabic and English {string.Join(", ", empty)}");
    var badArgs = Strings.Table.Where(kv => Holes(kv.Value.Ar) != Holes(kv.Value.En)).Select(kv => kv.Key).ToList();
    Check(badArgs.Count == 0, $"Arabic and English have the same {{0}} values {string.Join(", ", badArgs)}");
    foreach (var t in CleanTargets.All)
        Check(Strings.Table.ContainsKey("target." + t.Id) && Strings.Table.ContainsKey("target." + t.Id + ".d"), $"target {t.Id} has a name and description");
    var dynamicKeys = new List<string>();
    dynamicKeys.AddRange(Enum.GetNames<BackupState>().Select(n => "bstate." + n));
    dynamicKeys.AddRange(Enum.GetNames<FontFix>().Select(n => "fix." + n));
    dynamicKeys.AddRange(Enum.GetNames<FontStyleKind>().Select(n => "kind." + n));
    dynamicKeys.AddRange(Enum.GetNames<FontLocation>().Select(n => "loc." + n));
    dynamicKeys.AddRange(Enum.GetNames<Severity>().Select(n => "sev." + n));
    dynamicKeys.AddRange(Enum.GetNames<EntryStatus>().Select(n => "status." + n));
    dynamicKeys.AddRange(Enum.GetNames<ArabicSupport>().Select(n => "arabic." + n));
    dynamicKeys.AddRange(Enum.GetNames<SkipReason>().Select(n => "skip." + n));
    dynamicKeys.AddRange(Enum.GetNames<TargetGroup>().Select(n => "group." + n));
    dynamicKeys.AddRange(new[] { "cleaner", "fonts", "photoshop", "system", "repair" }.Select(n => "module." + n));
    var missingDynamic = dynamicKeys.Where(k => !Strings.Table.ContainsKey(k)).ToList();
    Check(missingDynamic.Count == 0, $"texts built from names exist ({dynamicKeys.Count}) {string.Join(", ", missingDynamic)}");
    foreach (Match m in Regex.Matches(string.Join("\n", Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)), @"Journal\.Add\([^,]+,[^,]+,\s*""([a-zA-Z.]+)"""))
        Check(Strings.Table.ContainsKey(m.Groups[1].Value), $"history title {m.Groups[1].Value} exists");
    L.Language = "en";
    Check(L.T("nav.home") == "Home", "English is used when chosen");
    L.Language = "ar";
    Check(L.T("nav.home") == "الرئيسية", "Arabic is the default");
    Check(L.T("clean.journalTitle", "3", "1 MB") == "تنظيف: 3 ملف — 1 MB", "values are filled in");

    // ------------------------------------------------------------------
    Section("Safety rules");
    Check(!Safety.IsAcceptableRoot("/", out _), "a drive root is refused");
    Check(!Safety.IsAcceptableRoot(Fake("Windows"), out _), "the Windows folder itself is refused");
    Check(!Safety.IsAcceptableRoot(Fake("Users", "me"), out _), "the user folder itself is refused");
    Check(!Safety.IsAcceptableRoot(Fake("Users", "me", "AppData", "Local"), out _), "LocalAppData itself is refused");
    Check(Safety.IsAcceptableRoot(Fake("Users", "me", "AppData", "Local", "Temp"), out _), "the Temp folder is accepted");
    Check(!Safety.IsAcceptableRoot(Paths.AppData, out _), "the app's own data folder is refused");
    Check(Safety.IsAcceptableRoot(work, out _), "a folder that contains the app's data is still accepted (its data is skipped while scanning)");
    Check(Safety.IsInside("/a/b/c", "/a/b") && !Safety.IsInside("/a/bc", "/a/b"), "inside-folder test does not confuse /a/bc with /a/b");
    var keep = new Safety.KeepRules(["*.keep", Fake("Users", "me", "AppData", "Local", "Temp", "mine"), "%TEMP%/report-??.txt"]);
    Check(keep.Matches(Fake("x", "a.keep")), "keep rule by name pattern");
    Check(keep.Matches(Fake("Users", "me", "AppData", "Local", "Temp", "mine", "deep", "f.bin")), "keep rule by folder");
    Check(keep.Matches(Fake("Users", "me", "AppData", "Local", "Temp", "report-01.txt")) && !keep.Matches(Fake("Users", "me", "AppData", "Local", "Temp", "report-001.txt")), "keep rule with %TEMP% and ?");

    // ------------------------------------------------------------------
    Section("Deep Cleaner — scan");
    var temp = Env.Override["TEMP"];
    Touch(Path.Combine(temp, "old1.tmp"), 1000, 48);
    Touch(Path.Combine(temp, "sub", "old2.log"), 2000, 72);
    Touch(Path.Combine(temp, "sub", "deeper", "old3.dat"), 3000, 100);
    Touch(Path.Combine(temp, "fresh.tmp"), 500, 1);                  // too new
    Touch(Path.Combine(temp, "x.keep"), 400, 100);                   // on the keep list
    Touch(Path.Combine(temp, "mine", "important.txt"), 400, 100);     // kept folder
    // A link inside Temp that points to a folder outside it: must never be followed.
    var outside = Path.Combine(work, "precious");
    Touch(Path.Combine(outside, "victim.docx"), 777, 500);
    Directory.CreateSymbolicLink(Path.Combine(temp, "link-to-precious"), outside);
    File.CreateSymbolicLink(Path.Combine(temp, "file-link.docx"), Path.Combine(outside, "victim.docx"));
    var chrome = Fake("Users", "me", "AppData", "Local", "Google", "Chrome", "User Data");
    Touch(Path.Combine(chrome, "Default", "Cache", "Cache_Data", "f_000001"), 5000, 1);
    Touch(Path.Combine(chrome, "Profile 1", "Cache", "Cache_Data", "f_000002"), 6000, 1);
    Touch(Path.Combine(chrome, "Default", "Login Data"), 123, 100);  // passwords: never part of a target
    Touch(Path.Combine(Fake("Users", "me", "AppData", "Local", "CrashDumps"), "app.exe.123.dmp"), 9000, 5);
    Touch(Path.Combine(Fake("Users", "me", "AppData", "Local", "CrashDumps"), "notes.txt"), 10, 5);  // pattern is *.dmp only

    Directory.CreateDirectory(Path.Combine(temp, "empty-app-folder"));                // an app's own empty folder
    settings.KeepList = ["*.keep", Path.Combine(temp, "mine")];
    var scanner = new Scanner { IsAdmin = false, ProbeInUse = false, Settings = settings };
    var report = scanner.Scan([CleanTargets.ById("userTemp")!, CleanTargets.ById("chrome")!, CleanTargets.ById("crashDumps")!, CleanTargets.ById("winTemp")!]);
    var tempScan = report.Targets[0];
    Check(tempScan.Items.Count == 3, $"Temp: 3 old files found (found {tempScan.Items.Count})");
    Check(tempScan.Items.Sum(i => i.Size) == 6000, "Temp: real sizes are added up");
    Check(tempScan.Skipped.GetValueOrDefault(SkipReason.TooNew) == 1, "Temp: the recent file is left alone");
    Check(tempScan.Skipped.GetValueOrDefault(SkipReason.Kept) == 2, "Temp: files on the keep list are left alone");
    Check(tempScan.Skipped.GetValueOrDefault(SkipReason.Link) == 2, "Temp: links are not followed");
    Check(tempScan.Items.All(i => !i.Path.Contains("precious")), "Temp: nothing outside Temp is listed");
    Check(tempScan.Roots.Count == 1, "Temp: %TEMP% and LocalAppData\\Temp count once");
    var ownInside = scanner.ScanTarget(new CleanTarget { Id = "userTemp", Roots = [work], MinAgeHours = 0 });
    Check(ownInside.Items.All(i => !Safety.IsInside(i.Path, Paths.AppData)) && ownInside.Skipped.GetValueOrDefault(SkipReason.OwnData) > 0, "the app's own data is skipped inside a scanned folder");
    var chromeScan = report.Targets[1];
    Check(chromeScan.Items.Count == 2, "Chrome: cache of every profile found, Login Data untouched");
    Check(report.Targets[2].Items.Count == 1 && report.Targets[2].Items[0].Path.EndsWith(".dmp"), "Crash dumps: only *.dmp");
    Check(report.Targets[3].NeedsAdmin, "Windows Temp is marked as needing administrator rights");

    // A misconfigured %TEMP% pointing at the user folder must be refused, not emptied.
    Env.Override["TEMP"] = Fake("Users", "me");
    var bad = scanner.ScanTarget(new CleanTarget { Id = "userTemp", Roots = ["%TEMP%"] });
    Check(bad.Items.Count == 0 && bad.Notes.Count == 1, "a %TEMP% pointing at the user folder is refused");
    Env.Override["TEMP"] = temp;

    // ------------------------------------------------------------------
    Section("Deep Cleaner — quarantine, undo, permanent");
    // Untick one file in the preview: it must stay.
    tempScan.Items.First(i => i.Path.EndsWith("old1.tmp")).Selected = false;
    var res = CleanRunner.Run(report, CleanMode.Quarantine);
    Check(res.Removed == 5, $"quarantine removed the 5 chosen files (removed {res.Removed}, skipped {res.Skipped.Count})");
    Check(File.Exists(Path.Combine(temp, "old1.tmp")), "the unticked file is kept");
    Check(!File.Exists(Path.Combine(temp, "sub", "deeper", "old3.dat")), "a chosen file is gone from its place");
    Check(!Directory.Exists(Path.Combine(temp, "sub")), "folders left empty in Temp are removed");
    Check(Directory.Exists(Path.Combine(temp, "empty-app-folder")), "empty folders the cleaning did not touch are kept");
    Check(File.Exists(Path.Combine(outside, "victim.docx")), "the file behind the link is untouched");
    Check(File.Exists(Path.Combine(temp, "fresh.tmp")) && File.Exists(Path.Combine(temp, "x.keep")), "recent and kept files are untouched");
    Check(File.Exists(Path.Combine(chrome, "Default", "Login Data")), "browser passwords file is untouched");
    Check(res.BackupId is not null && BackupStore.Get(res.BackupId!)!.Items.Count == 5, "the quarantine holds the 5 files");
    Check(res.Entry is not null && Journal.All[0].Id == res.Entry.Id && Journal.All[0].CanUndo, "the history has an entry that can be undone");

    var undo = Journal.Undo(Journal.All.First(e => e.Id == res.Entry!.Id), reg);
    Check(undo.Restored == 5 && undo.Ok, "undo restored every file");
    Check(File.Exists(Path.Combine(temp, "sub", "deeper", "old3.dat")) && new FileInfo(Path.Combine(temp, "sub", "deeper", "old3.dat")).Length == 3000, "files are back with their content");
    Check(Journal.All.First(e => e.Id == res.Entry!.Id).Undone, "the entry is marked as undone");

    var report2 = scanner.Scan([CleanTargets.ById("userTemp")!]);
    var res2 = CleanRunner.Run(report2, CleanMode.Permanent);
    Check(res2.Removed == 3 && res2.BackupId is null, "permanent delete removes the files without a backup");
    Check(!File.Exists(Path.Combine(temp, "old1.tmp")) && File.Exists(Path.Combine(outside, "victim.docx")), "deleted inside Temp only");

    // A file replaced by a link between scan and clean must not be deleted through the link.
    Touch(Path.Combine(temp, "swap", "a.tmp"), 10, 100);
    var report3 = scanner.Scan([CleanTargets.ById("userTemp")!]);
    Directory.Delete(Path.Combine(temp, "swap"), true);
    Directory.CreateSymbolicLink(Path.Combine(temp, "swap"), outside);
    Touch(Path.Combine(outside, "a.tmp"), 10, 100);
    var res3 = CleanRunner.Run(report3, CleanMode.Permanent);
    Check(File.Exists(Path.Combine(outside, "a.tmp")) && res3.Skipped.Count == 1, "a folder swapped for a link after the scan is refused");

    // ------------------------------------------------------------------
    Section("Font reader");
    var arabicFont = Path.Combine(dataDir, "NotoSansArabicUI-Regular.ttf");
    var latinFont = Path.Combine(dataDir, "NotoSans-Regular.ttf");
    var ar = OpenType.Parse(File.ReadAllBytes(arabicFont))[0];
    Check(!ar.IsCorrupt, "Noto Sans Arabic UI reads without problems " + string.Join(",", ar.Problems));
    Check(ar.Family.StartsWith("Noto Sans Arabic"), $"family name read ({ar.Family})");
    Check(ar.GsubScripts.Contains("arab") && ar.ArabicFeatures.Contains("init") && ar.ArabicFeatures.Contains("medi") && ar.ArabicFeatures.Contains("fina"), "Arabic joining features found in GSUB");
    var arReport = FontInventory.Analyze(new FontFileInfo { Path = arabicFont }, ar);
    Check(arReport.Arabic == ArabicSupport.Full, $"Arabic support: Full ({arReport.Arabic}, {arReport.ArabicLetters} letters)");
    Check(arReport.ArabicExtended, "گ چ پ ژ ڤ are covered");
    Check(arReport.Harakat, "harakat are covered");
    var la = OpenType.Parse(File.ReadAllBytes(latinFont))[0];
    var laReport = FontInventory.Analyze(new FontFileInfo { Path = latinFont }, la);
    Check(!la.IsCorrupt && laReport.Latin && laReport.LatinDigits, "Noto Sans: Latin letters and digits");
    Check(laReport.Arabic == ArabicSupport.None, "Noto Sans: no Arabic");
    Check(la.VersionNumber > 0, $"version read ({la.VersionText})");
    var ttc = Directory.Exists("/usr/share/fonts") ? Directory.EnumerateFiles("/usr/share/fonts", "*.ttc", SearchOption.AllDirectories).FirstOrDefault() : null;
    if (ttc is not null)
    {
        var faces = OpenType.Parse(File.ReadAllBytes(ttc));
        Check(faces.Count > 1 && faces.All(f => !f.IsCorrupt), $"a .ttc collection gives {faces.Count} faces");
    }

    var bytes = File.ReadAllBytes(latinFont);
    Check(OpenType.Parse(bytes[..(bytes.Length / 3)])[0].IsCorrupt, "a cut-short font is reported as damaged");
    var badHead = (byte[])bytes.Clone();
    var headOff = (int)la.Tables["head"].Offset;
    badHead[headOff + 12] ^= 0xFF;
    Check(OpenType.Parse(badHead)[0].Problems.Contains("font.p.badHeadMagic"), "a damaged header is found");
    var noise = new byte[4096];
    new Random(7).NextBytes(noise);
    Check(OpenType.Parse(noise)[0].Problems.Contains("font.p.badSignature"), "random bytes are not a font");
    Check(OpenType.Parse([])[0].IsCorrupt, "an empty file is reported");
    // Make the 'arab' script disappear from GSUB: the Arabic letters stay but cannot join.
    var noArab = File.ReadAllBytes(arabicFont);
    var gsub = (int)ar.Tables["GSUB"].Offset;
    var sl = gsub + OpenType.U16(noArab, gsub + 4);
    for (var i = 0; i < OpenType.U16(noArab, sl); i++)
    {
        var r = sl + 2 + i * 6;
        if (System.Text.Encoding.ASCII.GetString(noArab, r, 4) == "arab") { noArab[r] = (byte)'x'; }
    }
    var noArabFace = OpenType.Parse(noArab)[0];
    Check(FontInventory.Analyze(new FontFileInfo { Path = "x.ttf" }, noArabFace).Arabic is ArabicSupport.NoShaping or ArabicSupport.PresentationFormsOnly,
        "an Arabic font without joining rules is detected");

    // ------------------------------------------------------------------
    Section("Font Doctor — inventory, duplicates, broken entries");
    var sysFonts = Fake("Windows", "Fonts");
    var usrFonts = Fake("Users", "me", "AppData", "Local", "Microsoft", "Windows", "Fonts");
    Directory.CreateDirectory(sysFonts);
    Directory.CreateDirectory(usrFonts);
    File.Copy(arabicFont, Path.Combine(sysFonts, "NotoSansArabicUI-Regular.ttf"));
    File.Copy(latinFont, Path.Combine(sysFonts, "NotoSans-Regular.ttf"));
    File.Copy(latinFont, Path.Combine(usrFonts, "NotoSans-Regular-copy.ttf"));                 // duplicate
    File.WriteAllBytes(Path.Combine(sysFonts, "broken.ttf"), bytes[..5000]);                   // damaged
    File.WriteAllBytes(Path.Combine(sysFonts, "legacy.ttf"), noArab);                          // no joining
    File.WriteAllText(Path.Combine(sysFonts, "OldFont.pfm"), "type1");                         // Type 1
    reg.Set(Hive.LocalMachine, FontInventory.FontsKey, new RegValue("Noto Sans Arabic UI (TrueType)", "String", "NotoSansArabicUI-Regular.ttf"));
    reg.Set(Hive.LocalMachine, FontInventory.FontsKey, new RegValue("Noto Sans (TrueType)", "String", "NotoSans-Regular.ttf"));
    reg.Set(Hive.LocalMachine, FontInventory.FontsKey, new RegValue("Broken (TrueType)", "String", "broken.ttf"));
    reg.Set(Hive.CurrentUser, FontInventory.FontsKey, new RegValue("Noto Sans Copy (TrueType)", "String", Path.Combine(usrFonts, "NotoSans-Regular-copy.ttf")));
    reg.Set(Hive.CurrentUser, FontInventory.FontsKey, new RegValue("Gone Font (TrueType)", "String", Path.Combine(usrFonts, "gone.ttf")));
    var fonts = FontInventory.Scan(reg);
    Check(fonts.Files.Count == 6, $"6 font files found ({fonts.Files.Count})");
    Check(fonts.CorruptCount == 1 && fonts.Issues.Any(i => i.Code == "corrupt" && i.Path!.EndsWith("broken.ttf")), "the damaged font is found");
    Check(fonts.Issues.Any(i => i.Code == "dup" && i.Fix == FontFix.RemoveDuplicate), "the duplicate is found and can be removed");
    Check(fonts.BrokenRegistrations.Count == 1 && fonts.BrokenRegistrations[0].Name == "Gone Font (TrueType)", "the entry pointing to a missing file is found");
    Check(fonts.Issues.Any(i => i.Code == "type1"), "the Type 1 font is flagged");
    Check(fonts.Issues.Any(i => i.Code is "noShaping" or "presentationOnly" && i.Path!.EndsWith("legacy.ttf")), "the Arabic font that cannot join is flagged");
    Check(fonts.ArabicReady.Any(f => f.Family.StartsWith("Noto Sans Arabic")), "the good Arabic font is listed as ready");
    var dupIssue = fonts.Issues.First(i => i.Code == "dup" && i.Path!.Contains("copy"));
    var arabicDup = fonts.Issues.First(i => i.Code == "dup" && i.Path!.EndsWith("legacy.ttf"));
    Check(arabicDup.KeepPath!.EndsWith("NotoSansArabicUI-Regular.ttf"), "between two copies, the one whose Arabic joins is kept");
    Check(dupIssue.KeepPath is not null && dupIssue.KeepPath != dupIssue.Path, "a copy to keep is recommended");

    var (alts, toInstall) = GoogleFonts.SuggestAlternatives(fonts);
    Check(alts.Any(a => a.Family.StartsWith("Noto Sans Arabic")) && toInstall.Count > 20, "alternatives: installed Arabic fonts and fonts to install");

    var ps = PhotoshopDoctor.Diagnose(fonts, reg);
    Check(ps.Findings.Any(f => f.Code == "ps.corrupt") && ps.Findings.Any(f => f.Code == "ps.type1") && ps.Findings.Any(f => f.Code == "ps.arabic"), "Photoshop: damaged, Type 1 and Arabic problems reported");
    Check(ps.Advice.Count >= 5, "Photoshop: steps inside Photoshop are given");

    // ------------------------------------------------------------------
    Section("Font Doctor — repairs with backup and undo");
    var corrupt = fonts.Files.Where(f => f.IsCorrupt).ToList();
    Platform.AdminOverride = false;
    var refused = FontRepair.RemoveFonts(corrupt, "fix.corrupt", reg);
    Check(!refused.Ok && File.Exists(Path.Combine(sysFonts, "broken.ttf")), "without administrator rights a font for all users is not touched");
    Platform.AdminOverride = true;
    var rm = FontRepair.RemoveFonts(corrupt, "fix.corrupt", reg);
    Check(rm.Ok && !File.Exists(Path.Combine(sysFonts, "broken.ttf")), "the damaged font is removed");
    Check(reg.Get(Hive.LocalMachine, FontInventory.FontsKey, "Broken (TrueType)") is null, "its registry line is removed");
    var back = Journal.Undo(rm.Entry!, reg);
    Check(back.Ok && File.Exists(Path.Combine(sysFonts, "broken.ttf")) && reg.Get(Hive.LocalMachine, FontInventory.FontsKey, "Broken (TrueType)") is not null, "undo puts back the file and the registry line");

    var dupFile = fonts.Files.First(f => f.Path == dupIssue.Path);
    var rd = FontRepair.RemoveFonts([dupFile], "fix.dup", reg);
    Check(rd.Ok && !File.Exists(dupFile.Path) && reg.Get(Hive.CurrentUser, FontInventory.FontsKey, "Noto Sans Copy (TrueType)") is null, "the duplicate is removed with its registry line");

    var rr = FontRepair.RemoveRegistrations(fonts.BrokenRegistrations, reg);
    Check(rr.Ok && reg.Get(Hive.CurrentUser, FontInventory.FontsKey, "Gone Font (TrueType)") is null, "the broken registry line is removed");
    Journal.Undo(rr.Entry!, reg);
    Check(reg.Get(Hive.CurrentUser, FontInventory.FontsKey, "Gone Font (TrueType)") is not null, "undo restores the registry line");

    // Photoshop font cache
    var psDir = Fake("Program Files", "Adobe", "Adobe Photoshop 2025");
    Touch(Path.Combine(psDir, "Photoshop.exe"), 10, 1);
    var cache = Fake("Users", "me", "AppData", "Roaming", "Adobe", "Adobe Photoshop 2025", "CT Font Cache");
    Touch(Path.Combine(cache, "AdobeFnt_OSFonts.lst"), 4000, 1);
    Touch(Path.Combine(cache, "fntsub", "Fonts.dat"), 6000, 1);
    var installs = PhotoshopDoctor.FindInstalls(reg);
    Check(installs.Count == 1 && installs[0].Year == 2025 && installs[0].FontCacheSize == 10000 && installs[0].ExePath is not null, "Photoshop 2025 and its font cache are found");
    var pc = FontRepair.ClearPhotoshopCache(installs[0]);
    Check(pc.Ok && !File.Exists(Path.Combine(cache, "AdobeFnt_OSFonts.lst")), "Photoshop's font cache is cleared");
    Journal.Undo(pc.Entry!, reg);
    Check(File.Exists(Path.Combine(cache, "fntsub", "Fonts.dat")), "undo brings the cache back");

    // ------------------------------------------------------------------
    Section("Old Arabic fonts (FS_ style) → Unicode");
    var legacyPath = Path.Combine(dataDir, "LegacyArabicTest.ttf");
    var legacyBytes = File.ReadAllBytes(legacyPath);
    Check(LegacyArabic.Analyze(legacyBytes, out _) is null, "the old-style font is recognised as convertible");
    Check(LegacyArabic.Analyze(File.ReadAllBytes(arabicFont), out _) == "legacy.hasLayout", "a modern Arabic font is not touched");
    Check(LegacyArabic.Analyze(File.ReadAllBytes(latinFont), out _) is not null, "a Latin font is not touched");
    var before = OpenType.Parse(legacyBytes)[0];
    Check(before.IsSymbolEncoded && FontInventory.Analyze(new FontFileInfo { Path = legacyPath, LegacyConvertible = true }, before).Arabic == ArabicSupport.LegacySymbol, "before: symbol-encoded, flagged as old Arabic");
    var conv = LegacyArabic.Convert(legacyBytes);
    var after = OpenType.Parse(conv.Data)[0];
    Check(!after.IsCorrupt && after.Warnings.Count == 0, $"after: valid font, checksums right ({string.Join(",", after.Problems.Concat(after.Warnings))})");
    Check(after.HasUnicodeCmap && !after.IsSymbolEncoded, "after: Unicode cmap");
    Check(after.ArabicFeatures.IsSupersetOf(["init", "medi", "fina", "rlig"]), $"after: Arabic joining features ({string.Join(" ", after.ArabicFeatures)})");
    Check(after.GposFeatures.Contains("mark"), "after: harakat positioned (GPOS mark)");
    Check(after.Coverage.Contains(0x0627) && after.Coverage.Contains(0x0644) && after.Coverage.Contains(0x0661) && after.Coverage.Contains(0xF241), "after: ا ل ١ mapped, old private codes still mapped");
    Check(after.Coverage.Contains(0x060C) && after.Coverage.Contains(0x0661) && after.Coverage.Contains(0x0031), "after: punctuation and digits mapped by their standard position (، ١ 1)");
    Check(after.Family == before.Family, "after: same family name, so existing designs keep it");
    Check(after.VersionText.Contains(LegacyArabic.VersionTag), "after: version marked as converted");
    Check(conv.LamAlefs >= 2 && conv.Joined > 20, $"lam-alef ligatures and joining forms built ({conv.LamAlefs}, {conv.Joined})");
    Check(!LegacyArabic.CanConvert(conv.Data), "a converted font is not converted twice");
    var shapingCheck = Environment.GetEnvironmentVariable("IBRACARE_SHAPING_OUT");
    if (!string.IsNullOrEmpty(shapingCheck)) File.WriteAllBytes(shapingCheck, conv.Data);

    // Through the Font Doctor: scan, convert with backup, registry pointed at the new file, undo.
    var legacyInstalled = Path.Combine(usrFonts, "FS_Test.ttf");
    File.Copy(legacyPath, legacyInstalled, true);
    reg.Set(Hive.CurrentUser, FontInventory.FontsKey, new RegValue("Legacy Arabic Test (TrueType)", "String", legacyInstalled));
    var fonts2 = FontInventory.Scan(reg);
    var legacyIssue = fonts2.Issues.FirstOrDefault(i => i.Path == legacyInstalled && i.Fix == FontFix.ConvertLegacy);
    Check(legacyIssue is not null, "the scan offers 'Convert to Unicode' for it");
    Check(PhotoshopDoctor.Diagnose(fonts2, reg).Findings.Any(f => f.Code == "ps.legacy" && f.Fix == FontFix.ConvertLegacy), "the Photoshop check offers the conversion too");
    var legacyFile = fonts2.Files.First(f => f.Path == legacyInstalled);
    var cv = FontRepair.ConvertLegacy([legacyFile], reg);
    var converted = Path.Combine(usrFonts, "FS_Test-IBRA.ttf");
    Check(cv.Ok && File.Exists(converted) && !File.Exists(legacyInstalled), "converted file written, old one removed");
    Check(reg.Get(Hive.CurrentUser, FontInventory.FontsKey, "Legacy Arabic Test (TrueType)")?.Data == converted, "Windows now loads the converted file");
    var fonts3 = FontInventory.Scan(reg);
    Check(fonts3.Faces.Any(f => f.File.Path == converted && f.ArabicLetters >= 14 && f.Arabic != ArabicSupport.LegacySymbol && f.Face.ArabicFeatures.IsSupersetOf(["init", "medi", "fina", "rlig"])) && !fonts3.Issues.Any(i => i.Fix == FontFix.ConvertLegacy), "a new scan sees joined Arabic and nothing left to convert");
    var undoConv = Journal.Undo(cv.Entry!, reg);
    Check(undoConv.Ok && File.Exists(legacyInstalled) && !File.Exists(converted), "undo brings the old font back and removes the converted one");
    Check(reg.Get(Hive.CurrentUser, FontInventory.FontsKey, "Legacy Arabic Test (TrueType)")?.Data == legacyInstalled, "undo restores the registry line");
    File.Delete(legacyInstalled);
    reg.Delete(Hive.CurrentUser, FontInventory.FontsKey, "Legacy Arabic Test (TrueType)");

    // ------------------------------------------------------------------
    Section("Reports and backups");
    L.Language = "en";
    var html = Reports.Build(report, fonts, ps);
    Check(html.Contains("dir=\"ltr\"") && html.Contains("IBRA Deep Cleaner") && html.Contains("Photoshop"), "English report");
    L.Language = "ar";
    var reportFile = Reports.Write(report, fonts, ps);
    var text = File.ReadAllText(reportFile);
    Check(text.Contains("dir=\"rtl\"") && text.Contains("تقرير الصيانة") && text.Contains("victim") == false, "Arabic report written: " + reportFile);
    var sets = BackupStore.All();
    Check(sets.Count >= 5, $"backups listed ({sets.Count})");
    var last = sets.First(s => s.State != BackupState.Purged && s.Size > 0);
    BackupStore.Purge(last.Id);
    Check(BackupStore.Get(last.Id)!.State == BackupState.Purged, "a backup can be deleted for good");
    var expired = BackupStore.All().First(s => s.State != BackupState.Purged);
    expired.Expires = DateTime.Now.AddDays(-1);
    BackupStore.Save(expired);
    Check(BackupStore.PurgeExpired() >= 1, "expired backups are purged");
    Journal.Reload();
    Check(Journal.All.Count > 5, "the history is saved to disk and read back");

    // ------------------------------------------------------------------
    if (!offline)
    {
        Section("Google Fonts — trusted download, license check, install, update, undo");
        var cairo = GoogleFonts.Find("Cairo")!;
        var dl = await GoogleFonts.DownloadAsync(cairo);
        Check(dl.License == "OFL" && File.ReadAllText(dl.LicenseFile).Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase), "Cairo: OFL license verified");
        Check(dl.Files.Count == 1 && dl.Files[0].Face.IsVariable, "Cairo: one variable font file, valid");
        Check(FontInventory.Analyze(new FontFileInfo { Path = dl.Files[0].LocalPath }, dl.Files[0].Face).Arabic == ArabicSupport.Full, "Cairo: Arabic letters join");
        var inst = FontRepair.InstallForUser(dl, reg);
        var installed = Path.Combine(usrFonts, dl.Files[0].FileName);
        Check(inst.Ok && File.Exists(installed), "Cairo installed for the user");
        Check(reg.List(Hive.CurrentUser, FontInventory.FontsKey).Any(v => v.Data == installed), "Cairo registered for the user");
        var upd = FontRepair.InstallForUser(dl, reg);
        Check(upd.Lines.Any(l => l.StartsWith("↻")), "installing again counts as an update with a backup");
        Journal.Undo(inst.Entry!, reg);
        Check(!File.Exists(installed) || upd.Ok, "undo of the first install removes the font it added");
        Check(!reg.List(Hive.CurrentUser, FontInventory.FontsKey).Any(v => v.Data == installed), "undo removes the registry line");
        Check(GoogleFonts.LicenseTextMatches("OFL", "SIL Open Font License, Version 1.1") && !GoogleFonts.LicenseTextMatches("OFL", "All rights reserved"), "license text check");

        var catalogOk = 0;
        foreach (var c in GoogleFonts.Arabic)
        {
            try
            {
                var meta = GoogleFonts.ParseMetadata(await Http.Client.GetStringAsync(GoogleFonts.RawBase + c.Folder + "/METADATA.pb"));
                if (meta.License == "OFL" && meta.FileNames.Count > 0 && meta.Subsets.Contains("arabic")) catalogOk++;
                else Console.WriteLine($"       catalog: {c.Family} → {meta.License} {meta.FileNames.Count} {string.Join(",", meta.Subsets)}");
            }
            catch (Exception e) { Console.WriteLine($"       catalog: {c.Family} → {e.Message}"); }
        }
        Check(catalogOk == GoogleFonts.Arabic.Count, $"every catalog font exists with an open license and Arabic ({catalogOk}/{GoogleFonts.Arabic.Count})");
    }
}
catch (Exception e)
{
    failures++;
    Console.WriteLine($"  CRASH {e}");
}
finally
{
    try { Directory.Delete(work, true); } catch (Exception) { }
}

Console.WriteLine($"\n{passed} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static string Holes(string s) => string.Join(",", Regex.Matches(s, @"\{\d\}").Select(m => m.Value).OrderBy(x => x).Distinct());
