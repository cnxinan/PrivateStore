using System;
using Microsoft.Xna.Framework;

namespace DarkLoot.Game;

// Native pixel adaptation of pixelfx sword-slash, hit-spark and crit-hit.
// See ThirdPartyNotices.txt for upstream credits and the MIT license.
internal static class CombatEffectArt
{
    internal const int Size = 128;
    internal const int FrameCount = 12;
    private static readonly Color[] Steel =
        [new(42, 68, 88), new(74, 116, 148), new(151, 207, 221), new(237, 249, 244)];
    private static readonly Color[] Fire =
        [new(112, 40, 24), new(239, 107, 38), new(255, 195, 69), new(255, 240, 163), Color.White];

    internal static Color[] BuildFrame(CombatEffectKind kind, int frame)
    {
        if (frame < 0 || frame >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(frame));
        var canvas = new Canvas();
        var t = frame / (float)(FrameCount - 1);
        switch (kind)
        {
            case CombatEffectKind.Slash:
            case CombatEffectKind.Punch:
                Slash(canvas, t, kind == CombatEffectKind.Punch);
                break;
            case CombatEffectKind.Hit:
            case CombatEffectKind.Critical:
                Impact(canvas, t, kind == CombatEffectKind.Critical);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return canvas.Pixels;
    }

    private static void Slash(Canvas canvas, float t, bool punch)
    {
        var radius = punch ? 24 : 45;
        var end = .7f + Math.Min(1, t * 4) * .65f;
        var start = -1.2f + t * 2.4f;
        var fade = t < .25f ? 1 : t < .6f ? .7f : .35f;
        if (t < .9f)
        {
            Arc(canvas, radius - 6, .72f, start - .2f, end - .25f, punch ? 3 : 5, Steel[1] * (fade * .5f));
            Arc(canvas, radius, .72f, start, end, punch ? 4 : 8, Steel[1] * fade);
            Arc(canvas, radius, .72f, start + .08f, end, punch ? 2 : 5, Steel[2] * fade);
            Arc(canvas, radius, .72f, start + .2f, end - .03f, 1.5f, Steel[3] * fade);
        }
        for (var i = 0; i < 4; i++)
        {
            if (t > .75f + i * .05f)
                continue;
            var angle = -.9f + i * .5f;
            var distance = radius + t * (6 + i * 2);
            canvas.Dot(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance * .72f,
                Steel[i % 2 + 1] * fade);
        }
    }

    private static void Impact(Canvas canvas, float t, bool critical)
    {
        // Deterministic analytic sparks: no combat RNG, numerical integration or frame-rate dependence.
        var rays = critical ? 12 : 5;
        var flash = Math.Max(0, 1 - t * 3);
        if (flash > 0)
        {
            for (var i = 0; i < rays; i++)
            {
                var angle = critical ? i * MathF.Tau / rays + .12f : i switch
                {
                    0 => 0,
                    1 => -MathF.PI / 2,
                    2 => MathF.PI,
                    3 => MathF.PI / 2,
                    _ => -MathF.PI / 4
                };
                var length = critical ? 24 + i % 3 * 5 : i == 0 ? 25 : 9 + i % 3 * 3;
                for (var d = 0f; d < length * flash; d += 1)
                {
                    var ramp = d / length;
                    var color = Fire[ramp < .25f ? 4 : ramp < .6f ? 3 : ramp < .8f ? 2 : 1];
                    canvas.Dot(MathF.Cos(angle) * d, MathF.Sin(angle) * d, color);
                    if (critical && ramp < .45f)
                        canvas.Dot(MathF.Cos(angle) * d - MathF.Sin(angle) * 2,
                            MathF.Sin(angle) * d + MathF.Cos(angle) * 2, Fire[2]);
                }
            }
            for (var y = -3; y <= 3; y++)
                for (var x = -3; x <= 3; x++)
                    if (Math.Abs(x) + Math.Abs(y) <= (critical ? 3 : 2))
                        canvas.Dot(x * 2, y * 2, Math.Abs(x) + Math.Abs(y) < 2 ? Color.White : Fire[3]);
        }
        if (critical && t is > 0 and < .7f)
        {
            var radius = 10 + 24 * MathF.Sqrt(t);
            for (var i = 0; i < 7; i++)
                Arc(canvas, radius, 1, i * MathF.Tau / 7 + t * .2f,
                    i * MathF.Tau / 7 + .48f * (1 - t), 2, Fire[t < .3f ? 2 : 1]);
        }
        for (var i = 0; i < (critical ? 16 : 7); i++)
        {
            var life = .5f + i % 4 * .12f;
            if (t >= life)
                continue;
            var angle = -.85f + i * .28f;
            var speed = 18 + i % 5 * 5;
            var distance = 5 + speed * t;
            var x = MathF.Cos(angle) * distance;
            var y = MathF.Sin(angle) * distance + 14 * t * t;
            var color = Fire[t < .2f ? 3 : t < .5f ? 2 : 1];
            canvas.Line(x - MathF.Cos(angle) * 3, y - MathF.Sin(angle) * 3, x, y, color);
        }
    }

    private static void Arc(Canvas canvas, float radius, float squash, float start, float end,
        float width, Color color)
    {
        for (var angle = start; angle <= end; angle += .025f)
        {
            var taper = MathF.Sin((angle - start) / (end - start) * MathF.PI);
            for (var inset = 0f; inset <= width * taper; inset += 1)
                canvas.Dot(MathF.Cos(angle) * (radius - inset),
                    MathF.Sin(angle) * (radius - inset) * squash, color);
        }
    }

    private sealed class Canvas
    {
        internal Color[] Pixels { get; } = new Color[Size * Size];

        internal void Dot(float x, float y, Color color)
        {
            var px = (int)MathF.Round(x / 2) * 2 + Size / 2;
            var py = (int)MathF.Round(y / 2) * 2 + Size / 2;
            if (px < 0 || py < 0 || px >= Size - 1 || py >= Size - 1)
                throw new InvalidOperationException("Attack effect exceeds its texture bounds.");
            Pixels[py * Size + px] = Pixels[py * Size + px + 1] =
                Pixels[(py + 1) * Size + px] = Pixels[(py + 1) * Size + px + 1] = color;
        }

        internal void Line(float x0, float y0, float x1, float y1, Color color)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))));
            for (var i = 0; i <= steps; i++)
                Dot(MathHelper.Lerp(x0, x1, i / (float)steps), MathHelper.Lerp(y0, y1, i / (float)steps), color);
        }
    }
}
