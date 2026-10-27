using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public sealed record ProjectileState(
    string SourceInstanceId,
    Vector2 Position,
    Vector2 Direction,
    float Speed,
    int Damage);
