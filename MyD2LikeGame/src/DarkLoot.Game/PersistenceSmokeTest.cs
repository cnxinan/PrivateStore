using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;

namespace DarkLoot.Game;

internal sealed class PersistenceSmokeTest : IDisposable
{
    private readonly string _directory;
    private readonly string _logPath;
    private ProgressSnapshot? _expected;
    private int _draws;

    public PersistenceSmokeTest(string logPath)
    {
        _logPath = logPath;
        _directory = Path.Combine(Path.GetTempPath(), $"darkloot-window-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        SavePath = Path.Combine(_directory, "save.json");
    }

    public string SavePath { get; }

    public void RecordExpected(ArenaSimulation arena, GameContent content)
    {
        var expected = new ProgressPersistence(content).Capture(arena);
        var stored = new JsonSaveStore(SavePath).Load();
        if (expected.Items.Count < 2 || expected.EquippedWeaponId is null ||
            expected.EquippedArmorId is null || expected.KnownRecipeIds.Count < 2 ||
            expected.Materials.Count == 0 || expected.Recipes.Count == 0 ||
            stored is null || JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(stored))
        {
            throw new InvalidOperationException("Persistence smoke: saved file does not match actual crafted progress.");
        }
        _expected = expected;
        File.AppendAllText(_logPath, $"PASS persistence saved actual host progression{Environment.NewLine}");
    }

    public bool AfterRestoredDraw(ArenaSimulation arena, GameContent content)
    {
        if (_expected is null ||
            JsonSerializer.Serialize(_expected) !=
            JsonSerializer.Serialize(new ProgressPersistence(content).Capture(arena)) ||
            arena.Player.Position != new Vector2(160, 270) ||
            arena.Player.Health != arena.Player.MaximumHealth ||
            arena.Player.AttackSequence != 0 || arena.Player.IsDead ||
            arena.Player.DeathRemaining != 0 || arena.Player.InvulnerabilityRemaining != 0 ||
            arena.ResetSequence != 0 || arena.RemainingMonsters != 6 ||
            arena.Monsters.Any(monster => monster.Health != monster.MaxHealth || monster.IsWindingUp) ||
            arena.GroundResources.Count != 0 || arena.Projectiles.Count != 0 || arena.Events.Count != 0)
        {
            throw new InvalidOperationException(
                "Persistence smoke: second game session did not restore exact progress into a fresh full-health encounter.");
        }
        if (++_draws < 2)
            return false;
        File.AppendAllText(_logPath,
            $"PASS persistence restored in second game session; draws={_draws}; exact IDs/rolls/slots/resources/knowledge; fresh world{Environment.NewLine}");
        return true;
    }

    public void Dispose()
    {
        File.Delete(SavePath);
        Directory.Delete(_directory);
    }
}
