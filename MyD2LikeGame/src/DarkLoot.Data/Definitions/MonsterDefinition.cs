namespace DarkLoot.Data.Definitions;

public sealed record MonsterDefinition(
    string Id,
    string Name,
    int MaxHealth,
    int ContactDamage,
    float MoveSpeed,
    string LootTableId,
    MonsterBehavior Behavior = MonsterBehavior.Chase,
    RangedAttackDefinition? RangedAttack = null);
