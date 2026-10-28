namespace DarkLoot.Data.Definitions;

public sealed record ResourceLootTableDefinition(
    string Id,
    string MaterialId,
    int MinQuantity,
    int MaxQuantity,
    int RecipeChancePercent,
    IReadOnlyList<string> RecipeIds);
