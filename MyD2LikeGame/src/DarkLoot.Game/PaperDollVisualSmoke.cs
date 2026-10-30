using System;
using System.IO;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal static class PaperDollVisualSmoke
{
    internal static void Run(GraphicsDevice device, ShapeRenderer shapes, SpriteFont font,
        GameContent content, string directory)
    {
        using var target = new RenderTarget2D(device, 800, 640);
        using var batch = new SpriteBatch(device);
        var renderer = new ActorRenderer();
        var factory = new EquipmentFactory();
        var random = new Random(73);
        var pose = new ActorPose(NVector.Zero, new NVector(2), 0, 1, 0, 0, 0, NVector.UnitX);
        var previous = device.GetRenderTargets();
        try
        {
            device.SetRenderTarget(target);
            device.Clear(new Color(24, 25, 30));
            batch.Begin(samplerState: SamplerState.PointClamp);
            for (var row = 0; row < 3; row++)
            {
                var rarity = (ItemRarity)row;
                for (var column = 0; column < 4; column++)
                {
                    var arena = new ArenaSimulation(content, random);
                    Equip(arena, factory.Create("weapon.training_blade", rarity, content, random), (column & 1) != 0);
                    Equip(arena, factory.Create("armor.training_vest", rarity, content, random), (column & 2) != 0);
                    var caption = $"{rarity}: {new[] { "Base", "Weapon", "Armor", "Both" }[column]}";
                    batch.DrawString(font, caption, new Vector2(column * 200 + 8, row * 160 + 8), Color.Wheat);
                    renderer.Draw(batch, shapes, ActorVisualKind.Player,
                        new Vector2(column * 200 + 90, row * 160 + 96), pose,
                        appearance: PlayerAppearance.From(arena.Player.Inventory));
                }
            }
            var directions = new[] { -NVector.UnitY, NVector.UnitX, NVector.UnitY, -NVector.UnitX };
            for (var column = 0; column < 4; column++)
            {
                batch.DrawString(font, new[] { "Back", "Right", "Front", "Left" }[column],
                    new Vector2(column * 200 + 8, 488), Color.Wheat);
                renderer.Draw(batch, shapes, ActorVisualKind.Player,
                    new Vector2(column * 200 + 90, 576), pose with { Facing = directions[column] },
                    appearance: new(ItemRarity.Rare, ItemRarity.Rare));
            }
            batch.End();
        }
        finally
        {
            device.SetRenderTargets(previous);
        }
        var pixels = new Color[800 * 640];
        target.GetData(pixels);
        for (var row = 0; row < 3; row++)
        {
            var cells = Enumerable.Range(0, 4).Select(column =>
                Enumerable.Range(row * 160 + 40, 120).SelectMany(y =>
                    Enumerable.Range(column * 200, 200).Select(x => pixels[y * 800 + x])).ToArray()).ToArray();
            if (cells.Skip(1).Any(cell => cell.SequenceEqual(cells[0])) ||
                cells[1].SequenceEqual(cells[3]) || cells[2].SequenceEqual(cells[3]) ||
                cells.Any(cell => !cell.Contains(new Color(190, 143, 105))))
                throw new InvalidOperationException($"Paper-doll Draw: {row} equipped variants failed visible layer acceptance.");
        }
        using var file = File.Create(Path.Combine(directory, "paperdoll-equipment.png"));
        target.SaveAsPng(file, target.Width, target.Height);
        File.AppendAllText(Path.Combine(directory, "smoke-test.log"),
            $"PASS actual ActorRenderer renders base/weapon/armor/both for all three rarities and four facings{Environment.NewLine}");
    }

    private static void Equip(ArenaSimulation arena, ItemInstance item, bool equip)
    {
        if (arena.EquipmentOperations.PickUp(arena.Player, item) != EquipmentChangeResult.Applied ||
            equip && arena.EquipmentOperations.Equip(arena.Player, item.InstanceId) != EquipmentChangeResult.Applied)
            throw new InvalidOperationException("Paper-doll Draw: cannot prepare owned equipment through inventory services.");
    }
}
