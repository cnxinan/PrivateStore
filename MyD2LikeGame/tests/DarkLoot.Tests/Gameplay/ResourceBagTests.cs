using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class ResourceBagTests
{
    [Fact]
    public void PickUp_StacksResourcesAndDeduplicatesByDropId()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var pickup = new ResourcePickupService(content);
        var dropId = Guid.NewGuid();

        Assert.Equal(ResourcePickupResult.Applied,
            pickup.PickUp(arena.Player, Drop(dropId, ResourceKind.Material, "material.bone", 2)));
        Assert.Equal(ResourcePickupResult.AlreadyCollected,
            pickup.PickUp(arena.Player, Drop(dropId, ResourceKind.Material, "material.bone", 5)));
        Assert.Equal(ResourcePickupResult.Applied,
            pickup.PickUp(arena.Player, Drop(Guid.NewGuid(), ResourceKind.Material, "material.bone", 3)));

        Assert.Equal(5, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.Empty(arena.Player.Resources.Recipes);
    }

    [Fact]
    public void PickUp_OverflowLeavesCountAndReceiptUnchangedAndCanRetry()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var pickup = new ResourcePickupService(content);
        var learnableId = Guid.NewGuid();
        var overflowingId = Guid.NewGuid();

        Assert.Equal(ResourcePickupResult.Applied,
            pickup.PickUp(arena.Player, Drop(learnableId, ResourceKind.Recipe, "recipe.magic_blade", int.MaxValue)));
        Assert.Throws<OverflowException>(() =>
            pickup.PickUp(arena.Player, Drop(overflowingId, ResourceKind.Recipe, "recipe.magic_blade", 1)));
        Assert.Equal(int.MaxValue,
            arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));

        var smith = new BlacksmithService(
            content, new Random(17), arena.EquipmentOperations, new BlacksmithState());
        Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(ResourcePickupResult.Applied,
            pickup.PickUp(arena.Player, Drop(overflowingId, ResourceKind.Recipe, "recipe.magic_blade", 1)));
        Assert.Equal(int.MaxValue,
            arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));
    }

    [Fact]
    public void PublicResourceCollectionsCannotBeMutated()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var pickup = new ResourcePickupService(content);
        Assert.Equal(ResourcePickupResult.Applied,
            pickup.PickUp(arena.Player, Drop(Guid.NewGuid(), ResourceKind.Material, "material.bone", 1)));

        var materials = Assert.IsAssignableFrom<IDictionary<string, int>>(arena.Player.Resources.Materials);
        Assert.Throws<NotSupportedException>(() => materials.Add("material.slime", 1));
        Assert.Throws<ArgumentException>(() =>
            arena.Player.Resources.GetCount(ResourceKind.Material, string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            arena.Player.Resources.GetCount((ResourceKind)int.MaxValue, "material.bone"));
        Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, "material.unknown"));
    }

    [Fact]
    public void PickUp_InvalidDropThrowsWithoutChangingResources()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var pickup = new ResourcePickupService(content);
        var validId = Guid.NewGuid();

        Assert.Throws<ArgumentNullException>(() => pickup.PickUp(null!, Drop(validId, ResourceKind.Material, "material.bone", 1)));
        Assert.Throws<ArgumentNullException>(() => pickup.PickUp(arena.Player, null!));
        Assert.Throws<ArgumentException>(() =>
            pickup.PickUp(arena.Player, Drop(Guid.Empty, ResourceKind.Material, "material.bone", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pickup.PickUp(arena.Player, Drop(validId, (ResourceKind)int.MaxValue, "material.bone", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pickup.PickUp(arena.Player, Drop(validId, ResourceKind.Material, "material.bone", 0)));
        Assert.Throws<ArgumentException>(() =>
            pickup.PickUp(arena.Player, Drop(validId, ResourceKind.Material, "material.unknown", 1)));
        Assert.Throws<ArgumentException>(() =>
            pickup.PickUp(arena.Player, new GroundResource(
                validId, new ResourceStack(ResourceKind.Material, "material.bone", 1),
                new Vector2(float.NaN, 0))));

        Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
    }

    private static GroundResource Drop(Guid id, ResourceKind kind, string definitionId, int quantity) =>
        new(id, new ResourceStack(kind, definitionId, quantity), new Vector2(160, 270));

    private static GameContent LoadProductionContent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "content", "definitions", "recipes", "magic-blade.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return GameContentLoader.LoadDirectory(Path.Combine(directory!.FullName, "content", "definitions"));
    }
}
