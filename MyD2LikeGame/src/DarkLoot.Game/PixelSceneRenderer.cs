using System;
using DarkLoot.Engine.Camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class PixelSceneRenderer(PixelContent content)
{
    internal void Floor(SpriteBatch batch, Camera2D camera, Rectangle bounds, int firstTile)
    {
        foreach (var tile in PixelTileLayout.Floor(bounds, firstTile))
            batch.Draw(content.Terrain, Screen(camera, tile.World), tile.Source, Color.White);
    }

    internal void Prop(SpriteBatch batch, Camera2D camera, int slot, Rectangle bounds, Color? tint = null) =>
        batch.Draw(content.Props, Screen(camera, bounds),
            new Rectangle(slot % 8 * 64, slot / 8 * 64, 64, 64), tint ?? Color.White);

    internal void Torch(SpriteBatch batch, Camera2D camera, NVector world, double time, int index)
    {
        var point = camera.WorldToScreen(world);
        var flicker = (float)Math.Sin(time * 7.3 + index * 2.7);
        batch.Draw(content.Glow, new Rectangle((int)point.X - 106, (int)point.Y - 106, 212, 212),
            new Color(239, 161, 75) * (.13f + flicker * .025f));
        batch.Draw(content.Torch, Round(point), new Rectangle((int)(time * 9 + index) % 6 * 16, 0, 16, 32),
            Color.White, 0, new Vector2(8, 24), 2, SpriteEffects.None, 0);
    }

    internal void Portal(SpriteBatch batch, Camera2D camera, NVector world, double time, Color tint)
    {
        var point = camera.WorldToScreen(world);
        var pulse = .8f + (float)Math.Sin(time * 2.2) * .12f;
        batch.Draw(content.Glow, new Rectangle((int)point.X - 86, (int)point.Y - 103, 172, 172),
            tint * (.22f * pulse));
        batch.Draw(content.Portal, Round(point), new Rectangle((int)(time * 7) % 6 * 40, 0, 40, 56),
            tint, 0, new Vector2(20, 40), 2, SpriteEffects.None, 0);
    }

    internal Color[] FloorReference(Rectangle floor, Rectangle probe, int firstTile)
    {
        if (!floor.Contains(probe))
            throw new ArgumentOutOfRangeException(nameof(probe));
        var atlas = new Color[content.Terrain.Width * content.Terrain.Height];
        content.Terrain.GetData(atlas);
        var result = new Color[probe.Width * probe.Height];
        foreach (var tile in PixelTileLayout.Floor(floor, firstTile))
        {
            var overlap = Rectangle.Intersect(probe, tile.World);
            for (var y = overlap.Top; y < overlap.Bottom; y++)
                for (var x = overlap.Left; x < overlap.Right; x++)
                    result[(y - probe.Y) * probe.Width + x - probe.X] =
                        atlas[(tile.Source.Y + (y - tile.World.Y) / 2) * content.Terrain.Width +
                            tile.Source.X + (x - tile.World.X) / 2];
        }
        return result;
    }

    private static Rectangle Screen(Camera2D camera, Rectangle world)
    {
        var point = Round(camera.WorldToScreen(new NVector(world.X, world.Y)));
        return new Rectangle((int)point.X, (int)point.Y, world.Width, world.Height);
    }

    internal static Vector2 Round(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));
}
