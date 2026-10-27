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

    private static GameContent CreateContent()
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["item.weapon"] = new("item.weapon", "Weapon", 5)
        };
        return new GameContent(items, new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
    }
}
