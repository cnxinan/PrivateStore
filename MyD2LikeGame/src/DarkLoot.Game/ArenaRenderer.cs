using System;
using System.Collections.Generic;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed class ArenaRenderer
{
    private readonly ActorRenderer _actors = new();

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        ArenaSimulation arena, GameContent content, Camera2D camera, ActorPresentation presentation,
        GothicRoomRenderer room)
    {
        room.Draw(batch, camera);

        var smithPosition = camera.WorldToScreen(BlacksmithService.Position);
        shapes.Ellipse(batch, smithPosition + new Vector2(2, 22), new Vector2(31, 8), 0, Color.Black * .35f);
        shapes.Circle(batch, smithPosition + new Vector2(-14, -12), 9, Color.BurlyWood);
        shapes.Line(batch, smithPosition + new Vector2(-14, -2), smithPosition + new Vector2(-14, 18),
            Color.SaddleBrown, 12);
        shapes.Rectangle(batch, new Rectangle((int)smithPosition.X, (int)smithPosition.Y, 30, 8), Color.SlateGray);
        shapes.Rectangle(batch, new Rectangle((int)smithPosition.X + 8, (int)smithPosition.Y + 8, 14, 12), Color.DimGray);
        if (arena.BlacksmithOperations.CanInteract(arena.Player))
            GameText.Draw(batch, font, GameText.SmithPrompt,
                smithPosition + new Vector2(-30, -42), Color.Gold, 220);

        var lootRows = new List<Rectangle>(arena.GroundResources.Count);
        var viewport = batch.GraphicsDevice.Viewport;
        var lootBounds = new Rectangle(8, 70, viewport.Width - 16,
            new UiLayout(viewport.Width, viewport.Height).Console.Top - 78);
        foreach (var drop in arena.GroundResources)
        {
            var position = camera.WorldToScreen(drop.Position);
            var (name, color) = drop.Stack.Kind switch
            {
                ResourceKind.Material => (content.Materials[drop.Stack.DefinitionId].Name, Color.LightGray),
                ResourceKind.Recipe => (content.Recipes[drop.Stack.DefinitionId].Name, Color.MediumPurple),
                _ => throw new ArgumentOutOfRangeException(nameof(drop), drop.Stack.Kind, "Unknown resource kind.")
            };
            var label = string.Format(GameText.Pickup, name, drop.Stack.Quantity);
            var labelWidth = Math.Min(400, font.MeasureString(label).X);
            var row = LootLabelLayout.Place(
                new Rectangle((int)position.X - 13, (int)position.Y - 13,
                    (int)Math.Ceiling(labelWidth) + 38, Math.Max(28, font.LineSpacing + 4)),
                lootRows, lootBounds);
            lootRows.Add(row);
            var iconPosition = new Vector2(row.X + 13, row.Center.Y);
            shapes.Line(batch, position, iconPosition, new Color(83, 91, 83), 1);
            shapes.Rectangle(batch, row, new Color(15, 20, 29, 220));
            shapes.Circle(batch, iconPosition, 13, new Color(52, 48, 36));
            shapes.Line(batch, iconPosition - new Vector2(7, 7), iconPosition + new Vector2(7, 7), color, 5);
            GameText.Draw(batch, font, label,
                new Vector2(row.X + 32, row.Y + 2), color, row.Width - 38);
        }

        foreach (var monster in arena.Monsters)
        {
            var monsterPosition = camera.WorldToScreen(monster.Position);
            var kind = monster.Behavior == MonsterBehavior.Archer ? ActorVisualKind.Archer :
                monster.Id == "monster.fast_wolf" ? ActorVisualKind.Wolf : ActorVisualKind.Slime;
            var pose = presentation.GetMonsterPose(monster.InstanceId);
            shapes.Ellipse(batch, monsterPosition + new Vector2(3, 10), new Vector2(19, 6), 0,
                Color.Black * (.3f * pose.Opacity));
            _actors.Draw(batch, shapes, kind, monsterPosition, pose);
            if (monster.IsDefeated)
            {
                continue;
            }

            DrawHealthBar(batch, shapes, monsterPosition + new Vector2(-24, -31), 48,
                (float)monster.Health / monster.MaxHealth, Color.LimeGreen);
            var label = string.Format(GameText.Monster, content.Monsters[monster.Id].Name,
                monster.Health, monster.MaxHealth);
            var labelWidth = Math.Min(280, font.MeasureString(label).X);
            var labelX = Math.Clamp(monsterPosition.X - labelWidth / 2, 8,
                batch.GraphicsDevice.Viewport.Width - labelWidth - 8);
            GameText.Draw(batch, font, label,
                new Vector2(labelX, monsterPosition.Y - 57), Color.White, labelWidth);
            if (monster.IsWindingUp)
            {
                var locked = monster.LockedAimDirection;
                shapes.Line(batch, monsterPosition,
                    monsterPosition + new Vector2(locked.X, locked.Y) * 90, Color.Orange, 2);
            }
        }
        foreach (var arrow in arena.Projectiles)
        {
            var tip = camera.WorldToScreen(arrow.Position);
            var arrowDirection = new Vector2(arrow.Direction.X, arrow.Direction.Y);
            var side = new Vector2(-arrowDirection.Y, arrowDirection.X);
            shapes.Line(batch, tip - arrowDirection * 16, tip, Color.BurlyWood, 3);
            shapes.Line(batch, tip, tip - arrowDirection * 6 + side * 4, Color.White, 2);
            shapes.Line(batch, tip, tip - arrowDirection * 6 - side * 4, Color.White, 2);
        }

        var playerPosition = camera.WorldToScreen(arena.Player.Position);
        shapes.Ellipse(batch, playerPosition + new Vector2(3, 18), new Vector2(15, 5), 0,
            Color.Black * (.35f * presentation.PlayerPose.Opacity));
        _actors.Draw(batch, shapes, ActorVisualKind.Player, playerPosition, presentation.PlayerPose,
            presentation.SwingHasHit, presentation.SwingWasCritical);
        if (!arena.Player.IsDead)
            DrawHealthBar(batch, shapes, playerPosition + new Vector2(-24, 26), 48,
                (float)arena.Player.Health / arena.Player.MaximumHealth, Color.Crimson);

        if (presentation.SwingRemaining > 0 && !presentation.SwingHasHit)
        {
            GameText.Draw(batch, font, GameText.Swing,
                playerPosition + new Vector2(-30, -64), Color.LightCyan, 200);
        }
        foreach (var hit in presentation.HitTexts)
            GameText.Draw(batch, font,
                string.Format(hit.WasCritical ? GameText.CriticalHit : GameText.Hit, hit.Damage),
                camera.WorldToScreen(hit.Position) + new Vector2(-30, -78),
                hit.WasCritical ? Color.OrangeRed : Color.Gold, 200);
    }

    internal static void DrawHealthBar(SpriteBatch batch, ShapeRenderer shapes, Vector2 position,
        int width, float fraction, Color color)
    {
        var bounds = new Rectangle((int)position.X, (int)position.Y, width, 7);
        shapes.Rectangle(batch, bounds, Color.Black);
        bounds.Width = (int)(width * Math.Clamp(fraction, 0, 1));
        shapes.Rectangle(batch, bounds, color);
    }
}
