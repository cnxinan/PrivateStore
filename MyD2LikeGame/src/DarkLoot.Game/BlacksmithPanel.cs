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
    public void SetOpen(bool open) => IsOpen = open;
    public Rectangle Bounds(int width) => new(width - 660, 98, 640, 590);
    public Rectangle RowBounds(int width, int row) => new(width - 648, 168 + row * 100, 616, 94);
    public Rectangle LearnButtonBounds(int width, int row) => new(width - 240, 178 + row * 100, 90, 28);
    public Rectangle CraftButtonBounds(int width, int row) => new(width - 140, 178 + row * 100, 100, 28);
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
            var learn = LearnButtonBounds(width, row).Contains(point.X, point.Y);
            if (!learn && !CraftButtonBounds(width, row).Contains(point.X, point.Y))
                continue;

            var recipe = recipes[row];
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
        ArenaSimulation arena, GameContent content, int width)
    {
        if (!IsOpen)
            return;

        var bounds = Bounds(width);
        shapes.Rectangle(batch, bounds, new Color(28, 33, 48));
        GameText.Draw(batch, font, GameText.Smith, new Vector2(bounds.X + 12, 108), Color.White, 616);
        GameText.Draw(batch, font, GameText.SmithPaused, new Vector2(bounds.X + 12, 136), Color.LightGray, 616);
        var recipes = content.Recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray();
        for (var row = 0; row < recipes.Length; row++)
        {
            var recipe = recipes[row];
            var rowBounds = RowBounds(width, row);
            shapes.Rectangle(batch, rowBounds, new Color(43, 49, 65));
            GameText.Draw(batch, font, recipe.Name,
                new Vector2(rowBounds.X + 8, rowBounds.Y + 4), InventoryPanel.RarityColor(recipe.Rarity), 390);
            GameText.Draw(batch, font, string.Format(GameText.RecipeState,
                    arena.Blacksmith.HasLearned(recipe.Id) ? GameText.Known : GameText.Unknown,
                    arena.Player.Resources.GetCount(ResourceKind.Recipe, recipe.Id)),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 26), Color.White, 390);
            GameText.Draw(batch, font, GameText.MaterialCostHelp,
                new Vector2(rowBounds.X + 8, rowBounds.Y + 48), Color.LightGray, 600, 18);
            var column = 0;
            foreach (var cost in recipe.Costs)
            {
                var count = arena.Player.Resources.GetCount(ResourceKind.Material, cost.MaterialId);
                GameText.Draw(batch, font, string.Format(GameText.MaterialCost,
                        content.Materials[cost.MaterialId].Name, count, cost.Quantity),
                    new Vector2(rowBounds.X + 8 + column++ * 200, rowBounds.Y + 68),
                    count >= cost.Quantity ? Color.LightCyan : Color.Salmon, 196);
            }
            DrawButton(batch, shapes, font, LearnButtonBounds(width, row), GameText.Learn, CanLearn(arena, recipe));
            DrawButton(batch, shapes, font, CraftButtonBounds(width, row), GameText.Craft, CanCraft(arena, recipe));
        }
        GameText.Draw(batch, font, Feedback, new Vector2(bounds.X + 12, 578),
            LastResult?.Status == BlacksmithStatus.Applied ? Color.Gold : Color.Salmon, 616);
        GameText.Draw(batch, font, CraftedRolls, new Vector2(bounds.X + 12, 606), Color.White, 616);
    }

    private static void DrawButton(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        Rectangle bounds, string label, bool enabled)
    {
        shapes.Rectangle(batch, bounds, enabled ? new Color(53, 100, 70) : new Color(55, 58, 65));
        GameText.Draw(batch, font, label, new Vector2(bounds.X + 8, bounds.Y + 3),
            enabled ? Color.White : Color.Gray, bounds.Width - 16);
    }
}
