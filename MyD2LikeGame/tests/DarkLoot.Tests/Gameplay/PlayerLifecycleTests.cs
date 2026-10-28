using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class PlayerLifecycleTests
{
    private static readonly PlayerIntent Idle = new(Vector2.Zero, new Vector2(160, 270), false, false);

    [Fact]
    public void LethalContact_StartsFullDeathTimerOnceAndRespawnsWithoutConsumingOldInput()
    {
        var arena = CreateArena();
        var idle = new PlayerIntent(Vector2.Zero, arena.Player.Position, false, false);
        arena.Update(idle, 0);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(.6f, arena.Player.DeathRemaining, 3);
        Assert.Equal(0, arena.ResetSequence);
        Assert.Single(arena.Events, item => item.ActorId == "player" && item.Kind == CombatEventKind.Died);
        var deathPosition = arena.Player.Position;
        var attackSequence = arena.Player.AttackSequence;
        var controls = new PlayerIntent(Vector2.UnitX, new Vector2(900, 270), true, true);
        arena.Update(controls, .3f);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(.3f, arena.Player.DeathRemaining, 3);
        Assert.Equal(deathPosition, arena.Player.Position);
        Assert.Equal(attackSequence, arena.Player.AttackSequence);
        Assert.DoesNotContain(arena.Events, item => item.ActorId == "player");
        arena.ResetEncounter();
        Assert.True(arena.Player.IsDead);
        Assert.Equal(0, arena.ResetSequence);
        arena.Update(controls, .3f);

        Assert.False(arena.Player.IsDead);
        Assert.Equal(0, arena.Player.DeathRemaining);
        Assert.Equal(arena.Player.MaximumHealth, arena.Player.Health);
        Assert.Equal(new Vector2(160, 270), arena.Player.Position);
        Assert.Equal(.8f, arena.Player.InvulnerabilityRemaining, 3);
        Assert.Equal(attackSequence, arena.Player.AttackSequence);
        Assert.Equal(1, arena.ResetSequence);
        Assert.Empty(arena.Events);
        Assert.False(arena.Player.Reaction.IsActive);
    }

    [Fact]
    public void LethalUpdate_DoesNotSpendItsElapsedTimeOnNewDeath()
    {
        var arena = CreateArena();
        arena.Update(Idle, 2);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(.6f, arena.Player.DeathRemaining, 3);
        arena.Update(Idle, 0);
        Assert.Equal(.6f, arena.Player.DeathRemaining, 3);
        Assert.DoesNotContain(arena.Events, item => item.ActorId == "player");
    }

    [Fact]
    public void DeadPlayer_CannotEquipThroughEitherEntryPoint()
    {
        var arena = CreateArena();
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        Assert.True(new InventoryService().TryPickUp(arena.Player.Inventory, weapon));
        arena.Update(Idle, 0);

        Assert.False(arena.TryEquip(weapon.InstanceId));
        Assert.False(arena.Player.TryEquip(weapon.InstanceId));
        Assert.Null(arena.Player.Inventory.EquippedWeapon);
        Assert.Equal(0, arena.Player.Health);
        arena.Update(Idle, .6f);
        Assert.True(arena.TryEquip(weapon.InstanceId));
    }

    [Fact]
    public void DeadWorld_MovesChasersAndArrowsThroughCorpseThenResetsSixActorsAndPreservesRolledGear()
    {
        var fixture = MixedEncounterFixture.CreateContent(contactDamage: 100);
        var monsters = fixture.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.fast_wolf"] = monsters["monster.fast_wolf"] with { MaxHealth = 10 };
        var content = new GameContent(fixture.Items, monsters,
            new Dictionary<string, LootTableDefinition>
            {
                ["loot.training_empty"] = new("loot.training_empty", 1, 1,
                    [new LootEntryDefinition("weapon.training_blade", 1) { Guaranteed = true }])
            }, fixture.Affixes);
        var arena = new ArenaSimulation(content, new Random(17), MixedEncounterFixture.CreateEncounter(
            new("contact", "monster.training_slime", 140, 270),
            new("drop", "monster.fast_wolf", 160, 290),
            new("chaser", "monster.fast_wolf", 700, 270),
            new("archer.1", "monster.skeleton_archer", 190, 270),
            new("archer.2", "monster.skeleton_archer", 210, 270),
            new("chaser.2", "monster.fast_wolf", 700, 400)));
        var spawnPositions = arena.Monsters.Select(monster => monster.Position).ToArray();
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
            [new AffixRoll("affix.keen", ItemStat.Attack, 4)]);
        var armor = new ItemInstance(Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
            [new AffixRoll("affix.lucky", ItemStat.CriticalChance, 8)]);
        var inventory = new InventoryService();
        Assert.True(inventory.TryPickUp(arena.Player.Inventory, weapon));
        Assert.True(inventory.TryPickUp(arena.Player.Inventory, armor));
        Assert.True(arena.TryEquip(weapon.InstanceId));
        Assert.True(arena.TryEquip(armor.InstanceId));
        arena.Update(Idle with { AimWorldPosition = new Vector2(160, 400), AttackPressed = true }, 0);

        Assert.True(arena.Player.IsDead);
        Assert.Single(arena.GroundItems);
        var attackSequence = arena.Player.AttackSequence;
        arena.Update(Idle with { PickupPressed = true }, .3f);
        Assert.Equal(2, arena.Projectiles.Count);
        var arrowPositions = arena.Projectiles.Select(item => item.Position).ToArray();
        var chaserPosition = arena.Monsters[2].Position;
        arena.Update(Idle with { PickupPressed = true, AttackPressed = true }, .2f);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(0, arena.Player.Health);
        Assert.Equal(attackSequence, arena.Player.AttackSequence);
        Assert.Equal(2, arena.Player.Inventory.Items.Count);
        Assert.Single(arena.GroundItems);
        Assert.True(arena.Monsters[2].Position.X < chaserPosition.X);
        Assert.Equal(2, arena.Projectiles.Count);
        Assert.True(arena.Projectiles[0].Position.X < 160);
        Assert.All(arena.Projectiles.Select((arrow, i) => (arrow, i)), pair =>
            Assert.True(pair.arrow.Position.X < arrowPositions[pair.i].X));
        Assert.DoesNotContain(arena.Events, item => item.ActorId == "player");

        arena.Update(Idle, .1f);
        Assert.False(arena.Player.IsDead);
        Assert.Equal(1, arena.ResetSequence);
        Assert.Empty(arena.GroundItems);
        Assert.Empty(arena.Projectiles);
        Assert.Empty(arena.LastSwingHits);
        Assert.Equal(0, arena.Player.LastHitDamage);
        Assert.Equal(spawnPositions, arena.Monsters.Select(monster => monster.Position));
        Assert.All(arena.Monsters, monster =>
        {
            Assert.Equal(monster.MaxHealth, monster.Health);
            Assert.False(monster.IsWindingUp);
            Assert.Equal(0, monster.WindupRemaining);
            Assert.Equal(Vector2.UnitX, monster.LockedAimDirection);
            Assert.False(monster.Reaction.IsActive);
        });
        Assert.Equal(new[] { weapon, armor }, arena.Player.Inventory.Items);
        Assert.Same(weapon, arena.Player.Inventory.EquippedWeapon);
        Assert.Same(armor, arena.Player.Inventory.EquippedArmor);
        Assert.Equal(4, Assert.Single(weapon.Rolls).Value);
        Assert.Equal(8, Assert.Single(armor.Rolls).Value);
        arena.Update(Idle with { AimWorldPosition = new Vector2(160, 400), AttackPressed = true }, .3f);
        Assert.Equal(2, arena.Projectiles.Count);
        Assert.Single(arena.GroundItems);
    }

    [Fact]
    public void RespawnProtection_ExpiresAfterEightTenthsAndLiveResetClearsItWithoutHealing()
    {
        var arena = CreateArena();
        arena.Update(Idle, 0);
        arena.Update(Idle with { MoveDirection = Vector2.UnitX, AttackPressed = true }, 10);
        Assert.Equal(100, arena.Player.Health);
        Assert.Equal(.8f, arena.Player.InvulnerabilityRemaining, 3);
        Assert.Equal(0, arena.Player.AttackSequence);
        arena.Update(Idle, .799f);
        Assert.Equal(100, arena.Player.Health);
        Assert.True(arena.Player.IsInvulnerable);
        Assert.DoesNotContain(arena.Events, item => item.ActorId == "player");
        arena.Update(Idle, .001f);
        Assert.True(arena.Player.IsDead);

        arena.Update(Idle, .6f);
        arena.ResetEncounter();
        Assert.False(arena.Player.IsInvulnerable);
        Assert.Equal(100, arena.Player.Health);
        arena.Update(Idle, 0);
        Assert.True(arena.Player.IsDead);
    }

    private static ArenaSimulation CreateArena() => new(
        MixedEncounterFixture.CreateContent(contactDamage: 100), new Random(17),
        MixedEncounterFixture.CreateEncounter(
            new MonsterSpawnDefinition("contact", "monster.training_slime", 160, 270),
            new MonsterSpawnDefinition("distant", "monster.fast_wolf", 800, 270)));
}
