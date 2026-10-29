using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;

namespace DarkLoot.Game;

public enum PanelKind { None, Inventory, Blacksmith }

public readonly record struct PanelFrame(
    PanelKind ActivePanel, bool AdvanceWorld, bool HandlePanelActions,
    bool ExitRequested, bool RestartRequested, BlacksmithStatus? Feedback);

public sealed class PanelController
{
    public PanelKind ActivePanel { get; private set; }

    public PanelFrame Route(GameInputSnapshot input, PlayerState player, BlacksmithService smith)
    {
        if (player.IsDead)
        {
            ActivePanel = PanelKind.None;
            return new(ActivePanel, !input.ExitPressed, false, input.ExitPressed, false, null);
        }

        if (input.ExitPressed)
        {
            var exit = ActivePanel == PanelKind.None;
            ActivePanel = PanelKind.None;
            return new(ActivePanel, false, false, exit, false, null);
        }

        if (input.ClosePanelPressed && ActivePanel != PanelKind.None)
        {
            ActivePanel = PanelKind.None;
            return new(ActivePanel, false, false, false, false, null);
        }

        if (input.InventoryTogglePressed)
        {
            ActivePanel = ActivePanel == PanelKind.Inventory ? PanelKind.None : PanelKind.Inventory;
            return new(ActivePanel, false, false, false, false, null);
        }

        if (input.InteractPressed)
        {
            if (ActivePanel == PanelKind.Blacksmith)
            {
                ActivePanel = PanelKind.None;
            }
            else if (smith.CanInteract(player))
            {
                ActivePanel = PanelKind.Blacksmith;
            }
            else
            {
                return new(ActivePanel, ActivePanel == PanelKind.None, false, false, false, BlacksmithStatus.TooFar);
            }
            return new(ActivePanel, false, false, false, false, null);
        }

        var world = ActivePanel == PanelKind.None;
        return new(ActivePanel, world, !world, false, world && input.RestartPressed, null);
    }
}
