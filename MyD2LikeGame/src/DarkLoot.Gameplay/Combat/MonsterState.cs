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
    }

    public string InstanceId { get; }

    public string Id { get; }

    public Vector2 Position { get; internal set; }

    public int Health { get; internal set; }

    public int MaxHealth { get; }

    public int ContactDamage { get; }

    public float MoveSpeed { get; }

    public MonsterBehavior Behavior { get; }

    public bool IsDefeated => Health <= 0;

    internal MonsterDefinition Definition { get; }

    internal Vector2 SpawnPosition { get; }

    internal bool DeathResolved { get; set; }
}
