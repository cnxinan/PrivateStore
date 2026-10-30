using System.Numerics;
using DarkLoot.Engine.Input;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Tests.Gameplay;

namespace DarkLoot.Tests.Game;

public sealed class GothicUiInteractionTests
{
    private static readonly GameInputSnapshot Idle =
        new(Vector2.Zero, Vector2.UnitX, Vector2.Zero, false, false, false, false, false);

    [Fact]
    public void Smith_OpensBothAndIndependentTogglesKeepWorldFrozen()
    {
        var arena = new ArenaSimulation(PersistenceFixture.LoadContent(), new Random(17));
        var controller = new PanelController();
        var opened = Route(Idle with { InteractPressed = true });
        Assert.True(opened.InventoryOpen);
        Assert.True(opened.BlacksmithOpen);
        Assert.Equal(PanelKind.Blacksmith, opened.ActivePanel);
        Assert.False(opened.AdvanceWorld);
        var onlySmith = Route(Idle with { InventoryTogglePressed = true });
        Assert.False(onlySmith.InventoryOpen);
        Assert.True(onlySmith.BlacksmithOpen);
        Assert.False(onlySmith.AdvanceWorld);
        var both = Route(Idle with { InventoryTogglePressed = true });
        Assert.Equal(PanelKind.Inventory, both.ActivePanel);
        Assert.True(both.BlacksmithOpen);
        var onlyInventory = Route(Idle with { InteractPressed = true });
        Assert.True(onlyInventory.InventoryOpen);
        Assert.False(onlyInventory.BlacksmithOpen);
        Assert.False(onlyInventory.AdvanceWorld);
        var closed = Route(Idle with { InventoryTogglePressed = true });
        Assert.False(closed.InventoryOpen);
        Assert.False(closed.BlacksmithOpen);
        Assert.False(closed.AdvanceWorld);
        Assert.True(Route(Idle).AdvanceWorld);

        PanelFrame Route(GameInputSnapshot input) => controller.Route(input, arena.Player, arena.BlacksmithOperations);
    }

    [Fact]
    public void MostRecentClickChangesClosePriorityButHoverDoesNot()
    {
        var arena = new ArenaSimulation(PersistenceFixture.LoadContent(), new Random(17));
        var controller = new PanelController();
        var layout = new UiLayout(1120);
        Route(Idle with { InteractPressed = true });
        Assert.Equal(PanelKind.Blacksmith, Route(Idle with
        {
            PointerScreenPosition = Point(layout.Inventory.Center)
        }).ActivePanel);
        Assert.Equal(PanelKind.Inventory, Route(Click(layout.Inventory)).ActivePanel);
        var closed = Route(Idle with { ClosePanelPressed = true, RestartPressed = true, InteractPressed = true });
        Assert.False(closed.InventoryOpen);
        Assert.True(closed.BlacksmithOpen);
        Assert.False(closed.HandlePanelActions);
        Assert.False(closed.AdvanceWorld);
        Assert.False(closed.RestartRequested);
        closed = Route(Idle with { ExitPressed = true });
        Assert.Equal(PanelKind.None, closed.ActivePanel);
        Assert.False(closed.ExitRequested);
        Assert.False(closed.AdvanceWorld);
        Assert.True(Route(Idle with { ExitPressed = true }).ExitRequested);

        PanelFrame Route(GameInputSnapshot input) => controller.Route(input, arena.Player, arena.BlacksmithOperations);
    }

    [Fact]
    public void ConsoleAndPanelCloseButtonsUseSameRulesAndDoNotLeakWorldInput()
    {
        var arena = new ArenaSimulation(PersistenceFixture.LoadContent(), new Random(17));
        var controller = new PanelController();
        var layout = new UiLayout(1120);
        var opened = Route(Click(layout.SmithButton) with { PickupPressed = true, AttackHeld = true });
        Assert.True(opened.InventoryOpen);
        Assert.True(opened.BlacksmithOpen);
        Assert.False(opened.AdvanceWorld);
        var closeRight = Route(Click(layout.InventoryClose));
        Assert.False(closeRight.InventoryOpen);
        Assert.True(closeRight.BlacksmithOpen);
        Assert.False(closeRight.HandlePanelActions);
        Route(Click(layout.BlacksmithClose));
        var pointer = Route(Click(layout.Console) with
        {
            MoveDirection = Vector2.UnitX,
            PickupPressed = true,
            RestartPressed = true
        });
        Assert.True(pointer.AdvanceWorld);
        Assert.True(pointer.BlockWorldInput);
        Assert.True(pointer.PointerOverUi);
        Assert.False(pointer.RestartRequested);
        var restart = Route(Click(layout.RestartButton));
        Assert.True(restart.RestartRequested);
        Assert.True(restart.BlockWorldInput);

        PanelFrame Route(GameInputSnapshot input) => controller.Route(input, arena.Player, arena.BlacksmithOperations);
    }

    [Fact]
    public void GlobeAboveConsoleConsumesPointerActionsWithoutRestartOrCombatBleed()
    {
        var arena = new ArenaSimulation(PersistenceFixture.LoadContent(), new Random(17));
        var controller = new PanelController();
        var layout = new UiLayout(1120);
        var input = Idle with
        {
            PointerScreenPosition = new Vector2(layout.Globe.Center.X, layout.Console.Top - 10),
            AttackPressed = true,
            AttackHeld = true,
            PickupPressed = true,
            RestartPressed = true,
            MoveDirection = Vector2.UnitX
        };
        var frame = controller.Route(input, arena.Player, arena.BlacksmithOperations);
        Assert.True(frame.AdvanceWorld);
        Assert.True(frame.PointerOverUi);
        Assert.True(frame.BlockWorldInput);
        Assert.False(frame.RestartRequested);
    }

    [Fact]
    public void SharedLayout_ContainsAllCellsAndSeparatesPanelsFromConsole()
    {
        var layout = new UiLayout(1120);
        Assert.False(layout.Inventory.Intersects(layout.Blacksmith));
        Assert.False(layout.Inventory.Intersects(layout.Console));
        Assert.False(layout.Blacksmith.Intersects(layout.Console));
        var cells = Enumerable.Range(0, 32).Select(layout.Cell).ToArray();
        Assert.All(cells, cell => Assert.True(layout.Inventory.Contains(cell)));
        for (var index = 0; index < cells.Length; index++)
            Assert.All(cells.Skip(index + 1), other => Assert.False(cells[index].Intersects(other)));
        Assert.True(layout.Inventory.Contains(layout.InventoryClose));
        Assert.True(layout.Blacksmith.Contains(layout.BlacksmithClose));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Cell(32));
    }

    private static Vector2 Point(Microsoft.Xna.Framework.Point point) => new(point.X, point.Y);
    private static GameInputSnapshot Click(Microsoft.Xna.Framework.Rectangle bounds) =>
        Idle with { PointerScreenPosition = Point(bounds.Center), AttackPressed = true };
}
