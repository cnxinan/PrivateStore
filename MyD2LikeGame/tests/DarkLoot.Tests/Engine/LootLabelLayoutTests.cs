using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Engine;

public sealed class LootLabelLayoutTests
{
    [Theory]
    [InlineData(300, 240)]
    [InlineData(1100, 700)]
    public void Place_CoincidentDropsHaveSeparateVisibleRows(int x, int y)
    {
        var viewport = new Rectangle(8, 96, 1104, 616);
        var occupied = new List<Rectangle>();
        for (var index = 0; index < 12; index++)
        {
            var row = LootLabelLayout.Place(new Rectangle(x, y, 400, 28), occupied, viewport);
            Assert.True(viewport.Contains(row));
            Assert.All(occupied, previous => Assert.False(previous.Intersects(row)));
            occupied.Add(row);
        }
    }

    [Fact]
    public void Place_LeavesSeparatedDropsAtTheirPreferredPositions()
    {
        var desired = new Rectangle(500, 300, 250, 28);
        Assert.Equal(desired, LootLabelLayout.Place(desired,
            [new Rectangle(100, 300, 250, 28)], new Rectangle(8, 96, 1104, 616)));
    }
}
