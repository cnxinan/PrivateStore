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
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class RuntimeSmokeTest
{
    private enum SmokeStage
    {
        Approach, MultiHit, DodgeArrow, TakeArrow, GatherMaterials, Pickup, ApproachSmith,
        LearnCraft, InspectResources, Equip, ReopenEncounter, ClearEncounter,
        PauseWithArrow, RestartCleanup, DeathRespawn, PauseWithReaction, Complete
    }

    private readonly string _logPath;
    private SmokeStage _stage;
    private int _stageUpdates;
    private int _updates;
    private int _draws;
    private int _phase;
    private bool _selectingRecipe;
    private bool _sawBothPanels;
    private bool _sawResourceTooltip;
    private bool _sawItemTooltip;
    private bool _sawPartialGlobe;
    private int _fullGlobePixels;
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
    private ItemInstance[] _craftedItems = [];
    private GroundResource[] _pickupResources = [];
    private Dictionary<string, int> _retainedMaterials = [];
    private Dictionary<string, int> _retainedRecipes = [];
    private string[] _retainedKnowledge = [];
    private Dictionary<string, int> _materialsBeforeCraft = [];
    private int _recipesBeforeLearn;
    private string _smithSnapshot = "";
    private PlayerStatTotals? _statsBeforeCraft;
    private PlayerStatTotals? _equipPreview;
    private bool _sawCleanReset;
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
    private bool _sawDeathHud;
    private bool _sawPlayerHurtKnockback;
    private bool _sawIgnoredArrowDuringImmunity;
    private bool _sawMonsterLootBeforeFade;
    private bool _sawMonsterProgressWhileDead;
    private bool _sawArrowProgressWhileDead;
    private bool _sawReactionPause;
    private bool _sawResetWhileStunned;
    private ProjectileState[] _projectilesBeforeUpdate = [];
    private NumericsVector[] _monsterPositionsBeforeUpdate = [];
    private NumericsVector _playerPositionBeforeUpdate;
    private int _healthBeforeUpdate;
    private bool _playerWasDeadBeforeUpdate;
    private long _playerDamageUpdate = -1;
    private NumericsVector _playerPositionAtDamage;
    private float _pauseReactionRemaining;
    private float _pauseImmunityRemaining;
    private NumericsVector _pausePosition;
    private double _pauseSwingRemaining;
    private ActorHitText[] _pauseHitTexts = [];
    private bool _sawPlayerHitTextAtReset;
    private string? _interruptedArcherId;
    private bool _sawArcherRecoveryWindup;
    private bool _sawArcherRecoveryLaunch;
    private int _recoveryDodgeUpdates;
    private string[] _windingArcherIdsBeforeUpdate = [];

    public RuntimeSmokeTest(string logPath, bool appendLog = false)
    {
        _logPath = logPath;
        var header = $"DarkLoot mixed smoke; PID={Environment.ProcessId}; UTC={DateTime.UtcNow:O}{Environment.NewLine}";
        if (appendLog)
            File.AppendAllText(logPath, header);
        else
            File.WriteAllText(logPath, header);
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
            MaxHealth = pair.Value.Behavior == MonsterBehavior.Archer ? 60 :
                pair.Key == "monster.fast_wolf" ? 60 : 10,
            MoveSpeed = .01f,
            ContactDamage = pair.Key == "monster.fast_wolf" ? 200 : 0
        });
        var tables = production.ResourceLootTables.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MinQuantity = 8,
            MaxQuantity = 8,
            RecipeChancePercent = 100,
            RecipeIds = [pair.Value.MaterialId == "material.hide" ? "recipe.magic_vest" : "recipe.magic_blade"]
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
        var content = production with
        {
            Monsters = monsters,
            ResourceLootTableDefinitions = tables,
            AffixDefinitions = affixes
        };
        var encounter = configured with
        {
            Spawns =
            [
                new("smoke.slime.1", "monster.training_slime", 215, 260),
                new("smoke.slime.2", "monster.training_slime", 215, 280),
                new("smoke.wolf.1", "monster.fast_wolf", 285, 260),
                new("smoke.wolf.2", "monster.fast_wolf", 285, 280),
                new("smoke.archer.1", "monster.skeleton_archer", 380, 270),
                new("smoke.archer.2", "monster.skeleton_archer", 388, 270)
            ]
        };
        return (content, encounter);
    }

    public Random CreateRandom() => new SmokeRandom();

    public bool ReadyForPersistenceCheck => _stage == SmokeStage.ClearEncounter && _phase == 0;

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
        strings.AddRange(content.Materials.Values.Select(material => material.Name));
        strings.AddRange(content.Recipes.Values.Select(recipe => recipe.Name));
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
                layouts.Add((string.Format(GameText.Crafted, name), 616, 22));
                layouts.Add((InventoryPanel.FormatRolls(item, content), 616, 22));
                layouts.Add((string.Format(GameText.Pickup, name, 1), 400, 22));
                layouts.Add((string.Format(GameText.SlotLine, GameText.Slot(definition.Slot),
                    $"{GameText.Rarity(rarity)} {name}"), 616, 22));
                combinations++;
            }
        }

        layouts.AddRange(new[]
            {
                GameText.Movement, GameText.Controls, GameText.DeathControls, GameText.DeathWorld,
                GameText.Victory, GameText.Objective, GameText.Cleared
            }
            .Select(text => (text, 1080f, 22f)));
        layouts.AddRange(new[] { GameText.Inventory, GameText.Paused, GameText.Empty }
            .Select(text => (text, 616f, 22f)));
        layouts.AddRange(new[]
            {
                GameText.Smith, GameText.SmithPaused, GameText.EmptyResources
            }.Select(text => (text, 616f, 22f)));
        layouts.AddRange(Enum.GetValues<DarkLoot.Gameplay.Crafting.BlacksmithStatus>()
            .Select(status => (GameText.SmithStatus(status), 616f, 22f)));
        layouts.Add((GameText.EquipmentTab, 120, 22));
        layouts.Add((GameText.ResourceTab, 120, 22));
        layouts.Add((GameText.Learn, 74, 22));
        layouts.Add((GameText.Craft, 84, 22));
        layouts.Add((GameText.SmithPrompt, 220, 22));
        layouts.Add((GameText.MaterialCostHelp, 600, 18));
        foreach (var recipe in content.Recipes.Values)
        {
            layouts.Add((recipe.Name, 390, 22));
            layouts.Add((string.Format(GameText.RecipeResource, recipe.Name, int.MaxValue), 600, 22));
            layouts.Add((string.Format(GameText.Learned, recipe.Name), 616, 22));
            layouts.Add((string.Format(GameText.RecipeState, GameText.Known, int.MaxValue), 390, 22));
            layouts.Add((string.Format(GameText.RecipeState, GameText.Unknown, 0), 390, 22));
            foreach (var cost in recipe.Costs)
                layouts.Add((string.Format(GameText.MaterialCost,
                    content.Materials[cost.MaterialId].Name, int.MaxValue, cost.Quantity), 196, 22));
            var shortages = string.Join("  ", recipe.Costs.Select(cost =>
                string.Format(GameText.Shortage, content.Materials[cost.MaterialId].Name, cost.Quantity)));
            layouts.Add((string.Format(GameText.ShortageDetails, GameText.InsufficientMaterials, shortages), 616, 22));
        }
        layouts.AddRange(content.Materials.Values.Select(material =>
            (string.Format(GameText.MaterialResource, material.Name, int.MaxValue), 600f, 22f)));
        layouts.Add((string.Format(GameText.Enemies, 6, 6), 240, 22));
        layouts.Add((string.Format(GameText.Hud, int.MaxValue, int.MaxValue, int.MaxValue, 100, 9999d), 1080, 22));
        layouts.Add((string.Format(GameText.DeathHud, float.MaxValue, 9999d), 1080, 22));
        layouts.Add((string.Format(GameText.Totals, int.MaxValue, int.MaxValue, 100), 616, 22));
        layouts.Add((string.Format(GameText.Comparison, "+2147483647", "-2147483647", "+100"), 256, 21));
        foreach (var monster in content.Monsters.Values)
        {
            layouts.Add((string.Format(GameText.Monster, monster.Name, monster.MaxHealth, monster.MaxHealth), 280, 22));
        }
        layouts.AddRange(content.Materials.Values.Select(material =>
            (string.Format(GameText.Pickup, material.Name, int.MaxValue), 400f, 22f)));
        layouts.AddRange(content.Recipes.Values.Select(recipe =>
            (string.Format(GameText.Pickup, recipe.Name, int.MaxValue), 400f, 22f)));
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

    public GameInputSnapshot NextInput(
        ArenaSimulation arena,
        InventoryPanel panel,
        BlacksmithPanel smith,
        GameContent content,
        int width,
        double swingRemaining)
    {
        _resetBeforeUpdate = arena.ResetSequence;
        _attackBeforeUpdate = arena.Player.AttackSequence;
        _healthBeforeSwing = arena.Monsters.ToDictionary(monster => monster.InstanceId, monster => monster.Health);
        _projectilesBeforeUpdate = arena.Projectiles.ToArray();
        _monsterPositionsBeforeUpdate = arena.Monsters.Select(monster => monster.Position).ToArray();
        _playerPositionBeforeUpdate = arena.Player.Position;
        _healthBeforeUpdate = arena.Player.Health;
        _playerWasDeadBeforeUpdate = arena.Player.IsDead;
        // Any live world update can become the lethal boundary; keep that snapshot through respawn.
        if (!_playerWasDeadBeforeUpdate)
        {
            _retainedMaterials = arena.Player.Resources.Materials.ToDictionary(pair => pair.Key, pair => pair.Value);
            _retainedRecipes = arena.Player.Resources.Recipes.ToDictionary(pair => pair.Key, pair => pair.Value);
            _retainedKnowledge = arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).ToArray();
        }
        _windingArcherIdsBeforeUpdate = arena.Monsters
            .Where(monster => monster.Behavior == MonsterBehavior.Archer && monster.IsWindingUp)
            .Select(monster => monster.InstanceId).ToArray();
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
        _selectingRecipe = false;
        var restart = false;
        var interact = false;
        var escape = false;
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
                var drop = arena.GroundResources.FirstOrDefault();
                if (drop is not null)
                {
                    movement = Towards(arena.Player.Position, drop.Position, 18);
                    pickup = true;
                }
                break;
            case SmokeStage.GatherMaterials:
                var gatherTarget = arena.Monsters.Where(monster => !monster.IsDefeated)
                    .MinBy(monster => NumericsVector.DistanceSquared(monster.Position, arena.Player.Position));
                if (gatherTarget is not null)
                {
                    aim = gatherTarget.Position;
                    movement = Towards(arena.Player.Position, gatherTarget.Position, 40);
                    attack = movement == NumericsVector.Zero;
                }
                break;
            case SmokeStage.ApproachSmith:
                movement = Towards(arena.Player.Position, BlacksmithService.Position, 28);
                break;
            case SmokeStage.LearnCraft:
                if (_phase == 0)
                {
                    var button = new UiLayout(width).SmithButton;
                    pointer = new NumericsVector(button.Center.X, button.Center.Y);
                    attack = true;
                }
                else if (_phase == 7)
                {
                    var button = new UiLayout(width).SmithButton;
                    pointer = new NumericsVector(button.Center.X, button.Center.Y);
                    attack = true;
                }
                else
                {
                    var recipeId = _phase is 3 or 6 ? "recipe.magic_vest" : "recipe.magic_blade";
                    var recipe = content.Recipes[recipeId];
                    var row = content.Recipes.Keys.Order(StringComparer.Ordinal).ToList().IndexOf(recipeId);
                    var learn = _phase is 2 or 3 or 4;
                    _selectingRecipe = smith.SelectedRecipeIndex != row;
                    var button = _selectingRecipe ? smith.RowBounds(width, row) :
                        learn ? smith.LearnButtonBounds(width, row) : smith.CraftButtonBounds(width, row);
                    pointer = new NumericsVector(button.Center.X, button.Center.Y);
                    attack = true;
                    _recipesBeforeLearn = arena.Player.Resources.GetCount(ResourceKind.Recipe, recipeId);
                    _materialsBeforeCraft = arena.Player.Resources.Materials.ToDictionary(pair => pair.Key, pair => pair.Value);
                    _statsBeforeCraft = arena.Player.Stats;
                    Check(_selectingRecipe || _phase is 1 or 4 ||
                          (learn ? smith.CanLearn(arena, recipe) : smith.CanCraft(arena, recipe)),
                        $"real smith button is enabled before accepted click; phase={_phase}; recipe={recipeId}");
                }
                break;
            case SmokeStage.InspectResources:
                var tab = _phase == 3 ? panel.EquipmentTabBounds(width) :
                    _phase is 1 or 2 or 4 ? panel.RowBounds(width, 0) : panel.ResourceTabBounds(width);
                pointer = new NumericsVector(tab.Center.X, tab.Center.Y);
                attack = _phase is not (2 or 4);
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
                    var candidate = arena.Player.Inventory.Items[RowForSlot(arena, content, slot)];
                    _equipPreview = new ItemStatCalculator().Preview(arena.Player.BaseAttackDamage,
                        arena.Player.BaseMaximumHealth, arena.Player.Inventory, content, slot, candidate);
                }
                break;
            case SmokeStage.ReopenEncounter:
                escape = _phase == 0;
                if (_phase == 1)
                {
                    var button = new UiLayout(width).RestartButton;
                    pointer = new NumericsVector(button.Center.X, button.Center.Y);
                    attack = pickup = true;
                    movement = NumericsVector.UnitY;
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
                        if (_interruptedArcherId == target.InstanceId)
                        {
                            attack = _sawArcherRecoveryLaunch && movement == NumericsVector.Zero;
                            if (!_sawArcherRecoveryLaunch && _sawArcherRecoveryWindup &&
                                _recoveryDodgeUpdates < 2)
                            {
                                movement = new NumericsVector(-target.LockedAimDirection.Y,
                                    target.LockedAimDirection.X);
                                _recoveryDodgeUpdates++;
                            }
                            else if (!_sawArcherRecoveryLaunch)
                            {
                                movement = NumericsVector.Zero;
                            }
                        }
                        else
                        {
                            attack = movement == NumericsVector.Zero && target.IsWindingUp;
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
                    if (arena.GroundResources.Count > 0)
                    {
                        // Keep dodging locked arrows so one can coexist with the next windup.
                        movement = Towards(arena.Player.Position,
                            new NumericsVector(215, _stageUpdates % 20 < 10 ? 180 : 360), 10);
                        attack = false;
                    }
                    if (!arena.Player.IsDead && !arena.Player.Reaction.IsActive &&
                        _updates - _lastSwingUpdate >= 4 &&
                        arena.Projectiles.Count > 0 && arena.GroundResources.Count > 0 &&
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
                if (_phase == 0)
                {
                    escape = true;
                }
                else if (_phase == 1)
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
            case SmokeStage.PauseWithReaction:
                if (_phase == 0)
                {
                    var target = arena.Monsters.First(monster =>
                        monster.Id == "monster.training_slime" && !monster.IsDefeated);
                    aim = target.Position;
                    movement = Towards(arena.Player.Position, target.Position, 40);
                    attack = movement == NumericsVector.Zero;
                }
                else if (_phase == 1)
                {
                    aim = arena.Player.Position - NumericsVector.UnitX * 100;
                    attack = arena.Projectiles.Any(projectile =>
                        CrossesPlayer(projectile, arena.Player.Position, arena.Player.Position));
                }
                else if (_phase == 2)
                {
                    toggle = true;
                }
                else if (_phase == 4)
                {
                    restart = true;
                }
                else if (_phase == 5)
                {
                    escape = true;
                }
                else if (_phase == 7)
                {
                    restart = true;
                    Check(!panel.IsOpen && arena.Player.Reaction.IsActive &&
                          arena.Player.IsInvulnerable,
                        "R request begins from a live stunned player with immunity");
                }
                break;
        }
        if (restart)
            _sawCleanReset = false;
        return new GameInputSnapshot(movement, aim, pointer, attack, pickup, toggle, restart, escape,
            AttackHeld: attack && !panel.IsOpen && !smith.IsOpen, InteractPressed: interact);
    }

    public void AfterUpdate(
        ArenaSimulation arena,
        InventoryPanel panel,
        BlacksmithPanel smith,
        GameContent content,
        ActorPose playerPose,
        Func<string, ActorPose> monsterPose,
        double swingRemaining,
        IReadOnlyList<ActorHitText> hitTexts)
    {
        if (arena.Player.IsDead || _playerWasDeadBeforeUpdate)
            CheckResourceRetention(arena);

        var playerDamage = arena.Events.FirstOrDefault(item =>
            item.Kind == CombatEventKind.Damaged && item.ActorId == "player");
        if (playerDamage is not null)
        {
            _playerDamageUpdate = _updates;
            _playerPositionAtDamage = arena.Player.Position;
        }
        else if (_playerDamageUpdate >= 0 && _updates > _playerDamageUpdate &&
                 arena.Player.Reaction.IsActive &&
                 arena.Player.Position != _playerPositionAtDamage)
        {
            _sawPlayerHurtKnockback = true;
            Check(true, "actual player damage produced active stun and real knockback displacement");
            _playerDamageUpdate = -1;
        }

        if (!arena.Player.IsDead && arena.Player.IsInvulnerable &&
            arena.Player.Health == _healthBeforeUpdate &&
            !arena.Events.Any(item => item.Kind == CombatEventKind.Damaged && item.ActorId == "player") &&
            WasBlockedArrowRemoved(arena))
        {
            _sawIgnoredArrowDuringImmunity = true;
            Check(true, "a second real arrow was removed during active immunity without extending damage");
        }

        if (_playerWasDeadBeforeUpdate && arena.Player.IsDead)
        {
            _sawMonsterProgressWhileDead |= arena.Monsters
                .Select((monster, index) => monster.Position != _monsterPositionsBeforeUpdate[index])
                .Any(changed => changed);
            _sawArrowProgressWhileDead |= _projectilesBeforeUpdate.Any(before => arena.Projectiles.Any(current =>
                current.SourceInstanceId == before.SourceInstanceId && current.Position != before.Position));
        }

        if (arena.Player.IsDead)
        {
            var header = HudRenderer.PlayerHeader(arena, 0);
            Check(header == string.Format(GameText.DeathHud, arena.Player.DeathRemaining, 0d) &&
                  !header.Contains("生命", StringComparison.Ordinal),
                "death HUD shows the actual countdown instead of a misleading life total");
            if (!_sawDeathHud)
            {
                Check(HudRenderer.MovementInstructions(arena) == GameText.DeathControls &&
                      HudRenderer.ControlInstructions(arena) == GameText.DeathWorld,
                    "death HUD explains disabled controls and continuing enemies");
                _sawDeathHud = true;
            }
        }

        foreach (var death in arena.Events.Where(item => item.Kind == CombatEventKind.Died))
        {
            var pose = death.ActorId == "player" ? playerPose : monsterPose(death.ActorId);
            var anchor = death.ActorId == "player" ? arena.Player.Position :
                arena.Monsters.Single(monster => monster.InstanceId == death.ActorId).Position;
            Check(pose.Opacity == 1 && pose.Scale == NumericsVector.One && anchor == death.Position,
                $"new death starts visible at actual death position; actor={death.ActorId}");
            if (death.ActorId != "player" && !_sawMonsterLootBeforeFade)
            {
                Check(arena.GroundResources.Count > 0 &&
                      arena.Events.Any(item => item.ActorId == death.ActorId &&
                          item.Kind is CombatEventKind.Damaged or CombatEventKind.Died),
                    "actual monster death resolved ground loot before its corpse faded");
                _sawMonsterLootBeforeFade = true;
            }
        }

        foreach (var damage in arena.Events.Where(item =>
                     item.Kind == CombatEventKind.Damaged && item.ActorId != "player"))
        {
            if (_windingArcherIdsBeforeUpdate.Contains(damage.ActorId, StringComparer.Ordinal))
            {
                var archer = arena.Monsters.Single(monster => monster.InstanceId == damage.ActorId);
                if (!archer.IsDefeated && archer.Behavior == MonsterBehavior.Archer &&
                    archer.Reaction.IsActive && !archer.IsWindingUp)
                {
                    _interruptedArcherId = archer.InstanceId;
                    _sawArcherRecoveryWindup = false;
                    _sawArcherRecoveryLaunch = false;
                    _recoveryDodgeUpdates = 0;
                    Check(true, $"real accepted melee interrupted archer windup; archer={archer.InstanceId}");
                }
            }
        }

        if (_interruptedArcherId is not null)
        {
            var archer = arena.Monsters.Single(monster => monster.InstanceId == _interruptedArcherId);
            _sawArcherRecoveryWindup |= !archer.Reaction.IsActive && archer.IsWindingUp;
            if (!_sawArcherRecoveryLaunch && _sawArcherRecoveryWindup &&
                arena.Events.Any(item => item.Kind == CombatEventKind.ArrowLaunched &&
                    item.ActorId == _interruptedArcherId) &&
                arena.Projectiles.Any(item => item.SourceInstanceId == _interruptedArcherId))
            {
                _sawArcherRecoveryLaunch = true;
                Check(true, $"interrupted archer recovered, completed a fresh windup and launched; archer={_interruptedArcherId}");
            }
        }
        var partialDeath = arena.Monsters.FirstOrDefault(monster => monster.IsDefeated &&
            monsterPose(monster.InstanceId).Opacity is > 0 and < 1);
        if (partialDeath is not null)
        {
            Check(_sawMonsterLootBeforeFade,
                "monster death fade begins only after live loot resolution");
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
                    Check(arena.GroundResources.Count > 0, "fixture generated monster crafting resources");
                    Enter(SmokeStage.GatherMaterials);
                }
                break;
            case SmokeStage.GatherMaterials when arena.IsCleared:
                _pickupResources = arena.GroundResources.ToArray();
                Check(_pickupResources.Length == 12 && arena.Player.Inventory.Items.Count == 0 &&
                      arena.Blacksmith.KnownRecipeIds.Count == 0,
                    "six real unarmed kills yielded material and recipe stacks, never equipment or knowledge");
                Enter(SmokeStage.Pickup);
                break;
            case SmokeStage.Pickup when arena.GroundResources.Count == 0:
                Check(_pickupResources.GroupBy(drop => (drop.Stack.Kind, drop.Stack.DefinitionId)).All(group =>
                        arena.Player.Resources.GetCount(group.Key.Kind, group.Key.DefinitionId) ==
                        group.Sum(drop => drop.Stack.Quantity)),
                    "real E pickups transferred exactly every generated stack into the resource bag");
                foreach (var drop in _pickupResources)
                {
                    var count = arena.Player.Resources.GetCount(drop.Stack.Kind, drop.Stack.DefinitionId);
                    Check(arena.ResourceOperations.PickUp(arena.Player, drop) == ResourcePickupResult.AlreadyCollected,
                        $"real pickup receipt rejects duplicate identity without changing counts; drop={drop.DropId}");
                    Check(arena.Player.Resources.GetCount(drop.Stack.Kind, drop.Stack.DefinitionId) == count,
                        "duplicate receipt leaves the real owned count unchanged");
                }
                Check(arena.Player.Inventory.Items.Count == 0 &&
                      arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade") > 0 &&
                      arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_vest") > 0,
                    "picked both required recipe items but no directly granted equipment");
                Enter(SmokeStage.ApproachSmith);
                break;
            case SmokeStage.ApproachSmith when
                NumericsVector.Distance(arena.Player.Position, BlacksmithService.Position) <= 28:
                Check(arena.BlacksmithOperations.CanInteract(arena.Player),
                    "actual movement reached the smith within the real 64-unit gate");
                _healthBefore = arena.Player.Health;
                _smithSnapshot = Snapshot(arena);
                _pausedPlayerPose = playerPose;
                _pausedMonsterPoses = arena.Monsters.Select(monster => monsterPose(monster.InstanceId)).ToArray();
                _pauseSwingRemaining = swingRemaining;
                _pauseHitTexts = hitTexts.ToArray();
                Enter(SmokeStage.LearnCraft);
                break;
            case SmokeStage.LearnCraft:
                Check(Snapshot(arena) == _smithSnapshot && playerPose == _pausedPlayerPose &&
                      arena.Monsters.Select(monster => monsterPose(monster.InstanceId)).SequenceEqual(_pausedMonsterPoses) &&
                      swingRemaining == _pauseSwingRemaining && hitTexts.SequenceEqual(_pauseHitTexts),
                    "smith and switch frames freeze actual combat, health, timers and every actor pose");
                if (_phase == 0)
                {
                    Check(smith.IsOpen && panel.IsOpen, "console smith button opened both real panels with smith focus");
                }
                else if (_phase == 7)
                {
                    Check(panel.IsOpen && !smith.IsOpen, "console smith button closed only smith and retained inventory");
                    Enter(SmokeStage.InspectResources);
                    break;
                }
                else if (_selectingRecipe)
                {
                    var selectedId = _phase is 3 or 6 ? "recipe.magic_vest" : "recipe.magic_blade";
                    Check(smith.SelectedRecipeIndex ==
                          content.Recipes.Keys.Order(StringComparer.Ordinal).ToList().IndexOf(selectedId) &&
                          CountsEqual(_retainedMaterials, arena.Player.Resources.Materials) &&
                          CountsEqual(_retainedRecipes, arena.Player.Resources.Recipes) &&
                          arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).SequenceEqual(_retainedKnowledge) &&
                          arena.Player.Inventory.Items.SequenceEqual(_craftedItems) &&
                          arena.Player.Stats == _statsBeforeCraft,
                        "selecting a real recipe changed details but not resources, knowledge, items or stats");
                    break;
                }
                else if (_phase is 1 or 4)
                {
                    var expected = _phase == 1 ? BlacksmithStatus.NotLearned : BlacksmithStatus.AlreadyLearned;
                    Check(smith.LastResult?.Status == expected && smith.Feedback == GameText.SmithStatus(expected) &&
                          CountsEqual(_materialsBeforeCraft, arena.Player.Resources.Materials) &&
                          arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade") == _recipesBeforeLearn,
                        $"disabled real button reports {expected} without consuming materials or duplicate recipes");
                }
                else
                {
                    var recipeId = _phase is 3 or 6 ? "recipe.magic_vest" : "recipe.magic_blade";
                    var recipe = content.Recipes[recipeId];
                    Check(smith.LastResult?.Status == BlacksmithStatus.Applied,
                        $"actual smith click applied; phase={_phase}; recipe={recipeId}");
                    if (_phase is 2 or 3)
                    {
                        Check(arena.Blacksmith.HasLearned(recipeId) &&
                              arena.Player.Resources.GetCount(ResourceKind.Recipe, recipeId) == _recipesBeforeLearn - 1 &&
                              CountsEqual(_materialsBeforeCraft, arena.Player.Resources.Materials) &&
                              smith.Feedback == string.Format(GameText.Learned, recipe.Name),
                            "learning consumed exactly one actual recipe and no materials");
                    }
                    else
                    {
                        var item = smith.LastResult!.Item!;
                        Check(item.InstanceId != Guid.Empty &&
                              arena.Player.Inventory.Find(item.InstanceId) == item &&
                              _craftedItems.All(owned => owned.InstanceId != item.InstanceId) &&
                              item.DefinitionId == recipe.ItemId && item.Rarity == recipe.Rarity &&
                              item.Rolls.Count == 1 &&
                              _materialsBeforeCraft.All(pair =>
                                  arena.Player.Resources.GetCount(ResourceKind.Material, pair.Key) ==
                                  pair.Value - recipe.Costs.Where(cost => cost.MaterialId == pair.Key).Sum(cost => cost.Quantity)),
                            "craft created a new owned fixed-quality instance and charged the full production costs");
                        Check(arena.Player.Inventory.EquippedWeapon is null &&
                              arena.Player.Inventory.EquippedArmor is null &&
                              arena.Player.Health == _healthBefore && arena.Player.Stats == _statsBeforeCraft &&
                              arena.Player.Resources.GetCount(ResourceKind.Recipe, recipeId) == _recipesBeforeLearn &&
                              smith.Feedback == string.Format(GameText.Crafted,
                                  ItemNameFormatter.GetDisplayName(item, content.Items[item.DefinitionId], content)) &&
                              smith.CraftedRolls == InventoryPanel.FormatRolls(item, content),
                            "craft showed actual name and rolls, did not auto-equip, heal or consume more recipes");
                        _craftedItems = [.. _craftedItems, item];
                    }
                }
                _phase++;
                break;
            case SmokeStage.InspectResources:
                Check(panel.IsOpen && Snapshot(arena) == _smithSnapshot &&
                      arena.Player.Inventory.EquippedWeapon is null && arena.Player.Inventory.EquippedArmor is null,
                    "real resource tab and resource row clicks freeze world and never equip");
                if (_phase < 3)
                {
                    Check(panel.Page == InventoryPage.Resources &&
                          InventoryPanel.ResourceRows(arena.Player, content).Count == 5,
                        "resource grid shows actual three material counts and both retained duplicate recipes");
                    if (_phase is 1 or 2)
                        Check(panel.Tooltip is not null, "resource cell click/hover produces a real exact-count tooltip");
                    if (_phase == 2 && _draws > 0 && !_sawResourceTooltip)
                        break;
                    _phase++;
                }
                else
                {
                    Check(panel.Page == InventoryPage.Equipment, "actual equipment tab restores item cells");
                    if (_phase == 4)
                    {
                        Check(panel.Tooltip is not null, "equipment hover produces details without equipping");
                        if (_draws > 0 && !_sawItemTooltip)
                            break;
                        Enter(SmokeStage.Equip);
                    }
                    else
                        _phase++;
                }
                break;
            case SmokeStage.Equip when panel.IsOpen:
                var slot = _phase == 1 ? ItemSlot.Armor : ItemSlot.Weapon;
                var equipped = slot == ItemSlot.Weapon
                    ? arena.Player.Inventory.EquippedWeapon : arena.Player.Inventory.EquippedArmor;
                if (equipped is null)
                {
                    break;
                }
                Check(arena.Player.Stats == _equipPreview && arena.Player.Health == _healthBefore,
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
                    _pauseSnapshot = Snapshot(arena);
                    Enter(SmokeStage.ReopenEncounter);
                }
                break;
            case SmokeStage.ReopenEncounter:
                if (_phase == 0)
                {
                    Check(!panel.IsOpen && !smith.IsOpen && Snapshot(arena) == _pauseSnapshot,
                        "Esc closes the inventory without exiting or advancing the closing frame");
                    _phase = 1;
                }
                else
                {
                    Check(_sawCleanReset && arena.ResetSequence == _resetBeforeUpdate + 1,
                        "live R begins a new six-target round after crafting");
                    CheckGear(arena);
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
                    Check(arena.Projectiles.Count > 0 && arena.GroundResources.Count > 0 &&
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
                    Check(!panel.IsOpen && Snapshot(arena) == _pauseSnapshot &&
                          playerPose == _pausedPlayerPose &&
                          arena.Monsters.Select(monster => monsterPose(monster.InstanceId))
                              .SequenceEqual(_pausedMonsterPoses),
                        "Esc closing frame retains all paused state and animation without click-through");
                    _phase = 1;
                    break;
                }
                if (_phase == 1)
                {
                    Check(!panel.IsOpen && Snapshot(arena) != _pauseSnapshot &&
                          arena.Player.AttackSequence == _sequence,
                        "closing inventory resumed live arrow/windup but retained pre-pause swing cooldown");
                    Check(arena.GroundResources.Count > 0 && arena.Projectiles.Count > 0,
                        "R cleanup precondition still has real ground loot and arrows after resume");
                    _phase = 2;
                    break;
                }
                Check(!panel.IsOpen && _sawCleanReset && arena.ResetSequence == _resetBeforeUpdate + 1,
                    "RestartCleanup live R routed after close; zero-time cleanup was observed before world resumed");
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
                          arena.Projectiles.Count == 0 && arena.GroundResources.Count == 0 &&
                          arena.LastSwingHits.Count == 0 && arena.Events.Count == 0,
                        "DeathRespawn after 0.6s restored spawn/current maximum health, six clean monsters and full 0.8s protection");
                    Check(_sawDeathHud && _sawMonsterProgressWhileDead && _sawArrowProgressWhileDead,
                        "DeathRespawn showed countdown while both monsters and arrows advanced through the corpse");
                    CheckGear(arena);
                    Enter(SmokeStage.PauseWithReaction);
                }
                break;
            case SmokeStage.PauseWithReaction:
                if (_phase == 0 && arena.Monsters.Any(monster =>
                        monster.Id == "monster.training_slime" && monster.IsDefeated) &&
                    arena.GroundResources.Count > 0)
                {
                    Check(arena.Events.Any(item => item.Kind == CombatEventKind.Died &&
                              arena.Monsters.Any(monster => monster.InstanceId == item.ActorId)) &&
                          _sawMonsterLootBeforeFade,
                        "PauseWithReaction setup killed a real slime and retained its generated loot");
                    _phase = 1;
                }
                else if (_phase == 1 && playerDamage is not null)
                {
                    Check(arena.Player.Reaction.IsActive && arena.Player.IsInvulnerable &&
                          arena.Player.Health < _healthBeforeUpdate &&
                          arena.Player.AttackSequence == _attackBeforeUpdate + 1 &&
                          arena.Events.Any(item => item.Kind == CombatEventKind.Damaged &&
                              item.ActorId == "player") &&
                          arena.Events.Any(item => item.Kind == CombatEventKind.Swing &&
                              item.ActorId == "player") &&
                          arena.Projectiles.Count < _projectilesBeforeUpdate.Length,
                        "PauseWithReaction setup combined a real arrow hit, accepted swing, stun and immunity");
                    _phase = 2;
                }
                else if (_phase == 2 && panel.IsOpen)
                {
                    Check(arena.Player.Reaction.RemainingSeconds > 0 &&
                          arena.Player.InvulnerabilityRemaining > 0 &&
                          playerPose.WhiteMix > 0 && playerPose.Scale.Y < 1 &&
                          hitTexts.Any(text => text.ActorId == "player") &&
                          arena.Monsters.Any(monster => monster.IsDefeated) &&
                          arena.GroundResources.Count > 0,
                        "PauseWithReaction froze actual hurt pose, stun, immunity, damage feedback, corpse and loot");
                    _pausedPlayerPose = playerPose;
                    _pausedMonsterPoses = arena.Monsters.Select(monster => monsterPose(monster.InstanceId)).ToArray();
                    _pauseSnapshot = Snapshot(arena);
                    _pauseReactionRemaining = arena.Player.Reaction.RemainingSeconds;
                    _pauseImmunityRemaining = arena.Player.InvulnerabilityRemaining;
                    _pausePosition = arena.Player.Position;
                    _pauseSwingRemaining = swingRemaining;
                    _pauseHitTexts = hitTexts.ToArray();
                    _healthBeforeRestart = arena.Player.Health;
                    _pausedUpdates = 0;
                    _sawReactionPause = true;
                    _phase = 3;
                    break;
                }
                else if (_phase == 3)
                {
                    Check(panel.IsOpen && Snapshot(arena) == _pauseSnapshot &&
                          playerPose == _pausedPlayerPose &&
                          arena.Monsters.Select(monster => monsterPose(monster.InstanceId))
                              .SequenceEqual(_pausedMonsterPoses) &&
                          swingRemaining == _pauseSwingRemaining &&
                          hitTexts.SequenceEqual(_pauseHitTexts),
                        $"PauseWithReaction froze every actor, timer, arrow, corpse, loot and pose; update={++_pausedUpdates}");
                    if (_pausedUpdates == 20)
                    {
                        _phase = 4;
                    }
                    break;
                }
                else if (_phase == 4)
                {
                    Check(panel.IsOpen && Snapshot(arena) == _pauseSnapshot &&
                          playerPose == _pausedPlayerPose && swingRemaining == _pauseSwingRemaining &&
                          hitTexts.SequenceEqual(_pauseHitTexts) &&
                          arena.ResetSequence == _resetBeforeUpdate,
                        "R is refused while inventory pauses actual stun, immunity, arrows, corpse and feedback");
                    _phase = 5;
                }
                else if (_phase == 5)
                {
                    Check(!panel.IsOpen && Snapshot(arena) == _pauseSnapshot &&
                          playerPose == _pausedPlayerPose && swingRemaining == _pauseSwingRemaining &&
                          hitTexts.SequenceEqual(_pauseHitTexts),
                        "Esc closes hurt inventory without spending any stun or animation time");
                    _phase = 6;
                }
                else if (_phase == 6)
                {
                    Check(Snapshot(arena) != _pauseSnapshot && arena.Player.Reaction.IsActive &&
                          arena.Player.Reaction.RemainingSeconds < _pauseReactionRemaining &&
                          arena.Player.InvulnerabilityRemaining < _pauseImmunityRemaining &&
                          arena.Player.Position != _pausePosition &&
                          arena.Player.Health == _healthBeforeRestart &&
                          _sawIgnoredArrowDuringImmunity &&
                          hitTexts.Any(text => text.ActorId == "player"),
                        "closing inventory resumed knockback, stun, immunity and damage feedback; ignored arrow was removed");
                    _sawPlayerHitTextAtReset = hitTexts.Any(text => text.ActorId == "player");
                    _sawPlayerHurtKnockback = true;
                    _phase = 7;
                    break;
                }
                else if (_phase == 7)
                {
                    Check(!panel.IsOpen && _sawCleanReset && arena.ResetSequence == _resetBeforeUpdate + 1,
                        "live R while stunned cleared feedback after the gated pause and close");
                    _sawResetWhileStunned = true;
                    Check(_sawPlayerHitTextAtReset,
                        "R cleared actual player damage feedback after the stun pause");
                    CheckGear(arena);
                    Enter(SmokeStage.Complete);
                }
                break;
        }
        if (_stage == SmokeStage.RestartCleanup)
        {
            _healthBeforeRestart = arena.Player.Health;
        }
    }

    private int _healthBeforeRestart;

    public void ValidateUiDraw(GraphicsDevice device, SpriteFont font, ArenaSimulation arena,
        InventoryPanel inventory, BlacksmithPanel smith)
    {
        var panels = !_sawBothPanels && inventory.IsOpen && smith.IsOpen;
        var tooltip = inventory.IsOpen && inventory.Tooltip is not null &&
            (inventory.Page == InventoryPage.Resources ? !_sawResourceTooltip : !_sawItemTooltip);
        var full = arena.Player.Health == arena.Player.MaximumHealth && _fullGlobePixels == 0;
        var partial = !_sawPartialGlobe && arena.Player.Health > 0 && arena.Player.Health < arena.Player.MaximumHealth;
        if (!panels && !tooltip && !full && !partial)
            return;
        var layout = new UiLayout(device.Viewport.Width, device.Viewport.Height);
        var pixels = new Microsoft.Xna.Framework.Color[layout.Width * layout.Height];
        device.GetBackBufferData(pixels);
        Microsoft.Xna.Framework.Color Pixel(int x, int y) => pixels[y * layout.Width + x];
        bool GoldEdge(int x, int y)
        {
            var color = Pixel(x, y);
            return color.R > 100 && color.R > color.G && color.G > color.B;
        }
        if (panels)
        {
            Check(GoldEdge(layout.Inventory.X + 60, layout.Inventory.Y) &&
                  GoldEdge(layout.Blacksmith.X + 60, layout.Blacksmith.Y),
                "real Draw contains both independent brass-framed gothic panels");
            Check(Enumerable.Range(0, 32).All(index =>
                Pixel(layout.Cell(index).Center.X, layout.Cell(index).Center.Y) ==
                    new Microsoft.Xna.Framework.Color(13, 15, 18)),
                "real Draw contains all 32 empty inset cells in the shared 8x4 grid");
            _sawBothPanels = true;
            Capture("gothic-ui-both.png");
        }
        if (tooltip)
        {
            var measured = inventory.Tooltip!.Layout(font, inventory.Pointer, layout);
            Check(new Microsoft.Xna.Framework.Rectangle(0, 0, layout.Width, layout.Console.Top).Contains(measured.Bounds) &&
                  GoldEdge(measured.Bounds.X + 5, measured.Bounds.Y) &&
                  measured.Lines.All(line => line.Text.All(font.Characters.Contains) &&
                      font.MeasureString(line.Text).X * .9f <= measured.Bounds.Width - 24),
                "real Draw tooltip border, loaded glyphs and wrapped text fit inside playfield");
            if (inventory.Page == InventoryPage.Resources)
                _sawResourceTooltip = true;
            else
                _sawItemTooltip = true;
            Capture(inventory.Page == InventoryPage.Resources ? "gothic-ui-resource-tooltip.png" : "gothic-ui-item-tooltip.png");
        }
        if (full || partial)
        {
            var red = Enumerable.Range(layout.Globe.Top, layout.Globe.Height)
                .Sum(y => Enumerable.Range(layout.Globe.Left, layout.Globe.Width).Count(x =>
                {
                    var color = Pixel(x, y);
                    return color.R > 65 && color.R > color.G * 2 && color.R > color.B * 2;
                }));
            Check(red > 600 && (!partial || _fullGlobePixels == 0 || red < _fullGlobePixels),
                $"real Draw health globe follows actual {(full ? "full" : "partial")} health; redPixels={red}");
            if (full)
                _fullGlobePixels = red;
            else
                _sawPartialGlobe = true;
        }

        void Capture(string name)
        {
            using var texture = new Texture2D(device, layout.Width, layout.Height);
            texture.SetData(pixels);
            using var file = File.Create(Path.Combine(Path.GetDirectoryName(_logPath)!, name));
            texture.SaveAsPng(file, layout.Width, layout.Height);
        }
    }

    public void ValidateUiAcceptance() =>
        Check(_sawBothPanels && _sawResourceTooltip && _sawItemTooltip && _sawPartialGlobe && _fullGlobePixels > 0,
            "gothic UI actual Draw acceptance covered dual panels, 8x4 grid, both tooltips and full/partial globe");

    public void ValidatePanelPresentation(SpriteFont font, ArenaSimulation arena,
        InventoryPanel inventory, BlacksmithPanel smith, GameContent content)
    {
        var layouts = new List<(string Text, float Width, float Height)>();
        if (smith.IsOpen)
        {
            foreach (var recipe in content.Recipes.Values)
            {
                layouts.Add((recipe.Name, 390, 22));
                layouts.Add((string.Format(GameText.RecipeState,
                    arena.Blacksmith.HasLearned(recipe.Id) ? GameText.Known : GameText.Unknown,
                    arena.Player.Resources.GetCount(ResourceKind.Recipe, recipe.Id)), 390, 22));
                layouts.AddRange(recipe.Costs.Select(cost =>
                    (string.Format(GameText.MaterialCost, content.Materials[cost.MaterialId].Name,
                        arena.Player.Resources.GetCount(ResourceKind.Material, cost.MaterialId), cost.Quantity), 196f, 22f)));
            }
            layouts.Add((GameText.Learn, 74, 22));
            layouts.Add((GameText.Craft, 84, 22));
            layouts.Add((smith.Feedback, 616, 22));
            layouts.Add((smith.CraftedRolls, 616, 22));
        }
        if (inventory.IsOpen && inventory.Page == InventoryPage.Resources)
            layouts.AddRange(InventoryPanel.ResourceRows(arena.Player, content).Select(text => (text, 600f, 22f)));

        foreach (var (text, width, height) in layouts.Where(layout => layout.Text.Length > 0))
        {
            Check(text.All(character => font.Characters.Contains(character)),
                $"actual panel output has loaded glyphs; text={text}");
            var measured = font.MeasureString(text);
            var scale = Math.Min(1f, Math.Min(width / Math.Max(1, measured.X), height / Math.Max(1, measured.Y)));
            Check(float.IsFinite(scale) && scale >= .60f,
                $"actual panel MeasureString fits {width}x{height}; scale={scale:F3}; text={text}");
        }
    }

    public void AfterReset(ArenaSimulation arena, ActorPresentation presentation)
    {
        Check(arena.ResetSequence == _resetBeforeUpdate + 1 &&
              arena.Projectiles.Count == 0 && arena.GroundResources.Count == 0 &&
              arena.RemainingMonsters == 6 &&
              arena.Monsters.All(monster => monster.Health == monster.MaxHealth &&
                  !monster.IsWindingUp && !monster.Reaction.IsActive) &&
              arena.LastSwingHits.Count == 0 &&
              !arena.Player.Reaction.IsActive && !arena.Player.IsInvulnerable &&
              arena.Player.Health == _healthBeforeUpdate &&
              arena.Player.Position == _playerPositionBeforeUpdate &&
              presentation.SwingRemaining == 0 && presentation.HitTexts.Count == 0 &&
              presentation.PlayerPose.Offset == NumericsVector.Zero &&
              presentation.PlayerPose.AttackProgress == 0 && presentation.PlayerPose.Rotation == 0 &&
              arena.Monsters.All(monster =>
                  presentation.GetMonsterPose(monster.InstanceId).Scale == NumericsVector.One),
            "actual R reset cleans six actors, arrows, loot, stun, immunity and animation at zero time without healing");
        CheckGear(arena);
        CheckResourceRetention(arena);
        _sawCleanReset = true;
    }

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
            Check(ActorPixels(device, camera.WorldToScreen(arena.Player.Position))
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
        ValidateUiAcceptance();
        Check(_sawKilledFeedbackDraw && _sawIndependentFeedbackDraw && attackFlash == 0,
            "real Draw received killed-target and independent critical feedback; death cleared swing flash");
        Check(_sawPartialDeath && _sawIsolatedPoses && _sawPartialDeathPixels && _sawPlayerDeathPixels,
            "real smoke observed partial death geometry and per-instance independent phases/events");
        Check(_sawDeathHud && _sawPlayerHurtKnockback && _sawIgnoredArrowDuringImmunity &&
              _sawMonsterLootBeforeFade && _sawMonsterProgressWhileDead &&
              _sawArrowProgressWhileDead && _sawReactionPause &&
              _sawResetWhileStunned && _sawArcherRecoveryLaunch,
            "named lifecycle acceptance observed actual hurt, immunity, ignored arrow, corpse loot, death, respawn, pause, reset and archer recovery");
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

    private static Microsoft.Xna.Framework.Color[] ActorPixels(GraphicsDevice device, Microsoft.Xna.Framework.Vector2 anchor)
    {
        const int size = 31;
        var pixels = new Microsoft.Xna.Framework.Color[size * size];
        var bounds = new Microsoft.Xna.Framework.Rectangle(
            (int)anchor.X - size / 2, (int)anchor.Y - size / 2, size, size);
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
              _craftedItems.Length == 2 &&
              _craftedItems.All(item => arena.Player.Inventory.Items.Contains(item)),
            "both crafted slots, inventory identities and fixed rolls retained");
    }

    private void CheckResourceRetention(ArenaSimulation arena)
    {
        Check(CountsEqual(_retainedMaterials, arena.Player.Resources.Materials) &&
              CountsEqual(_retainedRecipes, arena.Player.Resources.Recipes) &&
              arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).SequenceEqual(_retainedKnowledge),
            "boundary material counts, recipe counts and learned knowledge retained");
    }

    private static bool CountsEqual(IReadOnlyDictionary<string, int> expected, IReadOnlyDictionary<string, int> actual) =>
        expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static string Snapshot(ArenaSimulation arena) =>
        $"{arena.Player.Position}|{arena.Player.Health}|{arena.Player.AttackSequence}|" +
        $"{arena.Player.Reaction.RemainingSeconds:R}|{arena.Player.InvulnerabilityRemaining:R}|" +
        $"{arena.Player.DeathRemaining:R}|" +
        string.Join(";", arena.Monsters.Select(monster =>
            $"{monster.InstanceId}:{monster.Position}:{monster.Health}:{monster.Reaction.RemainingSeconds:R}:{monster.IsWindingUp}:" +
            $"{monster.WindupRemaining:R}:{monster.LockedAimDirection}")) + "|" +
        string.Join(";", arena.Projectiles.Select(arrow => arrow.ToString())) + "|" +
        string.Join(";", arena.GroundResources.Select(drop => drop.DropId));

    private bool WasBlockedArrowRemoved(ArenaSimulation arena)
    {
        foreach (var before in _projectilesBeforeUpdate)
        {
            if (arena.Projectiles.Any(current =>
                    current.SourceInstanceId == before.SourceInstanceId) ||
                !CrossesPlayer(before, _playerPositionBeforeUpdate, arena.Player.Position))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool CrossesPlayer(
        ProjectileState projectile,
        NumericsVector previousPosition,
        NumericsVector currentPosition)
    {
        var end = projectile.Position + projectile.Direction * (projectile.Speed * .1f);
        foreach (var playerPosition in new[] { previousPosition, currentPosition })
        {
            var travel = end - projectile.Position;
            var lengthSquared = travel.LengthSquared();
            var t = lengthSquared == 0
                ? 0
                : Math.Clamp(NumericsVector.Dot(playerPosition - projectile.Position, travel) / lengthSquared, 0, 1);
            var nearest = projectile.Position + travel * t;
            if (NumericsVector.DistanceSquared(nearest, playerPosition) <= 14 * 14)
            {
                return true;
            }
        }

        return false;
    }

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
