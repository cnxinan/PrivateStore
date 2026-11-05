using System.Numerics;
using DarkLoot.Engine.Input;
using DarkLoot.Game;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Quests;
using DarkLoot.Tests.Gameplay;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class QuestPanelTests
{
    private static readonly GameInputSnapshot Idle =
        new(System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitX, System.Numerics.Vector2.Zero,
            false, false, false, false, false);

    [Fact]
    public void Journal_SwapsWithInventoryWithoutClosingSmithAndConsumesTransitionInput()
    {
        var arena = QuestServiceTests.CreateArena();
        var controller = new PanelController();
        var layout = new UiLayout(1120);
        controller.Route(Idle with { InteractPressed = true }, arena.Player, arena.BlacksmithOperations);
        var journal = Route(Click(layout.QuestButton) with { RestartPressed = true, AttackHeld = true });
        Assert.True(journal.JournalOpen);
        Assert.True(journal.BlacksmithOpen);
        Assert.False(journal.InventoryOpen);
        Assert.False(journal.AdvanceWorld);
        Assert.False(journal.HandlePanelActions);
        Assert.False(journal.RestartRequested);
        var paused = Route(Idle with { MoveDirection = System.Numerics.Vector2.UnitX, AttackHeld = true });
        Assert.False(paused.AdvanceWorld);
        Assert.True(paused.BlockWorldInput);
        var inventory = Route(Idle with { InventoryTogglePressed = true });
        Assert.True(inventory.InventoryOpen);
        Assert.False(inventory.JournalOpen);
        Assert.True(inventory.BlacksmithOpen);
        Assert.False(inventory.AdvanceWorld);
        Route(Click(layout.QuestButton));
        var close = Route(Click(layout.InventoryClose));
        Assert.False(close.JournalOpen);
        Assert.Equal(PanelKind.Blacksmith, close.ActivePanel);
        Assert.False(close.AdvanceWorld);

        PanelFrame Route(GameInputSnapshot input) => controller.Route(input, arena.Player, arena.BlacksmithOperations);
    }

    [Fact]
    public void Journal_OpensFarFromSmithButCannotAcceptAndClosingCannotAttackOrRestart()
    {
        var arena = QuestServiceTests.CreateArena();
        arena.Player.Position = new(700, 270);
        var controller = new PanelController();
        var layout = new UiLayout(1120);
        var open = controller.Route(Click(layout.QuestButton), arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.Journal, open.ActivePanel);
        Assert.False(open.AdvanceWorld);
        var journal = new QuestPanel();
        journal.Update(Click(layout.AcceptQuestButton), 1120);
        Assert.False(arena.Quests.Accepted);
        var close = controller.Route(Idle with
        {
            ClosePanelPressed = true,
            AttackPressed = true,
            AttackHeld = true,
            RestartPressed = true
        }, arena.Player, arena.BlacksmithOperations);
        Assert.Equal(PanelKind.None, close.ActivePanel);
        Assert.False(close.AdvanceWorld);
        Assert.False(close.HandlePanelActions);
        Assert.False(close.RestartRequested);
    }

    [Fact]
    public void CommissionTab_AcceptsAndClaimsManuallyWithoutClickThroughToNextQuest()
    {
        var arena = QuestServiceTests.CreateArena();
        var panel = new BlacksmithPanel();
        var layout = new UiLayout(1120);
        panel.SetOpen(true);
        panel.Update(Click(layout.CommissionTab), arena, PersistenceFixture.LoadContent(), 1120);
        Assert.Equal(BlacksmithTab.Commissions, panel.Tab);
        Assert.False(arena.Quests.Accepted);
        panel.Update(Click(layout.AcceptQuestButton), arena, PersistenceFixture.LoadContent(), 1120);
        Assert.True(arena.Quests.Accepted);
        QuestServiceTests.Kill(arena);
        arena.Player.Position = BlacksmithService.Position;
        panel.Update(Click(layout.TurnInQuestButton), arena, PersistenceFixture.LoadContent(), 1120);
        Assert.Equal(1, arena.Quests.ClaimedCount);
        Assert.False(arena.Quests.Accepted);
        panel.Update(Click(layout.TurnInQuestButton), arena, PersistenceFixture.LoadContent(), 1120);
        Assert.Equal(QuestStatus.NotAccepted, panel.LastQuestResult);
        Assert.False(arena.Quests.Accepted);
        panel.Update(Click(layout.CraftingTab), arena, PersistenceFixture.LoadContent(), 1120);
        Assert.Equal(BlacksmithTab.Craft, panel.Tab);
    }

    private static GameInputSnapshot Click(Rectangle bounds) => Idle with
    {
        AttackPressed = true,
        PointerScreenPosition = new System.Numerics.Vector2(bounds.Center.X, bounds.Center.Y)
    };
}
