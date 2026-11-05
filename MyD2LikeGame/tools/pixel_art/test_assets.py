"""Run from the project root: python -m unittest discover -s tools\\pixel_art -v."""
import hashlib
import unittest
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[2] / "src" / "DarkLoot.Game" / "Content" / "Pixel"
ACTORS = ("player", "player-armed", "slime", "wolf", "archer", "warrior", "cultist")
EQUIPMENT = tuple(f"{kind}-{rarity}" for kind in ("weapon", "armor")
                  for rarity in ("common", "magic", "rare"))


def frame(image, action=0, direction=2, index=0):
    x, y = index * 48, (action * 8 + direction) * 48
    return image.crop((x, y, x + 48, y + 48))


class PixelAssetTests(unittest.TestCase):
    def test_all_actor_frames_are_nonempty_padded_hard_pixels(self):
        for name in ACTORS:
            with self.subTest(actor=name), Image.open(ROOT / "Actors" / f"{name}.png") as sheet:
                self.assertEqual(("RGBA", (288, 1920)), (sheet.mode, sheet.size))
                for action in range(5):
                    for direction in range(8):
                        for index in range(6):
                            cell = frame(sheet, action, direction, index)
                            bounds = cell.getbbox()
                            self.assertIsNotNone(bounds)
                            self.assertGreaterEqual(min(bounds[:2]), 1)
                            self.assertLessEqual(max(bounds[2:]), 47)
                            self.assertEqual({0, 255}, set(cell.getchannel("A").get_flattened_data()))

    def test_directions_and_action_poses_are_distinct(self):
        for name in ACTORS:
            with self.subTest(actor=name), Image.open(ROOT / "Actors" / f"{name}.png") as sheet:
                views = {frame(sheet, direction=d).tobytes() for d in range(8)}
                self.assertEqual(8, len(views))
                for action in (1, 2, 3):
                    poses = {frame(sheet, action=action, index=f).tobytes() for f in range(6)}
                    self.assertGreaterEqual(len(poses), 4, (name, action))
                if name != "slime":
                    l, t, r, b = frame(sheet, action=4, index=5).getbbox()
                    self.assertGreater(r - l, b - t, name)

    def test_player_torso_alignment_and_equipment_attachment(self):
        from actors import Pose

        with Image.open(ROOT / "Actors" / "player.png") as bare, \
                Image.open(ROOT / "Actors" / "player-armed.png") as armed:
            for action in (0, 1, 3, 4):
                self.assertEqual(frame(bare, action).tobytes(), frame(armed, action).tobytes())
            self.assertNotEqual(frame(bare, 2, index=3).tobytes(),
                                frame(armed, 2, index=3).tobytes())
            for rarity in ("common", "magic", "rare"):
                with Image.open(ROOT / "Equipment" / f"armor-{rarity}.png") as armor, \
                        Image.open(ROOT / "Equipment" / f"weapon-{rarity}.png") as weapon:
                    for action in range(4):
                        for direction in range(8):
                            for index in range(6):
                                body = frame(armed, action, direction, index)
                                chest = frame(armor, action, direction, index)
                                blade = frame(weapon, action, direction, index)
                                self.assertIsNotNone(ImageChops.multiply(
                                    body.getchannel("A"), chest.getchannel("A")).getbbox())
                                # The hilt must be within one pixel of the actual hand/body.
                                from PIL import ImageFilter
                                reach = body.getchannel("A").filter(ImageFilter.MaxFilter(3))
                                self.assertIsNotNone(ImageChops.multiply(
                                    reach, blade.getchannel("A")).getbbox())
                                pose = Pose(direction, action, index, True)
                                # Raised sword grips must not disappear under a pauldron.
                                hx, hy = pose.hand
                                self.assertIsNone(chest.crop((hx - 1, hy - 1, hx + 2, hy + 2)).getbbox(),
                                                  (rarity, action, direction, index))
                    self.assertIsNone(frame(armor).crop((0, 0, 48, 16)).getbbox())

    def test_rarities_change_silhouette_and_palette(self):
        for kind in ("weapon", "armor"):
            images = [Image.open(ROOT / "Equipment" / f"{kind}-{r}.png")
                      for r in ("common", "magic", "rare")]
            try:
                self.assertEqual(3, len({image.tobytes() for image in images}))
                self.assertEqual(3, len({image.getchannel("A").tobytes() for image in images}))
                self.assertIn((66, 104, 158, 255), set(images[1].get_flattened_data()))
            finally:
                for image in images:
                    image.close()

    def test_flash_masks_preserve_every_alpha(self):
        for folder, names in (("Actors", ACTORS), ("Equipment", EQUIPMENT)):
            for name in names:
                with self.subTest(sheet=name), \
                        Image.open(ROOT / folder / f"{name}.png") as original, \
                        Image.open(ROOT / folder / f"{name}-flash.png") as flash:
                    self.assertEqual(original.size, flash.size)
                    self.assertEqual(original.getchannel("A").tobytes(),
                                     flash.getchannel("A").tobytes())
                    self.assertEqual({(255, 255, 255)}, set(flash.convert("RGB").get_flattened_data()))

    def test_world_slots_portal_and_light_alpha(self):
        for name, size, cell in (("terrain", (256, 128), (32, 32)),
                                 ("props", (512, 128), (64, 64)),
                                 ("portal", (240, 56), (40, 56)),
                                 ("torch", (96, 32), (16, 32))):
            with self.subTest(atlas=name), Image.open(ROOT / "World" / f"{name}.png") as atlas:
                self.assertEqual(size, atlas.size)
                slots = []
                for y in range(0, size[1], cell[1]):
                    for x in range(0, size[0], cell[0]):
                        tile = atlas.crop((x, y, x + cell[0], y + cell[1]))
                        self.assertIsNotNone(tile.getbbox())
                        slots.append(tile.tobytes())
                        if name == "terrain":
                            self.assertEqual((255, 255), tile.getchannel("A").getextrema())
                        else:
                            self.assertEqual(0, tile.getpixel((0, 0))[3])
                self.assertEqual(len(slots), len(set(slots)))
        with Image.open(ROOT / "World" / "glow.png") as glow:
            self.assertEqual((128, 128), glow.size)
            self.assertEqual({(255, 255, 255)}, set(glow.convert("RGB").get_flattened_data()))
            self.assertGreater(glow.getpixel((64, 64))[3], glow.getpixel((32, 64))[3])
            self.assertEqual(0, glow.getpixel((0, 0))[3])
        with Image.open(ROOT / "NPC" / "blacksmith.png") as smith:
            self.assertEqual((288, 48), smith.size)
            self.assertGreater(len({smith.crop((f * 48, 0, f * 48 + 48, 48)).tobytes()
                                   for f in range(6)}), 2)

    def test_builder_reproduces_authored_pixels(self):
        import build_assets
        image = build_assets.actor_sheet("warrior")
        with Image.open(ROOT / "Actors" / "warrior.png") as saved:
            self.assertEqual(hashlib.sha256(image.tobytes()).digest(),
                             hashlib.sha256(saved.tobytes()).digest())


if __name__ == "__main__":
    unittest.main()
