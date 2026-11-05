using DarkLoot.Game;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class DungeonArtTests
{
    [Fact]
    public void CryptMaterialIsOpaqueAndDeterministic()
    {
        var first = DungeonRoomArt.CreateRoomPixels();
        var second = DungeonRoomArt.CreateRoomPixels();

        Assert.Equal(first, second);
        Assert.All(first, pixel => Assert.Equal(255, pixel.A));
    }

    [Theory]
    [InlineData(960, 1120)]
    [InlineData(2080, 2240)]
    public void CorridorAndBothDoorwaysRemainFloorRatherThanWall(int start, int end)
    {
        var pixels = DungeonRoomArt.CreateRoomPixels();
        for (var x = start; x <= end; x += 8)
        {
            var floor = Brightness(pixels, x, 260);
            var wall = Brightness(pixels, Math.Min(x, end - 8), 160);
            Assert.True(floor > wall,
                $"Expected an open stone passage at {x},270.");
        }
    }

    [Fact]
    public void EntryBattleAndSanctumHaveDistinctMaterialPalettes()
    {
        var pixels = DungeonRoomArt.CreateRoomPixels();
        var entry = At(pixels, 700, 400);
        var battle = At(pixels, 1820, 400);
        var sanctum = At(pixels, 2940, 400);

        Assert.True(entry.B > entry.R);
        Assert.True(battle.R > battle.B);
        Assert.True(sanctum.R - sanctum.B > battle.R - battle.B);
        Assert.NotEqual(entry, battle);
        Assert.NotEqual(battle, sanctum);
    }

    private static Color At(Color[] pixels, int x, int y)
    {
        var bounds = DungeonRoomArt.Bounds;
        return pixels[(y - bounds.Y) * bounds.Width + x - bounds.X];
    }

    private static int Brightness(Color[] pixels, int left, int top)
    {
        var sum = 0;
        for (var y = top; y < top + 8; y++)
            for (var x = left; x < left + 8; x++)
            {
                var pixel = At(pixels, x, y);
                sum += pixel.R + pixel.G + pixel.B;
            }
        return sum;
    }
}
