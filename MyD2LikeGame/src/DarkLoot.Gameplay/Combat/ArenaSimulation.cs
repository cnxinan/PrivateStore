using System.Collections.ObjectModel;
using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Loot;

namespace DarkLoot.Gameplay.Combat;

public sealed class ArenaSimulation
{
    private const float ArenaWidth = 960f;
    private const float ArenaHeight = 540f;
    private const float PlayerSpeed = 180f;
    private const int PlayerMaxHealth = 100;
    private const int PlayerBaseAttackDamage = 10;
    private const float MeleeRange = 48f;
    private const float MeleeHalfAngleDegrees = 45f;
    private const float AttackCooldownSeconds = 0.35f;
    private const float MonsterContactRange = 24f;
    private const float MonsterContactCooldownSeconds = 0.8f;
    private const float PickupRange = 36f;
    private const string TrainingMonsterId = "monster.training_slime";
    private static readonly Vector2 PlayerSpawn = new(160f, 270f);
    private static readonly Vector2 MonsterSpawn = new(480f, 270f);

    private readonly GameContent _content;
    private readonly Random _random;
    private readonly InventoryService _inventoryService = new();
    private readonly LootGenerator _lootGenerator = new();
    private readonly LootTableDefinition _lootTable;
    private readonly List<GroundItem> _groundItems = [];
    private readonly ReadOnlyCollection<GroundItem> _readOnlyGroundItems;
    private float _attackCooldown;
    private float _monsterContactCooldown;
    private bool _lootGenerated;

    public ArenaSimulation(GameContent content, Random random)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        if (content.Monsters is null ||
            !content.Monsters.TryGetValue(TrainingMonsterId, out var monsterDefinition) ||
            monsterDefinition is null ||
            !string.Equals(monsterDefinition.Id, TrainingMonsterId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Game content is missing required monster '{TrainingMonsterId}'.",
                nameof(content));
        }

        if (monsterDefinition.MaxHealth <= 0 ||
            monsterDefinition.ContactDamage < 0 ||
            !float.IsFinite(monsterDefinition.MoveSpeed) ||
            monsterDefinition.MoveSpeed <= 0)
        {
            throw new ArgumentException(
                $"Monster '{TrainingMonsterId}' has invalid combat values.",
                nameof(content));
        }

        if (content.LootTables is null ||
            !content.LootTables.TryGetValue(monsterDefinition.LootTableId, out var lootTable) ||
            lootTable is null)
        {
            throw new ArgumentException(
                $"Game content is missing loot table '{monsterDefinition.LootTableId}' " +
                $"for monster '{TrainingMonsterId}'.",
                nameof(content));
        }

        _content = content;
        _random = random;
        _lootTable = lootTable;
        _readOnlyGroundItems = _groundItems.AsReadOnly();
        Player = new PlayerState(
            content,
            _inventoryService,
            PlayerSpawn,
            PlayerMaxHealth,
            PlayerMaxHealth,
            PlayerBaseAttackDamage);
        Monster = new MonsterState(monsterDefinition, MonsterSpawn);
    }

    public PlayerState Player { get; }

    public MonsterState Monster { get; }

    public IReadOnlyList<GroundItem> GroundItems => _readOnlyGroundItems;

    public bool TryEquip(Guid instanceId) => Player.TryEquip(instanceId);

    public void Update(PlayerIntent intent, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "Elapsed time must be finite and non-negative.");
        }

        _attackCooldown = Math.Max(0, _attackCooldown - elapsedSeconds);
        _monsterContactCooldown = Math.Max(0, _monsterContactCooldown - elapsedSeconds);

        MovePlayer(intent.MoveDirection, elapsedSeconds);
        UpdateFacing(intent.AimWorldPosition);
        TryAttack(intent.AttackPressed);

        if (!Monster.IsDefeated)
        {
            MoveMonster(elapsedSeconds);
            TryMonsterContactDamage();
        }

        if (intent.PickupPressed)
        {
            TryPickUpNearestItem();
        }
    }

    public void ResetEncounter()
    {
        ResetMonster();
        _attackCooldown = 0;
        Player.LastHitDamage = 0;
        Player.LastHitWasCritical = false;
    }

    private void MovePlayer(Vector2 direction, float elapsedSeconds)
    {
        var normalized = NormalizeFinite(direction);
        Player.Position = MoveAndClamp(
            Player.Position,
            normalized,
            PlayerSpeed,
            elapsedSeconds);
    }

    private void UpdateFacing(Vector2 aimWorldPosition)
    {
        if (!IsFinite(aimWorldPosition))
        {
            return;
        }

        var direction = aimWorldPosition - Player.Position;
        var normalized = NormalizeFinite(direction);
        if (normalized != Vector2.Zero)
        {
            Player.Facing = normalized;
        }
    }

    private void TryAttack(bool attackPressed)
    {
        if (!attackPressed || Monster.IsDefeated || _attackCooldown > 0)
        {
            return;
        }

        Player.LastHitDamage = 0;
        Player.LastHitWasCritical = false;
        if (!CombatRules.IsInMeleeRange(
                Player.Position,
                Player.Facing,
                Monster.Position,
                MeleeRange,
                MeleeHalfAngleDegrees))
        {
            return;
        }

        _attackCooldown = AttackCooldownSeconds;
        var critical = Player.CriticalChance switch
        {
            0 => false,
            100 => true,
            var chance => _random.Next(100) < chance
        };
        var damage = critical
            ? (int)Math.Min((long)Player.AttackDamage * 2, int.MaxValue)
            : Player.AttackDamage;
        var appliedDamage = Math.Min(damage, Monster.Health);
        Monster.Health -= appliedDamage;
        Player.LastHitDamage = appliedDamage;
        Player.LastHitWasCritical = critical;
        if (Monster.IsDefeated && !_lootGenerated)
        {
            GenerateMonsterLoot();
        }
    }

    private void GenerateMonsterLoot()
    {
        _lootGenerated = true;
        foreach (var item in _lootGenerator.Generate(_lootTable, _content, _random))
        {
            _groundItems.Add(new GroundItem(item, Monster.Position));
        }
    }

    private void MoveMonster(float elapsedSeconds)
    {
        var deltaX = (double)Player.Position.X - Monster.Position.X;
        var deltaY = (double)Player.Position.Y - Monster.Position.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance == 0)
        {
            return;
        }

        var travelDistance = Math.Min((double)Monster.MoveSpeed * elapsedSeconds, distance);
        if (travelDistance >= distance)
        {
            Monster.Position = Player.Position;
            return;
        }

        var direction = NormalizeFinite(new Vector2((float)deltaX, (float)deltaY));
        Monster.Position = MoveAndClamp(
            Monster.Position,
            direction,
            (float)travelDistance,
            1f);
    }

    private void TryMonsterContactDamage()
    {
        if (Monster.ContactDamage <= 0 ||
            _monsterContactCooldown > 0 ||
            Vector2.DistanceSquared(Player.Position, Monster.Position) >
            MonsterContactRange * MonsterContactRange)
        {
            return;
        }

        _monsterContactCooldown = MonsterContactCooldownSeconds;
        Player.Health = Math.Max(0, Player.Health - Monster.ContactDamage);
        if (Player.Health == 0)
        {
            RespawnAfterDeath();
        }
    }

    private void TryPickUpNearestItem()
    {
        var nearestIndex = -1;
        var nearestDistanceSquared = PickupRange * PickupRange;
        for (var index = 0; index < _groundItems.Count; index++)
        {
            var distanceSquared = Vector2.DistanceSquared(
                Player.Position,
                _groundItems[index].Position);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestIndex = index;
            }
        }

        if (nearestIndex < 0)
        {
            return;
        }

        var item = _groundItems[nearestIndex].Item;
        if (_inventoryService.TryPickUp(Player.Inventory, item))
        {
            _groundItems.RemoveAt(nearestIndex);
        }
    }

    private void RespawnAfterDeath()
    {
        Player.Position = PlayerSpawn;
        Player.Health = Player.MaximumHealth;
        Player.LastHitDamage = 0;
        Player.LastHitWasCritical = false;
        Player.Facing = Vector2.UnitX;
        _groundItems.Clear();
        _monsterContactCooldown = 0;
        ResetMonster();
        _attackCooldown = 0;
    }

    private void ResetMonster()
    {
        Monster.Position = MonsterSpawn;
        Monster.Health = Monster.MaxHealth;
        _monsterContactCooldown = 0;
        _lootGenerated = false;
    }

    private static Vector2 MoveAndClamp(
        Vector2 position,
        Vector2 direction,
        float speed,
        float elapsedSeconds)
    {
        var x = position.X + (double)direction.X * speed * elapsedSeconds;
        var y = position.Y + (double)direction.Y * speed * elapsedSeconds;
        return new Vector2(
            (float)Math.Clamp(x, 0d, ArenaWidth),
            (float)Math.Clamp(y, 0d, ArenaHeight));
    }

    private static Vector2 NormalizeFinite(Vector2 vector)
    {
        if (!IsFinite(vector))
        {
            return Vector2.Zero;
        }

        var x = (double)vector.X;
        var y = (double)vector.Y;
        var length = Math.Sqrt(x * x + y * y);
        if (length == 0)
        {
            return Vector2.Zero;
        }

        return new Vector2((float)(x / length), (float)(y / length));
    }

    private static bool IsFinite(Vector2 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y);
    }
}
