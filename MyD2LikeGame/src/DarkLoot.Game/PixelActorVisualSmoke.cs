using System;
using System.IO;
using System.Linq;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal static class PixelActorVisualSmoke
{
    internal static void Run(GraphicsDevice device, ShapeRenderer shapes, PixelContent content, SpriteFont font)
    {
        const int cell = 128;
        using var target = new RenderTarget2D(device, 1024, 1024);
        using var batch = new SpriteBatch(device);
        var renderer = new ActorRenderer(content);
        var background = new Color(24, 25, 30);
        var directions = new[]
        {
            new NVector(1, 0), new(1, 1), new(0, 1), new(-1, 1),
            new(-1, 0), new(-1, -1), new(0, -1), new(1, -1)
        };
        var names = new[] { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };
        var kinds = Enum.GetValues<ActorVisualKind>();
        var previous = device.GetRenderTargets();
        try
        {
            device.SetRenderTarget(target);
            device.Clear(background);
            batch.Begin(samplerState: SamplerState.PointClamp);
            for (var row = 0; row < kinds.Length; row++)
                for (var column = 0; column < 8; column++)
                {
                    var animator = new ActorAnimator(kinds[row]);
                    animator.Update(new(false, false, false, false, false, 0, false, directions[column]), 0);
                    renderer.Draw(batch, shapes, kinds[row],
                        new Vector2(column * cell + 64, row * cell + 80), animator.Pose, frame: animator.Frame);
                    Label(row, column, $"{kinds[row]} {names[column]}");
                }
            for (var column = 0; column < 6; column++)
            {
                var pose = new ActorPose(NVector.Zero, NVector.One, 0, 1, 0, 0, 0, NVector.UnitX);
                renderer.Draw(batch, shapes, ActorVisualKind.Player, new Vector2(column * cell + 64, 848),
                    pose, new PlayerAppearance(ItemRarity.Rare, ItemRarity.Rare),
                    new ActorFrame(ActorAction.Attack, column / 5f));
                Label(6, column, $"Attack {column}");
                renderer.Draw(batch, shapes, kinds[column], new Vector2(column * cell + 64, 976),
                    pose with { WhiteMix = 1 }, frame: new ActorFrame(ActorAction.Hurt, .5f));
                Label(7, column, $"Flash {kinds[column]}");
            }
            batch.End();

            void Label(int row, int column, string text) => batch.DrawString(font, text,
                new Vector2(column * cell + 3, row * cell + 4), Color.Wheat, 0, Vector2.Zero,
                .65f, SpriteEffects.None, 0);
        }
        finally
        {
            device.SetRenderTargets(previous);
        }
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        Color[] Cell(int row, int column) => Enumerable.Range(row * cell + 28, 96)
            .SelectMany(y => Enumerable.Range(column * cell + 3, 122)
                .Select(x => pixels[y * target.Width + x])).ToArray();
        for (var row = 0; row < kinds.Length; row++)
        {
            for (var column = 0; column < 8; column++)
                if (Cell(row, column).Count(pixel => pixel != background) < 60)
                    throw new InvalidOperationException($"Content sprite is blank: {kinds[row]} {names[column]}.");
            if (Cell(row, 2).SequenceEqual(Cell(row, 6)) || Cell(row, 0).SequenceEqual(Cell(row, 4)))
                throw new InvalidOperationException($"Content sprite lacks front/back or left/right views: {kinds[row]}.");
            if (Cell(7, row).Count(pixel => pixel == Color.White) < 40)
                throw new InvalidOperationException($"Content white flash mask is missing: {kinds[row]}.");
        }
        if (Cell(6, 1).SequenceEqual(Cell(6, 3)))
            throw new InvalidOperationException("Layered Content sword attack does not advance through its frames.");
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "pixel-actors-directions.png"));
        target.SaveAsPng(file, target.Width, target.Height);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"),
            $"PASS Content-loaded actor atlases: six kinds/eight directions, layered attack frames and white masks{Environment.NewLine}");
    }
}
