using System.Collections.ObjectModel;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed class Inventory
{
    private readonly List<ItemInstance> _items = [];
    private readonly ReadOnlyCollection<ItemInstance> _readOnlyItems;

    public Inventory()
    {
        _readOnlyItems = _items.AsReadOnly();
    }

    public IReadOnlyList<ItemInstance> Items => _readOnlyItems;

    public ItemInstance? EquippedWeapon { get; private set; }

    public ItemInstance? EquippedArmor { get; private set; }

    internal bool TryAdd(ItemInstance item)
    {
        if (_items.Any(existing => existing.InstanceId == item.InstanceId))
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    internal ItemInstance? Find(Guid instanceId)
    {
        return _items.FirstOrDefault(item => item.InstanceId == instanceId);
    }

    internal void Equip(ItemSlot slot, ItemInstance item)
    {
        switch (slot)
        {
            case ItemSlot.Weapon:
                EquippedWeapon = item;
                break;
            case ItemSlot.Armor:
                EquippedArmor = item;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown equipment slot.");
        }
    }
}
