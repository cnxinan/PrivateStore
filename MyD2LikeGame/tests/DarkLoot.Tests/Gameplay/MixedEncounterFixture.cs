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
                "resource.slime"),
            ["monster.fast_wolf"] = new(
                "monster.fast_wolf",
                "Fast Wolf",
                100,
                0,
                140,
                "resource.wolf"),
            ["monster.skeleton_archer"] = new(
                "monster.skeleton_archer",
                "Skeleton Archer",
                100,
                0,
                70,
                "resource.skeleton",
                MonsterBehavior.Archer,
                new RangedAttackDefinition(260, 240, 1.5f, .3f, 5))
        };
        var lootTables = new Dictionary<string, LootTableDefinition>
        {
            ["loot.training_empty"] = new("loot.training_empty", 0, 0, [])
        };
        var materials = new Dictionary<string, MaterialDefinition>
        {
            ["material.slime"] = new("material.slime", "Slime"),
            ["material.hide"] = new("material.hide", "Hide"),
            ["material.bone"] = new("material.bone", "Bone")
        };
        var recipes = new Dictionary<string, RecipeDefinition>
        {
            ["recipe.magic_blade"] = new(
                "recipe.magic_blade",
                "Magic Blade",
                "weapon.training_blade",
                ItemRarity.Magic,
                [new MaterialCost("material.slime", 1)]),
            ["recipe.magic_vest"] = new(
                "recipe.magic_vest",
                "Magic Vest",
                "armor.training_vest",
                ItemRarity.Magic,
                [new MaterialCost("material.slime", 1)]),
            ["recipe.rare_blade"] = new(
                "recipe.rare_blade",
                "Rare Blade",
                "weapon.training_blade",
                ItemRarity.Rare,
                [new MaterialCost("material.slime", 1)]),
            ["recipe.rare_vest"] = new(
                "recipe.rare_vest",
                "Rare Vest",
                "armor.training_vest",
                ItemRarity.Rare,
                [new MaterialCost("material.slime", 1)])
        };
        var resourceLootTables = new Dictionary<string, ResourceLootTableDefinition>
        {
            ["resource.slime"] = new(
                "resource.slime", "material.slime", 1, 1, 0, []),
            ["resource.wolf"] = new(
                "resource.wolf", "material.hide", 1, 1, 0, []),
            ["resource.skeleton"] = new(
                "resource.skeleton", "material.bone", 1, 1, 0, [])
        };

        return new GameContent(
            items, monsters, lootTables, affixes,
            MaterialDefinitions: materials,
            RecipeDefinitions: recipes,
            ResourceLootTableDefinitions: resourceLootTables);
    }

    public static EncounterDefinition CreateEncounter(params MonsterSpawnDefinition[] spawns) =>
        new("encounter.test", spawns);
}
