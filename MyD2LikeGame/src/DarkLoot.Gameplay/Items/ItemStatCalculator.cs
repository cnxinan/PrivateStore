using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public readonly record struct ItemStatContribution(
    ItemSlot Slot, long AttackDamage, long MaximumHealth, long CriticalChance);

public sealed class ItemStatCalculator
{
    public ItemStatContribution GetContribution(ItemInstance item, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(content);
        if (item.InstanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(item));
        }

        var definition = ResolveItemDefinition(content, item.DefinitionId);
        ValidateInstance(item, definition, content);
        long attack = definition.AttackBonus;
        long maximumHealth = 0;
        long criticalChance = 0;
        foreach (var roll in item.Rolls)
        {
            switch (roll.Stat)
            {
                case ItemStat.Attack:
                    attack += roll.Value;
                    break;
                case ItemStat.MaximumHealth:
                    maximumHealth += roll.Value;
                    break;
                case ItemStat.CriticalChance:
                    criticalChance += roll.Value;
                    break;
                default:
                    throw new ArgumentException(
                        $"Item '{item.DefinitionId}' has unsupported stat '{roll.Stat}'.",
                        nameof(content));
            }
        }

        return new ItemStatContribution(definition.Slot, attack, maximumHealth, criticalChance);
    }

    public PlayerStatTotals Calculate(
        int baseAttackDamage,
        int baseMaximumHealth,
        ItemInstance? weapon,
        ItemInstance? armor,
        GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (baseAttackDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseAttackDamage));
        }

        if (baseMaximumHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
        }

        long attack = baseAttackDamage;
        long maximumHealth = baseMaximumHealth;
        long criticalChance = 0;

        void Add(ItemInstance? item, ItemSlot expectedSlot)
        {
            if (item is null)
            {
                return;
            }

            var contribution = GetContribution(item, content);
            if (contribution.Slot != expectedSlot)
            {
                throw new ArgumentException(
                    $"Equipped item '{item.DefinitionId}' does not belong to {expectedSlot}.",
                    nameof(content));
            }

            attack += contribution.AttackDamage;
            maximumHealth += contribution.MaximumHealth;
            criticalChance += contribution.CriticalChance;
        }

        Add(weapon, ItemSlot.Weapon);
        Add(armor, ItemSlot.Armor);

        if (attack is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                "baseDamage",
                "Attack damage after equipped AttackBonus and affix bonuses must remain " +
                "between zero and Int32.MaxValue.");
        }

        if (maximumHealth is <= 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseMaximumHealth),
                "Maximum health after equipment must remain within 1..Int32.MaxValue.");
        }

        return new PlayerStatTotals(
            (int)attack,
            (int)maximumHealth,
            (int)Math.Clamp(criticalChance, 0, 100));
    }

    public PlayerStatTotals Preview(
        int baseAttackDamage,
        int baseMaximumHealth,
        Inventory inventory,
        GameContent content,
        ItemSlot slot,
        ItemInstance? replacement)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        if (slot is not (ItemSlot.Weapon or ItemSlot.Armor))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        if (replacement is not null)
        {
            var contribution = GetContribution(replacement, content);
            if (contribution.Slot != slot)
            {
                throw new ArgumentException(
                    $"Replacement item '{replacement.DefinitionId}' belongs to {contribution.Slot}, not {slot}.",
                    nameof(replacement));
            }
        }

        return Calculate(
            baseAttackDamage, baseMaximumHealth,
            slot == ItemSlot.Weapon ? replacement : inventory.EquippedWeapon,
            slot == ItemSlot.Armor ? replacement : inventory.EquippedArmor,
            content);
    }

    private static void ValidateInstance(
        ItemInstance item,
        ItemDefinition definition,
        GameContent content)
    {
        if (item.Rarity is not (ItemRarity.Common or ItemRarity.Magic or ItemRarity.Rare))
        {
            throw new ArgumentException(
                $"Item instance '{item.InstanceId}' has invalid rarity '{item.Rarity}'.",
                nameof(item));
        }

        var requiredRolls = EquipmentFactory.AffixCount(item.Rarity);
        if (item.Rolls is null ||
            item.Rolls.Any(roll => roll is null) ||
            item.Rolls.Count != requiredRolls ||
            item.Rolls.Select(roll => roll.AffixId).Distinct(StringComparer.Ordinal).Count() != item.Rolls.Count)
        {
            throw new ArgumentException(
                $"Item instance '{item.InstanceId}' must have exactly {requiredRolls} distinct affix rolls.",
                nameof(item));
        }

        foreach (var roll in item.Rolls)
        {
            if (roll is null ||
                string.IsNullOrWhiteSpace(roll.AffixId) ||
                !content.Affixes.TryGetValue(roll.AffixId, out var affix) ||
                !IsValidAffix(roll.AffixId, affix) ||
                affix.Stat != roll.Stat ||
                !affix.Slots.Contains(definition.Slot) ||
                roll.Value < affix.MinValue ||
                roll.Value > affix.MaxValue)
            {
                throw new ArgumentException(
                    $"Item instance '{item.InstanceId}' contains an invalid affix roll '{roll?.AffixId}'.",
                    nameof(item));
            }
        }
    }

    internal static bool IsValidAffix(string key, AffixDefinition? affix)
    {
        return affix is not null &&
               !string.IsNullOrWhiteSpace(affix.Id) &&
               string.Equals(key, affix.Id, StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(affix.Name) &&
               affix.Stat is ItemStat.Attack or ItemStat.MaximumHealth or ItemStat.CriticalChance &&
               affix.NameKind is AffixNameKind.Prefix or AffixNameKind.Suffix &&
               affix.Slots is { Count: > 0 } &&
               affix.Slots.All(slot => slot is ItemSlot.Weapon or ItemSlot.Armor) &&
               affix.Slots.Distinct().Count() == affix.Slots.Count &&
               affix.MinValue <= affix.MaxValue &&
               Math.Abs((long)affix.MinValue) <= AffixDefinition.MaxMagnitude &&
               Math.Abs((long)affix.MaxValue) <= AffixDefinition.MaxMagnitude &&
               (affix.Stat != ItemStat.CriticalChance ||
                (affix.MinValue >= -100 && affix.MaxValue <= 100));
    }

    internal static ItemDefinition ResolveItemDefinition(GameContent content, string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            throw new ArgumentException(
                "Item definition ID must not be empty.",
                nameof(definitionId));
        }

        if (content.Items is null)
        {
            throw new ArgumentException(
                "Game content must provide item definitions.",
                nameof(content));
        }

        if (!content.Items.TryGetValue(definitionId, out var definition) ||
            definition is null)
        {
            throw new ArgumentException(
                $"Item definition '{definitionId}' was not found in game content.",
                nameof(content));
        }

        if (definition.Slot is not (ItemSlot.Weapon or ItemSlot.Armor))
        {
            throw new ArgumentException(
                $"Item definition '{definitionId}' has invalid slot '{definition.Slot}'.",
                nameof(content));
        }

        if (definition.AttackBonus < 0 || definition.AttackBonus > ItemDefinition.MaxAttackBonus)
        {
            throw new ArgumentException(
                $"Item definition '{definitionId}' AttackBonus must be between 0 and " +
                $"{ItemDefinition.MaxAttackBonus}.",
                nameof(content));
        }

        return definition;
    }
}
