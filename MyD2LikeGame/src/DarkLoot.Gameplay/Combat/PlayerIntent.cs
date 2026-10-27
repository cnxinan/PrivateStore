using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public sealed record PlayerIntent(
    Vector2 MoveDirection,
    Vector2 AimWorldPosition,
    bool AttackPressed,
    bool PickupPressed);
