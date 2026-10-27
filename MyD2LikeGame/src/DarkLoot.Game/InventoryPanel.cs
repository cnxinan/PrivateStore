using System;
using DarkLoot.Data;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class InventoryPanel
{
    private const int VisibleRows = 10;
    private readonly InventoryService _service = new();
    private int _firstRow;

    public bool IsOpen { get; private set; }

    public Rectangle Bounds(int width) => new(width - 440, 104, 420, 510);

    public Rectangle RowBounds(int width, int visibleIndex) =>
        new(width - 428, 188 + visibleIndex * 38, 396, 34);

    public void Update(GameInputSnapshot input, Inventory inventory, GameContent content, int width)
    {
        if (input.InventoryTogglePressed)
        {
            IsOpen = !IsOpen;
        }
        if (!IsOpen)
        {
            return;
        }
        _firstRow = Math.Clamp(_firstRow - Math.Sign(input.ScrollDelta), 0, Math.Max(0, inventory.Items.Count - VisibleRows));
        if (!input.AttackPressed)
        {
            return;
        }
        for (var row = 0; row < VisibleRows && row + _firstRow < inventory.Items.Count; row++)
        {
            if (RowBounds(width, row).Contains(input.PointerScreenPosition.X, input.PointerScreenPosition.Y))
            {
                _service.TryEquip(inventory, inventory.Items[row + _firstRow].InstanceId, content);
                break;
            }
        }
    }

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font, Inventory inventory, GameContent content, int width)
    {
        if (!IsOpen)
        {
            return;
        }
        var bounds = Bounds(width);
        shapes.Rectangle(batch, bounds, new Color(28, 33, 48));
        batch.DrawString(font, "INVENTORY - click a weapon to equip", new Vector2(bounds.X + 12, 118), Color.White);
        batch.DrawString(font, "Paused. I to close. Wheel to scroll.", new Vector2(bounds.X + 12, 149), Color.LightCyan);
        if (inventory.Items.Count == 0)
        {
            batch.DrawString(font, "Empty - defeat a slime and press E.", new Vector2(bounds.X + 12, 194), Color.Gray);
        }
        for (var row = 0; row < VisibleRows && row + _firstRow < inventory.Items.Count; row++)
        {
            var item = inventory.Items[row + _firstRow];
            var definition = content.Items[item.DefinitionId];
            var equipped = inventory.EquippedWeapon?.InstanceId == item.InstanceId;
            var rowBounds = RowBounds(width, row);
            shapes.Rectangle(batch, rowBounds, equipped ? new Color(53, 84, 66) : new Color(43, 49, 65));
            batch.DrawString(font, $"{definition.Name} +{definition.AttackBonus}" + (equipped ? " [equipped]" : ""),
                new Vector2(rowBounds.X + 8, rowBounds.Y + 6), equipped ? Color.Gold : Color.White);
        }
    }
}
