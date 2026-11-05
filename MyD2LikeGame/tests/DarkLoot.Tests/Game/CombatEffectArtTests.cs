using DarkLoot.Game;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class CombatEffectArtTests
{
    [Theory]
    [InlineData(CombatEffectKind.Slash)]
    [InlineData(CombatEffectKind.Punch)]
    [InlineData(CombatEffectKind.Hit)]
    [InlineData(CombatEffectKind.Critical)]
    public void Frames_BurstThenDissipateWithCrispTwoPixelBlocks(CombatEffectKind kind)
    {
        var first = CombatEffectArt.BuildFrame(kind, 0);
        var last = CombatEffectArt.BuildFrame(kind, CombatEffectArt.FrameCount - 1);
        Assert.True(first.Count(color => color.A > 0) > 40);
        Assert.True(last.Count(color => color.A > 0) < first.Count(color => color.A > 0));
        Assert.False(first.SequenceEqual(last));
        for (var y = 0; y < CombatEffectArt.Size; y += 2)
            for (var x = 0; x < CombatEffectArt.Size; x += 2)
            {
                var index = y * CombatEffectArt.Size + x;
                Assert.Equal(first[index], first[index + 1]);
                Assert.Equal(first[index], first[index + CombatEffectArt.Size]);
            }
    }

    [Fact]
    public void AttackFrames_FaceForwardAndPunchHasShorterReachThanBlade()
    {
        float Reach(CombatEffectKind kind)
        {
            var frame = CombatEffectArt.BuildFrame(kind, 0);
            var right = frame.Where((color, index) => index % CombatEffectArt.Size > CombatEffectArt.Size / 2 &&
                color.A > 0).Count();
            var left = frame.Where((color, index) => index % CombatEffectArt.Size < CombatEffectArt.Size / 2 &&
                color.A > 0).Count();
            Assert.True(right > left * 3);
            return frame.Select((color, index) => (color, index)).Where(pixel => pixel.color.A > 0)
                .Max(pixel => pixel.index % CombatEffectArt.Size) - CombatEffectArt.Size / 2;
        }
        Assert.True(Reach(CombatEffectKind.Punch) < Reach(CombatEffectKind.Slash));
    }

    [Fact]
    public void CriticalFrame_HasStrongerBurstThanOrdinaryHit()
    {
        var hit = CombatEffectArt.BuildFrame(CombatEffectKind.Hit, 0);
        var critical = CombatEffectArt.BuildFrame(CombatEffectKind.Critical, 0);
        Assert.True(critical.Count(color => color.A > 0) > hit.Count(color => color.A > 0) * 1.4);
        Assert.Contains(Color.White, hit);
        Assert.Contains(Color.White, critical);
    }
}
