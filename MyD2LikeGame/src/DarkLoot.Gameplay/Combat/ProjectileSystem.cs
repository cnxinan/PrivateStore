using System.Collections.ObjectModel;
using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public sealed class ProjectileSystem
{
    private const float ArenaWidth = 960f;
    private const float ArenaHeight = 540f;
    private const float PlayerRadius = 14f;

    private readonly List<ProjectileState> _projectiles = [];
    private readonly ReadOnlyCollection<ProjectileState> _readOnlyProjectiles;

    public ProjectileSystem()
    {
        _readOnlyProjectiles = _projectiles.AsReadOnly();
    }

    public IReadOnlyList<ProjectileState> Projectiles => _readOnlyProjectiles;

    public void Spawn(ProjectileState projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        if (string.IsNullOrWhiteSpace(projectile.SourceInstanceId) ||
            !IsFinite(projectile.Position) ||
            !IsFinite(projectile.Direction) ||
            !float.IsFinite(projectile.Speed) ||
            projectile.Speed <= 0 ||
            projectile.Damage < 0)
        {
            throw new ArgumentException("Projectile has invalid state.", nameof(projectile));
        }

        var directionLength = Math.Sqrt(
            (double)projectile.Direction.X * projectile.Direction.X +
            (double)projectile.Direction.Y * projectile.Direction.Y);
        if (directionLength == 0)
        {
            throw new ArgumentException("Projectile direction must be non-zero.", nameof(projectile));
        }

        _projectiles.Add(projectile with
        {
            Direction = new Vector2(
                (float)(projectile.Direction.X / directionLength),
                (float)(projectile.Direction.Y / directionLength))
        });
    }

    public void Clear() => _projectiles.Clear();

    public bool Update(
        float elapsedSeconds,
        Vector2 playerPosition,
        bool playerAlive,
        Func<ProjectileState, bool> applyDamage)
    {
        ArgumentNullException.ThrowIfNull(applyDamage);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "Elapsed time must be finite and non-negative.");
        }

        foreach (var projectile in _projectiles.ToArray())
        {
            var index = _projectiles.IndexOf(projectile);
            if (index < 0)
            {
                continue;
            }

            var start = projectile.Position;
            var endX = (double)start.X +
                projectile.Direction.X * (double)projectile.Speed * elapsedSeconds;
            var endY = (double)start.Y +
                projectile.Direction.Y * (double)projectile.Speed * elapsedSeconds;

            if (playerAlive &&
                TryClipToArena(start, endX, endY, out var clippedStart, out var clippedEnd) &&
                IsFinite(playerPosition) &&
                SegmentHitsPlayer(clippedStart, clippedEnd, playerPosition))
            {
                _projectiles.RemoveAt(index);
                if (applyDamage(projectile))
                {
                    return true;
                }

                continue;
            }

            if (IsOutsideArena(endX, endY))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            _projectiles[index] = projectile with
            {
                Position = new Vector2((float)endX, (float)endY)
            };
        }

        return false;
    }

    private static bool SegmentHitsPlayer(Vector2 start, Vector2 end, Vector2 playerPosition)
    {
        var travel = end - start;
        var lengthSquared = travel.LengthSquared();
        var t = lengthSquared == 0
            ? 0
            : Math.Clamp(Vector2.Dot(playerPosition - start, travel) / lengthSquared, 0f, 1f);
        var nearest = start + travel * t;
        return Vector2.DistanceSquared(nearest, playerPosition) <= PlayerRadius * PlayerRadius;
    }

    private static bool TryClipToArena(
        Vector2 start,
        double endX,
        double endY,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        var deltaX = endX - start.X;
        var deltaY = endY - start.Y;
        var startTime = 0d;
        var endTime = 1d;
        if (!Clip(-deltaX, start.X, ref startTime, ref endTime) ||
            !Clip(deltaX, ArenaWidth - start.X, ref startTime, ref endTime) ||
            !Clip(-deltaY, start.Y, ref startTime, ref endTime) ||
            !Clip(deltaY, ArenaHeight - start.Y, ref startTime, ref endTime))
        {
            clippedStart = default;
            clippedEnd = default;
            return false;
        }

        clippedStart = new Vector2(
            (float)(start.X + startTime * deltaX),
            (float)(start.Y + startTime * deltaY));
        clippedEnd = new Vector2(
            (float)(start.X + endTime * deltaX),
            (float)(start.Y + endTime * deltaY));
        return true;
    }

    private static bool Clip(double direction, double distance, ref double startTime, ref double endTime)
    {
        if (direction == 0)
        {
            return distance >= 0;
        }

        var time = distance / direction;
        if (direction < 0)
        {
            if (time > endTime)
            {
                return false;
            }

            startTime = Math.Max(startTime, time);
        }
        else
        {
            if (time < startTime)
            {
                return false;
            }

            endTime = Math.Min(endTime, time);
        }

        return true;
    }

    private static bool IsOutsideArena(double x, double y) =>
        x < 0 || x > ArenaWidth || y < 0 || y > ArenaHeight;

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y);
}
