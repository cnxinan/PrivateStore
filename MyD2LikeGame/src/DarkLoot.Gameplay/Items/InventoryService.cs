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
        if (!player.Inventory.TryAdd(item))
        {
            throw new InvalidOperationException("Validated item could not be added.");
        }

        return EquipmentChangeResult.Applied;
    }

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
}
