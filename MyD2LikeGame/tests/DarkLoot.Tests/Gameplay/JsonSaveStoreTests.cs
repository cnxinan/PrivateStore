using System.Text.Json;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class JsonSaveStoreTests
{
    [Fact]
    public void Save_ReplacesFileAndRoundTripsExactRolledItems()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = PersistenceFixture.CreateProgress(content);
        var persistence = new ProgressPersistence(content);
        var store = new JsonSaveStore(files.Path);
        store.Save(persistence.Capture(arena));
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 3);
        var expected = persistence.Capture(arena);

        store.Save(expected);
        var restored = new ArenaSimulation(content, new Random(91));
        persistence.Restore(restored, new JsonSaveStore(files.Path).Load()!);

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(persistence.Capture(restored)));
        Assert.Single(Directory.GetFiles(files.Directory));
    }

    [Fact]
    public void Save_LockedDestinationPreservesOldBytesAndRemovesTemporaryFile()
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var arena = PersistenceFixture.CreateProgress(content);
        var persistence = new ProgressPersistence(content);
        var store = new JsonSaveStore(files.Path);
        store.Save(persistence.Capture(arena));
        var original = File.ReadAllBytes(files.Path);
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 1);
        using (var locked = new FileStream(files.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Assert.Throws<SaveGameException>(() => store.Save(persistence.Capture(arena)));
            Assert.Contains(files.Path, error.Message);
            Assert.IsAssignableFrom<IOException>(error.InnerException);
            Assert.Equal(original, File.ReadAllBytes(files.Path));
            Assert.Single(Directory.GetFiles(files.Directory));
        }
        store.Save(persistence.Capture(arena));
        Assert.NotEqual(original, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"version":1,"version":2}""")]
    public void Load_MalformedOrAmbiguousFileReportsPathWithoutChangingFile(string json)
    {
        using var files = new TemporarySave();
        File.WriteAllText(files.Path, json);

        var error = Assert.Throws<SaveGameException>(() => new JsonSaveStore(files.Path).Load());

        Assert.Contains(files.Path, error.Message);
        Assert.Equal(json, File.ReadAllText(files.Path));
    }

    [Fact]
    public void Load_AbsentFileAloneStartsFreshAndInaccessibleFileFails()
    {
        using var files = new TemporarySave();
        var store = new JsonSaveStore(files.Path);
        Assert.Null(store.Load());
        var content = PersistenceFixture.LoadContent();
        store.Save(new ProgressPersistence(content).Capture(new ArenaSimulation(content, new Random(17))));
        using var locked = new FileStream(files.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<SaveGameException>(() => store.Load());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("numeric-enum")]
    [InlineData("duplicate-resource")]
    public void Load_RejectsInvalidJsonSchema(string defect)
    {
        using var files = new TemporarySave();
        var content = PersistenceFixture.LoadContent();
        var store = new JsonSaveStore(files.Path);
        store.Save(new ProgressPersistence(content).Capture(PersistenceFixture.CreateProgress(content)));
        var json = File.ReadAllText(files.Path);
        json = defect switch
        {
            "missing" => json.Replace("\"version\": 1,", ""),
            "unknown" => json.Replace("\"version\": 1,", "\"version\": 1, \"unexpected\": true,"),
            "numeric-enum" => json.Replace("\"rare\"", "2"),
            "duplicate-resource" => json.Replace("\"materials\": {", "\"materials\": { \"material.bone\": 1,"),
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };
        File.WriteAllText(files.Path, json);

        Assert.Throws<SaveGameException>(() => store.Load());
    }
}

internal sealed class TemporarySave : IDisposable
{
    public TemporarySave()
    {
        Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"darkloot-save-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Directory);
        Path = System.IO.Path.Combine(Directory, "save.json");
    }

    public string Directory { get; }
    public string Path { get; }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
