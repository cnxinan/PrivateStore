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

    [Fact]
    public void TryEquip_WeaponAndArmorContributeToSharedTotalsAndReplacementPreview()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["item.weapon"] = new("item.weapon", "Weapon", 2) { Slot = ItemSlot.Weapon },
                ["item.armor"] = new("item.armor", "Armor", 0) { Slot = ItemSlot.Armor }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["attack"] = new("attack", "Keen", ItemStat.Attack, 3, 3, [ItemSlot.Weapon]),
                ["health"] = new("health", "Vital", ItemStat.MaximumHealth, 20, 20, [ItemSlot.Armor]),
                ["crit"] = new("crit", "Lucky", ItemStat.CriticalChance, 15, 15, [ItemSlot.Armor])
            });
        var weapon = new ItemInstance(
            Guid.NewGuid(), "item.weapon", ItemRarity.Magic,
            [new AffixRoll("attack", ItemStat.Attack, 3)]);
        var armor = new ItemInstance(
            Guid.NewGuid(), "item.armor", ItemRarity.Magic,
            [new AffixRoll("health", ItemStat.MaximumHealth, 20)]);
        var replacement = new ItemInstance(
            Guid.NewGuid(), "item.armor", ItemRarity.Magic,
            [new AffixRoll("crit", ItemStat.CriticalChance, 15)]);
        var inventory = new Inventory();
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, weapon));
        Assert.True(service.TryPickUp(inventory, armor));
        Assert.True(service.TryPickUp(inventory, replacement));
        Assert.True(service.TryEquip(inventory, weapon.InstanceId, content));
        Assert.True(service.TryEquip(inventory, armor.InstanceId, content));

        var current = service.GetTotals(10, 100, inventory, content);
        var preview = service.GetTotalsWithReplacement(
            10, 100, inventory, content, ItemSlot.Armor, replacement);

        Assert.Equal(new PlayerStatTotals(15, 120, 0), current);
        Assert.Equal(new PlayerStatTotals(15, 100, 15), preview);
        Assert.Equal(armor, inventory.EquippedArmor);
        Assert.Equal(weapon, inventory.EquippedWeapon);
        Assert.Equal(3, Assert.Single(weapon.Rolls).Value);
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
    public void TryEquip_RejectsRollThatReferencesMissingAffix()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["item.weapon"] = new("item.weapon", "Weapon", 0)
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
        var inventory = new Inventory();
        var item = new ItemInstance(
            Guid.NewGuid(), "item.weapon", ItemRarity.Magic,
            [new AffixRoll("affix.missing", ItemStat.Attack, 1)]);
        var service = new InventoryService();
        Assert.True(service.TryPickUp(inventory, item));

        var error = Assert.Throws<ArgumentException>(() =>
            service.TryEquip(inventory, item.InstanceId, content));

        Assert.Contains("affix.missing", error.Message);
        Assert.Null(inventory.EquippedWeapon);
    }

    [Fact]
    public void GetTotals_ClampsCriticalChanceAfterCombiningBothSlots()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["item.weapon"] = new("item.weapon", "Weapon", 0),
                ["item.armor"] = new("item.armor", "Armor", 0) { Slot = ItemSlot.Armor }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["crit.weapon"] = new("crit.weapon", "Lucky", ItemStat.CriticalChance, 80, 80, [ItemSlot.Weapon]),
                ["crit.armor"] = new("crit.armor", "Fortunate", ItemStat.CriticalChance, 80, 80, [ItemSlot.Armor])
            });
        var inventory = new Inventory();
        var service = new InventoryService();
        var weapon = new ItemInstance(
            Guid.NewGuid(), "item.weapon", ItemRarity.Magic,
            [new AffixRoll("crit.weapon", ItemStat.CriticalChance, 80)]);
        var armor = new ItemInstance(
            Guid.NewGuid(), "item.armor", ItemRarity.Magic,
            [new AffixRoll("crit.armor", ItemStat.CriticalChance, 80)]);
        Assert.True(service.TryPickUp(inventory, weapon));
        Assert.True(service.TryPickUp(inventory, armor));
        Assert.True(service.TryEquip(inventory, weapon.InstanceId, content));
        Assert.True(service.TryEquip(inventory, armor.InstanceId, content));

        Assert.Equal(100, service.GetTotals(10, 100, inventory, content).CriticalChance);
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
