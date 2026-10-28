using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Data;

public static class GameContentLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
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
        var affixes = LoadDefinitions<AffixDefinition>(
            definitionsRoot,
            "affixes",
            definition => definition.Id,
            ValidateAffix);
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
        var encounters = LoadDefinitions<EncounterDefinition>(
            definitionsRoot,
            "encounters",
            definition => definition.Id,
            ValidateEncounter);
        var materials = LoadDefinitions<MaterialDefinition>(
            definitionsRoot,
            "materials",
            definition => definition.Id,
            ValidateMaterial);
        var recipes = LoadDefinitions<RecipeDefinition>(
            definitionsRoot,
            "recipes",
            definition => definition.Id,
            ValidateRecipe);
        var resourceLootTables = LoadDefinitions<ResourceLootTableDefinition>(
            definitionsRoot,
            "resource-loot-tables",
            definition => definition.Id,
            ValidateResourceLootTable);

        ValidateReferences(
            items,
            affixes,
            monsters,
            lootTables,
            encounters,
            materials,
            recipes,
            resourceLootTables);

        return new GameContent(
            AsReadOnly(items),
            AsReadOnly(monsters),
            AsReadOnly(lootTables),
            AsReadOnly(affixes),
            AsReadOnly(encounters),
            AsReadOnly(materials),
            AsReadOnly(recipes),
            AsReadOnly(resourceLootTables));
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

        if (definition.AttackBonus > ItemDefinition.MaxAttackBonus)
        {
            throw Invalid(
                path,
                $"{nameof(definition.AttackBonus)} must not exceed {ItemDefinition.MaxAttackBonus}.");
        }

        if (definition.Slot is not (ItemSlot.Weapon or ItemSlot.Armor))
        {
            throw Invalid(path, $"{nameof(definition.Slot)} is not a supported equipment slot.");
        }
    }

    private static void ValidateAffix(AffixDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.Name, path, nameof(definition.Name));

        if (definition.Stat is not (ItemStat.Attack or ItemStat.MaximumHealth or ItemStat.CriticalChance))
        {
            throw Invalid(path, $"{nameof(definition.Stat)} is not a supported item stat.");
        }

        if (definition.NameKind is not (AffixNameKind.Prefix or AffixNameKind.Suffix))
        {
            throw Invalid(path, $"{nameof(definition.NameKind)} is not supported.");
        }

        if (definition.Slots is null || definition.Slots.Count == 0)
        {
            throw Invalid(path, $"{nameof(definition.Slots)} must contain at least one slot.");
        }

        if (definition.Slots.Any(slot => slot is not (ItemSlot.Weapon or ItemSlot.Armor)) ||
            definition.Slots.Distinct().Count() != definition.Slots.Count)
        {
            throw Invalid(path, $"{nameof(definition.Slots)} contains an invalid or duplicate slot.");
        }

        if (definition.MinValue > definition.MaxValue ||
            Math.Abs((long)definition.MinValue) > AffixDefinition.MaxMagnitude ||
            Math.Abs((long)definition.MaxValue) > AffixDefinition.MaxMagnitude)
        {
            throw Invalid(
                path,
                $"{nameof(definition.MinValue)} and {nameof(definition.MaxValue)} must be ordered " +
                $"within +/-{AffixDefinition.MaxMagnitude}.");
        }

        if (definition.Stat == ItemStat.CriticalChance &&
            (definition.MinValue < -100 || definition.MaxValue > 100))
        {
            throw Invalid(path, "CriticalChance affix values must be between -100 and 100.");
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

        if (!Enum.IsDefined(typeof(MonsterBehavior), definition.Behavior))
        {
            throw Invalid(path, $"{nameof(definition.Behavior)} is not supported.");
        }

        if (definition.Behavior == MonsterBehavior.Archer)
        {
            if (definition.RangedAttack is null)
            {
                throw Invalid(
                    path,
                    $"{nameof(definition.RangedAttack)} is required for {nameof(MonsterBehavior.Archer)}.");
            }

            ValidateRangedAttack(definition.RangedAttack, path);
        }
        else if (definition.RangedAttack is not null)
        {
            throw Invalid(
                path,
                $"{nameof(definition.RangedAttack)} is only supported for {nameof(MonsterBehavior.Archer)}.");
        }
    }

    private static void ValidateRangedAttack(RangedAttackDefinition attack, string path)
    {
        if (!float.IsFinite(attack.Range) || attack.Range <= 0)
        {
            throw Invalid(path, $"{nameof(attack.Range)} must be positive and finite.");
        }

        if (!float.IsFinite(attack.ProjectileSpeed) || attack.ProjectileSpeed <= 0)
        {
            throw Invalid(path, $"{nameof(attack.ProjectileSpeed)} must be positive and finite.");
        }

        if (!float.IsFinite(attack.FireInterval) || attack.FireInterval <= 0)
        {
            throw Invalid(path, $"{nameof(attack.FireInterval)} must be positive and finite.");
        }

        if (!float.IsFinite(attack.WindupSeconds) || attack.WindupSeconds <= 0)
        {
            throw Invalid(path, $"{nameof(attack.WindupSeconds)} must be positive and finite.");
        }

        if (attack.WindupSeconds > attack.FireInterval)
        {
            throw Invalid(path, $"{nameof(attack.WindupSeconds)} must not exceed {nameof(attack.FireInterval)}.");
        }

        if (attack.Damage < 0)
        {
            throw Invalid(path, $"{nameof(attack.Damage)} must not be negative.");
        }
    }

    private static void ValidateEncounter(EncounterDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        if (definition.Spawns is null || definition.Spawns.Count == 0)
        {
            throw Invalid(path, $"{nameof(definition.Spawns)} must contain at least one spawn.");
        }

        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spawn in definition.Spawns)
        {
            if (spawn is null)
            {
                throw Invalid(path, $"{nameof(definition.Spawns)} must not contain null values.");
            }

            RequireText(spawn.InstanceId, path, nameof(spawn.InstanceId));
            RequireText(spawn.MonsterId, path, nameof(spawn.MonsterId));
            if (!instanceIds.Add(spawn.InstanceId))
            {
                throw Invalid(path, $"Duplicate spawn instance ID '{spawn.InstanceId}'.");
            }

            if (!float.IsFinite(spawn.X) || spawn.X < 0 || spawn.X > 960 ||
                !float.IsFinite(spawn.Y) || spawn.Y < 0 || spawn.Y > 540)
            {
                throw Invalid(path, $"Spawn '{spawn.InstanceId}' must be within x=0..960 and y=0..540.");
            }
        }
    }

    private static void ValidateMaterial(MaterialDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.Name, path, nameof(definition.Name));
    }

    private static void ValidateRecipe(RecipeDefinition definition, string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.Name, path, nameof(definition.Name));
        RequireText(definition.ItemId, path, nameof(definition.ItemId));

        if (definition.Rarity is not (ItemRarity.Common or ItemRarity.Magic or ItemRarity.Rare))
        {
            throw Invalid(path, $"Recipe '{definition.Id}' has unsupported {nameof(definition.Rarity)}.");
        }

        if (definition.Costs is null || definition.Costs.Count == 0)
        {
            throw Invalid(path, $"{nameof(definition.Costs)} must contain at least one cost.");
        }

        var materialIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cost in definition.Costs)
        {
            if (cost is null)
            {
                throw Invalid(path, $"{nameof(definition.Costs)} must not contain null values.");
            }

            RequireText(cost.MaterialId, path, nameof(cost.MaterialId));
            if (cost.Quantity <= 0)
            {
                throw Invalid(path, $"{nameof(cost.Quantity)} must be positive.");
            }

            if (!materialIds.Add(cost.MaterialId))
            {
                throw Invalid(
                    path,
                    $"Recipe '{definition.Id}' has duplicate cost material '{cost.MaterialId}'.");
            }
        }
    }

    private static void ValidateResourceLootTable(
        ResourceLootTableDefinition definition,
        string path)
    {
        RequireText(definition.Id, path, nameof(definition.Id));
        RequireText(definition.MaterialId, path, nameof(definition.MaterialId));

        if (definition.MinQuantity < 1 || definition.MaxQuantity < definition.MinQuantity)
        {
            throw Invalid(
                path,
                $"{nameof(definition.MinQuantity)} must be at least 1 and no greater than " +
                $"{nameof(definition.MaxQuantity)}.");
        }

        if (definition.RecipeChancePercent is < 0 or > 100)
        {
            throw Invalid(
                path,
                $"{nameof(definition.RecipeChancePercent)} must be between 0 and 100.");
        }

        if (definition.RecipeIds is null)
        {
            throw Invalid(path, $"{nameof(definition.RecipeIds)} is required.");
        }

        if (definition.RecipeChancePercent > 0 && definition.RecipeIds.Count == 0)
        {
            throw Invalid(
                path,
                $"{nameof(definition.RecipeIds)} must not be empty when recipe chance is positive.");
        }

        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipeId in definition.RecipeIds)
        {
            RequireText(recipeId, path, nameof(definition.RecipeIds));
            if (!recipeIds.Add(recipeId))
            {
                throw Invalid(path, $"Duplicate recipe candidate ID '{recipeId}'.");
            }
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

        if (definition.RarityWeights is not null)
        {
            if (definition.RarityWeights.Count == 0)
            {
                throw Invalid(path, $"{nameof(definition.RarityWeights)} must not be empty.");
            }

            var seenRarities = new HashSet<ItemRarity>();
            double totalWeight = 0;
            foreach (var rarityWeight in definition.RarityWeights)
            {
                if (rarityWeight is null ||
                    rarityWeight.Rarity is not (ItemRarity.Common or ItemRarity.Magic or ItemRarity.Rare) ||
                    !double.IsFinite(rarityWeight.Weight) ||
                    rarityWeight.Weight <= 0 ||
                    !seenRarities.Add(rarityWeight.Rarity))
                {
                    throw Invalid(path, "RarityWeights must contain unique rarities and finite positive weights.");
                }

                totalWeight += rarityWeight.Weight;
                if (!double.IsFinite(totalWeight))
                {
                    throw Invalid(path, "RarityWeights exceed the supported numeric range.");
                }
            }
        }

        if (definition.MaxDrops > 0 && definition.Entries.Count == 0)
        {
            throw Invalid(
                path,
                $"Loot table '{definition.Id}' must contain at least one " +
                $"{nameof(definition.Entries)} when {nameof(definition.MaxDrops)} is positive.");
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

        var guaranteedCount = definition.Entries.Count(entry => entry.Guaranteed);
        if (guaranteedCount > definition.MinDrops)
        {
            throw Invalid(
                path,
                $"{guaranteedCount} guaranteed entries exceed {nameof(definition.MinDrops)} " +
                $"({definition.MinDrops}).");
        }

        if (guaranteedCount < definition.MaxDrops &&
            definition.Entries.All(entry => entry.Guaranteed))
        {
            throw Invalid(
                path,
                $"{nameof(definition.MaxDrops)} exceeds guaranteed entries, but no random entries exist.");
        }
    }

    private static void ValidateReferences(
        IReadOnlyDictionary<string, LoadedDefinition<ItemDefinition>> items,
        IReadOnlyDictionary<string, LoadedDefinition<AffixDefinition>> affixes,
        IReadOnlyDictionary<string, LoadedDefinition<MonsterDefinition>> monsters,
        IReadOnlyDictionary<string, LoadedDefinition<LootTableDefinition>> lootTables,
        IReadOnlyDictionary<string, LoadedDefinition<EncounterDefinition>> encounters,
        IReadOnlyDictionary<string, LoadedDefinition<MaterialDefinition>> materials,
        IReadOnlyDictionary<string, LoadedDefinition<RecipeDefinition>> recipes,
        IReadOnlyDictionary<string, LoadedDefinition<ResourceLootTableDefinition>> resourceLootTables)
    {
        foreach (var loadedMonster in monsters.Values)
        {
            var monster = loadedMonster.Definition;
            if (!resourceLootTables.ContainsKey(monster.LootTableId))
            {
                throw Invalid(
                    loadedMonster.SourcePath,
                    $"Monster '{monster.Id}' references missing resource loot table " +
                    $"'{monster.LootTableId}'.");
            }
        }

        foreach (var loadedLootTable in lootTables.Values)
        {
            var table = loadedLootTable.Definition;
            var slots = new HashSet<ItemSlot>();
            foreach (var entry in table.Entries)
            {
                if (!items.TryGetValue(entry.ItemId, out var item))
                {
                    throw Invalid(
                        loadedLootTable.SourcePath,
                        $"Loot table '{loadedLootTable.Definition.Id}' references missing item " +
                        $"'{entry.ItemId}'.");
                }

                slots.Add(item.Definition.Slot);
            }

            if (table.RarityWeights is not null)
            {
                foreach (var rarityWeight in table.RarityWeights)
                {
                    var requiredAffixes = rarityWeight.Rarity switch
                    {
                        ItemRarity.Common => 0,
                        ItemRarity.Magic => 1,
                        ItemRarity.Rare => 2,
                        _ => throw new InvalidOperationException("Validated rarity was not supported.")
                    };
                    foreach (var slot in slots)
                    {
                        var count = affixes.Values.Count(loaded =>
                            loaded.Definition.Slots.Contains(slot));
                        if (count < requiredAffixes)
                        {
                            throw Invalid(
                                loadedLootTable.SourcePath,
                                $"Loot table '{table.Id}' can generate {rarityWeight.Rarity} {slot} items, " +
                                $"but only {count} eligible affixes exist; {requiredAffixes} are required.");
                        }
                    }
                }
            }
        }

        foreach (var loadedEncounter in encounters.Values)
        {
            foreach (var spawn in loadedEncounter.Definition.Spawns)
            {
                if (!monsters.ContainsKey(spawn.MonsterId))
                {
                    throw Invalid(
                        loadedEncounter.SourcePath,
                        $"Encounter '{loadedEncounter.Definition.Id}' references missing monster " +
                        $"'{spawn.MonsterId}'.");
                }
            }
        }

        foreach (var loadedRecipe in recipes.Values)
        {
            var recipe = loadedRecipe.Definition;
            if (!items.TryGetValue(recipe.ItemId, out var item))
            {
                throw Invalid(
                    loadedRecipe.SourcePath,
                    $"Recipe '{recipe.Id}' references missing item '{recipe.ItemId}'.");
            }

            foreach (var cost in recipe.Costs)
            {
                if (!materials.ContainsKey(cost.MaterialId))
                {
                    throw Invalid(
                        loadedRecipe.SourcePath,
                        $"Recipe '{recipe.Id}' references missing material '{cost.MaterialId}'.");
                }
            }

            var requiredAffixes = recipe.Rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Magic => 1,
                ItemRarity.Rare => 2,
                _ => throw new InvalidOperationException("Validated recipe rarity was not supported.")
            };
            var slot = item.Definition.Slot;
            var count = affixes.Values.Count(loaded =>
                loaded.Definition.Slots.Contains(slot));
            if (count < requiredAffixes)
            {
                throw Invalid(
                    loadedRecipe.SourcePath,
                    $"Recipe '{recipe.Id}' for item '{recipe.ItemId}' requires " +
                    $"{requiredAffixes} eligible {slot} affixes, but only {count} exist.");
            }
        }

        foreach (var loadedResourceLootTable in resourceLootTables.Values)
        {
            var table = loadedResourceLootTable.Definition;
            if (!materials.ContainsKey(table.MaterialId))
            {
                throw Invalid(
                    loadedResourceLootTable.SourcePath,
                    $"Resource loot table '{table.Id}' references missing material " +
                    $"'{table.MaterialId}'.");
            }

            foreach (var recipeId in table.RecipeIds)
            {
                if (!recipes.ContainsKey(recipeId))
                {
                    throw Invalid(
                        loadedResourceLootTable.SourcePath,
                        $"Resource loot table '{table.Id}' references missing recipe '{recipeId}'.");
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
