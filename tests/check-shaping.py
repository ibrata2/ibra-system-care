# Independent check of the old-Arabic-font converter with HarfBuzz (the shaping engine used by Chrome, Android, LibreOffice…):
# the converted test font must join Arabic letters and build lam-alef. Usage: python3 tests/check-shaping.py <converted.ttf>
import sys
import uharfbuzz as hb
from fontTools.ttLib import TTFont

path = sys.argv[1]
t = TTFont(path)
for tag in t.keys():
    t[tag]  # every table must decompile
order = t.getGlyphOrder()
font = hb.Font(hb.Face(hb.Blob.from_file_path(path)))
expected = {
    "سلام": ["inseen", "fnlamalef", "meem"],
    "بسم": ["inbaa", "midseen", "fnmeem"],
    "محمد": ["inmeem", "midhaa", "midmeem", "fndal"],
    "لا إله": ["lamelef", "space", "hamzahunderalef", "inlam", "fnha"],
}
failed = 0
for text, want in expected.items():
    buf = hb.Buffer()
    buf.add_str(text)
    buf.guess_segment_properties()
    hb.shape(font, buf, {})
    got = [order[i.codepoint] for i in reversed(buf.glyph_infos)]
    ok = got == want
    failed += not ok
    print(("  ok   " if ok else "  FAIL ") + f"{text}: {' '.join(got)}")
sys.exit(1 if failed else 0)
