using System.Numerics;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Combat;

public sealed class MonsterBehaviorSystem
{
    private const float ArenaWidth = 960f;
    private const float ArenaHeight = 540f;

    public void Update(MonsterState monster, Vector2 playerPosition, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (monster.IsDefeated || elapsedSeconds <= 0 ||
            !float.IsFinite(elapsedSeconds) || !IsFinite(playerPosition))
        {
            return;
        }

        var delta = playerPosition - monster.Position;
        var distanceSquared = (double)delta.X * delta.X + (double)delta.Y * delta.Y;
        if (distanceSquared == 0)
        {
            return;
        }

        if (monster.Behavior == MonsterBehavior.Archer &&
            distanceSquared <= Math.Pow(monster.Definition.RangedAttack!.Range, 2))
        {
            return;
        }

        var distance = Math.Sqrt(distanceSquared);
        var travelDistance = Math.Min((double)monster.MoveSpeed * elapsedSeconds, distance);
        var position = travelDistance >= distance
            ? playerPosition
            : monster.Position + delta / (float)distance * (float)travelDistance;
        monster.Position = new Vector2(
            (float)Math.Clamp(position.X, 0d, ArenaWidth),
            (float)Math.Clamp(position.Y, 0d, ArenaHeight));
    }

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y);
}
