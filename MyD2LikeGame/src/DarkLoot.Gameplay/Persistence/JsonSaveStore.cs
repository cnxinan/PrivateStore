using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkLoot.Gameplay.Persistence;

public sealed class JsonSaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public JsonSaveStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public ProgressSnapshot? Load()
    {
        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(stream);
            RejectDuplicateProperties(document.RootElement, "$");
            return document.Deserialize<ProgressSnapshot>(Options)
                ?? throw new JsonException("The save root must be a progression object.");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new SaveGameException($"Cannot load save '{Path}': {exception.Message}", exception);
        }
    }

    public void Save(ProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        var temporary = System.IO.Path.Combine(directory, $".save-{Guid.NewGuid():N}.tmp");
        var createdTemporary = false;
        Exception? failure = null;
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                createdTemporary = true;
                JsonSerializer.Serialize(stream, snapshot, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(Path))
                File.Replace(temporary, Path, destinationBackupFileName: null);
            else
                File.Move(temporary, Path);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            failure = exception;
        }
        finally
        {
            if (createdTemporary)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (IsStorageFailure(exception))
                {
                    failure = failure is null ? exception : new AggregateException(failure, exception);
                }
            }
        }

        if (failure is not null)
            throw new SaveGameException($"Cannot write save '{Path}': {failure.Message}", failure);
    }

    internal static string Serialize(ProgressSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException;

    private static void RejectDuplicateProperties(JsonElement element, string field)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException($"Duplicate save property '{field}.{property.Name}'.");
                RejectDuplicateProperties(property.Value, $"{field}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var value in element.EnumerateArray())
                RejectDuplicateProperties(value, $"{field}[{index++}]");
        }
    }
}
