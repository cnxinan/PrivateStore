using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Gameplay.Resources;

public enum ResourcePickupResult
{
    Applied,
    PlayerDead,
    AlreadyCollected
}

public sealed class ResourcePickupService
{
    private readonly GameContent _content;

    public ResourcePickupService(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    public ResourcePickupResult PickUp(PlayerState player, GroundResource drop)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(drop);
        if (drop.DropId == Guid.Empty)
        {
            throw new ArgumentException("Drop ID must not be empty.", nameof(drop));
        }

        if (drop.Stack is null)
        {
            throw new ArgumentException("Resource stack must not be null.", nameof(drop));
        }

        if (!IsFinite(drop.Position))
        {
            throw new ArgumentException("Drop position must be finite.", nameof(drop));
        }

        if (drop.Stack.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drop), "Resource quantity must be positive.");
        }

        ValidateDefinition(drop.Stack.Kind, drop.Stack.DefinitionId);
        if (player.IsDead)
        {
            return ResourcePickupResult.PlayerDead;
        }

        if (player.Resources.HasCollected(drop.DropId))
        {
            return ResourcePickupResult.AlreadyCollected;
        }

        var candidate = player.Resources.PreparePickup(drop);
        player.Resources.Commit(candidate);
        return ResourcePickupResult.Applied;
    }

    private void ValidateDefinition(ResourceKind kind, string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        var exists = kind switch
        {
            ResourceKind.Material =>
                _content.Materials.TryGetValue(definitionId, out var material) &&
                material is not null &&
                string.Equals(material.Id, definitionId, StringComparison.Ordinal),
            ResourceKind.Recipe =>
                _content.Recipes.TryGetValue(definitionId, out var recipe) &&
                recipe is not null &&
                string.Equals(recipe.Id, definitionId, StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown resource kind.")
        };
        if (!exists)
        {
            throw new ArgumentException($"Unknown {kind} definition '{definitionId}'.", nameof(definitionId));
        }
    }

    private static bool IsFinite(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y);
}
