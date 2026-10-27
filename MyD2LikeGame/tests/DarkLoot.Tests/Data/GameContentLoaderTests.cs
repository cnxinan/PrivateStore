using DarkLoot.Data;
using DarkLoot.Data.Definitions;

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
            Assert.Equal(DarkLoot.Data.Definitions.ItemSlot.Weapon, item.Slot);

            var monster = Assert.Single(content.Monsters).Value;
            Assert.Equal("monster.training_slime", monster.Id);
            Assert.Equal(12, monster.MaxHealth);
            Assert.Equal("loot.training_slime", monster.LootTableId);
            Assert.Equal(MonsterBehavior.Chase, monster.Behavior);
            Assert.Null(monster.RangedAttack);

            var lootTable = Assert.Single(content.LootTables).Value;
            Assert.Equal("loot.training_slime", lootTable.Id);
            Assert.Equal("item.training_sword", Assert.Single(lootTable.Entries).ItemId);
            Assert.Null(lootTable.RarityWeights);
            Assert.Empty(content.Encounters);
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

    [Theory]
    [InlineData(1_000_001)]
    [InlineData(int.MaxValue)]
    public void LoadDirectory_OversizedAttackBonus_ReportsSourcePathAndField(int attackBonus)
    {
        var root = ContentFixture.CreateItemWithAttackBonus(attackBonus);
        var itemPath = Path.Combine(root, "items", "training-sword.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(itemPath, error.Message);
            Assert.Contains("AttackBonus", error.Message);
            Assert.Contains("1000000", error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void LoadDirectory_SupportedAttackBonus_IsValid(int attackBonus)
    {
        var root = ContentFixture.CreateItemWithAttackBonus(attackBonus);

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            Assert.Equal(attackBonus, content.Items["item.training_sword"].AttackBonus);
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

    [Fact]
    public void LoadDirectory_ArcherAndEncounter_ParsesRangedAttackAndRepeatedMonsterDefinitions()
    {
        var root = ContentFixture.CreateWithAdditionalDefinitions(
            [ArcherJson, WolfJson],
            EncounterJson(
                """
                {"instanceId":"slime.1","monsterId":"monster.training_slime","x":480,"y":150},
                {"instanceId":"slime.2","monsterId":"monster.training_slime","x":480,"y":390},
                {"instanceId":"wolf.1","monsterId":"monster.fast_wolf","x":650,"y":180},
                {"instanceId":"wolf.2","monsterId":"monster.fast_wolf","x":650,"y":360},
                {"instanceId":"archer.1","monsterId":"monster.skeleton_archer","x":820,"y":150},
                {"instanceId":"archer.2","monsterId":"monster.skeleton_archer","x":820,"y":390}
                """));

        try
        {
            var content = GameContentLoader.LoadDirectory(root);
            var archer = content.Monsters["monster.skeleton_archer"];
            var attack = Assert.IsType<RangedAttackDefinition>(archer.RangedAttack);
            var encounter = content.Encounters["encounter.training_arena"];
            var spawns = encounter.Spawns;

            Assert.Equal(MonsterBehavior.Archer, archer.Behavior);
            Assert.Equal(260f, attack.Range);
            Assert.Equal(240f, attack.ProjectileSpeed);
            Assert.Equal(1.5f, attack.FireInterval);
            Assert.Equal(0.3f, attack.WindupSeconds);
            Assert.Equal(5, attack.Damage);
            Assert.Equal(6, spawns.Count);
            Assert.Equal(6, spawns.Select(spawn => spawn.InstanceId).Distinct().Count());
            Assert.Equal(
                2,
                spawns.Count(spawn => spawn.MonsterId == "monster.training_slime"));
            Assert.Equal(
                2,
                spawns.Count(spawn => spawn.MonsterId == "monster.fast_wolf"));
            Assert.Equal(
                2,
                spawns.Count(spawn => spawn.MonsterId == "monster.skeleton_archer"));
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Theory]
    [InlineData("Unknown", null)]
    [InlineData("Archer", null)]
    [InlineData("Chase", "{\"range\":260,\"projectileSpeed\":240,\"fireInterval\":1.5,\"windupSeconds\":0.3,\"damage\":5}")]
    public void LoadDirectory_InvalidMonsterBehaviorConfiguration_ReportsSourcePath(
        string behavior,
        string? rangedAttackJson)
    {
        var root = ContentFixture.CreateWithAdditionalDefinitions(
            [MonsterJson(behavior, rangedAttackJson)]);
        var monsterPath = Path.Combine(root, "monsters", "additional-monster-0.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(monsterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Theory]
    [InlineData("range", "0")]
    [InlineData("range", "1e1000")]
    [InlineData("projectileSpeed", "1e1000")]
    [InlineData("projectileSpeed", "0")]
    [InlineData("fireInterval", "1e1000")]
    [InlineData("fireInterval", "0")]
    [InlineData("windupSeconds", "1e1000")]
    [InlineData("windupSeconds", "0")]
    public void LoadDirectory_NonPositiveOrNonFiniteRangedValues_ReportsSourcePath(
        string field,
        string value)
    {
        var rangedAttackJson = RangedAttackJson(field, value);
        var root = ContentFixture.CreateWithAdditionalDefinitions(
            [MonsterJson("Archer", rangedAttackJson)]);
        var monsterPath = Path.Combine(root, "monsters", "additional-monster-0.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(monsterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_WindupLongerThanFireInterval_ReportsSourcePath()
    {
        var root = ContentFixture.CreateWithAdditionalDefinitions(
            [MonsterJson(
                "Archer",
                """{"range":260,"projectileSpeed":240,"fireInterval":1,"windupSeconds":1.1,"damage":5}""")]);
        var monsterPath = Path.Combine(root, "monsters", "additional-monster-0.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(monsterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_NegativeRangedDamage_ReportsSourcePath()
    {
        var root = ContentFixture.CreateWithAdditionalDefinitions(
            [MonsterJson(
                "Archer",
                """{"range":260,"projectileSpeed":240,"fireInterval":1.5,"windupSeconds":0.3,"damage":-1}""")]);
        var monsterPath = Path.Combine(root, "monsters", "additional-monster-0.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(monsterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Theory]
    [InlineData("""{"id":"encounter.training_arena","spawns":[]}""")]
    [InlineData("""{"id":"encounter.training_arena","spawns":null}""")]
    [InlineData("""{"id":"encounter.training_arena","spawns":[{"instanceId":"same","monsterId":"monster.training_slime","x":10,"y":10},{"instanceId":"same","monsterId":"monster.training_slime","x":20,"y":20}]}""")]
    [InlineData("""{"id":"encounter.training_arena","spawns":[{"instanceId":"missing","monsterId":"monster.unknown","x":10,"y":10}]}""")]
    public void LoadDirectory_InvalidEncounterSpawns_ReportsSourcePath(string encounterJson)
    {
        var root = ContentFixture.CreateWithAdditionalDefinitions(encounterJson: encounterJson);
        var encounterPath = Path.Combine(root, "encounters", "training-arena.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(encounterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Theory]
    [InlineData(-1, 200)]
    [InlineData(961, 200)]
    [InlineData(200, -1)]
    [InlineData(200, 541)]
    public void LoadDirectory_SpawnOutsideArenaBounds_ReportsSourcePath(float x, float y)
    {
        var encounterJson = $$"""
            {
              "id": "encounter.training_arena",
              "spawns": [
                {"instanceId":"slime.1","monsterId":"monster.training_slime","x":{{x}},"y":{{y}}}
              ]
            }
            """;
        var root = ContentFixture.CreateWithAdditionalDefinitions(encounterJson: encounterJson);
        var encounterPath = Path.Combine(root, "encounters", "training-arena.json");

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(encounterPath, error.Message);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    private static string MonsterJson(string behavior, string? rangedAttackJson)
    {
        var rangedAttack = rangedAttackJson is null
            ? string.Empty
            : $", \"rangedAttack\": {rangedAttackJson}";
        return $$"""
            {
              "id": "monster.skeleton_archer",
              "name": "骷髅弓手",
              "maxHealth": 20,
              "contactDamage": 0,
              "moveSpeed": 70,
              "lootTableId": "loot.training_slime",
              "behavior": "{{behavior}}"{{rangedAttack}}
            }
            """;
    }

    private static string RangedAttackJson(string invalidField, string invalidValue)
    {
        var values = new Dictionary<string, string>
        {
            ["range"] = "260",
            ["projectileSpeed"] = "240",
            ["fireInterval"] = "1.5",
            ["windupSeconds"] = "0.3",
            ["damage"] = "5"
        };
        values[invalidField] = invalidValue;

        return $$"""
            {
              "range": {{values["range"]}},
              "projectileSpeed": {{values["projectileSpeed"]}},
              "fireInterval": {{values["fireInterval"]}},
              "windupSeconds": {{values["windupSeconds"]}},
              "damage": {{values["damage"]}}
            }
            """;
    }

    private static string EncounterJson(string spawnsJson)
    {
        return $$"""
            {
              "id": "encounter.training_arena",
              "spawns": [{{spawnsJson}}]
            }
            """;
    }

    private const string ArcherJson =
        """{"id":"monster.skeleton_archer","name":"骷髅弓手","maxHealth":20,"contactDamage":0,"moveSpeed":70,"lootTableId":"loot.training_slime","behavior":"Archer","rangedAttack":{"range":260,"projectileSpeed":240,"fireInterval":1.5,"windupSeconds":0.3,"damage":5}}""";

    private const string WolfJson =
        """{"id":"monster.fast_wolf","name":"Fast Wolf","maxHealth":20,"contactDamage":5,"moveSpeed":140,"lootTableId":"loot.training_slime","behavior":"Chase"}""";
}
