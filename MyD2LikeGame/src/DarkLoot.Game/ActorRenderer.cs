using System;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class ActorRenderer
{
    public void Draw(SpriteBatch batch, ShapeRenderer shapes, ActorVisualKind kind, Vector2 anchor, ActorPose pose,
        bool swingHasHit = false, bool swingWasCritical = false, PlayerAppearance appearance = default)
    {
        if (pose.Opacity <= 0 || pose.Scale.X <= 0 || pose.Scale.Y <= 0)
            return;
        var canvas = new LocalShapes(batch, shapes, anchor, pose);
        var stride = pose.LimbSwing * 14;
        switch (kind)
        {
            case ActorVisualKind.Player:
                foreach (var part in PlayerPaperDoll.Build(pose, appearance))
                {
                    if (part.Shape == PaperDollShape.Line)
                        shapes.Line(batch, anchor + part.Position, anchor + part.Position + part.Dimensions,
                            part.Color, part.Thickness);
                    else
                        shapes.Ellipse(batch, anchor + part.Position, part.Dimensions, part.Rotation, part.Color);
                }
                if (pose.AttackProgress > 0)
                {
                    var aimAngle = MathF.Atan2(pose.Facing.Y, pose.Facing.X);
                    var previous = new Vector2(MathF.Cos(aimAngle - MathF.PI / 4),
                        MathF.Sin(aimAngle - MathF.PI / 4)) * 48;
                    for (var segment = 1; segment <= 12; segment++)
                    {
                        var arcAngle = aimAngle - MathF.PI / 4 + segment * MathF.PI / 24;
                        var next = new Vector2(MathF.Cos(arcAngle), MathF.Sin(arcAngle)) * 48;
                        canvas.Line(previous, next, swingWasCritical ? Color.OrangeRed :
                            swingHasHit ? Color.Gold : Color.LightCyan, 5);
                        previous = next;
                    }
                }
                break;
            case ActorVisualKind.Slime:
                canvas.Ellipse(Vector2.Zero, new(18, 18), Color.YellowGreen);
                canvas.Ellipse(new(-6, -4), new(3, 3), Color.Black);
                canvas.Ellipse(new(6, -4), new(3, 3), Color.Black);
                break;
            case ActorVisualKind.Wolf:
                var flip = pose.Facing.X < 0 ? -1 : 1;
                Vector2 Wolf(float x, float y) => new(x * flip, y);
                canvas.Line(Wolf(-13, 8), Wolf(-13 + stride, 20), Color.SandyBrown, 5);
                canvas.Line(Wolf(10, 8), Wolf(10 - stride, 20), Color.SandyBrown, 5);
                canvas.Rectangle(Wolf(-3, 1), new(40, 20), Color.SandyBrown);
                canvas.Ellipse(Wolf(17, -4), new(12, 12), Color.SandyBrown);
                canvas.Line(Wolf(9, -10), Wolf(7, -23), Color.SandyBrown, 7);
                canvas.Line(Wolf(21, -10), Wolf(23, -23), Color.SandyBrown, 7);
                canvas.Line(Wolf(-20, 0), Wolf(-32, -12 - stride * .3f), Color.SandyBrown, 5);
                canvas.Ellipse(Wolf(18, -7), new(2, 2), Color.Black);
                canvas.Ellipse(Wolf(26, -1), new(3, 2), Color.Black);
                break;
            case ActorVisualKind.Archer:
                canvas.Line(new(0, 12), new(-9 + stride, 23), Color.LightGray, 3);
                canvas.Line(new(0, 12), new(9 - stride, 23), Color.LightGray, 3);
                canvas.Line(Vector2.Zero, new(0, 17), Color.LightGray, 3);
                canvas.Ellipse(new(0, -9), new(10, 10), Color.LightGray);
                canvas.Ellipse(new(-4, -11), new(2, 3), Color.Black);
                canvas.Ellipse(new(4, -11), new(2, 3), Color.Black);
                Vector2 Bow(float x, float y) =>
                    new(x * pose.Facing.X - y * pose.Facing.Y, x * pose.Facing.Y + y * pose.Facing.X);
                var pull = pose.LimbSwing * 12;
                canvas.Line(new(0, 4), Bow(18, 0), Color.LightGray, 3);
                canvas.Line(new(0, 4), Bow(18 - pull, 2), Color.LightGray, 3);
                canvas.Line(Bow(18, -16), Bow(25, 0), Color.BurlyWood, 3);
                canvas.Line(Bow(25, 0), Bow(18, 16), Color.BurlyWood, 3);
                canvas.Line(Bow(18, -16), Bow(18 - pull, 0), Color.Wheat, 1);
                canvas.Line(Bow(18 - pull, 0), Bow(18, 16), Color.Wheat, 1);
                break;
        }
    }

    private readonly struct LocalShapes(SpriteBatch batch, ShapeRenderer shapes, Vector2 anchor, ActorPose pose)
    {
        private Vector2 Point(Vector2 local)
        {
            var scaled = local * new Vector2(pose.Scale.X, pose.Scale.Y);
            var sin = MathF.Sin(pose.Rotation);
            var cos = MathF.Cos(pose.Rotation);
            return anchor + new Vector2(pose.Offset.X, pose.Offset.Y) +
                new Vector2(scaled.X * cos - scaled.Y * sin, scaled.X * sin + scaled.Y * cos);
        }

        private Color Tint(Color color) => Color.Lerp(color, Color.White, pose.WhiteMix) * pose.Opacity;

        public void Line(Vector2 start, Vector2 end, Color color, float thickness) =>
            shapes.Line(batch, Point(start), Point(end), Tint(color),
                thickness * (pose.Scale.X + pose.Scale.Y) / 2);

        public void Ellipse(Vector2 center, Vector2 radii, Color color) =>
            shapes.Ellipse(batch, Point(center), radii * new Vector2(pose.Scale.X, pose.Scale.Y),
                pose.Rotation, Tint(color));

        public void Rectangle(Vector2 center, Vector2 size, Color color) =>
            shapes.RotatedRectangle(batch, Point(center), size * new Vector2(pose.Scale.X, pose.Scale.Y),
                pose.Rotation, Tint(color));
    }
}
