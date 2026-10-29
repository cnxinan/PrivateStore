using System.Collections.ObjectModel;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public sealed class Inventory
{
    private List<ItemInstance> _items = [];
    private ReadOnlyCollection<ItemInstance> _readOnlyItems;

    public Inventory()
    {
        _readOnlyItems = _items.AsReadOnly();
    }

    public IReadOnlyList<ItemInstance> Items => _readOnlyItems;

    public Guid? EquippedWeaponId { get; private set; }

    public Guid? EquippedArmorId { get; private set; }

    public ItemInstance? EquippedWeapon =>
        EquippedWeaponId is { } id ? Find(id) : null;

    public ItemInstance? EquippedArmor =>
        EquippedArmorId is { } id ? Find(id) : null;

    internal void AppendValidated(ItemInstance item) => _items.Add(item);

    internal void RestoreValidated(List<ItemInstance> items, Guid? weaponId, Guid? armorId)
    {
        var view = items.AsReadOnly();
        _items = items;
        _readOnlyItems = view;
        EquippedWeaponId = weaponId;
        EquippedArmorId = armorId;
    }

    public ItemInstance? Find(Guid instanceId)
    {
        return _items.FirstOrDefault(item => item.InstanceId == instanceId);
    }

    internal void ReplaceSnapshot(ItemInstance replacement)
    {
        var index = _items.FindIndex(item => item.InstanceId == replacement.InstanceId);
        if (index < 0)
        {
            throw new InvalidOperationException("Cannot replace an item that is not owned.");
        }

        _items[index] = replacement;
    }

    internal void Equip(ItemSlot slot, Guid instanceId)
    {
        switch (slot)
        {
            case ItemSlot.Weapon:
                EquippedWeaponId = instanceId;
                break;
            case ItemSlot.Armor:
                EquippedArmorId = instanceId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown equipment slot.");
        }
    }
}
