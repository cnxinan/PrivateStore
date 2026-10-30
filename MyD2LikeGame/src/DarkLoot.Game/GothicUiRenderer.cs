using System;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public enum UiIcon
{
    Weapon,
    Armor,
    Bone,
    Hide,
    Slime,
    Recipe,
    Material
}

public sealed class GothicUiRenderer : IDisposable
{
    public static readonly Color Text = new(228, 218, 196);
    public static readonly Color Muted = new(157, 151, 139);
    public static readonly Color Gold = new(205, 166, 87);
    public static readonly Color Blue = new(113, 165, 221);
    public static readonly Color Red = new(225, 101, 93);
    public static readonly Color Green = new(132, 184, 115);

    private static readonly Color Shadow = new(9, 10, 12);
    private static readonly Color Brass = new(111, 87, 50);
    private static readonly Color BrassLight = new(161, 133, 82);
    private const int StoneSize = 96;
    private const int GlobeSize = 192;
    private readonly ShapeRenderer _shapes;
    private readonly Texture2D[] _textures = new Texture2D[4];
    private bool _disposed;

    public GothicUiRenderer(GraphicsDevice device, ShapeRenderer shapes)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(shapes);
        _shapes = shapes;
        try
        {
            _textures[0] = CreateTexture(device, StoneSize, UiTextureFactory.CreateStonePixels(StoneSize));
            _textures[1] = CreateTexture(device, GlobeSize, UiTextureFactory.CreateLiquidPixels(GlobeSize));
            _textures[2] = CreateTexture(device, GlobeSize, UiTextureFactory.CreateGlassPixels(GlobeSize));
            _textures[3] = CreateTexture(device, GlobeSize, UiTextureFactory.CreateRingPixels(GlobeSize));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void DrawPanel(SpriteBatch batch, Rectangle bounds)
    {
        if (!CanDraw(bounds))
            return;
        Stone(batch, bounds, Color.White);
        Bevel(batch, bounds, BrassLight, Shadow, 2);
        var inner = Inset(bounds, 4);
        Bevel(batch, inner, Brass, new Color(46, 37, 26), 1);
        _shapes.Rectangle(batch, new Rectangle(bounds.X + 8, bounds.Y + 8, Math.Max(0, bounds.Width - 16), 28),
            new Color(15, 16, 18) * .68f);
        _shapes.Line(batch, new Vector2(bounds.X + 12, bounds.Y + 38),
            new Vector2(bounds.Right - 12, bounds.Y + 38), Brass, 1);
        CornerOrnaments(batch, bounds);
    }

    public void DrawConsole(SpriteBatch batch, Rectangle bounds)
    {
        if (!CanDraw(bounds))
            return;
        Stone(batch, bounds, new Color(211, 207, 198));
        Bevel(batch, bounds, Shadow, Shadow, 3);
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Y + 3, bounds.Width, 2), BrassLight);
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Y + 6, bounds.Width, 2), Brass);
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Bottom - 5, bounds.Width, 1), Brass);
        for (var x = bounds.X + 16; x < bounds.Right - 8; x += 96)
            Rivet(batch, new Vector2(x, bounds.Y + 12), 2);
    }

    public void DrawWell(SpriteBatch batch, Rectangle bounds, bool hovered = false, bool selected = false)
    {
        if (!CanDraw(bounds))
            return;
        _shapes.Rectangle(batch, bounds, selected ? Brass : new Color(47, 45, 40));
        _shapes.Rectangle(batch, Inset(bounds, 1), new Color(13, 15, 18));
        Bevel(batch, Inset(bounds, 2), Shadow, new Color(61, 58, 49), 1);
        if (selected || hovered)
            Bevel(batch, bounds, selected ? Gold : BrassLight, selected ? BrassLight : Brass, 1);
        if (hovered)
            _shapes.Rectangle(batch, Inset(bounds, 3), new Color(143, 125, 81) * .10f);
    }

    public void DrawButton(SpriteBatch batch, Rectangle bounds, bool enabled = true, bool hovered = false, bool pressed = false)
    {
        if (!CanDraw(bounds))
            return;
        Stone(batch, bounds, !enabled ? new Color(126, 128, 132) :
            pressed ? new Color(163, 153, 135) : hovered ? new Color(255, 239, 204) : Color.White);
        var highlight = enabled ? hovered ? Gold : BrassLight : new Color(77, 75, 69);
        Bevel(batch, bounds, pressed ? Shadow : highlight, pressed ? highlight : Shadow, 2);
        Bevel(batch, Inset(bounds, 3), pressed ? new Color(25, 23, 21) : Brass, new Color(35, 31, 25), 1);
    }

    public void DrawIcon(SpriteBatch batch, Rectangle bounds, UiIcon icon, Color tint)
    {
        if (!CanDraw(bounds))
            return;
        var size = Math.Min(bounds.Width, bounds.Height);
        var origin = new Vector2(bounds.Center.X - size / 2f, bounds.Center.Y - size / 2f);
        var scale = size / 32f;
        switch (icon)
        {
            case UiIcon.Weapon:
                Sword(batch, origin, scale, tint);
                break;
            case UiIcon.Armor:
                Armor(batch, origin, scale, tint);
                break;
            case UiIcon.Bone:
                Bone(batch, origin, scale, Color.Lerp(tint, Text, .45f));
                break;
            case UiIcon.Hide:
                Hide(batch, origin, scale, Color.Lerp(tint, new Color(156, 107, 62), .60f));
                break;
            case UiIcon.Slime:
                Slime(batch, origin, scale, Color.Lerp(tint, Green, .65f));
                break;
            case UiIcon.Recipe:
                Scroll(batch, origin, scale, Color.Lerp(tint, new Color(198, 175, 125), .70f));
                break;
            default:
                Crystal(batch, origin, scale, tint);
                break;
        }
    }

    public void DrawHealthGlobe(SpriteBatch batch, Rectangle bounds, float fraction)
    {
        if (!CanDraw(bounds))
            return;
        var diameter = Math.Min(bounds.Width, bounds.Height);
        var globe = new Rectangle(bounds.Center.X - diameter / 2, bounds.Center.Y - diameter / 2, diameter, diameter);
        var inset = Math.Max(1, (int)MathF.Round(diameter * .105f));
        var liquid = Inset(globe, inset);
        _shapes.Circle(batch, new Vector2(globe.Center.X, globe.Center.Y), diameter * .41f, new Color(12, 14, 19));
        var (source, destination) = UiTextureFactory.GlobeFill(liquid, GlobeSize, fraction);
        // Crop the same bottom rows in both spaces: scaling a full circle into the fill would make a false oval.
        if (source.Height > 0 && destination.Height > 0)
            batch.Draw(_textures[1], destination, source, Color.White);
        batch.Draw(_textures[2], liquid, Color.White);
        batch.Draw(_textures[3], globe, Color.White);
        var radius = diameter * .45f;
        for (var i = 0; i < 4; i++)
        {
            var angle = MathHelper.PiOver4 + i * MathHelper.PiOver2;
            Rivet(batch, new Vector2(globe.Center.X + MathF.Cos(angle) * radius,
                globe.Center.Y + MathF.Sin(angle) * radius), Math.Max(1.3f, diameter * .018f));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var texture in _textures)
            texture?.Dispose();
    }

    private bool CanDraw(Rectangle bounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static Texture2D CreateTexture(GraphicsDevice device, int size, Color[] pixels)
    {
        var texture = new Texture2D(device, size, size);
        try
        {
            texture.SetData(pixels);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private void Stone(SpriteBatch batch, Rectangle bounds, Color tint)
    {
        for (var y = bounds.Y; y < bounds.Bottom; y += StoneSize)
        {
            for (var x = bounds.X; x < bounds.Right; x += StoneSize)
            {
                var width = Math.Min(StoneSize, bounds.Right - x);
                var height = Math.Min(StoneSize, bounds.Bottom - y);
                batch.Draw(_textures[0], new Rectangle(x, y, width, height), new Rectangle(0, 0, width, height), tint);
            }
        }
    }

    private void Bevel(SpriteBatch batch, Rectangle bounds, Color light, Color dark, int thickness)
    {
        if (bounds.Width < thickness * 2 || bounds.Height < thickness * 2)
            return;
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), light);
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), light);
        _shapes.Rectangle(batch, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), dark);
        _shapes.Rectangle(batch, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), dark);
    }

    private void CornerOrnaments(SpriteBatch batch, Rectangle bounds)
    {
        if (bounds.Width < 40 || bounds.Height < 40)
            return;
        for (var x = 0; x < 2; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                var center = new Vector2(x == 0 ? bounds.X + 10 : bounds.Right - 10,
                    y == 0 ? bounds.Y + 10 : bounds.Bottom - 10);
                _shapes.RotatedRectangle(batch, center, new Vector2(9, 9), MathHelper.PiOver4, Shadow);
                _shapes.RotatedRectangle(batch, center, new Vector2(6, 6), MathHelper.PiOver4, Brass);
                Rivet(batch, center, 1.8f);
            }
        }
    }

    private void Rivet(SpriteBatch batch, Vector2 center, float radius)
    {
        _shapes.Circle(batch, center + Vector2.One, radius + .5f, Shadow);
        _shapes.Circle(batch, center, radius, Brass);
        _shapes.Circle(batch, center - new Vector2(radius * .25f), radius * .45f, BrassLight);
    }

    private static Rectangle Inset(Rectangle bounds, int amount) =>
        new(bounds.X + amount, bounds.Y + amount, Math.Max(0, bounds.Width - amount * 2), Math.Max(0, bounds.Height - amount * 2));

    private void Stroke(SpriteBatch batch, Vector2 origin, float scale, float x1, float y1, float x2, float y2, Color color, float width = 2)
    {
        var start = origin + new Vector2(x1, y1) * scale;
        var end = origin + new Vector2(x2, y2) * scale;
        _shapes.Line(batch, start, end, Shadow, (width + 2) * scale);
        _shapes.Line(batch, start, end, color, width * scale);
    }

    private void Dot(SpriteBatch batch, Vector2 origin, float scale, float x, float y, float radius, Color color) =>
        _shapes.Circle(batch, origin + new Vector2(x, y) * scale, radius * scale, color);

    private void Sword(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        Stroke(batch, origin, scale, 10, 23, 25, 6, color, 3.8f);
        Stroke(batch, origin, scale, 12, 20, 25, 6, Color.Lerp(color, Color.White, .6f), 1);
        Stroke(batch, origin, scale, 5, 29, 11, 22, new Color(139, 102, 62), 3);
        Stroke(batch, origin, scale, 6, 18, 16, 27, Gold, 2.4f);
        Dot(batch, origin, scale, 5, 28, 2, BrassLight);
    }

    private void Armor(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        _shapes.RotatedRectangle(batch, origin + new Vector2(16, 19) * scale, new Vector2(16, 18) * scale, 0, Shadow);
        _shapes.RotatedRectangle(batch, origin + new Vector2(16, 19) * scale, new Vector2(13, 16) * scale, 0, color * .7f);
        Stroke(batch, origin, scale, 11, 6, 5, 12, color, 5);
        Stroke(batch, origin, scale, 21, 6, 27, 12, color, 5);
        Stroke(batch, origin, scale, 10, 10, 9, 26, color, 2);
        Stroke(batch, origin, scale, 22, 10, 23, 26, color, 2);
        Dot(batch, origin, scale, 16, 6, 4, Shadow);
        Stroke(batch, origin, scale, 16, 12, 16, 24, Color.Lerp(color, Color.White, .3f), 1);
        Stroke(batch, origin, scale, 10, 26, 22, 26, BrassLight, 2);
    }

    private void Bone(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        Stroke(batch, origin, scale, 9, 24, 23, 10, color, 4);
        Dot(batch, origin, scale, 7, 23, 4, Shadow);
        Dot(batch, origin, scale, 10, 27, 4, Shadow);
        Dot(batch, origin, scale, 22, 6, 4, Shadow);
        Dot(batch, origin, scale, 26, 10, 4, Shadow);
        Dot(batch, origin, scale, 7, 23, 3, color);
        Dot(batch, origin, scale, 10, 27, 3, color);
        Dot(batch, origin, scale, 22, 6, 3, color);
        Dot(batch, origin, scale, 26, 10, 3, color);
    }

    private void Hide(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        Stroke(batch, origin, scale, 7, 7, 12, 12, color, 5);
        Stroke(batch, origin, scale, 25, 7, 20, 12, color, 5);
        Stroke(batch, origin, scale, 6, 26, 12, 22, color, 5);
        Stroke(batch, origin, scale, 26, 26, 20, 22, color, 5);
        _shapes.Ellipse(batch, origin + new Vector2(16, 16) * scale, new Vector2(8, 12) * scale, 0, Shadow);
        _shapes.Ellipse(batch, origin + new Vector2(16, 16) * scale, new Vector2(7, 11) * scale, 0, color);
        Stroke(batch, origin, scale, 14, 11, 17, 22, color * .55f, 1);
        Stroke(batch, origin, scale, 12, 16, 19, 15, color * .55f, 1);
    }

    private void Slime(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        _shapes.Ellipse(batch, origin + new Vector2(16, 23) * scale, new Vector2(13, 6) * scale, 0, Shadow);
        _shapes.Ellipse(batch, origin + new Vector2(16, 20) * scale, new Vector2(11, 7) * scale, 0, color * .75f);
        Dot(batch, origin, scale, 17, 15, 7, color);
        Dot(batch, origin, scale, 12, 16, 3, Color.Lerp(color, Color.White, .4f));
        Dot(batch, origin, scale, 15, 19, 1.2f, Shadow);
        Dot(batch, origin, scale, 21, 19, 1.2f, Shadow);
    }

    private void Scroll(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        _shapes.RotatedRectangle(batch, origin + new Vector2(16, 17) * scale, new Vector2(18, 23) * scale, 0, Shadow);
        _shapes.RotatedRectangle(batch, origin + new Vector2(16, 17) * scale, new Vector2(15, 20) * scale, 0, color);
        Stroke(batch, origin, scale, 7, 6, 25, 6, Color.Lerp(color, Color.White, .2f), 4);
        Stroke(batch, origin, scale, 7, 27, 25, 27, color * .75f, 4);
        Stroke(batch, origin, scale, 12, 12, 21, 12, Brass, 1);
        Stroke(batch, origin, scale, 12, 16, 19, 16, Brass, 1);
        Stroke(batch, origin, scale, 12, 20, 17, 20, Brass, 1);
        Dot(batch, origin, scale, 22, 23, 3.2f, new Color(128, 43, 38));
        Dot(batch, origin, scale, 22, 23, 1.3f, Gold);
    }

    private void Crystal(SpriteBatch batch, Vector2 origin, float scale, Color color)
    {
        Stroke(batch, origin, scale, 16, 5, 7, 14, color, 3);
        Stroke(batch, origin, scale, 7, 14, 16, 28, color * .7f, 3);
        Stroke(batch, origin, scale, 16, 28, 25, 14, color * .7f, 3);
        Stroke(batch, origin, scale, 25, 14, 16, 5, color, 3);
        Stroke(batch, origin, scale, 16, 5, 16, 28, color * .8f, 4);
        Stroke(batch, origin, scale, 9, 14, 23, 14, Color.Lerp(color, Color.White, .35f), 1);
    }
}
