using System;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

internal static class GothicRoomArt
{
    private static readonly Color[] Glass =
    {
        new(38, 77, 112), new(30, 67, 87), new(104, 49, 66), new(133, 95, 55)
    };

    internal static Color[] CreateRoomPixels()
    {
        var canvas = new Canvas();
        var bounds = GothicRoomLayout.Bounds;
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                var floor = GothicRoomLayout.Floor.Contains(x, y);
                var row = (y + 112) / (floor ? 48 : 24);
                var column = (x + 80 + (row % 2) * 32) / (floor ? 64 : 72);
                var grain = Noise(x, y);
                var shade = (floor ? 49 : 35) + Noise(column, row) % 17 + grain % 9;
                var color = new Color(shade + 3, shade + 1, shade - 3);
                var localX = (x + 80 + (row % 2) * 32) % (floor ? 64 : 72);
                var localY = (y + 112) % (floor ? 48 : 24);
                if (localX < 2 || localY < 2)
                    color = floor ? new Color(27, 27, 26) : new Color(19, 20, 23);
                else if (localX == 2 || localY == 2)
                    color = Color.Lerp(color, new Color(132, 124, 107), .25f);
                if (floor)
                {
                    if (Noise(column, row) % 5 == 0 && localX is > 15 and < 53 &&
                        Math.Abs(localY - localX * .27f - Noise(column, row) % 19) < .6f)
                        color = Color.Lerp(color, new Color(19, 22, 23), .4f);
                    var edge = Math.Min(Math.Min(x, 960 - x), Math.Min(y, 540 - y));
                    color = Color.Lerp(new Color(20, 22, 24), color, .48f + .52f * Math.Clamp(edge / 65f, 0, 1));
                    for (var window = 96; window < 960; window += 192)
                    {
                        var beam = Math.Max(0, 1 - Math.Abs(x - window - y * .22f) / (24 + y * .18f));
                        color = Color.Lerp(color, new Color(108, 136, 155), beam * Math.Max(0, 1 - y / 410f) * .22f);
                    }
                    var forge = MathF.Exp(-((x - 45) * (x - 45) + (y - 214) * (y - 214)) / 13000f);
                    color = Color.Lerp(color, new Color(137, 83, 37), forge * .22f);
                    var dx = x - 480f;
                    var dy = y - 270f;
                    var radius = MathF.Sqrt(dx * dx + dy * dy);
                    var angle = MathF.Atan2(dy, dx);
                    var star = 104 * (.68f + .24f * Math.Abs(MathF.Cos(angle * 4)));
                    if (Math.Abs(radius - 128) < 1 || Math.Abs(radius - 120) < .7f ||
                        Math.Abs(radius - star) < .8f || radius < 34 && Math.Abs(radius - 28 * Math.Abs(MathF.Sin(angle * 4))) < 1)
                        color = Color.Lerp(color, new Color(139, 119, 76), .55f);
                    if ((x is 20 or 21 or 938 or 939 && y is > 20 and < 520) ||
                        (y is 20 or 21 or 518 or 519 && x is > 20 and < 940))
                        color = new Color(102, 91, 67);
                }
                canvas.Set(x, y, color);
            }
        }

        canvas.Fill(new Rectangle(-80, -112, 1120, 7), new Color(75, 69, 58));
        canvas.Fill(new Rectangle(-80, -105, 1120, 4), new Color(27, 28, 29));
        canvas.Fill(new Rectangle(-80, -12, 1120, 4), new Color(82, 77, 65));
        canvas.Fill(new Rectangle(-80, -8, 1120, 8), new Color(28, 29, 29));
        for (var x = 0; x <= 960; x += 192)
        {
            DrawPillar(canvas, x, -112, 112);
            if (x > 0 && x < 960)
                DrawBanner(canvas, x, -78);
        }
        for (var x = 96; x < 960; x += 192)
            DrawWindow(canvas, x);
        foreach (var x in new[] { -49, 1009 })
        {
            for (var y = 16; y < 540; y += 180)
                DrawPillar(canvas, x, y, 92);
        }
        canvas.Fill(new Rectangle(-80, 540, 1120, 5), new Color(110, 102, 83));
        canvas.Fill(new Rectangle(-80, 545, 1120, 7), new Color(30, 30, 29));
        for (var x = -76; x < 1040; x += 80)
        {
            canvas.Fill(new Rectangle(x, 552, 76, 30), new Color(53, 51, 46));
            canvas.Line(x + 2, 553, x + 72, 553, new Color(85, 78, 64));
            canvas.Line(x + 3, 578, x + 73, 578, new Color(29, 30, 31));
        }
        canvas.Fill(new Rectangle(424, 545, 112, 37), new Color(21, 21, 22));
        for (var x = 428; x < 534; x += 14)
            canvas.Fill(new Rectangle(x, 549, 10, 30), new Color(52, 40, 31));
        canvas.Line(427, 555, 532, 555, new Color(79, 74, 61), 3);
        canvas.Line(427, 573, 532, 573, new Color(79, 74, 61), 3);
        DrawForge(canvas);
        foreach (var position in GothicRoomLayout.Braziers)
        {
            if (position.X == 45)
                continue;
            var x = (int)position.X;
            var y = (int)position.Y;
            canvas.Circle(x + 4, y + 22, 19, new Color(20, 20, 23));
            canvas.Fill(new Rectangle(x - 7, y + 4, 14, 25), new Color(46, 42, 34));
            canvas.Fill(new Rectangle(x - 13, y + 22, 26, 8), new Color(82, 74, 54));
            canvas.Fill(new Rectangle(x - 13, y + 2, 26, 6), new Color(104, 83, 46));
            canvas.Line(x - 12, y + 2, x - 8, y + 14, new Color(30, 29, 27), 3);
            canvas.Line(x + 12, y + 2, x + 8, y + 14, new Color(30, 29, 27), 3);
        }
        return canvas.Pixels;
    }

    internal static Color[] CreateGlowPixels(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + .5f) * 2 / size - 1;
                var dy = (y + .5f) * 2 / size - 1;
                var distance = dx * dx + dy * dy;
                pixels[y * size + x] = Color.White * MathF.Pow(Math.Max(0, 1 - distance), 3);
            }
        return pixels;
    }

    private static void DrawPillar(Canvas canvas, int x, int top, int height)
    {
        canvas.Fill(new Rectangle(x - 25, top + 5, 56, height + 5), new Color(20, 21, 23));
        canvas.Fill(new Rectangle(x - 20, top, 40, height), new Color(63, 61, 55));
        canvas.Fill(new Rectangle(x - 15, top + 8, 6, height - 18), new Color(86, 81, 69));
        canvas.Fill(new Rectangle(x - 6, top + 8, 11, height - 18), new Color(43, 44, 44));
        canvas.Fill(new Rectangle(x + 12, top + 8, 5, height - 18), new Color(34, 35, 36));
        foreach (var y in new[] { top, top + 7, top + height - 14, top + height - 5 })
        {
            canvas.Fill(new Rectangle(x - 26, y, 52, 5), new Color(78, 73, 62));
            canvas.Line(x - 25, y, x + 24, y, new Color(116, 105, 83));
        }
        canvas.Circle(x, top + 16, 5, new Color(31, 33, 34));
        canvas.Circle(x - 1, top + 15, 2, new Color(132, 113, 75));
    }

    private static void DrawWindow(Canvas canvas, int center)
    {
        canvas.Arch(center, -40, -3, 44, (_, _) => new Color(18, 21, 26));
        canvas.Arch(center, -40, -7, 39, (x, y) => new Color(82 + Noise(x, y) % 12, 76, 62));
        canvas.Arch(center, -40, -13, 33, (_, _) => new Color(21, 25, 30));
        canvas.Arch(center, -40, -16, 29, (x, y) =>
        {
            var u = x - center + 29;
            var v = y + 94;
            var lead = Math.Abs(x - center) <= 1 || (u + v) % 22 < 2 ||
                (u - v + 220) % 22 < 2 || y is -38 or -37;
            return lead ? new Color(17, 25, 30) :
                Color.Lerp(Glass[(u / 15 + v / 22) % Glass.Length], new Color(153, 164, 163), Noise(x, y) % 10 / 65f);
        });
        canvas.Circle(center, -59, 10, new Color(141, 118, 70));
        canvas.Circle(center, -59, 8, new Color(23, 38, 52));
        for (var petal = 0; petal < 6; petal++)
        {
            var angle = petal * MathF.Tau / 6;
            canvas.Circle(center + (int)(MathF.Cos(angle) * 5), -59 + (int)(MathF.Sin(angle) * 5),
                2, new Color(70, 125, 150));
        }
        canvas.Fill(new Rectangle(center - 44, -7, 88, 7), new Color(100, 91, 72));
        canvas.Line(center - 43, -7, center + 43, -7, new Color(154, 134, 93));
    }

    private static void DrawBanner(Canvas canvas, int center, int top)
    {
        for (var row = 0; row < 53; row++)
        {
            var half = row < 39 ? 14 : 53 - row;
            for (var x = -half; x <= half; x++)
            {
                var edge = Math.Abs(x) >= half - 1;
                canvas.Set(center + x, top + row, edge ? new Color(134, 108, 58) :
                    new Color(75 + (int)(MathF.Cos(x * .22f) * 13), 27, 34));
            }
        }
        canvas.Line(center - 19, top - 2, center + 19, top - 2, new Color(144, 120, 71), 2);
        canvas.Line(center, top + 7, center + 7, top + 20, new Color(158, 133, 78), 2);
        canvas.Line(center + 7, top + 20, center, top + 33, new Color(158, 133, 78), 2);
        canvas.Line(center, top + 33, center - 7, top + 20, new Color(158, 133, 78), 2);
        canvas.Line(center - 7, top + 20, center, top + 7, new Color(158, 133, 78), 2);
    }

    private static void DrawForge(Canvas canvas)
    {
        canvas.Fill(new Rectangle(10, 170, 72, 76), new Color(22, 23, 25));
        for (var y = 174; y < 242; y += 12)
            for (var x = 14; x < 78; x += 16)
                canvas.Fill(new Rectangle(x, y, 14, 10), new Color(64 + Noise(x, y) % 16, 55, 45));
        canvas.Arch(45, 201, 234, 25, (_, _) => new Color(107, 89, 64));
        canvas.Arch(45, 201, 231, 19, (_, _) => new Color(25, 20, 20));
        canvas.Arch(45, 207, 230, 15, (x, y) =>
            Color.Lerp(new Color(231, 106, 31), new Color(98, 36, 20), Math.Clamp((y - 195) / 40f, 0, 1)));
        canvas.Fill(new Rectangle(7, 164, 78, 9), new Color(85, 78, 64));
        canvas.Line(8, 164, 84, 164, new Color(133, 117, 88));
        canvas.Fill(new Rectangle(8, 239, 76, 9), new Color(65, 58, 46));
        for (var x = 27; x < 66; x += 8)
            canvas.Line(x, 222, x + 2, 233, new Color(32, 29, 26), 2);
        canvas.Fill(new Rectangle(17, 262, 65, 6), new Color(64, 46, 32));
        for (var x = 25; x < 78; x += 17)
        {
            canvas.Line(x, 264, x + 3, 293, new Color(100, 85, 60), 2);
            canvas.Line(x - 4, 275, x + 7, 273, new Color(133, 131, 118), 4);
        }
    }

    private static int Noise(int x, int y)
    {
        var hash = unchecked((uint)(x * 374761393 + y * 668265263 + 3187));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        return (int)((hash ^ (hash >> 16)) & 255);
    }

    private sealed class Canvas
    {
        private readonly Rectangle _bounds = GothicRoomLayout.Bounds;
        internal Color[] Pixels { get; } = new Color[GothicRoomLayout.Bounds.Width * GothicRoomLayout.Bounds.Height];

        internal void Set(int x, int y, Color color)
        {
            if (_bounds.Contains(x, y))
                Pixels[(y - _bounds.Y) * _bounds.Width + x - _bounds.X] = color;
        }

        internal void Fill(Rectangle rectangle, Color color)
        {
            var clipped = Rectangle.Intersect(rectangle, _bounds);
            for (var y = clipped.Top; y < clipped.Bottom; y++)
                for (var x = clipped.Left; x < clipped.Right; x++)
                    Set(x, y, color);
        }

        internal void Circle(int cx, int cy, int radius, Color color)
        {
            for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius)
                        Set(cx + x, cy + y, color);
        }

        internal void Line(int x1, int y1, int x2, int y2, Color color, int thickness = 1)
        {
            var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            for (var index = 0; index <= steps; index++)
            {
                var t = steps == 0 ? 0 : index / (float)steps;
                Fill(new Rectangle((int)MathF.Round(x1 + (x2 - x1) * t),
                    (int)MathF.Round(y1 + (y2 - y1) * t), thickness, thickness), color);
            }
        }

        internal void Arch(int center, int spring, int bottom, int halfWidth, Func<int, int, Color> color)
        {
            var radiusSquared = halfWidth * halfWidth * 4;
            for (var y = spring - (int)Math.Ceiling(halfWidth * Math.Sqrt(3)); y < bottom; y++)
                for (var x = center - halfWidth; x <= center + halfWidth; x++)
                {
                    var dy = y - spring;
                    var left = x - center + halfWidth;
                    var right = x - center - halfWidth;
                    if (y >= spring || left * left + dy * dy <= radiusSquared && right * right + dy * dy <= radiusSquared)
                        Set(x, y, color(x, y));
                }
        }
    }
}
