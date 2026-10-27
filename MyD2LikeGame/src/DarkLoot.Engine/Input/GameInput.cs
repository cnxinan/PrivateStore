using DarkLoot.Engine.Camera;
using Microsoft.Xna.Framework.Input;
using NumericsVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

namespace DarkLoot.Engine.Input;

public sealed record GameInputSnapshot(
    NumericsVector MoveDirection,
    NumericsVector AimWorldPosition,
    NumericsVector PointerScreenPosition,
    bool AttackPressed,
    bool PickupPressed,
    bool InventoryTogglePressed,
    bool RestartPressed,
    bool ExitPressed,
    int ScrollDelta = 0);

public sealed class GameInput
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public GameInputSnapshot Capture(Camera2D camera)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var movement = new NumericsVector(
            (keyboard.IsKeyDown(Keys.D) ? 1 : 0) - (keyboard.IsKeyDown(Keys.A) ? 1 : 0),
            (keyboard.IsKeyDown(Keys.S) ? 1 : 0) - (keyboard.IsKeyDown(Keys.W) ? 1 : 0));
        if (movement != NumericsVector.Zero)
        {
            movement = NumericsVector.Normalize(movement);
        }

        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
        var snapshot = new GameInputSnapshot(
            movement,
            camera.ScreenToWorld(new XnaVector(mouse.X, mouse.Y)),
            new NumericsVector(mouse.X, mouse.Y),
            mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released,
            Pressed(Keys.E),
            Pressed(Keys.I),
            Pressed(Keys.R),
            keyboard.IsKeyDown(Keys.Escape),
            mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        return snapshot;
    }
}
