using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Gameplay.Persistence;

public sealed class SaveSession
{
    private readonly ProgressPersistence _progress;
    private readonly JsonSaveStore _store;
    private bool _loadAttempted;
    private bool _loaded;
    private bool _faulted;
    private string? _lastSaved;

    public SaveSession(GameContent content, string path)
    {
        _progress = new ProgressPersistence(content);
        _store = new JsonSaveStore(path);
    }

    public void Load(ArenaSimulation arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (_loadAttempted)
            throw new InvalidOperationException("A save session can only load once.");
        _loadAttempted = true;
        try
        {
            var snapshot = _store.Load();
            if (snapshot is not null)
                _progress.Restore(arena, snapshot);
            _lastSaved = JsonSaveStore.Serialize(_progress.Capture(arena));
            _loaded = true;
        }
        catch (SaveGameException exception)
        {
            _faulted = true;
            throw new SaveGameException($"Cannot restore progress from '{_store.Path}': {exception.Message}", exception);
        }
    }

    public void SaveIfChanged(ArenaSimulation arena)
    {
        if (!_loaded || _faulted)
            throw new InvalidOperationException("Saving requires a successfully loaded, non-faulted session.");
        Save(arena, force: false);
    }

    public void SaveOnExit(ArenaSimulation arena)
    {
        // Never overwrite an unreadable save or retry a failed write during shutdown.
        if (_loaded && !_faulted)
            Save(arena, force: true);
    }

    private void Save(ArenaSimulation arena, bool force)
    {
        ArgumentNullException.ThrowIfNull(arena);
        try
        {
            var snapshot = _progress.Capture(arena);
            var serialized = JsonSaveStore.Serialize(snapshot);
            if (!force && string.Equals(serialized, _lastSaved, StringComparison.Ordinal))
                return;
            _store.Save(snapshot);
            _lastSaved = serialized;
        }
        catch (SaveGameException)
        {
            _faulted = true;
            throw;
        }
    }
}
