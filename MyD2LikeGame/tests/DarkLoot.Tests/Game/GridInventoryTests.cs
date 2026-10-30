using System.Text.Json;
using DarkLoot.Engine.Input;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Resources;
using DarkLoot.Tests.Gameplay;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class GridInventoryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(65, 3)]
    public void Pages_SelectExactInstanceAndClampWithoutCapacityLimit(int count, int pages)
    {
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var panel = new InventoryPanel();
        var items = Enumerable.Range(0, count)
            .Select(_ => new ItemInstance(Guid.NewGuid(), "weapon.training_blade")).ToArray();
        foreach (var item in items)
            Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, item));
        panel.SetOpen(true);
        Assert.Equal(pages, panel.PageCount(arena.Player, content));
        for (var index = 0; index < pages + 2; index++)
            panel.Update(Click(panel.NextPageBounds(1120)), arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(pages - 1, panel.PageIndex);
        panel.Update(Click(panel.RowBounds(1120, 0)), arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(count == 0 ? (Guid?)null : items[(pages - 1) * 32].InstanceId,
            arena.Player.Inventory.EquippedWeaponId);
        if (count < 32)
        {
            var before = arena.Player.Inventory.EquippedWeaponId;
            panel.Update(Click(panel.RowBounds(1120, 31)), arena.Player, content, 1120, arena.EquipmentOperations);
            Assert.Equal(before, arena.Player.Inventory.EquippedWeaponId);
        }
    }

    [Fact]
    public void HoverAndTabNavigationDoNotMutateProgressAndOnlyGridWheelChangesPage()
    {
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var panel = new InventoryPanel();
        for (var index = 0; index < 33; index++)
            arena.EquipmentOperations.PickUp(arena.Player, new ItemInstance(Guid.NewGuid(), "weapon.training_blade"));
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", int.MaxValue);
        panel.SetOpen(true);
        var persistence = new ProgressPersistence(content);
        var before = JsonSerializer.Serialize(persistence.Capture(arena));
        var hover = Click(panel.RowBounds(1120, 0)) with { AttackPressed = false };
        panel.Update(hover, arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.NotNull(panel.Tooltip);
        panel.Update(hover with { PointerScreenPosition = System.Numerics.Vector2.Zero, ScrollDelta = -120 },
            arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(0, panel.PageIndex);
        panel.Update(hover with { ScrollDelta = -120 }, arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(1, panel.PageIndex);
        panel.Update(Click(panel.ResourceTabBounds(1120)), arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(0, panel.PageIndex);
        panel.Update(hover, arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Contains(panel.Tooltip!.Lines, line => line.Text.Contains(int.MaxValue.ToString()));
        panel.Update(Click(panel.RowBounds(1120, 0)), arena.Player, content, 1120, arena.EquipmentOperations);
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Capture(arena)));
    }

    [Fact]
    public void Tooltip_UsesOriginalRollsAndCurrentComparisonAndStaysInsidePlayfield()
    {
        var content = PersistenceFixture.LoadContent();
        var arena = PersistenceFixture.CreateProgress(content);
        var item = arena.Player.Inventory.Items[0];
        var tooltip = ItemTooltip.ForEquipment(arena.Player, item, content);
        Assert.Contains(tooltip.Lines, line => line.Text.Contains(
            ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content)));
        foreach (var roll in item.Rolls)
            Assert.Contains(tooltip.Lines, line => line.Text.Contains(roll.Value.ToString()));
        Assert.Contains(tooltip.Lines, line => line.Text.Contains(GameText.Equipped.Trim()));
        var layout = new UiLayout(1120);
        var bounds = ItemTooltip.Place(new System.Numerics.Vector2(1119, 719), 286, 260, layout);
        Assert.True(new Rectangle(0, 0, 1120, layout.Console.Top).Contains(bounds));
        var wrapped = ItemTooltip.Wrap("长名称与完整属性不能截断", text => text.Length * 16, 48);
        Assert.Equal("长名称与完整属性不能截断", string.Concat(wrapped));
        Assert.All(wrapped, line => Assert.True(line.Length <= 3));
    }

    private static GameInputSnapshot Click(Rectangle bounds) => new(
        System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitX,
        new System.Numerics.Vector2(bounds.Center.X, bounds.Center.Y), true, false, false, false, false);
}
