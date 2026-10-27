using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed class InventoryService
{
    public bool TryPickUp(Inventory inventory, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(item);

        if (item.InstanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            throw new ArgumentException("Item definition ID must not be empty.", nameof(item));
        }

        return inventory.TryAdd(item);
    }

    public bool TryEquip(Inventory inventory, Guid instanceId, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);

        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(instanceId));
        }

        var item = inventory.Find(instanceId);
        if (item is null)
        {
            return false;
        }

        var definition = ResolveItemDefinition(content, item.DefinitionId);
        ValidateInstance(item, definition, content);
        inventory.Equip(definition.Slot, item);
        return true;
    }

    public bool TryEquip(
        Inventory inventory,
        Guid instanceId,
        GameContent content,
        ItemSlot expectedSlot)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(instanceId));
        }

        if (expectedSlot is not (ItemSlot.Weapon or ItemSlot.Armor))
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSlot));
        }

        var item = inventory.Find(instanceId);
        if (item is null)
        {
            return false;
        }

        var definition = ResolveItemDefinition(content, item.DefinitionId);
        if (definition.Slot != expectedSlot)
        {
            throw new ArgumentException(
                $"Item '{item.DefinitionId}' belongs to {definition.Slot}, not {expectedSlot}.",
                nameof(instanceId));
        }

        ValidateInstance(item, definition, content);
        inventory.Equip(definition.Slot, item);
        return true;
    }

    public bool TryEquip(
        Inventory inventory,
        Guid instanceId,
        GameContent content,
        int baseAttackDamage,
        int baseMaximumHealth)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(instanceId));
        }

        var item = inventory.Find(instanceId);
        if (item is null)
        {
            return false;
        }

        var definition = ResolveItemDefinition(content, item.DefinitionId);
        ValidateInstance(item, definition, content);
        _ = GetTotalsWithReplacement(
            baseAttackDamage,
            baseMaximumHealth,
            inventory,
            content,
            definition.Slot,
            item);
        inventory.Equip(definition.Slot, item);
        return true;
    }

    public PlayerStatTotals GetTotals(
        int baseAttackDamage,
        int baseMaximumHealth,
        Inventory inventory,
        GameContent content)
    {
        ArgumentNullException.ThrowIfNull(inventory);
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
        AddItemStats(inventory.EquippedWeapon, ItemSlot.Weapon, content,
            ref attack, ref maximumHealth, ref criticalChance);
        AddItemStats(inventory.EquippedArmor, ItemSlot.Armor, content,
            ref attack, ref maximumHealth, ref criticalChance);

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

    public int GetAttackDamage(int baseDamage, Inventory inventory, GameContent content)
    {
        if (baseDamage < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseDamage),
                "Base damage must not be negative.");
        }

        return GetTotals(baseDamage, 1, inventory, content).AttackDamage;
    }

    public PlayerStatTotals GetTotalsWithReplacement(
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

        var preview = new Inventory();
        if (inventory.EquippedWeapon is { } weapon && slot != ItemSlot.Weapon)
        {
            preview.Equip(ItemSlot.Weapon, weapon);
        }

        if (inventory.EquippedArmor is { } armor && slot != ItemSlot.Armor)
        {
            preview.Equip(ItemSlot.Armor, armor);
        }

        if (replacement is not null)
        {
            var definition = ResolveItemDefinition(content, replacement.DefinitionId);
            if (definition.Slot != slot)
            {
                throw new ArgumentException(
                    $"Replacement item '{replacement.DefinitionId}' belongs to {definition.Slot}, not {slot}.",
                    nameof(replacement));
            }

            ValidateInstance(replacement, definition, content);
            preview.Equip(slot, replacement);
        }

        return GetTotals(baseAttackDamage, baseMaximumHealth, preview, content);
    }

    private static void AddItemStats(
        ItemInstance? item,
        ItemSlot expectedSlot,
        GameContent content,
        ref long attack,
        ref long maximumHealth,
        ref long criticalChance)
    {
        if (item is null)
        {
            return;
        }

        var definition = ResolveItemDefinition(content, item.DefinitionId);
        if (definition.Slot != expectedSlot)
        {
            throw new ArgumentException(
                $"Equipped item '{item.DefinitionId}' does not belong to {expectedSlot}.",
                nameof(content));
        }

        ValidateInstance(item, definition, content);
        attack += definition.AttackBonus;
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

        var requiredRolls = item.Rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Magic => 1,
            ItemRarity.Rare => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
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
                affix is null ||
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

    private static bool IsValidAffix(string key, AffixDefinition affix)
    {
        return string.Equals(key, affix.Id, StringComparison.Ordinal) &&
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

    private static ItemDefinition ResolveItemDefinition(GameContent content, string definitionId)
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
