using System;
using System.Collections.Generic;
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

public enum InventoryPage { Equipment, Resources }

public sealed class InventoryPanel
{
    private const int VisibleRows = 8;
    private readonly ItemStatCalculator _calculator = new();
    private int _firstRow;

    public bool IsOpen { get; private set; }
    public InventoryPage Page { get; private set; }
    public void SetOpen(bool open) => IsOpen = open;
    public Rectangle EquipmentTabBounds(int width) => new(width - 320, 108, 132, 25);
    public Rectangle ResourceTabBounds(int width) => new(width - 176, 108, 132, 25);
    public static IReadOnlyList<string> ResourceRows(PlayerState player, GameContent content) =>
        player.Resources.Materials.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => string.Format(GameText.MaterialResource, content.Materials[pair.Key].Name, pair.Value))
            .Concat(player.Resources.Recipes.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => string.Format(GameText.RecipeResource, content.Recipes[pair.Key].Name, pair.Value)))
            .ToArray();

    public Rectangle Bounds(int width) => new(width - 660, 98, 640, 590);

    public Rectangle RowBounds(int width, int visibleIndex) =>
        new(width - 648, 234 + visibleIndex * 56, 616, 50);

    public void Update(
        GameInputSnapshot input, PlayerState player, GameContent content, int width,
        InventoryService operations)
    {
        if (player.IsDead)
        {
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        if (input.AttackPressed)
        {
            var point = input.PointerScreenPosition;
            if (EquipmentTabBounds(width).Contains(point.X, point.Y) ||
                ResourceTabBounds(width).Contains(point.X, point.Y))
            {
                Page = ResourceTabBounds(width).Contains(point.X, point.Y)
                    ? InventoryPage.Resources : InventoryPage.Equipment;
                _firstRow = 0;
                return;
            }
        }

        var inventory = player.Inventory;
        var count = Page == InventoryPage.Resources ? ResourceRows(player, content).Count : inventory.Items.Count;
        _firstRow = Math.Clamp(
            _firstRow - Math.Sign(input.ScrollDelta),
            0,
            Math.Max(0, count - VisibleRows));
        if (!input.AttackPressed || Page == InventoryPage.Resources)
        {
            return;
        }

        for (var row = 0; row < VisibleRows && row + _firstRow < inventory.Items.Count; row++)
        {
            if (RowBounds(width, row).Contains(
                    input.PointerScreenPosition.X,
                    input.PointerScreenPosition.Y))
            {
                var result = operations.Equip(player, inventory.Items[row + _firstRow].InstanceId);
                switch (result)
                {
                    case EquipmentChangeResult.Applied:
                    case EquipmentChangeResult.PlayerDead:
                        break;
                    default:
                        throw new InvalidOperationException($"Inventory row equip was rejected: {result}.");
                }
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
        GameText.Draw(batch, font, GameText.Inventory,
            new Vector2(bounds.X + 12, 108), Color.White, 300);
        DrawTab(batch, shapes, font, EquipmentTabBounds(width), GameText.EquipmentTab, Page == InventoryPage.Equipment);
        DrawTab(batch, shapes, font, ResourceTabBounds(width), GameText.ResourceTab, Page == InventoryPage.Resources);
        GameText.Draw(batch, font, string.Format(GameText.SlotLine, GameText.Weapon, EquippedName(inventory.EquippedWeapon, content)),
            new Vector2(bounds.X + 12, 139), Color.LightCyan, 616);
        GameText.Draw(batch, font, string.Format(GameText.SlotLine, GameText.Armor, EquippedName(inventory.EquippedArmor, content)),
            new Vector2(bounds.X + 12, 161), Color.LightCyan, 616);
        var current = player.Stats;
        GameText.Draw(batch, font,
            string.Format(GameText.Totals, current.AttackDamage, current.MaximumHealth, current.CriticalChance),
            new Vector2(bounds.X + 12, 184), Color.Gold, 616);
        GameText.Draw(batch, font, GameText.Paused,
            new Vector2(bounds.X + 12, 207), Color.LightGray, 616);

        if (Page == InventoryPage.Resources)
        {
            var resources = ResourceRows(player, content);
            _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, resources.Count - VisibleRows));
            if (resources.Count == 0)
                GameText.Draw(batch, font, GameText.EmptyResources,
                    new Vector2(bounds.X + 12, 242), Color.Gray, 616);
            for (var row = 0; row < VisibleRows && row + _firstRow < resources.Count; row++)
            {
                var rowBounds = RowBounds(width, row);
                shapes.Rectangle(batch, rowBounds, new Color(43, 49, 65));
                GameText.Draw(batch, font, resources[row + _firstRow],
                    new Vector2(rowBounds.X + 8, rowBounds.Y + 12), Color.LightCyan, 600);
            }
            return;
        }

        if (inventory.Items.Count == 0)
        {
            GameText.Draw(batch, font, GameText.Empty,
                new Vector2(bounds.X + 12, 242), Color.Gray, 616);
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
            GameText.Draw(batch, font,
                string.Format(GameText.Item, GameText.Rarity(item.Rarity),
                    ItemNameFormatter.GetDisplayName(item, definition, content), GameText.Slot(slot),
                    equipped ? GameText.Equipped : ""),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 4),
                rarityColor, 600, 21);
            GameText.Draw(batch, font, FormatRolls(item, content),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 25), Color.White, 336, 21);

            var preview = _calculator.Preview(
                player.BaseAttackDamage,
                player.BaseMaximumHealth,
                inventory,
                content,
                slot,
                item);
            var comparison = string.Format(GameText.Comparison,
                FormatDelta((long)preview.AttackDamage - current.AttackDamage),
                FormatDelta((long)preview.MaximumHealth - current.MaximumHealth),
                FormatDelta((long)preview.CriticalChance - current.CriticalChance));
            GameText.Draw(batch, font, comparison,
                new Vector2(rowBounds.X + 352, rowBounds.Y + 25),
                new Color(165, 230, 180), 256, 21);
        }
    }

    private static void DrawTab(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        Rectangle bounds, string label, bool active)
    {
        shapes.Rectangle(batch, bounds, active ? new Color(53, 84, 66) : new Color(43, 49, 65));
        GameText.Draw(batch, font, label, new Vector2(bounds.X + 6, bounds.Y + 2), Color.White, bounds.Width - 12);
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
            ? GameText.None
            : $"{GameText.Rarity(item.Rarity)} {ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content)}";

    internal static string FormatRolls(ItemInstance item, GameContent content)
    {
        if (item.Rolls.Count == 0)
        {
            return GameText.NoAffixes;
        }

        return GameText.Rolls + string.Join("  ", item.Rolls.Select(roll =>
        {
            var affix = content.Affixes[roll.AffixId];
            var value = roll.Value > 0 ? $"+{roll.Value}" : roll.Value.ToString();
            var stat = GameText.Stat(roll.Stat);
            return $"{affix.Name} {value} {stat}";
        }));
    }

    private static string FormatDelta(long delta) =>
        delta > 0 ? $"+{delta}" : delta.ToString();
}
