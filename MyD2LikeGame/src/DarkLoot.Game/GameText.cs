using System;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Quests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

// Keep player-facing text here so the window smoke can check the complete font repertoire.
internal static class GameText
{
    public const string Title = "暗黑战利品";
    public const string HubTitle = "教堂";
    public const string SafeArea = "安全区域";
    public const string CryptEntry = "墓穴入口";
    public const string CryptBattle = "墓穴战场";
    public const string CryptBoss = "首领祭坛";
    public const string HubObjective = "教堂安全区 · 铁匠打造 · 东侧入口通往墓穴";
    public const string DungeonObjective = "探索墓穴 · 清理敌人 · 开启首领门";
    public const string DungeonGateOpen = "封印已解除 · 首领门已开启";
    public const string DungeonComplete = "首领已击败 · 出口已开启";
    public const string PortalEntry = "进入墓穴";
    public const string PortalReturn = "返回教堂";
    public const string SaveErrorTitle = "存档错误";
    public const string SaveError = "进度存读失败，游戏已停止。请检查下方路径及错误；现有存档不会自动重置。";
    public const string Hud = "暗黑战利品  |  生命 {0}/{1}  |  攻击 {2}  |  暴击 {3}%  |  FPS {4:F0}";
    public const string DeathHud = "已死亡  |  自动复活倒计时 {0:F1} 秒  |  FPS {1:F0}";
    public const string Movement = "移动 · 瞄准 · 近战 · 拾取";
    public const string Controls = "背包 · 铁匠 · 重开 · 退出";
    public const string DeathControls = "死亡期间无法移动、攻击、拾取、换装或打开面板";
    public const string DeathWorld = "怪物与箭矢仍会继续行动，请等待自动复活";
    public const string Enemies = "剩余怪物 {0}/{1}";
    public const string Cleared = "已清场，拾取材料与配方后找铁匠打造。";
    public const string Victory = "敌人已全部击败！收集材料与配方，向铁匠学习并打造装备。";
    public const string Objective = "击败敌人收集材料与配方，靠近铁匠学习并打造装备。死亡保留背包与已学配方。";
    public const string Inventory = "背包";
    public const string UnlimitedGrid = "每件一格 · 容量不限";
    public const string PageNumber = "{0} / {1}";
    public const string EquippedCell = "已装";
    public const string BaseAttack = "基础攻击 +{0}";
    public const string EquipComparison = "与当前装备比较";
    public const string Unequipped = "未装备";
    public const string MaterialLabel = "材料";
    public const string RecipeLabel = "配方";
    public const string ResourceQuantity = "持有数量：{0}";
    public const string PausedWorld = "面板打开 · 已暂停";
    public const string AreaTitle = "训练场";
    public const string HudStats = "攻击 {0}  |  生命上限 {1}  |  暴击 {2}%";
    public const string InventoryButton = "行囊";
    public const string SmithButton = "铁匠";
    public const string RestartButton = "重开";
    public const string SmithDetail = "固定品质 · 随机词缀";
    public const string ConsoleObjective = "击败敌人 · 收集材料与配方 · 铁匠打造";
    public const string ConsoleCleared = "已清场 · 收集战利品 · 打造装备";
    public const string DeathCountdown = "死亡 · {0:F1} 秒后复活";
    public const string HealthFraction = "{0}/{1}";
    public const string FrameRate = "FPS {0:F0}";
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
    public const string Paused = "已暂停。";
    public const string Empty = "暂无装备 - 向铁匠学习配方并打造。";
    public const string EmptyResources = "暂无材料或配方 - 击败敌人后拾取。";
    public const string MaterialResource = "材料  {0} ×{1}";
    public const string RecipeResource = "配方  {0} ×{1}";
    public const string Item = "{0} {1}  [{2}]{3}";
    public const string Equipped = "  [已装备]";
    public const string None = "无";
    public const string NoAffixes = "无词缀";
    public const string Rolls = "词缀：";
    public const string Comparison = "攻击 {0}  生命 {1}  暴击 {2}%";
    public const string Pickup = "{0} ×{1}";
    public const string Monster = "{0} {1}/{2}";
    public const string CriticalHit = "暴击！-{0}";
    public const string Hit = "命中 -{0}";
    public const string Swing = "挥击";
    public const string Smith = "铁匠 - 学习配方 / 打造装备";
    public const string SmithPrompt = "铁匠";
    public const string SmithPaused = "已暂停；打造后需手动装备。";
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
    public const string SmithTooFar = "距离铁匠太远，请靠近后交谈";
    public const string RecipeNotOwned = "没有这张配方，请先拾取";
    public const string AlreadyLearned = "已学会这张配方，不再消耗";
    public const string NotLearned = "尚未学会，请先学习配方";
    public const string InsufficientMaterials = "材料不足";
    public const string Shortage = "{0} 缺{1}";
    public const string ShortageDetails = "{0}： {1}";
    public const string QuestJournal = "任务日志";
    public const string QuestButton = "任务";
    public const string CommissionTab = "委托";
    public const string QuestExploration = "墓穴初探";
    public const string QuestSupplies = "锻造备料";
    public const string QuestGuardian = "讨伐守卫";
    public const string ExplorationStory = "墓穴中的邪物正在苏醒。替铁匠清除三只怪物，带回消息。";
    public const string SuppliesStory = "铁匠需要骨片修补武器。交付四份骨片，换取一张魔法武器配方。";
    public const string GuardianStory = "首领祭坛的守卫仍在阻挡道路。接下委托后击败它，再回教堂领取报酬。";
    public const string QuestLocked = "未解锁 · 先交付上一委托";
    public const string QuestAvailable = "可接取 · 返回铁匠处交谈";
    public const string QuestActive = "进行中";
    public const string QuestReady = "可交付 · 返回铁匠处领奖";
    public const string QuestClaimed = "已交付 · 奖励已领取";
    public const string QuestComplete = "铁匠委托已全部完成";
    public const string AcceptQuest = "接取委托";
    public const string TurnInQuest = "交付领奖";
    public const string QuestRewards = "一次性奖励";
    public const string QuestKills = "击败怪物 {0}/{1}";
    public const string QuestBones = "交付骨片 {0}/{1}";
    public const string QuestBoss = "击败墓穴守卫 {0}/{1}";
    public const string QuestRules = "接取后才计入击杀；交付时扣除材料。";
    public const string QuestJournalHelp = "接取与领奖需返回教堂找铁匠交谈。";
    public const string QuestNotAccepted = "请先接取委托";
    public const string QuestAlreadyAccepted = "已经接取该委托";
    public const string QuestNotReady = "目标尚未完成或交付材料不足";
    public const string QuestOverflow = "资源数量已达上限，未扣料或发奖";
    public const string QuestAccepted = "委托已接取";
    public const string QuestTurnedIn = "委托已交付，奖励已放入行囊";
    public const string QuestRewardLine = "{0} ×{1}";

    public static string QuestTitle(QuestDefinition quest) => quest.Objective switch
    {
        QuestObjective.Kills => QuestExploration,
        QuestObjective.Materials => QuestSupplies,
        QuestObjective.Guardian => QuestGuardian,
        _ => throw new ArgumentOutOfRangeException(nameof(quest))
    };

    public static string QuestStory(QuestDefinition quest) => quest.Objective switch
    {
        QuestObjective.Kills => ExplorationStory,
        QuestObjective.Materials => SuppliesStory,
        QuestObjective.Guardian => GuardianStory,
        _ => throw new ArgumentOutOfRangeException(nameof(quest))
    };

    public static string QuestResult(QuestStatus status) => status switch
    {
        QuestStatus.Applied => OperationApplied,
        QuestStatus.PlayerDead => SmithDead,
        QuestStatus.TooFar => SmithTooFar,
        QuestStatus.AlreadyAccepted => QuestAlreadyAccepted,
        QuestStatus.NotAccepted => QuestNotAccepted,
        QuestStatus.NotReady => QuestNotReady,
        QuestStatus.AllCompleted => QuestComplete,
        QuestStatus.ResourceOverflow => QuestOverflow,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
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
        Vector2 position, Color color, float width, float height = 22, bool centered = false)
    {
        var size = font.MeasureString(text);
        var scale = Math.Min(1f, Math.Min(width / Math.Max(1, size.X), height / Math.Max(1, size.Y)));
        if (centered)
            position += new Vector2((width - size.X * scale) / 2, (height - size.Y * scale) / 2);
        batch.DrawString(font, text, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }
}
