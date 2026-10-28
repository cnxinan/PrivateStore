using System.Numerics;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Combat;

public sealed class MonsterState
{
    internal MonsterState(MonsterSpawnDefinition spawn, MonsterDefinition definition)
    {
        InstanceId = spawn.InstanceId;
        Id = definition.Id;
        Definition = definition;
        SpawnPosition = new Vector2(spawn.X, spawn.Y);
        MaxHealth = definition.MaxHealth;
        ContactDamage = definition.ContactDamage;
        MoveSpeed = definition.MoveSpeed;
        Behavior = definition.Behavior;
        Position = SpawnPosition;
        Health = definition.MaxHealth;
        LockedAimDirection = Vector2.UnitX;
    }

    public string InstanceId { get; }

    public string Id { get; }

    public Vector2 Position { get; internal set; }

    public int Health { get; internal set; }

    public HitReactionState Reaction { get; } = new();

    public Vector2 Facing { get; internal set; } = Vector2.UnitX;

    public int MaxHealth { get; }

    public int ContactDamage { get; }

    public float MoveSpeed { get; }

    public MonsterBehavior Behavior { get; }

    public bool IsDefeated => Health <= 0;

    public bool IsWindingUp { get; internal set; }

    public float WindupRemaining { get; internal set; }

    public Vector2 LockedAimDirection { get; internal set; }

    internal MonsterDefinition Definition { get; }

    internal Vector2 SpawnPosition { get; }

    internal bool DeathResolved { get; set; }

    internal float RemainingUntilNextLaunch { get; set; }

    internal bool HasLaunched { get; set; }

    internal bool WasInRangedAttackRange { get; set; }

    internal void CancelWindup()
    {
        IsWindingUp = false;
        WindupRemaining = 0;
        LockedAimDirection = Vector2.UnitX;
    }

    internal void ResetBehaviorTimers()
    {
        CancelWindup();
        RemainingUntilNextLaunch = 0;
        HasLaunched = false;
        WasInRangedAttackRange = false;
    }
}
