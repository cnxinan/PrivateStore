using System;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class InventoryPanel
{
    private const int VisibleRows = 8;
    private readonly InventoryService _service = new();
    private int _firstRow;

    public bool IsOpen { get; private set; }

    public Rectangle Bounds(int width) => new(width - 660, 98, 640, 590);

    public Rectangle RowBounds(int width, int visibleIndex) =>
        new(width - 648, 234 + visibleIndex * 56, 616, 50);

    public void Update(GameInputSnapshot input, PlayerState player, GameContent content, int width)
    {
        if (input.InventoryTogglePressed)
        {
            IsOpen = !IsOpen;
        }

        if (!IsOpen)
        {
            return;
        }

        var inventory = player.Inventory;
        _firstRow = Math.Clamp(
            _firstRow - Math.Sign(input.ScrollDelta),
            0,
            Math.Max(0, inventory.Items.Count - VisibleRows));
        if (!input.AttackPressed)
        {
            return;
        }

        for (var row = 0; row < VisibleRows && row + _firstRow < inventory.Items.Count; row++)
        {
            if (RowBounds(width, row).Contains(
                    input.PointerScreenPosition.X,
                    input.PointerScreenPosition.Y))
            {
                player.TryEquip(inventory.Items[row + _firstRow].InstanceId);
                break;
            }
        }
    }

    public void Draw(
        SpriteBatch batch,
        ShapeRenderer shapes,
        SpriteFont font,
        PlayerState player,
        GameContent content,
        int width)
    {
        if (!IsOpen)
        {
            return;
        }

        var inventory = player.Inventory;
        var bounds = Bounds(width);
        shapes.Rectangle(batch, bounds, new Color(28, 33, 48));
        batch.DrawString(font, "INVENTORY - click an item to compare / equip",
            new Vector2(bounds.X + 12, 108), Color.White);
        batch.DrawString(font, $"WEAPON  {EquippedName(inventory.EquippedWeapon, content)}",
            new Vector2(bounds.X + 12, 139), Color.LightCyan);
        batch.DrawString(font, $"ARMOR    {EquippedName(inventory.EquippedArmor, content)}",
            new Vector2(bounds.X + 12, 161), Color.LightCyan);
        var current = player.Stats;
        batch.DrawString(font,
            $"TOTALS  ATK {current.AttackDamage}   MAX HP {current.MaximumHealth}   CRIT {current.CriticalChance}%",
            new Vector2(bounds.X + 12, 184), Color.Gold);
        batch.DrawString(font, "Paused. I to close. Wheel to scroll.",
            new Vector2(bounds.X + 12, 207), Color.LightGray);

        if (inventory.Items.Count == 0)
        {
            batch.DrawString(font, "Empty - defeat a slime and press E.",
                new Vector2(bounds.X + 12, 242), Color.Gray);
        }

        for (var row = 0; row < VisibleRows && row + _firstRow < inventory.Items.Count; row++)
        {
            var item = inventory.Items[row + _firstRow];
            var definition = content.Items[item.DefinitionId];
            var slot = definition.Slot;
            var equipped = slot == ItemSlot.Weapon
                ? inventory.EquippedWeapon?.InstanceId == item.InstanceId
                : inventory.EquippedArmor?.InstanceId == item.InstanceId;
            var rowBounds = RowBounds(width, row);
            shapes.Rectangle(
                batch,
                rowBounds,
                equipped ? new Color(53, 84, 66) : new Color(43, 49, 65));

            var rarityColor = RarityColor(item.Rarity);
            batch.DrawString(font,
                $"{item.Rarity} {ItemNameFormatter.GetDisplayName(item, definition, content)}  [{slot}]" +
                (equipped ? "  [equipped]" : ""),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 4),
                rarityColor);
            batch.DrawString(font, FormatRolls(item, content),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 25), Color.White);

            var preview = _service.GetTotalsWithReplacement(
                player.BaseAttackDamage,
                player.BaseMaximumHealth,
                inventory,
                content,
                slot,
                item);
            var comparison = $"ATK {FormatDelta((long)preview.AttackDamage - current.AttackDamage)}" +
                             $"   HP {FormatDelta((long)preview.MaximumHealth - current.MaximumHealth)}" +
                             $"   CRIT {FormatDelta((long)preview.CriticalChance - current.CriticalChance)}%";
            batch.DrawString(font, comparison,
                new Vector2(rowBounds.X + 352, rowBounds.Y + 25),
                new Color(165, 230, 180));
        }
    }

    public static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => Color.LightGray,
        ItemRarity.Magic => new Color(105, 165, 255),
        ItemRarity.Rare => new Color(255, 190, 75),
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown item rarity.")
    };

    private static string EquippedName(ItemInstance? item, GameContent content) =>
        item is null
            ? "None"
            : $"{item.Rarity} {ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content)}";

    private static string FormatRolls(ItemInstance item, GameContent content)
    {
        if (item.Rolls.Count == 0)
        {
            return "No affixes";
        }

        return "Rolls: " + string.Join("  ", item.Rolls.Select(roll =>
        {
            var affix = content.Affixes[roll.AffixId];
            var value = roll.Value > 0 ? $"+{roll.Value}" : roll.Value.ToString();
            var stat = roll.Stat switch
            {
                ItemStat.Attack => "ATK",
                ItemStat.MaximumHealth => "HP",
                ItemStat.CriticalChance => "CRIT",
                _ => throw new ArgumentOutOfRangeException(nameof(item), "Unsupported affix stat.")
            };
            return $"{affix.Name} {value} {stat}";
        }));
    }

    private static string FormatDelta(long delta) =>
        delta > 0 ? $"+{delta}" : delta.ToString();
}
