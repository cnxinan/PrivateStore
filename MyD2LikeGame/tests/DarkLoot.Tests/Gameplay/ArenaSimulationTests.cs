using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class ArenaSimulationTests
{
    [Fact]
    public void Update_HeldAttackRepeatsOnlyAfterCooldownAndReleaseStopsSwings()
    {
        var simulation = CreateSingleTargetArena(CreateContent(), new Random(17));
        var held = new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false);
        simulation.Update(held, 0);
        Assert.Equal(1, simulation.Player.AttackSequence);
        simulation.Update(held, .1f);
        Assert.Equal(1, simulation.Player.AttackSequence);
        simulation.Update(held, .25f);
        Assert.Equal(2, simulation.Player.AttackSequence);
        simulation.Update(held with { AttackPressed = false }, .5f);
        Assert.Equal(2, simulation.Player.AttackSequence);
    }

    [Fact]
    public void Update_NormalizesDiagonalMovementAndKeepsPositionsFiniteAndBounded()
    {
        var simulation = CreateSingleTargetArena(CreateContent(), new Random(17));
        var start = simulation.Player.Position;

        simulation.Update(
            new PlayerIntent(new Vector2(1, 1), new Vector2(480, 270), false, false),
            1f);

        Assert.Equal(180f, Vector2.Distance(start, simulation.Player.Position), 3);
        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monsters[0].Position);

        simulation.Update(
            new PlayerIntent(
                new Vector2(float.NaN, float.PositiveInfinity),
                new Vector2(float.NaN, float.NegativeInfinity),
                false,
                false),
            1000f);

        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monsters[0].Position);
    }

    [Fact]
    public void Update_AttacksOnlyWhenMonsterIsInRangeAndFacingArc()
    {
        var random = new CountingRandom();
        var simulation = CreateSingleTargetArena(CreateContent(), random);

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, new Vector2(480, 350), true, false),
            1f);
        Assert.Equal(12, simulation.Monsters[0].Health);

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monsters[0].Position, true, false),
            0.35f);

        Assert.Equal(2, simulation.Monsters[0].Health);
        Assert.False(simulation.Player.LastHitWasCritical);
        Assert.Equal(0, random.CriticalRolls);
    }

    [Fact]
    public void Update_EnforcesPlayerAttackCooldown()
    {
        var simulation = CreateSingleTargetArena(CreateContent(), new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monsters[0].Position, false, false),
            1f);
        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monsters[0].Position, true, false),
            0.35f);
        Assert.Equal(2, simulation.Monsters[0].Health);

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false),
            0.1f);
        Assert.Equal(2, simulation.Monsters[0].Health);

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false),
            0.25f);
        Assert.True(simulation.Monsters[0].IsDefeated);
    }

    [Fact]
    public void Update_HandlesCoincidentPlayerAndMonsterAndCoincidentAim()
    {
        var simulation = CreateSingleTargetArena(CreateContent(), new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, new Vector2(480, 270), false, false),
            100f);

        Assert.Equal(simulation.Player.Position, simulation.Monsters[0].Position);
        Assert.True(simulation.Player.Reaction.IsActive);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, true, false),
            0f);

        Assert.Equal(12, simulation.Monsters[0].Health);
        Assert.Equal(0, simulation.Player.AttackSequence);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, false),
            .15f);
        Assert.False(simulation.Player.Reaction.IsActive);
        Assert.Equal(simulation.Player.Position, simulation.Monsters[0].Position);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, true, false),
            0f);

        Assert.Equal(2, simulation.Monsters[0].Health);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, true, false),
            0.35f);
        Assert.True(simulation.Monsters[0].IsDefeated);
        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monsters[0].Position);
    }

    [Fact]
    public void Update_KillsOncePicksUpAndEquipsLootThenResetKeepsWeaponDamage()
    {
        var content = CreateContent();
        var simulation = CreateSingleTargetArena(content, new Random(17));

        var unarmedAttackCount = AdvanceUntilDefeated(simulation);
        Assert.Equal(2, unarmedAttackCount);

        Assert.True(simulation.Monsters[0].IsDefeated);
        var drop = Assert.Single(simulation.GroundItems);
        Assert.Equal("item.training_sword", drop.Item.DefinitionId);

        for (var index = 0; index < 20; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false),
                0.1f);
        }

        Assert.Single(simulation.GroundItems);
        Assert.Empty(simulation.Player.Inventory.Items);

        simulation.Update(
            new PlayerIntent(Vector2.UnitY, simulation.Monsters[0].Position, false, false),
            1f);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, true),
            0f);
        Assert.Single(simulation.GroundItems);
        Assert.Empty(simulation.Player.Inventory.Items);

        simulation.Update(
            new PlayerIntent(-Vector2.UnitY, simulation.Monsters[0].Position, false, true),
            1f);
        Assert.Empty(simulation.GroundItems);
        var weapon = Assert.Single(simulation.Player.Inventory.Items);

        Assert.Equal(EquipmentChangeResult.Applied,
            simulation.EquipmentOperations.Equip(simulation.Player, weapon.InstanceId));
        Assert.Equal(13, simulation.Player.AttackDamage);

        simulation.ResetEncounter();
        Assert.Empty(simulation.GroundItems);
        Assert.Equal(12, simulation.Monsters[0].Health);
        Assert.False(simulation.Monsters[0].IsDefeated);
        Assert.Same(weapon, simulation.Player.Inventory.EquippedWeapon);
        Assert.Equal(13, simulation.Player.AttackDamage);

        var equippedAttackCount = AdvanceUntilDefeated(simulation);

        Assert.True(simulation.Monsters[0].IsDefeated);
        Assert.Equal(1, equippedAttackCount);
        Assert.Single(simulation.GroundItems);
    }

    [Fact]
    public void Update_RespawnsPlayerAndMonsterAndClearsDropsWithoutLosingEquipment()
    {
        var content = CreateContent();
        var simulation = CreateSingleTargetArena(content, new Random(17));
        var sword = new ItemInstance(Guid.NewGuid(), "item.training_sword");
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, sword));
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, sword.InstanceId));

        AdvanceUntilDefeated(simulation);
        Assert.Single(simulation.GroundItems);
        simulation.ResetEncounter();
        Assert.Empty(simulation.GroundItems);

        var tookDamage = false;
        var sawDeath = false;
        for (var index = 0; index < 1000; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, false, false),
                0.1f);

            tookDamage |= simulation.Player.Health < 100;
            sawDeath |= simulation.Player.IsDead;
            if (tookDamage && simulation.Player.Health == 100)
            {
                break;
            }
        }

        Assert.True(sawDeath);
        Assert.Equal(100, simulation.Player.Health);
        Assert.Equal(new Vector2(160, 270), simulation.Player.Position);
        Assert.Equal(new Vector2(480, 270), simulation.Monsters[0].Position);
        Assert.False(simulation.Monsters[0].IsDefeated);
        Assert.Empty(simulation.GroundItems);
        Assert.Same(sword, simulation.Player.Inventory.EquippedWeapon);
        Assert.Equal(13, simulation.Player.AttackDamage);
    }

    [Fact]
    public void Update_LimitsMonsterContactDamageToOneHitPerCooldown()
    {
        var content = CreateContent(contactDamage: 2, monsterSpeed: 100);
        var simulation = CreateSingleTargetArena(content, new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, false),
            10f);
        Assert.Equal(98, simulation.Player.Health);

        for (var index = 0; index < 7; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, false),
                0.1f);
        }

        Assert.Equal(98, simulation.Player.Health);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, false),
            0.1f);
        Assert.Equal(96, simulation.Player.Health);
    }

    [Fact]
    public void Equip_ClampsHealthImmediatelyAndNeverHealsWhenMaximumChanges()
    {
        var content = CreateGearContent();
        var simulation = CreateSingleTargetArena(content, new Random(17));
        var highArmor = new ItemInstance(
            Guid.NewGuid(), "item.armor_high", ItemRarity.Magic,
            [new AffixRoll("health.high", ItemStat.MaximumHealth, 50)]);
        var lowArmor = new ItemInstance(
            Guid.NewGuid(), "item.armor_low", ItemRarity.Magic,
            [new AffixRoll("health.low", ItemStat.MaximumHealth, -50)]);
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, highArmor));
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, lowArmor));
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, highArmor.InstanceId));

        simulation.Update(new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, false, false), 3f);
        Assert.Equal(90, simulation.Player.Health);
        Assert.Equal(150, simulation.Player.MaximumHealth);

        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, lowArmor.InstanceId));
        Assert.Equal(50, simulation.Player.Health);
        Assert.Equal(50, simulation.Player.MaximumHealth);
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, highArmor.InstanceId));
        Assert.Equal(50, simulation.Player.Health);
        Assert.Equal(150, simulation.Player.MaximumHealth);
    }

    [Fact]
    public void Equip_RejectsHealthOverflowBeforeChangingEquipment()
    {
        var baseContent = CreateGearContent();
        var items = baseContent.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        items["item.armor_dead"] = new("item.armor_dead", "Deadly Armor", 0)
        { Slot = ItemSlot.Armor };
        var affixes = baseContent.Affixes.ToDictionary(pair => pair.Key, pair => pair.Value);
        affixes["health.dead"] = new(
            "health.dead", "Deadly", ItemStat.MaximumHealth, -200, -200, [ItemSlot.Armor]);
        var content = new GameContent(items, baseContent.Monsters, baseContent.LootTables, affixes);
        var simulation = CreateSingleTargetArena(content, new Random(17));
        var item = new ItemInstance(
            Guid.NewGuid(), "item.armor_dead", ItemRarity.Magic,
            [new AffixRoll("health.dead", ItemStat.MaximumHealth, -200)]);
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, item));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            simulation.EquipmentOperations.Equip(simulation.Player, item.InstanceId));

        Assert.Null(simulation.Player.Inventory.EquippedArmor);
        Assert.Null(simulation.Player.Inventory.EquippedArmorId);
        Assert.Same(item, simulation.Player.Inventory.Find(item.InstanceId));
        Assert.Equal(100, simulation.Player.Health);
    }

    [Fact]
    public void Update_GuaranteedCriticalDoublesSuccessfulHitAndExposesActualDamage()
    {
        var content = CreateGearContent();
        var random = new CountingRandom();
        var simulation = CreateSingleTargetArena(content, random);
        var weapon = new ItemInstance(
            Guid.NewGuid(), "item.crit_weapon", ItemRarity.Magic,
            [new AffixRoll("crit.max", ItemStat.CriticalChance, 100)]);
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, weapon.InstanceId));

        for (var index = 0; index < 20 && simulation.Player.LastHitDamage == 0; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.UnitX, simulation.Monsters[0].Position, true, false),
                0.2f);
        }

        Assert.True(simulation.Player.LastHitWasCritical);
        Assert.Equal(20, simulation.Player.LastHitDamage);
        Assert.Equal(80, simulation.Monsters[0].Health);
        Assert.Equal(0, random.CriticalRolls);
    }

    [Fact]
    public void Update_RollsCriticalRandomnessOnlyAfterSuccessfulMeleeHit()
    {
        var content = CreateGearContent();
        var random = new CountingRandom();
        var simulation = CreateSingleTargetArena(content, random);
        var weapon = new ItemInstance(
            Guid.NewGuid(), "item.crit_weapon", ItemRarity.Magic,
            [new AffixRoll("crit.mid", ItemStat.CriticalChance, 50)]);
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.PickUp(simulation.Player, weapon));
        Assert.Equal(EquipmentChangeResult.Applied, simulation.EquipmentOperations.Equip(simulation.Player, weapon.InstanceId));

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false),
            0.1f);
        Assert.Equal(0, random.CriticalRolls);

        for (var index = 0; index < 30 &&
             Vector2.Distance(simulation.Player.Position, simulation.Monsters[0].Position) > 48;
             index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.UnitX, simulation.Monsters[0].Position, false, false),
                0.05f);
        }

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monsters[0].Position, true, false),
            0f);

        Assert.Equal(1, random.CriticalRolls);
        Assert.True(simulation.Player.LastHitDamage > 0);
    }

    [Fact]
    public void Constructor_ReportsMissingTrainingMonsterId()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>(),
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());

        var error = Assert.Throws<ArgumentException>(
            () => new ArenaSimulation(
                content,
                new Random(17),
                MixedEncounterFixture.CreateEncounter(
                    new MonsterSpawnDefinition("missing", "monster.training_slime", 480, 270))));

        Assert.Contains("monster.training_slime", error.Message);
    }

    [Fact]
    public void Constructor_ReportsMissingLootTableId()
    {
        var completeContent = CreateContent();
        var content = new GameContent(
            completeContent.Items,
            completeContent.Monsters,
            new Dictionary<string, LootTableDefinition>());

        var error = Assert.Throws<ArgumentException>(
            () => new ArenaSimulation(
                content,
                new Random(17),
                MixedEncounterFixture.CreateEncounter(
                    new MonsterSpawnDefinition("single", "monster.training_slime", 480, 270))));

        Assert.Contains("loot.training_slime", error.Message);
    }

    private static int AdvanceUntilDefeated(ArenaSimulation simulation)
    {
        var attackCount = 0;
        for (var index = 0; index < 1000 && !simulation.Monsters[0].IsDefeated; index++)
        {
            var delta = simulation.Monsters[0].Position - simulation.Player.Position;
            var direction = delta.LengthSquared() <= 48 * 48
                ? Vector2.Zero
                : Vector2.Normalize(delta);
            var previousHealth = simulation.Monsters[0].Health;

            simulation.Update(
                new PlayerIntent(
                    direction,
                    simulation.Monsters[0].Position,
                    true,
                    false),
                0.05f);

            if (simulation.Monsters[0].Health < previousHealth)
            {
                attackCount++;
            }
        }

        return attackCount;
    }

    private static void AssertFiniteAndBounded(Vector2 position)
    {
        Assert.True(float.IsFinite(position.X));
        Assert.True(float.IsFinite(position.Y));
        Assert.InRange(position.X, 0f, 960f);
        Assert.InRange(position.Y, 0f, 540f);
    }

    private static ArenaSimulation CreateSingleTargetArena(GameContent content, Random random) =>
        new(
            content,
            random,
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("single", "monster.training_slime", 480, 270)));

    private static GameContent CreateContent(
        int contactDamage = 2,
        float monsterSpeed = 45.5f)
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["item.training_sword"] = new("item.training_sword", "Training Sword", 3)
        };
        var monsters = new Dictionary<string, MonsterDefinition>
        {
            ["monster.training_slime"] = new(
                "monster.training_slime",
                "Training Slime",
                12,
                contactDamage,
                monsterSpeed,
                "loot.training_slime")
        };
        var lootTables = new Dictionary<string, LootTableDefinition>
        {
            ["loot.training_slime"] = new(
                "loot.training_slime",
                1,
                1,
                [new LootEntryDefinition("item.training_sword", 1)])
        };

        return new GameContent(items, monsters, lootTables);
    }

    private static GameContent CreateGearContent()
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["item.training_sword"] = new("item.training_sword", "Training Sword", 3),
            ["item.armor_high"] = new("item.armor_high", "High Armor", 0) { Slot = ItemSlot.Armor },
            ["item.armor_low"] = new("item.armor_low", "Low Armor", 0) { Slot = ItemSlot.Armor },
            ["item.crit_weapon"] = new("item.crit_weapon", "Crit Weapon", 0) { Slot = ItemSlot.Weapon }
        };
        var monsters = new Dictionary<string, MonsterDefinition>
        {
            ["monster.training_slime"] = new(
                "monster.training_slime", "Training Slime", 100, 10, 100,
                "loot.training_slime")
        };
        var lootTables = new Dictionary<string, LootTableDefinition>
        {
            ["loot.training_slime"] = new(
                "loot.training_slime", 1, 1,
                [new LootEntryDefinition("item.training_sword", 1)])
        };
        var affixes = new Dictionary<string, AffixDefinition>
        {
            ["health.high"] = new("health.high", "Vital", ItemStat.MaximumHealth, 50, 50, [ItemSlot.Armor]),
            ["health.low"] = new("health.low", "Frailty", ItemStat.MaximumHealth, -50, -50, [ItemSlot.Armor]),
            ["crit.max"] = new("crit.max", "Lucky", ItemStat.CriticalChance, 100, 100, [ItemSlot.Weapon]),
            ["crit.mid"] = new("crit.mid", "Fortunate", ItemStat.CriticalChance, 50, 50, [ItemSlot.Weapon])
        };
        return new GameContent(items, monsters, lootTables, affixes);
    }

    private sealed class CountingRandom : Random
    {
        public int CriticalRolls { get; private set; }

        public override int Next(int maxValue)
        {
            CriticalRolls++;
            return 0;
        }
    }
}
