using System.Text.Json;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class SaveSessionTests
{
    [Fact]
    public void Session_SavesEveryProgressMutationAndIndependentSessionRestoresIt()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var session = new SaveSession(content, files.Path);
        session.Load(arena);
        session.SaveIfChanged(arena);
        Assert.False(File.Exists(files.Path));
        foreach (var id in content.Materials.Keys)
        {
            PersistenceFixture.PickUp(arena, ResourceKind.Material, id, 20);
            session.SaveIfChanged(arena);
            AssertSaved();
        }
        PersistenceFixture.PickUp(arena, ResourceKind.Recipe, "recipe.magic_blade", 2);
        session.SaveIfChanged(arena);
        AssertSaved();
        Assert.Equal(BlacksmithStatus.Applied, arena.BlacksmithOperations.Learn(arena.Player, "recipe.magic_blade").Status);
        session.SaveIfChanged(arena);
        AssertSaved();
        var craft = arena.BlacksmithOperations.Craft(arena.Player, "recipe.magic_blade");
        Assert.Equal(BlacksmithStatus.Applied, craft.Status);
        session.SaveIfChanged(arena);
        AssertSaved();
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, craft.Item!.InstanceId));
        session.SaveIfChanged(arena);
        AssertSaved();
        session.SaveOnExit(arena);
        var restored = new ArenaSimulation(content, new Random(99));
        new SaveSession(content, files.Path).Load(restored);
        Assert.Equal(arena.Player.Inventory.EquippedWeaponId, restored.Player.Inventory.EquippedWeaponId);
        Assert.Equal(arena.Player.Stats, restored.Player.Stats);
        Assert.Equal(restored.Player.MaximumHealth, restored.Player.Health);

        void AssertSaved()
        {
            var expected = new ProgressPersistence(content).Capture(arena);
            Assert.Equal(JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(new JsonSaveStore(files.Path).Load()));
        }
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("version")]
    [InlineData("invalid-progress")]
    public void Session_FailedLoadCannotOverwriteOriginalOnExit(string defect)
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var snapshot = new ProgressPersistence(content).Capture(PersistenceFixture.CreateProgress(content));
        if (defect == "version")
            snapshot = snapshot with { Version = 99 };
        else if (defect == "invalid-progress")
            snapshot = snapshot with { KnownRecipeIds = ["removed-recipe"] };
        if (defect == "malformed")
            File.WriteAllText(files.Path, "{");
        else
            new JsonSaveStore(files.Path).Save(snapshot);
        var before = File.ReadAllBytes(files.Path);
        var session = new SaveSession(content, files.Path);

        var error = Assert.Throws<SaveGameException>(() => session.Load(arena));
        Assert.Contains(files.Path, error.Message);
        session.SaveOnExit(arena);

        Assert.Equal(before, File.ReadAllBytes(files.Path));
        Assert.Empty(arena.Player.Inventory.Items);
        Assert.Empty(arena.Player.Resources.Materials);
        Assert.Empty(arena.Blacksmith.KnownRecipeIds);
    }

    [Fact]
    public void Session_UnchangedFramesDoNotWriteButExitDoesAndFailedSaveDisablesExitWrite()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var session = new SaveSession(content, files.Path);
        session.Load(arena);
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 1);
        session.SaveIfChanged(arena);
        var original = File.ReadAllBytes(files.Path);
        using (var locked = new FileStream(files.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            for (var frame = 0; frame < 10; frame++)
                session.SaveIfChanged(arena);
            Assert.Throws<SaveGameException>(() => session.SaveOnExit(arena));
        }
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 1);
        session.SaveOnExit(arena);
        Assert.Equal(original, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Session_FailedMutationSaveCannotWriteAgainDuringExit()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var session = new SaveSession(content, files.Path);
        session.Load(arena);
        session.SaveOnExit(arena);
        var original = File.ReadAllBytes(files.Path);
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 1);
        using (var locked = new FileStream(files.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<SaveGameException>(() => session.SaveIfChanged(arena));

        session.SaveOnExit(arena);

        Assert.Equal(original, File.ReadAllBytes(files.Path));
    }
}
