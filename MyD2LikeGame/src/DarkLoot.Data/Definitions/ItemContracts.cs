namespace DarkLoot.Data.Definitions;

public enum ItemSlot
{
    Weapon = 0,
    Armor = 1
}

public enum ItemRarity
{
    Common = 0,
    Magic = 1,
    Rare = 2
}

public enum ItemStat
{
    Attack = 0,
    MaximumHealth = 1,
    CriticalChance = 2
}

public enum AffixNameKind
{
    Prefix = 0,
    Suffix = 1
}

public sealed record AffixDefinition(
    string Id,
    string Name,
    ItemStat Stat,
    int MinValue,
    int MaxValue,
    IReadOnlyList<ItemSlot> Slots)
{
    public const int MaxMagnitude = 1_000_000;

    public AffixNameKind NameKind { get; init; } = AffixNameKind.Prefix;
}

public sealed record RarityWeightDefinition(ItemRarity Rarity, double Weight);
