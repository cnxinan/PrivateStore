using DarkLoot.Game;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class PixelTileLayoutTests
{
    [Theory]
    [InlineData(0, 0, 960, 540, 4)]
    [InlineData(960, 210, 160, 120, 24)]
    [InlineData(-80, -112, 1120, 704, 16)]
    public void Floor_CoversExactFootprintAndCropsPartialEdges(int x, int y, int width, int height, int first)
    {
        var bounds = new Rectangle(x, y, width, height);
        var tiles = PixelTileLayout.Floor(bounds, first).ToArray();
        Assert.All(tiles, tile =>
        {
            Assert.True(bounds.Contains(tile.World));
            Assert.True(new Rectangle(0, 0, 256, 128).Contains(tile.Source));
            Assert.Equal(tile.World.Width, tile.Source.Width * 2);
            Assert.Equal(tile.World.Height, tile.Source.Height * 2);
        });
        Assert.Equal(width * height, tiles.Sum(tile => tile.World.Width * tile.World.Height));
        Assert.Equal(bounds.Left, tiles.Min(tile => tile.World.Left));
        Assert.Equal(bounds.Bottom, tiles.Max(tile => tile.World.Bottom));
    }

    [Fact]
    public void Floor_DoesNotStretchTheLastPartialTile()
    {
        var tiles = PixelTileLayout.Floor(new Rectangle(0, 0, 96, 70), 0).ToArray();
        Assert.Equal(4, tiles.Length);
        Assert.Equal(new Rectangle(64, 64, 32, 6), tiles[^1].World);
        Assert.Equal(16, tiles[^1].Source.Width);
        Assert.Equal(3, tiles[^1].Source.Height);
    }
}
