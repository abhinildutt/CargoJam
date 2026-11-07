"""Builds the Cargo Jam "Loaded Truck" logo: the wordmark over a red truck carrying three parcels.

Writes outlined SVG masters to docs/brand/ (no font needed to open them) and renders the PNGs
the game uses with headless Chrome:

  CargoJam/Assets/Resources/UI/Brand/WordmarkLetters.png  white letters with the title's soft shadow, and the road
  CargoJam/Assets/Resources/UI/Brand/WordmarkTruck.png    the loaded truck, cropped, placed by Logo.cs
  CargoJam/Assets/Branding/AppIcon.png                    1024 x 1024, full bleed, no alpha

Run from the repo root:  python3 tools/brand/make_logo.py
"""

import os
import subprocess
import sys
import tempfile

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.ttLib import TTFont
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONT = os.path.join(ROOT, "CargoJam/Assets/Resources/UI/Fonts/Nunito-Black.ttf")
DOCS = os.path.join(ROOT, "docs/brand")
UI_OUT = os.path.join(ROOT, "CargoJam/Assets/Resources/UI/Brand")
ICON_OUT = os.path.join(ROOT, "CargoJam/Assets/Branding")
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

INK = "#1E2A3B"
PAPER = "#F6F0E4"
ASPHALT = "#39414F"
# The parcel colors and symbols in ParcelColors order (R B G Y P O).
PARCELS = [("#E5544A", "circle"), ("#3F7FE6", "square"), ("#3DB36B", "plus"),
           ("#F2C230", "diamond"), ("#8E5BD6", "x"), ("#F08A3C", "bars")]
TRUCK_COLOR = 0
LOAD = (0, 3, 1)     # red, yellow, blue parcels on the bed

# Lockup geometry, in design units.
SIZE = 96            # letter size
BASELINE = 90
TRUCK_X, TRUCK_Y = 149, 128   # top-left of the 342 x 150 truck box; wheels rest on y = TRUCK_Y + 150
ROAD_Y, ROAD_H = 266, 28
ROAD_OVERHANG = 40   # road runs this far past the wider of the word and the truck
PAD = 14             # room around the art for the shadow

# Shadow for the white wordmark, matching .cj-on-glass (0 2px 6px rgba(0,0,0,.35) at 150px type).
SHADOW = '<filter id="soft" x="-10%" y="-10%" width="120%" height="130%"><feDropShadow dx="0" dy="1.3" stdDeviation="1.9" flood-color="#000" flood-opacity=".35"/></filter>'


def shade(hex_color, f):
    n = int(hex_color[1:], 16)
    ch = [(n >> 16) & 255, (n >> 8) & 255, n & 255]
    ch = [round(v * f) if f < 1 else round(v + (255 - v) * (f - 1)) for v in ch]
    return "#%02X%02X%02X" % tuple(ch)


def symbol(kind, cx, cy, k, fill):
    if kind == "circle":
        return f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{k * .46:.2f}" fill="{fill}"/>'
    if kind == "square":
        return f'<rect x="{cx - k * .42:.2f}" y="{cy - k * .42:.2f}" width="{k * .84:.2f}" height="{k * .84:.2f}" rx="{k * .08:.2f}" fill="{fill}"/>'
    if kind in ("plus", "x"):
        a, b = k, k * .32
        turn = f' transform="rotate(45 {cx:.2f} {cy:.2f})"' if kind == "x" else ""
        return (f'<g{turn} fill="{fill}"><rect x="{cx - a / 2:.2f}" y="{cy - b / 2:.2f}" width="{a:.2f}" height="{b:.2f}" rx="{b * .2:.2f}"/>'
                f'<rect x="{cx - b / 2:.2f}" y="{cy - a / 2:.2f}" width="{b:.2f}" height="{a:.2f}" rx="{b * .2:.2f}"/></g>')
    if kind == "diamond":
        return f'<polygon points="{cx:.2f},{cy - k * .56:.2f} {cx + k * .5:.2f},{cy:.2f} {cx:.2f},{cy + k * .56:.2f} {cx - k * .5:.2f},{cy:.2f}" fill="{fill}"/>'
    w, h = k * .92, k * .26
    return (f'<g fill="{fill}"><rect x="{cx - w / 2:.2f}" y="{cy - k * .2 - h / 2:.2f}" width="{w:.2f}" height="{h:.2f}" rx="{h * .25:.2f}"/>'
            f'<rect x="{cx - w / 2:.2f}" y="{cy + k * .2 - h / 2:.2f}" width="{w:.2f}" height="{h:.2f}" rx="{h * .25:.2f}"/></g>')


def parcel(x, y, s, i):
    """The game's parcel seen from above: colored lid, darker front face with tape, paper label with its symbol."""
    c, kind = PARCELS[i]
    lid, d, r = s * .84, s * .2, s * .13
    cx, cy = x + s / 2, y + lid / 2
    lab = s * .58
    return (f'<rect x="{x}" y="{y}" width="{s}" height="{lid + d:.2f}" rx="{r:.2f}" fill="{shade(c, .68)}"/>'
            f'<rect x="{cx - s * .075:.2f}" y="{y + lid:.2f}" width="{s * .15:.2f}" height="{d * .82:.2f}" fill="{shade(c, .52)}"/>'
            f'<rect x="{x}" y="{y}" width="{s}" height="{lid:.2f}" rx="{r:.2f}" fill="{c}"/>'
            f'<rect x="{x + s * .1:.2f}" y="{y + s * .045:.2f}" width="{s * .8:.2f}" height="{s * .055:.2f}" rx="{s * .028:.2f}" fill="#FFFFFF" fill-opacity=".22"/>'
            f'<rect x="{cx - lab / 2:.2f}" y="{cy - lab / 2:.2f}" width="{lab:.2f}" height="{lab:.2f}" rx="{s * .07:.2f}" fill="{PAPER}"/>'
            + symbol(kind, cx, cy, s * .34, shade(c, .42)))


def truck(i, load):
    """Side-view toy truck in a 342 x 150 box, facing right, wheels resting on y = 150."""
    c = PARCELS[i][0]
    t = [f'<ellipse cx="171" cy="149" rx="168" ry="7" fill="#0C121C" fill-opacity=".28"/>',
         f'<rect x="10" y="108" width="322" height="20" rx="8" fill="#2F3644"/>',
         f'<rect x="0" y="92" width="240" height="26" rx="6" fill="{shade(c, .72)}"/>',
         f'<rect x="0" y="86" width="240" height="14" rx="5" fill="{c}"/>']
    for j, k in enumerate(load):
        t.append(parcel(12 + j * 74, 22, 64, k))
    t += [f'<path d="M244 120 V38 Q244 26 256 26 H302 Q310 26 314 33 L338 78 Q342 85 342 92 V120 Z" fill="{c}"/>',
          '<path d="M262 42 H298 L320 82 H262 Z" fill="#DCEBF5"/><path d="M262 42 H280 L268 82 H262 Z" fill="#FFFFFF" fill-opacity=".6"/>',
          f'<rect x="244" y="100" width="98" height="20" rx="4" fill="{shade(c, .72)}"/>',
          '<rect x="326" y="90" width="14" height="9" rx="3" fill="#FFE7A3"/>']
    for x in (62, 288):
        t.append(f'<circle cx="{x}" cy="128" r="22" fill="#232833"/><circle cx="{x}" cy="128" r="9" fill="#B9C0CB"/>')
    return "".join(t)


def road(x, y, w, h):
    r = [f'<rect x="{x:.2f}" y="{y}" width="{w:.2f}" height="{h}" rx="{h / 2}" fill="{ASPHALT}"/>']
    dash, step = 32, 64
    n = int((w - 60) // step)
    start = x + (w - (n * step - (step - dash))) / 2
    for k in range(n):
        r.append(f'<rect x="{start + k * step:.2f}" y="{y + h / 2 - 2}" width="{dash}" height="4" rx="2" fill="#FFFFFF" fill-opacity=".7"/>')
    return "".join(r)


class Type:
    def __init__(self, path):
        self.font = TTFont(path)
        self.glyphs = self.font.getGlyphSet()
        self.cmap = self.font.getBestCmap()
        self.scale = SIZE / self.font["head"].unitsPerEm
        self.kerning = self._kern_subtables()

    def _kern_subtables(self):
        gpos = self.font["GPOS"].table
        lookups = set()
        for rec in gpos.FeatureList.FeatureRecord:
            if rec.FeatureTag == "kern":
                lookups.update(rec.Feature.LookupListIndex)
        subs = []
        for i in sorted(lookups):
            lookup = gpos.LookupList.Lookup[i]
            for st in lookup.SubTable:
                if lookup.LookupType == 9:
                    st = st.ExtSubTable
                if getattr(st, "LookupType", 2) == 2:
                    subs.append(st)
        return subs

    def kern(self, left, right):
        for st in self.kerning:
            cov = st.Coverage.glyphs
            if left not in cov:
                continue
            if st.Format == 1:
                for rec in st.PairSet[cov.index(left)].PairValueRecord:
                    if rec.SecondGlyph == right and rec.Value1 is not None:
                        return getattr(rec.Value1, "XAdvance", 0) or 0
            else:
                c1 = st.ClassDef1.classDefs.get(left, 0)
                c2 = st.ClassDef2.classDefs.get(right, 0)
                v = st.Class1Record[c1].Class2Record[c2].Value1
                if v is not None and getattr(v, "XAdvance", 0):
                    return v.XAdvance
        return 0

    def layout(self, text):
        """Glyph names with x offsets (design units) and the advance width."""
        names = [self.cmap[ord(ch)] for ch in text]
        out, x = [], 0
        for i, name in enumerate(names):
            out.append((name, x))
            x += self.glyphs[name].width * self.scale
            if i + 1 < len(names):
                x += self.kern(name, names[i + 1]) * self.scale
        return out, x

    def ink_bounds(self, placed, x0, baseline):
        xs, ys = [], []
        for name, dx in placed:
            pen = BoundsPen(self.glyphs)
            self.glyphs[name].draw(pen)
            if pen.bounds:
                bx0, by0, bx1, by1 = pen.bounds
                xs += [x0 + dx + bx0 * self.scale, x0 + dx + bx1 * self.scale]
                ys += [baseline - by1 * self.scale, baseline - by0 * self.scale]
        return min(xs), min(ys), max(xs), max(ys)

    def path(self, placed, x0, baseline):
        parts = []
        for name, dx in placed:
            pen = SVGPathPen(self.glyphs)
            self.glyphs[name].draw(pen)
            d = pen.getCommands()
            if d:
                parts.append(f'<path transform="translate({x0 + dx:.2f} {baseline}) scale({self.scale:.5f} {-self.scale:.5f})" d="{d}"/>')
        return "".join(parts)


def build_lockup(t):
    """Returns (letters svg, road svg, truck svg, viewBox, truck box) in design units, centered on x = 320."""
    word, w = t.layout("Cargo Jam")
    x0 = 320 - w / 2
    wb = t.ink_bounds(word, x0, BASELINE)
    half = max(wb[2] - 320, 320 - wb[0], 171) + ROAD_OVERHANG
    road_svg = road(320 - half, ROAD_Y, half * 2, ROAD_H)

    tbox = (TRUCK_X - 2, TRUCK_Y + 20, TRUCK_X + 344, TRUCK_Y + 158)
    left = min(wb[0], 320 - half) - PAD
    right = max(wb[2], 320 + half) + PAD
    top = wb[1] - PAD
    bottom = ROAD_Y + ROAD_H + PAD
    view = (left, top, right - left, bottom - top)
    truck_svg = f'<g transform="translate({TRUCK_X} {TRUCK_Y})">{truck(TRUCK_COLOR, LOAD)}</g>'
    return t.path(word, x0, BASELINE), road_svg, truck_svg, view, tbox


def svg(view, body, defs=""):
    x, y, w, h = view
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x:.2f} {y:.2f} {w:.2f} {h:.2f}" '
            f'width="{w:.0f}" height="{h:.0f}">{defs}{body}</svg>\n')


def icon_svg():
    """Sky over a road, the loaded truck as large as the width allows, drawn on a 120 grid."""
    sky = ('<defs><linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7CCBEB"/>'
           '<stop offset="1" stop-color="#3F93C8"/></linearGradient></defs><rect width="120" height="120" fill="url(#sky)"/>')
    cloud = '<g fill="#FFFFFF" fill-opacity=".85"><ellipse cx="30" cy="24" rx="16" ry="7"/><ellipse cx="40" cy="20" rx="10" ry="8"/><ellipse cx="94" cy="34" rx="10" ry="4.5"/></g>'
    ground = f'<rect x="0" y="90" width="120" height="30" fill="{ASPHALT}"/><rect x="0" y="103.5" width="120" height="3" fill="#FFFFFF" fill-opacity=".6"/>'
    s = 0.32
    body = f'{cloud}{ground}<g transform="translate({(120 - 342 * s) / 2:.2f} {96 - 150 * s:.2f}) scale({s})">{truck(TRUCK_COLOR, LOAD)}</g>'
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" width="1024" height="1024">{sky}{body}</svg>\n'


def render(svg_text, width, height, out_png, opaque=False):
    """Rasterizes an SVG at an exact pixel size with headless Chrome."""
    with tempfile.TemporaryDirectory() as tmp:
        src = os.path.join(tmp, "art.svg")
        page = os.path.join(tmp, "page.html")
        with open(src, "w") as f:
            f.write(svg_text)
        with open(page, "w") as f:
            f.write(f'<html><body style="margin:0;background:transparent"><img src="art.svg" width="{width}" height="{height}" style="display:block"></body></html>')
        shot = os.path.join(tmp, "shot.png")
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-color-profile=srgb",
                        "--force-device-scale-factor=1", "--default-background-color=00000000",
                        f"--window-size={width},{height}", f"--screenshot={shot}", f"file://{page}"],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        im = Image.open(shot).convert("RGB" if opaque else "RGBA")
        im.save(out_png, optimize=True)


def main():
    for d in (DOCS, UI_OUT, ICON_OUT):
        os.makedirs(d, exist_ok=True)
    t = Type(FONT)
    letters, road_svg, truck_svg, view, tbox = build_lockup(t)
    white = f'<g fill="#FFFFFF" filter="url(#soft)">{letters}</g>'
    ink = f'<g fill="{INK}">{letters}</g>'

    # Masters.
    with open(os.path.join(DOCS, "wordmark.svg"), "w") as f:
        f.write(svg(view, ink + road_svg + truck_svg))
    with open(os.path.join(DOCS, "wordmark-white.svg"), "w") as f:
        f.write(svg(view, white + road_svg + truck_svg, f"<defs>{SHADOW}</defs>"))
    icon = icon_svg()
    with open(os.path.join(DOCS, "app-icon.svg"), "w") as f:
        f.write(icon)

    # Game PNGs. The truck is separate so the title can drive it in along the road.
    px_per_unit = 1280 / view[2]
    w, h = 1280, round(view[3] * px_per_unit)
    render(svg(view, white + road_svg, f"<defs>{SHADOW}</defs>"), w, h, os.path.join(UI_OUT, "WordmarkLetters.png"))
    tview = (tbox[0], tbox[1], tbox[2] - tbox[0], tbox[3] - tbox[1])
    tw, th = round(tview[2] * px_per_unit), round(tview[3] * px_per_unit)
    render(svg(tview, truck_svg), tw, th, os.path.join(UI_OUT, "WordmarkTruck.png"))
    render(icon, 1024, 1024, os.path.join(ICON_OUT, "AppIcon.png"), opaque=True)
    render(icon, 1024, 1024, os.path.join(DOCS, "app-icon.png"), opaque=True)
    render(svg(view, ink + road_svg + truck_svg), w, h, os.path.join(DOCS, "wordmark.png"))

    # Where the truck sits inside the lockup, as fractions, for the title layout.
    print(f"lockup {w}x{h}px  aspect {view[2] / view[3]:.4f}")
    print(f"truck {tw}x{th}px  left {(tview[0] - view[0]) / view[2]:.4f}  top {(tview[1] - view[1]) / view[3]:.4f}  "
          f"width {tview[2] / view[2]:.4f}  height {tview[3] / view[3]:.4f}")


if __name__ == "__main__":
    sys.exit(main())
