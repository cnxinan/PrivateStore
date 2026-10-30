using System;
using System.Collections.Generic;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public enum InventoryPage { Equipment, Resources }

public sealed class InventoryPanel
{
    private NumericsVector _pointer = new(-1, -1);
    public bool IsOpen { get; private set; }
    public InventoryPage Page { get; private set; }
    public int PageIndex { get; private set; }
    public ItemTooltip? Tooltip { get; private set; }
    internal NumericsVector Pointer => _pointer;

    public void SetOpen(bool open)
    {
        if (IsOpen != open)
            Tooltip = null;
        IsOpen = open;
    }

    public Rectangle Bounds(int width) => new UiLayout(width).Inventory;
    public Rectangle EquipmentTabBounds(int width) => new UiLayout(width).EquipmentTab;
    public Rectangle ResourceTabBounds(int width) => new UiLayout(width).ResourceTab;
    public Rectangle RowBounds(int width, int visibleIndex) => new UiLayout(width).Cell(visibleIndex);
    public Rectangle NextPageBounds(int width) => new UiLayout(width).NextPage;
    public Rectangle PreviousPageBounds(int width) => new UiLayout(width).PreviousPage;
    public int PageCount(PlayerState player, GameContent content)
    {
        var count = Count(player);
        return Math.Max(1, count / 32 + (count % 32 > 0 ? 1 : 0));
    }

    public static IReadOnlyList<string> ResourceRows(PlayerState player, GameContent content) =>
        Resources(player).Select(resource => resource.Kind == ResourceKind.Material
            ? string.Format(GameText.MaterialResource, content.Materials[resource.DefinitionId].Name, resource.Quantity)
            : string.Format(GameText.RecipeResource, content.Recipes[resource.DefinitionId].Name, resource.Quantity)).ToArray();

    public void Update(GameInputSnapshot input, PlayerState player, GameContent content, int width,
        InventoryService operations, IReadOnlyCollection<string>? knownRecipeIds = null)
    {
        Tooltip = null;
        if (!IsOpen || player.IsDead)
            return;
        _pointer = input.PointerScreenPosition;
        var layout = new UiLayout(width);
        var point = new Point((int)_pointer.X, (int)_pointer.Y);
        if (input.AttackPressed && (layout.EquipmentTab.Contains(point) || layout.ResourceTab.Contains(point)))
        {
            Page = layout.ResourceTab.Contains(point) ? InventoryPage.Resources : InventoryPage.Equipment;
            PageIndex = 0;
            return;
        }
        var lastPage = PageCount(player, content) - 1;
        var change = layout.Grid.Contains(point) ? -Math.Sign(input.ScrollDelta) : 0;
        if (input.AttackPressed && layout.NextPage.Contains(point))
            change = 1;
        else if (input.AttackPressed && layout.PreviousPage.Contains(point))
            change = -1;
        PageIndex = Math.Clamp(PageIndex + change, 0, lastPage);

        var resources = Page == InventoryPage.Resources ? Resources(player).ToArray() : Array.Empty<ResourceStack>();
        if (Page == InventoryPage.Equipment)
        {
            var equipped = layout.WeaponSlot.Contains(point) ? player.Inventory.EquippedWeapon :
                layout.ArmorSlot.Contains(point) ? player.Inventory.EquippedArmor : null;
            if (equipped is not null)
                Tooltip = ItemTooltip.ForEquipment(player, equipped, content);
        }
        for (var cell = 0; cell < 32; cell++)
        {
            var index = PageIndex * 32 + cell;
            if (index >= Count(player) || !layout.Cell(cell).Contains(point))
                continue;
            if (Page == InventoryPage.Resources)
            {
                var resource = resources[index];
                Tooltip = ItemTooltip.ForResource(resource,
                    knownRecipeIds?.Contains(resource.DefinitionId) == true, content);
            }
            else
            {
                var item = player.Inventory.Items[index];
                if (input.AttackPressed && change == 0)
                {
                    var result = operations.Equip(player, item.InstanceId);
                    if (result != EquipmentChangeResult.Applied)
                        throw new InvalidOperationException($"Inventory cell equip was rejected: {result}.");
                }
                Tooltip = ItemTooltip.ForEquipment(player, item, content);
            }
            break;
        }
    }

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, PlayerState player,
        GameContent content, int width, GothicUiRenderer ui)
    {
        if (!IsOpen)
            return;
        var layout = new UiLayout(width);
        ui.DrawPanel(batch, layout.Inventory);
        Text(batch, font, GameText.Inventory,
            new Rectangle(layout.Inventory.X + 28, layout.Inventory.Y + 8, 330, 28), GothicUiRenderer.Gold);
        CloseButton(batch, font, ui, layout.InventoryClose);
        DrawSlot(batch, font, ui, layout.WeaponSlot, player.Inventory.EquippedWeapon, GameText.Weapon, content);
        DrawSlot(batch, font, ui, layout.ArmorSlot, player.Inventory.EquippedArmor, GameText.Armor, content);
        var stats = player.Stats;
        Text(batch, font, string.Format(GameText.Totals, stats.AttackDamage, stats.MaximumHealth, stats.CriticalChance),
            layout.InventoryTotals, GothicUiRenderer.Gold);
        ui.DrawButton(batch, layout.EquipmentTab, hovered: Contains(layout.EquipmentTab),
            pressed: Page == InventoryPage.Equipment);
        ui.DrawButton(batch, layout.ResourceTab, hovered: Contains(layout.ResourceTab),
            pressed: Page == InventoryPage.Resources);
        Text(batch, font, GameText.EquipmentTab, layout.EquipmentTab, GothicUiRenderer.Text, centered: true);
        Text(batch, font, GameText.ResourceTab, layout.ResourceTab, GothicUiRenderer.Text, centered: true);
        var resources = Page == InventoryPage.Resources ? Resources(player).ToArray() : Array.Empty<ResourceStack>();
        for (var cell = 0; cell < 32; cell++)
        {
            var bounds = layout.Cell(cell);
            var index = PageIndex * 32 + cell;
            var occupied = index < Count(player);
            var item = occupied && Page == InventoryPage.Equipment ? player.Inventory.Items[index] : null;
            var equipped = item is not null &&
                (player.Inventory.EquippedWeaponId == item.InstanceId || player.Inventory.EquippedArmorId == item.InstanceId);
            ui.DrawWell(batch, bounds, Contains(bounds) && occupied, equipped);
            if (!occupied)
                continue;
            var iconBounds = new Rectangle(bounds.X + 7, bounds.Y + 5, bounds.Width - 14, bounds.Height - 12);
            if (item is not null)
            {
                var color = RarityColor(item.Rarity);
                shapes.Rectangle(batch, new Rectangle(bounds.X + 3, bounds.Y + 2, bounds.Width - 6, 2), color);
                ui.DrawIcon(batch, iconBounds, content.Items[item.DefinitionId].Slot == ItemSlot.Weapon
                    ? UiIcon.Weapon : UiIcon.Armor, color);
                if (equipped)
                    Text(batch, font, GameText.EquippedCell,
                        new Rectangle(bounds.X + 16, bounds.Bottom - 15, 30, 13), GothicUiRenderer.Gold);
            }
            else
            {
                var resource = resources[index];
                ui.DrawIcon(batch, iconBounds, ResourceIcon(resource), resource.Kind == ResourceKind.Recipe
                    ? GothicUiRenderer.Gold : GothicUiRenderer.Text);
                Text(batch, font, resource.Quantity > 999 ? "999+" : resource.Quantity.ToString(),
                    new Rectangle(bounds.X + 12, bounds.Bottom - 16, 34, 14), GothicUiRenderer.Text);
            }
        }
        ui.DrawButton(batch, layout.PreviousPage, PageIndex > 0, Contains(layout.PreviousPage));
        ui.DrawButton(batch, layout.NextPage, PageIndex + 1 < PageCount(player, content), Contains(layout.NextPage));
        Text(batch, font, "<", layout.PreviousPage, GothicUiRenderer.Text, centered: true);
        Text(batch, font, ">", layout.NextPage, GothicUiRenderer.Text, centered: true);
        Text(batch, font, string.Format(GameText.PageNumber, PageIndex + 1, PageCount(player, content)),
            layout.PageLabel, GothicUiRenderer.Gold, centered: true);
        var hint = Count(player) == 0
            ? Page == InventoryPage.Equipment ? GameText.Empty : GameText.EmptyResources
            : GameText.GridHelp;
        Text(batch, font, hint, layout.InventoryHint with { Height = 22 }, GothicUiRenderer.Muted);
        Text(batch, font, GameText.UnlimitedGrid,
            new Rectangle(layout.InventoryHint.X, layout.InventoryHint.Y + 23, layout.InventoryHint.Width, 20),
            GothicUiRenderer.Muted);
    }

    public void DrawTooltip(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, int width)
    {
        if (IsOpen)
            Tooltip?.Draw(batch, shapes, font, _pointer, new UiLayout(width));
    }

    private int Count(PlayerState player) =>
        Page == InventoryPage.Equipment ? player.Inventory.Items.Count :
            player.Resources.Materials.Count + player.Resources.Recipes.Count;

    private static IEnumerable<ResourceStack> Resources(PlayerState player) =>
        player.Resources.Materials.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ResourceStack(ResourceKind.Material, pair.Key, pair.Value))
            .Concat(player.Resources.Recipes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ResourceStack(ResourceKind.Recipe, pair.Key, pair.Value)));

    private bool Contains(Rectangle bounds) => bounds.Contains((int)_pointer.X, (int)_pointer.Y);

    internal static void Text(SpriteBatch batch, SpriteFont font, string text, Rectangle bounds, Color color,
        bool centered = false) =>
        GameText.Draw(batch, font, text, new Vector2(bounds.X + 4, bounds.Y + 2), color,
            bounds.Width - 8, bounds.Height - 4, centered);

    internal static void CloseButton(SpriteBatch batch, SpriteFont font, GothicUiRenderer ui, Rectangle bounds)
    {
        ui.DrawButton(batch, bounds);
        Text(batch, font, "X", bounds, GothicUiRenderer.Gold, centered: true);
    }

    private void DrawSlot(SpriteBatch batch, SpriteFont font, GothicUiRenderer ui,
        Rectangle bounds, ItemInstance? item, string label, GameContent content)
    {
        ui.DrawWell(batch, bounds, Contains(bounds), item is not null);
        if (item is not null)
            ui.DrawIcon(batch, new Rectangle(bounds.X + 12, bounds.Y + 10, bounds.Width - 24, bounds.Height - 20),
                content.Items[item.DefinitionId].Slot == ItemSlot.Weapon ? UiIcon.Weapon : UiIcon.Armor,
                RarityColor(item.Rarity));
        Text(batch, font, label, new Rectangle(bounds.X, bounds.Bottom + 3, bounds.Width, 21),
            GothicUiRenderer.Muted, centered: true);
    }

    internal static UiIcon ResourceIcon(ResourceStack stack) => stack.Kind == ResourceKind.Recipe ? UiIcon.Recipe :
        stack.DefinitionId switch
        {
            "material.bone" => UiIcon.Bone,
            "material.hide" => UiIcon.Hide,
            "material.slime" => UiIcon.Slime,
            _ => UiIcon.Material
        };

    public static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => GothicUiRenderer.Text,
        ItemRarity.Magic => GothicUiRenderer.Blue,
        ItemRarity.Rare => GothicUiRenderer.Gold,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown item rarity.")
    };

    internal static string FormatRolls(ItemInstance item, GameContent content) => item.Rolls.Count == 0
        ? GameText.NoAffixes
        : GameText.Rolls + string.Join("  ", item.Rolls.Select(roll =>
            $"{content.Affixes[roll.AffixId].Name} {ItemTooltip.Delta(roll.Value)} {GameText.Stat(roll.Stat)}"));
}
