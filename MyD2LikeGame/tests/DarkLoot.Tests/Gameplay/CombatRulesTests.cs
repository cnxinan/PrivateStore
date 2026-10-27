using System.Numerics;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Tests.Gameplay;

public sealed class CombatRulesTests
{
    [Fact]
    public void IsInMeleeRange_RequiresRangeAndFacingArc()
    {
        Assert.True(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.UnitX, new Vector2(30, 10), 48, 45));
        Assert.False(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.UnitX, new Vector2(49, 0), 48, 45));
        Assert.False(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.UnitX, new Vector2(-20, 10), 48, 45));
    }

    [Fact]
    public void IsInMeleeRange_HandlesCoincidentAndInvalidVectorsWithoutNaNs()
    {
        Assert.True(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.Zero, Vector2.Zero, 48, 45));
        Assert.False(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.UnitX, new Vector2(float.NaN, 0), 48, 45));
        Assert.False(CombatRules.IsInMeleeRange(
            Vector2.Zero, Vector2.Zero, Vector2.UnitX, 48, 45));
    }
}
