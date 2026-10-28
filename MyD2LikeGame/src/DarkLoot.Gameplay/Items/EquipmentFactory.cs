using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed class EquipmentFactory
{
    public ItemInstance Create(
        string itemId, ItemRarity rarity, GameContent content, Random random)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);

        var definition = ItemStatCalculator.ResolveItemDefinition(content, itemId);
        var count = AffixCount(rarity);
        foreach (var (key, affix) in content.Affixes)
        {
            if (!ItemStatCalculator.IsValidAffix(key, affix))
            {
                throw new ArgumentException(
                    $"Game content has an invalid affix definition '{key}'.", nameof(content));
            }
        }

        var eligible = content.Affixes.Values
            .Where(affix => affix.Slots.Contains(definition.Slot))
            .OrderBy(affix => affix.Id, StringComparer.Ordinal)
            .ToList();
        if (eligible.Count < count)
        {
            throw new ArgumentException(
                $"Cannot generate {rarity} {definition.Slot} items: only {eligible.Count} eligible " +
                $"affixes exist; {count} are required.", nameof(content));
        }

        var item = new ItemInstance(
            Guid.NewGuid(), itemId, rarity, RollAffixes(eligible, count, random));
        _ = new ItemStatCalculator().GetContribution(item, content);
        return item;
    }

    private static IReadOnlyList<AffixRoll> RollAffixes(
        List<AffixDefinition> eligible, int count, Random random)
    {
        if (count == 0)
        {
            return Array.Empty<AffixRoll>();
        }

        var rolls = new List<AffixRoll>(count);
        for (var index = 0; index < count; index++)
        {
            var chosenIndex = (int)random.NextInt64(eligible.Count);
            var definition = eligible[chosenIndex];
            eligible.RemoveAt(chosenIndex);
            var value = (int)random.NextInt64(
                definition.MinValue,
                (long)definition.MaxValue + 1);
            rolls.Add(new AffixRoll(definition.Id, definition.Stat, value));
        }

        return rolls.AsReadOnly();
    }

    internal static int AffixCount(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 0,
        ItemRarity.Magic => 1,
        ItemRarity.Rare => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown item rarity.")
    };
}
