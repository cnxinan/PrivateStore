using System;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

public readonly struct UiLayout
{
    public UiLayout(int width, int height = 720)
    {
        if (width < 1000 || height < 700)
            throw new ArgumentOutOfRangeException(nameof(width), "The interface requires at least 1000 by 700 pixels.");
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public Rectangle Console => new(0, Height - 120, Width, 120);
    public Rectangle Inventory => new(Width - 448, 20, 432, Height - 162);
    public Rectangle Blacksmith => new(16, 20, 360, Height - 162);
    public Rectangle InventoryClose => new(Inventory.Right - 32, Inventory.Y + 10, 22, 22);
    public Rectangle BlacksmithClose => new(Blacksmith.Right - 32, Blacksmith.Y + 10, 22, 22);
    public Rectangle EquipmentTab => new(Inventory.X + 24, Inventory.Y + 200, 180, 28);
    public Rectangle ResourceTab => new(Inventory.X + 220, Inventory.Y + 200, 188, 28);
    public Rectangle Grid => new(Inventory.X + 24, Inventory.Y + 244, 384, 192);
    public Rectangle WeaponSlot => new(Inventory.X + 86, Inventory.Y + 64, 68, 76);
    public Rectangle ArmorSlot => new(Inventory.X + 278, Inventory.Y + 64, 68, 76);
    public Rectangle InventoryTotals => new(Inventory.X + 20, Inventory.Y + 166, 392, 24);
    public Rectangle PreviousPage => new(Inventory.X + 64, Inventory.Y + 454, 40, 28);
    public Rectangle NextPage => new(Inventory.Right - 104, Inventory.Y + 454, 40, 28);
    public Rectangle PageLabel => new(Inventory.X + 112, Inventory.Y + 454, 208, 28);
    public Rectangle InventoryHint => new(Inventory.X + 18, Inventory.Y + 494, 396, 44);
    public Rectangle RecipeTitle => new(Blacksmith.X + 18, Blacksmith.Y + 252, 324, 27);
    public Rectangle RecipeHelp => new(Blacksmith.X + 18, Blacksmith.Y + 282, 324, 24);
    public Rectangle LearnButton => new(Blacksmith.X + 32, Blacksmith.Y + 382, 130, 34);
    public Rectangle CraftButton => new(Blacksmith.X + 182, Blacksmith.Y + 382, 130, 34);
    public Rectangle SmithFeedback => new(Blacksmith.X + 18, Blacksmith.Y + 432, 324, 98);
    public Rectangle Globe => new(24, Height - 140, 132, 132);
    public Rectangle InventoryButton => new(Width - 372, Height - 104, 108, 36);
    public Rectangle SmithButton => new(Width - 256, Height - 104, 108, 36);
    public Rectangle RestartButton => new(Width - 140, Height - 104, 108, 36);
    public Rectangle Objective => new(178, Height - 103, Width - 566, 28);
    public Rectangle HudStats => new(178, Height - 66, Width - 218, 24);
    public Rectangle HudHelp => new(178, Height - 32, Width - 322, 20);
    public Rectangle WorldStatus => new(Blacksmith.Right + 8, 10, Inventory.Left - Blacksmith.Right - 16, 50);

    public Rectangle Cell(int visibleIndex)
    {
        if (visibleIndex is < 0 or >= 32)
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        return new Rectangle(Grid.X + visibleIndex % 8 * 48, Grid.Y + visibleIndex / 8 * 48, 48, 48);
    }

    public Rectangle RecipeRow(int index)
    {
        if (index is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(Blacksmith.X + 18, Blacksmith.Y + 64 + index * 44, 324, 42);
    }

    public Rectangle MaterialCost(int index) =>
        new(Blacksmith.X + 22, Blacksmith.Y + 312 + index * 22, 316, 20);
}
