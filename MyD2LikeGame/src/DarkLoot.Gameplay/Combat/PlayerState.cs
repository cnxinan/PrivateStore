using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Combat;

public sealed class PlayerState
{
    private readonly GameContent _content;
    private readonly ItemStatCalculator _statCalculator;

    internal PlayerState(
        GameContent content,
        ItemStatCalculator statCalculator,
        Vector2 position,
        int health,
        int baseMaximumHealth,
        int baseAttackDamage)
    {
        _content = content;
        _statCalculator = statCalculator;
        Position = position;
        Health = health;
        BaseMaximumHealth = baseMaximumHealth;
        BaseAttackDamage = baseAttackDamage;
        Inventory = new Inventory();
        Resources = new ResourceBag();
    }

    public Vector2 Position { get; internal set; }

    public int Health { get; internal set; }

    public bool IsDead => Health == 0;

    public float DeathRemaining { get; internal set; }

    public HitReactionState Reaction { get; } = new();

    public float InvulnerabilityRemaining { get; internal set; }

    public bool IsInvulnerable => InvulnerabilityRemaining > 0;

    public int BaseAttackDamage { get; }

    public int BaseMaximumHealth { get; }

    public PlayerStatTotals Stats =>
        _statCalculator.Calculate(
            BaseAttackDamage, BaseMaximumHealth,
            Inventory.EquippedWeapon, Inventory.EquippedArmor, _content);

    public int AttackDamage => Stats.AttackDamage;

    public int MaximumHealth => Stats.MaximumHealth;

    public int CriticalChance => Stats.CriticalChance;

    public int LastHitDamage { get; internal set; }

    public bool LastHitWasCritical { get; internal set; }

    public long AttackSequence { get; internal set; }

    public Inventory Inventory { get; }

    public ResourceBag Resources { get; }

    public Vector2 Facing { get; internal set; } = Vector2.UnitX;
}
