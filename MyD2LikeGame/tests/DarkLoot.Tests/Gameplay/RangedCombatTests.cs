using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Tests.Gameplay;

public sealed class RangedCombatTests
{
    [Fact]
    public void Update_ArcherLocksAimDuringWindupAndLaunchesAfterThreeTenths()
    {
        var archer = CreateArcher(new Vector2(250, 270));
        var behavior = new MonsterBehaviorSystem();
        var launched = new List<ProjectileState>();
        var playerPosition = new Vector2(160, 270);

        behavior.Update(archer, playerPosition, 0, launched.Add);

        Assert.True(archer.IsWindingUp);
        Assert.Equal(Vector2.Normalize(playerPosition - archer.Position), archer.LockedAimDirection);
        Assert.Empty(launched);

        behavior.Update(archer, new Vector2(160, 360), .15f, launched.Add);

        Assert.Equal(Vector2.Normalize(playerPosition - new Vector2(250, 270)), archer.LockedAimDirection);
        Assert.Empty(launched);

        behavior.Update(archer, new Vector2(160, 360), .149f, launched.Add);
        Assert.Empty(launched);

        behavior.Update(archer, new Vector2(160, 360), .001f, launched.Add);

        var projectile = Assert.Single(launched);
        Assert.Equal("archer", projectile.SourceInstanceId);
        Assert.Equal(Vector2.Normalize(playerPosition - new Vector2(250, 270)), projectile.Direction);
        Assert.False(archer.IsWindingUp);
    }

    [Fact]
    public void Update_ArcherLaunchesAtLeastOneAndAHalfSecondsApart()
    {
        var archer = CreateArcher(new Vector2(250, 270));
        var behavior = new MonsterBehaviorSystem();
        var launchTimes = new List<float>();
        var elapsed = 0f;
        void Update(float delta)
        {
            elapsed += delta;
            behavior.Update(archer, new Vector2(160, 270), delta, _ => launchTimes.Add(elapsed));
        }

        Update(0);
        Update(.3f);
        Update(1.49f);
        Assert.Single(launchTimes);
        Update(.01f);

        Assert.Equal(2, launchTimes.Count);
        Assert.True(launchTimes[1] - launchTimes[0] >= 1.5f);
    }

    [Fact]
    public void Update_CoincidentArcherAimLaunchesWithFiniteUnitXDirection()
    {
        var archer = CreateArcher(new Vector2(250, 270));
        var behavior = new MonsterBehaviorSystem();
        var launched = new List<ProjectileState>();

        behavior.Update(archer, archer.Position, .3f, launched.Add);

        Assert.Equal(Vector2.UnitX, Assert.Single(launched).Direction);
    }

    [Fact]
    public void Update_ArcherApproachesOutsideRangeAndFinishesWindupAfterTargetLeavesRange()
    {
        var approaching = CreateArcher(new Vector2(500, 270));
        var behavior = new MonsterBehaviorSystem();
        var launched = new List<ProjectileState>();

        behavior.Update(approaching, new Vector2(160, 270), 1f, launched.Add);
        Assert.Equal(new Vector2(430, 270), approaching.Position);

        var winding = CreateArcher(new Vector2(250, 270));
        behavior.Update(winding, new Vector2(160, 270), 0, launched.Add);
        behavior.Update(winding, new Vector2(600, 270), .3f, launched.Add);

        Assert.Contains(launched, projectile => projectile.SourceInstanceId == "archer");
        Assert.Equal(Vector2.UnitX * -1, Assert.Single(launched).Direction);
    }

    [Fact]
    public void Update_SweptProjectileHitsPlayerBetweenItsEndpoints()
    {
        var projectiles = new ProjectileSystem();
        projectiles.Spawn(new ProjectileState("archer", new Vector2(0, 270), Vector2.UnitX, 240, 5));
        var appliedDamage = new List<int>();

        var playerDied = projectiles.Update(
            1f,
            new Vector2(160, 270),
            damage =>
            {
                appliedDamage.Add(damage);
                return false;
            });

        Assert.False(playerDied);
        Assert.Equal(new[] { 5 }, appliedDamage);
        Assert.Empty(projectiles.Projectiles);
    }

    [Fact]
    public void Update_ProjectileMissesDifferentLaneAndRemovesAfterBoundsExit()
    {
        var projectiles = new ProjectileSystem();
        projectiles.Spawn(new ProjectileState("archer", new Vector2(0, 300), Vector2.UnitX, 240, 5));
        var appliedDamage = 0;

        projectiles.Update(1f, new Vector2(160, 270), damage =>
        {
            appliedDamage += damage;
            return false;
        });

        Assert.Equal(0, appliedDamage);
        Assert.Single(projectiles.Projectiles);

        projectiles.Clear();
        projectiles.Spawn(new ProjectileState("archer", new Vector2(950, 200), Vector2.UnitX, 240, 5));
        projectiles.Update(.1f, new Vector2(960, 400), _ => false);

        Assert.Empty(projectiles.Projectiles);
    }

    [Fact]
    public void Update_ClipsMovementToArenaBeforeTestingBoundaryHitAndChecksZeroLength()
    {
        var projectiles = new ProjectileSystem();
        projectiles.Spawn(new ProjectileState("edge", new Vector2(950, 270), Vector2.UnitX, 240, 5));
        var damage = 0;

        projectiles.Update(1f, new Vector2(960, 270), amount =>
        {
            damage += amount;
            return false;
        });

        Assert.Equal(5, damage);

        projectiles.Spawn(new ProjectileState("nearby", new Vector2(150, 270), Vector2.UnitX, 240, 5));
        projectiles.Update(0, new Vector2(160, 270), amount =>
        {
            damage += amount;
            return false;
        });

        Assert.Equal(10, damage);
        Assert.Empty(projectiles.Projectiles);
    }

    [Fact]
    public void Update_LaunchedArrowSurvivesShooterDeath()
    {
        var content = WithArcherHealth(10);
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 210, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .3f);
        Assert.Single(arena.Projectiles);

        arena.Update(
            new PlayerIntent(new Vector2(1, 1), new Vector2(210, 270), true, false),
            20f / (180f * MathF.Sqrt(2)));

        Assert.True(Assert.Single(arena.Monsters).IsDefeated);
        Assert.Equal("archer", Assert.Single(arena.Projectiles).SourceInstanceId);
    }

    [Fact]
    public void Update_TwoArrowsCauseOneAtomicDeathAndResetClearsStaleDamage()
    {
        var content = WithArcherDamage(50);
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("archer.1", "monster.skeleton_archer", 250, 270),
                new MonsterSpawnDefinition("archer.2", "monster.skeleton_archer", 250, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .3f);
        Assert.Equal(2, arena.Projectiles.Count);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .5f);

        Assert.Equal(100, arena.Player.Health);
        Assert.All(arena.Monsters, monster => Assert.Equal(monster.MaxHealth, monster.Health));
        Assert.Empty(arena.Projectiles);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), 0);

        Assert.Equal(100, arena.Player.Health);
        Assert.Empty(arena.Projectiles);
    }

    [Fact]
    public void Update_DeadArcherCannotFinishWindup()
    {
        var arena = new ArenaSimulation(
            WithArcherHealth(10),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 190, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(160, 270), false, false), 0);
        Assert.True(Assert.Single(arena.Monsters).IsWindingUp);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);

        var deadArcher = Assert.Single(arena.Monsters);
        Assert.True(deadArcher.IsDefeated);
        Assert.False(deadArcher.IsWindingUp);
        Assert.Empty(arena.Projectiles);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .3f);

        Assert.Empty(arena.Projectiles);
    }

    [Fact]
    public void Update_ArrowDamageDoesNotResetOrConsumeSharedContactCooldown()
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(contactDamage: 2),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("contact", "monster.training_slime", 160, 270),
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 250, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), 0);
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .3f);
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .4f);

        Assert.Equal(93, arena.Player.Health);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .1f);

        Assert.Equal(91, arena.Player.Health);
    }

    [Fact]
    public void ResetEncounter_ClearsProjectilesAndCancelsEveryWindup()
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 250, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(160, 270), false, false), 0);
        Assert.True(Assert.Single(arena.Monsters).IsWindingUp);

        arena.ResetEncounter();

        Assert.Empty(arena.Projectiles);
        Assert.False(Assert.Single(arena.Monsters).IsWindingUp);
        Assert.Equal(0, Assert.Single(arena.Monsters).WindupRemaining);
    }

    private static MonsterState CreateArcher(Vector2 position)
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", position.X, position.Y)));
        return Assert.Single(arena.Monsters);
    }

    private static GameContent WithArcherHealth(int health) =>
        WithArcherDefinition(definition => definition with { MaxHealth = health });

    private static GameContent WithArcherDamage(int damage) =>
        WithArcherDefinition(definition => definition with
        {
            RangedAttack = definition.RangedAttack! with { Damage = damage }
        });

    private static GameContent WithArcherDefinition(
        Func<MonsterDefinition, MonsterDefinition> change)
    {
        var content = MixedEncounterFixture.CreateContent();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.skeleton_archer"] = change(monsters["monster.skeleton_archer"]);
        return new GameContent(content.Items, monsters, content.LootTables, content.Affixes);
    }
}
