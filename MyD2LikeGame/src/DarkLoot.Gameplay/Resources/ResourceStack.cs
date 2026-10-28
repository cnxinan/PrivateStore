namespace DarkLoot.Gameplay.Resources;

public enum ResourceKind
{
    Material,
    Recipe
}

public sealed record ResourceStack(ResourceKind Kind, string DefinitionId, int Quantity);
