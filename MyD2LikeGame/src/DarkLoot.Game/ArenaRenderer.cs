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
        var lootBounds = new Rectangle(8, 124, viewport.Width - 16, viewport.Height - 170);
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
            var monsterPosition = camera.WorldToScreen(monster.Position);
            var swingHit = arena.LastSwingHits.FirstOrDefault(
                hit => hit.MonsterInstanceId == monster.InstanceId);
            var hitThisSwing = swingHit is not null && attackFlash > 0;
            if (hitThisSwing)
            {
                var hitMessage = string.Format(
                    swingHit!.WasCritical ? GameText.CriticalHit : GameText.Hit, swingHit.Damage);
                GameText.Draw(batch, font, hitMessage,
                    monsterPosition + new Vector2(-30, -78),
                    swingHit.WasCritical ? Color.OrangeRed : Color.Gold, 200);
            }
            if (monster.IsDefeated)
            {
                continue;
            }

            DrawMonster(batch, shapes, monster, monsterPosition, hitThisSwing);
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

    private static void DrawMonster(SpriteBatch batch, ShapeRenderer shapes, MonsterState monster,
        Vector2 position, bool hit)
    {
        var color = hit ? Color.White : monster.Behavior == MonsterBehavior.Archer
            ? Color.LightGray : monster.Id == "monster.fast_wolf" ? Color.SandyBrown : Color.YellowGreen;
        if (monster.Behavior == MonsterBehavior.Archer)
        {
            shapes.Circle(batch, position + new Vector2(0, -9), 10, color);
            shapes.Line(batch, position, position + new Vector2(0, 17), color, 3);
            shapes.Line(batch, position + new Vector2(-10, 4), position + new Vector2(10, 4), color, 3);
            shapes.Line(batch, position + new Vector2(0, 12), position + new Vector2(-9, 22), color, 3);
            shapes.Line(batch, position + new Vector2(0, 12), position + new Vector2(9, 22), color, 3);
            shapes.Line(batch, position + new Vector2(18, -14), position + new Vector2(25, 2), Color.BurlyWood, 3);
            shapes.Line(batch, position + new Vector2(25, 2), position + new Vector2(18, 18), Color.BurlyWood, 3);
            shapes.Line(batch, position + new Vector2(18, -14), position + new Vector2(18, 18), Color.Wheat, 1);
        }
        else if (monster.Id == "monster.fast_wolf")
        {
            shapes.Rectangle(batch, new Rectangle((int)position.X - 23, (int)position.Y - 9, 40, 20), color);
            shapes.Circle(batch, position + new Vector2(17, -4), 12, color);
            shapes.Line(batch, position + new Vector2(9, -10), position + new Vector2(7, -23), color, 7);
            shapes.Line(batch, position + new Vector2(21, -10), position + new Vector2(23, -23), color, 7);
            shapes.Line(batch, position + new Vector2(-20, 0), position + new Vector2(-32, -12), color, 5);
            shapes.Line(batch, position + new Vector2(-13, 8), position + new Vector2(-13, 18), color, 5);
            shapes.Line(batch, position + new Vector2(10, 8), position + new Vector2(10, 18), color, 5);
        }
        else
        {
            shapes.Circle(batch, position, 18, color);
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
