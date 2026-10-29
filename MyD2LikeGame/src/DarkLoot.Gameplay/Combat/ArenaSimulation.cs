using System.Collections.ObjectModel;
using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Loot;
using DarkLoot.Gameplay.Resources;

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
    private const float PlayerHitstunSeconds = .15f;
    private const float PlayerKnockbackDistance = 18f;
    private const float PlayerHitImmunitySeconds = .5f;
    private const float PlayerDeathSeconds = .6f;
    private const float PlayerRespawnImmunitySeconds = .8f;
    private const float MonsterHitstunSeconds = .12f;
    private const float MonsterKnockbackDistance = 10f;
    private const float TimerEpsilon = .000001f;
    private const float PickupRange = 36f;
    private const string TrainingEncounterId = "encounter.training_arena";
    private static readonly Vector2 PlayerSpawn = new(160f, 270f);

    private readonly Random _random;
    private readonly ItemStatCalculator _itemStatCalculator = new();
    private readonly KillRewardService _killRewards;
    private readonly List<MonsterState> _monsters = [];
    private readonly ReadOnlyCollection<MonsterState> _readOnlyMonsters;
    private readonly List<MeleeHit> _lastSwingHits = [];
    private readonly ReadOnlyCollection<MeleeHit> _readOnlyLastSwingHits;
    private readonly List<GroundResource> _groundResources = [];
    private readonly ReadOnlyCollection<GroundResource> _readOnlyGroundResources;
    private readonly MonsterBehaviorSystem _monsterBehaviorSystem = new();
    private readonly ProjectileSystem _projectileSystem = new();
    private readonly List<CombatEvent> _events = [];
    private readonly ReadOnlyCollection<CombatEvent> _readOnlyEvents;
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

        EquipmentOperations = new InventoryService(content);
        _random = random;
        _killRewards = new KillRewardService(content, random);
        _killRewards.BeginRound(0);
        ResourceOperations = new ResourcePickupService(content);
        Blacksmith = new BlacksmithState();
        BlacksmithOperations = new BlacksmithService(content, random, EquipmentOperations, Blacksmith);
        _readOnlyMonsters = _monsters.AsReadOnly();
        _readOnlyLastSwingHits = _lastSwingHits.AsReadOnly();
        _readOnlyGroundResources = _groundResources.AsReadOnly();
        _readOnlyEvents = _events.AsReadOnly();
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spawn in encounter.Spawns)
        {
            if (spawn is null ||
                string.IsNullOrWhiteSpace(spawn.InstanceId) ||
                spawn.InstanceId == "player" ||
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

            if (content.ResourceLootTables is null ||
                !content.ResourceLootTables.TryGetValue(definition.LootTableId, out var lootTable) ||
                lootTable is null)
            {
                throw new ArgumentException(
                    $"Game content is missing resource loot table '{definition.LootTableId}' for monster " +
                    $"'{spawn.MonsterId}'.",
                    nameof(content));
            }

            _monsters.Add(new MonsterState(spawn, definition));
        }

        Player = new PlayerState(
            content,
            _itemStatCalculator,
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

    public long ResetSequence { get; private set; }

    public IReadOnlyList<GroundResource> GroundResources => _readOnlyGroundResources;

    public IReadOnlyList<ProjectileState> Projectiles => _projectileSystem.Projectiles;

    public IReadOnlyList<CombatEvent> Events => _readOnlyEvents;

    public Vector2 SwingDirection { get; private set; } = Vector2.UnitX;

    public InventoryService EquipmentOperations { get; }

    public ResourcePickupService ResourceOperations { get; }

    public BlacksmithState Blacksmith { get; }

    public BlacksmithService BlacksmithOperations { get; }

    internal bool HasStarted { get; private set; }

    public void Update(PlayerIntent intent, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "Elapsed time must be finite and non-negative.");
        }

        HasStarted = true;
        _events.Clear();
        if (Player.IsDead)
        {
            Player.DeathRemaining = Math.Max(0, Player.DeathRemaining - elapsedSeconds);
            if (Player.DeathRemaining <= TimerEpsilon)
            {
                CompleteRespawn();
                return;
            }
        }

        _attackCooldown = Math.Max(0, _attackCooldown - elapsedSeconds);
        _monsterContactCooldown = Math.Max(0, _monsterContactCooldown - elapsedSeconds);
        var immunityRemaining = Player.InvulnerabilityRemaining - elapsedSeconds;
        Player.InvulnerabilityRemaining = immunityRemaining > TimerEpsilon ? immunityRemaining : 0;

        var playerActionSeconds = Math.Max(0, elapsedSeconds - Player.Reaction.RemainingSeconds);
        Player.Position = ApplyDisplacement(Player.Position, Player.Reaction.Advance(elapsedSeconds));
        // Only reactions that existed at frame start consume this frame's time.
        var monsterFrames = _monsters.Select(monster =>
        {
            var stunnedSeconds = Math.Min(elapsedSeconds, monster.Reaction.RemainingSeconds);
            monster.Position = ApplyDisplacement(monster.Position, monster.Reaction.Advance(elapsedSeconds));
            if (monster.HasLaunched)
            {
                monster.RemainingUntilNextLaunch = Math.Max(
                    0, monster.RemainingUntilNextLaunch - stunnedSeconds);
            }

            return (Monster: monster, ActionSeconds: Math.Max(0, elapsedSeconds - stunnedSeconds));
        }).ToArray();

        if (!Player.IsDead)
        {
            MovePlayer(intent.MoveDirection, playerActionSeconds);
            UpdateFacing(intent.AimWorldPosition);
            TryAttack(intent.AttackPressed && !Player.Reaction.IsActive);
        }

        if (_projectileSystem.Update(elapsedSeconds, Player.Position, !Player.IsDead, ApplyProjectileDamage))
        {
            return;
        }

        foreach (var (monster, actionSeconds) in monsterFrames)
        {
            _monsterBehaviorSystem.Update(
                monster,
                Player.Position,
                actionSeconds,
                LaunchProjectile);
        }

        if (Player.IsDead || TryMonsterContactDamage())
        {
            return;
        }

        if (intent.PickupPressed && !Player.Reaction.IsActive)
        {
            TryPickUpNearestResource();
        }
    }

    public void ResetEncounter()
    {
        if (Player.IsDead)
        {
            return;
        }

        HasStarted = true;
        ResetSequence = checked(ResetSequence + 1);
        _killRewards.BeginRound(ResetSequence);
        _events.Clear();
        Player.DeathRemaining = 0;
        Player.Reaction.Clear();
        Player.InvulnerabilityRemaining = 0;
        SwingDirection = Vector2.UnitX;
        _groundResources.Clear();
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
        SwingDirection = Player.Facing;
        _events.Add(new CombatEvent(
            CombatEventKind.Swing, "player", Player.Position, SwingDirection, 0, false));

        var targets = _monsters.Where(monster =>
            !monster.IsDefeated &&
                CombatRules.IsInMeleeRange(
                    Player.Position,
                    SwingDirection,
                    monster.Position,
                    MeleeRange,
                    MeleeHalfAngleDegrees)).ToArray();
        foreach (var monster in targets)
        {
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

            if (appliedDamage <= 0)
            {
                continue;
            }

            var direction = HitReactionState.NormalizeDirection(monster.Position - Player.Position);
            _events.Add(new CombatEvent(
                CombatEventKind.Damaged, monster.InstanceId, monster.Position, direction, appliedDamage, critical));
            monster.CancelWindup();
            if (monster.IsDefeated && !monster.DeathResolved)
            {
                var rewards = ResolveMonsterRewards(monster);
                monster.DeathResolved = true;
                monster.Reaction.Clear();
                _events.Add(new CombatEvent(
                    CombatEventKind.Died, monster.InstanceId, monster.Position, direction, appliedDamage, critical));
                foreach (var resource in rewards.Resources)
                {
                    _groundResources.Add(new GroundResource(
                        Guid.NewGuid(), resource, monster.Position));
                }
            }
            else if (!monster.IsDefeated)
            {
                monster.Reaction.Begin(direction, MonsterKnockbackDistance, MonsterHitstunSeconds);
            }
        }
    }

    private KillRewardResult ResolveMonsterRewards(MonsterState monster)
    {
        var kill = new KillRecord(
            ResetSequence, monster.InstanceId, monster.Position, monster.Definition.LootTableId);
        var result = _killRewards.Resolve(kill);
        if (result.Status != KillRewardStatus.Granted)
        {
            throw new InvalidOperationException($"Fresh monster kill was rejected: {result.Status}.");
        }

        return result;
    }

    private bool TryMonsterContactDamage()
    {
        if (_monsterContactCooldown > 0)
        {
            return false;
        }

        foreach (var monster in _monsters)
        {
            if (monster.IsDefeated || monster.Reaction.IsActive ||
                monster.ContactDamage <= 0 ||
                Vector2.DistanceSquared(Player.Position, monster.Position) >
                MonsterContactRange * MonsterContactRange)
            {
                continue;
            }

            var direction = HitReactionState.NormalizeDirection(Player.Position - monster.Position);
            var appliedDamage = ApplyPlayerDamage(monster.ContactDamage, direction, out var died);
            if (appliedDamage <= 0)
            {
                continue;
            }

            _monsterContactCooldown = MonsterContactCooldownSeconds;
            monster.Facing = direction;
            _events.Add(new CombatEvent(
                CombatEventKind.ContactAttack, monster.InstanceId, monster.Position, direction, appliedDamage, false));
            if (died)
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private void TryPickUpNearestResource()
    {
        var nearestIndex = -1;
        var nearestDistanceSquared = PickupRange * PickupRange;
        for (var index = 0; index < _groundResources.Count; index++)
        {
            var distanceSquared = Vector2.DistanceSquared(
                Player.Position,
                _groundResources[index].Position);
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

        var drop = _groundResources[nearestIndex];
        switch (ResourceOperations.PickUp(Player, drop))
        {
            case ResourcePickupResult.Applied:
                _groundResources.RemoveAt(nearestIndex);
                break;
            case ResourcePickupResult.PlayerDead:
            case ResourcePickupResult.AlreadyCollected:
                break;
            default:
                throw new InvalidOperationException("Unexpected resource pickup result.");
        }
    }

    private void EnterDeath()
    {
        Player.DeathRemaining = PlayerDeathSeconds;
        Player.Reaction.Clear();
        Player.InvulnerabilityRemaining = 0;
    }

    private void CompleteRespawn()
    {
        Player.Position = PlayerSpawn;
        Player.Health = Player.MaximumHealth;
        Player.Facing = Vector2.UnitX;
        ResetEncounter();
        Player.InvulnerabilityRemaining = PlayerRespawnImmunitySeconds;
    }

    private bool ApplyProjectileDamage(ProjectileState projectile)
    {
        ApplyPlayerDamage(projectile.Damage, projectile.Direction, out var died);
        return died;
    }

    private int ApplyPlayerDamage(int damage, Vector2 direction, out bool died)
    {
        died = false;
        if (damage <= 0 || Player.Health <= 0 || Player.IsInvulnerable)
        {
            return 0;
        }

        var appliedDamage = Math.Min(damage, Player.Health);
        direction = HitReactionState.NormalizeDirection(direction);
        Player.Health -= appliedDamage;
        _events.Add(new CombatEvent(
            CombatEventKind.Damaged, "player", Player.Position, direction, appliedDamage, false));
        died = Player.Health == 0;
        if (died)
        {
            EnterDeath();
            _events.Add(new CombatEvent(
                CombatEventKind.Died, "player", Player.Position, direction, appliedDamage, false));
        }
        else
        {
            Player.Reaction.Begin(direction, PlayerKnockbackDistance, PlayerHitstunSeconds);
            Player.InvulnerabilityRemaining = PlayerHitImmunitySeconds;
        }

        return appliedDamage;
    }

    private void LaunchProjectile(ProjectileState projectile)
    {
        _projectileSystem.Spawn(projectile);
        _events.Add(new CombatEvent(
            CombatEventKind.ArrowLaunched, projectile.SourceInstanceId, projectile.Position,
            projectile.Direction, projectile.Damage, false));
    }

    private void ResetMonsters()
    {
        _projectileSystem.Clear();
        foreach (var monster in _monsters)
        {
            monster.Position = monster.SpawnPosition;
            monster.Health = monster.MaxHealth;
            monster.DeathResolved = false;
            monster.Reaction.Clear();
            monster.Facing = Vector2.UnitX;
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

    private static Vector2 ApplyDisplacement(Vector2 position, Vector2 delta) =>
        MoveAndClamp(position, delta, 1, 1);

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
