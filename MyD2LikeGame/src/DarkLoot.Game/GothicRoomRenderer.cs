using System;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed class GothicRoomRenderer : IDisposable
{
    private readonly ShapeRenderer _shapes;
    private readonly PixelSceneRenderer _scene;
    private bool _disposed;
    internal double AnimationTime { get; private set; }

    internal GothicRoomRenderer(PixelContent content, ShapeRenderer shapes)
    {
        _shapes = shapes;
        _scene = new PixelSceneRenderer(content);
    }

    public void Update(float seconds, bool advance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (advance)
            AnimationTime += seconds;
    }

    public void Draw(SpriteBatch batch, Camera2D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _scene.Floor(batch, camera, GothicRoomLayout.Bounds, 16);
        _scene.Floor(batch, camera, GothicRoomLayout.Floor, 0);
        for (var index = 0; index < 5; index++)
        {
            var x = 64 + index * 192;
            _scene.Prop(batch, camera, 7, new Rectangle(x, -108, 64, 96));
        }
        foreach (var x in new[] { -72, 196, 452, 708, 968 })
            _scene.Prop(batch, camera, 0, new Rectangle(x, -112, 64, 150));
        _scene.Prop(batch, camera, 15, new Rectangle(416, 210, 128, 128));
        _scene.Prop(batch, camera, 1, new Rectangle(0, 164, 88, 100));
        _scene.Prop(batch, camera, 2, new Rectangle(140, 238, 64, 48));
        _scene.Prop(batch, camera, 3, new Rectangle(12, 284, 120, 56));
        _scene.Prop(batch, camera, 4, new Rectangle(26, 86, 80, 80));
        _scene.Prop(batch, camera, 14, new Rectangle(74, 346, 56, 48));
        _scene.Prop(batch, camera, 5, new Rectangle(240, -4, 48, 88));
        _scene.Prop(batch, camera, 6, new Rectangle(704, -4, 48, 88));
        for (var index = 0; index < GothicRoomLayout.Braziers.Count; index++)
            _scene.Torch(batch, camera, GothicRoomLayout.Braziers[index], AnimationTime, index);
        for (var mote = 0; mote < 15; mote++)
        {
            var point = camera.WorldToScreen(new NVector(
                70 + mote % 5 * 192 + (float)Math.Sin(AnimationTime * .23 + mote) * 17,
                16 + mote / 5 * 64 + (float)Math.Cos(AnimationTime * .17 + mote) * 12));
            _shapes.Rectangle(batch, new Rectangle((int)point.X, (int)point.Y, 1, 1),
                new Color(152, 165, 167) * .3f);
        }
    }

    internal void DrawPortal(SpriteBatch batch, Camera2D camera, NVector world) =>
        _scene.Portal(batch, camera, world, AnimationTime, new Color(247, 188, 87));

    internal Color[] FloorReference(Rectangle probe) => _scene.FloorReference(GothicRoomLayout.Floor, probe, 0);

    public void Dispose() => _disposed = true;
}
