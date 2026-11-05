using System;
using DarkLoot.Engine.Animation;
using DarkLoot.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

public sealed class ActorRenderer
{
    private readonly PixelContent _content;

    internal ActorRenderer(PixelContent content) => _content = content;

    public void Draw(SpriteBatch batch, ShapeRenderer shapes, ActorVisualKind kind, Vector2 anchor, ActorPose pose,
        PlayerAppearance appearance = default, ActorFrame frame = default, float sizeScale = 1)
    {
        if (pose.Opacity <= 0)
            return;
        var source = _content.Layout.Frame(frame, pose.Facing);
        var position = PixelSceneRenderer.Round(anchor + new Vector2(pose.Offset.X, pose.Offset.Y));
        var scale = _content.Layout.Scale * sizeScale;
        var player = kind == ActorVisualKind.Player;
        var weaponBehind = player && appearance.Weapon.HasValue && pose.Facing.Y < -.25f;
        if (weaponBehind && appearance.Weapon is { } rearWeapon)
            Layer(_content.Weapon(rearWeapon));
        Layer(_content.Actor(kind, player && appearance.Weapon.HasValue));
        if (player && appearance.Armor.HasValue)
            Layer(_content.Armor(appearance.Armor.Value));
        if (player && appearance.Weapon.HasValue && !weaponBehind)
            Layer(_content.Weapon(appearance.Weapon.Value));

        void Layer(SpriteTexture texture)
        {
            batch.Draw(texture.Color, position, source, Color.White * pose.Opacity, 0,
                _content.Layout.Origin, scale, SpriteEffects.None, 0);
            if (pose.WhiteMix > 0)
                batch.Draw(texture.Flash, position, source, Color.White * (pose.WhiteMix * pose.Opacity), 0,
                    _content.Layout.Origin, scale, SpriteEffects.None, 0);
        }
    }

    internal void DrawBlacksmith(SpriteBatch batch, Vector2 anchor, double time)
    {
        batch.Draw(_content.Blacksmith, PixelSceneRenderer.Round(anchor),
            new Rectangle((int)(time * 6) % 6 * 48, 0, 48, 48), Color.White, 0,
            _content.Layout.Origin, _content.Layout.Scale, SpriteEffects.None, 0);
    }

    public void DrawProjectile(SpriteBatch batch, ShapeRenderer shapes, Vector2 tip, Vector2 direction,
        ActorVisualKind sourceKind)
    {
        if (sourceKind == ActorVisualKind.Cultist)
        {
            shapes.Line(batch, tip - direction * 24, tip, new Color(76, 35, 111), 5);
            shapes.Ellipse(batch, tip, new(9), 0, new Color(75, 38, 104));
            shapes.Ellipse(batch, tip, new(6), 0, Color.MediumPurple);
            shapes.Ellipse(batch, tip, new(2.5f), 0, new Color(227, 208, 255));
            return;
        }
        var side = new Vector2(-direction.Y, direction.X);
        shapes.Line(batch, tip - direction * 16, tip, Color.BurlyWood, 3);
        shapes.Line(batch, tip, tip - direction * 6 + side * 4, Color.White, 2);
        shapes.Line(batch, tip, tip - direction * 6 - side * 4, Color.White, 2);
    }
}
