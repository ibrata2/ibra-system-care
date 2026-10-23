using System.Text.RegularExpressions;

namespace IbraCare.Core.Fonts;

public enum FontStyleKind { Sans, Naskh, Kufi, Display, Calligraphy }

/// <summary>A free Arabic font from the official Google Fonts repository (github.com/google/fonts).</summary>
public sealed record CatalogFont(string Family, string Folder, FontStyleKind Kind, string NoteAr, string NoteEn)
{
    public string Note => L.Pick(NoteAr, NoteEn);
    public string PageUrl => "https://fonts.google.com/specimen/" + Family.Replace(' ', '+');
}

public sealed class DownloadedFont
{
    public required string Family { get; init; }
    public required string License { get; init; }
    public List<(string FileName, string LocalPath, FontFace Face)> Files { get; } = [];
    public required string LicenseFile { get; init; }
    public required string SourceUrl { get; init; }
}

/// <summary>Trusted source for installing and updating fonts: Google Fonts' official GitHub repository, over HTTPS.
/// A font is accepted only when its METADATA declares an open license (OFL, Apache, UFL), the license file is present
/// and really is that license, and every file reads as a valid font with no embedding restrictions.</summary>
public static class GoogleFonts
{
    public const string RawBase = "https://raw.githubusercontent.com/google/fonts/main/";

    public static readonly IReadOnlyList<CatalogFont> Arabic =
    [
        new("Noto Naskh Arabic", "ofl/notonaskharabic", FontStyleKind.Naskh, "نسخ واضح للنصوص الطويلة، من Google", "Clear Naskh for body text, by Google"),
        new("Noto Sans Arabic", "ofl/notosansarabic", FontStyleKind.Sans, "خط حديث بدون زوائد، أوزان كثيرة", "Modern sans, many weights"),
        new("Noto Kufi Arabic", "ofl/notokufiarabic", FontStyleKind.Kufi, "كوفي هندسي للعناوين", "Geometric Kufi for titles"),
        new("Cairo", "ofl/cairo", FontStyleKind.Sans, "مشهور للتصاميم والسوشيال", "Popular for design and social media"),
        new("Tajawal", "ofl/tajawal", FontStyleKind.Sans, "ناعم وواضح، 7 أوزان", "Soft and clear, 7 weights"),
        new("Almarai", "ofl/almarai", FontStyleKind.Sans, "بسيط وعصري", "Simple and modern"),
        new("IBM Plex Sans Arabic", "ofl/ibmplexsansarabic", FontStyleKind.Sans, "احترافي للواجهات", "Professional, for interfaces"),
        new("Readex Pro", "ofl/readexpro", FontStyleKind.Sans, "مقروء جداً", "Very readable"),
        new("Vazirmatn", "ofl/vazirmatn", FontStyleKind.Sans, "يدعم گ چ پ ژ بشكل كامل", "Full support for گ چ پ ژ"),
        new("Alexandria", "ofl/alexandria", FontStyleKind.Sans, "عصري للعناوين والنصوص", "Modern for titles and text"),
        new("Rubik", "ofl/rubik", FontStyleKind.Sans, "حواف دائرية لطيفة", "Friendly rounded corners"),
        new("Zain", "ofl/zain", FontStyleKind.Sans, "خفيف وأنيق", "Light and elegant"),
        new("Changa", "ofl/changa", FontStyleKind.Kufi, "كوفي عريض قوي", "Bold wide Kufi"),
        new("Reem Kufi", "ofl/reemkufi", FontStyleKind.Kufi, "كوفي فاطمي حديث", "Modern Fatimid Kufi"),
        new("Kufam", "ofl/kufam", FontStyleKind.Kufi, "كوفي للشعارات", "Kufi for logos"),
        new("El Messiri", "ofl/elmessiri", FontStyleKind.Display, "أنيق للعناوين", "Elegant for headlines"),
        new("Lemonada", "ofl/lemonada", FontStyleKind.Display, "مرح ودائري", "Playful and round"),
        new("Lalezar", "ofl/lalezar", FontStyleKind.Display, "عريض جداً للبوسترات", "Extra bold for posters"),
        new("Rakkas", "ofl/rakkas", FontStyleKind.Display, "عرض قوي للعناوين", "Strong display for titles"),
        new("Marhey", "ofl/marhey", FontStyleKind.Display, "مرح للتصاميم", "Fun for designs"),
        new("Baloo Bhaijaan 2", "ofl/baloobhaijaan2", FontStyleKind.Display, "سميك ومرح", "Heavy and playful"),
        new("Amiri", "ofl/amiri", FontStyleKind.Naskh, "نسخ كلاسيكي للكتب", "Classic book Naskh"),
        new("Scheherazade New", "ofl/scheherazadenew", FontStyleKind.Naskh, "نسخ تقليدي مع تشكيل كامل", "Traditional Naskh, full diacritics"),
        new("Markazi Text", "ofl/markazitext", FontStyleKind.Naskh, "نسخ للعناوين الصحفية", "Editorial Naskh"),
        new("Harmattan", "ofl/harmattan", FontStyleKind.Naskh, "نسخ مبسط", "Simplified Naskh"),
        new("Lateef", "ofl/lateef", FontStyleKind.Naskh, "نسخ للقراءة", "Naskh for reading"),
        new("Mada", "ofl/mada", FontStyleKind.Sans, "واضح على الشاشة", "Clear on screen"),
        new("Aref Ruqaa", "ofl/arefruqaa", FontStyleKind.Calligraphy, "خط رقعة", "Ruqaa calligraphy"),
        new("Mirza", "ofl/mirza", FontStyleKind.Calligraphy, "نستعليق مبسط", "Simplified Nastaliq"),
        new("Katibeh", "ofl/katibeh", FontStyleKind.Calligraphy, "خط يدوي للعناوين", "Hand-drawn titles"),
        new("Jomhuria", "ofl/jomhuria", FontStyleKind.Display, "عريض للعناوين الكبيرة", "Heavy for big titles"),
        new("Blaka Ink", "ofl/blakaink", FontStyleKind.Calligraphy, "خط بالحبر", "Ink calligraphy"),
    ];

    public static CatalogFont? Find(string family) =>
        Arabic.FirstOrDefault(c => string.Equals(c.Family, family, StringComparison.OrdinalIgnoreCase));

    public sealed record Metadata(string Name, string License, List<string> FileNames, List<string> Subsets);

    public static Metadata ParseMetadata(string text)
    {
        string One(string key) => Regex.Match(text, $@"^{key}:\s*""([^""]*)""", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : "";
        var files = Regex.Matches(text, @"^\s*filename:\s*""([^""]+)""", RegexOptions.Multiline).Select(m => m.Groups[1].Value).Distinct().ToList();
        var subsets = Regex.Matches(text, @"^subsets:\s*""([^""]+)""", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        return new Metadata(One("name"), One("license"), files, subsets);
    }

    private static string LicenseFileFor(string license) => license.ToUpperInvariant() switch
    {
        "OFL" => "OFL.txt",
        "APACHE2" => "LICENSE.txt",
        "UFL" => "UFL.txt",
        _ => "",
    };

    public static bool LicenseTextMatches(string license, string text) => license.ToUpperInvariant() switch
    {
        "OFL" => text.Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase),
        "APACHE2" => text.Contains("Apache License", StringComparison.OrdinalIgnoreCase),
        "UFL" => text.Contains("UBUNTU FONT LICENCE", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>Downloads a family into the app's download folder and verifies it. Nothing is installed here.</summary>
    public static async Task<DownloadedFont> DownloadAsync(CatalogFont font, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var baseUrl = RawBase + font.Folder + "/";
        progress?.Report(L.T("gf.readingMeta"));
        var meta = ParseMetadata(await GetString(baseUrl + "METADATA.pb", ct));
        if (meta.FileNames.Count == 0) throw new InvalidDataException(L.T("gf.noFiles"));
        var licenseFile = LicenseFileFor(meta.License);
        if (licenseFile.Length == 0) throw new InvalidDataException(L.T("gf.badLicense", meta.License));

        progress?.Report(L.T("gf.checkingLicense"));
        var licenseText = await GetString(baseUrl + licenseFile, ct);
        if (!LicenseTextMatches(meta.License, licenseText)) throw new InvalidDataException(L.T("gf.licenseMismatch"));

        var dir = Paths.Ensure(Path.Combine(Paths.Downloads, Safe(font.Family)));
        var licensePath = Path.Combine(dir, licenseFile);
        await File.WriteAllTextAsync(licensePath, licenseText, ct);
        var result = new DownloadedFont { Family = font.Family, License = meta.License, LicenseFile = licensePath, SourceUrl = baseUrl };

        foreach (var name in meta.FileNames)
        {
            if (name.Contains('/') || name.Contains('\\') || name.Contains("..")) throw new InvalidDataException(L.T("gf.badName"));
            progress?.Report(L.T("gf.downloading", name));
            var bytes = await Http.Client.GetByteArrayAsync(baseUrl + Uri.EscapeDataString(name), ct);
            var faces = OpenType.Parse(bytes);
            if (faces.Count != 1 || faces[0].IsCorrupt) throw new InvalidDataException(L.T("gf.invalidFont", name));
            var face = faces[0];
            if ((face.FsType & 0x0002) != 0) throw new InvalidDataException(L.T("gf.restricted", name));
            var path = Path.Combine(dir, name);
            await File.WriteAllBytesAsync(path, bytes, ct);
            result.Files.Add((name, path, face));
        }
        return result;
    }

    private static async Task<string> GetString(string url, CancellationToken ct)
    {
        using var resp = await Http.Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException(L.T("gf.http", (int)resp.StatusCode), null, resp.StatusCode);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static string Safe(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Alternatives for a font with an Arabic problem: installed fonts that join correctly, then catalog fonts not yet installed.</summary>
    public static (List<FaceReport> Installed, List<CatalogFont> ToInstall) SuggestAlternatives(FontScan scan, FontStyleKind? kind = null)
    {
        var installed = scan.ArabicReady
            .GroupBy(f => f.Family, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderBy(f => Math.Abs(f.Face.Weight - 400)).First())
            .OrderByDescending(f => f.ArabicExtended).ThenByDescending(f => f.Harakat).ThenBy(f => f.Family)
            .Take(12).ToList();
        var have = new HashSet<string>(scan.Faces.Select(f => f.Family), StringComparer.OrdinalIgnoreCase);
        var toInstall = Arabic.Where(c => !have.Contains(c.Family) && (kind is null || c.Kind == kind)).ToList();
        return (installed, toInstall);
    }
}
