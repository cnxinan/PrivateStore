using System.Numerics;

namespace DarkLoot.Gameplay.Loot;

public sealed record KillRecord(
    long RoundId,
    string MonsterInstanceId,
    Vector2 DeathPosition,
    string LootTableId);
