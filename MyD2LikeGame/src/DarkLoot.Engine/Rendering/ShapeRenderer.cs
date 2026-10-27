using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Engine.Rendering;

public sealed class ShapeRenderer : IDisposable
{
    private readonly Texture2D _pixel;
    private readonly Texture2D _circle;

    public ShapeRenderer(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _circle = new Texture2D(device, 32, 32);
        var pixels = new Color[32 * 32];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var dx = x - 15.5f;
                var dy = y - 15.5f;
                pixels[y * 32 + x] = dx * dx + dy * dy <= 256 ? Color.White : Color.Transparent;
            }
        }
        _circle.SetData(pixels);
    }

    public void Rectangle(SpriteBatch batch, Rectangle bounds, Color color) =>
        batch.Draw(_pixel, bounds, color);

    public void Circle(SpriteBatch batch, Vector2 center, float radius, Color color) =>
        batch.Draw(_circle, center, null, color, 0, new Vector2(16), radius / 16, SpriteEffects.None, 0);

    public void Line(SpriteBatch batch, Vector2 start, Vector2 end, Color color, float thickness = 2)
    {
        var delta = end - start;
        batch.Draw(_pixel, start, null, color, MathF.Atan2(delta.Y, delta.X),
            new Vector2(0, .5f), new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
    }

    public void Dispose()
    {
        _circle.Dispose();
        _pixel.Dispose();
    }
}
