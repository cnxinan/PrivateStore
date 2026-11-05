using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class HudRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, ArenaSimulation arena, int width, int height,
        GothicUiRenderer ui, double framesPerSecond, bool inventoryOpen, bool smithOpen,
        System.Numerics.Vector2 pointer, string? interactionFeedback = null,
        string? areaTitle = null, string? objective = null, bool journalOpen = false)
    {
        var layout = new UiLayout(width, height);
        ui.DrawConsole(batch, layout.Console);
        var dead = arena.Player.IsDead;
        var paused = inventoryOpen || smithOpen || journalOpen;
        ui.DrawHealthGlobe(batch, layout.Globe, (float)arena.Player.Health / arena.Player.MaximumHealth);
        var health = new Rectangle(layout.Globe.X + 18, layout.Globe.Bottom - 37, layout.Globe.Width - 36, 22);
        shapes.Rectangle(batch, health, new Color(5, 6, 5, 210));
        InventoryPanel.Text(batch, font, string.Format(GameText.HealthFraction, arena.Player.Health, arena.Player.MaximumHealth),
            health, GothicUiRenderer.Text, centered: true);
        ui.DrawWell(batch, layout.WorldStatus);
        InventoryPanel.Text(batch, font, (areaTitle ?? GameText.AreaTitle) + "  " +
            (arena.Map?.IsDungeon == false ? GameText.SafeArea :
                string.Format(GameText.Enemies, arena.RemainingMonsters, arena.Monsters.Count)),
            new Rectangle(layout.WorldStatus.X + 8, layout.WorldStatus.Y + 4, layout.WorldStatus.Width - 16, 22),
            GothicUiRenderer.Text);
        if (paused)
            InventoryPanel.Text(batch, font, GameText.PausedWorld,
                new Rectangle(layout.WorldStatus.X + 8, layout.WorldStatus.Y + 25, layout.WorldStatus.Width - 16, 20),
                GothicUiRenderer.Gold);
        var status = dead ? string.Format(GameText.DeathCountdown, arena.Player.DeathRemaining) :
            interactionFeedback ?? objective ?? (arena.IsCleared ? GameText.ConsoleCleared : GameText.ConsoleObjective);
        InventoryPanel.Text(batch, font, status, layout.Objective,
            dead || interactionFeedback is not null ? GothicUiRenderer.Red : GothicUiRenderer.Gold);
        InventoryPanel.Text(batch, font, string.Format(GameText.HudStats,
            arena.Player.AttackDamage, arena.Player.MaximumHealth, arena.Player.CriticalChance),
            layout.HudStats, GothicUiRenderer.Text);
        if (dead)
            InventoryPanel.Text(batch, font, MovementInstructions(arena), layout.HudHelp, GothicUiRenderer.Muted);
        InventoryPanel.Text(batch, font, string.Format(GameText.FrameRate, framesPerSecond),
            new Rectangle(width - 130, height - 30, 114, 18), GothicUiRenderer.Muted);
        DrawButton(layout.InventoryButton, GameText.InventoryButton, !dead, inventoryOpen);
        DrawButton(layout.QuestButton, GameText.QuestButton, !dead, journalOpen);
        DrawButton(layout.SmithButton, GameText.SmithButton,
            !dead && (smithOpen || arena.BlacksmithOperations.CanInteract(arena.Player)), smithOpen);
        DrawButton(layout.RestartButton, GameText.RestartButton, !dead && !paused, false);
        ui.DrawWell(batch, layout.QuestTracker);
        var quest = arena.Quests.Current;
        InventoryPanel.Text(batch, font, quest is null ? GameText.QuestComplete : GameText.QuestTitle(quest),
            new(layout.QuestTracker.X + 8, layout.QuestTracker.Y + 4, layout.QuestTracker.Width - 16, 22),
            GothicUiRenderer.Gold);
        QuestPanel.Wrapped(batch, font, quest is null ? GameText.QuestClaimed :
                !arena.Quests.Accepted ? GameText.QuestAvailable :
                arena.QuestOperations.IsReady(arena.Player) ? GameText.QuestReady :
                QuestPanel.Objective(arena, quest, arena.Quests.ClaimedCount),
            new(layout.QuestTracker.X + 8, layout.QuestTracker.Y + 30, layout.QuestTracker.Width - 16, 36),
            GothicUiRenderer.Text);

        void DrawButton(Rectangle bounds, string text, bool enabled, bool pressed)
        {
            ui.DrawButton(batch, bounds, enabled, bounds.Contains(pointer.X, pointer.Y), pressed);
            InventoryPanel.Text(batch, font, text, bounds,
                enabled ? GothicUiRenderer.Text : GothicUiRenderer.Muted, centered: true);
        }
    }

    internal static string PlayerHeader(ArenaSimulation arena, double framesPerSecond) =>
        arena.Player.IsDead
            ? string.Format(GameText.DeathHud, arena.Player.DeathRemaining, framesPerSecond)
            : string.Format(GameText.Hud, arena.Player.Health, arena.Player.MaximumHealth,
                arena.Player.AttackDamage, arena.Player.CriticalChance, framesPerSecond);

    internal static string MovementInstructions(ArenaSimulation arena) =>
        arena.Player.IsDead ? GameText.DeathControls : GameText.Movement;

    internal static string ControlInstructions(ArenaSimulation arena) =>
        arena.Player.IsDead ? GameText.DeathWorld : GameText.Controls;
}
