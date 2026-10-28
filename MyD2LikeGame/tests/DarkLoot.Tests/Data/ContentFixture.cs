namespace DarkLoot.Tests.Data;

internal static class ContentFixture
{
    public static string CreateValid()
    {
        return Create(
            itemJson: """
                {
                  "id": "item.training_sword",
                  "name": "Training Sword",
                  "attackBonus": 3
                }
                """,
            monsterJson: """
                {
                  "id": "monster.training_slime",
                  "name": "Training Slime",
                  "maxHealth": 12,
                  "contactDamage": 2,
                  "moveSpeed": 45.5,
                  "lootTableId": "loot.training_slime"
                }
                """,
            lootTableJson: """
                {
                  "id": "loot.training_slime",
                  "minDrops": 1,
                  "maxDrops": 1,
                  "entries": [
                    {
                      "itemId": "item.training_sword",
                      "weight": 100
                    }
                  ]
                }
                """);
    }

    public static string CreateWithAdditionalDefinitions(
        IEnumerable<string>? monsterJsons = null,
        string? encounterJson = null)
    {
        var root = Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);

        if (monsterJsons is not null)
        {
            var index = 0;
            foreach (var monsterJson in monsterJsons)
            {
                WriteDefinition(
                    root,
                    "monsters",
                    $"additional-monster-{index++}.json",
                    monsterJson);
            }
        }

        if (encounterJson is not null)
        {
            WriteDefinition(root, "encounters", "training-arena.json", encounterJson);
        }

        return root;
    }

    public static string CreateWithCraftingDefinitions(
        IEnumerable<string>? materialJsons = null,
        IEnumerable<string>? recipeJsons = null,
        IEnumerable<string>? resourceLootTableJsons = null,
        IEnumerable<string>? affixJsons = null)
    {
        var root = Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);

        WriteDefinitions(root, "materials", materialJsons ?? [ValidMaterialJson], "material");
        WriteDefinitions(root, "recipes", recipeJsons ?? [ValidRecipeJson], "recipe");
        WriteDefinitions(
            root,
            "resource-loot-tables",
            resourceLootTableJsons ?? [ValidResourceLootTableJson],
            "resource");
        WriteDefinitions(root, "affixes", affixJsons ?? ValidAffixJsons, "affix");

        return root;
    }

    public static string CreateMalformedItem()
    {
        return Create(
            itemJson: """{ "id": "item.training_sword", """,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);
    }

    public static string CreateItemWithAttackBonus(int attackBonus)
    {
        return Create(
            itemJson: $$"""
                {
                  "id": "item.training_sword",
                  "name": "Training Sword",
                  "attackBonus": {{attackBonus}}
                }
                """,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);
    }

    public static string CreateItemMissingAttackBonus()
    {
        return Create(
            itemJson: """
                {
                  "id": "item.training_sword",
                  "name": "Training Sword"
                }
                """,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);
    }

    public static string CreateMonsterWithoutLootTable()
    {
        return Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: null);
    }

    public static string CreateLootTableWithMissingItem()
    {
        return Create(
            itemJson: null,
            monsterJson: ValidMonsterJson,
            lootTableJson: ValidLootTableJson);
    }

    public static string CreateMonsterWithInvalidMaxHealth()
    {
        return Create(
            itemJson: ValidItemJson,
            monsterJson: """
                {
                  "id": "monster.training_slime",
                  "name": "Training Slime",
                  "maxHealth": 0,
                  "contactDamage": 2,
                  "moveSpeed": 45.5,
                  "lootTableId": "loot.training_slime"
                }
                """,
            lootTableJson: ValidLootTableJson);
    }

    public static string CreateLootTableWithInvalidWeight()
    {
        return Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: """
                {
                  "id": "loot.training_slime",
                  "minDrops": 1,
                  "maxDrops": 1,
                  "entries": [
                    {
                      "itemId": "item.training_sword",
                      "weight": 0
                    }
                  ]
                }
                """);
    }

    public static string CreateLootTableWithInvalidDropRange()
    {
        return Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: """
                {
                  "id": "loot.training_slime",
                  "minDrops": 2,
                  "maxDrops": 1,
                  "entries": [
                    {
                      "itemId": "item.training_sword",
                      "weight": 100
                    }
                  ]
                }
                """);
    }

    public static string CreateLootTableWithNoEntries(int maxDrops)
    {
        return Create(
            itemJson: ValidItemJson,
            monsterJson: ValidMonsterJson,
            lootTableJson: $$"""
                {
                  "id": "loot.training_slime",
                  "minDrops": 0,
                  "maxDrops": {{maxDrops}},
                  "entries": []
                }
                """);
    }

    public static void Delete(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var fixtureRoot = Directory.GetParent(root)?.FullName;
        if (fixtureRoot is not null &&
            Directory.Exists(fixtureRoot) &&
            !Directory.EnumerateFileSystemEntries(fixtureRoot).Any())
        {
            Directory.Delete(fixtureRoot);
        }
    }

    private static string Create(
        string? itemJson,
        string? monsterJson,
        string? lootTableJson)
    {
        var root = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".test-content",
            Guid.NewGuid().ToString("N"));

        WriteDefinition(root, "items", "training-sword.json", itemJson);
        WriteDefinition(root, "monsters", "training-slime.json", monsterJson);
        WriteDefinition(root, "loot-tables", "training-slime.json", lootTableJson);

        return root;
    }

    private static void WriteDefinition(
        string root,
        string directory,
        string fileName,
        string? json)
    {
        var directoryPath = Path.Combine(root, directory);
        Directory.CreateDirectory(directoryPath);

        if (json is not null)
        {
            File.WriteAllText(Path.Combine(directoryPath, fileName), json);
        }
    }

    private static void WriteDefinitions(
        string root,
        string directory,
        IEnumerable<string> definitions,
        string filePrefix)
    {
        var index = 0;
        foreach (var json in definitions)
        {
            WriteDefinition(root, directory, $"{filePrefix}-{index++}.json", json);
        }
    }

    private static readonly string[] ValidAffixJsons =
    [
        """{"id":"affix.weapon_one","name":"锋锐","stat":"Attack","minValue":1,"maxValue":2,"slots":["Weapon"]}""",
        """{"id":"affix.weapon_two","name":"迅捷","stat":"CriticalChance","minValue":1,"maxValue":2,"slots":["Weapon"]}""",
        """{"id":"affix.armor_one","name":"强健","stat":"MaximumHealth","minValue":1,"maxValue":2,"slots":["Armor"]}""",
        """{"id":"affix.armor_two","name":"坚韧","stat":"CriticalChance","minValue":1,"maxValue":2,"slots":["Armor"]}"""
    ];

    private const string ValidMaterialJson =
        """{"id":"material.slime","name":"黏液"}""";

    private const string ValidRecipeJson =
        """{"id":"recipe.magic_blade","name":"魔法武器配方","itemId":"item.training_sword","rarity":"Magic","costs":[{"materialId":"material.slime","quantity":1}]}""";

    private const string ValidResourceLootTableJson =
        """{"id":"resource.slime","materialId":"material.slime","minQuantity":1,"maxQuantity":2,"recipeChancePercent":20,"recipeIds":["recipe.magic_blade"]}""";

    private const string ValidItemJson = """
        {
          "id": "item.training_sword",
          "name": "Training Sword",
          "attackBonus": 3
        }
        """;

    private const string ValidMonsterJson = """
        {
          "id": "monster.training_slime",
          "name": "Training Slime",
          "maxHealth": 12,
          "contactDamage": 2,
          "moveSpeed": 45.5,
          "lootTableId": "loot.training_slime"
        }
        """;

    private const string ValidLootTableJson = """
        {
          "id": "loot.training_slime",
          "minDrops": 1,
          "maxDrops": 1,
          "entries": [
            {
              "itemId": "item.training_sword",
              "weight": 100
            }
          ]
        }
        """;
}
