using DarkLoot.Data;

namespace DarkLoot.Tests.Data;

public sealed class GameContentLoaderTests
{
    [Fact]
    public void LoadDirectory_ValidDefinitions_ReturnsContentKeyedById()
    {
        var root = ContentFixture.CreateValid();

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            var item = Assert.Single(content.Items).Value;
            Assert.Equal("item.training_sword", item.Id);
            Assert.Equal("Training Sword", item.Name);
            Assert.Equal(3, item.AttackBonus);

            var monster = Assert.Single(content.Monsters).Value;
            Assert.Equal("monster.training_slime", monster.Id);
            Assert.Equal(12, monster.MaxHealth);
            Assert.Equal("loot.training_slime", monster.LootTableId);

            var lootTable = Assert.Single(content.LootTables).Value;
            Assert.Equal("loot.training_slime", lootTable.Id);
            Assert.Equal("item.training_sword", Assert.Single(lootTable.Entries).ItemId);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_MalformedJson_ReportsOffendingPath()
    {
        var root = ContentFixture.CreateMalformedItem();
        var itemPath = Path.Combine(root, "items", "training-sword.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(itemPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_MissingRequiredProperty_ReportsOffendingPath()
    {
        var root = ContentFixture.CreateItemMissingAttackBonus();
        var itemPath = Path.Combine(root, "items", "training-sword.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(itemPath, error.Message);
            Assert.Contains("AttackBonus", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_MissingLootTableReference_ReportsMonsterAndId()
    {
        var root = ContentFixture.CreateMonsterWithoutLootTable();

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains("monster.training_slime", error.Message);
            Assert.Contains("loot.training_slime", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_MissingItemReference_ReportsLootTableAndId()
    {
        var root = ContentFixture.CreateLootTableWithMissingItem();

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains("loot.training_slime", error.Message);
            Assert.Contains("item.training_sword", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_NonPositiveMonsterHealth_ReportsSourcePath()
    {
        var root = ContentFixture.CreateMonsterWithInvalidMaxHealth();
        var monsterPath = Path.Combine(root, "monsters", "training-slime.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(monsterPath, error.Message);
            Assert.Contains("MaxHealth", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_NonPositiveLootWeight_ReportsSourcePath()
    {
        var root = ContentFixture.CreateLootTableWithInvalidWeight();
        var lootTablePath = Path.Combine(root, "loot-tables", "training-slime.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(lootTablePath, error.Message);
            Assert.Contains("Weight", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_InvalidDropRange_ReportsSourcePath()
    {
        var root = ContentFixture.CreateLootTableWithInvalidDropRange();
        var lootTablePath = Path.Combine(root, "loot-tables", "training-slime.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(lootTablePath, error.Message);
            Assert.Contains("MinDrops", error.Message);
            Assert.Contains("MaxDrops", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_EmptyEntriesWithPositiveMaxDrops_ReportsSourceAndDefinition()
    {
        var root = ContentFixture.CreateLootTableWithNoEntries(maxDrops: 1);
        var lootTablePath = Path.Combine(root, "loot-tables", "training-slime.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(lootTablePath, error.Message);
            Assert.Contains("loot.training_slime", error.Message);
            Assert.Contains("Entries", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_EmptyEntriesWithZeroMaxDrops_IsValid()
    {
        var root = ContentFixture.CreateLootTableWithNoEntries(maxDrops: 0);

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            Assert.Empty(content.LootTables["loot.training_slime"].Entries);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }
}
