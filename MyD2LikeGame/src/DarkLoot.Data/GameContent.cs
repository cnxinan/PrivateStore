using DarkLoot.Data.Definitions;

namespace DarkLoot.Data;

public sealed record GameContent(
    IReadOnlyDictionary<string, ItemDefinition> Items,
    IReadOnlyDictionary<string, MonsterDefinition> Monsters,
    IReadOnlyDictionary<string, LootTableDefinition> LootTables,
    IReadOnlyDictionary<string, AffixDefinition>? AffixDefinitions = null,
    IReadOnlyDictionary<string, EncounterDefinition>? EncounterDefinitions = null,
    IReadOnlyDictionary<string, MaterialDefinition>? MaterialDefinitions = null,
    IReadOnlyDictionary<string, RecipeDefinition>? RecipeDefinitions = null,
    IReadOnlyDictionary<string, ResourceLootTableDefinition>? ResourceLootTableDefinitions = null)
{
    private static readonly IReadOnlyDictionary<string, AffixDefinition> EmptyAffixes =
        new Dictionary<string, AffixDefinition>();
    private static readonly IReadOnlyDictionary<string, EncounterDefinition> EmptyEncounters =
        new Dictionary<string, EncounterDefinition>();
    private static readonly IReadOnlyDictionary<string, MaterialDefinition> EmptyMaterials =
        new Dictionary<string, MaterialDefinition>();
    private static readonly IReadOnlyDictionary<string, RecipeDefinition> EmptyRecipes =
        new Dictionary<string, RecipeDefinition>();
    private static readonly IReadOnlyDictionary<string, ResourceLootTableDefinition> EmptyResourceLootTables =
        new Dictionary<string, ResourceLootTableDefinition>();

    public IReadOnlyDictionary<string, AffixDefinition> Affixes =>
        AffixDefinitions ?? EmptyAffixes;

    public IReadOnlyDictionary<string, EncounterDefinition> Encounters =>
        EncounterDefinitions ?? EmptyEncounters;

    public IReadOnlyDictionary<string, MaterialDefinition> Materials =>
        MaterialDefinitions ?? EmptyMaterials;

    public IReadOnlyDictionary<string, RecipeDefinition> Recipes =>
        RecipeDefinitions ?? EmptyRecipes;

    public IReadOnlyDictionary<string, ResourceLootTableDefinition> ResourceLootTables =>
        ResourceLootTableDefinitions ?? EmptyResourceLootTables;
}
