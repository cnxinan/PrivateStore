using System;
using System.Collections.Generic;
using System.Linq;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed record TooltipLine(string Text, Color Color);

public sealed class ItemTooltip
{
    private ItemTooltip(IEnumerable<TooltipLine> lines) => Lines = Array.AsReadOnly(lines.ToArray());

    public IReadOnlyList<TooltipLine> Lines { get; }

    public static ItemTooltip ForEquipment(PlayerState player, ItemInstance item, GameContent content)
    {
        var definition = content.Items[item.DefinitionId];
        var preview = new ItemStatCalculator().Preview(player.BaseAttackDamage, player.BaseMaximumHealth,
            player.Inventory, content, definition.Slot, item);
        var current = player.Stats;
        var equipped = player.Inventory.EquippedWeaponId == item.InstanceId ||
            player.Inventory.EquippedArmorId == item.InstanceId;
        var lines = new List<TooltipLine>
        {
            new(ItemNameFormatter.GetDisplayName(item, definition, content), InventoryPanel.RarityColor(item.Rarity)),
            new($"{GameText.Rarity(item.Rarity)} · {GameText.Slot(definition.Slot)}", GothicUiRenderer.Muted),
            new(string.Format(GameText.BaseAttack, definition.AttackBonus), GothicUiRenderer.Text)
        };
        if (item.Rolls.Count == 0)
            lines.Add(new(GameText.NoAffixes, GothicUiRenderer.Muted));
        foreach (var roll in item.Rolls)
            lines.Add(new($"{content.Affixes[roll.AffixId].Name}  {Delta(roll.Value)} " +
                $"{GameText.Stat(roll.Stat)}{(roll.Stat == ItemStat.CriticalChance ? "%" : "")}", GothicUiRenderer.Blue));
        lines.Add(new(GameText.EquipComparison, GothicUiRenderer.Gold));
        AddComparison(GameText.Attack, (long)preview.AttackDamage - current.AttackDamage);
        AddComparison(GameText.Health, (long)preview.MaximumHealth - current.MaximumHealth);
        AddComparison(GameText.Critical, (long)preview.CriticalChance - current.CriticalChance, "%");
        lines.Add(new(equipped ? GameText.Equipped.Trim() : GameText.ClickToEquip, GothicUiRenderer.Muted));
        return new ItemTooltip(lines);

        void AddComparison(string label, long difference, string unit = "") =>
            lines.Add(new($"{label}  {Delta(difference)}{unit}",
                difference > 0 ? GothicUiRenderer.Green : difference < 0 ? GothicUiRenderer.Red : GothicUiRenderer.Muted));
    }

    public static ItemTooltip ForResource(ResourceStack resource, bool learned, GameContent content) =>
        new(new[]
        {
            new TooltipLine(resource.Kind == ResourceKind.Material
                ? content.Materials[resource.DefinitionId].Name : content.Recipes[resource.DefinitionId].Name,
                resource.Kind == ResourceKind.Material ? GothicUiRenderer.Text : GothicUiRenderer.Gold),
            new TooltipLine(resource.Kind == ResourceKind.Material ? GameText.MaterialLabel : GameText.RecipeLabel,
                GothicUiRenderer.Muted),
            new TooltipLine(string.Format(GameText.ResourceQuantity, resource.Quantity), GothicUiRenderer.Text)
        }.Concat(resource.Kind == ResourceKind.Recipe
            ? new[] { new TooltipLine(learned ? GameText.Known : GameText.Unknown, GothicUiRenderer.Blue) }
            : Array.Empty<TooltipLine>()));

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, SpriteFont font,
        NumericsVector pointer, UiLayout layout)
    {
        const float scale = .9f;
        var (lines, bounds, lineHeight) = Layout(font, pointer, layout);
        shapes.Rectangle(batch, bounds, GothicUiRenderer.Gold);
        shapes.Rectangle(batch, new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2),
            new Color(5, 8, 6, 250));
        for (var index = 0; index < lines.Length; index++)
            batch.DrawString(font, lines[index].Text, new Vector2(bounds.X + 13, bounds.Y + 12 + index * lineHeight),
                lines[index].Color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    internal (TooltipLine[] Lines, Rectangle Bounds, int LineHeight) Layout(
        SpriteFont font, NumericsVector pointer, UiLayout layout)
    {
        const float scale = .9f;
        var lines = Lines.SelectMany(line => Wrap(line.Text, text => font.MeasureString(text).X * scale, 264)
            .Select(text => new TooltipLine(text, line.Color))).ToArray();
        var lineHeight = (int)Math.Ceiling(font.LineSpacing * scale);
        var width = (int)Math.Ceiling(lines.Max(line => font.MeasureString(line.Text).X) * scale) + 26;
        var bounds = Place(pointer, Math.Max(170, width), lines.Length * lineHeight + 24, layout);
        return (lines, bounds, lineHeight);
    }

    public static Rectangle Place(NumericsVector pointer, int width, int height, UiLayout layout)
    {
        if (width <= 0 || height <= 0 || width > layout.Width - 16 || height > layout.Console.Top - 16)
            throw new ArgumentOutOfRangeException(nameof(width), "Tooltip does not fit the playfield.");
        var x = pointer.X + 18 + width > layout.Width - 8 ? (int)pointer.X - width - 12 : (int)pointer.X + 18;
        return new Rectangle(Math.Clamp(x, 8, layout.Width - width - 8),
            Math.Clamp((int)pointer.Y + 18, 8, layout.Console.Top - height - 8), width, height);
    }

    public static IReadOnlyList<string> Wrap(string text, Func<string, float> measure, float width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var lines = new List<string>();
        var current = "";
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lines.Add(current);
                current = "";
                continue;
            }
            if (current.Length > 0 && measure(current + character) > width)
            {
                lines.Add(current);
                current = "";
            }
            current += character;
            if (measure(current) > width)
                throw new ArgumentException("A tooltip character exceeds the available width.", nameof(width));
        }
        lines.Add(current);
        return lines.AsReadOnly();
    }

    internal static string Delta(long value) => value > 0 ? $"+{value}" : value.ToString();
}
