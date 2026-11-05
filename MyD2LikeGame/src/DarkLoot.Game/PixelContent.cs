using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace DarkLoot.Game;

internal sealed record SpriteTexture(Texture2D Color, Texture2D Flash);

internal sealed class PixelContent
{
    private readonly Dictionary<ActorVisualKind, SpriteTexture> _actors = new();
    private readonly SpriteTexture[] _weapons = new SpriteTexture[3];
    private readonly SpriteTexture[] _armor = new SpriteTexture[3];
    private readonly SpriteTexture _armedPlayer;
    internal SpriteAtlasLayout Layout { get; }
    internal Texture2D Terrain { get; }
    internal Texture2D Props { get; }
    internal Texture2D Portal { get; }
    internal Texture2D Torch { get; }
    internal Texture2D Glow { get; }
    internal Texture2D Blacksmith { get; }

    internal PixelContent(ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var metadata = Path.Combine(AppContext.BaseDirectory, content.RootDirectory, "Pixel", "atlas.json");
        try
        {
            Layout = SpriteAtlasLayout.Parse(File.ReadAllText(metadata));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new ContentLoadException($"Cannot load pixel atlas metadata '{metadata}': {error.Message}", error);
        }
        foreach (var (kind, name) in new[]
        {
            (ActorVisualKind.Player, "player"), (ActorVisualKind.Slime, "slime"),
            (ActorVisualKind.Wolf, "wolf"), (ActorVisualKind.Archer, "archer"),
            (ActorVisualKind.Warrior, "warrior"), (ActorVisualKind.Cultist, "cultist")
        })
            _actors.Add(kind, LoadSprite(content, $"Actors/{name}"));
        _armedPlayer = LoadSprite(content, "Actors/player-armed");
        for (var index = 0; index < 3; index++)
        {
            var name = new[] { "common", "magic", "rare" }[index];
            _weapons[index] = LoadSprite(content, $"Equipment/weapon-{name}");
            _armor[index] = LoadSprite(content, $"Equipment/armor-{name}");
        }
        Terrain = LoadTexture(content, "World/terrain", 256, 128);
        Props = LoadTexture(content, "World/props", 512, 128);
        Portal = LoadTexture(content, "World/portal", 240, 56);
        Torch = LoadTexture(content, "World/torch", 96, 32);
        Glow = LoadTexture(content, "World/glow", 128, 128);
        Blacksmith = LoadTexture(content, "NPC/blacksmith", 288, 48);
    }

    internal SpriteTexture Actor(ActorVisualKind kind, bool armed) =>
        kind == ActorVisualKind.Player && armed ? _armedPlayer : _actors[kind];

    internal SpriteTexture Weapon(ItemRarity rarity) => _weapons[RarityIndex(rarity)];
    internal SpriteTexture Armor(ItemRarity rarity) => _armor[RarityIndex(rarity)];

    private static int RarityIndex(ItemRarity rarity)
    {
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        return (int)rarity;
    }

    private SpriteTexture LoadSprite(ContentManager content, string name)
    {
        var color = LoadTexture(content, name, Layout.Width, Layout.Height);
        var flash = LoadTexture(content, $"{name}-flash", Layout.Width, Layout.Height);
        return new SpriteTexture(color, flash);
    }

    private static Texture2D LoadTexture(ContentManager content, string name, int width, int height)
    {
        var asset = $"Pixel/{name}";
        var texture = content.Load<Texture2D>(asset);
        if (texture.Width != width || texture.Height != height)
            throw new ContentLoadException(
                $"Pixel content '{asset}' is {texture.Width}x{texture.Height}; expected {width}x{height}.");
        return texture;
    }
}
