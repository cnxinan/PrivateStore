using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkLoot.Engine.Animation;
using Microsoft.Xna.Framework;
using NVector = System.Numerics.Vector2;

namespace DarkLoot.Game;

internal sealed class SpriteAtlasLayout
{
    private sealed record Description(
        int Version, int FrameWidth, int FrameHeight, int Columns,
        string[] Directions, string[] Actions, int OriginX, int OriginY, int Scale);

    private readonly Description _description;
    internal Vector2 Origin => new(_description.OriginX, _description.OriginY);
    internal int Scale => _description.Scale;
    internal int Width => _description.FrameWidth * _description.Columns;
    internal int Height => _description.FrameHeight * 40;

    private SpriteAtlasLayout(Description description) => _description = description;

    internal static SpriteAtlasLayout Parse(string json)
    {
        var description = JsonSerializer.Deserialize<Description>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Pixel atlas metadata is empty.");
        if (description.Version != 1)
            throw new InvalidDataException("Pixel atlas version must be 1.");
        if (description.FrameWidth != 48)
            throw new InvalidDataException("Pixel atlas version 1 frameWidth must be 48.");
        if (description.FrameHeight != 48)
            throw new InvalidDataException("Pixel atlas version 1 frameHeight must be 48.");
        if (description.Columns != 6)
            throw new InvalidDataException("Pixel atlas version 1 columns must be 6.");
        if (description.Scale != 2)
            throw new InvalidDataException("Pixel atlas version 1 scale must be 2.");
        if (description.OriginX != 24 || description.OriginY != 24)
            throw new InvalidDataException("Pixel atlas version 1 origin must be (24, 24).");
        if (description.Directions is null ||
            !description.Directions.SequenceEqual(new[] { "E", "SE", "S", "SW", "W", "NW", "N", "NE" }))
            throw new InvalidDataException("Pixel atlas directions must be E, SE, S, SW, W, NW, N, NE.");
        if (description.Actions is null ||
            !description.Actions.SequenceEqual(Enum.GetNames<ActorAction>()))
            throw new InvalidDataException("Pixel atlas actions must match Idle, Move, Attack, Hurt, Dead.");
        return new SpriteAtlasLayout(description);
    }

    internal Rectangle Frame(ActorFrame frame, NVector facing)
    {
        if (!Enum.IsDefined(frame.Action) || !float.IsFinite(frame.Progress) || frame.Progress is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(frame));
        if (!float.IsFinite(facing.X) || !float.IsFinite(facing.Y) || facing == NVector.Zero)
            throw new ArgumentOutOfRangeException(nameof(facing));
        var angle = Math.Atan2(facing.Y, facing.X);
        var direction = ((int)Math.Floor(angle / (Math.PI / 4) + .5) + 8) % 8;
        var column = Math.Min((int)(frame.Progress * _description.Columns), _description.Columns - 1);
        return new Rectangle(column * _description.FrameWidth,
            ((int)frame.Action * 8 + direction) * _description.FrameHeight,
            _description.FrameWidth, _description.FrameHeight);
    }

    internal void ValidateTextureSize(int width, int height, string assetName)
    {
        if (width != Width || height != Height)
            throw new InvalidDataException(
                $"Pixel atlas '{assetName}' is {width}x{height}; metadata requires {Width}x{Height}.");
    }
}
