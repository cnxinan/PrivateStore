using System;
using System.IO;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class RuntimeSmokeTest
{
    private readonly string _logPath;
    private int _stage;
    private int _updates;
    private int _draws;
    private int _pausedUpdates;
    private int _previousHealth = 100;
    private int _healthBeforeEquipping;
    private NumericsVector _startPosition;
    private NumericsVector _pausedPlayerPosition;
    private NumericsVector _pausedMonsterPosition;
    private int _pausedHealth;
    private Guid _weapon;
    private Guid _armor;
    private AffixRoll[] _weaponRolls = [];
    private AffixRoll[] _armorRolls = [];

    public RuntimeSmokeTest(string logPath)
    {
        _logPath = logPath;
        File.WriteAllText(logPath, $"DarkLoot gear smoke; PID={Environment.ProcessId}{Environment.NewLine}");
    }

    public GameInputSnapshot NextInput(
        ArenaSimulation arena,
        InventoryPanel panel,
        GameContent content,
        int width)
    {
        if (++_updates > 1200)
        {
            throw new InvalidOperationException(
                $"Smoke timed out at stage {_stage} after {_draws} draws; " +
                $"crit={arena.Player.CriticalChance}%, damage={arena.Player.AttackDamage}, " +
                $"lastCritical={arena.Player.LastHitWasCritical}.");
        }

        var movement = NumericsVector.Zero;
        var attack = false;
        var pickup = false;
        var toggle = false;
        var restart = false;
        var pointer = NumericsVector.Zero;
        if (_updates == 1)
        {
            _startPosition = arena.Player.Position;
        }

        if (_stage is 0 or 8)
        {
            var delta = arena.Monster.Position - arena.Player.Position;
            if (delta.Length() > 30)
            {
                movement = NumericsVector.Normalize(delta);
            }
            else if (_stage == 0)
            {
                Check(arena.Player.Position != _startPosition, "movement changed player position");
                _stage++;
            }
            else
            {
                _stage++;
            }
        }
        else if (_stage == 1)
        {
            attack = _updates % 2 == 0;
        }
        else if (_stage == 2 && arena.GroundItems.Count > 0)
        {
            var delta = arena.GroundItems[0].Position - arena.Player.Position;
            movement = delta.Length() > 20 ? NumericsVector.Normalize(delta) : NumericsVector.Zero;
            pickup = true;
        }
        else if (_stage == 3)
        {
            toggle = true;
        }
        else if (_stage is 4 or 6)
        {
            var row = panel.RowBounds(width, RowForSlot(arena, content, ItemSlot.Weapon));
            pointer = new NumericsVector(row.Center.X, row.Center.Y);
            attack = true;
        }
        else if (_stage == 5)
        {
            var row = panel.RowBounds(width, RowForSlot(arena, content, ItemSlot.Armor));
            pointer = new NumericsVector(row.Center.X, row.Center.Y);
            attack = true;
        }
        else if (_stage == 7)
        {
            toggle = true;
            restart = true;
        }
        else if (_stage == 9)
        {
            attack = _updates % 2 == 0;
            restart = arena.Monster.Health < arena.Monster.MaxHealth &&
                      !arena.Player.LastHitWasCritical;
        }
        else if (_stage == 10)
        {
            toggle = true;
        }
        else if (_stage == 11)
        {
            movement = NumericsVector.UnitX;
            attack = true;
        }
        else if (_stage == 12)
        {
            toggle = true;
        }

        return new GameInputSnapshot(
            movement,
            arena.Monster.Position,
            pointer,
            attack,
            pickup,
            toggle,
            restart,
            false);
    }

    public void AfterUpdate(ArenaSimulation arena, InventoryPanel panel, GameContent content)
    {
        switch (_stage)
        {
            case 1 when arena.Monster.IsDefeated:
                Check(arena.GroundItems.Count == 2, "slime generated guaranteed weapon and armor loot");
                Check(arena.GroundItems.Any(item => item.Item.DefinitionId == "weapon.training_blade") &&
                      arena.GroundItems.Any(item => item.Item.DefinitionId == "armor.training_vest"),
                    "both independent equipment slots have guaranteed drops");
                _stage = 2;
                break;
            case 2 when arena.GroundItems.Count == 0 && arena.Player.Inventory.Items.Count == 2:
                Check(arena.Player.Inventory.Items.Select(item => content.Items[item.DefinitionId].Slot)
                        .Distinct().Count() == 2,
                    "pickup transferred weapon and armor without replacement");
                _stage = 3;
                break;
            case 3 when panel.IsOpen:
                _healthBeforeEquipping = arena.Player.Health;
                Log("PASS inventory opened comparison panel");
                _stage = 4;
                break;
            case 4:
                {
                    var weapon = arena.Player.Inventory.Items.Single(item =>
                        content.Items[item.DefinitionId].Slot == ItemSlot.Weapon);
                    Check(arena.Player.Inventory.EquippedWeapon?.InstanceId == weapon.InstanceId,
                        "weapon row equipped to weapon slot");
                    Check(arena.Player.AttackDamage >= 15, "weapon and rolled attack stats contribute to total");
                    Check(arena.Player.Health <= _healthBeforeEquipping,
                        "equipping increased maximum health without granting current health");
                    var preview = new InventoryService().GetTotalsWithReplacement(
                        arena.Player.BaseAttackDamage,
                        arena.Player.BaseMaximumHealth,
                        arena.Player.Inventory,
                        content,
                        ItemSlot.Weapon,
                        weapon);
                    Check(arena.Player.Stats == preview, "weapon comparison equals equipped shared totals");
                    _weapon = weapon.InstanceId;
                    _weaponRolls = weapon.Rolls.ToArray();
                    _stage = 5;
                    break;
                }
            case 5:
                {
                    var armor = arena.Player.Inventory.Items.Single(item =>
                        content.Items[item.DefinitionId].Slot == ItemSlot.Armor);
                    Check(arena.Player.Inventory.EquippedArmor?.InstanceId == armor.InstanceId &&
                          arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon,
                        "armor equipped independently while weapon stayed equipped");
                    var preview = new InventoryService().GetTotalsWithReplacement(
                        arena.Player.BaseAttackDamage,
                        arena.Player.BaseMaximumHealth,
                        arena.Player.Inventory,
                        content,
                        ItemSlot.Armor,
                        armor);
                    Check(arena.Player.Stats == preview, "armor comparison equals equipped shared totals");
                    _armor = armor.InstanceId;
                    _armorRolls = armor.Rolls.ToArray();
                    _stage = 6;
                    break;
                }
            case 6:
                {
                    var weapon = arena.Player.Inventory.Items.Single(item => item.InstanceId == _weapon);
                    var armor = arena.Player.Inventory.Items.Single(item => item.InstanceId == _armor);
                    Check(arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon &&
                          arena.Player.Inventory.EquippedArmor?.InstanceId == _armor,
                        "re-equipping weapon retained armor slot");
                    Check(weapon.Rolls.SequenceEqual(_weaponRolls) &&
                          armor.Rolls.SequenceEqual(_armorRolls),
                        "rolled affix values remained fixed through repeated equipment changes");
                    Log($"PASS equipped crit chance={arena.Player.CriticalChance}%");
                    _stage = 7;
                    break;
                }
            case 7 when !panel.IsOpen:
                Check(arena.Monster.Health == arena.Monster.MaxHealth &&
                      arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon &&
                      arena.Player.Inventory.EquippedArmor?.InstanceId == _armor,
                    "R reset restored slime and preserved both equipped slots");
                _stage = 8;
                break;
            case 9 when arena.Player.LastHitWasCritical && arena.Player.LastHitDamage > 0:
                Check(arena.Player.LastHitDamage >= arena.Player.AttackDamage * 2,
                    "successful critical hit exposed doubled applied damage for feedback");
                Log($"PASS critical combat feedback damage={arena.Player.LastHitDamage}");
                _stage = 10;
                break;
            case 10 when panel.IsOpen:
                _pausedPlayerPosition = arena.Player.Position;
                _pausedMonsterPosition = arena.Monster.Position;
                _pausedHealth = arena.Player.Health;
                _stage = 11;
                break;
            case 11:
                if (++_pausedUpdates == 20)
                {
                    Check(panel.IsOpen && arena.Player.Position == _pausedPlayerPosition &&
                          arena.Monster.Position == _pausedMonsterPosition &&
                          arena.Player.Health == _pausedHealth && !arena.Monster.IsDefeated,
                        "inventory paused a live encounter and consumed inputs for 20 updates");
                    _stage = 12;
                }
                break;
            case 12 when !panel.IsOpen:
                Log("PASS inventory closed and resumed live encounter");
                _stage = 13;
                break;
            case 13 when arena.Player.Health < _previousHealth:
                Log($"PASS contact reduced health {_previousHealth} -> {arena.Player.Health}");
                _stage = 14;
                break;
            case 14 when arena.Player.Health > _previousHealth:
                Check(arena.Player.Health == arena.Player.MaximumHealth &&
                      arena.Monster.Health == arena.Monster.MaxHealth &&
                      arena.GroundItems.Count == 0 &&
                      arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon &&
                      arena.Player.Inventory.EquippedArmor?.InstanceId == _armor,
                    "death respawn restored current maximum health and retained both slots");
                _stage = 15;
                break;
        }

        _previousHealth = arena.Player.Health;
    }

    public bool AfterDraw()
    {
        _draws++;
        if (_stage != 15)
        {
            return false;
        }

        Check(_draws > 10, "real graphics update/draw loop rendered multiple frames");
        Log($"PASS gear smoke complete; updates={_updates}; draws={_draws}");
        return true;
    }

    private void Check(bool success, string message)
    {
        if (!success)
        {
            throw new InvalidOperationException($"FAIL {message} (stage {_stage}).");
        }

        Log($"PASS {message}");
    }

    private static int RowForSlot(ArenaSimulation arena, GameContent content, ItemSlot slot)
    {
        for (var index = 0; index < arena.Player.Inventory.Items.Count; index++)
        {
            var item = arena.Player.Inventory.Items[index];
            if (content.Items[item.DefinitionId].Slot == slot)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Smoke inventory has no item for slot {slot}.");
    }

    private void Log(string message) => File.AppendAllText(_logPath, message + Environment.NewLine);
}
