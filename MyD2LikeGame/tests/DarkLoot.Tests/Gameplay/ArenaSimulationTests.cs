using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class ArenaSimulationTests
{
    [Fact]
    public void Update_NormalizesDiagonalMovementAndKeepsPositionsFiniteAndBounded()
    {
        var simulation = new ArenaSimulation(CreateContent(), new Random(17));
        var start = simulation.Player.Position;

        simulation.Update(
            new PlayerIntent(new Vector2(1, 1), new Vector2(480, 270), false, false),
            1f);

        Assert.Equal(180f, Vector2.Distance(start, simulation.Player.Position), 3);
        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monster.Position);

        simulation.Update(
            new PlayerIntent(
                new Vector2(float.NaN, float.PositiveInfinity),
                new Vector2(float.NaN, float.NegativeInfinity),
                false,
                false),
            1000f);

        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monster.Position);
    }

    [Fact]
    public void Update_AttacksOnlyWhenMonsterIsInRangeAndFacingArc()
    {
        var simulation = new ArenaSimulation(CreateContent(), new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, new Vector2(480, 350), true, false),
            1f);
        Assert.Equal(12, simulation.Monster.Health);

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monster.Position, true, false),
            0.27f);

        Assert.Equal(2, simulation.Monster.Health);
    }

    [Fact]
    public void Update_EnforcesPlayerAttackCooldown()
    {
        var simulation = new ArenaSimulation(CreateContent(), new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monster.Position, false, false),
            1f);
        simulation.Update(
            new PlayerIntent(Vector2.UnitX, simulation.Monster.Position, true, false),
            0.35f);
        Assert.Equal(2, simulation.Monster.Health);

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monster.Position, true, false),
            0.1f);
        Assert.Equal(2, simulation.Monster.Health);

        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Monster.Position, true, false),
            0.25f);
        Assert.True(simulation.Monster.IsDefeated);
    }

    [Fact]
    public void Update_HandlesCoincidentPlayerAndMonsterAndCoincidentAim()
    {
        var simulation = new ArenaSimulation(CreateContent(), new Random(17));

        simulation.Update(
            new PlayerIntent(Vector2.UnitX, new Vector2(480, 270), false, false),
            100f);

        Assert.Equal(simulation.Player.Position, simulation.Monster.Position);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, true, false),
            0f);

        Assert.Equal(2, simulation.Monster.Health);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, true, false),
            0.35f);
        Assert.True(simulation.Monster.IsDefeated);
        AssertFiniteAndBounded(simulation.Player.Position);
        AssertFiniteAndBounded(simulation.Monster.Position);
    }

    [Fact]
    public void Update_KillsOncePicksUpAndEquipsLootThenResetKeepsWeaponDamage()
    {
        var content = CreateContent();
        var simulation = new ArenaSimulation(content, new Random(17));

        var unarmedAttackCount = AdvanceUntilDefeated(simulation);
        Assert.Equal(2, unarmedAttackCount);

        Assert.True(simulation.Monster.IsDefeated);
        var drop = Assert.Single(simulation.GroundItems);
        Assert.Equal("item.training_sword", drop.Item.DefinitionId);

        for (var index = 0; index < 20; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.Zero, simulation.Monster.Position, true, false),
                0.1f);
        }

        Assert.Single(simulation.GroundItems);
        Assert.Empty(simulation.Player.Inventory.Items);

        simulation.Update(
            new PlayerIntent(Vector2.UnitY, simulation.Monster.Position, false, false),
            1f);
        simulation.Update(
            new PlayerIntent(Vector2.Zero, simulation.Player.Position, false, true),
            0f);
        Assert.Single(simulation.GroundItems);
        Assert.Empty(simulation.Player.Inventory.Items);

        simulation.Update(
            new PlayerIntent(-Vector2.UnitY, simulation.Monster.Position, false, true),
            1f);
        Assert.Empty(simulation.GroundItems);
        var weapon = Assert.Single(simulation.Player.Inventory.Items);

        var inventoryService = new InventoryService();
        Assert.True(inventoryService.TryEquip(
            simulation.Player.Inventory, weapon.InstanceId, content));
        Assert.Equal(13, simulation.Player.AttackDamage);

        simulation.ResetEncounter();
        Assert.Equal(12, simulation.Monster.Health);
        Assert.False(simulation.Monster.IsDefeated);
        Assert.Same(weapon, simulation.Player.Inventory.EquippedWeapon);
        Assert.Equal(13, simulation.Player.AttackDamage);

        var equippedAttackCount = AdvanceUntilDefeated(simulation);

        Assert.True(simulation.Monster.IsDefeated);
        Assert.Equal(1, equippedAttackCount);
        Assert.Single(simulation.GroundItems);
    }

    [Fact]
    public void Update_RespawnsPlayerAndMonsterAndClearsDropsWithoutLosingEquipment()
    {
        var content = CreateContent();
        var simulation = new ArenaSimulation(content, new Random(17));
        var inventoryService = new InventoryService();
        var sword = new ItemInstance(Guid.NewGuid(), "item.training_sword");
        Assert.True(inventoryService.TryPickUp(simulation.Player.Inventory, sword));
        Assert.True(inventoryService.TryEquip(
            simulation.Player.Inventory, sword.InstanceId, content));

        AdvanceUntilDefeated(simulation);
        Assert.Single(simulation.GroundItems);
        simulation.ResetEncounter();
        Assert.Single(simulation.GroundItems);

        var tookDamage = false;
        for (var index = 0; index < 1000; index++)
        {
            simulation.Update(
                new PlayerIntent(Vector2.Zero, simulation.Monster.Position, false, false),
                0.1f);

            tookDamage |= simulation.Player.Health < 100;
            if (tookDamage && simulation.Player.Health == 100)
            {
                break;
            }
        }

        Assert.Equal(100, simulation.Player.Health);
        Assert.Equal(new Vector2(160, 270), simulation.Player.Position);
        Assert.Equal(new Vector2(480, 270), simulation.Monster.Position);
        Assert.False(simulation.Monster.IsDefeated);
        Assert.Empty(simulation.GroundItems);
        Assert.Same(sword, simulation.Player.Inventory.EquippedWeapon);
        Assert.Equal(13, simulation.Player.AttackDamage);
    }

    [Fact]
    public void Update_LimitsMonsterContactDamageToOneHitPerCooldown()
    {
        var content = CreateContent(contactDamage: 2, monsterSpeed: 100);
        var simulation = new ArenaSimulation(content, new Random(17));

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
    public void Constructor_ReportsMissingTrainingMonsterId()
    {
        var content = new GameContent(
            new Dictionary<string, ItemDefinition>(),
            new Dictionary<string, MonsterDefinition>(),
            new Dictionary<string, LootTableDefinition>());

        var error = Assert.Throws<ArgumentException>(
            () => new ArenaSimulation(content, new Random(17)));

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
            () => new ArenaSimulation(content, new Random(17)));

        Assert.Contains("loot.training_slime", error.Message);
    }

    private static int AdvanceUntilDefeated(ArenaSimulation simulation)
    {
        var attackCount = 0;
        for (var index = 0; index < 1000 && !simulation.Monster.IsDefeated; index++)
        {
            var delta = simulation.Monster.Position - simulation.Player.Position;
            var direction = delta.LengthSquared() <= 48 * 48
                ? Vector2.Zero
                : Vector2.Normalize(delta);
            var previousHealth = simulation.Monster.Health;

            simulation.Update(
                new PlayerIntent(
                    direction,
                    simulation.Monster.Position,
                    true,
                    false),
                0.05f);

            if (simulation.Monster.Health < previousHealth)
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
}
