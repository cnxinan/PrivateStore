using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Loot;

namespace DarkLoot.Tests.Gameplay;

public sealed class LootGeneratorTests
{
    [Fact]
    public void Generate_WithSameSeed_ReturnsSameItemDefinitionsFromContent()
    {
        var content = CreateContent();
        var table = new LootTableDefinition(
            "loot.test",
            1,
            5,
            [
                new LootEntryDefinition("item.first", 1),
                new LootEntryDefinition("item.second", 3)
            ]);
        var generator = new LootGenerator();

        var first = generator.Generate(table, content, new Random(17));
        var second = generator.Generate(table, content, new Random(17));

        Assert.Equal(
            first.Select(item => item.DefinitionId),
            second.Select(item => item.DefinitionId));
        Assert.All(first, item => Assert.Contains(item.DefinitionId, content.Items.Keys));
        Assert.All(first, item =>
        {
            Assert.Equal(ItemRarity.Common, item.Rarity);
            Assert.Empty(item.Rolls);
        });
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void Generate_IncludesConfiguredDropCountBounds(int chosenCount, int expectedCount)
    {
        var table = new LootTableDefinition(
            "loot.test",
            2,
            4,
            [new LootEntryDefinition("item.first", 1)]);

        var items = new LootGenerator().Generate(
            table,
            CreateContent(),
            new FixedRandom(chosenCount));

        Assert.Equal(expectedCount, items.Count);
    }

    [Theory]
    [InlineData(0, "weapon.training_blade")]
    [InlineData(1, "armor.training_vest")]
    public void Generate_ProductionTableAlwaysDropsOneEqualWeightedEquipmentItem(
        int entryTicket,
        string expectedItemId)
    {
        var content = LoadProductionContent();
        var table = content.LootTables["loot.training_slime"];
        var random = new ScriptedRandom(1, entryTicket);

        var items = new LootGenerator().Generate(table, content, random);

        Assert.Equal(expectedItemId, Assert.Single(items).DefinitionId);
    }

    [Fact]
    public void ProductionLootTable_UsesSharedEqualWeightedEquipmentAndConfiguredRarityWeights()
    {
        var content = LoadProductionContent();
        var table = content.LootTables["loot.training_slime"];

        Assert.All(content.Monsters.Values, monster =>
            Assert.Equal("loot.training_slime", monster.LootTableId));
        Assert.Equal(1, table.MinDrops);
        Assert.Equal(1, table.MaxDrops);
        Assert.Equal(
            new[] { "weapon.training_blade", "armor.training_vest" },
            table.Entries.Select(entry => entry.ItemId));
        Assert.All(table.Entries, entry =>
        {
            Assert.True(entry.Weight > 0);
            Assert.False(entry.Guaranteed);
        });
        Assert.Equal(table.Entries[0].Weight, table.Entries[1].Weight);
        Assert.Equal(
            new[]
            {
                new RarityWeightDefinition(ItemRarity.Common, 1),
                new RarityWeightDefinition(ItemRarity.Magic, 4),
                new RarityWeightDefinition(ItemRarity.Rare, 2)
            },
            table.RarityWeights);
    }

    [Theory]
    [InlineData(0, "item.first")]
    [InlineData(1, "item.second")]
    [InlineData(3, "item.second")]
    public void Generate_SelectsEntryByWeightTicket(int ticket, string expectedItemId)
    {
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [
                new LootEntryDefinition("item.first", 1),
                new LootEntryDefinition("item.second", 3)
            ]);

        var items = new LootGenerator().Generate(
            table,
            CreateContent(),
            new FixedRandom(ticket));

        Assert.Equal(expectedItemId, Assert.Single(items).DefinitionId);
    }

    [Fact]
    public void Generate_WhenTableReferencesMissingItem_ThrowsWithoutReturningLoot()
    {
        var table = new LootTableDefinition(
            "loot.broken",
            1,
            1,
            [new LootEntryDefinition("item.missing", 1)]);

        var error = Assert.Throws<ArgumentException>(
            () => new LootGenerator().Generate(table, CreateContent(), new Random(1)));

        Assert.Contains("item.missing", error.Message);
        Assert.Contains("loot.broken", error.Message);
    }

    [Fact]
    public void Generate_WithSeededRarityWeights_RollsStableDistinctAffixes()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0)
                { Slot = ItemSlot.Weapon }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["affix.attack"] = new("affix.attack", "Keen", ItemStat.Attack, 1, 3, [ItemSlot.Weapon]),
                ["affix.health"] = new("affix.health", "Vital", ItemStat.MaximumHealth, 2, 4, [ItemSlot.Weapon]),
                ["affix.crit"] = new("affix.crit", "Lucky", ItemStat.CriticalChance, 1, 5, [ItemSlot.Weapon])
            });
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [new LootEntryDefinition("weapon.blade", 1)],
            [new RarityWeightDefinition(ItemRarity.Rare, 1)]);

        var first = Assert.Single(new LootGenerator().Generate(table, content, new Random(81)));
        var second = Assert.Single(new LootGenerator().Generate(table, content, new Random(81)));

        Assert.Equal(ItemRarity.Rare, first.Rarity);
        Assert.Equal(first.Rolls, second.Rolls);
        Assert.Equal(2, first.Rolls.Count);
        Assert.Equal(2, first.Rolls.Select(roll => roll.AffixId).Distinct().Count());
        Assert.All(first.Rolls, roll => Assert.InRange(roll.Value,
            content.Affixes[roll.AffixId].MinValue,
            content.Affixes[roll.AffixId].MaxValue));
    }

    [Fact]
    public void Generate_WhenSelectableRarityHasTooFewEligibleAffixes_RejectsTable()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0)
                { Slot = ItemSlot.Weapon }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["affix.attack"] = new("affix.attack", "Keen", ItemStat.Attack, 1, 3, [ItemSlot.Weapon])
            });
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [new LootEntryDefinition("weapon.blade", 1)],
            [new RarityWeightDefinition(ItemRarity.Rare, 1)]);

        var error = Assert.Throws<ArgumentException>(
            () => new LootGenerator().Generate(table, content, new Random(81)));

        Assert.Contains("Rare", error.Message);
        Assert.Contains("affix", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_IncludesGuaranteedEntriesForEachEncounter()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0),
                ["armor.vest"] = new("armor.vest", "Vest", 0) { Slot = ItemSlot.Armor }
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
        var table = new LootTableDefinition(
            "loot.test",
            2,
            2,
            [
                new LootEntryDefinition("weapon.blade", 1) { Guaranteed = true },
                new LootEntryDefinition("armor.vest", 1) { Guaranteed = true }
            ]);

        var items = new LootGenerator().Generate(table, content, new Random(81));

        Assert.Equal(
            new[] { "weapon.blade", "armor.vest" },
            items.Select(item => item.DefinitionId));
        Assert.All(items, item => Assert.Equal(ItemRarity.Common, item.Rarity));
    }

    [Theory]
    [InlineData(9, 1d)]
    [InlineData(1, double.NaN)]
    [InlineData(1, 0d)]
    public void Generate_RejectsInvalidRarityEnumsAndWeights(int rarity, double weight)
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0)
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [new LootEntryDefinition("weapon.blade", 1)],
            [new RarityWeightDefinition((ItemRarity)rarity, weight)]);

        Assert.Throws<ArgumentException>(() =>
            new LootGenerator().Generate(table, content, new Random(81)));
    }

    [Fact]
    public void Generate_RejectsAffixRangesOutsideSupportedBounds()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0)
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["affix.broken"] = new(
                    "affix.broken", "Broken", ItemStat.Attack, 2, 1, [ItemSlot.Weapon])
            });
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [new LootEntryDefinition("weapon.blade", 1)],
            [new RarityWeightDefinition(ItemRarity.Magic, 1)]);

        Assert.Throws<ArgumentException>(() =>
            new LootGenerator().Generate(table, content, new Random(81)));
    }

    [Theory]
    [InlineData(ItemRarity.Common, 0)]
    [InlineData(ItemRarity.Magic, 1)]
    [InlineData(ItemRarity.Rare, 2)]
    public void Generate_ProducesConfiguredExactAffixCount(ItemRarity rarity, int expectedCount)
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>
            {
                ["weapon.blade"] = new("weapon.blade", "Blade", 0)
            },
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>(),
            new Dictionary<string, AffixDefinition>
            {
                ["affix.attack"] = new("affix.attack", "Keen", ItemStat.Attack, 1, 3, [ItemSlot.Weapon]),
                ["affix.health"] = new("affix.health", "Vital", ItemStat.MaximumHealth, 2, 4, [ItemSlot.Weapon]),
                ["affix.crit"] = new("affix.crit", "Lucky", ItemStat.CriticalChance, 1, 5, [ItemSlot.Weapon])
            });
        var table = new LootTableDefinition(
            "loot.test",
            1,
            1,
            [new LootEntryDefinition("weapon.blade", 1)],
            [new RarityWeightDefinition(rarity, 1)]);

        var item = Assert.Single(new LootGenerator().Generate(table, content, new Random(81)));

        Assert.Equal(rarity, item.Rarity);
        Assert.Equal(expectedCount, item.Rolls.Count);
        Assert.Equal(expectedCount, item.Rolls.Select(roll => roll.AffixId).Distinct().Count());
        var immutableRolls = Assert.IsAssignableFrom<IList<AffixRoll>>(item.Rolls);
        Assert.Throws<NotSupportedException>(() =>
            immutableRolls.Add(new AffixRoll("affix.fake", ItemStat.Attack, 1)));
    }

    private static GameContent CreateContent()
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["item.first"] = new("item.first", "First", 1),
            ["item.second"] = new("item.second", "Second", 2)
        };
        return new GameContent(items, new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());
    }

    private static GameContent LoadProductionContent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "content",
                   "definitions",
                   "loot-tables",
                   "training-slime.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return GameContentLoader.LoadDirectory(
            Path.Combine(directory!.FullName, "content", "definitions"));
    }

    private sealed class ScriptedRandom(params long[] values) : Random
    {
        private int _index;

        public override long NextInt64(long minValue, long maxValue) =>
            Math.Clamp(values[_index++], minValue, maxValue - 1);

        public override long NextInt64(long maxValue) =>
            Math.Clamp(values[_index++], 0, maxValue - 1);

        public override double NextDouble() => 0;
    }

    private sealed class FixedRandom(int nextValue) : Random
    {
        public override long NextInt64(long minValue, long maxValue)
        {
            return Math.Clamp((long)nextValue, minValue, maxValue - 1);
        }

        public override long NextInt64(long maxValue)
        {
            return Math.Clamp((long)nextValue, 0, maxValue - 1);
        }
    }
}
