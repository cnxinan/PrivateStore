using DarkLoot.Data;
using DarkLoot.Data.Definitions;
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
