using System;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class BlacksmithPanel
{
    public bool IsOpen { get; private set; }
    public BlacksmithResult? LastResult { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public string CraftedRolls { get; private set; } = string.Empty;
    public int SelectedRecipeIndex { get; private set; }
    public void SetOpen(bool open) => IsOpen = open;
    public Rectangle Bounds(int width) => new UiLayout(width).Blacksmith;
    public Rectangle RowBounds(int width, int row) => new UiLayout(width).RecipeRow(row);
    public Rectangle LearnButtonBounds(int width, int row) => new UiLayout(width).LearnButton;
    public Rectangle CraftButtonBounds(int width, int row) => new UiLayout(width).CraftButton;
    public bool CanLearn(ArenaSimulation arena, RecipeDefinition recipe) =>
        arena.BlacksmithOperations.CanInteract(arena.Player) &&
        !arena.Blacksmith.HasLearned(recipe.Id) &&
        arena.Player.Resources.GetCount(ResourceKind.Recipe, recipe.Id) > 0;

    public bool CanCraft(ArenaSimulation arena, RecipeDefinition recipe) =>
        arena.BlacksmithOperations.CanInteract(arena.Player) &&
        arena.Blacksmith.HasLearned(recipe.Id) &&
        recipe.Costs.GroupBy(cost => cost.MaterialId).All(costs =>
            arena.Player.Resources.GetCount(ResourceKind.Material, costs.Key) >= costs.Sum(cost => (long)cost.Quantity));

    public void Update(GameInputSnapshot input, ArenaSimulation arena, GameContent content, int width)
    {
        if (!IsOpen || !input.AttackPressed)
            return;

        var recipes = content.Recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray();
        for (var row = 0; row < recipes.Length; row++)
        {
            var point = input.PointerScreenPosition;
            if (!RowBounds(width, row).Contains(point.X, point.Y))
                continue;
            SelectedRecipeIndex = row;
            return;
        }
        var pointer = input.PointerScreenPosition;
        var learn = LearnButtonBounds(width, SelectedRecipeIndex).Contains(pointer.X, pointer.Y);
        if (learn || CraftButtonBounds(width, SelectedRecipeIndex).Contains(pointer.X, pointer.Y))
        {
            var recipe = recipes[SelectedRecipeIndex];
            // Disabled styling is advisory: the official service owns every refusal and mutation.
            var result = learn
                ? arena.BlacksmithOperations.Learn(arena.Player, recipe.Id)
                : arena.BlacksmithOperations.Craft(arena.Player, recipe.Id);
            LastResult = result;
            Feedback = GameText.SmithStatus(result.Status);
            CraftedRolls = string.Empty;
            if (result.Status == BlacksmithStatus.Applied)
            {
                Feedback = result.Item is null
                    ? string.Format(GameText.Learned, recipe.Name)
                    : string.Format(GameText.Crafted, ItemNameFormatter.GetDisplayName(
                        result.Item, content.Items[result.Item.DefinitionId], content));
                if (result.Item is not null)
                    CraftedRolls = InventoryPanel.FormatRolls(result.Item, content);
            }
            else if (result.Status == BlacksmithStatus.InsufficientMaterials)
            {
                Feedback = string.Format(GameText.ShortageDetails, Feedback,
                    string.Join("  ", recipe.Costs.GroupBy(cost => cost.MaterialId)
                    .Select(costs => (Id: costs.Key, Missing: costs.Sum(cost => (long)cost.Quantity) -
                        arena.Player.Resources.GetCount(ResourceKind.Material, costs.Key)))
                    .Where(cost => cost.Missing > 0)
                    .Select(cost => string.Format(GameText.Shortage, content.Materials[cost.Id].Name, cost.Missing))));
            }
            return;
        }
    }

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        ArenaSimulation arena, GameContent content, int width, GothicUiRenderer ui)
    {
        if (!IsOpen)
            return;

        var layout = new UiLayout(width);
        var bounds = layout.Blacksmith;
        ui.DrawPanel(batch, bounds);
        InventoryPanel.Text(batch, font, GameText.Smith,
            new Rectangle(bounds.X + 20, bounds.Y + 8, 290, 28), GothicUiRenderer.Gold);
        InventoryPanel.CloseButton(batch, font, ui, layout.BlacksmithClose);
        var recipes = content.Recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray();
        for (var row = 0; row < recipes.Length; row++)
        {
            var recipe = recipes[row];
            var rowBounds = RowBounds(width, row);
            ui.DrawWell(batch, rowBounds, selected: row == SelectedRecipeIndex);
            ui.DrawIcon(batch, new Rectangle(rowBounds.X + 7, rowBounds.Y + 6, 25, 26),
                content.Items[recipe.ItemId].Slot == ItemSlot.Weapon ? UiIcon.Weapon : UiIcon.Armor,
                InventoryPanel.RarityColor(recipe.Rarity));
            GameText.Draw(batch, font, recipe.Name,
                new Vector2(rowBounds.X + 40, rowBounds.Y + 2), InventoryPanel.RarityColor(recipe.Rarity), 272, 18);
            GameText.Draw(batch, font, string.Format(GameText.RecipeState,
                    arena.Blacksmith.HasLearned(recipe.Id) ? GameText.Known : GameText.Unknown,
                    arena.Player.Resources.GetCount(ResourceKind.Recipe, recipe.Id)),
                new Vector2(rowBounds.X + 40, rowBounds.Y + 21), GothicUiRenderer.Muted, 272, 18);
        }
        var selected = recipes[SelectedRecipeIndex];
        InventoryPanel.Text(batch, font, content.Items[selected.ItemId].Name + " · " + GameText.Rarity(selected.Rarity),
            layout.RecipeTitle, InventoryPanel.RarityColor(selected.Rarity));
        InventoryPanel.Text(batch, font, string.Format(GameText.RecipeState,
                arena.Blacksmith.HasLearned(selected.Id) ? GameText.Known : GameText.Unknown,
                arena.Player.Resources.GetCount(ResourceKind.Recipe, selected.Id)),
            layout.RecipeHelp, GothicUiRenderer.Text);
        var index = 0;
        foreach (var cost in selected.Costs.GroupBy(cost => cost.MaterialId))
        {
            var count = arena.Player.Resources.GetCount(ResourceKind.Material, cost.Key);
            var required = cost.Sum(entry => (long)entry.Quantity);
            InventoryPanel.Text(batch, font, string.Format(GameText.MaterialCost,
                    content.Materials[cost.Key].Name, count, required),
                layout.MaterialCost(index++), count >= required ? GothicUiRenderer.Text : GothicUiRenderer.Red);
        }
        DrawButton(batch, font, ui, layout.LearnButton, GameText.Learn, CanLearn(arena, selected));
        DrawButton(batch, font, ui, layout.CraftButton, GameText.Craft, CanCraft(arena, selected));
        var feedback = string.IsNullOrEmpty(Feedback) ? GameText.SmithDetail : Feedback;
        var lines = ItemTooltip.Wrap(feedback, text => font.MeasureString(text).X * .85f, 308)
            .Concat(string.IsNullOrEmpty(CraftedRolls) ? Array.Empty<string>() :
                ItemTooltip.Wrap(CraftedRolls, text => font.MeasureString(text).X * .85f, 308));
        var y = layout.SmithFeedback.Y;
        foreach (var line in lines)
        {
            batch.DrawString(font, line, new Vector2(layout.SmithFeedback.X + 6, y),
                LastResult is null ? GothicUiRenderer.Muted :
                    LastResult.Status == BlacksmithStatus.Applied ? GothicUiRenderer.Gold : GothicUiRenderer.Red,
                0, Vector2.Zero, .85f, SpriteEffects.None, 0);
            y += (int)Math.Ceiling(font.LineSpacing * .85f);
        }
    }

    private static void DrawButton(SpriteBatch batch, SpriteFont font, GothicUiRenderer ui,
        Rectangle bounds, string label, bool enabled)
    {
        ui.DrawButton(batch, bounds, enabled);
        InventoryPanel.Text(batch, font, label, bounds,
            enabled ? GothicUiRenderer.Text : GothicUiRenderer.Muted, centered: true);
    }
}
