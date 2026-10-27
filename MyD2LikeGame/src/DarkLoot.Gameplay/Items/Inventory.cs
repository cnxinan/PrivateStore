using System.Collections.ObjectModel;

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

    internal void EquipWeapon(ItemInstance item)
    {
        EquippedWeapon = item;
    }
}
