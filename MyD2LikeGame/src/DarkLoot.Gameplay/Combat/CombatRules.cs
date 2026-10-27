using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public static class CombatRules
{
    public static bool IsInMeleeRange(
        Vector2 origin,
        Vector2 facing,
        Vector2 target,
        float range,
        float halfAngleDegrees)
    {
        if (!IsFinite(origin) ||
            !IsFinite(facing) ||
            !IsFinite(target) ||
            !float.IsFinite(range) ||
            range < 0 ||
            !float.IsFinite(halfAngleDegrees) ||
            halfAngleDegrees is < 0 or > 180)
        {
            return false;
        }

        var targetX = (double)target.X - origin.X;
        var targetY = (double)target.Y - origin.Y;
        var targetLengthSquared = targetX * targetX + targetY * targetY;
        var rangeSquared = (double)range * range;
        if (targetLengthSquared > rangeSquared)
        {
            return false;
        }

        if (targetLengthSquared == 0)
        {
            return true;
        }

        var facingX = (double)facing.X;
        var facingY = (double)facing.Y;
        var facingLengthSquared = facingX * facingX + facingY * facingY;
        if (facingLengthSquared == 0)
        {
            return false;
        }

        var dot = targetX * facingX + targetY * facingY;
        var cosine = dot / Math.Sqrt(targetLengthSquared * facingLengthSquared);
        var minimumCosine = Math.Cos(halfAngleDegrees * Math.PI / 180d);
        return cosine >= minimumCosine;
    }

    private static bool IsFinite(Vector2 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y);
    }
}
