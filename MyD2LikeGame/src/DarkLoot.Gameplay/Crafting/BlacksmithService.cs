using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Crafting;

public enum BlacksmithStatus
{
    Applied,
    PlayerDead,
    TooFar,
    RecipeNotOwned,
    AlreadyLearned,
    NotLearned,
    InsufficientMaterials
}

public sealed record BlacksmithResult(BlacksmithStatus Status, ItemInstance? Item = null);

public sealed class BlacksmithService
{
    private const float InteractionDistanceSquared = 64 * 64;

    private readonly GameContent _content;
    private readonly Random _random;
    private readonly InventoryService _equipment;
    private readonly BlacksmithState _state;
    private readonly EquipmentFactory _equipmentFactory = new();

    public BlacksmithService(
        GameContent content, Random random, InventoryService equipment, BlacksmithState state)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(state);
        _content = content;
        _random = random;
        _equipment = equipment;
        _state = state;
    }

    public static Vector2 Position { get; } = new(120, 230);

    public bool CanInteract(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        ValidatePosition(player.Position);
        return !player.IsDead &&
            Vector2.DistanceSquared(player.Position, Position) <= InteractionDistanceSquared;
    }

    public BlacksmithResult Learn(PlayerState player, string recipeId)
    {
        ArgumentNullException.ThrowIfNull(player);
        var recipe = ResolveRecipe(recipeId);
        var access = CheckAccess(player);
        if (access is not null)
        {
            return new BlacksmithResult(access.Value);
        }

        if (player.Resources.GetCount(ResourceKind.Recipe, recipeId) == 0)
        {
            return new BlacksmithResult(BlacksmithStatus.RecipeNotOwned);
        }

        if (_state.HasLearned(recipeId))
        {
            return new BlacksmithResult(BlacksmithStatus.AlreadyLearned);
        }

        var resourceCandidate = player.Resources.PrepareLearn(recipeId);
        var knowledgeCandidate = _state.PrepareLearn(recipeId);
        player.Resources.Commit(resourceCandidate);
        _state.Commit(knowledgeCandidate);
        return new BlacksmithResult(BlacksmithStatus.Applied);
    }

    public BlacksmithResult Craft(PlayerState player, string recipeId)
    {
        ArgumentNullException.ThrowIfNull(player);
        var recipe = ResolveRecipe(recipeId);
        var access = CheckAccess(player);
        if (access is not null)
        {
            return new BlacksmithResult(access.Value);
        }

        if (!_state.HasLearned(recipeId))
        {
            return new BlacksmithResult(BlacksmithStatus.NotLearned);
        }

        if (!CanAfford(player.Resources, recipe.Costs))
        {
            return new BlacksmithResult(BlacksmithStatus.InsufficientMaterials);
        }

        var resourceCandidate = player.Resources.PrepareCraft(recipe.Costs);
        var item = _equipmentFactory.Create(recipe.ItemId, recipe.Rarity, _content, _random);
        var addition = _equipment.ValidateAddition(player, item);
        if (addition != EquipmentChangeResult.Applied)
        {
            throw new InvalidOperationException(
                $"Newly crafted item failed inventory validation: {addition}.");
        }

        player.Resources.Commit(resourceCandidate);
        _equipment.CommitAddition(player, item);
        return new BlacksmithResult(BlacksmithStatus.Applied, item);
    }

    private BlacksmithStatus? CheckAccess(PlayerState player)
    {
        if (player.IsDead)
        {
            return BlacksmithStatus.PlayerDead;
        }

        return CanInteract(player) ? null : BlacksmithStatus.TooFar;
    }

    private RecipeDefinition ResolveRecipe(string recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        if (!_content.Recipes.TryGetValue(recipeId, out var recipe) ||
            recipe is null ||
            !string.Equals(recipe.Id, recipeId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unknown recipe definition '{recipeId}'.", nameof(recipeId));
        }

        if (string.IsNullOrWhiteSpace(recipe.Name) ||
            recipe.Costs is null ||
            string.IsNullOrWhiteSpace(recipe.ItemId))
        {
            throw new ArgumentException($"Recipe '{recipeId}' has invalid fields.", nameof(_content));
        }

        _ = ItemStatCalculator.ResolveItemDefinition(_content, recipe.ItemId);
        _ = EquipmentFactory.AffixCount(recipe.Rarity);
        foreach (var cost in recipe.Costs)
        {
            if (cost is null ||
                string.IsNullOrWhiteSpace(cost.MaterialId) ||
                cost.Quantity <= 0 ||
                !_content.Materials.TryGetValue(cost.MaterialId, out var material) ||
                material is null ||
                !string.Equals(material.Id, cost.MaterialId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Recipe '{recipeId}' has invalid material costs.", nameof(_content));
            }
        }

        return recipe;
    }

    private static bool CanAfford(ResourceBag resources, IReadOnlyList<MaterialCost> costs)
    {
        var remaining = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            var available = remaining.TryGetValue(cost.MaterialId, out var current)
                ? current
                : resources.GetCount(ResourceKind.Material, cost.MaterialId);
            var afterCost = available - cost.Quantity;
            if (afterCost < 0)
            {
                return false;
            }

            remaining[cost.MaterialId] = afterCost;
        }

        return true;
    }

    private static void ValidatePosition(Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            throw new ArgumentException("Player position must be finite.", nameof(position));
        }
    }
}
