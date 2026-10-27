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

        ResolveItemDefinition(content, item.DefinitionId);
        inventory.EquipWeapon(item);
        return true;
    }

    public int GetAttackDamage(int baseDamage, Inventory inventory, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);

        if (baseDamage < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseDamage),
                "Base damage must not be negative.");
        }

        var equippedWeapon = inventory.EquippedWeapon;
        if (equippedWeapon is null)
        {
            return baseDamage;
        }

        var definition = ResolveItemDefinition(content, equippedWeapon.DefinitionId);
        if (baseDamage > int.MaxValue - definition.AttackBonus)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseDamage),
                "Base damage plus the equipped item's AttackBonus must not exceed Int32.MaxValue.");
        }

        return checked(baseDamage + definition.AttackBonus);
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
