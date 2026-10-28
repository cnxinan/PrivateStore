using System.Numerics;
using DarkLoot.Engine.Animation;

namespace DarkLoot.Tests.Engine;

public sealed class ActorAnimatorTests
{
    private static ActorAnimationInput Input => new(false, false, false, false, false, 0, false, Vector2.UnitX);

    [Theory]
    [InlineData(ActorVisualKind.Player)]
    [InlineData(ActorVisualKind.Slime)]
    [InlineData(ActorVisualKind.Wolf)]
    [InlineData(ActorVisualKind.Archer)]
    public void Locomotion_HasDistinctBoundedPhasesAndReadOnlyPose(ActorVisualKind kind)
    {
        var animator = new ActorAnimator(kind);
        animator.Update(Input, .05f);
        var idle = animator.Pose;
        animator.Update(Input, .05f);
        Assert.NotEqual(idle, animator.Pose);
        animator.Update(Input with { Moving = true }, .05f);
        var moving = animator.Pose;
        animator.Update(Input with { Moving = true }, .05f);
        Assert.NotEqual(moving, animator.Pose);
        Assert.Equal(ActorAction.Move, animator.Action);
        var frozen = animator.Pose;
        for (var i = 0; i < 100; i++)
            Assert.Equal(frozen, animator.Pose);
        AssertBounded(idle);
        AssertBounded(moving);
        AssertBounded(animator.Pose);
        if (kind is ActorVisualKind.Wolf or ActorVisualKind.Archer)
            Assert.NotEqual(0, animator.Pose.LimbSwing);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player)]
    [InlineData(ActorVisualKind.Slime)]
    [InlineData(ActorVisualKind.Wolf)]
    [InlineData(ActorVisualKind.Archer)]
    public void Update_PartitioningConstantInputPreservesPose(ActorVisualKind kind)
    {
        var whole = new ActorAnimator(kind);
        var split = new ActorAnimator(kind);
        var input = Input with { Moving = true, Invulnerable = true };
        whole.Update(input, .125f);
        for (var i = 0; i < 8; i++)
            split.Update(input, .015625f);
        AssertPoseNear(whole.Pose, split.Pose);
        whole.Update(Input with { Dead = true }, .125f);
        for (var i = 0; i < 8; i++)
            split.Update(Input with { Dead = true }, .015625f);
        AssertPoseNear(whole.Pose, split.Pose);
    }

    [Fact]
    public void StoppingMovement_DecaysContinuouslyWithoutSnappingLimbs()
    {
        var animator = new ActorAnimator(ActorVisualKind.Wolf);
        animator.Update(Input with { Moving = true }, .11f);
        var before = animator.Pose;
        animator.Update(Input, 0);
        Assert.Equal(before, animator.Pose);
        animator.Update(Input, .001f);
        Assert.InRange(Vector2.Distance(before.Offset, animator.Pose.Offset), 0, .1f);
        Assert.InRange(MathF.Abs(before.LimbSwing - animator.Pose.LimbSwing), 0, .02f);
        animator.Update(Input, 3);
        Assert.InRange(MathF.Abs(animator.Pose.LimbSwing), 0, .0001f);
        Assert.InRange(animator.Pose.Offset.Length(), 0, .0001f);
        Assert.Equal(ActorAction.Idle, animator.Action);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player, .18f)]
    [InlineData(ActorVisualKind.Slime, .16f)]
    [InlineData(ActorVisualKind.Wolf, .16f)]
    [InlineData(ActorVisualKind.Archer, .12f)]
    public void AttackImpulse_AdvancesOnceLocksFacingAndExpires(ActorVisualKind kind, float duration)
    {
        var animator = new ActorAnimator(kind);
        animator.Update(Input with { AttackTriggered = true, Facing = new Vector2(3, 4) }, duration / 4);
        var early = animator.Pose;
        Assert.Equal(ActorAction.Attack, animator.Action);
        Assert.InRange(early.AttackProgress, .24f, .26f);
        animator.Update(Input with { Facing = -Vector2.UnitX }, duration / 4);
        Assert.InRange(animator.Pose.AttackProgress, .49f, .51f);
        Assert.NotEqual(early.Offset, animator.Pose.Offset);
        Assert.Equal(early.Facing, animator.Pose.Facing);
        animator.Update(Input, duration / 2);
        Assert.Equal(ActorAction.Idle, animator.Action);
        Assert.Equal(0, animator.Pose.AttackProgress);
        animator.Update(Input with { AttackTriggered = true, Facing = -Vector2.UnitY }, 0);
        Assert.Equal(ActorAction.Attack, animator.Action);
        Assert.Equal(-Vector2.UnitY, animator.Pose.Facing);
    }

    [Fact]
    public void ArcherWindup_UsesClampedProgressAndLocksDirectionThenRecoils()
    {
        var animator = new ActorAnimator(ActorVisualKind.Archer);
        animator.Update(Input with { WindingUp = true, WindupProgress = -.5f }, 0);
        var start = animator.Pose;
        animator.Update(Input with { WindingUp = true, WindupProgress = .5f, Facing = -Vector2.UnitX }, 0);
        var middle = animator.Pose;
        animator.Update(Input with { WindingUp = true, WindupProgress = 2 }, 0);
        var end = animator.Pose;
        Assert.Equal(ActorAction.Attack, animator.Action);
        Assert.NotEqual(start.Offset, middle.Offset);
        Assert.NotEqual(middle.Offset, end.Offset);
        Assert.Equal(Vector2.UnitX, middle.Facing);
        Assert.Equal(1, end.AttackProgress);
        animator.Update(Input with { AttackTriggered = true, Facing = -Vector2.UnitX }, .03f);
        Assert.Equal(-Vector2.UnitX, animator.Pose.Facing);
        Assert.NotEqual(end, animator.Pose);
        animator.Update(Input, .09f);
        Assert.Equal(ActorAction.Idle, animator.Action);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player, .15f)]
    [InlineData(ActorVisualKind.Slime, .12f)]
    [InlineData(ActorVisualKind.Wolf, .12f)]
    [InlineData(ActorVisualKind.Archer, .12f)]
    public void Hurt_OverridesAttackDoesNotRestartAndDiscardsBlockedImpulse(ActorVisualKind kind, float duration)
    {
        var animator = new ActorAnimator(kind);
        animator.Update(Input with { AttackTriggered = true }, .01f);
        animator.Update(Input with { Hurt = true, Moving = true, WindingUp = true, AttackTriggered = true }, 0);
        var impact = animator.Pose;
        Assert.Equal(ActorAction.Hurt, animator.Action);
        Assert.True(impact.WhiteMix > 0);
        Assert.True(impact.Scale.Y < 1);
        Assert.Equal(0, impact.AttackProgress);
        animator.Update(Input with { Hurt = true, AttackTriggered = true }, duration / 2);
        Assert.InRange(animator.Pose.WhiteMix, 0, impact.WhiteMix - .01f);
        animator.Update(Input with { Hurt = true }, duration);
        Assert.Equal(ActorAction.Hurt, animator.Action);
        Assert.Equal(0, animator.Pose.WhiteMix);
        animator.Update(Input with { Moving = true }, 0);
        Assert.Equal(ActorAction.Move, animator.Action);
        Assert.Equal(0, animator.Pose.AttackProgress);
        animator.Update(Input with { Hurt = true }, 0);
        Assert.True(animator.Pose.WhiteMix > 0);
    }

    [Fact]
    public void BriefHurt_RemainsVisibleUntilRecoveryAndCannotReplayAttack()
    {
        var animator = new ActorAnimator(ActorVisualKind.Player);
        animator.Update(Input with { Hurt = true }, .025f);
        animator.Update(Input with { AttackTriggered = true }, .025f);
        Assert.Equal(ActorAction.Hurt, animator.Action);
        animator.Update(Input, .15f);
        Assert.Equal(ActorAction.Idle, animator.Action);
        Assert.Equal(0, animator.Pose.AttackProgress);
    }

    [Fact]
    public void ArcherHurt_CancelsWindupAndRequiresFreshAuthoritativeWindup()
    {
        var animator = new ActorAnimator(ActorVisualKind.Archer);
        animator.Update(Input with { WindingUp = true, WindupProgress = .8f }, .1f);
        animator.Update(Input with { Hurt = true, WindingUp = true, WindupProgress = 1 }, .02f);
        Assert.Equal(ActorAction.Hurt, animator.Action);
        Assert.Equal(0, animator.Pose.AttackProgress);
        animator.Update(Input, .12f);
        Assert.Equal(ActorAction.Idle, animator.Action);
        animator.Update(Input with { WindingUp = true, WindupProgress = .2f, Facing = Vector2.UnitY }, 0);
        Assert.Equal(ActorAction.Attack, animator.Action);
        Assert.Equal(.2f, animator.Pose.AttackProgress);
        Assert.Equal(Vector2.UnitY, animator.Pose.Facing);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player)]
    [InlineData(ActorVisualKind.Slime)]
    [InlineData(ActorVisualKind.Wolf)]
    [InlineData(ActorVisualKind.Archer)]
    public void Curves_StayBoundedThroughMovementAttackHurtAndDeath(ActorVisualKind kind)
    {
        var animator = new ActorAnimator(kind);
        for (var frame = 0; frame < 300; frame++)
        {
            var input = Input with
            {
                Moving = frame < 100,
                Invulnerable = true,
                AttackTriggered = frame == 50,
                Hurt = frame is >= 100 and < 120,
                Dead = frame >= 200
            };
            animator.Update(input, .01f);
            AssertBounded(animator.Pose, allowZeroScale: animator.Action == ActorAction.Dead);
        }
        Assert.Equal(0, animator.Pose.Opacity);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player, .6f)]
    [InlineData(ActorVisualKind.Slime, .3f)]
    [InlineData(ActorVisualKind.Wolf, .3f)]
    [InlineData(ActorVisualKind.Archer, .3f)]
    public void Death_HasMiddleEndAndTerminalPoseUntilReset(ActorVisualKind kind, float duration)
    {
        var animator = new ActorAnimator(kind);
        animator.Update(Input with { Dead = true, Hurt = true, AttackTriggered = true, Moving = true }, 0);
        var start = animator.Pose;
        animator.Update(Input with { Dead = true }, duration / 2);
        var middle = animator.Pose;
        Assert.Equal(ActorAction.Dead, animator.Action);
        Assert.InRange(middle.Opacity, .1f, .9f);
        Assert.True(middle.Scale.X < start.Scale.X);
        Assert.True(middle.Scale.Y > 0);
        if (kind == ActorVisualKind.Player)
            Assert.NotEqual(start.Rotation, middle.Rotation);
        animator.Update(Input with { Dead = true }, duration / 2);
        var end = animator.Pose;
        Assert.Equal(0, end.Opacity);
        AssertBounded(end, allowZeroScale: true);
        animator.Update(Input with { Hurt = true, AttackTriggered = true, Facing = -Vector2.UnitX }, 10);
        Assert.Equal(ActorAction.Dead, animator.Action);
        Assert.Equal(end, animator.Pose);
        animator.Reset();
        Assert.Equal(new ActorAnimator(kind).Pose, animator.Pose);
        Assert.Equal(ActorAction.Idle, animator.Action);
        animator.Update(Input with { AttackTriggered = true }, .01f);
        Assert.Equal(ActorAction.Attack, animator.Action);
    }

    [Theory]
    [InlineData(ActorVisualKind.Player, .6f)]
    [InlineData(ActorVisualKind.Slime, .3f)]
    public void Death_DoesNotRoundScaleOrOpacityToZeroBeforeDuration(ActorVisualKind kind, float duration)
    {
        var animator = new ActorAnimator(kind);
        var beforeEnd = MathF.BitDecrement(duration);
        animator.Update(Input with { Dead = true }, beforeEnd);
        Assert.True(animator.Pose.Scale.X > 0);
        Assert.True(animator.Pose.Scale.Y > 0);
        Assert.True(animator.Pose.Opacity > 0);
        animator.Update(Input with { Dead = true }, duration - beforeEnd);
        Assert.Equal(0, animator.Pose.Opacity);
    }

    [Fact]
    public void Reset_ClearsPhaseMovementHurtAndDirectionHistory()
    {
        var animator = new ActorAnimator(ActorVisualKind.Wolf);
        animator.Update(Input with { Moving = true, Hurt = true, Facing = -Vector2.UnitY }, .04f);
        animator.Reset();
        var fresh = new ActorAnimator(ActorVisualKind.Wolf);
        Assert.Equal(fresh.Pose, animator.Pose);
        var input = Input with { Hurt = true };
        animator.Update(input, .03f);
        fresh.Update(input, .03f);
        Assert.Equal(fresh.Pose, animator.Pose);
    }

    [Fact]
    public void Immunity_IsSlowBoundedBrightnessAndDoesNotHideActor()
    {
        var animator = new ActorAnimator(ActorVisualKind.Player);
        var brightness = new List<float>();
        for (var i = 0; i < 100; i++)
        {
            animator.Update(Input with { Invulnerable = true }, .01f);
            AssertBounded(animator.Pose);
            Assert.InRange(animator.Pose.WhiteMix, 0, .35f);
            Assert.Equal(1, animator.Pose.Opacity);
            if (brightness.Count > 0)
                Assert.InRange(MathF.Abs(animator.Pose.WhiteMix - brightness[^1]), 0, .03f);
            brightness.Add(animator.Pose.WhiteMix);
        }
        Assert.True(brightness.Max() - brightness.Min() > .1f);
        animator.Update(Input, 0);
        Assert.Equal(0, animator.Pose.WhiteMix);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidElapsed_ThrowsWithoutChangingState(float elapsed)
    {
        var animator = new ActorAnimator(ActorVisualKind.Player);
        var before = animator.Pose;
        Assert.Throws<ArgumentOutOfRangeException>(() => animator.Update(Input with { Dead = true }, elapsed));
        Assert.Equal(before, animator.Pose);
        Assert.Equal(ActorAction.Idle, animator.Action);
    }

    [Fact]
    public void NonfiniteInput_ThrowsEvenWhenUnusedOrDeadWithoutMutatingPose()
    {
        var animator = new ActorAnimator(ActorVisualKind.Slime);
        animator.Update(Input with { Dead = true }, .1f);
        var before = animator.Pose;
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => animator.Update(Input with { WindupProgress = value }, .1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => animator.Update(Input with { Facing = new Vector2(value, 0) }, .1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => animator.Update(Input with { Facing = new Vector2(0, value) }, .1f));
        }
        Assert.Equal(before, animator.Pose);
    }

    [Fact]
    public void ExtremeFiniteInput_ProducesFinitePoseAndStableZeroFacingFallback()
    {
        var animator = new ActorAnimator(ActorVisualKind.Player);
        animator.Update(Input with { Moving = true, Facing = new Vector2(float.MaxValue, float.MaxValue) }, float.MaxValue);
        AssertBounded(animator.Pose);
        Assert.InRange(animator.Pose.Facing.Length(), .999f, 1.001f);
        var facing = animator.Pose.Facing;
        animator.Update(Input with { Facing = Vector2.Zero }, 0);
        Assert.Equal(facing, animator.Pose.Facing);
        animator.Reset();
        animator.Update(Input with { Facing = Vector2.Zero }, 0);
        Assert.Equal(Vector2.UnitX, animator.Pose.Facing);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActorAnimator((ActorVisualKind)99));
    }

    private static void AssertBounded(ActorPose pose, bool allowZeroScale = false)
    {
        Assert.InRange(pose.Offset.Length(), 0, 5);
        Assert.InRange(pose.Scale.X, allowZeroScale ? 0 : .01f, 1.5f);
        Assert.InRange(pose.Scale.Y, allowZeroScale ? 0 : .01f, 1.5f);
        Assert.True(float.IsFinite(pose.Rotation));
        Assert.InRange(pose.Opacity, 0, 1);
        Assert.InRange(pose.WhiteMix, 0, 1);
        Assert.InRange(pose.LimbSwing, -1, 1);
        Assert.InRange(pose.AttackProgress, 0, 1);
        Assert.InRange(pose.Facing.Length(), .999f, 1.001f);
    }

    private static void AssertPoseNear(ActorPose expected, ActorPose actual)
    {
        Assert.InRange(Vector2.Distance(expected.Offset, actual.Offset), 0, .00001f);
        Assert.InRange(Vector2.Distance(expected.Scale, actual.Scale), 0, .00001f);
        Assert.Equal(expected.Rotation, actual.Rotation, 5);
        Assert.Equal(expected.Opacity, actual.Opacity, 5);
        Assert.Equal(expected.WhiteMix, actual.WhiteMix, 5);
        Assert.Equal(expected.LimbSwing, actual.LimbSwing, 5);
        Assert.Equal(expected.AttackProgress, actual.AttackProgress, 5);
        Assert.Equal(expected.Facing, actual.Facing);
    }
}
