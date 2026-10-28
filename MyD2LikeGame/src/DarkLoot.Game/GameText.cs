using System;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Crafting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

// Keep player-facing text here so the window smoke can check the complete font repertoire.
internal static class GameText
{
    public const string Title = "暗黑战利品 - 训练场";
    public const string Hud = "暗黑战利品  |  生命 {0}/{1}  |  攻击 {2}  |  暴击 {3}%  |  FPS {4:F0}";
    public const string DeathHud = "已死亡  |  自动复活倒计时 {0:F1} 秒  |  FPS {1:F0}";
    public const string Movement = "WASD：移动    鼠标：瞄准    左键：连续近战    E：拾取";
    public const string Controls = "I：背包    F：铁匠    R：重开（保留背包与配方）    Esc：关闭面板 / 退出";
    public const string DeathControls = "死亡期间无法移动、攻击、拾取、换装或打开面板；按 R 无效";
    public const string DeathWorld = "怪物与箭矢仍会继续行动，请等待自动复活";
    public const string Enemies = "剩余怪物 {0}/{1}";
    public const string Cleared = "已清场，拾取材料与配方后找铁匠打造；按 R 再战";
    public const string Victory = "敌人已全部击败！按 E 拾取材料与配方，靠近铁匠按 F 学习和打造。按 I 装备。";
    public const string Objective = "击败敌人收集材料与配方，靠近铁匠按 F 学习并打造装备。死亡保留背包与已学配方。";
    public const string Inventory = "背包";
    public const string EquipmentTab = "装备";
    public const string ResourceTab = "材料 / 配方";
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
    public const string Paused = "已暂停。I / Esc 关闭，滚轮翻页，点击装备穿戴。";
    public const string Empty = "暂无装备 - 向铁匠学习配方并打造。";
    public const string EmptyResources = "暂无材料或配方 - 击败敌人后按 E 拾取。";
    public const string MaterialResource = "材料  {0} ×{1}";
    public const string RecipeResource = "配方  {0} ×{1}";
    public const string Item = "{0} {1}  [{2}]{3}";
    public const string Equipped = "  [已装备]";
    public const string None = "无";
    public const string NoAffixes = "无词缀";
    public const string Rolls = "词缀：";
    public const string Comparison = "攻击 {0}  生命 {1}  暴击 {2}%";
    public const string Pickup = "E 拾取：{0} ×{1}";
    public const string Monster = "{0} {1}/{2}";
    public const string CriticalHit = "暴击！-{0}";
    public const string Hit = "命中 -{0}";
    public const string Swing = "挥击";
    public const string Smith = "铁匠 - 学习配方 / 打造装备";
    public const string SmithPrompt = "铁匠  F：交谈";
    public const string SmithPaused = "已暂停。F / Esc 关闭，I 切换背包；打造后需手动装备。";
    public const string Learn = "学习";
    public const string Craft = "打造";
    public const string Known = "已学会";
    public const string Unknown = "未学会";
    public const string RecipeState = "{0}  |  配方 ×{1}";
    public const string MaterialCost = "{0} {1}/{2}";
    public const string MaterialCostHelp = "材料：持有 / 需要";
    public const string Learned = "学习成功：{0}";
    public const string Crafted = "打造成功：{0}";
    public const string OperationApplied = "操作成功";
    public const string SmithDead = "死亡期间无法与铁匠交谈";
    public const string SmithTooFar = "距离铁匠太远，请靠近后按 F";
    public const string RecipeNotOwned = "没有这张配方，请先拾取";
    public const string AlreadyLearned = "已学会这张配方，不再消耗";
    public const string NotLearned = "尚未学会，请先学习配方";
    public const string InsufficientMaterials = "材料不足";
    public const string Shortage = "{0} 缺{1}";
    public const string ShortageDetails = "{0}： {1}";
    public static string SmithStatus(BlacksmithStatus status) => status switch
    {
        BlacksmithStatus.Applied => OperationApplied,
        BlacksmithStatus.PlayerDead => SmithDead,
        BlacksmithStatus.TooFar => SmithTooFar,
        BlacksmithStatus.RecipeNotOwned => RecipeNotOwned,
        BlacksmithStatus.AlreadyLearned => AlreadyLearned,
        BlacksmithStatus.NotLearned => NotLearned,
        BlacksmithStatus.InsufficientMaterials => InsufficientMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown blacksmith status.")
    };

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
