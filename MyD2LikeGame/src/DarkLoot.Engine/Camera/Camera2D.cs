using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector2;

namespace DarkLoot.Engine.Camera;

public sealed class Camera2D
{
    private Vector2 _center;
    private Vector2 _screenCenter;

    public void Follow(NumericsVector position, int viewportWidth, int viewportHeight)
    {
        _center = new Vector2(position.X, position.Y);
        _screenCenter = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
    }

    public NumericsVector ScreenToWorld(Vector2 position)
    {
        var world = position + _center - _screenCenter;
        return new NumericsVector(world.X, world.Y);
    }

    public Vector2 WorldToScreen(NumericsVector position) =>
        new Vector2(position.X, position.Y) - _center + _screenCenter;
}
