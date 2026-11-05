using System;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Quests;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class QuestPanel
{
    public int SelectedIndex { get; private set; }

    public void Update(GameInputSnapshot input, int width)
    {
        if (!input.AttackPressed)
            return;
        var layout = new UiLayout(width);
        for (var index = 0; index < SmithQuests.All.Count; index++)
            if (layout.QuestRow(index).Contains(input.PointerScreenPosition.X, input.PointerScreenPosition.Y))
                SelectedIndex = index;
    }

    public void Draw(SpriteBatch batch, SpriteFont font, ArenaSimulation arena, GameContent content,
        int width, GothicUiRenderer ui)
    {
        var layout = new UiLayout(width);
        var bounds = layout.Inventory;
        ui.DrawPanel(batch, bounds);
        InventoryPanel.Text(batch, font, GameText.QuestJournal,
            new(bounds.X + 20, bounds.Y + 8, 320, 28), GothicUiRenderer.Gold);
        InventoryPanel.CloseButton(batch, font, ui, layout.InventoryClose);
        for (var index = 0; index < SmithQuests.All.Count; index++)
        {
            var row = layout.QuestRow(index);
            ui.DrawWell(batch, row, selected: SelectedIndex == index);
            InventoryPanel.Text(batch, font, GameText.QuestTitle(SmithQuests.All[index]),
                new(row.X + 10, row.Y + 3, row.Width - 20, 22), GothicUiRenderer.Gold);
            InventoryPanel.Text(batch, font, Status(arena, index),
                new(row.X + 10, row.Y + 25, row.Width - 20, 18), GothicUiRenderer.Muted);
        }
        DrawDetails(batch, font, ui, arena, content, SmithQuests.All[SelectedIndex], SelectedIndex,
            new(bounds.X + 20, bounds.Y + 246, bounds.Width - 40, 260));
        Wrapped(batch, font, GameText.QuestJournalHelp,
            new(bounds.X + 20, bounds.Bottom - 42, bounds.Width - 40, 34), GothicUiRenderer.Muted);
    }

    internal static void DrawCommission(SpriteBatch batch, SpriteFont font, GothicUiRenderer ui,
        ArenaSimulation arena, GameContent content, UiLayout layout)
    {
        if (arena.Quests.Current is { } quest)
            DrawDetails(batch, font, ui, arena, content, quest, arena.Quests.ClaimedCount,
                new(layout.Blacksmith.X + 18, layout.Blacksmith.Y + 78, 324, 290));
        else
            Wrapped(batch, font, GameText.QuestComplete,
                new(layout.Blacksmith.X + 24, layout.Blacksmith.Y + 96, 312, 96), GothicUiRenderer.Gold);
    }

    internal static string Status(ArenaSimulation arena, int index) =>
        index < arena.Quests.ClaimedCount ? GameText.QuestClaimed :
        index > arena.Quests.ClaimedCount ? GameText.QuestLocked :
        !arena.Quests.Accepted ? GameText.QuestAvailable :
        arena.QuestOperations.IsReady(arena.Player) ? GameText.QuestReady : GameText.QuestActive;

    internal static string Objective(ArenaSimulation arena, QuestDefinition quest, int index)
    {
        var claimed = index < arena.Quests.ClaimedCount;
        var current = index == arena.Quests.ClaimedCount && arena.Quests.Accepted;
        var count = claimed ? quest.Required : !current ? 0 :
            quest.Objective == QuestObjective.Materials
                ? arena.Player.Resources.GetCount(ResourceKind.Material, quest.Costs[0].MaterialId)
                : arena.Quests.Kills;
        return string.Format(quest.Objective switch
        {
            QuestObjective.Kills => GameText.QuestKills,
            QuestObjective.Materials => GameText.QuestBones,
            QuestObjective.Guardian => GameText.QuestBoss,
            _ => throw new ArgumentOutOfRangeException(nameof(quest))
        }, count, quest.Required);
    }

    internal static string Marker(ArenaSimulation arena) =>
        arena.Quests.Current is null ? string.Empty : !arena.Quests.Accepted ? "!" :
        arena.QuestOperations.IsReady(arena.Player) ? "?" : string.Empty;

    private static void DrawDetails(SpriteBatch batch, SpriteFont font, GothicUiRenderer ui,
        ArenaSimulation arena, GameContent content, QuestDefinition quest, int index, Rectangle bounds)
    {
        InventoryPanel.Text(batch, font, GameText.QuestTitle(quest),
            new(bounds.X, bounds.Y, bounds.Width, 28), GothicUiRenderer.Gold);
        InventoryPanel.Text(batch, font, Status(arena, index),
            new(bounds.X, bounds.Y + 32, bounds.Width, 22), GothicUiRenderer.Muted);
        Wrapped(batch, font, GameText.QuestStory(quest),
            new(bounds.X, bounds.Y + 64, bounds.Width, 72), GothicUiRenderer.Text);
        ui.DrawWell(batch, new(bounds.X, bounds.Y + 142, bounds.Width, 30));
        InventoryPanel.Text(batch, font, Objective(arena, quest, index),
            new(bounds.X + 8, bounds.Y + 146, bounds.Width - 16, 22), GothicUiRenderer.Gold);
        InventoryPanel.Text(batch, font, GameText.QuestRewards,
            new(bounds.X, bounds.Y + 180, bounds.Width, 22), GothicUiRenderer.Muted);
        Wrapped(batch, font, string.Join("  ", quest.Rewards.Select(reward => string.Format(
                GameText.QuestRewardLine, reward.Kind == ResourceKind.Material
                    ? content.Materials[reward.DefinitionId].Name : content.Recipes[reward.DefinitionId].Name,
                reward.Quantity))),
            new(bounds.X, bounds.Y + 206, bounds.Width, 64), GothicUiRenderer.Text);
    }

    internal static void Wrapped(SpriteBatch batch, SpriteFont font, string text, Rectangle bounds, Color color)
    {
        var y = bounds.Y;
        var lineHeight = (int)Math.Ceiling(font.LineSpacing * .85f);
        foreach (var line in ItemTooltip.Wrap(text, value => font.MeasureString(value).X * .85f, bounds.Width))
        {
            if (y + lineHeight > bounds.Bottom)
                break;
            batch.DrawString(font, line, new(bounds.X, y), color, 0, Vector2.Zero, .85f, SpriteEffects.None, 0);
            y += lineHeight;
        }
    }
}
