using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Gameplay.Loot;

public sealed class LootGenerator
{
    public const int MaxAffixMagnitude = AffixDefinition.MaxMagnitude;

    public IReadOnlyList<ItemInstance> Generate(
        LootTableDefinition table,
        GameContent content,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);

        ValidateTable(table, content);
        var dropCount = (int)random.NextInt64(table.MinDrops, (long)table.MaxDrops + 1);
        var generated = new List<ItemInstance>(dropCount);
        foreach (var entry in table.Entries.Where(entry => entry.Guaranteed))
        {
            generated.Add(GenerateItem(entry, table, content, random));
        }

        var randomDropCount = dropCount - generated.Count;
        var selectableEntries = table.Entries.Where(entry => !entry.Guaranteed).ToArray();
        if (randomDropCount > 0 && selectableEntries.Length == 0)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' does not have enough guaranteed entries or random entries " +
                "to satisfy its drop count.",
                nameof(table));
        }

        for (var index = 0; index < randomDropCount; index++)
        {
            generated.Add(GenerateItem(
                SelectEntry(selectableEntries, random),
                table,
                content,
                random));
        }

        return generated.AsReadOnly();
    }

    private static void ValidateTable(LootTableDefinition table, GameContent content)
    {
        if (string.IsNullOrWhiteSpace(table.Id))
        {
            throw new ArgumentException("Loot table ID must not be empty.", nameof(table));
        }

        if (table.MinDrops < 0 || table.MaxDrops < table.MinDrops)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' must have a non-negative drop range where " +
                "MinDrops does not exceed MaxDrops.",
                nameof(table));
        }

        if (table.Entries is null)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' must provide entries.",
                nameof(table));
        }

        if (content.Items is null)
        {
            throw new ArgumentException("Game content must provide item definitions.", nameof(content));
        }

        long totalWeight = 0;
        var eligibleSlots = new HashSet<ItemSlot>();
        foreach (var entry in table.Entries)
        {
            if (entry is null)
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' must not contain null entries.",
                    nameof(table));
            }

            if (string.IsNullOrWhiteSpace(entry.ItemId))
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' contains an entry with an empty item ID.",
                    nameof(table));
            }

            if (entry.Weight <= 0)
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' entry for item '{entry.ItemId}' must have a positive weight.",
                    nameof(table));
            }

            if (!content.Items.TryGetValue(entry.ItemId, out var definition) || definition is null)
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' references missing item definition '{entry.ItemId}'.",
                    nameof(table));
            }

            ValidateItemDefinition(definition, entry.ItemId, table.Id);
            eligibleSlots.Add(definition.Slot);
            totalWeight = checked(totalWeight + entry.Weight);
        }

        if (table.MaxDrops > 0 && table.Entries.Count == 0)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' must contain entries when MaxDrops is positive.",
                nameof(table));
        }

        var guaranteedCount = table.Entries.Count(entry => entry is not null && entry.Guaranteed);
        if (guaranteedCount > table.MinDrops)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' has {guaranteedCount} guaranteed entries, which exceeds " +
                $"its minimum drop count of {table.MinDrops}.",
                nameof(table));
        }

        if (guaranteedCount < table.MaxDrops &&
            table.Entries.All(entry => entry.Guaranteed))
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' needs random entries to satisfy its maximum drop count.",
                nameof(table));
        }

        ValidateAffixes(table, content);
        ValidateRarityWeights(table, eligibleSlots, content);
    }

    private static void ValidateItemDefinition(ItemDefinition definition, string id, string tableId)
    {
        if (definition.Slot is not (ItemSlot.Weapon or ItemSlot.Armor))
        {
            throw new ArgumentException(
                $"Loot table '{tableId}' references item '{id}' with invalid slot '{definition.Slot}'.");
        }

        if (definition.AttackBonus < 0 || definition.AttackBonus > ItemDefinition.MaxAttackBonus)
        {
            throw new ArgumentException(
                $"Item definition '{id}' AttackBonus must be between 0 and {ItemDefinition.MaxAttackBonus}.");
        }
    }

    private static void ValidateRarityWeights(
        LootTableDefinition table,
        IReadOnlySet<ItemSlot> eligibleSlots,
        GameContent content)
    {
        if (table.RarityWeights is null)
        {
            return;
        }

        if (table.RarityWeights.Count == 0)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' must provide positive rarity weights when configured.");
        }

        double totalWeight = 0;
        var seen = new HashSet<ItemRarity>();
        foreach (var rarityWeight in table.RarityWeights)
        {
            if (rarityWeight is null ||
                rarityWeight.Rarity is not (ItemRarity.Common or ItemRarity.Magic or ItemRarity.Rare) ||
                !double.IsFinite(rarityWeight.Weight) ||
                rarityWeight.Weight <= 0 ||
                !seen.Add(rarityWeight.Rarity))
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' has an invalid or duplicate rarity weight.");
            }

            totalWeight += rarityWeight.Weight;
            if (!double.IsFinite(totalWeight))
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' rarity weights exceed the supported numeric range.");
            }

            foreach (var slot in eligibleSlots)
            {
                var required = AffixCount(rarityWeight.Rarity);
                var eligible = content.Affixes.Values.Count(affix =>
                    affix.Slots.Contains(slot));
                if (eligible < required)
                {
                    throw new ArgumentException(
                        $"Loot table '{table.Id}' can generate {rarityWeight.Rarity} {slot} items, " +
                        $"but only {eligible} eligible affixes exist; {required} are required.");
                }
            }
        }
    }

    private static void ValidateAffixes(LootTableDefinition table, GameContent content)
    {
        foreach (var (key, affix) in content.Affixes)
        {
            if (affix is null ||
                string.IsNullOrWhiteSpace(affix.Id) ||
                !string.Equals(key, affix.Id, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(affix.Name) ||
                affix.Stat is not (ItemStat.Attack or ItemStat.MaximumHealth or ItemStat.CriticalChance) ||
                affix.NameKind is not (AffixNameKind.Prefix or AffixNameKind.Suffix) ||
                affix.Slots is null ||
                affix.Slots.Count == 0 ||
                affix.Slots.Any(slot => slot is not (ItemSlot.Weapon or ItemSlot.Armor)) ||
                affix.Slots.Distinct().Count() != affix.Slots.Count ||
                affix.MinValue > affix.MaxValue ||
                Math.Abs((long)affix.MinValue) > MaxAffixMagnitude ||
                Math.Abs((long)affix.MaxValue) > MaxAffixMagnitude ||
                (affix.Stat == ItemStat.CriticalChance &&
                 (affix.MinValue < -100 || affix.MaxValue > 100)))
            {
                throw new ArgumentException(
                    $"Game content has an invalid affix definition '{key}' referenced by loot table '{table.Id}'.");
            }
        }
    }

    private static LootEntryDefinition SelectEntry(
        IReadOnlyList<LootEntryDefinition> entries,
        Random random)
    {
        var totalWeight = entries.Sum(entry => (long)entry.Weight);
        var selection = random.NextInt64(totalWeight);
        foreach (var entry in entries)
        {
            if (selection < entry.Weight)
            {
                return entry;
            }
            selection -= entry.Weight;
        }

        throw new InvalidOperationException("Loot entry weight selection exceeded its validated range.");
    }

    private static ItemInstance GenerateItem(
        LootEntryDefinition entry,
        LootTableDefinition table,
        GameContent content,
        Random random)
    {
        var definition = content.Items[entry.ItemId];
        var rarity = SelectRarity(table.RarityWeights, random);
        var rolls = RollAffixes(definition.Slot, rarity, content, random);
        return new ItemInstance(Guid.NewGuid(), entry.ItemId, rarity, rolls);
    }

    private static ItemRarity SelectRarity(
        IReadOnlyList<RarityWeightDefinition>? weights,
        Random random)
    {
        if (weights is null)
        {
            return ItemRarity.Common;
        }

        var totalWeight = weights.Sum(weight => weight.Weight);
        var selection = random.NextDouble() * totalWeight;
        foreach (var weight in weights)
        {
            if (selection < weight.Weight)
            {
                return weight.Rarity;
            }

            selection -= weight.Weight;
        }

        throw new InvalidOperationException(
            "Rarity weight selection exceeded its validated range.");
    }

    private static IReadOnlyList<AffixRoll> RollAffixes(
        ItemSlot slot,
        ItemRarity rarity,
        GameContent content,
        Random random)
    {
        var count = AffixCount(rarity);
        if (count == 0)
        {
            return Array.Empty<AffixRoll>();
        }

        var eligible = content.Affixes.Values
            .Where(affix => affix.Slots.Contains(slot))
            .OrderBy(affix => affix.Id, StringComparer.Ordinal)
            .ToList();
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

    private static int AffixCount(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 0,
        ItemRarity.Magic => 1,
        ItemRarity.Rare => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown item rarity.")
    };
}
