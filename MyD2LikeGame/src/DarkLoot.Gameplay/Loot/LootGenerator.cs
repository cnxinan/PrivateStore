using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Gameplay.Loot;

public sealed class LootGenerator
{
    public IReadOnlyList<ItemInstance> Generate(
        LootTableDefinition table,
        GameContent content,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);

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

        var items = content.Items;
        if (items is null)
        {
            throw new ArgumentException("Game content must provide item definitions.", nameof(content));
        }

        long totalWeight = 0;
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

            if (!items.TryGetValue(entry.ItemId, out var definition) || definition is null)
            {
                throw new ArgumentException(
                    $"Loot table '{table.Id}' references missing item definition '{entry.ItemId}'.",
                    nameof(table));
            }

            totalWeight += entry.Weight;
        }

        if (table.MaxDrops > 0 && table.Entries.Count == 0)
        {
            throw new ArgumentException(
                $"Loot table '{table.Id}' must contain entries when MaxDrops is positive.",
                nameof(table));
        }

        var dropCount = (int)random.NextInt64(table.MinDrops, (long)table.MaxDrops + 1);
        var generated = new List<ItemInstance>(dropCount);
        for (var index = 0; index < dropCount; index++)
        {
            var selection = random.NextInt64(totalWeight);
            foreach (var entry in table.Entries)
            {
                if (selection < entry.Weight)
                {
                    generated.Add(new ItemInstance(Guid.NewGuid(), entry.ItemId));
                    break;
                }

                selection -= entry.Weight;
            }
        }

        return generated.AsReadOnly();
    }
}
