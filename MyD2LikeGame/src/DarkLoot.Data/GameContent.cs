using DarkLoot.Data.Definitions;

namespace DarkLoot.Data;

public sealed record GameContent(
    IReadOnlyDictionary<string, ItemDefinition> Items,
    IReadOnlyDictionary<string, MonsterDefinition> Monsters,
    IReadOnlyDictionary<string, LootTableDefinition> LootTables);
