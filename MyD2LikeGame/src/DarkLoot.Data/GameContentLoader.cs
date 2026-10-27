using System.Collections.ObjectModel;
using System.Text.Json;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Data;

public static class GameContentLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        RespectRequiredConstructorParameters = true
    };

    public static GameContent LoadDirectory(string definitionsRoot)
    {
        if (string.IsNullOrWhiteSpace(definitionsRoot))
        {
            throw new ContentLoadException("Definitions root path must not be empty.");
        }

        if (!Directory.Exists(definitionsRoot))
        {
            throw new ContentLoadException(
                $"Definitions root directory '{definitionsRoot}' does not exist.");
        }

        var items = LoadDefinitions<ItemDefinition>(
            definitionsRoot,
            "items",
            definition => definition.Id,
            ValidateItem);
        var monsters = LoadDefinitions<MonsterDefinition>(
            definitionsRoot,
            "monsters",
            definition => definition.Id,
            ValidateMonster);
        var lootTables = LoadDefinitions<LootTableDefinition>(
            definitionsRoot,
            "loot-tables",
            definition => definition.Id,
            ValidateLootTable);

        ValidateReferences(items, monsters, lootTables);

        return new GameContent(
            AsReadOnly(items),
            AsReadOnly(monsters),
            AsReadOnly(lootTables));
    }

    private static Dictionary<string, LoadedDefinition<T>> LoadDefinitions<T>(
        string definitionsRoot,
        string directoryName,
        Func<T, string> getId,
        Action<T, string> validate)
    {
        var directoryPath = Path.Combine(definitionsRoot, directoryName);
        var definitions = new Dictionary<string, LoadedDefinition<T>>(
            StringComparer.Ordinal);

        if (!Directory.Exists(directoryPath))
        {
            return definitions;
        }

        foreach (var path in Directory.EnumerateFiles(directoryPath, "*.json")
                     .Order(StringComparer.Ordinal))
        {
            var definition = Deserialize<T>(path);
            validate(definition, path);

            var id = getId(definition);
            if (!definitions.TryAdd(id, new LoadedDefinition<T>(definition, path)))
            {
                throw Invalid(path, $"Duplicate definition ID '{id}'.");
            }
        }

        return definitions;
    }

    private static T Deserialize<T>(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw Invalid(path, "JSON did not contain a definition.");
        }
        catch (ContentLoadException)
        {
            throw;
        }
        catch (JsonException error)
        {
            throw new ContentLoadException(
                $"Invalid content in '{path}': malformed JSON. {error.Message}",
                error);
        }
        catch (IOException error)
        {
            throw new ContentLoadException(
                $"Could not read content file '{path}': {error.Message}",
                error);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new ContentLoadException(
                $"Could not read content file '{path}': {error.Message}",
                error);
        }
    }

    private static void ValidateItem(ItemDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.Name, path, nameof(definition.Name));

        if (definition.AttackBonus < 0)
        {
            throw Invalid(path, $"{nameof(definition.AttackBonus)} must not be negative.");
        }
    }

    private static void ValidateMonster(MonsterDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.Name, path, nameof(definition.Name));
        RequireText(definition.LootTableId, path, nameof(definition.LootTableId));

        if (definition.MaxHealth <= 0)
        {
            throw Invalid(path, $"{nameof(definition.MaxHealth)} must be positive.");
        }

        if (definition.ContactDamage < 0)
        {
            throw Invalid(
                path,
                $"{nameof(definition.ContactDamage)} must not be negative.");
        }

        if (!float.IsFinite(definition.MoveSpeed) || definition.MoveSpeed <= 0)
        {
            throw Invalid(path, $"{nameof(definition.MoveSpeed)} must be positive and finite.");
        }
    }

    private static void ValidateLootTable(
        LootTableDefinition definition,
        string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));

        if (definition.MinDrops < 0 || definition.MaxDrops < definition.MinDrops)
        {
            throw Invalid(
                path,
                $"{nameof(definition.MinDrops)} must be non-negative and no greater than " +
                $"{nameof(definition.MaxDrops)}.");
        }

        if (definition.Entries is null)
        {
            throw Invalid(path, $"{nameof(definition.Entries)} is required.");
        }

        foreach (var entry in definition.Entries)
        {
            if (entry is null)
            {
                throw Invalid(path, $"{nameof(definition.Entries)} must not contain null values.");
            }

            RequireText(entry.ItemId, path, nameof(entry.ItemId));

            if (entry.Weight <= 0)
            {
                throw Invalid(path, $"{nameof(entry.Weight)} must be positive.");
            }
        }
    }

    private static void ValidateReferences(
        IReadOnlyDictionary<string, LoadedDefinition<ItemDefinition>> items,
        IReadOnlyDictionary<string, LoadedDefinition<MonsterDefinition>> monsters,
        IReadOnlyDictionary<string, LoadedDefinition<LootTableDefinition>> lootTables)
    {
        foreach (var loadedMonster in monsters.Values)
        {
            var monster = loadedMonster.Definition;
            if (!lootTables.ContainsKey(monster.LootTableId))
            {
                throw Invalid(
                    loadedMonster.SourcePath,
                    $"Monster '{monster.Id}' references missing loot table " +
                    $"'{monster.LootTableId}'.");
            }
        }

        foreach (var loadedLootTable in lootTables.Values)
        {
            foreach (var entry in loadedLootTable.Definition.Entries)
            {
                if (!items.ContainsKey(entry.ItemId))
                {
                    throw Invalid(
                        loadedLootTable.SourcePath,
                        $"Loot table '{loadedLootTable.Definition.Id}' references missing item " +
                        $"'{entry.ItemId}'.");
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, T> AsReadOnly<T>(
        IReadOnlyDictionary<string, LoadedDefinition<T>> definitions)
    {
        var values = definitions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Definition,
            StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, T>(values);
    }

    private static void RequireText(string? value, string path, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(path, $"{propertyName} is required.");
        }
    }

    private static ContentLoadException Invalid(string path, string message)
    {
        return new ContentLoadException($"Invalid content in '{path}': {message}");
    }

    private sealed record LoadedDefinition<T>(T Definition, string SourcePath);
}
