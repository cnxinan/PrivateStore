using System.Numerics;
using DarkLoot.Engine.Animation;

namespace DarkLoot.Tests.Engine;

public sealed class ActorFrameTests
{
    private static ActorAnimationInput Input(bool moving = false, bool hurt = false, bool dead = false,
        bool winding = false, float windup = 0, bool attack = false) =>
        new(moving, hurt, dead, false, winding, windup, attack, Vector2.UnitX);

    [Fact]
    public void Frame_LoopsLivingAnimationButFreezesWithZeroElapsedAndResets()
    {
        var animator = new ActorAnimator(ActorVisualKind.Player);
        animator.Update(Input(moving: true), .25f);
        Assert.Equal(new ActorFrame(ActorAction.Move, .5f), animator.Frame);
        animator.Update(Input(moving: true), 0);
        Assert.Equal(.5f, animator.Frame.Progress);
        animator.Update(Input(moving: true), .25f);
        Assert.Equal(0, animator.Frame.Progress);
        animator.Reset();
        Assert.Equal(new ActorFrame(ActorAction.Idle, 0), animator.Frame);
    }

    [Fact]
    public void Frame_FollowsLockedWindupAndRealHitAndDeathDurations()
    {
        var archer = new ActorAnimator(ActorVisualKind.Archer);
        archer.Update(Input(winding: true, windup: .75f), 0);
        Assert.Equal(new ActorFrame(ActorAction.Attack, .75f), archer.Frame);
        archer.Update(Input(hurt: true), 0);
        Assert.Equal(new ActorFrame(ActorAction.Hurt, 0), archer.Frame);
        archer.Update(Input(hurt: true), .06f);
        Assert.Equal(.5f, archer.Frame.Progress, 5);
        archer.Update(Input(dead: true), 0);
        Assert.Equal(new ActorFrame(ActorAction.Dead, 0), archer.Frame);
        archer.Update(Input(dead: true), .15f);
        Assert.Equal(.5f, archer.Frame.Progress, 5);
        archer.Update(Input(dead: true), 1);
        Assert.Equal(new ActorFrame(ActorAction.Dead, 1), archer.Frame);
    }
}
