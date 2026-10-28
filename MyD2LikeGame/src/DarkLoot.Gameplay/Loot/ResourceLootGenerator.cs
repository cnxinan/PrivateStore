using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Loot;

public sealed class ResourceLootGenerator
{
    public IReadOnlyList<ResourceStack> Generate(
        ResourceLootTableDefinition table,
        GameContent content,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrWhiteSpace(table.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(table.MaterialId);
        if (table.MinQuantity < 1 || table.MaxQuantity < table.MinQuantity)
        {
            throw new ArgumentException("Resource quantity bounds must be positive and ordered.", nameof(table));
        }

        if (table.RecipeChancePercent is < 0 or > 100)
        {
            throw new ArgumentException("Recipe chance must be between 0 and 100.", nameof(table));
        }

        if (table.RecipeIds is null ||
            (table.RecipeChancePercent > 0 && table.RecipeIds.Count == 0))
        {
            throw new ArgumentException(
                "Recipe candidates are required when recipe chance is positive.", nameof(table));
        }

        if (!content.Materials.TryGetValue(table.MaterialId, out var material) ||
            material is null ||
            !string.Equals(material.Id, table.MaterialId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Resource loot table '{table.Id}' references missing material '{table.MaterialId}'.",
                nameof(table));
        }

        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipeId in table.RecipeIds)
        {
            if (string.IsNullOrWhiteSpace(recipeId) ||
                !recipeIds.Add(recipeId) ||
                !content.Recipes.TryGetValue(recipeId, out var recipe) ||
                recipe is null ||
                !string.Equals(recipe.Id, recipeId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Resource loot table '{table.Id}' references invalid recipe '{recipeId}'.",
                    nameof(table));
            }
        }

        var quantity = (int)random.NextInt64(table.MinQuantity, (long)table.MaxQuantity + 1);
        var result = new List<ResourceStack>
        {
            new(ResourceKind.Material, table.MaterialId, quantity)
        };
        var recipeDrops = table.RecipeChancePercent == 100 ||
            (table.RecipeChancePercent > 0 && random.Next(100) < table.RecipeChancePercent);
        if (recipeDrops)
        {
            result.Add(new ResourceStack(
                ResourceKind.Recipe,
                table.RecipeIds[random.Next(table.RecipeIds.Count)],
                1));
        }

        return result.AsReadOnly();
    }
}
