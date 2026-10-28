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

    [Fact]
    public void TryEquip_RejectsItemInDifferentSlotWithoutChangingEitherSlot()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["item.armor"] = new("item.armor", "Armor", 0) { Slot = ItemSlot.Armor }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
        var inventory = new Inventory();
        var item = new ItemInstance(Guid.NewGuid(), "item.armor");
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));

        Assert.Throws<ArgumentException>(() =>
            service.TryEquip(inventory, item.InstanceId, content, ItemSlot.Weapon));

        Assert.Null(inventory.EquippedWeapon);
        Assert.Null(inventory.EquippedArmor);
    }

    [Fact]
    public void ItemNameFormatter_ComposesPrefixAndSuffixAffixes()
    {
        var item = new ItemInstance(
            Guid.NewGuid(),
            "item.blade",
            ItemRarity.Rare,
            [
                new AffixRoll("keen", ItemStat.Attack, 3),
                new AffixRoll("fortune", ItemStat.CriticalChance, 5)
            ]);
        var definition = new ItemDefinition("item.blade", "Blade", 0);
        var content = new GameContent(
            new Dictionary<string, ItemDefinition> { ["item.blade"] = definition },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["keen"] = new("keen", "Keen", ItemStat.Attack, 1, 3, [ItemSlot.Weapon])
                { NameKind = AffixNameKind.Prefix },
                ["fortune"] = new(
                    "fortune", "of Fortune", ItemStat.CriticalChance, 1, 5, [ItemSlot.Weapon])
                { NameKind = AffixNameKind.Suffix }
            });

        Assert.Equal("Keen Blade of Fortune",
            ItemNameFormatter.GetDisplayName(item, definition, content));
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
