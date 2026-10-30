using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal static class GothicRoomLayout
{
    internal static Rectangle Floor => new(0, 0, 960, 540);
    internal static Rectangle Bounds => new(-80, -112, 1120, 704);
    internal static IReadOnlyList<NumericsVector> Braziers { get; } = Array.AsReadOnly(new[]
    {
        new NumericsVector(-26, 112), new NumericsVector(986, 112),
        new NumericsVector(-26, 386), new NumericsVector(986, 386),
        new NumericsVector(45, 214)
    });

    internal static NumericsVector CameraFocus(NumericsVector player, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var room = Bounds;
        // Bias toward the back wall; clamping prevents the old empty space beyond the arena.
        return new NumericsVector(
            width >= room.Width ? room.Center.X : Math.Clamp(player.X, room.Left + width / 2f, room.Right - width / 2f),
            height >= room.Height ? room.Center.Y :
                Math.Clamp(player.Y - 96, room.Top + height / 2f, room.Bottom - height / 2f));
    }
}
