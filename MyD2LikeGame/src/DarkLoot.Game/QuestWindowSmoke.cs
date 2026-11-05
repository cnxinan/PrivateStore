using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DarkLoot.Data;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Quests;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class QuestWindowSmoke : IDisposable
{
    private enum Stage
    {
        OpenFirst, AcceptFirst, CloseFirst, EnterFirst, PauseJournal, FirstKills,
        ReturnFirst, OpenDelivery, DeliverFirst, AcceptSupplies, DeliverSupplies,
        AcceptGuardian, CloseGuardian, EnterGuardian, GuardianKills, ReturnGuardian,
        OpenReward, DeliverGuardian, RepeatReward, CloseReward, Restart, Done
    }

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"darkloot-quests-{Guid.NewGuid():N}");
    private readonly string _log = Path.Combine(AppContext.BaseDirectory, "quest-smoke.log");
    private Stage _stage;
    private int _updates;
    private int _pauseTicks;
    private int _pauseDraws;
    private int _restoreDraws;
    private bool _availableDraw;
    private bool _readyDraw;
    private bool _commissionDraw;
    private bool _restarted;
    private string? _paused;
    private string? _completed;
    private ProgressSnapshot? _expected;
    private GameContent? _content;
    public bool Restoring { get; set; }
    public string SavePath => Path.Combine(_directory, "save.json");

    public QuestWindowSmoke()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_log, $"Quest actual-window smoke {DateTime.UtcNow:O}{Environment.NewLine}");
    }

    internal GameContent CreateContent(GameContent production) => production with
    {
        Monsters = production.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MaxHealth = 1,
            MoveSpeed = .01f,
            ContactDamage = 0,
            RangedAttack = pair.Value.RangedAttack is null ? null : pair.Value.RangedAttack with { Damage = 0 }
        }),
        ResourceLootTableDefinitions = production.ResourceLootTables.ToDictionary(pair => pair.Key,
            pair => pair.Value with { MinQuantity = 2, MaxQuantity = 2, RecipeChancePercent = 0 })
    };

    internal void ValidateFont(SpriteFont font)
    {
        var strings = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        Check(strings.SelectMany(text => text).All(font.Characters.Contains),
            "actual font atlas covers all quest text without fallback glyphs");
    }

    internal GameInputSnapshot NextInput(DungeonSession session, PanelController panels,
        BlacksmithPanel smith, GameContent content, ActorPose pose, double ambience)
    {
        if (++_updates > 3200)
            throw new InvalidOperationException($"Quest smoke timed out: {_stage}; position={session.Arena.Player.Position}");
        if (_updates % 120 == 0)
            File.AppendAllText(_log, $"Progress {_stage}; {session.Arena.Player.Position}; " +
                $"{session.Arena.Quests.Progress}{Environment.NewLine}");
        var arena = session.Arena;
        var player = arena.Player;
        var layout = new UiLayout(1120);
        var input = new GameInputSnapshot(NVector.Zero, player.Position + NVector.UnitX, NVector.Zero,
            false, false, false, false, false);
        switch (_stage)
        {
            case Stage.OpenFirst:
                if (_availableDraw)
                    OpenSmith(Stage.AcceptFirst);
                break;
            case Stage.AcceptFirst:
                CommissionAction(false, Stage.CloseFirst);
                break;
            case Stage.CloseFirst:
                ClosePanels(Stage.EnterFirst);
                break;
            case Stage.EnterFirst:
                Enter(Stage.PauseJournal);
                break;
            case Stage.PauseJournal:
                if (!panels.JournalOpen && _pauseTicks == 0)
                    input = Click(layout.QuestButton);
                else if (panels.JournalOpen)
                {
                    var state = $"{player.Position}|{player.Health}|{player.AttackSequence}|" +
                        $"{arena.ResetSequence}|{pose}|{ambience:R}|" +
                        string.Join(";", arena.Monsters.Select(monster =>
                            $"{monster.Position}:{monster.Health}:{monster.WindupRemaining}")) +
                        string.Join(";", arena.Projectiles.Select(projectile => projectile.Position));
                    _paused ??= state;
                    Check(_paused == state, "journal freezes movement, combat, poses and ambience");
                    input = input with { MoveDirection = NVector.UnitX, AttackHeld = true, RestartPressed = true };
                    if (++_pauseTicks >= 12 && _pauseDraws >= 2)
                    {
                        input = input with { ClosePanelPressed = true };
                        _stage = Stage.FirstKills;
                    }
                }
                break;
            case Stage.FirstKills:
                if (!Collect() && arena.Quests.Kills == 3)
                {
                    Check(arena.Quests.Accepted && arena.Quests.ClaimedCount == 0,
                        "three accepted real kills reach ready state without auto-claiming");
                    _stage = Stage.ReturnFirst;
                }
                else if (arena.GroundResources.Count == 0)
                    Fight();
                break;
            case Stage.ReturnFirst:
                Return(Stage.OpenDelivery, requireMarker: true);
                break;
            case Stage.OpenDelivery:
                OpenSmith(Stage.DeliverFirst);
                break;
            case Stage.DeliverFirst:
                CommissionAction(true, Stage.AcceptSupplies);
                break;
            case Stage.AcceptSupplies:
                CommissionAction(false, Stage.DeliverSupplies);
                break;
            case Stage.DeliverSupplies:
                if (arena.Quests.ClaimedCount == 1)
                    Check(arena.QuestOperations.IsReady(player), "existing collected bones can satisfy delivery");
                CommissionAction(true, Stage.AcceptGuardian);
                if (arena.Quests.ClaimedCount == 2)
                    Check(player.Resources.GetCount(ResourceKind.Material, "material.bone") == 0 &&
                        player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade") == 1 &&
                        !arena.Blacksmith.HasLearned("recipe.magic_blade"),
                        "manual delivery deducted four bones and granted one unlearned recipe");
                break;
            case Stage.AcceptGuardian:
                CommissionAction(false, Stage.CloseGuardian);
                break;
            case Stage.CloseGuardian:
                ClosePanels(Stage.EnterGuardian);
                break;
            case Stage.EnterGuardian:
                Enter(Stage.GuardianKills);
                break;
            case Stage.GuardianKills:
                if (!Collect() && session.Completed)
                {
                    Check(arena.Quests.Kills == 1 && arena.Quests.ClaimedCount == 2,
                        "accepted guardian kill advances quest without granting its reward");
                    _stage = Stage.ReturnGuardian;
                }
                else if (arena.GroundResources.Count == 0)
                    Fight();
                break;
            case Stage.ReturnGuardian:
                Return(Stage.OpenReward);
                break;
            case Stage.OpenReward:
                OpenSmith(Stage.DeliverGuardian);
                break;
            case Stage.DeliverGuardian:
                CommissionAction(true, Stage.RepeatReward);
                break;
            case Stage.RepeatReward:
                Check(arena.Quests.ClaimedCount == 3 &&
                    player.Resources.GetCount(ResourceKind.Recipe, "recipe.rare_vest") == 1 &&
                    !arena.Blacksmith.HasLearned("recipe.rare_vest"), "guardian reward is owned, not automatically learned");
                _completed ??= Snapshot(arena, content);
                if (smith.LastQuestResult != QuestStatus.AllCompleted)
                    input = Click(layout.TurnInQuestButton);
                else
                {
                    Check(_completed == Snapshot(arena, content), "repeated reward click cannot duplicate resources");
                    _stage = Stage.CloseReward;
                }
                break;
            case Stage.CloseReward:
                ClosePanels(Stage.Restart);
                break;
            case Stage.Restart:
                if (!_restarted)
                {
                    input = input with { RestartPressed = true };
                    _restarted = true;
                }
                else
                {
                    Check(_completed == Snapshot(arena, content) &&
                        session.Dungeon.Monsters.All(monster => !monster.IsDefeated),
                        "restart resets dungeon without resetting quest or rewards");
                    _stage = Stage.Done;
                }
                break;
        }
        return input;

        GameInputSnapshot Click(Rectangle bounds) => input with
        {
            PointerScreenPosition = new(bounds.Center.X, bounds.Center.Y),
            AttackPressed = true
        };
        void OpenSmith(Stage next)
        {
            if (panels.BlacksmithOpen)
                _stage = next;
            else if (arena.BlacksmithOperations.CanInteract(player))
                input = input with { InteractPressed = true };
            else
                Move(BlacksmithService.Position, 28);
        }
        void CommissionAction(bool turnIn, Stage next)
        {
            if (smith.Tab != BlacksmithTab.Commissions)
                input = Click(layout.CommissionTab);
            else if (turnIn ? !arena.Quests.Accepted : arena.Quests.Accepted)
                _stage = next;
            else
                input = Click(turnIn ? layout.TurnInQuestButton : layout.AcceptQuestButton);
        }
        void ClosePanels(Stage next)
        {
            if (panels.ActivePanel != PanelKind.None)
                input = input with { ClosePanelPressed = true };
            else
                _stage = next;
        }
        void Enter(Stage next)
        {
            if (session.InDungeon)
                _stage = next;
            else
                Move(DungeonSession.HubEntrance, 8);
        }
        void Return(Stage next, bool requireMarker = false)
        {
            if (session.InDungeon)
                Move(DungeonSession.DungeonEntrance, 8);
            else if (!requireMarker || _readyDraw)
                _stage = next;
        }
        bool Collect()
        {
            var drop = arena.GroundResources.MinBy(drop => NVector.DistanceSquared(player.Position, drop.Position));
            if (drop is null)
                return false;
            Move(drop.Position, 20);
            input = input with { PickupPressed = true };
            return true;
        }
        void Fight()
        {
            var target = arena.Monsters.Where(monster => !monster.IsDefeated &&
                (arena.Map!.BossGateOpen || monster.InstanceId != "boss.guardian"))
                .MinBy(monster => NVector.DistanceSquared(player.Position, monster.Position))
                ?? throw new InvalidOperationException("Quest smoke has no reachable combat objective.");
            Move(target.Position, 38);
            input = input with
            {
                AimWorldPosition = target.Position,
                AttackPressed = NVector.DistanceSquared(player.Position, target.Position) <= 48 * 48 &&
                    arena.Map!.HasLineOfSight(player.Position, target.Position)
            };
        }
        void Move(NVector target, float stop)
        {
            if (NVector.DistanceSquared(player.Position, target) > stop * stop)
            {
                var delta = arena.Map!.NextWaypoint(player.Position, target) - player.Position;
                input = input with { MoveDirection = delta.LengthSquared() > .001f ? NVector.Normalize(delta) : NVector.Zero };
            }
        }
    }

    internal bool AfterDraw(GraphicsDevice device, Camera2D camera, DungeonSession session,
        PanelController panels, BlacksmithPanel smith)
    {
        if (Restoring)
        {
            Check(_expected is not null && !session.InDungeon && session.Arena.Quests.ClaimedCount == 3 &&
                session.Dungeon.Monsters.All(monster => !monster.IsDefeated) &&
                JsonSerializer.Serialize(_expected) == Snapshot(session.Arena, _content!),
                "second independent window restores exact completed chain and rewards into fresh hub");
            if (_restoreDraws == 0)
                Capture(device, "quests-restored.png");
            return ++_restoreDraws >= 2;
        }
        if (!session.InDungeon && panels.ActivePanel == PanelKind.None &&
            QuestPanel.Marker(session.Arena) is { Length: > 0 } marker &&
            (marker == "!" && !_availableDraw || marker == "?" && !_readyDraw))
        {
            var anchor = camera.WorldToScreen(BlacksmithService.Position);
            var region = new Rectangle((int)anchor.X - 20, (int)anchor.Y - 74, 28, 30);
            var pixels = new Color[region.Width * region.Height];
            device.GetBackBufferData(region, pixels, 0, pixels.Length);
            Check(pixels.Contains(Color.Gold), $"actual smith marker pixels rendered '{marker}'");
            if (marker == "!" && !_availableDraw)
            {
                Capture(device, "quests-available.png");
                _availableDraw = true;
            }
            if (marker == "?" && !_readyDraw)
            {
                Capture(device, "quests-ready.png");
                _readyDraw = true;
            }
        }
        if (panels.BlacksmithOpen && smith.Tab == BlacksmithTab.Commissions && !_commissionDraw)
        {
            Capture(device, "quests-commission.png");
            _commissionDraw = true;
        }
        if (panels.JournalOpen && _stage == Stage.PauseJournal)
        {
            Check(!panels.InventoryOpen, "actual journal Draw replaces inventory rather than creating a third panel");
            if (_pauseDraws == 0)
                Capture(device, "quests-journal.png");
            _pauseDraws++;
        }
        if (_stage != Stage.Done)
            return false;
        Check(_availableDraw && _readyDraw && _commissionDraw && _pauseDraws >= 2,
            "actual Draw covers commission, journal pause and available/ready markers");
        Capture(device, "quests-completed.png");
        File.AppendAllText(_log, $"PASS quests Complete; updates={_updates}{Environment.NewLine}");
        return true;
    }

    internal void RecordExpected(ArenaSimulation arena, GameContent content)
    {
        _content = content;
        _expected = new ProgressPersistence(content).Capture(arena);
        Check(JsonSerializer.Serialize(_expected) == JsonSerializer.Serialize(new JsonSaveStore(SavePath).Load()),
            "completed quest state and exact rewards persisted to isolated save");
    }

    private static string Snapshot(ArenaSimulation arena, GameContent content) =>
        JsonSerializer.Serialize(new ProgressPersistence(content).Capture(arena));

    private static void Capture(GraphicsDevice device, string name)
    {
        using var texture = new Texture2D(device, device.Viewport.Width, device.Viewport.Height);
        var pixels = new Color[texture.Width * texture.Height];
        device.GetBackBufferData(pixels);
        texture.SetData(pixels);
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        texture.SaveAsPng(stream, texture.Width, texture.Height);
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Quest smoke failed: {message}; stage={_stage}");
        File.AppendAllText(_log, $"PASS {message}{Environment.NewLine}");
    }

    public void Dispose()
    {
        File.Delete(SavePath);
        Directory.Delete(_directory);
    }
}
