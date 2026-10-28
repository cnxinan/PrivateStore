using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Tests.Data;

public sealed class GameContentLoaderTests
{
    [Fact]
    public void LoadDirectory_CraftingDefinitions_ReturnsDefinitionsKeyedById()
    {
        var root = ContentFixture.CreateWithCraftingDefinitions();

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            Assert.Equal("黏液", content.Materials["material.slime"].Name);
            Assert.Equal(ItemRarity.Magic, content.Recipes["recipe.magic_blade"].Rarity);
            Assert.Equal(
                20,
                content.ResourceLootTables["resource.slime"].RecipeChancePercent);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void GameContent_OmittedCraftingDictionaries_AreEmpty()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>(),
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());

        Assert.Empty(content.Materials);
        Assert.Empty(content.Recipes);
        Assert.Empty(content.ResourceLootTables);
    }

    [Theory]
    [InlineData("duplicate-material", "materials", "material-1.json", "material.slime")]
    [InlineData("duplicate-recipe", "recipes", "recipe-1.json", "recipe.magic_blade")]
    [InlineData("duplicate-resource-table", "resource-loot-tables", "resource-1.json", "resource.slime")]
    [InlineData("empty-material-id", "materials", "material-0.json", "Id")]
    [InlineData("empty-recipe-id", "recipes", "recipe-0.json", "Id")]
    [InlineData("empty-item-id", "recipes", "recipe-0.json", "ItemId")]
    [InlineData("unknown-cost-material", "recipes", "recipe-0.json", "material.unknown")]
    [InlineData("empty-cost-material", "recipes", "recipe-0.json", "MaterialId")]
    [InlineData("zero-cost", "recipes", "recipe-0.json", "Quantity")]
    [InlineData("negative-cost", "recipes", "recipe-0.json", "Quantity")]
    [InlineData("duplicate-cost-material", "recipes", "recipe-0.json", "material.slime")]
    [InlineData("null-costs", "recipes", "recipe-0.json", "Costs")]
    [InlineData("null-cost-entry", "recipes", "recipe-0.json", "Costs")]
    [InlineData("empty-costs", "recipes", "recipe-0.json", "Costs")]
    [InlineData("unknown-item", "recipes", "recipe-0.json", "item.unknown")]
    [InlineData("invalid-rarity", "recipes", "recipe-0.json", "Rarity")]
    [InlineData("insufficient-magic-affixes", "recipes", "recipe-0.json", "recipe.invalid")]
    [InlineData("insufficient-rare-affixes", "recipes", "recipe-0.json", "recipe.invalid")]
    [InlineData("empty-resource-id", "resource-loot-tables", "resource-0.json", "Id")]
    [InlineData("zero-minimum", "resource-loot-tables", "resource-0.json", "MinQuantity")]
    [InlineData("max-below-minimum", "resource-loot-tables", "resource-0.json", "MaxQuantity")]
    [InlineData("negative-recipe-chance", "resource-loot-tables", "resource-0.json", "RecipeChancePercent")]
    [InlineData("excessive-recipe-chance", "resource-loot-tables", "resource-0.json", "RecipeChancePercent")]
    [InlineData("empty-resource-material", "resource-loot-tables", "resource-0.json", "MaterialId")]
    [InlineData("unknown-resource-material", "resource-loot-tables", "resource-0.json", "material.unknown")]
    [InlineData("null-recipe-ids", "resource-loot-tables", "resource-0.json", "RecipeIds")]
    [InlineData("null-recipe-id", "resource-loot-tables", "resource-0.json", "RecipeIds")]
    [InlineData("missing-recipe-candidates", "resource-loot-tables", "resource-0.json", "RecipeIds")]
    [InlineData("duplicate-recipe-candidate", "resource-loot-tables", "resource-0.json", "recipe.magic_blade")]
    [InlineData("unknown-recipe-candidate", "resource-loot-tables", "resource-0.json", "recipe.unknown")]
    public void LoadDirectory_InvalidCraftingDefinitions_ReportsSourcePathAndFieldOrReference(
        string scenario,
        string directory,
        string fileName,
        string expectedMessage)
    {
        var root = CreateInvalidCraftingContent(scenario);
        var sourcePath = Path.Combine(root, directory, fileName);

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains(sourcePath, error.Message);
            Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_ZeroRecipeChanceAllowsNoCandidates()
    {
        var root = ContentFixture.CreateWithCraftingDefinitions(
            resourceLootTableJsons:
            [
                ResourceJson(
                    id: "resource.slime",
                    recipeChancePercent: 0,
                    recipeIdsJson: "[]")
            ]);

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            Assert.Empty(content.ResourceLootTables["resource.slime"].RecipeIds);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

    [Fact]
    public void LoadDirectory_MaximumResourceQuantity_IsValid()
    {
        var root = ContentFixture.CreateWithCraftingDefinitions(
            resourceLootTableJsons:
            [
                ResourceJson(
                    id: "resource.slime",
                    maxQuantity: int.MaxValue,
                    recipeChancePercent: 0,
                    recipeIdsJson: "[]")
            ]);

        try
        {
            var content = GameContentLoader.LoadDirectory(root);

            Assert.Equal(
                int.MaxValue,
                content.ResourceLootTables["resource.slime"].MaxQuantity);
        }
        finally
        {
            ContentFixture.Delete(root);
        }
    }

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
            Assert.Single(content.Materials);
            Assert.Empty(content.Recipes);
            Assert.Single(content.ResourceLootTables);

            var monster = Assert.Single(content.Monsters).Value;
            Assert.Equal("monster.training_slime", monster.Id);
            Assert.Equal(12, monster.MaxHealth);
            Assert.Equal("resource.slime", monster.LootTableId);
            Assert.Equal(MonsterBehavior.Chase, monster.Behavior);
            Assert.Null(monster.RangedAttack);

            var lootTable = Assert.Single(content.LootTables).Value;
            Assert.Equal("loot.test_equipment", lootTable.Id);
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
    public void LoadDirectory_MissingResourceLootTableReference_ReportsMonsterAndId()
    {
        var root = ContentFixture.CreateMonsterWithoutLootTable();

        try
        {
            var error = Assert.Throws<ContentLoadException>(
                () => GameContentLoader.LoadDirectory(root));

            Assert.Contains("monster.training_slime", error.Message);
            Assert.Contains("resource.slime", error.Message);
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

            Assert.Contains("loot.test_equipment", error.Message);
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
            Assert.Contains("loot.test_equipment", error.Message);
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

            Assert.Empty(content.LootTables["loot.test_equipment"].Entries);
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
              "lootTableId": "resource.slime",
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

    private static string CreateInvalidCraftingContent(string scenario)
    {
        var materialJsons = new[] { MaterialJson() };
        var recipeJsons = new[] { RecipeJson() };
        var resourceJsons = new[] { ResourceJson() };
        IEnumerable<string>? affixJsons = null;

        switch (scenario)
        {
            case "duplicate-material":
                materialJsons = [MaterialJson(), MaterialJson()];
                break;
            case "duplicate-recipe":
                recipeJsons = [RecipeJson(), RecipeJson()];
                break;
            case "duplicate-resource-table":
                resourceJsons = [ResourceJson(), ResourceJson()];
                break;
            case "empty-material-id":
                materialJsons = [MaterialJson(id: "")];
                break;
            case "empty-recipe-id":
                recipeJsons = [RecipeJson(id: "")];
                break;
            case "empty-item-id":
                recipeJsons = [RecipeJson(id: "recipe.invalid", itemId: "")];
                break;
            case "unknown-cost-material":
                recipeJsons = [RecipeJson(id: "recipe.invalid", materialId: "material.unknown")];
                break;
            case "empty-cost-material":
                recipeJsons = [RecipeJson(id: "recipe.invalid", materialId: "")];
                break;
            case "zero-cost":
                recipeJsons = [RecipeJson(id: "recipe.invalid", quantity: 0)];
                break;
            case "negative-cost":
                recipeJsons = [RecipeJson(id: "recipe.invalid", quantity: -1)];
                break;
            case "duplicate-cost-material":
                recipeJsons = [RecipeJson(id: "recipe.invalid", duplicateCost: true)];
                break;
            case "null-costs":
                recipeJsons = [RecipeJson(id: "recipe.invalid", costsJson: "null")];
                break;
            case "null-cost-entry":
                recipeJsons = [RecipeJson(id: "recipe.invalid", costsJson: "[null]")];
                break;
            case "empty-costs":
                recipeJsons = [RecipeJson(id: "recipe.invalid", costsJson: "[]")];
                break;
            case "unknown-item":
                recipeJsons = [RecipeJson(id: "recipe.invalid", itemId: "item.unknown")];
                break;
            case "invalid-rarity":
                recipeJsons = [RecipeJson(id: "recipe.invalid", rarity: "Legendary")];
                break;
            case "insufficient-magic-affixes":
                recipeJsons = [RecipeJson(id: "recipe.invalid", rarity: "Magic")];
                affixJsons = [];
                break;
            case "insufficient-rare-affixes":
                recipeJsons = [RecipeJson(id: "recipe.invalid", rarity: "Rare")];
                affixJsons =
                [
                    """{"id":"affix.single","name":"锋锐","stat":"Attack","minValue":1,"maxValue":2,"slots":["Weapon"]}"""
                ];
                break;
            case "empty-resource-id":
                resourceJsons = [ResourceJson(id: "")];
                break;
            case "zero-minimum":
                resourceJsons = [ResourceJson(minQuantity: 0)];
                break;
            case "max-below-minimum":
                resourceJsons = [ResourceJson(minQuantity: 2, maxQuantity: 1)];
                break;
            case "negative-recipe-chance":
                resourceJsons = [ResourceJson(recipeChancePercent: -1)];
                break;
            case "excessive-recipe-chance":
                resourceJsons = [ResourceJson(recipeChancePercent: 101)];
                break;
            case "empty-resource-material":
                resourceJsons = [ResourceJson(materialId: "")];
                break;
            case "unknown-resource-material":
                resourceJsons = [ResourceJson(materialId: "material.unknown")];
                break;
            case "null-recipe-ids":
                resourceJsons = [ResourceJson(recipeIdsJson: "null")];
                break;
            case "null-recipe-id":
                resourceJsons = [ResourceJson(recipeIdsJson: """["recipe.magic_blade",null]""")];
                break;
            case "missing-recipe-candidates":
                resourceJsons = [ResourceJson(recipeIdsJson: "[]")];
                break;
            case "duplicate-recipe-candidate":
                resourceJsons =
                [
                    ResourceJson(
                        recipeIdsJson: """["recipe.magic_blade","recipe.magic_blade"]""")
                ];
                break;
            case "unknown-recipe-candidate":
                resourceJsons = [ResourceJson(recipeIdsJson: """["recipe.unknown"]""")];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return ContentFixture.CreateWithCraftingDefinitions(
            materialJsons,
            recipeJsons,
            resourceJsons,
            affixJsons);
    }

    private static string MaterialJson(string id = "material.slime")
    {
        return $$"""{"id":"{{id}}","name":"黏液"}""";
    }

    private static string RecipeJson(
        string id = "recipe.magic_blade",
        string itemId = "item.training_sword",
        string rarity = "Magic",
        string materialId = "material.slime",
        int quantity = 1,
        string? costsJson = null,
        bool duplicateCost = false)
    {
        costsJson ??= duplicateCost
            ? $$"""[{"materialId":"{{materialId}}","quantity":{{quantity}}},{"materialId":"{{materialId}}","quantity":{{quantity}}}]"""
            : $$"""[{"materialId":"{{materialId}}","quantity":{{quantity}}}]""";
        return $$"""{"id":"{{id}}","name":"魔法武器配方","itemId":"{{itemId}}","rarity":"{{rarity}}","costs":{{costsJson}}}""";
    }

    private static string ResourceJson(
        string id = "resource.slime",
        string materialId = "material.slime",
        int minQuantity = 1,
        int maxQuantity = 2,
        int recipeChancePercent = 20,
        string? recipeIdsJson = null)
    {
        recipeIdsJson ??= """["recipe.magic_blade"]""";
        return $$"""{"id":"{{id}}","materialId":"{{materialId}}","minQuantity":{{minQuantity}},"maxQuantity":{{maxQuantity}},"recipeChancePercent":{{recipeChancePercent}},"recipeIds":{{recipeIdsJson}}}""";
    }

    private const string ArcherJson =
        """{"id":"monster.skeleton_archer","name":"骷髅弓手","maxHealth":20,"contactDamage":0,"moveSpeed":70,"lootTableId":"resource.slime","behavior":"Archer","rangedAttack":{"range":260,"projectileSpeed":240,"fireInterval":1.5,"windupSeconds":0.3,"damage":5}}""";

    private const string WolfJson =
        """{"id":"monster.fast_wolf","name":"Fast Wolf","maxHealth":20,"contactDamage":5,"moveSpeed":140,"lootTableId":"resource.slime","behavior":"Chase"}""";
}
