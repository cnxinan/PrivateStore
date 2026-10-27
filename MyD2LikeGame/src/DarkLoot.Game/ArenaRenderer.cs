using System;
using System.Collections.Generic;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed class ArenaRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        ArenaSimulation arena, GameContent content, Camera2D camera, NumericsVector aim,
        float attackFlash)
    {
        var origin = camera.WorldToScreen(NumericsVector.Zero);
        var bounds = new Rectangle((int)origin.X - 18, (int)origin.Y - 18, 996, 576);
        shapes.Rectangle(batch, bounds, new Color(83, 91, 83));
        shapes.Rectangle(batch, new Rectangle((int)origin.X, (int)origin.Y, 960, 540), new Color(30, 43, 37));
        for (var x = 0; x <= 960; x += 60)
        {
            shapes.Line(batch, origin + new Vector2(x, 0), origin + new Vector2(x, 540), new Color(38, 53, 45), 1);
        }
        for (var y = 0; y <= 540; y += 60)
        {
            shapes.Line(batch, origin + new Vector2(0, y), origin + new Vector2(960, y), new Color(38, 53, 45), 1);
        }

        var lootRows = new List<Rectangle>(arena.GroundItems.Count);
        var viewport = batch.GraphicsDevice.Viewport;
        var lootBounds = new Rectangle(8, 96, viewport.Width - 16, viewport.Height - 104);
        foreach (var drop in arena.GroundItems)
        {
            var position = camera.WorldToScreen(drop.Position);
            var definition = content.Items[drop.Item.DefinitionId];
            var color = InventoryPanel.RarityColor(drop.Item.Rarity);
            var label = string.Format(GameText.Pickup, GameText.Rarity(drop.Item.Rarity),
                ItemNameFormatter.GetDisplayName(drop.Item, definition, content));
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
            if (monster.IsDefeated)
            {
                continue;
            }

            var swingHit = arena.LastSwingHits.FirstOrDefault(
                hit => hit.MonsterInstanceId == monster.InstanceId);
            var hitThisSwing = swingHit is not null && attackFlash > 0;
            var monsterPosition = camera.WorldToScreen(monster.Position);
            shapes.Circle(batch, monsterPosition, 18, hitThisSwing ? Color.White : new Color(121, 205, 87));
            shapes.Circle(batch, monsterPosition + new Vector2(-6, -4), 3, Color.Black);
            shapes.Circle(batch, monsterPosition + new Vector2(6, -4), 3, Color.Black);
            DrawHealthBar(batch, shapes, monsterPosition + new Vector2(-24, -31), 48,
                (float)monster.Health / monster.MaxHealth, Color.LimeGreen);
            var label = string.Format(GameText.Monster, content.Monsters[monster.Id].Name,
                monster.Health, monster.MaxHealth);
            var labelWidth = Math.Min(280, font.MeasureString(label).X);
            var labelX = Math.Clamp(monsterPosition.X - labelWidth / 2, 8,
                batch.GraphicsDevice.Viewport.Width - labelWidth - 8);
            GameText.Draw(batch, font, label,
                new Vector2(labelX, monsterPosition.Y - 57), Color.White, labelWidth);
            if (hitThisSwing)
            {
                var hitMessage = string.Format(
                    swingHit!.WasCritical ? GameText.CriticalHit : GameText.Hit,
                    swingHit.Damage);
                GameText.Draw(batch, font, hitMessage,
                    new Vector2(labelX, monsterPosition.Y - 78),
                    swingHit.WasCritical ? Color.OrangeRed : Color.Gold,
                    Math.Min(200, font.MeasureString(hitMessage).X));
            }
        }

        var playerPosition = camera.WorldToScreen(arena.Player.Position);
        shapes.Circle(batch, playerPosition, 14, new Color(87, 169, 236));
        var direction = aim - arena.Player.Position;
        if (direction.LengthSquared() > .001f)
        {
            direction = NumericsVector.Normalize(direction);
        }
        else
        {
            direction = NumericsVector.UnitX;
        }
        var facing = new Vector2(direction.X, direction.Y);
        shapes.Line(batch, playerPosition, playerPosition + facing * 24, Color.LightCyan, 4);
        DrawHealthBar(batch, shapes, playerPosition + new Vector2(-24, 23), 48,
            (float)arena.Player.Health / arena.Player.MaximumHealth, Color.Crimson);

        if (attackFlash > 0)
        {
            var hasHit = arena.LastSwingHits.Count > 0;
            var critical = arena.LastSwingHits.Any(hit => hit.WasCritical);
            var angle = MathF.Atan2(facing.Y, facing.X);
            var previous = playerPosition + new Vector2(MathF.Cos(angle - MathF.PI / 4), MathF.Sin(angle - MathF.PI / 4)) * 48;
            for (var segment = 1; segment <= 12; segment++)
            {
                var arcAngle = angle - MathF.PI / 4 + segment * MathF.PI / 24;
                var next = playerPosition + new Vector2(MathF.Cos(arcAngle), MathF.Sin(arcAngle)) * 48;
                shapes.Line(batch, previous, next, critical
                    ? Color.OrangeRed
                    : hasHit ? Color.Gold : Color.LightCyan, 5);
                previous = next;
            }
            if (!hasHit)
            {
                GameText.Draw(batch, font, GameText.Swing,
                    playerPosition + new Vector2(-30, -64), Color.LightCyan, 200);
            }
        }
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
