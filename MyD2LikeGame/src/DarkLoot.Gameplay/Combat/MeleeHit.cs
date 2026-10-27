namespace DarkLoot.Gameplay.Combat;

public sealed record MeleeHit(string MonsterInstanceId, int Damage, bool WasCritical);
