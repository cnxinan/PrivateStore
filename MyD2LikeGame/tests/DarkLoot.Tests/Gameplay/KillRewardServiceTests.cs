using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Loot;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class KillRewardServiceTests
{
    [Fact]
    public void Resolve_DuplicateAndStaleKillsDoNotConsumeRandomness()
    {
        var content = MixedEncounterFixture.CreateContent();
        var random = new CountingRandom();
        var service = new KillRewardService(content, random);
        var kill = new KillRecord(0, "slime.1", new Vector2(190, 270), "resource.slime");

        service.BeginRound(0);

        var first = service.Resolve(kill);
        Assert.Equal(KillRewardStatus.Granted, first.Status);
        Assert.Equal(new ResourceStack(ResourceKind.Material, "material.slime", 1),
            Assert.Single(first.Resources));
        var afterFirst = random.Calls;

        var duplicate = service.Resolve(kill);
        Assert.Equal(KillRewardStatus.AlreadyResolved, duplicate.Status);
        Assert.Empty(duplicate.Resources);
        Assert.Equal(afterFirst, random.Calls);

        service.BeginRound(1);
        Assert.Equal(KillRewardStatus.WrongRound, service.Resolve(kill).Status);
        Assert.Equal(KillRewardStatus.WrongRound,
            service.Resolve(kill with { RoundId = 2 }).Status);
        Assert.Equal(afterFirst, random.Calls);

        var nextRound = service.Resolve(kill with { RoundId = 1 });
        Assert.Equal(KillRewardStatus.Granted, nextRound.Status);
        Assert.Single(nextRound.Resources);
        Assert.True(random.Calls > afterFirst);
    }

    [Fact]
    public void Resolve_GenerationExceptionDoesNotRecordSuccess()
    {
        var fixture = MixedEncounterFixture.CreateContent();
        var tables = fixture.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["broken"] = new ResourceLootTableDefinition("broken", "material.missing", 1, 1, 0, []);
        var service = new KillRewardService(
            fixture with { ResourceLootTableDefinitions = tables }, new Random(17));
        service.BeginRound(0);
        var kill = new KillRecord(0, "slime.1", Vector2.Zero, "broken");

        Assert.Throws<ArgumentException>(() => service.Resolve(kill));

        var retried = service.Resolve(kill with { LootTableId = "resource.slime" });
        Assert.Equal(KillRewardStatus.Granted, retried.Status);
        Assert.Single(retried.Resources);
    }

    [Fact]
    public void Resolve_GrantsSeparateResourceResultsAndDeduplicatesByMonsterInstance()
    {
        var fixture = MixedEncounterFixture.CreateContent();
        var tables = fixture.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["resource.test_guaranteed"] = new ResourceLootTableDefinition(
            "resource.test_guaranteed", "material.slime", 1, 1, 0, []);
        var service = new KillRewardService(
            fixture with { ResourceLootTableDefinitions = tables }, new Random(17));
        service.BeginRound(0);

        var first = service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "resource.test_guaranteed"));
        var second = service.Resolve(
            new KillRecord(0, "slime.2", Vector2.One, "resource.test_guaranteed"));

        Assert.Equal(KillRewardStatus.Granted, first.Status);
        Assert.Equal(new ResourceStack(ResourceKind.Material, "material.slime", 1),
            Assert.Single(first.Resources));
        Assert.Equal(KillRewardStatus.Granted, second.Status);
        Assert.Equal(first.Resources, second.Resources);

        var duplicate = service.Resolve(
            new KillRecord(0, "slime.1", Vector2.One, "resource.test_guaranteed"));
        Assert.Equal(KillRewardStatus.AlreadyResolved, duplicate.Status);
        Assert.Empty(duplicate.Resources);
    }

    [Fact]
    public void Resolve_RejectsMissingResourceLootTable()
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
            new KillRecord(0, "slime.1", Vector2.Zero, "resource.slime")));
    }

    [Fact]
    public void Constructor_RejectsNullContentOrRandom()
    {
        Assert.Throws<ArgumentNullException>(() => new KillRewardService(null!, new Random(17)));
        Assert.Throws<ArgumentNullException>(() =>
            new KillRewardService(MixedEncounterFixture.CreateContent(), null!));
    }

    [Fact]
    public void BeginRound_RequiresStrictlyIncreasingIdsAndClearsCurrentRound()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));
        service.BeginRound(0);
        var firstKill = new KillRecord(0, "slime.1", Vector2.Zero, "resource.slime");
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
    public void Resolve_RejectsInvalidKillFields()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));
        service.BeginRound(0);

        Assert.Throws<ArgumentNullException>(() => service.Resolve(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Resolve(
            new KillRecord(-1, "slime.1", Vector2.Zero, "resource.slime")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "", Vector2.Zero, "resource.slime")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", Vector2.Zero, "")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", new Vector2(float.NaN, 0), "resource.slime")));
        Assert.Throws<ArgumentException>(() => service.Resolve(
            new KillRecord(0, "slime.1", new Vector2(0, float.PositiveInfinity), "resource.slime")));
    }

    [Fact]
    public void Resolve_OnlyDeduplicatesTheCurrentRoundAcrossManyRounds()
    {
        var service = new KillRewardService(MixedEncounterFixture.CreateContent(), new Random(17));

        for (long roundId = 0; roundId < 100; roundId++)
        {
            service.BeginRound(roundId);
            var kill = new KillRecord(roundId, "slime.1", Vector2.Zero, "resource.slime");

            Assert.Equal(KillRewardStatus.Granted, service.Resolve(kill).Status);
            Assert.Equal(KillRewardStatus.AlreadyResolved, service.Resolve(kill).Status);
            if (roundId > 0)
            {
                Assert.Equal(KillRewardStatus.WrongRound,
                    service.Resolve(kill with { RoundId = roundId - 1 }).Status);
            }
        }
    }

    private sealed class CountingRandom : Random
    {
        public int Calls { get; private set; }

        public override long NextInt64(long minValue, long maxValue)
        {
            Calls++;
            return minValue;
        }

        public override int Next(int maxValue)
        {
            Calls++;
            return 0;
        }
    }
}
