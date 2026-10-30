using System;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

internal static class UiTextureFactory
{
    internal static Color[] CreateLiquidPixels(int size) =>
        CirclePixels(size, (x, y, radius) =>
        {
            var light = Math.Clamp(1f - radius * .55f + x * .12f - y * .08f, 0f, 1f);
            return Color.Lerp(new Color(65, 5, 13), new Color(201, 28, 38), light);
        });

    internal static (Rectangle Source, Rectangle Destination) GlobeFill(Rectangle bounds, int textureSize, float fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureSize);
        var fill = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
        var rows = (int)MathF.Round(textureSize * fill);
        var height = (int)MathF.Round(bounds.Height * (rows / (float)textureSize));
        return (new Rectangle(0, textureSize - rows, textureSize, rows),
            new Rectangle(bounds.X, bounds.Bottom - height, bounds.Width, height));
    }

    internal static Color[] CreateGlassPixels(int size) =>
        CirclePixels(size, (x, y, radius) =>
        {
            var reflection = MathF.Exp(-((x + .38f) * (x + .38f) * 30f + (y + .48f) * (y + .48f) * 80f));
            var rim = Math.Clamp((radius - .82f) / .18f, 0f, 1f);
            var alpha = Math.Clamp(reflection * .42f + rim * .22f, 0f, .55f);
            return Color.FromNonPremultiplied(182, 203, 202, (int)(alpha * 255));
        });

    internal static Color[] CreateRingPixels(int size)
    {
        var pixels = new Color[size * size];
        var half = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + .5f - half) / half;
                var dy = (y + .5f - half) / half;
                var radius = MathF.Sqrt(dx * dx + dy * dy);
                if (radius is < .80f or >= 1f)
                    continue;
                var bevel = radius is < .83f or > .97f;
                var light = Math.Clamp(.52f - dx * .18f - dy * .35f, 0f, 1f);
                var metal = Color.Lerp(new Color(46, 34, 24), new Color(180, 145, 81), light);
                if (bevel)
                    metal = Color.Lerp(metal, new Color(15, 17, 18), .65f);
                var angle = MathF.Atan2(dy, dx);
                if (radius is > .87f and < .94f && MathF.Cos(angle * 16f) > .94f)
                    metal = new Color(43, 35, 25);
                pixels[y * size + x] = metal * Math.Clamp((1f - radius) * half, 0f, 1f);
            }
        }
        return pixels;
    }

    internal static Color[] CreateStonePixels(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var hash = unchecked((uint)(x * 374761393 + y * 668265263 + 1267));
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                var grain = (hash & 255) / 255f;
                var vein = MathF.Sin(x * .13f + MathF.Sin(y * .09f) * 2f) * 1.8f;
                var shade = (int)(26f + grain * 8f + vein);
                pixels[y * size + x] = new Color(shade, shade + 1, shade + 3);
            }
        }
        return pixels;
    }

    private static Color[] CirclePixels(int size, Func<float, float, float, Color> color)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var pixels = new Color[size * size];
        var half = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + .5f - half) / half;
                var dy = (y + .5f - half) / half;
                var radius = MathF.Sqrt(dx * dx + dy * dy);
                if (radius >= 1f)
                    continue;
                var coverage = Math.Clamp((1f - radius) * half, 0f, 1f);
                pixels[y * size + x] = color(dx, dy, radius) * coverage;
            }
        }
        return pixels;
    }
}
