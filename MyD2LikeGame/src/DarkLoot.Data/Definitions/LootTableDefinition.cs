namespace DarkLoot.Data.Definitions;

public sealed record LootTableDefinition(
    string Id,
    int MinDrops,
    int MaxDrops,
    IReadOnlyList<LootEntryDefinition> Entries);
