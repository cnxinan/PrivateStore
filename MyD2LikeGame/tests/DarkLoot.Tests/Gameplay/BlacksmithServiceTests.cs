using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class BlacksmithServiceTests
{
    [Fact]
    public void LearnThenCraft_ConsumesOnceAndProducesNewUnequippedInstance()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var state = new BlacksmithState();
        var smith = new BlacksmithService(content, new Random(17), arena.EquipmentOperations, state);
        Give(arena, content, ResourceKind.Recipe, "recipe.magic_blade", 2);
        Give(arena, content, ResourceKind.Material, "material.bone", 4);
        Give(arena, content, ResourceKind.Material, "material.slime", 2);

        Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.AlreadyLearned, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        var knownRecipeIds = Assert.IsAssignableFrom<IList<string>>(state.KnownRecipeIds);
        Assert.Throws<NotSupportedException>(() => knownRecipeIds.Add("recipe.rare_blade"));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));

        var first = smith.Craft(arena.Player, "recipe.magic_blade");
        var second = smith.Craft(arena.Player, "recipe.magic_blade");

        Assert.Equal(BlacksmithStatus.Applied, first.Status);
        Assert.Equal(BlacksmithStatus.Applied, second.Status);
        Assert.NotNull(first.Item);
        Assert.NotNull(second.Item);
        Assert.NotEqual(first.Item.InstanceId, second.Item.InstanceId);
        Assert.Equal(ItemRarity.Magic, first.Item.Rarity);
        Assert.Equal(2, arena.Player.Inventory.Items.Count);
        Assert.Null(arena.Player.Inventory.EquippedWeaponId);
        Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.Equal(100, arena.Player.Health);
    }

    [Fact]
    public void Craft_AllProductionRecipesConsumeTheirExactCostsAndCreateConfiguredItems()
    {
        var content = LoadProductionContent();

        foreach (var recipe in content.Recipes.Values)
        {
            var arena = new ArenaSimulation(content, new Random(17));
            var smith = new BlacksmithService(content, new Random(17), arena.EquipmentOperations, new BlacksmithState());
            Give(arena, content, ResourceKind.Recipe, recipe.Id, 1);
            foreach (var cost in recipe.Costs)
            {
                Give(arena, content, ResourceKind.Material, cost.MaterialId, cost.Quantity);
            }

            Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, recipe.Id).Status);
            var result = smith.Craft(arena.Player, recipe.Id);

            Assert.Equal(BlacksmithStatus.Applied, result.Status);
            Assert.NotNull(result.Item);
            Assert.Equal(recipe.ItemId, result.Item.DefinitionId);
            Assert.Equal(recipe.Rarity, result.Item.Rarity);
            Assert.All(recipe.Costs, cost =>
                Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, cost.MaterialId)));
            Assert.Single(arena.Player.Inventory.Items);
        }
    }

    [Fact]
    public void RefusedOperationsDoNotConsumeRandomnessOrChangeOwnership()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var state = new BlacksmithState();
        var random = new CountingRandom();
        var smith = new BlacksmithService(content, random, arena.EquipmentOperations, state);

        Assert.Equal(BlacksmithStatus.RecipeNotOwned, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.NotLearned, smith.Craft(arena.Player, "recipe.magic_blade").Status);
        Give(arena, content, ResourceKind.Recipe, "recipe.magic_blade", 1);
        Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Give(arena, content, ResourceKind.Recipe, "recipe.magic_blade", 1);
        Assert.Equal(BlacksmithStatus.AlreadyLearned, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.InsufficientMaterials, smith.Craft(arena.Player, "recipe.magic_blade").Status);

        arena.Update(new PlayerIntent(Vector2.UnitX, new Vector2(400, 270), false, false), 1);
        Assert.False(smith.CanInteract(arena.Player));
        Assert.Equal(BlacksmithStatus.TooFar, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.TooFar, smith.Craft(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(0, random.Calls);
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));
        Assert.Empty(arena.Player.Inventory.Items);
    }

    [Fact]
    public void DeadPlayer_CannotLearnCraftOrPickUpResources()
    {
        var content = LoadProductionContent();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with { ContactDamage = 100 };
        var lethalContent = content with { Monsters = monsters };
        var encounter = new EncounterDefinition("lethal-test",
            [new MonsterSpawnDefinition("contact", "monster.training_slime", 160, 270)]);
        var arena = new ArenaSimulation(lethalContent, new Random(17), encounter);
        var state = new BlacksmithState();
        var random = new CountingRandom();
        var smith = new BlacksmithService(lethalContent, random, arena.EquipmentOperations, state);
        var pickup = new ResourcePickupService(lethalContent);
        Give(arena, lethalContent, ResourceKind.Recipe, "recipe.magic_blade", 2);
        Give(arena, lethalContent, ResourceKind.Material, "material.bone", 2);
        Give(arena, lethalContent, ResourceKind.Material, "material.slime", 1);
        Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        arena.Update(new PlayerIntent(Vector2.Zero, arena.Player.Position, false, false), 0);

        Assert.True(arena.Player.IsDead);
        Assert.Equal(BlacksmithStatus.PlayerDead, smith.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.PlayerDead, smith.Craft(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(ResourcePickupResult.PlayerDead,
            pickup.PickUp(arena.Player,
                new GroundResource(Guid.NewGuid(),
                    new ResourceStack(ResourceKind.Material, "material.bone", 1), arena.Player.Position)));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.True(state.HasLearned("recipe.magic_blade"));
        Assert.Empty(arena.Player.Inventory.Items);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void Craft_RandomFailureLeavesResourcesKnowledgeEquipmentAndHealthUnchanged()
    {
        var content = LoadProductionContent();
        var arena = new ArenaSimulation(content, new Random(17));
        var state = new BlacksmithState();
        var random = new ThrowingRandom();
        var smith = new BlacksmithService(content, random, arena.EquipmentOperations, state);
        Give(arena, content, ResourceKind.Recipe, "recipe.magic_blade", 1);
        Give(arena, content, ResourceKind.Material, "material.bone", 2);
        Give(arena, content, ResourceKind.Material, "material.slime", 1);
        Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);

        Assert.Throws<InvalidOperationException>(() => smith.Craft(arena.Player, "recipe.magic_blade"));

        Assert.True(state.HasLearned("recipe.magic_blade"));
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Material, "material.slime"));
        Assert.Empty(arena.Player.Inventory.Items);
        Assert.Equal(100, arena.Player.Health);
    }

    private static void Give(
        ArenaSimulation arena, GameContent content, ResourceKind kind, string id, int quantity)
    {
        var result = new ResourcePickupService(content).PickUp(arena.Player,
            new GroundResource(Guid.NewGuid(), new ResourceStack(kind, id, quantity), arena.Player.Position));
        Assert.Equal(ResourcePickupResult.Applied, result);
    }

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

    private sealed class CountingRandom : Random
    {
        public int Calls { get; private set; }

        public override long NextInt64(long maxValue)
        {
            Calls++;
            return 0;
        }

        public override long NextInt64(long minValue, long maxValue)
        {
            Calls++;
            return minValue;
        }
    }

    private sealed class ThrowingRandom : Random
    {
        public override long NextInt64(long maxValue) => throw new InvalidOperationException("scripted failure");
    }
}
