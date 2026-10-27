using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Engine.Input;
using Microsoft.Xna.Framework.Input;

namespace DarkLoot.Tests.Engine;

public sealed class InputAndFrameRateTests
{
    [Fact]
    public void Capture_HoldingLeftButtonKeepsAttackHeldButClicksOnlyOnce()
    {
        var input = new GameInput();
        var camera = new Camera2D();
        var held = new MouseState(100, 100, 0, ButtonState.Pressed,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        var first = input.Capture(camera, default, held);
        var second = input.Capture(camera, default, held);
        var released = input.Capture(camera, default, default);

        Assert.True(first.AttackPressed);
        Assert.True(first.AttackHeld);
        Assert.False(second.AttackPressed);
        Assert.True(second.AttackHeld);
        Assert.False(released.AttackHeld);
        Assert.False(released.AttackPressed);
    }

    [Fact]
    public void FrameRate_UsesActualDrawCountAndWallTimeRatherThanUpdateRate()
    {
        var counter = new FrameRateCounter();
        for (var frame = 1; frame <= 30; frame++)
        {
            counter.RecordDraw(TimeSpan.FromSeconds(frame / 60d));
        }

        Assert.Equal(60, counter.FramesPerSecond, 3);
        counter.RecordDraw(TimeSpan.FromSeconds(1.5));
        Assert.Equal(1, counter.FramesPerSecond, 3);
    }
}
