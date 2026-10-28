using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class HitReactionTests
{
    private static readonly PlayerIntent Idle = new(Vector2.Zero, new Vector2(300, 270), false, false);

    [Theory]
    [InlineData(18f, .15f)]
    [InlineData(10f, .12f)]
    public void Reaction_SplitAndWholeAdvanceDeliverExactlyOneEasedDisplacement(float distance, float duration)
    {
        var reaction = new HitReactionState();
        reaction.Begin(Vector2.UnitX, distance, duration);
        var first = reaction.Advance(duration / 3);
        var displacement = first + reaction.Advance(duration / 3) + reaction.Advance(duration / 3);

        Assert.True(first.X > distance / 3);
        Assert.Equal(distance, displacement.X, 3);
        Assert.Equal(0f, displacement.Y);
        Assert.False(reaction.IsActive);
        Assert.Equal(Vector2.Zero, reaction.Advance(1f));

        reaction.Begin(Vector2.UnitX, distance, duration);
        Assert.Equal(displacement.X, reaction.Advance(duration * 2).X, 3);
    }

    [Fact]
    public void Reaction_RestartDiscardsUnfinishedDisplacementAndClearStopsMotion()
    {
        var reaction = new HitReactionState();
        reaction.Begin(Vector2.UnitX, 18, .15f);
        reaction.Advance(.05f);
        reaction.Begin(Vector2.UnitY, 10, .12f);
        Assert.Equal(new Vector2(0, 10), reaction.Advance(.12f));
        reaction.Begin(Vector2.UnitX, 18, .15f);
        reaction.Clear();
        Assert.False(reaction.IsActive);
        Assert.Equal(Vector2.Zero, reaction.Advance(1));
    }

    [Fact]
    public void Reaction_ZeroTimeIsInertAndInvalidTimesAreRejected()
    {
        var reaction = new HitReactionState();
        reaction.Begin(Vector2.UnitX, 18, .15f);
        Assert.Equal(Vector2.Zero, reaction.Advance(0));
        Assert.Equal(.15f, reaction.RemainingSeconds);
        foreach (var time in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { reaction.Advance(time); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { reaction.Begin(Vector2.UnitX, 18, time); });
        }

        reaction.Begin(Vector2.UnitX, 18, 0);
        Assert.False(reaction.IsActive);
        Assert.Equal(Vector2.Zero, reaction.Advance(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => { reaction.Begin(Vector2.UnitX, float.NaN, .15f); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { reaction.Begin(Vector2.UnitX, -1, .15f); });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(float.NaN, 1)]
    [InlineData(float.PositiveInfinity, 1)]
    [InlineData(float.MaxValue, float.MaxValue)]
    public void Reaction_DirectionsRemainFiniteNormalizedAndHaveStableFallback(float x, float y)
    {
        var reaction = new HitReactionState();
        reaction.Begin(new Vector2(x, y), 18, .15f);
        var delta = reaction.Advance(.15f);
        Assert.True(float.IsFinite(delta.X) && float.IsFinite(delta.Y));
        Assert.Equal(18, delta.Length(), 3);
        if (!float.IsFinite(x) || x == 0)
        {
            Assert.Equal(new Vector2(18, 0), delta);
        }
    }

    [Fact]
    public void Melee_CohortIsHitBeforeMotionAndEachTargetReceivesTenUnitsThenRecovers()
    {
        var arena = Create(
            new("first", "monster.training_slime", 190, 265),
            new("second", "monster.training_slime", 190, 275));
        var origins = arena.Monsters.Select(monster => monster.Position).ToArray();

        arena.Update(Idle with { AttackPressed = true }, .1f);

        Assert.Equal(new[] { 90, 90 }, arena.Monsters.Select(monster => monster.Health));
        Assert.Equal(origins, arena.Monsters.Select(monster => monster.Position));
        Assert.All(arena.Monsters, monster => Assert.Equal(.12f, monster.Reaction.RemainingSeconds));
        Assert.Equal(new[] { CombatEventKind.Swing, CombatEventKind.Damaged, CombatEventKind.Damaged },
            arena.Events.Select(combatEvent => combatEvent.Kind));
        Assert.Equal(Vector2.UnitX, arena.Events[0].Direction);
        var swingDirection = arena.SwingDirection;

        arena.Update(Idle with { AimWorldPosition = new Vector2(160, 0) }, .12f);

        Assert.Empty(arena.Events);
        Assert.Equal(swingDirection, arena.SwingDirection);
        for (var index = 0; index < origins.Length; index++)
        {
            Assert.Equal(10, Vector2.Distance(origins[index], arena.Monsters[index].Position), 3);
            Assert.False(arena.Monsters[index].Reaction.IsActive);
        }

        var recoveredPosition = arena.Monsters[0].Position;
        arena.Update(Idle, .01f);
        Assert.True(Vector2.Distance(arena.Player.Position, arena.Monsters[0].Position) <
            Vector2.Distance(arena.Player.Position, recoveredPosition));
    }

    [Fact]
    public void StunnedMonsterCannotContactOrChaseAndNewReactionDoesNotConsumeHitFrame()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new MonsterSpawnDefinition("slime", "monster.training_slime", 180, 270));
        arena.Update(Idle with { AttackPressed = true }, 1f);
        Assert.Equal(100, arena.Player.Health);
        Assert.Equal(new Vector2(180, 270), arena.Monsters[0].Position);
        Assert.Equal(.12f, arena.Monsters[0].Reaction.RemainingSeconds);

        arena.Update(Idle, .06f);
        Assert.Equal(new Vector2(187.5f, 270), arena.Monsters[0].Position);
        Assert.Equal(100, arena.Player.Health);
        arena.Update(Idle, .16f);
        Assert.Equal(184, arena.Monsters[0].Position.X, 3);
        Assert.Equal(98, arena.Player.Health);
        Assert.Equal(.15f, arena.Player.Reaction.RemainingSeconds);
    }

    [Fact]
    public void Contact_StartsOnePlayerReactionAndImmunityAndKeepsSharedCooldown()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new("first", "monster.training_slime", 160, 270),
            new("second", "monster.training_slime", 160, 270));
        arena.Update(Idle, 0);
        Assert.Equal(98, arena.Player.Health);
        Assert.Equal(.15f, arena.Player.Reaction.RemainingSeconds);
        Assert.Equal(.5f, arena.Player.InvulnerabilityRemaining);
        Assert.True(arena.Player.IsInvulnerable);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.ContactAttack);
        var damaged = Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Damaged);
        Assert.Equal("player", damaged.ActorId);
        Assert.Equal(Vector2.UnitX, damaged.Direction);
        Assert.Equal(2, damaged.Damage);

        arena.Update(Idle, .15f);
        Assert.Equal(new Vector2(178, 270), arena.Player.Position);
        arena.Update(Idle, .35f);
        Assert.False(arena.Player.IsInvulnerable);
        Assert.Equal(98, arena.Player.Health);
        arena.Update(Idle, .3f);
        Assert.Equal(96, arena.Player.Health);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.ContactAttack);
    }

    [Fact]
    public void Arrows_UseFlightDirectionAndSecondSameFrameHitCannotExtendReactionOrImmunity()
    {
        var arena = Create(
            new("first", "monster.skeleton_archer", 250, 270),
            new("second", "monster.skeleton_archer", 250, 270));
        arena.Update(Idle, .3f);
        Assert.Equal(2, arena.Events.Count(item => item.Kind == CombatEventKind.ArrowLaunched));
        arena.Update(Idle, .5f);

        Assert.Equal(95, arena.Player.Health);
        Assert.Empty(arena.Projectiles);
        Assert.Equal(.15f, arena.Player.Reaction.RemainingSeconds);
        Assert.Equal(.5f, arena.Player.InvulnerabilityRemaining);
        Assert.Equal(-Vector2.UnitX,
            Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Damaged).Direction);

        arena.Update(Idle, .15f);
        Assert.Equal(new Vector2(142, 270), arena.Player.Position);
        arena.Update(Idle, .35f);
        Assert.False(arena.Player.IsInvulnerable);
        arena.Update(Idle, .5f);
        Assert.Equal(2, arena.Projectiles.Count);
        arena.Update(Idle, .5f);
        Assert.Equal(90, arena.Player.Health);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Damaged);
    }

    [Fact]
    public void ArrowAndContactShareImmunityWithoutSpendingIgnoredContactCooldown()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new("archer", "monster.skeleton_archer", 250, 270),
            new("slime", "monster.training_slime", 220, 270));
        arena.Update(Idle, .3f);
        arena.Update(Idle, .5f);
        Assert.Equal(95, arena.Player.Health);
        Assert.DoesNotContain(arena.Events, item => item.Kind == CombatEventKind.ContactAttack);
        arena.Update(Idle, .5f);
        Assert.Equal(93, arena.Player.Health);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.ContactAttack);
    }

    [Fact]
    public void LaterArrowDuringImmunityIsRemovedWithoutRefreshingExistingStunOrKnockback()
    {
        var arena = Create(
            new("first", "monster.skeleton_archer", 250, 270),
            new("second", "monster.skeleton_archer", 258, 270));
        arena.Update(Idle, .3f);
        arena.Update(Idle, .32f);
        Assert.Equal(95, arena.Player.Health);
        Assert.Single(arena.Projectiles);
        arena.Update(Idle, .1f);
        Assert.Empty(arena.Projectiles);
        Assert.Equal(95, arena.Player.Health);
        Assert.Equal(.05f, arena.Player.Reaction.RemainingSeconds, 5);
        Assert.Equal(.4f, arena.Player.InvulnerabilityRemaining, 5);
        Assert.Empty(arena.Events);
        arena.Update(Idle, .05f);
        Assert.Equal(new Vector2(142, 270), arena.Player.Position);
        Assert.False(arena.Player.Reaction.IsActive);
    }

    [Fact]
    public void ImmunityExpiresAtHalfASecondOfSplitUpdatesAndPermitsAnArrowThatFrame()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new("contact", "monster.training_slime", 160, 270),
            new("archer", "monster.skeleton_archer", 240, 270));
        arena.Update(Idle, 0);
        for (var index = 0; index < 4; index++)
        {
            arena.Update(Idle, .1f);
        }

        Assert.Equal(98, arena.Player.Health);
        Assert.Single(arena.Projectiles);
        Assert.True(arena.Player.IsInvulnerable);
        arena.Update(Idle, .1f);
        Assert.Equal(93, arena.Player.Health);
        Assert.Empty(arena.Projectiles);
        Assert.Equal(.5f, arena.Player.InvulnerabilityRemaining);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Damaged);
    }

    [Fact]
    public void ZeroDamageDoesNotStartReactionImmunityOrDamageEvents()
    {
        var content = MixedEncounterFixture.CreateContent();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.skeleton_archer"] = monsters["monster.skeleton_archer"] with
        {
            RangedAttack = monsters["monster.skeleton_archer"].RangedAttack! with { Damage = 0 }
        };
        var arena = CreateWithContent(new GameContent(content.Items, monsters, content.LootTables, content.Affixes),
            new("archer", "monster.skeleton_archer", 250, 270),
            new MonsterSpawnDefinition("slime", "monster.training_slime", 160, 270));
        arena.Update(Idle, .3f);
        arena.Update(Idle, .5f);
        Assert.Equal(100, arena.Player.Health);
        Assert.False(arena.Player.Reaction.IsActive);
        Assert.False(arena.Player.IsInvulnerable);
        Assert.Empty(arena.Projectiles);
        Assert.DoesNotContain(arena.Events, item => item.Kind is CombatEventKind.Damaged or CombatEventKind.ContactAttack);
    }

    [Fact]
    public void Stun_BlocksMovementAndAttackOnlyForCoveredTimeAndDoesNotCatchUpSwings()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new MonsterSpawnDefinition("slime", "monster.training_slime", 160, 270));
        arena.Update(Idle, 0);
        arena.Update(Idle with { MoveDirection = Vector2.UnitY, AttackPressed = true }, .1f);
        Assert.Equal(270, arena.Player.Position.Y);
        Assert.Equal(0, arena.Player.AttackSequence);
        arena.Update(Idle with { MoveDirection = Vector2.UnitY, AttackPressed = true }, .1f);
        Assert.Equal(279, arena.Player.Position.Y, 3);
        Assert.Equal(1, arena.Player.AttackSequence);
        arena.Update(Idle with { AttackPressed = true }, 10f);
        Assert.Equal(2, arena.Player.AttackSequence);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Swing);
    }

    [Fact]
    public void Stun_BlocksPickupUntilRecovery()
    {
        var content = MixedEncounterFixture.CreateContent(contactDamage: 2);
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.fast_wolf"] = monsters["monster.fast_wolf"] with { MaxHealth = 10, MoveSpeed = .01f };
        var loot = new Dictionary<string, LootTableDefinition>
        {
            ["loot.training_empty"] = new("loot.training_empty", 1, 1,
                [new LootEntryDefinition("weapon.training_blade", 1) { Guaranteed = true }])
        };
        var arena = CreateWithContent(new GameContent(content.Items, monsters, loot, content.Affixes),
            new("loot", "monster.fast_wolf", 175, 270),
            new("contact", "monster.training_slime", 160, 270));
        arena.Update(Idle with { AttackPressed = true }, 0);
        // The coincident slime was also hit; let it recover before its first contact.
        arena.Update(Idle, .12f);
        Assert.True(arena.Player.Reaction.IsActive);
        arena.Update(Idle with { PickupPressed = true }, .05f);
        Assert.Single(arena.GroundItems);
        Assert.Empty(arena.Player.Inventory.Items);
        arena.Update(Idle with { PickupPressed = true }, .1f);
        Assert.Equal(new Vector2(142, 270), arena.Player.Position);
        Assert.Empty(arena.GroundItems);
        Assert.Single(arena.Player.Inventory.Items);
    }

    [Fact]
    public void AttackCooldownContinuesDuringStunAndHeldAttackResumesWithoutCatchup()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new("target", "monster.training_slime", 190, 270),
            new("contact", "monster.training_slime", 140, 270));
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.Equal(1, arena.Player.AttackSequence);
        Assert.True(arena.Player.Reaction.IsActive);
        arena.Update(Idle with { AttackPressed = true }, .1f);
        Assert.Equal(1, arena.Player.AttackSequence);
        Assert.DoesNotContain(arena.Events, item => item.Kind == CombatEventKind.Swing);
        arena.Update(Idle with { AttackPressed = true }, .25f);
        Assert.Equal(2, arena.Player.AttackSequence);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Swing);
    }

    [Fact]
    public void Archer_InterruptedWindupRequiresFullRecoveryWindupAndKeepsLaunchCadenceAndArrows()
    {
        var arena = Create(new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 190, 270));
        arena.Update(Idle, .3f);
        Assert.Single(arena.Projectiles);
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.Single(arena.Projectiles);
        Assert.False(arena.Monsters[0].IsWindingUp);
        arena.Update(Idle, .12f);
        arena.Update(Idle, 1.07f);
        Assert.Empty(arena.Projectiles);
        arena.Update(Idle, .01f);
        Assert.True(arena.Monsters[0].IsWindingUp);
        arena.Update(Idle, .299f);
        Assert.Empty(arena.Projectiles);
        arena.Update(Idle, .001f);
        Assert.Single(arena.Projectiles);

    }

    [Fact]
    public void Archer_PendingShotAfterPreviousLaunchIsCancelledAndRestartsFullWindup()
    {
        var arena = Create(new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 190, 270));
        arena.Update(Idle, .3f);
        arena.Update(Idle, .15f);
        arena.Update(Idle, .15f);
        arena.Update(Idle with { MoveDirection = Vector2.UnitX }, .25f);
        Assert.Equal(new Vector2(187, 270), arena.Player.Position);
        arena.Update(Idle, .65f);
        Assert.True(arena.Monsters[0].IsWindingUp);
        Assert.False(arena.Player.Reaction.IsActive);
        Assert.InRange(Vector2.Distance(arena.Player.Position, arena.Monsters[0].Position), 0, 48);
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.False(arena.Monsters[0].IsWindingUp);
        arena.Update(Idle, .12f);
        Assert.True(arena.Monsters[0].IsWindingUp);
        Assert.Equal(.3f, arena.Monsters[0].WindupRemaining, 4);
        arena.Update(Idle, .299f);
        Assert.DoesNotContain(arena.Events, item => item.Kind == CombatEventKind.ArrowLaunched);
        arena.Update(Idle, .001f);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.ArrowLaunched);
    }

    [Fact]
    public void InitialArcherWindupIsCancelledByHitAndCannotAdvanceWhileStunned()
    {
        var arena = Create(new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 190, 270));
        arena.Update(Idle, .2f);
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.False(arena.Monsters[0].IsWindingUp);
        arena.Update(Idle, .06f);
        Assert.False(arena.Monsters[0].IsWindingUp);
        Assert.Empty(arena.Projectiles);
        arena.Update(Idle, .06f);
        Assert.Equal(.3f, arena.Monsters[0].WindupRemaining);
        arena.Update(Idle, .299f);
        Assert.Empty(arena.Projectiles);
        arena.Update(Idle, .001f);
        Assert.Single(arena.Projectiles);
    }

    [Fact]
    public void Reaction_ClampsActualEntityPositionAtArenaBoundary()
    {
        var arena = Create(new MonsterSpawnDefinition("slime", "monster.training_slime", 959, 270));
        arena.Update(Idle with { MoveDirection = Vector2.UnitX }, 4.4f);
        Assert.Equal(952, arena.Player.Position.X, 3);
        arena.Update(Idle with { AttackPressed = true, AimWorldPosition = new Vector2(960, 270) }, 0);
        arena.Update(Idle, .12f);
        Assert.Equal(960, arena.Monsters[0].Position.X);

        var arrows = Create(new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 30, 270));
        arrows.Update(Idle with { MoveDirection = -Vector2.UnitX }, 1f);
        arrows.Update(Idle, .1f);
        Assert.True(arrows.Player.Reaction.IsActive);
        arrows.Update(Idle, .15f);
        Assert.Equal(0, arrows.Player.Position.X);
    }

    [Fact]
    public void Reset_ClearsReactionsImmunityEventsAndFeedbackWithoutHealing()
    {
        var arena = CreateWithContent(MixedEncounterFixture.CreateContent(contactDamage: 2),
            new("contact", "monster.training_slime", 160, 270),
            new("target", "monster.training_slime", 190, 270));
        arena.Update(Idle, 0);
        arena.Update(Idle, .15f);
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.True(arena.Monsters[1].Reaction.IsActive);
        var position = arena.Player.Position;
        arena.ResetEncounter();
        Assert.Equal(position, arena.Player.Position);
        Assert.Equal(98, arena.Player.Health);
        Assert.False(arena.Player.Reaction.IsActive);
        Assert.False(arena.Player.IsInvulnerable);
        Assert.All(arena.Monsters, monster => Assert.False(monster.Reaction.IsActive));
        Assert.Empty(arena.Events);
    }

    [Fact]
    public void LethalHits_EmitDeathOnceAtDeathPositionWithoutReactionAndStillRespawnImmediately()
    {
        var content = MixedEncounterFixture.CreateContent(contactDamage: 100);
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.fast_wolf"] = monsters["monster.fast_wolf"] with { MaxHealth = 10 };
        var arena = CreateWithContent(new GameContent(content.Items, monsters, content.LootTables, content.Affixes),
            new("target", "monster.fast_wolf", 180, 270),
            new("contact", "monster.training_slime", 140, 270));
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.Equal(100, arena.Player.Health);
        Assert.Equal(1, arena.ResetSequence);
        Assert.Equal(new[] { "target", "player" },
            arena.Events.Where(item => item.Kind == CombatEventKind.Died).Select(item => item.ActorId));
        Assert.Equal(new Vector2(180, 270), arena.Events.First(item => item.Kind == CombatEventKind.Died).Position);
        Assert.False(arena.Player.Reaction.IsActive);
        Assert.All(arena.Monsters, monster => Assert.False(monster.Reaction.IsActive));
        arena.Update(Idle with { MoveDirection = Vector2.UnitY }, 1);
        Assert.DoesNotContain(arena.Events, item => item.Kind == CombatEventKind.Died);
    }

    [Fact]
    public void LethalMonsterHitDoesNotKnockBackAndDeathEventDoesNotRepeat()
    {
        var content = MixedEncounterFixture.CreateContent();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with { MaxHealth = 10 };
        var arena = CreateWithContent(new GameContent(content.Items, monsters, content.LootTables, content.Affixes),
            new MonsterSpawnDefinition("target", "monster.training_slime", 180, 270));
        arena.Update(Idle with { AttackPressed = true }, 0);
        Assert.True(arena.IsCleared);
        Assert.Single(arena.Events, item => item.Kind == CombatEventKind.Died);
        Assert.Equal(new Vector2(180, 270), arena.Monsters[0].Position);
        Assert.False(arena.Monsters[0].Reaction.IsActive);
        arena.Update(Idle with { AttackPressed = true }, .35f);
        Assert.Empty(arena.Events);
        Assert.Equal(new Vector2(180, 270), arena.Monsters[0].Position);
    }

    [Fact]
    public void ProjectileCallbackReceivesFlightSourceAndStopDoesNotClearUnprocessedArrows()
    {
        var projectiles = new ProjectileSystem();
        var arrow = new ProjectileState("first", new Vector2(150, 270), Vector2.UnitX, 240, 7);
        projectiles.Spawn(arrow);
        projectiles.Spawn(arrow with { SourceInstanceId = "second" });
        var received = new List<ProjectileState>();
        Assert.True(projectiles.Update(0, new Vector2(160, 270), projectile =>
        {
            received.Add(projectile);
            return true;
        }));
        Assert.Equal(arrow, Assert.Single(received));
        Assert.Equal("second", Assert.Single(projectiles.Projectiles).SourceInstanceId);
    }

    [Fact]
    public void EncounterCannotReuseReservedPlayerEventIdentity()
    {
        Assert.Throws<ArgumentException>(() =>
            Create(new MonsterSpawnDefinition("player", "monster.training_slime", 190, 270)));
    }

    private static ArenaSimulation Create(params MonsterSpawnDefinition[] spawns) =>
        CreateWithContent(MixedEncounterFixture.CreateContent(), spawns);

    private static ArenaSimulation CreateWithContent(GameContent content, params MonsterSpawnDefinition[] spawns) =>
        new(content, new Random(17), MixedEncounterFixture.CreateEncounter(spawns));
}
