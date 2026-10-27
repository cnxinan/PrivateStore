using System;
using System.IO;
using DarkLoot.Engine.Input;
using DarkLoot.Gameplay.Combat;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

// Scripted input goes through the host's normal intent mapping, UI, simulation and drawing.
internal sealed class RuntimeSmokeTest
{
    private readonly string _logPath;
    private int _stage;
    private int _updates;
    private int _draws;
    private int _previousHealth = 100;
    private NumericsVector _startPosition;
    private NumericsVector _pausedPlayerPosition;
    private NumericsVector _pausedMonsterPosition;
    private int _pausedHealth;
    private int _pausedUpdates;
    private Guid _weapon;

    public RuntimeSmokeTest(string logPath)
    {
        _logPath = logPath;
        File.WriteAllText(logPath, $"DarkLoot runtime smoke; PID={Environment.ProcessId}{Environment.NewLine}");
    }

    public GameInputSnapshot NextInput(ArenaSimulation arena, InventoryPanel panel, int width)
    {
        if (++_updates > 1200)
        {
            throw new InvalidOperationException($"Smoke timed out at stage {_stage} after {_draws} draws.");
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

        if (_stage is 0 or 6)
        {
            var delta = arena.Monster.Position - arena.Player.Position;
            if (delta.Length() > 30)
            {
                movement = NumericsVector.Normalize(delta);
            }
            else
            {
                Check(arena.Player.Position != _startPosition, "movement changed player position");
                _stage++;
            }
        }
        else if (_stage is 1 or 7)
        {
            attack = _updates % 2 == 0;
        }
        else if (_stage == 2)
        {
            var delta = arena.GroundItems[0].Position - arena.Player.Position;
            movement = delta.Length() > 20 ? NumericsVector.Normalize(delta) : NumericsVector.Zero;
            pickup = true;
        }
        else if (_stage == 3)
        {
            toggle = true;
        }
        else if (_stage == 4)
        {
            var row = panel.RowBounds(width, 0);
            pointer = new NumericsVector(row.Center.X, row.Center.Y);
            attack = true;
        }
        else if (_stage == 5)
        {
            toggle = true;
            restart = true;
        }
        else if (_stage is 8 or 10)
        {
            toggle = true;
        }
        else if (_stage == 9)
        {
            movement = NumericsVector.UnitX;
            attack = true;
        }
        return new GameInputSnapshot(movement, arena.Monster.Position, pointer,
            attack, pickup, toggle, restart, false);
    }

    public void AfterUpdate(ArenaSimulation arena, InventoryPanel panel)
    {
        switch (_stage)
        {
            case 1 when arena.Monster.IsDefeated:
                Check(arena.GroundItems.Count == 1, "melee killed slime and generated exactly one drop");
                _stage = 2;
                break;
            case 2 when arena.Player.Inventory.Items.Count == 1:
                Check(arena.GroundItems.Count == 0, "pickup input transferred the ground item");
                _stage = 3;
                break;
            case 3 when panel.IsOpen:
                Log("PASS inventory toggle opened panel");
                _stage = 4;
                break;
            case 4:
                Check(panel.IsOpen && arena.Monster.IsDefeated, "inventory click did not attack or restart encounter");
                Check(arena.Player.Inventory.EquippedWeapon is not null && arena.Player.AttackDamage == 15,
                    "row click equipped training blade; attack=15");
                _weapon = arena.Player.Inventory.EquippedWeapon!.InstanceId;
                _stage = 5;
                break;
            case 5:
                Check(!panel.IsOpen && arena.Monster.Health == 20 &&
                    arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon,
                    "R reset restored slime and preserved equipped instance");
                _stage = 6;
                break;
            case 7 when arena.Monster.Health < 20:
                Check(arena.Monster.Health == 5, "repeat fight hit used equipped damage=15 (20 -> 5)");
                _stage = 8;
                break;
            case 8 when panel.IsOpen:
                _pausedPlayerPosition = arena.Player.Position;
                _pausedMonsterPosition = arena.Monster.Position;
                _pausedHealth = arena.Player.Health;
                _stage = 9;
                break;
            case 9:
                if (++_pausedUpdates == 20)
                {
                    Check(panel.IsOpen && arena.Player.Position == _pausedPlayerPosition &&
                        arena.Monster.Position == _pausedMonsterPosition &&
                        arena.Player.Health == _pausedHealth && arena.Monster.Health == 5,
                        "inventory paused a live encounter and consumed movement/attack inputs for 20 updates");
                    _stage = 10;
                }
                break;
            case 10 when !panel.IsOpen:
                Log("PASS inventory closed and resumed live encounter");
                _stage = 11;
                break;
            case 11 when arena.Player.Health < _previousHealth:
                Log($"PASS contact reduced health {_previousHealth} -> {arena.Player.Health}");
                _stage = 12;
                break;
            case 12 when arena.Player.Health > _previousHealth:
                Check(arena.Player.Health == 100 && arena.Monster.Health == 20 &&
                    arena.GroundItems.Count == 0 &&
                    arena.Player.Inventory.EquippedWeapon?.InstanceId == _weapon &&
                    arena.Player.AttackDamage == 15,
                    "death respawn restored arena and preserved equipment");
                _stage = 13;
                break;
        }
        _previousHealth = arena.Player.Health;
    }

    public bool AfterDraw()
    {
        _draws++;
        if (_stage != 13)
        {
            return false;
        }
        Check(_draws > 10, "real graphics update/draw loop rendered multiple frames");
        Log($"PASS runtime smoke complete; updates={_updates}; draws={_draws}");
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

    private void Log(string message) => File.AppendAllText(_logPath, message + Environment.NewLine);
}
