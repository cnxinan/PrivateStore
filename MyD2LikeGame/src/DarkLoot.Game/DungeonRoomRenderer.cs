using System;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed class DungeonRoomRenderer : IDisposable
{
    private static readonly NVector[] Torches =
    {
        new(180, -8), new(780, -8), new(1040, 202),
        new(1300, -8), new(1900, -8), new(2160, 202),
        new(2420, -8), new(3020, -8), new(2420, 548), new(3020, 548)
    };

    private readonly PixelSceneRenderer _scene;
    private readonly ShapeRenderer _shapes;
    private bool _disposed;
    internal double AnimationTime { get; private set; }

    internal DungeonRoomRenderer(PixelContent content, ShapeRenderer shapes)
    {
        _scene = new PixelSceneRenderer(content);
        _shapes = shapes;
    }

    public void Update(float seconds, bool advance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (advance)
            AnimationTime += seconds;
    }

    public void Draw(SpriteBatch batch, Camera2D camera, WorldMap map, bool completed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsDungeon)
            return;
        _scene.Floor(batch, camera, new Rectangle(-80, -112, 3360, 704), 16);
        foreach (var floor in map.Floors)
        {
            var first = floor.Width == 160 ? 24 : 4 + floor.X / 1120 * 4;
            _scene.Floor(batch, camera, Bounds(floor), first);
        }
        for (var room = 0; room < 3; room++)
        {
            var x = room * 1120;
            for (var column = 0; column < 4; column++)
                _scene.Prop(batch, camera, 9, new Rectangle(x + column * 256 - 16, -100, 64, 104));
            _scene.Prop(batch, camera, room == 2 ? 6 : 5, new Rectangle(x + 456, -96, 48, 88));
            _scene.Prop(batch, camera, 13, new Rectangle(x + 60, 474, 48, 64));
            _scene.Prop(batch, camera, 13, new Rectangle(x + 856, 474, 48, 64));
        }
        foreach (var obstacle in map.Obstacles)
            _scene.Prop(batch, camera, obstacle.X < 1120 ? 8 : obstacle.X >= 2240 ? 10 : 9, Bounds(obstacle));
        _scene.Prop(batch, camera, map.BossGateOpen ? 12 : 11, Bounds(map.BossGate));
        _scene.Portal(batch, camera, new NVector(64, 270), AnimationTime, new Color(112, 192, 225));
        if (completed)
            _scene.Portal(batch, camera, new NVector(3100, 270), AnimationTime, new Color(247, 188, 87));
        for (var index = 0; index < Torches.Length; index++)
            _scene.Torch(batch, camera, Torches[index], AnimationTime, index);
        for (var mote = 0; mote < 36; mote++)
        {
            var point = camera.WorldToScreen(new NVector(mote / 12 * 1120 + 110 + mote % 6 * 143 +
                (float)Math.Sin(AnimationTime * .21 + mote) * 13,
                76 + mote % 2 * 315 + (float)Math.Cos(AnimationTime * .16 + mote) * 21));
            _shapes.Rectangle(batch, new Rectangle((int)point.X, (int)point.Y, 1, 1),
                (mote / 12 == 2 ? new Color(191, 124, 79) : new Color(159, 179, 187)) * .28f);
        }
    }

    private static Rectangle Bounds(WorldRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    public void Dispose() => _disposed = true;
}
