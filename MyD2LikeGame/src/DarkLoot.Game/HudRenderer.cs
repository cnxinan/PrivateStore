using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class HudRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, ArenaSimulation arena, int width, int height,
        double framesPerSecond)
    {
        shapes.Rectangle(batch, new Rectangle(0, 0, width, 88), new Color(15, 20, 29));
        GameText.Draw(batch, font,
            string.Format(GameText.Hud, arena.Player.Health, arena.Player.MaximumHealth,
                arena.Player.AttackDamage, arena.Player.CriticalChance, framesPerSecond),
            new Vector2(20, 12), Color.White, width - 40);
        GameText.Draw(batch, font, GameText.Movement,
            new Vector2(20, 39), Color.LightCyan, width - 40);
        GameText.Draw(batch, font, GameText.Controls,
            new Vector2(20, 63), Color.LightCyan, width - 40);
        shapes.Rectangle(batch, new Rectangle(0, height - 38, width, 38), new Color(15, 20, 29));
        GameText.Draw(batch, font, string.Format(GameText.Enemies, arena.RemainingMonsters, arena.Monsters.Count),
            new Vector2(20, 96), Color.White, 240);
        var status = arena.IsCleared
            ? GameText.Cleared
            : GameText.Objective;
        GameText.Draw(batch, font, status, new Vector2(20, height - 29), Color.Gold, width - 40);
    }
}
