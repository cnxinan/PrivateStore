using DarkLoot.Data;
using DarkLoot.Engine.Input;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class CraftingPanelTests
{
    private const int Width = 1120;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SmithButtons_LearnAndCraftThroughServicesAndShowActualRolls(int row)
    {
        var (arena, content) = Create();
        var panel = new BlacksmithPanel();
        var recipe = content.Recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).ElementAt(row);
        Give(arena, ResourceKind.Recipe, recipe.Id, 2);
        foreach (var cost in recipe.Costs)
            Give(arena, ResourceKind.Material, cost.MaterialId, cost.Quantity);
        panel.Update(Click(panel.LearnButtonBounds(Width, row)), arena, content, Width);
        Assert.False(arena.Blacksmith.HasLearned(recipe.Id));
        panel.SetOpen(true);
        panel.Update(Click(panel.RowBounds(Width, row)), arena, content, Width);
        Assert.Equal(row, panel.SelectedRecipeIndex);
        Assert.Empty(arena.Blacksmith.KnownRecipeIds);
        Assert.Empty(arena.Player.Inventory.Items);
        Assert.True(panel.CanLearn(arena, recipe));
        Assert.False(panel.CanCraft(arena, recipe));
        panel.Update(Click(panel.LearnButtonBounds(Width, row)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.Applied, panel.LastResult!.Status);
        Assert.True(arena.Blacksmith.HasLearned(recipe.Id));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, recipe.Id));
        Assert.False(panel.CanLearn(arena, recipe));
        Assert.True(panel.CanCraft(arena, recipe));
        panel.Update(Click(panel.CraftButtonBounds(Width, row)), arena, content, Width);
        var item = Assert.Single(arena.Player.Inventory.Items);
        Assert.Same(item, panel.LastResult!.Item);
        Assert.Contains(ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content), panel.Feedback);
        Assert.Equal(InventoryPanel.FormatRolls(item, content), panel.CraftedRolls);
        Assert.Null(arena.Player.Inventory.EquippedWeapon);
        Assert.Null(arena.Player.Inventory.EquippedArmor);
        Assert.All(recipe.Costs, cost => Assert.Equal(0,
            arena.Player.Resources.GetCount(ResourceKind.Material, cost.MaterialId)));
        Assert.False(panel.CanCraft(arena, recipe));
    }

    [Fact]
    public void Smith_DeadPlayerGetsServiceRefusalAndNoLearnOrCraft()
    {
        var (_, content) = Create();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with { ContactDamage = 100 };
        var arena = new ArenaSimulation(content with { Monsters = monsters }, new Random(17),
            new DarkLoot.Data.Definitions.EncounterDefinition("lethal",
                [new("contact", "monster.training_slime", 160, 270)]));
        var panel = new BlacksmithPanel();
        panel.SetOpen(true);
        arena.Update(new PlayerIntent(System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitX, false, false), 0);
        panel.Update(Click(panel.LearnButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.PlayerDead, panel.LastResult!.Status);
        Assert.Equal(GameText.SmithStatus(BlacksmithStatus.PlayerDead), panel.Feedback);
        panel.Update(Click(panel.CraftButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.PlayerDead, panel.LastResult!.Status);
        Assert.Empty(arena.Blacksmith.KnownRecipeIds);
        Assert.Empty(arena.Player.Inventory.Items);
    }

    [Fact]
    public void SmithRefusals_ShowExactServiceStatusAndEachMaterialShortage()
    {
        var (arena, content) = Create();
        var panel = new BlacksmithPanel();
        panel.SetOpen(true);
        var recipe = content.Recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).First();
        panel.Update(Click(panel.LearnButtonBounds(Width, 0)), arena, content, Width);
        Assert.NotNull(panel.LastResult);
        Assert.Equal(BlacksmithStatus.RecipeNotOwned, panel.LastResult!.Status);
        Assert.Equal(GameText.SmithStatus(BlacksmithStatus.RecipeNotOwned), panel.Feedback);
        panel.Update(Click(panel.CraftButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.NotLearned, panel.LastResult!.Status);
        Give(arena, ResourceKind.Recipe, recipe.Id, 1);
        panel.Update(Click(panel.LearnButtonBounds(Width, 0)), arena, content, Width);
        panel.Update(Click(panel.LearnButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.AlreadyLearned, panel.LastResult!.Status);
        panel.Update(Click(panel.CraftButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.InsufficientMaterials, panel.LastResult!.Status);
        foreach (var cost in recipe.Costs)
            Assert.Contains(string.Format(GameText.Shortage, content.Materials[cost.MaterialId].Name, cost.Quantity),
                panel.Feedback);
        arena.Update(new PlayerIntent(System.Numerics.Vector2.UnitX, System.Numerics.Vector2.UnitX, false, false), 1);
        panel.Update(Click(panel.CraftButtonBounds(Width, 0)), arena, content, Width);
        Assert.Equal(BlacksmithStatus.TooFar, panel.LastResult!.Status);
        Assert.Equal(GameText.SmithStatus(BlacksmithStatus.TooFar), panel.Feedback);
        Assert.Empty(arena.Player.Inventory.Items);
    }

    [Fact]
    public void Inventory_HasOneExternalOpenStateAndResourceClicksNeverEquip()
    {
        var (arena, content) = Create();
        var panel = new InventoryPanel();
        var item = new EquipmentFactory().Create("weapon.training_blade", DarkLoot.Data.Definitions.ItemRarity.Magic,
            content, new Random(17));
        arena.EquipmentOperations.PickUp(arena.Player, item);
        var toggle = Click(default) with { InventoryTogglePressed = true, AttackPressed = false };
        panel.Update(toggle, arena.Player, content, Width, arena.EquipmentOperations);
        Assert.False(panel.IsOpen);
        panel.SetOpen(true);
        panel.Update(toggle, arena.Player, content, Width, arena.EquipmentOperations);
        Assert.True(panel.IsOpen);
        Give(arena, ResourceKind.Material, "material.bone", 3);
        Give(arena, ResourceKind.Recipe, "recipe.magic_blade", 1);
        Assert.Equal(2, InventoryPanel.ResourceRows(arena.Player, content).Count);
        panel.Update(Click(panel.ResourceTabBounds(Width)), arena.Player, content, Width, arena.EquipmentOperations);
        Assert.Equal(InventoryPage.Resources, panel.Page);
        panel.Update(Click(panel.RowBounds(Width, 0)), arena.Player, content, Width, arena.EquipmentOperations);
        Assert.Null(arena.Player.Inventory.EquippedWeapon);
        arena.BlacksmithOperations.Learn(arena.Player, "recipe.magic_blade");
        var onlyMaterial = Assert.Single(InventoryPanel.ResourceRows(arena.Player, content));
        Assert.Contains(content.Materials["material.bone"].Name, onlyMaterial);
        Assert.Contains("3", onlyMaterial);
        panel.Update(Click(panel.EquipmentTabBounds(Width)), arena.Player, content, Width, arena.EquipmentOperations);
        panel.Update(Click(panel.RowBounds(Width, 0)), arena.Player, content, Width, arena.EquipmentOperations);
        Assert.Equal(item.InstanceId, arena.Player.Inventory.EquippedWeaponId);
        panel.SetOpen(false);
        panel.Update(Click(panel.ResourceTabBounds(Width)), arena.Player, content, Width, arena.EquipmentOperations);
        Assert.Equal(InventoryPage.Equipment, panel.Page);
    }

    private static GameInputSnapshot Click(Rectangle bounds) => new(
        System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitX,
        new System.Numerics.Vector2(bounds.Center.X, bounds.Center.Y), true, false, false, false, false);

    private static void Give(ArenaSimulation arena, ResourceKind kind, string id, int quantity) =>
        Assert.Equal(ResourcePickupResult.Applied, arena.ResourceOperations.PickUp(arena.Player,
            new GroundResource(Guid.NewGuid(), new ResourceStack(kind, id, quantity), arena.Player.Position)));

    private static (ArenaSimulation Arena, GameContent Content) Create()
    {
        var content = GameContentLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "content", "definitions"));
        return (new ArenaSimulation(content, new Random(17)), content);
    }
}
