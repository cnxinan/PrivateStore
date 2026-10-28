using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Gameplay.Items;

public sealed class InventoryService
{
    private readonly GameContent _content;
    private readonly ItemStatCalculator _calculator = new();

    public InventoryService(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    public EquipmentChangeResult PickUp(PlayerState player, ItemInstance item)
    {
        var result = ValidateAddition(player, item);
        if (result != EquipmentChangeResult.Applied)
        {
            return result;
        }

        CommitAddition(player, item);
        return EquipmentChangeResult.Applied;
    }

    internal EquipmentChangeResult ValidateAddition(PlayerState player, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(item);
        if (item.InstanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(item));
        }

        if (player.IsDead)
        {
            return EquipmentChangeResult.PlayerDead;
        }

        if (player.Inventory.Find(item.InstanceId) is not null)
        {
            return EquipmentChangeResult.DuplicateInstance;
        }

        _ = _calculator.GetContribution(item, _content);
        return EquipmentChangeResult.Applied;
    }

    internal void CommitAddition(PlayerState player, ItemInstance item) =>
        player.Inventory.AppendValidated(item);

    public EquipmentChangeResult Equip(PlayerState player, Guid instanceId)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(instanceId));
        }

        if (player.IsDead)
        {
            return EquipmentChangeResult.PlayerDead;
        }

        var item = player.Inventory.Find(instanceId);
        if (item is null)
        {
            return EquipmentChangeResult.NotOwned;
        }

        var slot = _calculator.GetContribution(item, _content).Slot;
        var candidate = _calculator.Preview(
            player.BaseAttackDamage, player.BaseMaximumHealth,
            player.Inventory, _content, slot, item);
        player.Inventory.Equip(slot, instanceId);
        player.Health = Math.Min(player.Health, candidate.MaximumHealth);
        return EquipmentChangeResult.Applied;
    }

    internal EquipmentChangeResult ReplaceOwned(PlayerState player, ItemInstance replacement)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement.InstanceId == Guid.Empty)
        {
            throw new ArgumentException("Item instance ID must not be empty.", nameof(replacement));
        }

        if (player.IsDead)
        {
            return EquipmentChangeResult.PlayerDead;
        }

        var original = player.Inventory.Find(replacement.InstanceId);
        if (original is null)
        {
            return EquipmentChangeResult.NotOwned;
        }

        if (!string.Equals(original.DefinitionId, replacement.DefinitionId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Replacement must preserve the item definition.", nameof(replacement));
        }

        var oldSlot = _calculator.GetContribution(original, _content).Slot;
        var newSlot = _calculator.GetContribution(replacement, _content).Slot;
        if (oldSlot != newSlot)
        {
            throw new ArgumentException("Replacement must preserve the item slot.", nameof(replacement));
        }

        var inventory = player.Inventory;
        var equipped = inventory.EquippedWeaponId == replacement.InstanceId ||
            inventory.EquippedArmorId == replacement.InstanceId;
        var candidate = equipped
            ? _calculator.Preview(
                player.BaseAttackDamage, player.BaseMaximumHealth,
                inventory, _content, newSlot, replacement)
            : _calculator.Calculate(
                player.BaseAttackDamage, player.BaseMaximumHealth,
                inventory.EquippedWeapon, inventory.EquippedArmor, _content);

        inventory.ReplaceSnapshot(replacement);
        player.Health = Math.Min(player.Health, candidate.MaximumHealth);
        return EquipmentChangeResult.Applied;
    }
}
