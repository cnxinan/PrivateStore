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

    public static string CreateMalformedItem()
    {
        return Create(
            itemJson: """{ "id": "item.training_sword", """,
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
