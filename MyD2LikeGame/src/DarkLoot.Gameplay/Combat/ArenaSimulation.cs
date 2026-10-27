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
    private const int PlayerMaxHealth = 100;
    private const int PlayerBaseAttackDamage = 10;
    private const float MeleeRange = 48f;
    private const float MeleeHalfAngleDegrees = 45f;
    private const float AttackCooldownSeconds = 0.35f;
    private const float MonsterContactRange = 24f;
    private const float MonsterContactCooldownSeconds = 0.8f;
    private const float PickupRange = 36f;
    private const string TrainingEncounterId = "encounter.training_arena";
    private static readonly Vector2 PlayerSpawn = new(160f, 270f);

    private readonly GameContent _content;
    private readonly Random _random;
    private readonly InventoryService _inventoryService = new();
    private readonly LootGenerator _lootGenerator = new();
    private readonly List<MonsterState> _monsters = [];
    private readonly ReadOnlyCollection<MonsterState> _readOnlyMonsters;
    private readonly List<MeleeHit> _lastSwingHits = [];
    private readonly ReadOnlyCollection<MeleeHit> _readOnlyLastSwingHits;
    private readonly List<GroundItem> _groundItems = [];
    private readonly ReadOnlyCollection<GroundItem> _readOnlyGroundItems;
    private readonly MonsterBehaviorSystem _monsterBehaviorSystem = new();
    private readonly ProjectileSystem _projectileSystem = new();
    private float _attackCooldown;
    private float _monsterContactCooldown;

    public ArenaSimulation(GameContent content, Random random)
        : this(content, random, RequireTrainingEncounter(content))
    {
    }

    public ArenaSimulation(GameContent content, Random random, EncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(encounter);
        if (string.IsNullOrWhiteSpace(encounter.Id) ||
            encounter.Spawns is null ||
            encounter.Spawns.Count == 0)
        {
            throw new ArgumentException("Encounter must have an ID and at least one spawn.", nameof(encounter));
        }

        _content = content;
        _random = random;
        _readOnlyMonsters = _monsters.AsReadOnly();
        _readOnlyLastSwingHits = _lastSwingHits.AsReadOnly();
        _readOnlyGroundItems = _groundItems.AsReadOnly();
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spawn in encounter.Spawns)
        {
            if (spawn is null ||
                string.IsNullOrWhiteSpace(spawn.InstanceId) ||
                string.IsNullOrWhiteSpace(spawn.MonsterId) ||
                !float.IsFinite(spawn.X) || spawn.X is < 0 or > ArenaWidth ||
                !float.IsFinite(spawn.Y) || spawn.Y is < 0 or > ArenaHeight ||
                !instanceIds.Add(spawn.InstanceId))
            {
                throw new ArgumentException(
                    $"Encounter '{encounter.Id}' contains an invalid or duplicate spawn.",
                    nameof(encounter));
            }

            if (content.Monsters is null ||
                !content.Monsters.TryGetValue(spawn.MonsterId, out var definition) ||
                definition is null ||
                !string.Equals(definition.Id, spawn.MonsterId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Encounter '{encounter.Id}' spawn '{spawn.InstanceId}' references missing monster " +
                    $"'{spawn.MonsterId}'.",
                    nameof(content));
            }

            if (!IsValidMonster(definition))
            {
                throw new ArgumentException(
                    $"Monster '{spawn.MonsterId}' has invalid combat values or behavior.",
                    nameof(content));
            }

            if (content.LootTables is null ||
                !content.LootTables.TryGetValue(definition.LootTableId, out var lootTable) ||
                lootTable is null)
            {
                throw new ArgumentException(
                    $"Game content is missing loot table '{definition.LootTableId}' for monster " +
                    $"'{spawn.MonsterId}'.",
                    nameof(content));
            }

            _monsters.Add(new MonsterState(spawn, definition));
        }

        Player = new PlayerState(
            content,
            _inventoryService,
            PlayerSpawn,
            PlayerMaxHealth,
            PlayerMaxHealth,
            PlayerBaseAttackDamage);
    }

    public PlayerState Player { get; }

    public IReadOnlyList<MonsterState> Monsters => _readOnlyMonsters;

    public IReadOnlyList<MeleeHit> LastSwingHits => _readOnlyLastSwingHits;

    public int RemainingMonsters => _monsters.Count(monster => !monster.IsDefeated);

    public bool IsCleared => RemainingMonsters == 0;

    public IReadOnlyList<GroundItem> GroundItems => _readOnlyGroundItems;

    public IReadOnlyList<ProjectileState> Projectiles => _projectileSystem.Projectiles;

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

        if (_projectileSystem.Update(elapsedSeconds, Player.Position, ApplyProjectileDamage))
        {
            return;
        }

        foreach (var monster in _monsters)
        {
            _monsterBehaviorSystem.Update(
                monster,
                Player.Position,
                elapsedSeconds,
                _projectileSystem.Spawn);
        }

        if (TryMonsterContactDamage())
        {
            return;
        }

        if (intent.PickupPressed)
        {
            TryPickUpNearestItem();
        }
    }

    public void ResetEncounter()
    {
        ResetMonsters();
        _attackCooldown = 0;
    }

    private void MovePlayer(Vector2 direction, float elapsedSeconds)
    {
        var normalized = NormalizeFinite(direction);
        Player.Position = MoveAndClamp(
            Player.Position,
            normalized,
            180f,
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
        if (!attackPressed || IsCleared || _attackCooldown > 0)
        {
            return;
        }

        _lastSwingHits.Clear();
        Player.LastHitDamage = 0;
        Player.LastHitWasCritical = false;
        Player.AttackSequence++;
        _attackCooldown = AttackCooldownSeconds;

        foreach (var monster in _monsters)
        {
            if (monster.IsDefeated ||
                !CombatRules.IsInMeleeRange(
                    Player.Position,
                    Player.Facing,
                    monster.Position,
                    MeleeRange,
                    MeleeHalfAngleDegrees))
            {
                continue;
            }

            var critical = Player.CriticalChance switch
            {
                0 => false,
                100 => true,
                var chance => _random.Next(100) < chance
            };
            var damage = critical
                ? (int)Math.Min((long)Player.AttackDamage * 2, int.MaxValue)
                : Player.AttackDamage;
            var appliedDamage = Math.Min(damage, monster.Health);
            monster.Health -= appliedDamage;
            _lastSwingHits.Add(new MeleeHit(monster.InstanceId, appliedDamage, critical));
            if (_lastSwingHits.Count == 1)
            {
                Player.LastHitDamage = appliedDamage;
                Player.LastHitWasCritical = critical;
            }

            if (monster.IsDefeated && !monster.DeathResolved)
            {
                monster.DeathResolved = true;
                monster.CancelWindup();
                GenerateMonsterLoot(monster);
            }
        }
    }

    private void GenerateMonsterLoot(MonsterState monster)
    {
        var lootTable = _content.LootTables[monster.Definition.LootTableId];
        foreach (var item in _lootGenerator.Generate(lootTable, _content, _random))
        {
            _groundItems.Add(new GroundItem(item, monster.Position));
        }
    }

    private bool TryMonsterContactDamage()
    {
        if (_monsterContactCooldown > 0)
        {
            return false;
        }

        foreach (var monster in _monsters)
        {
            if (monster.IsDefeated ||
                monster.ContactDamage <= 0 ||
                Vector2.DistanceSquared(Player.Position, monster.Position) >
                MonsterContactRange * MonsterContactRange)
            {
                continue;
            }

            _monsterContactCooldown = MonsterContactCooldownSeconds;
            Player.Health = Math.Max(0, Player.Health - monster.ContactDamage);
            if (Player.Health == 0)
            {
                RespawnAfterDeath();
                return true;
            }

            return false;
        }

        return false;
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
        Player.Facing = Vector2.UnitX;
        _groundItems.Clear();
        ResetMonsters();
        _attackCooldown = 0;
    }

    private bool ApplyProjectileDamage(int damage)
    {
        Player.Health = Math.Max(0, Player.Health - damage);
        if (Player.Health == 0)
        {
            RespawnAfterDeath();
            return true;
        }

        return false;
    }

    private void ResetMonsters()
    {
        _projectileSystem.Clear();
        foreach (var monster in _monsters)
        {
            monster.Position = monster.SpawnPosition;
            monster.Health = monster.MaxHealth;
            monster.DeathResolved = false;
            monster.ResetBehaviorTimers();
        }

        _monsterContactCooldown = 0;
        ClearSwingFeedback();
    }

    private void ClearSwingFeedback()
    {
        _lastSwingHits.Clear();
        Player.LastHitDamage = 0;
        Player.LastHitWasCritical = false;
    }

    private static EncounterDefinition RequireTrainingEncounter(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Encounters.TryGetValue(TrainingEncounterId, out var encounter) &&
            encounter is not null)
        {
            return encounter;
        }

        throw new ArgumentException(
            $"Game content is missing required encounter '{TrainingEncounterId}'.",
            nameof(content));
    }

    private static bool IsValidMonster(MonsterDefinition definition)
    {
        if (definition.MaxHealth <= 0 ||
            definition.ContactDamage < 0 ||
            !float.IsFinite(definition.MoveSpeed) ||
            definition.MoveSpeed <= 0 ||
            !Enum.IsDefined(definition.Behavior))
        {
            return false;
        }

        return definition.Behavior switch
        {
            MonsterBehavior.Chase => definition.RangedAttack is null,
            MonsterBehavior.Archer =>
                definition.RangedAttack is { Range: > 0 } attack &&
                float.IsFinite(attack.Range) &&
                float.IsFinite(attack.ProjectileSpeed) &&
                attack.ProjectileSpeed > 0 &&
                float.IsFinite(attack.FireInterval) &&
                attack.FireInterval > 0 &&
                float.IsFinite(attack.WindupSeconds) &&
                attack.WindupSeconds > 0 &&
                attack.WindupSeconds <= attack.FireInterval &&
                attack.Damage >= 0,
            _ => false
        };
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

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y);
}
