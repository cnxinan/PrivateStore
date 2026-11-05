using System.Text.Json;
using System.Text.Json.Nodes;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Quests;

namespace DarkLoot.Tests.Gameplay;

public sealed class QuestPersistenceTests
{
    [Fact]
    public void DeathAndRestart_PreserveAcceptedProgressAndSharedState()
    {
        var content = PersistenceFixture.LoadContent();
        content = content with
        {
            Monsters = content.Monsters.ToDictionary(pair => pair.Key,
                pair => pair.Value with { ContactDamage = 100 })
        };
        var session = new DungeonSession(content, new Random(7));
        var persistence = new ProgressPersistence(content);
        persistence.Restore(session.Hub, persistence.Capture(session.Hub) with { Quests = new(0, true, 2) });
        session.Hub.Player.Position = DungeonSession.HubEntrance;
        Assert.True(session.TryTravel());
        var slime = session.Dungeon.Monsters.First();
        session.Arena.Player.Position = slime.Position;
        session.Update(new(System.Numerics.Vector2.Zero, slime.Position, false, false), 0);
        Assert.True(session.Arena.Player.IsDead);
        session.Update(new(System.Numerics.Vector2.Zero, slime.Position, false, false), .6f);
        Assert.False(session.InDungeon);
        Assert.Equal(new QuestProgress(0, true, 2), session.Hub.Quests.Progress);
        session.Restart();
        Assert.Equal(new QuestProgress(0, true, 2), session.Dungeon.Quests.Progress);
    }

    [Theory]
    [InlineData(0, true, 2)]
    [InlineData(1, true, 0)]
    [InlineData(2, true, 1)]
    [InlineData(3, false, 0)]
    public void Session_RestoresExactQuestStateAndFreshWorldCanCreditReusedMonsterIds(int claimed, bool accepted, int kills)
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var persistence = new ProgressPersistence(content);
        var source = QuestServiceTests.CreateArena();
        var snapshot = persistence.Capture(source) with { Quests = new(claimed, accepted, kills) };
        new JsonSaveStore(files.Path).Save(snapshot);
        var target = QuestServiceTests.CreateArena();
        var session = new SaveSession(content, files.Path);
        session.Load(target);
        Assert.Equal(snapshot.Quests, target.Quests.Progress);
        if (claimed == 0)
        {
            QuestServiceTests.Kill(target);
            Assert.Equal(3, target.Quests.Kills);
        }
        target.ResetEncounter();
        session.SaveIfChanged(target);
        session.SaveOnExit(target);
        var restored = QuestServiceTests.CreateArena();
        new SaveSession(content, files.Path).Load(restored);
        Assert.Equal(target.Quests.Progress, restored.Quests.Progress);
    }

    [Fact]
    public void VersionOneJson_MigratesWithoutChangingExistingEquipmentOrResources()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var persistence = new ProgressPersistence(content);
        var source = persistence.Capture(PersistenceFixture.CreateProgress(content));
        new JsonSaveStore(files.Path).Save(source);
        var old = JsonNode.Parse(File.ReadAllText(files.Path))!.AsObject();
        old.Remove("quests");
        old["version"] = 1;
        File.WriteAllText(files.Path, old.ToJsonString());
        var target = new ArenaSimulation(content, new Random(7));
        var session = new SaveSession(content, files.Path);
        session.Load(target);

        Assert.Equal(new QuestProgress(0, false, 0), target.Quests.Progress);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(persistence.Capture(target)));
        session.SaveOnExit(target);
        Assert.Equal(2, new JsonSaveStore(files.Path).Load()!.Version);
    }

    [Theory]
    [InlineData(-1, false, 0)]
    [InlineData(4, false, 0)]
    [InlineData(0, false, 1)]
    [InlineData(0, true, -1)]
    [InlineData(0, true, 4)]
    [InlineData(1, true, 1)]
    [InlineData(2, true, 2)]
    [InlineData(3, true, 0)]
    public void InvalidQuestState_RejectsWholeRestoreWithoutPartialOwnershipChanges(int claimed, bool accepted, int kills)
    {
        var content = PersistenceFixture.LoadContent();
        var persistence = new ProgressPersistence(content);
        var snapshot = persistence.Capture(PersistenceFixture.CreateProgress(content)) with
        {
            Quests = new(claimed, accepted, kills)
        };
        var target = new ArenaSimulation(content, new Random(7));
        var before = JsonSerializer.Serialize(persistence.Capture(target));

        Assert.Throws<SaveGameException>(() => persistence.Restore(target, snapshot));
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Capture(target)));
    }

    [Fact]
    public void VersionTwoMissingQuests_RefusesLoadAndPreservesOriginalSave()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var persistence = new ProgressPersistence(content);
        new JsonSaveStore(files.Path).Save(persistence.Capture(PersistenceFixture.CreateProgress(content)));
        var json = JsonNode.Parse(File.ReadAllText(files.Path))!.AsObject();
        json.Remove("quests");
        File.WriteAllText(files.Path, json.ToJsonString());
        var before = File.ReadAllBytes(files.Path);
        var session = new SaveSession(content, files.Path);
        var target = new ArenaSimulation(content, new Random(7));

        Assert.Throws<SaveGameException>(() => session.Load(target));
        session.SaveOnExit(target);
        Assert.Equal(before, File.ReadAllBytes(files.Path));
    }
}
