using System;
using System.Collections.Generic;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

internal enum PaperDollShape { Line, Ellipse }
internal enum PaperDollRegion { Body, Head, Arm, Leg, Boot, Armor, Weapon }

internal readonly record struct PaperDollPart(
    PaperDollShape Shape, PaperDollRegion Region, Vector2 Position, Vector2 Dimensions,
    float Rotation, Color Color, float Thickness);

internal static class PlayerPaperDoll
{
    internal static IReadOnlyList<PaperDollPart> Build(ActorPose pose, PlayerAppearance appearance)
    {
        var parts = new List<PaperDollPart>(64);
        if (pose.Opacity <= 0 || pose.Scale.X <= 0 || pose.Scale.Y <= 0)
            return parts;
        var outline = new Color(24, 22, 24);
        var leather = new Color(74, 47, 33);
        var cloth = new Color(55, 76, 110);
        var skin = new Color(190, 143, 105);
        var facing = new Vector2(pose.Facing.X, pose.Facing.Y);
        var back = facing.Y < -.25f;
        var profile = Math.Abs(facing.X);
        var halfWidth = 9 - profile * 2;
        var stride = pose.LimbSwing * 5;
        var swingAngle = pose.AttackProgress > 0 ? (pose.AttackProgress * 2 - 1) * .9f : 0;
        var angle = MathF.Atan2(facing.Y, facing.X) + swingAngle;
        var aim = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var side = new Vector2(-aim.Y, aim.X);
        var grip = aim * (appearance.Weapon.HasValue ? 17 : 14) + new Vector2(0, -2);
        var shoulder = new Vector2(facing.X < 0 ? -halfWidth : halfWidth, -6);
        var freeShoulder = new Vector2(-shoulder.X, -6);
        var freeHand = new Vector2(freeShoulder.X * 1.4f, 7 - stride * .5f);

        if (back)
            HeldArm();
        Arm(freeShoulder, freeHand);
        Leg(-5, stride);
        Leg(5, -stride);
        Ellipse(PaperDollRegion.Body, new(0, -1), new(halfWidth + 2, 13), outline);
        Ellipse(PaperDollRegion.Body, new(0, -2), new(halfWidth, 11), cloth);
        Line(PaperDollRegion.Body, new(-halfWidth + 1, 7), new(halfWidth - 1, 7), leather, 5);
        Ellipse(PaperDollRegion.Body, new(1, 7), new(2, 2), new(168, 138, 82));
        if (appearance.Armor is { } armor)
        {
            var (metal, trim) = Material(armor);
            Ellipse(PaperDollRegion.Armor, new(0, -3), new(halfWidth + 1, 10), outline);
            Ellipse(PaperDollRegion.Armor, new(0, -4), new(halfWidth - 1, 9), metal);
            Line(PaperDollRegion.Armor, new(-halfWidth + 2, 2), new(halfWidth - 2, 2), trim, 2);
            Line(PaperDollRegion.Armor, new(0, -10), new(0, 3), trim, 1);
            foreach (var x in new[] { -halfWidth, halfWidth })
            {
                Ellipse(PaperDollRegion.Armor, new(x, -8), new(5, 5), outline);
                Ellipse(PaperDollRegion.Armor, new(x, -9), new(4, 3), metal);
                Line(PaperDollRegion.Armor, new(x - 3, -9), new(x + 3, -9), trim, 1);
            }
            if (armor != ItemRarity.Common)
                Ellipse(PaperDollRegion.Armor, new(0, -6), new(2, 2), trim);
            if (armor == ItemRarity.Rare)
            {
                Line(PaperDollRegion.Armor, new(-4, -5), new(0, -1), trim, 2);
                Line(PaperDollRegion.Armor, new(0, -1), new(4, -5), trim, 2);
            }
        }
        Line(PaperDollRegion.Head, new(0, -11), new(0, -16), skin, 5);
        Ellipse(PaperDollRegion.Head, new(0, -17), new(7, 8), outline);
        Ellipse(PaperDollRegion.Head, new(facing.X * 1.5f, -16), new(5, 6), skin);
        Ellipse(PaperDollRegion.Head, new(-facing.X, back ? -16 : -21), new(6, back ? 7 : 3),
            new(59, 39, 29));
        if (!back)
        {
            var eyeX = profile > .65f ? MathF.Sign(facing.X) * 4 : -2;
            Ellipse(PaperDollRegion.Head, new(eyeX, -17), new(1, 1), outline);
            if (profile <= .65f)
                Ellipse(PaperDollRegion.Head, new(2, -17), new(1, 1), outline);
            Line(PaperDollRegion.Head, new(facing.X * 2 - 2, -12),
                new(facing.X * 2 + 2, -12), new(94, 60, 42), 1);
        }
        if (!back)
            HeldArm();
        return parts;

        void Leg(float x, float step)
        {
            var knee = new Vector2(x, 12);
            var ankle = new Vector2(x + step, 19);
            Line(PaperDollRegion.Leg, new(x * .8f, 5), knee, outline, 7);
            Line(PaperDollRegion.Leg, knee, ankle, outline, 6);
            Line(PaperDollRegion.Leg, new(x * .8f, 5), ankle, new(44, 49, 65), 4);
            Ellipse(PaperDollRegion.Boot, ankle + new Vector2(facing.X * 2, 1), new(4, 3), outline);
            Line(PaperDollRegion.Boot, ankle + new Vector2(0, -3), ankle + new Vector2(facing.X * 3, 1),
                leather, 4);
        }

        void Arm(Vector2 start, Vector2 hand)
        {
            Line(PaperDollRegion.Arm, start, hand, outline, 7);
            Line(PaperDollRegion.Arm, start, Vector2.Lerp(start, hand, .7f), cloth, 5);
            Ellipse(PaperDollRegion.Arm, hand, new(3, 3), skin);
        }

        void HeldArm()
        {
            Arm(shoulder, grip);
            if (appearance.Weapon is not { } weapon)
                return;
            var (metal, trim) = Material(weapon);
            var bladeBase = grip + aim * 5;
            var tip = grip + aim * 23;
            Line(PaperDollRegion.Weapon, grip - aim * 4, tip, outline, 6);
            Line(PaperDollRegion.Weapon, bladeBase, tip, metal, 4);
            Line(PaperDollRegion.Weapon, bladeBase - side, tip - side * .2f, new(216, 225, 228), 1);
            Line(PaperDollRegion.Weapon, grip - aim * 3, bladeBase, leather, 3);
            Line(PaperDollRegion.Weapon, bladeBase - side * 5, bladeBase + side * 5, trim, 3);
            Ellipse(PaperDollRegion.Weapon, grip - aim * 4, new(2, 2), trim);
            if (weapon != ItemRarity.Common)
                Ellipse(PaperDollRegion.Weapon, bladeBase, new(2, 2),
                    weapon == ItemRarity.Rare ? new(211, 93, 52) : new(82, 171, 235));
            if (weapon == ItemRarity.Rare)
            {
                Line(PaperDollRegion.Weapon, bladeBase + aim * 4 - side * 2,
                    bladeBase + aim * 8 + side * 2, trim, 1);
                Line(PaperDollRegion.Weapon, bladeBase + aim * 8 + side * 2,
                    bladeBase + aim * 12 - side * 2, trim, 1);
            }
            Ellipse(PaperDollRegion.Arm, grip, new(2, 2), skin);
        }

        Vector2 Point(Vector2 point)
        {
            point *= new Vector2(pose.Scale.X, pose.Scale.Y);
            var sin = MathF.Sin(pose.Rotation);
            var cos = MathF.Cos(pose.Rotation);
            return new Vector2(pose.Offset.X, pose.Offset.Y) +
                new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
        }

        Color Tint(Color color) => Color.Lerp(color, Color.White, pose.WhiteMix) * pose.Opacity;

        void Line(PaperDollRegion region, Vector2 start, Vector2 end, Color color, float thickness) =>
            parts.Add(new(PaperDollShape.Line, region, Point(start), Point(end) - Point(start), 0,
                Tint(color), thickness * (pose.Scale.X + pose.Scale.Y) / 2));

        void Ellipse(PaperDollRegion region, Vector2 center, Vector2 radii, Color color) =>
            parts.Add(new(PaperDollShape.Ellipse, region, Point(center),
                radii * new Vector2(pose.Scale.X, pose.Scale.Y), pose.Rotation, Tint(color), 0));
    }

    private static (Color Metal, Color Trim) Material(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => (new(118, 128, 139), new(177, 166, 139)),
        ItemRarity.Magic => (new(66, 104, 158), new(143, 189, 226)),
        ItemRarity.Rare => (new(121, 113, 100), new(213, 173, 79)),
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown equipment rarity.")
    };
}
