using System;
using System.Numerics;

namespace DarkLoot.Game;

public enum CombatEffectKind { Slash, Punch, Hit, Critical }

public sealed record CombatEffect(
    CombatEffectKind Kind, string ActorId, Vector2 Position, Vector2 Direction, float Age = 0)
{
    public float Duration => Kind switch
    {
        CombatEffectKind.Slash => .18f,
        CombatEffectKind.Punch => .12f,
        CombatEffectKind.Hit => .24f,
        CombatEffectKind.Critical => .36f,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}
