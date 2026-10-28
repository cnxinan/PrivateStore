using System.Numerics;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Combat;

public sealed class MonsterBehaviorSystem
{
    private const float ArenaWidth = 960f;
    private const float ArenaHeight = 540f;
    private const float TimerEpsilon = 0.000001f;

    public void Update(MonsterState monster, Vector2 playerPosition, float elapsedSeconds) =>
        Update(monster, playerPosition, elapsedSeconds, static _ => { });

    public void Update(
        MonsterState monster,
        Vector2 playerPosition,
        float elapsedSeconds,
        Action<ProjectileState> launch)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(launch);
        if (monster.IsDefeated || elapsedSeconds < 0 ||
            !float.IsFinite(elapsedSeconds) || !IsFinite(playerPosition))
        {
            return;
        }

        if (monster.Reaction.IsActive)
        {
            monster.CancelWindup();
            if (monster.HasLaunched)
            {
                monster.RemainingUntilNextLaunch = Math.Max(
                    0, monster.RemainingUntilNextLaunch - elapsedSeconds);
            }

            return;
        }

        var deltaX = (double)playerPosition.X - monster.Position.X;
        var deltaY = (double)playerPosition.Y - monster.Position.Y;
        var distanceSquared = deltaX * deltaX + deltaY * deltaY;
        var rangedAttack = monster.Behavior == MonsterBehavior.Archer
            ? monster.Definition.RangedAttack
            : null;
        var inRange = rangedAttack is not null &&
            distanceSquared <= (double)rangedAttack.Range * rangedAttack.Range;

        if (distanceSquared > 0 && !monster.IsWindingUp)
        {
            monster.Facing = NormalizeFinite(new Vector2((float)deltaX, (float)deltaY));
        }

        if (distanceSquared > 0 && !inRange)
        {
            MoveTowardsPlayer(monster, deltaX, deltaY, distanceSquared, elapsedSeconds);
        }

        if (rangedAttack is null)
        {
            return;
        }

        if (monster.IsWindingUp)
        {
            AdvanceWindup(monster, rangedAttack, elapsedSeconds, launch);
        }
        else if (inRange)
        {
            var aimDirection = NormalizeFinite(new Vector2((float)deltaX, (float)deltaY));
            if (aimDirection == Vector2.Zero)
            {
                aimDirection = Vector2.UnitX;
            }

            AdvanceReadyArcher(monster, rangedAttack, aimDirection, elapsedSeconds, launch);
        }
        else if (monster.HasLaunched)
        {
            monster.RemainingUntilNextLaunch = Math.Max(
                0,
                monster.RemainingUntilNextLaunch - elapsedSeconds);
        }

        monster.WasInRangedAttackRange = inRange;
    }

    private static void AdvanceReadyArcher(
        MonsterState monster,
        RangedAttackDefinition attack,
        Vector2 aimDirection,
        float elapsedSeconds,
        Action<ProjectileState> launch)
    {
        if (!monster.HasLaunched)
        {
            BeginWindup(monster, attack.WindupSeconds, aimDirection);
            AdvanceWindup(monster, attack, elapsedSeconds, launch);
            return;
        }

        var remaining = monster.RemainingUntilNextLaunch;
        if (!monster.WasInRangedAttackRange && remaining <= attack.WindupSeconds)
        {
            remaining = attack.WindupSeconds;
        }

        if (remaining > attack.WindupSeconds)
        {
            var untilWindup = remaining - attack.WindupSeconds;
            if (elapsedSeconds + TimerEpsilon < untilWindup)
            {
                monster.RemainingUntilNextLaunch = remaining - elapsedSeconds;
                return;
            }

            var windupElapsed = elapsedSeconds - untilWindup;
            BeginWindup(monster, attack.WindupSeconds, aimDirection);
            AdvanceWindup(monster, attack, windupElapsed, launch);
            return;
        }

        BeginWindup(monster, Math.Max(remaining, attack.WindupSeconds), aimDirection);
        AdvanceWindup(monster, attack, elapsedSeconds, launch);
    }

    private static void BeginWindup(MonsterState monster, float remaining, Vector2 aimDirection)
    {
        monster.IsWindingUp = true;
        monster.WindupRemaining = remaining;
        monster.LockedAimDirection = aimDirection;
        monster.Facing = aimDirection;
    }

    private static void AdvanceWindup(
        MonsterState monster,
        RangedAttackDefinition attack,
        float elapsedSeconds,
        Action<ProjectileState> launch)
    {
        var remaining = monster.WindupRemaining;
        if (elapsedSeconds + TimerEpsilon < remaining)
        {
            monster.WindupRemaining = remaining - elapsedSeconds;
            monster.RemainingUntilNextLaunch = monster.WindupRemaining;
            return;
        }

        var elapsedAfterLaunch = Math.Max(0, elapsedSeconds - remaining);
        var direction = NormalizeFinite(monster.LockedAimDirection);
        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        launch(new ProjectileState(
            monster.InstanceId,
            monster.Position,
            direction,
            attack.ProjectileSpeed,
            attack.Damage));

        monster.IsWindingUp = false;
        monster.WindupRemaining = 0;
        monster.HasLaunched = true;
        monster.RemainingUntilNextLaunch = Math.Max(0, attack.FireInterval - elapsedAfterLaunch);
    }

    private static void MoveTowardsPlayer(
        MonsterState monster,
        double deltaX,
        double deltaY,
        double distanceSquared,
        float elapsedSeconds)
    {
        var distance = Math.Sqrt(distanceSquared);
        var travelDistance = Math.Min((double)monster.MoveSpeed * elapsedSeconds, distance);
        var x = monster.Position.X + deltaX / distance * travelDistance;
        var y = monster.Position.Y + deltaY / distance * travelDistance;
        monster.Position = new Vector2(
            (float)Math.Clamp(x, 0d, ArenaWidth),
            (float)Math.Clamp(y, 0d, ArenaHeight));
    }

    private static Vector2 NormalizeFinite(Vector2 vector)
    {
        if (!IsFinite(vector))
        {
            return Vector2.Zero;
        }

        var x = (double)vector.X;
        var y = (double)vector.Y;
        var length = Math.Sqrt(x * x + y * y);
        return length == 0
            ? Vector2.Zero
            : new Vector2((float)(x / length), (float)(y / length));
    }

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y);
}
