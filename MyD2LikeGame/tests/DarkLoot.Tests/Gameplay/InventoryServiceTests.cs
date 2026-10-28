using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class InventoryServiceTests
{
    private static ArenaSimulation CreateArena(GameContent? content = null) =>
        new(content ?? MixedEncounterFixture.CreateContent(), new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("target", "monster.training_slime", 500, 270)));

    [Fact]
    public void Equip_BothSlotsResolveOwnedSnapshotsAndPreviewDoesNotChangeThem()
    {
        var content = MixedEncounterFixture.CreateContent();
        var arena = CreateArena(content);
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var armor = new ItemInstance(Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
            [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 20)]);
        var replacement = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
        foreach (var item in new[] { weapon, armor, replacement })
        {
            Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, item));
        }

        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, armor.InstanceId));
        var inventory = arena.Player.Inventory;
        Assert.Equal(new PlayerStatTotals(15, 120, 0), arena.Player.Stats);
        Assert.Equal(new PlayerStatTotals(15, 100, 0),
            new ItemStatCalculator().Preview(10, 100, inventory, content, ItemSlot.Armor, replacement));
        Assert.Equal(weapon.InstanceId, inventory.EquippedWeaponId);
        Assert.Equal(armor.InstanceId, inventory.EquippedArmorId);
        Assert.Same(weapon, inventory.EquippedWeapon);
        Assert.Same(armor, inventory.EquippedArmor);
        Assert.Equal(100, arena.Player.Health);
    }

    [Fact]
    public void Operations_RejectInvalidArgumentsWithoutChangingPlayer()
    {
        var arena = CreateArena();
        var service = arena.EquipmentOperations;
        var item = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        Assert.Throws<ArgumentNullException>(() => new InventoryService(null!));
        Assert.Throws<ArgumentNullException>(() => service.PickUp(null!, item));
        Assert.Throws<ArgumentNullException>(() => service.PickUp(arena.Player, null!));
        Assert.Throws<ArgumentNullException>(() => service.Equip(null!, item.InstanceId));
        Assert.Throws<ArgumentException>(() => service.PickUp(
            arena.Player, new ItemInstance(Guid.Empty, item.DefinitionId)));
        Assert.Throws<ArgumentException>(() => service.Equip(arena.Player, Guid.Empty));
        Assert.Throws<ArgumentException>(() => service.PickUp(
            arena.Player, new ItemInstance(Guid.NewGuid(), "missing.item")));
        Assert.Empty(arena.Player.Inventory.Items);
        Assert.Equal(100, arena.Player.Health);
    }

    [Fact]
    public void PickUp_DuplicateIdKeepsOriginalSnapshotAndOrder()
    {
        var arena = CreateArena();
        var original = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var other = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
        var duplicate = new ItemInstance(original.InstanceId, "armor.training_vest");
        var operations = arena.EquipmentOperations;

        Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
        Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, other));
        Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, original.InstanceId));
        Assert.Equal(EquipmentChangeResult.DuplicateInstance, operations.PickUp(arena.Player, duplicate));
        Assert.Equal(new[] { original, other }, arena.Player.Inventory.Items);
        Assert.Equal(original.InstanceId, arena.Player.Inventory.EquippedWeaponId);
        Assert.Same(original, arena.Player.Inventory.EquippedWeapon);
    }

    [Fact]
    public void Equip_NotOwnedDoesNotChangeHealthOrSlots()
    {
        var arena = CreateArena();
        var before = arena.Player.Stats;
        Assert.Equal(EquipmentChangeResult.NotOwned,
            arena.EquipmentOperations.Equip(arena.Player, Guid.NewGuid()));
        Assert.Equal(before, arena.Player.Stats);
        Assert.Equal(100, arena.Player.Health);
        Assert.Null(arena.Player.Inventory.EquippedWeaponId);
        Assert.Null(arena.Player.Inventory.EquippedArmorId);
    }

    [Fact]
    public void PickUp_EmptyDefinitionId_ThrowsWithoutChangingInventory()
    {
        var arena = CreateArena();

        var error = Assert.Throws<ArgumentException>(
            () => arena.EquipmentOperations.PickUp(
                arena.Player,
                new ItemInstance(Guid.NewGuid(), " ")));

        Assert.Contains("definition ID", error.Message);
        Assert.Empty(arena.Player.Inventory.Items);
    }

    [Fact]
    public void Equip_IncludesWeaponBonusOnlyAfterSuccessfulEquip()
    {
        var arena = CreateArena();
        var item = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var service = arena.EquipmentOperations;

        Assert.Equal(10, arena.Player.AttackDamage);
        Assert.Equal(EquipmentChangeResult.Applied, service.PickUp(arena.Player, item));
        Assert.Equal(10, arena.Player.AttackDamage);
        Assert.Equal(EquipmentChangeResult.Applied, service.Equip(arena.Player, item.InstanceId));
        Assert.Equal(15, arena.Player.AttackDamage);
        Assert.Same(item, arena.Player.Inventory.Find(item.InstanceId));
    }

    [Fact]
    public void Inventory_ItemsCannotBeModifiedThroughPublicCollection()
    {
        var arena = CreateArena();
        var inventory = arena.Player.Inventory;
        var item = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, item));

        var items = Assert.IsAssignableFrom<IList<ItemInstance>>(inventory.Items);

        Assert.Throws<NotSupportedException>(() => items.Add(item));
        Assert.Single(inventory.Items);
    }

    [Fact]
    public void PickUp_InvalidAffixThrowsBeforeOwnershipChanges()
    {
        var arena = CreateArena();
        var item = new ItemInstance(Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
            [new AffixRoll("affix.keen", ItemStat.Attack, 3)]);

        Assert.Throws<ArgumentException>(() =>
            arena.EquipmentOperations.PickUp(arena.Player, item));

        Assert.Empty(arena.Player.Inventory.Items);
        Assert.Null(arena.Player.Inventory.EquippedWeaponId);
        Assert.Null(arena.Player.Inventory.EquippedArmorId);
        Assert.Equal(100, arena.Player.Health);
    }

    [Fact]
    public void ReplaceOwned_EquippedSnapshotsInBothSlotsKeepIdentityOrderAndSiblingIsolation()
    {
        var arena = CreateArena(CreateContentWithVitalBounds());
        var operations = arena.EquipmentOperations;
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var sibling = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var armor = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
        foreach (var item in new[] { weapon, sibling, armor })
        {
            Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, item));
        }

        Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, weapon.InstanceId));
        Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, armor.InstanceId));
        var improvedWeapon = new ItemInstance(weapon.InstanceId, weapon.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.keen", ItemStat.Attack, 5)]);
        var improvedArmor = new ItemInstance(armor.InstanceId, armor.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 50)]);

        Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, improvedWeapon));
        Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, improvedArmor));

        Assert.Equal(weapon.InstanceId, arena.Player.Inventory.EquippedWeaponId);
        Assert.Equal(armor.InstanceId, arena.Player.Inventory.EquippedArmorId);
        Assert.Same(improvedWeapon, arena.Player.Inventory.EquippedWeapon);
        Assert.Same(improvedArmor, arena.Player.Inventory.EquippedArmor);
        Assert.Equal(new[] { improvedWeapon, sibling, improvedArmor }, arena.Player.Inventory.Items);
        Assert.Same(sibling, arena.Player.Inventory.Find(sibling.InstanceId));
        Assert.Empty(weapon.Rolls);
        Assert.Empty(armor.Rolls);
        Assert.Equal(20, arena.Player.AttackDamage);
        Assert.Equal(150, arena.Player.MaximumHealth);
        Assert.Equal(100, arena.Player.Health);
    }

    [Fact]
    public void ReplaceOwned_UnwornSnapshotDoesNotChangeStatsAndRejectsIdentityConversionAtomically()
    {
        var arena = CreateArena();
        var operations = arena.EquipmentOperations;
        var original = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
        var replacement = new ItemInstance(original.InstanceId, original.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.keen", ItemStat.Attack, 5)]);

        Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, replacement));
        Assert.Equal(10, arena.Player.AttackDamage);
        Assert.Same(replacement, arena.Player.Inventory.Find(original.InstanceId));

        Assert.Throws<ArgumentException>(() => operations.ReplaceOwned(arena.Player,
            new ItemInstance(original.InstanceId, "armor.training_vest")));
        Assert.Same(replacement, arena.Player.Inventory.Find(original.InstanceId));
        Assert.Null(arena.Player.Inventory.EquippedWeaponId);
        Assert.Null(arena.Player.Inventory.EquippedArmorId);
        Assert.Equal(100, arena.Player.Health);
        Assert.Equal(EquipmentChangeResult.NotOwned,
            operations.ReplaceOwned(arena.Player, new ItemInstance(Guid.NewGuid(), "weapon.training_blade")));
    }

    [Fact]
    public void ReplaceOwned_HealthIncreaseDoesNotHealDecreaseClampsAndInvalidCandidateDoesNotCommit()
    {
        var content = CreateContentWithVitalBounds(contactDamage: 10);
        var arena = new ArenaSimulation(content, new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("contact", "monster.training_slime", 160, 270)));
        var operations = arena.EquipmentOperations;
        var armor = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
        Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, armor));
        Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, armor.InstanceId));
        arena.Update(new PlayerIntent(Vector2.Zero, arena.Player.Position, false, false), 0);
        Assert.Equal(90, arena.Player.Health);

        var increased = new ItemInstance(armor.InstanceId, armor.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 50)]);
        Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, increased));
        Assert.Equal(150, arena.Player.MaximumHealth);
        Assert.Equal(90, arena.Player.Health);

        var decreased = new ItemInstance(armor.InstanceId, armor.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.vital", ItemStat.MaximumHealth, -50)]);
        Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, decreased));
        Assert.Equal(50, arena.Player.MaximumHealth);
        Assert.Equal(50, arena.Player.Health);

        var impossible = new ItemInstance(armor.InstanceId, armor.DefinitionId,
            ItemRarity.Magic, [new AffixRoll("affix.vital", ItemStat.MaximumHealth, -200)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => operations.ReplaceOwned(arena.Player, impossible));
        Assert.Same(decreased, arena.Player.Inventory.EquippedArmor);
        Assert.Same(decreased, arena.Player.Inventory.Find(armor.InstanceId));
        Assert.Equal(armor.InstanceId, arena.Player.Inventory.EquippedArmorId);
        Assert.Equal(50, arena.Player.Health);
        Assert.Equal(50, arena.Player.MaximumHealth);
    }

    [Fact]
    public void ReplaceOwned_EmptyIdAndInvalidAffixThrowWithoutChangingOwnedState()
    {
        var arena = CreateArena();
        var operations = arena.EquipmentOperations;
        var original = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
        Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
        Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, original.InstanceId));

        Assert.Throws<ArgumentException>(() => operations.ReplaceOwned(arena.Player,
            new ItemInstance(Guid.Empty, "armor.training_vest")));
        Assert.Throws<ArgumentException>(() => operations.ReplaceOwned(arena.Player,
            new ItemInstance(original.InstanceId, original.DefinitionId,
                ItemRarity.Magic, [new AffixRoll("affix.keen", ItemStat.Attack, 3)])));

        Assert.Same(original, arena.Player.Inventory.EquippedArmor);
        Assert.Same(original, arena.Player.Inventory.Find(original.InstanceId));
        Assert.Equal(original.InstanceId, arena.Player.Inventory.EquippedArmorId);
        Assert.Equal(100, arena.Player.Health);
    }

    private static GameContent CreateContentWithVitalBounds(int contactDamage = 0)
    {
        var fixture = MixedEncounterFixture.CreateContent(contactDamage);
        var affixes = fixture.Affixes.ToDictionary(pair => pair.Key, pair => pair.Value);
        affixes["affix.vital"] = new(
            "affix.vital", "Vital", ItemStat.MaximumHealth, -200, 50, [ItemSlot.Armor]);
        return new GameContent(fixture.Items, fixture.Monsters, fixture.LootTables, affixes);
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
}
