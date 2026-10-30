using DarkLoot.Game;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class GothicUiArtTests
{
    [Fact]
    public void LiquidPixels_AreCircularAndPremultiplied()
    {
        const int size = 64;
        var pixels = UiTextureFactory.CreateLiquidPixels(size);
        Assert.Equal(size * size, pixels.Length);
        Assert.Equal(255, pixels[(size / 2) * size + size / 2].A);
        Assert.Contains(pixels, pixel => pixel.A is > 0 and < 255);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = pixels[y * size + x];
                var dx = x + .5f - size / 2f;
                var dy = y + .5f - size / 2f;
                if (dx * dx + dy * dy >= size * size / 4f)
                    Assert.Equal(Color.Transparent, pixel);
                Assert.True(pixel.R <= pixel.A && pixel.G <= pixel.A && pixel.B <= pixel.A);
            }
        }
    }

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(0f, 0)]
    [InlineData(.25f, 16)]
    [InlineData(.5f, 32)]
    [InlineData(1f, 64)]
    [InlineData(2f, 64)]
    [InlineData(float.NaN, 0)]
    public void GlobeFill_CropsBothRectanglesFromTheBottom(float fraction, int expectedRows)
    {
        var bounds = new Rectangle(37, 51, 128, 128);
        var (source, destination) = UiTextureFactory.GlobeFill(bounds, 64, fraction);
        Assert.Equal(new Rectangle(0, 64 - expectedRows, 64, expectedRows), source);
        Assert.Equal(new Rectangle(37, 179 - expectedRows * 2, 128, expectedRows * 2), destination);
    }

    [Fact]
    public void HalfFullGlobe_PreservesTheMaskWithoutRescalingTheLiquid()
    {
        const int size = 64;
        var pixels = UiTextureFactory.CreateLiquidPixels(size);
        var (source, destination) = UiTextureFactory.GlobeFill(new Rectangle(0, 0, size, size), size, .5f);
        Assert.Equal(source, destination);
        var visiblePixels = 0;
        for (var y = source.Top; y < source.Bottom; y++)
        {
            for (var x = source.Left; x < source.Right; x++)
            {
                if (pixels[y * size + x].A > 0)
                    visiblePixels++;
            }
        }
        Assert.Equal(pixels.Count(pixel => pixel.A > 0) / 2, visiblePixels);
        Assert.Equal(Color.Transparent, pixels[(size - 1) * size]);
        Assert.True(pixels[(size - 1) * size + size / 2].A > 0);
    }
}
