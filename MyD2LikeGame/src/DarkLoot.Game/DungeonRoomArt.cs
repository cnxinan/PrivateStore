using System;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

internal static class DungeonRoomArt
{
    internal static Rectangle Bounds => new(-80, -112, 3360, 704);

    internal static Color[] CreateRoomPixels()
    {
        var canvas = new Canvas();
        var bounds = Bounds;
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                var floor = IsFloor(x, y);
                var room = x < 1120 ? 0 : x < 2240 ? 1 : 2;
                var row = (y + 112) / (floor ? 48 : 28);
                var offsetX = x + 80 + row % 2 * 32;
                var column = offsetX / (floor ? 64 : 80);
                var localX = offsetX % (floor ? 64 : 80);
                var localY = (y + 112) % (floor ? 48 : 28);
                var grain = Noise(x, y);
                var shade = (floor ? 61 : 24) + Noise(column, row) % 13 + grain % 7;
                var color = !floor ? new Color(shade, shade + 1, shade + 4) : room switch
                {
                    0 => new Color(shade - 6, shade + 1, shade + 10),
                    1 => new Color(shade + 3, shade + 1, shade - 3),
                    _ => new Color(shade + 6, shade - 12, shade - 17)
                };
                if (localX < 2 || localY < 2)
                    color = floor ? new Color(32, 31, 34) : new Color(13, 15, 19);
                else if (localX == 2 || localY == 2)
                    color = Color.Lerp(color, new Color(128, 121, 104), .18f);
                if (floor)
                {
                    if (grain % 31 == 0)
                        color = Color.Lerp(color, new Color(24, 27, 30), .24f);
                    if (Noise(column, row) % 4 == 0 && localX is > 16 and < 55 &&
                        Math.Abs(localY - localX * .31f - Noise(column, row) % 14) < .7f)
                        color = Color.Lerp(color, new Color(23, 26, 30), .45f);
                    // Shade only true floor boundaries, never the shared corridor seams.
                    if (!IsFloor(x, y - 10) || !IsFloor(x, y + 10) ||
                        !IsFloor(x - 10, y) || !IsFloor(x + 10, y))
                        color = Color.Lerp(color, new Color(21, 23, 28), .36f);
                    var roomX = x - (room == 0 ? 0 : room == 1 ? 1120 : 2240);
                    var dx = roomX - 480f;
                    var dy = y - 280f;
                    var radius = MathF.Sqrt(dx * dx + dy * dy);
                    if (Math.Abs(radius - 133) < 1 || Math.Abs(radius - 141) < .8f ||
                        room == 2 && Math.Abs(radius - 95) < .8f)
                        color = Color.Lerp(color, room == 2 ? new Color(154, 107, 49) :
                            new Color(112, 121, 119), .52f);
                    if (room == 2 && Math.Abs(dx) < 44 && y is > 188 and < 492)
                    {
                        color = Color.Lerp(color, new Color(78, 22, 30), .35f);
                        if (Math.Abs(dx) > 41)
                            color = new Color(125, 93, 52);
                    }
                }
                canvas.Set(x, y, color);
            }
        }

        for (var room = 0; room < 3; room++)
        {
            var origin = room * 1120;
            var trim = room == 2 ? new Color(117, 91, 54) : new Color(86, 91, 88);
            canvas.WallFill(new Rectangle(origin - 80, -106, 1120, 5), trim);
            canvas.WallFill(new Rectangle(origin - 80, -10, 1120, 5), trim);
            canvas.WallFill(new Rectangle(origin - 80, 540, 1120, 5), trim);
            for (var x = 80; x < 960; x += 160)
            {
                DrawArch(canvas, origin + x, -91, room, trim);
                canvas.WallFill(new Rectangle(origin + x - 55, -110, 12, 110),
                    new Color(46, 48, 49));
                canvas.WallFill(new Rectangle(origin + x - 54, -104, 3, 97), trim);
                if (room == 1 || room == 2 && x % 320 == 80)
                    DrawBanner(canvas, origin + x + 61, -78, room);
            }
        }

        foreach (var start in new[] { 960, 2080 })
        {
            // Corridor cornices stop at y=210 and restart at y=330.
            canvas.WallFill(new Rectangle(start, 202, 160, 5), new Color(81, 78, 69));
            canvas.WallFill(new Rectangle(start, 207, 160, 3), new Color(15, 18, 23));
            canvas.WallFill(new Rectangle(start, 330, 160, 5), new Color(103, 93, 74));
            canvas.WallFill(new Rectangle(start, 335, 160, 6), new Color(18, 21, 26));
            foreach (var x in new[] { start - 7, start + 160 })
            {
                canvas.WallFill(new Rectangle(x, 174, 7, 36), new Color(79, 77, 69));
                canvas.WallFill(new Rectangle(x, 330, 7, 36), new Color(79, 77, 69));
            }
        }
        foreach (var x in new[] { -48, 3240 })
        {
            for (var y = 24; y < 520; y += 144)
            {
                canvas.WallFill(new Rectangle(x - 12, y, 24, 84), new Color(54, 57, 59));
                canvas.WallFill(new Rectangle(x - 18, y, 36, 7), new Color(94, 88, 75));
                canvas.WallFill(new Rectangle(x - 18, y + 76, 36, 8), new Color(73, 72, 65));
            }
        }
        return canvas.Pixels;
    }

    private static bool IsFloor(int x, int y) =>
        y >= 0 && y < 540 && (x >= 0 && x < 960 || x >= 1120 && x < 2080 ||
            x >= 2240 && x < 3200 || y >= 210 && y < 330 &&
            (x >= 960 && x < 1120 || x >= 2080 && x < 2240));

    private static void DrawArch(Canvas canvas, int center, int top, int room, Color trim)
    {
        for (var y = top; y < -17; y++)
        {
            var half = Math.Min(35, (y - top) * .72f + 2);
            for (var x = -35; x <= 35; x++)
            {
                if (Math.Abs(x) > half)
                    continue;
                var edge = half - Math.Abs(x) < 5 || y > -23;
                var color = edge ? trim : room switch
                {
                    0 => new Color(17, 25, 33),
                    1 => new Color(20, 33, 42),
                    _ => new Color(48, 19, 29)
                };
                if (!edge && room == 0 && y > -67 && y < -42 && Math.Abs(x) < 19)
                    color = new Color(45 + Noise(x, y) % 8, 49, 54);
                if (!edge && room != 0 && (Math.Abs(x) < 2 || (x + y + 200) % 21 < 2))
                    color = new Color(12, 17, 23);
                canvas.SetWall(center + x, y, color);
            }
        }
        canvas.WallFill(new Rectangle(center - 40, -16, 80, 5), trim);
    }

    private static void DrawBanner(Canvas canvas, int center, int top, int room)
    {
        for (var y = 0; y < 58; y++)
        {
            var half = y < 43 ? 15 : 58 - y;
            for (var x = -half; x <= half; x++)
            {
                var edge = Math.Abs(x) > half - 2;
                canvas.SetWall(center + x, top + y, edge ? new Color(147, 112, 57) :
                    room == 2 ? new Color(81 + Noise(x, y) % 9, 22, 30) :
                        new Color(32, 47 + Noise(x, y) % 9, 61));
            }
        }
        canvas.WallFill(new Rectangle(center - 20, top - 3, 40, 3), new Color(135, 105, 61));
        for (var y = 12; y < 38; y++)
        {
            var x = (int)(Math.Sin((y - 12) / 26f * Math.PI) * 7);
            canvas.SetWall(center + x, top + y, new Color(163, 129, 69));
            canvas.SetWall(center - x, top + y, new Color(163, 129, 69));
        }
    }

    private static int Noise(int x, int y)
    {
        var hash = unchecked((uint)(x * 374761393 + y * 668265263 + 7159));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        return (int)((hash ^ (hash >> 16)) & 255);
    }

    private sealed class Canvas
    {
        internal Color[] Pixels { get; } = new Color[Bounds.Width * Bounds.Height];

        internal void Set(int x, int y, Color color)
        {
            if (Bounds.Contains(x, y))
                Pixels[(y - Bounds.Y) * Bounds.Width + x - Bounds.X] = color;
        }

        internal void SetWall(int x, int y, Color color)
        {
            if (!IsFloor(x, y))
                Set(x, y, color);
        }

        internal void WallFill(Rectangle rectangle, Color color)
        {
            var clipped = Rectangle.Intersect(rectangle, Bounds);
            for (var y = clipped.Top; y < clipped.Bottom; y++)
                for (var x = clipped.Left; x < clipped.Right; x++)
                    SetWall(x, y, color);
        }
    }
}
