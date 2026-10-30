using DarkLoot.Engine.Camera;
using DarkLoot.Game;
using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Tests.Game;

public sealed class GothicRoomTests
{
    [Fact]
    public void RoomPixels_ContainTexturedStoneColdGlassAndWarmForgeWithoutTransparency()
    {
        var pixels = GothicRoomArt.CreateRoomPixels();
        var bounds = GothicRoomLayout.Bounds;
        Assert.Equal(bounds.Width * bounds.Height, pixels.Length);
        Assert.All(pixels, pixel => Assert.Equal(255, pixel.A));
        var floor = Region(new Rectangle(240, 100, 160, 120)).ToArray();
        Assert.True(floor.Distinct().Count() > 80, "Floor must have material detail, not a flat grid.");
        Assert.InRange(floor.Average(color => (color.R + color.G + color.B) / 3d), 20, 110);
        Assert.Contains(Region(new Rectangle(50, -100, 95, 85)),
            color => color.B > color.R * 1.4 && color.B > 50);
        Assert.Contains(Region(new Rectangle(12, 170, 64, 70)),
            color => color.R > color.G * 1.5 && color.R > 100);
        Assert.Equal(pixels, GothicRoomArt.CreateRoomPixels());

        IEnumerable<Color> Region(Rectangle region)
        {
            for (var y = region.Top; y < region.Bottom; y++)
                for (var x = region.Left; x < region.Right; x++)
                    yield return pixels[(y - bounds.Y) * bounds.Width + x - bounds.X];
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(128)]
    public void Glow_HasSoftPremultipliedFalloffAndTransparentCorners(int size)
    {
        var pixels = GothicRoomArt.CreateGlowPixels(size);
        Assert.Equal(Color.Transparent, pixels[0]);
        Assert.True(pixels[size / 2 * size + size / 2].A > 240);
        Assert.True(pixels[size / 2 * size + size / 4].A <
            pixels[size / 2 * size + size / 2].A);
        Assert.All(pixels, pixel =>
            Assert.True(pixel.R <= pixel.A && pixel.G <= pixel.A && pixel.B <= pixel.A));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(160, 270)]
    [InlineData(480, 270)]
    [InlineData(960, 540)]
    public void RoomCamera_KeepsRoomInViewAndAimingRoundTripsAtEdges(float x, float y)
    {
        var player = new NumericsVector(x, y);
        var camera = new Camera2D();
        var focus = GothicRoomLayout.CameraFocus(player, 1120, 600);
        camera.Follow(focus, 1120, 600);
        var topLeft = camera.WorldToScreen(new NumericsVector(
            GothicRoomLayout.Bounds.Left, GothicRoomLayout.Bounds.Top));
        var bottomRight = camera.WorldToScreen(new NumericsVector(
            GothicRoomLayout.Bounds.Right, GothicRoomLayout.Bounds.Bottom));
        Assert.Equal(0, topLeft.X);
        Assert.Equal(1120, bottomRight.X);
        Assert.True(topLeft.Y <= 0);
        Assert.True(bottomRight.Y >= 600);
        Assert.Equal(player, camera.ScreenToWorld(camera.WorldToScreen(player)));
        var target = new NumericsVector(380, 270);
        Assert.Equal(target, camera.ScreenToWorld(camera.WorldToScreen(target)));
        Assert.InRange(camera.WorldToScreen(player).Y, 20, 580);
    }
}
