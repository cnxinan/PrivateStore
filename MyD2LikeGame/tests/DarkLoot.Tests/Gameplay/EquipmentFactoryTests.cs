using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Tests.Gameplay;

public sealed class EquipmentFactoryTests
{
    [Theory]
    [InlineData("weapon.training_blade", ItemRarity.Common, 0)]
    [InlineData("weapon.training_blade", ItemRarity.Magic, 1)]
    [InlineData("weapon.training_blade", ItemRarity.Rare, 2)]
    [InlineData("armor.training_vest", ItemRarity.Rare, 2)]
    public void Create_FixedQualityHasValidDistinctImmutableAffixes(
        string itemId, ItemRarity rarity, int count)
    {
        var content = MixedEncounterFixture.CreateContent();
        var factory = new EquipmentFactory();

        var first = factory.Create(itemId, rarity, content, new Random(17));
        var second = factory.Create(itemId, rarity, content, new Random(17));

        Assert.Equal(itemId, first.DefinitionId);
        Assert.Equal(rarity, first.Rarity);
        Assert.Equal(count, first.Rolls.Count);
        Assert.Equal(count, first.Rolls.Select(roll => roll.AffixId).Distinct().Count());
        Assert.Equal(first.Rolls, second.Rolls);
        Assert.NotEqual(Guid.Empty, first.InstanceId);
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.All(first.Rolls, roll =>
        {
            var definition = content.Affixes[roll.AffixId];
            Assert.Contains(content.Items[itemId].Slot, definition.Slots);
            Assert.Equal(definition.Stat, roll.Stat);
            Assert.InRange(roll.Value, definition.MinValue, definition.MaxValue);
        });
        var rolls = Assert.IsAssignableFrom<IList<AffixRoll>>(first.Rolls);
        Assert.Throws<NotSupportedException>(() =>
            rolls.Add(new AffixRoll("fake", ItemStat.Attack, 1)));
        _ = new ItemStatCalculator().GetContribution(first, content);
    }

    [Theory]
    [InlineData(ItemRarity.Common, 0)]
    [InlineData(ItemRarity.Magic, 2)]
    [InlineData(ItemRarity.Rare, 4)]
    public void Create_DoesNotRollRarity(ItemRarity rarity, int expectedIntegerCalls)
    {
        var random = new AffixOnlyRandom();

        var item = new EquipmentFactory().Create(
            "weapon.training_blade", rarity, MixedEncounterFixture.CreateContent(), random);

        Assert.Equal(rarity, item.Rarity);
        Assert.Equal(expectedIntegerCalls, random.IntegerCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("weapon.missing")]
    public void Create_RejectsInvalidItemIdBeforeRandomness(string? itemId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new EquipmentFactory().Create(
            itemId!, ItemRarity.Rare, MixedEncounterFixture.CreateContent(), new NoRandom()));
    }

    [Fact]
    public void Create_RejectsNullArgumentsBeforeRandomness()
    {
        var factory = new EquipmentFactory();
        var content = MixedEncounterFixture.CreateContent();

        Assert.Throws<ArgumentNullException>(() =>
        {
            factory.Create("weapon.training_blade", ItemRarity.Rare, null!, new NoRandom());
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            factory.Create("weapon.training_blade", ItemRarity.Rare, content, null!);
        });
    }

    [Fact]
    public void Create_RejectsUnknownRarityBeforeRandomness()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EquipmentFactory().Create(
            "weapon.training_blade", (ItemRarity)99,
            MixedEncounterFixture.CreateContent(), new NoRandom()));
    }

    [Fact]
    public void Create_RejectsInsufficientSlotSpecificPoolBeforeRandomness()
    {
        var content = MixedEncounterFixture.CreateContent() with
        {
            AffixDefinitions = new Dictionary<string, AffixDefinition>
            {
                ["affix.keen"] = new("affix.keen", "Keen", ItemStat.Attack, 1, 5, [ItemSlot.Weapon]),
                ["affix.vital"] = new("affix.vital", "Vital", ItemStat.MaximumHealth, 1, 5, [ItemSlot.Armor])
            }
        };

        var error = Assert.Throws<ArgumentException>(() => new EquipmentFactory().Create(
            "weapon.training_blade", ItemRarity.Rare, content, new NoRandom()));

        Assert.Contains("affix", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Create_RejectsInvalidItemContentBeforeRandomness(int invalidCase)
    {
        var content = MixedEncounterFixture.CreateContent();
        var definition = content.Items["weapon.training_blade"];
        content = content with
        {
            Items = invalidCase == 0 ? null! : new Dictionary<string, ItemDefinition>
            {
                ["weapon.training_blade"] = invalidCase switch
                {
                    1 => null!,
                    2 => definition with { Slot = (ItemSlot)99 },
                    _ => definition with { AttackBonus = ItemDefinition.MaxAttackBonus + 1 }
                }
            }
        };

        Assert.Throws<ArgumentException>(() => new EquipmentFactory().Create(
            "weapon.training_blade", ItemRarity.Rare, content, new NoRandom()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Create_RejectsInvalidAffixContentBeforeRandomness(int invalidCase)
    {
        var content = MixedEncounterFixture.CreateContent();
        var definition = content.Affixes["affix.keen"];
        var invalid = invalidCase switch
        {
            0 => null,
            1 => definition with { Id = "affix.other" },
            2 => definition with { Name = "" },
            3 => definition with { Stat = (ItemStat)99 },
            4 => definition with { NameKind = (AffixNameKind)99 },
            5 => definition with { Slots = null! },
            6 => definition with { Slots = [ItemSlot.Weapon, ItemSlot.Weapon] },
            7 => definition with { MinValue = 6, MaxValue = 5 },
            _ => definition with { MaxValue = AffixDefinition.MaxMagnitude + 1 }
        };
        var affixes = content.Affixes.ToDictionary(pair => pair.Key, pair => pair.Value);
        affixes["affix.keen"] = invalid!;
        content = content with { AffixDefinitions = affixes };

        Assert.Throws<ArgumentException>(() => new EquipmentFactory().Create(
            "weapon.training_blade", ItemRarity.Rare, content, new NoRandom()));
    }

    private sealed class AffixOnlyRandom : Random
    {
        public int IntegerCalls { get; private set; }

        public override double NextDouble() =>
            throw new InvalidOperationException("Fixed-quality creation must not roll rarity.");

        public override long NextInt64(long maxValue)
        {
            IntegerCalls++;
            return 0;
        }

        public override long NextInt64(long minValue, long maxValue)
        {
            IntegerCalls++;
            return minValue;
        }
    }

    private sealed class NoRandom : Random
    {
        public override double NextDouble() => throw new InvalidOperationException("Unexpected RNG.");
        public override long NextInt64(long maxValue) => throw new InvalidOperationException("Unexpected RNG.");
        public override long NextInt64(long minValue, long maxValue) =>
            throw new InvalidOperationException("Unexpected RNG.");
    }
}
