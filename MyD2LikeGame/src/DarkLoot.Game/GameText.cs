using System;
using DarkLoot.Data.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

// Keep player-facing text here so the window smoke can check the complete font repertoire.
internal static class GameText
{
    public const string Title = "暗黑战利品 - 训练场";
    public const string Hud = "暗黑战利品  |  生命 {0}/{1}  |  攻击 {2}  |  暴击 {3}%  |  FPS {4:F0}";
    public const string Movement = "WASD：移动    鼠标：瞄准    左键：连续近战    E：拾取";
    public const string Controls = "I：背包 / 点击装备    R：重开（清空地面装备，保留背包）    Esc：退出";
    public const string Enemies = "剩余怪物 {0}/{1}";
    public const string Cleared = "已清场，按 R 重开（清空地面装备）";
    public const string Victory = "敌人已全部击败！靠近战利品按 E 拾取，按 I 比较并装备武器和护甲。按 R 再战。";
    public const string Objective = "击败敌人可获得武器和护甲。死亡后恢复当前生命上限，保留装备。";
    public const string Inventory = "背包 - 点击物品比较 / 装备";
    public const string Weapon = "武器";
    public const string Armor = "护甲";
    public const string Common = "普通";
    public const string Magic = "魔法";
    public const string Rare = "稀有";
    public const string Attack = "攻击";
    public const string Health = "生命";
    public const string Critical = "暴击";
    public const string SlotLine = "{0}  {1}";
    public const string Totals = "总计  攻击 {0}   生命上限 {1}   暴击 {2}%";
    public const string Paused = "已暂停。按 I 关闭，滚轮翻页。";
    public const string Empty = "背包为空 - 击败敌人后按 E 拾取。";
    public const string Item = "{0} {1}  [{2}]{3}";
    public const string Equipped = "  [已装备]";
    public const string None = "无";
    public const string NoAffixes = "无词缀";
    public const string Rolls = "词缀：";
    public const string Comparison = "攻击 {0}  生命 {1}  暴击 {2}%";
    public const string Pickup = "E 拾取：{0} {1}";
    public const string Monster = "{0} {1}/{2}";
    public const string CriticalHit = "暴击！-{0}";
    public const string Hit = "命中 -{0}";
    public const string Swing = "挥击";

    public static string Slot(ItemSlot slot) => slot switch
    {
        ItemSlot.Weapon => Weapon,
        ItemSlot.Armor => Armor,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string Rarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => Common,
        ItemRarity.Magic => Magic,
        ItemRarity.Rare => Rare,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };

    public static string Stat(ItemStat stat) => stat switch
    {
        ItemStat.Attack => Attack,
        ItemStat.MaximumHealth => Health,
        ItemStat.CriticalChance => Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    public static void Draw(SpriteBatch batch, SpriteFont font, string text,
        Vector2 position, Color color, float width, float height = 22)
    {
        var size = font.MeasureString(text);
        var scale = Math.Min(1f, Math.Min(width / Math.Max(1, size.X), height / Math.Max(1, size.Y)));
        batch.DrawString(font, text, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }
}
