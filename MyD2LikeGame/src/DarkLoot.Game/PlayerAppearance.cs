using System;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Game;

public readonly record struct PlayerAppearance(ItemRarity? Weapon, ItemRarity? Armor)
{
    public static PlayerAppearance From(Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return new(inventory.EquippedWeapon?.Rarity, inventory.EquippedArmor?.Rarity);
    }
}
