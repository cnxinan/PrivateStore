using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public sealed class HitReactionState
{
    private Vector2 _direction;
    private float _distance;
    private double _duration;
    private double _elapsed;

    public float RemainingSeconds => (float)Math.Max(0, _duration - _elapsed);

    public bool IsActive => RemainingSeconds > 0;

    public void Begin(Vector2 direction, float distance, float duration)
    {
        if (!float.IsFinite(distance) || distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        if (!float.IsFinite(duration) || duration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _direction = NormalizeDirection(direction);
        _distance = distance;
        _duration = duration;
        _elapsed = 0;
    }

    public Vector2 Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!IsActive || elapsedSeconds == 0)
        {
            return Vector2.Zero;
        }

        var previousProgress = _elapsed / _duration;
        _elapsed = Math.Min(_duration, _elapsed + elapsedSeconds);
        if (_duration - _elapsed <= _duration * .000001)
        {
            _elapsed = _duration;
        }

        var progress = Math.Clamp(_elapsed / _duration, 0, 1);
        var previousEased = 1 - (1 - previousProgress) * (1 - previousProgress);
        var eased = 1 - (1 - progress) * (1 - progress);
        return _direction * (float)(_distance * (eased - previousEased));
    }

    public void Clear()
    {
        _direction = Vector2.Zero;
        _distance = 0;
        _duration = 0;
        _elapsed = 0;
    }

    internal static Vector2 NormalizeDirection(Vector2 direction)
    {
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            return Vector2.UnitX;
        }

        var length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
        return length == 0
            ? Vector2.UnitX
            : new Vector2((float)(direction.X / length), (float)(direction.Y / length));
    }
}
