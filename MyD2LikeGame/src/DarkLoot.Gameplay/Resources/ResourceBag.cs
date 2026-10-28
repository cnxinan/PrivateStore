using System.Collections.ObjectModel;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Gameplay.Resources;

public sealed class ResourceBag
{
    private Dictionary<string, int> _materials = new(StringComparer.Ordinal);
    private Dictionary<string, int> _recipes = new(StringComparer.Ordinal);
    private HashSet<Guid> _collectedDropIds = [];
    private IReadOnlyDictionary<string, int> _readOnlyMaterials;
    private IReadOnlyDictionary<string, int> _readOnlyRecipes;

    public ResourceBag()
    {
        _readOnlyMaterials = new ReadOnlyDictionary<string, int>(_materials);
        _readOnlyRecipes = new ReadOnlyDictionary<string, int>(_recipes);
    }

    public IReadOnlyDictionary<string, int> Materials => _readOnlyMaterials;

    public IReadOnlyDictionary<string, int> Recipes => _readOnlyRecipes;

    public int GetCount(ResourceKind kind, string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        return kind switch
        {
            ResourceKind.Material => _materials.GetValueOrDefault(definitionId),
            ResourceKind.Recipe => _recipes.GetValueOrDefault(definitionId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown resource kind.")
        };
    }

    internal bool HasCollected(Guid dropId) => _collectedDropIds.Contains(dropId);

    internal ResourceBagSnapshot PreparePickup(GroundResource drop)
    {
        var (materials, recipes) = CopyCounts();
        var target = drop.Stack.Kind == ResourceKind.Material ? materials : recipes;
        var current = target.GetValueOrDefault(drop.Stack.DefinitionId);
        target[drop.Stack.DefinitionId] = checked(current + drop.Stack.Quantity);
        var collected = new HashSet<Guid>(_collectedDropIds) { drop.DropId };
        return new ResourceBagSnapshot(materials, recipes, collected);
    }

    internal ResourceBagSnapshot PrepareLearn(string recipeId)
    {
        var (materials, recipes) = CopyCounts();
        Decrement(recipes, recipeId, 1);
        return new ResourceBagSnapshot(materials, recipes, new HashSet<Guid>(_collectedDropIds));
    }

    internal ResourceBagSnapshot PrepareCraft(IReadOnlyList<MaterialCost> costs)
    {
        var (materials, recipes) = CopyCounts();
        foreach (var cost in costs)
        {
            Decrement(materials, cost.MaterialId, cost.Quantity);
        }

        return new ResourceBagSnapshot(materials, recipes, new HashSet<Guid>(_collectedDropIds));
    }

    internal void Commit(ResourceBagSnapshot snapshot)
    {
        _materials = snapshot.Materials;
        _recipes = snapshot.Recipes;
        _collectedDropIds = snapshot.CollectedDropIds;
        _readOnlyMaterials = snapshot.ReadOnlyMaterials;
        _readOnlyRecipes = snapshot.ReadOnlyRecipes;
    }

    private (Dictionary<string, int> Materials, Dictionary<string, int> Recipes) CopyCounts() =>
        (new Dictionary<string, int>(_materials, StringComparer.Ordinal),
         new Dictionary<string, int>(_recipes, StringComparer.Ordinal));

    private static void Decrement(Dictionary<string, int> counts, string id, int quantity)
    {
        var remaining = counts[id] - quantity;
        if (remaining == 0)
        {
            counts.Remove(id);
        }
        else
        {
            counts[id] = remaining;
        }
    }
}

internal sealed record ResourceBagSnapshot(
    Dictionary<string, int> Materials,
    Dictionary<string, int> Recipes,
    HashSet<Guid> CollectedDropIds)
{
    public IReadOnlyDictionary<string, int> ReadOnlyMaterials { get; } =
        new ReadOnlyDictionary<string, int>(Materials);

    public IReadOnlyDictionary<string, int> ReadOnlyRecipes { get; } =
        new ReadOnlyDictionary<string, int>(Recipes);
}
