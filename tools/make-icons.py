# Draws the IBRA SYSTEM CARE icon (black tile, gold shield with a check) and the installer pictures.
# Usage: python3 tools/make-icons.py <repo folder>   (needs Pillow)
import sys, os
from PIL import Image, ImageDraw

root = sys.argv[1] if len(sys.argv) > 1 else "."
S = 1024
GOLD = (212, 175, 55); GOLD2 = (240, 208, 110); DARK1 = (34, 34, 38); DARK2 = (8, 8, 10)


def gradient(w, h, top, bottom):
    g = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(g)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3)) + (255,))
    return g


def shield(d, cx, top, w, h, fill=None, outline=None, width=0):
    # A shield: flat top with rounded shoulders, sides narrowing to a point.
    pts = []
    import math
    for i in range(0, 101):
        t = i / 100
        # right side from top to bottom point
        x = cx + w / 2 * (1 - t ** 2.2)
        y = top + h * t
        pts.append((x, y))
    for i in range(100, -1, -1):
        t = i / 100
        x = cx - w / 2 * (1 - t ** 2.2)
        y = top + h * t
        pts.append((x, y))
    d.polygon(pts, fill=fill, outline=outline)
    if width:
        d.line(pts + [pts[0]], fill=outline, width=width, joint="curve")


def icon():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([24, 24, S - 24, S - 24], radius=220, fill=255)
    img.paste(gradient(S, S, DARK1, DARK2), (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([24, 24, S - 24, S - 24], radius=220, outline=GOLD + (255,), width=22)
    shield(d, S // 2, 190, 560, 660, fill=GOLD + (255,))
    shield(d, S // 2, 250, 440, 540, fill=(20, 20, 23, 255))
    # check mark
    a, b, c = (S // 2 - 125, 505), (S // 2 - 35, 595), (S // 2 + 135, 410)
    d.line([a, b, c], fill=GOLD2 + (255,), width=52, joint="curve")
    for (x, y) in [a, b, c]:
        d.ellipse([x - 26, y - 26, x + 26, y + 26], fill=GOLD2 + (255,))
    return img


big = icon()
os.makedirs(f"{root}/assets", exist_ok=True)
big.resize((256, 256), Image.LANCZOS).save(f"{root}/assets/ibracare-256.png")
big.resize((512, 512), Image.LANCZOS).save(f"{root}/assets/ibracare-512.png")
big.save(f"{root}/assets/ibracare.ico", sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])

# Inno Setup pictures: side (164x314 at 100%) and corner (55x55 at 100%), plus 150% and 200%.
os.makedirs(f"{root}/installer", exist_ok=True)
for scale, tag in [(1, "100"), (1.5, "150"), (2, "200")]:
    w, h = int(164 * scale), int(314 * scale)
    side = gradient(w, h, (24, 24, 28), (6, 6, 8)).convert("RGB")
    logo = big.resize((int(w * 0.72), int(w * 0.72)), Image.LANCZOS)
    side.paste(logo, ((w - logo.width) // 2, int(h * 0.30)), logo)
    sd = ImageDraw.Draw(side)
    sd.rectangle([0, h - int(6 * scale), w, h], fill=GOLD)
    side.save(f"{root}/installer/wizard-side-{tag}.bmp")
    c = int(55 * scale)
    corner = Image.new("RGB", (c, c), (255, 255, 255))
    cl = big.resize((c, c), Image.LANCZOS)
    corner.paste(cl, (0, 0), cl)
    corner.save(f"{root}/installer/wizard-corner-{tag}.bmp")
print("icons written")
