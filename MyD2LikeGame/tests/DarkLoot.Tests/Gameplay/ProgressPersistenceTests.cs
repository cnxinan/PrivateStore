using System.Numerics;
using System.Text.Json;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class ProgressPersistenceTests
{
    [Fact]
    public void Restore_PreservesExactProgressButStartsFreshAtFullEquippedHealth()
    {
        var content = PersistenceFixture.LoadContent();
        var source = PersistenceFixture.CreateProgress(content);
        var persistence = new ProgressPersistence(content);
        var snapshot = persistence.Capture(source);
        source.Player.Health = 1;
        source.Player.Position = new Vector2(700, 400);
        source.Update(new PlayerIntent(Vector2.Zero, Vector2.Zero, true, false), .1f);
        var target = new ArenaSimulation(content, new Random(91));

        persistence.Restore(target, snapshot);

        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(persistence.Capture(target)));
        Assert.Equal(new Vector2(160, 270), target.Player.Position);
        Assert.True(target.Player.MaximumHealth > 100);
        Assert.Equal(target.Player.MaximumHealth, target.Player.Health);
        Assert.Equal(0, target.Player.AttackSequence);
        Assert.Equal(0, target.ResetSequence);
        Assert.Equal(6, target.RemainingMonsters);
        Assert.Empty(target.GroundResources);
        Assert.Empty(target.Projectiles);
        Assert.Empty(target.Events);
        Assert.Equal(ResourcePickupResult.Applied, target.ResourceOperations.PickUp(target.Player,
            new GroundResource(PersistenceFixture.ReceiptId,
                new ResourceStack(ResourceKind.Material, "material.bone", 1), target.Player.Position)));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("null-items")]
    [InlineData("duplicate-id")]
    [InlineData("empty-id")]
    [InlineData("missing-definition")]
    [InlineData("rarity")]
    [InlineData("roll-count")]
    [InlineData("roll-value")]
    [InlineData("null-roll")]
    [InlineData("missing-equipped")]
    [InlineData("wrong-slot")]
    [InlineData("null-materials")]
    [InlineData("unknown-material")]
    [InlineData("zero-material")]
    [InlineData("negative-recipe")]
    [InlineData("unknown-recipe")]
    [InlineData("null-knowledge")]
    [InlineData("unknown-knowledge")]
    [InlineData("duplicate-knowledge")]
    public void Restore_InvalidSnapshotRejectsEntireCandidateWithoutPartialMutation(string defect)
    {
        var content = PersistenceFixture.LoadContent();
        var persistence = new ProgressPersistence(content);
        var snapshot = persistence.Capture(PersistenceFixture.CreateProgress(content));
        var item = snapshot.Items[0];
        var broken = defect switch
        {
            "version" => snapshot with { Version = 99 },
            "null-items" => snapshot with { Items = null! },
            "duplicate-id" => snapshot with { Items = [item, item] },
            "empty-id" => snapshot with { Items = [item with { InstanceId = Guid.Empty }] },
            "missing-definition" => snapshot with { Items = [item with { DefinitionId = "missing" }] },
            "rarity" => snapshot with { Items = [item with { Rarity = (ItemRarity)99 }] },
            "roll-count" => snapshot with { Items = [item with { Rolls = [] }] },
            "roll-value" => snapshot with
            {
                Items = [item with { Rolls = item.Rolls.Select(roll => roll with { Value = int.MaxValue }).ToArray() }]
            },
            "null-roll" => snapshot with { Items = [item with { Rolls = [null!] }] },
            "missing-equipped" => snapshot with { EquippedWeaponId = Guid.NewGuid() },
            "wrong-slot" => snapshot with { EquippedWeaponId = snapshot.EquippedArmorId },
            "null-materials" => snapshot with { Materials = null! },
            "unknown-material" => snapshot with { Materials = new Dictionary<string, int> { ["missing"] = 1 } },
            "zero-material" => snapshot with { Materials = new Dictionary<string, int> { ["material.bone"] = 0 } },
            "negative-recipe" => snapshot with { Recipes = new Dictionary<string, int> { ["recipe.magic_blade"] = -1 } },
            "unknown-recipe" => snapshot with { Recipes = new Dictionary<string, int> { ["missing"] = 1 } },
            "null-knowledge" => snapshot with { KnownRecipeIds = null! },
            "unknown-knowledge" => snapshot with { KnownRecipeIds = ["missing"] },
            "duplicate-knowledge" => snapshot with { KnownRecipeIds = ["recipe.magic_blade", "recipe.magic_blade"] },
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };
        var target = new ArenaSimulation(content, new Random(91));
        var before = JsonSerializer.Serialize(persistence.Capture(target));

        var error = Assert.Throws<SaveGameException>(() => persistence.Restore(target, broken));

        Assert.NotEmpty(error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Capture(target)));
        Assert.Equal(100, target.Player.Health);
    }

    [Fact]
    public void Capture_IsDetachedFromSubsequentMutationsAndRejectsRestoringOverLiveProgress()
    {
        var content = PersistenceFixture.LoadContent();
        var source = PersistenceFixture.CreateProgress(content);
        var persistence = new ProgressPersistence(content);
        var snapshot = persistence.Capture(source);
        var encoded = JsonSerializer.Serialize(snapshot);
        PersistenceFixture.PickUp(source, ResourceKind.Material, "material.bone", 1);
        Assert.Equal(BlacksmithStatus.Applied,
            source.BlacksmithOperations.Craft(source.Player, "recipe.rare_blade").Status);

        Assert.Equal(encoded, JsonSerializer.Serialize(snapshot));
        var before = JsonSerializer.Serialize(persistence.Capture(source));
        Assert.Throws<InvalidOperationException>(() => persistence.Restore(source, snapshot));
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Capture(source)));

        var started = new ArenaSimulation(content, new Random(91));
        started.Update(new PlayerIntent(Vector2.Zero, Vector2.UnitX, false, false), .1f);
        Assert.Throws<InvalidOperationException>(() => persistence.Restore(started, snapshot));
    }
}

internal static class PersistenceFixture
{
    public static readonly Guid ReceiptId = Guid.Parse("429fcd58-4052-4c33-ace7-925d6691ec4c");

    public static GameContent LoadContent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "content", "definitions", "recipes", "magic-blade.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return GameContentLoader.LoadDirectory(Path.Combine(directory.FullName, "content", "definitions"));
    }

    public static ArenaSimulation CreateProgress(GameContent content)
    {
        var arena = new ArenaSimulation(content, new Random(17));
        foreach (var id in content.Materials.Keys)
            PickUp(arena, ResourceKind.Material, id, 30, id == "material.bone" ? ReceiptId : null);
        foreach (var id in new[] { "recipe.rare_blade", "recipe.magic_vest" })
        {
            PickUp(arena, ResourceKind.Recipe, id, 2);
            Assert.Equal(BlacksmithStatus.Applied, arena.BlacksmithOperations.Learn(arena.Player, id).Status);
            var crafted = arena.BlacksmithOperations.Craft(arena.Player, id);
            Assert.Equal(BlacksmithStatus.Applied, crafted.Status);
            Assert.Equal(EquipmentChangeResult.Applied,
                arena.EquipmentOperations.Equip(arena.Player, crafted.Item!.InstanceId));
        }
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player,
            new ItemInstance(Guid.NewGuid(), "weapon.training_blade")));
        return arena;
    }

    public static void PickUp(
        ArenaSimulation arena, ResourceKind kind, string id, int quantity, Guid? dropId = null) =>
        Assert.Equal(ResourcePickupResult.Applied, arena.ResourceOperations.PickUp(arena.Player,
            new GroundResource(dropId ?? Guid.NewGuid(), new ResourceStack(kind, id, quantity), arena.Player.Position)));
}
