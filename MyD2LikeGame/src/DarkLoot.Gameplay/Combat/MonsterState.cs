using System.Numerics;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Combat;

public sealed class MonsterState
{
    internal MonsterState(MonsterDefinition definition, Vector2 position)
    {
        Id = definition.Id;
        MaxHealth = definition.MaxHealth;
        ContactDamage = definition.ContactDamage;
        MoveSpeed = definition.MoveSpeed;
        Position = position;
        Health = definition.MaxHealth;
    }

    public string Id { get; }

    public Vector2 Position { get; internal set; }

    public int Health { get; internal set; }

    public int MaxHealth { get; }

    public int ContactDamage { get; }

    public float MoveSpeed { get; }

    public bool IsDefeated => Health <= 0;
}
