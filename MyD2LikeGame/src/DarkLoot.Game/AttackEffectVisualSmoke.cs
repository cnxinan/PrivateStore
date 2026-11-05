using System;
using System.IO;
using System.Linq;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal static class AttackEffectVisualSmoke
{
    internal static void Run(GraphicsDevice device, ShapeRenderer shapes, SpriteFont font,
        CombatEffectRenderer effects, PixelContent pixelsContent)
    {
        const int cell = 140;
        using var target = new RenderTarget2D(device, cell * 8, cell * 4);
        using var batch = new SpriteBatch(device);
        var actors = new ActorRenderer(pixelsContent);
        var background = new Color(24, 25, 30);
        var directions = new[] { NVector.UnitX, -NVector.UnitX, NVector.UnitY, -NVector.UnitY };
        var previous = device.GetRenderTargets();
        try
        {
            device.SetRenderTarget(target);
            device.Clear(background);
            batch.Begin(samplerState: SamplerState.PointClamp);
            foreach (var kind in Enum.GetValues<CombatEffectKind>())
            {
                var row = (int)kind;
                for (var column = 0; column < 8; column++)
                {
                    var actorKind = row < 2 ? ActorVisualKind.Player : ActorVisualKind.Slime;
                    var animator = new ActorAnimator(actorKind);
                    animator.Update(new(false, false, false, false, false, 0, false, NVector.UnitX), 0);
                    var anchor = new Vector2(column * cell + cell / 2, row * cell + 80);
                    actors.Draw(batch, shapes, actorKind, anchor, animator.Pose,
                        row == 0 ? new(DarkLoot.Data.Definitions.ItemRarity.Common, null) : default);
                    var effect = new CombatEffect(kind, "preview", NVector.Zero,
                        column < 4 ? directions[column] : NVector.UnitX);
                    var fraction = column < 4 ? 0 : (column - 3) * .25f;
                    effects.Draw(batch, effect with { Age = fraction * effect.Duration }, anchor);
                    batch.DrawString(font, $"{kind} {new[] { "R", "L", "D", "U", "25", "50", "75", "End" }[column]}",
                        new Vector2(column * cell + 4, row * cell + 4), Color.Wheat, 0, Vector2.Zero,
                        .75f, SpriteEffects.None, 0);
                }
            }
            batch.End();
        }
        finally
        {
            device.SetRenderTargets(previous);
        }
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        Color[] Cell(int row, int column) => Enumerable.Range(row * cell + 32, 104)
            .SelectMany(y => Enumerable.Range(column * cell + 4, 132)
                .Select(x => pixels[y * target.Width + x])).ToArray();
        for (var row = 0; row < 4; row++)
        {
            var end = Cell(row, 7);
            for (var column = 0; column < 4; column++)
                if (Cell(row, column).Zip(end).Count(pair => pair.First != pair.Second) < 40)
                    throw new InvalidOperationException($"Attack GPU frame lacks its burst: row={row}, direction={column}.");
            if (Cell(row, 0).SequenceEqual(Cell(row, 5)))
                throw new InvalidOperationException($"Attack GPU frame did not animate: row={row}.");
        }
        for (var row = 0; row < 2; row++)
            if (Cell(row, 0).SequenceEqual(Cell(row, 1)) || Cell(row, 2).SequenceEqual(Cell(row, 3)))
                throw new InvalidOperationException("Attack GPU rotation did not follow the four aim directions.");
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "attack-effects-frames.png"));
        target.SaveAsPng(file, target.Width, target.Height);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"),
            $"PASS actual GPU slash/punch/hit/critical frames, four directions, decay and expiry{Environment.NewLine}");
    }
}
