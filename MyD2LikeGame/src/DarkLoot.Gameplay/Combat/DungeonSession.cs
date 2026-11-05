using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Combat;

public sealed class DungeonSession
{
    public const int RoomStride = 1120;
    public static Vector2 HubEntrance { get; } = new(900, 270);
    public static Vector2 DungeonEntrance { get; } = new(64, 270);
    public static Vector2 BossExit { get; } = new(3100, 270);
    public ArenaSimulation Hub { get; }
    public ArenaSimulation Dungeon { get; }
    public ArenaSimulation Arena => InDungeon ? Dungeon : Hub;
    public bool InDungeon { get; private set; }
    public bool Completed => Dungeon.Monsters.Single(m => m.InstanceId == "boss.guardian").IsDefeated;

    public DungeonSession(GameContent content, Random random)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        Hub = new(content, random, new("encounter.cathedral_hub", []), WorldMap.Hub(), null);
        var spawns = new List<MonsterSpawnDefinition>();
        foreach (var (id, prefix, offset) in new[]
        {
            ("encounter.crypt_entry", "entry.", 0),
            ("encounter.crypt_battle", "battle.", RoomStride),
            ("encounter.crypt_boss", "boss.", RoomStride * 2)
        })
        {
            if (!content.Encounters.TryGetValue(id, out var encounter))
                throw new ArgumentException($"Missing dungeon encounter '{id}'.", nameof(content));
            spawns.AddRange(encounter.Spawns.Select(spawn => spawn with
            {
                InstanceId = prefix + spawn.InstanceId,
                X = spawn.X + offset
            }));
        }
        Dungeon = new(content, random, new("encounter.crypt", spawns.AsReadOnly()), WorldMap.Crypt(), Hub);
    }

    public void Update(PlayerIntent intent, float seconds)
    {
        var wasDead = Arena.Player.IsDead;
        Arena.Update(intent, seconds);
        Dungeon.Map!.BossGateOpen = Dungeon.Monsters
            .Where(m => m.InstanceId.StartsWith("battle.", StringComparison.Ordinal)).All(m => m.IsDefeated);
        if (InDungeon && wasDead && !Arena.Player.IsDead)
        {
            Dungeon.Map.BossGateOpen = false;
            InDungeon = false;
            Hub.Player.Position = new(160, 270);
        }
    }

    public bool TryTravel()
    {
        var player = Arena.Player;
        if (player.IsDead || player.Reaction.IsActive)
            return false;
        if (!InDungeon && Near(player.Position, HubEntrance))
        {
            InDungeon = true;
            player.Position = new(160, 270);
        }
        else if (InDungeon && (Near(player.Position, DungeonEntrance) ||
                 Completed && Near(player.Position, BossExit)))
        {
            InDungeon = false;
            player.Position = new(800, 270);
        }
        else
            return false;
        player.Reaction.Clear();
        player.Facing = Vector2.UnitX;
        return true;
    }

    public void Restart()
    {
        if (Arena.Player.IsDead)
            return;
        Dungeon.ResetEncounter();
        Dungeon.Map!.BossGateOpen = false;
        if (InDungeon)
            Arena.Player.Position = new(160, 270);
    }

    private static bool Near(Vector2 position, Vector2 portal) =>
        Vector2.DistanceSquared(position, portal) <= 28 * 28;
}
