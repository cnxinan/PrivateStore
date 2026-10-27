using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class HudRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, ArenaSimulation arena, int width, int height)
    {
        shapes.Rectangle(batch, new Rectangle(0, 0, width, 88), new Color(15, 20, 29));
        batch.DrawString(font,
            $"DARKLOOT   |   Health {arena.Player.Health}/{arena.Player.MaximumHealth}" +
            $"   |   Attack {arena.Player.AttackDamage}   |   Crit {arena.Player.CriticalChance}%",
            new Vector2(20, 12), Color.White);
        batch.DrawString(font, "WASD: move    Mouse: aim    Left click: melee    E: pick up",
            new Vector2(20, 39), Color.LightCyan);
        batch.DrawString(font, "I: inventory / click to equip    R: restart slime (keeps gear)    Esc: exit",
            new Vector2(20, 63), Color.LightCyan);
        shapes.Rectangle(batch, new Rectangle(0, height - 38, width, 38), new Color(15, 20, 29));
        var status = arena.Monster.IsDefeated
            ? "Slime defeated! E near loot, then I to compare/equip weapon and armor. R repeats."
            : "Defeat the slime for weapon and armor loot. Death restores current max HP and keeps gear.";
        batch.DrawString(font, status, new Vector2(20, height - 29), Color.Gold);
    }
}
