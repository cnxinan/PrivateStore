using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class HudRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, ArenaSimulation arena, int width, int height,
        double framesPerSecond, string? interactionFeedback = null)
    {
        shapes.Rectangle(batch, new Rectangle(0, 0, width, 88), new Color(15, 20, 29));
        var dead = arena.Player.IsDead;
        GameText.Draw(batch, font, PlayerHeader(arena, framesPerSecond),
            new Vector2(20, 12), dead ? Color.Salmon : Color.White, width - 40);
        GameText.Draw(batch, font, MovementInstructions(arena),
            new Vector2(20, 39), Color.LightCyan, width - 40);
        GameText.Draw(batch, font, ControlInstructions(arena),
            new Vector2(20, 63), Color.LightCyan, width - 40);
        shapes.Rectangle(batch, new Rectangle(0, height - 38, width, 38), new Color(15, 20, 29));
        GameText.Draw(batch, font, string.Format(GameText.Enemies, arena.RemainingMonsters, arena.Monsters.Count),
            new Vector2(20, 96), Color.White, 240);
        var status = interactionFeedback ?? (arena.IsCleared
            ? GameText.Cleared
            : GameText.Objective);
        GameText.Draw(batch, font, status, new Vector2(20, height - 29),
            interactionFeedback is null ? Color.Gold : Color.Salmon, width - 40);
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
