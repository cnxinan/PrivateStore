using System.Numerics;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Tests.Gameplay;

namespace DarkLoot.Tests.Game;

public sealed class ActorPresentationTests
{
    private static PlayerIntent Idle => new(Vector2.Zero, new Vector2(300, 270), false, false);

    [Fact]
    public void Swing_IsConsumedOnceLocksDirectionAndDoesNotReplayOnFollowingUpdate()
    {
        var arena = CreateArena();
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, .02f);
        presentation.Update(arena, .02f);
        Assert.True(presentation.PlayerPose.AttackProgress > 0);
        Assert.Single(presentation.HitTexts);
        presentation.Update(arena, .02f);
        Assert.Single(presentation.HitTexts);
        Assert.True(presentation.PlayerPose.AttackProgress > .2f);
        arena.Update(Idle with { AimWorldPosition = new Vector2(160, 0) }, .02f);
        presentation.Update(arena, .02f);
        Assert.Equal(Vector2.UnitX, presentation.PlayerPose.Facing);
        arena.Update(Idle, .2f);
        presentation.Update(arena, .2f);
        Assert.Equal(0, presentation.PlayerPose.AttackProgress);
        Assert.Equal(0, presentation.SwingRemaining);
        Assert.Empty(presentation.HitTexts);
    }

    [Fact]
    public void PerInstanceHitAndKnockback_DoNotAnimateUntouchedPeerOrCountAsWalking()
    {
        var arena = CreateArena();
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, 0);
        presentation.Update(arena, 0);
        Assert.True(presentation.GetMonsterPose("near").WhiteMix > 0);
        Assert.Equal(0, presentation.GetMonsterPose("far").WhiteMix);
        Assert.NotEqual(presentation.GetMonsterPose("near"), presentation.GetMonsterPose("far"));
        // A newly accepted contact pushes the player, with no voluntary movement.
        var contact = CreateArena(contactDamage: 10, nearX: 140);
        presentation.Reset(contact);
        contact.Update(Idle, 0);
        presentation.Update(contact, 0);
        contact.Update(Idle, .15f);
        presentation.Update(contact, .15f);
        Assert.NotEqual(new Vector2(160, 270), contact.Player.Position);
        Assert.Equal(0, presentation.PlayerPose.LimbSwing);
        Assert.Equal(Vector2.Zero, presentation.PlayerPose.Offset);
    }

    [Fact]
    public void NewDeath_RendersAtFullSizeThenFadesWithoutChangingLootOrAnchor()
    {
        var arena = CreateArena(health: 10);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, .1f);
        var deathPosition = arena.Monsters[0].Position;
        presentation.Update(arena, .1f);
        Assert.Equal(1, presentation.GetMonsterPose("near").Opacity);
        Assert.Equal(Vector2.One, presentation.GetMonsterPose("near").Scale);
        arena.Update(Idle, .15f);
        presentation.Update(arena, .15f);
        Assert.InRange(presentation.GetMonsterPose("near").Opacity, .49f, .51f);
        Assert.Equal(1, presentation.GetMonsterPose("far").Opacity);
        Assert.Equal(deathPosition, arena.Monsters[0].Position);
        arena.Update(Idle, .15f);
        presentation.Update(arena, .15f);
        Assert.Equal(0, presentation.GetMonsterPose("near").Opacity);
        Assert.Empty(arena.GroundItems);
    }

    [Fact]
    public void PausedReset_ClearsOldEventsAndPosesWithoutAdvancingPhase()
    {
        var arena = CreateArena();
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { MoveDirection = Vector2.UnitY, AttackPressed = true }, .05f);
        presentation.Update(arena, .05f);
        Assert.NotEqual(Vector2.Zero, presentation.PlayerPose.Offset);
        presentation.Reset(arena);
        Assert.Equal(Vector2.Zero, presentation.PlayerPose.Offset);
        Assert.Equal(0, presentation.PlayerPose.AttackProgress);
        Assert.Empty(presentation.HitTexts);
        presentation.Update(arena, 0);
        Assert.Equal(0, presentation.PlayerPose.AttackProgress);
        arena.ResetEncounter();
        presentation.Update(arena, .1f);
        Assert.Equal(Vector2.Zero, presentation.PlayerPose.Offset);
        Assert.Equal(Vector2.One, presentation.GetMonsterPose("near").Scale);
        Assert.Empty(presentation.HitTexts);
    }

    [Fact]
    public void PlayerDeath_CancelsAcceptedSwingThenRespawnClearsAllOldPresentation()
    {
        var arena = CreateArena(contactDamage: 100, nearX: 140);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, .1f);
        presentation.Update(arena, .1f);
        Assert.True(arena.Player.IsDead);
        Assert.Equal(1, presentation.PlayerPose.Opacity);
        Assert.Equal(0, presentation.PlayerPose.AttackProgress);
        Assert.Equal(0, presentation.SwingRemaining);
        var anchor = arena.Player.Position;
        arena.Update(Idle, .3f);
        presentation.Update(arena, .3f);
        Assert.InRange(presentation.PlayerPose.Opacity, .49f, .51f);
        Assert.True(presentation.PlayerPose.Rotation > 0);
        Assert.Equal(anchor, arena.Player.Position);
        arena.Update(Idle, .3f);
        presentation.Update(arena, .3f);
        Assert.False(arena.Player.IsDead);
        Assert.Equal(1, presentation.PlayerPose.Opacity);
        Assert.Equal(0, presentation.PlayerPose.Rotation);
        Assert.Equal(0, presentation.PlayerPose.AttackProgress);
        Assert.Empty(presentation.HitTexts);
    }

    [Fact]
    public void FreshSwingAcceptedByGameplayOnRecovery_IsPresented()
    {
        var arena = CreateArena(contactDamage: 10, nearX: 140);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle, 0);
        presentation.Update(arena, 0);
        arena.Update(Idle with { AttackPressed = true }, .15f);
        presentation.Update(arena, .15f);
        Assert.Contains(arena.Events, item => item.Kind == CombatEventKind.Swing);
        Assert.False(arena.Player.Reaction.IsActive);
        Assert.True(presentation.PlayerPose.AttackProgress > 0);
    }

    [Fact]
    public void InvalidElapsed_DoesNotChangePresentation()
    {
        var arena = CreateArena();
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        var before = presentation.PlayerPose;
        foreach (var elapsed in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => presentation.Update(arena, elapsed));
            Assert.Equal(before, presentation.PlayerPose);
        }
    }

    private static ArenaSimulation CreateArena(int health = 100, int contactDamage = 0, float nearX = 190)
    {
        var fixture = MixedEncounterFixture.CreateContent(contactDamage);
        var definitions = fixture.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MaxHealth = health,
            MoveSpeed = .01f
        });
        return new ArenaSimulation(fixture with { Monsters = definitions }, new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("near", "monster.training_slime", nearX, 270),
                new MonsterSpawnDefinition("far", "monster.training_slime", 700, 270)));
    }
}
