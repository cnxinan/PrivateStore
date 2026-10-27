using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed record AffixRoll(string AffixId, ItemStat Stat, int Value);

public sealed record ItemInstance(Guid InstanceId, string DefinitionId)
{
    public ItemInstance(
        Guid instanceId,
        string definitionId,
        ItemRarity rarity,
        IEnumerable<AffixRoll> rolls)
        : this(instanceId, definitionId)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        Rarity = rarity;
        Rolls = Array.AsReadOnly(rolls.ToArray());
    }

    public ItemRarity Rarity { get; private init; } = ItemRarity.Common;

    public IReadOnlyList<AffixRoll> Rolls { get; private init; } =
        Array.Empty<AffixRoll>();
}
