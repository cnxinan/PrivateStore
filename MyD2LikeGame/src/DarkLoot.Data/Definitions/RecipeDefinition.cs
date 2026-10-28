namespace DarkLoot.Data.Definitions;

public sealed record MaterialCost(string MaterialId, int Quantity);

public sealed record RecipeDefinition(
    string Id,
    string Name,
    string ItemId,
    ItemRarity Rarity,
    IReadOnlyList<MaterialCost> Costs);
