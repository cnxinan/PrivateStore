using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Loot;

namespace DarkLoot.Tests.Gameplay;

public sealed class KillRewardServiceTests
{
    [Fact]
    public void Resolve_DuplicateAndStaleKillsDoNotConsumeRandomness()
    {
        var content = MixedEncounterFixture.CreateContent();
        var random = new CountingRandom();
        var service = new KillRewardService(content, random);
        var kill = new KillRecord(0, "slime.1", new Vector2(190, 270), "loot.training_empty");

        service.BeginRound(0);

        var first = service.Resolve(kill);
        Assert.Equal(KillRewardStatus.Granted, first.Status);
        Assert.Empty(first.Items);
        var afterFirst = random.Calls;

        var duplicate = service.Resolve(kill);
        Assert.Equal(KillRewardStatus.AlreadyResolved, duplicate.Status);
        Assert.Empty(duplicate.Items);
        Assert.Equal(afterFirst, random.Calls);

        service.BeginRound(1);
        Assert.Equal(KillRewardStatus.WrongRound, service.Resolve(kill).Status);
        Assert.Equal(
            KillRewardStatus.WrongRound,
            service.Resolve(kill with { RoundId = 2 }).Status);
        Assert.Equal(afterFirst, random.Calls);

        var nextRound = service.Resolve(kill with { RoundId = 1 });
        Assert.Equal(KillRewardStatus.Granted, nextRound.Status);
        Assert.True(random.Calls > afterFirst);
    }

    [Fact]
    public void Resolve_GenerationExceptionDoesNotRecordSuccess()
    {
        var fixture = MixedEncounterFixture.CreateContent();
        var tables = fixture.LootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["broken"] = new LootTableDefinition(
            "broken",
            1,
            1,
            [new LootEntryDefinition("missing", 1)]);
        var content = new GameContent(fixture.Items, fixture.Monsters, tables, fixture.Affixes);
        var service = new KillRewardService(content, new Random(17));
        var kill = new KillRecord(0, "slime.1", Vector2.Zero, "broken");

        service.BeginRound(0);

        Assert.Throws<ArgumentException>(() => service.Resolve(kill));

        var retried = service.Resolve(kill with { LootTableId = "loot.training_empty" });
        Assert.Equal(KillRewardStatus.Granted, retried.Status);
        Assert.Empty(retried.Items);
    }

    [Fact]
    public void Resolve_GrantsASeparateItemForEachMonsterAndDeduplicatesByMonsterInstance()
    {
        var content = WithGuaranteedDrop();
        var service = new KillRewardService(content, new Random(17));
        service.BeginRound(0);

        var first = service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "loot.training_drop"));
        var second = service.Resolve(
            new KillRecord(0, "slime.2", Vector2.One, "loot.training_drop"));

        Assert.Equal(KillRewardStatus.Granted, first.Status);
        Assert.Equal("weapon.training_blade", Assert.Single(first.Items).DefinitionId);
        Assert.Equal(KillRewardStatus.Granted, second.Status);
        Assert.Equal("weapon.training_blade", Assert.Single(second.Items).DefinitionId);
        Assert.NotEqual(first.Items[0].InstanceId, second.Items[0].InstanceId);

        var duplicate = service.Resolve(
            new KillRecord(0, "slime.1", Vector2.One, "loot.training_drop"));
        Assert.Equal(KillRewardStatus.AlreadyResolved, duplicate.Status);
        Assert.Empty(duplicate.Items);
    }

    [Fact]
    public void Resolve_RejectsMissingLootTable()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));
        service.BeginRound(0);

        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "missing")));
    }

    [Fact]
    public void Resolve_RequiresBeginRoundBeforeResolving()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));

        Assert.Throws<InvalidOperationException>(() => service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "loot.training_empty")));
    }

    [Fact]
    public void Constructor_RejectsNullContentOrRandom()
    {
        Assert.Throws<ArgumentNullException>(() => new KillRewardService(null!, new Random(17)));
        Assert.Throws<ArgumentNullException>(() =>
            new KillRewardService(MixedEncounterFixture.CreateContent(), null!));
    }

    [Fact]
    public void BeginRound_RequiresStrictlyIncreasingNonNegativeIdsWithoutClearingCurrentRound()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));
        service.BeginRound(0);
        var firstKill = new KillRecord(0, "slime.1", Vector2.Zero, "loot.training_empty");
        Assert.Equal(KillRewardStatus.Granted, service.Resolve(firstKill).Status);
        service.BeginRound(1);
        var currentKill = firstKill with { RoundId = 1 };
        Assert.Equal(KillRewardStatus.Granted, service.Resolve(currentKill).Status);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.BeginRound(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.BeginRound(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.BeginRound(-1));

        Assert.Equal(KillRewardStatus.AlreadyResolved, service.Resolve(currentKill).Status);
    }

    [Fact]
    public void BeginRound_RejectsNonzeroInitialIdWithoutChangingState()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.BeginRound(1));

        service.BeginRound(0);
        Assert.Equal(
            KillRewardStatus.Granted,
            service.Resolve(new KillRecord(0, "slime.1", Vector2.Zero, "loot.training_empty")).Status);
    }

    [Fact]
    public void Resolve_RejectsInvalidKillFields()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));
        service.BeginRound(0);

        Assert.Throws<ArgumentNullException>(() => service.Resolve(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Resolve(
            new KillRecord(-1, "slime.1", Vector2.Zero, "loot.training_empty")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "", Vector2.Zero, "loot.training_empty")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", new Vector2(float.NaN, 0), "loot.training_empty")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", new Vector2(0, float.PositiveInfinity), "loot.training_empty")));
    }

    [Fact]
    public void Resolve_OnlyDeduplicatesTheCurrentRoundAcrossManyRounds()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));

        for (long roundId = 0; roundId < 100; roundId++)
        {
            service.BeginRound(roundId);
            var kill = new KillRecord(roundId, "slime.1", Vector2.Zero, "loot.training_empty");

            Assert.Equal(KillRewardStatus.Granted, service.Resolve(kill).Status);
            Assert.Equal(KillRewardStatus.AlreadyResolved, service.Resolve(kill).Status);
            if (roundId > 0)
            {
                Assert.Equal(
                    KillRewardStatus.WrongRound,
                    service.Resolve(kill with { RoundId = roundId - 1 }).Status);
            }
        }
    }

    private static GameContent WithGuaranteedDrop()
    {
        var fixture = MixedEncounterFixture.CreateContent();
        var tables = fixture.LootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["loot.training_drop"] = new LootTableDefinition(
            "loot.training_drop",
            1,
            1,
            [new LootEntryDefinition("weapon.training_blade", 1) { Guaranteed = true }]);
        return new GameContent(fixture.Items, fixture.Monsters, tables, fixture.Affixes);
    }

    private sealed class CountingRandom : Random
    {
        public int Calls { get; private set; }

        public override long NextInt64(long minValue, long maxValue)
        {
            Calls++;
            return minValue;
        }

        public override long NextInt64(long maxValue)
        {
            Calls++;
            return 0;
        }
    }
}
