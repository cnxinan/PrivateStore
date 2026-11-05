using System.Numerics;
using System.Text.Json;
using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Persistence;

namespace DarkLoot.Tests.Gameplay;

public sealed class DungeonSessionTests
{
    private static GameContent Content() => GameContentLoader.LoadDirectory(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content", "definitions")));
    private static PlayerIntent Idle(Vector2 aim) => new(Vector2.Zero, aim, false, false);

    [Fact]
    public void HubTravel_PreservesExactProgressAndDungeonStateWithoutHealing()
    {
        var content = Content();
        var session = new DungeonSession(content, new Random(7));
        Assert.Empty(session.Arena.Monsters);
        Assert.False(session.InDungeon);
        var item = new EquipmentFactory().Create("weapon.training_blade",
            DarkLoot.Data.Definitions.ItemRarity.Magic, content, new Random(3));
        session.Arena.EquipmentOperations.PickUp(session.Arena.Player, item);
        session.Arena.EquipmentOperations.Equip(session.Arena.Player, item.InstanceId);
        session.Arena.Player.Health = 63;
        var before = JsonSerializer.Serialize(new ProgressPersistence(content).Capture(session.Arena));
        session.Arena.Player.Position = DungeonSession.HubEntrance;
        Assert.True(session.TryTravel());
        Assert.True(session.InDungeon);
        var dungeon = session.Arena;
        Assert.Same(session.Hub.Player, dungeon.Player);
        Assert.False(dungeon.BlacksmithOperations.CanInteract(dungeon.Player));
        var monster = dungeon.Monsters[0];
        monster.Health--;
        dungeon.Player.Position = DungeonSession.DungeonEntrance;
        Assert.True(session.TryTravel());
        Assert.False(session.InDungeon);
        Assert.Equal(63, session.Arena.Player.Health);
        session.Arena.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        Assert.Same(dungeon, session.Arena);
        Assert.Equal(monster.MaxHealth - 1, monster.Health);
        Assert.Equal(before, JsonSerializer.Serialize(new ProgressPersistence(content).Capture(session.Arena)));
    }

    [Fact]
    public void ClearBattleAndBoss_OpensRealGateThenCompletionExit()
    {
        var session = new DungeonSession(Content(), new Random(13));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        Assert.False(session.Arena.Map!.BossGateOpen);
        session.Arena.Player.InvulnerabilityRemaining = 100;
        foreach (var monster in session.Arena.Monsters.Where(m => m.InstanceId.StartsWith("battle.", StringComparison.Ordinal)))
            Kill(session, monster);
        Assert.True(session.Arena.Map.BossGateOpen);
        Assert.False(session.Completed);
        var boss = session.Arena.Monsters.Single(m => m.InstanceId == "boss.guardian");
        Kill(session, boss);
        Assert.True(session.Completed);
        session.Arena.Player.Position = DungeonSession.BossExit;
        Assert.True(session.TryTravel());
        Assert.False(session.InDungeon);
    }

    [Fact]
    public void Death_ReturnsToHubAfterFullWaitAndResetsWorldButNotOwnedProgress()
    {
        var content = Content();
        var session = new DungeonSession(content, new Random(17));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var arena = session.Arena;
        var before = JsonSerializer.Serialize(new ProgressPersistence(content).Capture(arena));
        arena.Monsters[0].Health--;
        arena.Player.Health = 0;
        arena.Player.DeathRemaining = .6f;
        session.Update(Idle(arena.Player.Position), .3f);
        Assert.True(session.InDungeon);
        Assert.True(arena.Player.IsDead);
        session.Update(Idle(arena.Player.Position), .3f);
        Assert.False(session.InDungeon);
        Assert.Equal(session.Hub.Player.MaximumHealth, session.Hub.Player.Health);
        Assert.All(session.Dungeon.Monsters, m => Assert.Equal(m.MaxHealth, m.Health));
        Assert.False(session.Dungeon.Map!.BossGateOpen);
        Assert.Empty(session.Dungeon.GroundResources);
        Assert.Equal(before, JsonSerializer.Serialize(new ProgressPersistence(content).Capture(session.Arena)));
    }

    [Fact]
    public void Projectile_StopsAtClosedGateBeforeDamagingPlayerBehindIt()
    {
        var map = WorldMap.Crypt();
        var projectiles = new ProjectileSystem();
        projectiles.Spawn(new("archer", new(2140, 270), Vector2.UnitX, 240, 10));
        var hit = false;
        projectiles.Update(1, new(2280, 270), true, _ => { hit = true; return false; }, map);
        Assert.False(hit);
        Assert.Empty(projectiles.Projectiles);
    }

    [Fact]
    public void RestartAndLoad_StartFreshWithoutHealingOnRestart()
    {
        var content = Content();
        var session = new DungeonSession(content, new Random(23));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        session.Arena.Player.Health = 44;
        session.Arena.Monsters[0].Health--;
        session.Restart();
        Assert.True(session.InDungeon);
        Assert.Equal(44, session.Arena.Player.Health);
        Assert.All(session.Arena.Monsters, m => Assert.Equal(m.MaxHealth, m.Health));
        var persistence = new ProgressPersistence(content);
        var snapshot = persistence.Capture(session.Arena);
        var fresh = new DungeonSession(content, new Random(29));
        persistence.Restore(fresh.Arena, snapshot);
        Assert.False(fresh.InDungeon);
        Assert.Empty(fresh.Arena.Monsters);
        Assert.Equal(2, snapshot.Version);
    }

    private static void Kill(DungeonSession session, MonsterState monster)
    {
        for (var tick = 0; tick < 40 && !monster.IsDefeated; tick++)
        {
            session.Arena.Player.Position = Enumerable.Range(0, 16)
                .Select(index => monster.Position + new Vector2(MathF.Cos(index * MathF.Tau / 16),
                    MathF.Sin(index * MathF.Tau / 16)) * 30)
                .First(position => session.Arena.Map!.CanOccupy(position) &&
                    session.Arena.Map.HasLineOfSight(position, monster.Position));
            session.Update(new(Vector2.Zero, monster.Position, true, false), .4f);
        }
        Assert.True(monster.IsDefeated,
            $"{monster.InstanceId} health={monster.Health}; monster={monster.Position}; " +
            $"player={session.Arena.Player.Position}; alive={session.Arena.Player.Health}; " +
            $"visible={session.Arena.Map!.HasLineOfSight(session.Arena.Player.Position, monster.Position)}");
    }

    [Fact]
    public void ClosedGate_PreventsMeleeAndEnemyCrossing()
    {
        var session = new DungeonSession(Content(), new Random(31));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var arena = session.Arena;
        var wolf = arena.Monsters.First(m => m.Id == "monster.fast_wolf");
        arena.Player.Position = new(2170, 270);
        wolf.Position = new(2214, 270);
        var health = wolf.Health;
        session.Update(new(Vector2.Zero, wolf.Position, true, false), .1f);
        Assert.Equal(health, wolf.Health);
        for (var tick = 0; tick < 10; tick++)
            session.Update(Idle(wolf.Position), .1f);
        Assert.True(wolf.Position.X >= 2214);
        Assert.True(arena.Map!.CanOccupy(wolf.Position));
    }

    [Fact]
    public void Knockback_StopsAtSolidGateRatherThanPushingEnemyThroughIt()
    {
        var session = new DungeonSession(Content(), new Random(37));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var wolf = session.Arena.Monsters.First(m => m.Id == "monster.fast_wolf");
        session.Arena.Player.Position = new(2136, 270);
        wolf.Position = new(2170, 270);
        session.Update(new(Vector2.Zero, wolf.Position, true, false), .01f);
        Assert.True(wolf.Reaction.IsActive);
        session.Update(Idle(wolf.Position), .12f);
        Assert.True(wolf.Position.X <= 2170.01f);
        Assert.True(session.Arena.Map!.CanOccupy(wolf.Position));
    }

    [Fact]
    public void ChasingEnemy_NavigatesAroundPillarWithoutEnteringWalls()
    {
        var session = new DungeonSession(Content(), new Random(41));
        var map = session.Dungeon.Map!;
        var wolf = session.Dungeon.Monsters.First(m => m.Id == "monster.fast_wolf");
        wolf.Position = new(1380, 180);
        var target = new Vector2(1650, 180);
        var behavior = new MonsterBehaviorSystem();
        Assert.False(map.HasLineOfSight(wolf.Position, target));
        for (var tick = 0; tick < 100 && Vector2.Distance(wolf.Position, target) > 20; tick++)
        {
            behavior.Update(wolf, target, .1f, _ => Assert.Fail("Wolf cannot fire"), map);
            Assert.True(map.CanOccupy(wolf.Position));
        }
        Assert.True(Vector2.Distance(wolf.Position, target) <= 20);
    }

    [Fact]
    public void FutureRoom_StaysDormantUntilEnteredThenCanFollowPlayerBack()
    {
        var session = new DungeonSession(Content(), new Random(43));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var wolf = session.Arena.Monsters.First(m => m.Id == "monster.fast_wolf");
        var initial = wolf.Position;
        session.Update(Idle(Vector2.UnitX), .1f);
        Assert.Equal(initial, wolf.Position);
        session.Arena.Player.Position = new(1200, 270);
        session.Update(Idle(Vector2.UnitX), .1f);
        Assert.NotEqual(initial, wolf.Position);
        var awakened = wolf.Position;
        session.Arena.Player.Position = new(160, 270);
        session.Update(Idle(Vector2.UnitX), .1f);
        Assert.NotEqual(awakened, wolf.Position);
        session.Restart();
        var reset = wolf.Position;
        session.Update(Idle(Vector2.UnitX), .1f);
        Assert.Equal(reset, wolf.Position);
    }

    [Fact]
    public void SkeletonWarrior_TakesMultipleHitsAndDropsExistingBonesThenResets()
    {
        var session = new DungeonSession(Content(), new Random(47));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var arena = session.Arena;
        var warrior = Assert.Single(arena.Monsters, m => m.Id == "monster.skeleton_warrior");
        Assert.StartsWith("entry.", warrior.InstanceId);
        Assert.True(arena.Map!.CanOccupy(warrior.Position));
        arena.Player.InvulnerabilityRemaining = 100;
        arena.Player.Position = warrior.Position - Vector2.UnitX * 30;
        session.Update(new(Vector2.Zero, warrior.Position, true, false), 0);
        Assert.False(warrior.IsDefeated);
        Assert.Equal(40, warrior.Health);
        Kill(session, warrior);
        Assert.Contains(arena.GroundResources, drop => drop.Stack.DefinitionId == "material.bone");
        var rewards = arena.GroundResources.Count;
        session.Update(Idle(warrior.Position), .4f);
        Assert.Equal(rewards, arena.GroundResources.Count);
        session.Restart();
        Assert.Equal(warrior.MaxHealth, warrior.Health);
        Assert.Empty(arena.GroundResources);
    }

    [Fact]
    public void Cultist_RemainsRequiredForBattleGateAfterOriginalEnemiesDie()
    {
        var session = new DungeonSession(Content(), new Random(53));
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        session.TryTravel();
        var arena = session.Arena;
        var cultist = Assert.Single(arena.Monsters, m => m.Id == "monster.cultist_mage");
        Assert.StartsWith("battle.", cultist.InstanceId);
        Assert.True(arena.Map!.CanOccupy(cultist.Position));
        arena.Player.InvulnerabilityRemaining = 100;
        foreach (var monster in arena.Monsters.Where(m => m.InstanceId.StartsWith("battle.", StringComparison.Ordinal) &&
            m != cultist))
            Kill(session, monster);
        Assert.False(arena.Map.BossGateOpen);
        Kill(session, cultist);
        Assert.True(arena.Map.BossGateOpen);
    }

    [Fact]
    public void Cultist_LocksAimDuringCastAndProjectileStopsAtStonePillar()
    {
        var session = new DungeonSession(Content(), new Random(59));
        var mage = Assert.Single(session.Dungeon.Monsters, m => m.Id == "monster.cultist_mage");
        var behavior = new MonsterBehaviorSystem();
        var launches = new List<ProjectileState>();
        mage.Position = new(1400, 180);
        behavior.Update(mage, new(1300, 180), .2f, launches.Add, session.Dungeon.Map);
        Assert.True(mage.IsWindingUp);
        Assert.Empty(launches);
        Assert.Equal(-Vector2.UnitX, mage.LockedAimDirection);
        behavior.Update(mage, new(1400, 300), .4f, launches.Add, session.Dungeon.Map);
        var orb = Assert.Single(launches);
        Assert.Equal(-Vector2.UnitX, orb.Direction);
        Assert.Equal(mage.InstanceId, orb.SourceInstanceId);
        var projectiles = new ProjectileSystem();
        projectiles.Spawn(orb with { Direction = Vector2.UnitX });
        var hit = false;
        projectiles.Update(1, new(1620, 180), true, _ => { hit = true; return false; }, session.Dungeon.Map);
        Assert.False(hit);
        Assert.Empty(projectiles.Projectiles);
    }
}
