namespace DarkLoot.Data.Definitions;

public sealed record RangedAttackDefinition(
    float Range,
    float ProjectileSpeed,
    float FireInterval,
    float WindupSeconds,
    int Damage);
