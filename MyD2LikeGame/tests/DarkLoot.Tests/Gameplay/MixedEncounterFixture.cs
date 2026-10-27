using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Tests.Gameplay;

internal static class MixedEncounterFixture
{
    public static GameContent CreateContent(int contactDamage = 0)
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["weapon.training_blade"] = new("weapon.training_blade", "Training Blade", 5)
            {
                Slot = ItemSlot.Weapon
            },
            ["armor.training_vest"] = new("armor.training_vest", "Training Vest", 0)
            {
                Slot = ItemSlot.Armor
            }
        };
        var affixes = new Dictionary<string, AffixDefinition>
        {
            ["affix.keen"] = new(
                "affix.keen",
                "Keen",
                ItemStat.Attack,
                2,
                5,
                [ItemSlot.Weapon]),
            ["affix.lucky"] = new(
                "affix.lucky",
                "Lucky",
                ItemStat.CriticalChance,
                3,
                8,
                [ItemSlot.Weapon, ItemSlot.Armor]),
            ["affix.vital"] = new(
                "affix.vital",
                "Vital",
                ItemStat.MaximumHealth,
                10,
                30,
                [ItemSlot.Armor]),
            ["affix.test_crit"] = new(
                "affix.test_crit",
                "Test Crit",
                ItemStat.CriticalChance,
                50,
                50,
                [ItemSlot.Weapon])
        };
        var monsters = new Dictionary<string, MonsterDefinition>
        {
            ["monster.training_slime"] = new(
                "monster.training_slime",
                "Training Slime",
                100,
                contactDamage,
                60,
                "loot.training_empty"),
            ["monster.fast_wolf"] = new(
                "monster.fast_wolf",
                "Fast Wolf",
                100,
                0,
                140,
                "loot.training_empty"),
            ["monster.skeleton_archer"] = new(
                "monster.skeleton_archer",
                "Skeleton Archer",
                100,
                0,
                70,
                "loot.training_empty",
                MonsterBehavior.Archer,
                new RangedAttackDefinition(260, 240, 1.5f, .3f, 5))
        };
        var lootTables = new Dictionary<string, LootTableDefinition>
        {
            ["loot.training_empty"] = new("loot.training_empty", 0, 0, [])
        };

        return new GameContent(items, monsters, lootTables, affixes);
    }

    public static EncounterDefinition CreateEncounter(params MonsterSpawnDefinition[] spawns) =>
        new("encounter.test", spawns);
}
