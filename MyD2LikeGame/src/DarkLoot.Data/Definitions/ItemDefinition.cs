namespace DarkLoot.Data.Definitions;

public sealed record ItemDefinition(string Id, string Name, int AttackBonus)
{
    public const int MaxAttackBonus = 1_000_000;
}
