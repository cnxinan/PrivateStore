using System.Numerics;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
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
        var resource = Assert.Single(arena.GroundResources);
        Assert.Equal(deathPosition, resource.Position);
        Assert.Equal("material.slime", resource.Stack.DefinitionId);
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

    [Fact]
    public void Cultist_CastPoseUsesItsFullWindupRatherThanArcherTiming()
    {
        var content = DarkLoot.Data.GameContentLoader.LoadDirectory(Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content", "definitions")));
        var arena = new ArenaSimulation(content, new Random(61),
            new("encounter.cast", [new("mage", "monster.cultist_mage", 300, 270)]));
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle, .15f);
        presentation.Update(arena, .15f);
        var casting = presentation.GetMonsterPose("mage");
        Assert.True(arena.Monsters[0].IsWindingUp);
        Assert.InRange(casting.AttackProgress, .249f, .251f);
        Assert.True(casting.LimbSwing > 0);
        arena.Update(Idle, .15f);
        presentation.Update(arena, .15f);
        Assert.InRange(presentation.GetMonsterPose("mage").AttackProgress, .499f, .501f);
        Assert.Equal(casting.Facing, presentation.GetMonsterPose("mage").Facing);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void Miss_CreatesOnlyDirectionalPunchFromAcceptedSwing(float x, float y)
    {
        var arena = CreateArena(nearX: 700);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        var direction = new Vector2(x, y);
        arena.Update(Idle with
        {
            AttackPressed = true,
            AimWorldPosition = arena.Player.Position + direction * 100
        }, .1f);
        presentation.Update(arena, .1f);
        var effect = Assert.Single(presentation.Effects);
        Assert.Equal(CombatEffectKind.Punch, effect.Kind);
        Assert.Equal(direction, effect.Direction);
        Assert.Equal(arena.Events.Single(item => item.Kind == CombatEventKind.Swing).Position, effect.Position);
        Assert.Equal(0, effect.Age);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WeaponHit_UsesRealVictimPositionAndCriticalResult(bool critical)
    {
        var arena = CreateArena(random: new CriticalRandom(critical));
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
            [new AffixRoll("affix.test_crit", ItemStat.CriticalChance, 50)]);
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, .1f);
        presentation.Update(arena, .1f);
        Assert.Equal(2, presentation.Effects.Count);
        Assert.Contains(presentation.Effects, item => item.Kind == CombatEffectKind.Slash);
        var hit = arena.Events.Single(item => item.Kind == CombatEventKind.Damaged);
        var impact = presentation.Effects.Single(item => item.ActorId == "near");
        Assert.Equal(critical, hit.WasCritical);
        Assert.Equal(critical ? CombatEffectKind.Critical : CombatEffectKind.Hit, impact.Kind);
        Assert.Equal(hit.Position, impact.Position);
        Assert.Equal(hit.Direction, impact.Direction);
    }

    [Fact]
    public void Effects_DoNotReplayButEqualValuedLaterSwingIsNewAndTheyExpire()
    {
        var arena = CreateArena(nearX: 700);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, 0);
        presentation.Update(arena, 0);
        var first = Assert.Single(presentation.Effects);
        var firstEvent = Assert.Single(arena.Events);
        presentation.Update(arena, 0);
        Assert.Equal(first, Assert.Single(presentation.Effects));
        arena.Update(Idle with { AttackPressed = true }, .4f);
        Assert.Equal(firstEvent, Assert.Single(arena.Events));
        Assert.NotSame(firstEvent, arena.Events[0]);
        presentation.Update(arena, .4f);
        Assert.Equal(0, Assert.Single(presentation.Effects).Age);
        arena.Update(Idle, .4f);
        presentation.Update(arena, .4f);
        Assert.Empty(presentation.Effects);
    }

    [Fact]
    public void Effects_ResetAndSceneChangeClearOldBatchWhileDeathKeepsFinalImpact()
    {
        var arena = CreateArena(health: 10);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle with { AttackPressed = true }, .1f);
        presentation.Update(arena, .1f);
        Assert.True(arena.Monsters[0].IsDefeated);
        Assert.Contains(presentation.Effects, item => item.ActorId == "near");
        var frozen = presentation.Effects.ToArray();
        presentation.Update(arena, 0);
        Assert.Equal(frozen, presentation.Effects);
        presentation.Reset(arena);
        presentation.Update(arena, 0);
        Assert.Empty(presentation.Effects);
        var other = CreateArena();
        other.Update(Idle with { AttackPressed = true }, 0);
        presentation.Update(other, 0);
        Assert.Empty(presentation.Effects);
        other.ResetEncounter();
        presentation.Update(other, .1f);
        Assert.Empty(presentation.Effects);
    }

    [Fact]
    public void Effects_DoNotConsumeCombatRandomAndIgnoreIncomingPlayerDamage()
    {
        var random = new CriticalRandom(false);
        var arena = CreateArena(contactDamage: 10, nearX: 140, random: random);
        var presentation = new ActorPresentation();
        presentation.Reset(arena);
        arena.Update(Idle, 0);
        Assert.Contains(arena.Events, item => item.Kind == CombatEventKind.Damaged && item.ActorId == "player");
        var before = random.Calls;
        presentation.Update(arena, .1f);
        Assert.Empty(presentation.Effects);
        Assert.Equal(before, random.Calls);
        arena.Update(Idle with { AttackPressed = true }, .2f);
        before = random.Calls;
        presentation.Update(arena, .2f);
        Assert.NotEmpty(presentation.Effects);
        presentation.Update(arena, 0);
        Assert.Equal(before, random.Calls);
    }

    private sealed class CriticalRandom(bool critical) : Random
    {
        public int Calls { get; private set; }
        public override int Next(int maxValue)
        {
            Calls++;
            return critical ? 0 : maxValue - 1;
        }
    }

    private static ArenaSimulation CreateArena(int health = 100, int contactDamage = 0, float nearX = 190,
        Random? random = null)
    {
        var fixture = MixedEncounterFixture.CreateContent(contactDamage);
        var definitions = fixture.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MaxHealth = health,
            MoveSpeed = .01f
        });
        return new ArenaSimulation(fixture with { Monsters = definitions }, random ?? new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("near", "monster.training_slime", nearX, 270),
                new MonsterSpawnDefinition("far", "monster.training_slime", 700, 270)));
    }
}
