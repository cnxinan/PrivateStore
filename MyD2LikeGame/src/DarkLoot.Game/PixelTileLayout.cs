using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

internal readonly record struct PixelTile(Rectangle World, Rectangle Source);

internal static class PixelTileLayout
{
    internal static IEnumerable<PixelTile> Floor(Rectangle bounds, int firstTile)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width % 2 != 0 || bounds.Height % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Pixel floors require positive, even dimensions.");
        if (firstTile is < 0 or > 28 || firstTile % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(firstTile));
        for (var y = 0; y < bounds.Height; y += 64)
            for (var x = 0; x < bounds.Width; x += 64)
            {
                var width = Math.Min(64, bounds.Width - x);
                var height = Math.Min(64, bounds.Height - y);
                var slot = firstTile + (x / 64 * 3 + y / 64 * 5) % 4;
                yield return new PixelTile(new Rectangle(bounds.X + x, bounds.Y + y, width, height),
                    new Rectangle(slot % 8 * 32, slot / 8 * 32, width / 2, height / 2));
            }
    }
}
