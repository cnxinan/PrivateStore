using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class ItemStatCalculatorTests
{
    [Fact]
    public void Calculate_CombinesBothSlotsAndDoesNotMutateSnapshots()
    {
        var content = MixedEncounterFixture.CreateContent();
        var weapon = new ItemInstance(
            Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
            [new AffixRoll("affix.keen", ItemStat.Attack, 3)]);
        var armor = new ItemInstance(
            Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
            [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 20)]);
        var calculator = new ItemStatCalculator();

        Assert.Equal(new ItemStatContribution(ItemSlot.Weapon, 8, 0, 0),
            calculator.GetContribution(weapon, content));
        Assert.Equal(new PlayerStatTotals(18, 120, 0),
            calculator.Calculate(10, 100, weapon, armor, content));
        Assert.Equal(3, Assert.Single(weapon.Rolls).Value);
        Assert.Equal(20, Assert.Single(armor.Rolls).Value);
    }

    [Fact]
    public void Preview_UsesCandidateSlotWithoutChangingInventory()
    {
        var content = MixedEncounterFixture.CreateContent();
        var inventory = new Inventory();
        var candidate = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var calculator = new ItemStatCalculator();

        Assert.Equal(new PlayerStatTotals(15, 100, 0),
            calculator.Preview(10, 100, inventory, content, ItemSlot.Weapon, candidate));
        Assert.Empty(inventory.Items);
        Assert.Null(inventory.EquippedWeapon);
        Assert.Throws<ArgumentException>(() =>
        {
            calculator.Calculate(10, 100, null, candidate, content);
        });
    }

    [Fact]
    public void Calculate_CombinesSnapshotsAndKeepsUnchangedWeaponContribution()
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
        var calculator = new ItemStatCalculator();

        Assert.Equal(new PlayerStatTotals(15, 120, 0),
            calculator.Calculate(10, 100, weapon, armor, content));
        Assert.Equal(new PlayerStatTotals(15, 100, 15),
            calculator.Calculate(10, 100, weapon, replacement, content));
        Assert.Equal(20, Assert.Single(armor.Rolls).Value);
        Assert.Equal(3, Assert.Single(weapon.Rolls).Value);
    }

    [Fact]
    public void Calculate_ClampsCriticalChanceAfterCombiningBothSlots()
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
        var weapon = new ItemInstance(
            Guid.NewGuid(), "item.weapon", ItemRarity.Magic,
            [new AffixRoll("crit.weapon", ItemStat.CriticalChance, 80)]);
        var armor = new ItemInstance(
            Guid.NewGuid(), "item.armor", ItemRarity.Magic,
            [new AffixRoll("crit.armor", ItemStat.CriticalChance, 80)]);

        Assert.Equal(100,
            new ItemStatCalculator().Calculate(10, 100, weapon, armor, content).CriticalChance);
    }

    [Fact]
    public void GetContribution_UnknownDefinitionThrowsWithContentParameter()
    {
        var error = Assert.Throws<ArgumentException>(() =>
        {
            new ItemStatCalculator().GetContribution(
                new ItemInstance(Guid.NewGuid(), "item.missing"), CreateContent());
        });

        Assert.Equal("content", error.ParamName);
        Assert.Contains("item.missing", error.Message);
    }

    [Fact]
    public void GetContribution_MissingAffixThrowsWithItemParameter()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["item.weapon"] = new("item.weapon", "Weapon", 0)
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
        var item = new ItemInstance(
            Guid.NewGuid(), "item.weapon", ItemRarity.Magic,
            [new AffixRoll("affix.missing", ItemStat.Attack, 1)]);

        var error = Assert.Throws<ArgumentException>(() =>
        {
            new ItemStatCalculator().GetContribution(item, content);
        });

        Assert.Equal("item", error.ParamName);
        Assert.Contains("affix.missing", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(int.MaxValue)]
    public void GetContribution_InvalidAttackBonusThrowsWithContentParameter(int attackBonus)
    {
        var error = Assert.Throws<ArgumentException>(() =>
        {
            new ItemStatCalculator().GetContribution(
                new ItemInstance(Guid.NewGuid(), "item.weapon"), CreateContent(attackBonus));
        });

        Assert.Equal("content", error.ParamName);
        Assert.Contains("item.weapon", error.Message);
        Assert.Contains("AttackBonus", error.Message);
    }

    [Theory]
    [InlineData(10, 1_000_010)]
    [InlineData(int.MaxValue - 1_000_000, int.MaxValue)]
    public void Calculate_MaximumSupportedAttackBonusReturnsExactDamage(int baseDamage, int expected)
    {
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");

        Assert.Equal(expected,
            new ItemStatCalculator().Calculate(
                baseDamage, 1, item, null, CreateContent(1_000_000)).AttackDamage);
    }

    [Fact]
    public void Calculate_AttackDamageOverflowRejectsBaseDamageClearly()
    {
        var item = new ItemInstance(Guid.NewGuid(), "item.weapon");

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new ItemStatCalculator().Calculate(
                int.MaxValue - 1_000_000 + 1, 1, item, null, CreateContent(1_000_000));
        });

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
