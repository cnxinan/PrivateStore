using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class InventoryServiceTests
{
    [Fact]
    public void TryEquip_ItemNotInInventory_ReturnsFalseWithoutChangingEquipment()
    {
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();

        var equipped = service.TryEquip(inventory, item.InstanceId, CreateContent());

        Assert.False(equipped);
        Assert.Null(inventory.EquippedWeapon);
    }

    [Fact]
    public void TryPickUp_DuplicateInstanceId_ReturnsFalseWithoutAddingDuplicate()
    {
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();

        Assert.True(service.TryPickUp(inventory, item));
        Assert.False(service.TryPickUp(
            inventory,
            new ItemInstance(item.InstanceId, "item.other")));

        Assert.Equal(item, Assert.Single(inventory.Items));
    }

    [Fact]
    public void TryPickUp_EmptyDefinitionId_ThrowsWithoutChangingInventory()
    {
        var inventory = new Inventory();

        var error = Assert.Throws<ArgumentException>(
            () => new InventoryService().TryPickUp(
                inventory,
                new ItemInstance(Guid.NewGuid(), " ")));

        Assert.Contains("definition ID", error.Message);
        Assert.Empty(inventory.Items);
    }

    [Fact]
    public void TryEquip_ItemWithUnknownDefinition_ThrowsWithoutChangingEquipment()
    {
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.missing");
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));

        var error = Assert.Throws<ArgumentException>(
            () => service.TryEquip(inventory, item.InstanceId, CreateContent()));

        Assert.Contains("item.missing", error.Message);
        Assert.Null(inventory.EquippedWeapon);
    }

    [Fact]
    public void GetAttackDamage_IncludesEquippedWeaponBonusOnlyAfterSuccessfulEquip()
    {
        var inventory = new Inventory();
        var content = CreateContent();
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();

        Assert.Equal(10, service.GetAttackDamage(10, inventory, content));
        Assert.True(service.TryPickUp(inventory, item));
        Assert.True(service.TryEquip(inventory, item.InstanceId, content));
        Assert.Equal(15, service.GetAttackDamage(10, inventory, content));
    }

    [Fact]
    public void Inventory_ItemsCannotBeModifiedThroughPublicCollection()
    {
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        Assert.True(new InventoryService().TryPickUp(inventory, item));

        var items = Assert.IsAssignableFrom<IList<ItemInstance>>(inventory.Items);

        Assert.Throws<NotSupportedException>(() => items.Add(item));
        Assert.Single(inventory.Items);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(int.MaxValue)]
    public void TryEquip_InvalidAttackBonus_ThrowsWithoutChangingEquipment(int attackBonus)
    {
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));

        var error = Assert.Throws<ArgumentException>(
            () => service.TryEquip(inventory, item.InstanceId, CreateContent(attackBonus)));

        Assert.Equal("content", error.ParamName);
        Assert.Contains("item.weapon", error.Message);
        Assert.Contains("AttackBonus", error.Message);
        Assert.Null(inventory.EquippedWeapon);
    }

    [Theory]
    [InlineData(10, 1_000_010)]
    [InlineData(int.MaxValue - 1_000_000, int.MaxValue)]
    public void GetAttackDamage_MaximumSupportedBonus_ReturnsExactDamage(int baseDamage, int expected)
    {
        var inventory = new Inventory();
        var content = CreateContent(1_000_000);
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));
        Assert.True(service.TryEquip(inventory, item.InstanceId, content));

        Assert.Equal(expected, service.GetAttackDamage(baseDamage, inventory, content));
    }

    [Fact]
    public void GetAttackDamage_BaseDamageTooLargeForBonus_RejectsBaseDamageClearly()
    {
        var inventory = new Inventory();
        var content = CreateContent(1_000_000);
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));
        Assert.True(service.TryEquip(inventory, item.InstanceId, content));

        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => service.GetAttackDamage(int.MaxValue - 1_000_000 + 1, inventory, content));

        Assert.Equal("baseDamage", error.ParamName);
        Assert.Contains("AttackBonus", error.Message);
    }

    private static GameContent CreateContent(int attackBonus = 5)
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["item.weapon"] = new("item.weapon", "Weapon", attackBonus)
        };
        return new GameContent(items, new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
    }
}
