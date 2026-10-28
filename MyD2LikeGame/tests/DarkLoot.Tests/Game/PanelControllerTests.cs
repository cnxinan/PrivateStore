using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Input;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;

namespace DarkLoot.Tests.Game;

public sealed class PanelControllerTests
{
    private static readonly GameInputSnapshot Idle =
        new(Vector2.Zero, Vector2.UnitX, Vector2.Zero, false, false, false, false, false);

    [Fact]
    public void Escape_ClosesWithoutCombatRestartOrExitThenExitsOnNextEdge()
    {
        var arena = CreateArena();
        var controller = new PanelController();
        var opened = controller.Route(Idle with { InteractPressed = true }, arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.Blacksmith, opened.ActivePanel);
        Assert.False(opened.AdvanceWorld);
        Assert.False(opened.HandlePanelActions);
        var closing = controller.Route(Idle with
        {
            ExitPressed = true,
            AttackHeld = true,
            AttackPressed = true,
            RestartPressed = true,
            InventoryTogglePressed = true,
            InteractPressed = true
        }, arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.None, closing.ActivePanel);
        Assert.False(closing.AdvanceWorld);
        Assert.False(closing.HandlePanelActions);
        Assert.False(closing.RestartRequested);
        Assert.False(closing.ExitRequested);
        Assert.True(controller.Route(Idle with { ExitPressed = true },
            arena.Player, arena.BlacksmithOperations).ExitRequested);
    }

    [Fact]
    public void InventoryAndSmith_AreExclusiveAndInventoryWinsSimultaneousKeys()
    {
        var arena = CreateArena();
        var controller = new PanelController();
        var both = Idle with { InventoryTogglePressed = true, InteractPressed = true };
        Assert.Equal(PanelKind.Inventory, Route(both).ActivePanel);
        Assert.Equal(PanelKind.Blacksmith, Route(Idle with { InteractPressed = true }).ActivePanel);
        Assert.False(Route(Idle with { RestartPressed = true }).RestartRequested);
        var stable = Route(Idle with { AttackPressed = true });
        Assert.True(stable.HandlePanelActions);
        Assert.False(stable.AdvanceWorld);
        Assert.Equal(PanelKind.Inventory, Route(both).ActivePanel);
        var close = Route(Idle with { InventoryTogglePressed = true, AttackHeld = true });
        Assert.Equal(PanelKind.None, close.ActivePanel);
        Assert.False(close.AdvanceWorld);
        Assert.False(close.HandlePanelActions);

        PanelFrame Route(GameInputSnapshot input) => controller.Route(input, arena.Player, arena.BlacksmithOperations);
    }

    [Fact]
    public void SmithToggle_ClosesWithoutClickThrough()
    {
        var arena = CreateArena();
        var controller = new PanelController();
        Assert.Equal(PanelKind.Blacksmith, controller.Route(Idle with { InteractPressed = true },
            arena.Player, arena.BlacksmithOperations).ActivePanel);
        var closed = controller.Route(Idle with { InteractPressed = true, AttackPressed = true },
            arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.None, closed.ActivePanel);
        Assert.False(closed.AdvanceWorld);
        Assert.False(closed.HandlePanelActions);
    }

    [Fact]
    public void FarInteraction_ReturnsFeedbackWithoutReplacingInventory()
    {
        var arena = CreateArena();
        arena.Update(new PlayerIntent(Vector2.UnitX, Vector2.UnitX, false, false), 1);
        var controller = new PanelController();
        var refused = controller.Route(Idle with { InteractPressed = true }, arena.Player, arena.BlacksmithOperations);
        Assert.Equal(BlacksmithStatus.TooFar, refused.Feedback);
        Assert.Equal(PanelKind.None, refused.ActivePanel);
        Assert.True(refused.AdvanceWorld);
        controller.Route(Idle with { InventoryTogglePressed = true }, arena.Player, arena.BlacksmithOperations);
        refused = controller.Route(Idle with { InteractPressed = true, RestartPressed = true },
            arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.Inventory, refused.ActivePanel);
        Assert.False(refused.AdvanceWorld);
        Assert.False(refused.RestartRequested);
        Assert.False(refused.HandlePanelActions);
    }

    [Fact]
    public void NoPanel_AllowsHeldAttackAndRestart()
    {
        var arena = CreateArena();
        var frame = new PanelController().Route(Idle with { AttackHeld = true, RestartPressed = true },
            arena.Player, arena.BlacksmithOperations);
        Assert.True(frame.AdvanceWorld);
        Assert.True(frame.RestartRequested);
        Assert.False(frame.HandlePanelActions);
    }

    [Fact]
    public void Death_ClosesStalePanelIgnoresTogglesAndRestartButWorldAndExitWork()
    {
        var content = LoadContent();
        var monsters = content.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
        monsters["monster.training_slime"] = monsters["monster.training_slime"] with { ContactDamage = 100 };
        var arena = new ArenaSimulation(content with { Monsters = monsters }, new Random(17),
            new EncounterDefinition("lethal", [new("contact", "monster.training_slime", 160, 270)]));
        var controller = new PanelController();
        controller.Route(Idle with { InventoryTogglePressed = true }, arena.Player, arena.BlacksmithOperations);
        arena.Update(new PlayerIntent(Vector2.Zero, Vector2.UnitX, false, false), 0);
        Assert.True(arena.Player.IsDead);
        var dead = controller.Route(Idle with
        {
            InventoryTogglePressed = true,
            InteractPressed = true,
            RestartPressed = true
        }, arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.None, dead.ActivePanel);
        Assert.True(dead.AdvanceWorld);
        Assert.False(dead.HandlePanelActions);
        Assert.False(dead.RestartRequested);
        Assert.True(controller.Route(Idle with { ExitPressed = true },
            arena.Player, arena.BlacksmithOperations).ExitRequested);
    }

    private static ArenaSimulation CreateArena() => new(LoadContent(), new Random(17));

    private static GameContent LoadContent() => GameContentLoader.LoadDirectory(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content", "definitions"));
}
