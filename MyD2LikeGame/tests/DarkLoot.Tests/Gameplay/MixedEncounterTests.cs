using System.Numerics;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class MixedEncounterTests
{
    [Fact]
    public void Update_FullHealthLethalContactSignalsDeathThenDelayedResetEvenAfterSwing()
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(contactDamage: 100),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("contact", "monster.training_slime", 160, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);

        Assert.Equal(100, arena.Player.Health);
        Assert.Equal(1, arena.Player.AttackSequence);
        Assert.Single(arena.LastSwingHits);
        Assert.Equal(0, arena.ResetSequence);
        Assert.True(arena.Monsters[0].Reaction.IsActive);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .12f);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(0, arena.ResetSequence);
        Assert.Single(arena.Events, item => item.ActorId == "player" && item.Kind == CombatEventKind.Died);
        arena.ResetEncounter();
        Assert.Equal(0, arena.ResetSequence);
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .6f);
        Assert.Equal(100, arena.Player.Health);
        Assert.Empty(arena.LastSwingHits);
        Assert.Equal(1, arena.ResetSequence);
        arena.ResetEncounter();
        Assert.Equal(2, arena.ResetSequence);
    }

    [Fact]
    public void Update_AreaMeleeHitsEachFrontTargetOnceInSpawnOrder()
    {
        var content = MixedEncounterFixture.CreateContent();
        var encounter = MixedEncounterFixture.CreateEncounter(
            new("front.1", "monster.training_slime", 190, 265),
            new("front.2", "monster.training_slime", 190, 275),
            new("back", "monster.training_slime", 130, 270));
        var arena = new ArenaSimulation(content, new Random(17), encounter);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);

        Assert.Equal(new[] { 90, 90, 100 }, arena.Monsters.Select(monster => monster.Health));
        Assert.Equal(new[] { "front.1", "front.2" },
            arena.LastSwingHits.Select(hit => hit.MonsterInstanceId));
        Assert.Equal(2, arena.LastSwingHits.Count);

        var previousHits = arena.LastSwingHits.ToArray();
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .1f);

        Assert.Equal(new[] { 90, 90, 100 }, arena.Monsters.Select(monster => monster.Health));
        Assert.Equal(previousHits, arena.LastSwingHits);
    }

    [Fact]
    public void Update_RollsCriticalIndependentlyForEachTargetInConfiguredOrder()
    {
        var content = MixedEncounterFixture.CreateContent();
        var arena = new ArenaSimulation(
            content,
            new ScriptedRandom(0, 99),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("first", "monster.training_slime", 190, 270),
                new MonsterSpawnDefinition("second", "monster.training_slime", 195, 270)));
        var weapon = new ItemInstance(
            Guid.NewGuid(),
            "weapon.training_blade",
            ItemRarity.Magic,
            [new AffixRoll("affix.test_crit", ItemStat.CriticalChance, 50)]);
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);

        Assert.Equal(
            new[] { new MeleeHit("first", 30, true), new MeleeHit("second", 15, false) },
            arena.LastSwingHits);
        Assert.Equal(new[] { 70, 85 }, arena.Monsters.Select(monster => monster.Health));
        Assert.Equal(arena.LastSwingHits, arena.Events
            .Where(combatEvent => combatEvent.Kind == CombatEventKind.Damaged)
            .Select(combatEvent => new MeleeHit(combatEvent.ActorId, combatEvent.Damage, combatEvent.WasCritical)));
    }

    [Fact]
    public void Update_ResolvesEachKillImmediatelyAndPreservesCriticalAndLootRandomOrder()
    {
        var fixture = MixedEncounterFixture.CreateContent();
        var monsters = fixture.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with
        {
            MaxHealth = 1,
            LootTableId = "resource.slime"
        };
        var resourceTables = fixture.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        resourceTables["resource.slime"] = new ResourceLootTableDefinition(
            "resource.slime", "material.slime", 1, 2, 20,
            ["recipe.magic_blade", "recipe.magic_vest", "recipe.rare_blade", "recipe.rare_vest"]);
        var content = fixture with
        {
            Monsters = monsters,
            ResourceLootTableDefinitions = resourceTables
        };
        var random = new OrderedRandom();
        var arena = new ArenaSimulation(content, random,
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("first", "monster.training_slime", 190, 265),
                new MonsterSpawnDefinition("second", "monster.training_slime", 190, 275)));
        var weapon = new ItemInstance(
            Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
            [new AffixRoll("affix.test_crit", ItemStat.CriticalChance, 50)]);
        Assert.Equal(EquipmentChangeResult.Applied,
            arena.EquipmentOperations.PickUp(arena.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied,
            arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);

        Assert.Equal(new[]
        {
            "crit:100", "count:1:3", "recipe:100", "recipe-index:4",
            "crit:100", "count:1:3", "recipe:100", "recipe-index:4"
        }, random.Calls);
        Assert.Equal(new[] { "first", "second" },
            arena.LastSwingHits.Select(hit => hit.MonsterInstanceId));
        Assert.Equal(new[] { new Vector2(190, 265), new Vector2(190, 265),
            new Vector2(190, 275), new Vector2(190, 275) },
            arena.GroundResources.Select(drop => drop.Position));
        Assert.Equal(
            new[]
            {
                new ResourceStack(ResourceKind.Material, "material.slime", 1),
                new ResourceStack(ResourceKind.Recipe, "recipe.magic_blade", 1),
                new ResourceStack(ResourceKind.Material, "material.slime", 1),
                new ResourceStack(ResourceKind.Recipe, "recipe.magic_blade", 1)
            },
            arena.GroundResources.Select(drop => drop.Stack));
    }

    [Fact]
    public void Update_OutOfArcSwingConsumesCooldownAndDoesNotHitAfterTurning()
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("target", "monster.training_slime", 190, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(160, 170), true, false), 0);
        Assert.Empty(arena.LastSwingHits);
        Assert.Equal(1, arena.Player.AttackSequence);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .1f);
        Assert.Equal(100, Assert.Single(arena.Monsters).Health);
        Assert.Equal(1, arena.Player.AttackSequence);

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .25f);
        Assert.Equal(90, Assert.Single(arena.Monsters).Health);
        Assert.Equal(2, arena.Player.AttackSequence);
    }

    [Fact]
    public void Update_OnlyFirstEligibleContactDamagesPerSharedCooldown()
    {
        var arena = new ArenaSimulation(
            MixedEncounterFixture.CreateContent(contactDamage: 2),
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("first", "monster.training_slime", 160, 270),
                new MonsterSpawnDefinition("second", "monster.training_slime", 160, 270)));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), 0);

        Assert.Equal(98, arena.Player.Health);
        Assert.All(arena.Monsters, monster => Assert.Equal(100, monster.Health));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .1f);

        Assert.Equal(98, arena.Player.Health);
    }

    [Fact]
    public void Update_ContactDeathRespawnsAndShortCircuitsRemainingContacts()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent(contactDamage: 100);
        var monsters = fixtureContent.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.fast_wolf"] = new MonsterDefinition(
            "monster.fast_wolf",
            "Fast Wolf",
            100,
            1,
            140,
            "resource.wolf");
        var content = fixtureContent with { Monsters = monsters };
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("first", "monster.training_slime", 160, 270),
                new MonsterSpawnDefinition("second", "monster.fast_wolf", 160, 270)));
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var armor = new ItemInstance(
            Guid.NewGuid(),
            "armor.training_vest",
            ItemRarity.Magic,
            [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 30)]);
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, armor));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, armor.InstanceId));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, true), 0);

        Assert.True(arena.Player.IsDead);
        Assert.Single(arena.Events, item => item.ActorId == "player" && item.Kind == CombatEventKind.Damaged);
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, true), .6f);
        Assert.Equal(arena.Player.MaximumHealth, arena.Player.Health);
        Assert.Equal(130, arena.Player.MaximumHealth);
        Assert.Equal(new Vector2(160, 270), arena.Player.Position);
        Assert.Equal(new[] { weapon, armor }, arena.Player.Inventory.Items);
        Assert.Same(weapon, arena.Player.Inventory.EquippedWeapon);
        Assert.Same(armor, arena.Player.Inventory.EquippedArmor);
        Assert.All(arena.Monsters, monster => Assert.Equal(100, monster.Health));
        Assert.Equal(new[] { new Vector2(160, 270), new Vector2(160, 270) },
            arena.Monsters.Select(monster => monster.Position));
        Assert.Empty(arena.GroundResources);
    }

    [Fact]
    public void Update_ArcherApproachesUntilInRangeThenStands()
    {
        var content = MixedEncounterFixture.CreateContent();
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("approaching", "monster.skeleton_archer", 500, 270),
                new MonsterSpawnDefinition("standing", "monster.skeleton_archer", 250, 270)));
        var behavior = new MonsterBehaviorSystem();

        behavior.Update(arena.Monsters[0], new Vector2(160, 270), 1f);
        behavior.Update(arena.Monsters[1], new Vector2(160, 270), 1f);

        Assert.Equal(new Vector2(430, 270), arena.Monsters[0].Position);
        Assert.Equal(new Vector2(250, 270), arena.Monsters[1].Position);
    }

    [Fact]
    public void ResetEncounter_ResetsEachMonsterAndClearsSwingResults()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent();
        var monsters = fixtureContent.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with
        {
            MoveSpeed = .01f
        };
        var arena = new ArenaSimulation(
            fixtureContent with { Monsters = monsters },
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("front", "monster.training_slime", 190, 270),
                new MonsterSpawnDefinition("back", "monster.training_slime", 130, 270)));

        for (var swing = 0; swing < 10; swing++)
        {
            arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .35f);
            if (swing < 9)
            {
                // Follow the accepted knockback so the nearly stationary target stays in reach.
                arena.Update(new PlayerIntent(Vector2.UnitX, new Vector2(300, 270), false, false), 10f / 180);
            }
        }

        Assert.Equal(1, arena.RemainingMonsters);
        Assert.False(arena.IsCleared);
        Assert.True(arena.Monsters[0].IsDefeated);
        Assert.Single(arena.LastSwingHits);

        arena.ResetEncounter();

        Assert.Equal(2, arena.RemainingMonsters);
        Assert.False(arena.IsCleared);
        Assert.Equal(new[] { 100, 100 }, arena.Monsters.Select(monster => monster.Health));
        Assert.Empty(arena.LastSwingHits);
    }

    [Fact]
    public void ResetEncounter_ClearsGroundLootAndArrowsWhilePreservingPlayerStateAndGear()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent(contactDamage: 2);
        var monsters = fixtureContent.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with
        {
            MaxHealth = 10
        };
        monsters["monster.skeleton_archer"] = monsters["monster.skeleton_archer"] with
        {
            RangedAttack = monsters["monster.skeleton_archer"].RangedAttack! with
            {
                Range = 500
            }
        };
        monsters["monster.fast_wolf"] = monsters["monster.fast_wolf"] with
        {
            ContactDamage = 2
        };
        var resourceTables = fixtureContent.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        resourceTables["resource.slime"] = new ResourceLootTableDefinition(
            "resource.slime", "material.slime", 1, 1, 0, []);
        var content = fixtureContent with
        {
            Monsters = monsters,
            ResourceLootTableDefinitions = resourceTables
        };
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("slime", "monster.training_slime", 180, 270),
                new MonsterSpawnDefinition("archer", "monster.skeleton_archer", 500, 270),
                new MonsterSpawnDefinition("contact", "monster.fast_wolf", 160, 270)));
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));

        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .3f);
        Assert.Equal(100, arena.Player.Health);
        Assert.True(arena.Monsters[2].Reaction.IsActive);
        arena.Update(new PlayerIntent(Vector2.UnitY, new Vector2(300, 270), false, false), .1f);
        Assert.Equal(100, arena.Player.Health);
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .02f);

        Assert.Single(arena.GroundResources);
        Assert.Single(arena.Projectiles);
        var playerPosition = arena.Player.Position;
        var playerHealth = arena.Player.Health;
        var equippedWeapon = arena.Player.Inventory.EquippedWeapon;
        Assert.NotEqual(new Vector2(160, 270), playerPosition);
        Assert.NotEqual(arena.Player.MaximumHealth, playerHealth);

        arena.ResetEncounter();

        Assert.Empty(arena.GroundResources);
        Assert.Empty(arena.Projectiles);
        Assert.Equal(arena.Monsters.Count, arena.RemainingMonsters);
        Assert.Equal(playerPosition, arena.Player.Position);
        Assert.Equal(playerHealth, arena.Player.Health);
        Assert.Same(equippedWeapon, arena.Player.Inventory.EquippedWeapon);
        Assert.Empty(arena.LastSwingHits);
        Assert.All(arena.Monsters, monster => Assert.False(monster.IsWindingUp));
    }

    [Fact]
    public void Update_ThreeDefeatedInstancesEachDropOneDistinctResourceExactlyOnce()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent();
        var monsters = fixtureContent.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with
        {
            MaxHealth = 10
        };
        var content = fixtureContent with { Monsters = monsters };
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("slime.1", "monster.training_slime", 190, 265),
                new MonsterSpawnDefinition("slime.2", "monster.training_slime", 190, 270),
                new MonsterSpawnDefinition("slime.3", "monster.training_slime", 190, 275)));

        for (var swing = 0; swing < 8; swing++)
        {
            arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .35f);
        }

        Assert.Equal(0, arena.RemainingMonsters);
        Assert.Equal(3, arena.GroundResources.Count);
        Assert.Equal(3, arena.GroundResources.Select(drop => drop.DropId).Distinct().Count());
        Assert.All(arena.GroundResources, drop =>
            Assert.Equal(new ResourceStack(ResourceKind.Material, "material.slime", 1), drop.Stack));
    }

    [Fact]
    public void Update_UsesEachInstancesMonsterLootDefinitionExactlyOnce()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent();
        var monsters = fixtureContent.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with
        {
            LootTableId = "resource.weapon"
        };
        monsters["monster.fast_wolf"] = monsters["monster.fast_wolf"] with
        {
            LootTableId = "resource.armor"
        };
        var resourceTables = fixtureContent.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        resourceTables["resource.weapon"] = new ResourceLootTableDefinition(
            "resource.weapon", "material.slime", 1, 1, 0, []);
        resourceTables["resource.armor"] = new ResourceLootTableDefinition(
            "resource.armor", "material.hide", 1, 1, 0, []);
        var content = fixtureContent with
        {
            Monsters = monsters,
            ResourceLootTableDefinitions = resourceTables
        };
        var arena = new ArenaSimulation(
            content,
            new Random(17),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("slime", "monster.training_slime", 190, 270),
                new MonsterSpawnDefinition("wolf", "monster.fast_wolf", 190, 270)));

        for (var swing = 0; swing < 10; swing++)
        {
            arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);
            // Finish knockback, then let survivors chase before the next accepted swing.
            arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .12f);
            arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), false, false), .23f);
        }
        arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .35f);

        Assert.True(arena.IsCleared);
        Assert.Equal(0, arena.RemainingMonsters);
        Assert.Equal(
            new[] { "material.hide", "material.slime" },
            arena.GroundResources.Select(drop => drop.Stack.DefinitionId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Constructor_ConvenienceOverloadRequiresProductionEncounter()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new ArenaSimulation(MixedEncounterFixture.CreateContent(), new Random(17)));

        Assert.Contains("encounter.training_arena", error.Message);
    }

    [Fact]
    public void Constructor_ConvenienceOverloadCreatesConfiguredSpawnsInOrder()
    {
        var fixtureContent = MixedEncounterFixture.CreateContent();
        var encounter = MixedEncounterFixture.CreateEncounter(
            new MonsterSpawnDefinition("slime.1", "monster.training_slime", 480, 150),
            new MonsterSpawnDefinition("slime.2", "monster.training_slime", 480, 390),
            new MonsterSpawnDefinition("wolf.1", "monster.fast_wolf", 650, 180),
            new MonsterSpawnDefinition("wolf.2", "monster.fast_wolf", 650, 360),
            new MonsterSpawnDefinition("archer.1", "monster.skeleton_archer", 820, 150),
            new MonsterSpawnDefinition("archer.2", "monster.skeleton_archer", 820, 390));
        var content = fixtureContent with
        {
            EncounterDefinitions = new Dictionary<string, EncounterDefinition>
            {
                ["encounter.training_arena"] = encounter
            }
        };

        var arena = new ArenaSimulation(content, new Random(17));

        Assert.Equal(
            new[] { "slime.1", "slime.2", "wolf.1", "wolf.2", "archer.1", "archer.2" },
            arena.Monsters.Select(monster => monster.InstanceId));
        Assert.Equal(6, arena.RemainingMonsters);
        Assert.False(arena.IsCleared);
        Assert.All(arena.Monsters, monster => Assert.Equal(100, monster.Health));
    }

    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int maxValue) => values[_index++];
    }

    private sealed class OrderedRandom : Random
    {
        public List<string> Calls { get; } = [];
        private int _hundredDraws;

        public override int Next(int maxValue)
        {
            Calls.Add(maxValue == 100
                ? (_hundredDraws++ % 2 == 0 ? $"crit:{maxValue}" : $"recipe:{maxValue}")
                : $"recipe-index:{maxValue}");
            return 0;
        }

        public override long NextInt64(long minValue, long maxValue)
        {
            Calls.Add($"count:{minValue}:{maxValue}");
            return minValue;
        }
    }
}
