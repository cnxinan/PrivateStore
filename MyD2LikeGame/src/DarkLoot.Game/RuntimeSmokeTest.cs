using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class RuntimeSmokeTest
{
    private enum SmokeStage
    {
        Approach, MultiHit, DodgeArrow, TakeArrow, ClearEncounter, Pickup,
        Equip, PauseWithArrow, RestartCleanup, DeathRespawn, Complete
    }

    private readonly string _logPath;
    private SmokeStage _stage;
    private int _stageUpdates;
    private int _updates;
    private int _draws;
    private int _phase;
    private int _healthBefore;
    private long _sequence;
    private int _pausedUpdates;
    private bool _sawArrow;
    private bool _sawIndependentCriticals;
    private bool _sawCompletionArrow;
    private bool _sawKilledFeedbackDraw;
    private bool _sawIndependentFeedbackDraw;
    private ProjectileState? _dodgedArrow;
    private NumericsVector _lockedAim;
    private string _pauseSnapshot = "";
    private Guid _weapon;
    private Guid _armor;
    private AffixRoll[] _weaponRolls = [];
    private AffixRoll[] _armorRolls = [];
    private ItemInstance[] _pickupItems = [];
    private bool _deathContactExpected;
    private NumericsVector _deathPosition;
    private int _deathUpdates;
    private long _resetBeforeUpdate;
    private long _attackBeforeUpdate;
    private int _lastSwingUpdate;
    private Dictionary<string, int> _healthBeforeSwing = [];
    private ActorPose _pausedPlayerPose;
    private ActorPose[] _pausedMonsterPoses = [];
    private bool _sawPartialDeath;
    private bool _sawIsolatedPoses;
    private bool _sawPartialDeathPixels;
    private bool _sawPlayerDeathPixels;

    public RuntimeSmokeTest(string logPath)
    {
        _logPath = logPath;
        File.WriteAllText(logPath,
            $"DarkLoot mixed smoke; PID={Environment.ProcessId}; UTC={DateTime.UtcNow:O}{Environment.NewLine}");
    }

    public (GameContent Content, EncounterDefinition Encounter) CreateFixture(GameContent production)
    {
        var configured = production.Encounters["encounter.training_arena"];
        var productionArena = new ArenaSimulation(production, new Random(42), configured);
        Check(productionArena.Monsters.Count == 6 &&
              productionArena.Monsters.Select(monster => monster.Id).Distinct().Count() == 3 &&
              productionArena.Monsters.Select(monster => monster.Position).SequenceEqual(
                  configured.Spawns.Select(spawn => new NumericsVector(spawn.X, spawn.Y))),
            "unmodified production content loads six configured spawns");
        var monsters = production.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MaxHealth = pair.Value.Behavior == MonsterBehavior.Archer ? 30 :
                pair.Key == "monster.fast_wolf" ? 60 : 10,
            MoveSpeed = .01f,
            ContactDamage = pair.Key == "monster.fast_wolf" ? 200 : 0
        });
        var tables = production.LootTables.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MinDrops = 2,
            MaxDrops = 2,
            Entries =
            [
                new LootEntryDefinition("weapon.training_blade", 1) { Guaranteed = true },
                new LootEntryDefinition("armor.training_vest", 1) { Guaranteed = true }
            ],
            RarityWeights = [new RarityWeightDefinition(ItemRarity.Magic, 1)]
        });
        var affixes = new Dictionary<string, AffixDefinition>
        {
            ["affix.lucky"] = production.Affixes["affix.lucky"] with
            {
                MinValue = 50,
                MaxValue = 50,
                Slots = [ItemSlot.Weapon]
            },
            ["affix.vital"] = production.Affixes["affix.vital"] with
            {
                MinValue = 20,
                MaxValue = 20,
                Slots = [ItemSlot.Armor]
            }
        };
        var content = production with { Monsters = monsters, LootTables = tables, AffixDefinitions = affixes };
        var encounter = configured with
        {
            Spawns =
            [
                new("smoke.slime.1", "monster.training_slime", 215, 260),
                new("smoke.slime.2", "monster.training_slime", 215, 280),
                new("smoke.wolf.1", "monster.fast_wolf", 285, 260),
                new("smoke.wolf.2", "monster.fast_wolf", 285, 280),
                new("smoke.archer.1", "monster.skeleton_archer", 380, 270),
                new("smoke.archer.2", "monster.skeleton_archer", 420, 270)
            ]
        };
        return (content, encounter);
    }

    public Random CreateRandom() => new SmokeRandom();

    private sealed class SmokeRandom : Random
    {
        private int _criticalRoll;
        public SmokeRandom() : base(17) { }
        public override int Next(int maxValue) => maxValue == 100
            ? _criticalRoll++ % 2 == 0 ? 0 : 99
            : base.Next(maxValue);
    }

    public void ValidatePresentation(SpriteFont font, GameContent content)
    {
        var strings = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!).ToList();
        strings.AddRange(content.Items.Values.Select(item => item.Name));
        strings.AddRange(content.Monsters.Values.Select(monster => monster.Name));
        strings.AddRange(content.Affixes.Values.Select(affix => affix.Name));
        strings.AddRange(Enum.GetValues<ItemSlot>().Select(GameText.Slot));
        strings.AddRange(Enum.GetValues<ItemRarity>().Select(GameText.Rarity));
        strings.AddRange(Enum.GetValues<ItemStat>().Select(GameText.Stat));
        var layouts = new List<(string Text, float Width, float Height)>();
        var combinations = 0;
        foreach (var definition in content.Items.Values)
        {
            var eligible = content.Affixes.Values.Where(affix => affix.Slots.Contains(definition.Slot)).ToArray();
            var rolls = new List<AffixRoll[]> { Array.Empty<AffixRoll>() };
            foreach (var first in eligible)
            {
                foreach (var value in new[] { first.MinValue, first.MaxValue }.Distinct())
                {
                    var firstRoll = new AffixRoll(first.Id, first.Stat, value);
                    rolls.Add([firstRoll]);
                    foreach (var second in eligible.Where(affix => affix.Id != first.Id))
                    {
                        foreach (var secondValue in new[] { second.MinValue, second.MaxValue }.Distinct())
                        {
                            rolls.Add([firstRoll, new AffixRoll(second.Id, second.Stat, secondValue)]);
                        }
                    }
                }
            }

            foreach (var combination in rolls)
            {
                var rarity = combination.Length switch
                {
                    0 => ItemRarity.Common,
                    1 => ItemRarity.Magic,
                    _ => ItemRarity.Rare
                };
                var item = new ItemInstance(Guid.Empty, definition.Id, rarity, combination);
                var name = ItemNameFormatter.GetDisplayName(item, definition, content);
                layouts.Add((string.Format(GameText.Item, GameText.Rarity(rarity), name,
                    GameText.Slot(definition.Slot), GameText.Equipped), 600, 21));
                layouts.Add((InventoryPanel.FormatRolls(item, content), 336, 21));
                layouts.Add((string.Format(GameText.Pickup, GameText.Rarity(rarity), name), 400, 22));
                layouts.Add((string.Format(GameText.SlotLine, GameText.Slot(definition.Slot),
                    $"{GameText.Rarity(rarity)} {name}"), 616, 22));
                combinations++;
            }
        }

        layouts.AddRange(new[] { GameText.Movement, GameText.Controls, GameText.Victory, GameText.Objective, GameText.Cleared }
            .Select(text => (text, 1080f, 22f)));
        layouts.AddRange(new[] { GameText.Inventory, GameText.Paused, GameText.Empty }
            .Select(text => (text, 616f, 22f)));
        layouts.Add((string.Format(GameText.Enemies, 6, 6), 240, 22));
        layouts.Add((string.Format(GameText.Hud, int.MaxValue, int.MaxValue, int.MaxValue, 100, 9999d), 1080, 22));
        layouts.Add((string.Format(GameText.Totals, int.MaxValue, int.MaxValue, 100), 616, 22));
        layouts.Add((string.Format(GameText.Comparison, "+2147483647", "-2147483647", "+100"), 256, 21));
        foreach (var monster in content.Monsters.Values)
        {
            layouts.Add((string.Format(GameText.Monster, monster.Name, monster.MaxHealth, monster.MaxHealth), 280, 22));
        }
        layouts.Add((string.Format(GameText.Hit, int.MaxValue), 200, 22));
        layouts.Add((string.Format(GameText.CriticalHit, int.MaxValue), 200, 22));
        layouts.Add((GameText.Swing, 200, 22));
        strings.AddRange(layouts.Select(layout => layout.Text));
        var required = strings.SelectMany(text => text).Where(character => character is not '\n' and not '\r')
            .Distinct().Order().ToArray();
        var missing = required.Where(character => !font.Characters.Contains(character)).ToArray();
        Check(missing.Length == 0,
            $"loaded SpriteFont glyph coverage; strings={strings.Count}; combinations={combinations}; " +
            $"required={required.Length}; atlas={font.Characters.Count}; " +
            $"missing={string.Join(" ", missing.Select(character => $"U+{(int)character:X4}"))}");
        Check(font.DefaultCharacter is null, "font has no silent fallback character");
        var minimumScale = 1f;
        foreach (var (text, width, height) in layouts)
        {
            var measured = font.MeasureString(text);
            var scale = Math.Min(1f, Math.Min(width / Math.Max(1, measured.X), height / Math.Max(1, measured.Y)));
            Check(float.IsFinite(scale) && scale >= .60f,
                $"MeasureString layout fits readable bounds {width}x{height}; scale={scale:F3}; text={text}");
            minimumScale = Math.Min(minimumScale, scale);
        }
        Log($"PASS Chinese presentation complete; layouts={layouts.Count}; minimumScale={minimumScale:F3}");
    }

    public GameInputSnapshot NextInput(ArenaSimulation arena, InventoryPanel panel, GameContent content, int width)
    {
        _resetBeforeUpdate = arena.ResetSequence;
        _attackBeforeUpdate = arena.Player.AttackSequence;
        _healthBeforeSwing = arena.Monsters.ToDictionary(monster => monster.InstanceId, monster => monster.Health);
        _updates++;
        if (++_stageUpdates > 450 || _updates > 2400)
        {
            throw new InvalidOperationException(
                $"Smoke timeout: stage={_stage}; phase={_phase}; stageUpdates={_stageUpdates}; " +
                $"remaining={arena.RemainingMonsters}; arrows={arena.Projectiles.Count}; draws={_draws}.");
        }
        var movement = NumericsVector.Zero;
        var aim = arena.Player.Position + NumericsVector.UnitX;
        var attack = false;
        var pickup = false;
        var toggle = false;
        var restart = false;
        var pointer = NumericsVector.Zero;
        switch (_stage)
        {
            case SmokeStage.Approach:
                movement = NumericsVector.UnitX;
                break;
            case SmokeStage.MultiHit:
                aim = new NumericsVector(215, 270);
                attack = true;
                break;
            case SmokeStage.DodgeArrow:
                movement = _stageUpdates <= 8 ? -NumericsVector.UnitY : NumericsVector.Zero;
                break;
            case SmokeStage.TakeArrow:
                break;
            case SmokeStage.Pickup:
                var drop = arena.GroundItems.FirstOrDefault();
                if (drop is not null)
                {
                    movement = Towards(arena.Player.Position, drop.Position, 18);
                    pickup = true;
                }
                break;
            case SmokeStage.Equip:
                if (!panel.IsOpen)
                {
                    toggle = true;
                }
                else
                {
                    var slot = _phase == 1 ? ItemSlot.Armor : ItemSlot.Weapon;
                    var row = panel.RowBounds(width, RowForSlot(arena, content, slot));
                    pointer = new NumericsVector(row.Center.X, row.Center.Y);
                    attack = true;
                }
                break;
            case SmokeStage.ClearEncounter:
                if (arena.IsCleared)
                {
                    break;
                }
                if (panel.IsOpen)
                {
                    toggle = true;
                }
                else
                {
                    var target = arena.Monsters.Where(monster => !monster.IsDefeated)
                        .MinBy(monster => NumericsVector.DistanceSquared(monster.Position, arena.Player.Position))!;
                    aim = target.Position;
                    movement = Towards(arena.Player.Position, target.Position, 40);
                    attack = movement == NumericsVector.Zero;
                    if (arena.RemainingMonsters == 1 && target.Behavior == MonsterBehavior.Archer)
                    {
                        movement = Towards(arena.Player.Position, target.Position, 30);
                        attack = false;
                        if (_phase == 0 && movement == NumericsVector.Zero && target.IsWindingUp)
                        {
                            var locked = target.LockedAimDirection;
                            movement = new NumericsVector(-locked.Y, locked.X);
                            _phase = 1;
                        }
                        else if (_phase == 1)
                        {
                            movement = NumericsVector.Zero;
                            attack = arena.Projectiles.Any(arrow => arrow.SourceInstanceId == target.InstanceId);
                        }
                    }
                }
                break;
            case SmokeStage.PauseWithArrow:
                if (_phase == 0)
                {
                    restart = true;
                }
                else if (_phase == 1)
                {
                    var target = arena.Monsters[0];
                    aim = target.Position;
                    movement = arena.Player.Position.X > 250
                        ? Towards(arena.Player.Position, new NumericsVector(215, 180), 10)
                        : Towards(arena.Player.Position, target.Position, 40);
                    attack = movement == NumericsVector.Zero;
                    if (arena.GroundItems.Count > 0)
                    {
                        // Keep dodging locked arrows so one can coexist with the next windup.
                        movement = Towards(arena.Player.Position,
                            new NumericsVector(215, _stageUpdates % 20 < 10 ? 180 : 360), 10);
                        attack = false;
                    }
                    if (!arena.Player.IsDead && !arena.Player.Reaction.IsActive &&
                        _updates - _lastSwingUpdate >= 4 &&
                        arena.Projectiles.Count > 0 && arena.GroundItems.Count > 0 &&
                        arena.Monsters.Any(monster => monster.IsWindingUp &&
                            monster.WindupRemaining > .100001f))
                    {
                        movement = NumericsVector.Zero;
                        aim = arena.Player.Position - NumericsVector.UnitX * 100;
                        attack = true;
                        _sequence = arena.Player.AttackSequence;
                        _phase = 3;
                    }
                }
                else if (_phase == 3)
                {
                    toggle = true;
                }
                else
                {
                    movement = NumericsVector.UnitX;
                    attack = true;
                    pickup = true;
                }
                break;
            case SmokeStage.RestartCleanup:
                toggle = true;
                if (_phase == 0)
                {
                    attack = true;
                    aim = arena.Player.Position - NumericsVector.UnitX * 100;
                }
                else
                {
                    restart = true;
                }
                break;
            case SmokeStage.DeathRespawn:
                if (_phase == 1)
                {
                    movement = NumericsVector.UnitY;
                    attack = true;
                    pickup = true;
                    toggle = true;
                    restart = true;
                    break;
                }
                if (panel.IsOpen)
                {
                    toggle = true;
                }
                movement = Towards(arena.Player.Position, arena.Monsters[2].Position, 0);
                _healthBefore = arena.Player.Health;
                _deathContactExpected = !panel.IsOpen &&
                    NumericsVector.Distance(arena.Player.Position + movement * 18, arena.Monsters[2].Position) < 24 &&
                    arena.Monsters[2].ContactDamage >= _healthBefore;
                break;
        }
        return new GameInputSnapshot(movement, aim, pointer, attack, pickup, toggle, restart, false,
            AttackHeld: attack && !panel.IsOpen);
    }

    public void AfterUpdate(ArenaSimulation arena, InventoryPanel panel, GameContent content,
        ActorPose playerPose, Func<string, ActorPose> monsterPose)
    {
        foreach (var death in arena.Events.Where(item => item.Kind == CombatEventKind.Died))
        {
            var pose = death.ActorId == "player" ? playerPose : monsterPose(death.ActorId);
            var anchor = death.ActorId == "player" ? arena.Player.Position :
                arena.Monsters.Single(monster => monster.InstanceId == death.ActorId).Position;
            Check(pose.Opacity == 1 && pose.Scale == NumericsVector.One && anchor == death.Position,
                $"new death starts visible at actual death position; actor={death.ActorId}");
        }
        var partialDeath = arena.Monsters.FirstOrDefault(monster => monster.IsDefeated &&
            monsterPose(monster.InstanceId).Opacity is > 0 and < 1);
        if (partialDeath is not null)
        {
            _sawPartialDeath = true;
            Check(monsterPose(partialDeath.InstanceId).Scale.X is > 0 and < 1,
                "partial monster death retains nonzero geometry");
        }
        _sawIsolatedPoses |= arena.Monsters.GroupBy(monster => monster.Id).Any(group =>
            group.Select(monster => monsterPose(monster.InstanceId)).Distinct().Count() > 1);
        if (arena.Player.AttackSequence != _attackBeforeUpdate)
        {
            _lastSwingUpdate = _updates;
        }
        switch (_stage)
        {
            case SmokeStage.Approach when arena.Player.Position.X >= 196:
                Check(Math.Abs(playerPose.LimbSwing) > .001f && playerPose.Offset != NumericsVector.Zero,
                    "walking changes player pose from neutral geometry");
                Check(arena.Monsters.Count == 6 && arena.Player.Position.X > 160,
                    "Approach moved player in six-instance mixed arena");
                _lockedAim = arena.Monsters[4].LockedAimDirection;
                Check(arena.Monsters[4].IsWindingUp, "archer shows actual locked-aim windup");
                Enter(SmokeStage.MultiHit);
                break;
            case SmokeStage.MultiHit when arena.LastSwingHits.Count > 0:
                Check(arena.LastSwingHits.Select(hit => hit.MonsterInstanceId).Distinct().Count() >= 2 &&
                      arena.Monsters.Count(monster => monster.IsDefeated) == 2,
                    $"MultiHit one accepted held swing killed two slimes; hits={FormatHits(arena)}");
                Check(arena.Projectiles.Count > 0 &&
                      arena.Projectiles[0].Direction == _lockedAim,
                    "windup launched a real arrow along locked direction");
                _dodgedArrow = arena.Projectiles[0];
                _healthBefore = arena.Player.Health;
                _sequence = arena.Player.AttackSequence;
                Enter(SmokeStage.DodgeArrow);
                break;
            case SmokeStage.DodgeArrow:
                var arrow = arena.Projectiles.FirstOrDefault(projectile =>
                    projectile.SourceInstanceId == _dodgedArrow!.SourceInstanceId);
                if (arrow is not null)
                {
                    Check(arrow.Direction == _dodgedArrow!.Direction, "moving arrow does not home after dodge");
                }
                if (_stageUpdates == 10)
                {
                    Check(arena.Player.Health == _healthBefore &&
                          (arrow is null || arrow.Position.X < arena.Player.Position.X - 14),
                        $"DodgeArrow real arrow passed without damage; player={arena.Player.Position}; " +
                        $"arrow={arrow?.Position}; health={arena.Player.Health}");
                    Check(arena.Player.AttackSequence == _sequence,
                        "released held attack stopped swinging");
                    _sawArrow = false;
                    Enter(SmokeStage.TakeArrow);
                }
                break;
            case SmokeStage.TakeArrow:
                _sawArrow |= arena.Projectiles.Any(projectile => Math.Abs(projectile.Direction.Y) > .1f);
                if (arena.Player.Health < _healthBefore)
                {
                    Check(_sawArrow && _healthBefore - arena.Player.Health is 5 or 10,
                        $"TakeArrow real re-aimed projectile hit; health={_healthBefore}->{arena.Player.Health}");
                    _pickupItems = arena.GroundItems.Select(drop => drop.Item).ToArray();
                    Check(_pickupItems.Length == 4, "fixture generated actual two-slot multi-kill loot");
                    Enter(SmokeStage.Pickup);
                }
                break;
            case SmokeStage.Pickup when arena.GroundItems.Count == 0:
                Check(_pickupItems.All(item => arena.Player.Inventory.Items.Contains(item)) &&
                      arena.Player.Inventory.Items.Select(item => content.Items[item.DefinitionId].Slot)
                          .Distinct().Count() == 2,
                    "Pickup transferred both slots with original item identities and rolls");
                _healthBefore = arena.Player.Health;
                Enter(SmokeStage.Equip);
                break;
            case SmokeStage.Equip when panel.IsOpen:
                var slot = _phase == 1 ? ItemSlot.Armor : ItemSlot.Weapon;
                var equipped = slot == ItemSlot.Weapon
                    ? arena.Player.Inventory.EquippedWeapon : arena.Player.Inventory.EquippedArmor;
                if (equipped is null)
                {
                    break;
                }
                var preview = new InventoryService().GetTotalsWithReplacement(
                    arena.Player.BaseAttackDamage, arena.Player.BaseMaximumHealth,
                    arena.Player.Inventory, content, slot, equipped);
                Check(arena.Player.Stats == preview && arena.Player.Health == _healthBefore,
                    $"Equip {slot} real row click matches comparison totals without healing");
                if (_phase == 0)
                {
                    _weapon = equipped.InstanceId;
                    _weaponRolls = equipped.Rolls.ToArray();
                    _phase = 1;
                }
                else if (_phase == 1)
                {
                    _armor = equipped.InstanceId;
                    _armorRolls = equipped.Rolls.ToArray();
                    _phase = 2;
                }
                else
                {
                    CheckGear(arena);
                    Check(arena.Player.CriticalChance == 50 && arena.Player.MaximumHealth == 120,
                        "fixture gear applied real fixed critical and maximum-health affixes");
                    Enter(SmokeStage.ClearEncounter);
                }
                break;
            case SmokeStage.ClearEncounter:
                if (arena.Player.AttackSequence != _attackBeforeUpdate &&
                    arena.LastSwingHits.Count >= 2 &&
                    arena.LastSwingHits.Any(hit => hit.WasCritical) &&
                    arena.LastSwingHits.Any(hit => !hit.WasCritical))
                {
                    if (!_sawIndependentCriticals)
                    {
                        Check(arena.LastSwingHits.All(hit => hit.Damage ==
                                  Math.Min(_healthBeforeSwing[hit.MonsterInstanceId],
                                      arena.Player.AttackDamage * (hit.WasCritical ? 2 : 1))) &&
                              arena.LastSwingHits.Select(hit => hit.MonsterInstanceId).Distinct().Count() ==
                              arena.LastSwingHits.Count,
                            $"independent critical and normal per-target damage in one swing; hits={FormatHits(arena)}");
                    }
                    _sawIndependentCriticals = true;
                }
                if (arena.IsCleared && _phase != 2)
                {
                    Check(_sawIndependentCriticals && arena.RemainingMonsters == 0 &&
                          arena.Monsters.All(monster => monster.IsDefeated),
                        "ClearEncounter defeated all six and exposed completion prompt");
                    _sawCompletionArrow = arena.Projectiles.Count > 0;
                    Check(_sawCompletionArrow, "completion remains true with surviving in-flight arrows");
                    _phase = 2;
                }
                break;
            case SmokeStage.PauseWithArrow:
                if (_phase == 0)
                {
                    _phase = 1;
                }
                else if (_phase == 3 && !panel.IsOpen)
                {
                    Check(arena.Player.AttackSequence == _sequence + 1 && arena.LastSwingHits.Count == 0,
                        $"accepted later miss cleared old per-target damage before pause; " +
                        $"sequence={arena.Player.AttackSequence}/{_sequence}; stun={arena.Player.Reaction.RemainingSeconds}; " +
                        $"arrows={arena.Projectiles.Count}; hits={FormatHits(arena)}");
                    _sequence = arena.Player.AttackSequence;
                }
                else if (_phase == 3 && panel.IsOpen)
                {
                    Check(playerPose.AttackProgress > 0 && playerPose.Facing == -NumericsVector.UnitX,
                        "accepted swing retains locked direction when aim changes during pause");
                    _pausedPlayerPose = playerPose;
                    _pausedMonsterPoses = arena.Monsters.Select(monster => monsterPose(monster.InstanceId)).ToArray();
                    Check(arena.Projectiles.Count > 0 && arena.GroundItems.Count > 0 &&
                          arena.Monsters.Any(monster => monster.IsWindingUp),
                        "PauseWithArrow opened real inventory with nonempty arrows, active windup and ground loot");
                    _pauseSnapshot = Snapshot(arena);
                    _phase = 2;
                }
                else if (_phase == 2)
                {
                    Check(playerPose == _pausedPlayerPose &&
                          arena.Monsters.Select(monster => monsterPose(monster.InstanceId))
                              .SequenceEqual(_pausedMonsterPoses),
                        "inventory freezes nonneutral player swing and independent monster poses");
                    Check(panel.IsOpen && Snapshot(arena) == _pauseSnapshot,
                        $"inventory freezes every monster, windup, arrow, player health and swing; update={++_pausedUpdates}");
                    if (_pausedUpdates == 20)
                    {
                        Enter(SmokeStage.RestartCleanup);
                    }
                }
                break;
            case SmokeStage.RestartCleanup:
                if (_phase == 0)
                {
                    Check(!panel.IsOpen && Snapshot(arena) != _pauseSnapshot &&
                          arena.Player.AttackSequence == _sequence,
                        "closing inventory resumed live arrow/windup but retained pre-pause swing cooldown");
                    Check(arena.GroundItems.Count > 0 && arena.Projectiles.Count > 0,
                        "R cleanup precondition still has real ground loot and arrows after resume");
                    _phase = 1;
                    break;
                }
                Check(arena.Projectiles.Count == 0 && arena.GroundItems.Count == 0 &&
                      arena.RemainingMonsters == 6 &&
                      arena.Monsters.All(monster => monster.Health == monster.MaxHealth && !monster.IsWindingUp) &&
                      arena.LastSwingHits.Count == 0 &&
                      arena.Player.Health == _healthBeforeRestart &&
                      arena.Player.Position == _positionBeforeRestart,
                    "RestartCleanup R cleared previously nonempty loot/arrows and feedback, restored six, retained player state");
                Check(panel.IsOpen && playerPose.Offset == NumericsVector.Zero &&
                      playerPose.AttackProgress == 0 && playerPose.Rotation == 0 &&
                      arena.Monsters.All(monster => monsterPose(monster.InstanceId).Scale == NumericsVector.One),
                    "R while paused cleans animation immediately without advancing presentation time");
                CheckGear(arena);
                _healthBefore = arena.Player.Health;
                Enter(SmokeStage.DeathRespawn);
                break;
            case SmokeStage.DeathRespawn:
                if (_phase == 0 && arena.Player.IsDead)
                {
                    Check(_deathContactExpected && _healthBefore > 0 &&
                          arena.Player.Health == 0 && Math.Abs(arena.Player.DeathRemaining - .6f) < .00001f &&
                          arena.ResetSequence == _resetBeforeUpdate &&
                          arena.Events.Count(item => item.ActorId == "player" && item.Kind == CombatEventKind.Died) == 1 &&
                          arena.Events.Any(item => item.Kind == CombatEventKind.ContactAttack),
                        "DeathRespawn actual lethal contact entered explicit death with full 0.6s timer, no early reset");
                    _deathPosition = arena.Player.Position;
                    _sequence = arena.Player.AttackSequence;
                    _phase = 1;
                }
                else if (_phase == 1)
                {
                    _deathUpdates++;
                    Check(!panel.IsOpen && arena.Player.AttackSequence == _sequence,
                        "DeathRespawn dead inventory toggle and attack inputs ignored");
                    if (arena.Player.IsDead)
                    {
                        Check(_deathUpdates < 6 && arena.Player.Position == _deathPosition &&
                              arena.ResetSequence == _resetBeforeUpdate &&
                              Math.Abs(arena.Player.DeathRemaining - (.6f - _deathUpdates * .1f)) < .00001f &&
                              !arena.Events.Any(item => item.ActorId == "player"),
                            $"DeathRespawn waits with dead movement/R/pickup gated and no duplicate player events; update={_deathUpdates}");
                        break;
                    }
                    Check(_deathUpdates == 6 && arena.ResetSequence == _resetBeforeUpdate + 1 &&
                          arena.RemainingMonsters == 6 &&
                          arena.Player.Position == new NumericsVector(160, 270) &&
                          arena.Player.Health == arena.Player.MaximumHealth &&
                          arena.Player.DeathRemaining == 0 &&
                          Math.Abs(arena.Player.InvulnerabilityRemaining - .8f) < .00001f &&
                          arena.Monsters.All(monster => monster.Health == monster.MaxHealth && !monster.IsWindingUp) &&
                          arena.Projectiles.Count == 0 && arena.GroundItems.Count == 0 &&
                          arena.LastSwingHits.Count == 0 && arena.Events.Count == 0,
                        "DeathRespawn after 0.6s restored spawn/current maximum health, six clean monsters and full 0.8s protection");
                    CheckGear(arena);
                    Enter(SmokeStage.Complete);
                }
                break;
        }
        if (_stage == SmokeStage.RestartCleanup)
        {
            _healthBeforeRestart = arena.Player.Health;
            _positionBeforeRestart = arena.Player.Position;
        }
    }

    private int _healthBeforeRestart;
    private NumericsVector _positionBeforeRestart;

    public bool AfterDraw(ArenaSimulation arena, ActorPresentation presentation, Camera2D camera,
        GraphicsDevice device, double framesPerSecond)
    {
        var attackFlash = presentation.SwingRemaining;
        _draws++;
        if (!_sawPartialDeathPixels)
        {
            var corpse = arena.Monsters.FirstOrDefault(monster => monster.IsDefeated &&
                monster.Id == "monster.training_slime" &&
                presentation.GetMonsterPose(monster.InstanceId).Opacity is > .4f and < .9f);
            if (corpse is not null)
            {
                Check(BodyPixels(device, camera.WorldToScreen(corpse.Position))
                        .Any(pixel => pixel.G > pixel.R + 25 && pixel.G > pixel.B + 40),
                    "real Draw backbuffer retains green partial-death slime geometry at death anchor");
                _sawPartialDeathPixels = true;
            }
        }
        if (!_sawPlayerDeathPixels && arena.Player.IsDead &&
            presentation.PlayerPose.Opacity is > .4f and < .9f)
        {
            Check(BodyPixels(device, camera.WorldToScreen(arena.Player.Position))
                    .Any(pixel => pixel.B > pixel.R + 20 && pixel.B > pixel.G + 8),
                "real Draw backbuffer retains blue partial-death player geometry at death anchor");
            _sawPlayerDeathPixels = true;
        }
        if (attackFlash > 0)
        {
            foreach (var hit in arena.LastSwingHits)
                Check(presentation.HitTexts.Count(text => text.ActorId == hit.MonsterInstanceId &&
                          text.Damage == hit.Damage && text.WasCritical == hit.WasCritical) == 1,
                    $"real Draw receives accepted per-target hit text once; actor={hit.MonsterInstanceId}");
            _sawKilledFeedbackDraw |= arena.LastSwingHits.Any(hit =>
                arena.Monsters.Any(monster => monster.InstanceId == hit.MonsterInstanceId && monster.IsDefeated));
            _sawIndependentFeedbackDraw |= arena.LastSwingHits.Any(hit => hit.WasCritical) &&
                                          arena.LastSwingHits.Any(hit => !hit.WasCritical);
        }
        if (_stage == SmokeStage.ClearEncounter && _phase == 2)
        {
            Check(arena.IsCleared && arena.Projectiles.Count > 0,
                "real Draw presented completion while shooter-death arrows remained in flight");
            Enter(SmokeStage.PauseWithArrow);
        }
        if (_stage != SmokeStage.Complete)
        {
            return false;
        }
        Check(_sawKilledFeedbackDraw && _sawIndependentFeedbackDraw && attackFlash == 0,
            "real Draw received killed-target and independent critical feedback; death cleared swing flash");
        Check(_sawPartialDeath && _sawIsolatedPoses && _sawPartialDeathPixels && _sawPlayerDeathPixels,
            "real smoke observed partial death geometry and per-instance independent phases/events");
        Check(_draws > 20 && double.IsFinite(framesPerSecond) && framesPerSecond > 0,
            $"actual graphics Draw FPS={framesPerSecond:F1}; draws={_draws}");
        Log($"PASS mixed smoke Complete; updates={_updates}; draws={_draws}; completionArrow={_sawCompletionArrow}");
        return true;
    }

    private static Microsoft.Xna.Framework.Color[] BodyPixels(GraphicsDevice device, Microsoft.Xna.Framework.Vector2 anchor)
    {
        var pixels = new Microsoft.Xna.Framework.Color[25];
        var bounds = new Microsoft.Xna.Framework.Rectangle((int)anchor.X - 2, (int)anchor.Y - 2, 5, 5);
        device.GetBackBufferData(bounds, pixels, 0, pixels.Length);
        return pixels;
    }

    private void Enter(SmokeStage stage)
    {
        Log($"PASS stage {_stage} complete; updates={_stageUpdates}");
        _stage = stage;
        _stageUpdates = 0;
        _phase = 0;
    }

    private void CheckGear(ArenaSimulation arena)
    {
        Check(arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon &&
              arena.Player.Inventory.EquippedArmor?.InstanceId == _armor &&
              arena.Player.Inventory.EquippedWeapon.Rolls.SequenceEqual(_weaponRolls) &&
              arena.Player.Inventory.EquippedArmor.Rolls.SequenceEqual(_armorRolls) &&
              _pickupItems.All(item => arena.Player.Inventory.Items.Contains(item)),
            "both equipped slots, inventory identities and fixed rolls retained");
    }

    private static string Snapshot(ArenaSimulation arena) =>
        $"{arena.Player.Position}|{arena.Player.Health}|{arena.Player.AttackSequence}|" +
        string.Join(";", arena.Monsters.Select(monster =>
            $"{monster.InstanceId}:{monster.Position}:{monster.Health}:{monster.IsWindingUp}:" +
            $"{monster.WindupRemaining:R}:{monster.LockedAimDirection}")) + "|" +
        string.Join(";", arena.Projectiles.Select(arrow => arrow.ToString())) + "|" +
        string.Join(";", arena.GroundItems.Select(drop => drop.Item.InstanceId));

    private static NumericsVector Towards(NumericsVector position, NumericsVector target, float stop) =>
        NumericsVector.Distance(position, target) > stop ? NumericsVector.Normalize(target - position) : NumericsVector.Zero;

    private static string FormatHits(ArenaSimulation arena) =>
        string.Join(";", arena.LastSwingHits.Select(hit =>
            $"{hit.MonsterInstanceId}:damage={hit.Damage},critical={hit.WasCritical}"));

    private void Check(bool success, string message)
    {
        if (!success)
        {
            throw new InvalidOperationException($"FAIL {message} (stage {_stage}, phase {_phase}).");
        }
        Log($"PASS {message}");
    }

    private static int RowForSlot(ArenaSimulation arena, GameContent content, ItemSlot slot)
    {
        for (var index = 0; index < arena.Player.Inventory.Items.Count; index++)
        {
            if (content.Items[arena.Player.Inventory.Items[index].DefinitionId].Slot == slot)
            {
                return index;
            }
        }
        throw new InvalidOperationException($"Smoke inventory has no item for slot {slot}.");
    }

    private void Log(string message) => File.AppendAllText(_logPath, message + Environment.NewLine);
}
