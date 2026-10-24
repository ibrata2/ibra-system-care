# Builds tests/data/LegacyArabicTest.ttf: a font made the old way (symbol cmap at U+F2xx, one glyph per letter shape named
# alef / inbaa / midbaa / fnbaa / lamelef…, no GSUB/GPOS/GDEF), like the 1990s "FS_" Arabic fonts. The shapes are taken
# from Noto Sans Arabic UI (SIL Open Font License), so the fixture can live in the repository.
# Usage: python3 tools/make-legacy-fixture.py   (needs fontTools)
import os
from fontTools.ttLib import TTFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.recordingPen import DecomposingRecordingPen

here = os.path.dirname(os.path.abspath(__file__))
src = TTFont(os.path.join(here, "..", "tests", "data", "NotoSansArabicUI-Regular.ttf"))
cmap = src.getBestCmap()
gs = src.getGlyphSet()

# (old code, glyph name, presentation-form code point drawn from Noto)
L = [
    (0x20, "space", 0x20), (0x2C, "commaarabic", 0x60C), (0x30, "zeroarabic", 0x660), (0x31, "onearabic", 0x661), (0x32, "twoarabic", 0x662),
    (0x41, "alef", 0xFE8D), (0x42, "fnalef", 0xFE8E), (0x43, "hamzahonalef", 0xFE83), (0x44, "fnhamzahonalef", 0xFE84),
    (0x47, "hamzahunderalef", 0xFE87), (0x48, "fnhamzahunderalef", 0xFE88),
    (0x49, "inbaa", 0xFE91), (0x4A, "midbaa", 0xFE92), (0x4B, "fnbaa", 0xFE90), (0x4C, "baa", 0xFE8F),
    (0x55, "injeem", 0xFE9F), (0x56, "midjeem", 0xFEA0), (0x57, "fnjeem", 0xFE9E), (0x58, "jeem", 0xFE9D),
    (0x59, "inhaa", 0xFEA3), (0x5A, "midhaa", 0xFEA4), (0x5C, "fnhaa", 0xFEA2), (0x60, "haa", 0xFEA1),
    (0x65, "dal", 0xFEA9), (0x66, "fndal", 0xFEAA), (0x69, "ra", 0xFEAD), (0x6A, "fnra", 0xFEAE),
    (0x6D, "inseen", 0xFEB3), (0x6E, "midseen", 0xFEB4), (0x6F, "fnseen", 0xFEB2), (0x70, "seen", 0xFEB1),
    (0xBB, "inlam", 0xFEDF), (0xBC, "midlam", 0xFEE0), (0xBD, "fnlam", 0xFEDE), (0xBE, "lam", 0xFEDD),
    (0xBF, "inmeem", 0xFEE3), (0xC0, "midmeem", 0xFEE4), (0xC1, "fnmeem", 0xFEE2), (0xC2, "meem", 0xFEE1),
    (0xC7, "inha", 0xFEEB), (0xC8, "midha", 0xFEEC), (0xC9, "fnha", 0xFEEA), (0xCA, "ha", 0xFEE9),
    (0xCB, "waw", 0xFEED), (0xCC, "fnwaw", 0xFEEE),
    (0xCD, "inya", 0xFEF3), (0xCE, "midya", 0xFEF4), (0xCF, "fnya", 0xFEF2), (0xD0, "ya", 0xFEF1),
    (0xDC, "lamelef", 0xFEFB), (0xDD, "fnlamalef", 0xFEFC), (0xE2, "hamzahunderlamelef", 0xFEF9), (0xE3, "fnhamzahunderlamelef", 0xFEFA),
    (0xF2, "fathah", 0x64E), (0xF8, "kasrah", 0x650), (0xF4, "sukun", 0x652), (0xF7, "shaddah", 0x651),
]

order = [".notdef"] + [n for _, n, _ in L]
glyphs, metrics = {}, {}
for _, name, cp in [(0, ".notdef", None)] + L:
    pen = TTGlyphPen(None)
    if cp is not None:
        rec = DecomposingRecordingPen(gs)
        gs[cmap[cp]].draw(rec)
        rec.replay(pen)
        adv = src["hmtx"][cmap[cp]][0]
    else:
        adv = 500
    glyphs[name] = pen.glyph()
    metrics[name] = (adv, 0)

fb = FontBuilder(src["head"].unitsPerEm, isTTF=True)
fb.setupGlyphOrder(order)
fb.setupCharacterMap({0xF200 + code: name for code, name, _ in L}, allowFallback=True)
fb.setupGlyf(glyphs)
for n in glyphs:
    g = glyphs[n]
    g.recalcBounds(fb.font["glyf"])
    metrics[n] = (metrics[n][0], getattr(g, "xMin", 0))
fb.setupHorizontalMetrics(metrics)
fb.setupHorizontalHeader(ascent=1100, descent=-500)
fb.setupNameTable({"familyName": "Legacy Arabic Test", "styleName": "Regular", "version": "Version 1.0 - test"})
fb.setupOS2(sTypoAscender=1100, sTypoDescender=-500, usWinAscent=1100, usWinDescent=500, version=0)
fb.setupPost(keepGlyphNames=True)
font = fb.font
# Make it look like the old fonts: only a symbol Windows cmap (3,0) and symbol-encoded Windows names.
font["cmap"].tables = [t for t in font["cmap"].tables if t.platformID == 3]
for t in font["cmap"].tables:
    t.platEncID = 0
for rec in font["name"].names:
    if rec.platformID == 3:
        rec.platEncID = 0
out = os.path.join(here, "..", "tests", "data", "LegacyArabicTest.ttf")
font.save(out)
print("written", out, len(order), "glyphs")

# The MCS variant: same positions, but no usable glyph names (post format 3) and a "Unicode" (3,1) cmap at the private
# codes, so the converter must identify the letters by their position.
mcs = TTFont(out)
mcs["post"].formatType = 3.0
for t in mcs["cmap"].tables:
    t.platEncID = 1
for rec in mcs["name"].names:
    if rec.platformID == 3:
        rec.platEncID = 1
mcs["name"].setName("Legacy Arabic MCS Test", 1, 3, 1, 0x409)
mcs["name"].setName("Legacy Arabic MCS Test", 4, 3, 1, 0x409)
out2 = os.path.join(here, "..", "tests", "data", "LegacyArabicTestMCS.ttf")
mcs.save(out2)
print("written", out2)
