using System;
using System.Collections.Generic;
using System.Linq;
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
    private ActorRenderer _actors = null!;
    private CombatEffectRenderer _effects = null!;

    internal void LoadEffects(GraphicsDevice device, PixelContent content)
    {
        _actors = new ActorRenderer(content);
        _effects = new CombatEffectRenderer(device);
    }
    internal void UnloadEffects() => _effects?.Dispose();
    internal CombatEffectRenderer Effects => _effects;

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        ArenaSimulation arena, GameContent content, Camera2D camera, ActorPresentation presentation,
        GothicRoomRenderer room, DungeonRoomRenderer? dungeon = null, DungeonSession? adventure = null)
    {
        if (arena.Map?.IsDungeon == true)
        {
            if (dungeon is null || adventure is null)
                throw new InvalidOperationException("Dungeon rendering requires the live dungeon session.");
            dungeon.Draw(batch, camera, arena.Map, adventure.Completed);
        }
        else
            room.Draw(batch, camera);

        if (arena.Map?.IsDungeon != true)
        {
            var smithPosition = camera.WorldToScreen(BlacksmithService.Position);
            shapes.Ellipse(batch, smithPosition + new Vector2(2, 22), new Vector2(31, 8), 0, Color.Black * .35f);
            _actors.DrawBlacksmith(batch, smithPosition, room.AnimationTime);
            if (arena.BlacksmithOperations.CanInteract(arena.Player))
                GameText.Draw(batch, font, GameText.SmithPrompt,
                    smithPosition + new Vector2(-30, -42), Color.Gold, 220);
            var questMarker = QuestPanel.Marker(arena);
            if (questMarker.Length > 0)
                GameText.Draw(batch, font, questMarker,
                    smithPosition + new Vector2(-20, -74), Color.Gold, 28, 30, centered: true);
            if (adventure is not null)
            {
                var portal = camera.WorldToScreen(DungeonSession.HubEntrance);
                room.DrawPortal(batch, camera, DungeonSession.HubEntrance);
                GameText.Draw(batch, font, GameText.PortalEntry, portal + new Vector2(-40, -67), Color.Gold, 160);
            }
        }

        var lootRows = new List<Rectangle>(arena.GroundResources.Count);
        var viewport = batch.GraphicsDevice.Viewport;
        var lootBounds = new Rectangle(8, 70, viewport.Width - 16,
            new UiLayout(viewport.Width, viewport.Height).Console.Top - 78);
        foreach (var drop in arena.GroundResources)
        {
            var position = camera.WorldToScreen(drop.Position);
            if (arena.Map?.IsDungeon == true && (position.X < 0 || position.X > viewport.Width ||
                position.Y < 0 || position.Y > lootBounds.Bottom))
                continue;
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
            if (arena.Map?.IsDungeon == true && (monsterPosition.X < -70 || monsterPosition.X > viewport.Width + 70 ||
                monsterPosition.Y < -70 || monsterPosition.Y > lootBounds.Bottom + 70))
                continue;
            var kind = ActorPresentation.MonsterVisual(monster);
            var pose = presentation.GetMonsterPose(monster.InstanceId);
            shapes.Ellipse(batch, monsterPosition + new Vector2(3, 10), new Vector2(19, 6), 0,
                Color.Black * (.3f * pose.Opacity));
            _actors.Draw(batch, shapes, kind, monsterPosition, pose,
                frame: presentation.GetMonsterFrame(monster.InstanceId),
                sizeScale: monster.Id == "monster.crypt_guardian" ? 1.25f : 1);
        }
        foreach (var arrow in arena.Projectiles)
        {
            var tip = camera.WorldToScreen(arrow.Position);
            var arrowDirection = new Vector2(arrow.Direction.X, arrow.Direction.Y);
            var source = arena.Monsters.First(monster => monster.InstanceId == arrow.SourceInstanceId);
            _actors.DrawProjectile(batch, shapes, tip, arrowDirection, ActorPresentation.MonsterVisual(source));
        }

        var playerPosition = camera.WorldToScreen(arena.Player.Position);
        shapes.Ellipse(batch, playerPosition + new Vector2(3, 18), new Vector2(15, 5), 0,
            Color.Black * (.35f * presentation.PlayerPose.Opacity));
        _actors.Draw(batch, shapes, ActorVisualKind.Player, playerPosition, presentation.PlayerPose,
            PlayerAppearance.From(arena.Player.Inventory), presentation.PlayerFrame);
        _effects.Draw(batch, camera, presentation.Effects);

        // Actor labels and health bars stay above transient attack pixels.
        foreach (var monster in arena.Monsters)
        {
            if (monster.IsDefeated)
                continue;
            var monsterPosition = camera.WorldToScreen(monster.Position);
            if (arena.Map?.IsDungeon == true && (monsterPosition.X < -70 || monsterPosition.X > viewport.Width + 70 ||
                monsterPosition.Y < -70 || monsterPosition.Y > lootBounds.Bottom + 70))
                continue;
            var kind = ActorPresentation.MonsterVisual(monster);
            var headroom = kind is ActorVisualKind.Warrior or ActorVisualKind.Cultist ? 15 : 0;
            DrawHealthBar(batch, shapes, monsterPosition + new Vector2(-24, -31 - headroom), 48,
                (float)monster.Health / monster.MaxHealth, Color.LimeGreen);
            var label = string.Format(GameText.Monster, content.Monsters[monster.Id].Name,
                monster.Health, monster.MaxHealth);
            var labelWidth = Math.Min(280, font.MeasureString(label).X);
            var labelX = Math.Clamp(monsterPosition.X - labelWidth / 2, 8,
                batch.GraphicsDevice.Viewport.Width - labelWidth - 8);
            GameText.Draw(batch, font, label,
                new Vector2(labelX, monsterPosition.Y - 57 - headroom), Color.White, labelWidth);
            if (monster.IsWindingUp)
            {
                var locked = monster.LockedAimDirection;
                shapes.Line(batch, monsterPosition,
                    monsterPosition + new Vector2(locked.X, locked.Y) * 90,
                    kind == ActorVisualKind.Cultist ? Color.MediumPurple : Color.Orange, 2);
            }
        }
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
