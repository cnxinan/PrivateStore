namespace DarkLoot.Data.Definitions;

public sealed record LootEntryDefinition(string ItemId, int Weight)
{
    public bool Guaranteed { get; init; }
}
