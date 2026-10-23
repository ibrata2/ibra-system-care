using System.Text;

namespace IbraCare.Core.Fonts;

public enum FontFormat { TrueType, OpenTypeCff, Collection, Type1, Bitmap, Woff, Unknown }

/// <summary>Reading one face of a font file: names, the characters it covers, its tables, its Arabic layout support.
/// Written from the OpenType specification (no external library), and strict about bounds so a damaged file
/// is reported, never crashes the scan.</summary>
public sealed class FontFace
{
    public int Index { get; init; }
    public uint SfntVersion { get; set; }
    public Dictionary<string, (uint Offset, uint Length, uint Checksum)> Tables { get; } = new();

    public string Family { get; set; } = "";
    public string Subfamily { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PostScriptName { get; set; } = "";
    public string VersionText { get; set; } = "";
    public string TypographicFamily { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string LicenseText { get; set; } = "";
    public string LicenseUrl { get; set; } = "";
    public bool HasWindowsNames { get; set; }

    public int UnitsPerEm { get; set; }
    public int GlyphCount { get; set; }
    public int Weight { get; set; } = 400;
    public ushort FsType { get; set; }
    public string Vendor { get; set; } = "";
    public bool IsVariable => Tables.ContainsKey("fvar");
    public bool IsCff => Tables.ContainsKey("CFF ") || Tables.ContainsKey("CFF2");

    /// <summary>Unicode cmap present (format 4 or 12); false for symbol-encoded fonts.</summary>
    public bool HasUnicodeCmap { get; set; }
    public bool IsSymbolEncoded { get; set; }
    public HashSet<int> Coverage { get; } = [];

    public HashSet<string> GsubScripts { get; } = [];
    public HashSet<string> GsubFeatures { get; } = [];
    /// <summary>Features listed under the Arabic script ('arab') of GSUB.</summary>
    public HashSet<string> ArabicFeatures { get; } = [];
    public HashSet<string> GposFeatures { get; } = [];

    public List<string> Problems { get; } = [];  // keys of L10n texts: fatal
    public List<string> Warnings { get; } = [];  // keys of L10n texts

    public bool IsCorrupt => Problems.Count > 0;

    public double VersionNumber
    {
        get
        {
            var m = System.Text.RegularExpressions.Regex.Match(VersionText, @"(\d+)\.(\d+)");
            return m.Success && double.TryParse(m.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}

public static class OpenType
{
    public static readonly string[] Required = ["cmap", "head", "hhea", "hmtx", "maxp", "name"];

    public static FontFormat Detect(string path, byte[]? head = null)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".pfb" or ".pfm" or ".afm") return FontFormat.Type1;
        if (ext is ".fon" or ".fnt") return FontFormat.Bitmap;
        if (head is null)
        {
            try
            {
                using var f = File.OpenRead(path);
                head = new byte[4];
                if (f.Read(head, 0, 4) < 4) return FontFormat.Unknown;
            }
            catch (Exception) { return FontFormat.Unknown; }
        }
        if (head.Length < 4) return FontFormat.Unknown;
        var tag = Encoding.ASCII.GetString(head, 0, 4);
        return tag switch
        {
            "ttcf" => FontFormat.Collection,
            "OTTO" => FontFormat.OpenTypeCff,
            "wOFF" or "wOF2" => FontFormat.Woff,
            "true" => FontFormat.TrueType,
            _ when head[0] == 0 && head[1] == 1 && head[2] == 0 && head[3] == 0 => FontFormat.TrueType,
            _ => FontFormat.Unknown,
        };
    }

    /// <summary>Reads every face in the data (one, or several for a .ttc). Damage becomes a problem on the face, never an exception.</summary>
    public static List<FontFace> Parse(byte[] data)
    {
        var faces = new List<FontFace>();
        if (data.Length < 12)
        {
            var f = new FontFace();
            f.Problems.Add("font.p.tooSmall");
            faces.Add(f);
            return faces;
        }
        if (Encoding.ASCII.GetString(data, 0, 4) == "ttcf")
        {
            var count = U32(data, 8);
            if (count == 0 || count > 256 || 12 + count * 4 > data.Length)
            {
                var f = new FontFace();
                f.Problems.Add("font.p.badCollection");
                faces.Add(f);
                return faces;
            }
            for (var i = 0; i < count; i++) faces.Add(ParseFace(data, (int)U32(data, 12 + i * 4), i));
            return faces;
        }
        faces.Add(ParseFace(data, 0, 0));
        return faces;
    }

    public static FontFace ParseFace(byte[] d, int start, int index)
    {
        var face = new FontFace { Index = index };
        try
        {
            if (start < 0 || start + 12 > d.Length) { face.Problems.Add("font.p.truncated"); return face; }
            face.SfntVersion = U32(d, start);
            if (face.SfntVersion is not (0x00010000 or 0x4F54544F or 0x74727565)) { face.Problems.Add("font.p.badSignature"); return face; }
            int numTables = U16(d, start + 4);
            if (numTables is 0 or > 300 || start + 12 + numTables * 16 > d.Length) { face.Problems.Add("font.p.badDirectory"); return face; }
            var badChecksums = 0;
            for (var i = 0; i < numTables; i++)
            {
                var r = start + 12 + i * 16;
                var tag = Encoding.ASCII.GetString(d, r, 4);
                var sum = U32(d, r + 4);
                var off = U32(d, r + 8);
                var len = U32(d, r + 12);
                if ((ulong)off + len > (ulong)d.Length)
                {
                    face.Problems.Add("font.p.tableOutside");
                    face.Tables[tag] = (off, 0, sum);
                    continue;
                }
                face.Tables[tag] = (off, len, sum);
                if (tag != "head" && len > 0 && Checksum(d, (int)off, (int)len) != sum) badChecksums++;
            }
            if (face.Problems.Count > 0) return face;
            foreach (var t in Required)
                if (!face.Tables.ContainsKey(t)) face.Problems.Add("font.p.missing." + t);
            if (!face.Tables.ContainsKey("glyf") && !face.IsCff && !face.Tables.ContainsKey("EBDT") && !face.Tables.ContainsKey("CBDT") && !face.Tables.ContainsKey("sbix"))
                face.Problems.Add("font.p.noOutlines");
            if (!face.Tables.ContainsKey("post")) face.Warnings.Add("font.w.noPost");
            if (!face.Tables.ContainsKey("OS/2")) face.Warnings.Add("font.w.noOs2");
            if (badChecksums > 0) face.Warnings.Add("font.w.checksum");

            ReadHead(d, face);
            ReadMaxp(d, face);
            ReadName(d, face);
            ReadOs2(d, face);
            ReadCmap(d, face);
            ReadLayout(d, face, "GSUB");
            ReadLayout(d, face, "GPOS");
            if (face.GlyphCount == 0 && !face.Problems.Contains("font.p.missing.maxp")) face.Problems.Add("font.p.noGlyphs");
            if (!face.HasWindowsNames && face.Tables.ContainsKey("name")) face.Warnings.Add("font.w.noWindowsNames");
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            face.Problems.Add("font.p.damaged");
        }
        return face;
    }

    // ---------- tables ----------

    private static bool Table(byte[] d, FontFace f, string tag, int minLength, out int off, out int len)
    {
        off = len = 0;
        if (!f.Tables.TryGetValue(tag, out var t)) return false;
        off = (int)t.Offset;
        len = (int)t.Length;
        if (len < minLength) { f.Problems.Add("font.p.short." + tag.Trim().Replace("/", "")); return false; }
        return true;
    }

    private static void ReadHead(byte[] d, FontFace f)
    {
        if (!Table(d, f, "head", 54, out var o, out _)) return;
        if (U32(d, o + 12) != 0x5F0F3CF5) f.Problems.Add("font.p.badHeadMagic");
        f.UnitsPerEm = U16(d, o + 18);
        if (f.UnitsPerEm is < 16 or > 16384) f.Problems.Add("font.p.badUnitsPerEm");
    }

    private static void ReadMaxp(byte[] d, FontFace f)
    {
        if (!Table(d, f, "maxp", 6, out var o, out _)) return;
        f.GlyphCount = U16(d, o + 4);
    }

    private static void ReadOs2(byte[] d, FontFace f)
    {
        if (!f.Tables.ContainsKey("OS/2")) return;
        if (!Table(d, f, "OS/2", 68, out var o, out _)) return;
        f.Weight = U16(d, o + 4);
        f.FsType = (ushort)U16(d, o + 8);
        f.Vendor = Encoding.ASCII.GetString(d, o + 58, 4).Trim('\0', ' ');
    }

    private static void ReadName(byte[] d, FontFace f)
    {
        if (!Table(d, f, "name", 6, out var o, out var len)) return;
        int count = U16(d, o + 2);
        var strings = o + U16(d, o + 4);
        if (6 + count * 12 > len) { f.Problems.Add("font.p.badNames"); return; }
        // best[nameId] = (score, text): Windows English first, then any Windows, then Unicode, then Mac.
        var best = new Dictionary<int, (int Score, string Text)>();
        for (var i = 0; i < count; i++)
        {
            var r = o + 6 + i * 12;
            int platform = U16(d, r), encoding = U16(d, r + 2), language = U16(d, r + 4), nameId = U16(d, r + 6);
            int length = U16(d, r + 8), offset = U16(d, r + 10);
            var at = strings + offset;
            if (at < 0 || at + length > d.Length || at + length > o + len + 65536) continue;
            string text;
            int score;
            if (platform == 3 && encoding is 0 or 1 or 10)
            {
                f.HasWindowsNames = true;
                text = Encoding.BigEndianUnicode.GetString(d, at, length);
                score = language == 0x0409 ? 4 : 3;
            }
            else if (platform == 0)
            {
                text = Encoding.BigEndianUnicode.GetString(d, at, length);
                score = 2;
            }
            else if (platform == 1 && encoding == 0)
            {
                text = Encoding.Latin1.GetString(d, at, length);
                score = 1;
            }
            else continue;
            text = text.Replace("\0", "").Trim();
            if (text.Length == 0) continue;
            if (!best.TryGetValue(nameId, out var cur) || cur.Score < score) best[nameId] = (score, text);
        }
        string N(int id) => best.TryGetValue(id, out var v) ? v.Text : "";
        f.Copyright = N(0);
        f.Family = N(1);
        f.Subfamily = N(2);
        f.FullName = N(4);
        f.VersionText = N(5);
        f.PostScriptName = N(6);
        f.LicenseText = N(13);
        f.LicenseUrl = N(14);
        f.TypographicFamily = N(16);
        if (f.Family.Length == 0 && f.FullName.Length == 0) f.Problems.Add("font.p.noFamilyName");
        if (f.FullName.Length == 0) f.FullName = (f.Family + " " + f.Subfamily).Trim();
    }

    private static void ReadCmap(byte[] d, FontFace f)
    {
        if (!Table(d, f, "cmap", 4, out var o, out var len)) return;
        int count = U16(d, o + 2);
        if (4 + count * 8 > len) { f.Problems.Add("font.p.badCmap"); return; }
        int? best = null;
        var bestScore = -1;
        for (var i = 0; i < count; i++)
        {
            var r = o + 4 + i * 8;
            int platform = U16(d, r), encoding = U16(d, r + 2);
            var sub = o + (int)U32(d, r + 4);
            if (sub < o || sub + 2 > o + len) continue;
            var format = U16(d, sub);
            var score = (platform, encoding, format) switch
            {
                (3, 10, 12) => 6,
                (0, 4 or 6, 12) => 5,
                (3, 1, 4) => 4,
                (0, _, 4) => 3,
                (3, 0, 4) => 1,
                _ => -1,
            };
            if (score > bestScore) { bestScore = score; best = sub; }
        }
        if (best is null) { f.Warnings.Add("font.w.noUnicodeCmap"); return; }
        f.HasUnicodeCmap = bestScore >= 3;
        var s = best.Value;
        var fmt = U16(d, s);
        if (fmt == 4) ReadFormat4(d, s, f);
        else if (fmt == 12) ReadFormat12(d, s, f);
        if (!f.HasUnicodeCmap && bestScore == 1)
        {
            f.IsSymbolEncoded = true;
            f.Warnings.Add("font.w.symbolEncoded");
        }
        if (f.Coverage.Count == 0) f.Problems.Add("font.p.emptyCmap");
    }

    private static void ReadFormat4(byte[] d, int s, FontFace f)
    {
        int segX2 = U16(d, s + 6);
        var ends = s + 14;
        var starts = ends + segX2 + 2;
        var deltas = starts + segX2;
        var rangeOffsets = deltas + segX2;
        if (rangeOffsets + segX2 > d.Length) { f.Problems.Add("font.p.badCmap"); return; }
        for (var i = 0; i < segX2 / 2; i++)
        {
            int end = U16(d, ends + i * 2), start = U16(d, starts + i * 2);
            var delta = (short)U16(d, deltas + i * 2);
            int ro = U16(d, rangeOffsets + i * 2);
            if (start > end || start == 0xFFFF) continue;
            for (var c = start; c <= end; c++)
            {
                int gid;
                if (ro == 0) gid = (c + delta) & 0xFFFF;
                else
                {
                    var at = rangeOffsets + i * 2 + ro + (c - start) * 2;
                    if (at + 2 > d.Length) break;
                    gid = U16(d, at);
                    if (gid != 0) gid = (gid + delta) & 0xFFFF;
                }
                if (gid != 0 && (f.GlyphCount == 0 || gid < f.GlyphCount)) f.Coverage.Add(c);
            }
        }
    }

    private static void ReadFormat12(byte[] d, int s, FontFace f)
    {
        var groups = U32(d, s + 12);
        if (groups > 200_000 || s + 16 + groups * 12 > d.Length) { f.Problems.Add("font.p.badCmap"); return; }
        for (var i = 0; i < groups; i++)
        {
            var g = s + 16 + i * 12;
            uint start = U32(d, g), end = U32(d, g + 4), gid = U32(d, g + 8);
            if (end < start || end > 0x10FFFF || end - start > 0x30000) continue;
            for (var c = start; c <= end; c++, gid++)
                if (gid != 0 && (f.GlyphCount == 0 || gid < f.GlyphCount)) f.Coverage.Add((int)c);
        }
    }

    /// <summary>Scripts and features of GSUB/GPOS; for GSUB also which features the Arabic script uses.</summary>
    private static void ReadLayout(byte[] d, FontFace f, string tag)
    {
        if (!f.Tables.TryGetValue(tag, out var t) || t.Length < 10) return;
        var o = (int)t.Offset;
        var end = o + (int)t.Length;
        var scriptList = o + U16(d, o + 4);
        var featureList = o + U16(d, o + 6);
        if (scriptList + 2 > end || featureList + 2 > end) { f.Warnings.Add("font.w.badLayout"); return; }
        int featureCount = U16(d, featureList);
        var featureTags = new string[featureCount];
        for (var i = 0; i < featureCount; i++)
        {
            var r = featureList + 2 + i * 6;
            if (r + 6 > end) break;
            featureTags[i] = Encoding.ASCII.GetString(d, r, 4);
            (tag == "GSUB" ? f.GsubFeatures : f.GposFeatures).Add(featureTags[i]);
        }
        if (tag != "GSUB") return;
        int scriptCount = U16(d, scriptList);
        for (var i = 0; i < scriptCount; i++)
        {
            var r = scriptList + 2 + i * 6;
            if (r + 6 > end) break;
            var scriptTag = Encoding.ASCII.GetString(d, r, 4);
            f.GsubScripts.Add(scriptTag);
            if (scriptTag != "arab") continue;
            var script = scriptList + U16(d, r + 4);
            if (script + 4 > end) continue;
            var langSystems = new List<int>();
            int def = U16(d, script);
            if (def != 0) langSystems.Add(script + def);
            int langCount = U16(d, script + 2);
            for (var k = 0; k < langCount; k++)
            {
                var lr = script + 4 + k * 6;
                if (lr + 6 > end) break;
                langSystems.Add(script + U16(d, lr + 4));
            }
            foreach (var ls in langSystems)
            {
                if (ls + 6 > end) continue;
                int idxCount = U16(d, ls + 4);
                for (var k = 0; k < idxCount; k++)
                {
                    var at = ls + 6 + k * 2;
                    if (at + 2 > end) break;
                    int fi = U16(d, at);
                    if (fi < featureTags.Length && featureTags[fi] is { } ft) f.ArabicFeatures.Add(ft);
                }
            }
        }
    }

    // ---------- bytes ----------

    public static int U16(byte[] d, int i) => d[i] << 8 | d[i + 1];
    public static uint U32(byte[] d, int i) => (uint)(d[i] << 24 | d[i + 1] << 16 | d[i + 2] << 8 | d[i + 3]);

    private static uint Checksum(byte[] d, int off, int len)
    {
        uint sum = 0;
        var end = off + len;
        for (var i = off; i < end; i += 4)
        {
            uint v = 0;
            for (var k = 0; k < 4; k++) v = v << 8 | (i + k < end ? d[i + k] : 0u);
            sum = unchecked(sum + v);
        }
        return sum;
    }
}
