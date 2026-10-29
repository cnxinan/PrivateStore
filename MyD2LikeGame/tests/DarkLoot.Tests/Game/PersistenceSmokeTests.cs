using DarkLoot.Game;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Tests.Gameplay;

namespace DarkLoot.Tests.Game;

public sealed class PersistenceSmokeTests
{
    [Fact]
    public void Probe_RequiresSeparateSessionToRestoreExactLiveProgressAndFreshWorld()
    {
        var log = Path.Combine(Path.GetTempPath(), $"darkloot-persistence-{Guid.NewGuid():N}.log");
        try
        {
            using var probe = new PersistenceSmokeTest(log);
            Assert.StartsWith(Path.GetTempPath(), probe.SavePath, StringComparison.OrdinalIgnoreCase);
            var content = PersistenceFixture.LoadContent();
            var source = PersistenceFixture.CreateProgress(content);
            var session = new SaveSession(content, probe.SavePath);
            session.Load(new ArenaSimulation(content, new Random(91)));
            session.SaveIfChanged(source);
            session.SaveOnExit(source);
            probe.RecordExpected(source, content);
            var empty = new ArenaSimulation(content, new Random(91));
            Assert.Throws<InvalidOperationException>(() => probe.AfterRestoredDraw(empty, content));
            var restored = new ArenaSimulation(content, new Random(91));
            new SaveSession(content, probe.SavePath).Load(restored);

            Assert.False(probe.AfterRestoredDraw(restored, content));
            Assert.True(probe.AfterRestoredDraw(restored, content));
            Assert.Contains("PASS persistence restored", File.ReadAllText(log));
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void Probe_RejectsHostThatDidNotSaveItsActualProgress()
    {
        var log = Path.Combine(Path.GetTempPath(), $"darkloot-persistence-{Guid.NewGuid():N}.log");
        try
        {
            using var probe = new PersistenceSmokeTest(log);
            var content = PersistenceFixture.LoadContent();
            var source = PersistenceFixture.CreateProgress(content);
            new JsonSaveStore(probe.SavePath).Save(new ProgressPersistence(content).Capture(
                new ArenaSimulation(content, new Random(17))));

            Assert.Throws<InvalidOperationException>(() => probe.RecordExpected(source, content));
        }
        finally
        {
            File.Delete(log);
        }
    }
}
