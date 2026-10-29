using System.Reflection;
using System.Xml.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Game;

public sealed class FinalAcceptanceTests
{
    [Fact]
    public void SmokeInputs_CloseKillPickupLearnCraftEquipLoopWithoutGrantingGearOrKnowledge()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var artifacts = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(artifacts);
        var log = Path.Combine(artifacts, $"crafting-headless-{Guid.NewGuid():N}.log");
        var production = GameContentLoader.LoadDirectory(Path.Combine(root, "content", "definitions"));
        var smoke = new RuntimeSmokeTest(log);
        try
        {
            var (content, encounter) = smoke.CreateFixture(production);
            Assert.Equal(production.Materials, content.Materials);
            Assert.Equal(production.Recipes, content.Recipes);
            var arena = new ArenaSimulation(content, smoke.CreateRandom(), encounter);
            var inventory = new InventoryPanel();
            var smith = new BlacksmithPanel();
            var panels = new PanelController();
            var presentation = new ActorPresentation();
            presentation.Reset(arena);
            Assert.Empty(arena.Player.Inventory.Items);
            Assert.Empty(arena.Blacksmith.KnownRecipeIds);
            var sawPersistenceBoundary = false;
            for (var update = 0; update < 600 && (arena.ResetSequence == 0 || !arena.IsCleared); update++)
            {
                var input = smoke.NextInput(arena, inventory, smith, content, 1120, presentation.SwingRemaining);
                var frame = panels.Route(input, arena.Player, arena.BlacksmithOperations);
                Assert.False(frame.ExitRequested);
                inventory.SetOpen(frame.ActivePanel == PanelKind.Inventory);
                smith.SetOpen(frame.ActivePanel == PanelKind.Blacksmith);
                if (frame.HandlePanelActions)
                {
                    if (inventory.IsOpen)
                        inventory.Update(input, arena.Player, content, 1120, arena.EquipmentOperations);
                    else if (smith.IsOpen)
                        smith.Update(input, arena, content, 1120);
                }
                if (frame.AdvanceWorld)
                {
                    if (frame.RestartRequested)
                    {
                        arena.ResetEncounter();
                        presentation.Reset(arena);
                        smoke.AfterReset(arena, presentation);
                    }
                    arena.Update(new PlayerIntent(input.MoveDirection, input.AimWorldPosition,
                        input.AttackHeld || input.AttackPressed, input.PickupPressed), .1f);
                    presentation.Update(arena, .1f);
                }
                smoke.AfterUpdate(arena, inventory, smith, content, presentation.PlayerPose,
                    presentation.GetMonsterPose, presentation.SwingRemaining, presentation.HitTexts);
                sawPersistenceBoundary |= smoke.ReadyForPersistenceCheck;
            }
            Assert.True(sawPersistenceBoundary);
            Assert.Equal(2, arena.Player.Inventory.Items.Count);
            Assert.NotNull(arena.Player.Inventory.EquippedWeapon);
            Assert.NotNull(arena.Player.Inventory.EquippedArmor);
            Assert.Equal(50, arena.Player.CriticalChance);
            Assert.Equal(120, arena.Player.MaximumHealth);
            Assert.Equal(1, arena.ResetSequence);
            Assert.True(arena.IsCleared);
            Assert.NotEmpty(arena.Projectiles);
            Assert.Equal(new[] { "recipe.magic_blade", "recipe.magic_vest" },
                arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal));
            Assert.All(arena.Player.Inventory.Items, item =>
            {
                Assert.NotEqual(Guid.Empty, item.InstanceId);
                Assert.Equal(ItemRarity.Magic, item.Rarity);
            });

            var beforeMaterials = arena.Player.Resources.Materials.Values.Sum();
            var beforeRecipes = arena.Player.Resources.Recipes.Values.Sum();
            for (var pickupUpdate = 0; pickupUpdate < 200 && arena.GroundResources.Count > 0; pickupUpdate++)
            {
                var drop = arena.GroundResources[0];
                var movement = drop.Position - arena.Player.Position;
                arena.Update(new PlayerIntent(
                    movement.Length() > 18 ? System.Numerics.Vector2.Normalize(movement) : System.Numerics.Vector2.Zero,
                    drop.Position, false, true), .1f);
                presentation.Update(arena, .1f);
            }
            Assert.Empty(arena.GroundResources);
            Assert.True(arena.Player.Resources.Materials.Values.Sum() > beforeMaterials);
            Assert.True(arena.Player.Resources.Recipes.Values.Sum() > beforeRecipes);
            var materials = arena.Player.Resources.Materials.ToArray();
            var recipes = arena.Player.Resources.Recipes.ToArray();
            var knowledge = arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).ToArray();
            smoke.NextInput(arena, inventory, smith, content, 1120, presentation.SwingRemaining);
            arena.ResetEncounter();
            presentation.Reset(arena);
            smoke.AfterReset(arena, presentation);
            Assert.Equal(materials, arena.Player.Resources.Materials.ToArray());
            Assert.Equal(recipes, arena.Player.Resources.Recipes.ToArray());
            Assert.Equal(knowledge, arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal));

            smoke.NextInput(arena, inventory, smith, content, 1120, presentation.SwingRemaining);
            var lethal = arena.Monsters.First(monster => monster.Id == "monster.fast_wolf");
            var travel = lethal.Position - arena.Player.Position;
            arena.Update(new PlayerIntent(System.Numerics.Vector2.Normalize(travel), lethal.Position,
                false, false), travel.Length() / 180);
            presentation.Update(arena, .1f);
            Assert.True(arena.Player.IsDead);
            smoke.AfterUpdate(arena, inventory, smith, content, presentation.PlayerPose,
                presentation.GetMonsterPose, presentation.SwingRemaining, presentation.HitTexts);
            for (var tick = 0; tick < 2; tick++)
            {
                smoke.NextInput(arena, inventory, smith, content, 1120, presentation.SwingRemaining);
                arena.Update(new PlayerIntent(System.Numerics.Vector2.Zero, arena.Player.Position, false, false), .3f);
                presentation.Update(arena, .3f);
                smoke.AfterUpdate(arena, inventory, smith, content, presentation.PlayerPose,
                    presentation.GetMonsterPose, presentation.SwingRemaining, presentation.HitTexts);
            }
            Assert.False(arena.Player.IsDead);
            Assert.Equal(materials, arena.Player.Resources.Materials.ToArray());
            Assert.Equal(recipes, arena.Player.Resources.Recipes.ToArray());
            Assert.Equal(knowledge, arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal));
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void SmithFeedback_CoversEveryResultAndRejectsUnknownStatus()
    {
        Assert.All(Enum.GetValues<BlacksmithStatus>(), status =>
            Assert.False(string.IsNullOrWhiteSpace(GameText.SmithStatus(status))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameText.SmithStatus((BlacksmithStatus)999));
    }

    [Fact]
    public void FontAtlas_ContainsAllCentralizedTextAndResourceNames()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var font = XDocument.Load(Path.Combine(root, "src", "DarkLoot.Game", "Content", "Fonts", "UIFont.spritefont"),
            LoadOptions.PreserveWhitespace);
        var covered = font.Descendants("CharacterRegion").SelectMany(region =>
            Enumerable.Range(region.Element("Start")!.Value[0],
                region.Element("End")!.Value[0] - region.Element("Start")!.Value[0] + 1)).ToHashSet();
        var content = GameContentLoader.LoadDirectory(Path.Combine(root, "content", "definitions"));
        var crafted = content.Recipes.Values.Select(recipe =>
            new EquipmentFactory().Create(recipe.ItemId, recipe.Rarity, content, new Random(17))).ToArray();
        var texts = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(content.Materials.Values.Select(material => material.Name))
            .Concat(content.Recipes.Values.Select(recipe => recipe.Name))
            .Concat(content.Items.Values.Select(item => item.Name))
            .Concat(content.Monsters.Values.Select(monster => monster.Name))
            .Concat(content.Affixes.Values.Select(affix => affix.Name))
            .Concat(Enum.GetValues<ItemSlot>().Select(GameText.Slot))
            .Concat(Enum.GetValues<ItemRarity>().Select(GameText.Rarity))
            .Concat(Enum.GetValues<ItemStat>().Select(GameText.Stat))
            .Concat(Enum.GetValues<BlacksmithStatus>().Select(GameText.SmithStatus))
            .Concat(crafted.SelectMany(item => new[]
            {
                string.Format(GameText.Crafted,
                    ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content)),
                InventoryPanel.FormatRolls(item, content)
            }))
            .Concat(content.Recipes.Values.SelectMany(recipe => new[]
            {
                string.Format(GameText.Learned, recipe.Name),
                string.Format(GameText.RecipeResource, recipe.Name, int.MaxValue),
                string.Format(GameText.RecipeState, GameText.Known, int.MaxValue),
                string.Format(GameText.ShortageDetails, GameText.InsufficientMaterials,
                    string.Join("  ", recipe.Costs.Select(cost =>
                        string.Format(GameText.Shortage, content.Materials[cost.MaterialId].Name, cost.Quantity))))
            }.Concat(recipe.Costs.Select(cost => string.Format(GameText.MaterialCost,
                content.Materials[cost.MaterialId].Name, int.MaxValue, cost.Quantity)))))
            .Concat(content.Materials.Values.Select(material =>
                string.Format(GameText.MaterialResource, material.Name, int.MaxValue)));
        var missing = texts.SelectMany(text => text).Distinct().Where(character => !covered.Contains(character)).ToArray();
        Assert.True(missing.Length == 0, $"Missing glyphs: {new string(missing)}");
    }

    [Fact]
    public void ChineseDeathPresentation_ExplainsCountdownAndDisabledControls()
    {
        var strings = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Contains(strings, text => text.Contains("死亡") && text.Contains("复活") && text.Contains("{0"));
        Assert.Contains(strings, text => text.Contains("无法") && text.Contains("移动") &&
            text.Contains("攻击") && text.Contains("拾取"));
    }

    [Fact]
    public void ProductionWindowActivityGate_SkipsInactiveAndFirstReactivatedUpdate()
    {
        var gate = new WindowActivityGate();

        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(true));
        Assert.True(gate.CanAdvance(true));
        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(true));
        Assert.True(gate.CanAdvance(true));
    }
}
