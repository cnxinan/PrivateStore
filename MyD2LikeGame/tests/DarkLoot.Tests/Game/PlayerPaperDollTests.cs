using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Tests.Gameplay;
using Microsoft.Xna.Framework;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Tests.Game;

public sealed class PlayerPaperDollTests
{
    private static ActorPose Idle => new(NVector.Zero, NVector.One, 0, 1, 0, 0, 0, NVector.UnitY);

    [Fact]
    public void Appearance_UsesEquippedItemsNotInventoryContentsAndUpdatesWithoutAnimationTick()
    {
        var content = MixedEncounterFixture.CreateContent();
        var arena = new ArenaSimulation(content, new Random(9),
            MixedEncounterFixture.CreateEncounter(
                new MonsterSpawnDefinition("target", "monster.training_slime", 500, 270)));
        var weapon = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
        var armor = new ItemInstance(Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
            [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 20)]);
        foreach (var item in new[] { weapon, armor })
            Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, item));
        Assert.Equal(default, PlayerAppearance.From(arena.Player.Inventory));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));
        Assert.Equal(new PlayerAppearance(ItemRarity.Common, null), PlayerAppearance.From(arena.Player.Inventory));
        Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, armor.InstanceId));
        Assert.Equal(new PlayerAppearance(ItemRarity.Common, ItemRarity.Magic), PlayerAppearance.From(arena.Player.Inventory));
        Assert.Equal(0, arena.Player.AttackSequence);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Layers_OnlyShowEquipmentInOccupiedSlots(bool weapon, bool armor)
    {
        var parts = PlayerPaperDoll.Build(Idle, new(
            weapon ? ItemRarity.Magic : null, armor ? ItemRarity.Rare : null));
        Assert.Equal(weapon, parts.Any(part => part.Region == PaperDollRegion.Weapon));
        Assert.Equal(armor, parts.Any(part => part.Region == PaperDollRegion.Armor));
        Assert.Contains(parts, part => part.Region == PaperDollRegion.Head);
        Assert.Contains(parts, part => part.Region == PaperDollRegion.Boot);
    }

    [Fact]
    public void Rarity_ChangesEquipmentDetailButNotBodyOrWeaponReach()
    {
        var common = PlayerPaperDoll.Build(Idle, new(ItemRarity.Common, ItemRarity.Common));
        var magic = PlayerPaperDoll.Build(Idle, new(ItemRarity.Magic, ItemRarity.Magic));
        var rare = PlayerPaperDoll.Build(Idle, new(ItemRarity.Rare, ItemRarity.Rare));
        Assert.Equal(common.Where(p => p.Region == PaperDollRegion.Head),
            rare.Where(p => p.Region == PaperDollRegion.Head));
        Assert.NotEqual(common.Where(p => p.Region == PaperDollRegion.Armor).ToArray(),
            magic.Where(p => p.Region == PaperDollRegion.Armor).ToArray());
        Assert.True(rare.Count(p => p.Region == PaperDollRegion.Armor) >
            common.Count(p => p.Region == PaperDollRegion.Armor));
        Assert.Equal(common.First(p => p.Region == PaperDollRegion.Weapon).Position,
            rare.First(p => p.Region == PaperDollRegion.Weapon).Position);
    }

    [Fact]
    public void Facing_ChangesWeaponOcclusionAndAttackMovesHeldEquipment()
    {
        var appearance = new PlayerAppearance(ItemRarity.Magic, ItemRarity.Magic);
        var front = PlayerPaperDoll.Build(Idle, appearance).ToList();
        var back = PlayerPaperDoll.Build(Idle with { Facing = -NVector.UnitY }, appearance).ToList();
        Assert.True(front.FindIndex(p => p.Region == PaperDollRegion.Weapon) >
            front.FindIndex(p => p.Region == PaperDollRegion.Body));
        Assert.True(back.FindLastIndex(p => p.Region == PaperDollRegion.Weapon) <
            back.FindIndex(p => p.Region == PaperDollRegion.Body));
        var swing = PlayerPaperDoll.Build(Idle with { AttackProgress = .8f }, appearance);
        Assert.NotEqual(front.First(p => p.Region == PaperDollRegion.Weapon).Position,
            swing.First(p => p.Region == PaperDollRegion.Weapon).Position);
        Assert.Equal(front.Where(p => p.Region == PaperDollRegion.Head),
            swing.Where(p => p.Region == PaperDollRegion.Head));
    }

    [Fact]
    public void SharedPose_TransformsAndFadesAllEquipmentWithTheBody()
    {
        var appearance = new PlayerAppearance(ItemRarity.Rare, ItemRarity.Magic);
        var neutral = PlayerPaperDoll.Build(Idle, appearance);
        var posed = PlayerPaperDoll.Build(Idle with
        {
            Offset = new NVector(3, -2),
            Scale = new NVector(.8f, .6f),
            Rotation = .7f,
            Opacity = .5f,
            WhiteMix = 1
        }, appearance);
        Assert.Equal(neutral.Count, posed.Count);
        for (var index = 0; index < neutral.Count; index++)
        {
            var scaled = neutral[index].Position * new Vector2(.8f, .6f);
            var expected = new Vector2(3, -2) + new Vector2(
                scaled.X * MathF.Cos(.7f) - scaled.Y * MathF.Sin(.7f),
                scaled.X * MathF.Sin(.7f) + scaled.Y * MathF.Cos(.7f));
            Assert.True(Vector2.Distance(expected, posed[index].Position) < .001f);
            Assert.Equal(Color.White * .5f, posed[index].Color);
        }
        Assert.Empty(PlayerPaperDoll.Build(Idle with { Opacity = 0 }, appearance));
    }

    [Fact]
    public void RestoredProgress_RebuildsSameLayeredAppearanceWithoutVisualSaveFields()
    {
        var content = PersistenceFixture.LoadContent();
        var source = PersistenceFixture.CreateProgress(content);
        var restored = new ArenaSimulation(content, new Random(19));
        var persistence = new ProgressPersistence(content);
        persistence.Restore(restored, persistence.Capture(source));
        Assert.Equal(PlayerPaperDoll.Build(Idle, PlayerAppearance.From(source.Player.Inventory)),
            PlayerPaperDoll.Build(Idle, PlayerAppearance.From(restored.Player.Inventory)));
    }
}
