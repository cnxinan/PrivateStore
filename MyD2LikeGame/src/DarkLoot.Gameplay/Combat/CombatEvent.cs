using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public enum CombatEventKind { Swing, Damaged, ContactAttack, ArrowLaunched, Died }

public sealed record CombatEvent(
    CombatEventKind Kind, string ActorId, Vector2 Position,
    Vector2 Direction, int Damage, bool WasCritical);
