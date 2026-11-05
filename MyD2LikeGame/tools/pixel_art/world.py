"""Original carved gothic scenery, plus palette-edited local crypt material."""
import math

from PIL import Image, ImageDraw

from actors import INK, BRASS, GOLD, STEEL, new, poly


STONE = ("#30353f", "#4b5360", "#727d89", "#a0a9ad")


def stone_block(d, box, palette=STONE):
    x0, y0, x1, y1 = box
    dark, main, light, edge = palette
    d.rectangle(box, fill=main, outline=dark)
    d.line((x0 + 1, y1 - 1, x0 + 1, y0 + 1, x1 - 1, y0 + 1), fill=light)
    d.line((x1 - 1, y0 + 2, x1 - 1, y1 - 1, x0 + 2, y1 - 1), fill=dark)
    if x1 - x0 > 8:
        d.line((x0 + 3, y0 + 2, x0 + 6, y0 + 2), fill=edge)


def terrain(source):
    atlas = new((256, 128))
    with Image.open(source) as original:
        # Crop the supplied 64px material at its authored pixel scale, then remap
        # to five tones. Whole-image downsampling made the mortar too fine.
        material = original.convert("RGB").crop((16, 16, 48, 48))
    palettes = (
        ("#202730", "#39434e", "#535f6b", "#6f7a82", "#8e989c"),
        ("#1e262e", "#303f4b", "#465865", "#60717b", "#839197"),
        ("#25282d", "#3c4148", "#565d62", "#737a7b", "#969a94"),
        ("#271c24", "#422c35", "#5a3d45", "#78565b", "#957277"),
        ("#20252c", "#343b45", "#4d5865", "#6d7883", "#89949c"),
        ("#252a31", "#414955", "#65717d", "#8d979d", "#b1b8b4"),
        ("#24292d", "#3c474c", "#56626a", "#748088", "#959d9e"),
        ("#1f252c", "#36424b", "#535e66", "#737c83", "#939c9e"),
    )
    for slot in range(32):
        tile = new((32, 32))
        d = ImageDraw.Draw(tile)
        group, variant = slot // 4, slot % 4
        colors = palettes[group]
        d.rectangle((0, 0, 31, 31), fill=colors[0])
        if group in (1, 2):
            for y in range(32):
                for x in range(32):
                    r, g, b = material.getpixel(((x + variant * 7) % 32, (y + variant * 5) % 32))
                    brightness = (r * 3 + g * 6 + b) // 10
                    tone = min(4, brightness // 48)
                    d.point((x, y), fill=colors[tone])
            # Re-cut mortar and bevels into a readable, consistent scale.
            for yy in (0, 16, 31):
                d.line((0, yy, 31, yy), fill=colors[0])
                if yy < 31:
                    d.line((1, yy + 1, 30, yy + 1), fill=colors[3])
            for xx, yy in ((variant * 3 + 9, 0), ((variant * 3 + 25) % 32, 16)):
                d.line((xx, yy, xx, yy + 15), fill=colors[0])
                d.line((xx + 1, yy + 2, xx + 1, yy + 14), fill=colors[2])
        elif group == 4:
            for row in range(4):
                shift = ((row + variant) % 2) * 8
                for x in range(-16 + shift, 32, 16):
                    stone_block(d, (x, row * 8, x + 15, row * 8 + 7),
                                (colors[0], colors[1 + (row + variant) % 2], colors[3], colors[4]))
            d.point((5 + variant * 5, 6), fill=colors[4])
        elif group == 5:
            for yy in (0, 7, 24):
                stone_block(d, (0, yy, 31, min(31, yy + (6 if yy != 7 else 16))),
                            (colors[0], colors[2], colors[3], colors[4]))
            for x in range(3 + variant * 2, 32, 8):
                poly(d, [(x, 12), (x + 3, 16), (x, 20), (x - 3, 16)], colors[1], colors[3])
            d.line((0, 3, 31, 3), fill=colors[4])
            d.line((0, 27, 31, 27), fill=colors[4])
        else:
            for row in range(2):
                shift = ((row + variant) % 2) * 16
                for x in range(-32 + shift, 32, 32):
                    stone_block(d, (x, row * 16, x + 31, row * 16 + 15),
                                (colors[0], colors[2], colors[3], colors[4]))
                    d.rectangle((x + 4, row * 16 + 4, x + 23, row * 16 + 10), fill=colors[2])
            if group == 0:
                # Cathedral's restrained inlaid corner fragments.
                for x, y in ((4, 4), (27, 27)):
                    d.line((x, y + 3, x, y, x + 3, y), fill="#8d7b55")
            elif group == 3:
                d.line((2, 2, 29, 2, 29, 29), fill="#8b6860")
                if variant in (0, 3):
                    poly(d, [(16, 9), (21, 16), (16, 23), (11, 16)], "#643b43", "#9d7768")
            elif group == 6:
                d.line((3, 1, 3, 30), fill=colors[1])
                d.line((28, 1, 28, 30), fill=colors[1])
        # Sparse chisel chips, not noisy per-pixel spray.
        for i in range(5):
            x = (slot * 7 + i * 11) % 28 + 2
            y = (slot * 13 + i * 7) % 28 + 2
            d.line((x, y, min(30, x + 1 + i % 2), y), fill=colors[1 if i % 2 else 3])
        if group == 7 or variant == 3:
            x = 7 + variant * 4
            path = [(x, 2), (x - 2, 9), (x + 2, 15), (x, 21), (x + 4, 28)]
            d.line(path, fill=colors[0])
            d.line([(x + 1, 15), (x + 6, 12), (x + 8, 8)], fill=colors[0])
            d.line((x + 1, 22, x + 4, 27), fill=colors[3])
            if variant == 2:
                d.rectangle((21, 25, 23, 26), fill="#666b5b")
            if variant == 3:
                d.line((8, 25, 14, 23, 16, 25), fill="#776957")
        atlas.alpha_composite(tile, ((slot % 8) * 32, (slot // 8) * 32))
    return atlas


def column(d, crypt=False):
    colors = ("#292f37", "#434d59", "#677481", "#909caa") if crypt else STONE
    stone_block(d, (15, 50, 49, 58), colors)
    stone_block(d, (19, 46, 45, 50), colors)
    poly(d, [(22, 16), (42, 16), (41, 46), (23, 46)], colors[1], colors[0])
    d.rectangle((24, 17, 27, 45), fill=colors[2])
    d.line((25, 19, 25, 43), fill=colors[3])
    d.line((31, 18, 31, 45), fill=colors[0])
    d.line((35, 18, 35, 45), fill=colors[2])
    d.line((39, 18, 39, 45), fill=colors[0])
    stone_block(d, (18, 11, 46, 17), colors)
    stone_block(d, (15, 6, 49, 11), colors)
    for x in (20, 32, 44):
        poly(d, [(x - 3, 12), (x, 15), (x + 3, 12), (x, 10)], colors[2], colors[0])
    if crypt:
        poly(d, [(28, 8), (29, 4), (35, 4), (37, 8), (35, 12), (30, 12)], "#a19f89")
        d.point((30, 8), fill=INK)
        d.point((34, 8), fill=INK)
        d.point((32, 11), fill=INK)
        d.line((39, 30, 35, 34, 38, 40), fill=colors[0])
    else:
        d.line((28, 53, 32, 55, 36, 53), fill="#9d8b63")
        d.point((32, 54), fill=GOLD)


def candle(d, x, base, height, phase=0):
    d.rectangle((x - 2, base - height, x + 2, base), fill="#a99872", outline=INK)
    d.line((x - 1, base - height + 1, x - 1, base - 1), fill="#e2d5a5")
    d.point((x + 1, base - height + 3), fill="#d2c49b")
    poly(d, [(x, base - height - 7), (x + 2, base - height - 3),
             (x + 1, base - height), (x - 2, base - height - 2)], "#d17b32", None)
    d.line((x, base - height - 4, x, base - height - 1), fill="#ffe5a0")


def prop(slot):
    image = new((64, 64))
    d = ImageDraw.Draw(image)
    if slot in (0, 9):
        column(d, slot == 9)
    elif slot == 1:
        # Vaulted charcoal furnace; fire remains inside a carved stone mouth.
        stone_block(d, (7, 46, 57, 59))
        poly(d, [(11, 48), (11, 20), (20, 9), (44, 9), (53, 20), (53, 48)], STONE[1])
        stone_block(d, (22, 4, 42, 13))
        d.rectangle((25, 5, 28, 11), fill=STONE[2])
        poly(d, [(16, 46), (16, 28), (21, 20), (32, 16), (43, 20),
                 (48, 28), (48, 46)], "#1c1c24", STONE[3])
        for x, y in ((16, 20), (23, 15), (34, 14), (44, 20)):
            d.line((x, y, x + 4, y + 3), fill=STONE[2], width=2)
        d.rectangle((17, 39, 47, 45), fill="#632e25")
        for i, x in enumerate((21, 27, 33, 39, 44)):
            poly(d, [(x - 3, 43), (x - 2, 32 - i % 3 * 3), (x, 26 + i % 2 * 6),
                     (x + 2, 36), (x + 3, 43)], "#c46b32", None)
            d.line((x, 35, x, 42), fill="#ffca65", width=2)
        for x in range(18, 48, 6):
            d.rectangle((x, 42, x + 2, 48), fill="#4a4140")
            d.point((x, 43), fill="#b68e59")
        stone_block(d, (8, 49, 56, 53))
    elif slot == 2:
        stone_block(d, (16, 49, 49, 57), ("#302924", "#55473a", "#806b4f", "#a38a60"))
        poly(d, [(23, 49), (26, 34), (20, 29), (14, 28), (7, 23),
                 (10, 20), (50, 20), (55, 23), (51, 29), (40, 31),
                 (37, 37), (41, 49)], "#5d6871")
        poly(d, [(9, 21), (49, 21), (52, 23), (44, 25), (16, 25)], STEEL)
        d.line((24, 31, 40, 31), fill="#9da8ac")
        d.line((28, 35, 26, 46, 38, 46), fill="#89949a")
        d.rectangle((43, 22, 46, 24), fill="#424952")
        d.line((12, 27, 20, 29), fill="#35414b")
    elif slot == 3:
        for x in (12, 46):
            poly(d, [(x, 33), (x + 6, 33), (x + 5, 56), (x - 1, 56)], "#674d36")
            d.line((x + 1, 37, x, 53), fill="#a78253")
        d.rectangle((12, 44, 51, 48), fill="#46382b", outline=INK)
        poly(d, [(7, 26), (49, 26), (57, 32), (55, 37), (7, 37)], "#8c6946")
        for yy in (29, 33):
            d.line((9, yy, 51, yy), fill="#c19b63")
        d.line((27, 27, 28, 36), fill="#4d3a2f")
        poly(d, [(17, 24), (25, 24), (27, 28), (16, 28)], "#b9b08a")
        d.line((34, 25, 42, 31), fill="#48505a", width=2)
        d.rectangle((38, 22, 44, 25), fill=STEEL, outline=INK)
        d.line((12, 36, 52, 36), fill="#46382b")
    elif slot == 4:
        for x in (12, 49):
            stone_block(d, (x, 7, x + 4, 57), ("#2c2524", "#594632", "#8c6b45", "#b48a52"))
        for y in (14, 39):
            d.rectangle((11, y, 54, y + 4), fill="#745938", outline=INK)
            d.line((13, y + 1, 52, y + 1), fill="#b2915e")
        for x in (22, 33, 44):
            d.line((x, 19, x, 44), fill=INK, width=3)
            d.line((x, 19, x, 44), fill="#a78453")
        d.rectangle((18, 20, 26, 26), fill="#7b858b", outline=INK)
        d.line((19, 21, 25, 21), fill=STEEL)
        poly(d, [(31, 22), (31, 13), (35, 13), (35, 22)], "#8c969c")
        d.line((30, 14, 30, 20), fill=STEEL)
        poly(d, [(40, 23), (44, 18), (48, 23), (47, 30), (41, 30)], "#69777e")
        d.line((42, 23, 44, 20, 46, 23), fill=STEEL)
    elif slot in (5, 6):
        main, dark, light = ("#365b83", "#24374f", "#7098b3") if slot == 5 else ("#813c46", "#452b36", "#ba6c62")
        d.line((10, 8, 54, 8), fill=INK, width=4)
        d.line((10, 7, 54, 7), fill=BRASS, width=2)
        for x in (12, 52):
            d.rectangle((x - 1, 6, x + 1, 9), fill=GOLD)
        poly(d, [(17, 10), (47, 10), (47, 46), (39, 55), (32, 51),
                 (25, 55), (17, 47)], main)
        d.rectangle((19, 11, 21, 45), fill=light)
        d.rectangle((42, 11, 45, 46), fill=dark)
        d.line((28, 12, 27, 45), fill=dark)
        d.line((32, 15, 33, 47), fill=light)
        d.line((20, 46, 25, 51, 32, 47, 39, 51, 44, 46), fill=BRASS)
        poly(d, [(32, 21), (37, 29), (32, 37), (27, 29)], BRASS)
        d.line((32, 24, 32, 33), fill=GOLD)
        d.line((29, 28, 35, 28), fill=GOLD)
    elif slot == 7:
        outer = [(9, 59), (9, 24), (15, 15), (24, 8), (32, 3),
                 (40, 8), (49, 15), (55, 24), (55, 59)]
        poly(d, outer, STONE[1], STONE[0])
        inner = [(15, 55), (15, 26), (21, 18), (32, 9),
                 (43, 18), (49, 26), (49, 55)]
        poly(d, inner, "#203c65", STONE[3])
        # Original lancet tracery with discrete cobalt/ice-blue glass shards.
        for side in (-1, 1):
            cx = 32 + side * 9
            poly(d, [(cx, 21), (cx + 6, 28), (cx + 6, 51),
                     (cx - 6, 51), (cx - 6, 28)], "#406b9e", "#101e33")
            for yy in (29, 41):
                poly(d, [(cx, yy - 6), (cx + 5, yy), (cx, yy + 6),
                         (cx - 5, yy)], "#78adce", "#1a2a46")
                d.point((cx - 1, yy - 2), fill="#c4e1e4")
            d.line((cx - 5, 36, cx + 5, 36), fill="#b9a16c")
        poly(d, [(32, 12), (38, 19), (32, 25), (26, 19)], "#8fb9d8", "#17273f")
        poly(d, [(32, 15), (35, 19), (32, 22), (29, 19)], "#c5e1e8", "#466b93")
        d.line((32, 26, 32, 54), fill=STONE[2], width=3)
        stone_block(d, (8, 55, 56, 61))
        for x, y in ((11, 24), (17, 15), (24, 9), (37, 7), (46, 14), (52, 24)):
            d.line((x, y, x + 3, y + 3), fill=STONE[3])
    elif slot == 8:
        poly(d, [(9, 23), (42, 18), (56, 28), (54, 49),
                 (21, 56), (9, 47)], "#454c55")
        poly(d, [(12, 24), (42, 20), (53, 28), (21, 34)], "#828b91")
        poly(d, [(12, 29), (21, 35), (21, 52), (12, 46)], "#555f69")
        poly(d, [(23, 36), (52, 31), (51, 46), (23, 52)], "#343c47")
        d.line((24, 38, 49, 34), fill="#8c979b")
        d.line((26, 48, 48, 43), fill="#555f69")
        # Sculpted recumbent effigy on the bevelled lid.
        poly(d, [(23, 24), (26, 21), (30, 23), (29, 26), (25, 27)], "#bbb8a0")
        poly(d, [(29, 25), (37, 23), (46, 27), (37, 30)], "#9c9e93")
        d.line((32, 26, 39, 28), fill="#d0cbb3")
        d.line((38, 23, 39, 28, 44, 29), fill="#626c72")
        d.line((14, 40, 17, 44, 15, 49), fill="#282d37")
    elif slot == 10:
        stone_block(d, (10, 49, 54, 58))
        stone_block(d, (16, 31, 48, 49))
        poly(d, [(20, 32), (44, 32), (44, 49), (32, 46), (20, 49)], "#672e3e")
        d.line((22, 33, 22, 46), fill="#a16062")
        d.line((42, 33, 42, 46), fill="#3c2231")
        d.line((22, 46, 32, 43, 42, 46), fill=BRASS)
        poly(d, [(32, 35), (36, 39), (32, 43), (28, 39)], BRASS)
        stone_block(d, (9, 26, 55, 32))
        candle(d, 18, 25, 8)
        candle(d, 46, 25, 11)
        poly(d, [(26, 22), (31, 20), (38, 23), (37, 26), (26, 25)], "#9c8b73")
        d.line((31, 21, 31, 25), fill="#544d46")
        d.line((33, 22, 36, 23), fill="#665a4d")
    elif slot in (11, 12):
        # Slot12 is genuinely flat: no floating bars in the "open" state.
        stone_block(d, (5, 51, 59, 59))
        d.line((8, 54, 56, 54), fill="#859198")
        if slot == 11:
            for x in range(11, 55, 7):
                d.rectangle((x, 13, x + 2, 52), fill="#3a454f", outline=INK)
                d.line((x + 1, 14, x + 1, 50), fill="#88979f")
                poly(d, [(x - 1, 14), (x + 1, 8), (x + 3, 14)], "#b3b8ad")
            for yy in (22, 42):
                d.rectangle((8, yy, 56, yy + 3), fill="#4d5b66", outline=INK)
                for x in range(11, 55, 7):
                    d.point((x + 1, yy + 1), fill=BRASS)
            d.rectangle((29, 31, 36, 37), fill=BRASS, outline=INK)
            d.rectangle((32, 33, 33, 35), fill=INK)
        else:
            for x in range(11, 55, 7):
                d.rectangle((x, 51, x + 2, 53), fill="#292e36")
            d.line((13, 57, 52, 57), fill="#a89b77")
    elif slot == 13:
        for x, b, h in ((23, 51, 15), (32, 49, 24), (42, 53, 19)):
            d.ellipse((x - 5, b - 1, x + 5, b + 3), fill="#594f3b", outline=INK)
            candle(d, x, b, h)
        d.rectangle((29, 52, 34, 54), fill="#9e8a61")
    elif slot == 14:
        poly(d, [(11, 22), (42, 18), (54, 27), (53, 51), (22, 56),
                 (11, 47)], "#59422e")
        poly(d, [(12, 22), (42, 19), (52, 27), (22, 32)], "#a37a49")
        poly(d, [(22, 34), (51, 29), (50, 49), (23, 54)], "#82613d")
        for x in (26, 34, 42):
            d.line((x, 34 - (x - 22) // 6, x, 52 - (x - 22) // 6), fill="#49382a")
        d.line((25, 36, 47, 47), fill="#c0965f", width=3)
        d.line((26, 51, 48, 32), fill="#c0965f", width=3)
        for x, y in ((24, 34), (48, 30), (24, 52), (48, 48)):
            d.point((x, y), fill=STEEL)
        d.line((13, 25, 13, 45), fill="#b58c56", width=2)
        d.line((18, 29, 18, 49), fill="#302b25")
    elif slot == 15:
        # A transparent floor inlay, not a raised disk or a baked ground shadow.
        ring = [(32, 7), (48, 13), (56, 30), (50, 47), (32, 56),
                (14, 48), (7, 31), (14, 14), (32, 7)]
        d.line(ring, fill="#554932", width=3)
        d.line(ring, fill=BRASS, width=1)
        poly(d, [(32, 13), (37, 26), (49, 31), (37, 36), (32, 50),
                 (27, 36), (14, 31), (27, 26)], BRASS, "#574b35")
        poly(d, [(32, 19), (35, 28), (43, 31), (35, 34), (32, 43),
                 (29, 34), (21, 31), (29, 28)], "#514735", GOLD)
        d.line((32, 24, 32, 38), fill=GOLD)
        d.line((27, 29, 37, 29), fill=GOLD)
        for x, y in ((32, 9), (53, 31), (32, 53), (10, 31)):
            d.point((x, y), fill="#e0c57c")
    return image


def props():
    atlas = new((512, 128))
    for slot in range(16):
        atlas.alpha_composite(prop(slot), ((slot % 8) * 64, (slot // 8) * 64))
    return atlas


def portal():
    atlas = new((240, 56))
    for frame in range(6):
        image = new((40, 56))
        d = ImageDraw.Draw(image)
        poly(d, [(3, 52), (3, 23), (7, 14), (13, 7), (20, 2),
                 (27, 7), (33, 14), (37, 23), (37, 52)], "#6b6d73", "#333741")
        poly(d, [(8, 51), (8, 25), (11, 18), (20, 9), (29, 18),
                 (32, 25), (32, 51)], "#202531", "#c5c6c7")
        # These remain neutral so a single atlas can be tinted gold or cyan.
        inner = [(10, 49), (10, 26), (13, 20), (20, 13),
                 (27, 20), (30, 26), (30, 49)]
        d.line(inner, fill="#faf8ef", width=2)
        d.line([(12, 47), (12, 27), (15, 22), (20, 17),
                (25, 22), (28, 27), (28, 47)], fill="#898e97", width=1)
        for y in (27, 36, 45):
            d.line((4, y, 7, y), fill="#c4c6c6")
            d.line((33, y, 36, y), fill="#a3a8ac")
            d.point((5, y + 1), fill="#4a4e56")
        for x, y in ((8, 15), (13, 9), (24, 7), (30, 14)):
            d.line((x, y, x + 3, y + 3), fill="#c6c6c4")
        for i in range(8):
            yy = 47 - ((i * 7 + frame * 3) % 28)
            xx = 14 + (i * 5 + frame) % 13
            d.point((xx, yy), fill=("#7b808a", "#a2a6ae", "#d6d9db")[(i + frame) % 3])
            if i % 3 == frame % 3:
                d.point((xx, yy - 1), fill="#e6e9e9")
        stone_block(d, (2, 51, 38, 54), ("#353941", "#777d84", "#b6bbbd", "#dde0df"))
        atlas.alpha_composite(image, (frame * 40, 0))
    return atlas


def torch():
    atlas = new((96, 32))
    for frame in range(6):
        image = new((16, 32))
        d = ImageDraw.Draw(image)
        d.rectangle((6, 21, 9, 29), fill="#322a28", outline=INK)
        d.line((7, 22, 7, 28), fill="#9c7a4b")
        d.rectangle((4, 20, 11, 23), fill="#74614a", outline=INK)
        d.line((5, 21, 10, 21), fill=BRASS)
        lean = (-1, 0, 1, 2, 0, -2)[frame]
        poly(d, [(5, 20), (3, 15), (5, 10), (7 + lean, 3 + frame % 3),
                 (8 + lean, 10), (11, 13), (12, 17), (10, 20)], "#a14c2b", None)
        poly(d, [(6, 20), (5, 15), (7, 11), (8 + lean, 8),
                 (9, 14), (11, 17), (9, 21)], "#ed943c", None)
        poly(d, [(7, 20), (6, 17), (8, 13), (9, 18), (9, 20)], "#ffe6a0", None)
        d.point((max(2, 7 + lean), 2 + frame % 2), fill="#d58737")
        atlas.alpha_composite(image, (frame * 16, 0))
    return atlas


def glow():
    image = Image.new("RGBA", (128, 128), (255, 255, 255, 0))
    d = ImageDraw.Draw(image)
    for y in range(0, 128, 2):
        for x in range(0, 128, 2):
            distance = math.hypot(x + 1 - 64, y + 1 - 64) / 63
            # Gentle, quantised falloff; deliberately not an opaque additive sun.
            alpha = max(0, round((max(0, 1 - distance) ** 2) * 128 / 8) * 8)
            d.rectangle((x, y, x + 1, y + 1), fill=(255, 255, 255, alpha))
    return image
