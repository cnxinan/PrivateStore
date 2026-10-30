using System;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class GothicRoomRenderer : IDisposable
{
    private readonly ShapeRenderer _shapes;
    private Texture2D _room = null!;
    private Texture2D _glow = null!;
    private bool _disposed;
    internal double AnimationTime { get; private set; }

    public GothicRoomRenderer(GraphicsDevice device, ShapeRenderer shapes)
    {
        _shapes = shapes;
        try
        {
            var bounds = GothicRoomLayout.Bounds;
            _room = new Texture2D(device, bounds.Width, bounds.Height);
            _room.SetData(GothicRoomArt.CreateRoomPixels());
            _glow = new Texture2D(device, 128, 128);
            _glow.SetData(GothicRoomArt.CreateGlowPixels(128));
        }
        catch
        {
            Dispose();
            throw;
        }
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
        var bounds = GothicRoomLayout.Bounds;
        var origin = camera.WorldToScreen(new System.Numerics.Vector2(bounds.X, bounds.Y));
        batch.Draw(_room, origin, Color.White);
        var index = 0;
        foreach (var source in GothicRoomLayout.Braziers)
        {
            var position = camera.WorldToScreen(source);
            var flicker = (float)(Math.Sin(AnimationTime * 7.3 + index * 2.7) * .5 +
                Math.Sin(AnimationTime * 13.1 + index) * .3);
            var radius = index == 4 ? 93 : 115;
            batch.Draw(_glow, new Rectangle((int)position.X - radius, (int)position.Y - radius,
                radius * 2, radius * 2), new Color(238, 140, 55) * (.13f + flicker * .025f));
            var lean = (float)Math.Sin(AnimationTime * 5 + index) * 2;
            var height = 13 + flicker * 3;
            _shapes.Ellipse(batch, position + new Vector2(lean, -height * .4f),
                new Vector2(8, height), .1f, new Color(178, 52, 18) * .8f);
            _shapes.Ellipse(batch, position + new Vector2(-lean, -height * .5f),
                new Vector2(4, height * .8f), -.13f, new Color(240, 139, 43));
            _shapes.Ellipse(batch, position + new Vector2(0, -3),
                new Vector2(3, height * .37f), 0, new Color(255, 222, 129));
            for (var spark = 0; spark < 2; spark++)
            {
                var age = (float)((AnimationTime * .35 + index * .17 + spark * .5) % 1);
                var offset = new Vector2(MathF.Sin(age * 5 + index) * 7, -13 - age * 32);
                _shapes.Rectangle(batch, new Rectangle((int)(position.X + offset.X),
                    (int)(position.Y + offset.Y), 1, 2), new Color(248, 166, 78) * (1 - age) * .7f);
            }
            index++;
        }
        for (var mote = 0; mote < 15; mote++)
        {
            var x = 70 + mote % 5 * 192 + (float)Math.Sin(AnimationTime * .23 + mote) * 17;
            var y = 16 + mote / 5 * 64 + (float)Math.Cos(AnimationTime * .17 + mote) * 12;
            var position = camera.WorldToScreen(new System.Numerics.Vector2(x, y));
            _shapes.Rectangle(batch, new Rectangle((int)position.X, (int)position.Y, 1, 1),
                new Color(152, 165, 167) * .3f);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _room?.Dispose();
        _glow?.Dispose();
    }
}
