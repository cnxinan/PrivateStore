using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class DungeonWindowSmoke : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"darkloot-dungeon-{Guid.NewGuid():N}");
    private readonly string _log = Path.Combine(AppContext.BaseDirectory, "dungeon-smoke.log");
    private int _stage;
    private int _phase;
    private int _updates;
    private int _pauseTicks;
    private int _pauseDraws;
    private int _rooms;
    private bool _hubDraw;
    private bool _restarted;
    private bool _sawDeath;
    private int _restoreDraws;
    private string? _paused;
    private ProgressSnapshot? _expected;
    private Guid _weapon;
    private string? _ownedBeforeDeath;
    private int _lastDungeonHealth;
    private bool _closedGateDraw;
    private bool _openGateDraw;
    private bool _warriorDraw;
    private bool _cultistDraw;
    private bool _magicDraw;
    public bool Restoring { get; set; }
    public string SavePath => Path.Combine(_directory, "save.json");

    public DungeonWindowSmoke()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_log, $"Dungeon actual-window smoke {DateTime.UtcNow:O}{Environment.NewLine}");
    }

    internal GameContent CreateContent(GameContent production) => production with
    {
        Monsters = production.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MoveSpeed = .01f,
            MaxHealth = pair.Key == "monster.fast_wolf" ? 1 : pair.Value.MaxHealth,
            ContactDamage = pair.Key == "monster.fast_wolf" ? 10 : 0,
            RangedAttack = pair.Value.RangedAttack is null ? null : pair.Value.RangedAttack with { Damage = 0 }
        }),
        ResourceLootTableDefinitions = production.ResourceLootTables.ToDictionary(pair => pair.Key,
            pair => pair.Value with { RecipeChancePercent = 100, RecipeIds = ["recipe.magic_blade"] })
    };

    internal void ValidateFont(SpriteFont font, GameContent content)
    {
        var strings = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(content.Monsters.Values.Select(monster => monster.Name));
        Check(strings.SelectMany(text => text).All(font.Characters.Contains),
            "actual loaded font covers dungeon text, guardian and new enemy names");
    }

    internal GameInputSnapshot NextInput(DungeonSession session, InventoryPanel inventory,
        BlacksmithPanel smith, GameContent content, ActorPose pose, double ambience)
    {
        if (++_updates > 3600)
            throw new InvalidOperationException($"Dungeon smoke timed out: stage={_stage}; phase={_phase}; " +
                $"position={session.Arena.Player.Position}; remaining={session.Arena.RemainingMonsters}.");
        var arena = session.Arena;
        if (_updates % 120 == 0)
            File.AppendAllText(_log, $"Progress updates={_updates}; stage={_stage}; phase={_phase}; " +
                $"position={arena.Player.Position}; monsters={arena.RemainingMonsters}; " +
                $"smith={smith.LastResult?.Status}{Environment.NewLine}");
        var player = arena.Player;
        var movement = NVector.Zero;
        var aim = player.Position + NVector.UnitX;
        var attack = false;
        var pickup = false;
        var toggle = false;
        var escape = false;
        var interact = false;
        var restart = false;
        var pointer = NVector.Zero;
        switch (_stage)
        {
            case 0:
                if (session.InDungeon)
                    _stage = 1;
                else
                    movement = Towards(session, DungeonSession.HubEntrance, 8);
                break;
            case 1:
                if (!inventory.IsOpen && _pauseTicks == 0)
                    toggle = true;
                else if (inventory.IsOpen)
                {
                    var snapshot = Snapshot(session) + $"|{pose}|{ambience:R}";
                    _paused ??= snapshot;
                    Check(snapshot == _paused, "inventory freezes dungeon actors, poses, arrows and ambience");
                    if (++_pauseTicks >= 20 && (_rooms & 1) != 0 && _pauseDraws >= 2)
                    {
                        escape = true;
                        _stage = 2;
                    }
                }
                break;
            case 2:
                if (session.InDungeon)
                    _lastDungeonHealth = player.Health;
                if (arena.GroundResources.Count > 0)
                {
                    var drop = arena.GroundResources.MinBy(drop => NVector.DistanceSquared(drop.Position, player.Position))!;
                    movement = Towards(session, drop.Position, 20);
                    pickup = true;
                    break;
                }
                var target = arena.Monsters.Where(monster => !monster.IsDefeated &&
                    (arena.Map!.BossGateOpen || monster.InstanceId != "boss.guardian"))
                    .MinBy(monster => NVector.DistanceSquared(monster.Position, player.Position));
                if (target is not null)
                {
                    aim = target.Position;
                    attack = NVector.DistanceSquared(player.Position, aim) <= 48 * 48 &&
                        arena.Map!.HasLineOfSight(player.Position, aim);
                    movement = Towards(session, aim, 38);
                }
                else
                {
                    Check(session.Completed && arena.Map!.BossGateOpen,
                        $"real kills unlocked guardian gate and completion exit; dungeon={session.InDungeon}; " +
                        $"health={player.Health}; remaining={session.Dungeon.RemainingMonsters}");
                    _stage = 3;
                }
                break;
            case 3:
                if (session.InDungeon)
                    movement = Towards(session, DungeonSession.BossExit, 8);
                else
                {
                    Check(session.Dungeon.IsCleared, "completion exit returned to safe cathedral without respawning enemies");
                    Check(player.Health == _lastDungeonHealth, "ordinary completion trip did not heal");
                    Check(player.Resources.GetCount(DarkLoot.Gameplay.Resources.ResourceKind.Recipe,
                        "recipe.magic_blade") > 0, "collected real recipe drops before leaving dungeon");
                    _stage = 4;
                }
                break;
            case 4:
                if (_phase == 0)
                {
                    movement = Towards(session, DarkLoot.Gameplay.Crafting.BlacksmithService.Position, 28);
                    if (arena.BlacksmithOperations.CanInteract(player))
                    {
                        interact = true;
                        _phase = 1;
                    }
                }
                else if (_phase <= 3)
                {
                    var row = content.Recipes.Keys.Order(StringComparer.Ordinal).ToList().IndexOf("recipe.magic_blade");
                    if (smith.SelectedRecipeIndex != row)
                        Click(smith.RowBounds(1120, row));
                    else if (!arena.Blacksmith.HasLearned("recipe.magic_blade"))
                        Click(smith.LearnButtonBounds(1120, row));
                    else if (player.Inventory.Items.Count == 0)
                        Click(smith.CraftButtonBounds(1120, row));
                    else
                    {
                        _weapon = player.Inventory.Items[0].InstanceId;
                        _phase = 4;
                        interact = true;
                    }
                }
                else if (_phase == 4)
                {
                    if (inventory.Page != InventoryPage.Equipment)
                        Click(inventory.EquipmentTabBounds(1120));
                    else if (player.Inventory.EquippedWeaponId != _weapon)
                        Click(inventory.RowBounds(1120, 0));
                    else
                    {
                        Check(player.Inventory.EquippedWeaponId == _weapon, "real dungeon loot was learned, crafted and equipped at the hub");
                        escape = true;
                        _stage = 5;
                    }
                }
                break;
            case 5:
                if (!_restarted)
                {
                    _ownedBeforeDeath = JsonSerializer.Serialize(new ProgressPersistence(content).Capture(arena));
                    restart = true;
                    _restarted = true;
                }
                else if (!session.InDungeon && !_sawDeath)
                    movement = Towards(session, DungeonSession.HubEntrance, 8);
                else if (session.InDungeon && !player.IsDead)
                {
                    var wolf = arena.Monsters.First(monster => monster.Id == "monster.fast_wolf");
                    movement = Towards(session, wolf.Position, 0);
                }
                else if (player.IsDead)
                {
                    _sawDeath = true;
                    movement = NVector.UnitX;
                    attack = pickup = toggle = restart = true;
                }
                else
                {
                    Check(!session.InDungeon && session.Dungeon.Monsters.All(monster => monster.Health == monster.MaxHealth) &&
                        !session.Dungeon.Map!.BossGateOpen && session.Dungeon.GroundResources.Count == 0 &&
                        player.Health == player.MaximumHealth && player.Inventory.EquippedWeaponId == _weapon,
                        "actual death wait returned to cathedral, reset dungeon and retained equipped progress");
                    Check(_ownedBeforeDeath == JsonSerializer.Serialize(new ProgressPersistence(content).Capture(arena)),
                        "death retained exact items, rolls, resources and learned recipes");
                    _stage = 6;
                }
                break;
        }
        return new(movement, aim, pointer, attack, pickup, toggle, restart, escape,
            AttackHeld: attack && !inventory.IsOpen && !smith.IsOpen, InteractPressed: interact);

        void Click(Microsoft.Xna.Framework.Rectangle rectangle)
        {
            pointer = new NVector(rectangle.Center.X, rectangle.Center.Y);
            attack = true;
        }
    }

    internal bool AfterDraw(GraphicsDevice device, Camera2D camera, DungeonSession session, bool inventoryOpen)
    {
        if (Restoring)
        {
            Check(_expected is not null && !session.InDungeon && session.Hub.Monsters.Count == 0 &&
                session.Dungeon.Monsters.All(monster => monster.Health == monster.MaxHealth) &&
                !session.Dungeon.Map!.BossGateOpen && session.Dungeon.GroundResources.Count == 0 &&
                JsonSerializer.Serialize(_expected) ==
                JsonSerializer.Serialize(new ProgressPersistence(_content!).Capture(session.Arena)),
                "second window restored exact dungeon-earned progression into safe hub");
            if (_restoreDraws == 0)
                Capture(device, "dungeon-restored.png");
            return ++_restoreDraws >= 2;
        }
        if (_stage == 1 && inventoryOpen)
            _pauseDraws++;
        if (!_hubDraw && !session.InDungeon)
        {
            Capture(device, "dungeon-hub.png");
            _hubDraw = true;
        }
        if (session.InDungeon)
        {
            var gate = camera.WorldToScreen(new NVector(session.Arena.Map!.BossGate.X,
                session.Arena.Map.BossGate.Y));
            if (gate.X is > 0 and < 1080)
            {
                if (!session.Arena.Map.BossGateOpen && !_closedGateDraw)
                {
                    Capture(device, "dungeon-gate-closed.png");
                    _closedGateDraw = true;
                }
                else if (session.Arena.Map.BossGateOpen && !_openGateDraw)
                {
                    Capture(device, "dungeon-gate-open.png");
                    _openGateDraw = true;
                }
            }
            var room = session.Arena.Player.Position.X < 1120 ? 0 :
                session.Arena.Player.Position.X < 2240 ? 1 : 2;
            if ((_rooms & 1 << room) == 0)
            {
                var anchor = camera.WorldToScreen(session.Arena.Player.Position);
                Check(anchor.X is >= 0 and <= 1120 && anchor.Y is >= 0 and < 600,
                    $"actual room {room} camera keeps player in playfield");
                Capture(device, $"dungeon-room-{room}.png");
                _rooms |= 1 << room;
            }
        }
        if (_stage != 6)
            return false;
        Check(_hubDraw && _rooms == 7 && _sawDeath && _closedGateDraw && _openGateDraw,
            "actual Draw covered hub, three continuous rooms and closed/open guardian gate");
        Check(_warriorDraw && _cultistDraw && _magicDraw,
            $"actual world Draw covered warrior, cultist and magic bolt " +
            $"(warrior={_warriorDraw}; cultist={_cultistDraw}; bolt={_magicDraw})");
        Capture(device, "dungeon-death-return.png");
        File.AppendAllText(_log, $"PASS dungeon Complete; updates={_updates}{Environment.NewLine}");
        return true;
    }

    internal void ValidateEnemyDraw(GraphicsDevice device, Camera2D camera, ArenaSimulation arena)
    {
        foreach (var monster in arena.Monsters.Where(monster => !monster.IsDefeated &&
            monster.Id is "monster.skeleton_warrior" or "monster.cultist_mage"))
        {
            var anchor = camera.WorldToScreen(monster.Position);
            if (anchor.X is < 40 or > 1080 || anchor.Y is < 60 or > 550)
                continue;
            var pixels = Pixels(new((int)anchor.X - 35, (int)anchor.Y - 50, 70, 80));
            if (!_warriorDraw && monster.Id == "monster.skeleton_warrior" &&
                pixels.Contains(new Microsoft.Xna.Framework.Color(202, 195, 164)) &&
                pixels.Contains(new Microsoft.Xna.Framework.Color(163, 128, 65)))
            {
                Capture(device, "dungeon-skeleton-warrior.png");
                _warriorDraw = true;
                Check(true, "actual world pixels show bone warrior and brass-edged shield");
            }
            if (!_cultistDraw && monster.Id == "monster.cultist_mage" &&
                pixels.Contains(new Microsoft.Xna.Framework.Color(75, 35, 91)) &&
                pixels.Contains(Microsoft.Xna.Framework.Color.MediumPurple))
            {
                Capture(device, "dungeon-cultist-mage.png");
                _cultistDraw = true;
                Check(true, "actual world pixels show cultist robe and glowing staff");
            }
        }
        if (_magicDraw)
            return;
        var cultist = arena.Monsters.FirstOrDefault(monster => monster.Id == "monster.cultist_mage");
        foreach (var bolt in arena.Projectiles.Where(projectile => projectile.SourceInstanceId == cultist?.InstanceId))
        {
            var anchor = camera.WorldToScreen(bolt.Position);
            if (anchor.X is < 12 or > 1108 || anchor.Y is < 12 or > 588)
                continue;
            var pixels = Pixels(new((int)anchor.X - 10, (int)anchor.Y - 10, 20, 20));
            if (!pixels.Contains(Microsoft.Xna.Framework.Color.MediumPurple) ||
                !pixels.Contains(new Microsoft.Xna.Framework.Color(227, 208, 255)))
                continue;
            Capture(device, "dungeon-magic-bolt.png");
            _magicDraw = true;
            Check(true, "actual world pixels show launched violet magic bolt rather than an arrow");
            break;
        }

        Microsoft.Xna.Framework.Color[] Pixels(Microsoft.Xna.Framework.Rectangle bounds)
        {
            var pixels = new Microsoft.Xna.Framework.Color[bounds.Width * bounds.Height];
            device.GetBackBufferData(bounds, pixels, 0, pixels.Length);
            return pixels;
        }
    }

    private GameContent? _content;
    internal void RecordExpected(ArenaSimulation arena, GameContent content)
    {
        _content = content;
        _expected = new ProgressPersistence(content).Capture(arena);
        Check(JsonSerializer.Serialize(_expected) == JsonSerializer.Serialize(new JsonSaveStore(SavePath).Load()),
            "actual dungeon-earned progression persisted to isolated save");
    }

    private static NVector Towards(DungeonSession session, NVector target, float stop)
    {
        var origin = session.Arena.Player.Position;
        if (NVector.DistanceSquared(origin, target) <= stop * stop)
            return NVector.Zero;
        var waypoint = session.Arena.Map!.NextWaypoint(origin, target);
        var delta = waypoint - origin;
        return delta.LengthSquared() > .001f ? NVector.Normalize(delta) : NVector.Zero;
    }

    private static string Snapshot(DungeonSession session) =>
        $"{session.Arena.Player.Position}|{session.Arena.Player.Health}|" +
        string.Join(";", session.Arena.Monsters.Select(m => $"{m.Position}:{m.Health}:{m.WindupRemaining}")) +
        string.Join(";", session.Arena.Projectiles.Select(projectile => $"{projectile.Position}:{projectile.Direction}"));

    private static void Capture(GraphicsDevice device, string name)
    {
        using var texture = new Texture2D(device, device.Viewport.Width, device.Viewport.Height);
        var pixels = new Microsoft.Xna.Framework.Color[texture.Width * texture.Height];
        device.GetBackBufferData(pixels);
        texture.SetData(pixels);
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        texture.SaveAsPng(file, texture.Width, texture.Height);
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Dungeon smoke failed: {message}; stage={_stage}; phase={_phase}");
        File.AppendAllText(_log, $"PASS {message}{Environment.NewLine}");
    }

    public void Dispose()
    {
        File.Delete(SavePath);
        Directory.Delete(_directory);
    }
}
