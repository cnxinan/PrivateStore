"""Original 48px gothic actors. Integer clusters; no sampled runtime geometry.

All clothing, hands and equipment use the same pose. Directional features are
drawn in screen space, not rotated from a single character picture.
"""
import math
from dataclasses import dataclass

from PIL import Image, ImageDraw


INK = "#15151f"
SKIN = (190, 143, 105)
SKIN_DARK, SKIN_LIGHT = "#885b48", "#e0b583"
BONE, BONE_DARK, BONE_LIGHT = (202, 195, 164), "#817e71", "#eee1b6"
BRASS, GOLD = (163, 128, 65), "#e2bc6a"
STEEL = (179, 191, 197)
NAVY, NAVY_DARK, NAVY_LIGHT = "#354a61", "#233044", "#586e85"
ROBE, ROBE_DARK, ROBE_LIGHT = (75, 35, 91), "#301e42", "#87549b"
GLOW = (147, 112, 219)
VECTORS = ((1, 0), (.75, .6), (0, 1), (-.75, .6),
           (-1, 0), (-.75, -.6), (0, -1), (.75, -.6))
GAIT = (0, 2, 3, 0, -2, -3)


def new(size=(48, 48)):
    return Image.new("RGBA", size)


def poly(draw, points, fill, outline=INK):
    draw.polygon([(round(x), round(y)) for x, y in points], fill=fill)
    if outline:
        draw.line([(round(x), round(y)) for x, y in points + [points[0]]],
                  fill=outline, width=1)


def limb(draw, a, b, color, width=3, shine=None):
    a, b = tuple(map(round, a)), tuple(map(round, b))
    draw.line((a, b), fill=INK, width=width + 2)
    draw.line((a, b), fill=color, width=width)
    if shine:
        draw.line(((a[0] - 1, a[1]), (b[0] - 1, b[1])),
                  fill=shine, width=1)


@dataclass
class Pose:
    direction: int
    action: int
    frame: int
    armed: bool = False

    def __post_init__(self):
        self.fx, self.fy = VECTORS[self.direction]
        self.back = self.direction in (5, 6, 7)
        self.side = self.direction in (0, 4)
        self.diagonal = self.direction in (1, 3, 5, 7)
        self.x, self.y, self.stride = 24, 0, 0
        if self.action == 0:
            self.y = (0, -1, -1, 0, 1, 1)[self.frame]
        elif self.action == 1:
            self.stride = GAIT[self.frame]
            self.y = (0, -1, 0, 0, -1, 0)[self.frame]
        elif self.action == 2:
            self.x += round(self.fx * (0, -1, 0, 2, 3, 1)[self.frame])
            self.y += round(self.fy * (0, -1, 0, 2, 3, 1)[self.frame])
        elif self.action == 3:
            recoil = (0, 1, 3, 2, 1, 0)[self.frame]
            self.x -= round(self.fx * recoil)
            self.y += (0, 1, 2, 1, 0, -1)[self.frame]
        self.width = 5 if self.side else 7
        # Hand authored attack: preparation, draw back, high guard, extension,
        # follow-through, recover. Unarmed uses a shorter direct punching arc.
        if self.action == 2:
            reach = (4, 2, 5, 10, 8, 5)[self.frame]
            if self.armed:
                up = (-2, -7, -9, -3, 3, 0)[self.frame]
            else:
                up = (0, -1, -2, -1, 1, 0)[self.frame]
            self.hand = (round(self.x + self.fx * reach + (4 if self.fx >= 0 else -4)),
                         round(24 + self.y + self.fy * reach * .55 + up))
        else:
            swing = self.stride if self.action == 1 else (0, 0, 1, 1, 0, -1)[self.frame]
            self.hand = (self.x + (8 if self.fx >= 0 else -8),
                         26 + self.y - swing)
        self.shoulder = (self.x + (5 if self.fx >= 0 else -5), 19 + self.y)
        self.other = (self.x + (-8 if self.fx >= 0 else 8), 26 + self.y + self.stride)

    def finish(self, image):
        if self.action != 4:
            return image
        angle = (0, 12, 32, 58, 80, 90)[self.frame]
        if self.direction in (3, 4, 5):
            angle = -angle
        image = image.rotate(angle, resample=Image.Resampling.NEAREST, center=(24, 24))
        fallen = new()
        fallen.alpha_composite(image, (0, (0, 1, 3, 5, 7, 8)[self.frame]))
        return fallen


def boots(draw, p, skeleton=False):
    x, y, s = p.x, p.y, p.stride
    spread = 3 if p.side else 5
    for sign, depth in ((-1, s), (1, -s)):
        hip = (x + sign * 3, 27 + y)
        knee = (x + sign * spread + round(p.fx * depth / 2), 30 + y)
        foot = (x + sign * spread + round(p.fx * depth),
                34 + round(p.fy * depth / 2))
        limb(draw, hip, knee, BONE_DARK if skeleton else NAVY_DARK, 3)
        limb(draw, knee, foot, BONE if skeleton else "#5b4235", 3,
             BONE_LIGHT if skeleton else "#987458")
        if skeleton:
            draw.point(knee, fill=BONE_LIGHT)
            draw.line((foot, (foot[0] + (2 if p.fx >= 0 else -2), foot[1] + 1)), fill=BONE, width=2)
        else:
            draw.rectangle((foot[0] - 2, foot[1] - 1, foot[0] + 3, foot[1] + 1), fill=INK)
            draw.line((foot[0] - 1, foot[1] - 1, foot[0] + 2, foot[1] - 1), fill="#a0805b")


def adventurer(p):
    image = new()
    d = ImageDraw.Draw(image)
    x, y, w = p.x, p.y, p.width
    boots(d, p)
    limb(d, (x - (5 if p.fx >= 0 else -5), 19 + y), p.other, NAVY_DARK, 3, NAVY_LIGHT)
    d.rectangle((p.other[0] - 1, p.other[1] - 1, p.other[0] + 1, p.other[1] + 1), fill=SKIN)
    poly(d, [(x - w + 1, 17 + y), (x + w - 1, 17 + y),
             (x + w, 26 + y), (x + 4, 29 + y), (x - 5, 29 + y),
             (x - w, 25 + y)], NAVY)
    poly(d, [(x - w + 1, 19 + y), (x - 1, 18 + y), (x - 1, 26 + y),
             (x - 5, 26 + y)], NAVY_LIGHT, None)
    d.line((x + w - 2, 19 + y, x + w - 1, 25 + y), fill=NAVY_DARK, width=2)
    if p.back:
        poly(d, [(x - 4, 18 + y), (x + 3, 18 + y), (x + 5, 29 + y),
                 (x + 1, 27 + y), (x - 5, 29 + y)], "#29394d")
        d.line((x - 2, 19 + y, x - 1, 25 + y), fill="#6a7e8d")
    else:
        d.line((x - 4, 18 + y, x + 3, 25 + y), fill="#8a6c45", width=2)
        d.point((x - 2, 20 + y), fill="#baa57c")
    d.rectangle((x - w + 1, 26 + y, x + w - 1, 27 + y), fill="#41312b")
    d.rectangle((x - 1, 26 + y, x + 1, 27 + y), fill=BRASS)
    head_x = x + round(p.fx)
    poly(d, [(head_x - 4, 9 + y), (head_x + 3, 9 + y), (head_x + 5, 12 + y),
             (head_x + 4, 16 + y), (head_x + 1, 18 + y), (head_x - 3, 17 + y),
             (head_x - 5, 13 + y)], SKIN)
    d.rectangle((head_x - 3, 11 + y, head_x, 14 + y), fill=SKIN_LIGHT)
    d.line((head_x + 3, 13 + y, head_x + 2, 16 + y), fill=SKIN_DARK, width=2)
    poly(d, [(head_x - 5, 12 + y), (head_x - 4, 8 + y), (head_x + 2, 7 + y),
             (head_x + 5, 10 + y), (head_x + 4, 12 + y), (head_x, 10 + y),
             (head_x - 2, 12 + y)], "#594437")
    d.line((head_x - 3, 9 + y, head_x + 1, 8 + y), fill="#9e7950", width=1)
    if p.back:
        poly(d, [(head_x - 4, 10 + y), (head_x + 4, 10 + y),
                 (head_x + 3, 17 + y), (head_x - 2, 18 + y),
                 (head_x - 4, 15 + y)], "#594437")
        d.line((head_x - 2, 11 + y, head_x - 2, 15 + y), fill="#896745")
    elif p.side:
        s = 1 if p.fx > 0 else -1
        d.point((head_x + s * 3, 12 + y), fill=INK)
        d.point((head_x + s * 5, 14 + y), fill=SKIN_LIGHT)
        d.line((head_x + s * 2, 16 + y, head_x + s * 3, 16 + y), fill=SKIN_DARK)
        d.point((head_x - s * 2, 14 + y), fill=SKIN_DARK)
    else:
        eye = round(p.fx)
        d.point((head_x - 2 + eye, 13 + y), fill=INK)
        d.point((head_x + 2 + eye, 13 + y), fill=INK)
        d.point((head_x + eye, 14 + y), fill=SKIN_LIGHT)
        d.line((head_x - 1 + eye, 16 + y, head_x + 1 + eye, 16 + y), fill=SKIN_DARK)
    elbow = (round((p.shoulder[0] + p.hand[0]) / 2) + (1 if p.fx >= 0 else -1),
             round((p.shoulder[1] + p.hand[1]) / 2) + 2)
    limb(d, p.shoulder, elbow, NAVY, 3, NAVY_LIGHT)
    limb(d, elbow, p.hand, SKIN, 3, SKIN_LIGHT)
    d.rectangle((p.hand[0] - 1, p.hand[1] - 1, p.hand[0] + 1, p.hand[1] + 1), fill=SKIN)
    if p.action == 2 and not p.armed and p.frame in (3, 4):
        d.rectangle((p.hand[0] - 2, p.hand[1] - 1, p.hand[0] + 2, p.hand[1] + 2), fill=SKIN)
        d.line((p.hand[0] - 1, p.hand[1] - 1, p.hand[0] + 1, p.hand[1] - 1), fill=SKIN_LIGHT)
    return p.finish(image)


def equipment(kind, rarity, p, finish=True):
    image = new()
    d = ImageDraw.Draw(image)
    palettes = {
        "common": ("#707d86", STEEL, "#4a525c", "#8e7950"),
        "magic": ((66, 104, 158), "#a5c9e8", "#2d466f", "#6a92c9"),
        "rare": ("#8a989c", "#e4e6d4", "#465260", BRASS),
    }
    main, light, dark, trim = palettes[rarity]
    if kind == "armor":
        x, y, w = p.x, p.y, p.width
        poly(d, [(x - w + 2, 18 + y), (x + w - 2, 18 + y),
                 (x + w - 1, 24 + y), (x + 1, 26 + y),
                 (x - w + 1, 24 + y)], main)
        poly(d, [(x - w + 3, 19 + y), (x, 19 + y), (x - 1, 24 + y),
                 (x - w + 2, 23 + y)], light, None)
        d.line((x + 1, 19 + y, x + w - 2, 20 + y, x + w - 2, 23 + y),
               fill=dark, width=2)
        for side in (-1, 1):
            sx = x + side * w
            poly(d, [(sx - 3, 18 + y), (sx, 16 + y), (sx + 3, 18 + y),
                     (sx + 2, 21 + y), (sx - 2, 20 + y)], main)
            d.line((sx - 2, 18 + y, sx, 17 + y, sx + 2, 18 + y), fill=light)
        if rarity != "common":
            d.line((x - w + 2, 24 + y, x + 1, 26 + y, x + w - 1, 24 + y), fill=trim)
            d.rectangle((x - 1, 20 + y, x + 1, 22 + y), fill=main)
            d.point((x, 20 + y), fill="#bce3f0" if rarity == "magic" else GOLD)
            d.point((x - w - 1, 16 + y), fill=trim)
        if rarity == "rare":
            d.line((x - w - 2, 17 + y, x - w, 16 + y, x - w + 2, 17 + y), fill=BRASS)
            d.line((x + w - 2, 17 + y, x + w, 16 + y, x + w + 2, 17 + y), fill=GOLD)
            d.point((x, 23 + y), fill=GOLD)
            d.point((x + w + 1, 16 + y), fill=BRASS)
        # Armor is composited over the body at runtime: preserve exposed grips
        # during the high guard and preserve the chin inside the gorget.
        for hx, hy in (p.hand, p.other):
            d.rectangle((hx - 2, hy - 2, hx + 2, hy + 2), fill=(0, 0, 0, 0))
        hx = x + round(p.fx)
        d.polygon([(hx - 4, 9 + y), (hx + 3, 9 + y), (hx + 5, 12 + y),
                   (hx + 4, 16 + y), (hx + 1, 18 + y), (hx - 3, 17 + y),
                   (hx - 5, 13 + y)], fill=(0, 0, 0, 0))
    else:
        hx, hy = p.hand
        if p.action == 2:
            # Sword angle is a separate authored arc, aligned to the grip.
            angles = (-105, -145, -95, -30, 35, -65)
            angle = math.radians(angles[p.frame])
            sx = 1 if p.fx >= 0 else -1
            dx, dy = math.cos(angle) * sx, math.sin(angle)
            if p.direction in (2, 6):
                dx = (.1, -.6, .3, .8, -.6, .2)[p.frame]
                dy = (-1, -1, -.9, .7, .85, -1)[p.frame] * (1 if p.fy >= 0 else -1)
        else:
            dx, dy = (.24 if p.fx >= 0 else -.24), -.97
        length = 11 if rarity == "common" else 13
        if rarity == "rare":
            length = 14
        tip = (max(2, min(45, round(hx + dx * length))),
               max(2, min(45, round(hy + dy * length))))
        nx, ny = -dy, dx
        base = (hx + dx * 3, hy + dy * 3)
        poly(d, [(base[0] + nx * 1.5, base[1] + ny * 1.5), tip,
                 (base[0] - nx * 1.5, base[1] - ny * 1.5)], main)
        d.line((tuple(map(round, base)), tip), fill=light)
        d.line(((round(hx - dx), round(hy - dy)), (round(hx + dx * 3), round(hy + dy * 3))),
               fill="#624431", width=2)
        guard = (hx + dx * 2, hy + dy * 2)
        d.line(((round(guard[0] - nx * 3), round(guard[1] - ny * 3)),
                (round(guard[0] + nx * 3), round(guard[1] + ny * 3))), fill=trim, width=2)
        d.point((hx, hy), fill=light)
        if rarity == "magic":
            d.point((round(base[0] + dx * 3), round(base[1] + dy * 3)), fill=(66, 104, 158))
            d.point((round(hx - dx * 2), round(hy - dy * 2)), fill="#8cd9f2")
        if rarity == "rare":
            d.rectangle((hx - 1, hy + 1, hx + 1, hy + 2), fill=BRASS)
            d.point((hx, hy + 1), fill="#c76642")
            d.point((round(guard[0] + nx * 4), round(guard[1] + ny * 4)), fill=GOLD)
    return p.finish(image) if finish else image


def skull(d, x, y, p):
    poly(d, [(x - 4, y - 5), (x + 3, y - 5), (x + 5, y - 2),
             (x + 4, y + 3), (x + 2, y + 5), (x - 2, y + 5),
             (x - 4, y + 2), (x - 5, y - 2)], BONE)
    d.line((x - 3, y - 3, x + 1, y - 4), fill=BONE_LIGHT, width=2)
    d.line((x + 3, y - 1, x + 3, y + 2), fill=BONE_DARK)
    if p.back:
        d.line((x, y - 3, x - 1, y, x + 1, y + 2), fill=BONE_DARK)
        d.point((x - 3, y), fill=BONE_DARK)
    elif p.side:
        sign = 1 if p.fx > 0 else -1
        d.rectangle((x + sign * 2 - 1, y - 1, x + sign * 2 + 1, y + 1), fill=INK)
        d.point((x + sign * 4, y + 2), fill=INK)
        d.line((x + sign, y + 4, x + sign * 3, y + 4), fill=BONE_DARK)
    else:
        shift = round(p.fx)
        for offset in (-2, 2):
            d.rectangle((x + offset + shift - 1, y - 1, x + offset + shift, y + 1), fill=INK)
            d.point((x + offset + shift, y), fill="#a35748")
        d.point((x + shift, y + 2), fill=INK)
        d.line((x - 2, y + 4, x + 2, y + 4), fill=BONE_DARK)
        for tx in (-1, 1):
            d.point((x + tx, y + 4), fill=INK)


def skeleton(p, warrior=False):
    image = new()
    d = ImageDraw.Draw(image)
    x, y = p.x, p.y
    if not warrior:
        # Reserve the bow's full bend and the nocked arrow, even at extension.
        p.hand = (max(12, min(36, p.hand[0])), p.hand[1])
    boots(d, p, True)
    limb(d, (x - 4, 19 + y), p.other, BONE, 2, BONE_LIGHT)
    poly(d, [(x - 5, 18 + y), (x + 4, 18 + y), (x + 5, 24 + y),
             (x + 3, 27 + y), (x - 3, 27 + y), (x - 5, 24 + y)], BONE_DARK)
    d.line((x, 18 + y, x, 27 + y), fill=BONE_LIGHT, width=2)
    for row in (19, 22, 25):
        d.line((x - 4, row + y, x - 1, row + 1 + y), fill=BONE, width=2)
        d.line((x + 1, row + 1 + y, x + 4, row + y), fill=BONE, width=2)
    d.line((x - 3, 28 + y, x + 3, 28 + y), fill=BRASS if warrior else "#694a39", width=2)
    if p.back and not warrior:
        poly(d, [(x - 4, 17 + y), (x + 2, 18 + y), (x + 1, 29 + y),
                 (x - 5, 28 + y)], "#58422e")
        for i in range(3):
            d.line((x - 3 + i, 18 + y, x - 4 + i, 12 + y), fill="#bfb7a2")
    skull(d, x + round(p.fx), 12 + y, p)
    limb(d, p.shoulder, p.hand, BONE, 2, BONE_LIGHT)
    if warrior:
        # Skull remains exposed; cuirass and shield are brass-edged, not gold blobs.
        poly(d, [(x - 5, 18 + y), (x - 2, 17 + y), (x + 5, 18 + y),
                 (x + 4, 23 + y), (x - 4, 23 + y)], "#545d60")
        d.line((x - 4, 18 + y, x + 3, 18 + y), fill=BRASS, width=2)
        d.line((x - 3, 20 + y, x - 1, 20 + y), fill=STEEL)
        sx, sy = p.other[0], p.other[1] - 1
        width = 3 if p.side else 5
        poly(d, [(sx - width, sy - 6), (sx, sy - 7), (sx + width, sy - 6),
                 (sx + width, sy + 2), (sx, sy + 7), (sx - width, sy + 2)], BRASS)
        poly(d, [(sx - width + 1, sy - 4), (sx + width - 1, sy - 4),
                 (sx + width - 1, sy + 1), (sx, sy + 5), (sx - width + 1, sy + 1)],
             "#3a4651")
        d.line((sx, sy - 4, sx, sy + 3), fill=GOLD)
        d.line((sx - width + 2, sy - 2, sx + width - 2, sy - 2), fill=BRASS)
        weapon = equipment("weapon", "common", p, finish=False)
        image.alpha_composite(weapon)
    else:
        # Bow changes bend and string hand across the six draw/release phases.
        hx, hy = p.hand
        sign = 1 if p.fx >= 0 else -1
        bend = (5, 5, 6, 6, 4, 5)[p.frame] if p.action == 2 else 4
        bow_x = hx + sign * 2
        pts = [(bow_x - sign, hy - 10), (bow_x + sign * 3, hy - 7),
               (bow_x + sign * bend, hy), (bow_x + sign * 3, hy + 7),
               (bow_x - sign, hy + 10)]
        d.line(pts, fill=INK, width=3)
        d.line(pts, fill="#a47c4c", width=1)
        pull = (0, 1, 3, 5, 0, 0)[p.frame] if p.action == 2 else 0
        string = [(bow_x - sign, hy - 10), (bow_x - sign * pull, hy),
                  (bow_x - sign, hy + 10)]
        d.line(string, fill="#c5c6ad")
        if p.action == 2 and p.frame < 4:
            d.line((bow_x - sign * (pull + 4), hy, bow_x + sign * 7, hy), fill=BONE_LIGHT)
            d.point((bow_x + sign * 7, hy), fill=STEEL)
    return p.finish(image)


def cultist(p):
    image = new()
    d = ImageDraw.Draw(image)
    x, y = p.x, p.y
    s = p.stride
    poly(d, [(x - 4, 17 + y), (x + 4, 17 + y), (x + 7, 26 + y),
             (x + 8 + s // 2, 34), (x + 2, 35), (x - 1, 33),
             (x - 8 + s // 2, 35), (x - 7, 26 + y)], ROBE)
    poly(d, [(x - 4, 19 + y), (x - 1, 19 + y), (x - 3, 32),
             (x - 6 + s // 2, 33)], ROBE_LIGHT, None)
    poly(d, [(x + 2, 20 + y), (x + 5, 24 + y), (x + 6 + s // 2, 33),
             (x + 2, 33)], ROBE_DARK, None)
    d.line((x - 6, 32, x - 1, 31, x + 2, 33, x + 6, 32), fill=BRASS)
    d.line((x - 5, 25 + y, x + 5, 25 + y), fill=BRASS)
    d.rectangle((x - 1, 25 + y, x + 1, 27 + y), fill=GOLD)
    hx = x + round(p.fx)
    poly(d, [(hx, 6 + y), (hx + 5, 10 + y), (hx + 6, 17 + y),
             (hx + 3, 20 + y), (hx - 5, 19 + y), (hx - 6, 13 + y),
             (hx - 4, 9 + y)], ROBE)
    d.line((hx - 4, 11 + y, hx - 1, 8 + y, hx + 2, 10 + y), fill=ROBE_LIGHT)
    if not p.back:
        shift = round(p.fx * 2)
        if p.side:
            sign = 1 if p.fx > 0 else -1
            poly(d, [(hx + sign, 11 + y), (hx + sign * 5, 13 + y),
                     (hx + sign * 4, 17 + y), (hx + sign, 18 + y)], "#211c30")
            d.line((hx - sign * 3, 10 + y, hx - sign * 4, 17 + y), fill=ROBE_LIGHT)
            d.line((x - sign * 3, 21 + y, x - sign * 2, 30), fill=ROBE_DARK, width=2)
        else:
            poly(d, [(hx - 3 + shift, 12 + y), (hx + 2 + shift, 11 + y),
                     (hx + 4 + shift, 17 + y), (hx - 3 + shift, 17 + y)], "#211c30")
        d.line((hx - 3 + shift, 12 + y, hx - 3 + shift, 16 + y), fill="#aa719e")
        d.point((hx + shift, 14 + y), fill="#dac5f1")
        if not p.side:
            d.point((hx + 3 + shift, 14 + y), fill=GLOW)
    else:
        d.line((hx, 10 + y, hx + 1, 17 + y), fill=ROBE_DARK)
        d.line((hx - 2, 11 + y, hx - 3, 16 + y), fill=ROBE_LIGHT)
    limb(d, p.shoulder, p.hand, ROBE, 4, ROBE_LIGHT)
    d.rectangle((p.hand[0] - 1, p.hand[1] - 1, p.hand[0] + 1, p.hand[1] + 1), fill="#aba18e")
    tx, ty = p.hand[0], p.hand[1]
    # Staff upright even during recoil; all ornamental edges are integer pixels.
    d.line((tx, max(9, ty - 12), tx, min(39, ty + 11)), fill=INK, width=3)
    d.line((tx, max(9, ty - 12), tx, min(39, ty + 11)), fill="#84634b", width=1)
    gy = max(6, ty - 14)
    poly(d, [(tx, gy - 3), (tx + 3, gy), (tx + 2, gy + 3),
             (tx - 2, gy + 3), (tx - 3, gy)], BRASS)
    d.rectangle((tx - 1, gy - 1, tx + 1, gy + 2), fill=GLOW)
    d.point((tx, gy - 1), fill="#e1d6fa")
    if p.action == 2:
        radius = (2, 3, 4, 5, 4, 3)[p.frame]
        for sign in (-1, 1):
            d.point((tx + sign * radius, gy), fill=GLOW)
            d.point((tx, max(2, gy + sign * radius)), fill="#d0bff0")
    return p.finish(image)


def slime(direction, action, frame):
    image = new()
    d = ImageDraw.Draw(image)
    fx, fy = VECTORS[direction]
    bounce = (0, -1, -2, -1, 1, 0)[frame]
    width, height = 12, 16
    cx, bottom = 24 + round(fx * bounce), 35
    if action == 1:
        width += (1, 0, -2, -1, 2, 1)[frame]
        height += (0, 3, 5, 2, -2, -1)[frame]
        cx += round(fx * GAIT[frame])
    elif action == 2:
        width += (1, 3, 2, -3, -1, 0)[frame]
        height += (0, -3, -2, 7, 4, 1)[frame]
        cx += round(fx * (0, -1, 0, 5, 3, 1)[frame])
        bottom += round(fy * (0, 0, 0, 3, 2, 0)[frame])
    elif action == 3:
        width += (0, 3, 4, 2, 1, 0)[frame]
        height += (0, -4, -6, -3, -1, 1)[frame]
    elif action == 4:
        width += frame // 2
        height = (16, 13, 9, 6, 4, 3)[frame]
    else:
        height -= bounce
    top = bottom - height
    lean = round(fx * 3)
    poly(d, [(cx - width, bottom - 2), (cx - width + 1, top + 8),
             (cx - 6 + lean, top + 2), (cx + lean, top),
             (cx + 6 + lean, top + 2), (cx + width, top + 8),
             (cx + width + 1, bottom - 2), (cx + width - 3, bottom),
             (cx + 3, bottom - 1), (cx - 2, bottom + 1),
             (cx - width + 3, bottom)], "#547944")
    if height > 7:
        poly(d, [(cx - width + 2, bottom - 4), (cx - 6 + lean, top + 3),
                 (cx + lean, top + 1), (cx + 5 + lean, top + 3),
                 (cx + width - 2, bottom - 6), (cx + 3, bottom - 3)], "#80a55a", None)
        d.line((cx - 6 + lean, top + 4, cx - 3 + lean, top + 2, cx + lean, top + 2),
               fill="#bdd788", width=2)
        d.rectangle((cx - 6, bottom - 6, cx - 5, bottom - 5), fill="#a6c579")
        eye_x, eye_y = cx + round(fx * 5), top + height // 2 + round(fy * 2)
        if direction not in (5, 6, 7):
            for dx in ((0,) if direction in (0, 4) else (-3, 3)):
                d.rectangle((eye_x + dx, eye_y, eye_x + dx + 1, eye_y + 2), fill=INK)
                d.point((eye_x + dx, eye_y), fill="#eff3c7")
            d.line((eye_x - 1, eye_y + 4, eye_x + 2, eye_y + 4), fill="#334335")
        else:
            d.line((cx + lean - 2, top + 5, cx + lean + 2, top + 7), fill="#648a4d")
        # Distinct volume lobes per direction, including rear views.
        d.rectangle((cx + round(fx * 7) - 1, bottom - 4 + round(fy),
                     cx + round(fx * 7) + 1, bottom - 3 + round(fy)), fill="#344e36")
    else:
        d.line((cx - 7, bottom - 1, cx + 5, bottom - 1), fill="#8bac62")
        d.point((cx + round(fx * 9), bottom - 2 + round(fy)), fill="#bad57c")
    return image


def wolf(direction, action, frame):
    image = new()
    d = ImageDraw.Draw(image)
    fx, fy = VECTORS[direction]
    side = direction in (0, 4)
    rear = direction in (5, 6, 7)
    stride = GAIT[frame] if action == 1 else 0
    y = (0, -1, -2, -1, 0, 1)[frame]
    lean = (0, -1, 0, 3, 4, 1)[frame] if action == 2 else 0
    if action == 3:
        y += (0, 1, 3, 2, 1, 0)[frame]
    if action == 4:
        y += frame
    cx, cy = 24 + round(fx * lean), 24 + y
    length = 11 if side else (9 if fx else 7)
    headx = cx + round(fx * (10 if side else 7))
    heady = cy + round(fy * 6) - 2
    tan, shadow, light = "#ab8556", "#695344", "#d4b77a"
    if action == 4 and frame >= 4:
        # A quadruped must end on its flank, not rotate into a standing biped.
        # Flattened mane, closed eye and folded paws distinguish this silhouette.
        drop = 2 if frame == 5 else 0
        sign = 1 if fx >= 0 else -1

        def q(points):
            return [(24 + (x - 24) * sign, yy + drop) for x, yy in points]

        poly(d, q([(11, 30), (16, 26), (31, 26), (36, 31),
                   (34, 36), (14, 36)]), tan)
        poly(d, q([(14, 29), (18, 27), (30, 27), (33, 30),
                   (16, 31)]), light, None)
        poly(d, q([(32, 29), (37, 28), (41, 31), (42, 34),
                   (38, 37), (33, 35)]), tan)
        poly(d, q([(36, 32), (41, 32), (43, 34), (40, 36),
                   (36, 35)]), light)
        d.line(q([(35, 29), (32, 27), (33, 31)]), fill=shadow, width=2)
        d.line(q([(36, 31), (38, 31)]), fill=INK)
        d.point(q([(42, 34)])[0], fill=INK)
        for points in ([(16, 33), (20, 36), (15, 37)],
                       [(27, 33), (30, 36), (25, 37)]):
            d.line(q(points), fill=shadow, width=3)
            d.line(q([(points[-1][0], 36), (points[-1][0] + 3, 36)]), fill=light)
        d.line(q([(13, 31), (8, 33), (5, 31)]), fill=INK, width=4)
        d.line(q([(13, 31), (8, 33), (5, 31)]), fill=tan, width=2)
        d.point(q([(7, 32 + round(fy))])[0], fill=light)
        return image
    # Four legs with occluded far legs, thigh/knee shapes and differently placed paws.
    for far in (True, False):
        for sign in (-1, 1):
            lx = cx + sign * (length - 4) + (0 if far else 2)
            ly = cy + (1 if far else 4)
            step = stride * sign * (-1 if far else 1)
            foot = (lx + round(fx * step), 34 + round(fy * step / 2) - (2 if far else 0))
            limb(d, (lx, ly), (lx - 1, foot[1] - 4), shadow if far else tan, 2)
            limb(d, (lx - 1, foot[1] - 4), foot, shadow if far else light, 2)
            d.line((foot, (foot[0] + (2 if fx >= 0 else -2), foot[1])), fill=shadow, width=2)
    tail_start = (cx - round(fx * length), cy - round(fy * 3))
    tail_end = (cx - round(fx * (length + 7)) + (2 if not fx else 0),
                cy - round(fy * 9) - 4 + (frame % 3))
    limb(d, tail_start, tail_end, shadow, 4, tan)
    d.point(tuple(map(round, tail_end)), fill=light)
    poly(d, [(cx - length, cy - 3), (cx - length + 4, cy - 7),
             (cx + length - 3, cy - 6), (cx + length, cy - 2),
             (cx + length - 1, cy + 5), (cx + 2, cy + 6),
             (cx - length + 1, cy + 4)], tan)
    poly(d, [(cx - length + 3, cy - 4), (cx, cy - 6),
             (cx + length - 3, cy - 4), (cx + 3, cy - 1), (cx - length + 2, cy)],
         light, None)
    d.line((cx - length + 2, cy + 3, cx + 5, cy + 4), fill=shadow, width=2)
    # Raised mane, pointed ears and a genuine forward muzzle.
    poly(d, [(headx - 6, heady - 2), (headx - 3, heady - 6),
             (headx + 3, heady - 6), (headx + 6, heady - 2),
             (headx + 4, heady + 6), (headx, heady + 8),
             (headx - 4, heady + 5)], tan)
    for ex in (-4, 3):
        poly(d, [(headx + ex - 2, heady - 3), (headx + ex - 1, heady - 10),
                 (headx + ex + 2, heady - 4)], shadow)
        d.point((headx + ex - 1, heady - 7), fill="#c79578")
    if rear:
        d.line((headx - 2, heady - 4, headx - 1, heady + 4), fill=light, width=2)
        d.line((headx + 2, heady - 2, headx + 3, heady + 2), fill=shadow)
    else:
        mx, my = headx + round(fx * 4), heady + 3 + round(fy * 2)
        poly(d, [(mx - 3, my - 2), (mx + 3, my - 2),
                 (mx + 4, my + 1), (mx + 1, my + 4), (mx - 3, my + 2)], light)
        d.rectangle((mx - 1, my + 1, mx + 1, my + 2), fill=INK)
        for ex in ((round(fx * 3),) if side else (-3, 3)):
            d.point((headx + ex, heady), fill=INK)
            d.point((headx + ex, heady - 1), fill="#e3c56d")
        if action == 2 and frame in (3, 4):
            d.line((mx - 2, my + 3, mx + 2, my + 3), fill="#482c2a", width=2)
            d.point((mx - 1, my + 3), fill="#f1dfb5")
    if action == 4:
        angle = (0, 0, 12, 24)[frame]
        image = image.rotate(angle, Image.Resampling.NEAREST, center=(24, 28))
    return image


def render_actor(name, direction, action, frame):
    if name == "slime":
        return slime(direction, action, frame)
    if name == "wolf":
        return wolf(direction, action, frame)
    p = Pose(direction, action, frame, name != "player")
    if name in ("player", "player-armed"):
        return adventurer(p)
    if name in ("warrior", "archer"):
        return skeleton(p, name == "warrior")
    return cultist(p)


def blacksmith(frame):
    image = new()
    d = ImageDraw.Draw(image)
    y = (0, -1, -1, 0, 1, 0)[frame]
    # Broad forearms, leather apron, beard, tied hair and a steel smithing hammer.
    for x in (19, 29):
        d.rectangle((x - 2, 28, x + 2, 35), fill="#473a34", outline=INK)
        d.line((x - 1, 33, x + 1, 33), fill="#ae875d")
    poly(d, [(17, 16 + y), (30, 16 + y), (33, 24 + y),
             (30, 29 + y), (17, 29 + y), (14, 23 + y)], "#686d64")
    limb(d, (16, 19 + y), (13, 26 + y), SKIN, 4, SKIN_LIGHT)
    limb(d, (31, 19 + y), (34, 24 + y), SKIN, 4, SKIN_LIGHT)
    poly(d, [(19, 18 + y), (28, 18 + y), (30, 32),
             (24, 33), (17, 32)], "#79523a")
    d.line((19, 20 + y, 18, 30), fill="#b18355", width=2)
    d.line((28, 21 + y, 29, 30), fill="#4a332b", width=2)
    d.rectangle((20, 25 + y, 27, 28 + y), fill="#4a332b")
    d.line((20, 25 + y, 27, 25 + y), fill="#b18355")
    poly(d, [(20, 8 + y), (26, 7 + y), (29, 10 + y),
             (29, 15 + y), (26, 19 + y), (20, 18 + y), (18, 13 + y)], SKIN)
    d.rectangle((20, 10 + y, 23, 13 + y), fill=SKIN_LIGHT)
    poly(d, [(19, 10 + y), (19, 7 + y), (25, 6 + y), (29, 8 + y),
             (29, 11 + y), (26, 9 + y), (22, 9 + y)], "#4e3530")
    d.line((20, 7 + y, 24, 7 + y), fill="#9f7d5c")
    poly(d, [(20, 14 + y), (23, 15 + y), (25, 14 + y), (28, 13 + y),
             (28, 18 + y), (24, 21 + y), (21, 19 + y)], "#594035")
    d.line((22, 16 + y, 23, 19 + y), fill="#a3815c")
    d.point((22, 12 + y), fill=INK)
    d.point((27, 12 + y), fill=INK)
    d.line((26, 14 + y, 28, 14 + y), fill=SKIN_DARK)
    hy = 17 + y + (0, 0, 1, 2, 1, 0)[frame]
    limb(d, (34, 25 + y), (36, hy), "#8f673e", 2)
    poly(d, [(32, hy - 4), (40, hy - 4), (41, hy), (32, hy)], "#747e81")
    d.line((33, hy - 3, 39, hy - 3), fill=STEEL)
    d.rectangle((32, hy - 2, 34, hy), fill="#444d54")
    return image
