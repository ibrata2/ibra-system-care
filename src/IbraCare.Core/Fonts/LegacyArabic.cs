using System.Text;

namespace IbraCare.Core.Fonts;

/// <summary>
/// Converts old symbol-encoded Arabic fonts (the "FS_" family and others made the same way in the 1990s) into Unicode
/// OpenType fonts whose letters join in Photoshop, Word and every modern program.
///
/// These fonts keep every letter shape as a separate glyph with a descriptive name (alef, fnalef, inbaa, midbaa, fnbaa,
/// lamelef…) behind a symbol "cmap", so typing real Arabic finds nothing. The converter keeps every outline as it is and adds:
///  • a Unicode cmap (U+0621…U+064A, digits, punctuation, U+FDF2) — the old private codes U+F2xx stay mapped too,
///  • GSUB: init / medi / fina forms from the names, lam-alef ligatures (rlig), shadda combinations (ccmp),
///  • GDEF classes and GPOS mark positioning, so harakat sit centred over or under their letter,
///  • Windows names and OS/2 code pages that say "Unicode, Arabic" instead of "symbol".
/// </summary>
public static class LegacyArabic
{
    // Unicode letter → isolated glyph name. Forms are found as "in" / "mid" / "fn" + name.
    private static readonly (int Code, string Iso, string? FormBase)[] Letters =
    [
        (0x0621, "hamzah", null), (0x0622, "maddahonalef", null), (0x0623, "hamzahonalef", null), (0x0624, "hamzahonwaw", null),
        (0x0625, "hamzahunderalef", null), (0x0626, "hamzahonyah", "hamzahonya"), (0x0627, "alef", null), (0x0628, "baa", null),
        (0x0629, "taamarbutah", null), (0x062A, "taa", null), (0x062B, "thaa", null), (0x062C, "jeem", null), (0x062D, "haa", null),
        (0x062E, "khaa", null), (0x062F, "dal", null), (0x0630, "thal", null), (0x0631, "ra", null), (0x0632, "zain", null),
        (0x0633, "seen", null), (0x0634, "sheen", null), (0x0635, "sad", null), (0x0636, "dad", null), (0x0637, "tah", null),
        (0x0638, "dhah", null), (0x0639, "ain", null), (0x063A, "ghain", null), (0x0640, "tatweelwide", null), (0x0641, "fa", null),
        (0x0642, "qaf", null), (0x0643, "caf", null), (0x0644, "lam", null), (0x0645, "meem", null), (0x0646, "noon", null),
        (0x0647, "ha", null), (0x0648, "waw", null), (0x0649, "alefmaqsurah", null), (0x064A, "ya", null),
        (0x06A9, "caf", null), // Persian keheh drawn with kaf
    ];

    private static readonly (int Code, string Name, bool Below)[] Marks =
    [
        (0x064B, "fathatan", false), (0x064C, "dammatah", false), (0x064D, "kasratan", true), (0x064E, "fathah", false),
        (0x064F, "dammah", false), (0x0650, "kasrah", true), (0x0651, "shaddah", false), (0x0652, "sukun", false),
    ];

    private static readonly (int Code, string[] Names)[] Others =
    [
        (0x0020, ["space"]), (0x00A0, ["space"]), (0x0021, ["exclam"]), (0x0022, ["quotedbl"]), (0x0025, ["percentarabic"]),
        (0x066A, ["percentarabic"]), (0x0027, ["quotesingle"]), (0x0028, ["parenleft.1", "parenleft"]), (0x0029, ["parenright"]),
        (0x002A, ["asterisk"]), (0x002B, ["plus"]), (0x002C, ["commaarabic"]), (0x060C, ["commaarabic"]), (0x002D, ["minus"]),
        (0x2212, ["minus"]), (0x2013, ["minus"]), (0x2014, ["minus"]), (0x002E, ["period"]), (0x002F, ["slash"]), (0x003A, ["colon"]), (0x003B, ["semicolonarabic"]),
        (0x061B, ["semicolonarabic"]), (0x003D, ["equal"]), (0x003F, ["questionarabic"]), (0x061F, ["questionarabic"]),
        (0x005B, ["bracketleft"]), (0x005D, ["bracketright"]), (0x00AB, ["guillemotleft"]), (0x00BB, ["guillemotright"]),
        (0x00D7, ["multiply"]), (0x00F7, ["divide"]), (0x201C, ["quotedblleft"]), (0x201D, ["quotedblright"]), (0xFDF2, ["Allah"]),
    ];

    private static readonly string[] Digits = ["zeroarabic", "onearabic", "twoarabic", "threearabic", "fourarabic", "fivearabic", "sixarabic", "sevenarabic", "eightarabic", "ninearabic"];

    // lam + alef variant → (isolated ligature, final ligature)
    private static readonly (string Alef, string Iso, string Fina)[] LamAlef =
    [
        ("alef", "lamelef", "fnlamalef"), ("hamzahonalef", "hamzahonlamelef", "fnhamzahonlamelef"),
        ("maddahonalef", "maddahonlamelef", "fnmaddahonlamelef"), ("hamzahunderalef", "hamzahunderlamelef", "fnhamzahunderlamelef"),
    ];

    // two marks typed in either order → one combined glyph
    private static readonly (string A, string B, string Lig)[] ShaddaCombos =
    [
        ("shaddah", "fathah", "fahtanonshaddah"), ("shaddah", "dammah", "dammahonshaddah"),
        ("shaddah", "fathatan", "fathatanonshaddah"), ("shaddah", "dammatah", "dammatanonshaddah"),
    ];

    // slot in the old layout (0xF2xx) → Unicode characters it stands for
    private static readonly (int Slot, int[] Codes)[] PositionalPunctuation =
    [
        (0x21, [0x0021]), (0x22, [0x0022]), (0x23, [0x00AB]), (0x24, [0x00BB]), (0x25, [0x066A, 0x0025]), (0x26, [0x00D7]),
        (0x27, [0x00F7]), (0x28, [0x0028]), (0x29, [0x0029]), (0x2A, [0x002A]), (0x2B, [0x002B]), (0x2C, [0x060C]),
        (0x2D, [0x002D, 0x2212, 0x2013, 0x2014]), (0x2E, [0x002E]), (0x2F, [0x002F]), (0x3A, [0x003A]), (0x3B, [0x061B, 0x003B]),
        (0x3C, [0x201C]), (0x3D, [0x003D]), (0x3E, [0x201D]), (0x3F, [0x061F, 0x003F]), (0x5B, [0x005B]), (0x5D, [0x005D]),
        (0x5E, [0x002C]), (0x01, [0xFDF2]),
        (0x30, [0x0660, 0x06F0, 0x0030]), (0x31, [0x0661, 0x06F1, 0x0031]), (0x32, [0x0662, 0x06F2, 0x0032]), (0x33, [0x0663, 0x06F3, 0x0033]),
        (0x34, [0x0664, 0x06F4, 0x0034]), (0x35, [0x0665, 0x06F5, 0x0035]), (0x36, [0x0666, 0x06F6, 0x0036]), (0x37, [0x0667, 0x06F7, 0x0037]),
        (0x38, [0x0668, 0x06F8, 0x0038]), (0x39, [0x0669, 0x06F9, 0x0039]),
    ];

    private static bool IsStandardLayout(Dictionary<int, int> symbol, string[] names)
    {
        string? At(int slot) => symbol.TryGetValue(0xF200 + slot, out var g) && g < names.Length ? names[g] : null;
        return At(0x41) == "alef" && At(0x49) == "inbaa" && At(0x70) == "seen" && At(0xBE) == "lam" && At(0xC2) == "meem" && At(0xDC) == "lamelef";
    }

    public const string VersionTag = "IBRA Unicode";

    // ------------------------------------------------------------------ detection

    /// <summary>True when the font is symbol-encoded and its glyph names follow the old Arabic naming (alef, inbaa, lamelef…).</summary>
    public static bool CanConvert(byte[] data) => Analyze(data, out _) is null;

    /// <summary>Null when convertible; otherwise the reason (a text key).</summary>
    public static string? Analyze(byte[] data, out Sfnt? font)
    {
        font = null;
        try
        {
            font = Sfnt.Read(data);
            if (!font.Has("cmap") || !font.Has("post") || !font.Has("glyf") || !font.Has("loca") || !font.Has("hmtx")) return "legacy.notTrueType";
            if (font.Has("GSUB")) return "legacy.hasLayout";
            var names = PostNames(font);
            if (names is null) return "legacy.noNames";
            var set = names.ToHashSet();
            var needed = new[] { "alef", "fnalef", "inbaa", "midbaa", "fnbaa", "baa", "lam", "inlam", "meem", "lamelef" };
            if (!needed.All(set.Contains)) return "legacy.notArabicNames";
            if (!SymbolCmap(font)) return "legacy.notSymbol";
            return null;
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidDataException)
        {
            return "legacy.damaged";
        }
    }

    private static bool SymbolCmap(Sfnt f)
    {
        var d = f.Table("cmap");
        int n = OpenType.U16(d, 2);
        var symbol = false;
        for (var i = 0; i < n; i++)
        {
            int p = OpenType.U16(d, 4 + i * 8), e = OpenType.U16(d, 6 + i * 8);
            if (p == 3 && e is 1 or 10) return false; // already Unicode
            if (p == 3 && e == 0) symbol = true;
        }
        return symbol;
    }

    /// <summary>Glyph names from a 'post' table version 2.0, or null.</summary>
    public static string[]? PostNames(Sfnt f)
    {
        var d = f.Table("post");
        if (d.Length < 34 || OpenType.U32(d, 0) != 0x00020000) return null;
        int n = OpenType.U16(d, 32);
        var idx = new int[n];
        for (var i = 0; i < n; i++) idx[i] = OpenType.U16(d, 34 + i * 2);
        var custom = new List<string>();
        var p = 34 + n * 2;
        while (p < d.Length)
        {
            int len = d[p];
            if (p + 1 + len > d.Length) break;
            custom.Add(Encoding.ASCII.GetString(d, p + 1, len));
            p += 1 + len;
        }
        var names = new string[n];
        for (var i = 0; i < n; i++)
            names[i] = idx[i] < 258 ? MacGlyphNames.Standard[idx[i]] : idx[i] - 258 < custom.Count ? custom[idx[i] - 258] : $"glyph{i}";
        return names;
    }

    // ------------------------------------------------------------------ conversion

    public sealed class Result
    {
        public required byte[] Data { get; init; }
        public int Letters { get; init; }
        public int Joined { get; init; }
        public int LamAlefs { get; init; }
        public int MarksPositioned { get; init; }
        public string Family { get; init; } = "";
    }

    public static Result Convert(byte[] data)
    {
        var why = Analyze(data, out var f);
        if (why is not null || f is null) throw new InvalidDataException(L.T(why ?? "legacy.damaged"));
        var names = PostNames(f)!;
        var gid = new Dictionary<string, int>();
        for (var i = 0; i < names.Length; i++) gid.TryAdd(names[i], i);
        int G(string n) => gid.TryGetValue(n, out var g) ? g : -1;

        // ---- cmap: Unicode → glyph
        var map = new SortedDictionary<int, int>();
        void Map(int code, int g) { if (g > 0) map[code] = g; }
        var oldSymbol = SymbolMap(f);
        foreach (var (code, g) in oldSymbol) if (code is >= 0xF000 and <= 0xF8FF) Map(code, g); // old private codes keep working
        foreach (var (code, iso, _) in Letters) Map(code, G(iso));
        foreach (var (code, name, _) in Marks) Map(code, G(name));
        foreach (var (code, list) in Others) foreach (var n in list) if (G(n) > 0) { Map(code, G(n)); break; }
        for (var i = 0; i < 10; i++)
        {
            Map(0x0660 + i, G(Digits[i]));
            Map(0x06F0 + i, G(Digits[i]));
            Map(0x0030 + i, G(Digits[i]));
        }
        // Fonts in this family share one layout (alef at 0x41, lam at 0xBE…). Some (e.g. Al-Hadith) carry wrong names on their
        // punctuation glyphs, so when the layout is the standard one, punctuation and digits are mapped by position instead.
        if (IsStandardLayout(oldSymbol, names))
        {
            foreach (var (slot, codesForSlot) in PositionalPunctuation)
                if (oldSymbol.TryGetValue(0xF200 + slot, out var pg) && pg > 0 && !IsEmptyGlyph(f, pg))
                    foreach (var c in codesForSlot) map[c] = pg;
        }
        var lettersMapped = Letters.Count(l => G(l.Iso) > 0);

        // ---- GSUB single substitutions
        var init = new SortedDictionary<int, int>();
        var medi = new SortedDictionary<int, int>();
        var fina = new SortedDictionary<int, int>();
        foreach (var (code, iso, formBase) in Letters)
        {
            var g = G(iso);
            if (g <= 0) continue;
            var b = formBase ?? iso;
            if (G("in" + b) > 0) init[g] = G("in" + b);
            if (G("mid" + b) > 0) medi[g] = G("mid" + b);
            if (G("fn" + b) > 0) fina[g] = G("fn" + b);
        }
        // tatweel joins on both sides and has one shape
        // ---- rlig: lam (initial or medial form) + final alef → lam-alef ligature
        var rlig = new List<(int First, int[] Tail, int Lig)>();
        foreach (var (alef, iso, fin) in LamAlef)
        {
            int fnAlef = G("fn" + alef), inLam = G("inlam"), midLam = G("midlam");
            if (fnAlef <= 0) continue;
            if (inLam > 0 && G(iso) > 0) rlig.Add((inLam, [fnAlef], G(iso)));
            if (midLam > 0 && G(fin) > 0) rlig.Add((midLam, [fnAlef], G(fin)));
        }
        var ccmp = new List<(int First, int[] Tail, int Lig)>();
        foreach (var (a, b, lig) in ShaddaCombos)
        {
            if (G(a) <= 0 || G(b) <= 0 || G(lig) <= 0 || IsEmptyGlyph(f, G(lig))) continue;
            ccmp.Add((G(a), [G(b)], G(lig)));
            ccmp.Add((G(b), [G(a)], G(lig)));
        }

        // ---- GDEF classes and GPOS anchors
        var bboxes = Boxes(f, names.Length);
        var markGlyphs = new SortedDictionary<int, bool>(); // glyph → below?
        foreach (var (_, name, below) in Marks) if (G(name) > 0) markGlyphs[G(name)] = below;
        foreach (var (_, _, lig) in ShaddaCombos) if (G(lig) > 0) markGlyphs[G(lig)] = false;
        foreach (var n in names.Where(n => n.EndsWith('2') && Marks.Any(m => n == m.Name + "2"))) markGlyphs[G(n)] = Marks.First(m => n == m.Name + "2").Below;
        var ligGlyphs = LamAlef.SelectMany(l => new[] { G(l.Iso), G(l.Fina) }).Where(g => g > 0).ToHashSet();
        var baseGlyphs = new SortedSet<int>();
        foreach (var g in map.Values.Concat(init.Values).Concat(medi.Values).Concat(fina.Values).Concat(ligGlyphs))
            if (!markGlyphs.ContainsKey(g) && g > 0 && bboxes[g] is not null) baseGlyphs.Add(g);
        var positionedMarks = markGlyphs.Keys.Where(g => bboxes[g] is not null).ToList();

        // ---- build the tables
        var cmap = BuildCmap(map);
        var gsub = BuildGsub(init, medi, fina, rlig, ccmp);
        var gdef = BuildGdef(baseGlyphs, ligGlyphs, markGlyphs.Keys);
        var gpos = positionedMarks.Count > 0 ? BuildGpos(positionedMarks, markGlyphs, baseGlyphs, bboxes) : null;
        f.Set("cmap", cmap);
        f.Set("GSUB", gsub);
        f.Set("GDEF", gdef);
        if (gpos is not null) f.Set("GPOS", gpos);
        f.Set("hmtx", ZeroMarkAdvances(f, markGlyphs.Keys));
        f.Set("OS/2", FixOs2(f, map.Keys.First(), map.Keys.Last()));
        var family = FixNames(f);
        // An 'hdmx' or 'LTSH' holding device advances for the old marks would disagree with the new hmtx: drop them (optional tables).
        f.Remove("hdmx");
        f.Remove("LTSH");
        f.Remove("VDMX");
        return new Result
        {
            Data = f.Write(), Letters = lettersMapped, Joined = init.Count + medi.Count + fina.Count, LamAlefs = rlig.Count,
            MarksPositioned = gpos is null ? 0 : positionedMarks.Count, Family = family,
        };
    }

    private static Dictionary<int, int> SymbolMap(Sfnt f)
    {
        var d = f.Table("cmap");
        int n = OpenType.U16(d, 2);
        var result = new Dictionary<int, int>();
        for (var i = 0; i < n; i++)
        {
            int p = OpenType.U16(d, 4 + i * 8), e = OpenType.U16(d, 6 + i * 8);
            var off = (int)OpenType.U32(d, 8 + i * 8);
            if (p != 3 || e != 0 || OpenType.U16(d, off) != 4) continue;
            int segX2 = OpenType.U16(d, off + 6);
            int ends = off + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ros = deltas + segX2;
            for (var s = 0; s < segX2 / 2; s++)
            {
                int end = OpenType.U16(d, ends + s * 2), start = OpenType.U16(d, starts + s * 2);
                var delta = (short)OpenType.U16(d, deltas + s * 2);
                int ro = OpenType.U16(d, ros + s * 2);
                if (start == 0xFFFF) continue;
                for (var c = start; c <= end; c++)
                {
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        var at = ros + s * 2 + ro + (c - start) * 2;
                        if (at + 2 > d.Length) break;
                        g = OpenType.U16(d, at);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) result[c] = g;
                }
            }
        }
        return result;
    }

    private sealed record Box(int XMin, int YMin, int XMax, int YMax);

    private static Box?[] Boxes(Sfnt f, int count)
    {
        var head = f.Table("head");
        var longLoca = OpenType.U16(head, 50) == 1;
        var loca = f.Table("loca");
        var glyf = f.Table("glyf");
        var boxes = new Box?[count];
        for (var i = 0; i < count; i++)
        {
            int a, b;
            if (longLoca) { a = (int)OpenType.U32(loca, i * 4); b = (int)OpenType.U32(loca, i * 4 + 4); }
            else { a = OpenType.U16(loca, i * 2) * 2; b = OpenType.U16(loca, i * 2 + 2) * 2; }
            if (b - a < 10 || a + 10 > glyf.Length) continue;
            boxes[i] = new Box((short)OpenType.U16(glyf, a + 2), (short)OpenType.U16(glyf, a + 4), (short)OpenType.U16(glyf, a + 6), (short)OpenType.U16(glyf, a + 8));
        }
        return boxes;
    }

    private static bool IsEmptyGlyph(Sfnt f, int g) => Boxes(f, g + 1)[g] is null;

    // ------------------------------------------------------------------ table writers

    private static byte[] BuildCmap(SortedDictionary<int, int> map)
    {
        // format 4: contiguous runs of codes, each run with its own glyph id array (idRangeOffset), then the 0xFFFF end segment.
        var codes = map.Keys.Where(c => c <= 0xFFFF).ToList();
        var runs = new List<(int Start, int End)>();
        foreach (var c in codes)
        {
            if (runs.Count > 0 && runs[^1].End == c - 1) runs[^1] = (runs[^1].Start, c);
            else runs.Add((c, c));
        }
        runs.Add((0xFFFF, 0xFFFF));
        var segs = runs.Count;
        var glyphIds = new List<int>();
        var rangeOffsets = new int[segs];
        for (var i = 0; i < segs - 1; i++)
        {
            // offset from this idRangeOffset entry to the first glyph id for the segment
            rangeOffsets[i] = (segs - i + glyphIds.Count) * 2;
            for (var c = runs[i].Start; c <= runs[i].End; c++) glyphIds.Add(map[c]);
        }
        var sub = new Writer();
        var length = 16 + segs * 8 + glyphIds.Count * 2;
        var searchRange = 2 * (1 << (int)Math.Floor(Math.Log2(segs)));
        sub.U16(4); sub.U16(length); sub.U16(0);
        sub.U16(segs * 2); sub.U16(searchRange); sub.U16((int)Math.Log2(searchRange / 2)); sub.U16(segs * 2 - searchRange);
        foreach (var r in runs) sub.U16(r.End);
        sub.U16(0);
        foreach (var r in runs) sub.U16(r.Start);
        for (var i = 0; i < segs; i++) sub.U16(i == segs - 1 ? 1 : 0); // idDelta (0xFFFF+1 = 0 → .notdef)
        for (var i = 0; i < segs; i++) sub.U16(i == segs - 1 ? 0 : rangeOffsets[i]);
        foreach (var g in glyphIds) sub.U16(g);
        var w = new Writer();
        w.U16(0); w.U16(2);
        w.U16(0); w.U16(3); w.U32(4 + 2 * 8);
        w.U16(3); w.U16(1); w.U32(4 + 2 * 8);
        w.Bytes(sub.ToArray());
        return w.ToArray();
    }

    private static byte[] Coverage(IEnumerable<int> glyphs)
    {
        var w = new Writer();
        var list = glyphs.Distinct().OrderBy(g => g).ToList();
        w.U16(1); w.U16(list.Count);
        foreach (var g in list) w.U16(g);
        return w.ToArray();
    }

    private static byte[] SingleSubst(SortedDictionary<int, int> map)
    {
        // format 2: coverage + substitutes in coverage order
        var w = new Writer();
        w.U16(2);
        w.U16(6 + map.Count * 2);
        w.U16(map.Count);
        foreach (var v in map.Values) w.U16(v);
        w.Bytes(Coverage(map.Keys));
        return w.ToArray();
    }

    private static byte[] LigatureSubst(List<(int First, int[] Tail, int Lig)> ligs)
    {
        var byFirst = ligs.GroupBy(l => l.First).OrderBy(g => g.Key).ToList();
        var w = new Writer();
        var headerLen = 6 + byFirst.Count * 2;
        var setBlobs = new List<byte[]>();
        foreach (var grp in byFirst)
        {
            var set = new Writer();
            var items = grp.ToList();
            set.U16(items.Count);
            var off = 2 + items.Count * 2;
            var ligBlobs = new List<byte[]>();
            foreach (var l in items)
            {
                var lw = new Writer();
                lw.U16(l.Lig); lw.U16(l.Tail.Length + 1);
                foreach (var r in l.Tail) lw.U16(r);
                ligBlobs.Add(lw.ToArray());
            }
            foreach (var b in ligBlobs) { set.U16(off); off += b.Length; }
            foreach (var b in ligBlobs) set.Bytes(b);
            setBlobs.Add(set.ToArray());
        }
        var coverage = Coverage(byFirst.Select(g => g.Key));
        w.U16(1);
        w.U16(headerLen);
        w.U16(byFirst.Count);
        var pos = headerLen + coverage.Length;
        foreach (var b in setBlobs) { w.U16(pos); pos += b.Length; }
        w.Bytes(coverage);
        foreach (var b in setBlobs) w.Bytes(b);
        return w.ToArray();
    }

    /// <summary>ScriptList (DFLT and arab, every feature in the default language) + FeatureList + LookupList.</summary>
    private static byte[] LayoutTable(List<(string Tag, int Lookup)> features, List<(int Type, int Flag, byte[] Sub)> lookups)
    {
        // ScriptList
        var langSys = new Writer();
        langSys.U16(0); langSys.U16(0xFFFF); langSys.U16(features.Count);
        for (var i = 0; i < features.Count; i++) langSys.U16(i);
        var ls = langSys.ToArray();
        var script = new Writer();
        script.U16(4); script.U16(0); script.Bytes(ls);
        var sc = script.ToArray();
        var scriptList = new Writer();
        scriptList.U16(2);
        scriptList.Tag("DFLT"); scriptList.U16(2 + 12);
        scriptList.Tag("arab"); scriptList.U16(2 + 12 + sc.Length);
        scriptList.Bytes(sc); scriptList.Bytes(sc);
        // FeatureList (features must be sorted by tag)
        var featureList = new Writer();
        featureList.U16(features.Count);
        var fOff = 2 + features.Count * 6;
        foreach (var f in features) { featureList.Tag(f.Tag); featureList.U16(fOff); fOff += 6; }
        foreach (var f in features) { featureList.U16(0); featureList.U16(1); featureList.U16(f.Lookup); }
        // LookupList
        var lookupList = new Writer();
        lookupList.U16(lookups.Count);
        var lOff = 2 + lookups.Count * 2;
        var blobs = new List<byte[]>();
        foreach (var l in lookups)
        {
            var lw = new Writer();
            lw.U16(l.Type); lw.U16(l.Flag); lw.U16(1); lw.U16(8); lw.Bytes(l.Sub);
            blobs.Add(lw.ToArray());
        }
        foreach (var b in blobs) { lookupList.U16(lOff); lOff += b.Length; }
        foreach (var b in blobs) lookupList.Bytes(b);

        var s = scriptList.ToArray();
        var fl = featureList.ToArray();
        var ll = lookupList.ToArray();
        var w = new Writer();
        w.U32(0x00010000);
        w.U16(10); w.U16(10 + s.Length); w.U16(10 + s.Length + fl.Length);
        w.Bytes(s); w.Bytes(fl); w.Bytes(ll);
        return w.ToArray();
    }

    private static byte[] BuildGsub(SortedDictionary<int, int> init, SortedDictionary<int, int> medi, SortedDictionary<int, int> fina,
        List<(int, int[], int)> rlig, List<(int, int[], int)> ccmp)
    {
        const int IgnoreMarks = 0x0008;
        var lookups = new List<(int, int, byte[])>();
        var features = new List<(string Tag, int Lookup)>();
        void Add(string tag, int type, int flag, byte[] sub) { features.Add((tag, lookups.Count)); lookups.Add((type, flag, sub)); }
        if (ccmp.Count > 0) Add("ccmp", 4, 0, LigatureSubst(ccmp));
        Add("fina", 1, IgnoreMarks, SingleSubst(fina));
        Add("init", 1, IgnoreMarks, SingleSubst(init));
        Add("medi", 1, IgnoreMarks, SingleSubst(medi));
        if (rlig.Count > 0) Add("rlig", 4, IgnoreMarks, LigatureSubst(rlig));
        return LayoutTable(features.OrderBy(f => f.Tag, StringComparer.Ordinal).ToList(), lookups);
    }

    private static byte[] BuildGdef(IEnumerable<int> bases, IEnumerable<int> ligatures, IEnumerable<int> marks)
    {
        var cls = new SortedDictionary<int, int>();
        foreach (var g in bases) cls[g] = 1;
        foreach (var g in ligatures) cls[g] = 2;
        foreach (var g in marks) cls[g] = 3;
        // ClassDef format 2 with one range per glyph run of the same class
        var ranges = new List<(int Start, int End, int Class)>();
        foreach (var (g, c) in cls)
        {
            if (ranges.Count > 0 && ranges[^1].End == g - 1 && ranges[^1].Class == c) ranges[^1] = (ranges[^1].Start, g, c);
            else ranges.Add((g, g, c));
        }
        var cd = new Writer();
        cd.U16(2); cd.U16(ranges.Count);
        foreach (var r in ranges) { cd.U16(r.Start); cd.U16(r.End); cd.U16(r.Class); }
        var w = new Writer();
        w.U32(0x00010000);
        w.U16(12); w.U16(0); w.U16(0); w.U16(0);
        w.Bytes(cd.ToArray());
        return w.ToArray();
    }

    private static byte[] BuildGpos(List<int> marks, SortedDictionary<int, bool> below, SortedSet<int> bases, Box?[] boxes)
    {
        const int Gap = 60;
        var markCov = marks.OrderBy(g => g).ToList();
        var baseCov = bases.ToList();
        // MarkArray
        var markArray = new Writer();
        markArray.U16(markCov.Count);
        var anchorsStart = 2 + markCov.Count * 4;
        var anchorBlob = new Writer();
        foreach (var g in markCov)
        {
            var b = boxes[g]!;
            var isBelow = below[g];
            markArray.U16(isBelow ? 1 : 0);
            markArray.U16(anchorsStart + anchorBlob.Length);
            anchorBlob.U16(1); anchorBlob.S16((b.XMin + b.XMax) / 2); anchorBlob.S16(isBelow ? b.YMax + Gap : b.YMin - Gap);
        }
        markArray.Bytes(anchorBlob.ToArray());
        // BaseArray: two anchors (above, below) per base
        var baseArray = new Writer();
        baseArray.U16(baseCov.Count);
        var bStart = 2 + baseCov.Count * 4;
        var bAnchors = new Writer();
        foreach (var g in baseCov)
        {
            var b = boxes[g]!;
            var cx = (b.XMin + b.XMax) / 2;
            baseArray.U16(bStart + bAnchors.Length);
            bAnchors.U16(1); bAnchors.S16(cx); bAnchors.S16(b.YMax);
            baseArray.U16(bStart + bAnchors.Length);
            bAnchors.U16(1); bAnchors.S16(cx); bAnchors.S16(b.YMin);
        }
        baseArray.Bytes(bAnchors.ToArray());

        var mc = Coverage(markCov);
        var bc = Coverage(baseCov);
        var ma = markArray.ToArray();
        var ba = baseArray.ToArray();
        var sub = new Writer();
        sub.U16(1);
        sub.U16(12); sub.U16(12 + mc.Length);
        sub.U16(2);
        sub.U16(12 + mc.Length + bc.Length); sub.U16(12 + mc.Length + bc.Length + ma.Length);
        sub.Bytes(mc); sub.Bytes(bc); sub.Bytes(ma); sub.Bytes(ba);
        return LayoutTable([("mark", 0)], [(4, 0, sub.ToArray())]);
    }

    private static byte[] ZeroMarkAdvances(Sfnt f, IEnumerable<int> marks)
    {
        var hmtx = (byte[])f.Table("hmtx").Clone();
        int longMetrics = OpenType.U16(f.Table("hhea"), 34);
        foreach (var g in marks)
            if (g < longMetrics && g * 4 + 2 <= hmtx.Length) { hmtx[g * 4] = 0; hmtx[g * 4 + 1] = 0; }
        return hmtx;
    }

    /// <summary>OS/2 version 1 or more with Arabic code page and Unicode range, first/last characters from the new cmap.</summary>
    private static byte[] FixOs2(Sfnt f, int first, int last)
    {
        var old = f.Has("OS/2") ? f.Table("OS/2") : new byte[78];
        var hhea = f.Table("hhea");
        var t = new byte[Math.Max(86, old.Length)];
        Array.Copy(old, t, Math.Min(old.Length, t.Length));
        int version = OpenType.U16(t, 0);
        if (old.Length < 78)
        {
            // Apple's short version 0: fill the typographic metrics from 'hhea'
            Put16(t, 68, OpenType.U16(hhea, 4)); Put16(t, 70, OpenType.U16(hhea, 6)); Put16(t, 72, OpenType.U16(hhea, 8));
            Put16(t, 74, OpenType.U16(hhea, 4)); Put16(t, 76, (ushort)-(short)OpenType.U16(hhea, 6));
        }
        if (version < 1) Put16(t, 0, 1);
        // ulUnicodeRange1 bit 13 = Arabic, bit 0 = Basic Latin (digits and punctuation are mapped there)
        var r1 = OpenType.U32(t, 42) | (1u << 13) | 1u;
        Put32(t, 42, r1);
        // ulCodePageRange1 bit 6 = 1256 Arabic; bit 31 (symbol) off
        var cp = (OpenType.U32(t, 78) | (1u << 6)) & ~(1u << 31);
        Put32(t, 78, cp);
        Put16(t, 64, Math.Min(first, 0xFFFF));
        Put16(t, 66, Math.Min(last, 0xFFFF));
        return t;
    }

    /// <summary>Windows names re-encoded as Unicode (symbol encoding 0 → 1), version marked; returns the family name.</summary>
    private static string FixNames(Sfnt f)
    {
        var d = f.Table("name");
        int count = OpenType.U16(d, 2), storage = OpenType.U16(d, 4);
        var records = new List<(int P, int E, int L, int Id, byte[] Data)>();
        for (var i = 0; i < count; i++)
        {
            var r = 6 + i * 12;
            int p = OpenType.U16(d, r), e = OpenType.U16(d, r + 2), l = OpenType.U16(d, r + 4), id = OpenType.U16(d, r + 6);
            int len = OpenType.U16(d, r + 8), off = OpenType.U16(d, r + 10);
            if (storage + off + len > d.Length) continue;
            var bytes = d.AsSpan(storage + off, len).ToArray();
            if (p == 3 && e == 0) e = 1;
            if (id == 5 && p == 3)
            {
                var text = Encoding.BigEndianUnicode.GetString(bytes);
                if (!text.Contains(VersionTag)) bytes = Encoding.BigEndianUnicode.GetBytes(text + " - " + VersionTag);
            }
            if (id == 5 && p == 1)
            {
                var text = Encoding.Latin1.GetString(bytes);
                if (!text.Contains(VersionTag)) bytes = Encoding.Latin1.GetBytes(text + " - " + VersionTag);
            }
            records.Add((p, e, l, id, bytes));
        }
        records = records.OrderBy(r => r.P).ThenBy(r => r.E).ThenBy(r => r.L).ThenBy(r => r.Id).ToList();
        var w = new Writer();
        w.U16(0); w.U16(records.Count); w.U16(6 + records.Count * 12);
        var data = new Writer();
        foreach (var r in records)
        {
            w.U16(r.P); w.U16(r.E); w.U16(r.L); w.U16(r.Id); w.U16(r.Data.Length); w.U16(data.Length);
            data.Bytes(r.Data);
        }
        w.Bytes(data.ToArray());
        f.Set("name", w.ToArray());
        var fam = records.FirstOrDefault(r => r.P == 3 && r.Id == 1);
        return fam.Data is null ? "" : Encoding.BigEndianUnicode.GetString(fam.Data);
    }

    private static void Put16(byte[] d, int at, int v) { d[at] = (byte)(v >> 8); d[at + 1] = (byte)v; }
    private static void Put32(byte[] d, int at, uint v) { d[at] = (byte)(v >> 24); d[at + 1] = (byte)(v >> 16); d[at + 2] = (byte)(v >> 8); d[at + 3] = (byte)v; }
}

/// <summary>A TrueType file as a set of tables that can be replaced and written back with correct checksums.</summary>
public sealed class Sfnt
{
    private readonly SortedDictionary<string, byte[]> _tables = new(StringComparer.Ordinal);
    public uint Version { get; private set; }

    public static Sfnt Read(byte[] d)
    {
        if (d.Length < 12) throw new InvalidDataException();
        var f = new Sfnt { Version = OpenType.U32(d, 0) };
        if (f.Version is not (0x00010000 or 0x74727565)) throw new InvalidDataException();
        int n = OpenType.U16(d, 4);
        for (var i = 0; i < n; i++)
        {
            var r = 12 + i * 16;
            var tag = Encoding.ASCII.GetString(d, r, 4);
            var off = (int)OpenType.U32(d, r + 8);
            var len = (int)OpenType.U32(d, r + 12);
            if (off < 0 || len < 0 || off + len > d.Length) throw new InvalidDataException();
            f._tables[tag] = d.AsSpan(off, len).ToArray();
        }
        return f;
    }

    public bool Has(string tag) => _tables.ContainsKey(tag);
    public byte[] Table(string tag) => _tables.TryGetValue(tag, out var t) ? t : throw new InvalidDataException($"missing {tag}");
    public void Set(string tag, byte[] data) => _tables[tag] = data;
    public void Remove(string tag) => _tables.Remove(tag);

    public byte[] Write()
    {
        // head.checkSumAdjustment is computed over the whole file with the field at 0, then set.
        var head = (byte[])Table("head").Clone();
        head[8] = head[9] = head[10] = head[11] = 0;
        // modified date: seconds since 1904
        var modified = (long)(DateTime.UtcNow - new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        for (var i = 0; i < 8; i++) head[28 + i] = (byte)(modified >> (56 - 8 * i));
        _tables["head"] = head;

        var n = _tables.Count;
        var entrySelector = (int)Math.Floor(Math.Log2(n));
        var searchRange = (1 << entrySelector) * 16;
        var w = new Writer();
        w.U32(0x00010000);
        w.U16(n); w.U16(searchRange); w.U16(entrySelector); w.U16(n * 16 - searchRange);
        var offset = 12 + n * 16;
        var headOffset = 0;
        foreach (var (tag, data) in _tables)
        {
            if (tag == "head") headOffset = offset;
            w.Tag(tag); w.U32(Checksum(data)); w.U32((uint)offset); w.U32((uint)data.Length);
            offset += (data.Length + 3) & ~3;
        }
        foreach (var data in _tables.Values)
        {
            w.Bytes(data);
            w.Pad4();
        }
        var file = w.ToArray();
        var adjust = 0xB1B0AFBA - Checksum(file);
        file[headOffset + 8] = (byte)(adjust >> 24); file[headOffset + 9] = (byte)(adjust >> 16);
        file[headOffset + 10] = (byte)(adjust >> 8); file[headOffset + 11] = (byte)adjust;
        return file;
    }

    public static uint Checksum(byte[] d)
    {
        uint sum = 0;
        for (var i = 0; i < d.Length; i += 4)
        {
            uint v = 0;
            for (var k = 0; k < 4; k++) v = v << 8 | (i + k < d.Length ? d[i + k] : 0u);
            sum = unchecked(sum + v);
        }
        return sum;
    }
}

internal sealed class Writer
{
    private readonly MemoryStream _s = new();
    public int Length => (int)_s.Length;
    public void U16(int v) { _s.WriteByte((byte)(v >> 8)); _s.WriteByte((byte)v); }
    public void S16(int v) => U16((ushort)(short)v);
    public void U32(uint v) { U16((int)(v >> 16)); U16((int)(v & 0xFFFF)); }
    public void Tag(string t) => _s.Write(Encoding.ASCII.GetBytes(t.PadRight(4)[..4]));
    public void Bytes(byte[] b) => _s.Write(b);
    public void Pad4() { while (_s.Length % 4 != 0) _s.WriteByte(0); }
    public byte[] ToArray() => _s.ToArray();
}
