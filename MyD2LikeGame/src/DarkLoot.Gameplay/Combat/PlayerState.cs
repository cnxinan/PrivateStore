using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Gameplay.Combat;

public sealed class PlayerState
{
    private readonly GameContent _content;
    private readonly InventoryService _inventoryService;

    internal PlayerState(
        GameContent content,
        InventoryService inventoryService,
        Vector2 position,
        int health,
        int baseAttackDamage)
    {
        _content = content;
        _inventoryService = inventoryService;
        Position = position;
        Health = health;
        BaseAttackDamage = baseAttackDamage;
        Inventory = new Inventory();
    }

    public Vector2 Position { get; internal set; }

    public int Health { get; internal set; }

    public int BaseAttackDamage { get; }

    public int AttackDamage =>
        _inventoryService.GetAttackDamage(BaseAttackDamage, Inventory, _content);

    public Inventory Inventory { get; }

    internal Vector2 Facing { get; set; } = Vector2.UnitX;
}
