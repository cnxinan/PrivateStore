using DarkLoot.Data.Definitions;

namespace DarkLoot.Data;

public sealed record GameContent(
    IReadOnlyDictionary<string, ItemDefinition> Items,
    IReadOnlyDictionary<string, MonsterDefinition> Monsters,
    IReadOnlyDictionary<string, LootTableDefinition> LootTables,
    IReadOnlyDictionary<string, AffixDefinition>? AffixDefinitions = null,
    IReadOnlyDictionary<string, EncounterDefinition>? EncounterDefinitions = null)
{
    private static readonly IReadOnlyDictionary<string, AffixDefinition> EmptyAffixes =
        new Dictionary<string, AffixDefinition>();
    private static readonly IReadOnlyDictionary<string, EncounterDefinition> EmptyEncounters =
        new Dictionary<string, EncounterDefinition>();

    public IReadOnlyDictionary<string, AffixDefinition> Affixes =>
        AffixDefinitions ?? EmptyAffixes;

    public IReadOnlyDictionary<string, EncounterDefinition> Encounters =>
        EncounterDefinitions ?? EmptyEncounters;
}
