using System;
using System.Collections.Generic;
using DarkLoot.Engine.Camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

internal sealed class CombatEffectRenderer : IDisposable
{
    private readonly Dictionary<CombatEffectKind, Texture2D[]> _frames = [];

    internal CombatEffectRenderer(GraphicsDevice device)
    {
        foreach (var kind in Enum.GetValues<CombatEffectKind>())
        {
            var frames = new Texture2D[CombatEffectArt.FrameCount];
            _frames.Add(kind, frames);
            for (var frame = 0; frame < frames.Length; frame++)
            {
                frames[frame] = new Texture2D(device, CombatEffectArt.Size, CombatEffectArt.Size);
                frames[frame].SetData(CombatEffectArt.BuildFrame(kind, frame));
            }
        }
    }

    internal void Draw(SpriteBatch batch, Camera2D camera, IReadOnlyList<CombatEffect> effects)
    {
        foreach (var effect in effects)
            Draw(batch, effect, camera.WorldToScreen(effect.Position) + new Vector2(0, -6));
    }

    internal void Draw(SpriteBatch batch, CombatEffect effect, Vector2 anchor)
    {
        if (effect.Age >= effect.Duration)
            return;
        var index = Math.Clamp((int)(effect.Age / effect.Duration * CombatEffectArt.FrameCount),
            0, CombatEffectArt.FrameCount - 1);
        batch.Draw(_frames[effect.Kind][index], new Vector2(MathF.Round(anchor.X), MathF.Round(anchor.Y)), null,
            Color.White, MathF.Atan2(effect.Direction.Y, effect.Direction.X),
            new Vector2(CombatEffectArt.Size / 2), 1, SpriteEffects.None, 0);
    }

    public void Dispose()
    {
        foreach (var frames in _frames.Values)
            foreach (var frame in frames)
                frame?.Dispose();
    }
}
