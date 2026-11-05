using System;
using System.IO;
using System.Linq;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal static class NewEnemyVisualSmoke
{
    internal static void Run(GraphicsDevice device, ShapeRenderer shapes, SpriteFont font, PixelContent pixelsContent)
    {
        using var target = new RenderTarget2D(device, 800, 420);
        using var batch = new SpriteBatch(device);
        var renderer = new ActorRenderer(pixelsContent);
        var previous = device.GetRenderTargets();
        try
        {
            device.SetRenderTarget(target);
            device.Clear(new Color(24, 25, 30));
            batch.Begin(samplerState: SamplerState.PointClamp);
            var kinds = new[] { ActorVisualKind.Warrior, ActorVisualKind.Cultist };
            for (var row = 0; row < kinds.Length; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var animator = new ActorAnimator(kinds[row]);
                    var input = new ActorAnimationInput(column == 1, false, column == 3, false,
                        row == 1 && column == 2, .75f, row == 0 && column == 2, NVector.UnitX);
                    animator.Update(input, column == 3 ? .15f : .08f);
                    var pose = animator.Pose with { Scale = animator.Pose.Scale * 2 };
                    batch.DrawString(font, $"{kinds[row]} {new[] { "Idle", "Move", "Attack", "Death" }[column]}",
                        new Vector2(column * 200 + 8, row * 170 + 8), Color.Wheat);
                    renderer.Draw(batch, shapes, kinds[row], new Vector2(column * 200 + 90, row * 170 + 100), pose,
                        frame: animator.Frame, sizeScale: 2);
                }
            }
            batch.DrawString(font, "Arrow", new Vector2(220, 350), Color.Wheat);
            batch.DrawString(font, "Magic bolt", new Vector2(470, 350), Color.Wheat);
            renderer.DrawProjectile(batch, shapes, new(300, 390), Vector2.UnitX, ActorVisualKind.Archer);
            renderer.DrawProjectile(batch, shapes, new(550, 390), Vector2.UnitX, ActorVisualKind.Cultist);
            batch.End();
        }
        finally
        {
            device.SetRenderTargets(previous);
        }
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        Color[] Cell(int row, int column) => Enumerable.Range(row * 170 + 35, 125)
            .SelectMany(y => Enumerable.Range(column * 200 + 10, 180).Select(x => pixels[y * 800 + x])).ToArray();
        for (var row = 0; row < 2; row++)
        {
            var idle = Cell(row, 0);
            if (Enumerable.Range(1, 3).Any(column => idle.SequenceEqual(Cell(row, column))))
                throw new InvalidOperationException($"New enemy Draw has indistinguishable states: row={row}.");
        }
        if (!Cell(0, 0).Contains(new Color(163, 128, 65)) ||
            !Cell(0, 0).Contains(new Color(202, 195, 164)) ||
            !Enumerable.Range(40, 55).SelectMany(y => Enumerable.Range(110, 40)
                .Select(x => pixels[y * 800 + x])).Contains(new Color(179, 191, 197)) ||
            !Cell(1, 0).Contains(new Color(75, 35, 91)) ||
            !Cell(1, 0).Contains(Color.MediumPurple) ||
            pixels[390 * 800 + 550] != new Color(227, 208, 255) ||
            !Enumerable.Range(280, 20).Any(x => pixels[390 * 800 + x] == Color.BurlyWood))
            throw new InvalidOperationException("New enemy Draw lacks bone/shield, robe/staff or distinct arrow/magic pixels.");
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "new-enemy-poses.png"));
        target.SaveAsPng(file, target.Width, target.Height);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "dungeon-smoke.log"),
            $"PASS actual ActorRenderer draws sword/shield warrior, robed/staff cultist, distinct poses and magic bolt{Environment.NewLine}");
    }
}
