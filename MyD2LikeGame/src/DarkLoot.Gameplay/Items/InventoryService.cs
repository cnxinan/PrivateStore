using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed class InventoryService
{
    private readonly ItemStatCalculator _calculator = new();

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

        var contribution = _calculator.GetContribution(item, content);
        inventory.Equip(contribution.Slot, item);
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

        var contribution = _calculator.GetContribution(item, content);
        if (contribution.Slot != expectedSlot)
        {
            throw new ArgumentException(
                $"Item '{item.DefinitionId}' belongs to {contribution.Slot}, not {expectedSlot}.",
                nameof(instanceId));
        }

        inventory.Equip(contribution.Slot, item);
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

        var contribution = _calculator.GetContribution(item, content);
        _ = _calculator.Preview(
            baseAttackDamage,
            baseMaximumHealth,
            inventory,
            content,
            contribution.Slot,
            item);
        inventory.Equip(contribution.Slot, item);
        return true;
    }

    public PlayerStatTotals GetTotals(
        int baseAttackDamage,
        int baseMaximumHealth,
        Inventory inventory,
        GameContent content)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return _calculator.Calculate(
            baseAttackDamage, baseMaximumHealth,
            inventory.EquippedWeapon, inventory.EquippedArmor, content);
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
        return _calculator.Preview(
            baseAttackDamage, baseMaximumHealth, inventory, content, slot, replacement);
    }
}
