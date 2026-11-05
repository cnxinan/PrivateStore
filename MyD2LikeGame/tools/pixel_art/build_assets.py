"""Deterministic, editable DarkLoot pixel atelier (Python 3.12 / Pillow 13.0.0).

From the MyD2LikeGame root:
    python tools\\pixel_art\\build_assets.py
    python -m unittest discover -s tools\\pixel_art -v

--output, --source and --previews accept explicit directories/files. Defaults
resolve relative to this script, never an installed generator or working folder.
Only the checked-in 64px crypt material is sampled; every actor and prop is
original coordinate art. Edit actors.py for poses/palettes, world.py for scenery.
Sheets contain cosmetic clips only: no gameplay times, combat or baked shadows.
Preview labels use Pillow's built-in font, never a game/runtime dependency.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw

from actors import Pose, blacksmith, equipment, new, render_actor
import world


HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
ACTORS = ("player", "player-armed", "slime", "wolf", "archer", "warrior", "cultist")
DIRECTIONS = ("E", "SE", "S", "SW", "W", "NW", "N", "NE")
ACTIONS = ("Idle", "Move", "Attack", "Hurt", "Dead")
RARITIES = ("common", "magic", "rare")
ATLAS = {"version": 1, "frameWidth": 48, "frameHeight": 48, "columns": 6,
         "directions": list(DIRECTIONS), "actions": list(ACTIONS),
         "originX": 24, "originY": 24, "scale": 2}


def actor_sheet(name):
    sheet = new((288, 1920))
    for action in range(5):
        for direction in range(8):
            for frame in range(6):
                sheet.alpha_composite(render_actor(name, direction, action, frame),
                                      (frame * 48, (action * 8 + direction) * 48))
    return sheet


def equipment_sheet(kind, rarity):
    sheet = new((288, 1920))
    for action in range(5):
        for direction in range(8):
            for frame in range(6):
                sheet.alpha_composite(equipment(kind, rarity, Pose(direction, action, frame, True)),
                                      (frame * 48, (action * 8 + direction) * 48))
    return sheet


def flash_mask(image):
    mask = Image.new("RGBA", image.size, (255, 255, 255, 0))
    mask.putalpha(image.getchannel("A"))
    return mask


def save(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=False, compress_level=9)


def cell(sheet, action, direction, frame=0):
    x, y = frame * 48, (action * 8 + direction) * 48
    return sheet.crop((x, y, x + 48, y + 48))


def preview_canvas(size):
    image = Image.new("RGB", size, "#20232c")
    d = ImageDraw.Draw(image)
    for y in range(0, size[1], 12):
        for x in range(0, size[0], 12):
            if (x // 12 + y // 12) % 2:
                d.rectangle((x, y, x + 11, y + 11), fill="#252a34")
    return image


def paste_zoom(canvas, image, xy, scale=2):
    image = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    canvas.paste(image, xy, image)


def previews(sheets, textures, output):
    output.mkdir(parents=True, exist_ok=True)
    directions = preview_canvas((900, 7 * 110 + 36))
    d = ImageDraw.Draw(directions)
    d.text((12, 9), "DARKLOOT / ORIGINAL PIXEL ATELIER / 8 authored views", fill="#c9bb93")
    for row, name in enumerate(ACTORS):
        d.text((8, 43 + row * 110), name, fill="#d9d4c7")
        for direction, label in enumerate(DIRECTIONS):
            x, y = 116 + direction * 96, 36 + row * 110
            paste_zoom(directions, cell(sheets[name], 0, direction), (x, y))
            d.text((x + 40, y + 94), label, fill="#b4bbc8")
    save(directions, output / "actor-directions.png")
    for name in ACTORS:
        contact = preview_canvas((660, 5 * 110 + 34))
        d = ImageDraw.Draw(contact)
        d.text((8, 8), name + " / SE / six frames per clip (2x nearest)", fill="#d7c69e")
        for action, label in enumerate(ACTIONS):
            y = 30 + action * 110
            d.text((5, y + 39), label, fill="#bbc3cf")
            for frame in range(6):
                paste_zoom(contact, cell(sheets[name], action, 1, frame), (80 + frame * 96, y))
        save(contact, output / f"{name}-actions.png")
    contact = preview_canvas((880, 6 * 108 + 36))
    d = ImageDraw.Draw(contact)
    d.text((8, 8), "PLAYER / matching body + armor + sword / SE attacks / rarity silhouettes",
           fill="#dfcea3")
    for row in range(6):
        rarity = RARITIES[row % 3]
        action = 0 if row < 3 else 2
        y = 30 + row * 108
        d.text((8, y + 35), f"{rarity}\n{ACTIONS[action]}", fill="#c6c2b5")
        for frame in range(6):
            body = cell(sheets["player-armed"], action, 1, frame)
            body.alpha_composite(cell(sheets[f"armor-{rarity}"], action, 1, frame))
            body.alpha_composite(cell(sheets[f"weapon-{rarity}"], action, 1, frame))
            paste_zoom(contact, body, (100 + frame * 122, y))
    save(contact, output / "equipment-composites.png")
    contact = preview_canvas((1050, 910))
    d = ImageDraw.Draw(contact)
    d.text((10, 8), "TERRAIN / 32 opaque 32px slots / cathedral, crypt, altar, wall, trim, corridor, cracks", fill="#d7c69e")
    paste_zoom(contact, textures["terrain"], (10, 28))
    for group, label in enumerate(("Cathedral", "Entry crypt", "Battle crypt", "Altar",
                                    "Wall stone", "Wall trim", "Corridor", "Cracks")):
        d.text((550, 35 + group * 28), f"{group * 4:02}-{group * 4 + 3:02}  {label}", fill="#c5c6c9")
    d.text((10, 300), "PROPS / transparent 64px slots / IDs left-to-right, top-to-bottom", fill="#d7c69e")
    paste_zoom(contact, textures["props"], (10, 324))
    for slot in range(16):
        d.text((14 + slot % 8 * 128, 326 + slot // 8 * 128), str(slot), fill="#c5c6c9")
    d.text((10, 600), "NEUTRAL PORTAL / six 40x56 frames", fill="#d7c69e")
    paste_zoom(contact, textures["portal"], (10, 622))
    d.text((530, 600), "TORCH / six 16x32 frames", fill="#d7c69e")
    paste_zoom(contact, textures["torch"], (530, 622), 3)
    d.text((530, 742), "SMITH / six 48x48 idle frames", fill="#d7c69e")
    paste_zoom(contact, textures["blacksmith"], (530, 765), 1)
    d.text((10, 770), "LIGHT / alpha-band preview", fill="#d7c69e")
    glow = textures["glow"].copy()
    glow_rgb = Image.new("RGBA", glow.size, "#f7ba68")
    glow_rgb.putalpha(glow.getchannel("A"))
    paste_zoom(contact, glow_rgb, (16, 792), 1)
    save(contact, output / "world-atlases.png")


def build(output, source, preview_output):
    sheets = {}
    for name in ACTORS:
        sheets[name] = actor_sheet(name)
        save(sheets[name], output / "Actors" / f"{name}.png")
        save(flash_mask(sheets[name]), output / "Actors" / f"{name}-flash.png")
    for kind in ("weapon", "armor"):
        for rarity in RARITIES:
            name = f"{kind}-{rarity}"
            sheets[name] = equipment_sheet(kind, rarity)
            save(sheets[name], output / "Equipment" / f"{name}.png")
            save(flash_mask(sheets[name]), output / "Equipment" / f"{name}-flash.png")
    textures = {"terrain": world.terrain(source), "props": world.props(),
                "portal": world.portal(), "torch": world.torch(), "glow": world.glow()}
    for name, image in textures.items():
        save(image, output / "World" / f"{name}.png")
    textures["blacksmith"] = new((288, 48))
    for frame in range(6):
        textures["blacksmith"].alpha_composite(blacksmith(frame), (frame * 48, 0))
    save(textures["blacksmith"], output / "NPC" / "blacksmith.png")
    (output / "atlas.json").write_text(json.dumps(ATLAS, separators=(",", ":")) + "\n", encoding="utf-8")
    previews(sheets, textures, preview_output)
    print(f"Built 32 runtime PNGs + atlas.json in {output}")
    print(f"10 contact sheets in {preview_output}; 3,120 authored actor/equipment cells")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", type=Path, default=PROJECT / "src" / "DarkLoot.Game" / "Content" / "Pixel")
    parser.add_argument("--source", type=Path, default=HERE / "source" / "crypt-floor-64.png")
    parser.add_argument("--previews", type=Path, default=HERE / "previews")
    args = parser.parse_args()
    build(args.output, args.source, args.previews)


if __name__ == "__main__":
    main()
