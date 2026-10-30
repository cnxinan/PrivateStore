using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

public enum PanelKind { None, Inventory, Blacksmith }

public readonly record struct PanelFrame(
    PanelKind ActivePanel, bool AdvanceWorld, bool HandlePanelActions,
    bool ExitRequested, bool RestartRequested, BlacksmithStatus? Feedback,
    bool InventoryOpen = false, bool BlacksmithOpen = false,
    bool BlockWorldInput = false, bool PointerOverUi = false);

public sealed class PanelController
{
    public PanelKind ActivePanel { get; private set; }
    public bool InventoryOpen { get; private set; }
    public bool BlacksmithOpen { get; private set; }

    public PanelFrame Route(GameInputSnapshot input, PlayerState player, BlacksmithService smith,
        int width = 1120, int height = 720)
    {
        var layout = new UiLayout(width, height);
        var pointer = new Point((int)input.PointerScreenPosition.X, (int)input.PointerScreenPosition.Y);
        bool Click(Rectangle bounds) => input.AttackPressed && bounds.Contains(pointer);
        var overUi = layout.Console.Contains(pointer) || layout.Globe.Contains(pointer) ||
            InventoryOpen && layout.Inventory.Contains(pointer) ||
            BlacksmithOpen && layout.Blacksmith.Contains(pointer);
        PanelFrame Frame(bool world = false, bool actions = false, bool exit = false,
            bool restart = false, BlacksmithStatus? feedback = null, bool block = true) =>
            new(ActivePanel, world, actions, exit, restart, feedback,
                InventoryOpen, BlacksmithOpen, block, overUi);

        if (player.IsDead)
        {
            InventoryOpen = BlacksmithOpen = false;
            ActivePanel = PanelKind.None;
            return Frame(world: !input.ExitPressed, exit: input.ExitPressed);
        }

        if (input.ExitPressed)
        {
            var exit = ActivePanel == PanelKind.None;
            Close(ActivePanel);
            return Frame(exit: exit);
        }

        if (input.ClosePanelPressed && ActivePanel != PanelKind.None)
        {
            Close(ActivePanel);
            return Frame();
        }

        if (InventoryOpen && Click(layout.InventoryClose))
        {
            Close(PanelKind.Inventory);
            return Frame();
        }
        if (BlacksmithOpen && Click(layout.BlacksmithClose))
        {
            Close(PanelKind.Blacksmith);
            return Frame();
        }

        if (input.InventoryTogglePressed || Click(layout.InventoryButton))
        {
            if (InventoryOpen)
                Close(PanelKind.Inventory);
            else
            {
                InventoryOpen = true;
                ActivePanel = PanelKind.Inventory;
            }
            return Frame();
        }

        if (input.InteractPressed || Click(layout.SmithButton))
        {
            if (BlacksmithOpen)
                Close(PanelKind.Blacksmith);
            else if (smith.CanInteract(player))
            {
                InventoryOpen = BlacksmithOpen = true;
                ActivePanel = PanelKind.Blacksmith;
            }
            else
            {
                return Frame(world: ActivePanel == PanelKind.None, feedback: BlacksmithStatus.TooFar,
                    block: Click(layout.SmithButton));
            }
            return Frame();
        }

        if (input.AttackPressed)
        {
            if (InventoryOpen && layout.Inventory.Contains(pointer))
                ActivePanel = PanelKind.Inventory;
            else if (BlacksmithOpen && layout.Blacksmith.Contains(pointer))
                ActivePanel = PanelKind.Blacksmith;
        }
        var world = ActivePanel == PanelKind.None;
        var consumed = overUi &&
            (input.AttackPressed || input.AttackHeld || input.ClosePanelPressed || input.ScrollDelta != 0);
        return Frame(world: world, actions: !world,
            restart: world && (Click(layout.RestartButton) || input.RestartPressed && !consumed), block: consumed);
    }

    private void Close(PanelKind panel)
    {
        if (panel == PanelKind.Inventory)
            InventoryOpen = false;
        else if (panel == PanelKind.Blacksmith)
            BlacksmithOpen = false;
        ActivePanel = InventoryOpen ? PanelKind.Inventory :
            BlacksmithOpen ? PanelKind.Blacksmith : PanelKind.None;
    }
}
