using Microsoft.Xna.Framework;

namespace DarkLoot.Engine.Rendering;

public static class LootLabelLayout
{
    public static Rectangle Place(Rectangle desired, IReadOnlyList<Rectangle> occupied, Rectangle viewport)
    {
        const int gap = 4;
        var width = Math.Min(desired.Width, viewport.Width);
        var x = Math.Clamp(desired.X, viewport.Left, viewport.Right - width);
        var preferredY = Math.Clamp(desired.Y, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - desired.Height));
        var candidates = occupied.SelectMany(row => new[] { row.Bottom + gap, row.Top - desired.Height - gap })
            .Concat([preferredY, viewport.Top, viewport.Bottom - desired.Height])
            .Distinct()
            .OrderBy(y => Math.Abs((long)y - preferredY));

        foreach (var y in candidates)
        {
            var candidate = new Rectangle(x, y, width, desired.Height);
            if (viewport.Contains(candidate) && !occupied.Any(row => row.Intersects(candidate)))
            {
                return candidate;
            }
        }

        // When the viewport is full, keep excess rows separate rather than covering existing loot.
        return new Rectangle(x, Math.Max(viewport.Bottom, occupied.Max(row => row.Bottom) + gap),
            width, desired.Height);
    }
}
